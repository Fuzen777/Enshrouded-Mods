using Kiln;

namespace StreamlinedLogistics;

/// <summary>The player's settings: kiln config streamlined-logistics-client.json in the profile's config folder, changed from the Kiln menu or by saving the file. All of them apply while playing.</summary>
public sealed class ChestSettings
{
    [ConfigKey("craftFromLinkedChests")]
    [Comment("Craft From Linked Chests. On: crafting at a station, an NPC or from your inventory uses what your linked chests hold, in any base, after your backpack and the base's own chests; the tinting screen offers their dyes. Needs Kiln 0.2.0-dev.30.6.")]
    public bool CraftFromLinkedChests { get; set; } = true;

    [ConfigKey("fillStationsFromLinkedChests")]
    [Comment("Fill Stations From Linked Chests. On: a production station's +1 and +10 also take from your linked chests, in any base, what your backpack and the base's own chests can't cover. Needs Craft From Linked Chests and Kiln 0.2.0-dev.31.12.")]
    public bool FillStationsFromLinkedChests { get; set; } = true;

    [ConfigKey("buildFromLinkedChests")]
    [Comment("Build From Linked Chests. On: the Construction Hammer and the build menu count and use the blocks and materials your linked chests hold, in any base, after your backpack and the base's own chests. Needs Craft From Linked Chests and Kiln 0.2.0-dev.31.12.")]
    public bool BuildFromLinkedChests { get; set; } = true;

    [ConfigKey("locksFromLinkedChests")]
    [Comment("Lockpicks From Linked Chests. On: a lock that needs a lockpick or a key opens with one held in your linked chests, and takes it from there when your backpack has none. Needs Craft From Linked Chests and Kiln 0.2.0-dev.31.12.")]
    public bool LocksFromLinkedChests { get; set; } = true;

    [ConfigKey("linkRegularChests")]
    [Comment("Link Regular Chests. Off: only Magic Chests are offered for linking. On: regular chests too.")]
    public bool LinkRegularChests { get; set; }

    [ConfigKey("favoritesStayInBackpack")]
    [Comment("Favorites Stay In Backpack. On: the game's own Deposit Stacks, Deposit All and Quick Stack Station leave your favorite items in your backpack.")]
    public bool FavoritesStayInBackpack { get; set; } = true;

    [ConfigKey("searchAllTabs")]
    [Comment("Search Every Tab. On: while a search is typed, the grouped list shows what it finds in every category, whatever tab is open. Off: only in the open tab.")]
    public bool SearchAllTabs { get; set; } = true;

    [ConfigKey("allFavoritesInTabs")]
    [Comment("All Favorites In Every Tab. Off: a category tab of the grouped list shows its own favorites at the top. On: it shows all of them.")]
    public bool AllFavoritesInTabs { get; set; }

    [ConfigKey("favoritesInGameScreens")]
    [Comment("Favorites In The Game's Screens. On: a favorite's slot is gold in the game's own inventory and chest screens, and Shift + RMB on a slot there marks or unmarks its item.")]
    public bool FavoritesInGameScreens { get; set; } = true;

    [ConfigKey("quickDeposit")]
    [Comment("Quick Deposit. On: inside a base, Sort Inventory pressed twice (G by default) or a Quick Stack Station puts your backpack's items into the base's chests: stacks the chests already hold are topped up, and the rest goes to a chest named for its category or already holding the item. Stays off while the Greeneye Quick Deposit mod runs.")]
    public bool QuickDeposit { get; set; } = true;

    [ConfigKey("quickDepositOutsideBase")]
    [Comment("Quick Deposit Outside A Base. Off: Quick Deposit only works inside a base. On: outside a base it puts your items into your linked chests instead, by the same rules.")]
    public bool QuickDepositOutsideBase { get; set; }

    [ConfigKey("depositReportInChat")]
    [Comment("Deposit Report In Chat. On: after a quick deposit, your chat says which chest got what and what stayed. The same is always written to quick-deposit-report.txt.")]
    public bool DepositReportInChat { get; set; } = true;

    [ConfigKey("autoSalvage")]
    [Comment("Auto Salvage. On: gear you loot that a salvage rule takes (the rules are in the Settings tab of the mod's screen) is salvaged into runes right after it lands in your backpack, with the game's own Trash yield. Favorites, worn gear, gear with a gem and gear in the hotbars are never salvaged.")]
    public bool AutoSalvage { get; set; }

    [ConfigKey("autoQuickDeposit")]
    [Comment("Auto Quick Deposit. On: when looting fills your backpack (taking an item lying in the world, harvesting, looting a container in the world), a quick deposit runs by itself, by the same rules. Taking from your own storage chests and crafting never start it. Outside a base it needs Quick Deposit Outside A Base.")]
    public bool AutoQuickDeposit { get; set; }

    [ConfigKey("autoNameChests")]
    [Comment("Auto-Name Chests. 0 Off. 1 Children: when Quick Deposit or Re-Sort finds no chest named for an item with room, the closest chest that still has its default name (and a free slot) is named for the item's sub-category (\"Weapons\"). 2 Parent: named for its group (\"Gear\").")]
    [Range(0, 2)]
    public int AutoNameChests { get; set; }

    [ConfigKey("autoStationsCrafts")]
    [Comment("Auto-Stations Crafts. For every automated station of a base with a Magic Chest named Auto-Stations, the base's Magic Chests are kept holding the inputs of this many crafts of the station's selected recipe, brought from your linked chests in other bases into the Auto-Stations chest. 0 turns it off.")]
    [Range(0, 100)]
    public int AutoStationsCrafts { get; set; } = 10;

    [ConfigKey("autoStationsSeconds")]
    [Comment("Auto-Stations Every (s). How often the Auto-Stations chests are topped up.")]
    [Range(5, 300)]
    public int AutoStationsSeconds { get; set; } = 30;

    [ConfigKey("highlightDepositedChests")]
    [Comment("Highlight Chests After A Deposit. On: the chests a quick deposit put items into are lit while your inventory is open and then for the Highlight Time. The chests themselves get the game's outline when Streamlined Logistics Glow is installed (greeneye\\streamlined-logistics-glow); otherwise each gets a box and its name.")]
    public bool HighlightDepositedChests { get; set; } = true;

    [ConfigKey("highlightSeconds")]
    [Comment("Highlight Time (s). How long a chest clicked in the Chests list stays marked in the world, counted from the moment the screen closes, and how long the chests of a quick deposit stay lit once your inventory is closed.")]
    [Range(ChestRules.MinHighlightSeconds, ChestRules.MaxHighlightSeconds)]
    public int HighlightSeconds { get; set; } = ChestRules.DefaultHighlightSeconds;

    [ConfigKey("linkRadiusMeters")]
    [Comment("Link Radius (m). How far from your character Link Nearby Chests reaches.")]
    [Range(ChestRules.MinLinkRadiusMeters, ChestRules.MaxLinkRadiusMeters)]
    public int LinkRadiusMeters { get; set; } = (int)ChestRules.DefaultLinkRadiusMeters;
}
