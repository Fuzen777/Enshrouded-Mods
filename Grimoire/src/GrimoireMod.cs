using System;
using System.Linq;
using Kiln;

namespace Grimoire;

/// <summary>
/// Grimoire: spells kept out of the backpack in slots of their own (Kiln player storage), equipped and listed in the player's own order.
/// Built for arrows too: each kind is a Book (spells today). The screen (F3, or the button on the mod's page in Kiln's menu) shows the
/// books and the settings. Moved out of Better Spells 3.3.0-dev.11, where it was the spellbook.
/// </summary>
public sealed class GrimoireMod : Mod
{
    private const string WindowBindingId = "grimoire.window";
    private const string WindowDefaultKey = "F3";
    private const string WindowTitle = "Grimoire";
    // A Kiln screen has a fixed size: this fits the default 12 slots and a short backpack list; the rest scrolls.
    private const float ScreenWidth = 360f;
    private const float ScreenHeight = 760f;

    private ModConfig<GrimoireSettings>? _settings;
    private Book? _spells;
    private UiScreen? _window;
    private KeyBinding? _windowKey;
    private IDisposable? _settingsPage;

    public override void OnLoad()
    {
        _settings = Config.Register<GrimoireSettings>(ConfigKind.Common, null);
        _spells = new Book(this, BookKind.Spells, () => Setting(GrimoireSettings.SpellSlotsKey, GrimoireRules.DefaultSlots), () => Enabled);
        _spells.Start();
        _settings.Changed += OnSettingsChanged;
        if (Ui.Available)
        {
            _window = Ui.Screen(WindowTitle, DrawWindow, new UiScreenOptions { Width = ScreenWidth, Height = ScreenHeight, DimBackground = false });
            _windowKey = KeyBindings.Register(WindowBindingId, WindowDefaultKey, () => _window?.Toggle(), "Open Grimoire", Context.Info.Name);
            _settingsPage = Ui.AddConfigPage(Context.Info.Id, DrawSettingsPage);
        }
        Log.Info($"loaded {Context.Info.Version}{(_window == null ? ", without its screen (Kiln's menu is not available)" : $", {_windowKey?.Key} opens the grimoire")}");
    }

    public override void OnUnload()
    {
        if (_settings != null)
        {
            _settings.Changed -= OnSettingsChanged;
        }
        _spells?.Dispose();
        _spells = null;
        _settingsPage?.Dispose();
        _settingsPage = null;
        _windowKey?.Dispose();
        _windowKey = null;
        _window?.Dispose();
        _window = null;
    }

    [Subscribe]
    private void OnEquipmentChanged(PlayerEquipmentChangedEvent e) => _spells?.OnEquipmentChanged(e);

    [Subscribe]
    private void OnStorageRestored(PlayerStorageRestoredEvent e) => _spells?.OnStorageEvent(e.Key, e.ToString());

    [Subscribe]
    private void OnStorageParked(PlayerStorageParkedEvent e) => _spells?.OnStorageEvent(e.Key, e.ToString());

    [Subscribe]
    private void OnStorageDeath(PlayerStorageDeathEvent e) => _spells?.OnStorageEvent(e.Key, e.ToString());

    private void OnSettingsChanged(ConfigChangedEvent change) => _spells?.SettingsChanged();

    private bool Enabled => _settings?.Value.Enabled ?? true;

    // A number setting as the file holds it, inside its range (any thread).
    private int Setting(string key, int fallback)
    {
        try
        {
            object? value = _settings?.Get(key);
            return value == null ? fallback : GrimoireRules.ClampSlots(Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception error) when (error is FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            return fallback;
        }
    }

    // Writes a setting on the tick thread (Kiln's settings may only be changed there).
    private void Change(string key, object value)
    {
        ModConfig<GrimoireSettings>? settings = _settings;
        if (settings == null)
        {
            return;
        }
        Context.Scheduler.Post(() =>
        {
            if (!settings.TrySet(key, value, out string? error))
            {
                Log.Warn($"{key} not saved: {error}");
            }
        });
    }

    private void DrawSettingsPage(UiDraw ui)
    {
        if (ui.Button("Open Grimoire"))
        {
            _window?.Open();
        }
        ui.SameLine();
        ui.TextDisabled($"Or Press {_windowKey?.Key}");
    }

    // The screen: each book, then the settings.
    private void DrawWindow(UiDraw ui)
    {
        if (ui.BeginChild("##books", 0f, -(ui.MeasureText("X").Height * 6f)))
        {
            _spells?.Draw(ui);
        }
        ui.EndChild();
        ui.Separator("Settings");
        bool enabled = Enabled;
        if (ui.Checkbox("Mod Enabled", ref enabled))
        {
            Change(GrimoireSettings.EnabledKey, enabled);
        }
        ui.Tooltip("Off: the game's own spell list. The grimoire keeps what it holds.");
        int slots = Setting(GrimoireSettings.SpellSlotsKey, GrimoireRules.DefaultSlots);
        if (ui.Slider("Spell Slots", ref slots, GrimoireRules.MinSlots, GrimoireRules.MaxSlots))
        {
            Change(GrimoireSettings.SpellSlotsKey, slots);
        }
        ui.Tooltip("How many spells the grimoire holds, one slot per spell. Lowering it keeps the spells already in it.");
    }
}
