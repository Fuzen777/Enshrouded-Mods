using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Kiln;

namespace BetterSpells;

/// <summary>
/// Better Spells on Kiln: spells that last longer, hit harder, reach wider and summon more, one setting per spell, applied while playing
/// to what is cast afterwards. Every value is changed through Kiln's game data service as this mod's layer over the game's own value:
/// a setting at its neutral value gives the layer back, and unloading the mod gives back all of them.
/// Items, templates and casts are found by name (SpellCatalog); each spell item names the template it places.
/// The settings window (Shift+F3, or the button on the mod's page in Kiln's menu) has a row per spell with a stepped control
/// per setting; Kiln's own sliders on the mod's page are free and land on the nearest step.
/// </summary>
public sealed class BetterSpellsMod : Mod
{
    // Fields of keen::ItemInfo.
    private const string PlacedTemplatePath = "equipment.placedEntity";
    private const string DamageModifierPath = "damageSetup.dmgMod";
    private const string LockOnRangePath = "itemTargetingSetup.targetingRange";
    private const string TooltipList = "uiValues";
    // Config values an item carries, by config id. Charge Time is the game's own name for the first (its tooltip line); the second keeps
    // the caster locked in the cast (seen in game); the third is RangeTerraforming in the game's scripts, the radius of the ground cursor.
    private const string CursorRangePath = "impactValues.0x5258131e";
    // Tooltip lines, by text id: Duration and Charge Time in milliseconds, Radius in metres times 100.
    private const uint DurationTooltipLocaId = 609674671;
    private const uint RadiusTooltipLocaId = 3386387946;

    // Fields of a keen::ecs::TemplateResource, by component type. LifeTime is what removes what a spell places, in seconds here.
    private const string LifetimePath = "LifeTime.lifeTime";
    // A placed entity's health (see ApplyHealth).
    private const string HealthPath = "Health.dataStorage[0]";
    private const string HealthMaxBasePath = "Health.dataStorage[2]";
    private const string HealthScaleFactorPath = "EnemyScaling.enemyLevelScaling.healthScaleFactor";
    // An item's summon: its lifetime (TimeValues, read by the summoning scripts) and the template it spawns.
    private const string ItemLifetimeConfigPath = "impactValues.0x663d391d";
    private const string SpawnTemplateConfigPath = "impactValues.0xf6a785cd";
    // The Lightning Wisp's own flags: immune and not handling hits.
    private const string ImmunePath = "CombatFlags.immune";
    private const string HandleHitsPath = "CombatFlags.handleHits";
    // The faction an entity gets when it spawns. Totems are PlayerAlly, like skeletons and wisps.
    private const string FactionPath = "Faction.faction";
    // Enemies Ignore Totems: totems get a faction of their own, and every enemy faction is made neutral toward it. AutomatedPlayer is on
    // the players' side in the game's own rules (so a totem and its shots still treat players and their summons as friends and enemies
    // as enemies), and the game uses it for one template only, Player_Vukah_Melee.
    private static readonly Faction TotemFaction = Faction.AutomatedPlayer;
    // The factions the totems' own script shoots at (its FactionMask).
    private static readonly Faction[] EnemyFactions =
    {
        Faction.Fogger, Faction.Vukah, Faction.Scavenger, Faction.Wildlife, Faction.WildBeast, Faction.Skeleton, Faction.Cyclops, Faction.Drak, Faction.Ancients,
    };
    private const string ColliderComponent = "ColliderResourceComponent";
    private static readonly Regex ColliderRadiusPath = new Regex(@"^ColliderResourceComponent\.colliders\[[^\]]+\]\.dataArray\[[^\]]+\]\.shape\.radius$", RegexOptions.CultureInvariant);
    // How many colliders and shapes per collider are tried by index when Kiln's listing names no radius (the spells' templates have one collider with one or two shapes).
    private const int MaxCollidersTried = 2;
    private const int MaxShapesTried = 4;
    // Config values of a template's script: how long a zone's debuff lasts on an enemy, how far Root's zone reaches, the wait between two hits.
    private const string DebuffDurationPath = "ImpactConfiguration.configValues.0x130a0ef2";
    private const string ReachPath = "ImpactConfiguration.configValues.0x6c2fad6d";
    private const string HitWaitPath = "ImpactConfiguration.configValues.0xe21b4b72";
    private const string VfxParametersPath = "VfxParametersTemplateComponent.parameters.data";
    // Each transform is {position, orientation, scale}; RenderTransform also has the two it interpolates between.
    private static readonly string[] TransformPaths = { "CurrentTransform.transform", "RenderTransform.transform", "RenderTransform.interpolationSource", "RenderTransform.interpolationTarget" };
    private static readonly string[] ScaleAxes = { "x", "y", "z" };

    // Fields of a cast's one spawn event, in its keen::actor::ActorSubSequenceResource.
    private const string SpawnEventPath = "events[SpawnEntityEvent#0]";
    private const string SpawnSpeedPath = SpawnEventPath + ".spawnVelocity.z";
    // Chain Lightning's cast places its script with a sphere of its own (keen::actor::SpawnImpact), and the sphere's physics shape is
    // looked up by the key next to it.
    private const string ChainImpactPath = "events[SpawnImpact#0].colliderData";
    private const string ChainSpherePath = ChainImpactPath + ".dataArray[0].shape.radius";
    private static readonly string[] ChainShapeKeyPaths = { ChainImpactPath + ".joltObjects[0].hash", ChainImpactPath + ".joltObjects[0].hash.value" };

    private const int MaxRefusalLines = 40;
    private const string WindowBindingId = "better-spells.window";
    private const string WindowDefaultKey = "Shift+F3";
    private const string WindowTitle = "Better Spells";
    // The screen's size before the overlay's scale. A Kiln screen has a fixed size (it doesn't fit its contents): this fits the
    // longest card (Totems) under the six rows of icons, with the hint wrapped to the width.
    private const float ScreenWidth = 370f;
    private const float ScreenHeight = 760f;
    // The tables' ids. The overlay keeps a table's column widths by id, so a changed layout gets new ids and starts from fitted widths.
    // A row's height, before the overlay's scale: the spell's icon is a square of it, and so are the - and + buttons.
    private const float RowHeight = 26f;
    // The most entries read from a game data list (a cast's events of one type, an item's casts) before giving up.
    private const int MaxListScan = 64;
    // A spell's icon button in its section's row, before the overlay's scale.
    private const float IconButtonSize = 36f;

    private ModConfig<SpellSettings>? _settings;
    private IDisposable? _settingsPage;
    // A Kiln screen rather than a window: screens open centered every time (a window's place is kept by a number Kiln gives windows
    // in the order they are made, so "start centered" rarely applies), close with Escape, and have the input while open.
    private UiScreen? _window;
    // The size the screen's contents took, as last logged, to tune ScreenWidth and ScreenHeight.
    private string _loggedContentSize = "";
    // The left edge of the first line and the right edge of the widest item, in screen pixels, measured while drawing.
    private float _contentLeft;
    private float _contentRight;
    private KeyBinding? _windowKey;
    // The mouse wheel: a notch over a value steps it. The listener runs on the game's window thread, the window on the render side.
    private KeyListener? _wheelListener;
    private const uint WheelUpKey = 0x100;
    private const uint WheelDownKey = 0x101;
    // Shift and Ctrl, held while clicking - or + or turning the wheel, make one click or notch worth 10 or 100 steps.
    private const int ShiftSteps = 10;
    private const int CtrlSteps = 100;
    private static readonly uint[] ShiftKeys = { 0x10, 0xA0, 0xA1 };
    private static readonly uint[] CtrlKeys = { 0x11, 0xA2, 0xA3 };
    private volatile bool _shiftHeld;
    private volatile bool _ctrlHeld;
    // A hover older than this is from a window that is no longer drawn (closed), and the wheel goes to the game.
    private const long HoverFreshMilliseconds = 250;
    // The setting under the mouse as the window last drew it, and when; the notches waiting for the next frame and their setting.
    private volatile string? _hoveredKey;
    private long _hoveredAt;
    private string? _hoveredThisFrame;
    private volatile string? _wheelKey;
    private int _wheelNotches;
    // Each setting's description, by key: the comments of the settings file, shown as tooltips.
    private readonly Dictionary<string, string> _descriptions = new Dictionary<string, string>();
    private bool _subscribed;
    private bool _waitingForWorldLogged;
    // The settings' values at the last run of Apply (see AppliedValues).
    private string _appliedValues = "";
    // Values a click on the settings page asked for and the settings file doesn't hold yet (it is written on the world's thread).
    private readonly Dictionary<string, double> _pendingValues = new Dictionary<string, double>();
    private readonly object _pendingLock = new object();

