using System;
using System.Threading;
using Kiln;

namespace AutoDoorsKiln;

// The settings window (Shift+F7, or the button on the mod's page of the Kiln menu): every setting in groups, distances in metres
// with - and + buttons, the on/off switch at the top. It is drawn on the render thread; a change is shown at once from a copy
// of the settings and written on the tick thread, where the mod reads them.
public sealed partial class AutoDoorsMod
{
    private const string ScreenTitle = "Auto Doors";
    private const float ScreenWidth = 470f;
    private const float ScreenHeight = 650f;
    private const int ShiftSteps = 5;

    private UiScreen? _screen;
    private DoorSettings _draft = new();
    private int _pendingWrites;
    private double _lastTickAt = -10;

    private void CreateSettingsScreen()
    {
        _screen = Ui.Screen(ScreenTitle, DrawSettingsScreen,
            new UiScreenOptions { Width = ScreenWidth, Height = ScreenHeight, DimBackground = false, CloseButton = true, CloseOnEscape = true });
        _bindings.Add(_screen);
        _bindings.Add(KeyBindings.Register("auto-doors-kiln.settings", "Shift+F7", () => _screen?.Toggle(),
            "Auto Doors: Settings Window", Context.Info.Name));
        if (Ui.AddConfigPage(Context.Info.Id, DrawConfigPage) is { } page) _bindings.Add(page);
    }

    // Under Kiln's own controls on the mod's page of the Kiln menu.
    private void DrawConfigPage(UiDraw ui)
    {
        if (ui.Button("Open Settings Window", 0, 0)) _screen?.Open();
        ui.SameLine(0, -1);
        ui.TextDisabled("or press Shift + F7 in game.");
    }

    // Render thread. Changes go through Change: the copy shows them at once, the tick thread stores them.
    private void Change(Action<DoorSettings> edit)
    {
        edit(_draft);
        Interlocked.Increment(ref _pendingWrites);
        Context.Scheduler.Post(() =>
        {
            try
            {
                if (_settings == null) return;
                edit(_settings.Value);
                _settings.Save();
            }
            catch (Exception ex)
            {
                Log.Warn($"auto doors: saving a setting failed: {ex.Message}");
            }
            finally
            {
                Interlocked.Decrement(ref _pendingWrites);
            }
        });
    }

