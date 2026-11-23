using System;
using System.Collections.Generic;
using System.Text.Json;

namespace StreamlinedLogistics;

/// <summary>One sub-category of the tree: its group ("Gear"), its name ("Weapons") and its place in the screen's order.</summary>
public sealed record TreeSection(int Index, string Group, string Name)
{
    /// <summary>"Gear/Weapons": what the saved hidden and collapsed lists name it by.</summary>
    public string Key => Group + "/" + Name;
}

/// <summary>A sub-category's rows, in the tree's order, with the favorites under a section of their own.</summary>
public sealed record TreeGroup(string Key, string Group, string Name, IReadOnlyList<ItemRow> Rows);

/// <summary>
/// The category tree of the grouped view (since 0.9.0). Where an item goes comes from the game's data, through
/// <c>data\item_categories.json</c> (made by <c>Scripts\make_item_categories.py</c>): Gear, Consumables, Placeables, Building
/// and Other. Materials go last, under the player's hand-made categories that answer to the word "materials"
/// (chestsort_categories.json), with Other Materials for the rest. An item the table does not know (a later game version, a
/// mod's item) goes by the game's own category.
/// </summary>
public sealed class CategoryTree
{
    public const string FavoritesKey = "Favorites";
    public const string MaterialsGroup = "Materials";
    public const string OtherMaterials = "Other Materials";
    private const string MaterialsPlaceholder = "Materials";

    private readonly List<TreeSection> _sections = new();
    private readonly Dictionary<uint, (int Section, string Order)> _items = new();
    private readonly Dictionary<string, int> _sectionsByKey = new(StringComparer.OrdinalIgnoreCase);
    // The hand-made categories' bits of each material section, in the list's order.
    private readonly List<(ulong Bit, int Section)> _materialSections = new();
    private int _otherMaterials = -1;

    /// <summary>The sub-categories in the screen's order.</summary>
    public IReadOnlyList<TreeSection> Sections => _sections;

    /// <summary>The groups in the screen's order.</summary>
    public IReadOnlyList<string> Groups { get; private set; } = Array.Empty<string>();

    /// <summary>
    /// Reads the table. <paramref name="handMadeNames"/> and <paramref name="handMadeMaterial"/> are the hand-made list's categories
    /// and whether each answers to "materials"; their bit is 1 shifted by their place. False, with the reason, when the table can't be read.
    /// </summary>
    public static bool TryParse(string? text, IReadOnlyList<string> handMadeNames, IReadOnlyList<bool> handMadeMaterial, out CategoryTree tree, out string error)
    {
        tree = new CategoryTree();
        error = "";
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "the table is empty";
            return false;
        }
        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            if (!document.RootElement.TryGetProperty("sections", out JsonElement sectionList) || sectionList.ValueKind != JsonValueKind.Array
                || !document.RootElement.TryGetProperty("items", out JsonElement itemList) || itemList.ValueKind != JsonValueKind.Array)
            {
                error = "the table has no sections or items";
                return false;
            }
            // The table's sections as written; "Materials" stands for the hand-made material categories.
            var fileSections = new List<string>();
            foreach (JsonElement section in sectionList.EnumerateArray())
            {
                fileSections.Add(section.GetString() ?? "");
            }
            var fileToTree = new int[fileSections.Count];
            var groups = new List<string>();
            for (int index = 0; index < fileSections.Count; index++)
            {
                string key = fileSections[index];
                if (key == MaterialsPlaceholder)
                {
                    fileToTree[index] = -2;
                    continue;
                }
                int slash = key.IndexOf('/');
                if (slash <= 0)
                {
                    fileToTree[index] = -1;
                    continue;
                }
                fileToTree[index] = tree.Add(key[..slash], key[(slash + 1)..], groups);
            }
            for (int category = 0; category < handMadeNames.Count && category < 64; category++)
            {
                if (category < handMadeMaterial.Count && handMadeMaterial[category])
                {
                    tree._materialSections.Add((1ul << category, tree.Add(MaterialsGroup, handMadeNames[category], groups)));
                }
            }
            tree._otherMaterials = tree.Add(MaterialsGroup, OtherMaterials, groups);
            tree.Groups = groups;

