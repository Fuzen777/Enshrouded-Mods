using System;
using System.Linq;
using System.Collections.Generic;
using StreamlinedLogistics;

// Checks the rules that need no game: which chest a kept position names, how stacks become list lines, what a search matches,
// where a deposit goes, and that the state file reads back what was written.
int failures = 0;
void Check(bool condition, string what)
{
    if (!condition)
    {
        failures++;
        Console.WriteLine("FAIL: " + what);
    }
}

// Links: a chest is found again within 5 cm of its kept position, and the nearer link wins.
var links = new List<ChestPosition> { new(4743.0, 752.5, 5460.5), new(4743.0, 753.5, 5460.5), new(4747.5, 752.5, 5449.0) };
Check(ChestRules.FindLink(links, new ChestPosition(4743.0, 752.5, 5460.5)) == 0, "the exact position names its link");
Check(ChestRules.FindLink(links, new ChestPosition(4743.0004, 752.5003, 5460.4996)) == 0, "a position half a millimetre off (a save's 32-bit float) still names its link");
Check(ChestRules.FindLink(links, new ChestPosition(4743.0, 753.49, 5460.5)) == 1, "a chest stacked 1 m above another names its own link");
Check(ChestRules.FindLink(links, new ChestPosition(4743.0, 752.56, 5460.5)) == -1, "6 cm off names no link");
Check(ChestRules.FindLink(links, new ChestPosition(0, 0, 0)) == -1, "a position far from every link names none");
Check(ChestRules.FindLink(new List<ChestPosition>(), new ChestPosition(1, 2, 3)) == -1, "no links, no match");
var close = new List<ChestPosition> { new(10.00, 5, 5), new(10.03, 5, 5) };
Check(ChestRules.FindLink(close, new ChestPosition(10.025, 5, 5)) == 1, "between two links within reach, the nearer one wins");

// Link radius: kept inside its range.
Check(ChestRules.LinkRadius(100) == 100, "a radius in range stays");
Check(ChestRules.LinkRadius(1) == ChestRules.MinLinkRadiusMeters, "a radius below the range becomes the smallest");
Check(ChestRules.LinkRadius(9999) == ChestRules.MaxLinkRadiusMeters, "a radius above the range becomes the largest");
Check(ChestRules.LinkRadius(double.NaN) == ChestRules.DefaultLinkRadiusMeters, "a radius that is no number becomes the default");

// List lines: plain stacks of one item merge across chests; gear merges only at the same level and rarity.
const uint Wood = 722268629;
const uint Sword = 0x43108313;
const uint Torch = 77;
var names = new Dictionary<uint, string> { [Wood] = "Wood Logs", [Sword] = "Bronze Sword", [Torch] = "Torch" };
string NameOf(uint item) => names[item];
var stacks = new List<StackView>
{
    new(Holder: 100, Slot: 1, Item: Wood, Count: 9999, Level: 0, Rarity: -1, IsGear: false),
    new(Holder: 101, Slot: 7, Item: Wood, Count: 250, Level: 0, Rarity: -1, IsGear: false),
    new(Holder: 101, Slot: 8, Item: Wood, Count: 1, Level: 0, Rarity: -1, IsGear: false),
    new(Holder: 100, Slot: 2, Item: Sword, Count: 1, Level: 12, Rarity: 2, IsGear: true),
    new(Holder: 102, Slot: 3, Item: Sword, Count: 1, Level: 12, Rarity: 2, IsGear: true),
    new(Holder: 102, Slot: 4, Item: Sword, Count: 1, Level: 20, Rarity: 2, IsGear: true),
    new(Holder: 102, Slot: 5, Item: Sword, Count: 1, Level: 20, Rarity: 3, IsGear: true),
    new(Holder: 100, Slot: 9, Item: Torch, Count: 40, Level: 0, Rarity: -1, IsGear: false),
    new(Holder: 100, Slot: 10, Item: Torch, Count: 0, Level: 0, Rarity: -1, IsGear: false),
};
IReadOnlyList<ItemRow> rows = ChestRules.BuildRows(stacks, NameOf, new HashSet<uint>());
Check(rows.Count == 5, "9 stacks give 5 lines: wood, torch and three kinds of sword");
Check(rows[0].Name == "Bronze Sword" && rows[0].Level == 20 && rows[0].Rarity == 3, "by name first, then the higher level, then the higher rarity");
Check(rows[1].Level == 20 && rows[1].Rarity == 2 && rows[2].Level == 12, "the level 20 swords come before the level 12 ones");
Check(rows[2].Total == 2 && rows[2].HolderCount == 2 && rows[2].Stacks.Count == 2, "two equal swords in two chests share a line");
Check(rows[3].Name == "Torch" && rows[3].Total == 40 && rows[3].Stacks.Count == 1, "an empty stack is left out");
Check(rows[4].Name == "Wood Logs" && rows[4].Total == 10250 && rows[4].HolderCount == 2 && rows[4].Stacks.Count == 3, "three stacks of wood in two chests are one line of 10,250");
Check(!rows[4].IsFavorite, "nothing is a favorite until marked");

IReadOnlyList<ItemRow> withFavorite = ChestRules.BuildRows(stacks, NameOf, new HashSet<uint> { Wood });
Check(withFavorite[0].Name == "Wood Logs" && withFavorite[0].IsFavorite, "a favorite comes first");
Check(withFavorite[1].Name == "Bronze Sword" && !withFavorite[1].IsFavorite, "the rest keeps its order after the favorites");
IReadOnlyList<ItemRow> gearFavorite = ChestRules.BuildRows(stacks, NameOf, new HashSet<uint> { Sword, Torch });
Check(gearFavorite[0].IsFavorite && gearFavorite[1].IsFavorite && gearFavorite[2].IsFavorite && gearFavorite[3].Name == "Torch" && gearFavorite[4].Name == "Wood Logs",
    "a favorite gear item marks every one of its lines, and favorites sort by name among themselves");

// Search: every word, any order, any case.
Check(ChestRules.Matches("Wood Logs", ""), "an empty search matches");
Check(ChestRules.Matches("Wood Logs", null), "no search matches");
Check(ChestRules.Matches("Wood Logs", "   "), "a search of spaces matches");
Check(ChestRules.Matches("Wood Logs", "wood"), "a word in another case matches");
Check(ChestRules.Matches("Wood Logs", "log wo"), "two parts of words in any order match");
Check(!ChestRules.Matches("Wood Logs", "wood plank"), "one word that is not in the name fails the search");
Check(ChestRules.Matches("Bronze Sword", "  SWORD  "), "spaces around the search are ignored");

// Deposits: chests already holding the item first (the most first), then the nearest with a free slot.
var depositChests = new List<DepositChest>
{
    new(Entity: 1, HeldOfItem: 0, FreeSlots: 10, DistanceMeters: 5),
    new(Entity: 2, HeldOfItem: 300, FreeSlots: 0, DistanceMeters: 50),
    new(Entity: 3, HeldOfItem: 9000, FreeSlots: 2, DistanceMeters: 80),
    new(Entity: 4, HeldOfItem: 0, FreeSlots: 0, DistanceMeters: 1),
    new(Entity: 5, HeldOfItem: 0, FreeSlots: 3, DistanceMeters: 2),
};
IReadOnlyList<uint> order = ChestRules.DepositOrder(depositChests);
Check(order.Count == 4 && order[0] == 3 && order[1] == 2 && order[2] == 5 && order[3] == 1, "the chest with 9,000 first, then the one with 300, then the free chests by distance; the full chest without the item is left out");
Check(ChestRules.DepositOrder(new List<DepositChest>()).Count == 0, "no chests, no order");

// The state file reads back what was written.
var state = new StoredState();
state.Links.Add(new ChestPosition(4743.0, 752.5, 5460.5));
state.Links.Add(new ChestPosition(-12.25, 0.125, 99999.875));
state.Favorites.Add(Wood);
state.Favorites.Add(Sword);
string text = ChestRules.Serialize(state);
Check(ChestRules.TryParse(text, out StoredState readBack, out string error) && error == "", "the written state reads back");
Check(readBack.Links.Count == 2 && readBack.Links[0] == state.Links[0] && readBack.Links[1] == state.Links[1], "both links come back exactly");
Check(readBack.Favorites.SetEquals(state.Favorites), "both favorites come back");
Check(ChestRules.TryParse("", out StoredState empty, out _) && empty.Links.Count == 0 && empty.Favorites.Count == 0, "an empty file is an empty state");
Check(ChestRules.TryParse("{}", out empty, out _) && empty.Links.Count == 0, "a file without links is an empty state");
Check(!ChestRules.TryParse("{\"links\":[[1,2]]}", out StoredState broken, out error) && broken.Links.Count == 0 && error.Length > 0, "a link without three numbers is refused, with the reason");
Check(!ChestRules.TryParse("not json", out broken, out error) && error.Length > 0, "text that is not JSON is refused, with the reason");
Check(!ChestRules.TryParse("{\"links\":[[1,2,\"x\"]]}", out broken, out error) && broken.Links.Count == 0, "a link with text in it is refused");
Check(!ChestRules.TryParse("[]", out broken, out error), "a file that is a list is refused");

