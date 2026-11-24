using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Kiln;

namespace BetterSpells;

/// <summary>
/// Storage test (3.3.0-dev.10): the book's spells in Kiln's player storage (Kiln 0.2.0-dev.30.7, kiln#128) instead of mod data.
/// One storage per player, "better-spells:spellbook", 32 slots, spells only, kept at death. Shown in the book's column while the
/// book is off: move the book's spells in, store the backpack's spells, equip a stored spell where it lies (Items.Equip with the
/// storage slot, nothing moves), take them all back. The HUD spell list shows the stored spells first while the book is off.
/// What the test answers: does the game cast from (and use up) a stack in the storage, and does the storage come back after a
/// save and reload, with the spell still equipped. Every step is logged with "spellbook storage:".
/// </summary>
internal sealed partial class Spellbook
{
    private const string StorageKey = "spellbook";
    private PlayerStorage? _storage;
    private volatile StorageView _storageView = StorageView.Empty;

    private sealed record StorageRow(int Index, uint Item, int Count, bool Equipped, string Name);
    // Rows: the stored stacks, in slot order. Order: the spells' order (BookData.StorageOrder). Equipped: the spell in the Spell slot
    // (0 for none) and its count; equipping from the storage moves the stack to the Equipment page and the old one into its cell.
    private sealed record StorageView(StorageRow[] Rows, string Status, uint[] Order, uint Equipped, int EquippedCount)
    {
        public static readonly StorageView Empty = new StorageView(Array.Empty<StorageRow>(), "not read yet", Array.Empty<uint>(), 0, 0);
    }

    // From Start, before the UI checks: a storage registers on every side that may run a world.
    private void StartStorage()
    {
        if (!PlayerStorage.Available)
        {
            Log.Info($"spellbook storage: no player storage here: {Capabilities.Why(PlayerStorage.Capability)}");
            return;
        }
        try
        {
            _storage = Items.RegisterPlayerStorage(StorageKey, SpellRules.MaxSpellbookSlots, StorageFilter.Ammunition(AmmoKinds.Spell), StorageDeath.Keep);
            Log.Info($"spellbook storage: registered {_storage}");
        }
        catch (Exception error) when (error is KilnUnavailableException or InvalidOperationException or ArgumentException)
        {
            Log.Info($"spellbook storage: not registered: {error.Message}");
        }
    }

    // Kiln's storage events, from the mod's [Subscribe] handlers.
    public void OnStorageEvent(string key, string what)
    {
        if (_storage != null && key == _storage.FullKey)
        {
            Log.Info($"spellbook storage: {what}");
            QueueRefresh();
        }
    }

    private void RefreshStorage(EntityId player, BookData book, InventorySlot? equipped)
    {
        if (_storage == null)
        {
            _storageView = new StorageView(Array.Empty<StorageRow>(), "player storage is not available (see kiln.log)", Array.Empty<uint>(), 0, 0);
            return;
        }
        PlayerStorageStatus status = _storage.Status(player);
        InventoryContents? contents = _storage.IsReady(player) ? _storage.Read(player) : null;
        var rows = new List<StorageRow>();
        if (contents != null)
        {
            SlotId? source = SpellSource(player);
            for (int i = 0; i < _storage.Slots; i++)
            {
                SlotId at = _storage.Slot(player, i);
                InventorySlot? stack = contents.Slots.FirstOrDefault(s => s.Slot == at);
                if (stack != null && !stack.IsEmpty)
                {
                    rows.Add(new StorageRow(i, stack.Item.Value, stack.Count, stack.IsEquipped || (source is { } src && src == at), Name(stack.Item)));
                }
            }
        }
        uint equippedItem = equipped == null || equipped.IsEmpty ? 0u : equipped.Item.Value;
        if (contents != null)
        {
            // A spell joins the order where it first shows up, stored or equipped; one that is in neither stays listed, so it
            // keeps its place when it comes back.
            foreach (uint item in rows.Select(r => r.Item).Append(equippedItem).Where(i => i != 0 && !book.StorageOrder.Contains(i)).Distinct().ToList())
            {
                if (rows.Any(r => r.Item == item) || item == equippedItem)
                {
                    book.StorageOrder.Add(item);
                }
            }
        }
        _storageView = new StorageView(rows.ToArray(),
            $"{status.State}, {(status.Paired ? "paired" : "unpaired")}, from {status.Source}, {status.Stacks} stack(s), {status.Parked} parked",
            book.StorageOrder.ToArray(), equippedItem, equipped?.Count ?? 0);
    }