            foreach (JsonElement row in itemList.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 2 || !row[0].TryGetUInt32(out uint item) || !row[1].TryGetInt32(out int fileSection)
                    || fileSection < 0 || fileSection >= fileToTree.Length || fileToTree[fileSection] == -1)
                {
                    continue;
                }
                string order = row.GetArrayLength() > 2 && row[2].ValueKind == JsonValueKind.String ? row[2].GetString() ?? "" : "";
                tree._items[item] = (fileToTree[fileSection], order);
            }
            return true;
        }
        catch (Exception problem) when (problem is JsonException or InvalidOperationException or FormatException)
        {
            error = problem.Message;
            tree = new CategoryTree();
            return false;
        }
    }

    private int Add(string group, string name, List<string> groups)
    {
        string key = group + "/" + name;
        if (_sectionsByKey.TryGetValue(key, out int existing))
        {
            return existing;
        }
        var section = new TreeSection(_sections.Count, group, name);
        _sections.Add(section);
        _sectionsByKey[key] = section.Index;
        if (!groups.Contains(group))
        {
            groups.Add(group);
        }
        return section.Index;
    }

    /// <summary>
    /// Where an item goes, and its order inside its sub-category. <paramref name="handMadeBits"/>: the hand-made categories it is in
    /// (for materials); <paramref name="gameCategory"/>: the game's own category name, used when the table does not know the item.
    /// </summary>
    public (int Section, string Order) Place(uint item, ulong handMadeBits, string? gameCategory)
    {
        int section;
        string order;
        if (_items.TryGetValue(item, out (int Section, string Order) known))
        {
            (section, order) = known;
        }
        else
        {
            section = FallbackSection(gameCategory);
            order = "~|99|";
        }
        if (section == -2)
        {
            section = _otherMaterials;
            foreach ((ulong bit, int materialSection) in _materialSections)
            {
                if ((handMadeBits & bit) != 0)
                {
                    section = materialSection;
                    break;
                }
            }
        }
        return (section, order);
    }

    private int FallbackSection(string? gameCategory)
    {
        string key = gameCategory switch
        {
            "Weapons" => "Gear/Weapons",
            "Equipment" => "Gear/Armor",
            "Tools" or "FishingRod" or "Instrument" => "Gear/Tools",
            "Consumables" => "Consumables/Potions & General Consumables",
            "Ammunition" => "Consumables/Ammo",
            "BuildTools" => "Placeables/Decor",
            "WeaponGem" => "Other/Weapon Gems",
            "Collectibles" => "Other/Lore",
            "Materials" => MaterialsPlaceholder,
            _ => "Other/Miscellaneous",
        };
        if (key == MaterialsPlaceholder)
        {
            return -2;
        }
        return _sectionsByKey.TryGetValue(key, out int section) ? section : _otherMaterials;
    }

    /// <summary>True when an item in this section is hidden: its key ("Gear/Weapons") or its group ("Materials") is in the hidden list.</summary>
    public bool IsHidden(int section, ISet<string> hidden)
    {
        if (section < 0 || section >= _sections.Count)
        {
            return false;
        }
        TreeSection tree = _sections[section];
        return hidden.Contains(tree.Group) || hidden.Contains(tree.Key);
    }

    /// <summary>The sections a chest's name asks for (<see cref="SectionRanksOfChestName"/>).</summary>
    public HashSet<int> SectionsOfChestName(string? chestName)
    {
        return new HashSet<int>(SectionRanksOfChestName(chestName).Keys);
    }

    /// <summary>A chest named for a sub-category ("Weapons").</summary>
    public const int SectionNameRank = 3;
    /// <summary>A chest named for a group ("Gear"): a sub-category's own chest comes first.</summary>
    public const int GroupNameRank = 2;
    /// <summary>A hand-made category's word, used only when the tree's table is missing.</summary>
    public const int WordRank = 1;

    /// <summary>
    /// The sections a chest's name asks for, and how (since 0.10.16, the user's choice: no other names, "too confusing without a
    /// list at hand"): a chest named exactly as a sub-category ("Weapons", "Raw Food", "Ores"; case and outer spaces aside) asks
    /// for it at <see cref="SectionNameRank"/>; one named exactly as a group ("Gear", "Materials") asks for its sub-categories at
    /// <see cref="GroupNameRank"/>. Any other name asks for nothing. A deposit goes to the chests of the highest rank.
    /// </summary>
    public Dictionary<int, int> SectionRanksOfChestName(string? chestName)
    {
        var found = new Dictionary<int, int>();
        string name = NormalName(chestName);
        if (name.Length == 0)
        {
            return found;
        }
        foreach (TreeSection section in _sections)
        {
            if (string.Equals(NormalName(section.Name), name, StringComparison.OrdinalIgnoreCase))
            {
                found[section.Index] = SectionNameRank;
            }
            else if (string.Equals(NormalName(section.Group), name, StringComparison.OrdinalIgnoreCase))
            {
                found[section.Index] = GroupNameRank;
            }
        }
        return found;
    }

    /// <summary>The names a chest can be given to ask for something: each group, then its sub-categories, in the tree's order.</summary>
    public IReadOnlyList<string> ChestNameChoices()
    {
        var names = new List<string>();
        foreach (string group in Groups)
        {
            if (!names.Contains(group))
            {
                names.Add(group);
            }
            foreach (TreeSection section in _sections)
            {
                if (section.Group == group && !names.Contains(section.Name))
                {
                    names.Add(section.Name);
                }
            }
        }
        return names;
    }

    private static string NormalName(string? name)
    {
        return string.Join(' ', (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// The rows under the tree: the favorites first under their own section, then each sub-category in the tree's order, each ordered
    /// by kind, then by family (one item's ranks, "Vessel I" and "Vessel II", together at the family's lowest tier), tier and name. A favorite is also listed in its own sub-category. <paramref name="sectionOf"/> and <paramref name="orderOf"/>
    /// say where a row goes; a row of no section goes under Other Materials.
    /// </summary>
    public IReadOnlyList<TreeGroup> Group(IReadOnlyList<ItemRow> rows, Func<ItemRow, int> sectionOf, Func<ItemRow, string> orderOf)
    {
        var groups = new List<TreeGroup>();
        var favorites = new List<ItemRow>();
        var bySection = new List<(int Section, string Order, ItemRow Row)>[_sections.Count];
        foreach (ItemRow row in rows)
        {
            if (row.IsFavorite)
            {
                favorites.Add(row);
            }
            int section = sectionOf(row);
            if (section < 0 || section >= _sections.Count)
            {
                section = _otherMaterials;
            }
            (bySection[section] ??= new()).Add((section, orderOf(row), row));
        }
        if (favorites.Count > 0)
        {
            groups.Add(new TreeGroup(FavoritesKey, "", FavoritesKey, Ordered(favorites, sectionOf, orderOf)));
        }
        for (int section = 0; section < _sections.Count; section++)
        {
            if (bySection[section] is not { Count: > 0 } list)
            {
                continue;
            }
            TreeSection tree = _sections[section];
            groups.Add(new TreeGroup(tree.Key, tree.Group, tree.Name, SortEntries(list)));
        }
        return groups;
    }

    /// <summary>The same rows in the tree's order, without headers (one chest's content).</summary>
    public IReadOnlyList<ItemRow> Ordered(IReadOnlyList<ItemRow> rows, Func<ItemRow, int> sectionOf, Func<ItemRow, string> orderOf)
    {
        var list = new List<(int Section, string Order, ItemRow Row)>(rows.Count);
        foreach (ItemRow row in rows)
        {
            int section = sectionOf(row);
            list.Add((section < 0 ? int.MaxValue : section, orderOf(row), row));
        }
        return SortEntries(list);
    }

    // An entry's order is "kind|tier|debug name" (from the table); a family is the shown name without its last numeral.
    private readonly record struct SortKey(int Section, string Kind, string FamilyTier, string Family, string Rest, ItemRow Row);

    private static List<ItemRow> SortEntries(List<(int Section, string Order, ItemRow Row)> list)
    {
        var keys = new List<SortKey>(list.Count);
        var familyTiers = new Dictionary<(int, string, string), string>();
        foreach ((int section, string order, ItemRow row) in list)
        {
            int bar = order.IndexOf('|');
            string kind = bar < 0 ? order : order[..bar];
            string rest = bar < 0 ? "" : order[(bar + 1)..];
            int tierEnd = rest.IndexOf('|');
            string tier = tierEnd < 0 ? rest : rest[..tierEnd];
            string family = ChestRules.NameFamily(row.Name).ToLowerInvariant();
            var familyKey = (section, kind, family);
            if (!familyTiers.TryGetValue(familyKey, out string? lowest) || string.CompareOrdinal(tier, lowest) < 0)
            {
                familyTiers[familyKey] = tier;
            }
            keys.Add(new SortKey(section, kind, "", family, rest, row));
        }
        for (int index = 0; index < keys.Count; index++)
        {
            SortKey key = keys[index];
            keys[index] = key with { FamilyTier = familyTiers[(key.Section, key.Kind, key.Family)] };
        }
        keys.Sort(CompareEntries);
        var sorted = new List<ItemRow>(keys.Count);
        foreach (SortKey key in keys)
        {
            sorted.Add(key.Row);
        }
        return sorted;
    }

    private static int CompareEntries(SortKey left, SortKey right)
    {
        int compared = left.Section.CompareTo(right.Section);
        if (compared == 0)
        {
            compared = string.CompareOrdinal(left.Kind, right.Kind);
        }
        if (compared == 0)
        {
            compared = string.CompareOrdinal(left.FamilyTier, right.FamilyTier);
        }
        if (compared == 0)
        {
            compared = string.CompareOrdinal(left.Family, right.Family);
        }
        if (compared == 0)
        {
            compared = string.CompareOrdinal(left.Rest, right.Rest);
        }
        return compared != 0 ? compared : string.Compare(left.Row.Name, right.Row.Name, StringComparison.OrdinalIgnoreCase);
    }
}
