using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kiln;

namespace Grimoire;

/// <summary>
/// One book of the grimoire (spells today, arrows later): the player's stacks of one kind of ammunition kept in Kiln's player storage
/// (key "grimoire:spells", 32 slots, that kind only, kept at death), shown and equipped in the player's own order. Works in a world this
/// player hosts (single player or hosted co-op): the screen, the clicks and the HUD list act on the local player.
/// - The equipped stack lives in a backpack cell, where the game expects it, and every step is a plain move (no item is created or
///   destroyed). 0.1.0 to 0.1.2 kept it in an Equipment page cell (Kiln's GiveEquip): the game did not keep a spell there across a
///   reload, and with any spell in the backpack it equipped that one over it. Secure brings back what those versions left there.
/// - The order is the player's (CharacterData.Carried, key "order-spells"): a swap puts the old spell into the picked storage slot, so
///   the storage's own slot order changes on every switch and the list follows the order instead.
/// - The HUD's list is replaced (Ui.AmmoList, Replace): the book in its order, then the game's other rows. A pick of a stored spell
///   equips it.
/// - Running out: the next entry in the order is equipped (more of the same spell first).
/// - A grimoire spell the game switched away from by itself stays in the backpack; five seconds after the last change of the slot it
///   is filed back into the storage (Settle). The player's own backpack spells stay.
/// Every step is logged with the book's name ("spells:").
/// </summary>
internal sealed class Book : IDisposable
{
    private const float RowHeight = 34f;
    private const string SlotDragKind = "grimoire-entry";
    private const string BagDragKind = "grimoire-bag";
    private const long RefreshMilliseconds = 1000;
    private static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan BusyRetryDelay = TimeSpan.FromMilliseconds(250);
    private const int BusyRetries = 20;

    public sealed class OrderData
    {
        public List<uint> Items { get; set; } = new List<uint>();
    }

    // What the screen and the HUD list draw, as the tick thread last saw it (they are drawn on other threads).
    private sealed record Entry(uint Item, string Name, int Stored, bool Equipped, int EquippedCount)
    {
        public int Count => Stored + (Equipped ? EquippedCount : 0);
    }
    private sealed record BagEntry(uint Item, string Name, int Count, bool Equipped);
    private sealed record View(Entry[] Entries, BagEntry[] Bag, uint[] Shown, IReadOnlyDictionary<uint, int> Stored, uint Equipped, int EquippedCount,
        int Limit, bool Enabled, string? NotReady)
    {
        public static readonly View Empty = new View(Array.Empty<Entry>(), Array.Empty<BagEntry>(), Array.Empty<uint>(), new Dictionary<uint, int>(), 0, 0,
            GrimoireRules.DefaultSlots, false, "not read yet");
    }

    // The stack in the kind's equipment slot, and where it lies.
    private sealed record EquippedStack(InventorySlot Stack, bool InStorage)
    {
        public bool InBackpack => !InStorage && Stack.Category == InventoryCategory.Generic;
    }

    // One storage slot that holds something.
    private sealed record StoredStack(int Index, InventorySlot Stack);

    private readonly Mod _mod;
    private readonly BookKind _kind;
    private readonly Func<int> _limit;
    private readonly Func<bool> _enabled;
    private PlayerStorage? _storage;
    private AmmoListProvider? _rows;
    private IDisposable? _clicks;
    private volatile View _view = View.Empty;
    private bool _busy;
    private int _refreshQueued;
    private long _refreshedAt;
    // Bumped at every change of the slot; the settling waits until it stays the same for SettleDelay.
    private int _slotChanges;
    private ulong _loggedRevision = ulong.MaxValue;

    public Book(Mod mod, BookKind kind, Func<int> limit, Func<bool> enabled)
    {
        _mod = mod;
        _kind = kind;
        _limit = limit;
        _enabled = enabled;
    }

    private Logger Log => _mod.Context.Log;
    private string Tag => _kind.Name.ToLowerInvariant();

