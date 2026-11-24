using Kiln;

namespace PerennialCrops;

/// <summary>The crop settings: kiln config perennial-crops.json in the profile's config folder. They shape the game's data before it starts, so a
/// change applies at the next start of the game. A player who joins needs the host's values.</summary>
public sealed class CropSettings
{
    public const int MinSeconds = 1;
    public const int MaxSeconds = 86400;
    public const int DefaultSeconds = 600;

    [ConfigKey("enabled")]
    [Comment("Mod Enabled. Planted crops keep producing after harvest. Off: every crop is the game's own again. Applies at the next start of the game.")]
    [RequiresRestart]
    public bool Enabled { get; set; } = true;

    [ConfigKey("syncGrowthTime")]
    [Comment("Sync Growth Time. Every planted crop and tree takes Growth Time to ripen. Off: each keeps the game's own growth time. Applies at the next start of the game.")]
    [RequiresRestart]
    public bool SyncGrowthTime { get; set; } = false;

    [ConfigKey("growthSeconds")]
    [Comment("Growth Time (s). With Sync Growth Time on, how long a planted crop or tree takes from planting to ripe, before soil and world growth speed. Applies at the next start of the game.")]
    [Range(MinSeconds, MaxSeconds)]
    [RequiresRestart]
    public int GrowthSeconds { get; set; } = DefaultSeconds;

    [ConfigKey("regrowthSeconds")]
    [Comment("Regrowth Time (s). How long a harvested crop takes to produce again. Applies at the next start of the game.")]
    [Range(MinSeconds, MaxSeconds)]
    [RequiresRestart]
    public int RegrowthSeconds { get; set; } = DefaultSeconds;

    [ConfigKey("hideWhileRegrowing")]
    [Comment("Hide While Regrowing. A harvested crop hides its ripe model until it has produce again. Applies at the next start of the game.")]
    [RequiresRestart]
    public bool HideWhileRegrowing { get; set; } = true;
}

/// <summary>The area harvest settings: kiln config perennial-crops-server.json, used by the world's host while playing.</summary>
public sealed class HarvestSettings
{
    [ConfigKey("areaHarvest")]
    [Comment("Area Harvest. Harvesting a perennial crop by hand also harvests the ripe ones of the same kind around it.")]
    public bool AreaHarvest { get; set; } = true;

    [ConfigKey("harvestAnyCrop")]
    [Comment("Harvest Any Crop. Area harvest takes every ripe perennial crop around, whatever its kind.")]
    public bool HarvestAnyCrop { get; set; } = false;

    [ConfigKey("areaSizeMeters")]
    [Comment("Harvest Area Size (m). Side length of the square around the crop you harvest.")]
    [Range(HarvestRules.MinAreaMeters, HarvestRules.MaxAreaMeters, 1)]
    public double AreaSizeMeters { get; set; } = HarvestRules.DefaultAreaMeters;
}
