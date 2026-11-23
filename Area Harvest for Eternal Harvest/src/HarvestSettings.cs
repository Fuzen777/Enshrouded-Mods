using Kiln;

namespace EhAreaHarvest;

/// <summary>The host's settings: kiln config eh-area-harvest-server.json in the profile's config folder, reloaded by Kiln when saved.</summary>
public sealed class HarvestSettings
{
    [ConfigKey("enabled")]
    [Comment("Range harvesting. Harvesting one Eternal Harvest crop or planted fruit tree by hand also harvests the ripe ones around it.")]
    public bool Enabled { get; set; } = true;

    [ConfigKey("areaSizeMeters")]
    [Comment("Harvest area size (m). Side length in meters of the square around the crop you harvest.")]
    [Range(CropRules.MinAreaMeters, CropRules.MaxAreaMeters)]
    public double AreaSizeMeters { get; set; } = CropRules.DefaultAreaMeters;
}
