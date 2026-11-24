using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Kiln;

namespace PerennialCrops;

/// <summary>
/// Makes every planted crop perennial the way the game makes its own (banana, coconut, coffee, date palm, hazelnut): the ripe stage keeps its
/// inventory and refills it on a timer instead of being destroyed when looted. The changes are Kiln resource patches on the game's own templates,
/// staged in OnBoot from the crop settings and written before the game reads its data. A crop's ripe stage is often the template its wild version
/// uses too, so wild crops of those kinds refill as well (Kiln cannot copy the refill parts onto a private clone yet).
/// Which item a refill gives is a reference, which resource patches cannot set: OnBoot keeps the plan in the mod's data folder and the running mod
/// sets those through Kiln's game data once the world's content is ready.
/// While a world is hosted, harvesting a perennial crop by hand also harvests the ripe ones of the same kind around it (area harvest), and
/// Shift+F6 opens the settings window.
/// </summary>
public sealed class PerennialCropsMod : Mod
{
    private const string PlanFile = "plan.json";
    private const string ResourceType = "keen::ecs::TemplateResource";
    private const string WindowBindingId = "perennial-crops.settings";
    private const string WindowDefaultKey = "Shift+F6";
    private const int MaxDiagnosticLines = 16;
    // The answer to a sent request comes within a few steps; a crop without one (gone meanwhile) may be queued again after this.
    private const long QueuedForgetMilliseconds = 10_000;
    private const string TooManyPending = "Gameplay.Inject: too many pending requests";

    private ModConfig<CropSettings>? _crops;
    private ModConfig<HarvestSettings>? _harvest;
    private CropSettings? _bootCrops;
    private HashSet<string> _perennialStages = new HashSet<string>(StringComparer.Ordinal);
    private bool? _harvestReady;
    private bool _missingLogged;
    private readonly Dictionary<uint, long> _queuedAt = new Dictionary<uint, long>();
    private readonly Queue<(EntityId Player, EntityId Crop, uint Offer, bool LootAll)> _pending = new Queue<(EntityId, EntityId, uint, bool)>();
    private int _harvestLines;
    private int _declineLines;
    private bool _noNameLogged;
    private UiWindow? _window;
    private KeyBinding? _windowKey;

    public override void OnBoot(BootContext boot)
    {
        CropSettings settings = Config.Register<CropSettings>(ConfigKind.Common, null).Value;
        string planPath = Path.Combine(Context.DataDirectory, PlanFile);
        Directory.CreateDirectory(Context.DataDirectory);
        if (!settings.Enabled)
        {
            File.WriteAllText(planPath, JsonSerializer.Serialize(new PlanRecord(Copy(settings), new List<ItemRecord>(), new List<string>())));
            Log.Info("Mod Enabled is off: every crop stays the game's own");
            return;
        }

        double? growth = settings.SyncGrowthTime ? settings.GrowthSeconds : null;
        CropPlan plan = CropPlanner.Plan(query => Read(boot, query), growth, settings.RegrowthSeconds, settings.HideWhileRegrowing);
        var definitions = BootDefinitions.For(boot);
        foreach (TemplatePatch patch in plan.Patches)
        {
            var block = new ResourcePatchBlock
            {
                Resource = patch.Resource,
                Type = ResourceType,
                RemoveComponents = patch.RemoveComponents.Count > 0 ? new List<string>(patch.RemoveComponents) : null,
                CopyComponents = patch.CopyComponents.Count > 0 ? new Dictionary<string, Guid>(patch.CopyComponents) : null,
            };
            foreach (KeyValuePair<string, object> set in patch.Set)
            {
                block.Set[set.Key] = set.Value switch
                {
                    long whole => whole,
                    int whole => whole,
                    double number => number,
                    bool flag => flag,
                    string name => name,
                    _ => throw new InvalidOperationException($"{patch.TemplateName}: {set.Key} has a value of type {set.Value.GetType().Name}"),
                };
            }
            var staged = definitions.Define(patch.Key, block);
            if (!staged.Ok)
            {
                // A refusal keeps the whole mod out rather than half of the crops changed.
                throw new InvalidOperationException($"{patch.TemplateName}: {staged.Reason}");
            }
        }

        var items = plan.Items.Select(i => new ItemRecord(i.Template, i.TemplateName, i.Slot, i.Item)).ToList();
        File.WriteAllText(planPath, JsonSerializer.Serialize(new PlanRecord(Copy(settings), items, plan.PerennialStages)));
        foreach (string line in plan.Report)
        {
            Log.Info(line);
        }
        Log.Info($"{plan.Summary}; growth {(growth is double g ? g + " s for every plant" : "the game's own")}, regrowth {settings.RegrowthSeconds} s, " +
                 $"hide while regrowing {(settings.HideWhileRegrowing ? "on" : "off")}; {plan.Patches.Count} templates patched");
    }

