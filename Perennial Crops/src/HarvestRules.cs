using System;
using System.Collections.Generic;

namespace PerennialCrops;

/// <summary>
/// Which crops area harvest takes when a player harvests one by hand. No Kiln types, so the tests run without the game.
/// </summary>
public static class HarvestRules
{
    public const double MinAreaMeters = 1.0;
    public const double MaxAreaMeters = 100.0;
    public const double DefaultAreaMeters = 30.0;

    /// <summary>
    /// A crop around the harvested one is taken when it is perennial (a ripe stage this mod changed, or one of the game's perennials) and, unless
    /// Harvest Any Crop is on, of the same kind: the same ripe stage template.
    /// </summary>
    public static bool Takes(string? harvested, string? candidate, IReadOnlySet<string> perennialStages, bool anyCrop)
    {
        if (string.IsNullOrEmpty(harvested) || string.IsNullOrEmpty(candidate) || !perennialStages.Contains(candidate))
        {
            return false;
        }
        return anyCrop || string.Equals(harvested, candidate, StringComparison.Ordinal);
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
