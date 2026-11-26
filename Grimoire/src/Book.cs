using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kiln;

namespace Grimoire;

/// <summary>
/// One book of the grimoire (spells, arrows): the player's stacks of one kind of ammunition kept in Kiln's player storage
/// (keys "grimoire:spells" and "grimoire:arrows", 32 slots, that kind only, kept at death), shown and equipped in the player's own order.
/// It has two halves (1.0.2):
/// - The server half runs where the world runs (a dedicated server, or the game hosting it) and does every step for every player: the
///   storage, the character data and the inventory are there. It sends each player a view of the book after each change, and toasts.
/// - The player's half draws the screen and the HUD list from the last view, and asks for steps (pick, add, take out, move). On a game
///   joined to a server it sends them over Kiln's network (GrimoireNet); in a world it hosts it calls the server half directly.
///   1.0.0 and 1.0.1 had only the host's half: on a dedicated server nothing ran for the players, and their screen stayed empty.
/// The steps:
/// - The equipped stack lives in a backpack cell, where the game expects it, and every step is a plain move (no item is created or
///   destroyed). 0.1.0 to 0.1.2 kept it in an Equipment page cell (Kiln's GiveEquip): the game did not keep a spell there across a
///   reload, and with any spell in the backpack it equipped that one over it. Secure brings back what those versions left there.
/// - The order is the player's (CharacterData.Carried, key "order-spells"): a swap puts the old spell into the picked storage slot, so
///   the storage's own slot order changes on every switch and the list follows the order instead.
/// - The HUD's list is replaced (Ui.AmmoList, Replace): the book in its order, then the game's other rows. A pick of a stored spell
///   equips it.
/// - Running out: the next entry in the order is equipped (more of the same spell first).
/// - The backpack holds one grimoire stack at most: the equipped one. A grimoire spell left loose there (the game switched away from it,
///   or emptied the slot during a fast switch) takes the slot back when nothing is equipped, and is otherwise filed back into the
///   storage at the end of every switch and one second after the last change of the slot (Settle), from the action bar too. The
///   player's own backpack spells stay.
/// - Steps run one at a time, in the order they came; a pick replaces a pick of the same player still waiting, so only the player's
///   latest choice is equipped.
/// Every step is logged with the book's name ("spells:", "arrows:").
/// </summary>
internal sealed class Book : IDisposable
{
    private const float RowHeight = 34f;
    private const string SlotDragKind = "grimoire-entry";
    private const string BagDragKind = "grimoire-bag";
    private const long RefreshMilliseconds = 1000;
    private static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(10);

    public sealed class OrderData
    {
        public List<uint> Items { get; set; } = new List<uint>();
    }

    // What the screen and the HUD list draw, as the server last saw it (they are drawn on other threads). Names are looked up where
    // it is drawn, in that game's language.
    private sealed record Entry(uint Item, int Stored, bool Equipped, int EquippedCount)
    {
        public int Count => Stored + (Equipped ? EquippedCount : 0);
    }
    private sealed record BagEntry(uint Item, int Count, bool Equipped);
    private sealed record View(Entry[] Entries, BagEntry[] Bag, uint[] Shown, IReadOnlyDictionary<uint, int> Stored, uint Equipped, int EquippedCount,
        int Limit, bool Enabled, string? NotReady, uint OwnEquipped = 0)
    {
        public static readonly View Empty = new View(Array.Empty<Entry>(), Array.Empty<BagEntry>(), Array.Empty<uint>(), new Dictionary<uint, int>(), 0, 0,
            GrimoireRules.DefaultSlots, false, "Not Read Yet");
    }

    // The stack in the kind's equipment slot, and where it lies.
    private sealed record EquippedStack(InventorySlot Stack, bool InStorage)
    {
        public bool InBackpack => !InStorage && Stack.Category == InventoryCategory.Generic;
    }

    // One storage slot that holds something.
    private sealed record StoredStack(int Index, InventorySlot Stack);

    // A step waiting its turn, for one player. A waiting pick is updated in place by a later pick of the same player.
    private sealed class Step
    {
        public Step(EntityId player, string what, Func<EntityId, Task> action, bool quiet)
        {
            Player = player;
            What = what;
            Action = action;
            Quiet = quiet;
        }

        public EntityId Player { get; }
        public string What { get; set; }
        public Func<EntityId, Task> Action { get; set; }
        public bool Quiet { get; }
    }

    // The server half's state of one player.
    private sealed class PlayerState
    {
        public View View = View.Empty;
        public Step? WaitingPick;
        // The item the slot held before it last emptied: Settle equips it again when it is still loose in the backpack.
        public uint LastEmptied;
        // Bumped at every change of the slot; the settling waits until it stays the same for SettleDelay.
        public int SlotChanges;
        public int RefreshQueued;
    }

    private readonly Mod _mod;
    private readonly BookKind _kind;
    private readonly byte _index;
    private readonly GrimoireNet _net;
    private readonly Func<int> _limit;
    private readonly Func<bool> _enabled;
    private readonly Func<bool> _hideOthers;
    private readonly Func<EntityId, (bool Enabled, int Limit)> _prefs;
    private PlayerStorage? _storage;
    private AmmoListProvider? _rows;
    private IDisposable? _clicks;
    // The player's half: the view the screen and the HUD list draw.
    private volatile View _view = View.Empty;
    private volatile bool _busy;
    private readonly object _stepsGate = new object();
    private readonly Queue<Step> _steps = new Queue<Step>();
    private readonly Dictionary<EntityId, PlayerState> _players = new Dictionary<EntityId, PlayerState>();
    private bool _running;
    private long _refreshedAt;
    private ulong _loggedRevision = ulong.MaxValue;
    private static readonly ConcurrentDictionary<uint, string> Names = new ConcurrentDictionary<uint, string>();

    public Book(Mod mod, BookKind kind, byte index, GrimoireNet net, Func<int> limit, Func<bool> enabled, Func<bool> hideOthers,
        Func<EntityId, (bool Enabled, int Limit)> prefs)
    {
        _mod = mod;
        _kind = kind;
        _index = index;
        _net = net;
        _limit = limit;
        _enabled = enabled;
        _hideOthers = hideOthers;
        _prefs = prefs;
    }

    private Logger Log => _mod.Context.Log;
    private string Tag => _kind.Name.ToLowerInvariant();

    public string Name => _kind.Name;

    public BookKind Kind => _kind;

    // What the screen's default tab looks at, as the view last saw it (any thread).
    public bool HasEntries => _view.Entries.Length > 0;
    public bool HasBackpackStacks => _view.Bag.Length > 0;

    // Asks for a new view now (any thread).
    public void RefreshNow() => RequestRefresh(force: true);

