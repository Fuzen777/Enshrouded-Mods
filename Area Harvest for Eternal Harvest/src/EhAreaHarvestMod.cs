using System;
using System.Collections.Generic;
using Kiln;

namespace EhAreaHarvest;

/// <summary>
/// XHL Planting Assistant (Kiln edition) harvests the crops around one you harvest by hand, but only crops on its fixed list of game templates.
/// Eternal Harvest's planted crops ripen into copies with templates of their own, and the game's planted fruit trees were never on that list, so
/// Planting Assistant leaves them all. This mod does what it does for those crops only: when a player harvests one by hand, every other ripe one
/// in the square around it gets the game's own loot request for that player (the request Planting Assistant sends for its crops).
/// Side "server": Kiln loads it on a dedicated server, and in a game client only while it hosts the world.
/// </summary>
public sealed class EhAreaHarvestMod : Mod
{
    private const int MaxDiagnosticLines = 16;
    // The answer to a sent request comes within a few steps; a crop without one (gone meanwhile) may be queued again after this.
    private const long QueuedForgetMilliseconds = 10_000;
    private const string TooManyPending = "Gameplay.Inject: too many pending requests";

    private ModConfig<HarvestSettings>? _settings;
    private bool _ready;
    private readonly Dictionary<uint, long> _queuedAt = new Dictionary<uint, long>();
    private readonly Queue<(EntityId Player, EntityId Crop, uint Offer, bool LootAll)> _pending = new Queue<(EntityId, EntityId, uint, bool)>();
    private int _harvestLines;
    private int _declineLines;
    private bool _noNameLogged;

    public override void OnLoad()
    {
        _settings = Config.Register<HarvestSettings>(ConfigKind.Server, null);
        var missing = new List<string>();
        if (!Capabilities.Is("kiln.spawn"))
        {
            missing.Add("kiln.spawn (crop names)");
        }
        if (!Capabilities.Is("queries.overlap_origins"))
        {
            missing.Add("queries.overlap_origins (crops in an area)");
        }
        if (!Ecs.Available || !Ecs.Supports<Ecs.InteractionOfferRuntime>())
        {
            missing.Add("InteractionOfferRuntime (" + (Ecs.Available ? Ecs.WhyNot<Ecs.InteractionOfferRuntime>() : "kiln.world is off") + ")");
        }
        _ready = missing.Count == 0;
        Context.Tick.Pre += OnTick;
        HarvestSettings settings = _settings.Value;
        if (_ready)
        {
            Log.Info($"loaded {Context.Info.Version}: harvesting one Eternal Harvest crop or planted fruit tree by hand harvests the ripe ones in a {CropRules.AreaMeters(settings.AreaSizeMeters):0.#} m square around it (range harvesting {(settings.Enabled ? "on" : "off")})");
        }
        else
        {
            Log.Warn($"loaded {Context.Info.Version}, but it can't harvest here: {string.Join(", ", missing)} not available");
        }
    }

    public override void OnUnload()
    {
        Context.Tick.Pre -= OnTick;
        _pending.Clear();
        _queuedAt.Clear();
    }

    [Subscribe]
    private void OnLoot(LootEvent e)
    {
        if (e.Injected || e.BypassInteraction)
        {
            // Our own requests (and Planting Assistant's) coming through: that crop is taken care of.
            _queuedAt.Remove(e.Host.Value);
            return;
        }
        HarvestSettings? settings = _settings?.Value;
        if (!_ready || settings == null || !settings.Enabled || !e.ByPlayer || e.Guest.Value == 0 || e.Host.Value == 0)
        {
            return;
        }
        EntityInfo? crop = Spawn.Inspect(e.Host);
        if (crop == null)
        {
            return;
        }
        if (crop.TemplateName == null)
        {
            if (!_noNameLogged)
            {
                _noNameLogged = true;
                Log.Warn("Kiln gives no template name for harvested entities here, so no crop is recognized");
            }
            return;
        }
        if (!CropRules.IsCovered(crop.TemplateName))
        {
            return;
        }
        QueueAround(e.Guest, e.Host, crop, CropRules.AreaMeters(settings.AreaSizeMeters), e.LootAll);
        SendPending();
    }