    public override void OnLoad()
    {
        _crops = Config.Register<CropSettings>(ConfigKind.Common, null);
        _harvest = Config.Register<HarvestSettings>(ConfigKind.Server, null);
        OpenWindowOnKey();

        BootModReport boot = Mods.BootState(Context.Info.Id);
        if (boot.State != BootModState.Committed)
        {
            string issues = string.Join("; ", boot.Issues.Select(i => i.ToString()));
            Log.Warn($"the crop changes are not in the game ({boot.State}){(issues.Length > 0 ? ": " + issues : "")}; restart the game after fixing it");
            return;
        }
        PlanRecord? plan;
        try
        {
            plan = JsonSerializer.Deserialize<PlanRecord>(File.ReadAllText(Path.Combine(Context.DataDirectory, PlanFile)));
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            Log.Error($"the crop plan could not be read ({e.Message}): perennial crops would refill the hazelnut's or banana's produce; restart the game");
            return;
        }
        if (plan == null)
        {
            return;
        }
        _bootCrops = plan.Settings;
        _perennialStages = new HashSet<string>(plan.PerennialStages ?? new List<string>(), StringComparer.Ordinal);
        StartAreaHarvest();
        if (plan.Items == null || plan.Items.Count == 0)
        {
            return;
        }
        if (!Content.Available || !Content.GameDataAvailable)
        {
            Log.Error("kiln.content or kiln.gamedata is not available: perennial crops would refill the hazelnut's or banana's produce");
            return;
        }
        List<ItemRecord> items = plan.Items;
        Content.OnReady(_ => SetRefillItems(items));
    }

    public override void OnUnload()
    {
        Context.Tick.Pre -= OnTick;
        _pending.Clear();
        _queuedAt.Clear();
        _windowKey?.Dispose();
        _windowKey = null;
    }

    // ---- refill produce ------------------------------------------------------------------------------------------------------

    /// <summary>Points each refill slot of the patched crops at the crop's own produce (the copies came from the hazelnut bush or the banana plant).</summary>
    private void SetRefillItems(List<ItemRecord> items)
    {
        int set = 0;
        var failures = new List<string>();
        foreach (ItemRecord item in items)
        {
            try
            {
                GameDataEntry? template = Content.Templates.Get(item.Template);
                if (template == null)
                {
                    failures.Add($"{item.TemplateName}: not found");
                    continue;
                }
                template.Set($"InteractionLootRefill.items[{item.Slot}].lootableItemRef", item.Item);
                set++;
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or KilnUnavailableException)
            {
                failures.Add($"{item.TemplateName} slot {item.Slot}: {e.Message}");
            }
        }
        if (failures.Count == 0)
        {
            Log.Info($"refill produce set on {set} slots of {items.Select(i => i.Template).Distinct().Count()} crops");
        }
        else
        {
            Log.Error($"refill produce set on {set} of {items.Count} slots; these refill the hazelnut's or banana's produce instead: {string.Join("; ", failures)}");
        }
    }

