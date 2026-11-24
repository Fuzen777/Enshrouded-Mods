using Kiln;

namespace BetterSpells;

/// <summary>The player's settings: kiln config better-spells.json in the profile's config folder, changed from the Kiln menu (the mod's own rows step, Kiln's sliders are free and land on the nearest step) or by saving the file. All of them apply while playing, to what is cast afterwards. The keys, ranges and defaults are the ones Better Spells had in Shroudforge; SpellCatalog.Settings lists the same settings with their steps, and the tests check that the two agree.</summary>
public sealed class SpellSettings
{
    // Summons
    [ConfigKey("skeletonsPerCast")]
    [Comment("Skeletons Per Cast. How many skeletons one cast of the Skeleton spell places. The cast pays its mana once; the game's limit on summons stays.")]
    [Range(SpellRules.MinSummonsPerCast, SpellRules.MaxSummonsPerCast, 1)]
    public int SkeletonsPerCast { get; set; } = 1;

    [ConfigKey("totemsPerCast")]
    [Comment("Totems Per Cast. How many totems one cast of a totem spell places.")]
    [Range(SpellRules.MinSummonsPerCast, SpellRules.MaxSummonsPerCast, 1)]
    public int TotemsPerCast { get; set; } = 1;

    [ConfigKey("lightningWispsPerCast")]
    [Comment("Lightning Wisps Per Cast. How many wisps one cast of Lightning Wisp places. The cast pays its mana once. Experimental: the spell removes the caster's older wisps it touches.")]
    [Range(SpellRules.MinSummonsPerCast, SpellRules.MaxSummonsPerCast, 1)]
    public int LightningWispsPerCast { get; set; } = 1;

    [ConfigKey("keepOlderWisps")]
    [Comment("Keep Older Wisps. A new Lightning Wisp cast no longer removes your wisps within 20 units, so they add up while they last (no exact maximum). Experimental.")]
    public bool KeepOlderWisps { get; set; } = false;

    [ConfigKey("fireWispsPerUse")]
    [Comment("Fire Wisps Per Use. How many fire wisps one Fire Wisp scroll summons.")]
    [Range(SpellRules.MinSummonsPerCast, SpellRules.MaxSummonsPerCast, 1)]
    public int FireWispsPerUse { get; set; } = 1;

    [ConfigKey("skullVesselsPerCast")]
    [Comment("Skull Vessels Per Use. How many vessels one use throws, each summoning its skull where it lands. Uses one vessel and its mana once.")]
    [Range(SpellRules.MinSummonsPerCast, SpellRules.MaxSummonsPerCast, 1)]
    public int SkullVesselsPerCast { get; set; } = 1;

    [ConfigKey("maxTotems")]
    [Comment("Max Totems. How many totems can be out at once before skills add to it (the game's own is 1). Applies when you next load a world.")]
    [Range(SpellRules.MinSummonCap, SpellRules.MaxSummonCap, 1)]
    public int MaxTotems { get; set; } = SpellRules.DefaultSummonCap;

    [ConfigKey("maxSkeletons")]
    [Comment("Max Skeletons. How many skeletons can be out at once before skills add to it (the game's own is 1). Applies when you next load a world.")]
    [Range(SpellRules.MinSummonCap, SpellRules.MaxSummonCap, 1)]
    public int MaxSkeletons { get; set; } = SpellRules.DefaultSummonCap;

    [ConfigKey("totemHealthMultiplier")]
    [Comment("Totem Health. Totems have this many times their health. Step 0.05.")]
    [Range(SpellRules.MinHealth, SpellRules.MaxHealth, SpellRules.HealthStep)]
    public double TotemHealthMultiplier { get; set; } = 1;

    [ConfigKey("skeletonHealthMultiplier")]
    [Comment("Skeleton Health. Summoned skeletons have this many times their health. Step 0.05.")]
    [Range(SpellRules.MinHealth, SpellRules.MaxHealth, SpellRules.HealthStep)]
    public double SkeletonHealthMultiplier { get; set; } = 1;

    [ConfigKey("magicShieldHealthMultiplier")]
    [Comment("Magic Shield Health. The shield takes this many times the damage before it breaks. Step 0.05.")]
    [Range(SpellRules.MinHealth, SpellRules.MaxHealth, SpellRules.HealthStep)]
    public double MagicShieldHealthMultiplier { get; set; } = 1;