    // Where the Spell slot's stack lies, as CaptureSet names it (a plain stack), or null.
    private static SlotId? SpellSource(EntityId player)
    {
        EquipmentSetCaptureResult capture = Items.CaptureSet(player);
        if (capture.Ok && capture.Set != null)
        {
            foreach (EquipmentSetEntry entry in capture.Set.Entries.Where(e => e.Slot == EquipmentSlot.Spell))
            {
                if (entry.Selection is StackEquipment stack)
                {
                    return stack.Source;
                }
            }
        }
        return null;
    }

    private PlayerStorage? ReadyStorage(EntityId player, string what)
    {
        if (_storage == null || !_storage.IsReady(player))
        {
            string why = _storage == null ? "player storage is not available" : $"the storage is not ready ({_storage.Status(player).State})";
            Log.Info($"spellbook storage ({what}): {why}");
            Hud.Toast($"Spellbook storage: {why}", ToastKind.Info, 4);
            return null;
        }
        return _storage;
    }

    private static int FreeIndex(PlayerStorage storage, EntityId player, InventoryContents contents, uint item)
    {
        int free = -1;
        for (int i = 0; i < storage.Slots; i++)
        {
            SlotId at = storage.Slot(player, i);
            InventorySlot? stack = contents.Slots.FirstOrDefault(s => s.Slot == at);
            if (stack == null || stack.IsEmpty)
            {
                free = free < 0 ? i : free;
            }
            else if (stack.Item.Value == item)
            {
                return i; // the same spell: one slot each, as in the book
            }
        }
        return free;
    }

    // The book's spells into the storage, in the book's order: what the book holds is given into the storage slot, the book's stack
    // in the Spell slot (or the backpack) is moved there. A slot leaves the book only once all of it is in the storage.
    private async Task MoveBookIntoStorage(EntityId player)
    {
        PlayerStorage? storage = ReadyStorage(player, "move the book in");
        if (storage == null)
        {
            return;
        }
        EntityId holder = storage.EntityOf(player);
        BookData book = Book(player);
        foreach (BookSlot slot in book.Slots.ToList())
        {
            InventoryContents? contents = storage.Read(player);
            if (contents == null)
            {
                return;
            }
            int index = FreeIndex(storage, player, contents, slot.Item);
            if (index < 0)
            {
                Log.Info("spellbook storage (move the book in): the storage is full");
                Hud.Toast("Spellbook storage: full", ToastKind.Info, 3);
                break;
            }
            SlotId at = storage.Slot(player, index);
            string name = Name(new ItemId(slot.Item));
            if (slot.Active)
            {
                InventoryContents? inventory = Items.Read(player);
                foreach (InventorySlot stack in inventory?.Slots.Where(s => !s.IsEmpty && s.Item.Value == slot.Item).ToList() ?? new List<InventorySlot>())
                {
                    MoveResult moved = await Items.Move(player, stack.Slot, holder, 0, MoveOptions.None, at);
                    Log.Info($"spellbook storage (move the book in): {name} x{stack.Count} from {stack.Slot}{(stack.IsEquipped ? " (equipped)" : "")} -> slot {index}: {moved}");
                }
                if (Items.Read(player)?.Slots.Any(s => !s.IsEmpty && s.Item.Value == slot.Item) != true)
                {
                    slot.Active = false;
                }
            }
            if (slot.Count > 0)
            {
                ItemsResult given = await Items.Give(holder, new ItemId(slot.Item), slot.Count, null, GiveOptions.None, at);
                Log.Info($"spellbook storage (move the book in): {name} x{slot.Count} from the book -> slot {index}: {given}");
                if (given.Succeeded)
                {
                    slot.Count -= given.Done;
                }
            }
            if (slot.Count <= 0 && !slot.Active)
            {
                book.Slots.Remove(slot);
            }
        }
        Log.Info($"spellbook storage (move the book in): {book.Slots.Count} slot(s) left in the book");
    }

