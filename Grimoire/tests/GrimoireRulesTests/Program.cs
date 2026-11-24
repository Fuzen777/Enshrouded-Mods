using System;
using System.Collections.Generic;
using System.Linq;
using Grimoire;

// The grimoire's rules without the game: order, shown entries, reordering, the next entry after one ran out, storage slots, HUD rows.
int failures = 0;
void Check(bool condition, string what)
{
    if (!condition)
    {
        failures++;
        Console.WriteLine("FAIL: " + what);
    }
}

// Merge keeps known items in place and adds new ones at the end, in the order given.
var order = new List<uint> { 3, 1 };
Check(GrimoireRules.Merge(order, new uint[] { 1, 5, 0, 4, 5 }), "Merge reports a change");
Check(order.SequenceEqual(new uint[] { 3, 1, 5, 4 }), "Merge appends 5 then 4 and skips 0 and repeats");
Check(!GrimoireRules.Merge(order, new uint[] { 4, 3 }), "Merge of known items changes nothing");

// Shown: the order's present items, then present items the order lacks.
Check(GrimoireRules.Shown(new uint[] { 3, 1, 5, 4 }, new uint[] { 4, 1, 9 }).SequenceEqual(new uint[] { 1, 4, 9 }), "Shown follows the order and adds unknown items last");

// MoveEntry works on shown positions and keeps absent items where they are.
order = new List<uint> { 1, 7, 2, 3 };
var shown = new uint[] { 1, 2, 3 }; // 7 is absent
Check(GrimoireRules.MoveEntry(order, shown, 2, 0) && order.SequenceEqual(new uint[] { 3, 1, 7, 2 }), "moving the last entry to the top puts it before the first");
order = new List<uint> { 1, 7, 2, 3 };
Check(GrimoireRules.MoveEntry(order, shown, 0, 1) && order.SequenceEqual(new uint[] { 7, 2, 1, 3 }), "moving the first entry down puts it after the target");
order = new List<uint> { 1, 7, 2, 3 };
Check(GrimoireRules.MoveEntry(order, shown, 0, 11) && order.SequenceEqual(new uint[] { 7, 2, 3, 1 }), "dropping past the last entry puts it at the end");
Check(!GrimoireRules.MoveEntry(order, shown, 1, 1), "a drop on itself changes nothing");
Check(!GrimoireRules.MoveEntry(order, shown, -1, 1), "an unknown entry changes nothing");

// NextAfter: more of the same first, then the following entry, wrapping, skipping what is gone.
var before = new uint[] { 10, 20, 30, 40 };
Check(GrimoireRules.NextAfter(before, 20, new uint[] { 20, 10 }) == 20, "more of the same spell is equipped first");
Check(GrimoireRules.NextAfter(before, 20, new uint[] { 10, 40 }) == 40, "the following entry still stored comes next");
Check(GrimoireRules.NextAfter(before, 40, new uint[] { 10, 30 }) == 10, "after the last entry the list wraps");
Check(GrimoireRules.NextAfter(before, 20, Array.Empty<uint>()) == null, "nothing left: nothing is equipped");
Check(GrimoireRules.NextAfter(before, 99, new uint[] { 30 }) == 30, "an item not in the list falls back to what is stored");

// FreeIndex: one slot per item, else the first empty one.
Check(GrimoireRules.FreeIndex(new uint[] { 5, 0, 7, 0 }, 7) == 2, "a stored item tops up its own slot");
Check(GrimoireRules.FreeIndex(new uint[] { 5, 0, 7, 0 }, 8) == 1, "a new item goes into the first empty slot");
Check(GrimoireRules.FreeIndex(new uint[] { 5, 6 }, 8) == -1, "a full storage has no slot");

// HudRows: the book's entries with the equipped stack counted, then the game's other rows.
var stored = new Dictionary<uint, int> { [1] = 1, [2] = 50 };
var rows = GrimoireRules.HudRows(new uint[] { 2, 3, 1 }, stored, 3, 7, new (uint, int)[] { (9, 4), (3, 6) });
Check(rows.SequenceEqual(new (uint, int)[] { (2, 50), (3, 6), (1, 1), (9, 4) }), "HUD rows: book order, the game's live count for the equipped stack, then the game's other rows");
rows = GrimoireRules.HudRows(new uint[] { 3 }, stored, 3, 7, Array.Empty<(uint, int)>());
Check(rows.SequenceEqual(new (uint, int)[] { (3, 7) }), "HUD rows: the book's count of the equipped stack when the game lists none");

Check(GrimoireRules.ClampSlots(0) == GrimoireRules.MinSlots && GrimoireRules.ClampSlots(99) == GrimoireRules.MaxSlots && GrimoireRules.ClampSlots(12) == 12,
    "Spell Slots stays within 1 to 32");

Console.WriteLine(failures == 0 ? "All grimoire rules tests passed." : $"{failures} grimoire rules test(s) failed.");
return failures == 0 ? 0 : 1;
