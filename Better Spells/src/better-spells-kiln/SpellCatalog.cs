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

/// <summary>How a skill stat's setting changes its node configs (1.2.2, the user's picks from the Skill Stat Picker).</summary>
public enum SkillStatKind
{
    /// <summary>One multiplier for the value at every skill level (base and scaleFactor).</summary>
    Multiply,
    /// <summary>Two multipliers: one for the base value, one for what each level adds (the scaleFactor).</summary>
    BaseAndLevel,
    /// <summary>A whole number of its own for what each level adds (the scaleFactor).</summary>
    SetPerLevel,
    /// <summary>A whole number added to an attribute node's value.</summary>
    FlatAdd,
}

/// <summary>What a skill stat's config holds.</summary>
public enum SkillUnit
{
    Number,
    /// <summary>A whole number (Sint32, Uint32).</summary>
    Whole,
    /// <summary>A share: 0.1 is 10%.</summary>
    Share,
    /// <summary>A time, which Kiln reads and writes in seconds.</summary>
    Seconds,
}

/// <summary>
/// One setting of the Skills tab: the card's section (the skill tree's class), the skill, the line's label, the SkillTreeResource
/// nodes it changes (one, or every attribute node of an attribute), the configs of each node's `configValues` it changes (one
/// knob may drive several), how and in what unit. The game copies a node's configs into the skill's effect when the effect
/// starts (at world load), so a change applies after returning to the main menu and loading the world again.
/// </summary>
public sealed record SkillStat(string Section, string Skill, string Label, string[] Nodes, uint[] Configs, SkillStatKind Kind, SkillUnit Unit)
{
    /// <summary>"Radiant Aura", "Range" is radiantAuraSkillRange.</summary>
    public string BaseKey => SpellCatalog.CamelCase(Skill) + "Skill" + SpellCatalog.PascalCase(Label);
    public string Key => Kind switch
    {
        SkillStatKind.Multiply or SkillStatKind.BaseAndLevel => BaseKey + "Multiplier",
        SkillStatKind.FlatAdd => BaseKey + "Bonus",
        _ => BaseKey,
    };
    /// <summary>BaseAndLevel's second multiplier.</summary>
    public string PerLevelKey => BaseKey + "PerLevelMultiplier";
    public string DisplayName => Skill + " " + Label;

    public static string ConfigPath(uint config) => "configValues.0x" + config.ToString("x8", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>This stat's settings, in the order of its card's lines.</summary>
    public IEnumerable<SettingInfo> Settings()
    {
        string group = SpellCatalog.GroupSkills;
        switch (Kind)
        {
            case SkillStatKind.Multiply:
                yield return new SettingInfo(Key, DisplayName, group, SettingKind.Multiplier, SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep, 1);
                break;
            case SkillStatKind.BaseAndLevel:
                yield return new SettingInfo(Key, DisplayName, group, SettingKind.Multiplier, SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep, 1);
                yield return new SettingInfo(PerLevelKey, DisplayName + " Per Level", group, SettingKind.Multiplier, SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep, 1);
                break;
            case SkillStatKind.SetPerLevel:
                yield return new SettingInfo(Key, DisplayName, group, SettingKind.Count, SpellRules.MinSkillPerLevel, SpellRules.MaxSkillPerLevel, 1, 1);
                break;
            case SkillStatKind.FlatAdd:
                yield return new SettingInfo(Key, DisplayName, group, SettingKind.Levels, SpellRules.MinSkillNodeBonus, SpellRules.MaxSkillNodeBonus, 1, 0);
                break;
        }
    }

    /// <summary>This stat's lines on its skill's card.</summary>
    public IEnumerable<CardLine> Lines()
    {
        yield return new CardLine(Label, Key);
        if (Kind == SkillStatKind.BaseAndLevel)
        {
            yield return new CardLine(Label + " Per Level", PerLevelKey);
        }
    }
}

/// <summary>How a number of a skill's game description reads: 0.1 as 10%, a time in seconds, a plain number.</summary>
public enum SkillTextFormat { Number, Percent, Seconds }

/// <summary>What a skill config scales with: the skill's level (shown at max level), an attribute (shown as its factor), or nothing.</summary>
public enum SkillTextSource { Simple, Level, Attribute }

/// <summary>One %k of a skill's game description: the node config it shows and how.</summary>
public sealed record SkillTextConfig(uint Config, SkillTextFormat Format, SkillTextSource Source);

/// <summary>
/// A skill card's game data for the Skills tab's description (1.2.2): the node, the loca ids of its name and description, its
/// icon (a keen::UiTextureResource; only active skills have one), its max level, the configs its description's %k show (the
/// node's numeric configs in reverse order, as the game fills them), and for Radiant Aura the Base Damage config whose change
/// multiplies the damage the description shows (the description reads Damage Per Scale).
/// </summary>
public sealed record SkillInfo(string Skill, string Node, uint NameLoca, uint DescriptionLoca, string? Icon, int MaxLevel, SkillTextConfig[] TextConfigs, uint TextScaleBy);

/// <summary>
/// A group card of the Skills tab (1.2.2): one value for several skill stats of the same kind, and its mode. At Least: each
/// member takes the higher of its own value and the group's. Override: each member takes the group's, and its own is greyed.
/// </summary>
public sealed record SkillGroup(string Name, string[] Members, bool IsBonus)
{
    public string Key => "skillGroup" + SpellCatalog.PascalCase(Name.Replace("and ", "")) + (IsBonus ? "Bonus" : "Multiplier");
    public string ModeKey => "skillGroup" + SpellCatalog.PascalCase(Name.Replace("and ", "")) + "Override";

