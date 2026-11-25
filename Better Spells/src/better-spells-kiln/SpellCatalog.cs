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
public sealed record PerceptionSpell(string SettingKey, Regex ItemName, ItemSpell? ItemSpell, string[] ExtraTemplates, string[] Shots, string? ShotLifetimeKey = null);

/// <summary>
/// A weapon gem with a Damage setting: its name in the game without "Gem of", its item, the damage configs of its item's
/// `impactValues` (scaled by the gem's level: a base value and a scaleFactor), and the tier its row is under in the Gems tab.
/// Configs its own program or what it spawns pass to createDamageSetup as damage (not healing); the spawned explosions and zones
/// read them from the gem, so the gem item holds every one.
/// </summary>
public sealed record GemSpell(string Name, string ItemName, uint[] DamageConfigs, int Tier)
{
    /// <summary>"Meteoric Impact" is meteoricImpactGemDamageMultiplier.</summary>
    public string DamageKey => SpellCatalog.CamelCase(Name) + "GemDamageMultiplier";
    public string DisplayName => "Gem of " + Name;
    public Regex Item => new Regex("^" + Regex.Escape(ItemName) + "$", RegexOptions.CultureInvariant);
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
    /// The gems that deal damage, from their Impact programs (dump 26430625). 0x5e34f09b is the damage of most gems' own program; the
    /// Gem of Fracturing's and the Gem of Aerobatics' go to the homing projectiles they spawn, the Gem of Cold Composure's to its ice
    /// explosion. The Gem of Meteoric Impact has three: on hits while primed (0x5e34f09b), the meteor's explosion (0xa8338df8) and its
    /// fire trail (0xfaf3b41b). Left out: gems whose numbers are healing (Soul Reaping), stamina, armour, resistance, chances or
    /// summon levels.
    /// </summary>
    public static readonly IReadOnlyList<GemSpell> GemSpells = new[]
    {
        new GemSpell("Bleeding", "T1_Gem_GemOfBleeding", new[] { 0x5e34f09bu }, 1),
        new GemSpell("Burning", "T1_Gem_GemOfIgnition", new[] { 0x5e34f09bu }, 1),
        new GemSpell("Puncturing", "T1_Gem_GemOfRepetition", new[] { 0x5e34f09bu }, 1),
        new GemSpell("Fracturing", "T2_Gem_GemOfFracturing", new[] { 0x5e34f09bu }, 2),
        new GemSpell("Retaliation", "T2_Gem_GemOfRetaliation", new[] { 0x5e34f09bu }, 2),
        new GemSpell("Swiftness", "T2_Gem_GemOfSwiftness", new[] { 0x5e34f09bu }, 2),
        new GemSpell("Toxic Betrayal", "T2_Gem_GemOfToxicReaction", new[] { 0x5e34f09bu }, 2),
        new GemSpell("Agility", "T3_Gem_GemOfAgility", new[] { 0x5e34f09bu }, 3),
        new GemSpell("Storm's Eye", "T3_Gem_GemOfConduction", new[] { 0x5e34f09bu }, 3),
        new GemSpell("Corruption", "T4_Gem_GemOfCorruption", new[] { 0x5e34f09bu }, 4),
        new GemSpell("Focused Light", "T4_Gem_GemOfFocusedLight", new[] { 0x5e34f09bu }, 4),
        new GemSpell("Bloodlust", "T5_Gem_GemOfBloodlust", new[] { 0x5e34f09bu }, 5),
        new GemSpell("Cold Composure", "T5_Gem_GemOfColdComposure", new[] { 0x5e34f09bu }, 5),
        new GemSpell("Meteoric Impact", "T5_Gem_GemOfMeteoricDescent", new[] { 0x5e34f09bu, 0xa8338df8u, 0xfaf3b41bu }, 5),
        new GemSpell("Aerobatics", "T6_Gem_GemOfAerobatics_UnderFog", new[] { 0x5e34f09bu }, 6),
        new GemSpell("Currents", "T6_Gem_GemOfCurrents_OverFog", new[] { 0x5e34f09bu }, 6),
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
    });

    /// <summary>The groups of the settings page, in its order.</summary>
    public static readonly IReadOnlyList<string> Groups = new[] { GroupSummons, GroupDuration, GroupDamage, GroupRadius, GroupCasting, GroupHealth, GroupPerception, GroupCost, GroupCastTime, GroupGeneral, GroupGemDamage, GroupGemGeneral };

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
            new[] { "Projectile_Companion_Totem_Fire", "Projectile_Companion_Totem_Ice", "Projectile_Companion_Totem_Lightning" }),
        new PerceptionSpell(SkeletonPerceptionKey, SkeletonItem, null, Array.Empty<string>(), Array.Empty<string>()),
        new PerceptionSpell(LightningWispPerceptionKey, Item("Lightning_Wisp"), null, new[] { "Attack_Companion_ShockWisp_Shoot_Attachment" },
            new[] { "Projectile_Companion_ShockWisp" }, LightningWispShotLifetimeKey),
        new PerceptionSpell(SkullVesselPerceptionKey, ItemSpells[0].Items, ItemSpells[0], Array.Empty<string>(), Array.Empty<string>()),
        new PerceptionSpell(FireWispScrollPerceptionKey, ItemSpells[1].Items, ItemSpells[1], Array.Empty<string>(), new[] { "Projectile_Companion_FireWisp" }, FireWispScrollShotLifetimeKey),
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
                Line("Radius", BallRadiusKey), Line("Speed", BallSpeedKey), Line("Hit Rate", BallHitRateKey)),
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
                Line("Perception", TotemPerceptionKey), Line("Max Totems", MaxTotemsKey), Line("Per Cast", TotemsPerCastKey),
                Line("Spread", SummonSpreadKey), Line("Invulnerable", InvulnerableTotemsKey), Line("Enemies Ignore Them", EnemiesIgnoreTotemsKey)),
            Spell("Skeleton", SkeletonItem, Line("Duration", "skeletonDurationMultiplier"), Line("Damage", "skeletonDamageMultiplier"),
                Line("Health", SkeletonHealthKey), Line("Perception", SkeletonPerceptionKey), Line("Max Skeletons", MaxSkeletonsKey), Line("Per Cast", SkeletonsPerCastKey)),
            Spell("Lightning Wisp", Item("Lightning_Wisp"), Line("Duration", "lightningWispDurationMultiplier"), Line("Damage", "lightningWispDamageMultiplier"),
                Line("Perception", LightningWispPerceptionKey), Line("Shot Lifetime", LightningWispShotLifetimeKey), Line("Per Cast", LightningWispsPerCastKey), Line("Keep Older Wisps", KeepOlderWispsKey)),
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
                Line("Perception", FireWispScrollPerceptionKey), Line("Shot Lifetime", FireWispScrollShotLifetimeKey), Line("Per Use", FireWispsPerUseKey)),
            Spell("Meteor Shower Scroll", ItemSpells[2].Items, Line("Damage", "meteorShowerScrollDamageMultiplier")),
            Spell("Crawler Bomb", ItemSpells[3].Items, Line("Duration", "crawlerBombDurationMultiplier"), Line("Damage", "crawlerBombDamageMultiplier"), Line("Health", "crawlerBombHealthMultiplier")),
        }),
    };

    /// <summary>Every spell of the settings window, section after section: the index the window keeps for the chosen spell.</summary>
    public static readonly IReadOnlyList<WindowSpell> WindowSpells = Flatten(WindowSections);

    /// <summary>The Gems tab: General, then a row per tier, each gem's card a Damage line.</summary>
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
            WindowSpell[] gems = GemSpells.Where(gem => gem.Tier == tier).Select(gem => new WindowSpell(gem.DisplayName, gem.Item, new[] { Line("Damage", gem.DamageKey) })).ToArray();
            sections.Add(new WindowSection("Tier " + tier.ToString(System.Globalization.CultureInfo.InvariantCulture), gems));
        }
        return sections.ToArray();
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
        foreach (GemSpell gem in GemSpells)
        {
            settings.Add(new SettingInfo(gem.DamageKey, gem.DisplayName + " Damage", GroupGemDamage, SettingKind.Multiplier, SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep, 1));
        }
        settings.Add(new SettingInfo(GemGeneralDamageKey, "Gem General Damage", GroupGemGeneral, SettingKind.Multiplier, SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep, 1));
        settings.Add(new SettingInfo(GemGeneralAtLeastKey, "Gem General Mode", GroupGemGeneral, SettingKind.Toggle, 0, 1, 1, 0));
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