var withSwitches = new StoredState { GridView = true, IncludeHotbar = true };
Check(ChestRules.TryParse(ChestRules.Serialize(withSwitches), out StoredState switchesBack, out _) && switchesBack.GridView && switchesBack.IncludeHotbar, "the screen's two switches come back");
Check(ChestRules.TryParse("{\"links\":[],\"favorites\":[]}", out StoredState oldFile, out _) && oldFile.GridView && oldFile.IncludeHotbar, "a file without the switches reads with their defaults (on)");
Check(new StoredState().GridView && new StoredState().IncludeHotbar && !new StoredState().Ungrouped, "a new player starts with the grid and the hotbars, grouped");
var bothOff = new StoredState { GridView = false, IncludeHotbar = false };
Check(ChestRules.TryParse(ChestRules.Serialize(bothOff), out StoredState bothOffBack, out _) && !bothOffBack.GridView && !bothOffBack.IncludeHotbar, "switches turned off stay off");

// Which container is a chest: by the template's id, else by its name.
Check(ChestRules.KindOfChest(new Guid("955ba32f-08ea-414d-bd98-ac75990aef04"), null) == ChestKind.Magic, "the 24-slot Magic Chest's template id is a Magic Chest, without its name");
Check(ChestRules.KindOfChest(new Guid("bc834cbd-58f1-494a-97fb-0dc0248eb738"), "Prop_Decoration_T6_Storage_72_Magic") == ChestKind.Magic, "the 72-slot Magic Chest is a Magic Chest");
Check(ChestRules.KindOfChest(new Guid("65baa890-022a-454b-9cb7-b92cb8c06cd4"), "Prop_Decoration_T1_Storage_24") == ChestKind.Regular, "the 24-slot chest is a regular chest");
Check(ChestRules.KindOfChest(new Guid("0c667e7e-0fbd-4cb9-b58d-c3a20df7eaec"), null) == ChestKind.Regular, "the T5 chest is a regular chest");
Check(ChestRules.KindOfChest(Guid.NewGuid(), "Prop_Decoration_T9_Storage_96_Magic") == ChestKind.Magic, "a Magic Chest a later game version adds is told by its name");
Check(ChestRules.KindOfChest(Guid.NewGuid(), "Prop_Decoration_T9_Storage_96") == ChestKind.Regular, "a regular chest a later game version adds is told by its name");
Check(ChestRules.KindOfChest(Guid.NewGuid(), "EternalHarvest_Planted_Flax") == ChestKind.Other, "a planted crop is not a chest");
Check(ChestRules.KindOfChest(Guid.NewGuid(), "LootChest_Random_Wood_Locked_Pickable_T7") == ChestKind.Other, "a loot chest is not a storage chest");
Check(ChestRules.KindOfChest(Guid.NewGuid(), "Craftingstation_Workbench") == ChestKind.Other, "a station is not a chest");
Check(ChestRules.KindOfChest(Guid.NewGuid(), null) == ChestKind.Other, "an unknown template without a name is not a chest");
Check(ChestRules.KindOfChest(null, null) == ChestKind.Unknown, "no template at all is unknown");

// The player's pages, as the game laid them out for the user's character on 2026-11-21: worn pages, two backpack pages, the two
// hotbar pages, a third backpack page, then a Virtual link to the worn backpack item, which carries more backpack pages.
const byte Equipment = 2, Currency = 3, Generic = ChestRules.GenericCategory, Virtual = ChestRules.VirtualCategory;
var rootSetup = new SetupRead(
    new uint[] { 1, 10315, 10339, 10340, 10341, 10342, 10343, 10352, 10314, 10362, 10363, 0, 77, 0, 0, 0 },
    new byte[] { Equipment, Equipment, Generic, Generic, Generic, Generic, Generic, Equipment, Virtual, Currency, Equipment, 0, Generic, 0, 0, 0 },
    AvailableSlots: 40);
