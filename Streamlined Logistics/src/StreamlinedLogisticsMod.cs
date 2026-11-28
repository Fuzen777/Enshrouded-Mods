using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Kiln;

namespace StreamlinedLogistics;

/// <summary>
/// Streamlined Logistics on Kiln. The player links storage chests; the Open Streamlined Logistics key then shows, from anywhere, everything the
/// linked chests hold as a list or a grid, with a search field and favorites first. Taking moves stacks into the backpack, storing
/// moves backpack items into a linked chest that already holds the item, else one with a free slot.
/// Every move is Kiln's own slot move, which runs in the game's inventory transaction, so gear keeps its level and upgrades.
/// A move into the player always names its backpack slot: left to itself, Kiln would also use the inventories that tools carry.
/// It works in a world the player hosts: the screen is drawn in the player's game and the moves run in that game's server world.
/// </summary>
public sealed class StreamlinedLogisticsMod : Mod
{
    private const string OpenBindingId = "streamlined-logistics.open";
    private const string OpenDefaultKey = "F2";
    private const string StateFileName = "streamlined-logistics.json";
    // The mod was called Remote Chests (id remote-chests) up to 0.7.1: its settings, links, favorites and key are taken over once.
    private const string FormerModId = "remote-chests";
    private const string FormerStateFileName = "remote-chests.json";
    // The category list Quick Deposit and shroudlink's Assistants use, next to the game's exe.
    private const string CategoriesFileName = "chestsort_categories.json";
    // The same file bundled with the mod (since 0.10.7), used when the game folder has none.
    private const string BundledCategoriesResource = "StreamlinedLogistics.chestsort_categories.json";
    private static readonly DateTime BundledCategoriesTime = DateTime.MinValue.AddTicks(1);
    // The client's Sort Inventory command, by the game's own field name; its bytes are a version and, at 4, the inventory's owner.
    private const string SortActionName = "sortInventoryAction";
    private const int SortTargetOffset = 4;
    // The second Sort Inventory must follow the first this quickly to mean a quick deposit.
    private const long DoubleSortMilliseconds = 500;
    // A quick deposit follows a Quick Stack Station's own deposit after this long.
    private const long StationSettleMilliseconds = 600;
    private const int MaxReportChatLines = 8;
    private const string ReportFileName = "quick-deposit-report.txt";
    // The Greeneye mod this takes over from: while it runs, it keeps G twice and the Quick Stack Station.
    private const string OtherQuickDepositModule = "quick-deposit.dll";
    private const string OtherQuickDepositIni = @"greeneye\quick-deposit\quick-deposit.ini";
    // While the screen is open, the chests are read again this often (world steps).
    private const ulong StepsBetweenReads = 15;
    // The favorites' keep rules are checked against the settings and the player this often (world steps).
    private const ulong StepsBetweenRuleChecks = 60;
    private const int MaxRowsDrawn = 600;
    private const int MaxLoggedProblems = 20;
    private const int MaxKeepRules = 200;
    private const int MaxWornEntriesLogged = 80;
    private const float ListIconSize = 30f;
    private const float GridCellSize = 58f;
    private const float GridSpacing = 6f;
    // A grid icon is never drawn larger than this many real pixels. Kiln reads a trimmed icon at twice its drawn size and keeps
    // at most 256 icons in 24 MB: above 64 pixels it reads the 256-pixel pictures, of which only about 90 fit, and the rest of a
    // screenful then stays on placeholders.
    private const float MaxGridIconPixels = 64f;
    // The list of food and drink that can be used from a chest, made by Scripts\make_usable_items.py and built into the mod.
    // Where each item goes in the category tree, made by Scripts\make_item_categories.py and built into the mod.
    private const string ItemCategoriesResource = "StreamlinedLogistics.item_categories.json";
    // Chests stay lit while an inventory is open, at most this long, before their Highlight Time starts.
    private const long DepositHighlightLongestWaitMilliseconds = 60000;
    // Item details: tooltip rows read per pass of the chests (on the world's tick), texts looked up per post (on the client's thread).
    private const int DetailsReadsPerPass = 24;
    private const int TextLookupsPerPost = 200;
    // A setting changed in the Settings tab is written once it has stayed the same this long (a slider changes every frame).
    private const long SettingSettleMilliseconds = 400;
    // Auto Quick Deposit starts at most this often.
    private const long AutoDepositPauseMilliseconds = 10_000;
    // Drag and drop between the two sides (since 0.10.9): Kiln's drag kinds, one per side the stack comes from.
    private const string StoredDragKind = "stored-stack";
    private const string HotbarsKeyPrefix = "hotbar/";
    private const string CarriedDragKind = "carried-stack";
    private const string StackDragKind = "stack";
    // How near a container must be to be the chest whose screen the player has open, for Shift + RMB on gear there.
    private const double OpenChestReachMeters = 8;
    // Where the rarity lists of the Quick Deposit filters start, and their width (before the screen's scale).
    private const float RarityComboOffset = 120;
    private const float RarityComboWidth = 140;

    private static readonly Color FavoriteColor = new(255, 200, 60);
    private static readonly SlotMark FavoriteSlotMark = new(FavoriteColor, SlotMarkStyle.Border, 3);
    // A favorite's slot background in the game's own screens (Ui.Inventory.TintWhen).
    private static readonly Color FavoriteTint = new(255, 190, 40, 150);
    private static readonly Color SelectedColor = new(255, 255, 255);
    private static readonly Color CountBackground = new(0, 0, 0, 180);
    private static readonly Color WarningColor = new(255, 190, 80);

    // keen::ecs::InventorySetup, 84 bytes: the 16 links of an inventory owner, their categories and its slot counts.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private unsafe struct RawInventorySetup
    {
        public fixed uint Links[16];
        public fixed byte Categories[16];
        public byte Flags;
        public byte GenericSlotCount;
        public byte AvailableSlotCount;
        public byte IsInitialized;
    }

    // keen::ecs::SlotSelection, 216 bytes: actionbarEntityIds, the hotbar's two pages, is at offset 160.
    [StructLayout(LayoutKind.Explicit, Size = 216)]
    private struct RawSlotSelection
    {
        [FieldOffset(160)] public uint HotbarPage1;
        [FieldOffset(164)] public uint HotbarPage2;
    }

    private sealed record ChestView(uint Entity, ChestPosition Position, string Label, string KindText, string SlotsText, string DistanceText, double DistanceMeters, bool IsLinked,
        string? Name = null);

    private sealed record RowView(ItemRow Row, string Label, string CountText, string ShortCount, string HoldersText, string HoldersTooltip, string GridTooltip);

    /// <summary>Lines under one header. The key prefix tells the lines of two sections apart: an item can be in several chests.</summary>
    private sealed record ItemSection(string KeyPrefix, string Title, IReadOnlyList<RowView> Rows, string Group = "", string Key = "");

    // Key and Depth are set for the category tree's lines (since 0.9.0): a group at depth 0, its categories at 1.
    // NamedChests: the linked chests named exactly for this category, and their free slots (since 0.10.16).
    private sealed record CategoryView(string Name, bool IsHidden, int ItemCount, string Key = "", int Depth = 0, int NamedChests = 0, int NamedFreeSlots = 0);

    /// <summary>
    /// What the screen draws. The linked chests' items come twice: as one list under category headers (Grouped), and chest by
    /// chest (ByChest). Carried is the right side: the hotbar when it is included, then the backpack.
    /// </summary>
    private sealed record Snapshot(IReadOnlyList<ChestView> Chests, IReadOnlyList<ItemSection> Grouped, IReadOnlyList<ItemSection> ByChest, IReadOnlyList<ItemSection> Carried,
        IReadOnlyList<RowView> Stranded, IReadOnlyList<CategoryView> Categories, ulong HiddenCategories, int StoredItemCount, string Summary, string ChestsNote,
        string CategoriesNote, bool GridView, bool IncludeHotbar, bool Ungrouped, IReadOnlySet<int>? HiddenSections = null,
        IReadOnlyList<string>? SectionGroups = null);

    /// <summary>A chest to mark in the world. UntilTick 0: the time starts when the screen closes.</summary>
    private sealed record Highlight(WorldPosition Center, string Label, long UntilTick, uint Entity);
    private sealed record LitChest(uint Entity, WorldPosition Center, string Label);
    // Chests lit after a deposit. UntilTick 0: an inventory or this mod's screen is still open, the time has not started.
    // Outline: the native side lights the chests themselves; otherwise the mod draws boxes.
    private sealed record DepositHighlight(IReadOnlyList<LitChest> Chests, long UntilTick, long SinceTick, bool Outline, long CheckOutlineAtTick);

    private enum Side
    {
        None,
        Storage,
        Backpack,
    }

    /// <summary>Where a grid's cells go: its top left corner in the window, the cell and gap sizes, and the cells per row.</summary>
    private readonly record struct GridLayout(float OriginX, float OriginY, float CellSize, float Spacing, int Columns);

    private sealed record PlayerRead(PlayerLayout Layout, List<SlotContent> Slots, InventoryContents Contents);

    /// <summary>One chest of the base a quick deposit runs in, with its content as last read.</summary>
    private sealed class DepositChestState
    {
        public EntityId Entity;
        public string Label = "";
        public ulong NameCategories;
        // How closely the name asks for each of those sections (CategoryTree.SectionRanksOfChestName).
        public Dictionary<int, int> SectionRanks = new();
        // The chest is named All (since 1.0.1): it takes what no other chest's name asks for.
        public bool IsCatchAll;
        public double DistanceMeters;
        public InventoryContents Contents = null!;
        // The chest still has the game's default name (no name of its own): Auto-Name Chests may name it.
        public bool Unnamed;
    }

    private ModConfig<ChestSettings>? _settings;
    private KeyBinding? _openKey;
    private UiScreen? _screen;
    // The F2 window's size at UI scale 1 (since 0.10.40): the last size seen while it was open (render thread), and the size kept
    // in the state file (0: the default). Kiln centres a screen at its options' size each time it opens, so a kept size that
    // differs makes a new screen with that size before the next opening.
    private const float DefaultWindowWidth = 1240;
    private const float DefaultWindowHeight = 1060;
    private float _seenWindowWidth;
    private float _seenWindowHeight;
    private volatile float _keptWindowWidth;
    private volatile float _keptWindowHeight;
    private bool _subscribed;

    // The world's own thread only.
    private StoredState _state = new();
    private bool _stateLoaded;
    private readonly List<(EntityId Entity, ChestPosition Position)> _linkedChests = new();
    private readonly Dictionary<uint, ChestKind> _chestKinds = new();
    private readonly Dictionary<uint, TransferRule> _keepRules = new();
    // Favorite gear is kept by slot, one rule per slot that holds it (Kiln's item rule can't tell rarities).
    private readonly Dictionary<ulong, (TransferRule Rule, ulong Key)> _slotKeepRules = new();
    private EntityId _keepRulesPlayer;
    private ulong _lastReadStep;
    private ulong _lastRuleCheckStep;
    private bool _busy;
    private bool _chestKindsLogged;
    private bool _layoutLogged;
    private bool _keepRulesUnavailableLogged;
    private int _loggedProblems;
    private CategoryTable? _categories;
    private readonly Dictionary<uint, ulong> _categoriesOfItems = new();
    private CategoryTree? _tree;
    private string? _treeText;
    private bool _treeBroken;
    private readonly Dictionary<uint, (int Section, string Order)> _placeOfItems = new();
    private volatile HashSet<string> _collapsedView = new(StringComparer.OrdinalIgnoreCase);
    private volatile string? _focusGroup;
    // The navigation sidebar beside the stored items (since 0.10.39): hidden or shown (kept in the state file), and the header a
    // click in it asked to scroll to, for the next frame of the list (render thread only).
    private volatile bool _sidebarHiddenView;
    // The stored items' share of the window's width, beside the backpack (since 0.10.40: dragged at the bar between them, kept in the
    // state file), and the drag in progress (render thread).
    private volatile float _storageShareView = ChestRules.DefaultStorageShare;
    private bool _splitDragging;
    private float _splitDragStartX;
    private float _splitDragStartShare;
    private string? _jumpTo;
    private DateTime _categoriesFileTime;
    private string _categoriesNote = "";
    private EventSubscription? _clientActions;
    private EventSubscription? _interactions;
    // Auto Quick Deposit (since 0.10.23): the game's "+N" pickups, and when the backpack was last checked after one.
    private EventSubscription? _pickups;
    private bool _pickupCheckPending;
    private long _autoDepositNotBeforeTick;
    private readonly HashSet<uint> _playerInventoryOwners = new();
    private long _lastSortAt;
    private int _sortActionsLogged;
    private long _stationDepositAtTick;
    private int _stationsLogged;
    private bool? _otherQuickDepositRuns;
    private IDisposable? _favoriteMarks;
    private IDisposable? _favoriteTints;
    // The Link Chest / Unlink Chest button in a chest's own action row (Kiln 0.2.0-dev.31), and the caption it was made with.
    private IDisposable? _chestButton;
    private string? _chestButtonCaption;
    private bool _chestButtonBroken;
    private long _marksScreenCheckedUntil;
    private bool _marksInPlay;
    private IDisposable? _favoriteClicks;
    private IDisposable? _favoritePadClicks;
    private bool _gameScreenClicksBroken;
    private volatile bool _gameScreenFavoritesBroken;
    private EventSubscription? _renderFrames;
    private int _highlightSeconds = ChestRules.DefaultHighlightSeconds;

    // Written on the world's thread, read while drawing.
    private volatile Snapshot? _snapshot;
    private volatile string _status = "";
    // The favorites as the game's own screens are marked with them: replaced whole when they change, read while the game draws.
    private volatile HashSet<uint> _markedFavorites = new();
    // Gear favorites, for the same marks: the items with a favorite rarity, and the item entities of the player's slots that hold
    // one (Kiln's slot view has no rarity, so a slot is told by its item's entity). Written at the rule checks.
    private volatile HashSet<uint> _markedGearItems = new();
    // The favorite gear's keys (item and rarity), for slots that carry their rarity (Kiln 0.2.0-dev.30.5).
    private volatile HashSet<ulong> _markedGearKeys = new();
    // Every tool entry of the item catalog (a hammer's shape, a staff's helper) and the tool it stands for, read once on the
    // world's thread; read while the game draws.
    private static volatile Dictionary<uint, uint> _toolEntries = new();
    // The Construction Hammer's item: its icon, details and place stand for its shapes (Kiln's docs/re/building.md).
    private const uint ConstructionHammerItem = 0x2950da8d;
    // Render thread: the stack being dragged, and the payload Kiln carries for it.
    private ItemRow? _draggedRow;
    // Render thread: the top and bottom of the Hotbars list on screen (overlay pixels), where a stack dropped goes to the hotbars.
    private (float Top, float Bottom)? _hotbarsDropArea;
    private string? _draggedPayload;
    private bool _draggedFromHotbars;
    private volatile HashSet<uint> _markedGearEntities = new();
    private int _gearMarkChecksLogged;
    private volatile HashSet<uint> _usable = new();
    private volatile HashSet<uint> _notUsable = new();
    private int _useLogs;
    private readonly List<WorldOutline> _depositOutlines = new();
    private uint _renamingChest;
    private string _renameText = "";
    private bool _renameFocusPending;
    // Craft From Linked Chests (Kiln 0.2.0-dev.30.6's Crafting.AddSource): one source per linked chest found in the world, for the
    // local player; checked again when the saved state changes and every StepsBetweenCraftSourceChecks steps.
    private readonly Dictionary<uint, CraftSourceHandle> _craftSources = new();
    private IDisposable? _forgeOpened;
    private EntityId _gemOfferPlayer;
    private EntityId _craftSourcesPlayer;
    private ulong _craftSourcesCheckedStep;
    private int _craftSourcesStateVersion = -1;
    private int _stateVersion;
    private bool _craftSourcesBroken;
    // The options the current sources were registered with (Kiln copies them at registration): a change registers them again.
    private (bool Fills, bool Building, bool Locks) _craftSourcesOptions;
    private const ulong StepsBetweenCraftSourceChecks = 600;
    // The deposit and salvage rules as the Settings tab shows them (written on the world's thread after a load or a save), and the
    // render thread's edits waiting to be saved.
    private sealed record RulesView(IReadOnlyList<GearRule> Deposit, IReadOnlyList<GearRule> Salvage);
    private volatile RulesView _rulesView = new(ChestRules.DefaultDepositRules, ChestRules.DefaultSalvageRules);
    private List<GearRule>? _depositRulesEdit;
    private List<GearRule>? _salvageRulesEdit;
    private long _rulesSaveAtTick;
    // Auto Salvage (since 0.10.26): the loot waiting to be salvaged (the slot it landed in and its item), and the balancing.
    private readonly List<SalvageCandidate> _salvageQueue = new();
    // Loot from a container can be reported a step or more before it is in its slot: it is looked for again, this many times.
    private const int SalvageTries = 40;
    // A piece found in its slot is salvaged only once its level and rarity have read the same for this long (1.1.1): loot from a
    // container first reads level 0 and Common for a moment, and loot-level mods (Kiln's loot-leveler, Unified Tweaks' loot sync)
    // can set them after it lands. Up to 1.1.0 it was salvaged at once, so such pieces gave a level 0 Common piece's few runes.
    private const long SalvageSettleMilliseconds = 1500;
    private const long SalvageRecheckMilliseconds = 250;
    // After this long since the pickup a piece whose level still reads 0 is kept; one whose reads still change is judged as it reads.
    private const long SalvageGiveUpMilliseconds = 15000;
    private SalvageBalancing? _salvageBalancing;
    // The names a chest can be given from the Chests tab's list (the tree's groups and sub-categories), read while drawing.
    private volatile IReadOnlyList<string> _chestNameChoices = Array.Empty<string>();
    private volatile Dictionary<uint, ItemDetails> _itemDetails = new();
    private volatile Dictionary<uint, string> _texts = new();
    private string _textsLanguage = "";
    private readonly ConcurrentQueue<uint> _textsWanted = new();
    private int _textsPosted;
    private volatile bool _detailsBroken;
    private sealed record PendingSetting(object Value, long ApplyAtTick);
    private readonly ConcurrentDictionary<string, PendingSetting> _settingsPending = new();
    private volatile Highlight? _highlight;
    private volatile DepositHighlight? _depositHighlight;
    private int _highlightLogs;
    private int _readRequested;

    // The drawing thread only.
    private string _search = "";
    private bool _favoritesOnly;
    private bool _showHidden;
    private Side _selectedSide;
    private string? _selectedKey;
    private bool _cellRightClicked;

    public override void OnLoad()
    {
        string? formerKey = TakeOverFormerMod();
        _settings = Config.Register<ChestSettings>(ConfigKind.Client, null);
        if (!Client.Available || !Client.IsClient || !Ui.Available)
        {
            Log.Warn($"loaded {Context.Info.Version}, but it can't show anything here: Kiln's client and UI services are not both available");
            return;
        }
        _screen = MakeScreen(DefaultWindowWidth, DefaultWindowHeight);
        _openKey = KeyBindings.Register(OpenBindingId, formerKey ?? OpenDefaultKey, OnOpenKey, "Open Streamlined Logistics", Context.Info.Name);
        if (formerKey != null && KeyBindings.Rebind(OpenBindingId, formerKey))
        {
            // Rebind writes the key to keybinds.json, where the player's choice is kept from now on.
            Log.Info($"the key of Remote Chests ({formerKey}) now opens Streamlined Logistics");
        }
        _forgeOpened = Events.On<GemForgeOpenedEvent>(OnForgeOpened);
        Context.Tick.Pre += OnTick;
        _subscribed = true;
        Log.Info($"loaded {Context.Info.Version}: {_openKey.Key} opens the screen in a world you host (item icons: {Ui.ItemIconsAvailable}, outlines: {OutlinesOn()})");
    }

    public override void OnUnload()
    {
        if (_subscribed)
        {
            Context.Tick.Pre -= OnTick;
            _subscribed = false;
        }
        _clientActions?.Dispose();
        _clientActions = null;
        _interactions?.Dispose();
        _interactions = null;
        _pickups?.Dispose();
        _pickups = null;
        DropGameScreenFavorites();
        DropChestButton();
        _renderFrames?.Dispose();
        _renderFrames = null;
        DropChestHighlights();
        DropKeepRules();
        DropCraftSources();
        _forgeOpened?.Dispose();
        _forgeOpened = null;
        _openKey?.Dispose();
        _openKey = null;
        _screen?.Dispose();
        _screen = null;
    }

    private UiScreen MakeScreen(float width, float height)
    {
        UiScreen screen = Ui.Screen("Streamlined Logistics", Draw, new UiScreenOptions { Width = width, Height = height, Resizable = true });
        screen.Opened += RequestRead;
        screen.Closed += OnScreenClosed;
        return screen;
    }

    private void OnOpenKey()
    {
        UiScreen? screen = _screen;
        if (screen == null)
        {
            return;
        }
        float keptWidth = _keptWindowWidth;
        float keptHeight = _keptWindowHeight;
        if (!screen.IsOpen && keptWidth > 0 && keptHeight > 0
            && (Math.Abs(screen.Options.Width - keptWidth) > 0.5f || Math.Abs(screen.Options.Height - keptHeight) > 0.5f))
        {
            // The kept size: a new screen, which Kiln centres at that size.
            screen.Dispose();
            screen = MakeScreen(keptWidth, keptHeight);
            _screen = screen;
        }
        if (screen.IsOpen)
        {
            screen.Close();
            return;
        }
        if (!Client.IsHosting || !Client.InGame)
        {
            Hud.Toast("Streamlined Logistics works in a world you host.", ToastKind.Info, 4);
            return;
        }
        screen.Open();
    }

    private void RequestRead()
    {
        Interlocked.Exchange(ref _readRequested, 1);
    }

    // The hosted world's own thread, once per world step.
    private void OnTick(in TickInfo tick)
    {
        if (!Client.IsHosting)
        {
            _gemOfferPlayer = default; // Kiln clears forge offers on world exit.
            if (_snapshot != null || _linkedChests.Count > 0 || _chestKinds.Count > 0 || _keepRules.Count > 0 || _slotKeepRules.Count > 0 || _craftSources.Count > 0 || _pickups != null || _clientActions != null || _otherQuickDepositRuns != null || _favoriteMarks != null || _favoriteTints != null || _favoriteClicks != null || _chestButton != null || _depositHighlight != null)
            {
                _snapshot = null;
                _linkedChests.Clear();
                _chestKinds.Clear();
                DropKeepRules();
                DropCraftSources();
                _clientActions?.Dispose();
                _clientActions = null;
                DropGameScreenFavorites();
                DropChestButton();
                DropChestHighlights();
                _otherQuickDepositRuns = null;
                _playerInventoryOwners.Clear();
                _interactions?.Dispose();
                _interactions = null;
                _pickups?.Dispose();
                _pickups = null;
                _pickupCheckPending = false;
                _stationDepositAtTick = 0;
                _chestKindsLogged = false;
                _layoutLogged = false;
            }
            return;
        }
        try
        {
            ApplyPendingSettings();
            if (!_gemOfferPlayer.IsNone && (_settings?.Value.ShowLinkedGemsInGemForge == false || _gemOfferPlayer != Client.LocalPlayer))
            {
                WithdrawForgeGems();
            }
            if (tick.Step - _lastRuleCheckStep >= StepsBetweenRuleChecks)
            {
                _lastRuleCheckStep = tick.Step;
                LoadStateOnce();
                SyncKeepRules();
                SyncQuickDepositTriggers();
                SyncGameScreenFavorites();
                StartItemDetailCapture();
                SyncCraftSources(tick.Step);
            }
            StepDepositHighlight(tick.Step);
            if (_stationDepositAtTick != 0 && Environment.TickCount64 >= _stationDepositAtTick)
            {
                _stationDepositAtTick = 0;
                Post(() => QuickDeposit("Quick Stack Station"));
            }
            SalvageLootSoon();
            SyncChestButton();
            if (_pickupCheckPending && _salvageQueue.Count == 0)
            {
                CheckBackpackAfterLoot();
            }
            if (_screen?.IsOpen != true)
            {
                return;
            }
            bool requested = Interlocked.Exchange(ref _readRequested, 0) == 1;
            if (!requested && tick.Step - _lastReadStep < StepsBetweenReads)
            {
                return;
            }
            _lastReadStep = tick.Step;
            ReadChests();
        }
        catch (Exception problem)
        {
            Problem($"reading the chests stopped: {problem.GetType().Name}: {problem.Message}");
        }
    }

    // ---- Reading: the chests, the backpack, and what sits in an equipment slot by mistake. ----

