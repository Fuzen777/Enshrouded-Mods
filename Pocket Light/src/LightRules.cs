using System;
using System.Globalization;

namespace PocketLight;

/// <summary>What the light looks like and where it sits: the settings' values, each inside its range.</summary>
public readonly record struct LightLook(float Brightness, float RadiusMeters, byte Red, byte Green, byte Blue, float HeightMeters, float ForwardMeters, float FlameSize);

/// <summary>The settings' ranges and defaults, and how their values become a light. No Kiln here, so the tests run without the game.</summary>
public static class LightRules
{
    // The game's own lights, for scale: the small lantern is brightness 10 and radius 25, a hand torch 15 and 45, the Hollow Halls chandelier radius 64.
    public const double MinBrightness = 0.0;
    public const double MaxBrightness = 50.0;
    public const double DefaultBrightness = 10.0;

    public const double MinRadiusMeters = 1.0;
    public const double MaxRadiusMeters = 100.0;
    public const double DefaultRadiusMeters = 25.0;

    /// <summary>The small lantern's orange.</summary>
    public const string DefaultColor = "#f7995c";

    // From the character's feet: chest height, a little in front, so the character's own shadow falls behind.
    public const double MinHeightMeters = 0.0;
    public const double MaxHeightMeters = 3.0;
    public const double DefaultHeightMeters = 1.4;

    public const double MinForwardMeters = -2.0;
    public const double MaxForwardMeters = 2.0;
    public const double DefaultForwardMeters = 0.3;

    // The lantern effect's flame: 0 hides it, 2 is the size it has on the placed lantern.
    public const double MinFlameSize = 0.0;
    public const double MaxFlameSize = 2.0;
    public const double DefaultFlameSize = 0.0;

    /// <summary>Reads "#rrggbb" or "#rgb", with or without the "#", in either case.</summary>
    public static bool TryParseColor(string? text, out byte red, out byte green, out byte blue)
    {
        red = green = blue = 0;
        if (text == null)
        {
            return false;
        }
        string digits = text.Trim();
        if (digits.StartsWith('#'))
        {
            digits = digits.Substring(1);
        }
        if (digits.Length == 3)
        {
            digits = string.Concat(digits[0], digits[0], digits[1], digits[1], digits[2], digits[2]);
        }
        if (digits.Length != 6 || !uint.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint value))
        {
            return false;
        }
        red = (byte)(value >> 16);
        green = (byte)(value >> 8);
        blue = (byte)value;
        return true;
    }

    /// <summary>The light for these settings' values. A value outside its range takes the nearest allowed one; a color that can't be read is the default color.</summary>
    public static LightLook Look(double brightness, double radiusMeters, string? color, double heightMeters, double forwardMeters, double flameSize)
    {
        if (!TryParseColor(color, out byte red, out byte green, out byte blue))
        {
            TryParseColor(DefaultColor, out red, out green, out blue);
        }
        return new LightLook(
            Limit(brightness, MinBrightness, MaxBrightness, DefaultBrightness),
            Limit(radiusMeters, MinRadiusMeters, MaxRadiusMeters, DefaultRadiusMeters),
            red, green, blue,
            Limit(heightMeters, MinHeightMeters, MaxHeightMeters, DefaultHeightMeters),
            Limit(forwardMeters, MinForwardMeters, MaxForwardMeters, DefaultForwardMeters),
            Limit(flameSize, MinFlameSize, MaxFlameSize, DefaultFlameSize));
    }

    /// <summary>On or off after the toggle key was pressed this many times.</summary>
    public static bool AfterPresses(bool lightOn, int presses)
    {
        return (presses & 1) == 0 ? lightOn : !lightOn;
    }

    private static float Limit(double value, double minimum, double maximum, double fallback)
    {
        if (double.IsNaN(value))
        {
            return (float)fallback;
        }
        return (float)Math.Clamp(value, minimum, maximum);
    }
}
