using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BetterSpells;

/// <summary>A spell with a Duration setting. Its items are found by name; each item names the template it places.</summary>
public sealed record DurationSpell(string SettingKey, Regex ItemName, bool ScalesDebuffDuration = false, bool IsTotem = false);

/// <summary>A spell with a Damage setting.</summary>
public sealed record DamageSpell(string SettingKey, Regex ItemName);

/// <summary>
/// Templates that hold a spell's area, found by name. VfxSizeIndex: which word of the template's VFX parameters holds the zone's size
/// (zero-based, -1 for none). HasReach: the template has Root's reach config. SizedToMeters: the radius its area gets at x1, in place of its own.
/// </summary>
public sealed record AreaTemplates(Regex TemplateName, int VfxSizeIndex = -1, bool HasReach = false, double SizedToMeters = 0);

/// <summary>A spell with a Radius setting. VisualKey: the toggle that also scales the templates' transforms. KeepCursorRange: the items' ground cursor stays as it is.</summary>
public sealed record RadiusSpell(string SettingKey, Regex ItemName, AreaTemplates[] Templates, string? VisualKey = null, bool KeepCursorRange = false);

/// <summary>A summon spell's cast, by the name of its sequence, and the setting that says how many summons it places.</summary>
/// EventType and Events: the kind of event that places the summon and how many of them the cast has (the Lightning Wisp's has one
/// SpawnImpact per tier, each run only for its tier).
public sealed record SummonCast(string CountKey, string SequenceName, string EventType = "SpawnEntityEvent", int Events = 1);

/// <summary>
/// An item that acts like a spell (it costs mana): its items by name, its settings' keys (null where it has none), and the template it
/// summons when its items don't name one (a TemplateImpactConfig 0xf6a785cd).
/// </summary>
public sealed record ItemSpell(string Name, Regex Items, string DamageKey, string? DurationKey, string? HealthKey, string? FixedTemplate);

/// <summary>
/// A summon spell's Perception setting: its items (the template each places, or for an item spell what it summons), templates that go
/// with them by name (Lightning Wisp's shoot attachment), the shots they fire, whose lifetime Perception multiplies, and the Shot
/// Lifetime setting that replaces the shots' own lifetime (null: the game's).
/// </summary>
public sealed record PerceptionSpell(string SettingKey, Regex ItemName, ItemSpell? ItemSpell, string[] ExtraTemplates, string[] Shots, string? ShotLifetimeKey = null, string? ThroughWallsKey = null);

/// <summary>
/// A weapon gem of the Gems tab: its name in the game without "Gem of", its item, the damage configs of its item's `impactValues`
/// (scaled by the gem's level: a base value and a scaleFactor; none for a gem that deals no damage), and the tier its row is under.
/// Configs its own program or what it spawns pass to createDamageSetup as damage (not healing); the spawned explosions and zones
/// read them from the gem, so the gem item holds every one.
/// </summary>
public sealed record GemSpell(string Name, string ItemName, uint[] DamageConfigs, int Tier)
{
    /// <summary>"Meteoric Impact" is meteoricImpactGemDamageMultiplier.</summary>
    public string DamageKey => SpellCatalog.CamelCase(Name) + "GemDamageMultiplier";
    public bool HasDamage => DamageConfigs.Length > 0;
    public string DisplayName => "Gem of " + Name;
    public Regex Item => new Regex("^" + Regex.Escape(ItemName) + "$", RegexOptions.CultureInvariant);
}

/// <summary>How a gem stat's setting changes it.</summary>
public enum GemStatKind
{
    /// <summary>One multiplier for the value at every gem level (base and scaleFactor).</summary>
    Multiply,
    /// <summary>Two multipliers: one for the base value (gem level 1), one for what each level adds (the scaleFactor).</summary>
    BaseAndLevel,
    /// <summary>A value of its own in place of the game's (seconds, or a share in percent).</summary>
    SetValue,
    /// <summary>Percentage points added to a chance, which stays inside 0 and 100%.</summary>
    ChancePoints,
    /// <summary>Levels added to a summon's, and a Sync With Player toggle.</summary>
    LevelBonus,
}

/// <summary>What a gem stat's config holds.</summary>
public enum GemUnit
{
    /// <summary>A plain number (a share of damage).</summary>
    Number,
    /// <summary>A whole number: health, armor, stamina, a level.</summary>
    Whole,
    /// <summary>A time, which Kiln reads and writes in seconds.</summary>
    Seconds,
    /// <summary>A time kept as whole milliseconds.</summary>
    Milliseconds,
    /// <summary>A share: 0.25 is 25%.</summary>
    Share,
}

/// <summary>
/// One setting of a gem other than its damage (1.1.6, the user's picks from the Gem Stat Picker): the gem, the line's label, the
/// config of the gem item's `impactValues`, how the setting changes it, what it holds, the loca id of the tooltip row that shows it
/// (0 for none), the range of a SetValue (in seconds or percent), the buffs whose own lifetime is the same time (multiplied with
/// it), the buffs whose program waits for the first of those to end (they keep the game's margin over it: Corruption's marks run
/// the explosion when its 3 s timer buff 0x9216226a ends, and a mark that ends first never explodes), and for a summon's level the gem's program and the template it spawns (for Sync With Player).
/// </summary>
public sealed record GemStat(string Gem, string Label, uint Config, GemStatKind Kind, GemUnit Unit, uint TooltipLocaId = 0,
    double Minimum = double.NaN, double Maximum = double.NaN, double Step = double.NaN, double Default = double.NaN, uint[]? Buffs = null,
    string? Program = null, string? SpawnTemplate = null, uint[]? HostBuffs = null)
{
    /// <summary>"Storm's Eye", "Mark Duration" is stormsEyeGemMarkDuration.</summary>
    public string BaseKey => SpellCatalog.CamelCase(Gem) + "Gem" + SpellCatalog.PascalCase(Label);
    public string Key => Kind switch
    {
        GemStatKind.Multiply or GemStatKind.BaseAndLevel => BaseKey + "Multiplier",
        GemStatKind.ChancePoints => BaseKey + "Points",
        GemStatKind.LevelBonus => BaseKey + "Bonus",
        _ => BaseKey,
    };
    /// <summary>BaseAndLevel's second multiplier.</summary>
    public string PerLevelKey => BaseKey + "PerLevelMultiplier";
    /// <summary>LevelBonus's toggle.</summary>
    public string SyncKey => SpellCatalog.CamelCase(Gem) + "GemSyncWithPlayer";
    public string DisplayName => "Gem of " + Gem + " " + Label;
    public string ConfigPath => "impactValues.0x" + Config.ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
    public bool IsTime => Unit is GemUnit.Seconds or GemUnit.Milliseconds;
    /// <summary>The settings window shows what a multiplier or chance gives (the value itself reads in seconds or percent).</summary>
    public bool ShowsResult => Kind is GemStatKind.Multiply or GemStatKind.BaseAndLevel or GemStatKind.ChancePoints;

    /// <summary>This stat's settings, in the order of its card's lines.</summary>
    public IEnumerable<SettingInfo> Settings()
    {
        string group = SpellCatalog.GroupGemStats;
        switch (Kind)
        {
            case GemStatKind.Multiply:
                yield return new SettingInfo(Key, DisplayName, group, SettingKind.Multiplier, SpellRules.MinGemMultiplier,
                    double.IsNaN(Maximum) ? SpellRules.MaxGemMultiplier : Maximum, SpellRules.GemMultiplierStep, 1);
                break;
            case GemStatKind.BaseAndLevel:
                yield return new SettingInfo(Key, DisplayName, group, SettingKind.Multiplier, SpellRules.MinGemMultiplier, SpellRules.MaxGemMultiplier, SpellRules.GemMultiplierStep, 1);
                yield return new SettingInfo(PerLevelKey, DisplayName + " Per Level", group, SettingKind.Multiplier, SpellRules.MinGemMultiplier, SpellRules.MaxGemMultiplier, SpellRules.GemMultiplierStep, 1);
                break;
            case GemStatKind.SetValue:
                yield return new SettingInfo(Key, DisplayName, group, Unit == GemUnit.Seconds ? SettingKind.Seconds : SettingKind.Percent, Minimum, Maximum, Step, Default);
                break;
            case GemStatKind.ChancePoints:
                yield return new SettingInfo(Key, DisplayName, group, SettingKind.Points, SpellRules.MinChancePoints, SpellRules.MaxChancePoints, 1, 0);
                break;
            case GemStatKind.LevelBonus:
                yield return new SettingInfo(Key, DisplayName, group, SettingKind.Levels, SpellRules.MinLevelBonus, SpellRules.MaxLevelBonus, 1, 0);
                yield return new SettingInfo(SyncKey, "Gem of " + Gem + " Sync With Player", group, SettingKind.Toggle, 0, 1, 1, 0);
                break;
        }
    }

    /// <summary>This stat's lines on its gem's card.</summary>
    public IEnumerable<CardLine> Lines()
    {
        yield return new CardLine(Label, Key);
        if (Kind == GemStatKind.BaseAndLevel)
        {
            yield return new CardLine("Per Level", PerLevelKey);
        }
        else if (Kind == GemStatKind.LevelBonus)
        {
            yield return new CardLine("Sync With Player", SyncKey);
        }
    }
}

