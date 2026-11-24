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
public sealed class AutoDoorsMod : Mod
{
    private const int EventSize = 24;
    private const int AcceptedOfferOffset = 36;
    private const int OfferIdOffset = 40;
    private const int EvaluateEverySteps = 4;      // the world runs 60 steps a second
    private const int RescanEverySteps = 150;
    private const int CheckAfterSteps = 15;      // 0.25 s; a toggle moved the door within 3 steps in probe 4
    private const int AcceptWaitSteps = 10;
    private const double RetryAfterSeconds = 3.0;
    private const double ModeKeyReach = 8.0;     // the Door Mode key picks a door within this many metres of you
    private const double SurvivorSearchRadius = 60.0;

    private readonly ConcurrentQueue<(string Name, uint Guest, uint Host, uint Offer)> _outgoing = new();
    private readonly List<IDisposable> _bindings = new();
    private readonly Dictionary<uint, DoorState> _doors = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private ModConfig<DoorSettings>? _settings;
    private WorldConfig<DoorWorldRules>? _worldRules;
    private Dictionary<string, DoorMode> _modes = new();
    private IDisposable? _program;
    private EntitySet? _doorSet;
    private string _programState = "not bound";
    private ulong _step;
    private ulong _lastScan;
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
        public string Key = "";
        public string Name = "";
        public bool? LastSeen;
        public bool OpenedByMod;
        public bool HandHold;
        public double ClearSince = -1;
        public double RetryAt;
        // the action under way
        public Stage Stage;
        public bool Want;
        public uint Guest;
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
            _bindings.Add(KeyBindings.Register("auto-doors-kiln.toggle", "Shift+F7", () => OnTickThread("switch", ToggleEnabled),
                "Auto Doors: Switch On or Off", Context.Info.Name));
            Events.On<RenderFrameEvent>(OnFrame);
        }
        Log.Info($"auto doors {Context.Info.Version}: loaded; open {_settings.Value.OpenDistanceDm / 10.0:0.0} m, close {_settings.Value.CloseDistanceDm / 10.0:0.0} m");
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

    private DoorMode ModeOf(DoorState d) => _modes.TryGetValue(d.Key, out var m) ? m : DoorMode.Everyone;

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
                _doors[id.Value] = d = new DoorState { Id = id, Name = DisplayName(Spawn.Inspect(id)?.TemplateName) };
            d.At = at;
            d.Key = KeyOf(at);
            d.Forward = World.Transform(id) is { } tf ? Vector3.Transform(Vector3.UnitZ, tf.Rotation) : Vector3.Zero;
            d.InBase = bases.Any(b => b.Contains(at));
        }
        foreach (var gone in _doors.Keys.Where(k => !seen.Contains(k)).ToList()) _doors.Remove(gone);
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
        if ((s.OpenForSurvivors || s.OpenForAnimals) && Creatures.Available && players.Count > 0)
        {
            var seen = new HashSet<uint>();
            foreach (var p in players)
            {
                if (p.Position is not { } at) continue;
                if (s.OpenForSurvivors) AddCreatures(list, seen, at, CreatureKind.Villager, Who.Survivor);
                if (s.OpenForAnimals) AddCreatures(list, seen, at, CreatureKind.Animal, Who.Animal);
            }
        }
        return list;
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

    private static void AddCreatures(List<Trigger> list, HashSet<uint> seen, WorldPosition at, CreatureKind kind, Who who)
    {
        foreach (var e in Creatures.Near(at, SurvivorSearchRadius, kind))
            if (seen.Add(e.Value) && World.Position(e) is { } cp) list.Add(new Trigger(cp, e.Value, who));
    }

    private static bool Allowed(DoorMode mode, Who who) => mode switch
    {
        DoorMode.Everyone => true,
        DoorMode.Players => who is Who.Player or Who.Camera,
        DoorMode.PlayersAndSurvivors => who is Who.Player or Who.Camera or Who.Survivor,
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

    // The trigger nearest to the door within reach, or null.
    private static Trigger? Within(DoorState d, List<Trigger> triggers, DoorMode mode, double reach, double height)
    {
        Trigger? best = null;
        double bestDist = double.MaxValue;
        foreach (var t in triggers)
        {
            if (!Allowed(mode, t.Who) || Math.Abs(t.At.Y - d.At.Y) > height) continue;
            double dist = Ground(t.At, d.At);
            if (dist <= reach && dist < bestDist) { best = t; bestDist = dist; }
        }
        return best;
    }

    // ---------------------------------------------------------------------------------------------------- tick ----

    private void OnTick(in TickInfo tick)
    {
        _step = tick.Step;
        try
        {
            if (_program == null && (tick.Step % 25 == 0 || _step < 3)) EnsureProgram();
            if (_program == null) return;
            EnsureWorldRules();
            var s = S;
            if (s.Enabled != _wasEnabled) Switched(s.Enabled);
            if (tick.Step % EvaluateEverySteps != 0) return;
            if (_doors.Count == 0 || _step - _lastScan >= RescanEverySteps) Rescan();
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
        double open = s.OpenDistanceDm / 10.0;
        double close = Math.Max(s.CloseDistanceDm, s.OpenDistanceDm) / 10.0;
        double height = s.HeightReachDm / 10.0;
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

            if (d.LastSeen is { } last && last != isOpen)
            {
                // someone toggled it by hand (or a late toggle of ours landed)
                d.OpenedByMod = false;
                d.HandHold = s.RespectHandToggles;
                d.ClearSince = -1;
            }
            d.LastSeen = isOpen;

            var mode = ModeOf(d);
            if (mode == DoorMode.Manual || (s.OnlyInsideBases && !d.InBase)) continue;

            var keepOpen = Within(d, triggers, mode, close, height);
            if (d.HandHold)
            {
                if (keepOpen != null) continue;
                d.HandHold = false;
            }

            if (!isOpen)
            {
                if (now < d.RetryAt) continue;
                if (Within(d, triggers, mode, open, height) is { } who)
                {
                    uint openGuest = who.Who == Who.Camera ? GuestOnSide(d, who.At, triggers, players, who.Entity) : who.Entity;
                    d.GuestSide = Side(d, who.At);
                    Start(d, true, openGuest, who.Who.ToString().ToLowerInvariant(), players);
                }
                continue;
            }

            if (keepOpen != null) { d.ClearSince = -1; continue; }
            if (!d.OpenedByMod && !s.CloseDoorsOpenedByHand) continue;
            if (d.ClearSince < 0) d.ClearSince = now;
            if (now - d.ClearSince < s.CloseDelaySeconds || now < d.RetryAt) continue;
            if (NearestPlayer(d, players) is { } guest) Start(d, false, guest, "everyone left", players);
        }
    }

    // Which side of the door a point is on: + in front (the door's +Z), - behind, metres from the door's plane.
    private static double Side(DoorState d, WorldPosition p) =>
        d.Forward == Vector3.Zero ? 0 : (p.X - d.At.X) * d.Forward.X + (p.Y - d.At.Y) * d.Forward.Y + (p.Z - d.At.Z) * d.Forward.Z;

    // interaction_directional swings a door away from the toggle's guest, read from the guest's CurrentTransform. The build
    // camera has no entity, and its player may stand on the other side (0.2.3: camera-opened doors always swung the same
    // way). So the guest is an entity on the camera's side of the door: a player, a survivor or another door, the one
    // nearest to the door. Any entity with a transform does; the toggle checks the offer, not the guest.
    private uint GuestOnSide(DoorState d, WorldPosition camera, List<Trigger> triggers, List<Player> players, uint fallback)
    {
        double want = Side(d, camera);
        if (Math.Abs(want) < 0.05) return fallback;
        uint best = fallback;
        double bestDist = double.MaxValue;
        void Consider(uint entity, WorldPosition at)
        {
            if (entity == d.Id.Value) return;
            double side = Side(d, at);
            if (Math.Sign(side) != Math.Sign(want) || Math.Abs(side) < 0.3) return;
            double dist = Distance(at, d.At);
            if (dist < bestDist) { bestDist = dist; best = entity; }
        }
        foreach (var p in players) if (p.Position is { } at) Consider(p.Entity.Value, at);
        foreach (var t in triggers) if (t.Who is Who.Survivor or Who.Animal) Consider(t.Entity, t.At);
        foreach (var other in _doors.Values) Consider(other.Id.Value, other.At);
        if (_sideLogs++ < 20)
            Log.Info($"auto doors: build camera at door {d.Id.Value}: camera {(want > 0 ? "in front" : "behind")} ({want:0.0} m), guest {best}" +
                     (best == fallback ? " (no entity on that side, the player)" : $" ({bestDist:0.0} m from the door)"));
        return best;
    }

    private int _sideLogs, _directionLogs;

    private static uint? NearestPlayer(DoorState d, List<Player> players)
    {
        uint? best = null;
        double bestDist = double.MaxValue;
        foreach (var p in players)
        {
            if (p.Position is not { } at) continue;
            double dist = Distance(at, d.At);
            if (dist < bestDist) { bestDist = dist; best = p.Entity.Value; }
        }
        return best;
    }

    // First try: a toggle naming the door's last accepted offer, with the one who comes as guest (the door swings away from them).
    // A door never accepted (offer 0) gets an accept first, with a player as guest.
    private void Start(DoorState d, bool open, uint guest, string why, List<Player> players)
    {
        d.Want = open;
        d.Why = why;
        d.ClearSince = -1;
        uint offer = ReadRuntime(d.Id, AcceptedOfferOffset);
        if (offer != 0)
        {
            Send("ToggleInteractionEvent", guest, d.Id.Value, offer);
            d.Guest = guest;
            d.Stage = Stage.Toggled;
            d.Due = _step + CheckAfterSteps;
            return;
        }
        Accept(d, players);
    }

    private void Accept(DoorState d, List<Player> players)
    {
        if (NearestPlayer(d, players) is not { } player)
        {
            Fail(d, "no player to accept the door");
            return;
        }
        d.Guest = player;
        d.RuntimeBefore = RuntimeKey(d.Id);
        Send("AcceptOfferedInteractionEvent", player, d.Id.Value, ReadRuntime(d.Id, OfferIdOffset));
        d.Stage = Stage.Accepting;
        d.Due = _step + AcceptWaitSteps;
    }

    private void Follow(DoorState d, bool isOpen, List<Player> players)
    {
        if (d.Stage is Stage.Toggled or Stage.ToggledAfterAccept && isOpen == d.Want)
        {
            Done(d, isOpen);
            return;
        }
        switch (d.Stage)
        {
            case Stage.Toggled when _step >= d.Due:
                Accept(d, players);   // the toggle alone did nothing: accept with a player, then toggle again
                break;
            case Stage.Accepting:
                // wait until the accept landed (the runtime changed), at most AcceptWaitSteps
                if (RuntimeKey(d.Id) == d.RuntimeBefore && _step < d.Due) break;
                uint offer = ReadRuntime(d.Id, AcceptedOfferOffset);
                if (offer == 0) { Fail(d, "the door did not accept"); break; }
                Send("ToggleInteractionEvent", d.Guest, d.Id.Value, offer);
                d.Stage = Stage.ToggledAfterAccept;
                d.Due = _step + CheckAfterSteps;
                break;
            case Stage.ToggledAfterAccept when _step >= d.Due:
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

    private void ToggleEnabled(Player player)
    {
        if (_settings == null) return;
        bool on = !_settings.Value.Enabled;
        _settings.Value.Enabled = on;
        _settings.Save();
        Say(on ? "Auto Doors: on" : "Auto Doors: off", ToastKind.Info);
    }

    private void CycleDoorMode(Player player)
    {
        EnsureWorldRules();
        if (_doors.Count == 0 || _step - _lastScan >= RescanEverySteps) Rescan();
        if (Aimed(player) is not { } d || (!d.InBase && S.OnlyInsideBases)) return;   // the key was let through to the game
        var next = (DoorMode)(((int)ModeOf(d) + 1) % 4);
        if (next == DoorMode.Everyone) _modes.Remove(d.Key); else _modes[d.Key] = next;
        SaveModes();
        d.HandHold = false;
        d.ClearSince = -1;
        Log.Info($"auto doors: door {d.Id.Value} at {d.Key} is now {next}");
        Say($"Auto Doors: {d.Name} " + next switch
        {
            DoorMode.Everyone => "opens for everyone",
            DoorMode.Players => "opens for players",
            DoorMode.PlayersAndSurvivors => "opens for players & survivors",
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
        bool works = d != null && (d.InBase || !s.OnlyInsideBases);
        SetModeKeys(works);
        if (!works || !s.DoorLabel || !d!.InBase || ModeOf(d) is var mode && mode == DoorMode.Everyone)
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
