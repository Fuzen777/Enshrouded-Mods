using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace StreamlinedLogistics;

/// <summary>Where a chest stands, in metres. A link is kept by this, because the game gives a chest a new entity id at every load.</summary>
public readonly record struct ChestPosition(double X, double Y, double Z)
{
    public double DistanceTo(ChestPosition other)
    {
        double deltaX = X - other.X;
        double deltaY = Y - other.Y;
        double deltaZ = Z - other.Z;
        return Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);
    }
}

/// <summary>One stack as an inventory holds it. Rarity is -1 when the stack has none.</summary>
public sealed record StackView(uint Holder, ulong Slot, uint Item, int Count, int Level, int Rarity, bool IsGear);

/// <summary>One line of a list: every stack of one item, or one kind of gear at one level and rarity.</summary>
public sealed record ItemRow(string Key, uint Item, string Name, long Total, int HolderCount, int Level, int Rarity, bool IsGear, bool IsFavorite, IReadOnlyList<StackView> Stacks, ulong Categories = 0, int Section = -1);

/// <summary>What a deposit needs to know about one linked chest.</summary>
public sealed record DepositChest(uint Entity, long HeldOfItem, int FreeSlots, double DistanceMeters);

/// <summary>
/// What a quick deposit needs to know about one chest for one item: how many of the item it holds, how many more its stacks of it
/// that are not full can take, its free slots, how far it is, and whether its name asks for one of the item's categories.
/// </summary>
public sealed record DepositCandidate(uint Entity, long HeldOfItem, int PartialRoom, int FreeSlots, double DistanceMeters, bool NamedForCategory, int NameRank = 0);

/// <summary>Lines that go under one header.</summary>
public sealed record RowGroup(string Title, IReadOnlyList<ItemRow> Rows);

/// <summary>What a placed container is, by the template it was made from.</summary>
public enum ChestKind
{
    /// <summary>The template could not be read.</summary>
    Unknown,
    /// <summary>Not a player storage chest: a station, a loot chest, a crop, a fruit tree.</summary>
    Other,
    Regular,
    Magic,
}

/// <summary>One slot of the player's inventories as read: the page (an inventory entity of 8 slots) and the index in it.</summary>
public readonly record struct SlotContent(uint Page, int Index, uint Item, int Count, bool HasItemEntity);

/// <summary>A backpack slot a stack can go to, and how many of the item it can still take.</summary>
public readonly record struct Destination(uint Page, int Index, int Room);

/// <summary>One inventory owner's 16 links as the game keeps them (a link of 0 ends the list), and how many of its item slots are unlocked.</summary>
public sealed record SetupRead(IReadOnlyList<uint> Links, IReadOnlyList<byte> Categories, int AvailableSlots);

/// <summary>
/// Which of the player's inventory pages are what. The character's own list of pages holds the hotbar, a first part of the backpack,
/// and the worn equipment; the backpack item it wears carries the rest of the backpack as an inventory of its own (a Virtual link).
/// </summary>
public sealed class PlayerLayout
{
    private readonly record struct PageInfo(byte Category, int FirstFlatSlot, int AvailableSlots);

    private readonly Dictionary<uint, PageInfo> _pages = new();
    private readonly List<uint> _backpackPages = new();
    private readonly List<uint> _hotbarPages = new();

    /// <summary>The backpack's pages: the character's own item pages that are not the hotbar, then the pages its Virtual links carry.</summary>
    public IReadOnlyList<uint> BackpackPages => _backpackPages;

    public IReadOnlyList<uint> HotbarPages => _hotbarPages;

    /// <summary>The hotbar's pages in the game's order: hotbar 1, then hotbar 2 (those the character has).</summary>
    public IReadOnlyList<uint> HotbarPagesInOrder { get; private set; } = Array.Empty<uint>();

    internal void OrderHotbars(uint hotbarPage1, uint hotbarPage2)
    {
        var ordered = new List<uint>(2);
        foreach (uint page in new[] { hotbarPage1, hotbarPage2 })
        {
            if (page != 0 && _hotbarPages.Contains(page) && !ordered.Contains(page))
            {
                ordered.Add(page);
            }
        }
        HotbarPagesInOrder = ordered;
    }

    internal void AddPage(uint page, byte category, int firstFlatSlot, int availableSlots, bool isHotbar)
    {
        if (_pages.ContainsKey(page))
        {
            return;
        }
        _pages.Add(page, new PageInfo(category, firstFlatSlot, availableSlots));
        if (category != ChestRules.GenericCategory)
        {
            return;
        }
        if (isHotbar)
        {
            _hotbarPages.Add(page);
        }
        else
        {
            _backpackPages.Add(page);
        }
    }

    public bool IsBackpackPage(uint page)
    {
        return _backpackPages.Contains(page);
    }

    public bool IsHotbarPage(uint page)
    {
        return _hotbarPages.Contains(page);
    }

    /// <summary>True for a page of worn things: equipment, customization or currency.</summary>
    public bool IsWornPage(uint page)
    {
        return _pages.TryGetValue(page, out PageInfo info) && info.Category != ChestRules.GenericCategory;
    }

    /// <summary>True for a backpack slot the player has unlocked: its saved number is below its owner's unlocked count.</summary>
    public bool IsUsableBackpackSlot(uint page, int index)
    {
        return IsBackpackPage(page) && index >= 0 && index < ChestRules.SlotsPerPage
            && _pages.TryGetValue(page, out PageInfo info) && info.FirstFlatSlot + index < info.AvailableSlots;
    }
}

/// <summary>
/// The favorites as one read-only set: plain items by their id, gear by its id and rarity (since 0.10.6, keys of
/// <see cref="GearKey"/>). An id among the plain ones that is gear is a favorite of an older version, for every rarity, until
/// <see cref="ChestRules.MoveOldGearFavorites"/> gives it one rarity.
/// </summary>
public sealed class FavoriteSet
{
    private readonly HashSet<uint> _items;
    private readonly HashSet<ulong> _gear;
    private readonly HashSet<uint> _gearItems = new();

    private readonly IReadOnlyDictionary<uint, uint>? _toolEntries;

    /// <param name="toolEntries">A tool entry (a hammer's shape, a staff's helper) and the tool it stands for: its favorite is the tool's.</param>
    public FavoriteSet(IEnumerable<uint> items, IEnumerable<ulong> gear, IReadOnlyDictionary<uint, uint>? toolEntries = null)
    {
        _toolEntries = toolEntries;
        _items = new HashSet<uint>(items);
        _gear = new HashSet<ulong>(gear);
        foreach (ulong key in _gear)
        {
            _gearItems.Add((uint)key);
        }
    }

    public static FavoriteSet Empty { get; } = new(Array.Empty<uint>(), Array.Empty<ulong>());

    /// <summary>True when this stack is a favorite: its item, or for gear its item at this rarity.</summary>
    public bool Contains(uint item, int rarity, bool isGear)
    {
        if (_toolEntries != null && _toolEntries.TryGetValue(item, out uint tool))
        {
            return _items.Contains(tool);
        }
        return _items.Contains(item) || (isGear && _gear.Contains(GearKey(item, rarity)));
    }

    /// <summary>True when the item is a favorite whatever its rarity (a plain item, or gear of an older version).</summary>
    public bool ContainsItem(uint item)
    {
        return _items.Contains(item);
    }

    /// <summary>The items with at least one rarity among the favorites.</summary>
    public IReadOnlySet<uint> GearItems => _gearItems;