/// <summary>A line of a spell's card in the settings window: the name it has there and the setting's key.</summary>
public sealed record CardLine(string Label, string Key);

/// <summary>A spell of the settings window: its name, the items whose icon stands for it, and its card's lines.</summary>
public sealed record WindowSpell(string Spell, Regex? IconItem, CardLine[] Lines);

/// <summary>A section of the settings window: a row of spell icons under a name.</summary>
public sealed record WindowSection(string Name, WindowSpell[] Spells);

/// <summary>Every setting, and which items, templates and casts of the game each one changes, by name. No Kiln here, so the tests check the names against the game data dump.</summary>
public static class SpellCatalog
{
    public const string GroupSummons = "Summons";
    public const string GroupDuration = "Duration";
    public const string GroupDamage = "Damage";
    public const string GroupRadius = "Radius";
    public const string GroupCasting = "Casting";
    public const string GroupHealth = "Health";
    public const string GroupPerception = "Perception";
    public const string GroupCost = "Cost";
    public const string GroupCastTime = "Cast Time";
    public const string GroupGeneral = "General";
    public const string GroupGemDamage = "Gem Damage";
    public const string GroupGemGeneral = "Gem General";
    public const string GroupGemStats = "Gem Stats";

    // The General card: one multiplier per kind of setting for every spell, and whether it multiplies with the spells' own or is a
    // floor under them.
    public const string GeneralCostKey = "generalCostMultiplier";
    public const string GeneralCastTimeKey = "generalCastTimeMultiplier";
    public const string GeneralDamageKey = "generalDamageMultiplier";
    public const string GeneralDurationKey = "generalDurationMultiplier";
    public const string GeneralRadiusKey = "generalRadiusMultiplier";
    public const string GeneralHealthKey = "generalHealthMultiplier";
    public const string GeneralAtLeastKey = "generalAtLeast";

    // The Gems tab's General card: one Damage multiplier for every gem and its own mode.
    public const string GemGeneralDamageKey = "gemGeneralDamageMultiplier";
    public const string GemGeneralAtLeastKey = "gemGeneralAtLeast";
    public const string GemGeneralSpell = "All Gems";

    /// <summary>The General card's spell: no item stands for it, so its button shows a word.</summary>
    public const string GeneralSpell = "All Spells";
    public const string GeneralButtonText = "All";

    /// <summary>The ManaCost config of a spell item (Kiln's alias), and its tooltip line.</summary>
    public const string ManaCostPath = "impactValues.ManaCost";
    /// <summary>The Charge Time config of a spell item (its tooltip names it), its tooltip line in milliseconds, and the cast lock (config 0x41a7d373, how long the caster stays in the cast; seen in game).</summary>
    public const string ChargeTimePath = "impactValues.0x579b2657";
    public const uint ChargeTimeTooltipLocaId = 1551270780;
    public const string CastLockPath = "impactValues.0x41a7d373";

    /// <summary>The cards with a Cast Time setting: every spell, and the items with a Charge Time (not the two scrolls). Key: the name in camel case + CastTimeMultiplier.</summary>
    public static readonly IReadOnlyList<string> CastTimeSpellNames = new[]
    {
        "Fire Bolt",
        "Fireball",
        "Fire Pillar",
        "Light Burst",
        "Ice Bolt",
        "Blizzard",
        "Chain Lightning",
        "Ball Lightning",
        "Lightning Channel",
        "Shroud Meteor",
        "Shroud Beam",
        "Totems",
        "Skeleton",
        "Lightning Wisp",
        "Acid Cone",
        "Root",
        "Bone Channel",
        "Magic Shield",
        "Skull Vessel",
        "Crawler Bomb",
    };
    public const uint ManaCostTooltipLocaId = 3193644544;

    /// <summary>The spells with a Mana Cost setting: every card but General's. The key is the name in camel case + CostMultiplier.</summary>
    public static readonly IReadOnlyList<string> CostSpellNames = new[]
    {
        "Fire Bolt",
        "Fireball",
        "Fire Pillar",
        "Light Burst",
        "Ice Bolt",
        "Blizzard",
        "Chain Lightning",
        "Ball Lightning",
        "Lightning Channel",
        "Shroud Meteor",
        "Shroud Beam",
        "Totems",
        "Skeleton",
        "Lightning Wisp",
        "Acid Cone",
        "Root",
        "Bone Channel",
        "Magic Shield",
        "Skull Vessel",
        "Fire Wisp Scroll",
        "Meteor Shower Scroll",
        "Crawler Bomb",
    };

    /// <summary>The items that act like spells, asked for by the user: the Items row.</summary>
    public static readonly IReadOnlyList<ItemSpell> ItemSpells = new[]
    {
        new ItemSpell("Skull Vessel", new Regex(@"^Consumable_Necromancer_Grenade_T\d+$", RegexOptions.CultureInvariant), "skullVesselDamageMultiplier",
            "skullVesselDurationMultiplier", "skullVesselHealthMultiplier", null),
        new ItemSpell("Fire Wisp Scroll", new Regex(@"^Consumable_Scroll_Fire_Wisp_", RegexOptions.CultureInvariant), "fireWispScrollDamageMultiplier",
            "fireWispScrollDurationMultiplier", "fireWispScrollHealthMultiplier", "Enemy_Companion_FireWisp"),
        new ItemSpell("Meteor Shower Scroll", new Regex(@"^Consumable_Scroll_Fog_Meteor_Shower$", RegexOptions.CultureInvariant), "meteorShowerScrollDamageMultiplier",
            null, null, null),
        new ItemSpell("Crawler Bomb", new Regex(@"^Consumable_CritterCrawler_Bomb_Grenade_", RegexOptions.CultureInvariant), "crawlerBombDamageMultiplier",
            "crawlerBombDurationMultiplier", "crawlerBombHealthMultiplier", null),
    };

    /// <summary>
    /// Every weapon gem (29), with the configs that deal its damage, from their Impact programs (dump 26430625). 0x5e34f09b is the
    /// damage of most gems' own program; the Gem of Fracturing's and the Gem of Aerobatics' go to the homing projectiles they spawn, the
    /// Gem of Cold Composure's to its ice explosion. The Gem of Meteoric Impact has three: on hits while primed (0x5e34f09b), the
    /// meteor's explosion (0xa8338df8) and its fire trail (0xfaf3b41b). Gems with none deal no damage of their own (healing, stamina,
    /// armour, summons); their other numbers are in GemStats.
    /// </summary>
    public static readonly IReadOnlyList<GemSpell> GemSpells = new[]
    {
        new GemSpell("Bleeding", "T1_Gem_GemOfBleeding", new[] { 0x5e34f09bu }, 1),
        new GemSpell("Burning", "T1_Gem_GemOfIgnition", new[] { 0x5e34f09bu }, 1),
        new GemSpell("Puncturing", "T1_Gem_GemOfRepetition", new[] { 0x5e34f09bu }, 1),
        new GemSpell("Skulls", "T1_Gem_GemOfSkulls", Array.Empty<uint>(), 1),
        new GemSpell("Fracturing", "T2_Gem_GemOfFracturing", new[] { 0x5e34f09bu }, 2),
        new GemSpell("Hopeful Embers", "T2_Gem_GemOfHopefulEmbers", Array.Empty<uint>(), 2),
        new GemSpell("Retaliation", "T2_Gem_GemOfRetaliation", new[] { 0x5e34f09bu }, 2),
        new GemSpell("Swiftness", "T2_Gem_GemOfSwiftness", new[] { 0x5e34f09bu }, 2),
        new GemSpell("Toxic Betrayal", "T2_Gem_GemOfToxicReaction", new[] { 0x5e34f09bu }, 2),
        new GemSpell("Agility", "T3_Gem_GemOfAgility", new[] { 0x5e34f09bu }, 3),
        new GemSpell("Storm's Eye", "T3_Gem_GemOfConduction", new[] { 0x5e34f09bu }, 3),
        new GemSpell("Lesser Necromancy", "T3_Gem_GemOfLesserNecromancy", Array.Empty<uint>(), 3),
        new GemSpell("Overpowering", "T3_Gem_GemOfOverpowering", Array.Empty<uint>(), 3),
        new GemSpell("Corruption", "T4_Gem_GemOfCorruption", new[] { 0x5e34f09bu }, 4),
        new GemSpell("Focused Light", "T4_Gem_GemOfFocusedLight", new[] { 0x5e34f09bu }, 4),
        new GemSpell("Battle Mania", "T4_Gem_GemOfObsession", Array.Empty<uint>(), 4),
        new GemSpell("Soul Reaping", "T4_Gem_GemOfSoulReaping", Array.Empty<uint>(), 4),
        new GemSpell("Bloodlust", "T5_Gem_GemOfBloodlust", new[] { 0x5e34f09bu }, 5),
        new GemSpell("Cold Composure", "T5_Gem_GemOfColdComposure", new[] { 0x5e34f09bu }, 5),
        new GemSpell("Meteoric Impact", "T5_Gem_GemOfMeteoricDescent", new[] { 0x5e34f09bu, 0xa8338df8u, 0xfaf3b41bu }, 5),
        new GemSpell("Necromancy", "T5_Gem_GemOfNecromancy", Array.Empty<uint>(), 5),
        new GemSpell("Aerobatics", "T6_Gem_GemOfAerobatics_UnderFog", new[] { 0x5e34f09bu }, 6),
        new GemSpell("Currents", "T6_Gem_GemOfCurrents_OverFog", new[] { 0x5e34f09bu }, 6),
        new GemSpell("Greater Necromancy", "T6_Gem_GemOfGreaterNecromancy", Array.Empty<uint>(), 6),
        new GemSpell("Lycanthropy", "T6_Gem_GemOfLycanthropy", Array.Empty<uint>(), 6),
        new GemSpell("Armoring", "T7_Gem_GemOfArmoring", Array.Empty<uint>(), 7),
        new GemSpell("Cleansing", "T7_Gem_GemOfCleansing", Array.Empty<uint>(), 7),
        new GemSpell("Destiny", "T7_Gem_GemOfDestiny", Array.Empty<uint>(), 7),
        new GemSpell("Hyperspecialization", "T7_Gem_GemOfHyperSpecialization", Array.Empty<uint>(), 7),
    };