var backpackItemSetup = new SetupRead(
    new uint[] { 10358, 10359, 10360, 10361, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
    new byte[] { Generic, Generic, Generic, Generic, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
    AvailableSlots: 28);
var carriedSetups = new Dictionary<uint, SetupRead> { [10314] = backpackItemSetup };
PlayerLayout layout = ChestRules.BuildLayout(rootSetup, carriedSetups, hotbarPage1: 10341, hotbarPage2: 10342);
Check(layout.BackpackPages.Count == 7 && layout.BackpackPages[0] == 10339 && layout.BackpackPages[1] == 10340 && layout.BackpackPages[2] == 10343
    && layout.BackpackPages[3] == 10358 && layout.BackpackPages[6] == 10361, "the backpack is the character's own item pages without the hotbar, then the worn backpack item's pages");
Check(layout.HotbarPages.Count == 2 && layout.IsHotbarPage(10341) && layout.IsHotbarPage(10342) && !layout.IsBackpackPage(10341), "the two hotbar pages are not the backpack");
Check(!layout.IsBackpackPage(1) && !layout.IsBackpackPage(10362) && !layout.IsBackpackPage(10314), "equipment, currency and the link to the backpack item are not backpack pages");
Check(!layout.IsBackpackPage(77), "a link after the list's end is not read");
Check(layout.IsWornPage(1) && layout.IsWornPage(10315) && layout.IsWornPage(10362) && !layout.IsWornPage(10339) && !layout.IsWornPage(10358) && !layout.IsWornPage(10341) && !layout.IsWornPage(999),
    "equipment and currency pages are worn pages; backpack, hotbar and unknown pages are not");
// The character's own pages: saved numbers 0-15, 16-31 (hotbar), 32-39, all below 40.
Check(layout.IsUsableBackpackSlot(10339, 0) && layout.IsUsableBackpackSlot(10340, 7) && layout.IsUsableBackpackSlot(10343, 7), "every slot of the character's own backpack pages is unlocked at 40 of 40");
// The backpack item's pages: saved numbers 0-7, 8-15, 16-23, 24-31; 28 unlocked.
Check(layout.IsUsableBackpackSlot(10358, 0) && layout.IsUsableBackpackSlot(10360, 7) && layout.IsUsableBackpackSlot(10361, 3), "the backpack item's slots numbered below its own 28 are unlocked");
Check(!layout.IsUsableBackpackSlot(10361, 4) && !layout.IsUsableBackpackSlot(10361, 7), "its slots numbered 28 and up are locked");
Check(!layout.IsUsableBackpackSlot(10341, 0) && !layout.IsUsableBackpackSlot(1, 0) && !layout.IsUsableBackpackSlot(10339, 8) && !layout.IsUsableBackpackSlot(10339, -1) && !layout.IsUsableBackpackSlot(999, 0),
    "hotbar, worn and unknown pages and indexes outside a page are never usable");
PlayerLayout withoutCarried = ChestRules.BuildLayout(rootSetup, new Dictionary<uint, SetupRead>(), 10341, 10342);
Check(withoutCarried.BackpackPages.Count == 3, "a backpack item whose inventory can't be read adds no pages");
PlayerLayout noHotbar = ChestRules.BuildLayout(rootSetup, carriedSetups, 0, 0);
Check(noHotbar.BackpackPages.Count == 9 && noHotbar.HotbarPages.Count == 0, "without hotbar pages every item page counts as backpack");
var loop = new Dictionary<uint, SetupRead> { [10314] = new SetupRead(new uint[] { 10314, 10358 }, new byte[] { Virtual, Generic }, 8) };
Check(ChestRules.BuildLayout(rootSetup, loop, 10341, 10342).BackpackPages.Count == 4, "a link that leads back to its own owner ends, and its page is counted once");

// Where a taken stack goes. Page 1 is equipment with an empty slot, as when 0.1.0 lost a stack: it may never be chosen.
var playerSlots = new List<SlotContent>();
for (int index = 0; index < 8; index++)
{
    playerSlots.Add(new SlotContent(1, index, index == 0 ? Wood : 0u, index == 0 ? 10 : 0, false));
    playerSlots.Add(new SlotContent(10341, index, 0, 0, false));
    playerSlots.Add(new SlotContent(10339, index, Torch, 5, false));
    playerSlots.Add(new SlotContent(10340, index, Torch, 5, false));
    playerSlots.Add(new SlotContent(10343, index, Sword, 1, true));
    playerSlots.Add(new SlotContent(10358, index, index == 2 ? Wood : index == 5 ? 0u : Torch, index == 2 ? 9000 : index == 5 ? 0 : 5, false));
    playerSlots.Add(new SlotContent(10359, index, Torch, 5, false));
    playerSlots.Add(new SlotContent(10360, index, Torch, 5, false));
    playerSlots.Add(new SlotContent(10361, index, index == 6 ? 0u : Sword, index == 6 ? 0 : 1, index != 6));
}
Check(layout.HotbarPagesInOrder.Count == 2 && layout.HotbarPagesInOrder[0] == 10341 && layout.HotbarPagesInOrder[1] == 10342, "hotbar 1, then hotbar 2");
Check(ChestRules.ChooseHotbarSlot(layout, playerSlots, Torch, 20, isGear: false) == new Destination(10341, 0, 20), "a stack dropped on the hotbars goes to hotbar 1's first empty slot");
var fullHotbar1 = new List<SlotContent>(playerSlots);
for (int index = 0; index < 8; index++)
{
    fullHotbar1.RemoveAll(slot => slot.Page == 10341 && slot.Index == index);
    fullHotbar1.Add(new SlotContent(10341, index, Sword, 1, true));
    fullHotbar1.Add(new SlotContent(10342, index, index == 3 ? Torch : index == 5 ? 0u : Sword, index == 3 ? 15 : index == 5 ? 0 : 1, index != 3 && index != 5));
}
Check(ChestRules.ChooseHotbarSlot(layout, fullHotbar1, Torch, 20, isGear: false) == new Destination(10342, 3, 5), "hotbar 1 full: hotbar 2's stack of the item first");
Check(ChestRules.ChooseHotbarSlot(layout, fullHotbar1, Sword, 1, isGear: true) == new Destination(10342, 5, 1), "hotbar 1 full: gear takes hotbar 2's empty slot");
fullHotbar1.RemoveAll(slot => slot.Page == 10342 && slot.Index == 5);
fullHotbar1.Add(new SlotContent(10342, 5, Sword, 1, true));
Check(ChestRules.ChooseHotbarSlot(layout, fullHotbar1, Sword, 1, isGear: true) == null, "both hotbars full: no hotbar slot (the backpack is next)");
Check(ChestRules.BackpackFreeSlots(layout, playerSlots) == 1, "the backpack's free slots: unlocked empty ones only (10358:5), not a locked one (10361:6), the hotbar's or the worn pages'");
Check(ChestRules.AutoDepositWanted(4, 5) && !ChestRules.AutoDepositWanted(5, 5), "Auto Quick Deposit: fewer free slots than the threshold start it, as many don't");
Check(ChestRules.AutoDepositWanted(0, 1) && !ChestRules.AutoDepositWanted(1, 1), "Auto Quick Deposit at 1: only a full backpack starts it, as up to 1.1.1");
Check(ChestRules.AutoDepositWanted(0, 0) && !ChestRules.AutoDepositWanted(1, 0), "Auto Quick Deposit: a threshold below the range reads as 1");
Destination? woodPlace = ChestRules.ChooseBackpackSlot(layout, playerSlots, Wood, 9999, isGear: false);
Check(woodPlace == new Destination(10358, 2, 999), "wood goes onto the backpack's own stack of 9,000, with room for 999, not onto the stack in the equipment page");
Destination? amberPlace = ChestRules.ChooseBackpackSlot(layout, playerSlots, 12345, 250, isGear: false);
Check(amberPlace == new Destination(10358, 5, 250), "with the character's own pages full, an item goes to the backpack item's first empty slot, not to an equipment slot or the hotbar");
Destination? swordPlace = ChestRules.ChooseBackpackSlot(layout, playerSlots, Sword, 1, isGear: true);
Check(swordPlace == new Destination(10358, 5, 1), "gear takes an empty slot and never shares one");
Destination? torchPlace = ChestRules.ChooseBackpackSlot(layout, playerSlots, Torch, 5, isGear: false);
Check(torchPlace == new Destination(10358, 5, 5), "full stacks of the item are passed over");
Destination? unknownStack = ChestRules.ChooseBackpackSlot(layout, playerSlots, Wood, 0, isGear: false);
Check(unknownStack is Destination unknownSlot && unknownSlot.Page == 10358 && unknownSlot.Index == 5 && unknownSlot.Room == int.MaxValue, "with an unknown stack size only an empty slot is taken");
playerSlots.RemoveAll(slot => slot.Page == 10358 && slot.Index == 5);
playerSlots.Add(new SlotContent(10358, 5, Torch, 5, false));
Check(ChestRules.ChooseBackpackSlot(layout, playerSlots, 12345, 250, isGear: false) == null, "the only empty slot left is locked (the backpack item's slot number 30): the backpack is full");
Check(ChestRules.ChooseBackpackSlot(layout, playerSlots, Wood, 9999, isGear: false) == new Destination(10358, 2, 999), "a full backpack still takes more of a stack it holds");
Check(ChestRules.ChooseBackpackSlot(layout, new List<SlotContent>(), Wood, 9999, isGear: false) == null, "slots that were not read are never chosen");

// What sits where it can't belong: a plain material in a worn page.
Check(ChestRules.IsStranded(layout, 1, isGear: false, isMaterial: true), "a material in an equipment slot is stranded");
Check(ChestRules.IsStranded(layout, 10362, isGear: false, isMaterial: true), "a material in the currency page is stranded");
Check(!ChestRules.IsStranded(layout, 1, isGear: true, isMaterial: false), "worn gear is where it belongs");
Check(!ChestRules.IsStranded(layout, 1, isGear: false, isMaterial: false), "arrows in an equipment slot are where they belong");
Check(!ChestRules.IsStranded(layout, 10358, isGear: false, isMaterial: true) && !ChestRules.IsStranded(layout, 10339, isGear: false, isMaterial: true), "a material in the backpack is where it belongs");
Check(!ChestRules.IsStranded(layout, 10341, isGear: false, isMaterial: true), "a material on the hotbar is where it belongs");

// Texts.
Check(ChestRules.HoldersText(new List<string> { "Ores" }) == "Ores", "one chest shows its name");
Check(ChestRules.HoldersText(new List<string> { "Ores", "Wood" }) == "2 chests", "two chests show their number");
Check(ChestRules.ShortCount(950) == "950" && ChestRules.ShortCount(9999) == "9,999", "counts below 10,000 are written in full");
Check(ChestRules.ShortCount(12345) == "12.3k" && ChestRules.ShortCount(19999) == "19.9k", "tens of thousands get one decimal, never rounded up");
Check(ChestRules.ShortCount(150000) == "150k" && ChestRules.ShortCount(999999) == "999k", "hundreds of thousands are whole");
Check(ChestRules.ShortCount(2500000) == "2.5M", "millions get one decimal");

// Categories: the file Quick Deposit uses. Names without case, * for any text, ids, a leading - to remove, "rest" for what nothing lists.
const string CategoriesText = """
{
  "_readme": ["text that is not a category"],
  "categories": {
    "Blocks": { "words": ["building"], "items": ["Block_*"] },
    "Misc": { "rest": true, "words": ["other"], "items": ["Key_Bronze01"] },
    "Ores": { "words": ["metals", "materials"], "items": ["Material_T2_Copper_Bar", "Material_*_Ore", "722268629"] },
    "Plants": { "words": ["seeds", "materials"], "items": ["*_Seedling_*", "Material_*_Plant_*", "-*_Seedling_Tree_*", "_T6_Seedling_Tree_Papyrus"] },
    "Terrain": { "words": ["dirt"], "items": ["Build_TerrainMaterial_*"] },
    "Tools": { "words": [], "items": ["Tool_*", "-Tool_*_Rake_*"] }
  }
}
""";
Check(CategoryTable.TryParse(CategoriesText, out CategoryTable categories, out string categoriesError) && categoriesError == "", "the categories file is read");
Check(categories.Names.Count == 6 && categories.Names[0] == "Blocks" && categories.Names[5] == "Tools", "six categories, in the file's order");
const ulong Blocks = 1, Misc = 2, Ores = 4, Plants = 8, Terrain = 16, Tools = 32;
Check(categories.BitsOf(1, "Material_T2_Copper_Bar") == Ores, "an exact name is in its category");
Check(categories.BitsOf(1, "material_t2_COPPER_bar") == Ores, "a name's case does not matter");
Check(categories.BitsOf(1, "Material_T3_Iron_Ore") == Ores, "a * in the middle matches");
Check(categories.BitsOf(1, "Material_T3_Iron_Ore_Refined") == Misc, "a name that only starts like the pattern is not in it, so it goes to the rest");
Check(categories.BitsOf(722268629, "Material_T1_Wood_Logs") == Ores, "an item id in the list matches by id");
Check(categories.BitsOf(722268629, null) == Ores, "an id matches without a name");
Check(categories.BitsOf(1, "Block_T1_Stone") == Blocks && categories.BitsOf(1, "Build_TerrainMaterial_T1_Dirt_Grassland") == Terrain, "blocks and terrain are in their categories");
Check(categories.BitsOf(1, "_T1_Seedling_Bush_Reed") == Plants, "a seedling is a plant");
Check(categories.BitsOf(1, "_T2_Seedling_Tree_Oak") == Misc, "a tree seedling is removed by the - entry, so it goes to the rest");
Check(categories.BitsOf(1, "_T6_Seedling_Tree_Papyrus") == Plants, "an entry after the - entry puts one back");
Check(categories.BitsOf(1, "Tool_T1_Pickaxe_Scrappy") == Tools && categories.BitsOf(1, "Tool_T1_Rake_Scrappy") == Misc, "entries apply in order: the rake is taken out again");
Check(categories.BitsOf(1, "Key_Bronze01") == Misc, "a category marked rest keeps its own items");
Check(categories.BitsOf(1, "Something_Nobody_Lists") == Misc && categories.BitsOf(1, null) == Misc && categories.BitsOf(1, "") == Misc, "what no category lists goes to the rest");
Check(categories.BitsOfNames(new[] { "ores", "TERRAIN", "No Such Category" }) == (Ores | Terrain), "categories are found by name without case; unknown names are left out");
IReadOnlyList<string> defaultHidden = categories.DefaultHidden();
Check(defaultHidden.Count == 4 && defaultHidden[0] == "Blocks" && defaultHidden[1] == "Ores" && defaultHidden[2] == "Plants" && defaultHidden[3] == "Terrain",
    "hidden by default: the categories that answer to \"materials\", plus Blocks and Terrain");

Check(CategoryTable.WildcardMatch("abc", "abc") && !CategoryTable.WildcardMatch("abc", "abcd"), "without * the text must be the pattern");
Check(CategoryTable.WildcardMatch("a*", "a") && CategoryTable.WildcardMatch("*", "") && CategoryTable.WildcardMatch("a*c", "ac") && CategoryTable.WildcardMatch("a*c", "abbbc"), "* stands for any text, also none");
Check(!CategoryTable.WildcardMatch("a*c", "abcd") && !CategoryTable.WildcardMatch("ab*bc", "abc"), "the start and the end may not share letters");
Check(CategoryTable.WildcardMatch("a*b*c", "a1b2c") && !CategoryTable.WildcardMatch("a*b*c", "a1c2b"), "several * keep the parts in order");

Check(!CategoryTable.TryParse("", out _, out categoriesError) && categoriesError.Length > 0, "an empty file is refused, with the reason");
Check(!CategoryTable.TryParse("{\"other\": 1}", out _, out categoriesError) && categoriesError.Length > 0, "a file without categories is refused");
Check(!CategoryTable.TryParse("{ not json", out _, out categoriesError) && categoriesError.Length > 0, "text that is not JSON is refused");
Check(CategoryTable.TryParse("{\"categories\": {}}", out CategoryTable noCategories, out _) && noCategories.Names.Count == 0 && noCategories.BitsOf(1, "Anything") == 0, "a file with no category puts every item in none");

// Hiding by category: only when every category of the item is hidden; an item in none always shows.
Check(ChestRules.IsHiddenByCategory(Ores, Ores | Terrain), "an item whose category is hidden is hidden");
Check(!ChestRules.IsHiddenByCategory(Ores, Terrain), "an item whose category is shown shows");
Check(!ChestRules.IsHiddenByCategory(Ores | Tools, Ores), "an item in two categories shows while one of them is shown");
Check(ChestRules.IsHiddenByCategory(Ores | Tools, Ores | Tools | Misc), "it is hidden when both are");
Check(!ChestRules.IsHiddenByCategory(0, ulong.MaxValue), "an item in no category is never hidden");
Check(!ChestRules.IsHiddenByCategory(Ores, 0), "with nothing hidden, nothing is hidden");

// Lines carry their item's categories, and the state file keeps the hidden ones and the grouping.
IReadOnlyList<ItemRow> categorized = ChestRules.BuildRows(stacks, NameOf, new HashSet<uint>(), item => item == Wood ? Ores : 0);
Check(categorized[4].Name == "Wood Logs" && categorized[4].Categories == Ores && categorized[0].Categories == 0, "a line has its item's categories");
Check(rows[4].Categories == 0, "without categories a line has none");
var stateWithCategories = new StoredState { Ungrouped = true, HiddenCategoriesChosen = true };
stateWithCategories.HiddenCategories.Add("Terrain");
stateWithCategories.HiddenCategories.Add("Ores");
Check(ChestRules.TryParse(ChestRules.Serialize(stateWithCategories), out StoredState categoriesBack, out _) && categoriesBack.Ungrouped && categoriesBack.HiddenCategoriesChosen
    && categoriesBack.HiddenCategories.Count == 2 && categoriesBack.HiddenCategories.Contains("ores") && categoriesBack.HiddenCategories.Contains("TERRAIN"),
    "the grouping and the hidden categories come back, names without case");
var nothingHidden = new StoredState { HiddenCategoriesChosen = true };
Check(ChestRules.TryParse(ChestRules.Serialize(nothingHidden), out StoredState nothingHiddenBack, out _) && nothingHiddenBack.HiddenCategoriesChosen && nothingHiddenBack.HiddenCategories.Count == 0,
    "a player who shows every category keeps that choice");
Check(!oldFile.HiddenCategoriesChosen && !oldFile.Ungrouped, "a file from before categories has made no choice yet, so the defaults apply");

// Chest names: a chest named with category words asks for those categories.
Check(categories.BitsOfChestName("Ores") == Ores, "a chest named for a category asks for it");
Check(categories.BitsOfChestName("ores & PLANTS") == (Ores | Plants), "several categories in one name, without case");
Check(categories.BitsOfChestName("Ore") == Ores && categories.BitsOfChestName("Plant 2") == Plants, "a plural's s may be left out, and a number is just another word");
Check(categories.BitsOfChestName("Metals") == Ores && categories.BitsOfChestName("metal") == Ores, "a category answers to its words too, with or without their s");
Check(categories.BitsOfChestName("Materials") == (Ores | Plants), "a word shared by several categories asks for all of them");
Check(categories.BitsOfChestName("Seeds, Dirt") == (Plants | Terrain) && categories.BitsOfChestName("Other") == Misc, "words are split at anything that is no letter or digit");
Check(categories.BitsOfChestName("Storage 3") == 0 && categories.BitsOfChestName("") == 0 && categories.BitsOfChestName(null) == 0, "a name without a category word asks for none");
Check(categories.BitsOfChestName("Oreo") == 0 && categories.BitsOfChestName("Toolbox") == 0 && categories.BitsOfChestName("Tool") == Tools, "only whole words count");
Check(ChestRules.IsExcludedChestName("--My Stuff") && ChestRules.IsExcludedChestName("  -- keep") && !ChestRules.IsExcludedChestName("-Ores") && !ChestRules.IsExcludedChestName("Ores --") && !ChestRules.IsExcludedChestName(null),
    "a chest whose name starts with two hyphens is left alone");

// Quick deposit, first step: only stacks that are not full are topped up, the chest holding the most first.
var depositCandidates = new List<DepositCandidate>
{
    new(Entity: 1, HeldOfItem: 300, PartialRoom: 9699, FreeSlots: 0, DistanceMeters: 50, NamedForCategory: false),
    new(Entity: 2, HeldOfItem: 9999, PartialRoom: 0, FreeSlots: 5, DistanceMeters: 5, NamedForCategory: false),
    new(Entity: 3, HeldOfItem: 5000, PartialRoom: 4999, FreeSlots: 2, DistanceMeters: 80, NamedForCategory: true),
    new(Entity: 4, HeldOfItem: 0, PartialRoom: 0, FreeSlots: 10, DistanceMeters: 1, NamedForCategory: false),
    new(Entity: 5, HeldOfItem: 300, PartialRoom: 100, FreeSlots: 1, DistanceMeters: 10, NamedForCategory: false),
};
IReadOnlyList<DepositCandidate> topUps = ChestRules.TopUpOrder(depositCandidates);
Check(topUps.Count == 3 && topUps[0].Entity == 3 && topUps[1].Entity == 5 && topUps[2].Entity == 1,
    "top up: the chest with 5,000 first, then of the two with 300 the nearer; a chest whose stacks are full and a chest without the item are left out");
// Second step: a chest named for the category wins over one that only holds the item; an empty chest that asks for nothing gets nothing.
Check(ChestRules.PlaceChest(depositCandidates) == 3, "place: the chest named for the item's category");
var twoNamed = new List<DepositCandidate>
{
    new(1, 0, 0, 4, 60, true),
    new(2, 0, 0, 4, 20, true),
    new(3, 700, 0, 4, 90, true),
    new(4, 9000, 0, 4, 5, false),
};
Check(ChestRules.PlaceChest(twoNamed) == 3, "place: among chests named for the category, the one holding the most of the item");
twoNamed.RemoveAt(2);
Check(ChestRules.PlaceChest(twoNamed) == 2, "place: among chests named for the category that hold none, the nearest");
var noneNamed = new List<DepositCandidate>
{
    new(1, 300, 0, 0, 5, false),
    new(2, 300, 0, 3, 40, false),
    new(3, 900, 0, 1, 70, false),
    new(4, 0, 0, 12, 1, false),
    new(5, 2000, 0, 0, 2, true),
};
Check(ChestRules.PlaceChest(noneNamed) == 3, "place: with no free slot in the named chest, the chest holding the most of the item that has a free slot");
Check(ChestRules.PlaceChest(new List<DepositCandidate> { new(4, 0, 0, 12, 1, false), new(5, 50, 0, 0, 2, true) }) == 0,
    "place: no chest asks for the item and has room, so it stays in the backpack; the nearest empty chest is not used");
Check(ChestRules.PlaceChest(new List<DepositCandidate>()) == 0 && ChestRules.TopUpOrder(new List<DepositCandidate>()).Count == 0, "no chests, nowhere to go");

// A category added to the file later: hidden when it is one of those hidden by default, without touching the player's own choices.
var chosen = new StoredState { HiddenCategoriesChosen = true };
foreach (string name in new[] { "Ores", "Terrain" })
{
    chosen.HiddenCategories.Add(name);
}
foreach (string name in new[] { "Blocks", "Misc", "Ores", "Plants", "Terrain", "Tools" })
{
    chosen.KnownCategories.Add(name);
}
// The player had shown Blocks and Plants again. The file now also has Ingredients (hidden by default) and Weapon Gems (not).
IReadOnlyList<string> adopted = ChestRules.AdoptNewCategories(chosen, new[] { "Blocks", "Ingredients", "Misc", "Ores", "Plants", "Terrain", "Tools", "Weapon Gems" },
    new[] { "Blocks", "Ingredients", "Ores", "Plants", "Terrain" });
Check(adopted.Count == 1 && adopted[0] == "Ingredients", "of the two new categories, the one hidden by default is hidden");
Check(chosen.HiddenCategories.Count == 3 && chosen.HiddenCategories.Contains("Ingredients") && !chosen.HiddenCategories.Contains("Blocks") && !chosen.HiddenCategories.Contains("Weapon Gems"),
    "categories the player had shown again stay shown, and the new one that is not hidden by default shows");
Check(chosen.KnownCategories.Count == 8, "both new categories are known from now on");
Check(ChestRules.AdoptNewCategories(chosen, new[] { "Ingredients", "Ores" }, new[] { "Ingredients", "Ores" }).Count == 0, "a second look changes nothing");
chosen.HiddenCategories.Remove("Ingredients");
Check(ChestRules.AdoptNewCategories(chosen, new[] { "Ingredients" }, new[] { "Ingredients" }).Count == 0 && !chosen.HiddenCategories.Contains("Ingredients"),
    "a category the player shows after that is not hidden again");
var fromOlderVersion = new StoredState { HiddenCategoriesChosen = true };
fromOlderVersion.HiddenCategories.Add("Ores");
Check(ChestRules.AdoptNewCategories(fromOlderVersion, new[] { "Ingredients", "Ores", "Tools" }, new[] { "Ingredients", "Ores" }).Count == 1 && fromOlderVersion.HiddenCategories.Count == 2,
    "a state from before categories were remembered takes the defaults once");
Check(ChestRules.TryParse(ChestRules.Serialize(chosen), out StoredState knownBack, out _) && knownBack.KnownCategories.SetEquals(chosen.KnownCategories), "the known categories come back from the state file");

// A tool's slot reads as what the tool has selected.
Check(ChestRules.IsToolEntry("Blueprint_Voxel_Block_Column_4m") && ChestRules.IsToolEntry("NPC_Summon_Skeleton") && ChestRules.IsToolEntry("_no_blueprint_flag_Blueprint_Terrain_Whitebox_4x4_Cube"),
    "the Construction Hammer's shapes and the summoning staff's helpers are tool entries");
Check(!ChestRules.IsToolEntry("Material_T1_Wood_Logs") && !ChestRules.IsToolEntry("Tool_T1_Pickaxe_Scrappy") && !ChestRules.IsToolEntry(null) && !ChestRules.IsToolEntry(""),
    "ordinary items, and items without a name, are not");

// Headers: favorites, then the categories in the file's order, then what is in none. A line is under one header only.
ItemRow Line(string name, ulong lineCategories, bool favorite = false) => new(name, 1, name, 1, 1, 0, -1, false, favorite, new List<StackView>(), lineCategories);
var mixed = new List<ItemRow>
{
    Line("Amber", Ores), Line("Bandage", 0), Line("Copper Bars", Ores, favorite: true), Line("Dirt", Terrain), Line("Flax", Plants), Line("Iron Ore", Ores),
    Line("Key", Misc), Line("Odd Thing", 0), Line("Reed", Plants | Tools), Line("Torch", Tools, favorite: true),
};
string[] sixNames = { "Blocks", "Misc", "Ores", "Plants", "Terrain", "Tools" };
IReadOnlyList<RowGroup> groups = ChestRules.GroupByCategory(mixed, sixNames);
Check(groups.Count == 6 && groups[0].Title == "Favorites" && groups[1].Title == "Misc" && groups[2].Title == "Ores" && groups[3].Title == "Plants" && groups[4].Title == "Terrain" && groups[5].Title == "Other",
    "favorites first, then the categories that have lines in the file's order, then Other; Blocks and Tools have no line left and no header");
Check(groups[0].Rows.Count == 2 && groups[0].Rows[0].Name == "Copper Bars" && groups[0].Rows[1].Name == "Torch", "a favorite is under Favorites only, in the lines' own order");
Check(groups[2].Rows.Count == 2 && groups[2].Rows[0].Name == "Amber" && groups[2].Rows[1].Name == "Iron Ore", "Ores has its two lines that are not favorites");
Check(groups[3].Rows.Count == 2 && groups[3].Rows[1].Name == "Reed", "a line in two categories is under the first of them");
Check(groups[5].Rows.Count == 2 && groups[5].Rows[0].Name == "Bandage" && groups[5].Rows[1].Name == "Odd Thing", "lines in no category are under Other");
IReadOnlyList<ItemRow> inOrder = ChestRules.OrderByCategory(mixed, sixNames);
Check(inOrder.Count == mixed.Count && inOrder[0].Name == "Copper Bars" && inOrder[2].Name == "Key" && inOrder[3].Name == "Amber" && inOrder[9].Name == "Odd Thing", "one chest's lines take the same order without headers");
Check(ChestRules.GroupByCategory(new List<ItemRow>(), sixNames).Count == 0, "no lines, no headers");
IReadOnlyList<RowGroup> noFile = ChestRules.GroupByCategory(mixed, Array.Empty<string>());
Check(noFile.Count == 2 && noFile[0].Title == "Favorites" && noFile[1].Rows.Count == 8, "without categories there are only the favorites and the rest");
Check(ChestRules.FirstCategory(0) == -1 && ChestRules.FirstCategory(Ores) == 2 && ChestRules.FirstCategory(Plants | Tools) == 3, "an item's category is the first of those it is in");

// What the player used: only a Quick Stack Station starts a quick deposit.
Check(ChestRules.IsQuickStackStation(new Guid("f535f8e8-49f5-413e-819d-1db473286521"), null) && ChestRules.IsQuickStackStation(new Guid("89fddfce-08ed-4ceb-8592-8f6597e3b154"), null),
    "both Quick Stack Stations are told by their template ids");
Check(ChestRules.IsQuickStackStation(Guid.NewGuid(), "Prop_Storage_Loot_Drop_Station_32x32"), "a station a later game version adds is told by its name");
Check(!ChestRules.IsQuickStackStation(new Guid("955ba32f-08ea-414d-bd98-ac75990aef04"), "Prop_Decoration_T1_Storage_24_Magic") && !ChestRules.IsQuickStackStation(Guid.NewGuid(), "Craftingstation_Workbench")
    && !ChestRules.IsQuickStackStation(null, null), "a chest, a workbench and something unknown are not stations");

// Where a slot of the game's own screens is, in pixels. The numbers are the user's 4K screen: a canvas of 1920 x 1080 units,
// a viewport of 3840 x 2160 pixels, 2 pixels per unit, and the backpack's first slot seen at 411, 229, 176 pixels wide.
PixelRect? firstSlot = ChestRules.SlotPixels(205.5f, 114.5f, 88, 88, 0, 0, 3840, 2160, 2, 3840, 2160);
Check(firstSlot is PixelRect at4K && at4K.Left == 411 && at4K.Top == 229 && at4K.Right == 587 && at4K.Bottom == 405, "at 4K a unit is two pixels, not four");
Check(firstSlot!.Value.Contains(411, 229) && firstSlot.Value.Contains(586.9f, 404.9f) && !firstSlot.Value.Contains(587, 300) && !firstSlot.Value.Contains(410.9f, 300) && !firstSlot.Value.Contains(822, 458),
    "a pointer is on the slot from its left and top edges up to its right and bottom ones, and not at twice its place");
PixelRect? halfWindow = ChestRules.SlotPixels(205.5f, 114.5f, 88, 88, 0, 0, 3840, 2160, 2, 1920, 1080);
Check(halfWindow is PixelRect small && small.Left == 205.5f && small.Top == 114.5f && small.Right == 293.5f, "a target half the picture's size halves the pixels");
PixelRect? withBars = ChestRules.SlotPixels(100, 100, 50, 50, 240, 0, 1440, 1080, 0.75f, 1920, 1080);
Check(withBars is PixelRect barred && barred.Left == 315 && barred.Top == 75 && barred.Right == 352.5f && barred.Bottom == 112.5f, "a viewport with bars at its sides starts after the bar");
Check(ChestRules.SlotPixels(10, 10, 0, 50, 0, 0, 1920, 1080, 1, 1920, 1080) == null && ChestRules.SlotPixels(10, 10, 50, 50, 0, 0, 1920, 1080, 0, 1920, 1080) == null
    && ChestRules.SlotPixels(10, 10, 50, 50, 0, 0, 0, 1080, 1, 1920, 1080) == null && ChestRules.SlotPixels(float.NaN, 10, 50, 50, 0, 0, 1920, 1080, 1, 1920, 1080) == null
    && ChestRules.SlotPixels(10, 10, 50, 50, 0, 0, 1920, 1080, 1, 0, 0) == null, "numbers that can't be right give no rectangle");

// The stack a used item comes from.
var foodStacks = new List<StackView> { new(1, 100, 10, 12, 0, 0, false), new(2, 200, 10, 3, 0, 0, false), new(3, 300, 10, 0, 0, 0, false), new(4, 400, 10, 7, 0, 0, false) };
Check(ChestRules.SmallestStack(foodStacks)?.Holder == 2 && ChestRules.SmallestStack(new List<StackView>()) == null, "a used item comes from the smallest stack that holds any");

// Item details: durations, lines and the game's markup.
Check(ChestRules.FormatDuration(45) == "45s" && ChestRules.FormatDuration(1200) == "20min" && ChestRules.FormatDuration(90) == "1min 30s"
    && ChestRules.FormatDuration(3600) == "1h" && ChestRules.FormatDuration(5400) == "1h 30min" && ChestRules.FormatDuration(0) == "0s"
    && ChestRules.FormatDuration(double.NaN) == "0s", "durations read as the game writes them");
Check(ChestRules.DetailLine("Max Health", "+40") == "Max Health: +40" && ChestRules.DetailLine(null, "+40") == "+40" && ChestRules.DetailLine("Immune To Poison", "") == "Immune To Poison"
    && ChestRules.DetailLine("  Duration ", " 20min ") == "Duration: 20min", "a detail line is the label and the value, or the one that is there");
Check(ChestRules.PlainText("<b>Bold</b> text") == "Bold text" && ChestRules.PlainText("2 < 3 and 4 > 1") == "2 < 3 and 4 > 1" && ChestRules.PlainText(null) == ""
    && ChestRules.PlainText("<lorename>Flame</lorename>") == "Flame", "the game's markup is taken out, other angle brackets stay");

// The category tree: the game's data for most items, the hand-made list for materials, the game's category for unknown items.
const string treeText = """
{ "build": "1",
  "sections": ["Gear/Weapons", "Gear/Tools", "Consumables/Food", "Placeables/Stations", "Placeables/Decor", "Building/Blocks", "Other/Weapon Gems", "Other/Lore", "Other/Miscellaneous", "Materials"],
  "items": [[100, 0, "1 Sword|02|Sword_T2"], [101, 0, "1 Axe|01|Axe_T1"], [102, 1, "Key|99|Key_Gold"], [103, 3, "Factory|02|Smelter"],
            [104, 9, ""], [105, 9, ""], [106, 2, "Food|01|Bread"], [107, 5, "x|01|Block"], [108, 42, "bad section"]] }
""";
string[] handNames = { "Ores", "Wood", "Food", "Misc" };
bool[] handMaterial = { true, true, false, false };
Check(CategoryTree.TryParse(treeText, handNames, handMaterial, out CategoryTree tree, out string treeError), "the category table reads: " + treeError);
Check(tree.Groups.Count == 6 && tree.Groups[0] == "Gear" && tree.Groups[5] == "Materials", "groups keep the table's order, materials last");
int SectionNamed(string key) { foreach (TreeSection section in tree.Sections) { if (section.Key == key) return section.Index; } return -9; }
Check(tree.Place(102, 0, "Materials").Section == SectionNamed("Gear/Tools"), "an item the table files (a key) goes where the table says, whatever its game category");
Check(tree.Place(104, 0b01, null).Section == SectionNamed("Materials/Ores") && tree.Place(105, 0b10, null).Section == SectionNamed("Materials/Wood")
    && tree.Place(105, 0b1000, null).Section == SectionNamed("Materials/Other Materials"), "materials go by the hand-made material categories, else Other Materials");
Check(tree.Place(999, 0, "Weapons").Section == SectionNamed("Gear/Weapons") && tree.Place(998, 0, "WeaponGem").Section == SectionNamed("Other/Weapon Gems")
    && tree.Place(997, 0b01, "Materials").Section == SectionNamed("Materials/Ores") && tree.Place(996, 0, "Currency").Section == SectionNamed("Other/Miscellaneous"),
    "an item the table does not know goes by the game's category");
Check(tree.Place(108, 0, "Weapons").Section == SectionNamed("Gear/Weapons"), "a row naming a section the table does not have is ignored");
var hiddenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Materials", "Gear/Tools" };
Check(tree.IsHidden(SectionNamed("Materials/Wood"), hiddenKeys) && tree.IsHidden(SectionNamed("Gear/Tools"), hiddenKeys) && !tree.IsHidden(SectionNamed("Gear/Weapons"), hiddenKeys),
    "a section is hidden by its own key or its group's");
Check(tree.SectionsOfChestName("Tools").SetEquals(new[] { SectionNamed("Gear/Tools") }) && tree.SectionsOfChestName(" gear ").Count == 2
    && tree.SectionsOfChestName("Station").Count == 0 && tree.SectionsOfChestName("stations").Contains(SectionNamed("Placeables/Stations"))
    && tree.SectionsOfChestName("Ores").SetEquals(new[] { SectionNamed("Materials/Ores") }) && tree.SectionsOfChestName("Gems & Lore").Count == 0 && tree.SectionsOfChestName("Tools 2").Count == 0,
    "a chest's name asks for a sub-category or a whole group by its exact name (case and outer spaces aside), materials included");
ItemRow TreeRow(uint item, string name, bool favorite = false) => new(name, item, name, 1, 1, 0, -1, false, favorite, new List<StackView>());
var treeRows = new List<ItemRow> { TreeRow(100, "Sword"), TreeRow(101, "Axe", favorite: true), TreeRow(103, "Smelter"), TreeRow(104, "Iron Ore"), TreeRow(106, "Bread") };
var places = new Dictionary<uint, (int Section, string Order)>();
foreach (ItemRow treeRow in treeRows) { places[treeRow.Item] = tree.Place(treeRow.Item, treeRow.Item == 104 ? 0b01ul : 0, null); }
IReadOnlyList<TreeGroup> treeGroups = tree.Group(treeRows, r => places[r.Item].Section, r => places[r.Item].Order);
Check(treeGroups.Count == 5 && treeGroups[0].Key == CategoryTree.FavoritesKey && treeGroups[0].Rows.Count == 1 && treeGroups[1].Key == "Gear/Weapons"
    && treeGroups[1].Rows.Count == 2 && treeGroups[1].Rows[0].Name == "Axe" && treeGroups[2].Key == "Consumables/Food" && treeGroups[3].Key == "Placeables/Stations"
    && treeGroups[4].Key == "Materials/Ores", "favorites first and also in their own section; sections in the tree's order; inside one, by kind before name");
IReadOnlyList<ItemRow> treeOrdered = tree.Ordered(treeRows, r => places[r.Item].Section, r => places[r.Item].Order);
Check(treeOrdered[0].Name == "Axe" && treeOrdered[1].Name == "Sword" && treeOrdered[4].Name == "Iron Ore", "one chest's rows follow the same order without headers");
Check(ChestRules.NameFamily("Skull Summoning Vessel II") == "Skull Summoning Vessel" && ChestRules.NameFamily("Iron Bar 3") == "Iron Bar"
    && ChestRules.NameFamily("Vile Ichor") == "Vile Ichor" && ChestRules.NameFamily("III") == "III" && ChestRules.NameFamily(null) == "", "a name's family drops a last numeral word");
var familyRows = new List<ItemRow> { TreeRow(200, "Skull Summoning Vessel II"), TreeRow(201, "Bone Arrow"), TreeRow(202, "Skull Summoning Vessel I") };
var familyOrders = new Dictionary<uint, string> { [200] = "1 Spell|05|B", [201] = "1 Spell|03|A", [202] = "1 Spell|02|C" };
IReadOnlyList<ItemRow> familyOrdered = tree.Ordered(familyRows, r => 0, r => familyOrders[r.Item]);
Check(familyOrdered[0].Name == "Skull Summoning Vessel I" && familyOrdered[1].Name == "Skull Summoning Vessel II" && familyOrdered[2].Name == "Bone Arrow",
    "one item's ranks sit together, at the family's lowest tier");
// Deposit and salvage rules (0.10.26).
Check(ChestRules.DepositAllowed(GearKind.Weapons, 4, 30, ChestRules.DefaultDepositRules) && !ChestRules.DepositAllowed(GearKind.Weapons, 3, 30, ChestRules.DefaultDepositRules)
    && ChestRules.DepositAllowed(GearKind.Rings, 2, 5, new[] { new GearRule(GearKind.Rings, 2, 5) }) && !ChestRules.DepositAllowed(GearKind.Rings, 2, 4, new[] { new GearRule(GearKind.Rings, 2, 5) })
    && !ChestRules.DepositAllowed(GearKind.Armor, 4, 30, new[] { new GearRule(GearKind.Rings, 0, 0) }) && !ChestRules.DepositAllowed(GearKind.Armor, 4, 30, Array.Empty<GearRule>()),
    "a deposit rule takes its kind of its rarity or better and its level or more; no rule, no gear");
Check(ChestRules.SalvageWanted(GearKind.Armor, 0, 50, ChestRules.DefaultSalvageRules) && !ChestRules.SalvageWanted(GearKind.Armor, 1, 50, ChestRules.DefaultSalvageRules)
    && ChestRules.SalvageWanted(GearKind.Weapons, -1, 10, ChestRules.DefaultSalvageRules) && !ChestRules.SalvageWanted(GearKind.Weapons, 2, 10, new[] { new GearRule(GearKind.Weapons, 2, 9) })
    && ChestRules.SalvageWanted(GearKind.Weapons, 1, 9, new[] { new GearRule(GearKind.Weapons, 2, 9) }) && !ChestRules.SalvageWanted(GearKind.Shields, 0, 1, new[] { new GearRule(GearKind.Weapons, 4, 0) }),
    "a salvage rule takes its kind of its rarity or worse and its level or less (0: any); no rarity is Common");
Check(ChestRules.GearKindOfSection("Gear/Cosmetics") == null && ChestRules.GearKindOfSection("Gear/Rings") == GearKind.Rings && ChestRules.GearKindOfSection("Gear/Tools") == null
    && ChestRules.GearKindOfSection(null) == null, "the rules cover weapons, shields, armor and rings, not cosmetics or tools");
Check(ChestRules.TryParse("{\"depositRules\":[[4,1,0],[5,2,3]]}", out StoredState oldCosmetics, out _) && oldCosmetics.DepositRules.Count == 1
    && oldCosmetics.DepositRules[0] == new GearRule(GearKind.Rings, 2, 3), "a 0.10.26 rule for cosmetics is dropped; rings keep their number");
var balancing = new SalvageBalancing(8, 0.15f, 5, 20, 1f, 0.75f, 0.5f, 0x41a792dc);
// Cost(n) = round(8 * 0.15 * n + 8): level 10, 2 slots carried, none unlocked: Cost(10) = 20, x2 = 40.
Check(ChestRules.SalvageRunes(10, 2, 0, GearKind.Weapons, balancing) == 40, "a piece's carried perk slots pay their upgrade cost");
// Armor x0.75: Cost(10) = round(20 * 0.75) = 15, x2 = 30.
Check(ChestRules.SalvageRunes(10, 2, 0, GearKind.Armor, balancing) == 30, "armor pays three quarters");
// One slot unlocked at level 10: carried Cost(9) = round(18.8) = 19 x 1; trashed share 0.15 * 10 + 5 = 6.5, round(20 * 6.5) = 130, x 0.5 = 65.
Check(ChestRules.SalvageRunes(10, 1, 1, GearKind.Weapons, balancing) == 84, "an unlocked slot gives its share at the recycling factor");
string ruleText = """{ "salvage": { "seed": 8, "creep": 0.15, "trashStart": 5, "trashEnd": 20, "weaponsShield": 1.0, "armor": 0.75, "rings": 0.5, "runeItem": 1101501148 } }""";
Check(ChestRules.ParseSalvage(ruleText) is { Seed: 8, RuneItem: 1101501148 } && ChestRules.ParseSalvage("{}") == null, "the table's salvage balancing is read");
var ruleState = new StoredState();
Check(ruleState.DepositRules.SequenceEqual(ChestRules.DefaultDepositRules) && ruleState.SalvageRules.SequenceEqual(ChestRules.DefaultSalvageRules), "a new state has the default rules");
ruleState.DepositRules.Add(new GearRule(GearKind.Rings, 2, 30));
ruleState.SalvageRules.Clear();
Check(ChestRules.TryParse(ChestRules.Serialize(ruleState), out StoredState rulesBack, out _) && rulesBack.DepositRules.Count == 2 && rulesBack.DepositRules[1] == new GearRule(GearKind.Rings, 2, 30)
    && rulesBack.SalvageRules.Count == 0, "the rules are saved and read back, an emptied list too");
Check(ChestRules.TryParse("{\"links\":[],\"favorites\":[]}", out StoredState noRules, out _) && noRules.DepositRules.SequenceEqual(ChestRules.DefaultDepositRules), "a file without rules reads the defaults");
// Gear favorites by rarity (0.10.6): the Legendary staff is a favorite, the Uncommon one is not.
var gearState = new StoredState();
ChestRules.SetFavorite(gearState, 300, 4, isGear: true, favorite: true);
ChestRules.SetFavorite(gearState, 301, -1, isGear: false, favorite: true);
FavoriteSet gearSet = gearState.FavoriteSet();
Check(gearSet.Contains(300, 4, true) && !gearSet.Contains(300, 1, true) && gearSet.Contains(301, -1, false) && gearSet.GearItems.Contains(300u)
    && !gearSet.ContainsItem(300), "a gear favorite is one rarity; a plain favorite is its item");
var staffRows = ChestRules.BuildRows(new[] { new StackView(1, 1, 300, 1, 40, 4, true), new StackView(1, 2, 300, 1, 10, 1, true) }, _ => "Staff", gearSet);
Check(staffRows.Count == 2 && staffRows[0].IsFavorite && staffRows[0].Rarity == 4 && !staffRows[1].IsFavorite, "the rows mark only the favorite rarity");
Check(ChestRules.TryParse(ChestRules.Serialize(gearState), out StoredState gearRoundTrip, out _) && gearRoundTrip.GearFavorites.SetEquals(gearState.GearFavorites) && gearRoundTrip.Favorites.SetEquals(new uint[] { 301 }), "gear favorites are saved and read back");
var oldState = new StoredState();
oldState.Favorites.Add(400);
oldState.Favorites.Add(401);
int movedOld = ChestRules.MoveOldGearFavorites(oldState, new[] { new StackView(1, 1, 400, 1, 10, 1, true), new StackView(2, 1, 400, 1, 30, 4, true), new StackView(1, 3, 401, 5, 0, -1, false) });
Check(movedOld == 1 && !oldState.Favorites.Contains(400) && oldState.GearFavorites.Contains(FavoriteSet.GearKey(400, 4)) && oldState.Favorites.Contains(401),
    "an older gear favorite moves to the highest rarity seen; a plain one stays");
ChestRules.SetFavorite(oldState, 400, 4, isGear: true, favorite: false);
Check(oldState.GearFavorites.Count == 0, "unmarking gear removes that rarity");
string? missingNames = ChestRules.MissingChestNames(new List<(string, string, bool)>
{
    ("Iron Ore", "Ores", false), ("Copper Ore", "ores", false), ("Iron Sword", "Weapons", false), ("Rope", "Tools", true), ("Odd Thing", "", false),
});
Check(missingNames == "No chest named Ores (Iron Ore, Copper Ore), Weapons (Iron Sword). Full: Tools (Rope). No chest for Odd Thing.", "the missing chest names: " + missingNames);
Check(ChestRules.MissingChestNames(new List<(string, string, bool)>()) == null, "nothing stayed, nothing to say");
Check(ChestRules.MissingChestNames(new List<(string, string, bool)> { ("A", "X", false), ("B", "X", false), ("C", "Y", false) }, maxNames: 1, maxItemsPerName: 1)
    == "No chest named X (A +1), and 1 more.", "long lists are cut");
Check(ChestRules.ToolKeyOf("Blueprint_Wall_Stone") == ChestRules.ConstructionHammerKey && ChestRules.ToolKeyOf("_no_blueprint") == ChestRules.ConstructionHammerKey
    && ChestRules.ToolKeyOf("NPC_Crafter_Barber01") == ChestRules.SummoningStaffKey && ChestRules.ToolKeyOf("Tool_Quickbuilder_Hammer") == 0 && ChestRules.ToolKeyOf(null) == 0
    && ChestRules.ToolKeyOf("Building_Overgrowth_Shape_Big") == ChestRules.OvergrowthToolKey && ChestRules.ToolKeyOf("Building_Decay_Shape_Small") == ChestRules.DecayToolKey
    && ChestRules.ToolKeyOf("Material_T1_Overgrowth_Ivy") == 0 && ChestRules.ToolItemOf(ChestRules.DecayToolKey) != 0 && ChestRules.ToolItemOf(ChestRules.SummoningStaffKey) == 0,
    "a hammer's shape and a staff's helper stand for their tool");
var toolState = new StoredState();
toolState.Favorites.Add(ChestRules.ConstructionHammerKey);
FavoriteSet toolSet = toolState.FavoriteSet(new Dictionary<uint, uint> { [500] = ChestRules.ConstructionHammerKey, [501] = ChestRules.SummoningStaffKey });
Check(toolSet.Contains(500, -1, false) && !toolSet.Contains(501, -1, false) && !toolSet.Contains(502, -1, false), "a hammer's shape is a favorite when the hammer is");
Check(ChestRules.IsExcludedChestName("--Spare") && !ChestRules.IsExcludedChestName("Ores"), "a chest named with -- in front is kept out of deposits and re-sorts");
IReadOnlyList<ItemRow> favoritesFirst = ChestRules.FavoritesFirst(new List<ItemRow> { TreeRow(1, "A"), TreeRow(2, "B", favorite: true), TreeRow(3, "C"), TreeRow(4, "D", favorite: true) });
Check(favoritesFirst[0].Name == "B" && favoritesFirst[1].Name == "D" && favoritesFirst[2].Name == "A" && favoritesFirst[3].Name == "C", "favorites move to the top, each part keeps its order");
// A chest named for a sub-category comes before one named for its group (0.10.16).
Dictionary<int, int> gearRanks = tree.SectionRanksOfChestName("Gear");
Check(tree.SectionRanksOfChestName("Food")[SectionNamed("Consumables/Food")] == CategoryTree.SectionNameRank && gearRanks[SectionNamed("Gear/Tools")] == CategoryTree.GroupNameRank
    && tree.SectionRanksOfChestName("Food Stuff").Count == 0 && tree.SectionRanksOfChestName("Weapon Gems").Keys.SequenceEqual(new[] { SectionNamed("Other/Weapon Gems") }),
    "a sub-category's exact name ranks above its group's; no other name asks for anything");
IReadOnlyList<DepositCandidate> bestNamed = ChestRules.BestNamed(new List<DepositCandidate>
{
    new(1, 10, 5, 3, 1, true, CategoryTree.WordRank), new(2, 0, 0, 3, 9, true, CategoryTree.SectionNameRank), new(3, 50, 50, 3, 2, false),
});
Check(bestNamed.Count == 1 && bestNamed[0].Entity == 2, "only the chests of the best name are used");
Check(ChestRules.BestNamed(new List<DepositCandidate> { new(3, 50, 50, 3, 2, false), new(4, 0, 0, 3, 2, false) }).Count == 2, "no named chest: all of them");
IReadOnlyList<DepositCandidate> fullWeapons = ChestRules.BestNamed(new List<DepositCandidate>
{
    new(1, 10, 0, 0, 1, true, CategoryTree.SectionNameRank), new(2, 0, 0, 4, 9, true, CategoryTree.GroupNameRank), new(3, 0, 0, 5, 2, false),
});
Check(fullWeapons.Count == 1 && fullWeapons[0].Entity == 2, "a full Weapons chest lets the Gear chest take the item");
Check(ChestRules.BestNamed(new List<DepositCandidate> { new(1, 10, 0, 0, 1, true, CategoryTree.SectionNameRank), new(2, 0, 0, 0, 9, true, CategoryTree.GroupNameRank) }).Count == 1,
    "all named chests full: the best rank still (the item stays and is reported as Full)");
// A chest named All takes any item, but only when no other chest's name asks for it or those chests are full (1.0.1).
Check(ChestRules.IsCatchAllName(" all ") && ChestRules.IsCatchAllName("All") && !ChestRules.IsCatchAllName("All Ores") && !ChestRules.IsCatchAllName(null),
    "All is the catch-all's name, in any case");
IReadOnlyList<DepositCandidate> allAndGear = ChestRules.BestNamed(new List<DepositCandidate>
{
    new(1, 0, 0, 9, 1, true, CategoryTree.CatchAllRank), new(2, 0, 0, 3, 9, true, CategoryTree.GroupNameRank), new(3, 50, 50, 3, 2, false),
});
Check(allAndGear.Count == 1 && allAndGear[0].Entity == 2, "a chest named for the item comes before the All chest");
IReadOnlyList<DepositCandidate> allOnly = ChestRules.BestNamed(new List<DepositCandidate>
{
    new(1, 0, 0, 9, 9, true, CategoryTree.CatchAllRank), new(3, 50, 50, 3, 2, false),
});
Check(allOnly.Count == 1 && ChestRules.PlaceChest(allOnly) == 1, "no chest named for the item: the All chest takes it");
IReadOnlyList<DepositCandidate> fullOres = ChestRules.BestNamed(new List<DepositCandidate>
{
    new(1, 10, 0, 0, 1, true, CategoryTree.SectionNameRank), new(2, 0, 0, 5, 9, true, CategoryTree.CatchAllRank),
});
Check(fullOres.Count == 1 && fullOres[0].Entity == 2, "a full Ores chest lets the All chest take the item");
Check(ChestRules.BestNamed(new List<DepositCandidate> { new(1, 0, 0, 5, 1, true, CategoryTree.CatchAllRank), new(2, 0, 0, 3, 9, true) })[0].Entity == 2,
    "a hand-made category's chest comes before the All chest");
Check(!CategoryTree.TryParse("{}", handNames, handMaterial, out _, out _) && !CategoryTree.TryParse(null, handNames, handMaterial, out _, out _), "no table, no tree");
var treeState = new StoredState { HiddenTreeChosen = true };
treeState.HiddenTree.Add("Materials");
treeState.Collapsed.Add("Gear/Weapons");
Check(ChestRules.TryParse(ChestRules.Serialize(treeState), out StoredState treeRead, out _) && treeRead.HiddenTreeChosen && treeRead.HiddenTree.Contains("materials")
    && treeRead.Collapsed.Contains("Gear/Weapons"), "the tree's hidden and folded lists are saved and read back");

Console.WriteLine(failures == 0 ? "ChestRules: all checks passed" : $"ChestRules: {failures} check(s) failed");
return failures == 0 ? 0 : 1;