    /// <summary>One gear favorite: the item id in the low half, the rarity plus one above it (no rarity is 0).</summary>
    public static ulong GearKey(uint item, int rarity)
    {
        return ((ulong)(byte)(rarity + 1) << 32) | item;
    }

    public static (uint Item, int Rarity) OfGearKey(ulong key)
    {
        return ((uint)key, (int)((key >> 32) & 0xFF) - 1);
    }
}

/// <summary>The kinds of gear the deposit and salvage rules name (since 0.10.26).</summary>
public enum GearKind
{
    Any = 0,
    Weapons = 1,
    Shields = 2,
    Armor = 3,
    // 4 was Cosmetics (0.10.26 only); a saved rule of it is dropped.
    Rings = 5,
}

/// <summary>A deposit or salvage rule: a kind of gear, a rarity (0 Common to 4 Legendary) and a level (0: any).</summary>
public sealed record GearRule(GearKind Kind, int Rarity, int Level);

/// <summary>The game's salvage balancing (keen::BalancingTable), from the table: the rune cost seed and creep, the Trash share, the group factors, the rune item.</summary>
public sealed record SalvageBalancing(uint Seed, float Creep, uint TrashStart, uint TrashEnd, float WeaponsShield, float Armor, float Rings, uint RuneItem);

/// <summary>What the mod keeps between sessions: the linked chests' positions, the favorite items and the screen's two switches.</summary>
public sealed class StoredState
{
    public List<ChestPosition> Links { get; } = new();
    /// <summary>Favorite plain items by id (and, from before 0.10.6, gear of every rarity).</summary>
    public HashSet<uint> Favorites { get; } = new();
    /// <summary>Favorite gear by id and rarity (<see cref="FavoriteSet.GearKey"/>), since 0.10.6.</summary>
    public HashSet<ulong> GearFavorites { get; } = new();
    /// <summary>The deposit and salvage rules (since 0.10.26); a file without them reads the defaults.</summary>
    public List<GearRule> DepositRules { get; } = new(ChestRules.DefaultDepositRules);
    public List<GearRule> SalvageRules { get; } = new(ChestRules.DefaultSalvageRules);