    /// <summary>
    /// The gems' other numbers the user chose (1.1.6), from their programs (dump 26430625). Times are the gem item's Time configs;
    /// Puncturing's debuff and Corruption's marks also have a buff whose own lifetime is that time (Puncturing's Punctured is shared
    /// with the Gem of Retaliation). Left out: spawn waits, the Aerobatics projectile count, and configs no program reads.
    /// </summary>
    public static readonly IReadOnlyList<GemStat> GemStats = new[]
    {
        new GemStat("Bleeding", "Debuff Duration", 0x3ff81f2bu, GemStatKind.BaseAndLevel, GemUnit.Seconds, TooltipLocaId: 2609639373u),
        new GemStat("Bleeding", "Tick Damage Share", 0x854ea8fau, GemStatKind.Multiply, GemUnit.Number),
        new GemStat("Bleeding", "Tick Interval", 0xe21b4b72u, GemStatKind.SetValue, GemUnit.Seconds, Minimum: 0.1, Maximum: 1, Step: 0.05, Default: 0.5),
        new GemStat("Burning", "Debuff Duration", 0x3ff81f2bu, GemStatKind.BaseAndLevel, GemUnit.Seconds, TooltipLocaId: 2609639373u),
        new GemStat("Burning", "Cooldown", 0x2fd280c2u, GemStatKind.Multiply, GemUnit.Seconds, TooltipLocaId: 1262017188u),
        new GemStat("Burning", "Tick Damage Share", 0x854ea8fau, GemStatKind.Multiply, GemUnit.Number),
        new GemStat("Burning", "Tick Interval", 0x8fbfcd98u, GemStatKind.SetValue, GemUnit.Seconds, Minimum: 0.1, Maximum: 1, Step: 0.05, Default: 0.333),
        new GemStat("Puncturing", "Debuff Duration", 0x3ff81f2bu, GemStatKind.Multiply, GemUnit.Seconds, TooltipLocaId: 2609639373u, Buffs: new[] { 0xa7afb6b3u }),
        new GemStat("Skulls", "Trigger Chance", 0xe6ddff32u, GemStatKind.ChancePoints, GemUnit.Share, TooltipLocaId: 3117278103u),
        new GemStat("Skulls", "Skull Level", 0x1ef169b2u, GemStatKind.LevelBonus, GemUnit.Whole, TooltipLocaId: 1670435875u, Program: "8611b73a-2b76-4745-bbbf-222f87cf0199", SpawnTemplate: "d39d7329-c9f5-43ee-b070-0de8b9223ea5"),
        new GemStat("Fracturing", "Trigger Chance", 0xe6ddff32u, GemStatKind.ChancePoints, GemUnit.Share, TooltipLocaId: 3117278103u),
        new GemStat("Fracturing", "Extra Shard Chance", 0xde1695c2u, GemStatKind.SetValue, GemUnit.Share, TooltipLocaId: 3636205654u, Minimum: 1, Maximum: 100, Step: 1, Default: 50),
        new GemStat("Hopeful Embers", "Shroud Time Gained", 0x955055b2u, GemStatKind.Multiply, GemUnit.Whole, TooltipLocaId: 1125830499u),
        new GemStat("Hopeful Embers", "Shroud Time Threshold", 0x6fd0dcf1u, GemStatKind.BaseAndLevel, GemUnit.Whole, TooltipLocaId: 317193095u),
        new GemStat("Retaliation", "Trigger Chance", 0xe6ddff32u, GemStatKind.ChancePoints, GemUnit.Share, TooltipLocaId: 4215279162u),
        new GemStat("Retaliation", "Buff Duration", 0xbee6d4a9u, GemStatKind.Multiply, GemUnit.Seconds),
        new GemStat("Swiftness", "Buff Duration", 0xbee6d4a9u, GemStatKind.Multiply, GemUnit.Seconds, TooltipLocaId: 164611558u),
        new GemStat("Toxic Betrayal", "Poison Duration", 0x3ff81f2bu, GemStatKind.BaseAndLevel, GemUnit.Seconds, TooltipLocaId: 3393431252u),
        new GemStat("Agility", "Stamina On Hit", 0x5d8dc70du, GemStatKind.Multiply, GemUnit.Whole, TooltipLocaId: 555773910u),
        new GemStat("Agility", "Buff Duration", 0xbee6d4a9u, GemStatKind.Multiply, GemUnit.Seconds),
        new GemStat("Storm's Eye", "Mark Duration", 0xbee6d4a9u, GemStatKind.Multiply, GemUnit.Seconds),
        new GemStat("Storm's Eye", "Cooldown", 0x2fd280c2u, GemStatKind.Multiply, GemUnit.Seconds),
        new GemStat("Lesser Necromancy", "Trigger Chance", 0xe6ddff32u, GemStatKind.ChancePoints, GemUnit.Share, TooltipLocaId: 3117278103u),
        new GemStat("Lesser Necromancy", "Wretch Level", 0x1ef169b2u, GemStatKind.LevelBonus, GemUnit.Whole, TooltipLocaId: 382595869u, Program: "4694bf1e-feda-4064-9843-8880ac74e10f", SpawnTemplate: "c9c957ad-78bf-44fe-a891-b0771577a3b3"),
        new GemStat("Overpowering", "Stamina Damage", 0x8c622a97u, GemStatKind.Multiply, GemUnit.Whole, TooltipLocaId: 3550824552u),
        new GemStat("Corruption", "Explosion Delay", 0xbee6d4a9u, GemStatKind.Multiply, GemUnit.Seconds, Buffs: new[] { 0x9216226au }, HostBuffs: new[] { 0x26bfda91u, 0xc0a941f0u, 0xfcfb5ec6u, 0x32734118u, 0x504d285eu }),
        new GemStat("Focused Light", "Damage At 1 Stack", 0x6f307db7u, GemStatKind.Multiply, GemUnit.Share, TooltipLocaId: 3826727585u),
        new GemStat("Focused Light", "Damage At 2 Stacks", 0xb8f00949u, GemStatKind.Multiply, GemUnit.Share, TooltipLocaId: 2490680837u),
        new GemStat("Focused Light", "Damage At 3 Stacks", 0x0dd9e684u, GemStatKind.Multiply, GemUnit.Share, TooltipLocaId: 3477297884u),
        new GemStat("Battle Mania", "Health Regeneration", 0xb58d778au, GemStatKind.Multiply, GemUnit.Whole, TooltipLocaId: 1028242286u),
        new GemStat("Battle Mania", "Mana Regeneration", 0x62acdc61u, GemStatKind.Multiply, GemUnit.Whole, TooltipLocaId: 2857006190u),
        new GemStat("Battle Mania", "Stamina Regeneration", 0x867736f3u, GemStatKind.Multiply, GemUnit.Whole, TooltipLocaId: 3627626609u),
        new GemStat("Battle Mania", "Buff Duration", 0xbee6d4a9u, GemStatKind.Multiply, GemUnit.Seconds, TooltipLocaId: 164611558u),
        new GemStat("Battle Mania", "Regeneration Pause", 0x581f75ffu, GemStatKind.Multiply, GemUnit.Milliseconds, TooltipLocaId: 2609639373u, Maximum: 60),
        new GemStat("Soul Reaping", "Healing", 0x9106dde8u, GemStatKind.Multiply, GemUnit.Whole, TooltipLocaId: 4085987468u),
        new GemStat("Soul Reaping", "Debuff Duration", 0x130a0ef2u, GemStatKind.Multiply, GemUnit.Seconds, TooltipLocaId: 2609639373u),
        new GemStat("Bloodlust", "Orb Chance", 0xde1695c2u, GemStatKind.ChancePoints, GemUnit.Share, TooltipLocaId: 4193434764u),
        new GemStat("Bloodlust", "Orb Chance While Buffed", 0xe6ddff32u, GemStatKind.ChancePoints, GemUnit.Share, TooltipLocaId: 2633608633u),
        new GemStat("Cold Composure", "Physical Armor", 0x2b9400b4u, GemStatKind.Multiply, GemUnit.Whole, TooltipLocaId: 3889960709u),
        new GemStat("Cold Composure", "Magical Armor", 0x2ec994d1u, GemStatKind.Multiply, GemUnit.Whole, TooltipLocaId: 1963764541u),
        new GemStat("Cold Composure", "Movement Speed", 0x65d4559du, GemStatKind.SetValue, GemUnit.Share, TooltipLocaId: 2378794516u, Minimum: -90, Maximum: 50, Step: 5, Default: -29.99),
        new GemStat("Cold Composure", "Charge Time", 0xbee6d4a9u, GemStatKind.Multiply, GemUnit.Seconds),
        new GemStat("Meteoric Impact", "Buff Duration", 0xc9c2d7f1u, GemStatKind.BaseAndLevel, GemUnit.Seconds, TooltipLocaId: 164611558u),
        new GemStat("Necromancy", "Trigger Chance", 0xe6ddff32u, GemStatKind.ChancePoints, GemUnit.Share, TooltipLocaId: 3117278103u),
        new GemStat("Necromancy", "Damned Level", 0x1ef169b2u, GemStatKind.LevelBonus, GemUnit.Whole, TooltipLocaId: 1783032156u, Program: "4694bf1e-feda-4064-9843-8880ac74e10f", SpawnTemplate: "10f892be-2374-4d31-ae27-85e0a36f3853"),
        new GemStat("Aerobatics", "Volley Wait", 0xe21b4b72u, GemStatKind.SetValue, GemUnit.Seconds, Minimum: 0.1, Maximum: 10, Step: 0.1, Default: 1),
        new GemStat("Currents", "Trigger Chance", 0xe6ddff32u, GemStatKind.ChancePoints, GemUnit.Share, TooltipLocaId: 3216647939u),
        new GemStat("Currents", "Debuff Duration", 0x3ff81f2bu, GemStatKind.BaseAndLevel, GemUnit.Seconds, TooltipLocaId: 2609639373u),
        new GemStat("Greater Necromancy", "Trigger Chance", 0xe6ddff32u, GemStatKind.ChancePoints, GemUnit.Share, TooltipLocaId: 3117278103u),
        new GemStat("Greater Necromancy", "Reaper Level", 0x1ef169b2u, GemStatKind.LevelBonus, GemUnit.Whole, TooltipLocaId: 1783032156u, Program: "4694bf1e-feda-4064-9843-8880ac74e10f", SpawnTemplate: "ef0c170d-0653-4517-88a7-0c4c9aba8139"),
        new GemStat("Lycanthropy", "Trigger Chance", 0xe6ddff32u, GemStatKind.ChancePoints, GemUnit.Share, TooltipLocaId: 3117278103u),
        new GemStat("Lycanthropy", "Health On Orb", 0x9106dde8u, GemStatKind.Multiply, GemUnit.Whole, TooltipLocaId: 3930573097u),
        new GemStat("Lycanthropy", "Mana On Orb", 0xe395f2c8u, GemStatKind.Multiply, GemUnit.Whole, TooltipLocaId: 2471974202u),
        new GemStat("Lycanthropy", "Max Health", 0x766757f5u, GemStatKind.Multiply, GemUnit.Whole, TooltipLocaId: 4188525107u),
        new GemStat("Lycanthropy", "Max Mana", 0xe38f6439u, GemStatKind.Multiply, GemUnit.Whole, TooltipLocaId: 3994206546u),
        new GemStat("Lycanthropy", "Max Stamina", 0x3548f806u, GemStatKind.Multiply, GemUnit.Whole, TooltipLocaId: 3572690653u),
        new GemStat("Lycanthropy", "Melee Damage", 0x958958bcu, GemStatKind.Multiply, GemUnit.Share, TooltipLocaId: 1527944368u),
        new GemStat("Lycanthropy", "Buff Duration", 0xc9c2d7f1u, GemStatKind.BaseAndLevel, GemUnit.Seconds, TooltipLocaId: 164611558u),
        new GemStat("Armoring", "Buff Duration", 0xc9c2d7f1u, GemStatKind.BaseAndLevel, GemUnit.Seconds, TooltipLocaId: 164611558u),
        new GemStat("Cleansing", "Debuff Duration", 0x3ff81f2bu, GemStatKind.BaseAndLevel, GemUnit.Seconds, TooltipLocaId: 2609639373u),
        new GemStat("Destiny", "Health On Orb", 0x9106dde8u, GemStatKind.Multiply, GemUnit.Whole, TooltipLocaId: 3930573097u),
        new GemStat("Destiny", "Mana On Orb", 0xe395f2c8u, GemStatKind.Multiply, GemUnit.Whole, TooltipLocaId: 2471974202u),
        new GemStat("Destiny", "Stamina On Orb", 0x5d8dc70du, GemStatKind.Multiply, GemUnit.Whole, TooltipLocaId: 3485312036u),
        new GemStat("Destiny", "Mark Duration", 0x130a0ef2u, GemStatKind.Multiply, GemUnit.Seconds, TooltipLocaId: 2609639373u),
        new GemStat("Hyperspecialization", "Buff Duration", 0xbee6d4a9u, GemStatKind.Multiply, GemUnit.Seconds, TooltipLocaId: 164611558u),
    };

