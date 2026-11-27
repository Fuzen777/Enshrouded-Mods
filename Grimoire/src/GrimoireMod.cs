using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Kiln;

namespace Grimoire;

/// <summary>
/// Grimoire: spells and arrows kept out of the backpack in slots of their own (Kiln player storage), equipped and listed in the player's
/// own order. Each kind is a Book. The screen (F3, or the button on the mod's page in Kiln's menu) has a tab per book and the settings;
/// it opens on Arrows or Spells by GrimoireRules.OpenOnArrows. Moved out of Better Spells 3.3.0-dev.11, where it was the spellbook.
/// </summary>
public sealed class GrimoireMod : Mod
{
    private const string WindowBindingId = "grimoire.window";
    private const string WindowDefaultKey = "F3";
    private const string WindowTitle = "Grimoire";
    private const string WindowPlacementId = "grimoire";
    // Better Spells' settings window, which this window docks to on its right (1.1.1, Kiln 0.2.0-dev.31.16).
    private const string PartnerModId = "better-spells";
    private const string PartnerPlacementId = "settings";
    // The window's first size: it fits the default 12 slots and a short backpack list; the rest scrolls. The player can resize and move
    // it, and Kiln keeps both for the next game start (WindowPlacement on a Kiln without window docking).
    private const float ScreenWidth = 360f;
    private const float ScreenHeight = 780f;
    private const byte SpellsTab = 0;
    private const byte ArrowsTab = 1;

    private ModConfig<GrimoireSettings>? _settings;
    private Book[] _books = Array.Empty<Book>();
    private UiWindow? _window;
    // Only on a Kiln without window docking (before 0.2.0-dev.31.16): follows the window and keeps its place in window.json.
    private WindowPlacement? _placement;
    // The window docks to Better Spells' window; _alone: the player Shift-dragged it away from it this game start.
    private bool _docking;
    private volatile bool _alone;
    private GrimoireNet? _net;
    private KeyBinding? _windowKey;
    private IDisposable? _settingsPage;
    // The tab to select at the next frame (-1: none), set when the screen opens; the tab drawn last, for the settings below it.
    private int _pendingTab = -1;
    private int _shownTab;

