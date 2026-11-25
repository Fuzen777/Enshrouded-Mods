using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading;
using Kiln;

namespace AutoDoorsKiln;

/// <summary>
/// Auto Doors on Kiln: doors open when a player, a survivor or the build camera comes near and close once everyone has left.
/// Rebuilt from Auto Doors' described behaviour (SellTheWorldGames, closed source), not from its code.
///
/// How a door is moved (probes 1 to 4, CHANGES.md): the mod's ECS program door_toggle sends the game's own
/// ToggleInteractionEvent {guest +8, host +0xc, offer +0x10}. interaction_toggle_new flips ServerToggleState and
/// interaction_directional swings the door away from the guest. The offer has to be the door's last accepted offer
/// (InteractionOfferRuntime.lastAcceptData, offer at +36). A door that was never accepted has none: the mod first sends
/// AcceptOfferedInteractionEvent {player, door, offerId at +40}, then the toggle with the offer the accept stored.
///
/// The logic runs on the world's tick thread of the game that hosts the world (a hosted game or a dedicated server).
/// </summary>
public sealed partial class AutoDoorsMod : Mod
{
    private const int EventSize = 24;
    private const int AcceptedGuestOffset = 24;   // InteractionOfferRuntime.lastAcceptData.guestId
    private const int AcceptedOfferOffset = 36;
    private const int OfferIdOffset = 40;
    private const int EvaluateEverySteps = 4;      // the world runs 60 steps a second
    // Every door, creature and secret-piece search below is a pass over the world's whole entity table (Kiln has no
    // spatial index), so they run rarely (1.0.2, Fuzen's FPS check: 30 creature passes a second took ~17% of the world's
    // thread). Doors every 5 s (every 1 s while none is known), secret pieces every 30 s, creatures every 0.5 s and only
    // while a player is near a door they could open; between creature searches the ones found are followed by position.
    private const int RescanEverySteps = 300;
    private const int RescanEmptyEverySteps = 60;
    private const int SecretScanEverySteps = 1800;
    private const int CreatureSearchEverySteps = 30;
    private const int CheckAfterSteps = 15;      // 0.25 s; a toggle moved the door within 3 steps in probe 4
    private const int AcceptWaitSteps = 10;
    private const double RetryAfterSeconds = 3.0;
    // A door move that would name a player in a menu as guest waits this long and is tried again (1.1.1).
    private const double BusyRetrySeconds = 0.5;
    private const double ModeKeyReach = 8.0;     // the Door Mode key picks a door within this many metres of you
    private const double SurvivorSearchRadius = 60.0;

    private readonly ConcurrentQueue<(string Name, uint Guest, uint Host, uint Offer)> _outgoing = new();
    private readonly List<IDisposable> _bindings = new();
    private readonly Dictionary<uint, DoorState> _doors = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private ModConfig<DoorSettings>? _settings;
    private WorldConfig<DoorWorldRules>? _worldRules;
    private Dictionary<string, DoorMode> _modes = new();
    private HashSet<string> _unlocked = new();
    private IDisposable? _program;
    private EntitySet? _doorSet;
    private string _programState = "not bound";
    private ulong _step;
    private ulong _lastScan;
    private ulong _lastSecretScan;
    private bool _secretsScanned;
    private ulong _lastCreatureSearch;
    private bool _creaturesSearched;
    private readonly List<(EntityId Id, Who Who)> _creatures = new();
    private bool _wasEnabled;
    private int _opened, _closed, _failed;
    private volatile DoorLabel? _label;       // what the render thread draws under the crosshair
    private bool? _cameraFree;
    private int _cameraLogs;

    private sealed record DoorLabel(string Name, string State);

    private enum Who { Player, Survivor, Camera, Animal }

    private readonly record struct Trigger(WorldPosition At, uint Entity, Who Who);

    private enum Stage { None, Toggled, Accepting, ToggledAfterAccept }

    private sealed class DoorState
    {
        public required EntityId Id;
        public WorldPosition At;
        public Vector3 Forward;      // the door's +Z (the game's forward axis): DirectionalInteraction has a front and a back
        public double GuestSide;     // which side of the door the guest of the last open stood on (for the log)
        public bool InBase;
        public bool Secret;          // a secret door, shelf, floor, stair or mechanism: Manual or Close Only, no label
        public bool Locked;          // a locked door (lockpick or key) not yet seen open: left alone (1.2.1, 1.2.2)
        public bool LockTemplate;    // spawned from a locked template (*_Locked_*, *_Key)
        public string Key = "";
        public string Name = "";
        public double Height;        // how tall the doorway is (DoorHeights), 0 when it is not taller than the Height Reach
        public bool? LastSeen;
        public bool OpenedByMod;
        public bool HandHold;
        public double ClearSince = -1;
        public double RetryAt;
        // the action under way
        public Stage Stage;
        public bool Want;
        public uint Guest;
        public string GuestKind = "";   // camera opens: what the guest is (player, survivor, animal, door)
        public string Path = "";        // camera opens: how the door was moved (for the log)
        public bool GuestTrial;         // camera opens: the toggle runs with a non-player guest; falls back to the player
        public ulong Due;
        public string RuntimeBefore = "";
        public string Why = "";
    }

    public override void OnLoad()
    {
        _settings = Config.Register<DoorSettings>(ConfigKind.Common, null);
        _wasEnabled = _settings.Value.Enabled;
        Context.Tick.Post += OnTick;
        if (Ui.Available)
        {
            // The Door Mode keys only fire, and only keep the press from the game, while you look at a door the mod works on
            // (UpdateLabel turns them on and off): Shift + middle mouse stays the game's everywhere else.
            _modeKey = KeyBindings.Register("auto-doors-kiln.mode", "Shift+MouseMiddle", () => OnTickThread("door mode", CycleDoorMode),
                "Auto Doors: Door Mode (look at a door)", Context.Info.Name);
            _modePad = KeyBindings.Register("auto-doors-kiln.mode.pad", "Pad:LB+Y", () => OnTickThread("door mode", CycleDoorMode),
                "Auto Doors: Door Mode, Controller (look at a door)", Context.Info.Name);
            SetModeKeys(false);
            _bindings.Add(_modeKey);
            _bindings.Add(_modePad);
            CreateSettingsScreen();
            Events.On<RenderFrameEvent>(OnFrame);
        }
        Log.Info($"auto doors {Context.Info.Version}: loaded; open {_settings.Value.OpenDistance:0.0} m, close {_settings.Value.CloseDistance:0.0} m");
    }