    public const string SkeletonsPerCastKey = "skeletonsPerCast";
    public const string TotemsPerCastKey = "totemsPerCast";
    public const string SummonSpreadKey = "summonSpread";
    public const string SkullVesselsPerCastKey = "skullVesselsPerCast";
    public const string LightningWispsPerCastKey = "lightningWispsPerCast";
    public const string FireWispsPerUseKey = "fireWispsPerUse";
    public const string KeepOlderWispsKey = "keepOlderWisps";
    /// <summary>The Lightning Wisp's cast and its four SpawnImpact events (one per tier).</summary>
    public const string WispCastSequence = "Attack_Player_Staff_Overlay_ShockWisp";
    public const int WispCastEvents = 4;
    public const string TotemPerceptionKey = "totemPerceptionMultiplier";
    // Shots Through Walls (1.2.0): a summon's shots stop colliding with terrain and water and still hit enemies.
    public const string TotemThroughWallsKey = "totemShotsThroughWalls";
    public const string LightningWispThroughWallsKey = "lightningWispShotsThroughWalls";
    public const string FireWispScrollThroughWallsKey = "fireWispScrollShotsThroughWalls";
    public const string SkeletonPerceptionKey = "skeletonPerceptionMultiplier";
    public const string LightningWispPerceptionKey = "lightningWispPerceptionMultiplier";
    public const string SkullVesselPerceptionKey = "skullVesselPerceptionMultiplier";
    public const string FireWispScrollPerceptionKey = "fireWispScrollPerceptionMultiplier";
    public const string LightningWispShotLifetimeKey = "lightningWispShotLifetime";
    public const string FireWispScrollShotLifetimeKey = "fireWispScrollShotLifetime";
    public const string MaxTotemsKey = "maxTotems";
    public const string MaxSkeletonsKey = "maxSkeletons";
    public const string TotemHealthKey = "totemHealthMultiplier";
    public const string SkeletonHealthKey = "skeletonHealthMultiplier";
    public const string MagicShieldHealthKey = "magicShieldHealthMultiplier";
    public const string InvulnerableTotemsKey = "invulnerableTotems";
    public const string EnemiesIgnoreTotemsKey = "enemiesIgnoreTotems";
    public const string ChainLockOnKey = "chainLightningLockOnMultiplier";
    public const string BallSpeedKey = "ballLightningSpeedMultiplier";
    public const string BallRadiusKey = "ballLightningRadiusMultiplier";
    public const string ChainRadiusKey = "chainLightningRadiusMultiplier";
    public const string BallHitRateKey = "ballLightningHitRateMultiplier";
    public const string BallThroughWallsKey = "ballLightningThroughWalls";
    public const string ShroudMeteorVisualKey = "shroudMeteorVisual";
    public const string LightBurstVisualKey = "lightBurstVisual";