    // What one run of Apply found and did, for the log.
    private List<GameDataEntry> _spellItems = new List<GameDataEntry>();
    // Values written by the current run (the others were already in place), and each spell item's Cast Time floor for the loaded
    // world, by the item's GUID: the casts' data is read once per world, not at every settings change.
    private int _valuesWritten;
    private bool _castMasksLogged;
    private readonly Dictionary<Guid, double> _castTimeFloorCache = new Dictionary<Guid, double>();
    // Set while a run for changed settings is waiting for the world's thread, with the keys that changed: changes made in a row
    // (a value stepped several times, a slider dragged) wait for one run.
    private readonly object _changesLock = new object();
    private readonly SortedSet<string> _pendingChanges = new SortedSet<string>(StringComparer.Ordinal);
    private bool _applyQueued;
    // Each Cast Time setting's lowest multiplier that still casts, by key, found at each apply and shown in the setting's tooltip;
    // replaced as a whole, since the window reads it on the game's render side.
    private volatile Dictionary<string, double> _castTimeFloors = new Dictionary<string, double>();
    private List<GameDataEntry> _areaTemplates = new List<GameDataEntry>();
    // The item whose icon each row of the settings window shows, by row; 0 until a world has loaded. Found on the world's thread, drawn
    // on the game's render side, so a new array replaces the old one as a whole.
    private volatile uint[] _rowIconItems = new uint[SpellCatalog.WindowSpells.Count];
    // The spell whose card the settings window shows, by its place in SpellCatalog.WindowSpells; kept while the game runs.
    private volatile int _selectedSpell;
    // Measured once per frame: a row's height on screen, a line of text's height, the room a control's value gets between its - and +
    // buttons, and the gap beside each button.
    private float _rowHeight;
    private float _textHeight;
    private float _valueWidth;
    private float _controlGap;
    // A checkbox's size as last drawn, to center the next ones in their cells; 0 until one was drawn.
    private float _checkboxWidth;
    private float _checkboxHeight;
    private Tally _tally = new Tally("");
    private int _valuesInPlace;
    private int _valuesRefused;
    private int _refusalLines;

    private sealed class Tally
    {
        public Tally(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public int InPlace;
        public int Refused;
        public string? Example;
    }

