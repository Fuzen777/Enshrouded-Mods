using Kiln;

namespace PocketLight;

/// <summary>The player's settings: kiln config pocket-light-client.json in the profile's config folder, changed from the Kiln menu or by saving the file. All of them apply while playing.</summary>
public sealed class LightSettings
{
    [ConfigKey("lightOn")]
    [Comment("Light On. The Toggle Light key (K by default; Kiln menu, Keys) switches this.")]
    public bool LightOn { get; set; } = true;

    [ConfigKey("brightness")]
    [Comment("Brightness. The game's small lantern is 10, a hand torch 15.")]
    [Range(LightRules.MinBrightness, LightRules.MaxBrightness)]
    public double Brightness { get; set; } = LightRules.DefaultBrightness;

    [ConfigKey("radiusMeters")]
    [Comment("Radius (m). How far the light reaches. The game's small lantern is 25, a hand torch 45.")]
    [Range(LightRules.MinRadiusMeters, LightRules.MaxRadiusMeters)]
    public double RadiusMeters { get; set; } = LightRules.DefaultRadiusMeters;

    [ConfigKey("color")]
    [Comment("Color, as #rrggbb. #f7995c is the lantern's orange, #ffffff white, #a3e6ff a pale blue.")]
    public string Color { get; set; } = LightRules.DefaultColor;

    [ConfigKey("heightMeters")]
    [Comment("Height (m). How high above your character's feet the light sits.")]
    [Range(LightRules.MinHeightMeters, LightRules.MaxHeightMeters)]
    public double HeightMeters { get; set; } = LightRules.DefaultHeightMeters;

    [ConfigKey("forwardMeters")]
    [Comment("Forward (m). How far in front of your character the light sits; negative is behind.")]
    [Range(LightRules.MinForwardMeters, LightRules.MaxForwardMeters)]
    public double ForwardMeters { get; set; } = LightRules.DefaultForwardMeters;

    [ConfigKey("flameSize")]
    [Comment("Flame Size. 0 shows no flame, only the light; 2 is the flame of a placed lantern.")]
    [Range(LightRules.MinFlameSize, LightRules.MaxFlameSize)]
    public double FlameSize { get; set; } = LightRules.DefaultFlameSize;
}