    /// <summary>Every spell item: Ammo_T3_Spell_Ice_Totem_lvl17, Ammo_T7_Spell_Fire_Ball_lvl45_Eternal.</summary>
    public static readonly Regex AnySpellItem = new Regex(@"^Ammo_T\d+_Spell_", RegexOptions.CultureInvariant);

    private static readonly Regex TotemItem = Item("[A-Za-z]+_Totem");
    private static readonly Regex SkeletonItem = Item("Skeleton");
    public static readonly Regex ChainLightningItem = Item("Lightning_Chain");

    public static readonly IReadOnlyList<SettingInfo> Settings = WithCostAndGeneral(new[]
    {
        Count(SkeletonsPerCastKey, "Skeletons Per Cast"),
        Count(TotemsPerCastKey, "Totems Per Cast"),
        new SettingInfo(LightningWispsPerCastKey, "Lightning Wisps Per Cast", GroupSummons, SettingKind.Count, SpellRules.MinSummonsPerCast, SpellRules.MaxSummonsPerCast, 1, 1),
        new SettingInfo(KeepOlderWispsKey, "Keep Older Wisps", GroupSummons, SettingKind.Toggle, 0, 1, 1, 0),
        new SettingInfo(FireWispsPerUseKey, "Fire Wisps Per Use", GroupSummons, SettingKind.Count, SpellRules.MinSummonsPerCast, SpellRules.MaxSummonsPerCast, 1, 1),
        new SettingInfo(SkullVesselsPerCastKey, "Skull Vessels Per Use", GroupSummons, SettingKind.Count, SpellRules.MinSummonsPerCast, SpellRules.MaxSummonsPerCast, 1, 1),
        new SettingInfo(MaxTotemsKey, "Max Totems", GroupSummons, SettingKind.Count, SpellRules.MinSummonCap, SpellRules.MaxSummonCap, 1, SpellRules.DefaultSummonCap),
        new SettingInfo(MaxSkeletonsKey, "Max Skeletons", GroupSummons, SettingKind.Count, SpellRules.MinSummonCap, SpellRules.MaxSummonCap, 1, SpellRules.DefaultSummonCap),
        Health(TotemHealthKey, "Totem Health"),
        Health(SkeletonHealthKey, "Skeleton Health"),
        Health(MagicShieldHealthKey, "Magic Shield Health"),
        Perception(TotemPerceptionKey, "Totem Perception"),
        Perception(SkeletonPerceptionKey, "Skeleton Perception"),
        Perception(LightningWispPerceptionKey, "Lightning Wisp Perception"),
        Perception(SkullVesselPerceptionKey, "Skull Vessel Perception"),
        Perception(FireWispScrollPerceptionKey, "Fire Wisp Scroll Perception"),
        ShotLifetime(LightningWispShotLifetimeKey, "Lightning Wisp Shot Lifetime"),
        ShotLifetime(FireWispScrollShotLifetimeKey, "Fire Wisp Scroll Shot Lifetime"),
        new SettingInfo(TotemThroughWallsKey, "Totem Shots Through Walls", GroupPerception, SettingKind.Toggle, 0, 1, 1, 0),
        new SettingInfo(LightningWispThroughWallsKey, "Lightning Wisp Shots Through Walls", GroupPerception, SettingKind.Toggle, 0, 1, 1, 0),
        new SettingInfo(FireWispScrollThroughWallsKey, "Fire Wisp Scroll Shots Through Walls", GroupPerception, SettingKind.Toggle, 0, 1, 1, 0),
        new SettingInfo(SummonSpreadKey, "Totem Spread", GroupSummons, SettingKind.Units, SpellRules.MinSpreadUnits, SpellRules.MaxSpreadUnits, SpellRules.SpreadStep, SpellRules.DefaultSpreadUnits),

        Duration("totemDurationMultiplier", "Totem Duration", 1),
        new SettingInfo(InvulnerableTotemsKey, "Invulnerable Totems", GroupDuration, SettingKind.Toggle, 0, 1, 1, 0),
        new SettingInfo(EnemiesIgnoreTotemsKey, "Enemies Ignore Totems", GroupDuration, SettingKind.Toggle, 0, 1, 1, 0),
        Duration("skeletonDurationMultiplier", "Skeleton Duration", 1),
        Duration("magicShieldDurationMultiplier", "Magic Shield Duration", 1),
        Duration("lightningWispDurationMultiplier", "Lightning Wisp Duration", 1),
        Duration("blizzardDurationMultiplier", "Blizzard Duration", 1),
        Duration("lightningBallDurationMultiplier", "Lightning Ball Duration", 1),
        Duration("rootDurationMultiplier", "Root Duration", 1),
        Duration("firePillarDurationMultiplier", "Fire Pillar Duration", 1),
        Duration("acidConeDurationMultiplier", "Acid Cone Duration", 1),

        Damage("fireBoltDamageMultiplier", "Fire Bolt Damage"),
        Damage("fireballDamageMultiplier", "Fireball Damage"),
        Damage("iceBoltDamageMultiplier", "Ice Bolt Damage"),
        Damage("acidConeDamageMultiplier", "Acid Cone Damage"),
        Damage("firePillarDamageMultiplier", "Fire Pillar Damage"),
        Damage("blizzardDamageMultiplier", "Blizzard Damage"),
        Damage("lightningBallDamageMultiplier", "Lightning Ball Damage"),
        Damage("chainLightningDamageMultiplier", "Chain Lightning Damage"),
        Damage("shroudMeteorDamageMultiplier", "Shroud Meteor Damage"),
        Damage("rootDamageMultiplier", "Root Damage"),
        Damage("lightBurstDamageMultiplier", "Light Burst Damage"),
        Damage("totemDamageMultiplier", "Totem Damage"),
        Damage("lightningWispDamageMultiplier", "Lightning Wisp Damage"),
        Damage("skeletonDamageMultiplier", "Skeleton Damage"),
        Damage("boneChannelDamageMultiplier", "Bone Channel Damage"),
        Damage("shroudBeamDamageMultiplier", "Shroud Beam Damage"),
        Damage("lightningChannelDamageMultiplier", "Lightning Channel Damage"),

        Radius("fireballRadiusMultiplier", "Fireball Radius", SpellRules.DefaultRadius),
        Radius("iceBoltRadiusMultiplier", "Ice Bolt Radius", SpellRules.DefaultRadius),
        Radius("rootRadiusMultiplier", "Root Radius", SpellRules.DefaultRadius),
        Radius("firePillarRadiusMultiplier", "Fire Pillar Radius", SpellRules.DefaultRadius),
        Radius("blizzardRadiusMultiplier", "Blizzard Radius", SpellRules.DefaultRadius),
        Radius("shroudMeteorRadiusMultiplier", "Shroud Meteor Radius", SpellRules.DefaultRadius),
        new SettingInfo(ShroudMeteorVisualKey, "Shroud Meteor Visual", GroupRadius, SettingKind.Toggle, 0, 1, 1, 1),
        Radius("lightBurstRadiusMultiplier", "Light Burst Radius", SpellRules.DefaultRadius),
        new SettingInfo(LightBurstVisualKey, "Light Burst Visual (Experimental)", GroupRadius, SettingKind.Toggle, 0, 1, 1, 1),
        Radius(BallRadiusKey, "Ball Lightning Radius", 1),
        new SettingInfo(ChainRadiusKey, "Chain Lightning Radius", GroupRadius, SettingKind.Multiplier, SpellRules.MinRadius, SpellRules.MaxChainRadius, SpellRules.RadiusStep, 1),

        new SettingInfo(ChainLockOnKey, "Chain Lightning Lock-On Range", GroupCasting, SettingKind.Multiplier, SpellRules.MinLockOn, SpellRules.MaxLockOn, SpellRules.CastingStep, 1),
        new SettingInfo(BallSpeedKey, "Ball Lightning Speed", GroupCasting, SettingKind.Multiplier, SpellRules.MinBallSpeed, SpellRules.MaxBallSpeed, SpellRules.CastingStep, 1),
        new SettingInfo(BallHitRateKey, "Ball Lightning Hit Rate", GroupCasting, SettingKind.Multiplier, SpellRules.MinBallHitRate, SpellRules.MaxBallHitRate, SpellRules.CastingStep, 1),
        new SettingInfo(BallThroughWallsKey, "Ball Lightning Through Walls", GroupCasting, SettingKind.Toggle, 0, 1, 1, 0),
    });

    /// <summary>The groups of the settings page, in its order.</summary>
    public static readonly IReadOnlyList<string> Groups = new[] { GroupSummons, GroupDuration, GroupDamage, GroupRadius, GroupCasting, GroupHealth, GroupPerception, GroupCost, GroupCastTime, GroupGeneral, GroupGemDamage, GroupGemGeneral, GroupGemStats };