    // Every spell stack in the backpack (not the equipment pages) into the storage.
    private async Task StoreBackpackSpells(EntityId player)
    {
        PlayerStorage? storage = ReadyStorage(player, "store the backpack's spells");
        if (storage == null)
        {
            return;
        }
        List<InventorySlot> spells = Items.Read(player)?.Slots
            .Where(s => !s.IsEmpty && s.Category == InventoryCategory.Generic && IsSpell(s.Item)).ToList() ?? new List<InventorySlot>();
        if (spells.Count == 0)
        {
            Hud.Toast("Spellbook storage: no spells in your backpack", ToastKind.Info, 3);
            return;
        }
        foreach (InventorySlot stack in spells)
        {
            InventoryContents? contents = storage.Read(player);
            int index = contents == null ? -1 : FreeIndex(storage, player, contents, stack.Item.Value);
            MoveResult moved = index < 0
                ? await storage.Store(player, stack.Slot)
                : await Items.Move(player, stack.Slot, storage.EntityOf(player), 0, MoveOptions.None, storage.Slot(player, index));
            Log.Info($"spellbook storage (store): {Name(stack.Item)} x{stack.Count} from {stack.Slot}{(stack.IsEquipped ? " (equipped)" : "")} -> slot {index}: {moved}");
        }
    }

    // Equips a stored spell: Kiln moves its stack to the Equipment page's Spell cell and the spell equipped before into this slot.
    private async Task EquipFromStorage(EntityId player, int index, string why)
    {
        PlayerStorage? storage = ReadyStorage(player, "equip");
        if (storage == null)
        {
            return;
        }
        SlotId at = storage.Slot(player, index);
        InventorySlot? stack = storage.Read(player)?.Slots.FirstOrDefault(s => s.Slot == at);
        if (stack == null || stack.IsEmpty)
        {
            Log.Info($"spellbook storage ({why}): slot {index} holds nothing");
            return;
        }
        EquipResult result = await Items.Equip(player, at, EquipmentSlot.Spell, EquipOptions.None, stack.ItemEntity);
        Log.Info($"spellbook storage ({why}): {Name(stack.Item)} x{stack.Count} in slot {index} ({at}) -> equip {result}; Spell slot now at {SpellSource(player)?.ToString() ?? "(no stack source)"}");
        if (!result.Ok)
        {
            Hud.Toast($"Spellbook storage: {Name(stack.Item)} not equipped ({result.Status})", ToastKind.Info, 4);
        }
    }

    // Everything in the storage back into the backpack.
    private async Task TakeAllFromStorage(EntityId player)
    {
        PlayerStorage? storage = ReadyStorage(player, "take all back");
        if (storage == null)
        {
            return;
        }
        foreach (StorageRow row in _storageView.Rows)
        {
            MoveResult moved = await storage.Retrieve(player, row.Index);
            Log.Info($"spellbook storage (take all back): {row.Name} x{row.Count} from slot {row.Index} -> backpack: {moved}");
            if (!moved.Succeeded)
            {
                Hud.Toast($"Spellbook storage: {row.Name} stays in the storage ({moved.Status})", ToastKind.Info, 4);
            }
        }
    }