    [Subscribe]
    private void OnResult(InteractionResultEvent e)
    {
        if (!_queuedAt.Remove(e.Host.Value) || e.Accepted || _declineLines >= MaxDiagnosticLines)
        {
            return;
        }
        _declineLines++;
        Log.Info($"the game declined harvesting {Spawn.Inspect(e.Host)?.TemplateName ?? "entity " + e.Host.Value} for player {e.Guest.Value} (offer {e.OfferId})");
    }

    [Subscribe]
    private void OnStarted(ServerStartedEvent _)
    {
        Forget();
    }

    [Subscribe]
    private void OnWorldLoaded(WorldLoadedEvent _)
    {
        Forget();
    }

    private void Forget()
    {
        _pending.Clear();
        _queuedAt.Clear();
    }

    private void OnTick(in TickInfo _)
    {
        SendPending();
    }

    private void QueueAround(EntityId player, EntityId trigger, EntityInfo triggerCrop, double areaMeters, bool lootAll)
    {
        long now = Environment.TickCount64;
        var stale = new List<uint>();
        foreach (KeyValuePair<uint, long> entry in _queuedAt)
        {
            if (now - entry.Value > QueuedForgetMilliseconds)
            {
                stale.Add(entry.Key);
            }
        }
        foreach (uint id in stale)
        {
            _queuedAt.Remove(id);
        }

        WorldPosition center = triggerCrop.Position;
        double half = areaMeters / 2.0;
        EntityQueryResult found = Queries.EntitiesInBox(center, new WorldPosition(half, 1_000_000_000.0, half), trigger, Queries.MaxEntities);
        if (!found.Ok)
        {
            return;
        }
        // More entities in the box than one query returns: look at every entity with an interaction instead.
        IEnumerable<EntityId> candidates = found.Truncated ? Ecs.All<Ecs.InteractionOfferRuntime>() : found.Entities;
        int queued = 0, regrowing = 0, notOffered = 0;
        foreach (EntityId candidate in candidates)
        {
            if (candidate.Value == 0 || candidate == trigger || _queuedAt.ContainsKey(candidate.Value))
            {
                continue;
            }
            EntityInfo? crop = Spawn.Inspect(candidate);
            if (crop == null || !CropRules.IsCovered(crop.TemplateName)
                || !CropRules.InSquare(center.X, center.Z, crop.Position.X, crop.Position.Z, areaMeters))
            {
                continue;
            }
            if (!Ecs.TryGet(candidate, out Ecs.InteractionOfferRuntime offer) || offer.isDisabled)
            {
                notOffered++;
                continue;
            }
            if (Ecs.TryGet(candidate, out Ecs.InventoryEmptyState inventory) && inventory.isEmpty)
            {
                regrowing++;
                continue;
            }
            _queuedAt[candidate.Value] = now;
            _pending.Enqueue((player, candidate, offer.offerId, lootAll));
            queued++;
        }
        if (_harvestLines < MaxDiagnosticLines && (queued > 0 || regrowing > 0 || notOffered > 0))
        {
            _harvestLines++;
            Log.Info($"player {player.Value} harvested {triggerCrop.TemplateName}: {queued} more ripe crop(s) in the {areaMeters:0.#} m square sent for harvest ({regrowing} regrowing, {notOffered} with no harvest offered)");
        }
    }

    private void SendPending()
    {
        while (_pending.TryPeek(out (EntityId Player, EntityId Crop, uint Offer, bool LootAll) next))
        {
            try
            {
                Gameplay.Inject.Loot(next.Player, next.Crop, next.Offer, next.LootAll, bypassInteraction: true);
            }
            catch (InvalidOperationException error) when (error.Message == TooManyPending)
            {
                // The step's request arena is full: the rest go out next step.
                break;
            }
            catch (Exception error) when (error is InvalidOperationException || error is KilnUnavailableException || error is ArgumentException)
            {
                _queuedAt.Remove(next.Crop.Value);
                Log.Warn($"could not send the harvest of entity {next.Crop.Value}: {error.Message}");
            }
            _pending.Dequeue();
        }
    }
}