    // Root's and Blizzard's zones hold enemies and slow them for the spell's main duration (their DebuffDuration config), so it follows the setting.
    public static readonly IReadOnlyList<DurationSpell> DurationSpells = new[]
    {
        new DurationSpell("totemDurationMultiplier", TotemItem, IsTotem: true),
        new DurationSpell("skeletonDurationMultiplier", SkeletonItem),
        new DurationSpell("magicShieldDurationMultiplier", Item("MagicShield")),
        new DurationSpell("lightningWispDurationMultiplier", Item("Lightning_Wisp")),
        new DurationSpell("blizzardDurationMultiplier", Item("Ice_Blizzard"), ScalesDebuffDuration: true),
        new DurationSpell("lightningBallDurationMultiplier", Item("Lightning_Ball")),
        new DurationSpell("rootDurationMultiplier", Item("Root"), ScalesDebuffDuration: true),
        new DurationSpell("firePillarDurationMultiplier", Item("Fire_Pillar")),
        new DurationSpell("acidConeDurationMultiplier", Item("Acid_Cone")),
    };

    public static readonly IReadOnlyList<DamageSpell> DamageSpells = new[]
    {
        new DamageSpell("fireBoltDamageMultiplier", Item("Fire_Bolt")),
        new DamageSpell("fireballDamageMultiplier", Item("Fire_Ball")),
        new DamageSpell("iceBoltDamageMultiplier", Item("Ice_Bolt")),
        new DamageSpell("acidConeDamageMultiplier", Item("Acid_Cone")),
        new DamageSpell("firePillarDamageMultiplier", Item("Fire_Pillar")),
        new DamageSpell("blizzardDamageMultiplier", Item("Ice_Blizzard")),
        new DamageSpell("lightningBallDamageMultiplier", Item("Lightning_Ball")),
        new DamageSpell("chainLightningDamageMultiplier", Item("Lightning_Chain")),
        new DamageSpell("shroudMeteorDamageMultiplier", Item("Fog_Meteor")),
        new DamageSpell("rootDamageMultiplier", Item("Root")),
        new DamageSpell("lightBurstDamageMultiplier", Item("Fire_Lightburst")),
        new DamageSpell("totemDamageMultiplier", TotemItem),
        new DamageSpell("lightningWispDamageMultiplier", Item("Lightning_Wisp")),
        new DamageSpell("skeletonDamageMultiplier", SkeletonItem),
        new DamageSpell("boneChannelDamageMultiplier", Item("Bone_Channel")),
        new DamageSpell("shroudBeamDamageMultiplier", Item("Fog_Beam_Channel")),
        new DamageSpell("lightningChannelDamageMultiplier", Item("Lightning_Channel")),
    };

    // A spell's area is the collider of a template it places or spawns: an explosion per tier, or a zone per tier.
    public static readonly IReadOnlyList<RadiusSpell> RadiusSpells = new[]
    {
        new RadiusSpell("fireballRadiusMultiplier", Item("Fire_Ball"), new[] { Templates(@"^Explosion_Player_Spell_Fireball_\d+(_on_Water)?$") }),
        new RadiusSpell("iceBoltRadiusMultiplier", Item("Ice_Bolt"), new[]
        {
            Templates(@"^Explosion_Player_Spell_IceBolt_\d+$"),
            Templates(@"^Zone_Player_Spell_IceBolt_\d+$", vfxSizeIndex: 8),
        }),
        new RadiusSpell("rootRadiusMultiplier", Item("Root"), new[] { Templates(@"^Zone_Spell_Rooting_\d+$", vfxSizeIndex: 3, hasReach: true) }),
        new RadiusSpell("firePillarRadiusMultiplier", Item("Fire_Pillar"), new[] { Templates(@"^Zone_Spell_Fire_Pillar_\d+$", vfxSizeIndex: 0) }),
        new RadiusSpell("blizzardRadiusMultiplier", Item("Ice_Blizzard"), new[] { Templates(@"^Zone_Spell_Blizzard_\d+$", vfxSizeIndex: 0) }),
        // Its ground cursor stays: the crater's script reads it, and 25 m of terrain per meteor hitched the game.
        new RadiusSpell("shroudMeteorRadiusMultiplier", Item("Fog_Meteor"), new[] { Templates(@"^Explosion_Player_Spell_Meteor_Explosion$", sizedToMeters: 5) },
            VisualKey: ShroudMeteorVisualKey, KeepCursorRange: true),
        new RadiusSpell("lightBurstRadiusMultiplier", Item("Fire_Lightburst"), new[] { Templates(@"^Explosion_Player_Spell_LightBurst_Explosion\d+$") },
            VisualKey: LightBurstVisualKey),
    };

    public static readonly IReadOnlyList<SummonCast> SummonCasts = new[]
    {
        new SummonCast(SkeletonsPerCastKey, "Attack_Player_Staff_FixedAnim_Summon_Skeleton"),
        new SummonCast(TotemsPerCastKey, "Attack_Player_Staff_FixedAnim_Totem_Fire"),
        new SummonCast(TotemsPerCastKey, "Attack_Player_Staff_FixedAnim_Totem_Ice"),
        new SummonCast(TotemsPerCastKey, "Attack_Player_Staff_FixedAnim_Totem_Lightning"),
        // The Skull Summoning Vessels' throw, shared by every tier: each vessel thrown summons its skull where it lands.
        new SummonCast(SkullVesselsPerCastKey, "UseItem_Consumable_Tool_SkullSummon_Throw"),
        // The Lightning Wisp's cast places the wisp through a script (one SpawnImpact per tier) that also removes the caster's older wisps
        // it touches; the Fire Wisp scroll spawns its wisp directly.
        new SummonCast(LightningWispsPerCastKey, WispCastSequence, "SpawnImpact", WispCastEvents),
        new SummonCast(FireWispsPerUseKey, "UseItem_Scroll_Companion_FireWisp"),
    };


    /// <summary>
    /// The summons' Perception: each summon's sight (Enemy01BehaviorState) and detection sphere (20 units for wisps and totems), and the
    /// lifetime of their shots so they still reach that far. Lightning Wisp's and the Fire Wisp's shots live 2 s at 5 to 10 units/s in the
    /// game's data, too short for their 20-unit sphere: their Shot Lifetime (5 s by default) replaces it. Totems' live 3 s at 8 to 12
    /// units/s, which reaches. Skeletons and skulls have no shots.
    /// </summary>
    public static readonly IReadOnlyList<PerceptionSpell> PerceptionSpells = new[]
    {
        new PerceptionSpell(TotemPerceptionKey, TotemItem, null, Array.Empty<string>(),
            new[] { "Projectile_Companion_Totem_Fire", "Projectile_Companion_Totem_Ice", "Projectile_Companion_Totem_Lightning" }, null, TotemThroughWallsKey),
        new PerceptionSpell(SkeletonPerceptionKey, SkeletonItem, null, Array.Empty<string>(), Array.Empty<string>()),
        new PerceptionSpell(LightningWispPerceptionKey, Item("Lightning_Wisp"), null, new[] { "Attack_Companion_ShockWisp_Shoot_Attachment" },
            new[] { "Projectile_Companion_ShockWisp" }, LightningWispShotLifetimeKey, LightningWispThroughWallsKey),
        new PerceptionSpell(SkullVesselPerceptionKey, ItemSpells[0].Items, ItemSpells[0], Array.Empty<string>(), Array.Empty<string>()),
        new PerceptionSpell(FireWispScrollPerceptionKey, ItemSpells[1].Items, ItemSpells[1], Array.Empty<string>(), new[] { "Projectile_Companion_FireWisp" }, FireWispScrollShotLifetimeKey, FireWispScrollThroughWallsKey),
    };

    /// <summary>The player's templates, whose MaxTotemCount and MaxSkeletonSpellCount every player gets when it spawns.</summary>
    public static readonly IReadOnlyList<string> PlayerTemplates = new[] { "1_Player", "1_Player_AG2" };

    /// <summary>The spells whose placed entity's health a Health setting multiplies.</summary>
    public static readonly IReadOnlyList<(string SettingKey, Regex ItemName)> HealthSpells = new[]
    {
        (TotemHealthKey, TotemItem), (SkeletonHealthKey, SkeletonItem), (MagicShieldHealthKey, Item("MagicShield")),
    };

    /// <summary>Chain Lightning's cast, shared by its five items: one SpawnImpact event whose own sphere is the area the spell hits.</summary>
    public const string ChainCastSequence = "Attack_Player_Staff_FixedAnim_ChainLightning";

    /// <summary>Ball Lightning's cast, shared by its three items, and the ball each tier spawns.</summary>
    public const string BallCastSequence = "Attack_Player_Staff_FixedAnim_LightningBall";
    public static readonly Regex BallTemplate = new Regex(@"^Projectile_Player_Staff_LightningBall_\d+$", RegexOptions.CultureInvariant);