    public override void OnLoad()
    {
        _settings = Config.Register<GrimoireSettings>(ConfigKind.Common, null);
        GrimoireNet net = new GrimoireNet(this);
        _net = net;
        _books = new[]
        {
            new Book(this, BookKind.Spells, SpellsTab, net, () => Setting(GrimoireSettings.SpellSlotsKey, GrimoireRules.DefaultSlots), () => Enabled,
                () => HideOthers, () => SwitchDelay, player => PrefsOf(player, BookKind.Spells)),
            new Book(this, BookKind.Arrows, ArrowsTab, net, () => Setting(GrimoireSettings.ArrowSlotsKey, GrimoireRules.DefaultSlots), () => Enabled,
                () => HideOthers, () => SwitchDelay, player => PrefsOf(player, BookKind.Arrows)),
        };
        Book[] books = _books;
        net.Handle(
            (command, player) =>
            {
                if (command.Book < books.Length)
                {
                    books[command.Book].OnCommand(command.Op, command.Item, command.Index, player);
                }
            },
            view =>
            {
                if (view.Book < books.Length)
                {
                    books[view.Book].OnView(view);
                }
            });
        foreach (Book book in _books)
        {
            book.Start();
        }
        _settings.Changed += OnSettingsChanged;
        Network.ServerReady += OnServerReady;
        Network.Disconnected += OnDisconnected;
        if (Ui.Available)
        {
            // A Kiln window, not a screen: a screen always opens centred with a fixed size (1.0.0, asked 25 Nov 2026: resizable, and it
            // opens where the player left it).
            var placement = new WindowPlacement(Context.DataDirectory, Log, ScreenWidth, ScreenHeight);
            UiWindowOptions options = placement.Options(new UiWindowOptions { Interactive = true, StartOpen = false });
            try
            {
                // Kiln remembers where the player leaves it, and docks it to Better Spells' window (1.1.1); window.json gives its first place.
                _window = DockingWindow(options);
                _docking = true;
            }
            catch (Exception error) when (error is MissingMethodException or TypeLoadException)
            {
                Log.Info($"this Kiln has no window docking (0.2.0-dev.31.16 or newer): the grimoire opens where it was left, not docked to Better Spells ({error.Message})");
                _window = Ui.Window(WindowTitle, DrawWindow, options);
                _placement = placement;
            }
            _windowKey = KeyBindings.Register(WindowBindingId, WindowDefaultKey, ToggleWindow, "Open Grimoire", Context.Info.Name);
            _settingsPage = Ui.AddConfigPage(Context.Info.Id, DrawSettingsPage);
        }
        Log.Info($"loaded {Context.Info.Version}: {string.Join(", ", _books.Select(b => b.Name))}{(_window == null ? ", without its screen (Kiln's menu is not available)" : $", {_windowKey?.Key} opens the grimoire")}");
    }

    public override void OnUnload()
    {
        Network.ServerReady -= OnServerReady;
        Network.Disconnected -= OnDisconnected;
        if (_settings != null)
        {
            _settings.Changed -= OnSettingsChanged;
        }
        foreach (Book book in _books)
        {
            book.Dispose();
        }
        _books = Array.Empty<Book>();
        _settingsPage?.Dispose();
        _settingsPage = null;
        _windowKey?.Dispose();
        _windowKey = null;
        _window?.Dispose();
        _window = null;
        _net?.Dispose();
        _net = null;
    }

    [Subscribe]
    private void OnEquipmentChanged(PlayerEquipmentChangedEvent e)
    {
        foreach (Book book in _books)
        {
            book.OnEquipmentChanged(e);
        }
    }

    [Subscribe]
    private void OnStorageRestored(PlayerStorageRestoredEvent e) => OnStorageEvent(e.Key, e.Player, e.ToString());

    [Subscribe]
    private void OnStorageParked(PlayerStorageParkedEvent e) => OnStorageEvent(e.Key, e.Player, e.ToString());

    [Subscribe]
    private void OnStorageDeath(PlayerStorageDeathEvent e) => OnStorageEvent(e.Key, e.Player, e.ToString());

    private void OnStorageEvent(string key, EntityId player, string what)
    {
        foreach (Book book in _books)
        {
            book.OnStorageEvent(key, player, what);
        }
    }

    [Subscribe]
    private void OnPlayerLeft(PlayerLeftEvent e)
    {
        foreach (Book book in _books)
        {
            book.OnPlayerLeft(e.Player.Entity);
        }
        _net?.Forget(e.Player.Entity);
    }

    private void OnSettingsChanged(ConfigChangedEvent change)
    {
        SendPrefs();
        foreach (Book book in _books)
        {
            book.SettingsChanged();
        }
    }

    // This game joined a server with Kiln: the server gets this player's settings, and the books read their views.
    private void OnServerReady(NetPeer server)
    {
        SendPrefs();
        foreach (Book book in _books)
        {
            book.ConnectionChanged();
        }
    }

    private void OnDisconnected(NetPeer server)
    {
        foreach (Book book in _books)
        {
            book.ConnectionChanged();
        }
    }

    // On a game joined to a server: the settings the server's steps follow for this player.
    private void SendPrefs()
    {
        if (_net != null && !GrimoireNet.WorldHere && _net.ServerHasGrimoire)
        {
            _net.SendPrefs(new PrefsMessage(Enabled, Setting(GrimoireSettings.SpellSlotsKey, GrimoireRules.DefaultSlots),
                Setting(GrimoireSettings.ArrowSlotsKey, GrimoireRules.DefaultSlots)));
        }
    }

    // The settings a step of this player follows: the host's own, else what the player's game sent (the defaults until it did).
    private (bool Enabled, int Limit) PrefsOf(EntityId player, BookKind kind)
    {
        if (GrimoireNet.IsLocal(player))
        {
            return (Enabled, Setting(kind.SlotsKey, GrimoireRules.DefaultSlots));
        }
        PrefsMessage? prefs = _net?.PrefsOf(player);
        if (prefs == null)
        {
            return (true, GrimoireRules.DefaultSlots);
        }
        return (prefs.Enabled, kind == BookKind.Spells ? prefs.SpellSlots : prefs.ArrowSlots);
    }

    // Kiln 0.2.0-dev.31.16's members are used only in these helpers: on an older Kiln the call fails (MissingMethodException) instead of
    // the whole OnLoad.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private UiWindow DockingWindow(UiWindowOptions options)
    {
        UiWindow window = Ui.Window(WindowTitle, DrawWindow, options with { PlacementId = WindowPlacementId, RememberPlacement = true });
        // Shift-dragged away from Better Spells' window: it stays alone until the game restarts. Dropped back on it: docked again.
        window.DockChanged += change =>
        {
            if (change.ByPlayer)
            {
                _alone = change.DockedTo is null;
            }
        };
        return window;
    }

    // Docks the window on the right of Better Spells' settings window when that exists (its mod may load after this one, or not at all).
    // Kiln shows it beside that window, or holds it back up to 250 ms when one key press opens both.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DockToBetterSpells(UiWindow window)
    {
        if (window.DockedTo is null && Ui.FindWindow(PartnerModId, PartnerPlacementId) is { } settings)
        {
            window.DockTo(settings, DockSide.Right);
        }
    }

    private void ToggleWindow()
    {
        if (_window?.Open == true)
        {
            HideWindow();
        }
        else
        {
            OpenWindow();
        }
    }

    private void HideWindow()
    {
        _window?.Hide();
    }

    private void OpenWindow()
    {
        if (_window == null || _window.Open)
        {
            return;
        }
        if (_docking && !_alone)
        {
            try
            {
                DockToBetterSpells(_window);
            }
            catch (InvalidOperationException error)
            {
                Log.Warn($"not docked to Better Spells' window: {error.Message}");
            }
        }
        _window.Show();
        OnWindowOpened();
    }

    // The window opened: both books read the game again, then the default tab is chosen on what they hold.
    private void OnWindowOpened()
    {
        Context.Scheduler.Post(() =>
        {
            foreach (Book book in _books)
            {
                book.RefreshNow();
            }
            Book spells = _books[SpellsTab];
            Book arrows = _books[ArrowsTab];
            bool onArrows = GrimoireRules.OpenOnArrows(spells.HasBackpackStacks, spells.HasEntries, arrows.HasBackpackStacks, arrows.HasEntries);
            Interlocked.Exchange(ref _pendingTab, onArrows ? ArrowsTab : SpellsTab);
        });
    }

    private bool Enabled => _settings?.Value.Enabled ?? true;

    private bool HideOthers => _settings?.Value.HideOthers ?? true;

    private int SwitchDelay => GrimoireRules.ClampSwitchDelay(_settings?.Value.SwitchDelay ?? GrimoireRules.DefaultSwitchDelay);

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
            OpenWindow();
        }
        ui.SameLine();
        ui.TextDisabled($"Or Press {_windowKey?.Key}");
    }