    [ConfigKey("summonSpread")]
    [Comment("Totem Spread (units). How far from the cursor the totems of one cast may land, in the game's own distance units, while a cast places more than one. Skeletons always spread 1. Step 1.")]
    [Range(SpellRules.MinSpreadUnits, SpellRules.MaxSpreadUnits, SpellRules.SpreadStep)]
    public double SummonSpread { get; set; } = SpellRules.DefaultSpreadUnits;

    // Duration
    [ConfigKey("totemDurationMultiplier")]
    [Comment("Totem Duration. Fire, Ice and Lightning Totems last this many times longer (normally 8 to 11 s). Ice Totem's chill on enemies keeps its length. Step 0.05.")]
    [Range(SpellRules.MinDuration, SpellRules.MaxDuration, SpellRules.DurationStep)]
    public double TotemDurationMultiplier { get; set; } = 1;

    [ConfigKey("invulnerableTotems")]
    [Comment("Invulnerable Totems. Totems take no damage, like Lightning Wisps. Enemies still attack them.")]
    public bool InvulnerableTotems { get; set; } = false;

    [ConfigKey("enemiesIgnoreTotems")]
    [Comment("Enemies Ignore Totems. Enemies no longer pick totems as targets, so totems stop working as decoys. Totems placed afterwards get a faction of their own that enemies are neutral toward. Experimental.")]
    public bool EnemiesIgnoreTotems { get; set; } = false;

    [ConfigKey("skeletonDurationMultiplier")]
    [Comment("Skeleton Duration. Summoned skeletons last this many times longer (normally 60 to 120 s). Step 0.05.")]
    [Range(SpellRules.MinDuration, SpellRules.MaxDuration, SpellRules.DurationStep)]
    public double SkeletonDurationMultiplier { get; set; } = 1;

    [ConfigKey("magicShieldDurationMultiplier")]
    [Comment("Magic Shield Duration. The shield lasts this many times longer (normally 45 or 60 s). Step 0.05.")]
    [Range(SpellRules.MinDuration, SpellRules.MaxDuration, SpellRules.DurationStep)]
    public double MagicShieldDurationMultiplier { get; set; } = 1;

    [ConfigKey("lightningWispDurationMultiplier")]
    [Comment("Lightning Wisp Duration. Wisps last this many times longer (normally 12 to 18 s). Step 0.05.")]
    [Range(SpellRules.MinDuration, SpellRules.MaxDuration, SpellRules.DurationStep)]
    public double LightningWispDurationMultiplier { get; set; } = 1;

    [ConfigKey("blizzardDurationMultiplier")]
    [Comment("Blizzard Duration. The blizzard and its slow on enemies last this many times longer (normally 9 to 12 s). Step 0.05.")]
    [Range(SpellRules.MinDuration, SpellRules.MaxDuration, SpellRules.DurationStep)]
    public double BlizzardDurationMultiplier { get; set; } = 1;

    [ConfigKey("lightningBallDurationMultiplier")]
    [Comment("Lightning Ball Duration. The ball lasts this many times longer (normally 6 to 8 s). Step 0.05.")]
    [Range(SpellRules.MinDuration, SpellRules.MaxDuration, SpellRules.DurationStep)]
    public double LightningBallDurationMultiplier { get; set; } = 1;

    [ConfigKey("rootDurationMultiplier")]
    [Comment("Root Duration. The roots and their hold on enemies last this many times longer (normally 5 to 8 s). Step 0.05.")]
    [Range(SpellRules.MinDuration, SpellRules.MaxDuration, SpellRules.DurationStep)]
    public double RootDurationMultiplier { get; set; } = 1;

    [ConfigKey("firePillarDurationMultiplier")]
    [Comment("Fire Pillar Duration. The pillar lasts this many times longer (normally 4.5 to 6 s). The burn on enemies keeps its length. Step 0.05.")]
    [Range(SpellRules.MinDuration, SpellRules.MaxDuration, SpellRules.DurationStep)]
    public double FirePillarDurationMultiplier { get; set; } = 1;