    private void ReadChests()
    {
        LoadStateOnce();
        EntityId player = Client.LocalPlayer;
        if (player.IsNone || !Items.Available)
        {
            _snapshot = null;
            _status = "Kiln's item service is not ready in this world.";
            return;
        }
        ChestSettings settings = _settings?.Value ?? new ChestSettings();
        bool playerPlaced = World.TryGetPosition(player, out WorldPosition playerWorldPosition);
        ChestPosition playerPosition = ToChestPosition(playerWorldPosition);

        IReadOnlyList<EntityId> containers = Items.FindInventories(InventoryKinds.Containers);
        var placed = new List<(EntityId Entity, ChestPosition Position, ChestKind Kind)>(containers.Count);
        int magicCount = 0;
        int regularCount = 0;
        int unknownCount = 0;
        foreach (EntityId container in containers)
        {
            if (!World.TryGetPosition(container, out WorldPosition worldPosition))
            {
                continue;
            }
            ChestKind kind = KindOf(container);
            switch (kind)
            {
                case ChestKind.Magic: magicCount++; break;
                case ChestKind.Regular: regularCount++; break;
                case ChestKind.Unknown: unknownCount++; break;
            }
            placed.Add((container, ToChestPosition(worldPosition), kind));
        }
        // When no container's template can be read at all, the test does not work in this world: every placed container is then offered.
        bool kindsKnown = magicCount + regularCount > 0 || unknownCount == 0;
        if (!_chestKindsLogged)
        {
            _chestKindsLogged = true;
            Log.Info($"{containers.Count} container(s) in the world, {placed.Count} with a position: {magicCount} Magic Chest(s), {regularCount} regular chest(s), {unknownCount} whose template could not be read{(kindsKnown ? "" : ": every container is listed")}");
        }

        _linkedChests.Clear();
        var chestViews = new List<ChestView>();
        var chestLabels = new Dictionary<uint, string>();
        var chestTitles = new Dictionary<uint, string>();
        var storedStacks = new List<StackView>();
        var stacksByChest = new Dictionary<uint, List<StackView>>();
        var linksFound = new HashSet<int>();
        var linkedNames = new List<(string? Name, int FreeSlots)>();
        int freeSlots = 0;
        foreach ((EntityId entity, ChestPosition position, ChestKind kind) in placed)
        {
            int linkIndex = ChestRules.FindLink(_state.Links, position);
            bool isLinked = linkIndex >= 0;
            bool offered = !kindsKnown || kind == ChestKind.Magic || (kind == ChestKind.Regular && settings.LinkRegularChests);
            if (!isLinked && !offered)
            {
                continue;
            }
            InventoryContents? contents = Items.Read(entity);
            if (contents == null)
            {
                continue;
            }
            int usedSlots = 0;
            int totalSlots = 0;
            foreach (InventorySlot slot in contents.Slots)
            {
                if (slot.Category != InventoryCategory.Generic)
                {
                    continue;
                }
                totalSlots++;
                if (slot.IsEmpty)
                {
                    continue;
                }
                usedSlots++;
                if (isLinked)
                {
                    StackView stack = ToStackView(entity, slot);
                    storedStacks.Add(stack);
                    if (!stacksByChest.TryGetValue(entity.Value, out List<StackView>? chestStacks))
                    {
                        chestStacks = new List<StackView>();
                        stacksByChest.Add(entity.Value, chestStacks);
                    }
                    chestStacks.Add(stack);
                }
            }
            string kindText = kind switch
            {
                ChestKind.Magic => "Magic",
                ChestKind.Regular => "Regular",
                ChestKind.Other => "Not A Chest",
                _ => "?",
            };
            string? chestName = ContainerNameOrNull(entity);
            string label = chestName
                ?? string.Create(CultureInfo.InvariantCulture, $"{(kind == ChestKind.Magic ? "Magic Chest" : "Chest")} ({totalSlots} slots)");
            if (isLinked)
            {
                linkedNames.Add((chestName, totalSlots - usedSlots));
                linksFound.Add(linkIndex);
                _linkedChests.Add((entity, position));
                chestLabels[entity.Value] = label;
                chestTitles[entity.Value] = string.Create(CultureInfo.InvariantCulture, $"{label}  ({usedSlots} / {totalSlots})");
                freeSlots += totalSlots - usedSlots;
            }
            double distance = playerPlaced ? position.DistanceTo(playerPosition) : 0;
            chestViews.Add(new ChestView(entity.Value, position, label, kindText,
                string.Create(CultureInfo.InvariantCulture, $"{usedSlots} / {totalSlots}"),
                playerPlaced ? string.Create(CultureInfo.InvariantCulture, $"{distance:0} m") : "?",
                distance, isLinked, chestName));
        }
        chestViews.Sort((left, right) => left.DistanceMeters.CompareTo(right.DistanceMeters));

        var hotbarStacks = new List<StackView>();
        var backpackStacks = new List<StackView>();
        var strandedStacks = new List<StackView>();
        PlayerRead? playerRead = ReadPlayer(player);
        if (playerRead != null)
        {
            foreach (InventorySlot slot in playerRead.Contents.Slots)
            {
                if (slot.IsEmpty || slot.IsEquipped)
                {
                    continue;
                }
                uint page = slot.Slot.Inventory.Value;
                bool onHotbar = playerRead.Layout.IsHotbarPage(page);
                if (playerRead.Layout.IsBackpackPage(page) || (_state.IncludeHotbar && onHotbar))
                {
                    // The Construction Hammer's and the summoning staff's slots read as what they have selected; listed as the
                    // tool since 0.10.20 (0.4.1 to 0.10.19 left them out), still skipped by a quick deposit.
                    (onHotbar ? hotbarStacks : backpackStacks).Add(ToStackView(player, slot));
                }
                else if (ChestRules.IsStranded(playerRead.Layout, page, slot.IsGear, Items.Get(slot.Item)?.Category == ItemCategory.Materials))
                {
                    strandedStacks.Add(ToStackView(player, slot));
                }
            }
        }

        var seenStacks = new List<StackView>(storedStacks.Count + hotbarStacks.Count + backpackStacks.Count);
        seenStacks.AddRange(storedStacks);
        seenStacks.AddRange(hotbarStacks);
        seenStacks.AddRange(backpackStacks);
        int movedFavorites = ChestRules.MoveOldGearFavorites(_state, seenStacks);
        if (movedFavorites > 0)
        {
            Log.Info($"favorites: {movedFavorites} favorite gear item(s) of an older version now favorite at the highest rarity seen");
            SaveState();
            RefreshMarkedFavorites();
        }
        EnsureToolEntries();
        FavoriteSet favorites = _state.FavoriteSet(_toolEntries);

        LoadCategoriesIfChanged();
        const string StoredHints = "RMB: Take Stack\nShift + RMB: Favorite\nLMB: Select\nDrag To Your Inventory: Take Stack (Shift On Release: Every Stack)\nDrag To Your Hotbars: Into Hotbar 1, Then 2, Then The Backpack";
        const string CarriedHints = "RMB: Store\nShift + RMB: Favorite\nLMB: Select\nDrag To The Chests: Store";
        IReadOnlyList<string> categoryNames = _categories?.Names ?? Array.Empty<string>();
        EnsureTree();
        IReadOnlyList<ItemRow> storedRows = WithPlaces(ChestRules.BuildRows(storedStacks, NameOfItem, favorites, CategoriesOfItem));

        // One list under headers: the favorites, then the category tree's groups and categories in order (since 0.9.0); without the
        // tree's table, the category file's categories in its order.
        var grouped = new List<ItemSection>();
        if (_tree != null)
        {
            foreach (TreeGroup group in _tree.Group(storedRows, row => row.Section, row => PlaceOf(row.Item).Order))
            {
                grouped.Add(new ItemSection("tree:" + group.Key + "/", group.Name, ToRowViews(group.Rows, chestLabels, StoredHints), group.Group, group.Key));
            }
        }
        else if (_categories == null)
        {
            grouped.Add(new ItemSection("", "Linked Chests", ToRowViews(storedRows, chestLabels, StoredHints)));
        }
        else
        {
            foreach (RowGroup group in ChestRules.GroupByCategory(storedRows, categoryNames))
            {
                grouped.Add(new ItemSection("group:" + group.Title + "/", group.Title, ToRowViews(group.Rows, chestLabels, StoredHints)));
            }
        }

        // The same content chest by chest, by chest name; inside a chest in the same order, without the headers.
        var chestSections = new List<(uint Entity, ItemSection Section)>(stacksByChest.Count);
        foreach (KeyValuePair<uint, List<StackView>> chestStacks in stacksByChest)
        {
            IReadOnlyList<ItemRow> chestRows = _tree != null
                ? InTreeOrder(WithPlaces(ChestRules.BuildRows(chestStacks.Value, NameOfItem, favorites, CategoriesOfItem)))
                : ChestRules.OrderByCategory(ChestRules.BuildRows(chestStacks.Value, NameOfItem, favorites, CategoriesOfItem), categoryNames);
            chestSections.Add((chestStacks.Key, new ItemSection(chestStacks.Key.ToString(CultureInfo.InvariantCulture) + "/", chestTitles[chestStacks.Key], ToRowViews(chestRows, null, StoredHints))));
        }
        chestSections.Sort((left, right) =>
        {
            int byTitle = string.Compare(left.Section.Title, right.Section.Title, StringComparison.OrdinalIgnoreCase);
            return byTitle != 0 ? byTitle : left.Entity.CompareTo(right.Entity);
        });
        var byChest = new List<ItemSection>(chestSections.Count);
        foreach ((uint _, ItemSection section) in chestSections)
        {
            byChest.Add(section);
        }

        // The right side: the hotbars first, since they hold fewer items, then the backpack. Each lists its favorites first, then the
        // rest in the tree's order.
        IReadOnlyList<RowView> CarriedRows(IReadOnlyList<StackView> stacks) =>
            ToRowViews(ChestRules.FavoritesFirst(InTreeOrder(WithPlaces(ChestRules.BuildRows(stacks, NameOfItem, favorites, CategoriesOfItem)))), null, CarriedHints);
        var carried = new List<ItemSection>();
        if (_state.IncludeHotbar)
        {
            carried.Add(new ItemSection(HotbarsKeyPrefix, "Hotbars", CarriedRows(hotbarStacks)));
        }
        carried.Add(new ItemSection("backpack/", "Inventory", CarriedRows(backpackStacks)));

        ulong hiddenCategories = _categories?.BitsOfNames(_state.HiddenCategories) ?? 0;
        var categoryViews = _tree != null ? TreeCategoryViews(storedRows, linkedNames) : new List<CategoryView>();
        if (_tree == null && _categories != null)
        {
            for (int category = 0; category < _categories.Names.Count; category++)
            {
                ulong bit = 1ul << category;
                int itemCount = 0;
                foreach (ItemRow row in storedRows)
                {
                    if ((row.Categories & bit) != 0)
                    {
                        itemCount++;
                    }
                }
                categoryViews.Add(new CategoryView(_categories.Names[category], (hiddenCategories & bit) != 0, itemCount));
            }
        }
        IReadOnlyList<ItemRow> strandedRows = ChestRules.BuildRows(strandedStacks, NameOfItem, favorites);
        int linksMissing = _state.Links.Count - linksFound.Count;
        string summary = string.Create(CultureInfo.InvariantCulture,
            $"{_linkedChests.Count} linked chest(s), {storedRows.Count} different item(s), {freeSlots} free slot(s)")
            + (linksMissing > 0 ? string.Create(CultureInfo.InvariantCulture, $". {linksMissing} linked chest(s) not found in the world right now.") : ".");
        string chestsNote = !kindsKnown
            ? "The chests could not be told from other containers here, so every container is listed."
            : settings.LinkRegularChests
                ? "Magic Chests and regular chests are listed."
                : "Only Magic Chests are listed. Link Regular Chests in the Settings tab lists regular chests too.";
        _snapshot = new Snapshot(chestViews, grouped, byChest, carried, ToRowViews(strandedRows, null, ""), categoryViews, hiddenCategories, storedRows.Count,
            summary, chestsNote, _categoriesNote, _state.GridView, _state.IncludeHotbar, _state.Ungrouped, _tree != null ? HiddenSections() : null,
            _tree != null ? SectionGroupNames() : null);
        var shownRows = new List<ItemRow>(storedRows);
        foreach (ItemSection section in carried)
        {
            foreach (RowView view in section.Rows)
            {
                shownRows.Add(view.Row);
            }
        }
        ReadItemDetails(shownRows);
        NoteUsable(shownRows);
    }

    // ---- Categories: chestsort_categories.json in the game folder (Quick Deposit's, or one the player edits), read again when it
    // changes; without one, the copy bundled with the mod. ----

