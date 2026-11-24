using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kiln;

namespace BetterSpells;

/// <summary>
/// The spellbook: the player's spells kept by the mod with the character (CharacterData.Carried, key "spellbook"), one slot per spell up
/// to the Spellbook Slots setting, in the player's order. Works in a world this player hosts (single player or hosted co-op): the panel,
/// the clicks and the HUD list act on the local player.
/// - A spell equipped from the book goes into the Spell cell of the player's Equipment page, not the backpack, so no book spell takes a
///   backpack slot. A slot's Count is what the book holds outside the game's inventory; the equipped slot's stack is counted on top of it.
/// - The HUD's spell list is replaced (Ui.AmmoList, Replace): the book in its order, then the backpack's other spells. Picking a book
///   spell (or clicking it in the panel) equips it with Items.GiveEquip; a book spell it replaces goes straight back into its slot
///   (Replaced.Take, no free slot needed), any other spell stays in the backpack.
/// - A book spell the game switched away from by itself (a backpack spell picked) stays in the backpack until the player is done
///   switching: five seconds after the Spell slot last changed it is filed back into its slot.
/// - Running out: the slot is topped up from what the book still holds of it, else it is removed and the next book spell is equipped.
/// - The book's column beside the settings window (F3): drag a slot to reorder, Ctrl+click a spell in the backpack or drag it from the panel's backpack list
///   to add it, right-click a slot or drag it to the backpack list to take it out.
/// Kiln bug 20 (drafts): the game keeps a count-0 row in its open list for a spell that left the backpack, and a pick of it empties the
/// Spell slot. Book spells never enter the backpack, so it only hits spells equipped from the backpack; reopening the list fixes it.
/// The book is written only after the game confirmed a move. Every step is logged with "spellbook:".
/// </summary>
internal sealed partial class Spellbook : IDisposable
{
    private const string DataKey = "spellbook";
    // The book of 3.3.0-dev.1 to dev.6 (the probe), moved into this one once.
    private const string ProbeDataKey = "spellbook-probe";
    // The game's EquipmentSlotType of a spell (ItemInfo.EquipSlot): docs/re/items.md section 16.
    private const byte SpellSlotType = 7;
    private const string SlotDragKind = "book-slot";
    private const string BagDragKind = "bag-spell";
    /// <summary>The width of the book's column beside the settings window, in pixels at scale 1.</summary>
    public const float PanelWidth = 300f;
    private const float RowHeight = 34f;
    private static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(5);
    private const long PanelRefreshMilliseconds = 1000;

    public sealed class BookData
    {
        public List<BookSlot> Slots { get; set; } = new List<BookSlot>();
        public bool ProbeMoved { get; set; }
        // The order of the spells kept in Kiln's player storage (item ids). Equipping a stored spell swaps it with the equipped one,
        // so the storage's own slot order changes on every switch; the HUD list follows this list instead.
        public List<uint> StorageOrder { get; set; } = new List<uint>();
    }

    public sealed class BookSlot
    {
        public uint Item { get; set; }
        // What the book holds of it outside the backpack.
        public int Count { get; set; }
        // Its stack is in the backpack: in the Spell slot, or waiting to be filed back.
        public bool Active { get; set; }
    }

    // The probe's book (3.3.0-dev): Count was the whole count, the active entry's in the backpack.
    public sealed class ProbeBook
    {
        public List<ProbeEntry> Spells { get; set; } = new List<ProbeEntry>();
    }

    public sealed class ProbeEntry
    {
        public uint Item { get; set; }
        public int Count { get; set; }
        public bool Active { get; set; }
    }

    // What the panel and the spell list draw, as the tick thread last saw it (they are drawn on other threads).
    private sealed record SlotView(uint Item, int BookCount, int Live, bool Active, bool Equipped, string Name)
    {
        public int Count => BookCount + Live;
    }
    private sealed record BagView(uint Item, int Count, bool Equipped, string Name);
    private sealed record View(SlotView[] Slots, BagView[] Bag, int Capacity, bool Enabled)
    {
        public static readonly View Empty = new View(Array.Empty<SlotView>(), Array.Empty<BagView>(), SpellRules.DefaultSpellbookSlots, false);
    }