    // The settings window: a section per element, then Summons (what they do counts more than their element) and Others, each a row of
    // spell icons; the chosen spell's card has a line per setting. Every setting is on exactly one card.
    public static readonly IReadOnlyList<WindowSection> WindowSections = new[]
    {
        new WindowSection("General", new[]
        {
            new WindowSpell(GeneralSpell, null, new[]
            {
                Line("Mana Cost", GeneralCostKey), Line("Cast Time", GeneralCastTimeKey), Line("Damage", GeneralDamageKey), Line("Duration", GeneralDurationKey), Line("Radius", GeneralRadiusKey),
                Line("Health", GeneralHealthKey), Line("Mode", GeneralAtLeastKey),
            }),
        }),
        new WindowSection("Fire", new[]
        {
            Spell("Fire Bolt", Item("Fire_Bolt"), Line("Damage", "fireBoltDamageMultiplier")),
            Spell("Fireball", Item("Fire_Ball"), Line("Damage", "fireballDamageMultiplier"), Line("Radius", "fireballRadiusMultiplier")),
            Spell("Fire Pillar", Item("Fire_Pillar"), Line("Duration", "firePillarDurationMultiplier"), Line("Damage", "firePillarDamageMultiplier"),
                Line("Radius", "firePillarRadiusMultiplier")),
            Spell("Light Burst", Item("Fire_Lightburst"), Line("Damage", "lightBurstDamageMultiplier"), Line("Radius", "lightBurstRadiusMultiplier"),
                Line("Visual (Experimental)", LightBurstVisualKey)),
        }),
        new WindowSection("Ice", new[]
        {
            Spell("Ice Bolt", Item("Ice_Bolt"), Line("Damage", "iceBoltDamageMultiplier"), Line("Radius", "iceBoltRadiusMultiplier")),
            Spell("Blizzard", Item("Ice_Blizzard"), Line("Duration", "blizzardDurationMultiplier"), Line("Damage", "blizzardDamageMultiplier"),
                Line("Radius", "blizzardRadiusMultiplier")),
        }),
        new WindowSection("Lightning", new[]
        {
            Spell("Chain Lightning", ChainLightningItem, Line("Damage", "chainLightningDamageMultiplier"), Line("Radius", ChainRadiusKey),
                Line("Lock-On Range", ChainLockOnKey)),
            Spell("Ball Lightning", Item("Lightning_Ball"), Line("Duration", "lightningBallDurationMultiplier"), Line("Damage", "lightningBallDamageMultiplier"),
                Line("Radius", BallRadiusKey), Line("Speed", BallSpeedKey), Line("Hit Rate", BallHitRateKey),
                Line("Through Walls", BallThroughWallsKey)),
            Spell("Lightning Channel", Item("Lightning_Channel"), Line("Damage", "lightningChannelDamageMultiplier")),
        }),
        new WindowSection("Shroud", new[]
        {
            Spell("Shroud Meteor", Item("Fog_Meteor"), Line("Damage", "shroudMeteorDamageMultiplier"), Line("Radius", "shroudMeteorRadiusMultiplier"),
                Line("Visual", ShroudMeteorVisualKey)),
            Spell("Shroud Beam", Item("Fog_Beam_Channel"), Line("Damage", "shroudBeamDamageMultiplier")),
        }),
        new WindowSection("Summons", new[]
        {
            Spell("Totems", TotemItem, Line("Duration", "totemDurationMultiplier"), Line("Damage", "totemDamageMultiplier"), Line("Health", TotemHealthKey),
                Line("Perception", TotemPerceptionKey), Line("Shots Through Walls", TotemThroughWallsKey), Line("Max Totems", MaxTotemsKey), Line("Per Cast", TotemsPerCastKey),
                Line("Spread", SummonSpreadKey), Line("Invulnerable", InvulnerableTotemsKey), Line("Enemies Ignore Them", EnemiesIgnoreTotemsKey)),
            Spell("Skeleton", SkeletonItem, Line("Duration", "skeletonDurationMultiplier"), Line("Damage", "skeletonDamageMultiplier"),
                Line("Health", SkeletonHealthKey), Line("Perception", SkeletonPerceptionKey), Line("Max Skeletons", MaxSkeletonsKey), Line("Per Cast", SkeletonsPerCastKey)),
            Spell("Lightning Wisp", Item("Lightning_Wisp"), Line("Duration", "lightningWispDurationMultiplier"), Line("Damage", "lightningWispDamageMultiplier"),
                Line("Perception", LightningWispPerceptionKey), Line("Shot Lifetime", LightningWispShotLifetimeKey), Line("Shots Through Walls", LightningWispThroughWallsKey), Line("Per Cast", LightningWispsPerCastKey), Line("Keep Older Wisps", KeepOlderWispsKey)),
        }),
        new WindowSection("Others", new[]
        {
            Spell("Acid Cone", Item("Acid_Cone"), Line("Duration", "acidConeDurationMultiplier"), Line("Damage", "acidConeDamageMultiplier")),
            Spell("Root", Item("Root"), Line("Duration", "rootDurationMultiplier"), Line("Damage", "rootDamageMultiplier"), Line("Radius", "rootRadiusMultiplier")),
            Spell("Bone Channel", Item("Bone_Channel"), Line("Damage", "boneChannelDamageMultiplier")),
            Spell("Magic Shield", Item("MagicShield"), Line("Duration", "magicShieldDurationMultiplier"), Line("Health", MagicShieldHealthKey)),
        }),
        new WindowSection("Items", new[]
        {
            Spell("Skull Vessel", ItemSpells[0].Items, Line("Duration", "skullVesselDurationMultiplier"), Line("Damage", "skullVesselDamageMultiplier"), Line("Health", "skullVesselHealthMultiplier"),
                Line("Perception", SkullVesselPerceptionKey), Line("Per Use", SkullVesselsPerCastKey)),
            Spell("Fire Wisp Scroll", ItemSpells[1].Items, Line("Duration", "fireWispScrollDurationMultiplier"), Line("Damage", "fireWispScrollDamageMultiplier"), Line("Health", "fireWispScrollHealthMultiplier"),
                Line("Perception", FireWispScrollPerceptionKey), Line("Shot Lifetime", FireWispScrollShotLifetimeKey), Line("Shots Through Walls", FireWispScrollThroughWallsKey), Line("Per Use", FireWispsPerUseKey)),
            Spell("Meteor Shower Scroll", ItemSpells[2].Items, Line("Damage", "meteorShowerScrollDamageMultiplier")),
            Spell("Crawler Bomb", ItemSpells[3].Items, Line("Duration", "crawlerBombDurationMultiplier"), Line("Damage", "crawlerBombDamageMultiplier"), Line("Health", "crawlerBombHealthMultiplier")),
        }),
    };

    /// <summary>Every spell of the settings window, section after section: the index the window keeps for the chosen spell.</summary>
    public static readonly IReadOnlyList<WindowSpell> WindowSpells = Flatten(WindowSections);

    /// <summary>The Gems tab: General, then a row per tier, each gem's card its Damage line and its stats' lines.</summary>
    public static readonly IReadOnlyList<WindowSection> GemSections = MakeGemSections();

    /// <summary>Every gem card of the Gems tab, section after section.</summary>
    public static readonly IReadOnlyList<WindowSpell> GemWindowSpells = Flatten(GemSections);

    private static WindowSection[] MakeGemSections()
    {
        var sections = new List<WindowSection>
        {
            new WindowSection("General", new[] { new WindowSpell(GemGeneralSpell, null, new[] { Line("Damage", GemGeneralDamageKey), Line("Mode", GemGeneralAtLeastKey) }) }),
        };
        foreach (int tier in GemSpells.Select(gem => gem.Tier).Distinct().OrderBy(tier => tier))
        {
            WindowSpell[] gems = GemSpells.Where(gem => gem.Tier == tier).Select(gem => new WindowSpell(gem.DisplayName, gem.Item, GemLines(gem))).ToArray();
            sections.Add(new WindowSection("Tier " + tier.ToString(System.Globalization.CultureInfo.InvariantCulture), gems));
        }
        return sections.ToArray();
    }

    // A gem's card: its Damage (when it deals any), then its stats' lines.
    private static CardLine[] GemLines(GemSpell gem)
    {
        var lines = new List<CardLine>();
        if (gem.HasDamage)
        {
            lines.Add(Line("Damage", gem.DamageKey));
        }
        lines.AddRange(GemStats.Where(stat => stat.Gem == gem.Name).SelectMany(stat => stat.Lines()));
        return lines.ToArray();
    }

    // A spell's card: its Mana Cost first, then its own lines.
    private static WindowSpell Spell(string name, Regex iconItem, params CardLine[] lines)
    {
        var withCost = new List<CardLine> { Line("Mana Cost", CostKey(name)) };
        foreach (string castName in CastTimeSpellNames)
        {
            if (castName == name)
            {
                withCost.Add(Line("Cast Time", CastTimeKey(name)));
            }
        }
        withCost.AddRange(lines);
        return new WindowSpell(name, iconItem, withCost.ToArray());
    }

    /// <summary>The key of a spell's Mana Cost setting: "Fire Bolt" is fireBoltCostMultiplier.</summary>
    public static string CostKey(string spellName)
    {
        return CamelCase(spellName) + "CostMultiplier";
    }