    private void LoadCategoriesIfChanged()
    {
        string gamePath = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory, CategoriesFileName);
        string path = gamePath;
        try
        {
            bool inGameFolder = File.Exists(gamePath);
            DateTime fileTime = inGameFolder ? File.GetLastWriteTimeUtc(gamePath) : BundledCategoriesTime;
            if (fileTime == _categoriesFileTime)
            {
                return;
            }
            _categoriesFileTime = fileTime;
            string? text;
            if (inGameFolder)
            {
                text = File.ReadAllText(gamePath);
            }
            else
            {
                path = "the categories bundled with the mod";
                using Stream? bundled = typeof(StreamlinedLogisticsMod).Assembly.GetManifestResourceStream(BundledCategoriesResource);
                using StreamReader? reader = bundled != null ? new StreamReader(bundled) : null;
                text = reader?.ReadToEnd();
            }
            if (text == null)
            {
                _categories = null;
                _categoriesOfItems.Clear();
                _tree = null;
                _placeOfItems.Clear();
                _categoriesNote = $"No categories: {gamePath} was not found, and the mod has no copy of its own.";
                Log.Warn(_categoriesNote);
                return;
            }
            if (!CategoryTable.TryParse(text, out CategoryTable table, out string error))
            {
                _categoriesNote = $"The categories in {path} could not be read ({error}); the last ones read stay in use.";
                Log.Warn(_categoriesNote);
                return;
            }
            _categories = table;
            _categoriesOfItems.Clear();
            _tree = null;
            _placeOfItems.Clear();
            _categoriesNote = $"{table.Names.Count} categories from {path}.";
            if (!_state.HiddenCategoriesChosen)
            {
                _state.HiddenCategoriesChosen = true;
                _state.HiddenCategories.Clear();
                _state.KnownCategories.Clear();
            }
            int knownBefore = _state.KnownCategories.Count;
            IReadOnlyList<string> newlyHidden = ChestRules.AdoptNewCategories(_state, table.Names, table.DefaultHidden());
            if (_state.KnownCategories.Count != knownBefore)
            {
                SaveState();
                if (knownBefore > 0)
                {
                    Log.Info($"{_state.KnownCategories.Count - knownBefore} new categor(ies) in {path}{(newlyHidden.Count > 0 ? "; hidden like the others of their sort: " + string.Join(", ", newlyHidden) : "")}");
                }
            }
            Log.Info($"{table.Names.Count} categories read from {path}; hidden: {(_state.HiddenCategories.Count == 0 ? "none" : string.Join(", ", _state.HiddenCategories))}");
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException)
        {
            _categoriesNote = $"The categories in {path} could not be read: {problem.Message}";
            Problem(_categoriesNote);
        }
    }

    private ulong CategoriesOfItem(uint item)
    {
        if (_categories == null)
        {
            return 0;
        }
        if (!_categoriesOfItems.TryGetValue(item, out ulong bits))
        {
            bits = _categories.BitsOf(item, Items.Get(new ItemId(item))?.Name);
            _categoriesOfItems[item] = bits;
        }
        return bits;
    }

    private ChestKind KindOf(EntityId entity)
    {
        if (_chestKinds.TryGetValue(entity.Value, out ChestKind known))
        {
            return known;
        }
        ChestKind kind = ChestKind.Unknown;
        try
        {
            EntityInfo? info = Spawn.Inspect(entity);
            if (info != null)
            {
                kind = ChestRules.KindOfChest(info.Template, info.TemplateName);
            }
        }
        catch (Exception problem)
        {
            Problem($"a container's template could not be read: {problem.GetType().Name}: {problem.Message}");
        }
        _chestKinds[entity.Value] = kind;
        return kind;
    }

    /// <summary>The player's inventories with the backpack, the hotbar and the worn things told apart, or null when they can't be read.</summary>
    private PlayerRead? ReadPlayer(EntityId player)
    {
        InventoryContents? contents = Items.Read(player);
        SetupRead? root = ReadSetup(player);
        if (contents == null || root == null)
        {
            return null;
        }
        // The owners the Virtual links name (the backpack item the character wears), and theirs, as deep as Kiln itself follows them.
        var carried = new Dictionary<uint, SetupRead>();
        var toRead = new Queue<(SetupRead Setup, int Depth)>();
        toRead.Enqueue((root, 0));
        while (toRead.Count > 0)
        {
            (SetupRead setup, int depth) = toRead.Dequeue();
            for (int link = 0; link < setup.Links.Count && setup.Links[link] != 0; link++)
            {
                uint owner = setup.Links[link];
                if (setup.Categories[link] != ChestRules.VirtualCategory || depth >= 3 || carried.ContainsKey(owner))
                {
                    continue;
                }
                SetupRead? inner = ReadSetup(new EntityId(owner));
                if (inner != null)
                {
                    carried.Add(owner, inner);
                    toRead.Enqueue((inner, depth + 1));
                }
            }
        }
        World.TryRead(player, "SlotSelection", out RawSlotSelection selection);
        PlayerLayout layout = ChestRules.BuildLayout(root, carried, selection.HotbarPage1, selection.HotbarPage2);
        _playerInventoryOwners.Clear();
        _playerInventoryOwners.Add(player.Value);
        foreach (uint owner in carried.Keys)
        {
            _playerInventoryOwners.Add(owner);
        }
        foreach (uint page in layout.BackpackPages)
        {
            _playerInventoryOwners.Add(page);
        }
        var slots = new List<SlotContent>(contents.Slots.Count);
        foreach (InventorySlot slot in contents.Slots)
        {
            slots.Add(new SlotContent(slot.Slot.Inventory.Value, slot.Slot.Index, slot.IsEmpty ? 0 : slot.Item.Value, slot.IsEmpty ? 0 : slot.Count, !slot.ItemEntity.IsNone));
        }
        if (!_layoutLogged)
        {
            _layoutLogged = true;
            LogLayout(player, root, carried, layout, contents);
        }
        return new PlayerRead(layout, slots, contents);
    }

    private static unsafe SetupRead? ReadSetup(EntityId owner)
    {
        if (!World.TryRead(owner, "InventorySetup", out RawInventorySetup setup))
        {
            return null;
        }
        var links = new uint[16];
        var categories = new byte[16];
        for (int link = 0; link < 16; link++)
        {
            links[link] = setup.Links[link];
            categories[link] = setup.Categories[link];
        }
        // An owner that leaves its unlocked count at 0 is taken to have every slot of its pages unlocked.
        int unlocked = setup.AvailableSlotCount > 0 ? setup.AvailableSlotCount : setup.GenericSlotCount > 0 ? setup.GenericSlotCount : 16 * ChestRules.SlotsPerPage;
        return new SetupRead(links, categories, unlocked);
    }

    private static string DescribeSetup(SetupRead setup)
    {
        var links = new List<string>();
        for (int link = 0; link < setup.Links.Count && setup.Links[link] != 0; link++)
        {
            links.Add(string.Create(CultureInfo.InvariantCulture, $"{setup.Links[link]}:{(InventoryCategory)setup.Categories[link]}"));
        }
        return string.Create(CultureInfo.InvariantCulture, $"links [{string.Join(", ", links)}], {setup.AvailableSlots} item slot(s) unlocked");
    }

    // Once per world: how the character's inventories are laid out, and everything in its worn pages, to read back when a move went wrong.
    private void LogLayout(EntityId player, SetupRead root, Dictionary<uint, SetupRead> carried, PlayerLayout layout, InventoryContents contents)
    {
        Log.Info($"player {player.Value}'s inventories: {DescribeSetup(root)}; backpack pages [{string.Join(", ", layout.BackpackPages)}], hotbar pages [{string.Join(", ", layout.HotbarPages)}]");
        foreach (KeyValuePair<uint, SetupRead> entry in carried)
        {
            string template = "?";
            try
            {
                template = Spawn.Inspect(new EntityId(entry.Key))?.TemplateName ?? "?";
            }
            catch (Exception)
            {
                // The line is still worth having without the name.
            }
            Log.Info($"carried inventory {entry.Key} ({template}): {DescribeSetup(entry.Value)}");
        }
        int logged = 0;
        foreach (InventorySlot slot in contents.Slots)
        {
            uint page = slot.Slot.Inventory.Value;
            if (slot.IsEmpty || layout.IsBackpackPage(page) || layout.IsHotbarPage(page))
            {
                continue;
            }
            if (++logged > MaxWornEntriesLogged)
            {
                Log.Info("more entries outside the backpack and hotbar are not listed");
                break;
            }
            Log.Info($"outside the backpack and hotbar: page {page} slot {slot.Slot.Index} ({slot.Category}): {slot.Count} x {Items.Get(slot.Item)?.Name ?? "?"} (0x{slot.Item.Value:x8}){(slot.IsGear ? "" : ", not gear")}");
        }
    }

    private string? ContainerNameOrNull(EntityId entity)
    {
        if (!Items.ContainerNamesAvailable)
        {
            return null;
        }
        try
        {
            string? name = Items.ContainerName(entity);
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch (Exception problem)
        {
            Problem($"a chest's name could not be read: {problem.GetType().Name}: {problem.Message}");
            return null;
        }
    }

    private static string NameOfItem(uint item)
    {
        if (_toolEntries.TryGetValue(item, out uint toolKey))
        {
            // The tool's own name (the game's, where it has the tool's item), whatever it holds.
            uint toolItem = ChestRules.ToolItemOf(toolKey);
            return toolItem != 0 ? NameOfItem(toolItem) : ChestRules.ToolName(toolKey);
        }
        var itemId = new ItemId(item);
        return Items.DisplayName(itemId) ?? Items.Get(itemId)?.Label ?? string.Create(CultureInfo.InvariantCulture, $"Item 0x{item:x8}");
    }

    private static StackView ToStackView(EntityId holder, InventorySlot slot)
    {
        return new StackView(holder.Value, slot.Slot.Value, slot.Item.Value, slot.Count, slot.Level, RarityOf(slot), slot.IsGear);
    }

    private static int RarityOf(InventorySlot slot)
    {
        return slot.Rarity.HasValue ? (int)slot.Rarity.Value : -1;
    }

    private static ChestPosition ToChestPosition(WorldPosition position)
    {
        return new ChestPosition(position.X, position.Y, position.Z);
    }

    private static IReadOnlyList<RowView> ToRowViews(IReadOnlyList<ItemRow> rows, Dictionary<uint, string>? chestLabels, string actionsHint)
    {
        var views = new List<RowView>(rows.Count);
        foreach (ItemRow row in rows)
        {
            string label = row.Name;
            if (row.IsGear)
            {
                string rarity = row.Rarity >= 0 ? ", " + ((ItemRarity)row.Rarity) : "";
                label = string.Create(CultureInfo.InvariantCulture, $"{row.Name} (Level {row.Level}{rarity})");
            }
            string countText = row.Total.ToString("N0", CultureInfo.InvariantCulture);
            string holdersText = "";
            string holdersTooltip = "";
            if (chestLabels != null)
            {
                var names = new List<string>();
                var seenHolders = new HashSet<uint>();
                foreach (StackView stack in row.Stacks)
                {
                    if (seenHolders.Add(stack.Holder))
                    {
                        names.Add(chestLabels.TryGetValue(stack.Holder, out string? chestLabel) ? chestLabel : "Chest");
                    }
                }
                holdersText = ChestRules.HoldersText(names);
                holdersTooltip = string.Join(", ", names);
            }
            string gridTooltip = label + "\n" + countText
                + (chestLabels != null ? " in " + (row.HolderCount == 1 ? holdersTooltip : holdersText + ": " + holdersTooltip) : "")
                + (actionsHint.Length > 0 ? "\n" + actionsHint : "");
            views.Add(new RowView(row, label, countText, ChestRules.ShortCount(row.Total), holdersText, holdersTooltip, gridTooltip));
        }
        return views;
    }

    // ---- Favorites stay in the backpack: one keep rule of Kiln's per favorite item, for the game's own quick stack and move all. ----

    private void SyncKeepRules()
    {
        EntityId player = Client.LocalPlayer;
        bool wanted = (_settings?.Value.FavoritesStayInBackpack ?? true) && !player.IsNone && (_state.Favorites.Count > 0 || _state.GearFavorites.Count > 0);
        if (wanted && !(Items.Available && Items.Transfers.Available))
        {
            if (!_keepRulesUnavailableLogged)
            {
                _keepRulesUnavailableLogged = true;
                Log.Warn("Kiln's transfer rules are not available in this world: favorites are not kept out of the game's quick stack");
            }
            wanted = false;
        }
        if (!wanted || player != _keepRulesPlayer)
        {
            DropKeepRules();
        }
        if (!wanted)
        {
            // The game's screens still mark favorite gear by the slots read here.
            if (_favoriteMarks != null || _favoriteTints != null)
            {
                FavoriteGearSlots(player);
            }
            return;
        }
        _keepRulesPlayer = player;
        var noLongerFavorite = new List<uint>();
        foreach (uint item in _keepRules.Keys)
        {
            if (!_state.Favorites.Contains(item))
            {
                noLongerFavorite.Add(item);
            }
        }
        foreach (uint item in noLongerFavorite)
        {
            _keepRules[item].Dispose();
            _keepRules.Remove(item);
        }
        int added = 0;
        foreach (uint item in _state.Favorites)
        {
            if (ChestRules.IsToolKey(item))
            {
                continue;  // kept by slot (FavoriteGearSlots)
            }
            if (_keepRules.ContainsKey(item) || _keepRules.Count >= MaxKeepRules)
            {
                continue;
            }
            _keepRules[item] = Items.Transfers.KeepItem(new ItemId(item), player);
            added++;
        }
        if (added > 0 || noLongerFavorite.Count > 0)
        {
            Log.Info($"favorites kept in player {player.Value}'s backpack: {_keepRules.Count} keep rule(s) ({added} added, {noLongerFavorite.Count} removed)");
        }
        SyncSlotKeepRules(player, FavoriteGearSlots(player));
    }

    // The player's slots that hold favorite gear (slot to its favorite's key), read at the rule checks. The game's screens mark them
    // by their items' entities, kept here too.
    private Dictionary<ulong, ulong> FavoriteGearSlots(EntityId player)
    {
        var slots = new Dictionary<ulong, ulong>();
        var entities = new HashSet<uint>();
        bool tools = false;
        foreach (uint favorite in _state.Favorites)
        {
            tools |= ChestRules.IsToolKey(favorite);
        }
        if ((_state.GearFavorites.Count > 0 || tools) && !player.IsNone && Items.Available)
        {
            EnsureToolEntries();
            InventoryContents? contents = Items.Read(player);
            if (contents != null)
            {
                foreach (InventorySlot slot in contents.Slots)
                {
                    if (slot.IsEmpty)
                    {
                        continue;
                    }
                    if (tools && _toolEntries.TryGetValue(slot.Item.Value, out uint toolKey) && _state.Favorites.Contains(toolKey))
                    {
                        if (!slot.IsEquipped)
                        {
                            slots[slot.Slot.Value] = toolKey;
                        }
                        continue;
                    }
                    if (!slot.IsGear)
                    {
                        continue;
                    }
                    ulong key = FavoriteSet.GearKey(slot.Item.Value, RarityOf(slot));
                    if (!_state.GearFavorites.Contains(key))
                    {
                        continue;
                    }
                    if (!slot.ItemEntity.IsNone)
                    {
                        entities.Add(slot.ItemEntity.Value);
                    }
                    if (!slot.IsEquipped)
                    {
                        slots[slot.Slot.Value] = key;
                    }
                }
            }
        }
        if (!entities.SetEquals(_markedGearEntities))
        {
            _markedGearEntities = entities;
            Log.Info($"favorite gear carried: item entities {(entities.Count == 0 ? "none" : string.Join(", ", entities))}");
        }
        return slots;
    }

    // One Kiln keep rule per slot holding favorite gear: the stack stays while it lies in a transfer's source (the game's Deposit
    // All, Deposit Stacks, Quick Stack Station). Rules follow the slots at each check, so gear moved within the backpack is kept
    // again a second or so later.
    private void SyncSlotKeepRules(EntityId player, Dictionary<ulong, ulong> wanted)
    {
        var ended = new List<ulong>();
        foreach (KeyValuePair<ulong, (TransferRule Rule, ulong Key)> entry in _slotKeepRules)
        {
            if (!wanted.TryGetValue(entry.Key, out ulong key) || key != entry.Value.Key)
            {
                ended.Add(entry.Key);
            }
        }
        foreach (ulong slot in ended)
        {
            _slotKeepRules[slot].Rule.Dispose();
            _slotKeepRules.Remove(slot);
        }
        int added = 0;
        foreach (KeyValuePair<ulong, ulong> entry in wanted)
        {
            if (_slotKeepRules.ContainsKey(entry.Key) || _keepRules.Count + _slotKeepRules.Count >= MaxKeepRules)
            {
                continue;
            }
            _slotKeepRules[entry.Key] = (Items.Transfers.Keep(new SlotId(entry.Key)), entry.Value);
            added++;
        }
        if (added > 0 || ended.Count > 0)
        {
            Log.Info($"favorite gear kept in player {player.Value}'s slots: {_slotKeepRules.Count} slot rule(s) ({added} added, {ended.Count} ended)");
        }
    }

    private HashSet<uint> FindLinkedContainers()
    {
        var chests = new HashSet<uint>();
        if (_state.Links.Count > 0)
        {
            foreach (EntityId container in Items.FindInventories(InventoryKinds.Containers))
            {
                if (World.TryGetPosition(container, out WorldPosition position) && ChestRules.FindLink(_state.Links, ToChestPosition(position)) >= 0)
                {
                    chests.Add(container.Value);
                }
            }
        }
        return chests;
    }

    private void OnForgeOpened(GemForgeOpenedEvent forge)
    {
        if (!Client.IsHosting || forge.Player.IsNone || forge.Player != Client.LocalPlayer || forge.Forge.IsNone
            || _settings?.Value.ShowLinkedGemsInGemForge == false || !Capabilities.Is(Gems.SourcesCapability))
        {
            return;
        }
        LoadStateOnce();
        _gemOfferPlayer = forge.Player;
        foreach (uint chest in FindLinkedContainers())
        {
            // Kiln's default payer and recycle destination are both the player's inventory.
            Gems.OfferChestGems(forge.Player, new EntityId(chest));
        }
    }

    private void WithdrawForgeGems(EntityId chest = default)
    {
        if (!_gemOfferPlayer.IsNone && Capabilities.Is(Gems.SourcesCapability))
        {
            Gems.WithdrawChestGems(_gemOfferPlayer, chest);
        }
        if (chest.IsNone)
        {
            _gemOfferPlayer = default;
        }
    }

    // The world's thread: every linked chest standing in the world is a crafting source of the local player (any base, or none),
    // after the backpack and the crafting base's own chests; its dyes show in the tinting screen. Kiln pays a recipe all or
    // nothing and rechecks the player's permission. A source lives for the session: registered again after a load.
    private void SyncCraftSources(ulong step)
    {
        EntityId player = Client.LocalPlayer;
        ChestSettings? settings = _settings?.Value;
        bool wanted = (settings?.CraftFromLinkedChests ?? true) && !player.IsNone && !_craftSourcesBroken;
        // Fills, building and locks from linked chests (Kiln 0.2.0-dev.31.12, mod API 0.2.21): each off in Kiln until the link asks.
        (bool Fills, bool Building, bool Locks) options = (settings?.FillStationsFromLinkedChests ?? true,
            settings?.BuildFromLinkedChests ?? true, settings?.LocksFromLinkedChests ?? true);
        if (!wanted || player != _craftSourcesPlayer || (_craftSources.Count > 0 && options != _craftSourcesOptions))
        {
            DropCraftSources();
        }
        _craftSourcesOptions = options;
        if (!wanted || (_stateVersion == _craftSourcesStateVersion && step - _craftSourcesCheckedStep < StepsBetweenCraftSourceChecks))
        {
            return;
        }
        _craftSourcesStateVersion = _stateVersion;
        _craftSourcesCheckedStep = step;
        _craftSourcesPlayer = player;
        HashSet<uint> chests = FindLinkedContainers();
        var gone = new List<uint>();
        foreach (uint chest in _craftSources.Keys)
        {
            if (!chests.Contains(chest))
            {
                gone.Add(chest);
            }
        }
        foreach (uint chest in gone)
        {
            _craftSources[chest].Dispose();
            _craftSources.Remove(chest);
        }
        int added = 0;
        foreach (uint chest in chests)
        {
            if (_craftSources.ContainsKey(chest))
            {
                continue;
            }
            try
            {
                var sourceOptions = new CraftSourceOptions { Fills = options.Fills, Building = options.Building, Locks = options.Locks };
                // Older supported Kiln builds have no Respec property. Enable it whenever the installed API provides it.
                typeof(CraftSourceOptions).GetProperty(nameof(CraftSourceOptions.Respec))?.SetValue(sourceOptions, true);
                _craftSources[chest] = Crafting.AddSource(player, new EntityId(chest), sourceOptions);
                added++;
            }
            catch (KilnUnavailableException problem)
            {
                _craftSourcesBroken = true;
                DropCraftSources();
                Log.Info($"craft from linked chests: not available here ({problem.Message})");
                return;
            }
            catch (Exception problem) when (problem is ArgumentException or InvalidOperationException)
            {
                Log.Info($"craft from linked chests: chest {chest} can't be a source ({problem.Message})");
            }
        }
        if (added > 0 || gone.Count > 0)
        {
            Log.Info($"craft from linked chests: {_craftSources.Count} linked chest(s) lend to player {player.Value}'s crafting ({added} added, {gone.Count} removed; fills {options.Fills}, building {options.Building}, locks {options.Locks})");
        }
    }

    private void DropCraftSources()
    {
        foreach (CraftSourceHandle source in _craftSources.Values)
        {
            try
            {
                source.Dispose();
            }
            catch (Exception problem)
            {
                Problem($"a crafting source could not be removed: {problem.GetType().Name}: {problem.Message}");
            }
        }
        _craftSources.Clear();
        _craftSourcesPlayer = default;
        _craftSourcesStateVersion = -1;
    }

    private void DropKeepRules()
    {
        foreach (TransferRule rule in _keepRules.Values)
        {
            try
            {
                rule.Dispose();
            }
            catch (Exception problem)
            {
                Problem($"a keep rule could not be ended: {problem.GetType().Name}: {problem.Message}");
            }
        }
        _keepRules.Clear();
        foreach ((TransferRule rule, ulong _) in _slotKeepRules.Values)
        {
            try
            {
                rule.Dispose();
            }
            catch (Exception problem)
            {
                Problem($"a keep rule could not be ended: {problem.GetType().Name}: {problem.Message}");
            }
        }
        _slotKeepRules.Clear();
        _keepRulesPlayer = default;
    }

    // ---- What the buttons do. Each runs on the world's own thread, one at a time. ----

    private void Post(Func<Task> action)
    {
        Context.Scheduler.Post(() => _ = RunOne(action));
    }

    private async Task RunOne(Func<Task> action)
    {
        if (_busy)
        {
            _status = "Still busy with the last move.";
            return;
        }
        _busy = true;
        try
        {
            await action();
        }
        catch (Exception problem)
        {
            _status = $"Stopped: {problem.Message}";
            Problem($"an action stopped: {problem.GetType().Name}: {problem.Message}");
        }
        finally
        {
            _busy = false;
            RequestRead();
        }
    }

    private bool CanMove(EntityId player)
    {
        if (!player.IsNone && Client.IsHosting && Items.Available && Items.MovesAvailable)
        {
            return true;
        }
        _status = "Moves are not available in this world.";
        return false;
    }

    private async Task Take(ItemRow row, bool everyStack, bool toHotbars = false)
    {
        EntityId player = Client.LocalPlayer;
        if (!CanMove(player))
        {
            return;
        }
        int taken = 0;
        string ending = "";
        foreach (StackView stack in row.Stacks)
        {
            (int moved, string stackEnding) = await MoveIntoBackpack(new EntityId(stack.Holder), stack, player, "take", row.Name, toHotbars: toHotbars);
            taken += moved;
            ending = stackEnding;
            if (ending.Length > 0 || (!everyStack && moved > 0))
            {
                break;
            }
        }
        _status = string.Create(CultureInfo.InvariantCulture, $"Took {taken:N0} x {row.Name}.") + ending;
        if (taken > 0)
        {
            Hud.Toast(string.Create(CultureInfo.InvariantCulture, $"+{taken:N0} {row.Name}"), ToastKind.Success, 3);
        }
    }

    // A stack dragged between the hotbars and the inventory: Kiln moves between two owners only, so it passes through a linked
    // chest with a free slot (out of the player, then back into the chosen place). What can't come back stays in that chest.
    private async Task MoveWithinPlayer(ItemRow row, bool toHotbars)
    {
        EntityId player = Client.LocalPlayer;
        if (!CanMove(player) || row.Stacks.Count == 0)
        {
            return;
        }
        StackView stack = row.Stacks[0];
        EntityId relay = default;
        foreach ((EntityId entity, ChestPosition _) in _linkedChests)
        {
            if (Items.Read(entity) is InventoryContents contents && contents.FreeSlots() > 0)
            {
                relay = entity;
                break;
            }
        }
        if (relay.IsNone)
        {
            _status = "Moving between the hotbars and the inventory needs a linked chest with a free slot to pass through.";
            return;
        }
        PlayerRead? playerRead = ReadPlayer(player);
        bool fromHotbar = playerRead != null && playerRead.Layout.IsHotbarPage(new SlotId(stack.Slot).Inventory.Value);
        MoveResult out_ = await Items.Move(player, new SlotId(stack.Slot), relay, 0, fromHotbar ? MoveOptions.ActionBar : MoveOptions.None);
        Log.Info($"move within the player: {out_.Done} x {row.Name} out to chest {relay.Value}: {out_.Status}{(out_.Message.Length > 0 ? " (" + out_.Message + ")" : "")}");
        if (out_.Done <= 0 || out_.Changes.Count == 0)
        {
            _status = $"{row.Name} could not be moved. The game answered {out_.Status}{(out_.Message.Length > 0 ? ": " + out_.Message : "")}.";
            return;
        }
        int moved = 0;
        string ending = "";
        foreach (MoveChange change in out_.Changes)
        {
            var inChest = new StackView(relay.Value, change.To.Value, stack.Item, change.Amount, stack.Level, stack.Rarity, stack.IsGear);
            (int back, string stackEnding) = await MoveIntoBackpack(relay, inChest, player, "move within the player", row.Name, change.Amount, toHotbars);
            moved += back;
            if (stackEnding.Length > 0)
            {
                ending = stackEnding + " What did not fit is in a linked chest.";
            }
        }
        _status = string.Create(CultureInfo.InvariantCulture, $"Moved {moved:N0} x {row.Name} to your {(toHotbars ? "hotbars" : "backpack")}.") + ending;
    }

    private async Task Rescue(ItemRow row)
    {
        EntityId player = Client.LocalPlayer;
        if (!CanMove(player))
        {
            return;
        }
        // Kiln moves between two owners only, so a stranded stack goes through a linked chest: out of the worn slot, then into the backpack.
        int rescued = 0;
        string ending = "";
        foreach (StackView stack in row.Stacks)
        {
            if (HeldCount(player, stack) <= 0)
            {
                continue;
            }
            EntityId chest = default;
            foreach ((EntityId entity, ChestPosition _) in _linkedChests)
            {
                if (Items.Read(entity) is InventoryContents contents && contents.FreeSlots() > 0)
                {
                    chest = entity;
                    break;
                }
            }
            if (chest.IsNone)
            {
                ending = " It needs a linked chest with a free slot to pass through.";
                break;
            }
            MoveResult intoChest = await Items.Move(player, new SlotId(stack.Slot), chest, 0, MoveOptions.Equipped);
            Log.Info($"rescue: {intoChest.Done} x {row.Name} from the character's slot {new SlotId(stack.Slot).Inventory.Value}:{new SlotId(stack.Slot).Index} into chest {chest.Value}: {intoChest.Status}{(intoChest.Message.Length > 0 ? " (" + intoChest.Message + ")" : "")}");
            if (intoChest.Done <= 0 || intoChest.Changes.Count == 0)
            {
                ending = $" The game answered {intoChest.Status}{(intoChest.Message.Length > 0 ? ": " + intoChest.Message : "")}.";
                break;
            }
            foreach (MoveChange change in intoChest.Changes)
            {
                var inChest = new StackView(chest.Value, change.To.Value, stack.Item, change.Amount, stack.Level, stack.Rarity, stack.IsGear);
                (int moved, string stackEnding) = await MoveIntoBackpack(chest, inChest, player, "rescue", row.Name, change.Amount);
                rescued += moved;
                ending = stackEnding.Length > 0 ? stackEnding + " What did not fit is in a linked chest." : "";
            }
            if (ending.Length > 0)
            {
                break;
            }
        }
        _status = string.Create(CultureInfo.InvariantCulture, $"Moved {rescued:N0} x {row.Name} to your backpack.") + ending;
        if (rescued > 0)
        {
            Hud.Toast(string.Create(CultureInfo.InvariantCulture, $"+{rescued:N0} {row.Name}"), ToastKind.Success, 3);
        }
    }

    /// <summary>
    /// Moves one stack into the player's backpack, piece by piece: each piece goes to a backpack slot chosen here and is never larger
    /// than that slot's room, so Kiln has nothing left over to place by itself. Returns what moved and, when it stopped early, why.
    /// </summary>
    private async Task<(int Moved, string Ending)> MoveIntoBackpack(EntityId source, StackView stack, EntityId player, string verb, string itemName, int atMost = int.MaxValue,
        bool toHotbars = false)
    {
        int inSlot = HeldCount(source, stack);
        int left = Math.Min(inSlot, atMost);
        int moved = 0;
        ItemInfo? info = Items.Get(new ItemId(stack.Item));
        int maxStack = info?.MaxStack ?? 0;
        while (left > 0)
        {
            PlayerRead? playerRead = ReadPlayer(player);
            if (playerRead == null)
            {
                return (moved, " Your inventory could not be read.");
            }
            // Dropped on the hotbars: hotbar 1, then hotbar 2, then the backpack.
            Destination? destination = (toHotbars ? ChestRules.ChooseHotbarSlot(playerRead.Layout, playerRead.Slots, stack.Item, maxStack, stack.IsGear) : null)
                ?? ChestRules.ChooseBackpackSlot(playerRead.Layout, playerRead.Slots, stack.Item, maxStack, stack.IsGear);
            if (destination is not Destination slot)
            {
                return (moved, toHotbars ? " Your hotbars and backpack are full." : " Your backpack is full.");
            }
            int amount = Math.Min(left, slot.Room);
            // A count of 0 is Kiln's whole stack, which goes over in one transfer; a part goes one item at a time.
            // A hotbar slot needs Kiln's ActionBar option (0.10.10 was refused without it).
            MoveOptions into = playerRead.Layout.IsHotbarPage(slot.Page) ? MoveOptions.ActionBar : MoveOptions.None;
            MoveResult result = await Items.Move(source, new SlotId(stack.Slot), player, amount == inSlot ? 0 : amount, into,
                SlotId.Of(new EntityId(slot.Page), slot.Index));
            moved += result.Done;
            left -= result.Done;
            inSlot -= result.Done;
            int outside = 0;
            foreach (MoveChange change in result.Changes)
            {
                uint landedOn = change.To.Inventory.Value;
                if (!playerRead.Layout.IsBackpackPage(landedOn) && !(toHotbars && playerRead.Layout.IsHotbarPage(landedOn)))
                {
                    outside += change.Amount;
                }
            }
            Log.Info($"{verb}: {result.Done} x {itemName} from {source.Value} into {(playerRead.Layout.IsHotbarPage(slot.Page) ? "hotbar" : "backpack")} slot {slot.Page}:{slot.Index}: {result.Status}{(result.Message.Length > 0 ? " (" + result.Message + ")" : "")}");
            if (outside > 0)
            {
                Problem($"{verb}: {outside} x {itemName} landed outside the backpack");
                return (moved, string.Create(CultureInfo.InvariantCulture, $" {outside:N0} landed outside your backpack: see the list at the top of the Items tab."));
            }
            if (result.Done <= 0 || (result.Status != ItemsStatus.Done && result.Status != ItemsStatus.Partial))
            {
                return (moved, result.Done > 0 ? "" : $" The game answered {result.Status}{(result.Message.Length > 0 ? ": " + result.Message : "")}.");
            }
        }
        return (moved, "");
    }

    private async Task Store(ItemRow row)
    {
        EntityId player = Client.LocalPlayer;
        if (!CanMove(player))
        {
            return;
        }
        if (_linkedChests.Count == 0)
        {
            _status = "No chest is linked yet: link some on the Chests tab.";
            return;
        }
        PlayerRead? playerRead = ReadPlayer(player);
        if (playerRead == null)
        {
            _status = "Your inventory could not be read.";
            return;
        }
        bool playerPlaced = World.TryGetPosition(player, out WorldPosition playerWorldPosition);
        ChestPosition playerPosition = ToChestPosition(playerWorldPosition);
        int stored = 0;
        int wanted = 0;
        string ending = "";
        foreach (StackView stack in row.Stacks)
        {
            uint page = new SlotId(stack.Slot).Inventory.Value;
            bool fromHotbar = playerRead.Layout.IsHotbarPage(page);
            if (!playerRead.Layout.IsBackpackPage(page) && !(fromHotbar && _state.IncludeHotbar))
            {
                continue;
            }
            int left = HeldCount(player, stack);
            if (left <= 0)
            {
                continue;
            }
            wanted += left;
            var chests = new List<DepositChest>(_linkedChests.Count);
            foreach ((EntityId entity, ChestPosition position) in _linkedChests)
            {
                InventoryContents? contents = Items.Read(entity);
                if (contents == null)
                {
                    continue;
                }
                chests.Add(new DepositChest(entity.Value, contents.Count(new ItemId(stack.Item)), contents.FreeSlots(),
                    playerPlaced ? position.DistanceTo(playerPosition) : 0));
            }
            foreach (uint chestEntity in ChestRules.DepositOrder(chests))
            {
                MoveResult result = await Items.Move(player, new SlotId(stack.Slot), new EntityId(chestEntity), 0, fromHotbar ? MoveOptions.ActionBar : MoveOptions.None);
                stored += result.Done;
                left -= result.Done;
                Log.Info($"store: {result.Done} x {row.Name} into chest {chestEntity}: {result.Status}{(result.Message.Length > 0 ? " (" + result.Message + ")" : "")}");
                if (result.Status == ItemsStatus.Done || left <= 0)
                {
                    break;
                }
                if (result.Status != ItemsStatus.NoRoom && result.Status != ItemsStatus.Partial)
                {
                    ending = $" The game answered {result.Status}{(result.Message.Length > 0 ? ": " + result.Message : "")}.";
                    break;
                }
            }
            if (ending.Length > 0)
            {
                break;
            }
        }
        if (stored < wanted && ending.Length == 0)
        {
            ending = " The linked chests have no room for the rest.";
        }
        _status = string.Create(CultureInfo.InvariantCulture, $"Stored {stored:N0} x {row.Name}.") + ending;
        if (stored > 0)
        {
            Hud.Toast(string.Create(CultureInfo.InvariantCulture, $"Stored {stored:N0} {row.Name}"), ToastKind.Info, 3);
        }
    }

    // The screen can be half a second old: a slot is only moved when it still holds the item its line showed. Returns what it holds now.
    private static int HeldCount(EntityId holder, StackView stack)
    {
        InventoryContents? contents = Items.Read(holder);
        if (contents == null)
        {
            return 0;
        }
        foreach (InventorySlot slot in contents.Slots)
        {
            if (slot.Slot.Value == stack.Slot)
            {
                return !slot.IsEmpty && slot.Item.Value == stack.Item ? slot.Count : 0;
            }
        }
        return 0;
    }

    private Task SetLinked(ChestView chest, bool linked)
    {
        int linkIndex = ChestRules.FindLink(_state.Links, chest.Position);
        if (linked && linkIndex < 0)
        {
            _state.Links.Add(chest.Position);
        }
        else if (!linked && linkIndex >= 0)
        {
            _state.Links.RemoveAt(linkIndex);
            WithdrawForgeGems(new EntityId(chest.Entity));
        }
        SaveState();
        _status = linked ? $"Linked {chest.Label}." : $"Unlinked {chest.Label}.";
        return Task.CompletedTask;
    }

    private Task LinkNearby(IReadOnlyList<ChestView> chests, double radiusMeters)
    {
        int added = 0;
        foreach (ChestView chest in chests)
        {
            if (chest.DistanceMeters <= radiusMeters && ChestRules.FindLink(_state.Links, chest.Position) < 0)
            {
                _state.Links.Add(chest.Position);
                added++;
            }
        }
        SaveState();
        _status = string.Create(CultureInfo.InvariantCulture, $"Linked {added} more chest(s) within {radiusMeters:0} m.");
        return Task.CompletedTask;
    }

    private Task UnlinkAll()
    {
        int removed = _state.Links.Count;
        _state.Links.Clear();
        WithdrawForgeGems();
        SaveState();
        _status = string.Create(CultureInfo.InvariantCulture, $"Unlinked {removed} chest(s). Nothing was moved.");
        return Task.CompletedTask;
    }

    private Task SetFavorite(uint item, int rarity, bool isGear, bool favorite)
    {
        if (_toolEntries.TryGetValue(item, out uint toolKey))
        {
            item = toolKey;  // a hammer's shape or a staff's helper: the tool's favorite
            isGear = false;
        }
        ChestRules.SetFavorite(_state, item, rarity, isGear, favorite);
        RefreshMarkedFavorites();
        SaveState();
        SyncKeepRules();
        return Task.CompletedTask;
    }

    // The catalog's tool entries, read once the world has loaded its items.
    private void EnsureToolEntries()
    {
        if (_toolEntries.Count > 0 || !Items.Available)
        {
            return;
        }
        var entries = new Dictionary<uint, uint>();
        foreach (ItemInfo info in Items.All)
        {
            uint key = ChestRules.ToolKeyOf(info.Name);
            if (key != 0)
            {
                entries[info.Id.Value] = key;
            }
        }
        if (entries.Count > 0)
        {
            _toolEntries = entries;
            Log.Info($"tool entries: {entries.Count} (the Construction Hammer's shapes, the summoning staff's helpers, the Overgrowth and Decay tools' brushes)");
        }
    }

    private void RefreshMarkedFavorites()
    {
        EnsureToolEntries();
        _markedFavorites = new HashSet<uint>(_state.Favorites);
        _markedGearItems = new HashSet<uint>(_state.FavoriteSet().GearItems);
        _markedGearKeys = new HashSet<ulong>(_state.GearFavorites);
    }

    // ---- Using a food, drink, potion, bandage or scroll from the screen (since 0.10.0 through Kiln's Abilities.UseItem). ----
    //
    // The game's own use, as from the hotbar: the item's sequence plays (eating, drinking), the game takes one item at the sequence's
    // consume event and applies the buff with the item's own values and time, so the buff panel shows them as for a hand use. Kiln
    // needs the item in one of the player's own slots: an item in a chest is moved into the backpack first. 0.6.0 to 0.9.1 applied
    // the buff and wrote the item's numbers into it themselves, before Kiln had this call.

    // Kiln's render thread reads this: the consumables among the items the screen shows, made again at each read of the chests.
    private bool IsUsable(uint item)
    {
        return _usable.Contains(item) && !_notUsable.Contains(item);
    }

    // The hosted world's thread, at each read of the chests.
    private void NoteUsable(IEnumerable<ItemRow> rows)
    {
        var usable = new HashSet<uint>();
        foreach (ItemRow row in rows)
        {
            if (row.Item != 0 && !usable.Contains(row.Item) && Items.Get(new ItemId(row.Item))?.Category == ItemCategory.Consumables)
            {
                usable.Add(row.Item);
            }
        }
        _usable = usable;
    }

    private async Task Use(ItemRow row)
    {
        EntityId player = Client.LocalPlayer;
        if (!CanMove(player))
        {
            return;
        }
        if (!IsUsable(row.Item))
        {
            Notify($"{row.Name} can't be used from here: only consumables can.", ToastKind.Info);
            return;
        }
        if (!Abilities.Available)
        {
            Notify("Kiln can't make you use items in this world.", ToastKind.Warning);
            return;
        }
        StackView? stack = ChestRules.SmallestStack(row.Stacks);
        PlayerRead? playerRead = ReadPlayer(player);
        if (stack == null || playerRead == null)
        {
            _status = stack == null ? $"No {row.Name} left." : "Your inventory could not be read.";
            return;
        }
        var slot = new SlotId(stack.Slot);
        bool fromInventory = playerRead.Layout.IsBackpackPage(slot.Inventory.Value) || playerRead.Layout.IsHotbarPage(slot.Inventory.Value);
        if (!fromInventory)
        {
            // From a chest: one item into the backpack first, into a slot the mod names (a stack of it with room, else a free slot).
            int maxStack = Items.Get(new ItemId(stack.Item))?.MaxStack ?? 0;
            if (ChestRules.ChooseBackpackSlot(playerRead.Layout, playerRead.Slots, stack.Item, maxStack, stack.IsGear) is not Destination destination)
            {
                Notify($"Your backpack is full: {row.Name} can't be used from its chest.", ToastKind.Info);
                return;
            }
            SlotId into = SlotId.Of(new EntityId(destination.Page), destination.Index);
            MoveResult moved = await Items.Move(new EntityId(stack.Holder), slot, player, 1, MoveOptions.None, into);
            if (moved.Done < 1)
            {
                _status = $"{row.Name} could not be taken out of its chest: {moved.Status}{(moved.Message.Length > 0 ? " (" + moved.Message + ")" : "")}.";
                return;
            }
            slot = into;
        }

        ItemUseResult result;
        try
        {
            result = Abilities.UseItem(player, slot);
        }
        catch (Exception problem) when (problem is InvalidOperationException or KilnUnavailableException or ArgumentException)
        {
            Notify($"{row.Name} could not be used: {problem.Message}", ToastKind.Warning);
            Problem($"use: {row.Name}: {problem.GetType().Name}: {problem.Message}");
            return;
        }
        if (_useLogs < 30)
        {
            _useLogs++;
            Log.Info($"use: {row.Name} (0x{row.Item:x8}) {(fromInventory ? "from the inventory" : "from chest " + stack.Holder + ", moved into the backpack first")}: {result}");
        }
        string inBackpack = fromInventory ? "" : " It is in your backpack now.";
        switch (result)
        {
            case ItemUseResult.Started:
                _status = $"Using {row.Name}.";
                Hud.Toast($"Used {row.Name}", ToastKind.Success, 3);
                break;
            case ItemUseResult.Busy:
                Notify($"You are busy (another action is running): {row.Name} was not used.{inBackpack}", ToastKind.Info);
                break;
            case ItemUseResult.NotConsumable:
                // Kiln uses only what a sequence consumes at a consume event (food, drinks, potions); the repair kit pays its use
                // as a cost instead. Its Use goes, and one brought from a chest goes back.
                _notUsable = new HashSet<uint>(_notUsable) { row.Item };
                if (!fromInventory)
                {
                    MoveResult back = await Items.Move(player, slot, new EntityId(stack.Holder), 1);
                    inBackpack = back.Done > 0 ? " It went back to its chest." : inBackpack;
                }
                Notify($"{row.Name} can't be used from this screen: use it from the hotbar.{inBackpack}", ToastKind.Info);
                break;
            case ItemUseResult.Dead:
            case ItemUseResult.NotReady:
                Notify($"You can't use items right now.{inBackpack}", ToastKind.Info);
                break;
            default:
                Notify($"{row.Name} was not used ({result}).{inBackpack}", ToastKind.Info);
                break;
        }
    }

    // ---- Chests lit after a quick deposit: the ones that received items. ----
    //
    // The chests themselves get the game's own outline through Kiln (Ui.World.Outline, since Kiln 0.2.0-dev.30.3; 0.7.0 to 0.9.1
    // used a native companion made from Quick Deposit's code). Where Kiln can't outline, or none shows a second and a half in, each
    // chest gets the box and label the Chests list draws. They are lit while an inventory or this mod's screen is open, and then for
    // the Highlight Time. Only chests within 200 m are lit.

    // Kiln 0.2.0-dev.30.5's through-walls silhouettes: with them the outline is seen anywhere, and the boxes are not drawn (the
    // user's choice: the box only while the outline can't be seen through walls). Written on the world's thread, read while drawing.
    private static volatile bool _throughWalls;

    private static bool ThroughWallsOn()
    {
        try
        {
            _throughWalls = Ui.World.ThroughWallsAvailable;
        }
        catch (Exception)
        {
            _throughWalls = false;
        }
        return _throughWalls;
    }

    private static bool OutlinesOn()
    {
        try
        {
            return Ui.World.OutlinesAvailable;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void HighlightChests(List<LitChest> chests, string after, bool asked = false)
    {
        if (chests.Count == 0 || (!asked && !(_settings?.Value.HighlightDepositedChests ?? true)))
        {
            return;
        }
        DropDepositOutlines();
        long now = Environment.TickCount64;
        if (OutlinesOn())
        {
            foreach (LitChest chest in chests)
            {
                try
                {
                    _depositOutlines.Add(Ui.World.Outline(new EntityId(chest.Entity), new OutlineOptions { Color = FavoriteColor, Pulse = true, ThroughWalls = ThroughWallsOn() }));
                }
                catch (Exception problem) when (problem is ArgumentException or InvalidOperationException or KilnUnavailableException)
                {
                    Problem($"highlight: chest {chest.Entity} can't be outlined ({problem.Message})");
                }
            }
        }
        bool outline = _depositOutlines.Count > 0;
        // The boxes are drawn with the outline too (since 0.10.13): the game's outline does not show through walls.
        bool boxes = EnsureRenderFrames();
        if (!outline && !boxes)
        {
            return;
        }
        _depositHighlight = new DepositHighlight(chests, 0, now, outline, outline ? now + 1500 : 0);
        if (_highlightLogs < 20)
        {
            _highlightLogs++;
            Log.Info($"highlight: {chests.Count} chest(s) after {after}, as {(outline ? "the game's outline (Kiln) and boxes" : "boxes (Kiln's outline is not available here)")}");
        }
    }

    // The hosted world's thread, every step while chests are lit: a few comparisons, and the open screens asked ten times a second.
    private void StepDepositHighlight(ulong step)
    {
        DepositHighlight? lit = _depositHighlight;
        if (lit == null)
        {
            return;
        }
        long now = Environment.TickCount64;
        if (lit.CheckOutlineAtTick != 0 && now >= lit.CheckOutlineAtTick)
        {
            // What the outlines do a second and a half in: when none shows, the boxes take over.
            int shown = 0;
            var states = new List<string>();
            foreach (WorldOutline outline in _depositOutlines)
            {
                if (outline.State == OutlineState.Shown)
                {
                    shown++;
                }
                states.Add(outline.State + "/" + outline.Reason);
            }
            if (_highlightLogs < 20)
            {
                _highlightLogs++;
                Log.Info($"highlight: {shown} of {_depositOutlines.Count} outline(s) shown ({string.Join(", ", states)})");
            }
            bool boxesInstead = shown == 0 && EnsureRenderFrames();
            if (boxesInstead)
            {
                DropDepositOutlines();
            }
            lit = lit with { CheckOutlineAtTick = 0, Outline = !boxesInstead };
            _depositHighlight = lit;
        }
        if (lit.UntilTick == 0)
        {
            if (step % 6 != 0)
            {
                return;
            }
            bool screensOpen = _screen?.IsOpen == true || ShowsItemSlots(Ui.Screens.Current.Hud);
            if (screensOpen && now - lit.SinceTick < DepositHighlightLongestWaitMilliseconds)
            {
                return;
            }
            int seconds = Math.Clamp(_settings?.Value.HighlightSeconds ?? ChestRules.DefaultHighlightSeconds, ChestRules.MinHighlightSeconds, ChestRules.MaxHighlightSeconds);
            _depositHighlight = lit with { UntilTick = now + seconds * 1000L };
            return;
        }
        if (now > lit.UntilTick)
        {
            DropDepositOutlines();
            _depositHighlight = null;
        }
    }

    // LMB on a category of the Categories tab (since 0.10.18): the chests named for it are marked in the world, as after a quick
    // deposit (outline and box, while the screen is open and then for the Highlight Time). A group marks the chests named for it
    // and for each of its sub-categories. Every chest the screen lists counts, linked or not, within the marking distance.
    private Task HighlightNamedChests(string key)
    {
        if (_tree == null || _snapshot is not { } snapshot)
        {
            return Task.CompletedTask;
        }
        bool isGroup = !key.Contains('/');
        var lit = new List<LitChest>();
        foreach (ChestView chest in snapshot.Chests)
        {
            if (chest.Name == null || chest.DistanceMeters > ChestRules.MaxHighlightMeters)
            {
                continue;
            }
            bool named = false;
            foreach (KeyValuePair<int, int> entry in _tree.SectionRanksOfChestName(chest.Name))
            {
                TreeSection section = _tree.Sections[entry.Key];
                named |= isGroup ? section.Group == key : section.Key == key && entry.Value == CategoryTree.SectionNameRank;
            }
            if (named && World.TryGetPosition(new EntityId(chest.Entity), out WorldPosition foot))
            {
                lit.Add(new LitChest(chest.Entity, new WorldPosition(foot.X, foot.Y + 0.5, foot.Z), chest.Label));
            }
        }
        string name = isGroup ? key : key[(key.IndexOf('/') + 1)..];
        _status = lit.Count == 0
            ? $"No chest within {ChestRules.MaxHighlightMeters:0} m is named for {name}."
            : string.Create(CultureInfo.InvariantCulture, $"{lit.Count} chest(s) named for {name} marked in the world.");
        HighlightChests(lit, "a click on " + name, asked: true);
        return Task.CompletedTask;
    }

    private void DropDepositOutlines()
    {
        foreach (WorldOutline outline in _depositOutlines)
        {
            outline.Dispose();
        }
        _depositOutlines.Clear();
    }

    // The chest clicked in the Chests list: its own outline for the Highlight Time, besides the box, label and arrow.
    private static void OutlineFor(uint entity, int seconds)
    {
        if (entity == 0 || !OutlinesOn())
        {
            return;
        }
        try
        {
            Ui.World.Outline(new EntityId(entity), new OutlineOptions { Color = FavoriteColor, Pulse = true, Duration = TimeSpan.FromSeconds(seconds), ThroughWalls = ThroughWallsOn() });
        }
        catch (Exception problem) when (problem is ArgumentException or InvalidOperationException or KilnUnavailableException)
        {
            // The box, label and arrow still show.
        }
    }

    private bool EnsureRenderFrames()
    {
        try
        {
            _renderFrames ??= Events.On<RenderFrameEvent>(OnRenderFrame);
            return true;
        }
        catch (Exception problem)
        {
            Problem($"Kiln can't draw in the world here ({problem.GetType().Name}: {problem.Message})");
            return false;
        }
    }

    private void DropChestHighlights()
    {
        _highlight = null;
        _depositHighlight = null;
        DropDepositOutlines();
    }

    // ---- The category tree (since 0.9.0): where each item goes, from data\item_categories.json and the hand-made material list. ----

    private void EnsureTree()
    {
        if (_tree != null || _treeBroken)
        {
            return;
        }
        try
        {
            if (_treeText == null)
            {
                using Stream? table = typeof(StreamlinedLogisticsMod).Assembly.GetManifestResourceStream(ItemCategoriesResource);
                if (table == null)
                {
                    _treeBroken = true;
                    Log.Warn("the category table is not in the mod: the list is grouped by the category file only");
                    return;
                }
                using var reader = new StreamReader(table);
                _treeText = reader.ReadToEnd();
            }
            IReadOnlyList<string> names = _categories?.Names ?? Array.Empty<string>();
            IReadOnlyList<bool> materials = _categories?.MaterialFlags() ?? Array.Empty<bool>();
            if (!CategoryTree.TryParse(_treeText, names, materials, out CategoryTree tree, out string error))
            {
                _treeBroken = true;
                Log.Warn($"the category table could not be read ({error}): the list is grouped by the category file only");
                return;
            }
            _tree = tree;
            // All first (since 1.0.1), then the tree's names.
            var choices = new List<string> { ChestRules.CatchAllName };
            choices.AddRange(tree.ChestNameChoices());
            _chestNameChoices = choices;
            _placeOfItems.Clear();
            if (!_state.HiddenTreeChosen)
            {
                // The tree's first start: what is only used through the craft menu or the Construction Hammer starts hidden.
                _state.HiddenTreeChosen = true;
                _state.HiddenTree.Clear();
                _state.HiddenTree.Add(CategoryTree.MaterialsGroup);
                _state.HiddenTree.Add("Building");
                SaveState();
            }
            Log.Info($"category tree: {tree.Sections.Count} categories in {tree.Groups.Count} groups; hidden: {string.Join(", ", _state.HiddenTree)}");
        }
        catch (Exception problem) when (problem is IOException or InvalidOperationException)
        {
            _treeBroken = true;
            Problem($"the category table could not be read: {problem.Message}");
        }
    }

    // The item whose icon and details a row shows: the Construction Hammer for its shapes, else the item itself.
    private static ItemId ShownItem(uint item)
    {
        return new ItemId(_toolEntries.TryGetValue(item, out uint toolKey) && ChestRules.ToolItemOf(toolKey) != 0 ? ChestRules.ToolItemOf(toolKey) : item);
    }

    private (int Section, string Order) PlaceOf(uint item)
    {
        if (_toolEntries.TryGetValue(item, out uint toolKey))
        {
            // With the tools, as the hammer: the staff right after it.
            uint toolItem = ChestRules.ToolItemOf(toolKey);
            (int section, string order) = PlaceOf(toolItem != 0 ? toolItem : ConstructionHammerItem);
            return toolItem != 0 ? (section, order) : (section, order + "~");
        }
        EnsureTree();
        if (_tree == null)
        {
            return (-1, "");
        }
        if (!_placeOfItems.TryGetValue(item, out (int Section, string Order) place))
        {
            ItemInfo? info = Items.Get(new ItemId(item));
            place = _tree.Place(item, CategoriesOfItem(item), info?.Category.ToString());
            _placeOfItems[item] = place;
        }
        return place;
    }

    private IReadOnlyList<ItemRow> WithPlaces(IReadOnlyList<ItemRow> rows)
    {
        if (_tree == null)
        {
            return rows;
        }
        var placed = new List<ItemRow>(rows.Count);
        foreach (ItemRow row in rows)
        {
            placed.Add(row with { Section = PlaceOf(row.Item).Section });
        }
        return placed;
    }

    private IReadOnlyList<ItemRow> InTreeOrder(IReadOnlyList<ItemRow> rows)
    {
        return _tree == null ? rows : _tree.Ordered(rows, row => row.Section, row => PlaceOf(row.Item).Order);
    }

    // The Categories tab's lines for the tree: each group, then its categories, with the items of the linked chests in each.
    private List<CategoryView> TreeCategoryViews(IReadOnlyList<ItemRow> storedRows, List<(string? Name, int FreeSlots)> linkedNames)
    {
        var views = new List<CategoryView>();
        if (_tree == null)
        {
            return views;
        }
        // The linked chests named exactly for each sub-category and each group, with their free slots.
        var sectionChests = new int[_tree.Sections.Count];
        var sectionFree = new int[_tree.Sections.Count];
        var groupChests = new Dictionary<string, (int Chests, int Free)>();
        foreach ((string? name, int free) in linkedNames)
        {
            string? namedGroup = null;
            foreach (KeyValuePair<int, int> entry in _tree.SectionRanksOfChestName(name))
            {
                if (entry.Value == CategoryTree.SectionNameRank)
                {
                    sectionChests[entry.Key]++;
                    sectionFree[entry.Key] += free;
                }
                else if (entry.Value == CategoryTree.GroupNameRank)
                {
                    namedGroup = _tree.Sections[entry.Key].Group;
                }
            }
            if (namedGroup != null)
            {
                (int chests, int groupFree) = groupChests.GetValueOrDefault(namedGroup);
                groupChests[namedGroup] = (chests + 1, groupFree + free);
            }
        }
        var counts = new int[_tree.Sections.Count];
        foreach (ItemRow row in storedRows)
        {
            if (row.Section >= 0 && row.Section < counts.Length)
            {
                counts[row.Section]++;
            }
        }
        foreach (string group in _tree.Groups)
        {
            int groupCount = 0;
            var children = new List<CategoryView>();
            foreach (TreeSection section in _tree.Sections)
            {
                if (section.Group != group)
                {
                    continue;
                }
                groupCount += counts[section.Index];
                children.Add(new CategoryView(section.Name, _tree.IsHidden(section.Index, _state.HiddenTree), counts[section.Index], section.Key, 1,
                    sectionChests[section.Index], sectionFree[section.Index]));
            }
            (int groupNamed, int groupNamedFree) = groupChests.GetValueOrDefault(group);
            views.Add(new CategoryView(group, _state.HiddenTree.Contains(group), groupCount, group, 0, groupNamed, groupNamedFree));
            views.AddRange(children);
        }
        return views;
    }

    private HashSet<int> HiddenSections()
    {
        var hidden = new HashSet<int>();
        if (_tree != null)
        {
            foreach (TreeSection section in _tree.Sections)
            {
                if (_tree.IsHidden(section.Index, _state.HiddenTree))
                {
                    hidden.Add(section.Index);
                }
            }
        }
        return hidden;
    }

    private Task SetTreeHidden(string key, bool hidden)
    {
        EnsureTree();
        _state.HiddenTreeChosen = true;
        int slash = key.IndexOf('/');
        if (hidden)
        {
            _state.HiddenTree.Add(key);
        }
        else if (slash > 0 && _state.HiddenTree.Contains(key[..slash]) && _tree != null)
        {
            // Shown out of a hidden group: the group is shown, with its other categories hidden one by one.
            string group = key[..slash];
            _state.HiddenTree.Remove(group);
            foreach (TreeSection section in _tree.Sections)
            {
                if (section.Group == group && !string.Equals(section.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    _state.HiddenTree.Add(section.Key);
                }
            }
            _state.HiddenTree.Remove(key);
        }
        else
        {
            _state.HiddenTree.Remove(key);
            if (slash < 0 && _tree != null)
            {
                // A group shown: its categories too.
                foreach (TreeSection section in _tree.Sections)
                {
                    if (section.Group == key)
                    {
                        _state.HiddenTree.Remove(section.Key);
                    }
                }
            }
        }
        SaveState();
        return Task.CompletedTask;
    }

    /// <summary>Hides every group, none, or (null) the ones hidden by default: Materials and Building.</summary>
    private Task SetTreeHiddenAll(bool? all)
    {
        EnsureTree();
        _state.HiddenTreeChosen = true;
        _state.HiddenTree.Clear();
        if (all == true && _tree != null)
        {
            foreach (string group in _tree.Groups)
            {
                _state.HiddenTree.Add(group);
            }
        }
        else if (all == null)
        {
            _state.HiddenTree.Add(CategoryTree.MaterialsGroup);
            _state.HiddenTree.Add("Building");
        }
        SaveState();
        return Task.CompletedTask;
    }

    // Kiln's render thread: a header that folds its category or group, its state kept in the state file.
    private bool DrawFoldHeader(UiDraw ui, string key, string title, int depth, int count)
    {
        bool collapsed = _collapsedView.Contains(key);
        string label = string.Create(CultureInfo.InvariantCulture, $"{(depth > 0 ? "      " : "")}{(collapsed ? "+" : "-")}  {title}  ({count})##fold:{key}");
        if (depth == 0)
        {
            ui.Separator();
        }
        if (ui.Selectable(label))
        {
            SetFolded(key, !collapsed);
        }
        ui.Tooltip(collapsed ? "LMB: Unfold" : "LMB: Fold");
        return !collapsed;
    }

    // Kiln's render thread: folds or unfolds a header now, and keeps it in the state file.
    private void SetFolded(string key, bool collapsed)
    {
        if (_collapsedView.Contains(key) == collapsed)
        {
            return;
        }
        var view = new HashSet<string>(_collapsedView, StringComparer.OrdinalIgnoreCase);
        if (collapsed)
        {
            view.Add(key);
        }
        else
        {
            view.Remove(key);
        }
        _collapsedView = view;
        Context.Scheduler.Post(() =>
        {
            if (collapsed)
            {
                _state.Collapsed.Add(key);
            }
            else
            {
                _state.Collapsed.Remove(key);
            }
            SaveState();
        });
    }

    // ---- The navigation sidebar (since 0.10.39, asked by the user on 24 Nov 2026): the groups and categories the list shows (or the
    // chests, chest by chest), with their counts; a click scrolls the list to that header and unfolds it. "<" folds the sidebar to
    // a strip, ">" brings it back. ----

    private const float SidebarWidth = 190;
    private const float SidebarStripWidth = 34;
    private const string GroupJumpPrefix = "group-header:";

    private sealed record NavEntry(string Label, string Target, int Depth, string? GroupFold, string? SectionFold);

    private void DrawSidebar(UiDraw ui, IReadOnlyList<NavEntry> entries, bool chestByChest)
    {
        bool hidden = _sidebarHiddenView;
        if (ui.Button(hidden ? ">##sidebar-toggle" : "<##sidebar-toggle"))
        {
            bool nowHidden = !hidden;
            _sidebarHiddenView = nowHidden;
            Context.Scheduler.Post(() =>
            {
                _state.SidebarHidden = nowHidden;
                SaveState();
            });
        }
        ui.Tooltip(hidden ? "Show Sidebar" : "Hide Sidebar");
        if (hidden)
        {
            return;
        }
        ui.SameLine();
        ui.Text(chestByChest ? "Chests" : "Categories");
        ui.Separator();
        if (entries.Count == 0)
        {
            ui.TextDisabled("Nothing Shown");
            return;
        }
        foreach (NavEntry entry in entries)
        {
            if (ui.Selectable((entry.Depth > 0 ? "   " : "") + entry.Label + "##nav:" + entry.Target))
            {
                if (entry.GroupFold != null)
                {
                    SetFolded(entry.GroupFold, false);
                }
                if (entry.SectionFold != null)
                {
                    SetFolded(entry.SectionFold, false);
                }
                _jumpTo = entry.Target;
            }
        }
    }

    // Kiln's render thread: the bar between the stored items and the backpack. Pressed and dragged, it moves the share of the
    // window's width the stored items take; released, the share is kept.
    private void DrawSplitter(UiDraw ui)
    {
        float height = Math.Max(ui.Cursor.AvailHeight, 40 * ui.Scale);
        ui.Button("##storage-backpack-split", 6 * ui.Scale, height);
        bool hovered = ui.ItemHovered;
        Vector2? pointer = ui.Pointer;
        if (!_splitDragging && hovered && pointer is { } start && ui.ItemClicked(UiMouseButton.Left))
        {
            _splitDragging = true;
            _splitDragStartX = start.X;
            _splitDragStartShare = _storageShareView;
        }
        if (_splitDragging)
        {
            float width = ui.WindowRect.Width;
            if (ui.ButtonHeld(UiMouseButton.Left) && pointer is { } now && width > 0)
            {
                _storageShareView = ChestRules.ClampStorageShare(_splitDragStartShare + (now.X - _splitDragStartX) / width);
            }
            else if (!ui.ButtonHeld(UiMouseButton.Left))
            {
                _splitDragging = false;
                float share = _storageShareView;
                Context.Scheduler.Post(() =>
                {
                    _state.StorageShare = share;
                    SaveState();
                });
            }
        }
        else if (hovered)
        {
            ui.Tooltip("Drag: Resize");
        }
    }

    // Kiln's render thread, inside the list: scrolls so the header drawn next sits at the top, when it is the one asked for.
    private void JumpIfAsked(UiDraw ui, string target)
    {
        if (_jumpTo == target)
        {
            ui.ScrollHere(0);
            _jumpTo = null;
        }
    }

    // Kiln's render thread: the bar above the grouped list, to show one group at a time.
    // Kiln's render thread: tabs above the grouped list, to show one group at a time (All, Favorites, Gear, ...).
    private void DrawCategoryBar(UiDraw ui, IReadOnlyList<ItemSection> sections)
    {
        var groups = new List<string>();
        bool favorites = false;
        foreach (ItemSection section in sections)
        {
            if (section.Key == CategoryTree.FavoritesKey)
            {
                favorites = true;
            }
            else if (section.Group.Length > 0 && !groups.Contains(section.Group))
            {
                groups.Add(section.Group);
            }
        }
        if (groups.Count == 0 || !ui.BeginTabBar("category-tabs"))
        {
            return;
        }
        string? selected = null;
        if (ui.BeginTab("All"))
        {
            ui.EndTab();
        }
        if (favorites && ui.BeginTab(CategoryTree.FavoritesKey))
        {
            selected = CategoryTree.FavoritesKey;
            ui.EndTab();
        }
        foreach (string group in groups)
        {
            if (ui.BeginTab(group))
            {
                selected = group;
                ui.EndTab();
            }
        }
        ui.EndTabBar();
        _focusGroup = selected;
    }

    private List<string> SectionGroupNames()
    {
        var groups = new List<string>();
        if (_tree != null)
        {
            foreach (TreeSection section in _tree.Sections)
            {
                groups.Add(section.Group);
            }
        }
        return groups;
    }

    // The hand-made categories' words ask for items only when the tree's table is missing (since 0.10.16: exact names only).
    private ulong HandMadeBitsOfChestName(string? name)
    {
        EnsureTree();
        return _tree == null ? _categories?.BitsOfChestName(name) ?? 0 : 0;
    }

    private Dictionary<int, int> TreeSectionRanksOfChestName(string? name)
    {
        EnsureTree();
        return _tree?.SectionRanksOfChestName(name) ?? new Dictionary<int, int>();
    }

    // How closely a chest's name asks for an item: its sub-category's rank, or a hand-made category's word; a chest named All asks
    // for every item, after all of those (since 1.0.1).
    private static int NameRankFor(DepositChestState chest, ulong itemCategories, int itemSection)
    {
        int rank = itemSection >= 0 ? chest.SectionRanks.GetValueOrDefault(itemSection) : 0;
        if ((chest.NameCategories & itemCategories) != 0)
        {
            rank = Math.Max(rank, CategoryTree.WordRank);
        }
        return rank == 0 && chest.IsCatchAll ? CategoryTree.CatchAllRank : rank;
    }

    // ---- Item details on hover: the rows of the game's own tooltip (effects, durations), with its labels, and the description. ----
    //
    // Kiln gives an item type's tooltip rows (Content.TooltipValues: its uiValues, as the game's data holds them) on the world's
    // thread, and the game's texts (Content.Text) only on the game client's main thread. So the rows are read on the hosted
    // world's tick for the items the screen shows, a few at a time, and their labels are looked up on the client's thread.
    // The drawing reads both through immutable copies. Damage, a stack's level and rarity, gems and perks are not in the rows.

    private sealed record ItemDetails(IReadOnlyList<TooltipValue> Rows, uint DescriptionLoca);

    // The hosted world's thread, after a read of the chests: details of the items on screen not read yet, at most a few each time.
    private void ReadItemDetails(IEnumerable<ItemRow> rows)
    {
        if (_detailsBroken)
        {
            return;
        }
        Dictionary<uint, ItemDetails> known = _itemDetails;
        Dictionary<uint, ItemDetails>? added = null;
        int reads = 0;
        try
        {
            foreach (ItemRow row in rows)
            {
                if (reads >= DetailsReadsPerPass)
                {
                    break;
                }
                if (row.Item == 0 || known.ContainsKey(row.Item) || (added != null && added.ContainsKey(row.Item)))
                {
                    continue;
                }
                reads++;
                IReadOnlyList<TooltipValue> tooltipRows = Content.TooltipValues(new ItemId(row.Item));
                uint description = 0;
                if (Content.ItemData.Get(row.Item)?.GetJson("description") is JsonNode node && node.GetValueKind() == JsonValueKind.String
                    && Guid.TryParse(node.GetValue<string>(), out Guid tag) && tag != Guid.Empty)
                {
                    description = Texts.TagOf(tag);
                }
                added ??= new Dictionary<uint, ItemDetails>(known);
                added[row.Item] = new ItemDetails(tooltipRows, description);
                foreach (TooltipValue value in tooltipRows)
                {
                    if (value.LocaId != 0)
                    {
                        _textsWanted.Enqueue(value.LocaId);
                    }
                }
                if (description != 0)
                {
                    _textsWanted.Enqueue(description);
                }
            }
        }
        catch (Exception problem) when (problem is InvalidOperationException or KilnUnavailableException or ArgumentException or KeyNotFoundException)
        {
            _detailsBroken = true;
            Problem($"item details can't be read here ({problem.GetType().Name}: {problem.Message})");
        }
        if (added != null)
        {
            _itemDetails = added;
        }
        if (!_textsWanted.IsEmpty && Interlocked.Exchange(ref _textsPosted, 1) == 0)
        {
            Client.Post(LookUpTexts);
        }
    }

    // The game client's main thread: the game's texts of the labels and descriptions asked for, in the language it shows now.
    private void LookUpTexts()
    {
        Interlocked.Exchange(ref _textsPosted, 0);
        try
        {
            if (!Content.TextAvailable)
            {
                return;
            }
            string language = Content.TextLanguage ?? "";
            Dictionary<uint, string> texts = _texts;
            if (language != _textsLanguage)
            {
                // Another language: every text is looked up again, the details' labels included.
                texts = new Dictionary<uint, string>();
                _textsLanguage = language;
                foreach (ItemDetails details in _itemDetails.Values)
                {
                    foreach (TooltipValue value in details.Rows)
                    {
                        _textsWanted.Enqueue(value.LocaId);
                    }
                    _textsWanted.Enqueue(details.DescriptionLoca);
                }
            }
            Dictionary<uint, string>? updated = null;
            int lookups = 0;
            while (lookups < TextLookupsPerPost && _textsWanted.TryDequeue(out uint loca))
            {
                if (loca == 0 || texts.ContainsKey(loca) || (updated != null && updated.ContainsKey(loca)))
                {
                    continue;
                }
                lookups++;
                updated ??= new Dictionary<uint, string>(texts);
                updated[loca] = ChestRules.PlainText(Content.Text(loca));
            }
            _texts = updated ?? texts;
            if (!_textsWanted.IsEmpty && Interlocked.Exchange(ref _textsPosted, 1) == 0)
            {
                Client.Post(LookUpTexts);
            }
        }
        catch (Exception problem) when (problem is InvalidOperationException or KilnUnavailableException)
        {
            Problem($"the game's texts can't be read here ({problem.GetType().Name}: {problem.Message})");
        }
    }

    // Kiln's render thread: the details of an item as tooltip lines, or "" when there are none (yet).
    private string DetailsOf(uint item)
    {
        if (!_itemDetails.TryGetValue(item, out ItemDetails? details))
        {
            return "";
        }
        Dictionary<uint, string> texts = _texts;
        var lines = new List<string>();
        foreach (TooltipValue value in details.Rows)
        {
            string? label = texts.TryGetValue(value.LocaId, out string? text) && text.Length > 0 ? text : null;
            string number = value.Format == TooltipValueFormat.Duration ? ChestRules.FormatDuration(value.Value) : value.Display;
            string line = ChestRules.DetailLine(label, number);
            if (line.Length > 0)
            {
                lines.Add(line);
            }
        }
        if (texts.TryGetValue(details.DescriptionLoca, out string? description) && description.Length > 0)
        {
            if (lines.Count > 0)
            {
                lines.Add("");
            }
            lines.Add(description);
        }
        return string.Join("\n", lines);
    }

    // A tooltip with the item's details under it, built only while the widget it belongs to is hovered.
    private void ItemTooltip(UiDraw ui, string text, uint item, int level = 0, int rarity = -1, bool isGear = false)
    {
        if (!ui.ItemHovered)
        {
            return;
        }
        // The game's own item detail (damage, perks, gems), as the game drew it when this item was last looked at in its
        // screens this session (Kiln 0.2.0-dev.30.5); else the tooltip rows read from the game's data.
        string details = GameDetailOf(item, level, rarity, isGear) ?? DetailsOf(item);
        string shown = details.Length == 0 ? text : text.Length == 0 ? details : text + "\n\n" + details;
        if (shown.Length > 0)
        {
            ui.Tooltip(shown);
        }
    }

    // Kiln captures the game's item detail panels from its first read on: read once early, so the items looked at in the game's
    // screens before the F2 screen's first tooltip are kept too.
    private bool _itemDetailStarted;

    private void StartItemDetailCapture()
    {
        if (_itemDetailStarted)
        {
            return;
        }
        _itemDetailStarted = true;
        try
        {
            Ui.Values.ItemDetail(new ItemId(ChestRules.ToolItemOf(ChestRules.ConstructionHammerKey)));
        }
        catch (Exception problem)
        {
            Log.Info($"item detail: not available here ({problem.GetType().Name}: {problem.Message})");
        }
    }

    private static string? GameDetailOf(uint item, int level, int rarity, bool isGear)
    {
        try
        {
            UiItemDetailResult result = Ui.Values.ItemDetail(ShownItem(item), isGear ? level : null, isGear && rarity >= 0 ? (ItemRarity)rarity : null);
            if (!result.Ok || result.Snapshot is not { } snapshot || snapshot.Lines.Count == 0)
            {
                return null;
            }
            var lines = new List<string>(snapshot.Lines.Count);
            foreach (UiItemDetailLine line in snapshot.Lines)
            {
                string text = line.Text.Length > 0 ? line.Text : line.Value is { } value ? value.ToString("0.#", CultureInfo.InvariantCulture) : "";
                if (text.Length > 0)
                {
                    lines.Add(ChestRules.PlainText(text));
                }
            }
            return lines.Count > 0 ? string.Join("\n", lines) : null;
        }
        catch (Exception)
        {
            return null;  // an older Kiln, or the capability is off
        }
    }

    // ---- The Settings tab: the same settings as Kiln's menu page, changed here. ----
    //
    // Kiln writes a setting on the tick thread (ModConfig.TrySet), and a slider changes its value every frame while it is
    // dragged: a change waits here until it has stayed the same for a moment, then the hosted world's tick writes it.

    private void DrawSettings(UiDraw ui)
    {
        ChestSettings? settings = _settings?.Value;
        if (settings == null)
        {
            ui.TextDisabled("The settings can't be read here.");
            return;
        }
        ui.TextWrapped("Changes apply at once and are saved. The same settings are in the Kiln menu's mod page.");
        ui.Separator("Linking");
        SettingToggle(ui, "linkRegularChests", "Link Regular Chests", settings.LinkRegularChests,
            "Off: only Magic Chests are offered for linking. On: regular chests too.");
        SettingSlider(ui, "linkRadiusMeters", "Link Radius (m)", settings.LinkRadiusMeters, ChestRules.MinLinkRadiusMeters, ChestRules.MaxLinkRadiusMeters,
            "How far from your character Link Nearby Chests reaches.");
        SettingToggle(ui, "showLinkedGemsInGemForge", "Show Linked Gems in Gem Forge", settings.ShowLinkedGemsInGemForge,
            "Use loose gems from linked chests at the Gem Forge. Your backpack pays the cost and receives recycled essence.");
        SettingToggle(ui, "craftFromLinkedChests", "Craft From Linked Chests", settings.CraftFromLinkedChests,
            "Crafting at a station, an NPC or from your inventory uses what your linked chests hold, in any base,\nafter your backpack and the base's own chests. The tinting screen offers their dyes too. Skill resets and unlearning skills can use linked runes.");
        SettingToggle(ui, "fillStationsFromLinkedChests", "Fill Stations From Linked Chests", settings.FillStationsFromLinkedChests,
            "A production station's +1 and +10 also take from your linked chests, in any base,\nwhat your backpack and the base's own chests can't cover. Needs Craft From Linked Chests.");
        SettingToggle(ui, "buildFromLinkedChests", "Build From Linked Chests", settings.BuildFromLinkedChests,
            "The Construction Hammer and the build menu count and use the blocks and materials your linked chests hold,\nin any base, after your backpack and the base's own chests. Needs Craft From Linked Chests.");
        SettingToggle(ui, "locksFromLinkedChests", "Lockpicks From Linked Chests", settings.LocksFromLinkedChests,
            "A lock that needs a lockpick or a key opens with one held in your linked chests,\nand takes it from there when your backpack has none. Needs Craft From Linked Chests.");
        ui.Separator("Favorites");
        SettingToggle(ui, "favoritesStayInBackpack", "Favorites Stay In Backpack", settings.FavoritesStayInBackpack,
            "The game's own Deposit Stacks, Deposit All and Quick Stack Station leave your favorite items in your backpack.");
        SettingToggle(ui, "searchAllTabs", "Search Every Tab", settings.SearchAllTabs,
            "On: while a search is typed, the list shows what it finds in every category, whatever tab is open. Off: only in the open tab.");
        SettingToggle(ui, "allFavoritesInTabs", "All Favorites In Every Tab", settings.AllFavoritesInTabs,
            "Off: a category tab shows its own favorites at the top. On: it shows all of them.");
        SettingToggle(ui, "favoritesInGameScreens", "Favorites In The Game's Screens", settings.FavoritesInGameScreens,
            "A favorite's slot is gold in the game's own inventory and chest screens. (Shift + RMB or L3 on a slot there always marks or unmarks it.)");
        ui.Separator("Quick Deposit");
        SettingToggle(ui, "quickDeposit", "Quick Deposit", settings.QuickDeposit,
            "Inside a base, Sort Inventory pressed twice (G) or a Quick Stack Station puts your backpack's items into the base's chests.");
        SettingToggle(ui, "quickDepositOutsideBase", "Quick Deposit Outside A Base", settings.QuickDepositOutsideBase,
            "Outside a base, Sort Inventory pressed twice puts your items into your linked chests instead, by the same rules.");
        SettingToggle(ui, "depositReportInChat", "Deposit Report In Chat", settings.DepositReportInChat,
            "After a quick deposit, your chat says which chest got what and what stayed.");
        SettingToggle(ui, "autoQuickDeposit", "Auto Quick Deposit", settings.AutoQuickDeposit,
            "When looting leaves fewer free backpack slots than Auto Deposit Free Slots, a quick deposit runs by itself (taking from your chests and crafting never start it).\nOutside a base it needs Quick Deposit Outside A Base.");
        SettingSlider(ui, "autoDepositFreeSlots", "Auto Deposit Free Slots", settings.AutoDepositFreeSlots, ChestRules.MinAutoDepositFreeSlots, ChestRules.MaxAutoDepositFreeSlots,
            "Auto Quick Deposit runs when fewer backpack slots than this are free, so several items arriving at once don't spill into the hotbar.\n1: only when the backpack is full.");
        SettingChoice(ui, "autoNameChests", "Auto-Name Chests", settings.AutoNameChests, ChestRules.AutoNameChoices,
            "When Quick Deposit or Re-Sort finds no chest named for an item with room, the closest chest that still has its default name is named for it.\nChildren: its sub-category (Weapons). Parent: its group (Gear).");
        DrawRules(ui, salvage: false);
        ui.Separator("Salvage");
        SettingToggle(ui, "autoSalvage", "Auto Salvage", settings.AutoSalvage,
            "Gear you loot that a salvage rule takes is salvaged into runes with the game's own Trash yield, about 1.5 s after it lands in your backpack (so loot-level mods can set its level first).\nFavorites, worn gear, gear with a gem and gear in the hotbars are never salvaged.");
        DrawRules(ui, salvage: true);
        ui.Separator("Highlight");
        SettingToggle(ui, "highlightDepositedChests", "Highlight Chests After A Deposit", settings.HighlightDepositedChests,
            "The chests a quick deposit put items into are lit while your inventory is open, then for the Highlight Time.");
        SettingSlider(ui, "highlightSeconds", "Highlight Time (s)", settings.HighlightSeconds, ChestRules.MinHighlightSeconds, ChestRules.MaxHighlightSeconds,
            "How long a chest clicked in the Chests list, or the chests of a quick deposit, stay marked.");
    }

    private void SettingToggle(UiDraw ui, string key, string label, bool current, string help)
    {
        bool value = _settingsPending.TryGetValue(key, out PendingSetting? pending) && pending.Value is bool shown ? shown : current;
        if (ui.Checkbox(label, ref value))
        {
            _settingsPending[key] = new PendingSetting(value, 0);
        }
        ui.Tooltip(help);
    }

    private void SettingChoice(UiDraw ui, string key, string label, int current, IReadOnlyList<string> choices, string help)
    {
        int value = _settingsPending.TryGetValue(key, out PendingSetting? pending) && pending.Value is int shown ? shown : current;
        value = Math.Clamp(value, 0, choices.Count - 1);
        ui.NextItemWidth(RarityComboWidth * ui.Scale);
        if (ui.Combo(label, ref value, choices))
        {
            _settingsPending[key] = new PendingSetting(value, 0);
        }
        ui.Tooltip(help);
    }

    // Kiln's render thread: the deposit or salvage rules, one row each ([Kind] [Rarity] [Level] Remove) and Add Rule. An edit is
    // kept here and saved on the world's thread once it has stayed the same for a moment (the level slider changes every frame).
    private void DrawRules(UiDraw ui, bool salvage)
    {
        RulesView view = _rulesView;
        List<GearRule> rules = (salvage ? _salvageRulesEdit : _depositRulesEdit) ?? new List<GearRule>(salvage ? view.Salvage : view.Deposit);
        ui.TextDisabled(salvage ? "Salvage Rules (Rarity Or Worse, Up To Level; 0 = Any)" : "Deposit Rules (Rarity Or Better, At Least Level)");
        ui.Tooltip(salvage
            ? "Gear of a rule's kind, of its rarity or worse and of its level or less, is salvaged when Auto Salvage is on."
            : "Quick Deposit puts gear into chests only when a rule takes it: its kind, its rarity or better, its level or more. Other items always go.");
        bool changed = false;
        int remove = -1;
        for (int index = 0; index < rules.Count; index++)
        {
            GearRule rule = rules[index];
            using (ui.Id((salvage ? "salvage-rule:" : "deposit-rule:") + index.ToString(CultureInfo.InvariantCulture)))
            {
                int kind = Math.Max(0, Array.IndexOf((GearKind[])ChestRules.RuleKinds, rule.Kind));
                int rarity = rule.Rarity;
                int level = rule.Level;
                ui.NextItemWidth(RarityComboWidth * ui.Scale);
                changed |= ui.Combo("##kind", ref kind, ChestRules.GearKindNames);
                ui.SameLine();
                ui.NextItemWidth(RarityComboWidth * ui.Scale);
                changed |= ui.Combo("##rarity", ref rarity, ChestRules.RarityNames);
                ui.SameLine();
                ui.NextItemWidth(RarityComboWidth * ui.Scale);
                changed |= ui.Slider("##level", ref level, 0, ChestRules.MaxRuleLevel);
                ui.Tooltip(salvage ? "Up to this level (0: any level)." : "From this level up (0: any level).");
                ui.SameLine();
                if (ui.SmallButton("Remove"))
                {
                    remove = index;
                }
                rules[index] = new GearRule(ChestRules.RuleKinds[Math.Clamp(kind, 0, ChestRules.RuleKinds.Count - 1)], Math.Clamp(rarity, 0, ChestRules.RarityNames.Count - 1), Math.Clamp(level, 0, ChestRules.MaxRuleLevel));
            }
        }
        if (remove >= 0)
        {
            rules.RemoveAt(remove);
            changed = true;
        }
        if (rules.Count < ChestRules.MaxRules && ui.SmallButton(salvage ? "Add Salvage Rule" : "Add Deposit Rule"))
        {
            rules.Add(salvage ? new GearRule(GearKind.Any, 0, 0) : new GearRule(GearKind.Any, 4, 0));
            changed = true;
        }
        if (changed)
        {
            if (salvage)
            {
                _salvageRulesEdit = rules;
            }
            else
            {
                _depositRulesEdit = rules;
            }
            _rulesSaveAtTick = Environment.TickCount64 + SettingSettleMilliseconds;
        }
        if ((_depositRulesEdit != null || _salvageRulesEdit != null) && Environment.TickCount64 >= _rulesSaveAtTick)
        {
            var deposit = new List<GearRule>(_depositRulesEdit ?? view.Deposit);
            var salvageRules = new List<GearRule>(_salvageRulesEdit ?? view.Salvage);
            _rulesView = new RulesView(deposit, salvageRules);
            _depositRulesEdit = null;
            _salvageRulesEdit = null;
            Post(() => SaveRules(deposit, salvageRules));
        }
    }

    // The world's thread: the rules edited in the Settings tab.
    private Task SaveRules(List<GearRule> deposit, List<GearRule> salvage)
    {
        _state.DepositRules.Clear();
        _state.DepositRules.AddRange(deposit);
        _state.SalvageRules.Clear();
        _state.SalvageRules.AddRange(salvage);
        _rulesView = new RulesView(deposit, salvage);
        SaveState();
        Log.Info($"rules: {deposit.Count} deposit rule(s), {salvage.Count} salvage rule(s)");
        return Task.CompletedTask;
    }

    private void SettingSlider(UiDraw ui, string key, string label, int current, double min, double max, string help)
    {
        int value = _settingsPending.TryGetValue(key, out PendingSetting? pending) && pending.Value is int shown ? shown : current;
        if (ui.Slider(label, ref value, (int)min, (int)max))
        {
            _settingsPending[key] = new PendingSetting(value, Environment.TickCount64 + SettingSettleMilliseconds);
        }
        ui.Tooltip(help);
    }

    // The hosted world's thread, every step: the settings changed in the tab and left alone for a moment are written.
    private void ApplyPendingSettings()
    {
        if (_settingsPending.IsEmpty || _settings == null)
        {
            return;
        }
        long now = Environment.TickCount64;
        foreach (KeyValuePair<string, PendingSetting> entry in _settingsPending)
        {
            if (entry.Value.ApplyAtTick > now)
            {
                continue;
            }
            if (!_settings.TrySet(entry.Key, entry.Value.Value, out string error))
            {
                Problem($"the setting {entry.Key} was not changed: {error}");
                _status = $"That setting could not be changed: {error}";
            }
            // Only the value written goes; a newer one set meanwhile stays to be written.
            ((ICollection<KeyValuePair<string, PendingSetting>>)_settingsPending).Remove(entry);
        }
    }

    // ---- Favorites in the game's own inventory and chest screens: a gold border on their slots, and Shift + RMB to mark one. ----
    //
    // Kiln's own marks and clicks (Ui.Inventory.MarkWhen and OnClick), which land on the right slot at every resolution since Kiln
    // 0.2.0-dev.30.3. 0.5.3 to 0.9.1 placed the borders and tested the pointer themselves, while Kiln put them at twice their place
    // on a 4K screen.

    // The gold marks follow Favorites In The Game's Screens; marking with Shift + RMB or L3 always works (since 0.10.27, the
    // user: "R3 and Shift + RMB should work in inventory even if the Favorites in inventory settings is turned off").
    private void SyncGameScreenFavorites()
    {
        if (_favoriteClicks == null && !_gameScreenClicksBroken)
        {
            try
            {
                RefreshMarkedFavorites();
                _favoriteClicks = Ui.Inventory.OnClick(new SlotClickOptions { Button = SlotButton.Right, Modifiers = SlotModifiers.Shift }, OnFavoriteSlotClicked);
                Log.Info("favorites: Shift + RMB on a slot of the game's own screens marks or unmarks its item");
                try
                {
                    // A controller: L3 on the selected slot (Kiln 0.2.0-dev.30.5). R3 did it too from 0.10.27 to 0.10.39 and alone in
                    // 0.10.40; since 0.10.41 only L3 (the user: "the game already uses R3 to open a tutorial overlay in the inventory").
                    _favoritePadClicks = Ui.Inventory.OnClick(new SlotClickOptions { Pad = KeyChord.Parse("Pad:LS") }, OnFavoriteSlotClicked);
                    Log.Info("favorites: L3 on a controller marks or unmarks the selected slot's item");
                }
                catch (Exception problem)
                {
                    Log.Info($"favorites: no controller press here ({problem.GetType().Name}: {problem.Message})");
                }
            }
            catch (Exception problem)
            {
                _gameScreenClicksBroken = true;
                Problem($"favorites can't be marked in the game's own screens here ({problem.GetType().Name}: {problem.Message})");
            }
        }
        bool wanted = (_settings?.Value.FavoritesInGameScreens ?? true) && !_gameScreenFavoritesBroken;
        if (wanted && _favoriteMarks == null && _favoriteTints == null)
        {
            RefreshMarkedFavorites();
            try
            {
                // The game's own slot background (Kiln 0.2.0-dev.31); Kiln's drawn borders only where that is not available.
                _favoriteTints = Ui.Inventory.TintWhen(TintFavoriteSlot);
                Log.Info("favorites have a gold background in the game's own inventory and chest screens");
            }
            catch (Exception problem)
            {
                _favoriteTints?.Dispose();
                _favoriteTints = null;
                Log.Info($"favorites: the game's slot colours can't be changed here ({problem.GetType().Name}: {problem.Message}): gold borders instead");
                try
                {
                    _favoriteMarks = Ui.Inventory.MarkWhen(MarkFavoriteSlot);
                    Log.Info("favorites have a gold border in the game's own inventory and chest screens");
                }
                catch (Exception markProblem)
                {
                    _gameScreenFavoritesBroken = true;
                    _favoriteMarks?.Dispose();
                    _favoriteMarks = null;
                    Problem($"favorites can't be shown in the game's own screens here ({markProblem.GetType().Name}: {markProblem.Message})");
                }
            }
        }
        else if (!wanted)
        {
            _favoriteTints?.Dispose();
            _favoriteTints = null;
            _favoriteMarks?.Dispose();
            _favoriteMarks = null;
        }
    }

    private void DropGameScreenFavorites()
    {
        _favoriteTints?.Dispose();
        _favoriteTints = null;
        _favoriteMarks?.Dispose();
        _favoriteMarks = null;
        _favoriteClicks?.Dispose();
        _favoriteClicks = null;
        _favoritePadClicks?.Dispose();
        _favoritePadClicks = null;
    }

    // ---- Link Chest / Unlink Chest in a chest's own screen (since 0.10.35, Kiln 0.2.0-dev.31's Ui.Inventory.AddChestButton). ----
    //
    // The world's thread, every step: the caption follows the open chest's link. The game draws the button every frame, so a new
    // caption shows at once. Kiln has four such buttons for all mods together.
    private const string LinkChestCaption = "Link Chest";
    private const string UnlinkChestCaption = "Unlink Chest";

    private void SyncChestButton()
    {
        if (_chestButtonBroken)
        {
            return;
        }
        string? caption = null;
        if (Ui.Inventory.Screen is { Kind: InventoryScreenKind.Chest, Hud: HudScreen.StorageChest } screen && !screen.Container.IsNone)
        {
            caption = ChestButtonCaption(screen.Container);
        }
        if (caption == _chestButtonCaption)
        {
            return;
        }
        DropChestButton();
        if (caption == null)
        {
            return;
        }
        bool linking = caption == LinkChestCaption;
        try
        {
            _chestButton = Ui.Inventory.AddChestButton(caption, pressed => OnChestButton(pressed, linking));
            _chestButtonCaption = caption;
        }
        catch (Exception problem)
        {
            _chestButtonBroken = true;
            _chestButton?.Dispose();
            _chestButton = null;
            Log.Info($"no Link Chest button in the chests' own screens here ({problem.GetType().Name}: {problem.Message})");
        }
    }

    // Unlink Chest for a linked chest; Link Chest for a chest the Chests tab offers (Magic, or any with Link Regular Chests on);
    // null (no button) for any other container.
    private string? ChestButtonCaption(EntityId chest)
    {
        LoadStateOnce();
        if (!World.TryGetPosition(chest, out WorldPosition position))
        {
            return null;
        }
        if (ChestRules.FindLink(_state.Links, ToChestPosition(position)) >= 0)
        {
            return UnlinkChestCaption;
        }
        ChestKind kind = KindOf(chest);
        bool offered = kind == ChestKind.Magic || (kind == ChestKind.Regular && (_settings?.Value.LinkRegularChests ?? false));
        return offered ? LinkChestCaption : null;
    }

    // The game's UI thread: the press goes to the world's thread with the chest the screen shows now.
    private void OnChestButton(InventoryScreen screen, bool linking)
    {
        if (screen.Container.IsNone || !Client.IsHosting)
        {
            return;
        }
        EntityId chest = screen.Container;
        Post(() => SetOpenChestLinked(chest, linking));
    }

    private Task SetOpenChestLinked(EntityId chest, bool linking)
    {
        LoadStateOnce();
        if (!World.TryGetPosition(chest, out WorldPosition position))
        {
            return Task.CompletedTask;
        }
        ChestPosition chestPosition = ToChestPosition(position);
        int linkIndex = ChestRules.FindLink(_state.Links, chestPosition);
        ChestKind kind = KindOf(chest);
        string label = ContainerNameOrNull(chest) ?? (kind == ChestKind.Magic ? "Magic Chest" : "Chest");
        if (linking && linkIndex < 0 && kind is ChestKind.Magic or ChestKind.Regular)
        {
            _state.Links.Add(chestPosition);
            SaveState();
            Hud.Toast("Linked: " + label, ToastKind.Info, 2);
            Log.Info($"chest button: linked {label} ({chest.Value}); {_state.Links.Count} link(s)");
        }
        else if (!linking && linkIndex >= 0)
        {
            _state.Links.RemoveAt(linkIndex);
            WithdrawForgeGems(chest);
            SaveState();
            Hud.Toast("Unlinked: " + label, ToastKind.Info, 2);
            Log.Info($"chest button: unlinked {label} ({chest.Value}); {_state.Links.Count} link(s)");
        }
        return Task.CompletedTask;
    }

    private void DropChestButton()
    {
        _chestButton?.Dispose();
        _chestButton = null;
        _chestButtonCaption = null;
    }

    // Kiln's render thread, once per slot the game draws: a lookup, nothing more. No mark while the game is not in play (a
    // loading screen still draws the hotbar's slots under it); the game's screen is asked at most ten times a second. Used only
    // when the game's own slot colours can't be changed (TintFavoriteSlot below).
    private SlotMark? MarkFavoriteSlot(VisibleSlot slot)
    {
        if (!IsFavoriteSlot(slot.Item.Value, slot.Rarity is { } rarity ? (int)rarity : -1, slot.ServerEntity.Value, slot.ClientEntity.Value, slot.HolderFlags.ToString()))
        {
            return null;
        }
        long now = Environment.TickCount64;
        if (now >= _marksScreenCheckedUntil)
        {
            GameScreen screen = Ui.Screens.Current;
            _marksInPlay = screen.State == GameUiState.Play && screen.Kind is not (ScreenKind.Loading or ScreenKind.Cinematic or ScreenKind.None);
            _marksScreenCheckedUntil = now + 100;
        }
        return _marksInPlay ? FavoriteSlotMark : null;
    }

    // The game's UI thread, once per slot it draws: a favorite's slot background turns gold (Kiln 0.2.0-dev.31). The game draws
    // it as its own background, so its item menu (RMB on an item) stays on top, which the borders were not (kiln#117).
    private Color? TintFavoriteSlot(UiItemView slot)
    {
        return IsFavoriteSlot(slot.Item.Value, (int)slot.Rarity, slot.ServerEntity.Value, slot.ClientEntity.Value, null) ? FavoriteTint : null;
    }

    // Whether a slot of the game's screens holds a favorite. rarity: the slot's item's rarity, -1 when the slot view has none.
    // holderFlags: for the log line of the gear check, null when the caller has none.
    private bool IsFavoriteSlot(uint item, int rarity, uint serverEntity, uint clientEntity, string? holderFlags)
    {
        if (item == 0)
        {
            return false;
        }
        // The Construction Hammer's and the summoning staff's slots hold their selection: the tool's favorite stands for them.
        uint favoriteItem = _toolEntries.TryGetValue(item, out uint toolKey) ? toolKey : item;
        if (_markedFavorites.Contains(favoriteItem))
        {
            return true;
        }
        // Gear with a favorite rarity: only the slots whose item is one of the player's favorite pieces.
        if (!_markedGearItems.Contains(item))
        {
            return false;
        }
        HashSet<uint> entities = _markedGearEntities;
        // Since Kiln 0.2.0-dev.30.5 a slot says its item's rarity: any slot of favorite gear is marked, chests' included. Older
        // Kilns: the player's own pieces, by their entities.
        bool matched = rarity >= 0
            ? _markedGearKeys.Contains(FavoriteSet.GearKey(item, rarity))
            : (serverEntity != 0 && entities.Contains(serverEntity)) || (clientEntity != 0 && entities.Contains(clientEntity));
        // Logged once the favorite pieces are known (0.10.6 logged its six before they were, which told nothing).
        if (entities.Count > 0 && _gearMarkChecksLogged < 6 && Interlocked.Increment(ref _gearMarkChecksLogged) <= 6)
        {
            Log.Info($"favorite gear mark check: item 0x{item:x8}, rarity {rarity}, server entity {serverEntity}, client entity {clientEntity}{(holderFlags != null ? ", holder flags " + holderFlags : "")}: {(matched ? "a favorite piece" : "no match")} among {entities.Count} favorite piece(s) carried");
        }
        return matched;
    }

    // The game's main thread: Shift + RMB on a slot of the game's own screens. Kiln keeps the press from the game.
    private void OnFavoriteSlotClicked(SlotClick click)
    {
        uint item = click.Item.Value;
        uint serverEntity = click.Slot.ServerEntity.Value;
        uint clientEntity = click.Slot.ClientEntity.Value;
        int slotRarity = click.Slot.Rarity is { } rarity ? (int)rarity : -1;
        if (item != 0 && Client.IsHosting)
        {
            Post(() => ToggleFavorite(item, serverEntity, clientEntity, slotRarity));
        }
    }

    // The game's screens that show item slots under a pointer.
    private static bool ShowsItemSlots(HudScreen screen)
    {
        return screen is HudScreen.InGameMenu or HudScreen.PlayerCrafting or HudScreen.Backpack or HudScreen.StorageChest
            or HudScreen.StationBasedCrafting or HudScreen.NpcCrafting or HudScreen.FactoryCrafting or HudScreen.CommerceProductChest;
    }

    // Shift + RMB in the game's screens: the clicked item is found by its entity in the player's inventory, then in the containers
    // around the player (an open chest), for its rarity. Gear found nowhere is left alone, since its rarity is unknown.
    private Task ToggleFavorite(uint item, uint serverEntity, uint clientEntity, int slotRarity = -1)
    {
        uint toolKey = ChestRules.ToolKeyOf(Items.Get(new ItemId(item))?.Name);
        if (toolKey != 0)
        {
            // The hammer or the staff itself, whatever it has selected.
            bool toolFavorite = !_state.Favorites.Contains(toolKey);
            uint toolItem = ChestRules.ToolItemOf(toolKey);
            Hud.Toast((toolFavorite ? "Favorite: " : "Not A Favorite: ") + (toolItem != 0 ? NameOfItem(toolItem) : ChestRules.ToolName(toolKey)), ToastKind.Info, 2);
            return SetFavorite(toolKey, -1, false, toolFavorite);
        }
        InventorySlot? clicked = FindItemByEntity(serverEntity, clientEntity);
        string name = NameOfItem(item);
        if (clicked == null && IsGearSection(item) && slotRarity >= 0)
        {
            // Gear the mod can't find by its entity (a chest further away): the slot's own rarity says which (Kiln 0.2.0-dev.30.5).
            bool gearFavorite = !_state.FavoriteSet().Contains(item, slotRarity, true);
            string slotRarityText = slotRarity < ChestRules.RarityNames.Count ? " (" + ChestRules.RarityNames[slotRarity] + ")" : "";
            Hud.Toast((gearFavorite ? "Favorite: " : "Not A Favorite: ") + name + slotRarityText, ToastKind.Info, 2);
            return SetFavorite(item, slotRarity, true, gearFavorite);
        }
        if (clicked == null && IsGearSection(item))
        {
            Log.Info($"favorite click: item 0x{item:x8} (server entity {serverEntity}, client entity {clientEntity}) not found in the player's inventory or the chests around");
            Hud.Toast($"{name}: its rarity can't be read here. Mark it in the Streamlined Logistics screen.", ToastKind.Info, 3);
            return Task.CompletedTask;
        }
        bool isGear = clicked?.IsGear ?? false;
        int rarity = clicked != null ? RarityOf(clicked) : -1;
        bool nowFavorite = !_state.FavoriteSet().Contains(item, rarity, isGear);
        string rarityText = isGear && rarity >= 0 && rarity < ChestRules.RarityNames.Count ? " (" + ChestRules.RarityNames[rarity] + ")" : "";
        Hud.Toast((nowFavorite ? "Favorite: " : "Not A Favorite: ") + name + rarityText, ToastKind.Info, 2);
        return SetFavorite(item, rarity, isGear, nowFavorite);
    }

    private InventorySlot? FindItemByEntity(uint serverEntity, uint clientEntity)
    {
        if (serverEntity == 0 && clientEntity == 0)
        {
            return null;
        }
        EntityId player = Client.LocalPlayer;
        var holders = new List<EntityId> { player };
        if (World.TryGetPosition(player, out WorldPosition playerPosition))
        {
            foreach (EntityId container in Items.FindInventories(InventoryKinds.Containers))
            {
                if (World.TryGetPosition(container, out WorldPosition position) && position.DistanceTo(playerPosition) <= OpenChestReachMeters)
                {
                    holders.Add(container);
                }
            }
        }
        foreach (EntityId holder in holders)
        {
            InventoryContents? contents = Items.Read(holder);
            if (contents == null)
            {
                continue;
            }
            foreach (InventorySlot slot in contents.Slots)
            {
                if (!slot.IsEmpty && !slot.ItemEntity.IsNone && (slot.ItemEntity.Value == serverEntity || slot.ItemEntity.Value == clientEntity))
                {
                    return slot;
                }
            }
        }
        return null;
    }

    private bool IsGearSection(uint item)
    {
        int section = PlaceOf(item).Section;
        return _tree != null && section >= 0 && section < _tree.Sections.Count
            && _tree.Sections[section].Key is "Gear/Weapons" or "Gear/Shields" or "Gear/Armor" or "Gear/Rings";
    }

    // ---- A chest marked in the world: clicked in the Chests list, shown once the screen is closed. ----

    private Task StartHighlight(ChestView chest)
    {
        if (chest.DistanceMeters > ChestRules.MaxHighlightMeters)
        {
            _status = string.Create(CultureInfo.InvariantCulture, $"{chest.Label} is {chest.DistanceText} away: too far to mark ({ChestRules.MaxHighlightMeters:0} m at most).");
            return Task.CompletedTask;
        }
        if (!EnsureRenderFrames())
        {
            _status = "Kiln can't draw in the world here.";
            return Task.CompletedTask;
        }
        _highlightSeconds = Math.Clamp(_settings?.Value.HighlightSeconds ?? ChestRules.DefaultHighlightSeconds, ChestRules.MinHighlightSeconds, ChestRules.MaxHighlightSeconds);
        // The chest's position is its foot: the mark sits half a metre up, around its middle.
        _highlight = new Highlight(new WorldPosition(chest.Position.X, chest.Position.Y + 0.5, chest.Position.Z), chest.Label, 0, chest.Entity);
        _status = string.Create(CultureInfo.InvariantCulture, $"Close this screen to see {chest.Label} marked for {_highlightSeconds} s.");
        return Task.CompletedTask;
    }

    private void OnScreenClosed()
    {
        float width = _seenWindowWidth;
        float height = _seenWindowHeight;
        if (width > 0 && height > 0 && (Math.Abs(width - _keptWindowWidth) > 0.5f || Math.Abs(height - _keptWindowHeight) > 0.5f))
        {
            _keptWindowWidth = width;
            _keptWindowHeight = height;
            Context.Scheduler.Post(() =>
            {
                _state.WindowWidth = width;
                _state.WindowHeight = height;
                SaveState();
            });
        }
        Highlight? pending = _highlight;
        if (pending != null && pending.UntilTick == 0)
        {
            _highlight = pending with { UntilTick = Environment.TickCount64 + _highlightSeconds * 1000L };
            OutlineFor(pending.Entity, _highlightSeconds);
        }
    }

    // Kiln's render thread, every frame once a chest was marked.
    private void OnRenderFrame(RenderFrameEvent frame)
    {
        if (frame.Capturing || !frame.Camera.Valid)
        {
            return;
        }
        long now = Environment.TickCount64;
        // Only in the play view: what is drawn in the world shows over the game's menus (the map, the inventory).
        HudScreen hud = Ui.Screens.Current.Hud;
        bool inPlayView = hud == HudScreen.InGame || hud == HudScreen.Invalid;
        Highlight? highlight = _highlight;
        if (highlight != null && highlight.UntilTick != 0)
        {
            if (now > highlight.UntilTick)
            {
                _highlight = null;
            }
            else if (inPlayView)
            {
                if (!_throughWalls)
                {
                    frame.World.Box(highlight.Center, new Vector3(1.3f, 1.3f, 1.3f), FavoriteColor, 3);
                }
                frame.World.Marker(highlight.Center, FavoriteColor, 12);
                frame.World.Label(highlight.Center, string.Create(CultureInfo.InvariantCulture, $"{highlight.Label}  {frame.World.DistanceTo(highlight.Center):0} m"), FavoriteColor);
                if (!frame.World.ToScreenClamped(highlight.Center, 40, out Vector2 edge, out float angle))
                {
                    frame.Draw.Arrow(edge, angle, 16, FavoriteColor);
                }
            }
        }
        DepositHighlight? lit = _depositHighlight;
        if (lit != null && inPlayView && (lit.UntilTick == 0 || now <= lit.UntilTick))
        {
            foreach (LitChest chest in lit.Chests)
            {
                // A chest the character has walked away from is no longer marked.
                if (frame.World.DistanceTo(chest.Center) > ChestRules.MaxHighlightMeters)
                {
                    continue;
                }
                if (!(lit.Outline && _throughWalls))
                {
                    frame.World.Box(chest.Center, new Vector3(1.3f, 1.3f, 1.3f), FavoriteColor, 3);
                }
                frame.World.Label(chest.Center, chest.Label, FavoriteColor);
            }
        }
    }

    // The game's own rename of a chest, through Kiln (since Kiln 0.2.0-dev.30.3): the name is kept in the save and seen by everyone.
    private async Task RenameChest(uint entity, string name)
    {
        try
        {
            bool taken = await Items.SetContainerName(new EntityId(entity), name);
            _status = taken ? (name.Length == 0 ? "The chest's name is cleared." : $"The chest is named {name}.") : "The game did not take the name: see kiln.log.";
            Log.Info($"rename: chest {entity} to \"{name}\": {(taken ? "done" : "refused")}");
        }
        catch (Exception problem) when (problem is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            _status = $"The chest could not be renamed: {problem.Message}";
        }
    }

    private Task SetGridView(bool gridView)
    {
        _state.GridView = gridView;
        SaveState();
        return Task.CompletedTask;
    }

    private Task SetIncludeHotbar(bool includeHotbar)
    {
        _state.IncludeHotbar = includeHotbar;
        SaveState();
        return Task.CompletedTask;
    }

    private Task SetUngrouped(bool ungrouped)
    {
        _state.Ungrouped = ungrouped;
        SaveState();
        return Task.CompletedTask;
    }

    private Task SetCategoryHidden(string name, bool hidden)
    {
        _state.HiddenCategoriesChosen = true;
        if (hidden)
        {
            _state.HiddenCategories.Add(name);
        }
        else
        {
            _state.HiddenCategories.Remove(name);
        }
        SaveState();
        return Task.CompletedTask;
    }

    /// <summary>Hides every category, none, or (null) the ones hidden by default.</summary>
    private Task SetHiddenCategories(bool? all)
    {
        _state.HiddenCategoriesChosen = true;
        _state.HiddenCategories.Clear();
        if (_categories != null)
        {
            foreach (string name in _categories.Names)
            {
                _state.KnownCategories.Add(name);
            }
        }
        if (_categories != null && all != false)
        {
            foreach (string name in all == true ? _categories.Names : _categories.DefaultHidden())
            {
                _state.HiddenCategories.Add(name);
            }
        }
        SaveState();
        return Task.CompletedTask;
    }

    // ---- Quick Deposit: G twice with the inventory open inside a base, or a Quick Stack Station. ----
    // Each backpack stack first tops up the stacks of its item that the base's chests already hold, and what is left goes to a chest
    // named for the item's category, else to a chest that already holds the item. What no chest asks for stays in the backpack.
    // The hotbar, worn things, favorites and chests named with -- in front are never touched.

    private void SyncQuickDepositTriggers()
    {
        bool wanted = _settings?.Value.QuickDeposit ?? true;
        if (wanted)
        {
            _otherQuickDepositRuns ??= OtherQuickDepositRuns();
            wanted = _otherQuickDepositRuns != true;
        }
        if (wanted && _clientActions == null)
        {
            try
            {
                _clientActions = Events.On<PlayerClientActionEvent>(OnClientAction);
                Log.Info("quick deposit: watching for Sort Inventory pressed twice");
            }
            catch (Exception problem)
            {
                Problem($"quick deposit: the client's commands can't be watched here ({problem.GetType().Name}: {problem.Message}): G twice is off");
            }
        }
        else if (!wanted && _clientActions != null)
        {
            _clientActions.Dispose();
            _clientActions = null;
        }
        if (wanted && _interactions == null)
        {
            try
            {
                _interactions = Events.On<InteractionResultEvent>(OnInteractionResult);
                Log.Info("quick deposit: watching for Quick Stack Stations");
            }
            catch (Exception problem)
            {
                Problem($"quick deposit: interactions can't be watched here ({problem.GetType().Name}: {problem.Message}): stations keep the game's own deposit");
            }
        }
        else if (!wanted && _interactions != null)
        {
            _interactions.Dispose();
            _interactions = null;
            _stationDepositAtTick = 0;
        }
        bool autoWanted = (wanted && (_settings?.Value.AutoQuickDeposit ?? false)) || (_settings?.Value.AutoSalvage ?? false);
        if (autoWanted && _pickups == null)
        {
            try
            {
                _pickups = Events.On<ItemPickupEvent>(OnItemPickup);
                Log.Info("auto quick deposit: watching for loot filling the backpack");
            }
            catch (Exception problem)
            {
                Problem($"auto quick deposit: pickups can't be watched here ({problem.GetType().Name}: {problem.Message})");
            }
        }
        else if (!autoWanted && _pickups != null)
        {
            _pickups.Dispose();
            _pickups = null;
            _pickupCheckPending = false;
        }
    }

    // The world's thread: items arrived in an inventory (the game's "+N"). Only loot counts: items for the local player that
    // came from something (not crafting's output, which has no source) other than a storage chest (taking from one), and not
    // while the mod moves items itself. The backpack is checked once at the next step, however many items arrived.
    private void OnItemPickup(ItemPickupEvent pickup)
    {
        if (pickup.Receiver != Client.LocalPlayer)
        {
            return;
        }
        // Gear is rare among pickups: what happens to it is logged, for Auto Salvage's checks.
        bool gear = (_settings?.Value.AutoSalvage ?? false) && Items.Get(new ItemId(pickup.ItemId))?.IsGear == true;
        string ignored = _busy ? "the mod was moving items" : pickup.Source.IsNone ? "no source (crafted or given)" : IsPlayersStorage(pickup.Source) ? "from the player's own chest" : "";
        if (gear)
        {
            Log.Info($"auto salvage: picked up {NameOfItem(pickup.ItemId)} from {pickup.Source.Value} into {pickup.SlotInventory.Value}:{pickup.SlotIndex}{(ignored.Length > 0 ? ": not loot, " + ignored : "")}");
        }
        if (ignored.Length > 0)
        {
            return;
        }
        _pickupCheckPending = _settings?.Value.AutoQuickDeposit ?? false;
        if ((_settings?.Value.AutoSalvage ?? false) && !pickup.SlotInventory.IsNone && _salvageQueue.Count < 64)
        {
            _salvageQueue.Add(new SalvageCandidate(SlotId.Of(pickup.SlotInventory, pickup.SlotIndex).Value, pickup.ItemId, Environment.TickCount64));
        }
    }

    // Taking from one of the player's own chests is no loot: a storage chest in a base, or a linked one. A dungeon's or a ruin's
    // chest (no base) is loot (0.10.23 to 0.10.28 counted every storage-like chest as the player's, so loot from them was not
    // salvaged).
    private bool IsPlayersStorage(EntityId source)
    {
        if (KindOf(source) is not (ChestKind.Magic or ChestKind.Regular))
        {
            return false;
        }
        if (!World.TryGetPosition(source, out WorldPosition position))
        {
            return true;
        }
        return ChestRules.FindLink(_state.Links, ToChestPosition(position)) >= 0 || (Bases.Available && Bases.At(position) != null);
    }

    // A looted piece waiting for Auto Salvage: where it landed, when it was picked up, the steps it was not yet found, and the
    // level and rarity it last read (and since when), so it is salvaged only once they have settled.
    private sealed class SalvageCandidate(ulong slot, uint item, long pickedUpTick)
    {
        public ulong Slot { get; } = slot;
        public uint Item { get; } = item;
        public long PickedUpTick { get; } = pickedUpTick;
        public int Tries { get; set; }
        public int Level { get; set; } = -1;
        public int Rarity { get; set; } = -1;
        public long StableSinceTick { get; set; }
        public long NextCheckTick { get; set; }
    }

    // The world's thread, at each step while loot waits: the looted gear that is due is looked at (one task for all of it); the
    // backpack check of Auto Quick Deposit waits until the queue is empty.
    private void SalvageLootSoon()
    {
        if (_salvageQueue.Count == 0 || _busy)
        {
            return;
        }
        long now = Environment.TickCount64;
        var due = _salvageQueue.FindAll(candidate => candidate.NextCheckTick <= now);
        if (due.Count == 0)
        {
            return;
        }
        _salvageQueue.RemoveAll(candidate => candidate.NextCheckTick <= now);
        Post(() => SalvageLoot(due));
    }

    private async Task SalvageLoot(List<SalvageCandidate> looted)
    {
        EntityId player = Client.LocalPlayer;
        if (player.IsNone || !Items.Available)
        {
            return;
        }
        // The table is read by the tree: before the F2 screen or a deposit ever needed it, it isn't yet (0.10.26 to 0.10.30 then
        // salvaged nothing until the screen was opened).
        LoadCategoriesIfChanged();
        EnsureTree();
        _salvageBalancing ??= ChestRules.ParseSalvage(_treeText);
        if (_salvageBalancing is not { } balancing)
        {
            Log.Warn("auto salvage: the table has no salvage balancing: nothing is salvaged");
            return;
        }
        if (ReadPlayer(player) is not { } playerRead)
        {
            return;
        }
        FavoriteSet favorites = _state.FavoriteSet(_toolEntries);
        float perkCostFactor = GameSetting("perkCostFactor", 1f);
        float recyclingFactor = GameSetting("perkUpgradeRecyclingFactor", 0.5f);
        var salvaged = new List<string>();
        long runes = 0;
        foreach (SalvageCandidate candidateLoot in looted)
        {
            ulong slotValue = candidateLoot.Slot;
            uint item = candidateLoot.Item;
            int tries = candidateLoot.Tries;
            InventorySlot? slot = null;
            foreach (InventorySlot candidate in playerRead.Contents.Slots)
            {
                if (candidate.Slot.Value == slotValue)
                {
                    slot = candidate;
                    break;
                }
            }
            // Only gear still where it landed, in the backpack (not worn, not in the hotbars), no favorite, a rule's kind.
            if (slot == null || slot.IsEmpty || slot.Item.Value != item)
            {
                // Not there yet (a container's loot is reported before it lands, 0.10.29 gave up at once), or moved away.
                if (Items.Get(new ItemId(item))?.IsGear == true)
                {
                    if (tries + 1 < SalvageTries)
                    {
                        candidateLoot.Tries = tries + 1;
                        candidateLoot.NextCheckTick = 0;
                        _salvageQueue.Add(candidateLoot);
                    }
                    else
                    {
                        Log.Info($"auto salvage: {NameOfItem(item)} kept: never found in the slot it was reported in");
                    }
                }
                continue;
            }
            if (tries > 0 && candidateLoot.Level < 0)
            {
                Log.Info($"auto salvage: {NameOfItem(item)} found in its slot after {tries} more step(s)");
            }
            if (!slot.IsGear || GearKindOf(item) is not { } kind)
            {
                continue;
            }
            // Wait until the level and rarity have settled (see SalvageSettleMilliseconds).
            {
                long now = Environment.TickCount64;
                int readRarity = RarityOf(slot);
                if (slot.Level != candidateLoot.Level || readRarity != candidateLoot.Rarity)
                {
                    if (candidateLoot.Level >= 0)
                    {
                        Log.Info($"auto salvage: {NameOfItem(item)} now reads level {slot.Level}, rarity {readRarity} (was level {candidateLoot.Level}, rarity {candidateLoot.Rarity}): waiting until it settles");
                    }
                    candidateLoot.Level = slot.Level;
                    candidateLoot.Rarity = readRarity;
                    candidateLoot.StableSinceTick = now;
                }
                bool givenUp = now - candidateLoot.PickedUpTick >= SalvageGiveUpMilliseconds;
                if (!givenUp && (slot.Level <= 0 || now - candidateLoot.StableSinceTick < SalvageSettleMilliseconds))
                {
                    candidateLoot.NextCheckTick = now + SalvageRecheckMilliseconds;
                    _salvageQueue.Add(candidateLoot);
                    continue;
                }
                if (slot.Level <= 0)
                {
                    Log.Info($"auto salvage: {NameOfItem(item)} kept: its level still reads 0 after {SalvageGiveUpMilliseconds / 1000} s");
                    continue;
                }
            }
            string skipped = slot.IsEquipped || slot.ItemEntity.IsNone ? "worn"
                : !playerRead.Layout.IsBackpackPage(slot.Slot.Inventory.Value) ? "not in the backpack"
                : "";
            int rarity = RarityOf(slot);
            if (skipped.Length == 0 && favorites.Contains(item, rarity, true))
            {
                skipped = "a favorite";
            }
            if (skipped.Length == 0 && !ChestRules.SalvageWanted(kind, rarity, slot.Level, _state.SalvageRules))
            {
                continue;  // no rule takes it: nothing to say
            }
            if (skipped.Length == 0)
            {
                try
                {
                    if (Gems.Socket(slot.Ref) != null)
                    {
                        skipped = "a gem in it";
                    }
                }
                catch (Exception problem)
                {
                    skipped = "its socket can't be read (" + problem.Message + ")";
                }
            }
            ushort perks = 0;
            if (skipped.Length == 0 && !World.TryRead(slot.ItemEntity, "PerkContainerNew", out perks))
            {
                skipped = "its perk slots can't be read";
            }
            if (skipped.Length > 0)
            {
                Log.Info($"auto salvage: {NameOfItem(item)} kept: {skipped}");
                continue;
            }
            int count = ChestRules.SalvageRunes(slot.Level, perks & 0xFF, perks >> 8, kind, balancing, perkCostFactor, recyclingFactor);
            string name = NameOfItem(item);
            // The runes must fit before the piece goes; then the piece, then the runes (as Kiln Auto Loot does).
            if (count > 0 && (await Items.Give(player, new ItemId(balancing.RuneItem), count, null, GiveOptions.DryRun | GiveOptions.AllOrNothing, null)).Status != ItemsStatus.Done)
            {
                Log.Info($"auto salvage: {name}: no room for {count} rune(s)");
                continue;
            }
            if ((await Items.TakeSlot(player, slot, false)).Status != ItemsStatus.Done)
            {
                Log.Info($"auto salvage: {name}: the game kept it");
                continue;
            }
            if (count > 0 && (await Items.Give(player, new ItemId(balancing.RuneItem), count, null, GiveOptions.AllOrNothing | GiveOptions.Notify, null)).Status != ItemsStatus.Done)
            {
                Problem($"auto salvage: {name} was taken but its {count} rune(s) could not be given");
                continue;
            }
            Log.Info($"auto salvage: {name} (level {slot.Level}, {(rarity >= 0 && rarity < ChestRules.RarityNames.Count ? ChestRules.RarityNames[rarity] : "no rarity")}) for {count} rune(s)");
            salvaged.Add(name);
            runes += count;
        }
        if (salvaged.Count > 0)
        {
            Hud.Toast(string.Create(CultureInfo.InvariantCulture, $"Salvaged {string.Join(", ", salvaged)} for {runes:N0} rune(s)."), ToastKind.Info, 4);
        }
    }

    // A world setting of the game (Kiln.Settings), or its default when it can't be read.
    private static float GameSetting(string name, float fallback)
    {
        try
        {
            return Convert.ToSingle(Kiln.Settings.Get(name), CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    // Once after loot arrived: a quick deposit when fewer backpack slots than Auto Deposit Free Slots are free, at most every few
    // seconds, so a backpack that stays that full (nothing any chest takes) doesn't start one deposit after another.
    private void CheckBackpackAfterLoot()
    {
        _pickupCheckPending = false;
        long now = Environment.TickCount64;
        if (_busy || now < _autoDepositNotBeforeTick)
        {
            return;
        }
        int threshold = _settings?.Value.AutoDepositFreeSlots ?? ChestRules.DefaultAutoDepositFreeSlots;
        EntityId player = Client.LocalPlayer;
        if (player.IsNone || ReadPlayer(player) is not { } playerRead
            || !ChestRules.AutoDepositWanted(ChestRules.BackpackFreeSlots(playerRead.Layout, playerRead.Slots), threshold))
        {
            return;
        }
        _autoDepositNotBeforeTick = now + AutoDepositPauseMilliseconds;
        Log.Info($"auto quick deposit: loot left fewer than {threshold} free backpack slots");
        Post(() => QuickDeposit("loot filled the backpack", quiet: true));
    }

    /// <summary>True while GreeneyeDS's Quick Deposit is loaded and switched on: it then keeps G twice and the stations, and this mod leaves them alone.</summary>
    private bool OtherQuickDepositRuns()
    {
        try
        {
            bool loaded = false;
            foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
            {
                if (string.Equals(module.ModuleName, OtherQuickDepositModule, StringComparison.OrdinalIgnoreCase))
                {
                    loaded = true;
                    break;
                }
            }
            if (!loaded)
            {
                return false;
            }
            string iniPath = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory, OtherQuickDepositIni);
            if (File.Exists(iniPath))
            {
                foreach (string line in File.ReadAllLines(iniPath))
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("enabled", StringComparison.OrdinalIgnoreCase) && trimmed.Contains('='))
                    {
                        string value = trimmed[(trimmed.IndexOf('=') + 1)..].Trim();
                        if (value == "0")
                        {
                            Log.Info($"quick deposit: the Greeneye Quick Deposit is loaded but switched off in {iniPath}");
                            return false;
                        }
                        break;
                    }
                }
            }
            Log.Info($"quick deposit: the Greeneye Quick Deposit is running, so G twice and the Quick Stack Station are left to it. Set enabled=0 in {iniPath} (game closed) to have Streamlined Logistics do them.");
            return true;
        }
        catch (Exception problem)
        {
            Problem($"quick deposit: could not tell whether the Greeneye Quick Deposit runs ({problem.GetType().Name}: {problem.Message}); taken as not running");
            return false;
        }
    }

    // The world's thread, one step before the game runs the client's command.
    private void OnClientAction(PlayerClientActionEvent action)
    {
        if (!Client.IsHosting || !string.Equals(action.Name, SortActionName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        EntityId player = Client.LocalPlayer;
        if (player.IsNone || action.Player.Entity != player)
        {
            return;
        }
        uint target = action.Payload.Length >= SortTargetOffset + 4 ? BitConverter.ToUInt32(action.Payload, SortTargetOffset) : 0;
        if (_playerInventoryOwners.Count == 0)
        {
            ReadPlayer(player);
        }
        bool ownInventory = target == 0 || target == player.Value || _playerInventoryOwners.Contains(target);
        if (_sortActionsLogged < 5)
        {
            _sortActionsLogged++;
            Log.Info($"quick deposit: Sort Inventory on inventory {target} ({action.Payload.Length} bytes), {(ownInventory ? "the character's own" : "not the character's: ignored")}");
        }
        if (!ownInventory)
        {
            return;
        }
        long now = Environment.TickCount64;
        if (now - _lastSortAt <= DoubleSortMilliseconds)
        {
            _lastSortAt = 0;
            Post(() => QuickDeposit("Sort Inventory twice"));
        }
        else
        {
            _lastSortAt = now;
        }
    }

    // The world's thread, when the game answers an interaction: the local player using a Quick Stack Station is followed by a whole
    // quick deposit, once the station's own has run. Nothing is polled: 0.4.0 and 0.4.1 asked Kiln for the game's deposit events
    // after every step, which cost about 60 ms each time and slowed the whole world down.
    private void OnInteractionResult(InteractionResultEvent result)
    {
        if (!result.Accepted || !Client.IsHosting)
        {
            return;
        }
        EntityId player = Client.LocalPlayer;
        if (player.IsNone || result.Guest != player)
        {
            return;
        }
        EntityInfo? used;
        try
        {
            used = Spawn.Inspect(result.Host);
        }
        catch (Exception problem)
        {
            Problem($"quick deposit: what the player used could not be read ({problem.GetType().Name}: {problem.Message})");
            return;
        }
        if (used == null || !ChestRules.IsQuickStackStation(used.Template, used.TemplateName))
        {
            return;
        }
        if (_stationsLogged < 5)
        {
            _stationsLogged++;
            Log.Info($"quick deposit: Quick Stack Station {result.Host.Value} ({used.TemplateName ?? "?"}) used");
        }
        _stationDepositAtTick = Environment.TickCount64 + StationSettleMilliseconds;
    }

    // From the inventory (Sort Inventory twice) and a Quick Stack Station the items go to the chests of the base the player
    // stands in. From this mod's own screen they always go to the linked chests, in a base or not: the screen is about them.
    private async Task QuickDeposit(string trigger, bool alwaysIntoLinkedChests = false, bool quiet = false)
    {
        EntityId player = Client.LocalPlayer;
        if (!CanMove(player))
        {
            return;
        }
        LoadStateOnce();
        LoadCategoriesIfChanged();
        BaseInfo? home = !alwaysIntoLinkedChests && Bases.Available ? Bases.Of(player) : null;
        bool intoLinkedChests = home == null;
        if (intoLinkedChests && !alwaysIntoLinkedChests && !(_settings?.Value.QuickDepositOutsideBase ?? false))
        {
            if (quiet)
            {
                Log.Info($"quick deposit ({trigger}): outside a base, and Quick Deposit Outside A Base is off: nothing done");
                return;
            }
            Notify("Quick Deposit works inside a base. The setting Quick Deposit Outside A Base sends items to your linked chests from anywhere.", ToastKind.Info);
            return;
        }
        PlayerRead? playerRead = ReadPlayer(player);
        if (playerRead == null)
        {
            Notify("Quick Deposit could not read your inventory.", ToastKind.Warning);
            return;
        }
        List<DepositChestState> chests = intoLinkedChests ? ReadLinkedDepositChests(player) : ReadBaseChests(home!, player);
        string place = intoLinkedChests ? "your linked chests" : $"the base \"{home!.Name ?? "?"}\"";
        if (chests.Count == 0)
        {
            Notify(intoLinkedChests ? "Quick Deposit found none of your linked chests." : "Quick Deposit found no storage chest in this base.", ToastKind.Info);
            return;
        }
        var chestsByEntity = new Dictionary<uint, DepositChestState>();
        foreach (DepositChestState chest in chests)
        {
            chestsByEntity[chest.Entity.Value] = chest;
        }

        var stacks = new List<StackView>();
        FavoriteSet favorites = _state.FavoriteSet();
        int favoriteStacks = 0;
        int belowRarityStacks = 0;
        foreach (InventorySlot slot in playerRead.Contents.Slots)
        {
            if (slot.IsEmpty || slot.IsEquipped || !playerRead.Layout.IsBackpackPage(slot.Slot.Inventory.Value))
            {
                continue;
            }
            if (favorites.Contains(slot.Item.Value, RarityOf(slot), slot.IsGear))
            {
                favoriteStacks++;
                continue;
            }
            if (ChestRules.IsToolEntry(Items.Get(slot.Item)?.Name))
            {
                continue;
            }
            StackView view = ToStackView(player, slot);
            if (!DepositAllowedFor(view))
            {
                belowRarityStacks++;
                continue;
            }
            stacks.Add(view);
        }

        var received = new Dictionary<uint, Dictionary<string, long>>();
        var stayed = new Dictionary<string, long>();
        var stayedPlaces = new List<(string ItemName, string ChestName, bool NamedChestExists)>();
        var namedChests = new List<string>();
        int autoName = _settings?.Value.AutoNameChests ?? 0;
        long deposited = 0;
        foreach (StackView stack in stacks)
        {
            int left = HeldCount(player, stack);
            if (left <= 0)
            {
                continue;
            }
            string itemName = NameOfItem(stack.Item);
            int maxStack = Items.Get(new ItemId(stack.Item))?.MaxStack ?? 0;
            ulong itemCategories = CategoriesOfItem(stack.Item);
            int itemSection = PlaceOf(stack.Item).Section;
            if (autoName != 0 && itemSection >= 0 && await AutoNameChest(chests, stack.Item, maxStack, itemCategories, itemSection, autoName) is { } newName)
            {
                namedChests.Add(newName);
            }

            // First step: fill the stacks of this item that are not full, wherever these chests have them.
            if (!stack.IsGear && maxStack > 0)
            {
                foreach (DepositCandidate candidate in ChestRules.TopUpOrder(ChestRules.BestNamed(DepositCandidates(chests, stack.Item, maxStack, itemCategories, itemSection))))
                {
                    int amount = Math.Min(left, candidate.PartialRoom);
                    int moved = await DepositInto(player, stack, chestsByEntity[candidate.Entity], amount == left ? 0 : amount, itemName, received);
                    left -= moved;
                    deposited += moved;
                    if (left <= 0)
                    {
                        break;
                    }
                }
            }
            // Second step: the rest goes to a chest named for the item's category, else to one that already holds the item.
            if (left > 0)
            {
                uint placeIn = ChestRules.PlaceChest(ChestRules.BestNamed(DepositCandidates(chests, stack.Item, maxStack, itemCategories, itemSection)));
                if (placeIn != 0)
                {
                    int moved = await DepositInto(player, stack, chestsByEntity[placeIn], 0, itemName, received);
                    left -= moved;
                    deposited += moved;
                }
            }
            if (left > 0)
            {
                if (!stayed.ContainsKey(itemName))
                {
                    (string chestName, bool exists) = ChestNameFor(itemSection, itemCategories, chests);
                    stayedPlaces.Add((itemName, chestName, exists));
                }
                stayed[itemName] = stayed.GetValueOrDefault(itemName) + left;
            }
        }
        if (namedChests.Count > 0)
        {
            Hud.Toast($"Named {namedChests.Count} chest(s): {string.Join(", ", namedChests)}.", ToastKind.Info, 6);
        }
        ReportDeposit(player, trigger, place, chests, received, stayed, ChestRules.MissingChestNames(stayedPlaces), favoriteStacks, belowRarityStacks, deposited);
    }

    // Auto-Name Chests (since 0.10.17, off by default): when no chest of this deposit named for the item has room for it (none is
    // named for it, or all those are full), the closest chest that still has its default name and a free slot is named for the
    // item's sub-category (Children) or its group (Parent), and the deposit goes on with that name. Returns the name, or null.
    private async Task<string?> AutoNameChest(List<DepositChestState> chests, uint item, int maxStack, ulong itemCategories, int itemSection, int mode)
    {
        if (_tree == null || itemSection >= _tree.Sections.Count)
        {
            return null;
        }
        foreach (DepositCandidate candidate in DepositCandidates(chests, item, maxStack, itemCategories, itemSection))
        {
            // A chest named All doesn't count (since 1.0.1): it only takes what no chest can be named for.
            if (candidate.NamedForCategory && candidate.NameRank > CategoryTree.CatchAllRank && (candidate.FreeSlots > 0 || candidate.PartialRoom > 0))
            {
                return null;  // a chest named for it still has room
            }
        }
        DepositChestState? closest = null;
        foreach (DepositChestState chest in chests)
        {
            if (chest.Unnamed && chest.Contents.FreeSlots() > 0 && (closest == null || chest.DistanceMeters < closest.DistanceMeters))
            {
                closest = chest;
            }
        }
        if (closest == null)
        {
            return null;
        }
        TreeSection section = _tree.Sections[itemSection];
        string name = mode == ChestRules.AutoNameParent ? section.Group : section.Name;
        bool taken;
        try
        {
            taken = await Items.SetContainerName(closest.Entity, name);
        }
        catch (Exception problem) when (problem is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            Problem($"auto-name: chest {closest.Entity.Value} could not be named {name}: {problem.Message}");
            return null;
        }
        Log.Info($"auto-name: chest {closest.Entity.Value} ({closest.DistanceMeters:0} m) named \"{name}\": {(taken ? "done" : "refused")}");
        if (!taken)
        {
            closest.Unnamed = false;  // not tried again in this deposit
            return null;
        }
        closest.Unnamed = false;
        closest.Label = name;
        closest.SectionRanks = TreeSectionRanksOfChestName(name);
        return name;
    }

    // The chest name that would take an item in a quick deposit: its sub-category in the tree ("Weapons", "Ores"), else its first
    // hand-made category; and whether a chest of these already has that name (so they are full).
    private (string Name, bool Exists) ChestNameFor(int itemSection, ulong itemCategories, List<DepositChestState> chests)
    {
        if (_tree != null && itemSection >= 0 && itemSection < _tree.Sections.Count)
        {
            bool named = chests.Exists(chest => chest.SectionRanks.ContainsKey(itemSection));
            return (_tree.Sections[itemSection].Name, named);
        }
        if (_categories != null && itemCategories != 0)
        {
            int category = BitOperations.TrailingZeroCount(itemCategories);
            if (category < _categories.Names.Count)
            {
                ulong bit = 1UL << category;
                return (_categories.Names[category], chests.Exists(chest => (chest.NameCategories & bit) != 0));
            }
        }
        return ("", false);
    }

    // The rule kind of an item by its sub-category in the tree (weapons, shields, armor, cosmetics, rings), or null.
    private GearKind? GearKindOf(uint item)
    {
        if (_tree == null)
        {
            return null;
        }
        int section = PlaceOf(item).Section;
        return section >= 0 && section < _tree.Sections.Count ? ChestRules.GearKindOfSection(_tree.Sections[section].Key) : null;
    }

    // Quick Deposit's deposit rules (since 0.10.26): gear goes when a rule takes it; every other item always goes.
    private bool DepositAllowedFor(StackView stack)
    {
        return !stack.IsGear || GearKindOf(stack.Item) is not { } kind || ChestRules.DepositAllowed(kind, stack.Rarity, stack.Level, _state.DepositRules);
    }

    // The Re-Sort Chests button (since 0.10.11): items in a linked chest whose name does not ask for them go to a linked chest whose
    // name does, by Quick Deposit's rules (that chest's stacks of the item are topped up first, then a free slot). Items no chest
    // asks for, and those already in a chest that asks for them, stay; chests named with -- in front are left alone.
    private async Task ReSortChests()
    {
        EntityId player = Client.LocalPlayer;
        if (!CanMove(player))
        {
            return;
        }
        LoadStateOnce();
        LoadCategoriesIfChanged();
        EnsureTree();
        List<DepositChestState> chests = ReadLinkedDepositChests(player);
        if (chests.Count < 2)
        {
            _status = "Re-Sort needs at least two linked chests in the world.";
            return;
        }
        var chestsByEntity = new Dictionary<uint, DepositChestState>();
        foreach (DepositChestState chest in chests)
        {
            chestsByEntity[chest.Entity.Value] = chest;
        }
        var received = new Dictionary<uint, Dictionary<string, long>>();
        var noRoom = new Dictionary<string, long>();
        var namedChests = new List<string>();
        int autoName = _settings?.Value.AutoNameChests ?? 0;
        long moved = 0;
        foreach (DepositChestState source in chests)
        {
            foreach (InventorySlot slot in new List<InventorySlot>(source.Contents.Slots))
            {
                if (slot.IsEmpty || slot.Category != InventoryCategory.Generic)
                {
                    continue;
                }
                uint item = slot.Item.Value;
                ulong itemCategories = CategoriesOfItem(item);
                int itemSection = PlaceOf(item).Section;
                // Auto-Name Chests (since 0.10.19 for Re-Sort too): a stack in a chest that doesn't ask for it, with no chest named
                // for it that has room, gets the closest default-named chest named for it (possibly its own chest).
                if (autoName != 0 && itemSection >= 0 && NameRankFor(source, itemCategories, itemSection) <= CategoryTree.CatchAllRank
                    && await AutoNameChest(chests, item, Items.Get(new ItemId(item))?.MaxStack ?? 0, itemCategories, itemSection, autoName) is { } newName)
                {
                    namedChests.Add(newName);
                }
                // The chests whose names ask for the item most closely ("Meals" before "Food"); one already there stays.
                int bestRank = 0;
                foreach (DepositChestState chest in chests)
                {
                    bestRank = Math.Max(bestRank, NameRankFor(chest, itemCategories, itemSection));
                }
                if (bestRank == 0 || NameRankFor(source, itemCategories, itemSection) == bestRank)
                {
                    continue;
                }
                var targets = new List<DepositChestState>();
                foreach (DepositChestState chest in chests)
                {
                    if (chest != source && NameRankFor(chest, itemCategories, itemSection) == bestRank)
                    {
                        targets.Add(chest);
                    }
                }
                StackView stack = ToStackView(source.Entity, slot);
                int left = HeldCount(source.Entity, stack);
                string itemName = NameOfItem(item);
                int maxStack = Items.Get(new ItemId(item))?.MaxStack ?? 0;
                if (!stack.IsGear && maxStack > 0)
                {
                    foreach (DepositCandidate candidate in ChestRules.TopUpOrder(DepositCandidates(targets, item, maxStack, itemCategories, itemSection)))
                    {
                        int amount = Math.Min(left, candidate.PartialRoom);
                        int done = await DepositInto(source.Entity, stack, chestsByEntity[candidate.Entity], amount == left ? 0 : amount, itemName, received);
                        left -= done;
                        moved += done;
                        if (left <= 0)
                        {
                            break;
                        }
                    }
                }
                if (left > 0)
                {
                    uint placeIn = ChestRules.PlaceChest(DepositCandidates(targets, item, maxStack, itemCategories, itemSection));
                    if (placeIn != 0)
                    {
                        int done = await DepositInto(source.Entity, stack, chestsByEntity[placeIn], 0, itemName, received);
                        left -= done;
                        moved += done;
                    }
                }
                if (left > 0)
                {
                    noRoom[itemName] = noRoom.GetValueOrDefault(itemName) + left;
                }
                _status = string.Create(CultureInfo.InvariantCulture, $"Re-Sort: {moved:N0} item(s) moved so far...");
            }
        }
        if (namedChests.Count > 0)
        {
            Hud.Toast($"Named {namedChests.Count} chest(s): {string.Join(", ", namedChests)}.", ToastKind.Info, 6);
        }
        ReportDeposit(player, "the screen's button", "your linked chests", chests, received, noRoom,
            noRoom.Count > 0 ? "No room in the chests named for " + DescribeItems(noRoom) : null, 0, 0, moved, "Re-Sort");
    }

    /// <summary>Moves a stack, or part of it (a count of 0 is all of it), into one chest and notes what arrived. Returns how many moved.</summary>
    private async Task<int> DepositInto(EntityId player, StackView stack, DepositChestState chest, int count, string itemName, Dictionary<uint, Dictionary<string, long>> received)
    {
        MoveResult result = await Items.Move(player, new SlotId(stack.Slot), chest.Entity, count);
        if (result.Done > 0)
        {
            if (!received.TryGetValue(chest.Entity.Value, out Dictionary<string, long>? items))
            {
                items = new Dictionary<string, long>();
                received.Add(chest.Entity.Value, items);
            }
            items[itemName] = items.GetValueOrDefault(itemName) + result.Done;
        }
        else if (result.Status != ItemsStatus.NoRoom)
        {
            Log.Info($"quick deposit: {itemName} into {chest.Label} ({chest.Entity.Value}): {result.Status}{(result.Message.Length > 0 ? " (" + result.Message + ")" : "")}");
        }
        chest.Contents = Items.Read(chest.Entity) ?? chest.Contents;
        return result.Done;
    }

    /// <summary>The storage chests of this base that take deposits, with their content: Magic and regular ones, not those named with -- in front.</summary>
    private List<DepositChestState> ReadBaseChests(BaseInfo home, EntityId player)
    {
        bool playerPlaced = World.TryGetPosition(player, out WorldPosition playerPosition);
        // The Magic Chests whose "Receive Quick Stacking" is on, by the game's own values. Regular chests have no such switch to read.
        HashSet<uint>? takingDeposits = null;
        try
        {
            IReadOnlyList<EntityId> magicChests = Bases.Storage(home.Id, deposit: true);
            if (magicChests.Count > 0)
            {
                takingDeposits = new HashSet<uint>();
                foreach (EntityId magicChest in magicChests)
                {
                    takingDeposits.Add(magicChest.Value);
                }
            }
        }
        catch (Exception problem)
        {
            Problem($"quick deposit: the base's chests that take deposits could not be listed ({problem.GetType().Name}: {problem.Message}): every chest is used");
        }
        var chests = new List<DepositChestState>();
        foreach (EntityId container in Items.FindInventories(InventoryKinds.Containers))
        {
            ChestKind kind = KindOf(container);
            if ((kind != ChestKind.Magic && kind != ChestKind.Regular) || !World.TryGetPosition(container, out WorldPosition position))
            {
                continue;
            }
            if (kind == ChestKind.Magic && takingDeposits != null && !takingDeposits.Contains(container.Value))
            {
                continue;
            }
            BaseInfo? chestBase = Bases.At(position);
            if (chestBase == null || !chestBase.Id.Equals(home.Id))
            {
                continue;
            }
            string? name = ContainerNameOrNull(container);
            if (ChestRules.IsExcludedChestName(name))
            {
                continue;
            }
            InventoryContents? contents = Items.Read(container);
            if (contents == null)
            {
                continue;
            }
            chests.Add(new DepositChestState
            {
                Entity = container,
                Label = name ?? (kind == ChestKind.Magic ? "Magic Chest" : "Chest"),
                NameCategories = HandMadeBitsOfChestName(name),
                Unnamed = name == null && Items.ContainerNamesAvailable,
                SectionRanks = TreeSectionRanksOfChestName(name),
                IsCatchAll = ChestRules.IsCatchAllName(name),
                DistanceMeters = playerPlaced ? position.DistanceTo(playerPosition) : 0,
                Contents = contents,
            });
        }
        return chests;
    }

    /// <summary>The linked chests found in the world, wherever they stand, with their content: the targets of a quick deposit outside a base.</summary>
    private List<DepositChestState> ReadLinkedDepositChests(EntityId player)
    {
        bool playerPlaced = World.TryGetPosition(player, out WorldPosition playerPosition);
        var chests = new List<DepositChestState>();
        foreach (EntityId container in Items.FindInventories(InventoryKinds.Containers))
        {
            if (!World.TryGetPosition(container, out WorldPosition position) || ChestRules.FindLink(_state.Links, ToChestPosition(position)) < 0)
            {
                continue;
            }
            string? name = ContainerNameOrNull(container);
            if (ChestRules.IsExcludedChestName(name))
            {
                continue;
            }
            InventoryContents? contents = Items.Read(container);
            if (contents == null)
            {
                continue;
            }
            chests.Add(new DepositChestState
            {
                Entity = container,
                Label = name ?? (KindOf(container) == ChestKind.Magic ? "Magic Chest" : "Chest"),
                NameCategories = HandMadeBitsOfChestName(name),
                Unnamed = name == null && Items.ContainerNamesAvailable,
                SectionRanks = TreeSectionRanksOfChestName(name),
                IsCatchAll = ChestRules.IsCatchAllName(name),
                DistanceMeters = playerPlaced ? position.DistanceTo(playerPosition) : 0,
                Contents = contents,
            });
        }
        return chests;
    }

    private static List<DepositCandidate> DepositCandidates(List<DepositChestState> chests, uint item, int maxStack, ulong itemCategories, int itemSection)
    {
        var candidates = new List<DepositCandidate>(chests.Count);
        foreach (DepositChestState chest in chests)
        {
            long held = 0;
            long partialRoom = 0;
            int freeSlots = 0;
            foreach (InventorySlot slot in chest.Contents.Slots)
            {
                if (slot.Category != InventoryCategory.Generic)
                {
                    continue;
                }
                if (slot.IsEmpty)
                {
                    freeSlots++;
                }
                else if (slot.Item.Value == item)
                {
                    held += slot.Count;
                    if (!slot.IsGear && slot.ItemEntity.IsNone && slot.Count < maxStack)
                    {
                        partialRoom += maxStack - slot.Count;
                    }
                }
            }
            int rank = NameRankFor(chest, itemCategories, itemSection);
            candidates.Add(new DepositCandidate(chest.Entity.Value, held, (int)Math.Min(partialRoom, int.MaxValue), freeSlots, chest.DistanceMeters, rank > 0, rank));
        }
        return candidates;
    }

    private void ReportDeposit(EntityId player, string trigger, string place, List<DepositChestState> chests, Dictionary<uint, Dictionary<string, long>> received,
        Dictionary<string, long> stayed, string? missingChestNames, int favoriteStacks, int belowRarityStacks, long deposited, string action = "Quick Deposit")
    {
        var lines = new List<string>();
        foreach (DepositChestState chest in chests)
        {
            if (received.TryGetValue(chest.Entity.Value, out Dictionary<string, long>? items))
            {
                lines.Add(chest.Label + ": " + DescribeItems(items));
            }
        }
        string summary = deposited > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{action}: {deposited:N0} item(s) into {received.Count} chest(s).")
            : action == "Quick Deposit" ? "Quick Deposit: nothing to deposit." : $"{action}: nothing to move.";
        string? stayedLine = stayed.Count > 0 ? "Stayed: " + (missingChestNames ?? DescribeItems(stayed)) : null;
        var keptParts = new List<string>();
        if (favoriteStacks > 0)
        {
            keptParts.Add(string.Create(CultureInfo.InvariantCulture, $"{favoriteStacks} favorite stack(s)"));
        }
        if (belowRarityStacks > 0)
        {
            keptParts.Add(string.Create(CultureInfo.InvariantCulture, $"{belowRarityStacks} piece(s) of gear no deposit rule takes"));
        }
        string? favoritesLine = keptParts.Count > 0 ? "Kept: " + string.Join(", ", keptParts) + "." : null;

        _status = summary;
        Hud.Toast(summary, deposited > 0 ? ToastKind.Success : ToastKind.Info, 4);
        if (missingChestNames != null)
        {
            // What to name a chest, without opening the screen's categories (asked on 21 Nov 2026).
            Hud.Toast(missingChestNames, ToastKind.Info, 8);
        }
        var filled = new List<LitChest>();
        bool playerPlaced = World.TryGetPosition(player, out WorldPosition playerFoot);
        foreach (DepositChestState chest in chests)
        {
            // Only the chests near enough to be seen: a deposit into linked chests can fill some far away. The chest's
            // position is its foot: a box sits half a metre up, around its middle.
            if (received.ContainsKey(chest.Entity.Value) && World.TryGetPosition(chest.Entity, out WorldPosition foot)
                && (!playerPlaced || Math.Sqrt(Math.Pow(foot.X - playerFoot.X, 2) + Math.Pow(foot.Y - playerFoot.Y, 2) + Math.Pow(foot.Z - playerFoot.Z, 2)) <= ChestRules.MaxHighlightMeters))
            {
                filled.Add(new LitChest(chest.Entity.Value, new WorldPosition(foot.X, foot.Y + 0.5, foot.Z), chest.Label));
            }
        }
        HighlightChests(filled, "a " + action.ToLowerInvariant());
        Log.Info($"{action.ToLowerInvariant()} ({trigger}): {deposited} item(s) into {received.Count} of {chests.Count} chest(s) of {place}; {stayed.Count} kind(s) of item stayed, {favoriteStacks} favorite stack(s) and {belowRarityStacks} gear below rarity kept");

        string reportPath = Path.Combine(Context.DataDirectory, ReportFileName);
        try
        {
            Directory.CreateDirectory(Context.DataDirectory);
            var report = new List<string> { $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {trigger}, {place}, {chests.Count} chest(s)", summary };
            report.AddRange(lines);
            if (stayedLine != null)
            {
                report.Add(stayedLine);
            }
            if (favoritesLine != null)
            {
                report.Add(favoritesLine);
            }
            File.WriteAllLines(reportPath, report);
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException)
        {
            Problem($"quick deposit: {reportPath} could not be written: {problem.Message}");
        }

        if (!(_settings?.Value.DepositReportInChat ?? true) || !Chat.Available || deposited <= 0 && stayedLine == null)
        {
            return;
        }
        try
        {
            Chat.Tell(player, summary);
            for (int line = 0; line < lines.Count && line < MaxReportChatLines; line++)
            {
                Chat.Tell(player, lines[line]);
            }
            if (lines.Count > MaxReportChatLines)
            {
                Chat.Tell(player, string.Create(CultureInfo.InvariantCulture, $"... and {lines.Count - MaxReportChatLines} more chest(s): see {ReportFileName}."));
            }
            if (stayedLine != null)
            {
                Chat.Tell(player, stayedLine);
            }
            if (favoritesLine != null)
            {
                Chat.Tell(player, favoritesLine);
            }
        }
        catch (Exception problem)
        {
            Problem($"quick deposit: the chat report could not be sent ({problem.GetType().Name}: {problem.Message})");
        }
    }

    private static string DescribeItems(Dictionary<string, long> items)
    {
        var names = new List<string>(items.Keys);
        names.Sort(StringComparer.OrdinalIgnoreCase);
        var parts = new List<string>(names.Count);
        foreach (string name in names)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{name} x{items[name]:N0}"));
        }
        return string.Join(", ", parts);
    }

    private void Notify(string message, ToastKind kind)
    {
        _status = message;
        Hud.Toast(message, kind, 4);
    }

    // ---- The state file: <Kiln data folder of this mod>\streamlined-logistics.json ----

    private string StateFilePath => Path.Combine(Context.DataDirectory, StateFileName);

    // Once, the first time Streamlined Logistics loads: the settings file, the links and favorites, and the key of the mod's former
    // id are copied to the new id's places. The former files stay where they are, as a backup. Returns the former key when it is
    // to be taken over (the new id has no key of its own yet), else null.
    private string? TakeOverFormerMod()
    {
        string? formerKey = null;
        try
        {
            string configFolder = Config.Directory;
            string formerSettings = Path.Combine(configFolder, FormerModId + "-client.json");
            string settings = Path.Combine(configFolder, Context.Info.Id + "-client.json");
            if (File.Exists(formerSettings) && !File.Exists(settings))
            {
                File.Copy(formerSettings, settings);
                Log.Info($"the settings of Remote Chests were copied to {settings}");
            }

            string? dataParent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Context.DataDirectory));
            string formerState = dataParent == null ? "" : Path.Combine(dataParent, FormerModId, FormerStateFileName);
            if (dataParent != null && File.Exists(formerState) && !File.Exists(StateFilePath))
            {
                Directory.CreateDirectory(Context.DataDirectory);
                File.Copy(formerState, StateFilePath);
                Log.Info($"the links and favorites of Remote Chests were copied to {StateFilePath}");
            }

            string keys = Path.Combine(configFolder, "keybinds.json");
            if (File.Exists(keys))
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(keys), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                if (document.RootElement.TryGetProperty("bindings", out JsonElement bindings) && bindings.ValueKind == JsonValueKind.Object
                    && !bindings.TryGetProperty(OpenBindingId, out _)
                    && bindings.TryGetProperty(FormerModId + ".open", out JsonElement former) && former.ValueKind == JsonValueKind.String)
                {
                    formerKey = former.GetString();
                }
            }
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Warn($"what Remote Chests kept could not all be taken over ({problem.GetType().Name}: {problem.Message})");
        }
        return string.IsNullOrWhiteSpace(formerKey) ? null : formerKey;
    }

    private void LoadStateOnce()
    {
        if (_stateLoaded)
        {
            return;
        }
        _stateLoaded = true;
        string path = StateFilePath;
        try
        {
            if (!File.Exists(path))
            {
                Log.Info($"no links yet; they will be kept in {path}");
                return;
            }
            if (ChestRules.TryParse(File.ReadAllText(path), out StoredState state, out string error))
            {
                _state = state;
                _markedFavorites = new HashSet<uint>(state.Favorites);
                _collapsedView = new HashSet<string>(state.Collapsed, StringComparer.OrdinalIgnoreCase);
                _sidebarHiddenView = state.SidebarHidden;
                _keptWindowWidth = state.WindowWidth;
                _keptWindowHeight = state.WindowHeight;
                _storageShareView = state.StorageShare;
                _rulesView = new RulesView(new List<GearRule>(state.DepositRules), new List<GearRule>(state.SalvageRules));
                Log.Info($"{state.Links.Count} linked chest(s) and {state.Favorites.Count} favorite(s) read from {path}");
                return;
            }
            string setAside = path + ".unreadable";
            File.Copy(path, setAside, overwrite: true);
            Log.Warn($"{path} could not be read ({error}): it was copied to {setAside} and the mod starts with no links");
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"{path} could not be read: {problem.Message}");
        }
    }

    private void SaveState()
    {
        _stateVersion++;
        string path = StateFilePath;
        try
        {
            Directory.CreateDirectory(Context.DataDirectory);
            string temporaryPath = path + ".new";
            File.WriteAllText(temporaryPath, ChestRules.Serialize(_state));
            File.Move(temporaryPath, path, overwrite: true);
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException)
        {
            _status = $"The links could not be saved: {problem.Message}";
            Problem($"{path} could not be written: {problem.Message}");
        }
    }

    private void Problem(string message)
    {
        if (_loggedProblems >= MaxLoggedProblems)
        {
            return;
        }
        _loggedProblems++;
        Log.Warn(message);
    }

    // ---- Drawing: Kiln's render thread, every frame while the screen is open. It only reads the snapshot and posts actions. ----

    private void Draw(UiDraw ui)
    {
        if (ui.Scale > 0)
        {
            var window = ui.WindowRect;
            _seenWindowWidth = MathF.Round(window.Width / ui.Scale);
            _seenWindowHeight = MathF.Round(window.Height / ui.Scale);
        }
        Snapshot? snapshot = _snapshot;
        if (snapshot == null)
        {
            ui.Text("Reading the chests...");
            return;
        }
        ui.Text(snapshot.Summary);
        string status = _status;
        ui.TextDisabled(status.Length > 0 ? status : " ");
        if (!ui.BeginTabBar("streamlined-logistics-tabs"))
        {
            return;
        }
        if (ui.BeginTab("Items"))
        {
            DrawItems(ui, snapshot);
            ui.EndTab();
        }
        if (ui.BeginTab("Chests"))
        {
            DrawChests(ui, snapshot);
            ui.EndTab();
        }
        if (ui.BeginTab("Categories"))
        {
            DrawCategories(ui, snapshot);
            ui.EndTab();
        }
        if (ui.BeginTab("Settings"))
        {
            DrawSettings(ui);
            ui.EndTab();
        }
        ui.EndTabBar();
    }

    private void DrawItems(UiDraw ui, Snapshot snapshot)
    {
        _cellRightClicked = false;
        DrawStranded(ui, snapshot);

        ui.NextItemWidth(280 * ui.Scale);
        ui.InputText("Search", ref _search, 64);
        ui.SameLine();
        ui.Checkbox("Favorites Only", ref _favoritesOnly);
        ui.SameLine();
        ui.Checkbox("Show Hidden Categories", ref _showHidden);

        if (ui.Button(snapshot.GridView ? "List View" : "Grid View"))
        {
            bool gridView = !snapshot.GridView;
            Post(() => SetGridView(gridView));
        }
        ui.SameLine();
        if (ui.Button(snapshot.Ungrouped ? "Group" : "Ungroup"))
        {
            bool ungrouped = !snapshot.Ungrouped;
            Post(() => SetUngrouped(ungrouped));
        }
        ui.Tooltip(snapshot.Ungrouped ? "Show every chest's items as one list, under category headers." : "Show the items chest by chest.");
        ui.SameLine();
        bool includeHotbar = snapshot.IncludeHotbar;
        if (ui.Checkbox("Include Hotbars", ref includeHotbar))
        {
            bool nowIncluded = includeHotbar;
            Post(() => SetIncludeHotbar(nowIncluded));
        }
        ui.SameLine();
        if (ui.Button("Quick Deposit"))
        {
            Post(() => QuickDeposit("the screen's button", alwaysIntoLinkedChests: true));
        }
        ui.Tooltip("Your backpack's items go into your linked chests, wherever you are.\nFavorites, the hotbar and what no chest asks for stay.");
        ui.SameLine();
        if (ui.Button("Re-Sort Chests"))
        {
            Post(ReSortChests);
        }
        ui.Tooltip("Items in a linked chest whose name does not ask for them go to a linked chest whose name does.\nWhat no chest asks for, and chests named with -- in front, stay as they are.");
        if (snapshot.GridView)
        {
            DrawSelected(ui, snapshot);
        }

        IReadOnlyList<ItemSection> storedSections = StoredSections(snapshot);
        bool inTree = !snapshot.Ungrouped && snapshot.HiddenSections != null;
        if (inTree)
        {
            DrawCategoryBar(ui, storedSections);
        }

        int hiddenByCategory = 0;
        // What each section and group shows, before the headers that say how many.
        // A search looks through every tab (Search Every Tab, since 0.10.22, on by default).
        bool searchEverywhere = (_settings?.Value.SearchAllTabs ?? true) && !string.IsNullOrWhiteSpace(_search);
        string? focus = inTree && !searchEverywhere ? _focusGroup : null;
        bool allFavorites = _settings?.Value.AllFavoritesInTabs ?? false;
        if (focus != null && focus != CategoryTree.FavoritesKey && !allFavorites && snapshot.SectionGroups is { } sectionGroups)
        {
            // In a group's tab, the favorites at the top are that group's.
            var tabSections = new List<ItemSection>(storedSections.Count);
            foreach (ItemSection section in storedSections)
            {
                if (section.Key != CategoryTree.FavoritesKey)
                {
                    tabSections.Add(section);
                    continue;
                }
                var rows = new List<RowView>();
                foreach (RowView view in section.Rows)
                {
                    if (view.Row.Section >= 0 && view.Row.Section < sectionGroups.Count && sectionGroups[view.Row.Section] == focus)
                    {
                        rows.Add(view);
                    }
                }
                tabSections.Add(section with { Rows = rows });
            }
            storedSections = tabSections;
        }
        var shownCounts = new int[storedSections.Count];
        var groupCounts = new Dictionary<string, int>();
        for (int index = 0; index < storedSections.Count; index++)
        {
            ItemSection section = storedSections[index];
            bool isFavorites = section.Key == CategoryTree.FavoritesKey;
            if (focus != null && !isFavorites && section.Group != focus)
            {
                continue;
            }
            foreach (RowView view in section.Rows)
            {
                if (IsShownStored(view.Row, snapshot))
                {
                    shownCounts[index]++;
                }
                else if (!isFavorites && IsShown(view.Row, _favoritesOnly))
                {
                    hiddenByCategory++;
                }
            }
            if (section.Group.Length > 0)
            {
                groupCounts[section.Group] = groupCounts.GetValueOrDefault(section.Group) + shownCounts[index];
            }
        }
        // The sidebar's entries: what the list below shows, in its order.
        var navEntries = new List<NavEntry>();
        string? navGroup = null;
        for (int index = 0; index < storedSections.Count; index++)
        {
            ItemSection section = storedSections[index];
            if (shownCounts[index] == 0)
            {
                continue;
            }
            bool inGroup = inTree && section.Group.Length > 0;
            if (inGroup && section.Group != navGroup)
            {
                navGroup = section.Group;
                navEntries.Add(new NavEntry(string.Create(CultureInfo.InvariantCulture, $"{section.Group}  ({groupCounts.GetValueOrDefault(section.Group)})"),
                    GroupJumpPrefix + section.Group, 0, section.Group, null));
            }
            else if (!inGroup)
            {
                navGroup = null;
            }
            navEntries.Add(new NavEntry(string.Create(CultureInfo.InvariantCulture, $"{section.Title}  ({shownCounts[index]})"), section.KeyPrefix,
                inGroup ? 1 : 0, inGroup ? section.Group : null, inTree ? section.Key : null));
        }

        float storageWidth = ui.WindowRect.Width * ChestRules.ClampStorageShare(_storageShareView);
        float sidebarWidth = (_sidebarHiddenView ? SidebarStripWidth : SidebarWidth) * ui.Scale;
        if (ui.BeginChild("sidebar", sidebarWidth, 0))
        {
            DrawSidebar(ui, navEntries, snapshot.Ungrouped);
        }
        ui.EndChild();
        ui.SameLine();
        if (ui.BeginChild("storage", Math.Max(storageWidth - sidebarWidth - 8 * ui.Scale, 100 * ui.Scale), 0))
        {
            int drawn = 0;
            string? currentGroup = null;
            bool groupOpen = true;
            for (int index = 0; index < storedSections.Count; index++)
            {
                ItemSection section = storedSections[index];
                if (shownCounts[index] == 0)
                {
                    continue;
                }
                if (inTree)
                {
                    if (section.Group.Length == 0)
                    {
                        currentGroup = null;
                        groupOpen = true;
                    }
                    else if (section.Group != currentGroup)
                    {
                        currentGroup = section.Group;
                        JumpIfAsked(ui, GroupJumpPrefix + section.Group);
                        groupOpen = DrawFoldHeader(ui, section.Group, section.Group, 0, groupCounts.GetValueOrDefault(section.Group));
                    }
                    if (!groupOpen)
                    {
                        continue;
                    }
                    JumpIfAsked(ui, section.KeyPrefix);
                    if (!DrawFoldHeader(ui, section.Key, section.Title, section.Group.Length > 0 ? 1 : 0, shownCounts[index]))
                    {
                        continue;
                    }
                }
                else
                {
                    JumpIfAsked(ui, section.KeyPrefix);
                    ui.Separator(section.Title);
                }
                drawn += snapshot.GridView ? DrawSectionGrid(ui, snapshot, section, Side.Storage) : DrawStorageList(ui, snapshot, section, showChests: !snapshot.Ungrouped);
            }
            // A header that was not drawn (filtered out meanwhile) is not waited for.
            _jumpTo = null;
            if (drawn == 0)
            {
                ui.TextDisabled(snapshot.StoredItemCount == 0 ? "Nothing here yet: link chests on the Chests tab." : "No item matches.");
            }
            if (hiddenByCategory > 0)
            {
                ui.TextDisabled(string.Create(CultureInfo.InvariantCulture, $"{hiddenByCategory} more in hidden categories. Search finds them, and the Categories tab chooses what is hidden."));
            }
        }
        ui.EndChild();
        bool storageRightClicked = ui.ItemClicked(UiMouseButton.Right);
        DropOn(ui, Side.Storage);

        ui.SameLine();
        DrawSplitter(ui);
        ui.SameLine();
        if (ui.BeginChild("backpack", 0, 0))
        {
            _hotbarsDropArea = null;
            foreach (ItemSection section in snapshot.Carried)
            {
                ui.Separator(section.Title);
                float sectionTop = ui.LastItem.MinY;
                int drawn = snapshot.GridView ? DrawSectionGrid(ui, snapshot, section, Side.Backpack) : DrawBackpackList(ui, section);
                if (drawn == 0)
                {
                    ui.TextDisabled(section.Rows.Count == 0 ? "Empty." : "No item matches.");
                }
                if (section.KeyPrefix == HotbarsKeyPrefix)
                {
                    ui.Space(1, 1);
                    _hotbarsDropArea = (sectionTop, ui.LastItem.MaxY);
                }
            }
        }
        ui.EndChild();
        bool backpackRightClicked = ui.ItemClicked(UiMouseButton.Right);
        DropOn(ui, Side.Backpack);

        // RMB on a list's empty space lets go of the selected item.
        if ((storageRightClicked || backpackRightClicked) && !_cellRightClicked)
        {
            _selectedSide = Side.None;
            _selectedKey = null;
        }
    }

    private static IReadOnlyList<ItemSection> StoredSections(Snapshot snapshot)
    {
        return snapshot.Ungrouped ? snapshot.ByChest : snapshot.Grouped;
    }

    // A plain material found in an equipment slot (version 0.1.0 could leave a taken stack there), with a button to bring it back.
    private void DrawStranded(UiDraw ui, Snapshot snapshot)
    {
        if (snapshot.Stranded.Count == 0)
        {
            return;
        }
        ui.Text(WarningColor, "Found in an equipment slot, where it can't be used:");
        foreach (RowView view in snapshot.Stranded)
        {
            ItemRow row = view.Row;
            using (ui.Id("stranded:" + row.Key))
            {
                ui.ItemIconTrimmed(ShownItem(row.Item), ListIconSize * ui.Scale);
                ui.SameLine();
                ui.Text(view.Label + " x" + view.CountText);
                ui.SameLine();
                if (ui.SmallButton("Move To Backpack"))
                {
                    Post(() => Rescue(row));
                }
            }
        }
        ui.Separator();
    }

    // Grid view: what the selected cell is, with the buttons the list view has on every line.
    private void DrawSelected(UiDraw ui, Snapshot snapshot)
    {
        RowView? selected = null;
        ItemSection? selectedSection = null;
        if (_selectedKey != null && _selectedSide != Side.None)
        {
            foreach (ItemSection section in _selectedSide == Side.Storage ? StoredSections(snapshot) : snapshot.Carried)
            {
                foreach (RowView view in section.Rows)
                {
                    if (section.KeyPrefix + view.Row.Key == _selectedKey)
                    {
                        selected = view;
                        selectedSection = section;
                        break;
                    }
                }
                if (selected != null)
                {
                    break;
                }
            }
        }
        if (selected == null || selectedSection == null)
        {
            ui.TextDisabled("LMB: Select     RMB: Take Or Store A Stack     Shift + RMB: Favorite     MMB: Use");
            return;
        }
        ItemRow row = selected.Row;
        using (ui.Id("selected:" + _selectedKey))
        {
            ui.ItemIconTrimmed(ShownItem(row.Item), ListIconSize * ui.Scale);
            ItemTooltip(ui, "", row.Item, row.Level, row.Rarity, row.IsGear);
            ui.SameLine();
            if (_selectedSide == Side.Storage)
            {
                if (snapshot.Ungrouped)
                {
                    ui.Text(selected.Label + " x" + selected.CountText + " in " + selectedSection.Title);
                }
                else
                {
                    ui.Text(selected.Label + " x" + selected.CountText + " in " + (row.HolderCount == 1 ? selected.HoldersTooltip : selected.HoldersText));
                    if (row.HolderCount > 1)
                    {
                        ui.Tooltip(selected.HoldersTooltip);
                    }
                }
                ui.SameLine();
                DrawTakeButtons(ui, row);
            }
            else
            {
                ui.Text(selected.Label + " x" + selected.CountText);
                ui.SameLine();
                if (ui.SmallButton("Store"))
                {
                    Post(() => Store(row));
                }
                if (IsUsable(row.Item))
                {
                    ui.SameLine();
                    if (ui.SmallButton("Use"))
                    {
                        Post(() => Use(row));
                    }
                    ui.Tooltip("Use one, as from the hotbar.\nFrom a chest, it goes into your backpack first.");
                }
            }
            ui.SameLine();
            DrawFavoriteCheckbox(ui, row, "Favorite");
        }
    }

    private void DrawTakeButtons(UiDraw ui, ItemRow row)
    {
        if (ui.SmallButton("Take Stack"))
        {
            Post(() => Take(row, everyStack: false));
        }
        if (row.Stacks.Count > 1)
        {
            ui.SameLine();
            if (ui.SmallButton("Take All"))
            {
                Post(() => Take(row, everyStack: true));
            }
        }
        if (IsUsable(row.Item))
        {
            ui.SameLine();
            if (ui.SmallButton("Use"))
            {
                Post(() => Use(row));
            }
            ui.Tooltip("Use one, as from the hotbar.\nFrom a chest, it goes into your backpack first.");
        }
    }

    private void DrawFavoriteCheckbox(UiDraw ui, ItemRow row, string label)
    {
        bool favorite = row.IsFavorite;
        if (ui.Checkbox(label, ref favorite))
        {
            uint item = row.Item;
            int rarity = row.Rarity;
            bool isGear = row.IsGear;
            bool nowFavorite = favorite;
            Post(() => SetFavorite(item, rarity, isGear, nowFavorite));
        }
    }

    private bool IsShown(ItemRow row, bool favoritesOnly)
    {
        return (!favoritesOnly || row.IsFavorite) && ChestRules.Matches(row.Name, _search);
    }

    /// <summary>
    /// The same for the chests' side, where hidden categories also leave items out. A favorite always shows, and so does everything
    /// while a search is typed or Show Hidden Categories is on.
    /// </summary>
    private bool IsShownStored(ItemRow row, Snapshot snapshot)
    {
        return IsShown(row, _favoritesOnly)
            && (_showHidden || row.IsFavorite || !string.IsNullOrWhiteSpace(_search)
                || (snapshot.HiddenSections != null ? !snapshot.HiddenSections.Contains(row.Section) : !ChestRules.IsHiddenByCategory(row.Categories, snapshot.HiddenCategories)));
    }

    private int DrawStorageList(UiDraw ui, Snapshot snapshot, ItemSection section, bool showChests)
    {
        int drawn = 0;
        bool tableOpen = showChests
            ? ui.BeginTable("storage-rows:" + section.KeyPrefix, "Favorite", "##icon", "Item", "Count", "Chests", "##actions")
            : ui.BeginTable("storage-rows:" + section.KeyPrefix, "Favorite", "##icon", "Item", "Count", "##actions");
        if (!tableOpen)
        {
            return drawn;
        }
        foreach (RowView view in section.Rows)
        {
            ItemRow row = view.Row;
            if (!IsShownStored(row, snapshot))
            {
                continue;
            }
            if (++drawn > MaxRowsDrawn)
            {
                break;
            }
            using (ui.Id("stored:" + section.KeyPrefix + row.Key))
            {
                ui.NextRow();
                ui.NextColumn();
                DrawFavoriteCheckbox(ui, row, "##favorite");
                ui.NextColumn();
                ui.ItemIconTrimmed(ShownItem(row.Item), ListIconSize * ui.Scale);
                DragFrom(ui, StoredDragKind, section.KeyPrefix, row);
                ItemTooltip(ui, "", row.Item, row.Level, row.Rarity, row.IsGear);
                ui.NextColumn();
                ui.Text(view.Label);
                DragFrom(ui, StoredDragKind, section.KeyPrefix, row);
                ItemTooltip(ui, "", row.Item, row.Level, row.Rarity, row.IsGear);
                ui.NextColumn();
                ui.Text(view.CountText);
                if (showChests)
                {
                    ui.NextColumn();
                    ui.Text(view.HoldersText);
                    if (row.HolderCount > 1)
                    {
                        ui.Tooltip(view.HoldersTooltip);
                    }
                }
                ui.NextColumn();
                DrawTakeButtons(ui, row);
            }
        }
        ui.EndTable();
        return drawn;
    }

    private int DrawBackpackList(UiDraw ui, ItemSection section)
    {
        int drawn = 0;
        if (!ui.BeginTable("backpack-rows:" + section.KeyPrefix, "Favorite", "##icon", "Item", "Count", "##actions"))
        {
            return drawn;
        }
        foreach (RowView view in section.Rows)
        {
            ItemRow row = view.Row;
            if (!IsShown(row, favoritesOnly: false))
            {
                continue;
            }
            if (++drawn > MaxRowsDrawn)
            {
                break;
            }
            using (ui.Id("carried:" + section.KeyPrefix + row.Key))
            {
                ui.NextRow();
                ui.NextColumn();
                DrawFavoriteCheckbox(ui, row, "##favorite");
                ui.NextColumn();
                ui.ItemIconTrimmed(ShownItem(row.Item), ListIconSize * ui.Scale);
                DragFrom(ui, CarriedDragKind, section.KeyPrefix, row);
                ItemTooltip(ui, "", row.Item, row.Level, row.Rarity, row.IsGear);
                ui.NextColumn();
                ui.Text(view.Label);
                DragFrom(ui, CarriedDragKind, section.KeyPrefix, row);
                ItemTooltip(ui, "", row.Item, row.Level, row.Rarity, row.IsGear);
                ui.NextColumn();
                ui.Text(view.CountText);
                ui.NextColumn();
                if (ui.SmallButton("Store"))
                {
                    Post(() => Store(row));
                }
                if (IsUsable(row.Item))
                {
                    ui.SameLine();
                    if (ui.SmallButton("Use"))
                    {
                        Post(() => Use(row));
                    }
                    ui.Tooltip("Use one, as from the hotbar.\nFrom a chest, it goes into your backpack first.");
                }
            }
        }
        ui.EndTable();
        return drawn;
    }

    /// <summary>One section as a grid, on either side: LMB selects, RMB moves a stack to the other side, Shift + RMB marks a favorite, MMB uses a food or drink, on either side.</summary>
    private int DrawSectionGrid(UiDraw ui, Snapshot snapshot, ItemSection section, Side side)
    {
        int drawn = 0;
        GridLayout grid = BeginGrid(ui);
        foreach (RowView view in section.Rows)
        {
            ItemRow row = view.Row;
            bool shown = side == Side.Storage ? IsShownStored(row, snapshot) : IsShown(row, favoritesOnly: false);
            if (!shown)
            {
                continue;
            }
            if (drawn >= MaxRowsDrawn)
            {
                break;
            }
            string key = section.KeyPrefix + row.Key;
            bool isSelected = _selectedSide == side && key == _selectedKey;
            bool clicked = DrawGridCell(ui, (side == Side.Storage ? "stored:" : "carried:") + section.KeyPrefix, view, drawn, grid, isSelected, out bool rightClicked, out bool middleClicked,
                side == Side.Storage ? StoredDragKind : CarriedDragKind, section.KeyPrefix);
            drawn++;
            if (middleClicked)
            {
                Post(() => Use(row));
            }
            if (clicked)
            {
                _selectedSide = side;
                _selectedKey = key;
            }
            if (rightClicked)
            {
                if (side == Side.Storage)
                {
                    OnCellRightClicked(ui, row, () => Take(row, everyStack: false));
                }
                else
                {
                    OnCellRightClicked(ui, row, () => Store(row));
                }
            }
        }
        EndGrid(ui, grid, drawn);
        return drawn;
    }

    // A stack's icon, name or cell, dragged with LMB: it carries the stack to the other side's list. The row itself stays on this
    // thread; the payload only names it, so a drop matches the drag it came from.
    private void DragFrom(UiDraw ui, string kind, string sectionKey, ItemRow row)
    {
        // One Kiln drag kind for every stack (since 0.10.14), so the right side takes drops from both sides; the payload says
        // where the stack comes from.
        string payload = kind + "|" + sectionKey + row.Key;
        if (ui.DragSource(StackDragKind, payload, string.Create(CultureInfo.InvariantCulture, $"{row.Name} x {row.Total:N0}")))
        {
            _draggedRow = row;
            _draggedPayload = payload;
            _draggedFromHotbars = kind == CarriedDragKind && sectionKey == HotbarsKeyPrefix;
        }
    }

    // Kiln's render thread: a stack dropped on a side's list. From the chests to the right side it is taken (one stack, every
    // stack with Shift held), into the hotbars when dropped over them; from the right side to the chests it is stored, as RMB
    // does; between the hotbars and the inventory it goes through a linked chest (Kiln moves between two owners only).
    private void DropOn(UiDraw ui, Side target)
    {
        if (ui.DropTarget(StackDragKind) is not { } payload || payload != _draggedPayload || _draggedRow is not { } row)
        {
            return;
        }
        bool fromChests = payload.StartsWith(StoredDragKind + "|", StringComparison.Ordinal);
        bool fromHotbars = _draggedFromHotbars;
        _draggedRow = null;
        _draggedPayload = null;
        bool toHotbars = target == Side.Backpack && _hotbarsDropArea is { } area && ui.Pointer is { } pointer && pointer.Y >= area.Top && pointer.Y <= area.Bottom;
        bool everyStack = (ui.HeldMods & KeyMods.Shift) != 0;
        if (target == Side.Storage)
        {
            if (!fromChests)
            {
                Post(() => Store(row));
            }
        }
        else if (fromChests)
        {
            Post(() => Take(row, everyStack, toHotbars));
        }
        else if (fromHotbars != toHotbars)
        {
            Post(() => MoveWithinPlayer(row, toHotbars));
        }
    }

    // RMB on a cell moves a stack to the other side; with Shift it marks or unmarks the item as a favorite.
    private void OnCellRightClicked(UiDraw ui, ItemRow row, Func<Task> move)
    {
        _cellRightClicked = true;
        if ((ui.HeldMods & KeyMods.Shift) != 0)
        {
            uint item = row.Item;
            int rarity = row.Rarity;
            bool isGear = row.IsGear;
            bool nowFavorite = !row.IsFavorite;
            Post(() => SetFavorite(item, rarity, isGear, nowFavorite));
            return;
        }
        Post(move);
    }

    // A grid's cells are placed by hand, each at its own spot, so that nothing depends on how wide Kiln draws one: an icon that
    // is scrolled out of view is drawn as bare space of another size.
    private static GridLayout BeginGrid(UiDraw ui)
    {
        UiCursor cursor = ui.Cursor;
        float cellSize = GridCellSize * ui.Scale;
        float spacing = GridSpacing * ui.Scale;
        int columns = Math.Max(1, (int)((cursor.AvailWidth + spacing) / (cellSize + spacing)));
        return new GridLayout(cursor.X, cursor.Y, cellSize, spacing, columns);
    }

    /// <summary>Puts what is drawn next under the grid's last row.</summary>
    private static void EndGrid(UiDraw ui, GridLayout grid, int cells)
    {
        if (cells == 0)
        {
            return;
        }
        int rows = (cells + grid.Columns - 1) / grid.Columns;
        ui.SetCursor(grid.OriginX, grid.OriginY + rows * (grid.CellSize + grid.Spacing));
        ui.Space(1, 1);
    }

    /// <summary>One cell of a grid: a button with the item's icon on it, the count always shown, a border when it is a favorite or selected, and its name and place on hover. True when clicked with LMB.</summary>
    private bool DrawGridCell(UiDraw ui, string idPrefix, RowView view, int cellIndex, GridLayout grid, bool isSelected, out bool rightClicked, out bool middleClicked,
        string dragKind, string sectionKey)
    {
        ItemRow row = view.Row;
        float cellX = grid.OriginX + (cellIndex % grid.Columns) * (grid.CellSize + grid.Spacing);
        float cellY = grid.OriginY + (cellIndex / grid.Columns) * (grid.CellSize + grid.Spacing);
        float margin = 3 * ui.Scale;
        bool clicked;
        using (ui.Id(idPrefix + row.Key))
        {
            ui.SetCursor(cellX, cellY);
            clicked = ui.Button("##cell", grid.CellSize, grid.CellSize);
            rightClicked = ui.ItemClicked(UiMouseButton.Right);
            middleClicked = ui.ItemClicked(UiMouseButton.Middle);
            UiItem cell = ui.LastItem;
            DragFrom(ui, dragKind, sectionKey, row);
            ItemTooltip(ui, view.GridTooltip, row.Item, row.Level, row.Rarity, row.IsGear);

            float iconSize = Math.Min(grid.CellSize - 2 * margin, MaxGridIconPixels);
            ui.SetCursor(cellX + (grid.CellSize - iconSize) / 2, cellY + (grid.CellSize - iconSize) / 2);
            ui.ItemIconTrimmed(ShownItem(row.Item), iconSize);

            if (cell.Width > 0)
            {
                (float textWidth, float textHeight) = ui.MeasureText(view.ShortCount, 0);
                ui.DrawRect(UiLayer.Window, cell.MaxX - textWidth - 3 * margin, cell.MaxY - textHeight - 2 * margin, cell.MaxX - margin, cell.MaxY - margin, CountBackground, 3 * ui.Scale, 0);
                ui.DrawText(UiLayer.Window, cell.MaxX - textWidth - 2 * margin, cell.MaxY - textHeight - 1.5f * margin, view.ShortCount, Color.White);
                if (row.IsFavorite)
                {
                    ui.DrawRect(UiLayer.Window, cell.MinX, cell.MinY, cell.MaxX, cell.MaxY, FavoriteColor, 3 * ui.Scale, 2 * ui.Scale);
                }
                if (isSelected)
                {
                    ui.DrawRect(UiLayer.Window, cell.MinX + margin, cell.MinY + margin, cell.MaxX - margin, cell.MaxY - margin, SelectedColor, 3 * ui.Scale, 1 * ui.Scale);
                }
            }
        }
        return clicked;
    }

    private void DrawCategories(UiDraw ui, Snapshot snapshot)
    {
        ui.TextWrapped("A hidden category is left out of the Linked Chests list. Favorites always show, and a search looks through everything.");
        if (snapshot.Categories.Count == 0)
        {
            ui.TextDisabled(snapshot.CategoriesNote);
            return;
        }
        bool tree = snapshot.HiddenSections != null;
        if (ui.Button("Show All"))
        {
            Post(() => tree ? SetTreeHiddenAll(false) : SetHiddenCategories(false));
        }
        ui.SameLine();
        if (ui.Button("Hide All"))
        {
            Post(() => tree ? SetTreeHiddenAll(true) : SetHiddenCategories(true));
        }
        ui.SameLine();
        if (ui.Button("Reset To Default"))
        {
            Post(() => tree ? SetTreeHiddenAll(null) : SetHiddenCategories(null));
        }
        ui.Tooltip(tree
            ? "Hides what is only used through the craft menu or the Construction Hammer: Materials and Building."
            : "Hides the categories that are only used through the craft menu or the Construction Hammer: materials, Blocks and Terrain.");
        if (tree)
        {
            ui.TextDisabled("Materials are sorted by the category file's material categories: " + snapshot.CategoriesNote);
        }
        ui.TextDisabled(snapshot.CategoriesNote);
        if (ui.BeginChild("categories", 0, 0))
        {
            if (ui.BeginTable("category-rows", "Shown", "Category", "Items In Linked Chests", "Chest Named For It", "Free Slots"))
            {
                foreach (CategoryView category in snapshot.Categories)
                {
                    using (ui.Id("category:" + (category.Key.Length > 0 ? category.Key : category.Name)))
                    {
                        ui.NextRow();
                        ui.NextColumn();
                        bool shown = !category.IsHidden;
                        if (ui.Checkbox("##shown", ref shown))
                        {
                            string name = category.Name;
                            string key = category.Key;
                            bool nowHidden = !shown;
                            Post(() => key.Length > 0 ? SetTreeHidden(key, nowHidden) : SetCategoryHidden(name, nowHidden));
                        }
                        ui.NextColumn();
                        if (ui.Selectable(category.Depth > 0 ? "      " + category.Name : category.Name) && tree)
                        {
                            string key = category.Key;
                            Post(() => HighlightNamedChests(key));
                        }
                        if (tree)
                        {
                            ui.Tooltip(category.Depth > 0 ? "LMB: Mark the chests named for it in the world." : "LMB: Mark the chests named for it, or for one of its categories, in the world.");
                        }
                        ui.NextColumn();
                        ui.Text(category.ItemCount.ToString(CultureInfo.InvariantCulture));
                        ui.NextColumn();
                        if (tree)
                        {
                            // Read only: whether a linked chest is named exactly for this category (the Chests tab names them).
                            bool named = category.NamedChests > 0;
                            ui.Checkbox(category.NamedChests > 1 ? string.Create(CultureInfo.InvariantCulture, $"x{category.NamedChests}##named") : "##named", ref named);
                            ui.Tooltip(category.NamedChests > 0
                                ? string.Create(CultureInfo.InvariantCulture, $"{category.NamedChests} linked chest(s) named \"{category.Name}\".")
                                : $"No linked chest is named \"{category.Name}\": name one on the Chests tab.");
                            ui.NextColumn();
                            ui.Text(category.NamedChests > 0 ? category.NamedFreeSlots.ToString(CultureInfo.InvariantCulture) : "");
                        }
                        else
                        {
                            ui.NextColumn();
                        }
                    }
                }
                ui.EndTable();
            }
        }
        ui.EndChild();
    }

    // A chest's name as a list of the category names (since 0.10.16): choosing one renames the chest, so Quick Deposit and
    // Re-Sort send it that category. RMB turns it into a text field for a name of the player's own (Enter saves, Escape cancels).
    private void DrawChestNameChoice(UiDraw ui, ChestView chest)
    {
        IReadOnlyList<string> choices = _chestNameChoices;
        string current = chest.Name ?? "";
        int index = -1;
        for (int choice = 0; choice < choices.Count; choice++)
        {
            if (string.Equals(choices[choice], current.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                index = choice;
                break;
            }
        }
        IReadOnlyList<string> items = choices;
        int offset = 0;
        if (index < 0)
        {
            // A name that is no category's (or none): shown first, as it is.
            var withCurrent = new List<string>(choices.Count + 1) { current.Length > 0 ? current : chest.Label + " (No Name)" };
            withCurrent.AddRange(choices);
            items = withCurrent;
            index = 0;
            offset = 1;
        }
        int chosen = index;
        ui.NextItemWidth(Math.Max(ui.Cursor.AvailWidth, 120 * ui.Scale));
        if (ui.Combo("##name", ref chosen, items) && chosen != index && chosen >= offset)
        {
            uint entity = chest.Entity;
            string name = items[chosen];
            Post(() => RenameChest(entity, name));
        }
        if (ui.ItemClicked(UiMouseButton.Right))
        {
            _renamingChest = chest.Entity;
            _renameText = current;
            _renameFocusPending = true;
        }
        ui.Tooltip("Choose a category's name: Quick Deposit and Re-Sort send this chest that category.\nAll: this chest takes any item that no other chest takes.\nRMB: type a name of your own. An empty name clears it.");
    }

    private void DrawChests(UiDraw ui, Snapshot snapshot)
    {
        double radiusMeters = ChestRules.LinkRadius(_settings?.Value.LinkRadiusMeters ?? ChestRules.DefaultLinkRadiusMeters);
        IReadOnlyList<ChestView> chests = snapshot.Chests;
        if (ui.Button(string.Create(CultureInfo.InvariantCulture, $"Link Nearby Chests ({radiusMeters:0} m)")))
        {
            Post(() => LinkNearby(chests, radiusMeters));
        }
        ui.SameLine();
        if (ui.Button("Unlink All"))
        {
            Post(UnlinkAll);
        }
        ui.TextDisabled("Linking only adds a chest to the list. Items stay in their chests. " + snapshot.ChestsNote);
        if (ui.BeginChild("chests", 0, 0))
        {
            if (ui.BeginTable("chest-rows", "Linked", "Chest", "Kind", "Slots Used", "Distance", "##show"))
            {
                foreach (ChestView chest in chests)
                {
                    using (ui.Id("chest:" + chest.Entity.ToString(CultureInfo.InvariantCulture)))
                    {
                        ui.NextRow();
                        ui.NextColumn();
                        bool linked = chest.IsLinked;
                        if (ui.Checkbox("##linked", ref linked))
                        {
                            bool nowLinked = linked;
                            Post(() => SetLinked(chest, nowLinked));
                        }
                        ui.NextColumn();
                        bool renaming = _renamingChest == chest.Entity;
                        bool saveName = false;
                        bool cancelName = false;
                        if (renaming)
                        {
                            // The field fills the name's column and takes the keyboard when Rename is clicked; Enter saves, Escape
                            // cancels. Save and Cancel are in the last column (0.10.0 put them after the field, outside the column).
                            if (_renameFocusPending)
                            {
                                ui.FocusKeyboardHere();
                                _renameFocusPending = false;
                            }
                            ui.NextItemWidth(Math.Max(ui.Cursor.AvailWidth, 120 * ui.Scale));
                            ui.InputText("##name", ref _renameText, Items.ContainerNameMaxChars);
                            saveName = ui.KeyPressed(UiKey.Enter, claim: true);
                            cancelName = ui.KeyPressed(UiKey.Escape, claim: true);
                        }
                        else
                        {
                            DrawChestNameChoice(ui, chest);
                        }
                        ui.NextColumn();
                        ui.Text(chest.KindText);
                        ui.NextColumn();
                        ui.Text(chest.SlotsText);
                        ui.NextColumn();
                        ui.Text(chest.DistanceText);
                        ui.NextColumn();
                        if (renaming)
                        {
                            saveName |= ui.SmallButton("Save");
                            ui.Tooltip("Enter also saves. An empty name clears it.");
                            ui.SameLine();
                            cancelName |= ui.SmallButton("Cancel");
                            ui.Tooltip("Escape also cancels.");
                            if (saveName)
                            {
                                uint entity = chest.Entity;
                                string name = _renameText;
                                _renamingChest = 0;
                                Post(() => RenameChest(entity, name));
                            }
                            else if (cancelName)
                            {
                                _renamingChest = 0;
                            }
                        }
                        else
                        {
                            if (ui.SmallButton("Show"))
                            {
                                Post(() => StartHighlight(chest));
                            }
                            ui.Tooltip(chest.DistanceMeters <= ChestRules.MaxHighlightMeters ? "Mark this chest in the world." : "Too far to mark in the world.");
                        }
                    }
                }
                ui.EndTable();
            }
        }
        ui.EndChild();
    }
}