    public override void OnUnload()
    {
        Context.Tick.Post -= OnTick;
        foreach (var b in _bindings) b.Dispose();
        _bindings.Clear();
        _program?.Dispose();
        _program = null;
    }

    private double Now => _clock.Elapsed.TotalSeconds;

    // ------------------------------------------------------------------------------------------------ program ----

    // A server program binds on the tick thread only (a client's OnLoad runs on the game's main thread in the main menu).
    private void EnsureProgram()
    {
        if (_program != null) return;
        var result = EcsPrograms.TryBind("door_toggle", Program, WorldKind.Server);
        if (result.Ok)
        {
            _program = result.Handle;
            _programState = "bound";
            Log.Info("auto doors: ECS program door_toggle bound");
        }
        else if (_programState != "refused: " + result.Reason)
        {
            _programState = "refused: " + result.Reason;
            Log.Warn($"auto doors: ECS program door_toggle {_programState}");
        }
    }

    // Game worker thread: send what the tick queued.
    private void Program(EcsProgramContext context)
    {
        Span<byte> e = stackalloc byte[EventSize];
        while (_outgoing.TryDequeue(out var o))
        {
            e.Clear();
            BitConverter.TryWriteBytes(e.Slice(8, 4), o.Guest);
            BitConverter.TryWriteBytes(e.Slice(12, 4), o.Host);
            BitConverter.TryWriteBytes(e.Slice(16, 4), o.Offer);
            if (!context.EmitEvent(o.Name, e)) Log.Warn($"auto doors: {o.Name} for door {o.Host} refused (event buffer full)");
        }
    }

    private void Send(string name, uint guest, uint door, uint offer) => _outgoing.Enqueue((name, guest, door, offer));

    // ------------------------------------------------------------------------------------------------ settings ----

    private DoorSettings S => _settings?.Value ?? new DoorSettings();

    private void EnsureWorldRules()
    {
        if (_worldRules != null) return;
        try
        {
            _worldRules = WorldConfig<DoorWorldRules>("doors");
            _worldRules.Changed += _ => ReadModes();
            ReadModes();
        }
        catch (Exception ex)
        {
            Log.Warn($"auto doors: per-door modes are not available ({ex.Message})");
            _worldRules = null;
        }
    }

    private void ReadModes()
    {
        var modes = new Dictionary<string, DoorMode>();
        foreach (var line in _worldRules?.Value.Doors ?? new List<string>())
        {
            int eq = line.IndexOf('=');
            if (eq <= 0 || ParseMode(line[(eq + 1)..].Trim()) is not { } mode) continue;
            if (ParseKey(line[..eq].Trim()) is not { } key) continue;
            modes[key] = mode;
        }
        _modes = modes;
        var unlocked = new HashSet<string>();
        foreach (var line in _worldRules?.Value.Unlocked ?? new List<string>())
            if (ParseKey(line.Trim()) is { } key) unlocked.Add(key);
        _unlocked = unlocked;
    }

    // 0.1.0 and 0.2.0 wrote PlayersOnly, SurvivorsOnly and Off.
    private static DoorMode? ParseMode(string text) => text.ToLowerInvariant() switch
    {
        "playersonly" => DoorMode.Players,
        "survivorsonly" => DoorMode.PlayersAndSurvivors,
        "off" => DoorMode.Manual,
        _ => Enum.TryParse(text, true, out DoorMode m) ? m : null,
    };

    private void SaveModes()
    {
        if (_worldRules == null) return;
        _worldRules.Value.Doors = _modes.Where(m => m.Value != DoorMode.Everyone).OrderBy(m => m.Key, StringComparer.Ordinal)
            .Select(m => m.Key + "=" + m.Value).ToList();
        _worldRules.Save();
    }

    // A door's key: its position to the decimetre. Entity ids change between sessions, positions don't.
    private static string KeyOf(WorldPosition p) =>
        string.Create(CultureInfo.InvariantCulture, $"{Math.Round(p.X, 1):0.0},{Math.Round(p.Y, 1):0.0},{Math.Round(p.Z, 1):0.0}");

