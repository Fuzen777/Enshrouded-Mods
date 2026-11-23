using System;
using PocketLight;

// Checks how the settings' values become a light: colors as text, values outside their ranges, and what the toggle key does.
int failures = 0;
void Check(bool condition, string what)
{
    if (!condition)
    {
        failures++;
        Console.WriteLine("FAIL: " + what);
    }
}

// Colors: #rrggbb and #rgb, with or without the #, in either case.
Check(LightRules.TryParseColor("#f7995c", out byte red, out byte green, out byte blue) && red == 0xf7 && green == 0x99 && blue == 0x5c, "#f7995c is read");
Check(LightRules.TryParseColor("A3E6FF", out red, out green, out blue) && red == 0xa3 && green == 0xe6 && blue == 0xff, "A3E6FF is read without #, in upper case");
Check(LightRules.TryParseColor(" #fff ", out red, out green, out blue) && red == 0xff && green == 0xff && blue == 0xff, "#fff is white, spaces around it ignored");
Check(LightRules.TryParseColor("#1a2", out red, out green, out blue) && red == 0x11 && green == 0xaa && blue == 0x22, "#1a2 is #11aa22");
foreach (string? unreadable in new string?[] { null, "", "#", "orange", "#12345", "#1234567", "#gg0000", "#-12345", "0x112233" })
{
    Check(!LightRules.TryParseColor(unreadable, out _, out _, out _), $"\"{unreadable ?? "null"}\" is not a color");
}

// The defaults are the game's small lantern, with no flame.
LightLook defaults = LightRules.Look(LightRules.DefaultBrightness, LightRules.DefaultRadiusMeters, LightRules.DefaultColor,
    LightRules.DefaultHeightMeters, LightRules.DefaultForwardMeters, LightRules.DefaultFlameSize);
Check(defaults == new LightLook(10f, 25f, 0xf7, 0x99, 0x5c, 1.4f, 0.3f, 0f), "the defaults are brightness 10, radius 25 m, lantern orange, 1.4 m up, 0.3 m forward, no flame");

// A color that can't be read is the default color; the other values stay.
LightLook badColor = LightRules.Look(20, 40, "blue-ish", 2, -1, 1);
Check(badColor == new LightLook(20f, 40f, 0xf7, 0x99, 0x5c, 2f, -1f, 1f), "an unreadable color gives the lantern's orange");

// Values outside their ranges take the nearest allowed one.
LightLook tooMuch = LightRules.Look(1000, 1000, "#000000", 1000, 1000, 1000);
Check(tooMuch == new LightLook(50f, 100f, 0, 0, 0, 3f, 2f, 2f), "values above their ranges become the maximums");
LightLook tooLittle = LightRules.Look(-1000, -1000, "#000000", -1000, -1000, -1000);
Check(tooLittle == new LightLook(0f, 1f, 0, 0, 0, 0f, -2f, 0f), "values below their ranges become the minimums");
LightLook notNumbers = LightRules.Look(double.NaN, double.NaN, null, double.NaN, double.NaN, double.NaN);
Check(notNumbers == defaults, "values that are not numbers become the defaults");

// The same settings give an equal light, so an unchanged light is left running.
Check(LightRules.Look(12.5, 30, "#FFF", 1, 0, 0) == LightRules.Look(12.5, 30, "ffffff", 1, 0, 0), "the same settings written differently are the same light");
Check(LightRules.Look(12.5, 30, "#fff", 1, 0, 0) != LightRules.Look(12.6, 30, "#fff", 1, 0, 0), "a changed brightness is another light");

// The toggle key: an odd number of presses in one step switches, an even number doesn't.
Check(LightRules.AfterPresses(true, 0), "no press leaves the light on");
Check(!LightRules.AfterPresses(true, 1), "one press switches the light off");
Check(LightRules.AfterPresses(false, 1), "one press switches the light on");
Check(LightRules.AfterPresses(true, 2), "two presses in one step leave the light as it was");
Check(!LightRules.AfterPresses(true, 3), "three presses in one step switch it once");

Console.WriteLine(failures == 0 ? "All light rule checks passed." : failures + " check(s) failed.");
return failures == 0 ? 0 : 1;