    public override void OnLoad()
    {
        _settings = Config.Register<SpellSettings>(ConfigKind.Common, null);
        if (!Content.Available || !Content.GameDataAvailable)
        {
            Log.Warn($"loaded {Context.Info.Version}, but it can't change spells here: Kiln's content and game data services are not both available");
            return;
        }
        _settings.Changed += OnSettingsChanged;
        _subscribed = true;
        // Runs at every world load, after Kiln has put the game's data in place.
        Content.OnReady(_ =>
        {
            _castTimeFloorCache.Clear();
            _castMasksLogged = false;
            Apply("world loaded");
        });
        if (Ui.Available)
        {
            ReadDescriptions();
            _window = Ui.Screen(WindowTitle, DrawWindow, new UiScreenOptions { Width = ScreenWidth, Height = ScreenHeight, DimBackground = false });
            _windowKey = KeyBindings.Register(WindowBindingId, WindowDefaultKey, () => _window?.Toggle(), "Open Settings Window", Context.Info.Name);
            _wheelListener = KeyInput.Listen(OnKeyOrWheel);
            if (!_wheelListener.Ok || !_wheelListener.Mouse)
            {
                Log.Info($"the mouse wheel can't step values here: {_wheelListener.Reason ?? "this Kiln's overlay gives keys only"}");
            }
            _settingsPage = Ui.AddConfigPage(Context.Info.Id, DrawSettingsPage);
        }
        Log.Info($"loaded {Context.Info.Version}: {SpellCatalog.Settings.Count} settings{(_window == null ? ", without its settings window (Kiln's menu is not available)" : $", {_windowKey?.Key} opens the settings window")}");
    }

    public override void OnUnload()
    {
        if (_subscribed && _settings != null)
        {
            _settings.Changed -= OnSettingsChanged;
            _subscribed = false;
        }
        _settingsPage?.Dispose();
        _settingsPage = null;
        _windowKey?.Dispose();
        _windowKey = null;
        _wheelListener?.Dispose();
        _wheelListener = null;
        _window?.Dispose();
        _window = null;
    }

    // Settings ----------------------------------------------------------------------------------------------------

    // A setting changed in the Kiln menu or in the file. Kiln's game data and settings may only be changed on the world's own thread.
    private void OnSettingsChanged(ConfigChangedEvent change)
    {
        lock (_changesLock)
        {
            _pendingChanges.UnionWith(change.Changed);
            if (_applyQueued)
            {
                return;
            }
            _applyQueued = true;
        }
        Context.Scheduler.Post(() =>
        {
            string reason;
            lock (_changesLock)
            {
                reason = "settings changed (" + string.Join(", ", _pendingChanges) + ")";
                _pendingChanges.Clear();
                _applyQueued = false;
            }
            SnapSettingsToSteps();
            // Writing a snapped value back may raise this event again, with the values already applied.
            if (AppliedValues() != _appliedValues)
            {
                Apply(reason);
            }
        });
    }

    // Every setting as it is applied, as one text: what tells a real change from an event that changes nothing.
    private string AppliedValues()
    {
        var values = new System.Text.StringBuilder();
        foreach (SettingInfo setting in SpellCatalog.Settings)
        {
            values.Append(Value(setting.Key).ToString("R", CultureInfo.InvariantCulture)).Append(';');
        }
        return values.ToString();
    }

    // Puts every number on its step in the settings file, so Kiln's free sliders land on x3.5 and not x3.3712.
    private void SnapSettingsToSteps()
    {
        ModConfig<SpellSettings>? settings = _settings;
        if (settings == null)
        {
            return;
        }
        foreach (SettingInfo setting in SpellCatalog.Settings)
        {
            if (setting.IsToggle)
            {
                continue;
            }
            double stored = StoredValue(setting);
            double snapped = SpellRules.Snap(setting, stored);
            if (!SpellRules.Same(stored, snapped))
            {
                Store(setting, snapped);
            }
        }
    }

    // A setting as the settings file holds it; a toggle is 1 for on.
    private double StoredValue(SettingInfo setting)
    {
        try
        {
            object? value = _settings?.Get(setting.Key);
            return value switch
            {
                null => setting.Default,
                bool on => on ? 1 : 0,
                _ => Convert.ToDouble(value, CultureInfo.InvariantCulture),
            };
        }
        catch (Exception error) when (error is FormatException or InvalidCastException or KeyNotFoundException or ArgumentException)
        {
            return setting.Default;
        }
    }

    // A setting as it is applied: inside its range and on its step.
    private double Value(string key)
    {
        SettingInfo setting = SpellCatalog.Setting(key);
        double own = setting.IsToggle ? StoredValue(setting) : SpellRules.Snap(setting, StoredValue(setting));
        string? generalKey = SpellCatalog.GeneralKeyFor(setting);
        if (generalKey == null)
        {
            return own;
        }
        // A per-spell multiplier, with the General card's of its kind.
        SettingInfo general = SpellCatalog.Setting(generalKey);
        return SpellRules.Combine(own, SpellRules.Snap(general, StoredValue(general)), Toggle(SpellCatalog.GeneralAtLeastKey), SpellCatalog.LowerIsBetter(setting.Group));
    }

    private bool Toggle(string key)
    {
        return Value(key) != 0;
    }

    // Writes one setting to the settings file. World thread only.
    private bool Store(SettingInfo setting, double value)
    {
        ModConfig<SpellSettings>? settings = _settings;
        if (settings == null)
        {
            return false;
        }
        object boxed = setting.Kind switch
        {
            SettingKind.Toggle => value != 0,
            SettingKind.Count => (int)Math.Round(value),
            _ => value,
        };
        try
        {
            if (settings.TrySet(setting.Key, boxed, out string error))
            {
                return true;
            }
            Log.Warn($"could not save {setting.Label}: {error}");
        }
        catch (InvalidOperationException error)
        {
            Log.Warn($"could not save {setting.Label}: {error.Message}");
        }
        return false;
    }

    private void ReadDescriptions()
    {
        foreach (System.Reflection.PropertyInfo property in typeof(SpellSettings).GetProperties())
        {
            string? key = (Attribute.GetCustomAttribute(property, typeof(ConfigKeyAttribute)) as ConfigKeyAttribute)?.Key;
            string? description = (Attribute.GetCustomAttribute(property, typeof(CommentAttribute)) as CommentAttribute)?.Text;
            if (key != null && description != null)
            {
                _descriptions[key] = description;
            }
        }
    }

    // The mod's own part of its page in the Kiln menu, under Kiln's generated sliders: the way to the settings window.
    private void DrawSettingsPage(UiDraw ui)
    {
        if (ui.Button("Open Settings Window"))
        {
            _window?.Open();
        }
        ui.SameLine();
        ui.TextDisabled($"or press {_windowKey?.Key}: the spells by element, click one to see its settings. The sliders above are free and land on the nearest step.");
    }

    // The settings window: a row of spell icons per section; clicking one opens its card below, with a line per setting. The card's
    // area keeps the height of the longest card, so the window does not change size from one spell to the next. Everything in a line
    // is as tall as the line or centered in it.
    private void DrawWindow(UiDraw ui)
    {
        _rowHeight = RowHeight * ui.Scale;
        _textHeight = ui.MeasureText("X").Height;
        // Every value gets the room of the widest one, so the + buttons of a card line up.
        _valueWidth = Math.Max(ui.MeasureText("x00.00").Width, ui.MeasureText("00 units").Width);
        _controlGap = _textHeight * 0.35f;
        float iconSize = IconButtonSize * ui.Scale;
        float sectionWidth = 0f;
        float labelWidth = 0f;
        int longestCard = 0;
        foreach (WindowSection section in SpellCatalog.WindowSections)
        {
            sectionWidth = Math.Max(sectionWidth, ui.MeasureText(section.Name).Width);
            foreach (WindowSpell spell in section.Spells)
            {
                longestCard = Math.Max(longestCard, spell.Lines.Length);
                foreach (CardLine line in spell.Lines)
                {
                    labelWidth = Math.Max(labelWidth, ui.MeasureText(line.Label).Width);
                }
            }
        }
        sectionWidth += _textHeight;
        labelWidth += _textHeight;
        uint[] rowIconItems = _rowIconItems;
        int selected = Math.Clamp(_selectedSpell, 0, SpellCatalog.WindowSpells.Count - 1);
        ApplyWheelNotches();
        _hoveredThisFrame = null;

        ui.TextWrapped("Click a spell to see its settings, scroll over a value to change it; hold Shift for 10 steps, Ctrl for 100. A dot marks a spell with a changed value.");
        _contentLeft = ui.LastItem.MinX;
        _contentRight = _contentLeft;
        int spellIndex = 0;
        foreach (WindowSection section in SpellCatalog.WindowSections)
        {
            DrawRowText(ui, section.Name, sectionWidth, iconSize, false, ui.Palette.TextMuted);
            foreach (WindowSpell spell in section.Spells)
            {
                ui.SameLine(0f, _controlGap);
                DrawSpellButton(ui, spell, spellIndex, spellIndex == selected, spellIndex < rowIconItems.Length ? rowIconItems[spellIndex] : 0, iconSize);
                spellIndex++;
            }
        }

        ui.Separator();
        WindowSpell shown = SpellCatalog.WindowSpells[selected];
        uint shownIcon = selected < rowIconItems.Length ? rowIconItems[selected] : 0;
        if (shownIcon != 0)
        {
            ui.ItemIconTrimmed(new ItemId(shownIcon), _rowHeight);
        }
        else
        {
            ui.Space(_rowHeight, _rowHeight);
        }
        ui.SameLine(0f, _controlGap);
        DrawRowText(ui, shown.Spell, ui.MeasureText(shown.Spell).Width, _rowHeight, false, null);
        foreach (CardLine line in shown.Lines)
        {
            SettingInfo setting = SpellCatalog.Setting(line.Key);
            DrawRowText(ui, line.Label, labelWidth, _rowHeight, false, null);
            Describe(ui, setting);
            ui.SameLine(0f, _controlGap);
            DrawControl(ui, line.Key);
            _contentRight = Math.Max(_contentRight, ui.LastItem.MaxX);
        }
        _hoveredKey = _hoveredThisFrame;
        Interlocked.Exchange(ref _hoveredAt, Environment.TickCount64);
        LogContentSize(ui, sectionWidth, iconSize, labelWidth);
    }

    // The room the screen's contents need, in pixels at scale 1, logged when it changes: the screen has a fixed size, and this is
    // what to give it. Width: the widest icon row or card line; height: where the last line ended.
    private void LogContentSize(UiDraw ui, float sectionWidth, float iconSize, float labelWidth)
    {
        // The window's room: its contents' widest item plus the same margin on the right as on the left; down to where the last
        // line ended, plus the top margin again.
        var window = ui.WindowRect;
        float scale = Math.Max(ui.Scale, 0.01f);
        float margin = Math.Max(0f, _contentLeft - window.X);
        UiCursor cursor = ui.Cursor;
        string size = string.Create(CultureInfo.InvariantCulture,
            $"{(_contentRight - window.X + margin) / scale:0} x {(cursor.Y + cursor.ScrollY + margin) / scale:0} needed, screen {ScreenWidth:0} x {ScreenHeight:0} (scale {ui.Scale:0.##})");
        if (size != _loggedContentSize)
        {
            _loggedContentSize = size;
            Log.Info("settings screen contents: " + size);
        }
    }

    // A notch of the mouse wheel over a value of the settings window steps it: up raises, down lowers. Runs on the game's window
    // thread before the game sees the notch, so it only counts the notch for the next frame, and keeps it from the game (the camera)
    // when it was used. Everything else goes on to the game.
    private bool OnKeyOrWheel(KeyPress press)
    {
        // Shift and Ctrl are followed for the clicks; the game gets them as always.
        if (Array.IndexOf(ShiftKeys, press.VirtualKey) >= 0)
        {
            _shiftHeld = press.Down;
            return false;
        }
        if (Array.IndexOf(CtrlKeys, press.VirtualKey) >= 0)
        {
            _ctrlHeld = press.Down;
            return false;
        }
        if (!press.Down || (press.VirtualKey != WheelUpKey && press.VirtualKey != WheelDownKey))
        {
            return false;
        }
        string? key = _hoveredKey;
        if (key == null || Environment.TickCount64 - Interlocked.Read(ref _hoveredAt) > HoverFreshMilliseconds)
        {
            return false;
        }
        if (_wheelKey != key)
        {
            // A notch over another value: the notches still waiting were for the previous one.
            Interlocked.Exchange(ref _wheelNotches, 0);
            _wheelKey = key;
        }
        int steps = StepsFor((press.Mods & KeyMods.Ctrl) != 0 || _ctrlHeld, (press.Mods & KeyMods.Shift) != 0 || _shiftHeld);
        Interlocked.Add(ref _wheelNotches, press.VirtualKey == WheelUpKey ? steps : -steps);
        return true;
    }

    // How many steps one click or notch is worth: Ctrl 100, Shift 10, else 1.
    private static int StepsFor(bool ctrl, bool shift)
    {
        return ctrl ? CtrlSteps : shift ? ShiftSteps : 1;
    }

    // The notches counted since the last frame, as steps of the setting they were for.
    private void ApplyWheelNotches()
    {
        int notches = Interlocked.Exchange(ref _wheelNotches, 0);
        string? key = _wheelKey;
        if (notches == 0 || key == null)
        {
            return;
        }
        SettingInfo setting = SpellCatalog.Setting(key);
        if (!setting.IsToggle)
        {
            Ask(setting, SpellRules.Stepped(setting, ShownValue(setting), notches));
        }
    }

    // A spell's icon as a button: clicking it shows its card. The chosen spell is framed, and a dot marks a spell with a value changed
    // from its default. Before a world has loaded there is no icon to show, so the button shows the spell's initials.
    private void DrawSpellButton(UiDraw ui, WindowSpell spell, int spellIndex, bool selected, uint iconItem, float size)
    {
        string id = "##spell" + spellIndex.ToString(CultureInfo.InvariantCulture);
        UiImage? icon = iconItem != 0 ? Ui.ItemImage(new ItemId(iconItem)) : null;
        string text = spell.IconItem == null ? SpellCatalog.GeneralButtonText : Initials(spell.Spell);
        bool clicked = icon != null ? ui.ImageButtonTrimmed(id, icon, size, size) : ui.Button(text + id, size, size);
        if (clicked)
        {
            _selectedSpell = spellIndex;
        }
        ui.Tooltip(spell.Spell);
        UiItem button = ui.LastItem;
        _contentRight = Math.Max(_contentRight, button.MaxX);
        float edge = 2f * ui.Scale;
        if (selected)
        {
            ui.DrawRect(UiLayer.Window, button.MinX - edge, button.MinY - edge, button.MaxX + edge, button.MaxY + edge, ui.Palette.Accent, 3f * ui.Scale, 1.5f * ui.Scale);
        }
        if (IsChanged(spell))
        {
            float dot = 8f * ui.Scale;
            // On the window's own layer: the plain DrawRect draws over everything, tooltips included.
            ui.DrawRect(UiLayer.Window, button.MaxX - dot + edge, button.MinY - edge, button.MaxX + edge, button.MinY - edge + dot, ui.Palette.Accent, dot / 2f);
        }
    }

    private bool IsChanged(WindowSpell spell)
    {
        foreach (CardLine line in spell.Lines)
        {
            SettingInfo setting = SpellCatalog.Setting(line.Key);
            if (!SpellRules.Same(ShownValue(setting), setting.Default))
            {
                return true;
            }
        }
        return false;
    }

    private static string Initials(string name)
    {
        var initials = new System.Text.StringBuilder();
        foreach (string word in name.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            initials.Append(char.ToUpperInvariant(word[0]));
        }
        return initials.ToString();
    }

    // A text in the middle of a line's height: a slot as tall as the line takes its place, and the text is drawn into it. A text
    // widget would sit at the top of the line, or lower by an amount that depends on what was drawn before it.
    private void DrawRowText(UiDraw ui, string text, float slotWidth, float slotHeight, bool centered, Color? color)
    {
        ui.Space(slotWidth, slotHeight);
        UiItem slot = ui.LastItem;
        float left = centered ? slot.MinX + (float)Math.Floor(Math.Max(0f, slotWidth - ui.MeasureText(text).Width) / 2f) : slot.MinX;
        float top = slot.MinY + (float)Math.Floor((slotHeight - _textHeight) / 2f);
        // On the window's own layer, so other windows and tooltips cover it; the plain DrawText draws over everything.
        ui.DrawText(UiLayer.Window, left, top, text, color ?? ui.Palette.Text);
    }

    // One setting on the current line: a step down, the value, a step up. A toggle is a checkbox in the middle of that room.
    private void DrawControl(UiDraw ui, string key)
    {
        SettingInfo setting = SpellCatalog.Setting(key);
        double shown = ShownValue(setting);
        if (key == SpellCatalog.GeneralAtLeastKey)
        {
            DrawModeSwitch(ui, setting, shown != 0);
            return;
        }
        // The same labels repeat on every card, so each setting's widgets get the setting's key as their id.
        using (ui.Id(key))
        {
            if (setting.IsToggle)
            {
                // A checkbox has its own size, known once one was drawn: from then on it is moved to the middle of the room a
                // "- value +" control takes, across and down.
                if (_checkboxHeight > 0)
                {
                    UiCursor cursor = ui.Cursor;
                    float controlWidth = 2f * _rowHeight + 2f * _controlGap + _valueWidth;
                    ui.SetCursor(cursor.X + (float)Math.Floor(Math.Max(0f, controlWidth - _checkboxWidth) / 2f),
                        cursor.Y + (float)Math.Floor(Math.Max(0f, _rowHeight - _checkboxHeight) / 2f));
                }
                bool on = shown != 0;
                if (ui.Checkbox("##toggle", ref on))
                {
                    Ask(setting, on ? 1 : 0);
                }
                UiItem checkbox = ui.LastItem;
                _checkboxWidth = checkbox.Width;
                _checkboxHeight = checkbox.Height;
                Describe(ui, setting);
                return;
            }
            // Square buttons of the line's height: the overlay centers a button's label in a button of a given size.
            int steps = StepsFor(_ctrlHeld, _shiftHeld);
            if (ui.Button("-", _rowHeight, _rowHeight))
            {
                Ask(setting, SpellRules.Stepped(setting, shown, -steps));
            }
            bool hovered = ui.LastItem.Hovered;
            ui.SameLine(0f, _controlGap);
            DrawRowText(ui, setting.Format(shown), _valueWidth, _rowHeight, true, SpellRules.Same(shown, setting.Default) ? null : ui.Palette.Accent);
            hovered |= ui.LastItem.Hovered;
            Describe(ui, setting);
            ui.SameLine(0f, _controlGap);
            if (ui.Button("+", _rowHeight, _rowHeight))
            {
                Ask(setting, SpellRules.Stepped(setting, shown, steps));
            }
            hovered |= ui.LastItem.Hovered;
            if (hovered)
            {
                _hoveredThisFrame = key;
            }
        }
    }

    // The General card's mode as two buttons side by side, the chosen one in the accent color: Kiln has no segmented control.
    private void DrawModeSwitch(UiDraw ui, SettingInfo setting, bool atLeast)
    {
        // As wide as the longer label plus the button's own margin, so neither is cut.
        float width = (float)Math.Ceiling(Math.Max(ui.MeasureText("Multiply").Width, ui.MeasureText("At Least").Width) + 1.2f * _textHeight);
        using (ui.Id(setting.Key))
        {
            if (ModeButton(ui, "Multiply", !atLeast, width))
            {
                Ask(setting, 0);
            }
            Describe(ui, setting);
            ui.SameLine(0f, _controlGap);
            if (ModeButton(ui, "At Least", atLeast, width))
            {
                Ask(setting, 1);
            }
            Describe(ui, setting);
        }
    }

    private bool ModeButton(UiDraw ui, string label, bool chosen, float width)
    {
        if (!chosen)
        {
            return ui.Button(label, width, _rowHeight);
        }
        using (ui.PushColor(UiColorSlot.Button, ui.Palette.Accent))
        {
            return ui.Button(label, width, _rowHeight);
        }
    }

    // The setting's description and default, while the widget drawn last is hovered.
    private void Describe(UiDraw ui, SettingInfo setting)
    {
        string description = _descriptions.TryGetValue(setting.Key, out string? text) ? text : setting.Label;
        string floor = _castTimeFloors.TryGetValue(setting.Key, out double lowest) && lowest > SpellRules.MinCastTime
            ? $" Below {setting.Format(lowest)}, the cast lock stays at {setting.Format(lowest)} so the spell still goes out; Charge Time follows the setting."
            : "";
        ui.Tooltip($"{description} Default: {setting.Format(setting.Default)}.{floor}");
    }

    // What a row shows: the value a click asked for until the settings file holds it, then the file's.
    private double ShownValue(SettingInfo setting)
    {
        double stored = setting.IsToggle ? StoredValue(setting) : SpellRules.Snap(setting, StoredValue(setting));
        lock (_pendingLock)
        {
            if (_pendingValues.TryGetValue(setting.Key, out double pending))
            {
                if (!SpellRules.Same(pending, stored))
                {
                    return pending;
                }
                _pendingValues.Remove(setting.Key);
            }
        }
        return stored;
    }

    // A click on the settings page. The page is drawn on the game's render side, the settings file is written on the world's thread.
    private void Ask(SettingInfo setting, double value)
    {
        lock (_pendingLock)
        {
            _pendingValues[setting.Key] = value;
        }
        Context.Scheduler.Post(() =>
        {
            bool stored = Store(setting, value);
            if (!stored)
            {
                lock (_pendingLock)
                {
                    _pendingValues.Remove(setting.Key);
                }
            }
        });
    }

    // Applying ----------------------------------------------------------------------------------------------------

    private void Apply(string reason)
    {
        if (_settings == null)
        {
            return;
        }
        if (!Content.Ready)
        {
            if (!_waitingForWorldLogged)
            {
                _waitingForWorldLogged = true;
                Log.Info($"{reason}: no world is loaded, the settings apply when one is");
            }
            return;
        }
        _waitingForWorldLogged = false;
        _appliedValues = AppliedValues();
        try
        {
            ApplyAll(reason);
        }
        catch (Exception error)
        {
            Log.Error($"{reason}: stopped by {error.GetType().Name}: {error.Message}");
        }
    }

    private void ApplyAll(string reason)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        _valuesInPlace = 0;
        _valuesWritten = 0;
        _valuesRefused = 0;
        _refusalLines = 0;
        FindSpellData();
        FindRowIcons();
        Log.Info($"{reason}: {_spellItems.Count} spell items and {_areaTemplates.Count} area and ball templates found");

        ApplySummons();
        ApplyDurations();
        ApplyDamage();
        ApplyRadii();
        ApplyBallLightning();
        ApplyLockOn();
        ApplyChainRadius();
        ApplySummonCaps();
        ApplyCost();
        ApplyCastTime();
        ApplyItemSpells();
        ApplyKeepOlderWisps();
        ApplyHealth();
        Log.Info($"{_valuesInPlace} values in place ({_valuesWritten} written), {_valuesRefused} refused, in {watch.ElapsedMilliseconds} ms");
    }

    // The spell items, and the templates found by name (areas and balls). Looked up at every run: a world load gives new entries.
    private void FindSpellData()
    {
        _spellItems = new List<GameDataEntry>();
        foreach (GameDataEntry item in Content.ItemData)
        {
            if (SpellCatalog.AnySpellItem.IsMatch(item.Name) || SpellCatalog.ItemSpells.Any(spell => spell.Items.IsMatch(item.Name)))
            {
                _spellItems.Add(item);
            }
        }
        var templatePatterns = new List<Regex> { SpellCatalog.BallTemplate };
        foreach (RadiusSpell spell in SpellCatalog.RadiusSpells)
        {
            foreach (AreaTemplates templates in spell.Templates)
            {
                templatePatterns.Add(templates.TemplateName);
            }
        }
        _areaTemplates = new List<GameDataEntry>();
        foreach (GameDataEntry template in Content.Templates)
        {
            string name = template.Name;
            // Every pattern's names start with one of these three words; the game has over 12,000 templates.
            if (!name.StartsWith("Explosion_", StringComparison.Ordinal) && !name.StartsWith("Zone_", StringComparison.Ordinal) && !name.StartsWith("Projectile_", StringComparison.Ordinal))
            {
                continue;
            }
            foreach (Regex pattern in templatePatterns)
            {
                if (pattern.IsMatch(name))
                {
                    _areaTemplates.Add(template);
                    break;
                }
            }
        }
    }

    // The icon of each spell of the settings window: the first of the spell's items by name, so the lowest tier.
    private void FindRowIcons()
    {
        var rowIconItems = new uint[SpellCatalog.WindowSpells.Count];
        for (int rowIndex = 0; rowIndex < rowIconItems.Length; rowIndex++)
        {
            GameDataEntry? first = null;
            Regex? iconItem = SpellCatalog.WindowSpells[rowIndex].IconItem;
            if (iconItem == null)
            {
                continue;
            }
            foreach (GameDataEntry item in Items(iconItem))
            {
                if (first == null || string.CompareOrdinal(item.Name, first.Name) < 0)
                {
                    first = item;
                }
            }
            rowIconItems[rowIndex] = first?.Id ?? 0;
        }
        _rowIconItems = rowIconItems;
    }

    private List<GameDataEntry> Items(Regex? itemName)
    {
        if (itemName == null)
        {
            return new List<GameDataEntry>();
        }
        return _spellItems.FindAll(item => itemName.IsMatch(item.Name));
    }

    private List<GameDataEntry> Templates(Regex templateName)
    {
        return _areaTemplates.FindAll(template => templateName.IsMatch(template.Name));
    }

    // Skeletons and Totems Per Cast and Totem Spread: the counts and the spread of each summon cast's spawn event. Skeletons walk off by
    // themselves, so they get a fixed spread that only keeps them from landing on one spot.
    private void ApplySummons()
    {
        foreach (SummonCast cast in SpellCatalog.SummonCasts)
        {
            SettingInfo setting = SpellCatalog.Setting(cast.CountKey);
            double count = Value(cast.CountKey);
            // Totems stand where they land, so they get Totem Spread; skeletons and skull vessels move off by themselves: 1 unit.
            double spread = cast.CountKey == SpellCatalog.TotemsPerCastKey ? Value(SpellCatalog.SummonSpreadKey) : SpellRules.SkeletonSpreadUnits;
            Begin($"{cast.SequenceName}: {setting.Format(count)} per cast, spread {SpellCatalog.Setting(SpellCatalog.SummonSpreadKey).Format(spread)}");
            GameDataEntry? sequence = Content.Sequences.Get(cast.SequenceName);
            if (sequence == null)
            {
                Note("its cast is not in this world's data");
            }
            else
            {
                // At one per cast the game's own counts and spread come back. The maximum goes first, so the minimum is never above it.
                for (int eventIndex = 0; eventIndex < cast.Events; eventIndex++)
                {
                    string spawnEvent = string.Create(CultureInfo.InvariantCulture, $"events[{cast.EventType}#{eventIndex}]");
                    PutNumber(sequence, spawnEvent + ".maxSpawnCount", game => count > 1 ? count : game, whole: true);
                    PutNumber(sequence, spawnEvent + ".minSpawnCount", game => count > 1 ? count : game, whole: true);
                    PutNumber(sequence, spawnEvent + ".randomSpawnRadius.maxRadius", game => count > 1 ? spread : game);
                }
            }
            End();
        }
    }

    // Duration: the tooltip's duration line, the lifetime of what the spell places, a zone's debuff where it is the spell's duration, and
    // the totems' combat flags.
    private void ApplyDurations()
    {
        bool invulnerable = Toggle(SpellCatalog.InvulnerableTotemsKey);
        bool ignored = ApplyTotemRelations();
        foreach (DurationSpell spell in SpellCatalog.DurationSpells)
        {
            SettingInfo setting = SpellCatalog.Setting(spell.SettingKey);
            double multiplier = Value(spell.SettingKey);
            List<GameDataEntry> items = Items(spell.ItemName);
            Begin($"{setting.Label} {setting.Format(multiplier)}{(spell.IsTotem ? ", invulnerable totems " + (invulnerable ? "on" : "off") + ", enemies ignore totems " + (ignored ? "on" : "off") : "")}: {items.Count} items");
            var seenTemplates = new HashSet<Guid>();
            foreach (GameDataEntry item in items)
            {
                double tooltipSeconds = PutTooltip(item, DurationTooltipLocaId, multiplier) / 1000.0;
                GameDataEntry? template = PlacedTemplate(item);
                if (template == null || !seenTemplates.Add(template.Guid))
                {
                    continue;
                }
                PutTime(template, LifetimePath, game => SpellRules.LifetimeSeconds(game, tooltipSeconds, multiplier));
                if (spell.ScalesDebuffDuration && Has(template, DebuffDurationPath))
                {
                    PutNumber(template, DebuffDurationPath, game => SpellRules.Multiplied(game, multiplier));
                }
                if (spell.IsTotem)
                {
                    PutFlag(template, ImmunePath, invulnerable, true);
                    PutFlag(template, HandleHitsPath, invulnerable, false);
                    PutName(template, FactionPath, ignored, TotemFaction.ToString());
                }
            }
            End($"{seenTemplates.Count} placed templates");
        }
    }

    // Enemies Ignore Totems, the world's side of it: every enemy faction neutral toward the totems' faction, through Kiln's creature AI
    // service, which answers the game's "would a fight b" check that enemies ask about each target candidate. One way only: the totems'
    // side stays the game's. Kiln drops the table when a world stops, so this runs at every world load. Returns whether totems are
    // ignored, so their faction is only changed when the relations are in place.
    private bool ApplyTotemRelations()
    {
        bool wanted = Toggle(SpellCatalog.EnemiesIgnoreTotemsKey);
        if (!Factions.Available)
        {
            if (wanted)
            {
                Note("Enemies Ignore Totems is on, but Kiln's creature AI service is not available here: totems stay targets");
            }
            return false;
        }
        try
        {
            foreach (Faction enemy in EnemyFactions)
            {
                Factions.Relate(enemy, TotemFaction, wanted ? Relation.Neutral : Relation.Game, mutual: false);
            }
            return wanted;
        }
        catch (Exception error) when (IsRefusal(error) || error is NotSupportedException)
        {
            Note($"Enemies Ignore Totems: Kiln refused the factions' relations ({error.Message}): totems stay targets");
            return false;
        }
    }

    // Damage: the spell item's damage modifier, which the game multiplies into the spell's share of a cast's damage.
    private void ApplyDamage()
    {
        foreach (DamageSpell spell in SpellCatalog.DamageSpells)
        {
            SettingInfo setting = SpellCatalog.Setting(spell.SettingKey);
            double multiplier = Value(spell.SettingKey);
            List<GameDataEntry> items = Items(spell.ItemName);
            Begin($"{setting.Label} {setting.Format(multiplier)}: {items.Count} items");
            foreach (GameDataEntry item in items)
            {
                PutNumber(item, DamageModifierPath, game => SpellRules.Multiplied(game, multiplier));
            }
            End();
        }
    }

    // Radius: the colliders of the templates that hold the spell's area, with what follows the area (a zone's VFX size, Root's reach, the
    // explosion's scale while its Visual toggle is on), and on the items the tooltip's radius line and the ground cursor.
    private void ApplyRadii()
    {
        foreach (RadiusSpell spell in SpellCatalog.RadiusSpells)
        {
            SettingInfo setting = SpellCatalog.Setting(spell.SettingKey);
            double multiplier = Value(spell.SettingKey);
            bool visual = spell.VisualKey != null && Toggle(spell.VisualKey);
            List<GameDataEntry> items = Items(spell.ItemName);
            Begin($"{setting.Label} {setting.Format(multiplier)}{(spell.VisualKey == null ? "" : ", visual " + (visual ? "on" : "off"))}: {items.Count} items");
            foreach (GameDataEntry item in items)
            {
                PutTooltip(item, RadiusTooltipLocaId, multiplier);
                if (!spell.KeepCursorRange && Has(item, CursorRangePath))
                {
                    PutNumber(item, CursorRangePath, game => SpellRules.Multiplied(game, multiplier));
                }
            }
            int templateCount = 0;
            foreach (AreaTemplates area in spell.Templates)
            {
                foreach (GameDataEntry template in Templates(area.TemplateName))
                {
                    templateCount++;
                    ApplyArea(template, area, multiplier, spell.VisualKey != null, visual);
                }
            }
            End($"{templateCount} templates");
        }
    }

    private void ApplyArea(GameDataEntry template, AreaTemplates area, double multiplier, bool hasVisualToggle, bool visual)
    {
        List<string> radiusPaths = ColliderRadiusPaths(template);
        if (radiusPaths.Count == 0)
        {
            Note($"{template.Name} has no collider with a radius, left alone");
            return;
        }
        // The first collider is the area; it says how many times the template grows, and the rest of the template grows as much.
        double gameRadius = Original(template, radiusPaths[0]);
        double factor = SpellRules.AreaFactor(gameRadius, multiplier, area.SizedToMeters);
        foreach (string radiusPath in radiusPaths)
        {
            PutNumber(template, radiusPath, game => SpellRules.Multiplied(game, factor));
        }
        if (area.VfxSizeIndex >= 0)
        {
            string sizePath = string.Create(CultureInfo.InvariantCulture, $"{VfxParametersPath}[{area.VfxSizeIndex}]");
            // The zone's look follows only where that word really holds the area's radius.
            if (Has(template, sizePath) && SpellRules.Same(SpellRules.FloatFromBits((long)Original(template, sizePath)), gameRadius))
            {
                PutNumber(template, sizePath, bits => SpellRules.BitsFromFloat((float)(SpellRules.FloatFromBits((long)bits) * factor)), whole: true);
            }
            else
            {
                Note($"{template.Name}: VFX word {area.VfxSizeIndex} does not hold its {gameRadius.ToString("0.##", CultureInfo.InvariantCulture)} m area, its look is left alone");
            }
        }
        if (area.HasReach && Has(template, ReachPath))
        {
            PutNumber(template, ReachPath, game => SpellRules.Multiplied(game, factor));
        }
        if (!hasVisualToggle)
        {
            return;
        }
        foreach (string transformPath in TransformPaths)
        {
            foreach (string axis in ScaleAxes)
            {
                string scalePath = transformPath + ".scale." + axis;
                if (Has(template, scalePath))
                {
                    PutNumber(template, scalePath, game => visual ? SpellRules.Multiplied(game, factor) : game);
                }
            }
        }
    }

    // Ball Lightning: the speed its cast gives the ball, the area it hits enemies in (the larger of the ball's two spheres; the small one is
    // the ball itself, against the world), and the wait between two hits.
    private void ApplyBallLightning()
    {
        double speed = Value(SpellCatalog.BallSpeedKey);
        double radius = Value(SpellCatalog.BallRadiusKey);
        double hitRate = Value(SpellCatalog.BallHitRateKey);
        List<GameDataEntry> balls = Templates(SpellCatalog.BallTemplate);
        Begin($"Ball Lightning speed x{speed:0.##}, radius x{radius:0.##}, hit rate x{hitRate:0.##}: {balls.Count} templates");
        GameDataEntry? cast = Content.Sequences.Get(SpellCatalog.BallCastSequence);
        if (cast == null)
        {
            Note("Ball Lightning's cast is not in this world's data");
        }
        else
        {
            PutNumber(cast, SpawnSpeedPath, game => SpellRules.Multiplied(game, speed));
        }
        foreach (GameDataEntry ball in balls)
        {
            string? areaPath = null;
            double areaRadius = 0;
            foreach (string radiusPath in ColliderRadiusPaths(ball))
            {
                double gameRadius = Original(ball, radiusPath);
                if (gameRadius > areaRadius)
                {
                    areaPath = radiusPath;
                    areaRadius = gameRadius;
                }
            }
            if (areaPath == null)
            {
                Note($"{ball.Name} has no collider with a radius, left alone");
                continue;
            }
            PutNumber(ball, areaPath, game => SpellRules.Multiplied(game, radius));
            if (Has(ball, HitWaitPath))
            {
                PutNumber(ball, HitWaitPath, game => SpellRules.Divided(game, hitRate));
            }
        }
        End();
    }

    // Mana Cost: the ManaCost config of each card's items (whole, never below 1) and its tooltip line.
    private void ApplyCost()
    {
        foreach (WindowSpell spell in SpellCatalog.WindowSpells)
        {
            if (spell.IconItem == null)
            {
                continue;
            }
            string key = SpellCatalog.CostKey(spell.Spell);
            SettingInfo setting = SpellCatalog.Setting(key);
            double multiplier = Value(key);
            List<GameDataEntry> items = Items(spell.IconItem);
            Begin($"{setting.Label} {setting.Format(multiplier)}: {items.Count} items");
            foreach (GameDataEntry item in items)
            {
                if (!Has(item, SpellCatalog.ManaCostPath))
                {
                    continue;
                }
                PutNumber(item, SpellCatalog.ManaCostPath, game => SpellRules.ManaCost((long)Math.Round(game), multiplier), whole: true);
                PutTooltip(item, SpellCatalog.ManaCostTooltipLocaId, multiplier);
            }
            End();
        }
    }

    // The Items row: Damage on the items' damage modifier; Duration on the items' time value (config 0x663d391d, which the summoning
    // scripts read as the summon's lifetime), its tooltip line, and the summoned template's LifeTime; Health on the summoned template,
    // as for the summon spells. Their Mana Cost is ApplyCost's.
    private void ApplyItemSpells()
    {
        foreach (ItemSpell spell in SpellCatalog.ItemSpells)
        {
            List<GameDataEntry> items = Items(spell.Items);
            double damage = Value(spell.DamageKey);
            double duration = spell.DurationKey == null ? 1 : Value(spell.DurationKey);
            double health = spell.HealthKey == null ? 1 : Value(spell.HealthKey);
            Begin(string.Create(CultureInfo.InvariantCulture, $"{spell.Name}: damage x{damage:0.##}, duration x{duration:0.##}, health x{health:0.##}, {items.Count} items"));
            var seen = new HashSet<Guid>();
            foreach (GameDataEntry item in items)
            {
                PutNumber(item, DamageModifierPath, game => SpellRules.Multiplied(game, damage));
                if (spell.DurationKey != null)
                {
                    if (Has(item, ItemLifetimeConfigPath))
                    {
                        PutTime(item, ItemLifetimeConfigPath, game => SpellRules.Multiplied(game, duration));
                    }
                    PutTooltip(item, DurationTooltipLocaId, duration);
                }
                GameDataEntry? summon = SummonedTemplate(item, spell);
                if (summon == null || !seen.Add(summon.Guid))
                {
                    continue;
                }
                if (spell.DurationKey != null && Has(summon, LifetimePath))
                {
                    PutTime(summon, LifetimePath, game => SpellRules.Multiplied(game, duration));
                }
                if (spell.HealthKey != null)
                {
                    PutHealth(summon, health);
                }
            }
            End($"{seen.Count} summoned templates");
        }
    }

    // What an item summons: the template its TemplateImpactConfig 0xf6a785cd names, else the card's fixed one (the Fire Wisp scroll's
    // cast spawns it). Null for an item that summons nothing (the meteor shower).
    private GameDataEntry? SummonedTemplate(GameDataEntry item, ItemSpell spell)
    {
        try
        {
            if (Has(item, SpawnTemplateConfigPath))
            {
                ContentValue reference = item.Get(SpawnTemplateConfigPath);
                GameDataEntry? template = reference.Guid != Guid.Empty ? Content.Templates.Get(reference.Guid) : null;
                if (template == null && !string.IsNullOrEmpty(reference.Name))
                {
                    template = Content.Templates.Get(reference.Name);
                }
                if (template != null)
                {
                    return template;
                }
            }
        }
        catch (Exception error) when (IsRefusal(error))
        {
            Refuse(item, SpawnTemplateConfigPath, error.Message);
        }
        return spell.FixedTemplate == null ? null : Content.Templates.Get(spell.FixedTemplate);
    }

    // A summon's health: its base maximum (and starting health) where the template holds one, else its level scaling factor.
    private void PutHealth(GameDataEntry template, double multiplier)
    {
        if (Has(template, HealthMaxBasePath) && Original(template, HealthMaxBasePath) > 0)
        {
            PutNumber(template, HealthMaxBasePath, game => SpellRules.Multiplied(game, multiplier), whole: true);
            if (Has(template, HealthPath) && Original(template, HealthPath) > 0)
            {
                PutNumber(template, HealthPath, game => SpellRules.Multiplied(game, multiplier), whole: true);
            }
        }
        else if (Has(template, HealthScaleFactorPath))
        {
            PutNumber(template, HealthScaleFactorPath, game => SpellRules.Multiplied(game, multiplier));
        }
        else
        {
            Note($"{template.Name} has no health to change");
        }
    }

    // Keep Older Wisps: the Lightning Wisp's script removes the caster's wisps its sphere (20 units around the caster) touches before it
    // spawns the new one. While on, the sphere is made tiny (it still touches the caster, whose collision lets the script go on to
    // the spawn), with a new physics shape key as for Chain Lightning, so no older wisp is in it.
    private void ApplyKeepOlderWisps()
    {
        bool keep = Toggle(SpellCatalog.KeepOlderWispsKey);
        Begin($"Keep Older Wisps {(keep ? "on" : "off")}");
        GameDataEntry? cast = Content.Sequences.Get(SpellCatalog.WispCastSequence);
        if (cast == null)
        {
            Note("Lightning Wisp's cast is not in this world's data");
            End();
            return;
        }
        for (int eventIndex = 0; eventIndex < SpellCatalog.WispCastEvents; eventIndex++)
        {
            string collider = string.Create(CultureInfo.InvariantCulture, $"events[SpawnImpact#{eventIndex}].colliderData");
            string radiusPath = collider + ".dataArray[0].shape.radius";
            if (!Has(cast, radiusPath))
            {
                continue;
            }
            double gameRadius = Original(cast, radiusPath);
            PutNumber(cast, radiusPath, game => keep ? SpellRules.KeptWispSphereRadius : game);
            string? keyPath = NumberPath(cast, new[] { collider + ".joltObjects[0].hash", collider + ".joltObjects[0].hash.value" });
            if (keyPath != null && gameRadius > 0)
            {
                PutNumber(cast, keyPath, game => keep ? SpellRules.ShapeKey((uint)game, SpellRules.KeptWispSphereRadius / gameRadius) : game, whole: true);
            }
        }
        End();
    }

    // Cast Time: each card's items' Charge Time (config 0x579b2657, what the tooltip calls it) with its tooltip line, and the cast
    // lock (0x41a7d373). The old Summon Recast Time changed the same values: the spell is ready again sooner; the cast's animation
    // keeps its length (the user chose this over moving the cast's events). Charge Time takes the setting all the way down; only the
    // cast lock stops at the lowest multiplier its cast still goes out at (CastTimeFloor): below it the cast ended before the spell
    // was released (Chain Lightning under x0.55, Lightning Wisp under x0.3, reported by players). The user chose this over holding
    // the whole setting at the floor (1.0.1 to 1.0.3).
    private void ApplyCastTime()
    {
        var floors = new Dictionary<string, double>();
        foreach (string name in SpellCatalog.CastTimeSpellNames)
        {
            WindowSpell? spell = null;
            foreach (WindowSpell candidate in SpellCatalog.WindowSpells)
            {
                if (candidate.Spell == name)
                {
                    spell = candidate;
                }
            }
            if (spell?.IconItem == null)
            {
                continue;
            }
            string key = SpellCatalog.CastTimeKey(name);
            SettingInfo setting = SpellCatalog.Setting(key);
            List<GameDataEntry> items = Items(spell.IconItem);
            double floor = SpellRules.MinCastTime;
            foreach (GameDataEntry item in items)
            {
                floor = Math.Max(floor, CastTimeFloor(item));
            }
            floors[key] = floor;
            double multiplier = Value(key);
            double lockMultiplier = Math.Max(multiplier, floor);
            string limit = lockMultiplier > multiplier ? $", cast lock held at {setting.Format(lockMultiplier)}" : floor > SpellRules.MinCastTime ? $" (cast lock floor {setting.Format(floor)})" : "";
            Begin($"{setting.Label} {setting.Format(multiplier)}{limit}: {items.Count} items");
            foreach (GameDataEntry item in items)
            {
                if (Has(item, SpellCatalog.ChargeTimePath))
                {
                    PutNumber(item, SpellCatalog.ChargeTimePath, game => SpellRules.Multiplied(game, multiplier));
                    PutTooltip(item, SpellCatalog.ChargeTimeTooltipLocaId, multiplier);
                }
                if (Has(item, SpellCatalog.CastLockPath))
                {
                    PutNumber(item, SpellCatalog.CastLockPath, game => SpellRules.Multiplied(game, lockMultiplier));
                }
            }
            End();
        }
        _castTimeFloors = floors;
    }

    // The lowest Cast Time multiplier an item's cast still goes out at (SpellRules.MinCastTimeMultiplier), from the game's own cast
    // lock and the events of each cast the item triggers; the setting's own minimum when nothing limits it or the data can't be read.
    private double CastTimeFloor(GameDataEntry item)
    {
        if (_castTimeFloorCache.TryGetValue(item.Guid, out double known))
        {
            return known;
        }
        double floor = FindCastTimeFloor(item);
        _castTimeFloorCache[item.Guid] = floor;
        return floor;
    }

    private double FindCastTimeFloor(GameDataEntry item)
    {
        double floor = SpellRules.MinCastTime;
        if (!Has(item, SpellCatalog.CastLockPath))
        {
            return floor;
        }
        double castLock = Original(item, SpellCatalog.CastLockPath);
        for (int index = 0; index < MaxListScan; index++)
        {
            string path = string.Create(CultureInfo.InvariantCulture, $"sequences[{index}].sequence");
            if (!Has(item, path))
            {
                break;
            }
            GameDataEntry? cast = null;
            try
            {
                ContentValue reference = item.Get(path);
                cast = reference.Guid != Guid.Empty ? Content.Sequences.Get(reference.Guid) : null;
                if (cast == null && !string.IsNullOrEmpty(reference.Name))
                {
                    cast = Content.Sequences.Get(reference.Name);
                }
            }
            catch (Exception error) when (IsRefusal(error))
            {
                Refuse(item, path, error.Message);
            }
            if (cast != null && !_castMasksLogged)
            {
                // Once per world: how this Kiln gives a cast's keep-alive masks, to check NeedsShooting against.
                _castMasksLogged = true;
                var masks = new List<string>();
                for (int keepAlive = 0; keepAlive < MaxListScan; keepAlive++)
                {
                    string time = string.Create(CultureInfo.InvariantCulture, $"events[KeepAliveEvent#{keepAlive}].time");
                    if (!Has(cast, time))
                    {
                        break;
                    }
                    string maskPath = string.Create(CultureInfo.InvariantCulture, $"events[KeepAliveEvent#{keepAlive}].keepAliveMask");
                    System.Text.Json.Nodes.JsonNode? mask = cast.GetJson(maskPath);
                    // The set flags only: Kiln's description also lists every flag the type has.
                    System.Text.Json.Nodes.JsonNode? set = (mask as System.Text.Json.Nodes.JsonObject)?["value"] ?? mask;
                    masks.Add($"{Original(cast, time):0.###} s {set?.ToJsonString() ?? "none"} shooting {SpellRules.NeedsShooting(mask)}");
                }
                Log.Info($"{cast.Name} keep-alive masks: {string.Join("; ", masks)}");
            }
            (double ShootingFrom, double ReleaseAt)? window = cast == null ? null : ShootingWindow(cast);
            if (window.HasValue)
            {
                floor = Math.Max(floor, SpellRules.MinCastTimeMultiplier(window.Value.ShootingFrom, window.Value.ReleaseAt, castLock));
            }
        }
        return floor;
    }

    // When a cast's shooting part starts (its first KeepAliveEvent that needs InRangeCombatShooting) and when it releases the spell
    // after that: the later of its first spawn (SpawnImpact or SpawnEntityEvent) and its first cost, so the spell goes out and is paid
    // for. Null for a cast without a shooting part, or that releases nothing in it (thrown items, channels).
    private static (double ShootingFrom, double ReleaseAt)? ShootingWindow(GameDataEntry cast)
    {
        double? shootingFrom = null;
        for (int index = 0; index < MaxListScan && shootingFrom == null; index++)
        {
            string keepAlive = string.Create(CultureInfo.InvariantCulture, $"events[KeepAliveEvent#{index}]");
            if (!Has(cast, keepAlive + ".time"))
            {
                break;
            }
            if (SpellRules.NeedsShooting(cast.GetJson(keepAlive + ".keepAliveMask")))
            {
                shootingFrom = Original(cast, keepAlive + ".time");
            }
        }
        if (shootingFrom == null)
        {
            return null;
        }
        double? spawn = FirstEventAfter(cast, "SpawnImpact", shootingFrom.Value);
        double? spawnEntity = FirstEventAfter(cast, "SpawnEntityEvent", shootingFrom.Value);
        if (spawnEntity != null && (spawn == null || spawnEntity < spawn))
        {
            spawn = spawnEntity;
        }
        double? cost = FirstEventAfter(cast, "PayUsageCost", shootingFrom.Value);
        if (spawn == null && cost == null)
        {
            return null;
        }
        return (shootingFrom.Value, Math.Max(spawn ?? 0, cost ?? 0));
    }

    // The time of a cast's first event of a type at or after a time.
    private static double? FirstEventAfter(GameDataEntry cast, string eventType, double from)
    {
        double? first = null;
        for (int index = 0; index < MaxListScan; index++)
        {
            string time = string.Create(CultureInfo.InvariantCulture, $"events[{eventType}#{index}].time");
            if (!Has(cast, time))
            {
                break;
            }
            double at = Original(cast, time);
            if (at >= from && (first == null || at < first))
            {
                first = at;
            }
        }
        return first;
    }

    // Max Totems and Max Skeletons: how many can be out at once before skills add to it. The player's templates carry the game's 1
    // in MaxTotemCount and MaxSkeletonSpellCount (attributes the game's limited_entity_spawn program reads), and a player gets them
    // when it spawns, so a change applies at the next world load.
    private void ApplySummonCaps()
    {
        double totems = Value(SpellCatalog.MaxTotemsKey);
        double skeletons = Value(SpellCatalog.MaxSkeletonsKey);
        Begin($"Max Totems {totems:0}, Max Skeletons {skeletons:0}");
        int found = 0;
        foreach (string name in SpellCatalog.PlayerTemplates)
        {
            GameDataEntry? player = Content.Templates.Get(name);
            if (player == null)
            {
                continue;
            }
            found++;
            if (Has(player, "MaxTotemCount.dataStorage[0]"))
            {
                PutNumber(player, "MaxTotemCount.dataStorage[0]", game => totems, whole: true);
            }
            if (Has(player, "MaxSkeletonSpellCount.dataStorage[0]"))
            {
                PutNumber(player, "MaxSkeletonSpellCount.dataStorage[0]", game => skeletons, whole: true);
            }
        }
        End($"{found} player templates");
    }

    // Health: the health a totem, skeleton or Magic Shield spawns with. In a template's Health attribute, slot 0 is the health it
    // starts with and slot 2 its base maximum (Kiln's notes: Health, Health_Min, Health_Max_Base, Health_Max_Adder, ...): totems
    // 662 in slot 2, the shield 1200 or 1500 in both. Skeletons have neither: their health comes from their level, through
    // EnemyScaling.enemyLevelScaling.healthScaleFactor (2.5), which is multiplied instead. The shield's tooltip line that shows its
    // health follows.
    private void ApplyHealth()
    {
        foreach ((string key, Regex itemName) in SpellCatalog.HealthSpells)
        {
            SettingInfo setting = SpellCatalog.Setting(key);
            double multiplier = Value(key);
            List<GameDataEntry> items = Items(itemName);
            Begin($"{setting.Label} {setting.Format(multiplier)}: {items.Count} items");
            var seen = new HashSet<Guid>();
            foreach (GameDataEntry item in items)
            {
                GameDataEntry? template = PlacedTemplate(item);
                if (template == null || !seen.Add(template.Guid))
                {
                    continue;
                }
                double gameHealth = Has(template, HealthMaxBasePath) ? Original(template, HealthMaxBasePath) : 0;
                if (gameHealth > 0)
                {
                    PutNumber(template, HealthMaxBasePath, game => SpellRules.Multiplied(game, multiplier), whole: true);
                    if (Has(template, HealthPath) && Original(template, HealthPath) > 0)
                    {
                        PutNumber(template, HealthPath, game => SpellRules.Multiplied(game, multiplier), whole: true);
                    }
                    PutTooltipShowing(item, (long)gameHealth, multiplier);
                }
                else if (Has(template, HealthScaleFactorPath))
                {
                    PutNumber(template, HealthScaleFactorPath, game => SpellRules.Multiplied(game, multiplier));
                }
                else
                {
                    Note($"{template.Name} has no health to change");
                }
            }
            End($"{seen.Count} placed templates");
        }
    }

    // The tooltip line of an item that shows a given value of the game (the shield's health), multiplied like that value.
    private void PutTooltipShowing(GameDataEntry item, long gameValue, double multiplier)
    {
        try
        {
            foreach (TooltipValue row in item.TooltipValues(TooltipList))
            {
                if (row.Raw == gameValue)
                {
                    PutTooltip(item, row.LocaId, multiplier);
                    return;
                }
            }
        }
        catch (Exception error) when (IsRefusal(error))
        {
            Refuse(item, TooltipList, error.Message);
        }
    }

    // Chain Lightning Radius (experimental): the radius of the sphere its cast places on the target, in which its script hits every
    // enemy once, and a new key for that sphere's physics shape. Without a new key the game reuses the shape it built for the old size:
    // the likely reason the Shroudforge mods' attempt (radius only) never widened the area.
    private void ApplyChainRadius()
    {
        SettingInfo setting = SpellCatalog.Setting(SpellCatalog.ChainRadiusKey);
        double multiplier = Value(SpellCatalog.ChainRadiusKey);
        Begin($"{setting.Label} {setting.Format(multiplier)}");
        GameDataEntry? cast = Content.Sequences.Get(SpellCatalog.ChainCastSequence);
        if (cast == null)
        {
            Note("Chain Lightning's cast is not in this world's data");
            End();
            return;
        }
        PutNumber(cast, ChainSpherePath, game => SpellRules.Multiplied(game, multiplier));
        string? keyPath = NumberPath(cast, ChainShapeKeyPaths);
        if (keyPath == null)
        {
            Note($"Chain Lightning's sphere has no shape key Kiln can read ({cast.GetJson(ChainImpactPath + ".joltObjects")?.ToJsonString() ?? "no value"}): its radius is set alone");
            End();
            return;
        }
        PutNumber(cast, keyPath, game => SpellRules.Same(multiplier, 1) ? game : SpellRules.ShapeKey((uint)game, multiplier), whole: true);
        End($"shape key {cast.Original(keyPath)} -> {cast.Get(keyPath)}");
    }

    // The first of the paths that holds a number in the entry (Kiln may name a 32-bit key with or without its inner field).
    private static string? NumberPath(GameDataEntry entry, string[] paths)
    {
        foreach (string path in paths)
        {
            try
            {
                if (entry.GetJson(path) is System.Text.Json.Nodes.JsonObject found && found["value"] is System.Text.Json.Nodes.JsonValue)
                {
                    return path;
                }
            }
            catch (Exception error) when (IsRefusal(error))
            {
            }
        }
        return null;
    }

    // Chain Lightning Lock-On Range: how far away the spell's first target can be.
    private void ApplyLockOn()
    {
        SettingInfo setting = SpellCatalog.Setting(SpellCatalog.ChainLockOnKey);
        double multiplier = Value(SpellCatalog.ChainLockOnKey);
        List<GameDataEntry> items = Items(SpellCatalog.ChainLightningItem);
        Begin($"{setting.Label} {setting.Format(multiplier)}: {items.Count} items");
        foreach (GameDataEntry item in items)
        {
            PutNumber(item, LockOnRangePath, game => SpellRules.Multiplied(game, multiplier));
        }
        End();
    }

    // Kiln's game data ----------------------------------------------------------------------------------------------

    // The template a spell item places: the totem, the zone, the summon itself.
    private GameDataEntry? PlacedTemplate(GameDataEntry item)
    {
        try
        {
            // Kiln gives a reference as the GUID of what it points at, and for some as its name.
            ContentValue placed = item.Get(PlacedTemplatePath);
            GameDataEntry? template = placed.Guid == Guid.Empty ? null : Content.Templates.Get(placed.Guid);
            if (template == null && !string.IsNullOrEmpty(placed.Name))
            {
                template = Content.Templates.Get(placed.Name);
            }
            if (template == null)
            {
                Note($"{item.Name}: the template it places is not in this world's data ({item.GetJson(PlacedTemplatePath)?.ToJsonString() ?? "no value"})");
            }
            return template;
        }
        catch (Exception error) when (IsRefusal(error))
        {
            Refuse(item, PlacedTemplatePath, error.Message);
            return null;
        }
    }

    // The radius of every sphere and capsule of a template, in the template's own order.
    private List<string> ColliderRadiusPaths(GameDataEntry template)
    {
        var paths = new List<string>();
        try
        {
            foreach (GameDataField field in template.Fields(ColliderComponent))
            {
                if (ColliderRadiusPath.IsMatch(field.Path) && Original(template, field.Path) > 0)
                {
                    paths.Add(field.Path);
                }
            }
        }
        catch (Exception error) when (IsRefusal(error))
        {
            Refuse(template, ColliderComponent, error.Message);
        }
        if (paths.Count > 0)
        {
            return paths;
        }
        // Kiln's listing may write a list's places differently from the path it takes: try the documented path, place by place.
        for (int colliderIndex = 0; colliderIndex < MaxCollidersTried; colliderIndex++)
        {
            for (int shapeIndex = 0; shapeIndex < MaxShapesTried; shapeIndex++)
            {
                string path = string.Create(CultureInfo.InvariantCulture, $"{ColliderComponent}.colliders[{colliderIndex}].dataArray[{shapeIndex}].shape.radius");
                if (Has(template, path) && Original(template, path) > 0)
                {
                    paths.Add(path);
                }
            }
        }
        return paths;
    }

    // Whether an entry has a value at a path. Asked before a config value is set: Kiln adds a config an entry doesn't have.
    private static bool Has(GameDataEntry entry, string path)
    {
        try
        {
            System.Text.Json.Nodes.JsonNode? node = entry.GetJson(path);
            return node != null && !(node is System.Text.Json.Nodes.JsonObject found && found.ContainsKey("absent"));
        }
        catch (Exception error) when (IsRefusal(error))
        {
            return false;
        }
    }

    private static double Original(GameDataEntry entry, string path)
    {
        return entry.Original(path).AsDouble;
    }

    // Puts one number in place: the game's own when the settings leave it alone, the wanted one otherwise.
    private void PutNumber(GameDataEntry entry, string path, Func<double, double> wanted, bool whole = false)
    {
        Put(entry, path, () =>
        {
            double game = Original(entry, path);
            double value = wanted(game);
            double current = entry.Get(path).AsDouble;
            // A whole number is compared exactly: a VFX word is a float's 32 bits, where two near numbers are different floats.
            bool Equal(double first, double second) => whole ? Math.Round(first) == Math.Round(second) : SpellRules.Same(first, second);
            // Only a value that changes is written: each write costs the game time, and a settings change rewrote every value.
            if (Equal(value, current))
            {
                return;
            }
            _valuesWritten++;
            if (Equal(value, game))
            {
                entry.Reset(path);
            }
            else if (whole)
            {
                entry.Set(path, (long)Math.Round(value));
            }
            else
            {
                entry.Set(path, value);
            }
        });
    }

    // A keen::Time field of a component, which Kiln reads and writes in seconds.
    private void PutTime(GameDataEntry entry, string path, Func<double, double> wantedSeconds)
    {
        Put(entry, path, () =>
        {
            double game = Original(entry, path);
            double value = wantedSeconds(game);
            if (SpellRules.Same(value, entry.Get(path).AsDouble))
            {
                return;
            }
            _valuesWritten++;
            if (SpellRules.Same(game, value))
            {
                entry.Reset(path);
            }
            else
            {
                entry.Set(path, TimeSpan.FromSeconds(value));
            }
        });
    }

    // An on/off field that takes a given value while a toggle is on, and the game's own while it is off.
    private void PutFlag(GameDataEntry entry, string path, bool toggleOn, bool valueWhileOn)
    {
        Put(entry, path, () =>
        {
            bool wanted = toggleOn ? valueWhileOn : entry.Original(path).Bool;
            if (entry.Get(path).Bool == wanted)
            {
                return;
            }
            _valuesWritten++;
            if (toggleOn && entry.Original(path).Bool != valueWhileOn)
            {
                entry.Set(path, valueWhileOn);
            }
            else
            {
                entry.Reset(path);
            }
        });
    }

    // A field that holds a name (an enum's value) and takes a given one while a toggle is on, and the game's own while it is off.
    private void PutName(GameDataEntry entry, string path, bool toggleOn, string nameWhileOn)
    {
        Put(entry, path, () =>
        {
            if (toggleOn)
            {
                entry.Set(path, nameWhileOn);
            }
            else
            {
                entry.Reset(path);
            }
        });
    }

    // A tooltip line of an item, found by its text id and not by its place. Returns the game's own value of that line, or NaN without one.
    private double PutTooltip(GameDataEntry item, uint locaId, double multiplier)
    {
        try
        {
            IReadOnlyList<TooltipValue> rows = item.TooltipValues(TooltipList);
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                if (rows[rowIndex].LocaId != locaId)
                {
                    continue;
                }
                string rowPath = string.Create(CultureInfo.InvariantCulture, $"{TooltipList}[{rowIndex}].value");
                // The rows Kiln lists must be the list's own rows in order, or the index would point at another line.
                if (item.Get(rowPath).AsDouble != rows[rowIndex].Raw)
                {
                    Refuse(item, rowPath, $"it holds {item.Get(rowPath)}, not the {rows[rowIndex].Raw} Kiln lists there");
                    return double.NaN;
                }
                double game = Original(item, rowPath);
                PutNumber(item, rowPath, value => SpellRules.TooltipValue((long)value, multiplier), whole: true);
                return game;
            }
        }
        catch (Exception error) when (IsRefusal(error))
        {
            Refuse(item, TooltipList, error.Message);
        }
        return double.NaN;
    }

    private void Put(GameDataEntry entry, string path, Action put)
    {
        try
        {
            put();
            _tally.InPlace++;
            _valuesInPlace++;
            _tally.Example ??= $"{entry.Name} {path} {entry.Original(path)} -> {entry.Get(path)}";
        }
        catch (Exception error) when (IsRefusal(error))
        {
            Refuse(entry, path, error.Message);
        }
    }

    // What Kiln throws for a path or a value it doesn't take.
    private static bool IsRefusal(Exception error)
    {
        return error is ArgumentException or KeyNotFoundException or InvalidOperationException;
    }

    // The log -----------------------------------------------------------------------------------------------------

    private void Begin(string name)
    {
        _tally = new Tally(name);
    }

    // One line per setting: what it is set to, what was found, how many values are in place, and the first of them as an example.
    private void End(string? found = null)
    {
        string line = _tally.Name + (found == null ? "" : ", " + found) + $"; {_tally.InPlace} values in place";
        if (_tally.Refused > 0)
        {
            line += $", {_tally.Refused} refused";
        }
        Log.Info(_tally.Example == null ? line : line + " (" + _tally.Example + ")");
    }

    private void Refuse(GameDataEntry entry, string path, string why)
    {
        _tally.Refused++;
        _valuesRefused++;
        Note($"{entry.Name} {path} refused: {why}");
    }

    private void Note(string message)
    {
        if (_refusalLines < MaxRefusalLines)
        {
            _refusalLines++;
            Log.Warn(message);
        }
    }
}
