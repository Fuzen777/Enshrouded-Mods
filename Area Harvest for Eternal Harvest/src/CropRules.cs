using System;

namespace EhAreaHarvest;

/// <summary>
/// Which crops this mod harvests and which of them lie in the harvest square. No Kiln types, so the tests run without the game.
/// </summary>
public static class CropRules
{
    /// <summary>Eternal Harvest names the ripe stage of each planted crop after its growth root: EternalHarvest_Planted_T6_Plant_Growth_Banana.</summary>
    public const string EternalHarvestPrefix = "EternalHarvest_Planted_";

    /// <summary>The game's planted fruit trees (banana, coconut, coffee, hazelnut, date palm): Scatter_Loot_..._PlayerPlanted_Refill, also spelled Playerplanted.</summary>
    public const string PlantedFruitTreeSuffix = "PlayerPlanted_Refill";

    public const double MinAreaMeters = 1.0;
    public const double MaxAreaMeters = 100.0;
    public const double DefaultAreaMeters = 30.0;

    /// <summary>
    /// The crops Planting Assistant's range harvest leaves out: Eternal Harvest's ripe planted crops, which have templates of their own, and the
    /// game's planted fruit trees, which are not on Planting Assistant's list. Its own crops (the game's templates) never match, so no crop is
    /// harvested by both mods.
    /// </summary>
    public static bool IsCovered(string? templateName)
    {
        if (string.IsNullOrEmpty(templateName))
        {
            return false;
        }
        return templateName.StartsWith(EternalHarvestPrefix, StringComparison.Ordinal)
            || templateName.EndsWith(PlantedFruitTreeSuffix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The area setting as used: 1 to 100 m, the default when it isn't a number.</summary>
    public static double AreaMeters(double configured)
    {
        return double.IsFinite(configured) ? Math.Clamp(configured, MinAreaMeters, MaxAreaMeters) : DefaultAreaMeters;
    }

    /// <summary>Inside the square of side <paramref name="sideMeters"/> centred on the harvested crop, any height (crops on terraces count).</summary>
    public static bool InSquare(double centerX, double centerZ, double x, double z, double sideMeters)
    {
        double half = sideMeters / 2.0;
        return Math.Abs(x - centerX) <= half && Math.Abs(z - centerZ) <= half;
    }
}