    public void Start()
    {
        if (PlayerStorage.Available)
        {
            try
            {
                _storage = Items.RegisterPlayerStorage(_kind.StorageKey, GrimoireRules.MaxSlots, StorageFilter.Ammunition(_kind.AmmoKind), StorageDeath.Keep);
                Log.Info($"{Tag}: storage registered: {_storage}");
            }
            catch (Exception error) when (error is KilnUnavailableException or InvalidOperationException or ArgumentException)
            {
                Log.Warn($"{Tag}: storage not registered: {error.Message}");
            }
        }
        else
        {
            Log.Warn($"{Tag}: no player storage here: {Capabilities.Why(PlayerStorage.Capability)}");
        }
        if (!Ui.Available || !Client.Available || !Client.IsClient)
        {
            Log.Info($"{Tag}: no game UI here (a dedicated server): the steps of joined players with Grimoire run here");
            return;
        }
        if (Ui.AmmoList.Available)
        {
            _rows = Ui.AmmoList.Provide(_kind.ListKind, BuildRows, new AmmoListOptions { Mode = AmmoListMode.Replace });
            Ui.AmmoList.Picked += OnPicked;
        }
        else
        {
            Log.Info($"{Tag}: the HUD list can't follow the grimoire on this build: {Capabilities.Why(Ui.AmmoList.ListCapability)}");
        }
        try
        {
            _clicks = Ui.Inventory.OnClick(new SlotClickOptions { Button = SlotButton.Left, Modifiers = SlotModifiers.Ctrl, Take = false }, OnSlotClicked);
        }
        catch (Exception error) when (error is KilnUnavailableException or InvalidOperationException or ArgumentException)
        {
            Log.Info($"{Tag}: Ctrl+click can't add to the grimoire on this build: {error.Message}");
        }
        Ui.Inventory.ScreenChanged += OnScreenChanged;
        Log.Info($"{Tag}: ready. HUD list {(_rows != null ? "follows the grimoire" : "is the game's")}");
        RequestRefresh(force: true);
    }

    public void Dispose()
    {
        if (Ui.Available)
        {
            Ui.Inventory.ScreenChanged -= OnScreenChanged;
        }
        if (_rows != null)
        {
            Ui.AmmoList.Picked -= OnPicked;
            _rows.Dispose();
            _rows = null;
        }
        _clicks?.Dispose();
        _clicks = null;
    }

    // The settings changed (on or off, the slot count): the list and the screen follow.
    public void SettingsChanged()
    {
        View view = _view;
        _view = view with { Enabled = _enabled(), Limit = GrimoireRules.ClampSlots(_limit()) };
        _rows?.Refresh();
        RequestRefresh(force: true);
    }

    // The connection to a server came up or went away: the view is read again (or emptied).
    public void ConnectionChanged()
    {
        _view = View.Empty with { Enabled = _enabled(), Limit = GrimoireRules.ClampSlots(_limit()) };
        _rows?.Refresh();
        RequestRefresh(force: true);
    }

    // Server half: events ---------------------------------------------------------------------------------------------------

    // The player's equipment events, from the mod's [Subscribe] handler: the slot may have run out or the game may have switched.
    public void OnEquipmentChanged(PlayerEquipmentChangedEvent e)
    {
        EntityId player = e.Player.Entity;
        if (e.Slot != _kind.PlayerSlot || !GrimoireNet.WorldHere || !GrimoireNet.Serves(player))
        {
            return;
        }
        Log.Info($"{Tag}: slot of {e.Player.Name}: {e.Before} -> {e.Now}{(_busy ? " (during a step)" : "")}");
        uint before = e.Before.Id;
        if (e.Now.IsNone && before != 0)
        {
            uint[] shownBefore;
            lock (_stepsGate)
            {
                PlayerState state = StateOfLocked(player);
                state.LastEmptied = before;
                shownBefore = state.View.Shown;
            }
            Post(player, "ran out", p => RanOut(p, before, shownBefore), quiet: true);
        }
        else
        {
            QueueRefresh(player);
        }
        ScheduleSettle(player);
    }

    // Kiln's storage events, from the mod's [Subscribe] handlers.
    public void OnStorageEvent(string key, EntityId player, string what)
    {
        if (_storage != null && key == _storage.FullKey && GrimoireNet.WorldHere && GrimoireNet.Serves(player))
        {
            Log.Info($"{Tag}: storage {what}");
            QueueRefresh(player);
            ScheduleSettle(player); // brings back what an older version left in the Equipment page
        }
    }

    // A player left: their state goes (their steps still waiting find no player and do nothing).
    public void OnPlayerLeft(EntityId player)
    {
        lock (_stepsGate)
        {
            _players.Remove(player);
        }
    }

    // A command of a player, from their game (the channel, tick thread) or from the host's own screen and HUD (any thread).
    public void OnCommand(BookOp op, uint item, int index, EntityId player)
    {
        if (player.IsNone || !GrimoireNet.WorldHere)
        {
            return;
        }
        index = Math.Clamp(index, 0, GrimoireRules.MaxSlots - 1);
        switch (op)
        {
            case BookOp.Refresh:
                QueueRefresh(player);
                break;
            case BookOp.Pick:
                PostPick(player, item, p => Equip(p, item, "a pick"));
                break;
            case BookOp.Add:
                Post(player, "add", p => Add(p, item));
                break;
            case BookOp.TakeOut:
                Post(player, "take out", p => TakeOut(p, item));
                break;
            case BookOp.AddAll:
                Post(player, "add all", AddAll);
                break;
            case BookOp.TakeAllOut:
                Post(player, "take all out", TakeAllOut);
                break;
            case BookOp.Move:
                Post(player, "move", p => Move(p, item, index));
                break;
            case BookOp.AddAt:
                Post(player, "add", async p =>
                {
                    await Add(p, item);
                    Refresh(p);
                    await Move(p, item, index);
                });
                break;
        }
    }

    // Player's half: input ------------------------------------------------------------------------------------------------

    // A step for the server half: called directly in a world this game hosts, else sent to the server.
    private void Request(BookOp op, uint item = 0, int index = 0)
    {
        if (GrimoireNet.WorldHere)
        {
            if (Client.Available && Client.IsHosting)
            {
                OnCommand(op, item, index, Client.LocalPlayer);
            }
            return;
        }
        if (!_net.ServerHasGrimoire)
        {
            if (op != BookOp.Refresh)
            {
                Hud.Toast("Grimoire: This Server Doesn't Run Grimoire", ToastKind.Info, 4);
            }
            return;
        }
        _net.SendCommand(new CommandMessage(_index, op, item, index));
    }

    // Asks for a new view at most once a second (force: now).
    private void RequestRefresh(bool force = false)
    {
        long now = Environment.TickCount64;
        long last = Interlocked.Read(ref _refreshedAt);
        if (!force && now - last <= RefreshMilliseconds)
        {
            return;
        }
        Interlocked.Exchange(ref _refreshedAt, now);
        if (!GrimoireNet.WorldHere && !_net.ServerHasGrimoire)
        {
            string why = Network.ServerHasKiln ? "This Server Doesn't Run Grimoire" : "The Server Needs Kiln And Grimoire";
            if (_view.NotReady != why)
            {
                _view = View.Empty with { Enabled = _enabled(), Limit = GrimoireRules.ClampSlots(_limit()), NotReady = why };
                _rows?.Refresh();
            }
            return;
        }
        Request(BookOp.Refresh);
    }

    // A view from the server (the channel, the game's main thread).
    public void OnView(ViewMessage message)
    {
        Entry[] entries = message.Entries.Select(e => new Entry(e.Item, e.Stored, e.Equipped, e.EquippedCount)).ToArray();
        var stored = new Dictionary<uint, int>();
        foreach (Entry entry in entries)
        {
            stored[entry.Item] = entry.Stored;
        }
        _view = new View(entries, message.Bag.Select(b => new BagEntry(b.Item, b.Count, b.Equipped)).ToArray(), entries.Select(e => e.Item).ToArray(),
            stored, message.Equipped, message.EquippedCount, GrimoireRules.ClampSlots(_limit()), _enabled(), message.NotReady, message.OwnEquipped);
        Interlocked.Exchange(ref _refreshedAt, Environment.TickCount64);
        _rows?.Refresh();
    }

    private void OnPicked(AmmoListPick pick)
    {
        if (pick.Kind != _kind.ListKind || !_view.Enabled)
        {
            return;
        }
        Log.Info($"{Tag}: HUD list pick {ItemName(pick.Item.Value)}");
        Request(BookOp.Pick, pick.Item.Value);
    }

    private void OnScreenChanged(InventoryScreen? before, InventoryScreen? now)
    {
        if (now != null)
        {
            RequestRefresh(force: true);
        }
    }

    // Ctrl+click on an item of this kind in the player's own inventory adds it. A joined game has no item catalog, so it can't tell
    // spells from arrows: it asks both books, and the server's book of the other kind says nothing.
    private void OnSlotClicked(SlotClick click)
    {
        if (!_view.Enabled || click.Item.Value == 0)
        {
            return;
        }
        if (Items.Get(click.Item) != null && !_kind.Holds(click.Item))
        {
            return;
        }
        Request(BookOp.Add, click.Item.Value);
    }

    // Server half: steps ------------------------------------------------------------------------------------------------------

    private PlayerState StateOfLocked(EntityId player)
    {
        if (!_players.TryGetValue(player, out PlayerState? state))
        {
            state = new PlayerState();
            _players[player] = state;
        }
        return state;
    }

    private PlayerState StateOf(EntityId player)
    {
        lock (_stepsGate)
        {
            return StateOfLocked(player);
        }
    }

    private View ViewOf(EntityId player) => StateOf(player).View;

    // The player's settings: the host's own for the host, else what the player's game sent (the defaults until it did).
    private (bool Enabled, int Limit) Prefs(EntityId player)
    {
        (bool enabled, int limit) = _prefs(player);
        return (enabled, GrimoireRules.ClampSlots(limit));
    }

    // A toast on the player's game: the host's own, or sent to a joined player's game.
    private void Toast(EntityId player, string text, ToastKind kind, double seconds)
    {
        if (GrimoireNet.IsLocal(player))
        {
            Hud.Toast(text, kind, seconds);
        }
        else
        {
            _net.SendToast(player, text, kind, seconds);
        }
    }

    // Clicks, picks and the screen arrive on the UI or render thread, commands of joined players on the tick thread; inventories and
    // character data are read and changed on the tick thread, one step at a time and in the order they came. 0.2.0 retried a step that
    // came during another every 250 ms, so with picks faster than a swap they ran late and out of order (kiln.log, 25 Nov 2026: a
    // Hollow Wretch pick ran after the Blizzard and Shock Totem picks that followed it).
    private void Post(EntityId player, string what, Func<EntityId, Task> action, bool quiet = false)
    {
        lock (_stepsGate)
        {
            _steps.Enqueue(new Step(player, what, action, quiet));
        }
        Pump();
    }

    // A pick of an entry (the HUD list, the screen): one of the same player still waiting is replaced, so only the latest choice is
    // equipped and a burst of picks (the game can raise the same pick several times within a few milliseconds) is one swap.
    private void PostPick(EntityId player, uint item, Func<EntityId, Task> action)
    {
        string what = $"pick {ItemName(item)}";
        lock (_stepsGate)
        {
            PlayerState state = StateOfLocked(player);
            if (state.WaitingPick != null)
            {
                state.WaitingPick.What = what;
                state.WaitingPick.Action = action;
                return;
            }
            state.WaitingPick = new Step(player, what, action, quiet: false);
            _steps.Enqueue(state.WaitingPick);
        }
        Pump();
    }

    private void Pump()
    {
        lock (_stepsGate)
        {
            if (_running || _steps.Count == 0)
            {
                return;
            }
            _running = true;
        }
        _mod.Context.Scheduler.Post(RunSteps);
    }

    // Runs the waiting steps one after the other on the tick thread (awaits come back to it).
    private async void RunSteps()
    {
        while (true)
        {
            Step step;
            lock (_stepsGate)
            {
                if (_steps.Count == 0)
                {
                    _running = false;
                    return;
                }
                step = _steps.Dequeue();
                if (_players.TryGetValue(step.Player, out PlayerState? state) && ReferenceEquals(step, state.WaitingPick))
                {
                    state.WaitingPick = null;
                }
            }
            try
            {
                await RunStep(step);
            }
            catch (Exception ex)
            {
                Log.Warn($"{Tag} ({step.What}) failed after the step: {ex}");
            }
        }
    }

    private async Task RunStep(Step step)
    {
        EntityId player = step.Player;
        if (!GrimoireNet.WorldHere || !GrimoireNet.Serves(player))
        {
            return;
        }
        _busy = true;
        try
        {
            Task work = step.Action(player);
            if (await Task.WhenAny(work, Task.Delay(StepTimeout)) != work)
            {
                Log.Warn($"{Tag} ({step.What}): still running after {StepTimeout.TotalSeconds:0} s; the next steps go on");
                return;
            }
            await work;
        }
        catch (Exception ex)
        {
            Log.Warn($"{Tag} ({step.What}) failed: {ex}");
            Toast(player, $"Grimoire: {step.What} failed ({ex.GetType().Name}), see kiln.log", ToastKind.Error, 6);
        }
        finally
        {
            _busy = false;
            Refresh(player);
        }
    }

    private void ScheduleSettle(EntityId player)
    {
        int seen;
        lock (_stepsGate)
        {
            seen = ++StateOfLocked(player).SlotChanges;
        }
        _ = Task.Delay(SettleDelay).ContinueWith(_ =>
        {
            bool settled;
            lock (_stepsGate)
            {
                settled = _players.TryGetValue(player, out PlayerState? state) && state.SlotChanges == seen;
            }
            if (settled)
            {
                Post(player, "settle", Settle, quiet: true);
            }
        }, TaskScheduler.Default);
    }

    private PlayerStorage? Ready(EntityId player, string what, bool quiet = false)
    {
        if (_storage != null && _storage.IsReady(player))
        {
            return _storage;
        }
        string why = _storage == null ? "player storage is not available" : $"the storage is not ready ({_storage.Status(player).State})";
        Log.Info($"{Tag} ({what}): {why}");
        if (!quiet)
        {
            Toast(player, $"Grimoire: {why}", ToastKind.Info, 4);
        }
        return null;
    }

    private OrderData Order(EntityId player) => CharacterData.Carried.Get<OrderData>(player, _kind.OrderKey);

    // Equips an item of the grimoire with plain moves only: no step creates or destroys an item. The equipped stack lives in a backpack
    // cell (the action bar counts), where the game expects it, and the backpack keeps one grimoire stack at most. 0.1.0 to 0.1.2 kept it
    // in an Equipment page cell through Kiln's GiveEquip: the game did not keep it there across a reload (Eternal Pillar Of Flames lost,
    // 24 Nov 2026), and with any spell in the backpack the game equipped that one over it.
    // - The slot holds a spell the grimoire doesn't keep: it is unequipped and stays in the backpack, then as for an empty slot.
    // - Empty: a grimoire stack left loose in the backpack takes the slot back first, where it lies (the picked one if it is loose). In
    //   0.2.0 a fast switch could find the slot emptied by the game with the last spell still in the backpack (kiln.log, 25 Nov 2026,
    //   11:33:05: Shock Wisp equipped at 11417:5, the slot empty 80 ms later), and the picked spell came out of the storage into a
    //   second backpack cell; every such switch added one more.
    // - Still empty: the stored stack moves into the backpack and is equipped there. No free backpack cell: nothing happens.
    // - The slot holds a grimoire stack: the picked one, if loose, is equipped where it lies; else a swap with the stored stack (Items.Equip
    //   from the storage slot): the picked stack takes the cell, the old one the storage slot.
    // Then whatever grimoire stack is still loose goes back into the storage.
    private async Task Equip(EntityId player, uint item, string why)
    {
        PlayerStorage? storage = Ready(player, why);
        if (storage == null || !Prefs(player).Enabled)
        {
            return;
        }
        await Secure(player, storage, why);
        string name = ItemName(new ItemId(item));
        EquippedStack? equipped = Equipped(player, storage);
        if (equipped != null && equipped.Stack.Item.Value == item)
        {
            await FileLoose(player, storage, why);
            return;
        }
        if (!Stored(player, storage).Any(s => s.Stack.Item.Value == item) && !Loose(player, storage, equipped).Any(s => s.Item.Value == item))
        {
            Log.Info($"{Tag} ({why}): {name} is not in the grimoire");
            return;
        }
        if (equipped != null && !equipped.InBackpack)
        {
            Log.Info($"{Tag} ({why}): the equipped {ItemName(equipped.Stack.Item)} is still outside the backpack ({Where(equipped)}); nothing is swapped");
            Toast(player, "Grimoire: Free A Backpack Slot To Switch", ToastKind.Info, 4);
            return;
        }
        if (equipped != null && !Kept(player, storage, equipped.Stack.Item.Value))
        {
            EquipResult off = await Items.Unequip(player, _kind.Slot);
            Log.Info($"{Tag} ({why}): {ItemName(equipped.Stack.Item)} x{equipped.Stack.Count}, not the grimoire's, unequipped where it lies -> {off}");
            equipped = Equipped(player, storage);
            if (!off.Ok || equipped != null)
            {
                Toast(player, $"Grimoire: {name} Not Equipped", ToastKind.Info, 3);
                return;
            }
        }
        if (equipped == null)
        {
            List<InventorySlot> loose = Loose(player, storage, null);
            InventorySlot? back = loose.FirstOrDefault(s => s.Item.Value == item) ?? loose.FirstOrDefault();
            if (back != null)
            {
                EquipResult again = await Items.Equip(player, back.Slot, _kind.Slot, EquipOptions.None, back.ItemEntity);
                Log.Info($"{Tag} ({why}): the slot was empty; {ItemName(back.Item)} x{back.Count}, loose in cell {back.Slot}, equipped there -> {again}");
                equipped = Equipped(player, storage);
            }
        }
        if (equipped == null)
        {
            await Retrieve(player, storage, item, why);
        }
        else if (equipped.Stack.Item.Value != item)
        {
            InventorySlot? loosePicked = Loose(player, storage, equipped).FirstOrDefault(s => s.Item.Value == item);
            StoredStack? stored = Stored(player, storage).FirstOrDefault(s => s.Stack.Item.Value == item);
            if (loosePicked != null)
            {
                EquipResult on = await Items.Equip(player, loosePicked.Slot, _kind.Slot, EquipOptions.None, loosePicked.ItemEntity);
                Log.Info($"{Tag} ({why}): {name} x{loosePicked.Count}, loose in cell {loosePicked.Slot}, equipped there -> {on}");
                if (!on.Ok)
                {
                    Toast(player, $"Grimoire: {name} Not Equipped ({on.Reason})", ToastKind.Error, 4);
                }
            }
            else if (stored != null)
            {
                EquipResult swapped = await Items.Equip(player, stored.Stack.Slot, _kind.Slot, EquipOptions.None, stored.Stack.ItemEntity);
                Log.Info($"{Tag} ({why}): {name} x{stored.Stack.Count} in slot {stored.Index} swapped with {ItemName(equipped.Stack.Item)} -> {swapped}; now in {Where(Equipped(player, storage))}");
                if (!swapped.Ok)
                {
                    Toast(player, $"Grimoire: {name} Not Equipped ({swapped.Reason})", ToastKind.Error, 4);
                }
            }
            else
            {
                Log.Info($"{Tag} ({why}): {name} is no longer in the grimoire");
            }
        }
        await FileLoose(player, storage, why);
    }

    // The slot is empty and no grimoire stack is loose: the stored stack moves into the backpack and is equipped there.
    private async Task Retrieve(EntityId player, PlayerStorage storage, uint item, string why)
    {
        StoredStack? stored = Stored(player, storage).FirstOrDefault(s => s.Stack.Item.Value == item);
        string name = ItemName(new ItemId(item));
        if (stored == null)
        {
            Log.Info($"{Tag} ({why}): {name} is no longer in the grimoire");
            return;
        }
        MoveResult moved = await storage.Retrieve(player, stored.Index);
        Log.Info($"{Tag} ({why}): {name} x{stored.Stack.Count} from slot {stored.Index} into the backpack -> {moved}");
        if (!moved.Succeeded)
        {
            Toast(player, $"Grimoire: Backpack Full, {name} Not Equipped", ToastKind.Info, 4);
            return;
        }
        InventorySlot? loose = Items.Read(player)?.Slots.FirstOrDefault(s => !s.IsEmpty && s.Category == InventoryCategory.Generic && s.Item.Value == item
            && !s.IsEquipped && (stored.Stack.ItemEntity.IsNone || s.ItemEntity == stored.Stack.ItemEntity));
        if (loose == null)
        {
            Log.Warn($"{Tag} ({why}): {name} is not found in the backpack after the move");
            return;
        }
        EquipResult on = await Items.Equip(player, loose.Slot, _kind.Slot, EquipOptions.None, loose.ItemEntity);
        Log.Info($"{Tag} ({why}): {name} x{loose.Count} equipped in backpack cell {loose.Slot} -> {on}");
        if (!on.Ok)
        {
            MoveResult back = await Store(player, storage, loose);
            Log.Info($"{Tag} ({why}): {name} back into the grimoire -> {back}");
            Toast(player, $"Grimoire: {name} Not Equipped ({on.Reason})", ToastKind.Error, 4);
        }
    }

    // The grimoire's stacks of this kind lying in the player's backpack or action bar (both are Generic pages), not equipped: what the
    // game's own switch or a fast switch left behind. The player's own stacks (items the grimoire doesn't keep) are not among them.
    private List<InventorySlot> Loose(EntityId player, PlayerStorage storage, EquippedStack? equipped)
    {
        var kept = new HashSet<uint>(Order(player).Items);
        foreach (StoredStack stored in Stored(player, storage))
        {
            kept.Add(stored.Stack.Item.Value);
        }
        return Items.Read(player)?.Slots
            .Where(s => !s.IsEmpty && s.Category == InventoryCategory.Generic && _kind.Holds(s.Item) && !s.IsEquipped
                        && !(equipped != null && !equipped.InStorage && equipped.Stack.Slot == s.Slot) && kept.Contains(s.Item.Value)).ToList()
            ?? new List<InventorySlot>();
    }

    // Files every loose grimoire stack (Loose) back into the storage.
    private async Task FileLoose(EntityId player, PlayerStorage storage, string why)
    {
        foreach (InventorySlot stack in Loose(player, storage, Equipped(player, storage)))
        {
            MoveResult moved = await Store(player, storage, stack);
            Log.Info($"{Tag} ({why}): {ItemName(stack.Item)} x{stack.Count} loose in cell {stack.Slot}, filed back -> {moved}");
        }
    }

    // Whether the grimoire keeps this item: it is in the player's order or stored. Anything else in the backpack is the player's own.
    private bool Kept(EntityId player, PlayerStorage storage, uint item) =>
        Order(player).Items.Contains(item) || Stored(player, storage).Any(s => s.Stack.Item.Value == item);

    // Brings back what 0.1.0 to 0.1.2 left in an Equipment page: an equipped stack there is unequipped into the backpack (the game's own
    // unequip, a move) and equipped again where it landed; a stack there that isn't equipped goes into the storage.
    private async Task Secure(EntityId player, PlayerStorage storage, string why)
    {
        EquippedStack? equipped = Equipped(player, storage);
        if (equipped != null && !equipped.InStorage && equipped.Stack.Category == InventoryCategory.Equipment)
        {
            InventorySlot stack = equipped.Stack;
            string name = ItemName(stack.Item);
            EquipResult off = await Items.Unequip(player, _kind.Slot);
            Log.Info($"{Tag} ({why}): {name} x{stack.Count} equipped in Equipment page cell {stack.Slot}: unequipped into the backpack -> {off}");
            if (!off.Ok)
            {
                Toast(player, $"Grimoire: Free A Backpack Slot For {name}", ToastKind.Error, 6);
                return;
            }
            InventorySlot? landed = Items.Read(player)?.Slots.FirstOrDefault(s => !s.IsEmpty && s.Category == InventoryCategory.Generic && s.Item == stack.Item
                && (s.Slot == off.To || (!stack.ItemEntity.IsNone && s.ItemEntity == stack.ItemEntity)));
            if (landed != null)
            {
                EquipResult on = await Items.Equip(player, landed.Slot, _kind.Slot, EquipOptions.None, landed.ItemEntity);
                Log.Info($"{Tag} ({why}): {name} equipped again in backpack cell {landed.Slot} -> {on}");
            }
        }
        await FileStranded(player, storage, why);
    }

    // The slot emptied: a stack that is nowhere in the player's inventory ran out, and the next entry is equipped (more of it first).
    private async Task RanOut(EntityId player, uint item, uint[] shownBefore)
    {
        PlayerStorage? storage = Ready(player, "ran out", quiet: true);
        if (storage == null || !Prefs(player).Enabled || !shownBefore.Contains(item))
        {
            return;
        }
        if (Equipped(player, storage) != null)
        {
            return; // the game or a step equipped something already
        }
        if (Items.Read(player)?.Slots.Any(s => !s.IsEmpty && s.Item.Value == item) == true)
        {
            return; // taken off, not used up
        }
        var available = Stored(player, storage).Select(s => s.Stack.Item.Value).ToHashSet();
        uint? next = GrimoireRules.NextAfter(shownBefore, item, available);
        Log.Info($"{Tag}: {ItemName(new ItemId(item))} ran out; next {(next is { } n ? ItemName(new ItemId(n)) : "nothing")}");
        if (next is { } nextItem)
        {
            await Equip(player, nextItem, "ran out, next");
        }
    }

    // The player is done switching (one second without a change of the slot). A slot the game emptied while its spell is still loose in
    // the backpack gets it back where it lies (the one it held last first), then every other loose grimoire stack goes back into the
    // storage, from the action bar too: 0.2.0 asked for a plain move, which Kiln refuses there ("the slot is on player 1's action bar",
    // kiln.log, 25 Nov 2026), so spells left on the action bar stayed there and fell off the grimoire's list. The player's own backpack
    // spells stay.
    private async Task Settle(EntityId player)
    {
        PlayerStorage? storage = Ready(player, "settle", quiet: true);
        if (storage == null || !Prefs(player).Enabled)
        {
            return;
        }
        await Secure(player, storage, "settled");
        if (Equipped(player, storage) == null)
        {
            List<InventorySlot> loose = Loose(player, storage, null);
            uint last = StateOf(player).LastEmptied;
            InventorySlot? back = loose.FirstOrDefault(s => s.Item.Value == last) ?? loose.FirstOrDefault();
            if (back != null)
            {
                EquipResult again = await Items.Equip(player, back.Slot, _kind.Slot, EquipOptions.None, back.ItemEntity);
                Log.Info($"{Tag} (settled): the slot was empty; {ItemName(back.Item)} x{back.Count}, loose in cell {back.Slot}, equipped there -> {again}");
            }
        }
        await FileLoose(player, storage, "settled");
    }

    // Files every stack of this kind that lies in an Equipment page but is not equipped back into the storage.
    private async Task FileStranded(EntityId player, PlayerStorage storage, string why)
    {
        EquippedStack? equipped = Equipped(player, storage);
        List<InventorySlot> stranded = Items.Read(player)?.Slots
            .Where(s => !s.IsEmpty && s.Category == InventoryCategory.Equipment && _kind.Holds(s.Item) && !s.IsEquipped
                        && !(equipped != null && !equipped.InStorage && equipped.Stack.Slot == s.Slot)).ToList() ?? new List<InventorySlot>();
        foreach (InventorySlot stack in stranded)
        {
            MoveResult moved = await Store(player, storage, stack);
            Log.Info($"{Tag} ({why}): {ItemName(stack.Item)} x{stack.Count} left at {stack.Slot}, not equipped, filed back -> {moved}");
        }
    }

    // Adds an item: every backpack stack of it that isn't equipped into its storage slot or a free one. The equipped stack stays where it
    // is, in the backpack, and counts as added.
    private async Task Add(EntityId player, uint item)
    {
        if (!_kind.Holds(new ItemId(item)))
        {
            return; // a joined game can't tell spells from arrows (no item catalog there): it asks both books
        }
        PlayerStorage? storage = Ready(player, "add");
        if (storage == null)
        {
            return;
        }
        string name = ItemName(new ItemId(item));
        View view = ViewOf(player);
        int limit = GrimoireRules.ClampSlots(Prefs(player).Limit);
        if (!view.Shown.Contains(item) && view.Shown.Length >= limit)
        {
            Toast(player, $"Grimoire: {_kind.Name} Full ({limit} Slots)", ToastKind.Info, 3);
            return;
        }
        await Secure(player, storage, "add");
        EquippedStack? equipped = Equipped(player, storage);
        bool equippedIsIt = equipped != null && equipped.Stack.Item.Value == item;
        List<InventorySlot> stacks = Items.Read(player)?.Slots
            .Where(s => !s.IsEmpty && s.Item.Value == item && s.Category == InventoryCategory.Generic && !s.IsEquipped
                        && !(equipped != null && !equipped.InStorage && equipped.Stack.Slot == s.Slot)).ToList() ?? new List<InventorySlot>();
        int added = 0;
        foreach (InventorySlot stack in stacks)
        {
            MoveResult moved = await Store(player, storage, stack);
            Log.Info($"{Tag} (add): {name} x{stack.Count} at {stack.Slot} -> {moved}");
            if (moved.Status == ItemsStatus.NoRoom)
            {
                Toast(player, $"Grimoire: {_kind.Name} Storage Full", ToastKind.Info, 3);
                break;
            }
            added += Math.Max(0, moved.Done);
        }
        if (added > 0 || equippedIsIt)
        {
            GrimoireRules.Merge(Order(player).Items, new[] { item });
            Toast(player, $"Grimoire: {name} Added", ToastKind.Info, 2);
        }
        else if (stacks.Count == 0)
        {
            Toast(player, $"Grimoire: No {name} In The Backpack", ToastKind.Info, 2);
        }
    }

    private async Task AddAll(EntityId player)
    {
        PlayerStorage? storage = Ready(player, "add all");
        if (storage == null)
        {
            return;
        }
        List<uint> items = Items.Read(player)?.Slots.Where(s => !s.IsEmpty && s.Category == InventoryCategory.Generic && _kind.Holds(s.Item))
            .Select(s => s.Item.Value).Distinct().ToList() ?? new List<uint>();
        foreach (uint item in items)
        {
            await Add(player, item);
            Refresh(player);
        }
    }

    // Moves a stack into the storage slot of its item, else the first empty one (none free: Kiln's choice). A stack on the action bar
    // moves too (MoveOptions.ActionBar): Kiln leaves the action bar alone otherwise.
    private async Task<MoveResult> Store(EntityId player, PlayerStorage storage, InventorySlot stack)
    {
        var slotItems = new uint[storage.Slots];
        foreach (StoredStack stored in Stored(player, storage))
        {
            slotItems[stored.Index] = stored.Stack.Item.Value;
        }
        int index = GrimoireRules.FreeIndex(slotItems, stack.Item.Value);
        MoveOptions options = MoveOptions.ActionBar | (stack.Category == InventoryCategory.Equipment ? MoveOptions.Equipped : MoveOptions.None);
        SlotId? to = index < 0 ? null : storage.Slot(player, index);
        return await Items.Move(player, stack.Slot, storage.EntityOf(player), 0, options, to);
    }

    // Takes an entry out: its stored stacks into the backpack. The equipped stack is in the backpack already and stays equipped.
    private async Task TakeOut(EntityId player, uint item)
    {
        PlayerStorage? storage = Ready(player, "take out");
        if (storage == null)
        {
            return;
        }
        await Secure(player, storage, "take out");
        string name = ItemName(new ItemId(item));
        bool left = false;
        foreach (StoredStack stored in Stored(player, storage).Where(s => s.Stack.Item.Value == item).ToList())
        {
            MoveResult moved = await storage.Retrieve(player, stored.Index);
            Log.Info($"{Tag} (take out): {name} x{stored.Stack.Count} from slot {stored.Index} -> {moved}");
            left |= !moved.Succeeded || moved.Done < stored.Stack.Count;
        }
        if (left)
        {
            Toast(player, $"Grimoire: Backpack Full, Some {name} Stays", ToastKind.Info, 3);
            return;
        }
        Order(player).Items.Remove(item);
        Toast(player, $"Grimoire: {name} Taken Out", ToastKind.Info, 2);
    }

    private async Task TakeAllOut(EntityId player)
    {
        foreach (uint item in ViewOf(player).Shown.ToList())
        {
            await TakeOut(player, item);
            Refresh(player);
        }
    }

    private Task Move(EntityId player, uint item, int to)
    {
        View view = ViewOf(player);
        int from = Array.IndexOf(view.Shown, item);
        if (GrimoireRules.MoveEntry(Order(player).Items, view.Shown, from, to))
        {
            Log.Info($"{Tag}: {ItemName(new ItemId(item))} moved from entry {from + 1} to {Math.Min(to, view.Shown.Length - 1) + 1}");
        }
        return Task.CompletedTask;
    }

    // Server half: the view ---------------------------------------------------------------------------------------------------

    private void QueueRefresh(EntityId player)
    {
        lock (_stepsGate)
        {
            PlayerState state = StateOfLocked(player);
            if (state.RefreshQueued != 0)
            {
                return;
            }
            state.RefreshQueued = 1;
        }
        _mod.Context.Scheduler.Post(() =>
        {
            lock (_stepsGate)
            {
                if (_players.TryGetValue(player, out PlayerState? state))
                {
                    state.RefreshQueued = 0;
                }
            }
            Refresh(player);
        });
    }

    // Reads the player's book from the game (tick thread) and sends the view to their game.
    private void Refresh(EntityId player)
    {
        if (!GrimoireNet.WorldHere || !GrimoireNet.Serves(player))
        {
            return;
        }
        (bool enabled, int limit) = Prefs(player);
        View view;
        if (_storage == null || !_storage.IsReady(player))
        {
            string why = _storage == null ? "Player Storage Not Available" : $"Storage {_storage.Status(player).State}";
            view = View.Empty with { Enabled = enabled, Limit = limit, NotReady = why };
        }
        else
        {
            view = Read(player, _storage, enabled, limit);
        }
        lock (_stepsGate)
        {
            StateOfLocked(player).View = view;
        }
        Publish(player, view);
    }

    private View Read(EntityId player, PlayerStorage storage, bool enabled, int limit)
    {
        List<StoredStack> stored = Stored(player, storage);
        var counts = new Dictionary<uint, int>();
        foreach (StoredStack stack in stored)
        {
            counts[stack.Stack.Item.Value] = counts.GetValueOrDefault(stack.Stack.Item.Value) + stack.Stack.Count;
        }
        EquippedStack? equipped = Equipped(player, storage);
        // The equipped stack is the grimoire's when the grimoire keeps its item (in the order or stored); else it is the player's own.
        uint bookEquipped = equipped != null && Kept(player, storage, equipped.Stack.Item.Value) ? equipped.Stack.Item.Value : 0;
        var present = new List<uint>(stored.Select(s => s.Stack.Item.Value));
        if (bookEquipped != 0)
        {
            present.Add(bookEquipped);
        }
        // A grimoire stack loose in the backpack (until it is filed back) keeps its entry and its place in the list.
        List<InventorySlot> loose = Loose(player, storage, equipped);
        var looseCells = new HashSet<SlotId>(loose.Select(s => s.Slot));
        foreach (InventorySlot stack in loose)
        {
            counts[stack.Item.Value] = counts.GetValueOrDefault(stack.Item.Value) + stack.Count;
            present.Add(stack.Item.Value);
        }
        List<uint> order = Order(player).Items;
        GrimoireRules.Merge(order, present);
        List<uint> shown = GrimoireRules.Shown(order, present.Distinct().ToList());
        int equippedCount = equipped?.Stack.Count ?? 0;
        Entry[] entries = shown.Select(item => new Entry(item, counts.GetValueOrDefault(item), item == bookEquipped,
            item == bookEquipped ? equippedCount : 0)).ToArray();
        var bag = new List<BagEntry>();
        InventoryContents? inventory = Items.Read(player);
        if (inventory != null)
        {
            foreach (IGrouping<uint, InventorySlot> group in inventory.Slots
                         .Where(s => !s.IsEmpty && s.Category == InventoryCategory.Generic && _kind.Holds(s.Item) && !looseCells.Contains(s.Slot))
                         .GroupBy(s => s.Item.Value))
            {
                bool isEquipped = equipped != null && equipped.InBackpack && group.Any(s => s.Slot == equipped.Stack.Slot);
                // The grimoire's own equipped stack is shown on its entry, not here.
                List<InventorySlot> own = group.Where(s => !(isEquipped && bookEquipped == group.Key && s.Slot == equipped!.Stack.Slot)).ToList();
                if (own.Count > 0)
                {
                    bag.Add(new BagEntry(group.Key, own.Sum(s => s.Count), isEquipped && bookEquipped != group.Key));
                }
            }
        }
        // The player's own equipped stack (not the grimoire's) keeps its HUD row when the other rows are hidden.
        uint ownEquipped = equipped != null && !equipped.InStorage && bookEquipped == 0 ? equipped.Stack.Item.Value : 0;
        return new View(entries, bag.ToArray(), shown.ToArray(), counts, bookEquipped, equippedCount, limit, enabled, null, ownEquipped);
    }

    // The view goes to the player's game: the host's own half directly, a joined player's game over the channel (nothing when that
    // game has no Grimoire).
    private void Publish(EntityId player, View view)
    {
        if (GrimoireNet.IsLocal(player))
        {
            _view = view;
            Interlocked.Exchange(ref _refreshedAt, Environment.TickCount64);
            _rows?.Refresh();
            return;
        }
        _net.SendView(player, new ViewMessage(_index, view.NotReady, view.Equipped, view.EquippedCount, view.OwnEquipped,
            view.Entries.Select(e => new ViewMessage.EntryRow(e.Item, e.Stored, e.Equipped, e.EquippedCount)).ToArray(),
            view.Bag.Select(b => new ViewMessage.BagRow(b.Item, b.Count, b.Equipped)).ToArray()));
    }

    // Player's half: drawing -------------------------------------------------------------------------------------------------

    // The HUD list, on the UI thread: the book in its order, then the game's other rows. Off or not ready: the game's own rows.
    private IReadOnlyList<AmmoListRow> BuildRows(AmmoListContext ctx)
    {
        View view = _view;
        RequestRefresh();
        if (!view.Enabled || view.NotReady != null || view.Shown.Length == 0)
        {
            return ctx.GameRows;
        }
        List<(uint Item, int Count)> rows = GrimoireRules.HudRows(view.Shown, view.Stored, view.Equipped, view.EquippedCount,
            ctx.GameRows.Select(r => (r.Item.Value, r.Count)), _hideOthers(), view.OwnEquipped);
        if (ctx.Revision != _loggedRevision)
        {
            _loggedRevision = ctx.Revision;
            Log.Info($"{Tag}: HUD list (revision {ctx.Revision}): game rows [{string.Join(", ", ctx.GameRows.Select(r => $"{ItemName(r.Item.Value)} x{r.Count}"))}] -> shown [{string.Join(", ", rows.Select(r => $"{ItemName(r.Item)} x{r.Count}"))}]");
        }
        return rows.Select(r => new AmmoListRow(new ItemId(r.Item), r.Count)).ToList();
    }

    // The book's part of the grimoire screen, on the render thread: its entries (click to equip, drag to reorder, right-click to take
    // out), empty slots up to the limit, then the backpack's items of this kind (click or drag into the book to add).
    public void Draw(UiDraw ui)
    {
        View view = _view;
        RequestRefresh();
        ui.Separator(string.Create(CultureInfo.InvariantCulture, $"{_kind.Name}  {view.Entries.Length} / {view.Limit}"));
        if (view.NotReady != null)
        {
            ui.TextDisabled(view.NotReady);
            return;
        }
        if (!view.Enabled)
        {
            ui.TextDisabled("Mod Disabled");
        }
        float row = RowHeight * ui.Scale;
        float icon = row - 4f * ui.Scale;
        int shown = Math.Max(view.Limit, view.Entries.Length);
        for (int i = 0; i < shown; i++)
        {
            string number = (i + 1).ToString(CultureInfo.InvariantCulture);
            if (i < view.Entries.Length)
            {
                Entry entry = view.Entries[i];
                string name = ItemName(entry.Item);
                ui.ItemIconTrimmed(new ItemId(entry.Item), icon);
                ui.SameLine(0f, 6f * ui.Scale);
                string label = $"{number}. {name}  x{entry.Count}{(entry.Equipped ? "  (Equipped)" : "")}{(i >= view.Limit ? "  (Over Limit)" : "")}##entry{number}";
                if (ui.Selectable(label, entry.Equipped, 0f, row, UiSelectableFlags.None) && view.Enabled)
                {
                    Request(BookOp.Pick, entry.Item);
                }
                if (ui.ItemClicked(UiMouseButton.Right))
                {
                    Request(BookOp.TakeOut, entry.Item);
                }
                ui.Tooltip(entry.Equipped ? "Equipped. Drag to reorder, right-click to take out." : "Click to equip. Drag to reorder, right-click to take out.");
                ui.DragSource(SlotDragKind, entry.Item.ToString(CultureInfo.InvariantCulture), name);
            }
            else
            {
                ui.Space(icon, icon);
                ui.SameLine(0f, 6f * ui.Scale);
                ui.Selectable($"{number}. -##entry{number}", false, 0f, row, UiSelectableFlags.None);
            }
            DropOnEntry(ui, i);
        }
        ui.Separator("In Your Backpack");
        foreach (BagEntry bagEntry in view.Bag)
        {
            string name = ItemName(bagEntry.Item);
            ui.ItemIconTrimmed(new ItemId(bagEntry.Item), icon);
            ui.SameLine(0f, 6f * ui.Scale);
            if (ui.Selectable($"{name}  x{bagEntry.Count}{(bagEntry.Equipped ? "  (Equipped)" : "")}##bag{bagEntry.Item}", false, 0f, row, UiSelectableFlags.None))
            {
                Request(BookOp.Add, bagEntry.Item);
            }
            ui.Tooltip("Click or drag into the grimoire to add it.");
            ui.DragSource(BagDragKind, bagEntry.Item.ToString(CultureInfo.InvariantCulture), name);
            DropOnBag(ui);
        }
        ui.Selectable(view.Bag.Length == 0 ? $"No {_kind.Name} In The Backpack##bagdrop" : "Drop Here To Take Out##bagdrop", false, 0f, row, UiSelectableFlags.None);
        DropOnBag(ui);
        ui.TextDisabled($"Ctrl+Click A {_kind.Singular} In The Backpack To Add It");
        if (view.Bag.Length > 0 && ui.SmallButton($"Add All##{Tag}"))
        {
            Request(BookOp.AddAll);
        }
        if (view.Bag.Length > 0 && view.Entries.Length > 0)
        {
            ui.SameLine();
        }
        if (view.Entries.Length > 0 && ui.SmallButton($"Take All Out##{Tag}"))
        {
            Request(BookOp.TakeAllOut);
        }
    }

    private void DropOnEntry(UiDraw ui, int index)
    {
        if (ui.DropTarget(SlotDragKind) is { } dragged && uint.TryParse(dragged, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint item))
        {
            Request(BookOp.Move, item, index);
        }
        if (ui.DropTarget(BagDragKind) is { } added && uint.TryParse(added, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint addedItem))
        {
            Request(BookOp.AddAt, addedItem, index);
        }
    }

    private void DropOnBag(UiDraw ui)
    {
        if (ui.DropTarget(SlotDragKind) is { } dragged && uint.TryParse(dragged, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint item))
        {
            Request(BookOp.TakeOut, item);
        }
    }

    // Reading the game ---------------------------------------------------------------------------------------------------

    private static List<StoredStack> Stored(EntityId player, PlayerStorage storage)
    {
        var stored = new List<StoredStack>();
        InventoryContents? contents = storage.Read(player);
        if (contents == null)
        {
            return stored;
        }
        for (int i = 0; i < storage.Slots; i++)
        {
            SlotId at = storage.Slot(player, i);
            InventorySlot? stack = contents.Slots.FirstOrDefault(s => s.Slot == at);
            if (stack != null && !stack.IsEmpty)
            {
                stored.Add(new StoredStack(i, stack));
            }
        }
        return stored;
    }

    // The stack in the kind's equipment slot and where it lies, or null. CaptureSet names a plain stack's cell (StackEquipment.Source);
    // an item entity (an eternal spell) is a slot marked equipped. The storage is looked at too: the game equips a spell where it lies.
    private EquippedStack? Equipped(EntityId player, PlayerStorage storage)
    {
        InventoryContents? inventory = Items.Read(player);
        InventoryContents? contents = storage.IsReady(player) ? storage.Read(player) : null;
        EquipmentSetCaptureResult capture = Items.CaptureSet(player);
        if (capture.Ok && capture.Set != null)
        {
            foreach (EquipmentSetEntry entry in capture.Set.Entries.Where(e => e.Slot == _kind.Slot))
            {
                if (entry.Selection is StackEquipment stack)
                {
                    if (inventory?.Slots.FirstOrDefault(s => !s.IsEmpty && s.Slot == stack.Source && s.Item == stack.Item) is { } own)
                    {
                        return new EquippedStack(own, false);
                    }
                    if (contents?.Slots.FirstOrDefault(s => !s.IsEmpty && s.Slot == stack.Source && s.Item == stack.Item) is { } kept)
                    {
                        return new EquippedStack(kept, true);
                    }
                }
            }
        }
        if (inventory?.Slots.FirstOrDefault(s => !s.IsEmpty && s.IsEquipped && _kind.Holds(s.Item)) is { } marked)
        {
            return new EquippedStack(marked, false);
        }
        if (contents?.Slots.FirstOrDefault(s => !s.IsEmpty && s.IsEquipped) is { } markedKept)
        {
            return new EquippedStack(markedKept, true);
        }
        return null;
    }

    private static string Where(EquippedStack? equipped) =>
        equipped == null ? "no stack" : $"{equipped.Stack.Slot} ({(equipped.InStorage ? "storage" : equipped.Stack.Category.ToString())})";

    private static string ItemName(ItemId item) => ItemName(item.Value);

    // The item's name in this game's language (a joined game has no item catalog, but has the game's texts). Kept once found.
    private static string ItemName(uint item)
    {
        if (item == 0)
        {
            return "nothing";
        }
        if (Names.TryGetValue(item, out string? known))
        {
            return known;
        }
        string? name = Items.DisplayName(new ItemId(item)) ?? Items.Get(new ItemId(item))?.Name;
        if (name == null)
        {
            return $"item {item:x8}";
        }
        Names[item] = name;
        return name;
    }
}