    private static string? ParseKey(string text)
    {
        var parts = text.Split(',');
        if (parts.Length != 3) return null;
        var v = new double[3];
        for (int i = 0; i < 3; i++)
            if (!double.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v[i])) return null;
        return KeyOf(new WorldPosition(v[0], v[1], v[2]));
    }

    // Secret pieces: Close Only (default), Players or Manual (Fuzen, 24 Nov 2026). Doors: never Close Only.
    private DoorMode ModeOf(DoorState d) => d.Secret
        ? (_modes.TryGetValue(d.Key, out var sm) && sm is DoorMode.Players or DoorMode.Manual ? sm : DoorMode.CloseOnly)
        : (_modes.TryGetValue(d.Key, out var m) && m != DoorMode.CloseOnly ? m : DoorMode.Everyone);

    private static DoorMode DefaultMode(DoorState d) => d.Secret ? DoorMode.CloseOnly : DoorMode.Everyone;

    // Locked kinds: *_Locked_Pickable_*, *_Locked_Key, *_Locked_bronze/silver, Prop_Door_Ancient_night_locked and the
    // dungeons' Prop_Door_Cairn_Huge_DungeonNN_Key (all carry an InteractionLock with DestroyLock in the game's data).
    private static bool IsLockTemplate(string? template) => template != null &&
        (template.Contains("locked", StringComparison.OrdinalIgnoreCase) && !template.Contains("unlocked", StringComparison.OrdinalIgnoreCase)
         || template.EndsWith("_Key", StringComparison.OrdinalIgnoreCase));

    private static string LockParts(EntityId id) =>
        $"InteractionLock {World.ComponentSize(id, "InteractionLock")} B, ClientInteractionLock {World.ComponentSize(id, "ClientInteractionLock")} B";

    // A locked door seen open has been picked or opened with its key: from now on it works like any other door.
    private void Unlocked(DoorState d)
    {
        d.Locked = false;
        if (!_unlocked.Add(d.Key)) return;
        if (_lockLogs++ < 40) Log.Info($"auto doors: door {d.Id.Value} ({d.Name}) at {d.Key} is open, so unlocked: works again; {LockParts(d.Id)}");
        if (_worldRules == null) return;
        _worldRules.Value.Unlocked = _unlocked.OrderBy(k => k, StringComparer.Ordinal).ToList();
        _worldRules.Save();
    }

    // -------------------------------------------------------------------------------------------------- doors ----

    private void Rescan()
    {
        _lastScan = _step;
        _doorSet ??= World.Query("DirectionalInteraction", "ServerToggleState", "InteractionOfferRuntime");
        _doorSet.Refresh();
        var seen = new HashSet<uint>();
        var bases = Build.Bases.Where(b => !b.Abandoned).ToList();
        foreach (var id in _doorSet.Entities)
        {
            if (World.Position(id) is not { } at) continue;
            seen.Add(id.Value);
            if (!_doors.TryGetValue(id.Value, out var d))
            {
                string? template = Spawn.Inspect(id)?.TemplateName;
                _doors[id.Value] = d = new DoorState { Id = id, Name = DisplayName(template), Height = DoorHeight(template),
                    LockTemplate = IsLockTemplate(template) };
                if (d.LockTemplate && _lockLogs++ < 20)
                    Log.Info($"auto doors: door {id.Value} ({template}) at {KeyOf(at)} is a locked kind{(_unlocked.Contains(KeyOf(at)) ? ", unlocked before" : "")}; {LockParts(id)}");
            }
            // Locked doors (1.2.1-1.2.3; Fuzen: outside bases the mod opened doors that need a lockpick). Before 1.2.1 the
            // mod's accept as a player went through the lock and removed it (DestroyLock) without a lockpick. A door of a
            // locked template (*_Locked_*, *_Key) is locked while it carries InteractionLock, and also (should the lock be
            // template-only) until it is seen open, which it can only be once picked or opened with its key. Doors seen
            // open are kept by position in the world's door file.
            d.At = at;
            d.Key = KeyOf(at);
            d.Locked = d.LockTemplate && (World.ComponentSize(id, "InteractionLock") > 0 || !_unlocked.Contains(d.Key));
            d.Forward = World.Transform(id) is { } tf ? Vector3.Transform(Vector3.UnitZ, tf.Rotation) : Vector3.Zero;
            d.InBase = bases.Any(b => b.Contains(at));
        }
        // Secret pieces (Base_Secret_Door, _Shelf, _Floor, ...): toggles without a front and a back, found by template name.
        // (A query for InteractionToggle + ServerToggleState + InteractionOfferRuntime finds none of them.)
        // The name search lists every entity of the world, whatever its centre and radius, so it runs every 30 s (and only
        // with a base); in between, the pieces found before are kept while they exist. Only pieces inside a base are kept:
        // the mod never moves one outside a base.
        IEnumerable<EntityId> named;
        if (bases.Count > 0 && (!_secretsScanned || _step - _lastSecretScan >= SecretScanEverySteps))
        {
            _lastSecretScan = _step;
            _secretsScanned = true;
            try { named = Spawn.FindByName("Secret", null, 1e9, 2000).ToList(); }
            catch (Exception ex)
            {
                if (!_secretFindFailed) Log.Warn($"auto doors: finding secret pieces failed: {ex.Message}");
                _secretFindFailed = true;
                named = Array.Empty<EntityId>();
            }
        }
        else
        {
            named = _doors.Values.Where(d => d.Secret).Select(d => d.Id).ToList();
        }
        foreach (var id in named)
        {
            if (seen.Contains(id.Value) || World.Position(id) is not { } at || !bases.Any(b => b.Contains(at))) continue;
            if (!_doors.TryGetValue(id.Value, out var d))
            {
                if (_notSecret.Contains(id.Value)) continue;
                string? template = Spawn.Inspect(id)?.TemplateName;
                if (template == null) continue;   // not known yet: look again next time
                bool toggles = World.ReadBytes(id, "ServerToggleState") != null && World.ReadBytes(id, "InteractionOfferRuntime") != null;
                if (!toggles || template.IndexOf("Secret", StringComparison.OrdinalIgnoreCase) < 0) { _notSecret.Add(id.Value); continue; }
                _doors[id.Value] = d = new DoorState { Id = id, Name = SecretName(template), Secret = true };
            }
            seen.Add(id.Value);
            d.At = at;
            d.Key = KeyOf(at);
            d.Forward = World.Transform(id) is { } tf ? Vector3.Transform(Vector3.UnitZ, tf.Rotation) : Vector3.Zero;
            d.InBase = bases.Any(b => b.Contains(at));
        }
        foreach (var gone in _doors.Keys.Where(k => !seen.Contains(k)).ToList()) _doors.Remove(gone);
        if (_notSecret.Count > 20000) _notSecret.Clear();
    }

    private bool _secretFindFailed;
    private readonly HashSet<uint> _notSecret = new();

    private bool RescanDue => _step - _lastScan >= (ulong)(_doors.Count == 0 ? RescanEmptyEverySteps : RescanEverySteps);

    // "Base_Secret_Shelf" -> "Secret Shelf".
    private static string SecretName(string template)
    {
        foreach (var part in new[] { "Shelf", "Floor", "Stair", "Mechanism" })
            if (template.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return "Secret " + part;
        return "Secret Door";
    }

    private static bool? IsOpen(EntityId door) =>
        World.TryRead(door, "ServerToggleState", out byte state) ? state != 0 : null;

    private static uint ReadRuntime(EntityId door, int offset)
    {
        var bytes = World.ReadBytes(door, "InteractionOfferRuntime");
        return bytes != null && bytes.Length >= offset + 4 ? BitConverter.ToUInt32(bytes, offset) : 0;
    }

    // The runtime without its offerId, which runs on every step for a while after an accept.
    private static string RuntimeKey(EntityId door)
    {
        var bytes = World.ReadBytes(door, "InteractionOfferRuntime");
        if (bytes == null) return "";
        if (bytes.Length >= OfferIdOffset + 4) Array.Clear(bytes, OfferIdOffset, 4);
        return Convert.ToHexString(bytes);
    }

    // ------------------------------------------------------------------------------------------------ triggers ----

    private List<Trigger> Triggers(DoorSettings s)
    {
        var list = new List<Trigger>();
        var players = Players.All.ToList();
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (s.OpenForPlayers && p.Position is { } at) list.Add(new Trigger(at, p.Entity.Value, Who.Player));
            if (s.OpenForBuildCamera && BuildCamera(p, i == 0) is { } cam) list.Add(new Trigger(cam, p.Entity.Value, Who.Camera));
        }
        SearchCreatures(s, players);
        foreach (var (id, who) in _creatures)
            if (World.Position(id) is { } cp) list.Add(new Trigger(cp, id.Value, who));
        return list;
    }

    // Survivors and animals within SurvivorSearchRadius of a player who is near a door they could open. Creatures.Near is a
    // pass over every entity with a Faction, so it runs every CreatureSearchEverySteps, once per such player for both kinds.
    private void SearchCreatures(DoorSettings s, List<Player> players)
    {
        if (!(s.OpenForSurvivors || s.OpenForAnimals) || !Creatures.Available || players.Count == 0)
        {
            _creatures.Clear();
            return;
        }
        if (_creaturesSearched && _step - _lastCreatureSearch < CreatureSearchEverySteps) return;
        _lastCreatureSearch = _step;
        _creaturesSearched = true;
        _creatures.Clear();
        var seen = new HashSet<uint>();
        foreach (var p in players)
        {
            if (p.Position is not { } at || !CreatureDoorNear(at, s)) continue;
            foreach (var e in Creatures.Near(at, SurvivorSearchRadius))
            {
                if (!seen.Add(e.Value)) continue;
                var kind = Spawn.Inspect(e)?.Template is { } g ? Creatures.Template(g)?.Kind : null;
                if (kind == CreatureKind.Villager && s.OpenForSurvivors) _creatures.Add((e, Who.Survivor));
                else if (kind == CreatureKind.Animal && s.OpenForAnimals) _creatures.Add((e, Who.Animal));
            }
        }
    }

    // A door within SurvivorSearchRadius of the player that a survivor or an animal could open or keep open.
    private bool CreatureDoorNear(WorldPosition at, DoorSettings s)
    {
        foreach (var d in _doors.Values)
        {
            if (Distance(d.At, at) > SurvivorSearchRadius) continue;
            if (!d.InBase && (d.Secret || s.OnlyInsideBases)) continue;
            var mode = d.InBase ? ModeOf(d) : DoorMode.Everyone;
            if ((s.OpenForSurvivors && Allowed(mode, Who.Survivor)) || (s.OpenForAnimals && Allowed(mode, Who.Animal))) return true;
        }
        return false;
    }

    // Where a player's build camera is, when it is away from the character. The player of this game (the host, first slot)
    // uses the camera the game renders with: 0.1.0 used the camera the client sends with its input when the game's
    // IsInFreeCam flag was set, and the build camera opened nothing. A joined player still has only that.
    // 0.2.0 counted the camera as away from 3 m beyond the boom: the ordinary third-person camera sits 5 to 6 m from the
    // character's feet (boom 2.5 m), so it flapped while walking and its 30 log lines were gone before build mode was tried.
    // Away now starts 6 m beyond the boom and ends 4 m beyond it, and the game's free-cam flag is logged when it changes.
    private const double CameraAwayBeyondBoom = 6.0, CameraBackBeyondBoom = 4.0;
    private bool? _lastFlag;

    private WorldPosition? BuildCamera(Player p, bool local)
    {
        bool? flag = p.Check(PlayerStateId.ClientUiIsInFreeCam);
        if (flag != _lastFlag && local && _cameraLogs++ < 60)
            Log.Info($"auto doors: free-cam flag {_lastFlag?.ToString() ?? "unreadable"} -> {flag?.ToString() ?? "unreadable"}");
        if (local) _lastFlag = flag;
        if (local && Ui.Available && Render.CameraAvailable && p.Position is { } at)
        {
            var rc = Render.Camera;
            if (!rc.Valid) return null;
            double away = Distance(rc.Position, at);
            double boom = Math.Max(rc.Distance, 2.0);
            bool free = flag == true || away > boom + (_cameraFree == true ? CameraBackBeyondBoom : CameraAwayBeyondBoom);
            if (free != (_cameraFree ?? false) && _cameraLogs++ < 60)
            {
                double? aimAway = p.Aim is { } a ? Distance(a.Origin, at) : null;
                Log.Info($"auto doors: build camera {(free ? "away" : "back")}: free-cam flag {flag?.ToString() ?? "unreadable"}, " +
                         $"render camera {away:0.0} m from you (boom {rc.Distance:0.0} m), input camera {(aimAway is { } d ? d.ToString("0.0", CultureInfo.InvariantCulture) + " m" : "none")}");
            }
            _cameraFree = free;
            return free ? rc.Position : null;
        }
        return flag == true && p.Aim is { } aim ? aim.Origin : null;
    }

    private static bool Allowed(DoorMode mode, Who who) => mode switch
    {
        DoorMode.Everyone => true,
        DoorMode.Players => who is Who.Player or Who.Camera,
        DoorMode.PlayersAndSurvivors => who is Who.Player or Who.Camera or Who.Survivor,
        DoorMode.CloseOnly => who is Who.Player or Who.Camera,   // who keeps a secret door open; it never opens by itself
        _ => false,
    };

    private static double Ground(WorldPosition a, WorldPosition b)
    {
        double dx = a.X - b.X, dz = a.Z - b.Z;
        return Math.Sqrt(dx * dx + dz * dz);
    }

    private static double Distance(WorldPosition a, WorldPosition b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    // Someone on the door's floor: within the Height Reach above or below the door's foot. The build camera also counts
    // anywhere up to the top of a doorway taller than that (1.0.1, Fuzen: the Round Ancient Door, 6 m tall, only opened for
    // a camera near the floor). Walkers keep the plain reach, so a floor above a tall door does not open it.
    private static bool InHeight(DoorState d, Trigger t, double height)
    {
        double dy = t.At.Y - d.At.Y;
        return t.Who == Who.Camera ? dy >= -height && dy <= Math.Max(height, d.Height) : Math.Abs(dy) <= height;
    }

    // Doorways taller than 2.6 m: the top of their solid colliders above the door's foot, from the game's door templates
    // (offline dump of game build 26430625). Other doors use the Height Reach alone.
    private static readonly Dictionary<string, double> DoorHeights = new(StringComparer.OrdinalIgnoreCase)
    {
        ["_Prop_Door_HollowHalls_Huge"] = 12.0, ["Base_Door_Giant_Interact"] = 12.0, ["_060_Prop_Door_Huge_ColdHeights_HASBUGS"] = 11.0,
        ["_Prop_Door_HollowHalls_Fence"] = 8.0, ["Prop_Door_Huge_ColdHeights_02"] = 7.0,
        ["_Prop_Door_HollowHalls_Large"] = 6.0, ["Prop_Door_Round_Ancient_Crafted"] = 6.0, ["Prop_Door_Large_Wetland_Temple"] = 6.0,
        ["Prop_Door_Huge_Ancient_01"] = 6.0, ["Prop_Door_Ancient_night_unlocked_PLAYER_CRAFTED"] = 6.0,
        ["Prop_Door_Ancient_night_unlocked"] = 6.0, ["Prop_Door_Ancient_night_locked"] = 6.0, ["Prop_Door_Round_Ancient"] = 5.65,
        ["Prop_Door_Large_Vukah"] = 5.0, ["Prop_Door_Large_Iron"] = 5.0, ["Prop_Door_Huge_Desert"] = 5.0, ["Base_Door_Huge_Interact"] = 5.0,
        ["Prop_Door_Small_Watergate"] = 4.16, ["Prop_Door_Large_Dungeon"] = 4.0, ["Prop_Door_Large_Ancient"] = 4.0,
        ["Prop_Door_Large_Steppe_Crafted"] = 3.7, ["Prop_Door_Large_Steppe"] = 3.7, ["Prop_Door_Large_Nation01"] = 3.65,
        ["Prop_Door_Small_Watergate_02"] = 3.6, ["Prop_Door_Pine_silver_Large_ColdHeights_Crafted"] = 3.5,
        ["Prop_Door_Large_Wood_Crafted"] = 3.5, ["Prop_Door_Large_Wood"] = 3.5, ["Prop_Door_Large_T5_Crafted"] = 3.5,
        ["Prop_Door_Large_T1_Crafted"] = 3.5, ["Prop_Door_Large_T1"] = 3.5, ["Prop_Door_Large_T0_Crafted"] = 3.5, ["Prop_Door_Large_T0"] = 3.5,
        ["Prop_Door_Large_Steel_Crafted"] = 3.5, ["Prop_Door_Large_Steel"] = 3.5, ["Prop_Door_Large_Quarters"] = 3.5,
        ["Prop_Door_Large_Iron_02"] = 3.5, ["Prop_Door_Large_Desert_Crafted"] = 3.5, ["Prop_Door_Large_Desert"] = 3.5,
        ["Prop_Door_Large_ColdHeights"] = 3.5, ["Prop_Door_Large_Castle_Crafted"] = 3.5, ["Prop_Door_Large_Castle_02_Crafted"] = 3.5,
        ["Prop_Door_Large_Castle_02"] = 3.5, ["Prop_Door_Large_Castle"] = 3.5, ["Base_Door_Large_Interact"] = 3.5,
        ["Prop_Door_Round_Vault_Crafted"] = 3.0, ["Prop_Door_Round_Vault"] = 3.0, ["Prop_Door_Round_T1_Crafted"] = 3.0,
        ["Prop_Door_Round_T1"] = 3.0, ["Prop_Door_Round_Roughwood_Crafted"] = 3.0, ["Prop_Door_Round_Roughwood"] = 3.0,
        ["Base_Door_Round_Interact"] = 3.0,
    };

    private static double DoorHeight(string? template) =>
        template != null && DoorHeights.TryGetValue(template, out double h) ? h : 0;

    // The trigger nearest to the door within reach, or null.
    private static Trigger? Within(DoorState d, List<Trigger> triggers, DoorMode mode, double reach, double height)
    {
        Trigger? best = null;
        double bestDist = double.MaxValue;
        foreach (var t in triggers)
        {
            if (!Allowed(mode, t.Who) || !InHeight(d, t, height)) continue;
            double dist = Ground(t.At, d.At);
            if (dist <= reach && dist < bestDist) { best = t; bestDist = dist; }
        }
        return best;
    }

    // ---------------------------------------------------------------------------------------------------- tick ----

    private void OnTick(in TickInfo tick)
    {
        _step = tick.Step;
        _lastTickAt = Now;
        try
        {
            if (_program == null && (tick.Step % 25 == 0 || _step < 3)) EnsureProgram();
            if (_program == null) return;
            EnsureWorldRules();
            var s = S;
            if (s.Enabled != _wasEnabled) Switched(s.Enabled);
            if (tick.Step % EvaluateEverySteps != 0) return;
            if (RescanDue) Rescan();
            if (s.Enabled) Evaluate(s);
            UpdateLabel(s);
        }
        catch (Exception ex)
        {
            Log.Warn($"auto doors: tick failed: {ex}");
        }
    }

    private void Switched(bool enabled)
    {
        _wasEnabled = enabled;
        foreach (var d in _doors.Values) { d.Stage = Stage.None; d.HandHold = false; d.ClearSince = -1; d.LastSeen = null; }
        Log.Info($"auto doors: {(enabled ? "on" : "off")}");
    }

    private void Evaluate(DoorSettings s)
    {
        var triggers = Triggers(s);
        var players = Players.All.ToList();
        FindBusyPlayers(players);
        double open = s.OpenDistance;
        double close = Math.Max(s.CloseDistance, s.OpenDistance);
        double height = s.HeightReach;
        double now = Now;

        foreach (var d in _doors.Values)
        {
            if (!World.Exists(d.Id)) continue;
            bool? state = IsOpen(d.Id);
            if (state is not { } isOpen) continue;

            if (d.Stage != Stage.None)
            {
                Follow(d, isOpen, players);
                continue;
            }
            if (d.Locked && !s.BypassLockedDoors)
            {
                if (!isOpen) { d.LastSeen = isOpen; continue; }
                Unlocked(d);
            }

            if (d.LastSeen is { } last && last != isOpen)
            {
                // someone toggled it by hand (or a late toggle of ours landed)
                d.OpenedByMod = false;
                d.HandHold = s.RespectHandToggles;
                d.ClearSince = -1;
            }
            d.LastSeen = isOpen;

            // modes are set and kept for base doors only; secret pieces outside bases are always left alone
            var mode = d.InBase ? ModeOf(d) : d.Secret ? DoorMode.Manual : DoorMode.Everyone;
            if (mode == DoorMode.Manual || (s.OnlyInsideBases && !d.InBase)) continue;

            var keepOpen = Within(d, triggers, mode, close, height);
            if (d.HandHold)
            {
                if (keepOpen != null) continue;
                d.HandHold = false;
            }

            if (!isOpen)
            {
                if (mode == DoorMode.CloseOnly || now < d.RetryAt) continue;
                if (Within(d, triggers, mode, open, height) is { } who)
                {
                    d.GuestKind = "";
                    d.Path = "";
                    uint openGuest = who.Who == Who.Camera ? GuestOnSide(d, who.At, triggers, players, who.Entity) : who.Entity;
                    d.GuestSide = Side(d, who.At);
                    d.GuestTrial = who.Who == Who.Camera ? d.GuestKind != "player" : who.Who != Who.Player;
                    if (who.Who != Who.Camera) d.GuestKind = who.Who.ToString().ToLowerInvariant();
                    Start(d, true, openGuest, who.Who.ToString().ToLowerInvariant(), players);
                }
                continue;
            }

            if (keepOpen != null) { d.ClearSince = -1; continue; }
            if (!d.OpenedByMod && !s.CloseDoorsOpenedByHand && mode != DoorMode.CloseOnly) continue;
            if (d.ClearSince < 0) d.ClearSince = now;
            if (now - d.ClearSince < s.CloseDelaySeconds || now < d.RetryAt) continue;
            if (CloseGuest(d, players) is { } guest) Start(d, false, guest, "everyone left", players);
            else Wait(d, "everyone left");
        }
    }

    // Which side of the door a point is on: + in front (the door's +Z), - behind, metres from the door's plane.
    private static double Side(DoorState d, WorldPosition p) =>
        d.Forward == Vector3.Zero ? 0 : (p.X - d.At.X) * d.Forward.X + (p.Y - d.At.Y) * d.Forward.Y + (p.Z - d.At.Z) * d.Forward.Z;

    // interaction_directional swings a door away from the toggle's guest, read from the guest's CurrentTransform. The build
    // camera has no entity, and its player may stand anywhere (0.2.3: camera-opened doors always swung the same way).
    // 0.2.7's log: a toggle with a door 396 m away as guest did nothing, so the fallback accepted and toggled with the
    // player, and the door swung away from the player again. Now the guest is an entity on the camera's side within
    // GuestReach of the door: a player or survivor or animal first, else another door. If the toggle with a non-player
    // guest does nothing, the door is accepted with that guest and toggled again; only then with the player (Follow).
    // Kinds that never worked are skipped after two tries (_guestFails).
    private uint GuestOnSide(DoorState d, WorldPosition camera, List<Trigger> triggers, List<Player> players, uint fallback)
    {
        double want = Side(d, camera);
        d.GuestKind = "player";
        if (Math.Abs(want) < 0.05) return fallback;
        uint best = fallback;
        string bestKind = "player";
        double bestScore = double.MaxValue;
        var playerIds = new HashSet<uint>(players.Select(p => p.Entity.Value));
        void Consider(uint entity, WorldPosition at, string kind, int rank)
        {
            if (entity == d.Id.Value) return;
            if (_guestFails.TryGetValue(kind, out var f) && f.Fails >= 2 && f.Works == 0) return;
            double side = Side(d, at);
            if (Math.Sign(side) != Math.Sign(want) || Math.Abs(side) < 0.3) return;
            double dist = Distance(at, d.At);
            double score = rank * 1000 + dist;
            if (score < bestScore) { bestScore = score; best = entity; bestKind = kind; }
        }
        foreach (var p in players) if (p.Position is { } at && !_busy.Contains(p.Entity.Value)) Consider(p.Entity.Value, at, "player", 0);
        foreach (var t in triggers)
            if (t.Who is Who.Survivor or Who.Animal && !playerIds.Contains(t.Entity))
                Consider(t.Entity, t.At, t.Who == Who.Survivor ? "survivor" : "animal", 0);
        foreach (var other in _doors.Values) if (!other.Secret) Consider(other.Id.Value, other.At, "door", 1);
        d.GuestKind = bestKind;
        if (_sideLogs++ < 30)
        {
            var me = players.FirstOrDefault(p => p.Entity.Value == fallback);
            string you = me?.Position is { } pos ? $"; you stand {(Side(d, pos) >= 0 ? "in front" : "behind")} ({Side(d, pos):0.00} m)" : "";
            Log.Info($"auto doors: build camera at door {d.Id.Value}: camera {(want > 0 ? "in front" : "behind")} ({want:0.0} m), guest {best} ({bestKind}" +
                     (best == fallback ? ", none on that side" : $", {Distance(_doors.TryGetValue(best, out var g) ? g.At : d.At, d.At):0.0} m from the door") + $"){you}");
        }
        return best;
    }

    // Which guest kinds moved a door for the camera, and which did not.
    private readonly Dictionary<string, (int Works, int Fails)> _guestFails = new();

    private void GuestResult(string kind, bool worked)
    {
        _guestFails.TryGetValue(kind, out var f);
        _guestFails[kind] = worked ? (f.Works + 1, f.Fails) : (f.Works, f.Fails + 1);
    }

    private int _sideLogs, _directionLogs, _cameraOpenLogs;

    // Players in a menu (1.1.1, a user's report, reproduced by Fuzen: a survivor walking through a door closed the
    // player's backpack, storage or crafting menu). The close after the survivor had gone accepted the door's offer
    // with the nearest player as guest, and an accept as a player ends that player's open interaction and its menu.
    // Now no door event names a player in a full-screen menu or a looting menu as guest: the move waits until the menu
    // is closed (or uses someone else), and closes use the door's last accepted guest without a new accept.
    private readonly HashSet<uint> _busy = new();
    private int _busyLogs, _lockLogs;

    private void FindBusyPlayers(List<Player> players)
    {
        _busy.Clear();
        foreach (var p in players)
            if (p.Check(PlayerStateId.InFullScreenUi) == true || p.Check(PlayerStateId.ClientUiIsInLootingMenu) == true)
                _busy.Add(p.Entity.Value);
    }

    private void Wait(DoorState d, string why)
    {
        d.Stage = Stage.None;
        d.LastSeen = IsOpen(d.Id);
        d.RetryAt = Now + BusyRetrySeconds;
        if (_busyLogs++ < 10)
            Log.Info($"auto doors: door {d.Id.Value} ({why}) waits: the player it would name is in a menu");
    }

    // Who closes a door: its last accepted guest while that one still exists, so the toggle needs no new accept (a
    // survivor who walked through stays the guest). Else the nearest player not in a menu; null when there is none.
    private uint? CloseGuest(DoorState d, List<Player> players)
    {
        uint accepted = ReadRuntime(d.Id, AcceptedGuestOffset);
        if (accepted != 0 && ReadRuntime(d.Id, AcceptedOfferOffset) != 0 && !_busy.Contains(accepted) &&
            World.Exists(new EntityId(accepted)))
            return accepted;
        return NearestPlayer(d, players);
    }

    // The nearest player not in a menu.
    private uint? NearestPlayer(DoorState d, List<Player> players)
    {
        uint? best = null;
        double bestDist = double.MaxValue;
        foreach (var p in players)
        {
            if (p.Position is not { } at || _busy.Contains(p.Entity.Value)) continue;
            double dist = Distance(at, d.At);
            if (dist < bestDist) { bestDist = dist; best = p.Entity.Value; }
        }
        return best;
    }

    // First try: a toggle naming the door's last accepted offer, with the one who comes as guest (the door swings away from them).
    // A door never accepted (offer 0) gets an accept first, with a player as guest.
    private void Start(DoorState d, bool open, uint guest, string why, List<Player> players)
    {
        if (_busy.Contains(guest)) { Wait(d, why); return; }
        d.Want = open;
        d.Why = why;
        d.ClearSince = -1;
        if (!open) { d.GuestTrial = false; d.Path = ""; }
        // A toggle only moves the door when its guest is the door's last accepted guest (0.2.8's log: survivors and the
        // player far away both worked once accepted). Accept with the guest first when it is someone else.
        uint offer = ReadRuntime(d.Id, AcceptedOfferOffset);
        if (offer != 0 && ReadRuntime(d.Id, AcceptedGuestOffset) != guest)
        {
            if (d.Path.Length == 0) d.Path = "accepted with the guest";
            AcceptAs(d, guest);
            return;
        }
        if (offer != 0)
        {
            Send("ToggleInteractionEvent", guest, d.Id.Value, offer);
            d.Guest = guest;
            d.Stage = Stage.Toggled;
            d.Due = _step + CheckAfterSteps;
            return;
        }
        if (d.Path.Length == 0) d.Path = "accepted with the guest";
        AcceptAs(d, guest);
    }

    private void Accept(DoorState d, List<Player> players)
    {
        if (NearestPlayer(d, players) is not { } player)
        {
            if (_busy.Count > 0) Wait(d, d.Why);
            else Fail(d, "no player to accept the door");
            return;
        }
        d.Guest = player;
        d.RuntimeBefore = RuntimeKey(d.Id);
        Send("AcceptOfferedInteractionEvent", player, d.Id.Value, ReadRuntime(d.Id, OfferIdOffset));
        d.Stage = Stage.Accepting;
        d.Due = _step + AcceptWaitSteps;
    }

    private void FallBackToPlayer(DoorState d, List<Player> players, string why)
    {
        Log.Info($"auto doors: door {d.Id.Value}: {d.GuestKind} {d.Guest} as guest: {why}; using you");
        GuestResult(d.GuestKind, false);
        d.GuestTrial = false;
        d.GuestKind = "player";
        d.Path = "fell back to you";
        Accept(d, players);
    }

    private void AcceptAs(DoorState d, uint guest)
    {
        d.Guest = guest;
        d.RuntimeBefore = RuntimeKey(d.Id);
        Send("AcceptOfferedInteractionEvent", guest, d.Id.Value, ReadRuntime(d.Id, OfferIdOffset));
        d.Stage = Stage.Accepting;
        d.Due = _step + AcceptWaitSteps;
    }

    private void Follow(DoorState d, bool isOpen, List<Player> players)
    {
        // the guest went into a menu meanwhile: before the next event with it, stop and start again once the menu is closed
        if (_busy.Contains(d.Guest) && isOpen != d.Want && (d.Stage == Stage.Accepting || d.Stage == Stage.Toggled && _step >= d.Due))
        {
            Wait(d, d.Why);
            return;
        }
        if (d.Stage is Stage.Toggled or Stage.ToggledAfterAccept && isOpen == d.Want)
        {
            Done(d, isOpen);
            return;
        }
        switch (d.Stage)
        {
            case Stage.Toggled when _step >= d.Due:
                if (d.GuestTrial)
                {
                    // the toggle with a non-player guest did nothing: accept with that guest, then toggle again
                    Log.Info($"auto doors: door {d.Id.Value}: toggle with {d.GuestKind} {d.Guest} as guest did nothing; accepting with it");
                    d.Path = "accepted with the guest";
                    AcceptAs(d, d.Guest);
                }
                else
                {
                    if (d.Why == "camera") d.Path = "accepted with you";
                    Accept(d, players);   // the toggle alone did nothing: accept with a player, then toggle again
                }
                break;
            case Stage.Accepting:
                // wait until the accept landed (the runtime changed), at most AcceptWaitSteps
                if (RuntimeKey(d.Id) == d.RuntimeBefore && _step < d.Due) break;
                if (d.GuestTrial && RuntimeKey(d.Id) == d.RuntimeBefore) { FallBackToPlayer(d, players, "the door did not accept it"); break; }
                uint offer = ReadRuntime(d.Id, AcceptedOfferOffset);
                if (offer == 0)
                {
                    if (d.GuestTrial) FallBackToPlayer(d, players, "the door did not accept it");
                    else Fail(d, "the door did not accept");
                    break;
                }
                Send("ToggleInteractionEvent", d.Guest, d.Id.Value, offer);
                d.Stage = Stage.ToggledAfterAccept;
                d.Due = _step + CheckAfterSteps;
                break;
            case Stage.ToggledAfterAccept when _step >= d.Due:
                if (d.GuestTrial)
                {
                    FallBackToPlayer(d, players, "no move after an accept either");
                    break;
                }
                Fail(d, "the door did not move");
                break;
        }
    }

    private void Done(DoorState d, bool isOpen)
    {
        d.Stage = Stage.None;
        d.LastSeen = isOpen;
        d.OpenedByMod = isOpen;
        d.ClearSince = -1;
        if (isOpen) _opened++; else _closed++;
        if (_opened + _closed <= 20)
            Log.Info($"auto doors: {(isOpen ? "opened" : "closed")} door {d.Id.Value} at {d.Key} ({d.Why})");
        // which DirectionalInteraction state goes with which side, to check the front/back reading above
        if (isOpen && d.Why == "camera")
        {
            if (d.GuestTrial) GuestResult(d.GuestKind, true);
            d.GuestTrial = false;
            if (_cameraOpenLogs++ < 30 && World.ReadBytes(d.Id, "DirectionalInteraction") is { Length: >= 4 } cam)
                Log.Info($"auto doors: door {d.Id.Value} opened for the camera {(d.GuestSide > 0 ? "in front" : "behind")} ({d.GuestSide:0.0} m) " +
                         $"with {d.GuestKind} {d.Guest} as guest ({(d.Path.Length > 0 ? d.Path : "toggle only")}): directional state {BitConverter.ToUInt32(cam, 0)} " +
                         $"({(BitConverter.ToUInt32(cam, 0) == (d.GuestSide > 0 ? 1u : 2u) ? "away from the camera" : "TOWARD the camera")})");
        }
        if (isOpen && _directionLogs++ < 12 && World.ReadBytes(d.Id, "DirectionalInteraction") is { Length: >= 4 } dir)
            Log.Info($"auto doors: door {d.Id.Value} opened ({d.Why}) with the one who came {(d.GuestSide > 0 ? "in front" : "behind")} ({d.GuestSide:0.0} m): directional state {BitConverter.ToUInt32(dir, 0)}");
    }

    private void Fail(DoorState d, string reason)
    {
        d.Stage = Stage.None;
        d.LastSeen = IsOpen(d.Id);
        d.RetryAt = Now + RetryAfterSeconds;
        if (++_failed <= 20)
            Log.Warn($"auto doors: could not {(d.Want ? "open" : "close")} door {d.Id.Value} at {d.Key} ({d.Why}): {reason}; trying again in {RetryAfterSeconds:0} s");
    }

    // ---------------------------------------------------------------------------------------------------- keys ----

    private void OnTickThread(string what, Action<Player> action)
    {
        Context.Scheduler.Post(() =>
        {
            try
            {
                if (Players.All.FirstOrDefault() is not { } player)
                {
                    Say("Auto Doors: works in a world you host", ToastKind.Warning);
                    return;
                }
                action(player);
            }
            catch (Exception ex)
            {
                Log.Warn($"auto doors ({what}) failed: {ex}");
            }
        });
    }

    private void CycleDoorMode(Player player)
    {
        EnsureWorldRules();
        if (RescanDue) Rescan();
        if (Aimed(player) is not { } d || !d.InBase) return;   // the key was let through to the game
        var next = d.Secret
            ? ModeOf(d) switch { DoorMode.CloseOnly => DoorMode.Players, DoorMode.Players => DoorMode.Manual, _ => DoorMode.CloseOnly }
            : (DoorMode)(((int)ModeOf(d) + 1) % 4);
        if (next == DefaultMode(d)) _modes.Remove(d.Key); else _modes[d.Key] = next;
        SaveModes();
        d.HandHold = false;
        d.ClearSince = -1;
        Log.Info($"auto doors: door {d.Id.Value} at {d.Key} is now {next}");
        Say($"Auto Doors: {d.Name} " + next switch
        {
            DoorMode.Everyone => "opens for everyone",
            DoorMode.Players => "opens for players",
            DoorMode.PlayersAndSurvivors => "opens for players & survivors",
            DoorMode.CloseOnly => "closes by itself",
            _ => "is manual",
        }, ToastKind.Info);
    }

    // The door the player looks at: the one nearest to the view ray of the camera the game renders with (the build camera
    // too), else of the camera the client sent; within reach of the camera.
    private DoorState? Aimed(Player player)
    {
        WorldPosition origin;
        Vector3 dir;
        if (Ui.Available && Render.CameraAvailable && Render.Camera is { Valid: true } rc)
        {
            origin = rc.Position;
            dir = rc.Forward;
        }
        else if (player.Aim is { } aim)
        {
            origin = aim.Origin;
            dir = aim.Direction;
        }
        else return null;
        if (dir.LengthSquared() < 1e-6f) return null;
        dir = Vector3.Normalize(dir);
        DoorState? best = null;
        double bestScore = double.MaxValue;
        foreach (var d in _doors.Values)
        {
            // aim at the door's middle, about a metre above its base
            var v = new Vector3((float)(d.At.X - origin.X), (float)(d.At.Y + 1.0 - origin.Y), (float)(d.At.Z - origin.Z));
            float along = Vector3.Dot(v, dir);
            if (along <= 0.3f || along > ModeKeyReach) continue;
            double off = (v - along * dir).Length();
            if (off > 1.3) continue;
            double score = off / along;
            if (score < bestScore) { bestScore = score; best = d; }
        }
        return best;
    }

    // ------------------------------------------------------------------------------------------------- label ----

    // Tick thread: the door under the crosshair, as text for the render thread.
    // Only doors inside a flame altar's area get a label, and only their mode when that is not Everyone: no name, no open
    // or closed (Fuzen, 23 Nov 2026).
    private void UpdateLabel(DoorSettings s)
    {
        var d = Ui.Available && Players.All.FirstOrDefault() is { } player ? Aimed(player) : null;
        // Door modes are for base doors only, also with Work Outside Bases on (Fuzen, 24 Nov 2026): outside a base the
        // key stays the game's and there is no label.
        bool inBase = d is { InBase: true };
        SetModeKeys(inBase);
        if (!inBase || !s.DoorLabel || d!.Secret || ModeOf(d) is var mode && mode == DoorMode.Everyone)
        {
            _label = null;
            return;
        }
        var label = new DoorLabel(ModeText(mode), "");
        if (label != _label) _label = label;
    }

    private KeyBinding? _modeKey, _modePad;
    private bool? _modeKeysOn;

    private void SetModeKeys(bool on)
    {
        if (_modeKeysOn == on) return;
        _modeKeysOn = on;
        foreach (var b in new[] { _modeKey, _modePad })
        {
            if (b == null) continue;
            b.Enabled = on;
            b.Consume = on;
        }
    }

    private static string ModeText(DoorMode mode) => mode switch
    {
        DoorMode.Everyone => "Everyone",
        DoorMode.Players => "Players",
        DoorMode.PlayersAndSurvivors => "Players & Survivors",
        DoorMode.CloseOnly => "Close Only",
        _ => "Manual",
    };

    // Render thread: draws the label under the crosshair while the game's HUD is up.
    private void OnFrame(RenderFrameEvent e)
    {
        if (_label is not { } label || e.MenuOpen || e.Capturing || !e.Screen.InGame || e.Screen.Hud != HudScreen.InGame) return;
        float x = e.Width / 2f, y = e.Height / 2f + 48f * e.Scale;
        e.Draw.Text(x, y, label.Name, Color.White, 22f * e.Scale, TextAlign.Center, TextStyle.Outline, false);
        if (label.State.Length > 0)
            e.Draw.Text(x, y + 26f * e.Scale, label.State, new Color(210, 210, 210, 255), 18f * e.Scale, TextAlign.Center, TextStyle.Outline, false);
    }

    // "Prop_Door_Small_Steel_Crafted" -> "Small Steel Door"; "_Prop_Door_HollowHalls_Large" -> "Large Hollow Halls Door".
    internal static string DisplayName(string? template)
    {
        if (string.IsNullOrWhiteSpace(template)) return "Door";
        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "prop", "door", "doors", "crafted", "building", "bp", "deco" };
        var size = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "small", "large", "big", "huge", "medium", "double", "single", "wide" };
        var words = new List<string>();
        var sizes = new List<string>();
        foreach (var part in template.Split(new[] { '_', ' ', '-' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (skip.Contains(part) || part.All(char.IsDigit)) continue;
            // split CamelCase: HollowHalls -> Hollow Halls
            var w = System.Text.RegularExpressions.Regex.Replace(part, "(?<=[a-z])(?=[A-Z])", " ");
            w = char.ToUpperInvariant(w[0]) + w[1..];
            (size.Contains(part) ? sizes : words).Add(w);
        }
        return string.Join(" ", sizes.Concat(words).Append("Door"));
    }

    private void Say(string text, ToastKind kind)
    {
        Log.Info(text);
        if (S.Toasts && Ui.Available) Hud.Toast(text, kind, 3);
    }
}