    private readonly Mod _mod;
    private readonly Func<int> _capacity;
    private readonly Func<bool> _enabled;
    private AmmoListProvider? _ammoRows;
    private IDisposable? _clicks;
    private volatile View _view = View.Empty;
    private bool _busy;
    private bool _checkQueued;
    private int _refreshQueued;
    private long _refreshedAt;
    // Bumped at every change of the Spell slot; the filing-back waits until it stays the same for SettleDelay.
    private int _spellChanges;
    private ulong _loggedRevision = ulong.MaxValue;

    public Spellbook(Mod mod, Func<int> capacity, Func<bool> enabled)
    {
        _mod = mod;
        _capacity = capacity;
        _enabled = enabled;
    }

    private Logger Log => _mod.Context.Log;

    public void Start()
    {
        StartStorage();
        if (!Ui.Available || !Client.Available || !Client.IsClient)
        {
            Log.Info("spellbook: no game UI here (a dedicated server): the spellbook works in a world you host");
            return;
        }
        if (Ui.AmmoList.Available)
        {
            _ammoRows = Ui.AmmoList.Provide(AmmoListKind.Spell, BuildRows, new AmmoListOptions { Mode = AmmoListMode.Replace });
            Ui.AmmoList.Picked += OnPicked;
        }
        else
        {
            Log.Info($"spellbook: the HUD spell list can't follow the book on this build: {Capabilities.Why(Ui.AmmoList.ListCapability)}");
        }
        try
        {
            _clicks = Ui.Inventory.OnClick(new SlotClickOptions { Button = SlotButton.Left, Modifiers = SlotModifiers.Ctrl, Take = false }, OnSlotClicked);
        }
        catch (KilnUnavailableException error)
        {
            Log.Info($"spellbook: Ctrl+click can't add spells on this build: {error.Message}");
        }
        Ui.Inventory.ScreenChanged += OnScreenChanged;
        Log.Info($"spellbook: ready. GiveEquip {(Items.GiveEquipAvailable ? "available" : "not available here yet")}, HUD list {(_ammoRows != null ? "follows the book" : "is the game's")}; " +
                 "the book is drawn beside the settings window (F3)");
        QueueRefresh();
    }

    public void Dispose()
    {
        Ui.Inventory.ScreenChanged -= OnScreenChanged;
        if (_ammoRows != null)
        {
            Ui.AmmoList.Picked -= OnPicked;
            _ammoRows.Dispose();
            _ammoRows = null;
        }
        _clicks?.Dispose();
        _clicks = null;
    }

    // The settings changed (on or off, the slot count): the list and the panel follow.
    public void SettingsChanged() => QueueRefresh();

    // Events ------------------------------------------------------------------------------------------------------