    private void DrawSettingsScreen(UiDraw ui)
    {
        // The stored values, unless a change is still on its way (then the copy already holds it).
        if (Volatile.Read(ref _pendingWrites) == 0) _draft = S.Clone();
        var s = _draft;
        bool shift = (ui.HeldMods & KeyMods.Shift) != 0;

        bool on = s.Enabled;
        if (ui.Checkbox("Auto Doors on", ref on)) Change(x => x.Enabled = on);
        ui.Tooltip("Doors open and close by themselves. Off: every door stays as it is until you use it.");
        if (Now - _lastTickAt > 2) ui.TextDisabled("Changes apply once you are in a world you host.");

        ui.Spacing();
        ui.Separator("Where");
        Toggle(ui, "Work outside bases", !s.OnlyInsideBases, (x, v) => x.OnlyInsideBases = !v,
            "Doors outside a flame altar's area open and close too. Door modes, their key and the label stay for doors in your base.");

        ui.Spacing();
        ui.Separator("Who opens doors");
        Toggle(ui, "Players", s.OpenForPlayers, (x, v) => x.OpenForPlayers = v, "You and the players in your world.");
        Toggle(ui, "Survivors", s.OpenForSurvivors, (x, v) => x.OpenForSurvivors = v, "Survivors and workshop NPCs as they walk about the base.");
        Toggle(ui, "Build camera", s.OpenForBuildCamera, (x, v) => x.OpenForBuildCamera = v, "The free camera of build mode, swinging the door away from it.");
        Toggle(ui, "Animals", s.OpenForAnimals, (x, v) => x.OpenForAnimals = v, "Farm animals and pets. They may wander out.");

        ui.Spacing();
        ui.Separator("Distances");
        var metres = (Func<int, string>)(dm => $"{dm / 10.0:0.0} m");
        int step = shift ? ShiftSteps : 1;
        Number(ui, "open", "Open distance", s.OpenDistanceDm, 40, metres, step, x => x.OpenDistanceDm, (x, v) =>
        {
            x.OpenDistanceDm = Math.Clamp(v, DoorRules.MinDistanceDm, DoorRules.MaxOpenDistanceDm);
            x.CloseDistanceDm = Math.Max(x.CloseDistanceDm, x.OpenDistanceDm);
        }, "How close someone has to come, along the ground, for a door to open.");
        Number(ui, "close", "Close distance", s.CloseDistanceDm, 46, metres, step, x => x.CloseDistanceDm,
            (x, v) => x.CloseDistanceDm = Math.Clamp(v, x.OpenDistanceDm, DoorRules.MaxCloseDistanceDm),
            "A door closes once everyone is farther than this. Never less than the open distance.");
        Number(ui, "height", "Height reach", s.HeightReachDm, 25, metres, step, x => x.HeightReachDm,
            (x, v) => x.HeightReachDm = Math.Clamp(v, DoorRules.MinHeightReachDm, DoorRules.MaxHeightReachDm),
            "Someone farther than this above or below a door, on another floor, neither opens it nor keeps it open.");
        Number(ui, "delay", "Close delay", s.CloseDelaySeconds, 0, sec => $"{sec} s", step, x => x.CloseDelaySeconds,
            (x, v) => x.CloseDelaySeconds = Math.Clamp(v, 0, 30),
            "How long a door stays open after everyone has left.");
        ui.TextDisabled($"Hold Shift for {ShiftSteps} steps at a time.");

        ui.Spacing();
        ui.Separator("Doors used by hand");
        Toggle(ui, "Keep them as you left them", s.RespectHandToggles, (x, v) => x.RespectHandToggles = v,
            "A door you open or close by hand stays that way while you are near it, then works on its own again once everyone has left.");
        Toggle(ui, "Close them too", s.CloseDoorsOpenedByHand, (x, v) => x.CloseDoorsOpenedByHand = v,
            "A door someone opened by hand closes too once everyone has left. Off: only doors the mod opened close by themselves.");

        ui.Spacing();
        ui.Separator("On screen");
        Toggle(ui, "Door label", s.DoorLabel, (x, v) => x.DoorLabel = v,
            "Looking at a door in your base shows its mode under the crosshair (Players, Players & Survivors or Manual); nothing for Everyone.");
        Toggle(ui, "Messages", s.Toasts, (x, v) => x.Toasts = v, "A message on screen when the Door Mode key changes a door's mode.");

        ui.Spacing();
        ui.Separator();
        ui.TextDisabled("Shift + middle mouse (LB + Y): change the mode of the");
        ui.TextDisabled("door you look at in your base. Shift + F7: this window.");
        ui.Spacing();
        if (ui.Button("Defaults", 0, 0))
        {
            Change(x =>
            {
                var d = new DoorSettings { Enabled = x.Enabled };
                x.OpenDistanceDm = d.OpenDistanceDm; x.CloseDistanceDm = d.CloseDistanceDm; x.HeightReachDm = d.HeightReachDm;
                x.CloseDelaySeconds = d.CloseDelaySeconds; x.OnlyInsideBases = d.OnlyInsideBases;
                x.OpenForPlayers = d.OpenForPlayers; x.OpenForSurvivors = d.OpenForSurvivors; x.OpenForBuildCamera = d.OpenForBuildCamera;
                x.OpenForAnimals = d.OpenForAnimals; x.CloseDoorsOpenedByHand = d.CloseDoorsOpenedByHand;
                x.RespectHandToggles = d.RespectHandToggles; x.DoorLabel = d.DoorLabel; x.Toasts = d.Toasts;
            });
        }
        ui.Tooltip("Every setting back to its default, except on/off. Door modes are kept.");
        ui.SameLine(0, -1);
        if (ui.Button("Close", 0, 0)) ui.CloseScreen();
    }

    private void Toggle(UiDraw ui, string label, bool value, Action<DoorSettings, bool> set, string tip)
    {
        bool v = value;
        if (ui.Checkbox(label, ref v)) Change(x => set(x, v));
        ui.Tooltip(tip);
    }

    // [-] value [+] label: the value in the look's accent colour while it is not the default.
    private void Number(UiDraw ui, string id, string label, int value, int def, Func<int, string> format, int step,
        Func<DoorSettings, int> get, Action<DoorSettings, int> set, string tip)
    {
        using var scope = ui.Id(id);
        float size = (float)Math.Ceiling(ui.MeasureText("X", 0).Height * 1.5f);
        float gap = (float)Math.Ceiling(size * 0.3f);
        float valueWidth = ui.MeasureText("10.0 m", 0).Width + 2 * gap;
        float left = ui.Cursor.X;
        if (ui.Button("-", size, size)) Change(x => set(x, get(x) - step));
        ui.Tooltip(tip);
        string text = format(value);
        ui.SameLine(left + size + gap + (float)Math.Floor((valueWidth - ui.MeasureText(text, 0).Width) / 2), -1);
        if (value == def) ui.Text(text); else ui.Text(ui.Palette.Accent, text);
        ui.Tooltip(tip);
        ui.SameLine(left + size + 2 * gap + valueWidth, -1);
        if (ui.Button("+", size, size)) Change(x => set(x, get(x) + step));
        ui.Tooltip(tip);
        ui.SameLine(0, 2 * gap);
        ui.Text(label);
        ui.Tooltip(tip);
    }
}