    [ConfigKey("acidConeDurationMultiplier")]
    [Comment("Acid Cone Duration. The acid lasts this many times longer (normally 5 s). The poison on enemies keeps its length. Step 0.05.")]
    [Range(SpellRules.MinDuration, SpellRules.MaxDuration, SpellRules.DurationStep)]
    public double AcidConeDurationMultiplier { get; set; } = 1;

    // Damage
    [ConfigKey("fireBoltDamageMultiplier")]
    [Comment("Fire Bolt Damage. Multiplies the spell's share of the damage; the staff adds its own. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double FireBoltDamageMultiplier { get; set; } = 1;

    [ConfigKey("fireballDamageMultiplier")]
    [Comment("Fireball Damage. Multiplies the spell's share of the damage; the staff adds its own. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double FireballDamageMultiplier { get; set; } = 1;

    [ConfigKey("iceBoltDamageMultiplier")]
    [Comment("Ice Bolt Damage. Multiplies the spell's share of the damage; the staff adds its own. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double IceBoltDamageMultiplier { get; set; } = 1;

    [ConfigKey("acidConeDamageMultiplier")]
    [Comment("Acid Cone Damage. Multiplies the spell's share of the damage; the staff adds its own. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double AcidConeDamageMultiplier { get; set; } = 1;

    [ConfigKey("firePillarDamageMultiplier")]
    [Comment("Fire Pillar Damage. Multiplies the spell's share of the damage; the staff adds its own. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double FirePillarDamageMultiplier { get; set; } = 1;

    [ConfigKey("blizzardDamageMultiplier")]
    [Comment("Blizzard Damage. Multiplies the spell's share of the damage; the staff adds its own. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double BlizzardDamageMultiplier { get; set; } = 1;

    [ConfigKey("lightningBallDamageMultiplier")]
    [Comment("Lightning Ball Damage. Multiplies the spell's share of the damage; the staff adds its own. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double LightningBallDamageMultiplier { get; set; } = 1;

    [ConfigKey("chainLightningDamageMultiplier")]
    [Comment("Chain Lightning Damage. Multiplies the spell's share of the damage; the staff adds its own. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double ChainLightningDamageMultiplier { get; set; } = 1;

    [ConfigKey("shroudMeteorDamageMultiplier")]
    [Comment("Shroud Meteor Damage. Multiplies the spell's share of the damage; the staff adds its own. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double ShroudMeteorDamageMultiplier { get; set; } = 1;

    [ConfigKey("rootDamageMultiplier")]
    [Comment("Root Damage. Multiplies the spell's share of the damage; the staff adds its own. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double RootDamageMultiplier { get; set; } = 1;

    [ConfigKey("lightBurstDamageMultiplier")]
    [Comment("Light Burst Damage. Multiplies the spell's share of the damage; the staff adds its own. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double LightBurstDamageMultiplier { get; set; } = 1;

    [ConfigKey("totemDamageMultiplier")]
    [Comment("Totem Damage. Multiplies the spell's share of the damage; the staff adds its own. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double TotemDamageMultiplier { get; set; } = 1;

    [ConfigKey("lightningWispDamageMultiplier")]
    [Comment("Lightning Wisp Damage. Multiplies the spell's share of the damage; the staff adds its own. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double LightningWispDamageMultiplier { get; set; } = 1;

    [ConfigKey("skeletonDamageMultiplier")]
    [Comment("Skeleton Damage. Multiplies the spell's share of the damage; the staff adds its own. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double SkeletonDamageMultiplier { get; set; } = 1;

    [ConfigKey("boneChannelDamageMultiplier")]
    [Comment("Bone Channel Damage. Multiplies the spell's share of the damage; the staff adds its own. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double BoneChannelDamageMultiplier { get; set; } = 1;

    [ConfigKey("shroudBeamDamageMultiplier")]
    [Comment("Shroud Beam Damage. Multiplies the spell's share of the damage; the staff adds its own. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double ShroudBeamDamageMultiplier { get; set; } = 1;

    [ConfigKey("lightningChannelDamageMultiplier")]
    [Comment("Lightning Channel Damage. Multiplies the spell's share of the damage; the staff adds its own. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double LightningChannelDamageMultiplier { get; set; } = 1;

    // Radius
    [ConfigKey("fireballRadiusMultiplier")]
    [Comment("Fireball Radius. The spell's area is this many times wider, with its look and its tooltip. Step 0.05.")]
    [Range(SpellRules.MinRadius, SpellRules.MaxRadius, SpellRules.RadiusStep)]
    public double FireballRadiusMultiplier { get; set; } = SpellRules.DefaultRadius;

    [ConfigKey("iceBoltRadiusMultiplier")]
    [Comment("Ice Bolt Radius. The spell's area is this many times wider, with its look and its tooltip. Also its frost zone. Step 0.05.")]
    [Range(SpellRules.MinRadius, SpellRules.MaxRadius, SpellRules.RadiusStep)]
    public double IceBoltRadiusMultiplier { get; set; } = SpellRules.DefaultRadius;

    [ConfigKey("rootRadiusMultiplier")]
    [Comment("Root Radius. The spell's area is this many times wider, with its look and its tooltip. Step 0.05.")]
    [Range(SpellRules.MinRadius, SpellRules.MaxRadius, SpellRules.RadiusStep)]
    public double RootRadiusMultiplier { get; set; } = SpellRules.DefaultRadius;

    [ConfigKey("firePillarRadiusMultiplier")]
    [Comment("Fire Pillar Radius. The spell's area is this many times wider, with its look and its tooltip. Step 0.05.")]
    [Range(SpellRules.MinRadius, SpellRules.MaxRadius, SpellRules.RadiusStep)]
    public double FirePillarRadiusMultiplier { get; set; } = SpellRules.DefaultRadius;

    [ConfigKey("blizzardRadiusMultiplier")]
    [Comment("Blizzard Radius. The spell's area is this many times wider, with its look and its tooltip. Step 0.05.")]
    [Range(SpellRules.MinRadius, SpellRules.MaxRadius, SpellRules.RadiusStep)]
    public double BlizzardRadiusMultiplier { get; set; } = SpellRules.DefaultRadius;

    [ConfigKey("shroudMeteorRadiusMultiplier")]
    [Comment("Shroud Meteor Radius. The spell's area is this many times wider, with its look and its tooltip. The blast is 5 m times this, where the game's own is 3 m; the crater stays. Step 0.05.")]
    [Range(SpellRules.MinRadius, SpellRules.MaxRadius, SpellRules.RadiusStep)]
    public double ShroudMeteorRadiusMultiplier { get; set; } = SpellRules.DefaultRadius;

    [ConfigKey("shroudMeteorVisual")]
    [Comment("Shroud Meteor Visual. The explosion's look grows with Shroud Meteor Radius.")]
    public bool ShroudMeteorVisual { get; set; } = true;

    [ConfigKey("lightBurstRadiusMultiplier")]
    [Comment("Light Burst Radius. The spell's area is this many times wider, with its tooltip. Step 0.05.")]
    [Range(SpellRules.MinRadius, SpellRules.MaxRadius, SpellRules.RadiusStep)]
    public double LightBurstRadiusMultiplier { get; set; } = SpellRules.DefaultRadius;

    [ConfigKey("lightBurstVisual")]
    [Comment("Light Burst Visual (Experimental). The explosion's look grows with Light Burst Radius.")]
    public bool LightBurstVisual { get; set; } = true;

    [ConfigKey("ballLightningRadiusMultiplier")]
    [Comment("Ball Lightning Radius. The area the ball hits enemies in (normally 4 m) is this many times wider. Step 0.05.")]
    [Range(SpellRules.MinRadius, SpellRules.MaxRadius, SpellRules.RadiusStep)]
    public double BallLightningRadiusMultiplier { get; set; } = 1;

    [ConfigKey("chainLightningRadiusMultiplier")]
    [Comment("Chain Lightning Radius. The area around the target in which Chain Lightning hits every enemy (normally 7 units) is this many times wider, so it reaches more enemies. Step 0.05.")]
    [Range(SpellRules.MinRadius, SpellRules.MaxChainRadius, SpellRules.RadiusStep)]
    public double ChainLightningRadiusMultiplier { get; set; } = 1;

    // Casting
    [ConfigKey("chainLightningLockOnMultiplier")]
    [Comment("Chain Lightning Lock-On Range. How far away Chain Lightning's first target can be, times this (normally 24 units). Step 0.05.")]
    [Range(SpellRules.MinLockOn, SpellRules.MaxLockOn, SpellRules.CastingStep)]
    public double ChainLightningLockOnMultiplier { get; set; } = 1;

    [ConfigKey("ballLightningSpeedMultiplier")]
    [Comment("Ball Lightning Speed. The ball flies this many times faster. Step 0.05.")]
    [Range(SpellRules.MinBallSpeed, SpellRules.MaxBallSpeed, SpellRules.CastingStep)]
    public double BallLightningSpeedMultiplier { get; set; } = 1;

    [ConfigKey("ballLightningHitRateMultiplier")]
    [Comment("Ball Lightning Hit Rate. The ball hits the enemies in its area this many times more often (normally every 0.5 s). Step 0.05.")]
    [Range(SpellRules.MinBallHitRate, SpellRules.MaxBallHitRate, SpellRules.CastingStep)]
    public double BallLightningHitRateMultiplier { get; set; } = 1;

    [ConfigKey("fireBoltCostMultiplier")]
    [Comment("Fire Bolt Mana Cost. The spell costs this share of its mana (x0.5 is half, x2 twice). Step 0.05.")]
    [Range(SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep)]
    public double FireBoltCostMultiplier { get; set; } = 1;

    [ConfigKey("fireballCostMultiplier")]
    [Comment("Fireball Mana Cost. The spell costs this share of its mana (x0.5 is half, x2 twice). Step 0.05.")]
    [Range(SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep)]
    public double FireballCostMultiplier { get; set; } = 1;

    [ConfigKey("firePillarCostMultiplier")]
    [Comment("Fire Pillar Mana Cost. The spell costs this share of its mana (x0.5 is half, x2 twice). Step 0.05.")]
    [Range(SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep)]
    public double FirePillarCostMultiplier { get; set; } = 1;

    [ConfigKey("lightBurstCostMultiplier")]
    [Comment("Light Burst Mana Cost. The spell costs this share of its mana (x0.5 is half, x2 twice). Step 0.05.")]
    [Range(SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep)]
    public double LightBurstCostMultiplier { get; set; } = 1;

    [ConfigKey("iceBoltCostMultiplier")]
    [Comment("Ice Bolt Mana Cost. The spell costs this share of its mana (x0.5 is half, x2 twice). Step 0.05.")]
    [Range(SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep)]
    public double IceBoltCostMultiplier { get; set; } = 1;

    [ConfigKey("blizzardCostMultiplier")]
    [Comment("Blizzard Mana Cost. The spell costs this share of its mana (x0.5 is half, x2 twice). Step 0.05.")]
    [Range(SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep)]
    public double BlizzardCostMultiplier { get; set; } = 1;

    [ConfigKey("chainLightningCostMultiplier")]
    [Comment("Chain Lightning Mana Cost. The spell costs this share of its mana (x0.5 is half, x2 twice). Step 0.05.")]
    [Range(SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep)]
    public double ChainLightningCostMultiplier { get; set; } = 1;

    [ConfigKey("ballLightningCostMultiplier")]
    [Comment("Ball Lightning Mana Cost. The spell costs this share of its mana (x0.5 is half, x2 twice). Step 0.05.")]
    [Range(SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep)]
    public double BallLightningCostMultiplier { get; set; } = 1;

    [ConfigKey("lightningChannelCostMultiplier")]
    [Comment("Lightning Channel Mana Cost. The spell costs this share of its mana (x0.5 is half, x2 twice). Step 0.05.")]
    [Range(SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep)]
    public double LightningChannelCostMultiplier { get; set; } = 1;

    [ConfigKey("shroudMeteorCostMultiplier")]
    [Comment("Shroud Meteor Mana Cost. The spell costs this share of its mana (x0.5 is half, x2 twice). Step 0.05.")]
    [Range(SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep)]
    public double ShroudMeteorCostMultiplier { get; set; } = 1;

    [ConfigKey("shroudBeamCostMultiplier")]
    [Comment("Shroud Beam Mana Cost. The spell costs this share of its mana (x0.5 is half, x2 twice). Step 0.05.")]
    [Range(SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep)]
    public double ShroudBeamCostMultiplier { get; set; } = 1;

    [ConfigKey("totemsCostMultiplier")]
    [Comment("Totems Mana Cost. The spell costs this share of its mana (x0.5 is half, x2 twice). Step 0.05.")]
    [Range(SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep)]
    public double TotemsCostMultiplier { get; set; } = 1;

    [ConfigKey("skeletonCostMultiplier")]
    [Comment("Skeleton Mana Cost. The spell costs this share of its mana (x0.5 is half, x2 twice). Step 0.05.")]
    [Range(SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep)]
    public double SkeletonCostMultiplier { get; set; } = 1;

    [ConfigKey("lightningWispCostMultiplier")]
    [Comment("Lightning Wisp Mana Cost. The spell costs this share of its mana (x0.5 is half, x2 twice). Step 0.05.")]
    [Range(SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep)]
    public double LightningWispCostMultiplier { get; set; } = 1;

    [ConfigKey("acidConeCostMultiplier")]
    [Comment("Acid Cone Mana Cost. The spell costs this share of its mana (x0.5 is half, x2 twice). Step 0.05.")]
    [Range(SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep)]
    public double AcidConeCostMultiplier { get; set; } = 1;

    [ConfigKey("rootCostMultiplier")]
    [Comment("Root Mana Cost. The spell costs this share of its mana (x0.5 is half, x2 twice). Step 0.05.")]
    [Range(SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep)]
    public double RootCostMultiplier { get; set; } = 1;

    [ConfigKey("boneChannelCostMultiplier")]
    [Comment("Bone Channel Mana Cost. The spell costs this share of its mana (x0.5 is half, x2 twice). Step 0.05.")]
    [Range(SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep)]
    public double BoneChannelCostMultiplier { get; set; } = 1;

    [ConfigKey("magicShieldCostMultiplier")]
    [Comment("Magic Shield Mana Cost. The spell costs this share of its mana (x0.5 is half, x2 twice). Step 0.05.")]
    [Range(SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep)]
    public double MagicShieldCostMultiplier { get; set; } = 1;

    // Items that act like spells.
    [ConfigKey("skullVesselCostMultiplier")]
    [Comment("Skull Vessel Mana Cost. The item costs this share of its mana (x0.5 is half, x2 twice). Step 0.05.")]
    [Range(SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep)]
    public double SkullVesselCostMultiplier { get; set; } = 1;

    [ConfigKey("skullVesselDurationMultiplier")]
    [Comment("Skull Vessel Duration. What it summons lasts this many times longer. Step 0.05.")]
    [Range(SpellRules.MinDuration, SpellRules.MaxDuration, SpellRules.DurationStep)]
    public double SkullVesselDurationMultiplier { get; set; } = 1;

    [ConfigKey("skullVesselDamageMultiplier")]
    [Comment("Skull Vessel Damage. Multiplies the item's share of the damage. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double SkullVesselDamageMultiplier { get; set; } = 1;

    [ConfigKey("skullVesselHealthMultiplier")]
    [Comment("Skull Vessel Health. What it summons has this many times its health. Step 0.05.")]
    [Range(SpellRules.MinHealth, SpellRules.MaxHealth, SpellRules.HealthStep)]
    public double SkullVesselHealthMultiplier { get; set; } = 1;

    [ConfigKey("fireWispScrollCostMultiplier")]
    [Comment("Fire Wisp Scroll Mana Cost. The item costs this share of its mana (x0.5 is half, x2 twice). Step 0.05.")]
    [Range(SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep)]
    public double FireWispScrollCostMultiplier { get; set; } = 1;

    [ConfigKey("fireWispScrollDurationMultiplier")]
    [Comment("Fire Wisp Scroll Duration. What it summons lasts this many times longer. Step 0.05.")]
    [Range(SpellRules.MinDuration, SpellRules.MaxDuration, SpellRules.DurationStep)]
    public double FireWispScrollDurationMultiplier { get; set; } = 1;

    [ConfigKey("fireWispScrollDamageMultiplier")]
    [Comment("Fire Wisp Scroll Damage. Multiplies the item's share of the damage. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double FireWispScrollDamageMultiplier { get; set; } = 1;

    [ConfigKey("fireWispScrollHealthMultiplier")]
    [Comment("Fire Wisp Scroll Health. What it summons has this many times its health. Step 0.05.")]
    [Range(SpellRules.MinHealth, SpellRules.MaxHealth, SpellRules.HealthStep)]
    public double FireWispScrollHealthMultiplier { get; set; } = 1;

    [ConfigKey("meteorShowerScrollCostMultiplier")]
    [Comment("Meteor Shower Scroll Mana Cost. The item costs this share of its mana (x0.5 is half, x2 twice). Step 0.05.")]
    [Range(SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep)]
    public double MeteorShowerScrollCostMultiplier { get; set; } = 1;

    [ConfigKey("meteorShowerScrollDamageMultiplier")]
    [Comment("Meteor Shower Scroll Damage. Multiplies the item's share of the damage. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double MeteorShowerScrollDamageMultiplier { get; set; } = 1;

    [ConfigKey("crawlerBombCostMultiplier")]
    [Comment("Crawler Bomb Mana Cost. The item costs this share of its mana (x0.5 is half, x2 twice). Step 0.05.")]
    [Range(SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep)]
    public double CrawlerBombCostMultiplier { get; set; } = 1;

    [ConfigKey("crawlerBombDurationMultiplier")]
    [Comment("Crawler Bomb Duration. What it summons lasts this many times longer. Step 0.05.")]
    [Range(SpellRules.MinDuration, SpellRules.MaxDuration, SpellRules.DurationStep)]
    public double CrawlerBombDurationMultiplier { get; set; } = 1;

    [ConfigKey("crawlerBombDamageMultiplier")]
    [Comment("Crawler Bomb Damage. Multiplies the item's share of the damage. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double CrawlerBombDamageMultiplier { get; set; } = 1;

    [ConfigKey("crawlerBombHealthMultiplier")]
    [Comment("Crawler Bomb Health. What it summons has this many times its health. Step 0.05.")]
    [Range(SpellRules.MinHealth, SpellRules.MaxHealth, SpellRules.HealthStep)]
    public double CrawlerBombHealthMultiplier { get; set; } = 1;

    // Cast Time: the spells' Charge Time, its tooltip line and the cast lock.

    [ConfigKey("fireBoltCastTimeMultiplier")]
    [Comment("Fire Bolt Cast Time. The spell's Charge Time and cast lock take this share of their time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double FireBoltCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("fireballCastTimeMultiplier")]
    [Comment("Fireball Cast Time. The spell's Charge Time and cast lock take this share of their time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double FireballCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("firePillarCastTimeMultiplier")]
    [Comment("Fire Pillar Cast Time. The spell's Charge Time and cast lock take this share of their time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double FirePillarCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("lightBurstCastTimeMultiplier")]
    [Comment("Light Burst Cast Time. The spell's Charge Time and cast lock take this share of their time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double LightBurstCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("iceBoltCastTimeMultiplier")]
    [Comment("Ice Bolt Cast Time. The spell's Charge Time and cast lock take this share of their time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double IceBoltCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("blizzardCastTimeMultiplier")]
    [Comment("Blizzard Cast Time. The spell's Charge Time and cast lock take this share of their time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double BlizzardCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("chainLightningCastTimeMultiplier")]
    [Comment("Chain Lightning Cast Time. The spell's Charge Time and cast lock take this share of their time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double ChainLightningCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("ballLightningCastTimeMultiplier")]
    [Comment("Ball Lightning Cast Time. The spell's Charge Time and cast lock take this share of their time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double BallLightningCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("lightningChannelCastTimeMultiplier")]
    [Comment("Lightning Channel Cast Time. The spell's Charge Time and cast lock take this share of their time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double LightningChannelCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("shroudMeteorCastTimeMultiplier")]
    [Comment("Shroud Meteor Cast Time. The spell's Charge Time and cast lock take this share of their time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double ShroudMeteorCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("shroudBeamCastTimeMultiplier")]
    [Comment("Shroud Beam Cast Time. The spell's Charge Time and cast lock take this share of their time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double ShroudBeamCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("totemsCastTimeMultiplier")]
    [Comment("Totems Cast Time. The spell's Charge Time and cast lock take this share of their time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double TotemsCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("skeletonCastTimeMultiplier")]
    [Comment("Skeleton Cast Time. The spell's Charge Time and cast lock take this share of their time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double SkeletonCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("lightningWispCastTimeMultiplier")]
    [Comment("Lightning Wisp Cast Time. The spell's Charge Time and cast lock take this share of their time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double LightningWispCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("acidConeCastTimeMultiplier")]
    [Comment("Acid Cone Cast Time. The spell's Charge Time and cast lock take this share of their time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double AcidConeCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("rootCastTimeMultiplier")]
    [Comment("Root Cast Time. The spell's Charge Time and cast lock take this share of their time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double RootCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("boneChannelCastTimeMultiplier")]
    [Comment("Bone Channel Cast Time. The spell's Charge Time and cast lock take this share of their time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double BoneChannelCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("magicShieldCastTimeMultiplier")]
    [Comment("Magic Shield Cast Time. The spell's Charge Time and cast lock take this share of their time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double MagicShieldCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("skullVesselCastTimeMultiplier")]
    [Comment("Skull Vessel Cast Time. The spell's Charge Time and cast lock take this share of their time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double SkullVesselCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("crawlerBombCastTimeMultiplier")]
    [Comment("Crawler Bomb Cast Time. The spell's Charge Time and cast lock take this share of their time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double CrawlerBombCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("generalCastTimeMultiplier")]
    [Comment("General Cast Time. Every spell's Cast Time, with its own. Under At Least, the shorter of the two. Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double GeneralCastTimeMultiplier { get; set; } = 1;

    // General: one multiplier per kind for every spell, with the spells' own (Multiply) or as a floor under them (At Least).
    [ConfigKey("generalCostMultiplier")]
    [Comment("General Mana Cost. Every spell's mana cost, with its own Mana Cost. Under At Least, the cheaper of the two. Step 0.05.")]
    [Range(SpellRules.MinCost, SpellRules.MaxCost, SpellRules.CostStep)]
    public double GeneralCostMultiplier { get; set; } = 1;

    [ConfigKey("generalDamageMultiplier")]
    [Comment("General Damage. Every spell's damage setting, with its own. Under At Least, the higher of the two. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double GeneralDamageMultiplier { get; set; } = 1;

    [ConfigKey("generalDurationMultiplier")]
    [Comment("General Duration. Every spell's duration setting, with its own. Under At Least, the higher of the two. Step 0.05.")]
    [Range(SpellRules.MinDuration, SpellRules.MaxDuration, SpellRules.DurationStep)]
    public double GeneralDurationMultiplier { get; set; } = 1;

    [ConfigKey("generalRadiusMultiplier")]
    [Comment("General Radius. Every spell's radius setting, with its own. Under At Least, the higher of the two. Step 0.05.")]
    [Range(SpellRules.MinRadius, SpellRules.MaxRadius, SpellRules.RadiusStep)]
    public double GeneralRadiusMultiplier { get; set; } = 1;

    [ConfigKey("generalHealthMultiplier")]
    [Comment("General Health. Every spell's health setting, with its own. Under At Least, the higher of the two. Step 0.05.")]
    [Range(SpellRules.MinHealth, SpellRules.MaxHealth, SpellRules.HealthStep)]
    public double GeneralHealthMultiplier { get; set; } = 1;

    [ConfigKey("generalAtLeast")]
    [Comment("General Mode. Off (Multiply): the General multipliers multiply each spell's own. On (At Least): each is a floor, so a spell keeps its own value only where it is better.")]
    public bool GeneralAtLeast { get; set; } = false;

    // Spellbook
    [ConfigKey("spellbookEnabled")]
    [Comment("Spellbook. Keeps your spells in a book beside the backpack: only the equipped spell takes a backpack slot, and the HUD's spell list follows the book's order. Off: the game's own list; the book keeps its spells until it is on again. Works in a world you host.")]
    public bool SpellbookEnabled { get; set; } = true;

    [ConfigKey("spellbookSlots")]
    [Comment("Spellbook Slots. How many spells the book holds, one slot per spell; a spell already in the book tops up its slot. Lowering it keeps the spells already in the book.")]
    [Range(SpellRules.MinSpellbookSlots, SpellRules.MaxSpellbookSlots, 1)]
    public int SpellbookSlots { get; set; } = SpellRules.DefaultSpellbookSlots;
}