    public string Name => _kind.Name;

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
            Log.Info($"{Tag}: no game UI here (a dedicated server): the grimoire works in a world you host");
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
        catch (KilnUnavailableException error)
        {
            Log.Info($"{Tag}: Ctrl+click can't add to the grimoire on this build: {error.Message}");
        }
        Ui.Inventory.ScreenChanged += OnScreenChanged;
        Log.Info($"{Tag}: ready. HUD list {(_rows != null ? "follows the grimoire" : "is the game's")}");
        QueueRefresh();
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
    public void SettingsChanged() => QueueRefresh();

    // Events ------------------------------------------------------------------------------------------------------

    // The player's equipment events, from the mod's [Subscribe] handler: the slot may have run out or the game may have switched.
    public void OnEquipmentChanged(PlayerEquipmentChangedEvent e)
    {
        if (e.Slot != _kind.PlayerSlot)
        {
            return;
        }
        Log.Info($"{Tag}: slot of {e.Player.Name}: {e.Before} -> {e.Now}{(_busy ? " (during a step)" : "")}");
        uint before = e.Before.Id;
        if (e.Now.IsNone && before != 0)
        {
            uint[] shownBefore = _view.Shown;
            Post("ran out", p => RanOut(p, before, shownBefore), quiet: true);
        }
        else
        {
            QueueRefresh();
        }
        ScheduleSettle();
    }

    // Kiln's storage events, from the mod's [Subscribe] handlers.
    public void OnStorageEvent(string key, string what)
    {
        if (_storage != null && key == _storage.FullKey)
        {
            Log.Info($"{Tag}: storage {what}");
            QueueRefresh();
            ScheduleSettle(); // brings back what an older version left in the Equipment page
        }
    }

    private void OnPicked(AmmoListPick pick)
    {
        if (pick.Kind != _kind.ListKind || !_view.Enabled)
        {
            return;
        }
        Log.Info($"{Tag}: HUD list pick {ItemName(pick.Item)}");
        uint item = pick.Item.Value;
        Post("pick", p => Equip(p, item, "a HUD list pick"));
    }

    private void OnScreenChanged(InventoryScreen? before, InventoryScreen? now)
    {
        if (now != null)
        {
            QueueRefresh();
        }
    }

    // Ctrl+click on an item of this kind in the player's own inventory adds it.
    private void OnSlotClicked(SlotClick click)
    {
        if (!_view.Enabled || click.Item.Value == 0 || !_kind.Holds(click.Item) || !Client.IsHosting)
        {
            return;
        }
        uint item = click.Item.Value;
        Post("add", p => Add(p, item));
    }

    // Steps ------------------------------------------------------------------------------------------------------

    // Clicks, picks and the screen arrive on the UI or render thread; inventories and character data are read and changed on the tick
    // thread, one step at a time. A step that arrives while another runs waits for it (a few seconds at most).
    private void Post(string what, Func<EntityId, Task> action, bool quiet = false, int retries = BusyRetries)
    {
        _mod.Context.Scheduler.Post(async () =>
        {
            EntityId player = Client.Available ? Client.LocalPlayer : EntityId.None;
            if (player.IsNone || !Client.IsHosting)
            {
                if (!quiet)
                {
                    Hud.Toast("Grimoire: Works In A World You Host", ToastKind.Info, 3);
                }
                return;
            }
            if (_busy)
            {
                if (retries > 0)
                {
                    _ = Task.Delay(BusyRetryDelay).ContinueWith(_ => Post(what, action, quiet, retries - 1), TaskScheduler.Default);
                }
                else
                {
                    Log.Info($"{Tag} ({what}): dropped, the previous step is still running");
                }
                return;
            }
            _busy = true;
            try
            {
                await action(player);
            }
            catch (Exception ex)
            {
                Log.Warn($"{Tag} ({what}) failed: {ex}");
                Hud.Toast($"Grimoire: {what} failed ({ex.GetType().Name}), see kiln.log", ToastKind.Error, 6);
            }
            finally
            {
                _busy = false;
                Refresh(player);
            }
        });
    }

    private void ScheduleSettle()
    {
        int seen = Interlocked.Increment(ref _slotChanges);
        _ = Task.Delay(SettleDelay).ContinueWith(_ =>
        {
            if (Volatile.Read(ref _slotChanges) == seen)
            {
                Post("settle", Settle, quiet: true);
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
            Hud.Toast($"Grimoire: {why}", ToastKind.Info, 4);
        }
        return null;
    }

    private OrderData Order(EntityId player) => CharacterData.Carried.Get<OrderData>(player, _kind.OrderKey);

    // Equips a stored item with plain moves only: no step creates or destroys an item. The equipped stack lives in a backpack cell,
    // where the game expects it. 0.1.0 to 0.1.2 kept it in an Equipment page cell through Kiln's GiveEquip: the game did not keep it there
    // across a reload (Eternal Pillar Of Flames lost, 24 Nov 2026), and with any spell in the backpack the game equipped that one over it.
    // - The slot holds a grimoire spell (in the backpack): a swap with the stored stack (Items.Equip from the storage slot); the picked
    //   stack takes the backpack cell, the old one the storage slot.
    // - The slot holds a spell the grimoire doesn't keep: it is unequipped and stays in the backpack, then as for an empty slot.
    // - Empty: the stored stack moves into the backpack and is equipped there. No free backpack cell: nothing happens.
    private async Task Equip(EntityId player, uint item, string why)
    {
        PlayerStorage? storage = Ready(player, why);
        if (storage == null || !_enabled())
        {
            return;
        }
        await Secure(player, storage, why);
        EquippedStack? equipped = Equipped(player, storage);
        if (equipped != null && equipped.Stack.Item.Value == item)
        {
            return;
        }
        StoredStack? stored = Stored(player, storage).FirstOrDefault(s => s.Stack.Item.Value == item);
        if (stored == null)
        {
            Log.Info($"{Tag} ({why}): {ItemName(new ItemId(item))} is not in the grimoire");
            return;
        }
        string name = ItemName(stored.Stack.Item);
        if (equipped != null && !equipped.InBackpack)
        {
            Log.Info($"{Tag} ({why}): the equipped {ItemName(equipped.Stack.Item)} is still outside the backpack ({Where(equipped)}); nothing is swapped");
            Hud.Toast("Grimoire: Free A Backpack Slot To Switch", ToastKind.Info, 4);
            return;
        }
        if (equipped != null && Kept(player, storage, equipped.Stack.Item.Value))
        {
            EquipResult swapped = await Items.Equip(player, stored.Stack.Slot, _kind.Slot, EquipOptions.None, stored.Stack.ItemEntity);
            Log.Info($"{Tag} ({why}): {name} x{stored.Stack.Count} in slot {stored.Index} swapped with {ItemName(equipped.Stack.Item)} -> {swapped}; now in {Where(Equipped(player, storage))}");
            if (!swapped.Ok)
            {
                Hud.Toast($"Grimoire: {name} Not Equipped ({swapped.Reason})", ToastKind.Error, 4);
            }
            return;
        }
        if (equipped != null)
        {
            EquipResult off = await Items.Unequip(player, _kind.Slot);
            Log.Info($"{Tag} ({why}): {ItemName(equipped.Stack.Item)} x{equipped.Stack.Count}, not the grimoire's, unequipped where it lies -> {off}");
            if (!off.Ok || Equipped(player, storage) != null)
            {
                Hud.Toast($"Grimoire: {name} Not Equipped", ToastKind.Info, 3);
                return;
            }
        }
        MoveResult moved = await storage.Retrieve(player, stored.Index);
        Log.Info($"{Tag} ({why}): {name} x{stored.Stack.Count} from slot {stored.Index} into the backpack -> {moved}");
        if (!moved.Succeeded)
        {
            Hud.Toast($"Grimoire: Backpack Full, {name} Not Equipped", ToastKind.Info, 4);
            return;
        }
        InventorySlot? loose = Items.Read(player)?.Slots.FirstOrDefault(s => !s.IsEmpty && s.Category == InventoryCategory.Generic && s.Item.Value == item
            && (stored.Stack.ItemEntity.IsNone || s.ItemEntity == stored.Stack.ItemEntity));
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
            Hud.Toast($"Grimoire: {name} Not Equipped ({on.Reason})", ToastKind.Error, 4);
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
                Hud.Toast($"Grimoire: Free A Backpack Slot For {name}", ToastKind.Error, 6);
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
        if (storage == null || !_enabled() || !shownBefore.Contains(item))
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

    // The player is done switching: what was left outside the storage goes back. A grimoire spell the game switched away from stays in
    // the backpack, unequipped, and is filed back; the player's own backpack spells stay.
    private async Task Settle(EntityId player)
    {
        PlayerStorage? storage = Ready(player, "settle", quiet: true);
        if (storage == null || !_enabled())
        {
            return;
        }
        await Secure(player, storage, "settled");
        EquippedStack? equipped = Equipped(player, storage);
        List<InventorySlot> loose = Items.Read(player)?.Slots
            .Where(s => !s.IsEmpty && s.Category == InventoryCategory.Generic && _kind.Holds(s.Item) && !s.IsEquipped
                        && !(equipped != null && !equipped.InStorage && equipped.Stack.Slot == s.Slot) && Kept(player, storage, s.Item.Value)).ToList()
            ?? new List<InventorySlot>();
        foreach (InventorySlot stack in loose)
        {
            MoveResult moved = await Store(player, storage, stack);
            Log.Info($"{Tag} (settled): {ItemName(stack.Item)} x{stack.Count} in the backpack, not equipped, filed back -> {moved}");
        }
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
        PlayerStorage? storage = Ready(player, "add");
        if (storage == null)
        {
            return;
        }
        string name = ItemName(new ItemId(item));
        View view = _view;
        int limit = GrimoireRules.ClampSlots(_limit());
        if (!view.Shown.Contains(item) && view.Shown.Length >= limit)
        {
            Hud.Toast($"Grimoire: {_kind.Name} Full ({limit} Slots)", ToastKind.Info, 3);
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
                Hud.Toast($"Grimoire: {_kind.Name} Storage Full", ToastKind.Info, 3);
                break;
            }
            added += Math.Max(0, moved.Done);
        }
        if (added > 0 || equippedIsIt)
        {
            GrimoireRules.Merge(Order(player).Items, new[] { item });
            Hud.Toast($"Grimoire: {name} Added", ToastKind.Info, 2);
        }
        else if (stacks.Count == 0)
        {
            Hud.Toast($"Grimoire: No {name} In The Backpack", ToastKind.Info, 2);
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

    // Moves a stack into the storage slot of its item, else the first empty one.
    private async Task<MoveResult> Store(EntityId player, PlayerStorage storage, InventorySlot stack)
    {
        var slotItems = new uint[storage.Slots];
        foreach (StoredStack stored in Stored(player, storage))
        {
            slotItems[stored.Index] = stored.Stack.Item.Value;
        }
        int index = GrimoireRules.FreeIndex(slotItems, stack.Item.Value);
        if (index < 0)
        {
            return await storage.Store(player, stack.Slot);
        }
        MoveOptions options = stack.Category == InventoryCategory.Equipment ? MoveOptions.Equipped : MoveOptions.None;
        return await Items.Move(player, stack.Slot, storage.EntityOf(player), 0, options, storage.Slot(player, index));
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
            Hud.Toast($"Grimoire: Backpack Full, Some {name} Stays", ToastKind.Info, 3);
            return;
        }
        Order(player).Items.Remove(item);
        Hud.Toast($"Grimoire: {name} Taken Out", ToastKind.Info, 2);
    }

    private async Task TakeAllOut(EntityId player)
    {
        foreach (uint item in _view.Shown.ToList())
        {
            await TakeOut(player, item);
            Refresh(player);
        }
    }

    private Task Move(EntityId player, uint item, int to)
    {
        View view = _view;
        int from = Array.IndexOf(view.Shown, item);
        if (GrimoireRules.MoveEntry(Order(player).Items, view.Shown, from, to))
        {
            Log.Info($"{Tag}: {ItemName(new ItemId(item))} moved from entry {from + 1} to {Math.Min(to, view.Shown.Length - 1) + 1}");
        }
        return Task.CompletedTask;
    }

    // The view ------------------------------------------------------------------------------------------------------

    private void QueueRefresh()
    {
        if (Interlocked.Exchange(ref _refreshQueued, 1) == 1)
        {
            return;
        }
        _mod.Context.Scheduler.Post(() =>
        {
            Interlocked.Exchange(ref _refreshQueued, 0);
            EntityId player = Client.Available ? Client.LocalPlayer : EntityId.None;
            if (player.IsNone || !Client.IsHosting)
            {
                _view = View.Empty with { NotReady = "Works In A World You Host" };
                _rows?.Refresh();
                return;
            }
            Refresh(player);
        });
    }

    private void Refresh(EntityId player)
    {
        Interlocked.Exchange(ref _refreshedAt, Environment.TickCount64);
        bool enabled = _enabled();
        int limit = GrimoireRules.ClampSlots(_limit());
        if (_storage == null || !_storage.IsReady(player))
        {
            string why = _storage == null ? "Player Storage Not Available" : $"Storage {_storage.Status(player).State}";
            _view = View.Empty with { Enabled = enabled, Limit = limit, NotReady = why };
            _rows?.Refresh();
            return;
        }
        List<StoredStack> stored = Stored(player, _storage);
        var counts = new Dictionary<uint, int>();
        foreach (StoredStack stack in stored)
        {
            counts[stack.Stack.Item.Value] = counts.GetValueOrDefault(stack.Stack.Item.Value) + stack.Stack.Count;
        }
        EquippedStack? equipped = Equipped(player, _storage);
        // The equipped stack is the grimoire's when the grimoire keeps its item (in the order or stored); else it is the player's own.
        uint bookEquipped = equipped != null && Kept(player, _storage, equipped.Stack.Item.Value) ? equipped.Stack.Item.Value : 0;
        var present = new List<uint>(stored.Select(s => s.Stack.Item.Value));
        if (bookEquipped != 0)
        {
            present.Add(bookEquipped);
        }
        List<uint> order = Order(player).Items;
        GrimoireRules.Merge(order, present);
        List<uint> shown = GrimoireRules.Shown(order, present.Distinct().ToList());
        int equippedCount = equipped?.Stack.Count ?? 0;
        Entry[] entries = shown.Select(item => new Entry(item, ItemName(new ItemId(item)), counts.GetValueOrDefault(item), item == bookEquipped,
            item == bookEquipped ? equippedCount : 0)).ToArray();
        var bag = new List<BagEntry>();
        InventoryContents? inventory = Items.Read(player);
        if (inventory != null)
        {
            foreach (IGrouping<uint, InventorySlot> group in inventory.Slots
                         .Where(s => !s.IsEmpty && s.Category == InventoryCategory.Generic && _kind.Holds(s.Item)).GroupBy(s => s.Item.Value))
            {
                bool isEquipped = equipped != null && equipped.InBackpack && group.Any(s => s.Slot == equipped.Stack.Slot);
                // The grimoire's own equipped stack is shown on its entry, not here.
                List<InventorySlot> own = group.Where(s => !(isEquipped && bookEquipped == group.Key && s.Slot == equipped!.Stack.Slot)).ToList();
                if (own.Count > 0)
                {
                    bag.Add(new BagEntry(group.Key, ItemName(new ItemId(group.Key)), own.Sum(s => s.Count), isEquipped && bookEquipped != group.Key));
                }
            }
        }
        _view = new View(entries, bag.ToArray(), shown.ToArray(), counts, bookEquipped, equippedCount, limit, enabled, null);
        _rows?.Refresh();
    }

    // The HUD list, on the UI thread: the book in its order, then the game's other rows. Off or not ready: the game's own rows.
    private IReadOnlyList<AmmoListRow> BuildRows(AmmoListContext ctx)
    {
        View view = _view;
        if (Environment.TickCount64 - Interlocked.Read(ref _refreshedAt) > RefreshMilliseconds)
        {
            QueueRefresh();
        }
        if (!view.Enabled || view.NotReady != null || view.Shown.Length == 0)
        {
            return ctx.GameRows;
        }
        List<(uint Item, int Count)> rows = GrimoireRules.HudRows(view.Shown, view.Stored, view.Equipped, view.EquippedCount,
            ctx.GameRows.Select(r => (r.Item.Value, r.Count)));
        if (ctx.Revision != _loggedRevision)
        {
            _loggedRevision = ctx.Revision;
            Log.Info($"{Tag}: HUD list (revision {ctx.Revision}): game rows [{string.Join(", ", ctx.GameRows.Select(r => $"{ItemName(r.Item)} x{r.Count}"))}] -> shown [{string.Join(", ", rows.Select(r => $"{ItemName(new ItemId(r.Item))} x{r.Count}"))}]");
        }
        return rows.Select(r => new AmmoListRow(new ItemId(r.Item), r.Count)).ToList();
    }

    // The book's part of the grimoire screen, on the render thread: its entries (click to equip, drag to reorder, right-click to take
    // out), empty slots up to the limit, then the backpack's items of this kind (click or drag into the book to add).
    public void Draw(UiDraw ui)
    {
        View view = _view;
        if (Environment.TickCount64 - Interlocked.Read(ref _refreshedAt) > RefreshMilliseconds)
        {
            QueueRefresh();
        }
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
                ui.ItemIconTrimmed(new ItemId(entry.Item), icon);
                ui.SameLine(0f, 6f * ui.Scale);
                string label = $"{number}. {entry.Name}  x{entry.Count}{(entry.Equipped ? "  (Equipped)" : "")}{(i >= view.Limit ? "  (Over Limit)" : "")}##entry{number}";
                if (ui.Selectable(label, entry.Equipped, 0f, row, UiSelectableFlags.None) && view.Enabled)
                {
                    uint item = entry.Item;
                    Post("equip", p => Equip(p, item, "a screen click"));
                }
                if (ui.ItemClicked(UiMouseButton.Right))
                {
                    uint item = entry.Item;
                    Post("take out", p => TakeOut(p, item));
                }
                ui.Tooltip(entry.Equipped ? "Equipped. Drag to reorder, right-click to take out." : "Click to equip. Drag to reorder, right-click to take out.");
                ui.DragSource(SlotDragKind, entry.Item.ToString(CultureInfo.InvariantCulture), entry.Name);
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
            ui.ItemIconTrimmed(new ItemId(bagEntry.Item), icon);
            ui.SameLine(0f, 6f * ui.Scale);
            if (ui.Selectable($"{bagEntry.Name}  x{bagEntry.Count}{(bagEntry.Equipped ? "  (Equipped)" : "")}##bag{bagEntry.Item}", false, 0f, row, UiSelectableFlags.None))
            {
                uint item = bagEntry.Item;
                Post("add", p => Add(p, item));
            }
            ui.Tooltip("Click or drag into the grimoire to add it.");
            ui.DragSource(BagDragKind, bagEntry.Item.ToString(CultureInfo.InvariantCulture), bagEntry.Name);
            DropOnBag(ui);
        }
        ui.Selectable(view.Bag.Length == 0 ? $"No {_kind.Name} In The Backpack##bagdrop" : "Drop Here To Take Out##bagdrop", false, 0f, row, UiSelectableFlags.None);
        DropOnBag(ui);
        ui.TextDisabled($"Ctrl+Click A {_kind.Singular} In The Backpack To Add It");
        if (view.Bag.Length > 0 && ui.SmallButton($"Add All##{Tag}"))
        {
            Post("add all", AddAll);
        }
        if (view.Bag.Length > 0 && view.Entries.Length > 0)
        {
            ui.SameLine();
        }
        if (view.Entries.Length > 0 && ui.SmallButton($"Take All Out##{Tag}"))
        {
            Post("take all out", TakeAllOut);
        }
    }

    private void DropOnEntry(UiDraw ui, int index)
    {
        if (ui.DropTarget(SlotDragKind) is { } dragged && uint.TryParse(dragged, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint item))
        {
            Post("move", p => Move(p, item, index));
        }
        if (ui.DropTarget(BagDragKind) is { } added && uint.TryParse(added, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint addedItem))
        {
            Post("add", async p =>
            {
                await Add(p, addedItem);
                Refresh(p);
                await Move(p, addedItem, index);
            });
        }
    }

    private void DropOnBag(UiDraw ui)
    {
        if (ui.DropTarget(SlotDragKind) is { } dragged && uint.TryParse(dragged, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint item))
        {
            Post("take out", p => TakeOut(p, item));
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

    private static string ItemName(ItemId item) =>
        item.Value == 0 ? "nothing" : Items.DisplayName(item) ?? Items.Get(item)?.Name ?? $"item {item.Value:x8}";
}