    // The player's equipment events, from the mod's [Subscribe] handler: a book spell may have left the Spell slot or run out.
    public void OnEquipmentChanged(PlayerEquipmentChangedEvent e)
    {
        // Not gated on the view: the first equipment events of a world are what refreshes it.
        if (!e.Slot.ToString().Contains("Spell", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        Log.Info($"spellbook: Spell slot of {e.Player.Name}: {e.Before} -> {e.Now}{(_busy ? " (during a step)" : "")}");
        if (!_checkQueued)
        {
            _checkQueued = true;
            Post("check", p => Reconcile(p, false), quiet: true);
        }
        ScheduleSettle();
    }

    private void OnPicked(AmmoListPick pick)
    {
        if (!_view.Enabled)
        {
            StoragePicked(pick);
            return;
        }
        Log.Info($"spellbook: HUD list pick {Name(pick.Item)} (a player pick or the game's own selection)");
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

    // Ctrl+click on a spell in the player's own inventory adds it to the book.
    private void OnSlotClicked(SlotClick click)
    {
        if (!_view.Enabled || click.Item.Value == 0 || !IsSpell(click.Item) || !Client.IsHosting)
        {
            return;
        }
        uint item = click.Item.Value;
        int count = (int)click.Slot.Count;
        Post("add", p => Add(p, item, count, -1));
    }

    // Steps ------------------------------------------------------------------------------------------------------

    // Clicks, picks and the panel arrive on the UI or render thread; inventories and character data are read and changed on the tick
    // thread. Every step ends with Reconcile, so the book follows the Spell slot.
    private void Post(string what, Func<EntityId, Task> action, bool quiet = false)
    {
        _mod.Context.Scheduler.Post(async () =>
        {
            if (what == "check")
            {
                _checkQueued = false;
            }
            EntityId player = Client.LocalPlayer;
            if (player.IsNone || !Client.IsHosting)
            {
                if (!quiet)
                {
                    Hud.Toast("Spellbook: works in a world you host", ToastKind.Info, 3);
                }
                return;
            }
            if (_busy)
            {
                if (what == "settle")
                {
                    return; // the running step schedules it again when it ends
                }
                if (!quiet)
                {
                    Log.Info($"spellbook ({what}): skipped, the previous step is still running");
                }
                return;
            }
            _busy = true;
            try
            {
                await action(player);
                if (what != "check" && what != "settle")
                {
                    await Reconcile(player, false);
                    ScheduleSettle();
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"spellbook ({what}) failed: {ex}");
                Hud.Toast($"Spellbook: {what} failed ({ex.GetType().Name}), see kiln.log", ToastKind.Error, 6);
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
        int seen = Interlocked.Increment(ref _spellChanges);
        _ = Task.Delay(SettleDelay).ContinueWith(_ =>
        {
            if (Volatile.Read(ref _spellChanges) == seen)
            {
                Post("settle", p => Reconcile(p, true), quiet: true);
            }
        }, TaskScheduler.Default);
    }

    private BookData Book(EntityId player)
    {
        BookData book = CharacterData.Carried.Get<BookData>(player, DataKey);
        if (!book.ProbeMoved)
        {
            book.ProbeMoved = true;
            ProbeBook probe = CharacterData.Carried.Get<ProbeBook>(player, ProbeDataKey);
            foreach (ProbeEntry entry in probe.Spells)
            {
                // The probe counted the active entry's stack (in the backpack) in Count; the book does not.
                book.Slots.Add(new BookSlot { Item = entry.Item, Count = entry.Active ? 0 : entry.Count, Active = entry.Active });
            }
            if (probe.Spells.Count > 0)
            {
                Log.Info($"spellbook: the probe's book moved in: {string.Join(", ", probe.Spells.Select(s => Name(new ItemId(s.Item))))}");
                probe.Spells.Clear();
            }
        }
        return book;
    }

    // Equips a book spell straight into the Spell slot.
    private async Task Equip(EntityId player, uint item, string why)
    {
        await Reconcile(player, false);
        BookData book = Book(player);
        BookSlot? slot = book.Slots.FirstOrDefault(s => s.Item == item);
        if (slot == null)
        {
            Log.Info($"spellbook ({why}): {Name(new ItemId(item))} is not in the book");
            return;
        }
        InventorySlot? equipped = SpellSlot(player);
        if (equipped != null && equipped.Item.Value == item)
        {
            return;
        }
        if (slot.Active)
        {
            // Its stack is in the backpack: equip it in place.
            InventorySlot? stack = Items.Read(player)?.Slots.FirstOrDefault(s => !s.IsEmpty && s.Item.Value == item);
            if (stack != null)
            {
                EquipResult moved = await Items.Equip(player, stack.Slot, EquipmentSlot.Spell, EquipOptions.None, stack.ItemEntity);
                Log.Info($"spellbook ({why}): {Name(new ItemId(item))} is in the backpack, equipped in place -> {moved}");
            }
            return;
        }
        if (slot.Count <= 0)
        {
            return;
        }
        if (!Items.GiveEquipAvailable)
        {
            Hud.Toast($"Spellbook: can't equip here ({Capabilities.Why("items.give_equip")})", ToastKind.Error, 4);
            return;
        }
        int count = Math.Min(slot.Count, Math.Max(1, Items.Get(new ItemId(item))?.MaxStack ?? 1));
        // A book spell in the Spell slot goes straight back into its slot (no free slot needed).
        bool bookInSlot = equipped != null && book.Slots.Any(s => s.Active && s.Item == equipped.Item.Value);
        Replaced replaced = bookInSlot ? Replaced.Take : Replaced.Backpack;
        EquipResult result = await Items.GiveEquip(player, new ItemId(item), count, EquipmentSlot.Spell, new GiveEquipOptions { Replaced = replaced });
        Log.Info($"spellbook ({why}): {Name(new ItemId(item))} x{count} ({replaced}) -> {result}; replaced {Name(result.ReplacedItem)} x{result.ReplacedCount}");
        if (!result.Ok)
        {
            Hud.Toast($"Spellbook: {Name(new ItemId(item))} not equipped ({result.Reason})", ToastKind.Error, 4);
            return;
        }
        // The game confirmed: only now is the book changed.
        book = Book(player);
        slot = book.Slots.FirstOrDefault(s => s.Item == item);
        if (slot != null)
        {
            slot.Count = Math.Max(0, slot.Count - count);
            slot.Active = true;
        }
        if (replaced == Replaced.Take && result.ReplacedCount > 0)
        {
            BookSlot? back = book.Slots.FirstOrDefault(s => s.Active && s.Item == result.ReplacedItem.Value);
            if (back != null)
            {
                back.Count += result.ReplacedCount;
                back.Active = false;
            }
            else
            {
                Log.Warn($"spellbook ({why}): the game took {Name(result.ReplacedItem)} x{result.ReplacedCount}, which is not a book spell in the backpack");
            }
        }
    }

    // Makes the book follow the backpack. A book spell whose stack is gone ran out (or went to a grave): the slot is topped up from
    // the book when it still holds some, else removed and the next book spell is equipped when the Spell slot is empty. With settle
    // (the player is done switching), every book spell in the backpack but not in the Spell slot is filed back into its slot.
    private async Task Reconcile(EntityId player, bool settle)
    {
        if (!_enabled())
        {
            return;
        }
        BookData book = Book(player);
        InventorySlot? equipped = SpellSlot(player);
        InventoryContents? inventory = Items.Read(player);
        if (inventory == null)
        {
            return;
        }
        uint? next = null;
        foreach (BookSlot slot in book.Slots.ToList())
        {
            if (!slot.Active)
            {
                continue;
            }
            bool inSlot = equipped != null && equipped.Item.Value == slot.Item;
            InventorySlot? stack = inSlot ? equipped : inventory.Slots.FirstOrDefault(s => !s.IsEmpty && s.Item.Value == slot.Item);
            if (stack == null)
            {
                slot.Active = false;
                int index = book.Slots.IndexOf(slot);
                if (slot.Count > 0)
                {
                    Log.Info($"spellbook: {Name(new ItemId(slot.Item))} ran out; the book still holds {slot.Count}");
                    next ??= slot.Item;
                }
                else
                {
                    book.Slots.Remove(slot);
                    Log.Info($"spellbook: {Name(new ItemId(slot.Item))} ran out: its slot is removed");
                    if (book.Slots.Count > 0)
                    {
                        next ??= book.Slots[index % book.Slots.Count].Item;
                    }
                }
                continue;
            }
            if (inSlot || !settle)
            {
                continue;
            }
            ItemsResult result = await Items.TakeSlot(player, stack, false);
            Log.Info($"spellbook: settled, filing {Name(new ItemId(slot.Item))} x{stack.Count} at {stack.Slot} back into slot {book.Slots.IndexOf(slot) + 1} -> {result}");
            if (result.Succeeded && result.Done > 0)
            {
                slot.Count += result.Done;
                slot.Active = false;
            }
        }
        if (next is { } nextItem && SpellSlot(player) == null)
        {
            await Equip(player, nextItem, "ran out, next");
        }
    }

    // Adds a spell from the backpack: every stack of it there (the book's own equipped stack aside), to its slot or a new one at
    // `at` (-1: the end). The equipped stack of a spell that is not a book spell is added too.
    private async Task Add(EntityId player, uint item, int clickedCount, int at)
    {
        BookData book = Book(player);
        BookSlot? slot = book.Slots.FirstOrDefault(s => s.Item == item);
        int capacity = _capacity();
        if (slot == null && book.Slots.Count >= capacity)
        {
            Hud.Toast($"Spellbook: the book is full ({capacity} slots, see F3)", ToastKind.Info, 3);
            return;
        }
        InventoryContents? inventory = Items.Read(player);
        InventorySlot? equipped = SpellSlot(player);
        bool bookEquipped = slot != null && slot.Active;
        List<InventorySlot> stacks = inventory?.Slots.Where(s => !s.IsEmpty && s.Item.Value == item && !(bookEquipped && equipped != null && s.Slot == equipped.Slot)).ToList()
            ?? new List<InventorySlot>();
        if (stacks.Count == 0)
        {
            Hud.Toast($"Spellbook: {Name(new ItemId(item))} is already in the book", ToastKind.Info, 2);
            return;
        }
        int taken = 0;
        bool tookEquipped = false;
        foreach (InventorySlot stack in stacks)
        {
            ItemsResult result = await Items.TakeSlot(player, stack, false);
            Log.Info($"spellbook (add): {Name(stack.Item)} x{stack.Count} at {stack.Slot} -> {result}");
            if (result.Succeeded)
            {
                taken += result.Done;
                tookEquipped |= equipped != null && stack.Slot == equipped.Slot && result.Done > 0;
            }
        }
        if (taken <= 0)
        {
            return;
        }
        book = Book(player);
        slot = book.Slots.FirstOrDefault(s => s.Item == item);
        if (slot != null)
        {
            slot.Count += taken;
            if (slot.Active && equipped != null && equipped.Item.Value == item && SpellSlot(player) == null)
            {
                slot.Active = false; // the active stack itself was taken (a spell the book held twice)
            }
        }
        else
        {
            slot = new BookSlot { Item = item, Count = taken };
            if (at >= 0 && at < book.Slots.Count)
            {
                book.Slots.Insert(at, slot);
            }
            else
            {
                book.Slots.Add(slot);
            }
        }
        Hud.Toast($"Spellbook: {Name(new ItemId(item))} x{taken} added", ToastKind.Info, 2);
        if (tookEquipped && SpellSlot(player) == null)
        {
            // It was the equipped spell: it stays equipped, now from the book.
            await Equip(player, item, "added while equipped");
        }
    }

    // Takes a slot out of the book: what the book holds of it goes back to the backpack (its equipped stack is there already).
    private async Task Remove(EntityId player, int index)
    {
        BookData book = Book(player);
        if (index < 0 || index >= book.Slots.Count)
        {
            return;
        }
        BookSlot slot = book.Slots[index];
        int count = slot.Count;
        if (count > 0)
        {
            ItemsResult result = await Items.Give(player, new ItemId(slot.Item), count, null, GiveOptions.None, null);
            Log.Info($"spellbook (remove): {Name(new ItemId(slot.Item))} x{count} back to the backpack -> {result}");
            book = Book(player);
            slot.Count -= Math.Max(0, result.Done);
            if (slot.Count > 0)
            {
                Hud.Toast($"Spellbook: the backpack is full; {slot.Count} {Name(new ItemId(slot.Item))} stay in the book", ToastKind.Info, 3);
                return;
            }
        }
        book.Slots.Remove(slot);
        Hud.Toast($"Spellbook: {Name(new ItemId(slot.Item))} taken out", ToastKind.Info, 2);
    }

    private Task Move(EntityId player, int from, int to)
    {
        BookData book = Book(player);
        if (from < 0 || from >= book.Slots.Count || to < 0 || from == to)
        {
            return Task.CompletedTask;
        }
        BookSlot slot = book.Slots[from];
        book.Slots.RemoveAt(from);
        book.Slots.Insert(Math.Min(to, book.Slots.Count), slot);
        Log.Info($"spellbook: {Name(new ItemId(slot.Item))} moved from slot {from + 1} to {Math.Min(to, book.Slots.Count - 1) + 1}");
        return Task.CompletedTask;
    }

    private async Task TakeAllOut(EntityId player)
    {
        BookData book = Book(player);
        while (book.Slots.Count > 0)
        {
            int before = book.Slots.Count;
            await Remove(player, 0);
            book = Book(player);
            if (book.Slots.Count == before)
            {
                return; // the backpack is full
            }
        }
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
                _view = View.Empty with { Enabled = false };
                _ammoRows?.Refresh();
                return;
            }
            Refresh(player);
        });
    }

    private void Refresh(EntityId player)
    {
        Interlocked.Exchange(ref _refreshedAt, Environment.TickCount64);
        bool enabled = _enabled();
        BookData book = Book(player);
        InventoryContents? inventory = Items.Read(player);
        InventorySlot? equipped = SpellSlot(player);
        var bag = new List<BagView>();
        if (inventory != null)
        {
            foreach (IGrouping<uint, InventorySlot> spell in inventory.Slots.Where(s => !s.IsEmpty && IsSpell(s.Item)).GroupBy(s => s.Item.Value))
            {
                BookSlot? own = book.Slots.FirstOrDefault(s => s.Item == spell.Key);
                if (own != null && own.Active)
                {
                    continue; // the book's own stack in the backpack is shown on its slot
                }
                bag.Add(new BagView(spell.Key, spell.Sum(s => s.Count), equipped != null && equipped.Item.Value == spell.Key, Name(new ItemId(spell.Key))));
            }
        }
        var slots = new List<SlotView>();
        foreach (BookSlot slot in book.Slots)
        {
            int live = 0;
            if (slot.Active && inventory != null)
            {
                live = inventory.Slots.Where(s => !s.IsEmpty && s.Item.Value == slot.Item).Sum(s => s.Count);
            }
            slots.Add(new SlotView(slot.Item, slot.Count, live, slot.Active, equipped != null && equipped.Item.Value == slot.Item, Name(new ItemId(slot.Item))));
        }
        _view = new View(slots.ToArray(), bag.ToArray(), Math.Max(_capacity(), 1), enabled);
        RefreshStorage(player, book, equipped);
        _ammoRows?.Refresh();
    }

    // The spell list, on the UI thread: the book in its order, then every spell the game lists that the book does not hold, in the
    // game's order. Book off: the game's own rows.
    private IReadOnlyList<AmmoListRow> BuildRows(AmmoListContext ctx)
    {
        View view = _view;
        if (Environment.TickCount64 - Interlocked.Read(ref _refreshedAt) > PanelRefreshMilliseconds)
        {
            QueueRefresh();
        }
        if (!view.Enabled)
        {
            return StorageRows(ctx);
        }
        var rows = new List<AmmoListRow>(view.Slots.Length + ctx.GameRows.Count);
        foreach (SlotView slot in view.Slots)
        {
            int count = slot.Count;
            if (slot.Active)
            {
                foreach (AmmoListRow live in ctx.GameRows)
                {
                    if (live.Item.Value == slot.Item)
                    {
                        // The game's count of the stack in the backpack is fresher than the view's.
                        count = live.Count + slot.BookCount;
                        break;
                    }
                }
            }
            rows.Add(new AmmoListRow(new ItemId(slot.Item), count));
        }
        foreach (AmmoListRow row in ctx.GameRows)
        {
            if (!view.Slots.Any(s => s.Item == row.Item.Value))
            {
                rows.Add(row);
            }
        }
        if (ctx.Revision != _loggedRevision)
        {
            _loggedRevision = ctx.Revision;
            Log.Info($"spellbook: spell list (revision {ctx.Revision}): game rows [{string.Join(", ", ctx.GameRows.Select(r => $"{Name(r.Item)} x{r.Count}"))}] -> shown [{string.Join(", ", rows.Select(r => $"{Name(r.Item)} x{r.Count}"))}]");
        }
        return rows;
    }

    // The book's column beside the settings window (F3), on the render thread: the book's slots (drag to reorder, click to equip,
    // right-click to take out), then the backpack's spells (click or drag into the book to add). Asked by the user on 23 Nov 2026 in
    // place of a panel beside the backpack, which got in the way of the inventory and character screens.
    public void Draw(UiDraw ui)
    {
        View view = _view;
        if (Environment.TickCount64 - Interlocked.Read(ref _refreshedAt) > PanelRefreshMilliseconds)
        {
            QueueRefresh();
        }
        ui.Text("Spellbook");
        if (!view.Enabled)
        {
            ui.TextWrapped(Client.Available && Client.IsHosting
                ? "The spellbook is off: turn it on in the Spellbook card."
                : "The spellbook works in a world you host.");
            if (Client.Available && Client.IsHosting)
            {
                DrawStorageTest(ui);
            }
            return;
        }
        float row = RowHeight * ui.Scale;
        float icon = row - 4f * ui.Scale;
        ui.Text(ui.Palette.TextMuted, string.Create(CultureInfo.InvariantCulture, $"{view.Slots.Length} / {view.Capacity} slots"));
        int shown = Math.Max(view.Capacity, view.Slots.Length);
        for (int i = 0; i < shown; i++)
        {
            string number = (i + 1).ToString(CultureInfo.InvariantCulture);
            if (i < view.Slots.Length)
            {
                SlotView slot = view.Slots[i];
                ui.ItemIconTrimmed(new ItemId(slot.Item), icon);
                ui.SameLine(0f, 6f * ui.Scale);
                string label = $"{number}. {slot.Name}  x{slot.Count}{(i >= view.Capacity ? "  (over the limit)" : "")}##slot{number}";
                if (ui.Selectable(label, slot.Equipped, 0f, row, UiSelectableFlags.None))
                {
                    uint item = slot.Item;
                    Post("equip", p => Equip(p, item, "a panel click"));
                }
                if (ui.ItemClicked(UiMouseButton.Right))
                {
                    int index = i;
                    Post("remove", p => Remove(p, index));
                }
                ui.Tooltip(slot.Equipped ? "Equipped. Drag to move; right-click to take it out of the book." : "Click to equip. Drag to move; right-click to take it out of the book.");
                ui.DragSource(SlotDragKind, i.ToString(CultureInfo.InvariantCulture), slot.Name);
            }
            else
            {
                ui.Space(icon, icon);
                ui.SameLine(0f, 6f * ui.Scale);
                ui.Selectable($"{number}. -##slot{number}", false, 0f, row, UiSelectableFlags.None);
            }
            DropOnSlot(ui, i);
        }
        ui.Separator("In your backpack");
        foreach (BagView spell in view.Bag)
        {
            ui.ItemIconTrimmed(new ItemId(spell.Item), icon);
            ui.SameLine(0f, 6f * ui.Scale);
            if (ui.Selectable($"{spell.Name}  x{spell.Count}{(spell.Equipped ? "  (equipped)" : "")}##bag{spell.Item}", false, 0f, row, UiSelectableFlags.None))
            {
                uint item = spell.Item;
                int count = spell.Count;
                Post("add", p => Add(p, item, count, -1));
            }
            ui.Tooltip("Click or drag into the book to add it.");
            ui.DragSource(BagDragKind, spell.Item.ToString(CultureInfo.InvariantCulture), spell.Name);
            DropOnBag(ui);
        }
        ui.Selectable(view.Bag.Length == 0 ? "No other spells. Drop a slot here to take it out.##bagdrop" : "Drop a slot here to take it out.##bagdrop", false, 0f, row, UiSelectableFlags.None);
        DropOnBag(ui);
        ui.TextWrapped("Ctrl+click a spell in your backpack to add it.");
        if (view.Slots.Length > 0 && ui.SmallButton("Take All Out"))
        {
            Post("take all out", TakeAllOut);
        }
    }

    private void DropOnSlot(UiDraw ui, int index)
    {
        if (ui.DropTarget(SlotDragKind) is { } from && int.TryParse(from, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fromIndex))
        {
            Post("move", p => Move(p, fromIndex, index));
        }
        if (ui.DropTarget(BagDragKind) is { } item && uint.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint itemId))
        {
            Post("add", p => Add(p, itemId, 0, index));
        }
    }

    private void DropOnBag(UiDraw ui)
    {
        if (ui.DropTarget(SlotDragKind) is { } from && int.TryParse(from, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fromIndex))
        {
            Post("remove", p => Remove(p, fromIndex));
        }
    }

    // Reading the game ---------------------------------------------------------------------------------------------------

    // The stack in the player's Spell slot, as a slot of their inventory, or null. The Spell slot refers to a backpack stack:
    // CaptureSet names it (StackEquipment.Source) for a plain stack; an eternal spell (an item entity) is a slot marked equipped.
    private static InventorySlot? SpellSlot(EntityId player)
    {
        InventoryContents? inventory = Items.Read(player);
        if (inventory == null)
        {
            return null;
        }
        EquipmentSetCaptureResult capture = Items.CaptureSet(player);
        if (capture.Ok && capture.Set != null)
        {
            foreach (EquipmentSetEntry entry in capture.Set.Entries.Where(e => e.Slot == EquipmentSlot.Spell))
            {
                if (entry.Selection is StackEquipment stack)
                {
                    InventorySlot? source = inventory.Slots.FirstOrDefault(s => !s.IsEmpty && s.Slot == stack.Source && s.Item == stack.Item);
                    if (source != null)
                    {
                        return source;
                    }
                }
            }
        }
        return inventory.Slots.FirstOrDefault(s => !s.IsEmpty && s.IsEquipped && IsSpell(s.Item));
    }

    private static string Name(ItemId item) =>
        item.Value == 0 ? "nothing" : Items.DisplayName(item) ?? Items.Get(item)?.Name ?? $"item {item.Value:x8}";

    public static bool IsSpell(ItemId item) => Items.Get(item) is { } info && info.Category == ItemCategory.Ammunition && info.EquipSlot == SpellSlotType;
}