    // The HUD list while the book is off: the stored spells and the equipped one in the storage order (one row each), then the game's
    // other rows. The equipped spell keeps its place, so the game's next and previous walk the list in order.
    private IReadOnlyList<AmmoListRow> StorageRows(AmmoListContext ctx)
    {
        StorageView view = _storageView;
        if (view.Rows.Length == 0)
        {
            return ctx.GameRows;
        }
        var rows = new List<AmmoListRow>(view.Order.Length + ctx.GameRows.Count);
        var listed = new HashSet<uint>();
        foreach (uint item in view.Order)
        {
            int count = view.Rows.Where(r => r.Item == item).Sum(r => r.Count);
            if (item == view.Equipped)
            {
                count += view.EquippedCount;
            }
            if (count > 0 && listed.Add(item))
            {
                rows.Add(new AmmoListRow(new ItemId(item), count));
            }
        }
        foreach (StorageRow row in view.Rows)
        {
            if (listed.Add(row.Item))
            {
                rows.Add(new AmmoListRow(new ItemId(row.Item), view.Rows.Where(r => r.Item == row.Item).Sum(r => r.Count)));
            }
        }
        foreach (AmmoListRow row in ctx.GameRows)
        {
            if (!listed.Contains(row.Item.Value))
            {
                rows.Add(row);
            }
        }
        if (ctx.Revision != _loggedRevision)
        {
            _loggedRevision = ctx.Revision;
            Log.Info($"spellbook storage: spell list (revision {ctx.Revision}): game rows [{string.Join(", ", ctx.GameRows.Select(r => $"{Name(r.Item)} x{r.Count}"))}] -> shown [{string.Join(", ", rows.Select(r => $"{Name(r.Item)} x{r.Count}"))}]");
        }
        return rows;
    }

    private void StoragePicked(AmmoListPick pick)
    {
        if (pick.Item.Value == _storageView.Equipped)
        {
            Log.Info($"spellbook storage: HUD list pick {Name(pick.Item)}: already equipped");
            return;
        }
        StorageRow? row = _storageView.Rows.FirstOrDefault(r => r.Item == pick.Item.Value);
        Log.Info($"spellbook storage: HUD list pick {Name(pick.Item)}{(row == null ? " (not stored: the game's own)" : $" -> slot {row.Index}")}");
        if (row != null)
        {
            int index = row.Index;
            Post("storage equip", p => EquipFromStorage(p, index, "a HUD list pick"));
        }
    }

    // The test's part of the book's column, while the book is off.
    private void DrawStorageTest(UiDraw ui)
    {
        StorageView view = _storageView;
        ui.Separator("Storage test (dev.11)");
        ui.TextWrapped($"Kiln player storage: {view.Status}");
        float row = RowHeight * ui.Scale;
        float icon = row - 4f * ui.Scale;
        foreach (StorageRow stored in view.Rows)
        {
            ui.ItemIconTrimmed(new ItemId(stored.Item), icon);
            ui.SameLine(0f, 6f * ui.Scale);
            string number = (stored.Index + 1).ToString(CultureInfo.InvariantCulture);
            if (ui.Selectable($"{number}. {stored.Name}  x{stored.Count}##stored{number}", stored.Equipped, 0f, row, UiSelectableFlags.None))
            {
                int index = stored.Index;
                Post("storage equip", p => EquipFromStorage(p, index, "a panel click"));
            }
            ui.Tooltip(stored.Equipped ? "Equipped from the storage." : "Click to equip it where it lies.");
        }
        if (view.Rows.Length == 0)
        {
            ui.TextWrapped("The storage is empty.");
        }
        if (ui.SmallButton("Move the book's spells in"))
        {
            Post("storage book in", MoveBookIntoStorage);
        }
        if (ui.SmallButton("Store the backpack's spells"))
        {
            Post("storage store", StoreBackpackSpells);
        }
        if (view.Rows.Length > 0 && ui.SmallButton("Take all back to the backpack"))
        {
            Post("storage take all", TakeAllFromStorage);
        }
    }
}
