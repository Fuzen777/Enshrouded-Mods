using System;
using System.Globalization;

namespace BetterSpells;

/// <summary>What a setting's value is: a multiplier ("x3.5"), a whole count, a distance in metres, or an on/off toggle.</summary>
public enum SettingKind
{
    Multiplier,
    Count,
    /// <summary>A distance in the game's own units, which are not quite metres.</summary>
    Units,
    Toggle,
}

/// <summary>One setting: its key in the settings file, its name and group on the settings page, and its range, step and default. A toggle's default is 1 for on.</summary>
public sealed record SettingInfo(string Key, string Label, string Group, SettingKind Kind, double Minimum, double Maximum, double Step, double Default)
{
    public bool IsToggle => Kind == SettingKind.Toggle;

    /// <summary>The value as the settings page and the log show it: x3.5, 4, 1.5 m.</summary>
    public string Format(double value)
    {
        return Kind switch
        {
            SettingKind.Multiplier => string.Create(CultureInfo.InvariantCulture, $"x{value:0.##}"),
            SettingKind.Units => string.Create(CultureInfo.InvariantCulture, $"{value:0.##} {(value == 1 ? "unit" : "units")}"),
            SettingKind.Toggle => value != 0 ? "on" : "off",
            _ => string.Create(CultureInfo.InvariantCulture, $"{value:0}"),
        };
    }
}

/// <summary>The settings' ranges and steps, and how a setting's value becomes a value of the game. No Kiln here, so the tests run without the game.</summary>
public static class SpellRules
{
    // Every multiplier goes from x0.1 (a nerf) up, in steps of 0.05; Shift and Ctrl make a click worth 10 or 100 steps.
    public const double MinDuration = 0.1;
    public const double MaxDuration = 10.0;
    public const double DurationStep = 0.05;

    public const double MinDamage = 0.1;
    public const double MaxDamage = 50.0;
    public const double DamageStep = 0.05;

    public const double MinRadius = 0.1;
    public const double MaxRadius = 5.0;
    public const double RadiusStep = 0.05;
    public const double DefaultRadius = 1.0;
    // Chain Lightning's sphere goes further: the user wants room for players who want even more range.
    public const double MaxChainRadius = 10.0;


    public const double MinLockOn = 0.1;
    // The game holds a spell's targeting range to 42 (24 at x1): seen in game, x10 reached no farther than x1.75. The user chose x2.
    public const double MaxLockOn = 2.0;
    public const double MinBallSpeed = 0.1;
    public const double MaxBallSpeed = 10.0;
    public const double MinBallHitRate = 0.1;
    public const double MaxBallHitRate = 10.0;
    public const double CastingStep = 0.05;

    public const int MinSummonsPerCast = 1;
    public const int MaxSummonsPerCast = 5;

    // How many totems and skeletons can be out at once before skills raise it: the player's MaxTotemCount and MaxSkeletonSpellCount,
    // 1 each in the game's data.
    public const int MinSummonCap = 1;
    public const int MaxSummonCap = 10;
    public const int DefaultSummonCap = 1;

    // Mana cost: a share of the game's cost, or up to ten times it (to go with more summons per cast).
    // Cast Time: the spells' Charge Time (shorter readies the spell again sooner).
    public const double MinCastTime = 0.1;
    public const double MaxCastTime = 10.0;
    public const double CastTimeStep = 0.05;

    public const double MinCost = 0.1;
    public const double MaxCost = 10.0;
    public const double CostStep = 0.05;

    public const double MinHealth = 0.1;
    public const double MaxHealth = 10.0;
    public const double HealthStep = 0.05;
    // The spread of a cast's summons, in the game's own distance units (the user found them not to read as metres).
    public const double MinSpreadUnits = 0.0;
    public const double MaxSpreadUnits = 25.0;
    public const double SpreadStep = 1.0;
    public const double DefaultSpreadUnits = 2.0;
    // Keep Older Wisps: the radius of the Lightning Wisp script's sphere, small enough to touch only the caster.
    public const double KeptWispSphereRadius = 0.05;
    // Skeletons walk off by themselves, so they only need to land apart: a fixed spread while a cast places more than one.
    public const double SkeletonSpreadUnits = 1.0;

    // Two values this close, as a share of their size, are the same value as the game holds it (a float32).
    private const double SameValueTolerance = 0.000001;

    /// <summary>A setting's value inside its range and on its step, counted from the minimum. A value that is not a number becomes the default.</summary>
    public static double Snap(SettingInfo setting, double value)
    {
        if (double.IsNaN(value))
        {
            return setting.Default;
        }
        double clamped = Math.Clamp(value, setting.Minimum, setting.Maximum);
        if (setting.Step <= 0)
        {
            return clamped;
        }
        double steps = Math.Round((clamped - setting.Minimum) / setting.Step, MidpointRounding.AwayFromZero);
        // Rounded once more, so that 1 + 3 x 0.05 reads 1.15 and not 1.1500000000000001.
        return Math.Clamp(Math.Round(setting.Minimum + steps * setting.Step, 6), setting.Minimum, setting.Maximum);
    }

    /// <summary>The value one step up (direction 1) or down (direction -1), inside the range.</summary>
    public static double Stepped(SettingInfo setting, double value, int direction)
    {
        return Snap(setting, Snap(setting, value) + direction * setting.Step);
    }

    public static bool Same(double first, double second)
    {
        return Math.Abs(first - second) <= SameValueTolerance * Math.Max(1.0, Math.Max(Math.Abs(first), Math.Abs(second)));
    }