    public IEnumerable<SettingInfo> Settings()
    {
        yield return IsBonus
            ? new SettingInfo(Key, Name + " Group", SpellCatalog.GroupSkills, SettingKind.Levels, SpellRules.MinSkillNodeBonus, SpellRules.MaxSkillNodeBonus, 1, 0)
            : new SettingInfo(Key, Name + " Group", SpellCatalog.GroupSkills, SettingKind.Multiplier, SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep, 1);
        yield return new SettingInfo(ModeKey, Name + " Group Mode", SpellCatalog.GroupSkills, SettingKind.Toggle, 0, 1, 1, 0);
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
    public const string GroupSkills = "Skills";

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

    /// <summary>
    /// The Skills tab (1.2.2): the skill-tree node configs the user picked in the Skill Stat Picker. Arsonist, Dark Arts, Iceman and
    /// Thunder have one knob for their three configs (Value, Value2, Value3). Ranger's Value is Endurance, Value1 Dexterity (its
    /// program's attribute references). Attribute nodes (program 937e39f4) hold a whole number (Sint32): Per Node adds to it.
    /// </summary>
    public static readonly IReadOnlyList<SkillStat> SkillStats = new[]
    {
        new SkillStat("Attributes", "Constitution", "Per Node", new[] { "0x07fdab40", "0x23d90cb2", "0xd95cba03", "0xe525ca0e", "0xb7fa7d01", "0xa73bb3fe", "0x1f76cc0c", "0x436c3ca1", "0x265b7080", "0xe16a0bd5", "0x276188b0" }, new[] { 0x0a0c87e7u }, SkillStatKind.FlatAdd, SkillUnit.Whole),
        new SkillStat("Attributes", "Dexterity", "Per Node", new[] { "0xd9a99abb", "0x8805b7a5", "0x4e497f8b", "0x9ea706a6", "0xa10a2c87", "0x561378d0", "0x85268cfa" }, new[] { 0x0a0c87e7u }, SkillStatKind.FlatAdd, SkillUnit.Whole),
        new SkillStat("Attributes", "Endurance", "Per Node", new[] { "0x8d353fe4", "0x98556d99", "0x63801a62", "0xaec55c06", "0x3f89db33", "0xa8d21e53", "0x0e27a5d2", "0x404f1b30" }, new[] { 0x0a0c87e7u }, SkillStatKind.FlatAdd, SkillUnit.Whole),
        new SkillStat("Attributes", "Intelligence", "Per Node", new[] { "0x81d8b64f", "0x37fbee8b", "0x9a35d01f", "0x1c3a25dc", "0x8dcc1e4a", "0xe62d5a59", "0x24631f02", "0x29216e93", "0xcfa5985b", "0x8569bab7", "0x6c76b64d" }, new[] { 0x0a0c87e7u }, SkillStatKind.FlatAdd, SkillUnit.Whole),
        new SkillStat("Attributes", "Spirit", "Per Node", new[] { "0x01e263aa", "0xfbcb2152", "0x65c9d96e", "0xebfdb593", "0x578ecb1d", "0x92dea854", "0xf85cd58a", "0x2399545c", "0x92f1b848", "0x5ea31612", "0x2867deaa" }, new[] { 0x0a0c87e7u }, SkillStatKind.FlatAdd, SkillUnit.Whole),
        new SkillStat("Attributes", "Strength", "Per Node", new[] { "0x6f199127", "0xa199a688", "0xed47da97", "0x51668697", "0x447ef6da", "0xd1c108c5", "0xbcbaceaf", "0xe8b6b8f3", "0xfcab03b3", "0x0b61f08f", "0x359f2550" }, new[] { 0x0a0c87e7u }, SkillStatKind.FlatAdd, SkillUnit.Whole),
        new SkillStat("Warrior", "Warrior's Path", "Damage", new[] { "0x994d15dc" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Warrior", "Shiny Plates", "Magical Armor", new[] { "0xc9ca4e4c" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Warrior", "Heavy Plates", "Physical Armor", new[] { "0x3d027d83" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Warrior", "Slasher", "Damage", new[] { "0xbb3cba58" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Warrior", "Brute", "Damage", new[] { "0xa3ffbe6e" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Warrior", "Heavy Handed", "Effect", new[] { "0xfdfb1f82" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Warrior", "Veteran", "Critical Chance", new[] { "0xd9c5f716" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Warrior", "Absorb", "Chance", new[] { "0xd5264acf" }, new[] { 0xde1695c2u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Warrior", "Blood Rage", "Range", new[] { "0x68bb7cc0" }, new[] { 0x6c2fad6du }, SkillStatKind.Multiply, SkillUnit.Number),
        new SkillStat("Warrior", "Soul Leech", "Mana", new[] { "0x0d201c4f" }, new[] { 0xec54a581u }, SkillStatKind.Multiply, SkillUnit.Whole),
        new SkillStat("Warrior", "Soul Leech", "Range", new[] { "0x0d201c4f" }, new[] { 0x6c2fad6du }, SkillStatKind.Multiply, SkillUnit.Number),
        new SkillStat("Warrior", "Battle Heal", "Healing", new[] { "0xe96e8844" }, new[] { 0x2904adf8u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Warrior", "Purification", "Shroud Time", new[] { "0x8404ad58" }, new[] { 0xf99ee229u }, SkillStatKind.Multiply, SkillUnit.Whole),
        new SkillStat("Warrior", "Vigorous Deflection", "Stamina", new[] { "0x7cb04215" }, new[] { 0xdda61333u }, SkillStatKind.Multiply, SkillUnit.Whole),
        new SkillStat("Warrior", "Nemesis", "Attention", new[] { "0x8bd59975" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Warrior", "Bash", "Damage", new[] { "0xc6e89f3d" }, new[] { 0x963ebe20u }, SkillStatKind.Multiply, SkillUnit.Whole),
        new SkillStat("Warrior", "Finesse", "Chance", new[] { "0x35ef19a8" }, new[] { 0xde1695c2u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Warrior", "Steadfast", "Durability", new[] { "0xec8552a9" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Whole),
        new SkillStat("Warrior", "Titan Edge", "Damage", new[] { "0x9461f8cf" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Warrior", "Athlete", "Count", new[] { "0xf2a8b9be" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Number),
        new SkillStat("Warrior", "Barbarian's Path", "Damage", new[] { "0x45f50e0d" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Ranger", "Airborne", "Cost Reduction", new[] { "0x1d7d4631" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Ranger", "Marksman", "Damage", new[] { "0x33139a82" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Ranger", "Snake Eater", "Resistance", new[] { "0x0ddc8e5f" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Ranger", "Counter Battery", "Damage", new[] { "0x0d790efd" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Ranger", "Ranger", "Endurance", new[] { "0x1f12273a" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Whole),
        new SkillStat("Ranger", "Ranger", "Stamina Regeneration", new[] { "0x1f12273a" }, new[] { 0x41f97f36u }, SkillStatKind.Multiply, SkillUnit.Whole),
        new SkillStat("Ranger", "Ranger", "Dexterity", new[] { "0x1f12273a" }, new[] { 0x1c307d25u }, SkillStatKind.Multiply, SkillUnit.Whole),
        new SkillStat("Ranger", "Sniper", "Critical Chance", new[] { "0x55fe49d1" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Ranger", "Blessed Arrows", "Mana", new[] { "0x20657dd6" }, new[] { 0xec54a581u }, SkillStatKind.Multiply, SkillUnit.Whole),
        new SkillStat("Ranger", "Thrust", "Damage", new[] { "0x956650b0" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Ranger", "Backstab Mastery", "Damage", new[] { "0x4e2ddcf1" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Ranger", "Vitality Surge", "Stamina", new[] { "0x0837bcd1" }, new[] { 0x8ebd4150u }, SkillStatKind.Multiply, SkillUnit.Whole),
        new SkillStat("Ranger", "Eagle's Bane", "Damage", new[] { "0x7a7c0ac5" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Ranger", "Skill Shot", "Damage", new[] { "0x5beee72c" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Ranger", "Swiftshot Sustenance", "Chance", new[] { "0x48d94229" }, new[] { 0xde1695c2u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Ranger", "Arachnoid", "Cost Reduction", new[] { "0xf6d40426" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Ranger", "Dagger Mastery", "Damage", new[] { "0x2de81b98" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Ranger", "Cutthroat", "Damage", new[] { "0x5e97f26e" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Ranger", "Rebound", "Stamina Regeneration", new[] { "0x81224094" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Ranger", "Cull the Herd", "Damage", new[] { "0x9a052e49" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Ranger", "Primal Force", "Damage", new[] { "0x1880773e" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Ranger", "Vile Concoction", "Damage", new[] { "0x4105f234" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Ranger", "Fatal Precision", "Critical Damage", new[] { "0xb3256800" }, new[] { 0xcc845864u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Ranger", "Fatal Precision", "Critical Chance", new[] { "0xb3256800" }, new[] { 0x9fa40585u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Ranger", "Veiled Vigor", "Count", new[] { "0x407f0db5" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Number),
        new SkillStat("Ranger", "Puncture", "Damage", new[] { "0xd28c86aa" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Ranger", "Barrage", "Chance", new[] { "0x148d951c" }, new[] { 0xde1695c2u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Ranger", "Survival Instinct", "Shroud Time", new[] { "0x516969f0" }, new[] { 0xf99ee229u }, SkillStatKind.Multiply, SkillUnit.Whole),
        new SkillStat("Mage", "Healing Revive", "Health", new[] { "0xab7e8921" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Mage Apprentice", "Damage", new[] { "0xbcc2af54" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Arsonist", "Damage", new[] { "0x6a03aac2" }, new[] { 0x0a0c87e7u, 0x41f97f36u, 0x9fa40585u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Dark Arts", "Damage", new[] { "0xfde08d1a" }, new[] { 0x0a0c87e7u, 0x41f97f36u, 0x9fa40585u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Iceman", "Damage", new[] { "0xdb0d0cdf" }, new[] { 0x0a0c87e7u, 0x41f97f36u, 0x9fa40585u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Frost", "Primer Duration", new[] { "0x2a7d8c19" }, new[] { 0xbee6d4a9u }, SkillStatKind.Multiply, SkillUnit.Seconds),
        new SkillStat("Mage", "Quick Charge", "Charge Time", new[] { "0x8029ba35" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Counterstrike", "Reflected Damage", new[] { "0xc1409035" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Counterstrike", "Chance", new[] { "0xc1409035" }, new[] { 0xde1695c2u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Wizard", "Critical Chance", new[] { "0x3c5488aa" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Necromancer", "Chance", new[] { "0x9504fb59" }, new[] { 0xde1695c2u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Terror", "Chance", new[] { "0xace4072f" }, new[] { 0xde1695c2u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Bloodletting", "Chance", new[] { "0xb63acd97" }, new[] { 0xde1695c2u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Shroud Mitigation", "Chance", new[] { "0x5d8ab511" }, new[] { 0xde1695c2u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Healer", "Healing", new[] { "0xab7d6537" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Arcane Deflection", "Mana", new[] { "0x41dcedaa" }, new[] { 0xec54a581u }, SkillStatKind.Multiply, SkillUnit.Whole),
        new SkillStat("Mage", "Thunder", "Damage", new[] { "0xe2b07e6b" }, new[] { 0x0a0c87e7u, 0x41f97f36u, 0x9fa40585u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Water Aura", "Range", new[] { "0x5482295c" }, new[] { 0x6c2fad6du }, SkillStatKind.BaseAndLevel, SkillUnit.Number),
        new SkillStat("Mage", "Radiant Aura", "Damage", new[] { "0x6c18dc03" }, new[] { 0xf62f45c7u }, SkillStatKind.Multiply, SkillUnit.Whole),
        new SkillStat("Mage", "Radiant Aura", "Range", new[] { "0x6c18dc03" }, new[] { 0x6c2fad6du }, SkillStatKind.BaseAndLevel, SkillUnit.Number),
        new SkillStat("Mage", "Unity", "Mana Share", new[] { "0xe887d6dc" }, new[] { 0x550cd550u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Unity", "Chance", new[] { "0xe887d6dc" }, new[] { 0xde1695c2u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Eternal Spark", "Chance", new[] { "0x20ea4235" }, new[] { 0xde1695c2u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Life Essences", "Count", new[] { "0x760ea7fa" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Number),
        new SkillStat("Mage", "Shroud Resistance", "Resistance", new[] { "0xc11e36cd" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Fire Resistance", "Resistance", new[] { "0xc5ae2214" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Ice Resistance", "Resistance", new[] { "0x832e8d41" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Shock Resistance", "Resistance", new[] { "0x4b50fa5c" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Righteous Fire", "Fire Damage", new[] { "0x41a127b8" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Bone to Ash", "Damage", new[] { "0x53e61fff" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Sleight of Hand", "Damage", new[] { "0x0a67d349" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Mana Currents", "Mana Regeneration", new[] { "0xa878bc4e" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Number),
        new SkillStat("Mage", "Spellslinger", "Damage", new[] { "0x1853a39b" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Wand Mastery", "Damage", new[] { "0x35f3fd54" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Mage", "Totem Mastery", "Totems Per Level", new[] { "0x1e0d8327" }, new[] { 0x0a0c87e7u }, SkillStatKind.SetPerLevel, SkillUnit.Whole),
        new SkillStat("Mage", "Advanced Necromancy", "Allies Per Level", new[] { "0x6a343bfa" }, new[] { 0x0a0c87e7u }, SkillStatKind.SetPerLevel, SkillUnit.Whole),
        new SkillStat("Neutral", "Well Rested", "Rested Duration", new[] { "0xa598792e" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Number),
        new SkillStat("Neutral", "Mason", "Damage", new[] { "0x2fdadb3d" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Neutral", "Lumberjack", "Damage", new[] { "0x7e2a4e3c" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Neutral", "Power Parry", "Parry Power", new[] { "0x82bcfd39" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Neutral", "Inner Fires", "Amount", new[] { "0x11653829" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Whole),
        new SkillStat("Neutral", "Opportunity", "Damage", new[] { "0x47536d86" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
        new SkillStat("Neutral", "Fisherman's Resolve", "Fishing Endurance", new[] { "0xba5bb0e5" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Number),
        new SkillStat("Neutral", "Prospector", "Damage", new[] { "0x113d826c" }, new[] { 0x0a0c87e7u }, SkillStatKind.Multiply, SkillUnit.Share),
    };

    /// <summary>Each skill card's game data (generated from the dump's SkillTreeResource, build 26430625).</summary>
    public static readonly IReadOnlyList<SkillInfo> SkillInfos = new[]
    {
        new SkillInfo("Constitution", "0x07fdab40", 2141966844u, 3464267517u, null, 0, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Number, SkillTextSource.Simple) }, 0u),
        new SkillInfo("Dexterity", "0xd9a99abb", 2225266883u, 4187597224u, null, 0, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Number, SkillTextSource.Simple) }, 0u),
        new SkillInfo("Endurance", "0x8d353fe4", 2317575873u, 3868743912u, null, 0, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Number, SkillTextSource.Simple) }, 0u),
        new SkillInfo("Intelligence", "0x81d8b64f", 672616651u, 498847806u, null, 0, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Number, SkillTextSource.Simple) }, 0u),
        new SkillInfo("Spirit", "0x01e263aa", 1392960737u, 2444765464u, null, 0, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Number, SkillTextSource.Simple) }, 0u),
        new SkillInfo("Strength", "0x6f199127", 1301436537u, 1742376863u, null, 0, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Number, SkillTextSource.Simple) }, 0u),
        new SkillInfo("Warrior's Path", "0x994d15dc", 817490139u, 2994914437u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Shiny Plates", "0xc9ca4e4c", 2715838441u, 699413234u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Heavy Plates", "0x3d027d83", 2434523546u, 882830393u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Slasher", "0xbb3cba58", 3886000562u, 1825557606u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Brute", "0xa3ffbe6e", 4031743929u, 3886792070u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Heavy Handed", "0xfdfb1f82", 2709237677u, 1521677849u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Veteran", "0xd9c5f716", 4043280988u, 67356638u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Absorb", "0xd5264acf", 1186917209u, 1010386680u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0xde1695c2u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Blood Rage", "0x68bb7cc0", 3435131839u, 4200247319u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x6c2fad6du, SkillTextFormat.Number, SkillTextSource.Level), new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Simple) }, 0u),
        new SkillInfo("Soul Leech", "0x0d201c4f", 428350649u, 3579633382u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x6c2fad6du, SkillTextFormat.Number, SkillTextSource.Level), new SkillTextConfig(0xec54a581u, SkillTextFormat.Number, SkillTextSource.Level) }, 0u),
        new SkillInfo("Battle Heal", "0xe96e8844", 1325549243u, 642723681u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x2904adf8u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Purification", "0x8404ad58", 1073228234u, 3856847524u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0xf99ee229u, SkillTextFormat.Number, SkillTextSource.Level), new SkillTextConfig(0xde1695c2u, SkillTextFormat.Number, SkillTextSource.Simple) }, 0u),
        new SkillInfo("Vigorous Deflection", "0x7cb04215", 2268148329u, 3802766420u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0xdda61333u, SkillTextFormat.Number, SkillTextSource.Level) }, 0u),
        new SkillInfo("Nemesis", "0x8bd59975", 2642464801u, 2796781201u, "6598821c-1a56-45f2-a9bd-221d0f1e7258", 3, new SkillTextConfig[] { new SkillTextConfig(0x41f97f36u, SkillTextFormat.Percent, SkillTextSource.Level), new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Bash", "0xc6e89f3d", 1619380793u, 1952884755u, "1e0c85be-9543-4c82-83d5-2984244cfc8b", 3, new SkillTextConfig[] { new SkillTextConfig(0x963ebe20u, SkillTextFormat.Number, SkillTextSource.Level) }, 0u),
        new SkillInfo("Finesse", "0x35ef19a8", 400095027u, 821932357u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0xde1695c2u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Steadfast", "0xec8552a9", 1522126428u, 3135298843u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Number, SkillTextSource.Level) }, 0u),
        new SkillInfo("Titan Edge", "0x9461f8cf", 3606836841u, 1635067720u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Athlete", "0xf2a8b9be", 826107874u, 3271636889u, "f35c9761-331c-44d9-a2a6-c887432dd8a4", 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Number, SkillTextSource.Level), new SkillTextConfig(0x951389d2u, SkillTextFormat.Number, SkillTextSource.Attribute) }, 0u),
        new SkillInfo("Barbarian's Path", "0x45f50e0d", 2483035066u, 1820127822u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Airborne", "0x1d7d4631", 1640832255u, 2568927126u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Marksman", "0x33139a82", 882243161u, 1062440003u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Snake Eater", "0x0ddc8e5f", 3697526057u, 951220202u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Counter Battery", "0x0d790efd", 2976540223u, 1410738479u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Ranger", "0x1f12273a", 290727359u, 2989031414u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x1c307d25u, SkillTextFormat.Number, SkillTextSource.Level), new SkillTextConfig(0x41f97f36u, SkillTextFormat.Number, SkillTextSource.Level), new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Number, SkillTextSource.Level) }, 0u),
        new SkillInfo("Sniper", "0x55fe49d1", 2756714769u, 3946863399u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Blessed Arrows", "0x20657dd6", 1424864512u, 522948230u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0xec54a581u, SkillTextFormat.Number, SkillTextSource.Level) }, 0u),
        new SkillInfo("Thrust", "0x956650b0", 3104720071u, 3370155513u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Backstab Mastery", "0x4e2ddcf1", 3630532219u, 4169978088u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Vitality Surge", "0x0837bcd1", 3571001085u, 3674679466u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x8ebd4150u, SkillTextFormat.Number, SkillTextSource.Level) }, 0u),
        new SkillInfo("Eagle's Bane", "0x7a7c0ac5", 4220795372u, 583932783u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Skill Shot", "0x5beee72c", 3225554412u, 2852613187u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Swiftshot Sustenance", "0x48d94229", 1805093420u, 704533554u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0xde1695c2u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Arachnoid", "0xf6d40426", 4249277756u, 75197992u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Dagger Mastery", "0x2de81b98", 524055565u, 679425180u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Cutthroat", "0x5e97f26e", 3581243624u, 3329142594u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Rebound", "0x81224094", 2599520900u, 3847631289u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Cull the Herd", "0x9a052e49", 3440867845u, 465674172u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Primal Force", "0x1880773e", 1349963745u, 2157727274u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Vile Concoction", "0x4105f234", 367843691u, 3243607103u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Fatal Precision", "0xb3256800", 1911489856u, 302340431u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x9fa40585u, SkillTextFormat.Percent, SkillTextSource.Level), new SkillTextConfig(0xcc845864u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Veiled Vigor", "0x407f0db5", 3029678559u, 4119206275u, "40619970-0adc-4d2b-8298-d74bc8b75724", 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Number, SkillTextSource.Level), new SkillTextConfig(0x12221574u, SkillTextFormat.Number, SkillTextSource.Attribute) }, 0u),
        new SkillInfo("Puncture", "0xd28c86aa", 1291472205u, 1863810094u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Barrage", "0x148d951c", 3261187468u, 1925626990u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0xde1695c2u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Survival Instinct", "0x516969f0", 164154762u, 3714143992u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0xf99ee229u, SkillTextFormat.Number, SkillTextSource.Level), new SkillTextConfig(0xde1695c2u, SkillTextFormat.Number, SkillTextSource.Simple) }, 0u),
        new SkillInfo("Healing Revive", "0xab7e8921", 2876134097u, 3724069235u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Mage Apprentice", "0xbcc2af54", 3392377264u, 3216922990u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Arsonist", "0x6a03aac2", 3713637442u, 1264519439u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x41f97f36u, SkillTextFormat.Percent, SkillTextSource.Level), new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level), new SkillTextConfig(0x9fa40585u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Dark Arts", "0xfde08d1a", 1042465877u, 4142140054u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x41f97f36u, SkillTextFormat.Percent, SkillTextSource.Level), new SkillTextConfig(0x9fa40585u, SkillTextFormat.Percent, SkillTextSource.Level), new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Iceman", "0xdb0d0cdf", 1895275492u, 420841242u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x41f97f36u, SkillTextFormat.Percent, SkillTextSource.Level), new SkillTextConfig(0x9fa40585u, SkillTextFormat.Percent, SkillTextSource.Level), new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Frost", "0x2a7d8c19", 3296657060u, 2051907950u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0xbee6d4a9u, SkillTextFormat.Seconds, SkillTextSource.Level) }, 0u),
        new SkillInfo("Quick Charge", "0x8029ba35", 550150265u, 4086383655u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Counterstrike", "0xc1409035", 1493279624u, 2040625352u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0xde1695c2u, SkillTextFormat.Percent, SkillTextSource.Level), new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Wizard", "0x3c5488aa", 1492387235u, 274759538u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Necromancer", "0x9504fb59", 3424057087u, 490098776u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0xde1695c2u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Terror", "0xace4072f", 3240035900u, 2815296646u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0xde1695c2u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Bloodletting", "0xb63acd97", 171591903u, 3912417426u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0xde1695c2u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Shroud Mitigation", "0x5d8ab511", 4078679501u, 3252117447u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0xde1695c2u, SkillTextFormat.Percent, SkillTextSource.Level), new SkillTextConfig(0xf99ee229u, SkillTextFormat.Number, SkillTextSource.Simple), new SkillTextConfig(0x6c2fad6du, SkillTextFormat.Number, SkillTextSource.Simple) }, 0u),
        new SkillInfo("Healer", "0xab7d6537", 728416469u, 1905595684u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Arcane Deflection", "0x41dcedaa", 3352137116u, 947223904u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0xec54a581u, SkillTextFormat.Number, SkillTextSource.Level) }, 0u),
        new SkillInfo("Thunder", "0xe2b07e6b", 3989693657u, 2289039412u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x9fa40585u, SkillTextFormat.Percent, SkillTextSource.Level), new SkillTextConfig(0x41f97f36u, SkillTextFormat.Percent, SkillTextSource.Level), new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Water Aura", "0x5482295c", 3199096055u, 1641307815u, "cdd9d291-28f7-42f0-9df8-538689348547", 3, new SkillTextConfig[] { new SkillTextConfig(0x6c2fad6du, SkillTextFormat.Number, SkillTextSource.Level) }, 0u),
        new SkillInfo("Radiant Aura", "0x6c18dc03", 1301280840u, 154857609u, "1faafc57-22d8-427f-a1cc-05e0516fb7dd", 3, new SkillTextConfig[] { new SkillTextConfig(0x6c2fad6du, SkillTextFormat.Number, SkillTextSource.Level), new SkillTextConfig(0xebb0bfd0u, SkillTextFormat.Number, SkillTextSource.Attribute), new SkillTextConfig(0xf62f45c7u, SkillTextFormat.Number, SkillTextSource.Level) }, 0xf62f45c7u),
        new SkillInfo("Unity", "0xe887d6dc", 3570859605u, 2841884903u, "579125e0-a0e5-40cf-8690-60a9ffe33be6", 3, new SkillTextConfig[] { new SkillTextConfig(0xde1695c2u, SkillTextFormat.Percent, SkillTextSource.Level), new SkillTextConfig(0x550cd550u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Eternal Spark", "0x20ea4235", 1466091116u, 2418135834u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0xde1695c2u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Life Essences", "0x760ea7fa", 779584848u, 2582904188u, "02383fe8-82fd-4ef0-9deb-eb6f69b11a3f", 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Number, SkillTextSource.Level), new SkillTextConfig(0xebb0bfd0u, SkillTextFormat.Number, SkillTextSource.Attribute) }, 0u),
        new SkillInfo("Shroud Resistance", "0xc11e36cd", 1418846804u, 3141512281u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Fire Resistance", "0xc5ae2214", 218758u, 1485730355u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Ice Resistance", "0x832e8d41", 2248837897u, 539007688u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Shock Resistance", "0x4b50fa5c", 603523182u, 2594474861u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Righteous Fire", "0x41a127b8", 3613380126u, 3280605850u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Bone to Ash", "0x53e61fff", 3491745907u, 2113495151u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Sleight of Hand", "0x0a67d349", 1442517471u, 3950266716u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Mana Currents", "0xa878bc4e", 3875541120u, 3623491717u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Number, SkillTextSource.Level) }, 0u),
        new SkillInfo("Spellslinger", "0x1853a39b", 1694738467u, 1149828903u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Wand Mastery", "0x35f3fd54", 3479465379u, 605842036u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Totem Mastery", "0x1e0d8327", 443587329u, 3682829485u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Number, SkillTextSource.Level) }, 0u),
        new SkillInfo("Advanced Necromancy", "0x6a343bfa", 3050064543u, 4263908308u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Number, SkillTextSource.Level) }, 0u),
        new SkillInfo("Well Rested", "0xa598792e", 2708389714u, 514772092u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Number, SkillTextSource.Level) }, 0u),
        new SkillInfo("Mason", "0x2fdadb3d", 4089884376u, 4089064218u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Lumberjack", "0x7e2a4e3c", 3253680604u, 3118258031u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Power Parry", "0x82bcfd39", 3417692650u, 3266756074u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Inner Fires", "0x11653829", 2508354976u, 3128461742u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Seconds, SkillTextSource.Level) }, 0u),
        new SkillInfo("Opportunity", "0x47536d86", 3489782624u, 188848285u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
        new SkillInfo("Fisherman's Resolve", "0xba5bb0e5", 3208111312u, 3851977745u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Number, SkillTextSource.Level) }, 0u),
        new SkillInfo("Prospector", "0x113d826c", 3814769682u, 2987254257u, null, 3, new SkillTextConfig[] { new SkillTextConfig(0x0a0c87e7u, SkillTextFormat.Percent, SkillTextSource.Level) }, 0u),
    };

    // A skill stat's key, for the groups below.
    private static string SkillKey(string skill, string label) => SkillStats.Single(stat => stat.Skill == skill && stat.Label == label).Key;

    /// <summary>The Skills tab's groups (1.2.2, the user's choice): Damage Bonuses are the "% damage per level" skills (not Bash and
    /// Radiant Aura, which add flat damage, nor the terrain ones).</summary>
    public static readonly IReadOnlyList<SkillGroup> SkillGroups = new[]
    {
        new SkillGroup("Stats", SkillStats.Where(stat => stat.Kind == SkillStatKind.FlatAdd).Select(stat => stat.Key).ToArray(), true),
        new SkillGroup("Damage Bonuses", SkillStats.Where(stat => stat.Label == "Damage" && stat.Skill is not ("Bash" or "Radiant Aura" or "Mason" or "Lumberjack" or "Prospector")).Select(stat => stat.Key).ToArray(), false),
        new SkillGroup("Terrain Damage", new[] { SkillKey("Mason", "Damage"), SkillKey("Lumberjack", "Damage"), SkillKey("Prospector", "Damage") }, false),
        new SkillGroup("Critical Chance", new[] { SkillKey("Wizard", "Critical Chance"), SkillKey("Sniper", "Critical Chance"), SkillKey("Veteran", "Critical Chance"), SkillKey("Fatal Precision", "Critical Chance") }, false),
        new SkillGroup("Resistances", SkillStats.Where(stat => stat.Label == "Resistance").Select(stat => stat.Key).ToArray(), false),
        new SkillGroup("Armor", new[] { SkillKey("Shiny Plates", "Magical Armor"), SkillKey("Heavy Plates", "Physical Armor") }, false),
        new SkillGroup("Mana and Stamina Gains", new[] { SkillKey("Blessed Arrows", "Mana"), SkillKey("Soul Leech", "Mana"), SkillKey("Arcane Deflection", "Mana"), SkillKey("Vitality Surge", "Stamina"), SkillKey("Vigorous Deflection", "Stamina") }, false),
        new SkillGroup("Healing", new[] { SkillKey("Healer", "Healing"), SkillKey("Battle Heal", "Healing"), SkillKey("Healing Revive", "Health") }, false),
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
    public static readonly IReadOnlyList<string> Groups = new[] { GroupSummons, GroupDuration, GroupDamage, GroupRadius, GroupCasting, GroupHealth, GroupPerception, GroupCost, GroupCastTime, GroupGeneral, GroupGemDamage, GroupGemGeneral, GroupGemStats, GroupSkills };

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

    /// <summary>The Skills tab's list: the groups, then each skill-tree class with its skills' cards.</summary>
    public static readonly IReadOnlyList<WindowSection> SkillSections = MakeSkillSections();

    /// <summary>Every card of the Skills tab, section after section: the index the window keeps for the chosen one.</summary>
    public static readonly IReadOnlyList<WindowSpell> SkillWindowSpells = Flatten(SkillSections);

    public const string SkillGroupsSection = "Groups";

    private static WindowSection[] MakeSkillSections()
    {
        var sections = new List<WindowSection>
        {
            new WindowSection(SkillGroupsSection, SkillGroups.Select(group => new WindowSpell(group.Name, null,
                new[] { Line(group.IsBonus ? "Bonus Per Node" : "Multiplier", group.Key), Line("Mode", group.ModeKey) })).ToArray()),
        };
        foreach (string section in SkillStats.Select(stat => stat.Section).Distinct())
        {
            WindowSpell[] skills = SkillStats.Where(stat => stat.Section == section).Select(stat => stat.Skill).Distinct()
                .Select(skill => new WindowSpell(skill, null, SkillStats.Where(stat => stat.Section == section && stat.Skill == skill).SelectMany(stat => stat.Lines()).ToArray()))
                .ToArray();
            sections.Add(new WindowSection(section, skills));
        }
        return sections.ToArray();
    }

    /// <summary>The group a skill stat's setting is in, or null.</summary>
    public static SkillGroup? GroupOf(string key)
    {
        return SkillGroups.FirstOrDefault(group => group.Members.Contains(key));
    }

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
        settings.AddRange(SkillStats.SelectMany(stat => stat.Settings()));
        settings.AddRange(SkillGroups.SelectMany(group => group.Settings()));
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
