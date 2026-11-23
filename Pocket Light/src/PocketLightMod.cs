using System;
using System.Numerics;
using System.Threading;
using Kiln;

namespace PocketLight;

/// <summary>
/// A light that follows the player's character with nothing to equip. It plays the game's own small lantern light effect on the local
/// player's character (Kiln attaches it, so it moves with the character), with the settings' brightness, radius and color, and by default
/// with the effect's flame shrunk to nothing. The Toggle Light key switches it; the settings apply while playing.
/// Side "client": it runs in the player's game only, and other players don't see the light.
/// </summary>
public sealed class PocketLightMod : Mod
{
    // The light effect of the game's small lantern when lit (Prop_Buildings_Lantern_01_A): a flame and a point light.
    private static readonly VfxId LanternLight = VfxId.Of(new Guid("ca49de18-616f-4b87-8c00-19efe3f027bb"));
    // That effect's parameters, by the names the game gives them.
    private const string RadiusParameter = "light_radius";
    private const string BrightnessParameter = "light_intensity";
    private const string ColorParameter = "light_color";
    private const string LightOffsetParameter = "light_position_offset";
    private const string FlameSizeParameter = "scaleMultiplier";

    private const string ToggleBindingId = "pocket-light.toggle";
    private const string ToggleDefaultKey = "K";
    private const string LightOnPath = "lightOn";
    // A light that went out by itself (a respawn, a new world) is started again at most this often, changed settings restart it at most this often.
    private const long LostLightWaitMilliseconds = 1000;
    private const long ChangedSettingsWaitMilliseconds = 200;
    private const int MaxDiagnosticLines = 12;

    private ModConfig<LightSettings>? _settings;
    private KeyBinding? _toggleKey;
    private bool _subscribed;
    private bool _lightOn;
    // The file's Light On as last seen, to tell a change made in the Kiln menu or the file from our own.
    private bool _seenLightOn;
    private int _pendingWrites;
    private VfxHandle _light;
    private EntityId _lightOwner;
    private LightLook _lightLook;
    private long _nextStartAt;
    private int _diagnosticLines;
    private bool _effectMissingLogged;

    public override void OnLoad()
    {
        _settings = Config.Register<LightSettings>(ConfigKind.Client, null);
        _lightOn = _seenLightOn = _settings.Value.LightOn;
        if (!Client.Available || !Client.IsClient || !Vfx.Available)
        {
            Log.Warn($"loaded {Context.Info.Version}, but it can't light anything here: Kiln's client and effect services are not both available");
            return;
        }
        _toggleKey = KeyBindings.Register(ToggleBindingId, ToggleDefaultKey, null, "Toggle Light", Context.Info.Name);
        Client.Tick += OnClientTick;
        _subscribed = true;
        Log.Info($"loaded {Context.Info.Version}: {_toggleKey.Key} switches the light, which starts {(_lightOn ? "on" : "off")}");
    }

    public override void OnUnload()
    {
        if (_subscribed)
        {
            Client.Tick -= OnClientTick;
            _subscribed = false;
        }
        StopLight();
        _toggleKey?.Dispose();
        _toggleKey = null;
    }

    // The game's main thread, about 60 times a second while in a world.
    private void OnClientTick(in TickInfo _)
    {
        LightSettings? settings = _settings?.Value;
        if (settings == null)
        {
            return;
        }
        if (Volatile.Read(ref _pendingWrites) == 0)
        {
            if (settings.LightOn != _seenLightOn)
            {
                _lightOn = settings.LightOn;
            }
            _seenLightOn = settings.LightOn;
        }
        int presses = _toggleKey?.TakePresses() ?? 0;
        if (LightRules.AfterPresses(_lightOn, presses) != _lightOn)
        {
            _lightOn = !_lightOn;
            Remember(_lightOn);
            Note($"light switched {(_lightOn ? "on" : "off")}");
        }

        EntityId player = Client.LocalPlayer;
        if (!_lightOn || player.IsNone)
        {
            StopLight();
            return;
        }
        LightLook look = LightRules.Look(settings.Brightness, settings.RadiusMeters, settings.Color, settings.HeightMeters, settings.ForwardMeters, settings.FlameSize);
        bool running = !_light.IsNone && _lightOwner == player && Vfx.IsPlaying(_light);
        if (running && look == _lightLook)
        {
            return;
        }
        long now = Environment.TickCount64;
        if (now < _nextStartAt)
        {
            return;
        }
        if (!Vfx.IsKnown(LanternLight))
        {
            // The game's effects aren't listed until a world has run for a moment.
            _nextStartAt = now + LostLightWaitMilliseconds;
            if (!_effectMissingLogged)
            {
                _effectMissingLogged = true;
                Log.Info("waiting for the game to list the lantern's light effect");
            }
            return;
        }
        bool lost = !_light.IsNone && !running;
        if (lost)
        {
            VfxInfo? ended = Vfx.Info(_light);
            Note($"the light went out ({(ended == null ? "no details" : ended.State + ", " + ended.Reason)}), starting it again");
        }
        StopLight();
        StartLight(player, look);
        _nextStartAt = now + (lost ? LostLightWaitMilliseconds : ChangedSettingsWaitMilliseconds);
    }

    private void StartLight(EntityId player, LightLook look)
    {
        VfxParams parameters = new VfxParams()
            .Set(RadiusParameter, look.RadiusMeters)
            .Set(BrightnessParameter, look.Brightness)
            .Set(ColorParameter, new Color(look.Red, look.Green, look.Blue))
            // The light exactly where the settings put it: the lantern's own light sits 15 cm above its flame.
            .Set(LightOffsetParameter, Vector3.Zero)
            .Set(FlameSizeParameter, look.FlameSize);
        // The character's own axes: y up, z the way it faces.
        var position = new Offset(new Vector3(0f, look.HeightMeters, look.ForwardMeters));
        _light = Vfx.PlayOn(LanternLight, player, Slot.Root, position, parameters);
        _lightOwner = player;
        _lightLook = look;
        if (_light.IsNone)
        {
            Note("Kiln did not start the light");
            return;
        }
        Note($"light on character {player.Value}: brightness {look.Brightness:0.#}, radius {look.RadiusMeters:0.#} m, color #{look.Red:x2}{look.Green:x2}{look.Blue:x2}, {look.HeightMeters:0.##} m up and {look.ForwardMeters:0.##} m forward, flame size {look.FlameSize:0.##}");
    }

    private void StopLight()
    {
        if (_light.IsNone)
        {
            return;
        }
        Vfx.Stop(_light, immediate: true);
        _light = VfxHandle.None;
    }

    // Writes Light On to the settings file, so the light starts as it was left. Kiln changes settings on its own thread; if that never
    // runs here (in a world someone else hosts), the switch still holds for this session.
    private void Remember(bool lightOn)
    {
        ModConfig<LightSettings>? settings = _settings;
        if (settings == null)
        {
            return;
        }
        Interlocked.Increment(ref _pendingWrites);
        Context.Scheduler.Post(() =>
        {
            try
            {
                if (!settings.TrySet(LightOnPath, lightOn, out string error))
                {
                    Log.Warn($"could not save Light On: {error}");
                }
            }
            catch (InvalidOperationException error)
            {
                Log.Warn($"could not save Light On: {error.Message}");
            }
            finally
            {
                Interlocked.Decrement(ref _pendingWrites);
            }
        });
    }

    private void Note(string message)
    {
        if (_diagnosticLines >= MaxDiagnosticLines)
        {
            return;
        }
        _diagnosticLines++;
        Log.Info(message);
    }
}