    // Hide Other Spells And Arrows, right after a book's "In Your Backpack" section (the user's place for it, 25 Nov 2026). It is also on
    // the mod's page in Kiln's menu.
    private void DrawHideOthers(UiDraw ui)
    {
        bool hideOthers = HideOthers;
        if (ui.Checkbox("Hide Other Spells And Arrows", ref hideOthers))
        {
            Change(GrimoireSettings.HideOthersKey, hideOthers);
        }
        ui.Tooltip("The HUD lists show only what is in the grimoire, and the one you have equipped. Off: the other spells and arrows in your backpack come after the grimoire's.");
        int delay = SwitchDelay;
        if (ui.Slider("HUD Switch Delay (ms)", ref delay, GrimoireRules.MinSwitchDelay, GrimoireRules.MaxSwitchDelay))
        {
            Change(GrimoireSettings.SwitchDelayKey, GrimoireRules.ClampSwitchDelay(delay / GrimoireRules.SwitchDelayStep * GrimoireRules.SwitchDelayStep));
        }
        ui.Tooltip("How long the HUD list's cursor stays on a grimoire entry before it is equipped. Scrolling past entries equips nothing, so the list scrolls smoothly. 0: each entry is equipped as the cursor reaches it.");
    }

    // The screen: a tab per book, then the settings (Mod Enabled and the shown book's slots).
    private void DrawWindow(UiDraw ui)
    {
        _placement?.Track(ui);
        // Escape closes it, as it closed the Kiln screen it was before 1.0.0.
        if (ui.KeyPressed(UiKey.Escape, claim: true))
        {
            HideWindow();
        }
        Book[] books = _books;
        if (books.Length == 0)
        {
            return;
        }
        float settingsHeight = ui.MeasureText("X").Height * 6f;
        int pending = Volatile.Read(ref _pendingTab);
        if (ui.BeginTabBar("##books"))
        {
            for (int i = 0; i < books.Length; i++)
            {
                if (ui.BeginControlledTab(books[i].Name, pending == i))
                {
                    _shownTab = i;
                    if (ui.BeginChild("##book" + books[i].Name, 0f, -settingsHeight))
                    {
                        books[i].Draw(ui);
                        DrawHideOthers(ui);
                    }
                    ui.EndChild();
                    ui.EndTab();
                }
            }
            ui.EndTabBar();
        }
        if (pending >= 0)
        {
            Interlocked.CompareExchange(ref _pendingTab, -1, pending);
        }
        ui.Separator("Settings");
        bool enabled = Enabled;
        if (ui.Checkbox("Mod Enabled", ref enabled))
        {
            Change(GrimoireSettings.EnabledKey, enabled);
        }
        ui.Tooltip("Off: the game's own spell and arrow lists. The grimoire keeps what it holds.");
        BookKind kind = books[Math.Clamp(_shownTab, 0, books.Length - 1)].Kind;
        int slots = Setting(kind.SlotsKey, GrimoireRules.DefaultSlots);
        if (ui.Slider($"{kind.Singular} Slots", ref slots, GrimoireRules.MinSlots, GrimoireRules.MaxSlots))
        {
            Change(kind.SlotsKey, slots);
        }
        ui.Tooltip($"How many kinds of {kind.Name.ToLowerInvariant()} the grimoire holds, one slot each. Lowering it keeps what is already in it.");
    }
}