    /// <summary>A name in camel case without its apostrophes: "Storm's Eye" is stormsEye.</summary>
    public static string CamelCase(string name)
    {
        string[] words = name.Replace("'", "", StringComparison.Ordinal).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var key = new System.Text.StringBuilder(words[0].ToLowerInvariant());
        for (int index = 1; index < words.Length; index++)
        {
            key.Append(char.ToUpperInvariant(words[index][0])).Append(words[index], 1, words[index].Length - 1);
        }
        return key.ToString();
    }

    /// <summary>A name in Pascal case without its apostrophes: "Damage At 1 Stack" is DamageAt1Stack.</summary>
    public static string PascalCase(string name)
    {
        string camel = CamelCase(name);
        return char.ToUpperInvariant(camel[0]) + camel.Substring(1);
    }

    /// <summary>The key of a card's Cast Time setting: "Fire Bolt" is fireBoltCastTimeMultiplier.</summary>
    public static string CastTimeKey(string spellName)
    {
        string costKey = CostKey(spellName);
        return costKey.Substring(0, costKey.Length - "CostMultiplier".Length) + "CastTimeMultiplier";
    }

    /// <summary>A lower value is the better one for these groups (At Least keeps the lower).</summary>
    public static bool LowerIsBetter(string group)
    {
        return group == GroupCost || group == GroupCastTime;
    }

    /// <summary>The General card's setting that goes with a per-spell multiplier, or null when none does.</summary>
    public static string? GeneralKeyFor(SettingInfo setting)
    {
        if (setting.Kind != SettingKind.Multiplier)
        {
            return null;
        }
        return setting.Group switch
        {
            GroupCost => GeneralCostKey,
            GroupCastTime => GeneralCastTimeKey,
            GroupDamage => GeneralDamageKey,
            GroupDuration => GeneralDurationKey,
            GroupRadius => GeneralRadiusKey,
            GroupHealth => GeneralHealthKey,
            GroupGemDamage => GemGeneralDamageKey,
            _ => null,
        };
    }

    /// <summary>The mode (Multiply or At Least) that goes with a General multiplier: the Gems tab has its own.</summary>
    public static string AtLeastKeyFor(SettingInfo setting)
    {
        return setting.Group == GroupGemDamage ? GemGeneralAtLeastKey : GeneralAtLeastKey;
    }

    private static IReadOnlyList<SettingInfo> WithCostAndGeneral(SettingInfo[] spellSettings)
    {
        var settings = new List<SettingInfo>(spellSettings);
        foreach (string name in CostSpellNames)
        {
            settings.Add(new SettingInfo(CostKey(name), name + " Mana Cost", GroupCost, SettingKind.Multiplier, SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep, 1));
        }
        settings.Add(Duration("skullVesselDurationMultiplier", "Skull Vessel Duration", 1));
        settings.Add(Damage("skullVesselDamageMultiplier", "Skull Vessel Damage"));
        settings.Add(Health("skullVesselHealthMultiplier", "Skull Vessel Health"));
        settings.Add(Duration("fireWispScrollDurationMultiplier", "Fire Wisp Scroll Duration", 1));
        settings.Add(Damage("fireWispScrollDamageMultiplier", "Fire Wisp Scroll Damage"));
        settings.Add(Health("fireWispScrollHealthMultiplier", "Fire Wisp Scroll Health"));
        settings.Add(Damage("meteorShowerScrollDamageMultiplier", "Meteor Shower Scroll Damage"));
        settings.Add(Duration("crawlerBombDurationMultiplier", "Crawler Bomb Duration", 1));
        settings.Add(Damage("crawlerBombDamageMultiplier", "Crawler Bomb Damage"));
        settings.Add(Health("crawlerBombHealthMultiplier", "Crawler Bomb Health"));
        foreach (string name in CastTimeSpellNames)
        {
            settings.Add(new SettingInfo(CastTimeKey(name), name + " Cast Time", GroupCastTime, SettingKind.Multiplier, SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep, 1));
        }
        settings.Add(new SettingInfo(GeneralCastTimeKey, "General Cast Time", GroupGeneral, SettingKind.Multiplier, SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep, 1));
        settings.Add(new SettingInfo(GeneralCostKey, "General Mana Cost", GroupGeneral, SettingKind.Multiplier, SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep, 1));
        settings.Add(new SettingInfo(GeneralDamageKey, "General Damage", GroupGeneral, SettingKind.Multiplier, SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep, 1));
        settings.Add(new SettingInfo(GeneralDurationKey, "General Duration", GroupGeneral, SettingKind.Multiplier, SpellRules.MinDuration, SpellRules.MaxDuration, SpellRules.DurationStep, 1));
        settings.Add(new SettingInfo(GeneralRadiusKey, "General Radius", GroupGeneral, SettingKind.Multiplier, SpellRules.MinRadius, SpellRules.MaxRadius, SpellRules.RadiusStep, 1));
        settings.Add(new SettingInfo(GeneralHealthKey, "General Health", GroupGeneral, SettingKind.Multiplier, SpellRules.MinHealth, SpellRules.MaxHealth, SpellRules.HealthStep, 1));
        settings.Add(new SettingInfo(GeneralAtLeastKey, "General Mode", GroupGeneral, SettingKind.Toggle, 0, 1, 1, 0));
        foreach (GemSpell gem in GemSpells.Where(gem => gem.HasDamage))
        {
            settings.Add(new SettingInfo(gem.DamageKey, gem.DisplayName + " Damage", GroupGemDamage, SettingKind.Multiplier, SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep, 1));
        }
        settings.Add(new SettingInfo(GemGeneralDamageKey, "Gem General Damage", GroupGemGeneral, SettingKind.Multiplier, SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep, 1));
        settings.Add(new SettingInfo(GemGeneralAtLeastKey, "Gem General Mode", GroupGemGeneral, SettingKind.Toggle, 0, 1, 1, 0));
        settings.AddRange(GemStats.SelectMany(stat => stat.Settings()));
        return settings;
    }

    private static CardLine Line(string label, string key)
    {
        return new CardLine(label, key);
    }

    private static WindowSpell[] Flatten(IReadOnlyList<WindowSection> sections)
    {
        var spells = new List<WindowSpell>();
        foreach (WindowSection section in sections)
        {
            spells.AddRange(section.Spells);
        }
        return spells.ToArray();
    }

    public static SettingInfo Setting(string key)
    {
        foreach (SettingInfo setting in Settings)
        {
            if (setting.Key == key)
            {
                return setting;
            }
        }
        throw new KeyNotFoundException("No setting " + key);
    }

    private static Regex Item(string spellName)
    {
        return new Regex(@"^Ammo_T\d+_Spell_" + spellName + "_", RegexOptions.CultureInvariant);
    }

    private static AreaTemplates Templates(string namePattern, int vfxSizeIndex = -1, bool hasReach = false, double sizedToMeters = 0)
    {
        return new AreaTemplates(new Regex(namePattern, RegexOptions.CultureInvariant), vfxSizeIndex, hasReach, sizedToMeters);
    }

    private static SettingInfo Count(string key, string label)
    {
        return new SettingInfo(key, label, GroupSummons, SettingKind.Count, SpellRules.MinSummonsPerCast, SpellRules.MaxSummonsPerCast, 1, 1);
    }

    private static SettingInfo Duration(string key, string label, double defaultMultiplier)
    {
        return new SettingInfo(key, label, GroupDuration, SettingKind.Multiplier, SpellRules.MinDuration, SpellRules.MaxDuration, SpellRules.DurationStep, defaultMultiplier);
    }

    private static SettingInfo Damage(string key, string label)
    {
        return new SettingInfo(key, label, GroupDamage, SettingKind.Multiplier, SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep, 1);
    }

    private static SettingInfo Health(string key, string label)
    {
        return new SettingInfo(key, label, GroupHealth, SettingKind.Multiplier, SpellRules.MinHealth, SpellRules.MaxHealth, SpellRules.HealthStep, 1);
    }

    private static SettingInfo Perception(string key, string label)
    {
        return new SettingInfo(key, label, GroupPerception, SettingKind.Multiplier, SpellRules.MinPerception, SpellRules.MaxPerception, SpellRules.PerceptionStep, 1);
    }

    private static SettingInfo ShotLifetime(string key, string label)
    {
        return new SettingInfo(key, label, GroupPerception, SettingKind.Seconds, SpellRules.MinShotLifetime, SpellRules.MaxShotLifetime, SpellRules.ShotLifetimeStep, SpellRules.DefaultShotLifetime);
    }

    private static SettingInfo Radius(string key, string label, double defaultMultiplier)
    {
        return new SettingInfo(key, label, GroupRadius, SettingKind.Multiplier, SpellRules.MinRadius, SpellRules.MaxRadius, SpellRules.RadiusStep, defaultMultiplier);
    }
}