    // ---- area harvest --------------------------------------------------------------------------------------------------------

    private void StartAreaHarvest()
    {
        Context.Tick.Pre += OnTick;
    }

    /// <summary>Whether area harvest can run in the world being hosted. Asked at hand harvests, not at load: a both-sides mod loads in the main
    /// menu, where the server-side services (crops in an area) are not up yet (1.0.1 asked there and never ran).</summary>
    private bool HarvestReady()
    {
        if (_harvestReady is bool known)
        {
            return known;
        }
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
        if (missing.Count > 0)
        {
            // Asked again at the next hand harvest: a service may come up later. Said once per hosted world.
            if (!_missingLogged)
            {
                _missingLogged = true;
                Log.Warn($"area harvest can't run yet: {string.Join(", ", missing)} not available");
            }
            return false;
        }
        _harvestReady = _perennialStages.Count > 0;
        Log.Info($"area harvest ready: {_perennialStages.Count} perennial crop kinds");
        return _harvestReady.Value;
    }

    [Subscribe]
    private void OnLoot(LootEvent e)
    {
        if (e.Injected || e.BypassInteraction)
        {
            // Our own requests (and other mods') coming through: that crop is taken care of.
            _queuedAt.Remove(e.Host.Value);
            return;
        }
        HarvestSettings? settings = _harvest?.Value;
        if (settings == null || !settings.AreaHarvest || !Lifecycle.IsServerRunning || !e.ByPlayer || e.Guest.Value == 0 || e.Host.Value == 0 || !HarvestReady())
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
        if (!_perennialStages.Contains(crop.TemplateName))
        {
            return;
        }
        QueueAround(e.Guest, e.Host, crop, settings, e.LootAll);
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
        _harvestReady = null;
        _missingLogged = false;
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

    private void QueueAround(EntityId player, EntityId trigger, EntityInfo triggerCrop, HarvestSettings settings, bool lootAll)
    {
        long now = Environment.TickCount64;
        foreach (uint id in _queuedAt.Where(entry => now - entry.Value > QueuedForgetMilliseconds).Select(entry => entry.Key).ToList())
        {
            _queuedAt.Remove(id);
        }

        double areaMeters = HarvestRules.AreaMeters(settings.AreaSizeMeters);
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
            if (crop == null || !HarvestRules.Takes(triggerCrop.TemplateName, crop.TemplateName, _perennialStages, settings.HarvestAnyCrop)
                || !HarvestRules.InSquare(center.X, center.Z, crop.Position.X, crop.Position.Z, areaMeters))
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
            string which = settings.HarvestAnyCrop ? "ripe crop(s)" : "ripe crop(s) of the same kind";
            Log.Info($"player {player.Value} harvested {triggerCrop.TemplateName}: {queued} more {which} in the {areaMeters:0.#} m square sent for harvest ({regrowing} regrowing, {notOffered} with no harvest offered)");
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

    // ---- settings window -----------------------------------------------------------------------------------------------------

    private void OpenWindowOnKey()
    {
        if (!Ui.Available)
        {
            return;
        }
        _window = Ui.Window(Context.Info.Name, DrawWindow, new UiWindowOptions { Width = 460, Height = 420, StartOpen = false });
        _windowKey = KeyBindings.Register(WindowBindingId, WindowDefaultKey, () => _window?.Toggle(), "Open Perennial Crops Settings", Context.Info.Name);
    }

    /// <summary>Runs on the render thread: reads the settings' current values and posts every change to the tick thread, which writes the file.</summary>
    private void DrawWindow(UiDraw ui)
    {
        CropSettings? crops = _crops?.Value;
        HarvestSettings? harvest = _harvest?.Value;
        if (crops == null || harvest == null)
        {
            return;
        }

        ui.Separator("Crops");
        bool enabled = crops.Enabled;
        if (ui.Checkbox("Mod Enabled", ref enabled))
        {
            Change(_crops, "enabled", enabled);
        }
        bool sync = crops.SyncGrowthTime;
        if (ui.Checkbox("Sync Growth Time", ref sync))
        {
            Change(_crops, "syncGrowthTime", sync);
        }
        ui.Tooltip("Every planted crop and tree takes Growth Time to ripen. Off: each keeps the game's own growth time.");
        if (sync)
        {
            int growth = crops.GrowthSeconds;
            if (ui.Slider("Growth Time (s)", ref growth, CropSettings.MinSeconds, 7200))
            {
                Change(_crops, "growthSeconds", growth);
            }
        }
        int regrowth = crops.RegrowthSeconds;
        if (ui.Slider("Regrowth Time (s)", ref regrowth, CropSettings.MinSeconds, 7200))
        {
            Change(_crops, "regrowthSeconds", regrowth);
        }
        bool hide = crops.HideWhileRegrowing;
        if (ui.Checkbox("Hide While Regrowing", ref hide))
        {
            Change(_crops, "hideWhileRegrowing", hide);
        }
        ui.TextDisabled("Ctrl+Click A Slider To Type A Value");
        if (_bootCrops != null && !SameCrops(_bootCrops, crops))
        {
            ui.Text(new Color(255, 190, 80, 255), "Restart The Game To Apply");
        }

        ui.Spacing();
        ui.Separator("Area Harvest");
        bool area = harvest.AreaHarvest;
        if (ui.Checkbox("Area Harvest", ref area))
        {
            Change(_harvest, "areaHarvest", area);
        }
        ui.Tooltip("Harvesting a perennial crop by hand also harvests the ripe ones around it, in a world you host.");
        bool any = harvest.HarvestAnyCrop;
        if (ui.Checkbox("Harvest Any Crop", ref any))
        {
            Change(_harvest, "harvestAnyCrop", any);
        }
        ui.Tooltip("Off: only crops of the kind you harvested.");
        int size = (int)Math.Round(HarvestRules.AreaMeters(harvest.AreaSizeMeters));
        if (ui.Slider("Harvest Area Size (m)", ref size, (int)HarvestRules.MinAreaMeters, (int)HarvestRules.MaxAreaMeters))
        {
            Change(_harvest, "areaSizeMeters", (double)size);
        }
    }

    private void Change(ConfigFile? file, string key, object value)
    {
        if (file == null)
        {
            return;
        }
        Context.Scheduler.Post(() =>
        {
            if (!file.TrySet(key, value, out string? error))
            {
                Log.Warn($"{key} not saved: {error}");
            }
        });
    }

    private static bool SameCrops(CropSettings a, CropSettings b) =>
        a.Enabled == b.Enabled && a.SyncGrowthTime == b.SyncGrowthTime && (!a.SyncGrowthTime || a.GrowthSeconds == b.GrowthSeconds)
        && a.RegrowthSeconds == b.RegrowthSeconds && a.HideWhileRegrowing == b.HideWhileRegrowing;

    private static CropSettings Copy(CropSettings s) => new CropSettings
    {
        Enabled = s.Enabled,
        SyncGrowthTime = s.SyncGrowthTime,
        GrowthSeconds = s.GrowthSeconds,
        RegrowthSeconds = s.RegrowthSeconds,
        HideWhileRegrowing = s.HideWhileRegrowing,
    };

    private static GameRecord? Read(BootContext boot, string query)
    {
        BootDataResult result = boot.GameData.Read(query);
        return result.Ok && result.Data != null ? new GameRecord(result.Data.ResourceId, result.Data.Value) : null;
    }

    private sealed record ItemRecord(Guid Template, string TemplateName, int Slot, Guid Item);

    private sealed record PlanRecord(CropSettings Settings, List<ItemRecord> Items, List<string> PerennialStages);
}