    /// <summary>
    /// A per-spell multiplier with the General card's one of the same kind. Multiply: both apply (x2 and x3 give x6). At Least: the
    /// better of the two, which for a cost is the lower one (x0.5 lowers anything above it) and otherwise the higher one (x2 lifts
    /// anything below it).
    /// </summary>
    public static double Combine(double perSpell, double general, bool atLeast, bool lowerIsBetter)
    {
        if (!atLeast)
        {
            return perSpell * general;
        }
        return lowerIsBetter ? Math.Min(perSpell, general) : Math.Max(perSpell, general);
    }

    /// <summary>A spell's mana cost at a multiplier: whole, and never below 1 while the game's is above 0.</summary>
    public static long ManaCost(long gameCost, double multiplier)
    {
        return gameCost <= 0 ? gameCost : Math.Max(1L, (long)Math.Round(gameCost * multiplier, MidpointRounding.AwayFromZero));
    }

    /// <summary>The game's value times the setting. A value of 0 or less is the game's "none" and stays.</summary>
    public static double Multiplied(double gameValue, double multiplier)
    {
        return gameValue <= 0 ? gameValue : gameValue * multiplier;
    }

    /// <summary>
    /// The lowest Cast Time multiplier at which a spell still goes out, on the setting's steps. Its cast holds the caster in the cast's
    /// shooting part (from the cast's InRangeCombatShooting keep-alive) only for the cast lock (config 0x41a7d373); the cast must still
    /// be in it when it releases the spell (its spawn and its cost), or the cast ends with no spell. Chain Lightning: 0.467 s from
    /// shooting to release over a 0.85 s lock is x0.55; Lightning Wisp: 0.267 s over 1 s is x0.3, the two limits players found. A cast
    /// with no shooting part, or that releases before it, has no limit: the setting's own minimum.
    /// </summary>
    public static double MinCastTimeMultiplier(double shootingFrom, double releaseAt, double castLock)
    {
        if (castLock <= 0 || releaseAt <= shootingFrom)
        {
            return MinCastTime;
        }
        double needed = (releaseAt - shootingFrom) / castLock;
        // Up to the next step; a share already on a step (within float noise) stays on it.
        double steps = Math.Ceiling((needed - MinCastTime) / CastTimeStep - 0.0001);
        return Math.Clamp(Math.Round(MinCastTime + Math.Max(0, steps) * CastTimeStep, 6), MinCastTime, MaxCastTime);
    }

    /// <summary>The game's value divided by the setting: a wait between two hits at a hit rate of x2 is half as long.</summary>
    public static double Divided(double gameValue, double divisor)
    {
        return gameValue <= 0 || divisor <= 0 ? gameValue : gameValue / divisor;
    }

    /// <summary>
    /// How long what a spell places lasts. It gains the time the spell's main duration (the tooltip's) gains, so a spell whose placed
    /// explosion is shorter than its tooltip (Acid Cone: 4 s under 5 s) keeps that gap. Without a tooltip duration the lifetime is multiplied.
    /// The game's 0 means no limit and stays 0.
    /// </summary>
    public static double LifetimeSeconds(double gameSeconds, double tooltipSeconds, double multiplier)
    {
        if (gameSeconds <= 0)
        {
            return gameSeconds;
        }
        // Below x1 the lifetime is multiplied, so a gap like Acid Cone's never takes it below zero.
        if (multiplier < 1 || double.IsNaN(tooltipSeconds) || tooltipSeconds <= 0)
        {
            return gameSeconds * multiplier;
        }
        return gameSeconds + tooltipSeconds * (multiplier - 1.0);
    }

    /// <summary>A tooltip line the game keeps as a whole number: milliseconds, or metres times 100.</summary>
    public static long TooltipValue(long gameValue, double multiplier)
    {
        return gameValue <= 0 ? gameValue : (long)Math.Round(gameValue * multiplier, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// How many times a template's area grows. Normally the setting. A template sized to a radius (Shroud Meteor's blast: 5 m, its
    /// tooltip's, where the game's own sphere is 3 m) gets that radius times the setting, also at x1.
    /// </summary>
    public static double AreaFactor(double gameRadius, double multiplier, double sizedToMeters)
    {
        return sizedToMeters > 0 && gameRadius > 0 ? sizedToMeters * multiplier / gameRadius : multiplier;
    }

    /// <summary>
    /// The key of a sphere's physics shape at a new size. The game keeps built physics shapes by that key, so a sphere that keeps its
    /// key at a new radius gets the old shape back. The new key is worked out from the game's key and the multiplier (FNV-1a), so one
    /// size always has the same key, and it is never the game's own.
    /// </summary>
    public static uint ShapeKey(uint gameKey, double multiplier)
    {
        uint hash = 2166136261;
        long thousandths = (long)Math.Round(multiplier * 1000.0);
        foreach (long part in new long[] { gameKey, thousandths, 0x42535043 })
        {
            for (int shift = 0; shift < 64; shift += 8)
            {
                hash ^= (byte)(part >> shift);
                hash *= 16777619;
            }
        }
        return hash == gameKey ? hash ^ 1u : hash;
    }

    /// <summary>A VFX parameter is a float32 kept as its 32 bits.</summary>
    public static float FloatFromBits(long bits)
    {
        return BitConverter.UInt32BitsToSingle(unchecked((uint)bits));
    }

    public static long BitsFromFloat(float value)
    {
        return BitConverter.SingleToUInt32Bits(value);
    }
}
