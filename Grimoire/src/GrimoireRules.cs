using System;
using System.Collections.Generic;
using System.Linq;

namespace Grimoire;

/// <summary>
/// The rules of a book that have no game in them, so the tests run without Kiln: the player's order of the items, the rows the HUD's
/// list shows, where a stored stack goes, and which item comes next when one runs out. Items are the game's item ids.
/// </summary>
public static class GrimoireRules
{
    public const int MinSlots = 1;
    public const int MaxSlots = 32;
    public const int DefaultSlots = 12;

    public static int ClampSlots(int slots) => Math.Clamp(slots, MinSlots, MaxSlots);

    /// <summary>Adds the items present that the order does not know yet at its end, in the order given. An item the order knows keeps its
    /// place, also while it is absent, so it comes back where it was.</summary>
    public static bool Merge(List<uint> order, IEnumerable<uint> present)
    {
        bool changed = false;
        foreach (uint item in present)
        {
            if (item != 0 && !order.Contains(item))
            {
                order.Add(item);
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>The book's entries as shown: the order's items that are present, then any present item the order lacks.</summary>
    public static List<uint> Shown(IReadOnlyList<uint> order, IReadOnlyCollection<uint> present)
    {
        var shown = new List<uint>(present.Count);
        foreach (uint item in order)
        {
            if (present.Contains(item) && !shown.Contains(item))
            {
                shown.Add(item);
            }
        }
        foreach (uint item in present)
        {
            if (item != 0 && !shown.Contains(item))
            {
                shown.Add(item);
            }
        }
        return shown;
    }

    /// <summary>Moves the shown entry at <paramref name="from"/> to the place of the shown entry at <paramref name="to"/> (or after the last
    /// one when <paramref name="to"/> is past the end), in the order that holds absent items too.</summary>
    public static bool MoveEntry(List<uint> order, IReadOnlyList<uint> shown, int from, int to)
    {
        if (from < 0 || from >= shown.Count || to < 0 || from == to)
        {
            return false;
        }
        uint moved = shown[from];
        Merge(order, shown);
        order.Remove(moved);
        if (to >= shown.Count)
        {
            int last = order.IndexOf(shown[^1]);
            order.Insert(last < 0 ? order.Count : last + 1, moved);
            return true;
        }
        int target = order.IndexOf(shown[to]);
        if (target < 0)
        {
            order.Add(moved);
            return true;
        }
        // Moving down puts it after the target, moving up before it, as a drag onto a row reads.
        order.Insert(from < to ? target + 1 : target, moved);
        return true;
    }

    /// <summary>The entry to equip after <paramref name="item"/> ran out: the one that followed it in <paramref name="shownBefore"/> (the
    /// entries before it ran out) and is still in <paramref name="available"/>, wrapping around; null when none is left.</summary>
    public static uint? NextAfter(IReadOnlyList<uint> shownBefore, uint item, IReadOnlyCollection<uint> available)
    {
        if (available.Contains(item))
        {
            return item; // more of it is stored
        }
        int at = -1;
        for (int i = 0; i < shownBefore.Count; i++)
        {
            if (shownBefore[i] == item)
            {
                at = i;
                break;
            }
        }
        for (int step = 1; step <= shownBefore.Count; step++)
        {
            uint next = shownBefore[((at < 0 ? -1 : at) + step + shownBefore.Count) % shownBefore.Count];
            if (next != item && available.Contains(next))
            {
                return next;
            }
        }
        foreach (uint other in available)
        {
            if (other != item && other != 0)
            {
                return other;
            }
        }
        return null;
    }

    /// <summary>The storage slot a stack of <paramref name="item"/> goes into: the slot that holds the same item (one slot per item, as the
    /// player sees it), else the first empty one; -1 when the storage is full. <paramref name="slotItems"/> holds 0 for an empty slot.</summary>
    public static int FreeIndex(IReadOnlyList<uint> slotItems, uint item)
    {
        int free = -1;
        for (int i = 0; i < slotItems.Count; i++)
        {
            if (slotItems[i] == item)
            {
                return i;
            }
            if (slotItems[i] == 0 && free < 0)
            {
                free = i;
            }
        }
        return free;
    }

    /// <summary>The HUD list: the book's entries in their order with what is stored of each, the equipped stack counted on its entry,
    /// then every row of the game's list the book does not show, in the game's order.</summary>
    public static List<(uint Item, int Count)> HudRows(IReadOnlyList<uint> shown, IReadOnlyDictionary<uint, int> stored, uint equipped, int equippedCount,
        IEnumerable<(uint Item, int Count)> gameRows)
    {
        var rows = new List<(uint Item, int Count)>();
        var listed = new HashSet<uint>();
        var game = gameRows.ToList();
        foreach (uint item in shown)
        {
            int count = stored.TryGetValue(item, out int kept) ? kept : 0;
            if (item == equipped)
            {
                // The game's own count of the equipped stack is fresher than the book's picture of it.
                int live = game.Where(row => row.Item == item).Select(row => row.Count).DefaultIfEmpty(equippedCount).First();
                count += live;
            }
            if (listed.Add(item))
            {
                rows.Add((item, count));
            }
        }
        foreach ((uint item, int count) in game)
        {
            if (listed.Add(item))
            {
                rows.Add((item, count));
            }
        }
        return rows;
    }
}
