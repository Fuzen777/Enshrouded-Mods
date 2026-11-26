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

    // Perception
    [ConfigKey("totemPerceptionMultiplier")]
    [Comment("Totem Perception. How far totems see and shoot at enemies, this many times the game's (20 units); their shots live as many times longer, so they still reach. Step 0.05.")]
    [Range(SpellRules.MinPerception, SpellRules.MaxPerception, SpellRules.PerceptionStep)]
    public double TotemPerceptionMultiplier { get; set; } = 1;

    [ConfigKey("skeletonPerceptionMultiplier")]
    [Comment("Skeleton Perception. How far skeletons notice enemies and go for them, this many times the game's (20 units). Step 0.05.")]
    [Range(SpellRules.MinPerception, SpellRules.MaxPerception, SpellRules.PerceptionStep)]
    public double SkeletonPerceptionMultiplier { get; set; } = 1;

    [ConfigKey("lightningWispPerceptionMultiplier")]
    [Comment("Lightning Wisp Perception. How far Lightning Wisps shoot at enemies, this many times the game's (20 units); their shots live as many times longer than Shot Lifetime, so they still reach. Step 0.05.")]
    [Range(SpellRules.MinPerception, SpellRules.MaxPerception, SpellRules.PerceptionStep)]
    public double LightningWispPerceptionMultiplier { get; set; } = 1;

    [ConfigKey("skullVesselPerceptionMultiplier")]
    [Comment("Skull Vessel Perception. How far the skulls it summons notice enemies and go for them, this many times the game's (30 units). Step 0.05.")]
    [Range(SpellRules.MinPerception, SpellRules.MaxPerception, SpellRules.PerceptionStep)]
    public double SkullVesselPerceptionMultiplier { get; set; } = 1;

    [ConfigKey("fireWispScrollPerceptionMultiplier")]
    [Comment("Fire Wisp Scroll Perception. How far the fire wisp notices and shoots at enemies, this many times the game's (20 to 30 units); its shots live as many times longer than Shot Lifetime, so they still reach. Step 0.05.")]
    [Range(SpellRules.MinPerception, SpellRules.MaxPerception, SpellRules.PerceptionStep)]
    public double FireWispScrollPerceptionMultiplier { get; set; } = 1;

    [ConfigKey("totemShotsThroughWalls")]
    [Comment("Totem Shots Through Walls. A totem's shots fly through terrain, walls and water and still hit enemies. Applies to shots fired afterwards. Experimental.")]
    public bool TotemShotsThroughWalls { get; set; } = false;

    [ConfigKey("lightningWispShotsThroughWalls")]
    [Comment("Lightning Wisp Shots Through Walls. A Lightning Wisp's shots fly through terrain, walls and water and still hit enemies. Applies to shots fired afterwards. Experimental.")]
    public bool LightningWispShotsThroughWalls { get; set; } = false;

    [ConfigKey("fireWispScrollShotsThroughWalls")]
    [Comment("Fire Wisp Scroll Shots Through Walls. A Fire Wisp's shots fly through terrain, walls and water and still hit enemies. Applies to shots fired afterwards. Experimental.")]
    public bool FireWispScrollShotsThroughWalls { get; set; } = false;

    [ConfigKey("lightningWispShotLifetime")]
    [Comment("Lightning Wisp Shot Lifetime (s). How long a Lightning Wisp's shot flies before it fades, at Perception x1 (the game's is 2 s, too short to reach enemies at the edge of its range). Step 0.5.")]
    [Range(SpellRules.MinShotLifetime, SpellRules.MaxShotLifetime, SpellRules.ShotLifetimeStep)]
    public double LightningWispShotLifetime { get; set; } = SpellRules.DefaultShotLifetime;

    [ConfigKey("fireWispScrollShotLifetime")]
    [Comment("Fire Wisp Scroll Shot Lifetime (s). How long the fire wisp's shot flies before it fades, at Perception x1 (the game's is 2 s). Step 0.5.")]
    [Range(SpellRules.MinShotLifetime, SpellRules.MaxShotLifetime, SpellRules.ShotLifetimeStep)]
    public double FireWispScrollShotLifetime { get; set; } = SpellRules.DefaultShotLifetime;

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

    [ConfigKey("ballLightningThroughWalls")]
    [Comment("Ball Lightning Through Walls. The ball flies through terrain, walls and water instead of stopping on them, and still hits enemies. Applies to balls cast afterwards. Experimental.")]
    public bool BallLightningThroughWalls { get; set; } = false;

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
    [Comment("Fire Bolt Cast Time. The spell's Charge Time, how long the staff is held before the spell goes, takes this share of its time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double FireBoltCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("fireballCastTimeMultiplier")]
    [Comment("Fireball Cast Time. The spell's Charge Time, how long the staff is held before the spell goes, takes this share of its time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double FireballCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("firePillarCastTimeMultiplier")]
    [Comment("Fire Pillar Cast Time. The spell's Charge Time, how long the staff is held before the spell goes, takes this share of its time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double FirePillarCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("lightBurstCastTimeMultiplier")]
    [Comment("Light Burst Cast Time. The spell's Charge Time, how long the staff is held before the spell goes, takes this share of its time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double LightBurstCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("iceBoltCastTimeMultiplier")]
    [Comment("Ice Bolt Cast Time. The spell's Charge Time, how long the staff is held before the spell goes, takes this share of its time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double IceBoltCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("blizzardCastTimeMultiplier")]
    [Comment("Blizzard Cast Time. The spell's Charge Time, how long the staff is held before the spell goes, takes this share of its time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double BlizzardCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("chainLightningCastTimeMultiplier")]
    [Comment("Chain Lightning Cast Time. The spell's Charge Time, how long the staff is held before the spell goes, takes this share of its time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double ChainLightningCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("ballLightningCastTimeMultiplier")]
    [Comment("Ball Lightning Cast Time. The spell's Charge Time, how long the staff is held before the spell goes, takes this share of its time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double BallLightningCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("lightningChannelCastTimeMultiplier")]
    [Comment("Lightning Channel Cast Time. The spell's Charge Time, how long the staff is held before the spell goes, takes this share of its time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double LightningChannelCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("shroudMeteorCastTimeMultiplier")]
    [Comment("Shroud Meteor Cast Time. The spell's Charge Time, how long the staff is held before the spell goes, takes this share of its time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double ShroudMeteorCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("shroudBeamCastTimeMultiplier")]
    [Comment("Shroud Beam Cast Time. The spell's Charge Time, how long the staff is held before the spell goes, takes this share of its time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double ShroudBeamCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("totemsCastTimeMultiplier")]
    [Comment("Totems Cast Time. The spell's Charge Time, how long the staff is held before the spell goes, takes this share of its time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double TotemsCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("skeletonCastTimeMultiplier")]
    [Comment("Skeleton Cast Time. The spell's Charge Time, how long the staff is held before the spell goes, takes this share of its time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double SkeletonCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("lightningWispCastTimeMultiplier")]
    [Comment("Lightning Wisp Cast Time. The spell's Charge Time, how long the staff is held before the spell goes, takes this share of its time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double LightningWispCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("acidConeCastTimeMultiplier")]
    [Comment("Acid Cone Cast Time. The spell's Charge Time, how long the staff is held before the spell goes, takes this share of its time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double AcidConeCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("rootCastTimeMultiplier")]
    [Comment("Root Cast Time. The spell's Charge Time, how long the staff is held before the spell goes, takes this share of its time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double RootCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("boneChannelCastTimeMultiplier")]
    [Comment("Bone Channel Cast Time. The spell's Charge Time, how long the staff is held before the spell goes, takes this share of its time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double BoneChannelCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("magicShieldCastTimeMultiplier")]
    [Comment("Magic Shield Cast Time. The spell's Charge Time, how long the staff is held before the spell goes, takes this share of its time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double MagicShieldCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("skullVesselCastTimeMultiplier")]
    [Comment("Skull Vessel Cast Time. The spell's Charge Time, how long the staff is held before the spell goes, takes this share of its time (x0.5 is half). Step 0.05.")]
    [Range(SpellRules.MinCastTime, SpellRules.MaxCastTime, SpellRules.CastTimeStep)]
    public double SkullVesselCastTimeMultiplier { get; set; } = 1;

    [ConfigKey("crawlerBombCastTimeMultiplier")]
    [Comment("Crawler Bomb Cast Time. The spell's Charge Time, how long the staff is held before the spell goes, takes this share of its time (x0.5 is half). Step 0.05.")]
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

    // Gems: each damage gem's damage, with the Gems tab's General Damage (Multiply or At Least).
    [ConfigKey("bleedingGemDamageMultiplier")]
    [Comment("Gem of Bleeding Damage. Multiplies the gem's damage at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double BleedingGemDamageMultiplier { get; set; } = 1;

    [ConfigKey("burningGemDamageMultiplier")]
    [Comment("Gem of Burning Damage. Multiplies the gem's damage at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double BurningGemDamageMultiplier { get; set; } = 1;

    [ConfigKey("puncturingGemDamageMultiplier")]
    [Comment("Gem of Puncturing Damage. Multiplies the gem's damage at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double PuncturingGemDamageMultiplier { get; set; } = 1;

    [ConfigKey("fracturingGemDamageMultiplier")]
    [Comment("Gem of Fracturing Damage. Multiplies the gem's damage at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double FracturingGemDamageMultiplier { get; set; } = 1;

    [ConfigKey("retaliationGemDamageMultiplier")]
    [Comment("Gem of Retaliation Damage. Multiplies the gem's damage at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double RetaliationGemDamageMultiplier { get; set; } = 1;

    [ConfigKey("swiftnessGemDamageMultiplier")]
    [Comment("Gem of Swiftness Damage. Multiplies the gem's damage at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double SwiftnessGemDamageMultiplier { get; set; } = 1;

    [ConfigKey("toxicBetrayalGemDamageMultiplier")]
    [Comment("Gem of Toxic Betrayal Damage. Multiplies the gem's damage at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double ToxicBetrayalGemDamageMultiplier { get; set; } = 1;

    [ConfigKey("agilityGemDamageMultiplier")]
    [Comment("Gem of Agility Damage. Multiplies the gem's damage at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double AgilityGemDamageMultiplier { get; set; } = 1;

    [ConfigKey("stormsEyeGemDamageMultiplier")]
    [Comment("Gem of Storm's Eye Damage. Multiplies the gem's damage at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double StormsEyeGemDamageMultiplier { get; set; } = 1;

    [ConfigKey("corruptionGemDamageMultiplier")]
    [Comment("Gem of Corruption Damage. Multiplies the gem's damage at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double CorruptionGemDamageMultiplier { get; set; } = 1;

    [ConfigKey("focusedLightGemDamageMultiplier")]
    [Comment("Gem of Focused Light Damage. Multiplies the gem's damage at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double FocusedLightGemDamageMultiplier { get; set; } = 1;

    [ConfigKey("bloodlustGemDamageMultiplier")]
    [Comment("Gem of Bloodlust Damage. Multiplies the gem's damage at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double BloodlustGemDamageMultiplier { get; set; } = 1;

    [ConfigKey("coldComposureGemDamageMultiplier")]
    [Comment("Gem of Cold Composure Damage. Multiplies the gem's damage at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double ColdComposureGemDamageMultiplier { get; set; } = 1;

    [ConfigKey("meteoricImpactGemDamageMultiplier")]
    [Comment("Gem of Meteoric Impact Damage. Multiplies the gem's damage at every gem level. Covers its meteor's explosion and fire trail too. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double MeteoricImpactGemDamageMultiplier { get; set; } = 1;

    [ConfigKey("aerobaticsGemDamageMultiplier")]
    [Comment("Gem of Aerobatics Damage. Multiplies the gem's damage at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double AerobaticsGemDamageMultiplier { get; set; } = 1;

    [ConfigKey("currentsGemDamageMultiplier")]
    [Comment("Gem of Currents Damage. Multiplies the gem's damage at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double CurrentsGemDamageMultiplier { get; set; } = 1;

    [ConfigKey("gemGeneralDamageMultiplier")]
    [Comment("Gem General Damage. Every gem's damage, with its own. Under At Least, the higher of the two. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinDamage, SpellRules.MaxDamage, SpellRules.DamageStep)]
    public double GemGeneralDamageMultiplier { get; set; } = 1;

    [ConfigKey("gemGeneralAtLeast")]
    [Comment("Gem General Mode. Off (Multiply): Gem General Damage multiplies each gem's own. On (At Least): it is a floor, so a gem keeps its own value only where it is higher.")]
    public bool GemGeneralAtLeast { get; set; } = false;

    // Gem stats (1.1.6): the gems' other numbers, card by card (SpellCatalog.GemStats).
    [ConfigKey("bleedingGemDebuffDurationMultiplier")]
    [Comment("Gem of Bleeding Debuff Duration. How long the bleed lasts. Multiplies its value at gem level 1. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, SpellRules.MaxGemMultiplier, SpellRules.GemMultiplierStep)]
    public double BleedingGemDebuffDurationMultiplier { get; set; } = 1;

    [ConfigKey("bleedingGemDebuffDurationPerLevelMultiplier")]
    [Comment("Gem of Bleeding Debuff Duration Per Level. Multiplies what each gem level adds to it. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, SpellRules.MaxGemMultiplier, SpellRules.GemMultiplierStep)]
    public double BleedingGemDebuffDurationPerLevelMultiplier { get; set; } = 1;

    [ConfigKey("bleedingGemTickDamageShareMultiplier")]
    [Comment("Gem of Bleeding Tick Damage Share. The share of the gem's damage each bleed tick deals. Multiplies the game's value at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double BleedingGemTickDamageShareMultiplier { get; set; } = 1;

    [ConfigKey("bleedingGemTickInterval")]
    [Comment("Gem of Bleeding Tick Interval. The time between two bleed ticks: shorter means more ticks, so more damage in all. Replaces the game's value (0.5 s) in seconds. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(0.1, 1, 0.05)]
    public double BleedingGemTickInterval { get; set; } = 0.5;

    [ConfigKey("burningGemDebuffDurationMultiplier")]
    [Comment("Gem of Burning Debuff Duration. How long the burn lasts. Multiplies its value at gem level 1. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, SpellRules.MaxGemMultiplier, SpellRules.GemMultiplierStep)]
    public double BurningGemDebuffDurationMultiplier { get; set; } = 1;

    [ConfigKey("burningGemDebuffDurationPerLevelMultiplier")]
    [Comment("Gem of Burning Debuff Duration Per Level. Multiplies what each gem level adds to it. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, SpellRules.MaxGemMultiplier, SpellRules.GemMultiplierStep)]
    public double BurningGemDebuffDurationPerLevelMultiplier { get; set; } = 1;

    [ConfigKey("burningGemCooldownMultiplier")]
    [Comment("Gem of Burning Cooldown. The time before the gem can burn again. Multiplies the game's value. Never below 0.1 s. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double BurningGemCooldownMultiplier { get; set; } = 1;

    [ConfigKey("burningGemTickDamageShareMultiplier")]
    [Comment("Gem of Burning Tick Damage Share. The share of the gem's damage each burn tick deals. Multiplies the game's value at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double BurningGemTickDamageShareMultiplier { get; set; } = 1;

    [ConfigKey("burningGemTickInterval")]
    [Comment("Gem of Burning Tick Interval. The time between two burn ticks: shorter means more ticks, so more damage in all. Replaces the game's value (0.333 s) in seconds. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(0.1, 1, 0.05)]
    public double BurningGemTickInterval { get; set; } = 0.333;

    [ConfigKey("puncturingGemDebuffDurationMultiplier")]
    [Comment("Gem of Puncturing Debuff Duration. How long enemies stay Punctured. The Gem of Retaliation applies the same debuff, so it follows too. Multiplies the game's value. Never below 0.1 s. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double PuncturingGemDebuffDurationMultiplier { get; set; } = 1;

    [ConfigKey("skullsGemTriggerChancePoints")]
    [Comment("Gem of Skulls Trigger Chance. Adds to the chance that a sneak or merciless kill spawns a skull, capped at 100%. In percentage points, also below 0. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 1.")]
    [Range(SpellRules.MinChancePoints, SpellRules.MaxChancePoints, 1)]
    public double SkullsGemTriggerChancePoints { get; set; } = 0;

    [ConfigKey("skullsGemSkullLevelBonus")]
    [Comment("Gem of Skulls Skull Level. Levels added to the companion skull's. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 1.")]
    [Range(SpellRules.MinLevelBonus, SpellRules.MaxLevelBonus, 1)]
    public double SkullsGemSkullLevelBonus { get; set; } = 0;

    [ConfigKey("skullsGemSyncWithPlayer")]
    [Comment("Gem of Skulls Sync With Player. The summon takes your level, plus Skull Level, instead of the gem's. Read when the weapon is equipped or switched to: switch away and back after a level up. Experimental.")]
    public bool SkullsGemSyncWithPlayer { get; set; } = false;

    [ConfigKey("fracturingGemTriggerChancePoints")]
    [Comment("Gem of Fracturing Trigger Chance. Adds to the chance that a blunt kill spawns shards, capped at 100%. In percentage points, also below 0. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 1.")]
    [Range(SpellRules.MinChancePoints, SpellRules.MaxChancePoints, 1)]
    public double FracturingGemTriggerChancePoints { get; set; } = 0;

    [ConfigKey("fracturingGemExtraShardChance")]
    [Comment("Gem of Fracturing Extra Shard Chance. The chance for each extra shard, up to 4. Replaces the game's value (50%) in percent. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 1.")]
    [Range(1, 100, 1)]
    public double FracturingGemExtraShardChance { get; set; } = 50;

    [ConfigKey("hopefulEmbersGemShroudTimeGainedMultiplier")]
    [Comment("Gem of Hopeful Embers Shroud Time Gained. The Shroud time each orb gives back. Multiplies the game's value at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double HopefulEmbersGemShroudTimeGainedMultiplier { get; set; } = 1;

    [ConfigKey("hopefulEmbersGemShroudTimeThresholdMultiplier")]
    [Comment("Gem of Hopeful Embers Shroud Time Threshold. Orbs drop only while your Shroud time is below this. Multiplies its value at gem level 1. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, SpellRules.MaxGemMultiplier, SpellRules.GemMultiplierStep)]
    public double HopefulEmbersGemShroudTimeThresholdMultiplier { get; set; } = 1;

    [ConfigKey("hopefulEmbersGemShroudTimeThresholdPerLevelMultiplier")]
    [Comment("Gem of Hopeful Embers Shroud Time Threshold Per Level. Multiplies what each gem level adds to it. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, SpellRules.MaxGemMultiplier, SpellRules.GemMultiplierStep)]
    public double HopefulEmbersGemShroudTimeThresholdPerLevelMultiplier { get; set; } = 1;

    [ConfigKey("retaliationGemTriggerChancePoints")]
    [Comment("Gem of Retaliation Trigger Chance. Adds to the chance that a block arms the bonus, capped at 100%. In percentage points, also below 0. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 1.")]
    [Range(SpellRules.MinChancePoints, SpellRules.MaxChancePoints, 1)]
    public double RetaliationGemTriggerChancePoints { get; set; } = 0;

    [ConfigKey("retaliationGemBuffDurationMultiplier")]
    [Comment("Gem of Retaliation Buff Duration. How long the armed bonus waits for your next attack. Multiplies the game's value. Never below 0.1 s. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double RetaliationGemBuffDurationMultiplier { get; set; } = 1;

    [ConfigKey("swiftnessGemBuffDurationMultiplier")]
    [Comment("Gem of Swiftness Buff Duration. How long the bonus lasts after sprinting (each hit refreshes it). Multiplies the game's value. Never below 0.1 s. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double SwiftnessGemBuffDurationMultiplier { get; set; } = 1;

    [ConfigKey("toxicBetrayalGemPoisonDurationMultiplier")]
    [Comment("Gem of Toxic Betrayal Poison Duration. How long the poison lasts. Multiplies its value at gem level 1. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, SpellRules.MaxGemMultiplier, SpellRules.GemMultiplierStep)]
    public double ToxicBetrayalGemPoisonDurationMultiplier { get; set; } = 1;

    [ConfigKey("toxicBetrayalGemPoisonDurationPerLevelMultiplier")]
    [Comment("Gem of Toxic Betrayal Poison Duration Per Level. Multiplies what each gem level adds to it. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, SpellRules.MaxGemMultiplier, SpellRules.GemMultiplierStep)]
    public double ToxicBetrayalGemPoisonDurationPerLevelMultiplier { get; set; } = 1;

    [ConfigKey("agilityGemStaminaOnHitMultiplier")]
    [Comment("Gem of Agility Stamina On Hit. The stamina the hit after an evade gives back. Multiplies the game's value at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double AgilityGemStaminaOnHitMultiplier { get; set; } = 1;

    [ConfigKey("agilityGemBuffDurationMultiplier")]
    [Comment("Gem of Agility Buff Duration. How long after an evade the bonus waits for a hit. Multiplies the game's value. Never below 0.1 s. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double AgilityGemBuffDurationMultiplier { get; set; } = 1;

    [ConfigKey("stormsEyeGemMarkDurationMultiplier")]
    [Comment("Gem of Storm's Eye Mark Duration. How long an enemy stays marked. Multiplies the game's value. Never below 0.1 s. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double StormsEyeGemMarkDurationMultiplier { get; set; } = 1;

    [ConfigKey("stormsEyeGemCooldownMultiplier")]
    [Comment("Gem of Storm's Eye Cooldown. The time before the same enemy can trigger it again. Multiplies the game's value. Never below 0.1 s. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double StormsEyeGemCooldownMultiplier { get; set; } = 1;

    [ConfigKey("lesserNecromancyGemTriggerChancePoints")]
    [Comment("Gem of Lesser Necromancy Trigger Chance. Adds to the chance that a kill spawns a Hollow Wretch, capped at 100%. In percentage points, also below 0. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 1.")]
    [Range(SpellRules.MinChancePoints, SpellRules.MaxChancePoints, 1)]
    public double LesserNecromancyGemTriggerChancePoints { get; set; } = 0;

    [ConfigKey("lesserNecromancyGemWretchLevelBonus")]
    [Comment("Gem of Lesser Necromancy Wretch Level. Levels added to the Wretch's. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 1.")]
    [Range(SpellRules.MinLevelBonus, SpellRules.MaxLevelBonus, 1)]
    public double LesserNecromancyGemWretchLevelBonus { get; set; } = 0;

    [ConfigKey("lesserNecromancyGemSyncWithPlayer")]
    [Comment("Gem of Lesser Necromancy Sync With Player. The summon takes your level, plus Wretch Level, instead of the gem's. Read when the weapon is equipped or switched to: switch away and back after a level up. Experimental.")]
    public bool LesserNecromancyGemSyncWithPlayer { get; set; } = false;

    [ConfigKey("overpoweringGemStaminaDamageMultiplier")]
    [Comment("Gem of Overpowering Stamina Damage. The stamina taken from a blocking enemy. Multiplies the game's value at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double OverpoweringGemStaminaDamageMultiplier { get; set; } = 1;

    [ConfigKey("corruptionGemExplosionDelayMultiplier")]
    [Comment("Gem of Corruption Explosion Delay. How long after the last hit a marked enemy explodes (3 s in the game). The marks follow, so they last until it explodes. Multiplies the game's value. Never below 0.1 s. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double CorruptionGemExplosionDelayMultiplier { get; set; } = 1;

    [ConfigKey("focusedLightGemDamageAt1StackMultiplier")]
    [Comment("Gem of Focused Light Damage At 1 Stack. The bonus's attack modifier at 1 stack. Multiplies the game's value at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double FocusedLightGemDamageAt1StackMultiplier { get; set; } = 1;

    [ConfigKey("focusedLightGemDamageAt2StacksMultiplier")]
    [Comment("Gem of Focused Light Damage At 2 Stacks. The bonus's attack modifier at 2 stacks. Multiplies the game's value at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double FocusedLightGemDamageAt2StacksMultiplier { get; set; } = 1;

    [ConfigKey("focusedLightGemDamageAt3StacksMultiplier")]
    [Comment("Gem of Focused Light Damage At 3 Stacks. The bonus's attack modifier at 3 stacks. Multiplies the game's value at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double FocusedLightGemDamageAt3StacksMultiplier { get; set; } = 1;

    [ConfigKey("battleManiaGemHealthRegenerationMultiplier")]
    [Comment("Gem of Battle Mania Health Regeneration. Health regeneration while Satisfied. Multiplies the game's value at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double BattleManiaGemHealthRegenerationMultiplier { get; set; } = 1;

    [ConfigKey("battleManiaGemManaRegenerationMultiplier")]
    [Comment("Gem of Battle Mania Mana Regeneration. Mana regeneration while Satisfied. Multiplies the game's value at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double BattleManiaGemManaRegenerationMultiplier { get; set; } = 1;

    [ConfigKey("battleManiaGemStaminaRegenerationMultiplier")]
    [Comment("Gem of Battle Mania Stamina Regeneration. Stamina regeneration while Satisfied. Multiplies the game's value at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double BattleManiaGemStaminaRegenerationMultiplier { get; set; } = 1;

    [ConfigKey("battleManiaGemBuffDurationMultiplier")]
    [Comment("Gem of Battle Mania Buff Duration. How long Satisfied lasts after a hit. Multiplies the game's value. Never below 0.1 s. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double BattleManiaGemBuffDurationMultiplier { get; set; } = 1;

    [ConfigKey("battleManiaGemRegenerationPauseMultiplier")]
    [Comment("Gem of Battle Mania Regeneration Pause. How long Unsatisfied stops regeneration. Multiplies the game's value. Never below 0.1 s. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 60, SpellRules.GemMultiplierStep)]
    public double BattleManiaGemRegenerationPauseMultiplier { get; set; } = 1;

    [ConfigKey("soulReapingGemHealingMultiplier")]
    [Comment("Gem of Soul Reaping Healing. The health a soul phial gives. Multiplies the game's value at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double SoulReapingGemHealingMultiplier { get; set; } = 1;

    [ConfigKey("soulReapingGemDebuffDurationMultiplier")]
    [Comment("Gem of Soul Reaping Debuff Duration. How long an enemy stays marked. Multiplies the game's value. Never below 0.1 s. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double SoulReapingGemDebuffDurationMultiplier { get; set; } = 1;

    [ConfigKey("bloodlustGemOrbChancePoints")]
    [Comment("Gem of Bloodlust Orb Chance. Adds to the chance that a kill drops a health orb, capped at 100%. In percentage points, also below 0. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 1.")]
    [Range(SpellRules.MinChancePoints, SpellRules.MaxChancePoints, 1)]
    public double BloodlustGemOrbChancePoints { get; set; } = 0;

    [ConfigKey("bloodlustGemOrbChanceWhileBuffedPoints")]
    [Comment("Gem of Bloodlust Orb Chance While Buffed. Adds to the orb chance while the buff is on, capped at 100%. In percentage points, also below 0. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 1.")]
    [Range(SpellRules.MinChancePoints, SpellRules.MaxChancePoints, 1)]
    public double BloodlustGemOrbChanceWhileBuffedPoints { get; set; } = 0;

    [ConfigKey("coldComposureGemPhysicalArmorMultiplier")]
    [Comment("Gem of Cold Composure Physical Armor. Physical armor while the buff is on. Multiplies the game's value at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double ColdComposureGemPhysicalArmorMultiplier { get; set; } = 1;

    [ConfigKey("coldComposureGemMagicalArmorMultiplier")]
    [Comment("Gem of Cold Composure Magical Armor. Magical armor while the buff is on. Multiplies the game's value at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double ColdComposureGemMagicalArmorMultiplier { get; set; } = 1;

    [ConfigKey("coldComposureGemMovementSpeed")]
    [Comment("Gem of Cold Composure Movement Speed. Your movement speed while the buff is on (the game slows you by 30%). Replaces the game's value (-29.99%) in percent. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 5.")]
    [Range(-90, 50, 5)]
    public double ColdComposureGemMovementSpeed { get; set; } = -29.99;

    [ConfigKey("coldComposureGemChargeTimeMultiplier")]
    [Comment("Gem of Cold Composure Charge Time. How long you block or aim before the buff starts. Multiplies the game's value. Never below 0.1 s. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double ColdComposureGemChargeTimeMultiplier { get; set; } = 1;

    [ConfigKey("meteoricImpactGemBuffDurationMultiplier")]
    [Comment("Gem of Meteoric Impact Buff Duration. How long attacks keep the bonus fire damage. Multiplies its value at gem level 1. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, SpellRules.MaxGemMultiplier, SpellRules.GemMultiplierStep)]
    public double MeteoricImpactGemBuffDurationMultiplier { get; set; } = 1;

    [ConfigKey("meteoricImpactGemBuffDurationPerLevelMultiplier")]
    [Comment("Gem of Meteoric Impact Buff Duration Per Level. Multiplies what each gem level adds to it. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, SpellRules.MaxGemMultiplier, SpellRules.GemMultiplierStep)]
    public double MeteoricImpactGemBuffDurationPerLevelMultiplier { get; set; } = 1;

    [ConfigKey("necromancyGemTriggerChancePoints")]
    [Comment("Gem of Necromancy Trigger Chance. Adds to the chance that a kill spawns a Hollow Damned, capped at 100%. In percentage points, also below 0. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 1.")]
    [Range(SpellRules.MinChancePoints, SpellRules.MaxChancePoints, 1)]
    public double NecromancyGemTriggerChancePoints { get; set; } = 0;

    [ConfigKey("necromancyGemDamnedLevelBonus")]
    [Comment("Gem of Necromancy Damned Level. Levels added to the Damned's. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 1.")]
    [Range(SpellRules.MinLevelBonus, SpellRules.MaxLevelBonus, 1)]
    public double NecromancyGemDamnedLevelBonus { get; set; } = 0;

    [ConfigKey("necromancyGemSyncWithPlayer")]
    [Comment("Gem of Necromancy Sync With Player. The summon takes your level, plus Damned Level, instead of the gem's. Read when the weapon is equipped or switched to: switch away and back after a level up. Experimental.")]
    public bool NecromancyGemSyncWithPlayer { get; set; } = false;

    [ConfigKey("aerobaticsGemVolleyWait")]
    [Comment("Gem of Aerobatics Volley Wait. The wait between two volleys. Replaces the game's value (1 s) in seconds. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.1.")]
    [Range(0.1, 10, 0.1)]
    public double AerobaticsGemVolleyWait { get; set; } = 1;

    [ConfigKey("currentsGemTriggerChancePoints")]
    [Comment("Gem of Currents Trigger Chance. Adds to the chance that a hit on a soaked enemy adds the shock, capped at 100%. In percentage points, also below 0. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 1.")]
    [Range(SpellRules.MinChancePoints, SpellRules.MaxChancePoints, 1)]
    public double CurrentsGemTriggerChancePoints { get; set; } = 0;

    [ConfigKey("currentsGemDebuffDurationMultiplier")]
    [Comment("Gem of Currents Debuff Duration. How long enemies stay Soaked. Multiplies its value at gem level 1. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, SpellRules.MaxGemMultiplier, SpellRules.GemMultiplierStep)]
    public double CurrentsGemDebuffDurationMultiplier { get; set; } = 1;

    [ConfigKey("currentsGemDebuffDurationPerLevelMultiplier")]
    [Comment("Gem of Currents Debuff Duration Per Level. Multiplies what each gem level adds to it. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, SpellRules.MaxGemMultiplier, SpellRules.GemMultiplierStep)]
    public double CurrentsGemDebuffDurationPerLevelMultiplier { get; set; } = 1;

    [ConfigKey("greaterNecromancyGemTriggerChancePoints")]
    [Comment("Gem of Greater Necromancy Trigger Chance. Adds to the chance that a kill spawns a Hollow Reaper, capped at 100%. In percentage points, also below 0. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 1.")]
    [Range(SpellRules.MinChancePoints, SpellRules.MaxChancePoints, 1)]
    public double GreaterNecromancyGemTriggerChancePoints { get; set; } = 0;

    [ConfigKey("greaterNecromancyGemReaperLevelBonus")]
    [Comment("Gem of Greater Necromancy Reaper Level. Levels added to the Reaper's. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 1.")]
    [Range(SpellRules.MinLevelBonus, SpellRules.MaxLevelBonus, 1)]
    public double GreaterNecromancyGemReaperLevelBonus { get; set; } = 0;

    [ConfigKey("greaterNecromancyGemSyncWithPlayer")]
    [Comment("Gem of Greater Necromancy Sync With Player. The summon takes your level, plus Reaper Level, instead of the gem's. Read when the weapon is equipped or switched to: switch away and back after a level up. Experimental.")]
    public bool GreaterNecromancyGemSyncWithPlayer { get; set; } = false;

    [ConfigKey("lycanthropyGemTriggerChancePoints")]
    [Comment("Gem of Lycanthropy Trigger Chance. Adds to the chance that a night kill drops a Lunar Orb, capped at 100%. In percentage points, also below 0. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 1.")]
    [Range(SpellRules.MinChancePoints, SpellRules.MaxChancePoints, 1)]
    public double LycanthropyGemTriggerChancePoints { get; set; } = 0;

    [ConfigKey("lycanthropyGemHealthOnOrbMultiplier")]
    [Comment("Gem of Lycanthropy Health On Orb. The health a Lunar Orb gives. Multiplies the game's value at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double LycanthropyGemHealthOnOrbMultiplier { get; set; } = 1;

    [ConfigKey("lycanthropyGemManaOnOrbMultiplier")]
    [Comment("Gem of Lycanthropy Mana On Orb. The mana a Lunar Orb gives. Multiplies the game's value at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double LycanthropyGemManaOnOrbMultiplier { get; set; } = 1;

    [ConfigKey("lycanthropyGemMaxHealthMultiplier")]
    [Comment("Gem of Lycanthropy Max Health. Max health while Lunar Howl is on. Multiplies the game's value at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double LycanthropyGemMaxHealthMultiplier { get; set; } = 1;

    [ConfigKey("lycanthropyGemMaxManaMultiplier")]
    [Comment("Gem of Lycanthropy Max Mana. Max mana while Lunar Howl is on. Multiplies the game's value at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double LycanthropyGemMaxManaMultiplier { get; set; } = 1;

    [ConfigKey("lycanthropyGemMaxStaminaMultiplier")]
    [Comment("Gem of Lycanthropy Max Stamina. Max stamina while Lunar Howl is on. Multiplies the game's value at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double LycanthropyGemMaxStaminaMultiplier { get; set; } = 1;

    [ConfigKey("lycanthropyGemMeleeDamageMultiplier")]
    [Comment("Gem of Lycanthropy Melee Damage. The melee damage bonus while Lunar Howl is on. Multiplies the game's value at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double LycanthropyGemMeleeDamageMultiplier { get; set; } = 1;

    [ConfigKey("lycanthropyGemBuffDurationMultiplier")]
    [Comment("Gem of Lycanthropy Buff Duration. How long Lunar Howl lasts. Multiplies its value at gem level 1. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, SpellRules.MaxGemMultiplier, SpellRules.GemMultiplierStep)]
    public double LycanthropyGemBuffDurationMultiplier { get; set; } = 1;

    [ConfigKey("lycanthropyGemBuffDurationPerLevelMultiplier")]
    [Comment("Gem of Lycanthropy Buff Duration Per Level. Multiplies what each gem level adds to it. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, SpellRules.MaxGemMultiplier, SpellRules.GemMultiplierStep)]
    public double LycanthropyGemBuffDurationPerLevelMultiplier { get; set; } = 1;

    [ConfigKey("armoringGemBuffDurationMultiplier")]
    [Comment("Gem of Armoring Buff Duration. How long you resist all damage after taking the orb. Multiplies its value at gem level 1. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, SpellRules.MaxGemMultiplier, SpellRules.GemMultiplierStep)]
    public double ArmoringGemBuffDurationMultiplier { get; set; } = 1;

    [ConfigKey("armoringGemBuffDurationPerLevelMultiplier")]
    [Comment("Gem of Armoring Buff Duration Per Level. Multiplies what each gem level adds to it. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, SpellRules.MaxGemMultiplier, SpellRules.GemMultiplierStep)]
    public double ArmoringGemBuffDurationPerLevelMultiplier { get; set; } = 1;

    [ConfigKey("cleansingGemDebuffDurationMultiplier")]
    [Comment("Gem of Cleansing Debuff Duration. How long an enemy loses its Shroud damage resistance. Multiplies its value at gem level 1. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, SpellRules.MaxGemMultiplier, SpellRules.GemMultiplierStep)]
    public double CleansingGemDebuffDurationMultiplier { get; set; } = 1;

    [ConfigKey("cleansingGemDebuffDurationPerLevelMultiplier")]
    [Comment("Gem of Cleansing Debuff Duration Per Level. Multiplies what each gem level adds to it. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, SpellRules.MaxGemMultiplier, SpellRules.GemMultiplierStep)]
    public double CleansingGemDebuffDurationPerLevelMultiplier { get; set; } = 1;

    [ConfigKey("destinyGemHealthOnOrbMultiplier")]
    [Comment("Gem of Destiny Health On Orb. The health the orb gives. Multiplies the game's value at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double DestinyGemHealthOnOrbMultiplier { get; set; } = 1;

    [ConfigKey("destinyGemManaOnOrbMultiplier")]
    [Comment("Gem of Destiny Mana On Orb. The mana the orb gives. Multiplies the game's value at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double DestinyGemManaOnOrbMultiplier { get; set; } = 1;

    [ConfigKey("destinyGemStaminaOnOrbMultiplier")]
    [Comment("Gem of Destiny Stamina On Orb. The stamina the orb gives. Multiplies the game's value at every gem level. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double DestinyGemStaminaOnOrbMultiplier { get; set; } = 1;

    [ConfigKey("destinyGemMarkDurationMultiplier")]
    [Comment("Gem of Destiny Mark Duration. How long an enemy stays Marked by Destiny. Multiplies the game's value. Never below 0.1 s. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double DestinyGemMarkDurationMultiplier { get; set; } = 1;

    [ConfigKey("hyperspecializationGemBuffDurationMultiplier")]
    [Comment("Gem of Hyperspecialization Buff Duration. How long after the special ability kills give focus back. Multiplies the game's value. Never below 0.1 s. A running gem takes it when its weapon is next equipped or switched to on the hotbar. Step 0.05.")]
    [Range(SpellRules.MinGemMultiplier, 10, SpellRules.GemMultiplierStep)]
    public double HyperspecializationGemBuffDurationMultiplier { get; set; } = 1;

    // Skill stats (1.2.2): skill-tree node configs, card by card (SpellCatalog.SkillStats). A change applies after returning to the main menu and loading the world again.
    [ConfigKey("constitutionSkillPerNodeBonus")]
    [Comment("Constitution Per Node. Added to what each Constitution node of the skill tree gives (game: 1 per node). Whole numbers only. Applies after returning to the main menu and loading the world again. Step 1.")]
    [Range(SpellRules.MinSkillNodeBonus, SpellRules.MaxSkillNodeBonus, 1)]
    public double ConstitutionSkillPerNodeBonus { get; set; } = 0;

    [ConfigKey("dexteritySkillPerNodeBonus")]
    [Comment("Dexterity Per Node. Added to what each Dexterity node of the skill tree gives (game: 1 per node). Whole numbers only. Applies after returning to the main menu and loading the world again. Step 1.")]
    [Range(SpellRules.MinSkillNodeBonus, SpellRules.MaxSkillNodeBonus, 1)]
    public double DexteritySkillPerNodeBonus { get; set; } = 0;

    [ConfigKey("enduranceSkillPerNodeBonus")]
    [Comment("Endurance Per Node. Added to what each Endurance node of the skill tree gives (game: 1 per node). Whole numbers only. Applies after returning to the main menu and loading the world again. Step 1.")]
    [Range(SpellRules.MinSkillNodeBonus, SpellRules.MaxSkillNodeBonus, 1)]
    public double EnduranceSkillPerNodeBonus { get; set; } = 0;

    [ConfigKey("intelligenceSkillPerNodeBonus")]
    [Comment("Intelligence Per Node. Added to what each Intelligence node of the skill tree gives (game: 1 per node). Whole numbers only. Applies after returning to the main menu and loading the world again. Step 1.")]
    [Range(SpellRules.MinSkillNodeBonus, SpellRules.MaxSkillNodeBonus, 1)]
    public double IntelligenceSkillPerNodeBonus { get; set; } = 0;

    [ConfigKey("spiritSkillPerNodeBonus")]
    [Comment("Spirit Per Node. Added to what each Spirit node of the skill tree gives (game: 1 per node). Whole numbers only. Applies after returning to the main menu and loading the world again. Step 1.")]
    [Range(SpellRules.MinSkillNodeBonus, SpellRules.MaxSkillNodeBonus, 1)]
    public double SpiritSkillPerNodeBonus { get; set; } = 0;

    [ConfigKey("strengthSkillPerNodeBonus")]
    [Comment("Strength Per Node. Added to what each Strength node of the skill tree gives (game: 1 per node). Whole numbers only. Applies after returning to the main menu and loading the world again. Step 1.")]
    [Range(SpellRules.MinSkillNodeBonus, SpellRules.MaxSkillNodeBonus, 1)]
    public double StrengthSkillPerNodeBonus { get; set; } = 0;

    [ConfigKey("warriorsPathSkillDamageMultiplier")]
    [Comment("Warrior's Path Damage. Multiplies what the skill gives at every level (game: 5% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double WarriorsPathSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("shinyPlatesSkillMagicalArmorMultiplier")]
    [Comment("Shiny Plates Magical Armor. Multiplies what the skill gives at every level (game: 4% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double ShinyPlatesSkillMagicalArmorMultiplier { get; set; } = 1;

    [ConfigKey("heavyPlatesSkillPhysicalArmorMultiplier")]
    [Comment("Heavy Plates Physical Armor. Multiplies what the skill gives at every level (game: 4% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double HeavyPlatesSkillPhysicalArmorMultiplier { get; set; } = 1;

    [ConfigKey("slasherSkillDamageMultiplier")]
    [Comment("Slasher Damage. Multiplies what the skill gives at every level (game: 10% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double SlasherSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("bruteSkillDamageMultiplier")]
    [Comment("Brute Damage. Multiplies what the skill gives at every level (game: 10% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double BruteSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("heavyHandedSkillEffectMultiplier")]
    [Comment("Heavy Handed Effect. Multiplies what the skill gives at every level (game: 10% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double HeavyHandedSkillEffectMultiplier { get; set; } = 1;

    [ConfigKey("veteranSkillCriticalChanceMultiplier")]
    [Comment("Veteran Critical Chance. Multiplies what the skill gives at every level (game: 2% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double VeteranSkillCriticalChanceMultiplier { get; set; } = 1;

    [ConfigKey("absorbSkillChanceMultiplier")]
    [Comment("Absorb Chance. Multiplies what the skill gives at every level (game: 4% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double AbsorbSkillChanceMultiplier { get; set; } = 1;

    [ConfigKey("bloodRageSkillRangeMultiplier")]
    [Comment("Blood Rage Range. Multiplies what the skill gives at every level (game: 5 per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double BloodRageSkillRangeMultiplier { get; set; } = 1;

    [ConfigKey("soulLeechSkillManaMultiplier")]
    [Comment("Soul Leech Mana. Multiplies what the skill gives at every level (game: 15 per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double SoulLeechSkillManaMultiplier { get; set; } = 1;

    [ConfigKey("soulLeechSkillRangeMultiplier")]
    [Comment("Soul Leech Range. Multiplies what the skill gives at every level (game: 5 per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double SoulLeechSkillRangeMultiplier { get; set; } = 1;

    [ConfigKey("battleHealSkillHealingMultiplier")]
    [Comment("Battle Heal Healing. Multiplies what the skill gives at every level (game: 1% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double BattleHealSkillHealingMultiplier { get; set; } = 1;

    [ConfigKey("purificationSkillShroudTimeMultiplier")]
    [Comment("Purification Shroud Time. Multiplies what the skill gives at every level (game: 3 per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double PurificationSkillShroudTimeMultiplier { get; set; } = 1;

    [ConfigKey("vigorousDeflectionSkillStaminaMultiplier")]
    [Comment("Vigorous Deflection Stamina. Multiplies what the skill gives at every level (game: 15 per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double VigorousDeflectionSkillStaminaMultiplier { get; set; } = 1;

    [ConfigKey("nemesisSkillAttentionMultiplier")]
    [Comment("Nemesis Attention. Multiplies what the skill gives at every level (game: 25% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double NemesisSkillAttentionMultiplier { get; set; } = 1;

    [ConfigKey("bashSkillDamageMultiplier")]
    [Comment("Bash Damage. Multiplies what the skill gives at every level (game: 10 per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double BashSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("finesseSkillChanceMultiplier")]
    [Comment("Finesse Chance. Multiplies what the skill gives at every level (game: 15% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double FinesseSkillChanceMultiplier { get; set; } = 1;

    [ConfigKey("steadfastSkillDurabilityMultiplier")]
    [Comment("Steadfast Durability. Multiplies what the skill gives at every level (game: 1 per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double SteadfastSkillDurabilityMultiplier { get; set; } = 1;

    [ConfigKey("titanEdgeSkillDamageMultiplier")]
    [Comment("Titan Edge Damage. Multiplies what the skill gives at every level (game: 5% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double TitanEdgeSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("athleteSkillCountMultiplier")]
    [Comment("Athlete Count. Multiplies what the skill gives at every level (game: 3 per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double AthleteSkillCountMultiplier { get; set; } = 1;

    [ConfigKey("barbariansPathSkillDamageMultiplier")]
    [Comment("Barbarian's Path Damage. Multiplies what the skill gives at every level (game: 5% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double BarbariansPathSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("airborneSkillCostReductionMultiplier")]
    [Comment("Airborne Cost Reduction. Multiplies what the skill gives at every level (game: 12% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double AirborneSkillCostReductionMultiplier { get; set; } = 1;

    [ConfigKey("marksmanSkillDamageMultiplier")]
    [Comment("Marksman Damage. Multiplies what the skill gives at every level (game: 5% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double MarksmanSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("snakeEaterSkillResistanceMultiplier")]
    [Comment("Snake Eater Resistance. Multiplies what the skill gives at every level (game: 4% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double SnakeEaterSkillResistanceMultiplier { get; set; } = 1;

    [ConfigKey("counterBatterySkillDamageMultiplier")]
    [Comment("Counter Battery Damage. Multiplies what the skill gives at every level (game: 8% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double CounterBatterySkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("rangerSkillEnduranceMultiplier")]
    [Comment("Ranger Endurance. Multiplies what the skill gives at every level (game: 1 per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double RangerSkillEnduranceMultiplier { get; set; } = 1;

    [ConfigKey("rangerSkillStaminaRegenerationMultiplier")]
    [Comment("Ranger Stamina Regeneration. Multiplies what the skill gives at every level (game: 5 per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double RangerSkillStaminaRegenerationMultiplier { get; set; } = 1;

    [ConfigKey("rangerSkillDexterityMultiplier")]
    [Comment("Ranger Dexterity. Multiplies what the skill gives at every level (game: 1 per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double RangerSkillDexterityMultiplier { get; set; } = 1;

    [ConfigKey("sniperSkillCriticalChanceMultiplier")]
    [Comment("Sniper Critical Chance. Multiplies what the skill gives at every level (game: 2% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double SniperSkillCriticalChanceMultiplier { get; set; } = 1;

    [ConfigKey("blessedArrowsSkillManaMultiplier")]
    [Comment("Blessed Arrows Mana. Multiplies what the skill gives at every level (game: 8 per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double BlessedArrowsSkillManaMultiplier { get; set; } = 1;

    [ConfigKey("thrustSkillDamageMultiplier")]
    [Comment("Thrust Damage. Multiplies what the skill gives at every level (game: 10% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double ThrustSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("backstabMasterySkillDamageMultiplier")]
    [Comment("Backstab Mastery Damage. Multiplies what the skill gives at every level (game: 20% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double BackstabMasterySkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("vitalitySurgeSkillStaminaMultiplier")]
    [Comment("Vitality Surge Stamina. Multiplies what the skill gives at every level (game: 3 per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double VitalitySurgeSkillStaminaMultiplier { get; set; } = 1;

    [ConfigKey("eaglesBaneSkillDamageMultiplier")]
    [Comment("Eagle's Bane Damage. Multiplies what the skill gives at every level (game: 10% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double EaglesBaneSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("skillShotSkillDamageMultiplier")]
    [Comment("Skill Shot Damage. Multiplies what the skill gives at every level (game: 20% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double SkillShotSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("swiftshotSustenanceSkillChanceMultiplier")]
    [Comment("Swiftshot Sustenance Chance. Multiplies what the skill gives at every level (game: 12% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double SwiftshotSustenanceSkillChanceMultiplier { get; set; } = 1;

    [ConfigKey("arachnoidSkillCostReductionMultiplier")]
    [Comment("Arachnoid Cost Reduction. Multiplies what the skill gives at every level (game: 20% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double ArachnoidSkillCostReductionMultiplier { get; set; } = 1;

    [ConfigKey("daggerMasterySkillDamageMultiplier")]
    [Comment("Dagger Mastery Damage. Multiplies what the skill gives at every level (game: 5% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double DaggerMasterySkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("cutthroatSkillDamageMultiplier")]
    [Comment("Cutthroat Damage. Multiplies what the skill gives at every level (game: 100% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double CutthroatSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("reboundSkillStaminaRegenerationMultiplier")]
    [Comment("Rebound Stamina Regeneration. Multiplies what the skill gives at every level (game: 10% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double ReboundSkillStaminaRegenerationMultiplier { get; set; } = 1;

    [ConfigKey("cullTheHerdSkillDamageMultiplier")]
    [Comment("Cull the Herd Damage. Multiplies what the skill gives at every level (game: 5% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double CullTheHerdSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("primalForceSkillDamageMultiplier")]
    [Comment("Primal Force Damage. Multiplies what the skill gives at every level (game: 5% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double PrimalForceSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("vileConcoctionSkillDamageMultiplier")]
    [Comment("Vile Concoction Damage. Multiplies what the skill gives at every level (game: 12% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double VileConcoctionSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("fatalPrecisionSkillCriticalDamageMultiplier")]
    [Comment("Fatal Precision Critical Damage. Multiplies what the skill gives at every level (game: 5% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double FatalPrecisionSkillCriticalDamageMultiplier { get; set; } = 1;

    [ConfigKey("fatalPrecisionSkillCriticalChanceMultiplier")]
    [Comment("Fatal Precision Critical Chance. Multiplies what the skill gives at every level (game: 1% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double FatalPrecisionSkillCriticalChanceMultiplier { get; set; } = 1;

    [ConfigKey("veiledVigorSkillCountMultiplier")]
    [Comment("Veiled Vigor Count. Multiplies what the skill gives at every level (game: 3 per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double VeiledVigorSkillCountMultiplier { get; set; } = 1;

    [ConfigKey("punctureSkillDamageMultiplier")]
    [Comment("Puncture Damage. Multiplies what the skill gives at every level (game: 10% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double PunctureSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("barrageSkillChanceMultiplier")]
    [Comment("Barrage Chance. Multiplies what the skill gives at every level (game: 15% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double BarrageSkillChanceMultiplier { get; set; } = 1;

    [ConfigKey("survivalInstinctSkillShroudTimeMultiplier")]
    [Comment("Survival Instinct Shroud Time. Multiplies what the skill gives at every level (game: 3 per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double SurvivalInstinctSkillShroudTimeMultiplier { get; set; } = 1;

    [ConfigKey("healingReviveSkillHealthMultiplier")]
    [Comment("Healing Revive Health. Multiplies what the skill gives at every level (game: 8% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double HealingReviveSkillHealthMultiplier { get; set; } = 1;

    [ConfigKey("mageApprenticeSkillDamageMultiplier")]
    [Comment("Mage Apprentice Damage. Multiplies what the skill gives at every level (game: 5% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double MageApprenticeSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("arsonistSkillDamageMultiplier")]
    [Comment("Arsonist Damage. Multiplies what the skill gives at every level (game: 10% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double ArsonistSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("darkArtsSkillDamageMultiplier")]
    [Comment("Dark Arts Damage. Multiplies what the skill gives at every level (game: 10% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double DarkArtsSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("icemanSkillDamageMultiplier")]
    [Comment("Iceman Damage. Multiplies what the skill gives at every level (game: 10% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double IcemanSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("frostSkillPrimerDurationMultiplier")]
    [Comment("Frost Primer Duration. Multiplies what the skill gives at every level (game: 3 s per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double FrostSkillPrimerDurationMultiplier { get; set; } = 1;

    [ConfigKey("quickChargeSkillChargeTimeMultiplier")]
    [Comment("Quick Charge Charge Time. Multiplies what the skill gives at every level (game: -10% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double QuickChargeSkillChargeTimeMultiplier { get; set; } = 1;

    [ConfigKey("counterstrikeSkillReflectedDamageMultiplier")]
    [Comment("Counterstrike Reflected Damage. Multiplies what the skill gives at every level (game: 20% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double CounterstrikeSkillReflectedDamageMultiplier { get; set; } = 1;

    [ConfigKey("counterstrikeSkillChanceMultiplier")]
    [Comment("Counterstrike Chance. Multiplies what the skill gives at every level (game: 8% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double CounterstrikeSkillChanceMultiplier { get; set; } = 1;

    [ConfigKey("wizardSkillCriticalChanceMultiplier")]
    [Comment("Wizard Critical Chance. Multiplies what the skill gives at every level (game: 2% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double WizardSkillCriticalChanceMultiplier { get; set; } = 1;

    [ConfigKey("necromancerSkillChanceMultiplier")]
    [Comment("Necromancer Chance. Multiplies what the skill gives at every level (game: 5% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double NecromancerSkillChanceMultiplier { get; set; } = 1;

    [ConfigKey("terrorSkillChanceMultiplier")]
    [Comment("Terror Chance. Multiplies what the skill gives at every level (game: 20% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double TerrorSkillChanceMultiplier { get; set; } = 1;

    [ConfigKey("bloodlettingSkillChanceMultiplier")]
    [Comment("Bloodletting Chance. Multiplies what the skill gives at every level (game: 20% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double BloodlettingSkillChanceMultiplier { get; set; } = 1;

    [ConfigKey("shroudMitigationSkillChanceMultiplier")]
    [Comment("Shroud Mitigation Chance. Multiplies what the skill gives at every level (game: 10% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double ShroudMitigationSkillChanceMultiplier { get; set; } = 1;

    [ConfigKey("healerSkillHealingMultiplier")]
    [Comment("Healer Healing. Multiplies what the skill gives at every level (game: 15% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double HealerSkillHealingMultiplier { get; set; } = 1;

    [ConfigKey("arcaneDeflectionSkillManaMultiplier")]
    [Comment("Arcane Deflection Mana. Multiplies what the skill gives at every level (game: 10 per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double ArcaneDeflectionSkillManaMultiplier { get; set; } = 1;

    [ConfigKey("thunderSkillDamageMultiplier")]
    [Comment("Thunder Damage. Multiplies what the skill gives at every level (game: 10% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double ThunderSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("waterAuraSkillRangeMultiplier")]
    [Comment("Water Aura Range. Multiplies its base value (game: 0.1 + 5 per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double WaterAuraSkillRangeMultiplier { get; set; } = 1;

    [ConfigKey("waterAuraSkillRangePerLevelMultiplier")]
    [Comment("Water Aura Range Per Level. Multiplies what each skill level adds to it. Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double WaterAuraSkillRangePerLevelMultiplier { get; set; } = 1;

    [ConfigKey("radiantAuraSkillDamageMultiplier")]
    [Comment("Radiant Aura Damage. Multiplies what the skill gives at every level (game: 1 per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double RadiantAuraSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("radiantAuraSkillRangeMultiplier")]
    [Comment("Radiant Aura Range. Multiplies its base value (game: 0.1 + 2 per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double RadiantAuraSkillRangeMultiplier { get; set; } = 1;

    [ConfigKey("radiantAuraSkillRangePerLevelMultiplier")]
    [Comment("Radiant Aura Range Per Level. Multiplies what each skill level adds to it. Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double RadiantAuraSkillRangePerLevelMultiplier { get; set; } = 1;

    [ConfigKey("unitySkillManaShareMultiplier")]
    [Comment("Unity Mana Share. Multiplies what the skill gives at every level (game: 2% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double UnitySkillManaShareMultiplier { get; set; } = 1;

    [ConfigKey("unitySkillChanceMultiplier")]
    [Comment("Unity Chance. Multiplies what the skill gives at every level (game: 10% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double UnitySkillChanceMultiplier { get; set; } = 1;

    [ConfigKey("eternalSparkSkillChanceMultiplier")]
    [Comment("Eternal Spark Chance. Multiplies what the skill gives at every level (game: 15% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double EternalSparkSkillChanceMultiplier { get; set; } = 1;

    [ConfigKey("lifeEssencesSkillCountMultiplier")]
    [Comment("Life Essences Count. Multiplies what the skill gives at every level (game: 3 per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double LifeEssencesSkillCountMultiplier { get; set; } = 1;

    [ConfigKey("shroudResistanceSkillResistanceMultiplier")]
    [Comment("Shroud Resistance Resistance. Multiplies what the skill gives at every level (game: 5% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double ShroudResistanceSkillResistanceMultiplier { get; set; } = 1;

    [ConfigKey("fireResistanceSkillResistanceMultiplier")]
    [Comment("Fire Resistance Resistance. Multiplies what the skill gives at every level (game: 5% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double FireResistanceSkillResistanceMultiplier { get; set; } = 1;

    [ConfigKey("iceResistanceSkillResistanceMultiplier")]
    [Comment("Ice Resistance Resistance. Multiplies what the skill gives at every level (game: 5% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double IceResistanceSkillResistanceMultiplier { get; set; } = 1;

    [ConfigKey("shockResistanceSkillResistanceMultiplier")]
    [Comment("Shock Resistance Resistance. Multiplies what the skill gives at every level (game: 5% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double ShockResistanceSkillResistanceMultiplier { get; set; } = 1;

    [ConfigKey("righteousFireSkillFireDamageMultiplier")]
    [Comment("Righteous Fire Fire Damage. Multiplies what the skill gives at every level (game: 20% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double RighteousFireSkillFireDamageMultiplier { get; set; } = 1;

    [ConfigKey("boneToAshSkillDamageMultiplier")]
    [Comment("Bone to Ash Damage. Multiplies what the skill gives at every level (game: 5% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double BoneToAshSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("sleightOfHandSkillDamageMultiplier")]
    [Comment("Sleight of Hand Damage. Multiplies what the skill gives at every level (game: 30% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double SleightOfHandSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("manaCurrentsSkillManaRegenerationMultiplier")]
    [Comment("Mana Currents Mana Regeneration. Multiplies what the skill gives at every level (game: 5 per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double ManaCurrentsSkillManaRegenerationMultiplier { get; set; } = 1;

    [ConfigKey("spellslingerSkillDamageMultiplier")]
    [Comment("Spellslinger Damage. Multiplies what the skill gives at every level (game: 5% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double SpellslingerSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("wandMasterySkillDamageMultiplier")]
    [Comment("Wand Mastery Damage. Multiplies what the skill gives at every level (game: 5% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double WandMasterySkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("totemMasterySkillTotemsPerLevel")]
    [Comment("Totem Mastery Totems Per Level. How many each skill level gives, in place of the game's 1. Applies after returning to the main menu and loading the world again. Step 1.")]
    [Range(SpellRules.MinSkillPerLevel, SpellRules.MaxSkillPerLevel, 1)]
    public int TotemMasterySkillTotemsPerLevel { get; set; } = 1;

    [ConfigKey("advancedNecromancySkillAlliesPerLevel")]
    [Comment("Advanced Necromancy Allies Per Level. How many each skill level gives, in place of the game's 1. Applies after returning to the main menu and loading the world again. Step 1.")]
    [Range(SpellRules.MinSkillPerLevel, SpellRules.MaxSkillPerLevel, 1)]
    public int AdvancedNecromancySkillAlliesPerLevel { get; set; } = 1;

    [ConfigKey("wellRestedSkillRestedDurationMultiplier")]
    [Comment("Well Rested Rested Duration. Multiplies what the skill gives at every level (game: 2 per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double WellRestedSkillRestedDurationMultiplier { get; set; } = 1;

    [ConfigKey("masonSkillDamageMultiplier")]
    [Comment("Mason Damage. Multiplies what the skill gives at every level (game: 10% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double MasonSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("lumberjackSkillDamageMultiplier")]
    [Comment("Lumberjack Damage. Multiplies what the skill gives at every level (game: 10% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double LumberjackSkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("powerParrySkillParryPowerMultiplier")]
    [Comment("Power Parry Parry Power. Multiplies what the skill gives at every level (game: 10% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double PowerParrySkillParryPowerMultiplier { get; set; } = 1;

    [ConfigKey("innerFiresSkillAmountMultiplier")]
    [Comment("Inner Fires Amount. Multiplies what the skill gives at every level (game: 60 per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double InnerFiresSkillAmountMultiplier { get; set; } = 1;

    [ConfigKey("opportunitySkillDamageMultiplier")]
    [Comment("Opportunity Damage. Multiplies what the skill gives at every level (game: 40% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double OpportunitySkillDamageMultiplier { get; set; } = 1;

    [ConfigKey("fishermansResolveSkillFishingEnduranceMultiplier")]
    [Comment("Fisherman's Resolve Fishing Endurance. Multiplies what the skill gives at every level (game: 5 per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double FishermansResolveSkillFishingEnduranceMultiplier { get; set; } = 1;

    [ConfigKey("prospectorSkillDamageMultiplier")]
    [Comment("Prospector Damage. Multiplies what the skill gives at every level (game: 10% per level). Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double ProspectorSkillDamageMultiplier { get; set; } = 1;

    // Skill groups (1.2.2): one value for several skill stats, At Least (the higher of the two) or Override (the group's).
    [ConfigKey("skillGroupStatsBonus")]
    [Comment("Stats Group. Bonus per node for the six attribute nodes' bonus (Constitution, Dexterity, Endurance, Intelligence, Spirit, Strength). At Least: each takes the higher of its own and this; Override: each takes this. Applies after returning to the main menu and loading the world again. Step 1.")]
    [Range(SpellRules.MinSkillNodeBonus, SpellRules.MaxSkillNodeBonus, 1)]
    public double SkillGroupStatsBonus { get; set; } = 0;

    [ConfigKey("skillGroupStatsOverride")]
    [Comment("Stats Group Mode. Off: At Least, each member takes the higher of its own value and the group's. On: Override, each member takes the group's value.")]
    public bool SkillGroupStatsOverride { get; set; } = false;

    [ConfigKey("skillGroupDamageBonusesMultiplier")]
    [Comment("Damage Bonuses Group. Multiplier for every \"% damage per level\" skill. At Least: each takes the higher of its own and this; Override: each takes this. Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double SkillGroupDamageBonusesMultiplier { get; set; } = 1;

    [ConfigKey("skillGroupDamageBonusesOverride")]
    [Comment("Damage Bonuses Group Mode. Off: At Least, each member takes the higher of its own value and the group's. On: Override, each member takes the group's value.")]
    public bool SkillGroupDamageBonusesOverride { get; set; } = false;

    [ConfigKey("skillGroupTerrainDamageMultiplier")]
    [Comment("Terrain Damage Group. Multiplier for Mason, Lumberjack and Prospector. At Least: each takes the higher of its own and this; Override: each takes this. Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double SkillGroupTerrainDamageMultiplier { get; set; } = 1;

    [ConfigKey("skillGroupTerrainDamageOverride")]
    [Comment("Terrain Damage Group Mode. Off: At Least, each member takes the higher of its own value and the group's. On: Override, each member takes the group's value.")]
    public bool SkillGroupTerrainDamageOverride { get; set; } = false;

    [ConfigKey("skillGroupCriticalChanceMultiplier")]
    [Comment("Critical Chance Group. Multiplier for Wizard, Sniper, Veteran and Fatal Precision's critical chance. At Least: each takes the higher of its own and this; Override: each takes this. Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double SkillGroupCriticalChanceMultiplier { get; set; } = 1;

    [ConfigKey("skillGroupCriticalChanceOverride")]
    [Comment("Critical Chance Group Mode. Off: At Least, each member takes the higher of its own value and the group's. On: Override, each member takes the group's value.")]
    public bool SkillGroupCriticalChanceOverride { get; set; } = false;

    [ConfigKey("skillGroupResistancesMultiplier")]
    [Comment("Resistances Group. Multiplier for Snake Eater and the Shroud, Fire, Ice and Shock Resistance skills. At Least: each takes the higher of its own and this; Override: each takes this. Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double SkillGroupResistancesMultiplier { get; set; } = 1;

    [ConfigKey("skillGroupResistancesOverride")]
    [Comment("Resistances Group Mode. Off: At Least, each member takes the higher of its own value and the group's. On: Override, each member takes the group's value.")]
    public bool SkillGroupResistancesOverride { get; set; } = false;

    [ConfigKey("skillGroupArmorMultiplier")]
    [Comment("Armor Group. Multiplier for Shiny Plates and Heavy Plates. At Least: each takes the higher of its own and this; Override: each takes this. Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double SkillGroupArmorMultiplier { get; set; } = 1;

    [ConfigKey("skillGroupArmorOverride")]
    [Comment("Armor Group Mode. Off: At Least, each member takes the higher of its own value and the group's. On: Override, each member takes the group's value.")]
    public bool SkillGroupArmorOverride { get; set; } = false;

    [ConfigKey("skillGroupManaStaminaGainsMultiplier")]
    [Comment("Mana and Stamina Gains Group. Multiplier for Blessed Arrows, Soul Leech, Arcane Deflection, Vitality Surge and Vigorous Deflection. At Least: each takes the higher of its own and this; Override: each takes this. Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double SkillGroupManaStaminaGainsMultiplier { get; set; } = 1;

    [ConfigKey("skillGroupManaStaminaGainsOverride")]
    [Comment("Mana and Stamina Gains Group Mode. Off: At Least, each member takes the higher of its own value and the group's. On: Override, each member takes the group's value.")]
    public bool SkillGroupManaStaminaGainsOverride { get; set; } = false;

    [ConfigKey("skillGroupHealingMultiplier")]
    [Comment("Healing Group. Multiplier for Healer, Battle Heal and Healing Revive. At Least: each takes the higher of its own and this; Override: each takes this. Applies after returning to the main menu and loading the world again. Step 0.05.")]
    [Range(SpellRules.MinSkillMultiplier, SpellRules.MaxSkillMultiplier, SpellRules.SkillMultiplierStep)]
    public double SkillGroupHealingMultiplier { get; set; } = 1;

    [ConfigKey("skillGroupHealingOverride")]
    [Comment("Healing Group Mode. Off: At Least, each member takes the higher of its own value and the group's. On: Override, each member takes the group's value.")]
    public bool SkillGroupHealingOverride { get; set; } = false;
}