    public FavoriteSet FavoriteSet(IReadOnlyDictionary<uint, uint>? toolEntries = null)
    {
        return new FavoriteSet(Favorites, GearFavorites, toolEntries);
    }
    public bool GridView { get; set; } = true;
    public bool IncludeHotbar { get; set; } = true;
    /// <summary>True when the chests' content is shown chest by chest instead of as one list.</summary>
    public bool Ungrouped { get; set; }
    /// <summary>The names of the categories left out of the list.</summary>
    public HashSet<string> HiddenCategories { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>The categories the category file had when the hidden ones were last settled: one that is not here yet is new.</summary>
    public HashSet<string> KnownCategories { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>False until the hidden categories have been chosen once (by the defaults, or by the player).</summary>
    public bool HiddenCategoriesChosen { get; set; }
    /// <summary>The category tree's groups ("Materials") and sub-categories ("Gear/Weapons") left out of the list (since 0.9.0).</summary>
    public HashSet<string> HiddenTree { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>False until the tree's hidden list has been chosen once.</summary>
    public bool HiddenTreeChosen { get; set; }
    /// <summary>The tree's headers that are folded ("Favorites", "Gear", "Gear/Weapons").</summary>
    public HashSet<string> Collapsed { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>A rectangle in pixels, with its left and top edges inside and its right and bottom edges outside.</summary>
public readonly record struct PixelRect(float Left, float Top, float Right, float Bottom)
{
    public bool Contains(float x, float y) => x >= Left && x < Right && y >= Top && y < Bottom;
}

/// <summary>The mod's rules that need no game: which chest a position names, how stacks become list lines, what a search matches, where a deposit goes, and the state file.</summary>
public static class ChestRules
{
    /// <summary>A chest within this distance of a kept position is that link's chest. A save keeps positions as 32-bit floats, so a chest can come back a fraction of a millimetre off.</summary>
    public const double LinkToleranceMeters = 0.05;

    public const double MinLinkRadiusMeters = 5;
    public const double MaxLinkRadiusMeters = 500;
    public const double DefaultLinkRadiusMeters = 100;

    public const int MinHighlightSeconds = 1;
    public const int MaxHighlightSeconds = 120;
    public const int DefaultHighlightSeconds = 15;
    /// <summary>A chest farther away than this is not marked in the world.</summary>
    public const double MaxHighlightMeters = 200;

    public static double LinkRadius(double value)
    {
        if (double.IsNaN(value))
        {
            return DefaultLinkRadiusMeters;
        }
        return Math.Clamp(value, MinLinkRadiusMeters, MaxLinkRadiusMeters);
    }

    /// <summary>The index of the link that names the chest at this position, or -1.</summary>
    public static int FindLink(IReadOnlyList<ChestPosition> links, ChestPosition position)
    {
        int nearestIndex = -1;
        double nearestDistance = LinkToleranceMeters;
        for (int index = 0; index < links.Count; index++)
        {
            double distance = links[index].DistanceTo(position);
            if (distance <= nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = index;
            }
        }
        return nearestIndex;
    }

    /// <summary>Stacks of one plain item share a line. Gear shares a line only with gear of the same item, level and rarity.</summary>
    public static string RowKey(StackView stack)
    {
        return stack.IsGear
            ? string.Create(CultureInfo.InvariantCulture, $"{stack.Item}:{stack.Level}:{stack.Rarity}")
            : stack.Item.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>The list lines for these stacks: favorites first, then by name, then the higher level first.</summary>
    public static IReadOnlyList<ItemRow> BuildRows(IEnumerable<StackView> stacks, Func<uint, string> nameOfItem, IReadOnlySet<uint> favorites)
    {
        return BuildRows(stacks, nameOfItem, favorites, null);
    }

    /// <summary>The same, with each line's categories from <paramref name="categoriesOfItem"/> (bits of a <see cref="CategoryTable"/>).</summary>
    public static IReadOnlyList<ItemRow> BuildRows(IEnumerable<StackView> stacks, Func<uint, string> nameOfItem, IReadOnlySet<uint> favorites, Func<uint, ulong>? categoriesOfItem)
    {
        return BuildRows(stacks, nameOfItem, new FavoriteSet(favorites, Array.Empty<ulong>()), categoriesOfItem);
    }

    /// <summary>The same, with gear favorites told apart by rarity.</summary>
    public static IReadOnlyList<ItemRow> BuildRows(IEnumerable<StackView> stacks, Func<uint, string> nameOfItem, FavoriteSet favorites, Func<uint, ulong>? categoriesOfItem = null)
    {
        var stacksByKey = new Dictionary<string, List<StackView>>(StringComparer.Ordinal);
        foreach (StackView stack in stacks)
        {
            if (stack.Count <= 0)
            {
                continue;
            }
            string key = RowKey(stack);
            if (!stacksByKey.TryGetValue(key, out List<StackView>? sameKey))
            {
                sameKey = new List<StackView>();
                stacksByKey.Add(key, sameKey);
            }
            sameKey.Add(stack);
        }

        var rows = new List<ItemRow>(stacksByKey.Count);
        foreach (KeyValuePair<string, List<StackView>> entry in stacksByKey)
        {
            StackView first = entry.Value[0];
            long total = 0;
            var holders = new HashSet<uint>();
            foreach (StackView stack in entry.Value)
            {
                total += stack.Count;
                holders.Add(stack.Holder);
            }
            rows.Add(new ItemRow(entry.Key, first.Item, nameOfItem(first.Item), total, holders.Count, first.Level, first.Rarity, first.IsGear, favorites.Contains(first.Item, first.Rarity, first.IsGear), entry.Value,
                categoriesOfItem?.Invoke(first.Item) ?? 0));
        }
        rows.Sort(CompareRows);
        return rows;
    }

    private static int CompareRows(ItemRow left, ItemRow right)
    {
        if (left.IsFavorite != right.IsFavorite)
        {
            return left.IsFavorite ? -1 : 1;
        }
        int byName = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        if (byName != 0)
        {
            return byName;
        }
        if (left.Level != right.Level)
        {
            return right.Level.CompareTo(left.Level);
        }
        if (left.Rarity != right.Rarity)
        {
            return right.Rarity.CompareTo(left.Rarity);
        }
        return string.CompareOrdinal(left.Key, right.Key);
    }

    /// <summary>True when the name holds every word of the search, in any order and any case. An empty search matches everything.</summary>
    public static bool Matches(string name, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }
        foreach (string word in search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!name.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>The chests to try for a deposit, in order: those already holding the item (the most first), then those with a free slot (the nearest first). A chest with neither is left out.</summary>
    public static IReadOnlyList<uint> DepositOrder(IReadOnlyList<DepositChest> chests)
    {
        var candidates = new List<DepositChest>();
        foreach (DepositChest chest in chests)
        {
            if (chest.HeldOfItem > 0 || chest.FreeSlots > 0)
            {
                candidates.Add(chest);
            }
        }
        candidates.Sort((left, right) =>
        {
            bool leftHolds = left.HeldOfItem > 0;
            bool rightHolds = right.HeldOfItem > 0;
            if (leftHolds != rightHolds)
            {
                return leftHolds ? -1 : 1;
            }
            if (left.HeldOfItem != right.HeldOfItem)
            {
                return right.HeldOfItem.CompareTo(left.HeldOfItem);
            }
            int byDistance = left.DistanceMeters.CompareTo(right.DistanceMeters);
            return byDistance != 0 ? byDistance : left.Entity.CompareTo(right.Entity);
        });
        var order = new List<uint>(candidates.Count);
        foreach (DepositChest chest in candidates)
        {
            order.Add(chest.Entity);
        }
        return order;
    }

    public const int SlotsPerPage = 8;
    /// <summary>keen::InventoryCategory.Generic: a page of ordinary item slots.</summary>
    public const byte GenericCategory = 4;

    // The game's player storage chests, by template id (the game's data, build 26430625).
    private static readonly Dictionary<Guid, ChestKind> StorageTemplates = new()
    {
        [new Guid("669facd1-dc9a-41ab-8fa5-6b9f98c6a382")] = ChestKind.Regular, // Prop_Decoration_T0_Storage_16
        [new Guid("65baa890-022a-454b-9cb7-b92cb8c06cd4")] = ChestKind.Regular, // Prop_Decoration_T1_Storage_24
        [new Guid("b0dd7d8a-1e69-4518-b52f-e94cfa769ebc")] = ChestKind.Regular, // Prop_Decoration_T2_Storage_32
        [new Guid("d47db835-fe54-44fd-b5b3-d6c340673c41")] = ChestKind.Regular, // Prop_Decoration_T2_Storage_32_Chest
        [new Guid("bdebd6a9-9e59-4c65-b056-ad0070db97dc")] = ChestKind.Regular, // Prop_Decoration_T3_Storage_40
        [new Guid("fae7a62e-bc70-4d64-b55a-8d222c152f3a")] = ChestKind.Regular, // Prop_Decoration_T4_Storage_56
        [new Guid("0c667e7e-0fbd-4cb9-b58d-c3a20df7eaec")] = ChestKind.Regular, // Prop_Decoration_T5_Storage_40_Chest
        [new Guid("955ba32f-08ea-414d-bd98-ac75990aef04")] = ChestKind.Magic,   // Prop_Decoration_T1_Storage_24_Magic
        [new Guid("35258d2b-393e-4d04-9392-bd990be5ae61")] = ChestKind.Magic,   // Prop_Decoration_T2_Storage_32_Magic
        [new Guid("439225f1-191d-415c-a335-013419b202e7")] = ChestKind.Magic,   // Prop_Decoration_T3_Storage_40_Magic
        [new Guid("bc9da485-1665-46fa-aa45-c40911719925")] = ChestKind.Magic,   // Prop_Decoration_T4_Storage_56_Magic
        [new Guid("bc834cbd-58f1-494a-97fb-0dc0248eb738")] = ChestKind.Magic,   // Prop_Decoration_T6_Storage_72_Magic
    };

    /// <summary>What a container is, from its template's id, else from its template's name (a chest a later game version adds).</summary>
    public static ChestKind KindOfChest(Guid? template, string? templateName)
    {
        if (template is Guid id && StorageTemplates.TryGetValue(id, out ChestKind known))
        {
            return known;
        }
        if (templateName != null)
        {
            if (templateName.StartsWith("Prop_Decoration_T", StringComparison.Ordinal) && templateName.Contains("_Storage_", StringComparison.Ordinal))
            {
                return templateName.EndsWith("_Magic", StringComparison.Ordinal) ? ChestKind.Magic : ChestKind.Regular;
            }
            return ChestKind.Other;
        }
        return template == null ? ChestKind.Unknown : ChestKind.Other;
    }

    /// <summary>keen::InventoryCategory.Virtual: a link to another inventory owner, such as the backpack item the character wears.</summary>
    public const byte VirtualCategory = 5;
    private const int MaxLayoutDepth = 3;

    /// <summary>
    /// The player's pages sorted out. <paramref name="root"/> is the character's own setup; <paramref name="carried"/> holds the setup of
    /// each owner a Virtual link names. Within one owner a slot's saved number is 8 x (the Generic links before its page) + its index,
    /// and the slots numbered below that owner's unlocked count are usable.
    /// </summary>
    public static PlayerLayout BuildLayout(SetupRead root, IReadOnlyDictionary<uint, SetupRead> carried, uint hotbarPage1, uint hotbarPage2)
    {
        var layout = new PlayerLayout();
        AddSetup(layout, root, carried, hotbarPage1, hotbarPage2, 0);
        layout.OrderHotbars(hotbarPage1, hotbarPage2);
        return layout;
    }

    private static void AddSetup(PlayerLayout layout, SetupRead setup, IReadOnlyDictionary<uint, SetupRead> carried, uint hotbarPage1, uint hotbarPage2, int depth)
    {
        int genericLinksBefore = 0;
        for (int link = 0; link < setup.Links.Count && link < setup.Categories.Count; link++)
        {
            uint page = setup.Links[link];
            if (page == 0)
            {
                break;
            }
            byte category = setup.Categories[link];
            if (category == VirtualCategory)
            {
                if (depth < MaxLayoutDepth && carried.TryGetValue(page, out SetupRead? inner))
                {
                    AddSetup(layout, inner, carried, hotbarPage1, hotbarPage2, depth + 1);
                }
                continue;
            }
            int firstFlatSlot = 0;
            if (category == GenericCategory)
            {
                firstFlatSlot = genericLinksBefore * SlotsPerPage;
                genericLinksBefore++;
            }
            layout.AddPage(page, category, firstFlatSlot, setup.AvailableSlots, page == hotbarPage1 || page == hotbarPage2);
        }
    }

    /// <summary>
    /// Where in the backpack a stack of this item goes: a stack of the same item that is not full, else the first empty unlocked slot.
    /// Null when the backpack has no room. Gear never shares a slot. A <paramref name="maxStack"/> of 0 (unknown) only takes empty slots.
    /// </summary>
    public static Destination? ChooseBackpackSlot(PlayerLayout layout, IReadOnlyList<SlotContent> slots, uint item, int maxStack, bool isGear)
    {
        return ChooseSlot(layout.BackpackPages, layout.IsUsableBackpackSlot, slots, item, maxStack, isGear);
    }

    /// <summary>
    /// Where in the hotbars a stack dropped on them goes (since 0.10.10): hotbar 1, then hotbar 2, each by the backpack's rule (a
    /// stack of the item that is not full, else the first empty slot). Null when both are full: the backpack is next.
    /// </summary>
    public static Destination? ChooseHotbarSlot(PlayerLayout layout, IReadOnlyList<SlotContent> slots, uint item, int maxStack, bool isGear)
    {
        foreach (uint page in layout.HotbarPagesInOrder)
        {
            if (ChooseSlot(new[] { page }, (_, index) => index >= 0 && index < SlotsPerPage, slots, item, maxStack, isGear) is Destination destination)
            {
                return destination;
            }
        }
        return null;
    }

    private static Destination? ChooseSlot(IReadOnlyList<uint> pages, Func<uint, int, bool> isUsable, IReadOnlyList<SlotContent> slots, uint item, int maxStack, bool isGear)
    {
        Destination? firstEmpty = null;
        foreach (uint page in pages)
        {
            for (int index = 0; index < SlotsPerPage; index++)
            {
                SlotContent? found = null;
                foreach (SlotContent slot in slots)
                {
                    if (slot.Page == page && slot.Index == index)
                    {
                        found = slot;
                        break;
                    }
                }
                if (found is not SlotContent content)
                {
                    continue;
                }
                bool isEmpty = content.Item == 0 || content.Count <= 0;
                if (isEmpty)
                {
                    if (firstEmpty == null && isUsable(page, index))
                    {
                        firstEmpty = new Destination(page, index, isGear ? 1 : (maxStack > 0 ? maxStack : int.MaxValue));
                    }
                    continue;
                }
                if (!isGear && maxStack > 0 && content.Item == item && !content.HasItemEntity && content.Count < maxStack)
                {
                    return new Destination(page, index, maxStack - content.Count);
                }
            }
        }
        return firstEmpty;
    }

    /// <summary>
    /// True for a stack that sits where it can't belong: a plain material (not gear) in a page of worn things. Version 0.1.0 could
    /// leave a taken stack there, in an equipment slot that happened to be empty.
    /// </summary>
    public static bool IsStranded(PlayerLayout layout, uint page, bool isGear, bool isMaterial)
    {
        return layout.IsWornPage(page) && !isGear && isMaterial;
    }

    public const string FavoritesTitle = "Favorites";
    public const string UncategorizedTitle = "Other";

    /// <summary>The place of an item's category in the category file (its first one when it is in several), or -1 when it is in none.</summary>
    public static int FirstCategory(ulong categories)
    {
        return categories == 0 ? -1 : BitOperations.TrailingZeroCount(categories);
    }

    /// <summary>
    /// The lines under headers: the favorites first, then one group per category in the category file's order, then what is in no
    /// category. A line is in one group only, and a group without lines is left out. Inside a group the lines keep their order.
    /// </summary>
    public static IReadOnlyList<RowGroup> GroupByCategory(IReadOnlyList<ItemRow> rows, IReadOnlyList<string> categoryNames)
    {
        var favorites = new List<ItemRow>();
        var uncategorized = new List<ItemRow>();
        var byCategory = new List<ItemRow>[categoryNames.Count];
        foreach (ItemRow row in rows)
        {
            int category = FirstCategory(row.Categories);
            if (row.IsFavorite)
            {
                favorites.Add(row);
            }
            else if (category < 0 || category >= categoryNames.Count)
            {
                uncategorized.Add(row);
            }
            else
            {
                (byCategory[category] ??= new List<ItemRow>()).Add(row);
            }
        }
        var groups = new List<RowGroup>();
        if (favorites.Count > 0)
        {
            groups.Add(new RowGroup(FavoritesTitle, favorites));
        }
        for (int category = 0; category < byCategory.Length; category++)
        {
            if (byCategory[category] != null)
            {
                groups.Add(new RowGroup(categoryNames[category], byCategory[category]));
            }
        }
        if (uncategorized.Count > 0)
        {
            groups.Add(new RowGroup(UncategorizedTitle, uncategorized));
        }
        return groups;
    }

    /// <summary>The same order without headers, for one chest's lines: favorites, then category by category, then what is in none.</summary>
    public static IReadOnlyList<ItemRow> OrderByCategory(IReadOnlyList<ItemRow> rows, IReadOnlyList<string> categoryNames)
    {
        var ordered = new List<ItemRow>(rows.Count);
        foreach (RowGroup group in GroupByCategory(rows, categoryNames))
        {
            ordered.AddRange(group.Rows);
        }
        return ordered;
    }

    /// <summary>
    /// True for an item that the hidden categories leave out of the list: it is in at least one category and every one of them is hidden.
    /// An item in no category at all is never hidden.
    /// </summary>
    public static bool IsHiddenByCategory(ulong itemCategories, ulong hiddenCategories)
    {
        return itemCategories != 0 && (itemCategories & ~hiddenCategories) == 0;
    }

    // The stack a used item comes from: the smallest one, so that a slot is freed soonest.
    public static StackView? SmallestStack(IReadOnlyList<StackView> stacks)
    {
        StackView? smallest = null;
        foreach (StackView stack in stacks)
        {
            if (stack.Count > 0 && (smallest == null || stack.Count < smallest.Count))
            {
                smallest = stack;
            }
        }
        return smallest;
    }

    // A duration as the game writes it in a tooltip: "45s", "20min", "1min 30s", "1h", "1h 30min".
    public static string FormatDuration(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0)
        {
            return "0s";
        }
        long total = (long)Math.Round(seconds);
        long hours = total / 3600;
        long minutes = total % 3600 / 60;
        long rest = total % 60;
        var parts = new List<string>();
        if (hours > 0)
        {
            parts.Add(hours.ToString(CultureInfo.InvariantCulture) + "h");
        }
        if (minutes > 0)
        {
            parts.Add(minutes.ToString(CultureInfo.InvariantCulture) + "min");
        }
        if (rest > 0 && hours == 0)
        {
            parts.Add(rest.ToString(CultureInfo.InvariantCulture) + "s");
        }
        return parts.Count > 0 ? string.Join(" ", parts) : "0s";
    }

    // One line of an item's details: "Label: value", or the one of the two that is there.
    public static string DetailLine(string? label, string value)
    {
        string name = (label ?? "").Trim();
        string number = value.Trim();
        if (name.Length == 0)
        {
            return number;
        }
        return number.Length == 0 ? name : name + ": " + number;
    }

    // The game's rarities, in Kiln's ItemRarity order, for the Quick Deposit filters.
    public static readonly IReadOnlyList<string> RarityNames = new[] { "Common", "Uncommon", "Rare", "Epic", "Legendary" };

    // The Auto-Name Chests setting's choices, in its config value's order (0, 1, 2).
    public static readonly IReadOnlyList<string> AutoNameChoices = new[] { "Off", "Children", "Parent" };
    public const int AutoNameChildren = 1;
    public const int AutoNameParent = 2;
    // ---- Deposit and salvage rules (since 0.10.26, replacing 0.10.5's rarity filters). Only gear is ruled: weapons, shields,
    // armor, cosmetics and rings, by their sub-category; every other item is always deposited and never salvaged. ----

    // The rule kinds in the lists' order (cosmetics are no rule kind since 0.10.27: always deposited, never salvaged).
    public static readonly IReadOnlyList<GearKind> RuleKinds = new[] { GearKind.Any, GearKind.Weapons, GearKind.Shields, GearKind.Armor, GearKind.Rings };
    public static readonly IReadOnlyList<string> GearKindNames = new[] { "Any", "Weapons", "Shields", "Armor", "Rings" };
    public static readonly IReadOnlyList<GearRule> DefaultDepositRules = new[] { new GearRule(GearKind.Any, 4, 0) };
    public static readonly IReadOnlyList<GearRule> DefaultSalvageRules = new[] { new GearRule(GearKind.Any, 0, 0) };
    public const int MaxRuleLevel = 60;
    public const int MaxRules = 12;

    /// <summary>The rule kind of a sub-category of the tree, or null for what no rule covers.</summary>
    public static GearKind? GearKindOfSection(string? sectionKey)
    {
        return sectionKey switch
        {
            "Gear/Weapons" => GearKind.Weapons,
            "Gear/Shields" => GearKind.Shields,
            "Gear/Armor" => GearKind.Armor,
            "Gear/Rings" => GearKind.Rings,
            _ => null,
        };
    }

    // A deposit rule takes gear of its kind of its rarity or better and of its level or more. A stack with no rarity is Common.
    public static bool DepositAllowed(GearKind kind, int rarity, int level, IReadOnlyList<GearRule> rules)
    {
        foreach (GearRule rule in rules)
        {
            if ((rule.Kind == GearKind.Any || rule.Kind == kind) && Math.Max(rarity, 0) >= rule.Rarity && level >= rule.Level)
            {
                return true;
            }
        }
        return false;
    }

    // A salvage rule takes gear of its kind of its rarity or worse and of its level or less (0: any level).
    public static bool SalvageWanted(GearKind kind, int rarity, int level, IReadOnlyList<GearRule> rules)
    {
        foreach (GearRule rule in rules)
        {
            if ((rule.Kind == GearKind.Any || rule.Kind == kind) && Math.Max(rarity, 0) <= rule.Rarity && (rule.Level == 0 || level <= rule.Level))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The salvage balancing written by the table script ("salvage"), or null.</summary>
    public static SalvageBalancing? ParseSalvage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            if (!document.RootElement.TryGetProperty("salvage", out JsonElement s) || s.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            return new SalvageBalancing(s.GetProperty("seed").GetUInt32(), s.GetProperty("creep").GetSingle(), s.GetProperty("trashStart").GetUInt32(),
                s.GetProperty("trashEnd").GetUInt32(), s.GetProperty("weaponsShield").GetSingle(), s.GetProperty("armor").GetSingle(),
                s.GetProperty("rings").GetSingle(), s.GetProperty("runeItem").GetUInt32());
        }
        catch (Exception problem) when (problem is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    // The game's Trash yield of a piece of gear in runes. Ported from Kiln Auto Loot's SalvageFormula (MIT, Copyright (c) 2026
    // Frostdev), which reads the same BalancingTable: the upgrade cost of the levels the perk slots carry, plus a share of the
    // unlocked slots' cost, the share growing from trashStart to trashEnd percent with the level.
    public static int SalvageRunes(int level, int availablePerks, int unlockedPerks, GearKind kind, SalvageBalancing balancing, float perkCostFactor = 1f, float recyclingFactor = 0.5f)
    {
        float groupFactor = kind switch
        {
            GearKind.Weapons or GearKind.Shields => balancing.WeaponsShield,
            GearKind.Armor => balancing.Armor,
            GearKind.Rings => balancing.Rings,
            _ => 1f,
        };
        int Cost(int n) => RoundHalfAway((balancing.Seed * balancing.Creep * n + balancing.Seed) * perkCostFactor * groupFactor);
        int carried = Cost(Math.Max(level - unlockedPerks, 1)) * availablePerks;
        int trashed = 0;
        if (unlockedPerks > 0)
        {
            float share = (balancing.TrashEnd - balancing.TrashStart) / 100f * level + balancing.TrashStart;
            trashed = RoundHalfAway(Cost(level) * share) * unlockedPerks;
        }
        return RoundHalfAway(trashed * recyclingFactor) + carried;
    }

    private static int RoundHalfAway(float value)
    {
        return value >= 0 ? (int)(value + 0.5f) : (int)(value - 0.5f);
    }

    // Marks or unmarks a favorite: a plain item by its id, gear by its id and rarity. Unmarking gear also ends an older version's
    // favorite of that item (every rarity).
    public static void SetFavorite(StoredState state, uint item, int rarity, bool isGear, bool favorite)
    {
        if (!isGear)
        {
            if (favorite)
            {
                state.Favorites.Add(item);
            }
            else
            {
                state.Favorites.Remove(item);
            }
            return;
        }
        ulong key = FavoriteSet.GearKey(item, rarity);
        if (favorite)
        {
            state.GearFavorites.Add(key);
        }
        else
        {
            state.GearFavorites.Remove(key);
            state.Favorites.Remove(item);
        }
    }

    // Favorites of an older version that are gear (one id for every rarity) become favorites of the highest rarity among these
    // stacks. Returns how many moved; one not among the stacks stays as it is.
    public static int MoveOldGearFavorites(StoredState state, IEnumerable<StackView> stacks)
    {
        if (state.Favorites.Count == 0)
        {
            return 0;
        }
        var highest = new Dictionary<uint, int>();
        foreach (StackView stack in stacks)
        {
            if (stack.IsGear && stack.Count > 0 && state.Favorites.Contains(stack.Item))
            {
                highest[stack.Item] = highest.TryGetValue(stack.Item, out int seen) ? Math.Max(seen, stack.Rarity) : stack.Rarity;
            }
        }
        foreach (KeyValuePair<uint, int> entry in highest)
        {
            state.Favorites.Remove(entry.Key);
            state.GearFavorites.Add(FavoriteSet.GearKey(entry.Key, entry.Value));
        }
        return highest.Count;
    }

    // After a quick deposit, the items no chest took under the chest name that would take them: "No chest named Ores (Iron Ore,
    // Copper Ore), Weapons (Iron Sword). Full: Tools (Rope)." A name some chest already has means those chests are full; an item
    // with no name to give stays apart. Null when everything found a chest.
    public static string? MissingChestNames(IReadOnlyList<(string ItemName, string ChestName, bool NamedChestExists)> stayed, int maxNames = 4, int maxItemsPerName = 3)
    {
        var missing = new List<(string Name, List<string> Items)>();
        var full = new List<(string Name, List<string> Items)>();
        var unnamed = new List<string>();
        foreach ((string itemName, string chestName, bool exists) in stayed)
        {
            if (chestName.Length == 0)
            {
                unnamed.Add(itemName);
                continue;
            }
            List<(string Name, List<string> Items)> groups = exists ? full : missing;
            int index = groups.FindIndex(group => string.Equals(group.Name, chestName, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                groups.Add((chestName, new List<string>()));
                index = groups.Count - 1;
            }
            groups[index].Items.Add(itemName);
        }
        string Items(List<string> items)
        {
            string shown = string.Join(", ", items.GetRange(0, Math.Min(items.Count, maxItemsPerName)));
            return items.Count > maxItemsPerName ? string.Create(CultureInfo.InvariantCulture, $"{shown} +{items.Count - maxItemsPerName}") : shown;
        }
        string Groups(List<(string Name, List<string> Items)> groups)
        {
            var parts = new List<string>();
            for (int index = 0; index < groups.Count && index < maxNames; index++)
            {
                parts.Add($"{groups[index].Name} ({Items(groups[index].Items)})");
            }
            string text = string.Join(", ", parts);
            return groups.Count > maxNames ? string.Create(CultureInfo.InvariantCulture, $"{text}, and {groups.Count - maxNames} more") : text;
        }
        var sentences = new List<string>();
        if (missing.Count > 0)
        {
            sentences.Add("No chest named " + Groups(missing) + ".");
        }
        if (full.Count > 0)
        {
            sentences.Add("Full: " + Groups(full) + ".");
        }
        if (unnamed.Count > 0)
        {
            sentences.Add("No chest for " + Items(unnamed) + ".");
        }
        return sentences.Count > 0 ? string.Join(" ", sentences) : null;
    }

    // The backpack's empty unlocked slots (Auto Quick Deposit starts when there are none left).
    public static int BackpackFreeSlots(PlayerLayout layout, IReadOnlyList<SlotContent> slots)
    {
        int free = 0;
        foreach (SlotContent slot in slots)
        {
            if ((slot.Item == 0 || slot.Count <= 0) && layout.IsUsableBackpackSlot(slot.Page, slot.Index))
            {
                free++;
            }
        }
        return free;
    }

    private static List<int[]> RuleRows(IReadOnlyList<GearRule> rules)
    {
        var rows = new List<int[]>(rules.Count);
        foreach (GearRule rule in rules)
        {
            rows.Add(new[] { (int)rule.Kind, rule.Rarity, rule.Level });
        }
        return rows;
    }

    // A file that names the rules replaces the defaults, also with none (the player removed them all).
    private static void ReadRules(JsonElement root, string name, List<GearRule> rules)
    {
        if (!root.TryGetProperty(name, out JsonElement list) || list.ValueKind != JsonValueKind.Array)
        {
            return;
        }
        rules.Clear();
        foreach (JsonElement row in list.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.Array && row.GetArrayLength() == 3)
            {
                int kind = row[0].GetInt32();
                if (Enum.IsDefined(typeof(GearKind), kind))
                {
                    rules.Add(new GearRule((GearKind)kind, Math.Clamp(row[1].GetInt32(), 0, RarityNames.Count - 1), Math.Clamp(row[2].GetInt32(), 0, MaxRuleLevel)));
                }
            }
        }
    }

    // The same rows with the favorites moved to the top, each part keeping its order.
    public static IReadOnlyList<ItemRow> FavoritesFirst(IReadOnlyList<ItemRow> rows)
    {
        var ordered = new List<ItemRow>(rows.Count);
        foreach (ItemRow row in rows)
        {
            if (row.IsFavorite)
            {
                ordered.Add(row);
            }
        }
        foreach (ItemRow row in rows)
        {
            if (!row.IsFavorite)
            {
                ordered.Add(row);
            }
        }
        return ordered;
    }

    // A name without its last word when that word is a numeral ("Skull Summoning Vessel II" gives "Skull Summoning Vessel"), so that
    // one item's ranks sit together in a sorted list.
    public static string NameFamily(string? name)
    {
        string trimmed = (name ?? "").Trim();
        int space = trimmed.LastIndexOf(' ');
        if (space <= 0)
        {
            return trimmed;
        }
        string last = trimmed[(space + 1)..];
        bool digits = true;
        bool roman = true;
        foreach (char letter in last)
        {
            digits &= char.IsAsciiDigit(letter);
            roman &= letter is 'I' or 'V' or 'X' or 'L' or 'C';
        }
        return digits || roman ? trimmed[..space].TrimEnd() : trimmed;
    }

    // The game's markup taken out of a text ("<b>", "<lorename>", ...), with its spaces kept tidy.
    public static string PlainText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }
        var plain = new System.Text.StringBuilder(text.Length);
        int index = 0;
        while (index < text.Length)
        {
            // A tag starts with a letter or "/" and holds no space: "2 < 3 and 4 > 1" is no tag.
            if (text[index] == '<' && index + 1 < text.Length && (char.IsLetter(text[index + 1]) || text[index + 1] == '/'))
            {
                int close = text.IndexOf('>', index + 1);
                if (close > index && close - index <= 65 && text.IndexOfAny(new[] { '<', ' ' }, index + 1, close - index - 1) < 0)
                {
                    index = close + 1;
                    continue;
                }
            }
            plain.Append(text[index]);
            index++;
        }
        return plain.ToString().Trim();
    }

    // Where the game draws a slot, in pixels of a target (the overlay, or the window's client area). The game gives a slot's
    // rectangle in its own UI units; a pixel of the game's picture is viewport + unit x pixels per unit, and the picture
    // (the viewport with the same margin on both sides) is stretched over the target. Kiln 0.2.0-dev.30.1 divides the
    // target by the canvas instead, which is in units, so its own marks and clicks land at twice the place on a 4K screen.
    public static PixelRect? SlotPixels(float unitX, float unitY, float unitWidth, float unitHeight, float viewportX, float viewportY,
        float viewportWidth, float viewportHeight, float pixelsPerUnit, float targetWidth, float targetHeight)
    {
        float pictureWidth = viewportWidth + 2 * viewportX;
        float pictureHeight = viewportHeight + 2 * viewportY;
        bool usable = pixelsPerUnit > 0 && viewportX >= 0 && viewportY >= 0 && viewportWidth > 0 && viewportHeight > 0 && targetWidth > 0 && targetHeight > 0
            && unitWidth > 0 && unitHeight > 0 && float.IsFinite(unitX) && float.IsFinite(unitY) && float.IsFinite(unitWidth) && float.IsFinite(unitHeight)
            && float.IsFinite(pixelsPerUnit) && float.IsFinite(pictureWidth) && float.IsFinite(pictureHeight);
        if (!usable)
        {
            return null;
        }
        float acrossScale = targetWidth / pictureWidth;
        float downScale = targetHeight / pictureHeight;
        float left = (viewportX + unitX * pixelsPerUnit) * acrossScale;
        float top = (viewportY + unitY * pixelsPerUnit) * downScale;
        return new PixelRect(left, top, left + unitWidth * pixelsPerUnit * acrossScale, top + unitHeight * pixelsPerUnit * downScale);
    }

    /// <summary>A count short enough for a grid cell: 950, 9,999, 12.3k, 150k, 2.5M.</summary>
    public static string ShortCount(long count)
    {
        if (count < 10_000)
        {
            return count.ToString("N0", CultureInfo.InvariantCulture);
        }
        if (count < 100_000)
        {
            return (Math.Floor(count / 100.0) / 10).ToString("0.#", CultureInfo.InvariantCulture) + "k";
        }
        if (count < 1_000_000)
        {
            return (count / 1000).ToString(CultureInfo.InvariantCulture) + "k";
        }
        return (Math.Floor(count / 100_000.0) / 10).ToString("0.#", CultureInfo.InvariantCulture) + "M";
    }

    /// <summary>What the Chests column shows: the chest's name when one chest holds the item, else how many chests do.</summary>
    public static string HoldersText(IReadOnlyList<string> chestNames)
    {
        return chestNames.Count == 1 ? chestNames[0] : string.Create(CultureInfo.InvariantCulture, $"{chestNames.Count} chests");
    }

    /// <summary>
    /// A quick deposit's first step for one item: the chests whose stacks of it are not full, the one holding the most first, then the
    /// nearest. Only those stacks are filled in this step, so nothing takes a new slot yet.
    /// </summary>
    /// <summary>
    /// The chests a deposit may use (since 0.10.15): when some chest's name asks for the item, only those of the highest rank
    /// (<see cref="CategoryTree.SectionRanksOfChestName"/>: a chest named for the exact sub-category before one sharing a word
    /// with it); when none does, all of them.
    /// </summary>
    public static IReadOnlyList<DepositCandidate> BestNamed(IReadOnlyList<DepositCandidate> chests)
    {
        // The best rank among the named chests with room for the item; when they are all full, the best rank of all (since
        // 0.10.17: a full "Weapons" chest lets "Gear" take the rest).
        int best = 0;
        int bestWithRoom = 0;
        foreach (DepositCandidate chest in chests)
        {
            int rank = chest.NamedForCategory ? Math.Max(chest.NameRank, 1) : 0;
            best = Math.Max(best, rank);
            if (chest.FreeSlots > 0 || chest.PartialRoom > 0)
            {
                bestWithRoom = Math.Max(bestWithRoom, rank);
            }
        }
        best = bestWithRoom > 0 ? bestWithRoom : best;
        if (best == 0)
        {
            return chests;
        }
        var kept = new List<DepositCandidate>();
        foreach (DepositCandidate chest in chests)
        {
            if (chest.NamedForCategory && Math.Max(chest.NameRank, 1) == best)
            {
                kept.Add(chest);
            }
        }
        return kept;
    }

    public static IReadOnlyList<DepositCandidate> TopUpOrder(IReadOnlyList<DepositCandidate> chests)
    {
        var withRoom = new List<DepositCandidate>();
        foreach (DepositCandidate chest in chests)
        {
            if (chest.HeldOfItem > 0 && chest.PartialRoom > 0)
            {
                withRoom.Add(chest);
            }
        }
        withRoom.Sort(CompareByHeldThenNearest);
        return withRoom;
    }

    /// <summary>
    /// A quick deposit's second step for what is left of a stack: a chest named for the item's category, else a chest that already
    /// holds the item; among several, the one holding the most of it, then the nearest. The chest needs a free slot. 0 when no chest
    /// qualifies: the stack then stays in the backpack, since a guess could put it where it is not wanted.
    /// </summary>
    public static uint PlaceChest(IReadOnlyList<DepositCandidate> chests)
    {
        DepositCandidate? best = null;
        foreach (DepositCandidate chest in chests)
        {
            if (chest.NamedForCategory && chest.FreeSlots > 0 && (best == null || CompareByHeldThenNearest(chest, best) < 0))
            {
                best = chest;
            }
        }
        if (best != null)
        {
            return best.Entity;
        }
        foreach (DepositCandidate chest in chests)
        {
            if (chest.HeldOfItem > 0 && chest.FreeSlots > 0 && (best == null || CompareByHeldThenNearest(chest, best) < 0))
            {
                best = chest;
            }
        }
        return best?.Entity ?? 0;
    }

    private static int CompareByHeldThenNearest(DepositCandidate left, DepositCandidate right)
    {
        if (left.HeldOfItem != right.HeldOfItem)
        {
            return right.HeldOfItem.CompareTo(left.HeldOfItem);
        }
        int byDistance = left.DistanceMeters.CompareTo(right.DistanceMeters);
        return byDistance != 0 ? byDistance : left.Entity.CompareTo(right.Entity);
    }

    /// <summary>
    /// Takes in the categories the state has not seen yet: each becomes known, and one that is hidden by default is hidden.
    /// So a category added to the file later starts out like the others of its sort, and the player's own choices stay.
    /// Returns the names it hid. A state from before this was kept knows no category, so every default applies once.
    /// </summary>
    public static IReadOnlyList<string> AdoptNewCategories(StoredState state, IReadOnlyList<string> names, IReadOnlyList<string> hiddenByDefault)
    {
        var newlyHidden = new List<string>();
        foreach (string name in names)
        {
            if (!state.KnownCategories.Add(name))
            {
                continue;
            }
            foreach (string hidden in hiddenByDefault)
            {
                if (string.Equals(hidden, name, StringComparison.OrdinalIgnoreCase) && state.HiddenCategories.Add(name))
                {
                    newlyHidden.Add(name);
                }
            }
        }
        return newlyHidden;
    }

    /// <summary>
    /// True for a slot that shows what a tool has selected instead of the tool: the Construction Hammer's slot reads as its shape
    /// (Blueprint_...), the summoning staff's as its helper (NPC_...). Such a line is no item to store.
    /// </summary>
    public static bool IsToolEntry(string? debugName)
    {
        if (string.IsNullOrEmpty(debugName))
        {
            return false;
        }
        return ToolKeyOf(debugName) != 0;
    }

    // The tools whose slot holds what they have selected (since 0.10.19): a favorite of the Construction Hammer or the summoning
    // staff is kept under one of these ids, which no item of the game has, since the slot's item changes with every selection.
    public const uint ConstructionHammerKey = 0xFFFFFF01;
    public const uint SummoningStaffKey = 0xFFFFFF02;
    // Since 0.10.21: the Overgrowth and Decay tools hold their brush (Building_Overgrowth_Shape_*, Building_Decay_Shape_*).
    public const uint OvergrowthToolKey = 0xFFFFFF03;
    public const uint DecayToolKey = 0xFFFFFF04;

    /// <summary>The tool a tool entry stands for (one of the keys above), or 0 for an ordinary item.</summary>
    public static uint ToolKeyOf(string? debugName)
    {
        if (string.IsNullOrEmpty(debugName))
        {
            return 0;
        }
        if (debugName.StartsWith("NPC_", StringComparison.OrdinalIgnoreCase))
        {
            return SummoningStaffKey;
        }
        if (debugName.StartsWith("Building_Overgrowth_Shape", StringComparison.OrdinalIgnoreCase))
        {
            return OvergrowthToolKey;
        }
        if (debugName.StartsWith("Building_Decay_Shape", StringComparison.OrdinalIgnoreCase))
        {
            return DecayToolKey;
        }
        return debugName.StartsWith("Blueprint", StringComparison.OrdinalIgnoreCase) || debugName.StartsWith("_no_blueprint", StringComparison.OrdinalIgnoreCase)
            ? ConstructionHammerKey : 0;
    }

    public static bool IsToolKey(uint item)
    {
        return item is ConstructionHammerKey or SummoningStaffKey or OvergrowthToolKey or DecayToolKey;
    }

    public static string ToolName(uint toolKey)
    {
        return toolKey switch
        {
            SummoningStaffKey => "Summoning Staff",
            OvergrowthToolKey => "Overgrowth Tool",
            DecayToolKey => "Decay Tool",
            _ => "Construction Hammer",
        };
    }

    /// <summary>The game's item a tool key stands for (its icon, details and place in the list), or 0 (the staff has none).</summary>
    public static uint ToolItemOf(uint toolKey)
    {
        return toolKey switch
        {
            ConstructionHammerKey => 0x2950da8d,   // Tool_Quickbuilder_Hammer
            OvergrowthToolKey => 0xbe8b0750,       // Tool_Building_Overgrowth
            DecayToolKey => 0xc3dd790f,            // Tool_Building_Decay
            _ => 0,
        };
    }

    // The game's two Quick Stack Stations, by template id (the game's data, build 26430625).
    private static readonly Guid QuickStackStation = new("f535f8e8-49f5-413e-819d-1db473286521");      // Prop_Storage_Loot_Drop_Station_16x16
    private static readonly Guid LargeQuickStackStation = new("89fddfce-08ed-4ceb-8592-8f6597e3b154"); // Prop_Storage_Loot_Drop_Station_24x24

    /// <summary>True for a Quick Stack Station, from its template's id, else from its template's name (one a later game version adds).</summary>
    public static bool IsQuickStackStation(Guid? template, string? templateName)
    {
        if (template is Guid id && (id == QuickStackStation || id == LargeQuickStackStation))
        {
            return true;
        }
        return templateName != null && templateName.StartsWith("Prop_Storage_Loot_Drop_Station", StringComparison.Ordinal);
    }

    /// <summary>True for a chest the player named to be left alone: a name that starts with two hyphens, as Quick Deposit reads it.</summary>
    public static bool IsExcludedChestName(string? chestName)
    {
        return chestName != null && chestName.TrimStart().StartsWith("--", StringComparison.Ordinal);
    }

    /// <summary>The state file's text: {"links":[[x,y,z],...],"favorites":[itemId,...],"gridView":false,"includeHotbar":false}.</summary>
    public static string Serialize(StoredState state)
    {
        var links = new List<double[]>(state.Links.Count);
        foreach (ChestPosition link in state.Links)
        {
            links.Add(new[] { link.X, link.Y, link.Z });
        }
        var favorites = new List<uint>(state.Favorites);
        favorites.Sort();
        var gearKeys = new List<ulong>(state.GearFavorites);
        gearKeys.Sort();
        var gearFavorites = new List<int[]>(gearKeys.Count);
        foreach (ulong key in gearKeys)
        {
            (uint item, int rarity) = FavoriteSet.OfGearKey(key);
            gearFavorites.Add(new[] { unchecked((int)item), rarity });
        }
        var document = new Dictionary<string, object>
        {
            ["links"] = links,
            ["favorites"] = favorites,
            ["depositRules"] = RuleRows(state.DepositRules),
            ["salvageRules"] = RuleRows(state.SalvageRules),
            ["gearFavorites"] = gearFavorites,
            ["gridView"] = state.GridView,
            ["includeHotbar"] = state.IncludeHotbar,
            ["ungrouped"] = state.Ungrouped,
        };
        if (state.HiddenCategoriesChosen)
        {
            var hidden = new List<string>(state.HiddenCategories);
            hidden.Sort(StringComparer.OrdinalIgnoreCase);
            document["hiddenCategories"] = hidden;
            var known = new List<string>(state.KnownCategories);
            known.Sort(StringComparer.OrdinalIgnoreCase);
            document["knownCategories"] = known;
        }
        if (state.HiddenTreeChosen)
        {
            var hiddenTree = new List<string>(state.HiddenTree);
            hiddenTree.Sort(StringComparer.OrdinalIgnoreCase);
            document["hiddenTree"] = hiddenTree;
        }
        if (state.Collapsed.Count > 0)
        {
            var collapsed = new List<string>(state.Collapsed);
            collapsed.Sort(StringComparer.OrdinalIgnoreCase);
            document["collapsed"] = collapsed;
        }
        return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Reads the state file's text. False, with the reason, when it can't be read; the state is then empty.</summary>
    public static bool TryParse(string? text, out StoredState state, out string error)
    {
        state = new StoredState();
        error = "";
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }
        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "the file is not a JSON object";
                return false;
            }
            if (root.TryGetProperty("links", out JsonElement links) && links.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement link in links.EnumerateArray())
                {
                    if (link.ValueKind != JsonValueKind.Array || link.GetArrayLength() != 3)
                    {
                        error = "a link is not [x, y, z]";
                        state = new StoredState();
                        return false;
                    }
                    state.Links.Add(new ChestPosition(link[0].GetDouble(), link[1].GetDouble(), link[2].GetDouble()));
                }
            }
            if (root.TryGetProperty("favorites", out JsonElement favorites) && favorites.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement favorite in favorites.EnumerateArray())
                {
                    state.Favorites.Add(favorite.GetUInt32());
                }
            }
            ReadRules(root, "depositRules", state.DepositRules);
            ReadRules(root, "salvageRules", state.SalvageRules);
            if (root.TryGetProperty("gearFavorites", out JsonElement gearFavorites) && gearFavorites.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement favorite in gearFavorites.EnumerateArray())
                {
                    if (favorite.ValueKind == JsonValueKind.Array && favorite.GetArrayLength() == 2)
                    {
                        state.GearFavorites.Add(FavoriteSet.GearKey(unchecked((uint)favorite[0].GetInt32()), favorite[1].GetInt32()));
                    }
                }
            }
            // A switch the file does not name keeps its default (on, since 0.10.7).
            state.GridView = root.TryGetProperty("gridView", out JsonElement gridView) ? gridView.ValueKind == JsonValueKind.True : state.GridView;
            state.IncludeHotbar = root.TryGetProperty("includeHotbar", out JsonElement includeHotbar) ? includeHotbar.ValueKind == JsonValueKind.True : state.IncludeHotbar;
            state.Ungrouped = root.TryGetProperty("ungrouped", out JsonElement ungrouped) && ungrouped.ValueKind == JsonValueKind.True;
            if (root.TryGetProperty("hiddenCategories", out JsonElement hiddenCategories) && hiddenCategories.ValueKind == JsonValueKind.Array)
            {
                state.HiddenCategoriesChosen = true;
                foreach (JsonElement hiddenCategory in hiddenCategories.EnumerateArray())
                {
                    if (hiddenCategory.ValueKind == JsonValueKind.String)
                    {
                        state.HiddenCategories.Add(hiddenCategory.GetString()!);
                    }
                }
            }
            if (root.TryGetProperty("hiddenTree", out JsonElement hiddenTree) && hiddenTree.ValueKind == JsonValueKind.Array)
            {
                state.HiddenTreeChosen = true;
                foreach (JsonElement key in hiddenTree.EnumerateArray())
                {
                    if (key.ValueKind == JsonValueKind.String)
                    {
                        state.HiddenTree.Add(key.GetString()!);
                    }
                }
            }
            if (root.TryGetProperty("collapsed", out JsonElement collapsed) && collapsed.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement key in collapsed.EnumerateArray())
                {
                    if (key.ValueKind == JsonValueKind.String)
                    {
                        state.Collapsed.Add(key.GetString()!);
                    }
                }
            }
            if (root.TryGetProperty("knownCategories", out JsonElement knownCategories) && knownCategories.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement knownCategory in knownCategories.EnumerateArray())
                {
                    if (knownCategory.ValueKind == JsonValueKind.String)
                    {
                        state.KnownCategories.Add(knownCategory.GetString()!);
                    }
                }
            }
            return true;
        }
        catch (Exception problem) when (problem is JsonException or InvalidOperationException or FormatException)
        {
            error = problem.Message;
            state = new StoredState();
            return false;
        }
    }
}
