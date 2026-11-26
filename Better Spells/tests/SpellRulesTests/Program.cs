using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BetterSpells;
using Kiln;

// Checks the Kiln mod without the game: the steps and calculations (SpellRules), that the settings file's entries and the settings
// list agree (SpellSettings, SpellCatalog.Settings), and, with the folder of the game data dump as the first argument
// (D:\Dev\Enshrouded\Dump\<build>), that every name pattern finds exactly the items, templates and casts it is meant to.
int failures = 0;
void Check(bool condition, string what)
{
    if (!condition)
    {
        failures++;
        Console.WriteLine("FAIL: " + what);
    }
}

SettingInfo duration = SpellCatalog.Setting("totemDurationMultiplier");
SettingInfo damage = SpellCatalog.Setting("totemDamageMultiplier");
SettingInfo count = SpellCatalog.Setting(SpellCatalog.SkeletonsPerCastKey);
SettingInfo spread = SpellCatalog.Setting(SpellCatalog.SummonSpreadKey);

// Steps: a value lands on the nearest step inside the range.
Check(SpellRules.Snap(duration, 3.3712) == 3.35, "x3.3712 lands on x3.35 (step 0.05)");
Check(SpellRules.Snap(duration, 3.2) == 3.2 && SpellRules.Snap(duration, 0.5) == 0.5, "x3.2 and x0.5 are on a step");
Check(SpellRules.Snap(damage, 3.3712) == 3.35, "a damage of x3.3712 lands on x3.35");
Check(SpellRules.Snap(damage, 400) == 50, "above the range becomes the maximum");
Check(SpellRules.Snap(duration, 0.02) == 0.1, "below the range becomes the minimum, x0.1");
SettingInfo lockOn = SpellCatalog.Setting(SpellCatalog.ChainLockOnKey);
Check(SpellRules.Snap(lockOn, 1.33) == 1.35, "x1.33 lands on x1.35 (step 0.05 from 0.1)");
Check(SpellRules.Snap(spread, 24.6) == 25, "24.6 units land on 25");
Check(SpellRules.Snap(count, 2.6) == 3, "2.6 summons become 3");
Check(SpellRules.Snap(spread, 1.3) == 1 && SpellRules.Snap(spread, 1.5) == 2, "a spread of 1.3 units lands on 1, 1.5 on 2 (step 1)");
Check(SpellRules.Snap(spread, 40) == 25, "a spread above 25 units becomes 25");
Check(SpellRules.Snap(damage, double.NaN) == damage.Default, "a value that is not a number becomes the default");
foreach (SettingInfo setting in SpellCatalog.Settings.Where(setting => !setting.IsToggle))
{
    Check(SpellRules.Snap(setting, setting.Default) == setting.Default, $"{setting.Label}: the default is on a step");
    Check(SpellRules.Snap(setting, setting.Maximum) == setting.Maximum && SpellRules.Snap(setting, setting.Minimum) == setting.Minimum, $"{setting.Label}: both ends of the range are on a step");
}

// One click: a step up or down, stopping at the ends.
Check(SpellRules.Stepped(duration, 3.5, 1) == 3.55 && SpellRules.Stepped(duration, 3.5, -1) == 3.45, "a click moves Totem Duration by 0.05");
Check(SpellRules.Stepped(duration, 3.5, 10) == 4 && SpellRules.Stepped(duration, 3.5, -100) == 0.1, "Shift moves it by 0.5, Ctrl by 5, down to x0.1");
Check(SpellRules.Stepped(duration, 10, 1) == 10 && SpellRules.Stepped(duration, 0.1, -1) == 0.1, "a click at an end of the range stays there");
Check(SpellRules.Stepped(spread, 5, -1) == 4 && SpellRules.Stepped(spread, 5, 1) == 6, "a click moves Totem Spread by 1 unit");
Check(SpellRules.Stepped(damage, 3.3712, 1) == 3.4, "a click from a value between two steps goes from the nearest step");

// What the page shows.
Check(duration.Format(3.5) == "x3.5" && damage.Format(10) == "x10" && lockOn.Format(1.25) == "x1.25", "multipliers read x3.5, x10, x1.25");
Check(SpellCatalog.Setting(SpellCatalog.LightningWispShotLifetimeKey).Format(5) == "5 s" && SpellCatalog.Setting(SpellCatalog.LightningWispShotLifetimeKey).Format(2.5) == "2.5 s", "a shot lifetime reads 5 s or 2.5 s");
Check(count.Format(4) == "4" && spread.Format(2) == "2 units" && spread.Format(1) == "1 unit", "a count reads 4, a spread 2 units or 1 unit");
Check(SpellCatalog.Setting(SpellCatalog.InvulnerableTotemsKey).Format(1) == "on", "a toggle reads on or off");

// Values of the game.
Check(SpellRules.Multiplied(0.33, 5) > 1.6499 && SpellRules.Multiplied(0.33, 5) < 1.6501, "a damage modifier of 0.33 at x5 is 1.65");
Check(SpellRules.Multiplied(0, 50) == 0, "a value of 0 stays 0");
Check(SpellRules.Divided(0.5, 2) == 0.25 && SpellRules.Divided(0.5, 1) == 0.5, "a hit wait of 0.5 s at hit rate x2 is 0.25 s");
Check(SpellRules.LifetimeSeconds(8, 8, 10) == 80, "an 8 s totem at x10 lasts 80 s");
Check(SpellRules.LifetimeSeconds(4, 5, 2) == 9, "Acid Cone's 4 s explosion under a 5 s tooltip lasts 9 s at x2, keeping its 1 s gap");
Check(SpellRules.LifetimeSeconds(4, 5, 1) == 4, "x1 keeps the game's lifetime");
Check(SpellRules.LifetimeSeconds(6, double.NaN, 3) == 18, "without a tooltip duration the lifetime is multiplied");
Check(SpellRules.LifetimeSeconds(0, 5, 10) == 0, "no limit stays no limit");
Check(SpellRules.LifetimeSeconds(4, 5, 0.5) == 2 && SpellRules.LifetimeSeconds(8, 8, 0.1) > 0.79 && SpellRules.LifetimeSeconds(8, 8, 0.1) < 0.81, "below x1 a lifetime is multiplied: Acid Cone's 4 s at x0.5 is 2 s");
Check(SpellRules.TooltipValue(8000, 10) == 80000 && SpellRules.TooltipValue(699, 0.25) == 175, "tooltip lines are whole numbers (699 ms at x0.25 reads 175)");
Check(SpellRules.TooltipValue(260, 1.5) == 390, "a 2.6 m radius line at x1.5 reads 3.9 m");
Check(SpellRules.AreaFactor(2.6, 1.5, 0) == 1.5, "an area grows by the setting");
Check(SpellRules.Same(SpellRules.AreaFactor(3, 1, 5), 5.0 / 3.0) && SpellRules.Same(3 * SpellRules.AreaFactor(3, 2, 5), 10), "Shroud Meteor's 3 m blast is 5 m at x1 and 10 m at x2");
Check(SpellRules.FloatFromBits(1077936128) == 3.0f && SpellRules.BitsFromFloat(3.0f) == 1077936128, "a VFX word of 1077936128 is 3.0");
Check(SpellRules.BitsFromFloat(SpellRules.FloatFromBits(1077936128) * 1.5f) == 1083179008, "3.0 times 1.5 is the word of 4.5");
Check(SpellRules.ShapeKey(1744623269, 2) != 1744623269 && SpellRules.ShapeKey(1744623269, 2) == SpellRules.ShapeKey(1744623269, 2.0001), "a sphere at x2 gets a key of its own, the same for the same size");
Check(SpellRules.ShapeKey(1744623269, 2) != SpellRules.ShapeKey(1744623269, 2.25), "two sizes get two keys");
Check(SpellRules.Same(2.6, 2.5999999046325684) && !SpellRules.Same(2.6, 2.61), "a float32 and its double are the same value, 2.6 and 2.61 are not");

// The settings: 219, each once, in a known group, and the settings file's entries agree with the list.
Check(SpellCatalog.Settings.Count == 219, $"there are 219 settings (found {SpellCatalog.Settings.Count})");
Check(SpellCatalog.Settings.Select(setting => setting.Key).Distinct().Count() == SpellCatalog.Settings.Count, "no setting key is listed twice");
Check(SpellCatalog.Settings.All(setting => SpellCatalog.Groups.Contains(setting.Group)), "every setting is in a group of the settings page");
var fileDefaults = new SpellSettings();
var fileEntries = new Dictionary<string, PropertyInfo>();
foreach (PropertyInfo property in typeof(SpellSettings).GetProperties())
{
    string? key = property.GetCustomAttribute<ConfigKeyAttribute>()?.Key;
    Check(key != null && property.GetCustomAttribute<CommentAttribute>() != null, $"{property.Name} has a key and a comment");
    if (key != null)
    {
        fileEntries[key] = property;
    }
}
Check(fileEntries.Count == SpellCatalog.Settings.Count, $"the settings file has one entry per setting ({fileEntries.Count} entries)");
foreach (SettingInfo setting in SpellCatalog.Settings)
{
    if (!fileEntries.TryGetValue(setting.Key, out PropertyInfo? property))
    {
        Check(false, $"{setting.Key} is in the settings file");
        continue;
    }
    object? fileDefault = property.GetValue(fileDefaults);
    double defaultValue = fileDefault is bool on ? (on ? 1 : 0) : Convert.ToDouble(fileDefault);
    Check(defaultValue == setting.Default, $"{setting.Key}: the file's default {defaultValue} is the list's {setting.Default}");
    Check(property.PropertyType == (setting.IsToggle ? typeof(bool) : setting.Kind == SettingKind.Count ? typeof(int) : typeof(double)), $"{setting.Key}: the file's entry has the setting's kind");
    RangeAttribute? range = property.GetCustomAttribute<RangeAttribute>();
    Check(setting.IsToggle ? range == null : range != null && range.Min == setting.Minimum && range.Max == setting.Maximum, $"{setting.Key}: the file's range is the list's");
    Check(property.GetCustomAttribute<CommentAttribute>()!.Text.StartsWith(setting.Label, StringComparison.Ordinal), $"{setting.Key}: the file's comment starts with the label {setting.Label}");
}
foreach (string key in SpellCatalog.DurationSpells.Select(spell => spell.SettingKey).Concat(SpellCatalog.DamageSpells.Select(spell => spell.SettingKey))
    .Concat(SpellCatalog.RadiusSpells.SelectMany(spell => spell.VisualKey == null ? new[] { spell.SettingKey } : new[] { spell.SettingKey, spell.VisualKey }))
    .Concat(SpellCatalog.SummonCasts.Select(cast => cast.CountKey)))
{
    Check(fileEntries.ContainsKey(key), $"the spell list's {key} is a setting");
}

// The settings window shows every setting on exactly one card, in the sections the user chose, and each card's labels are its own.
List<string> windowKeys = SpellCatalog.WindowSpells.Concat(SpellCatalog.GemWindowSpells).SelectMany(spell => spell.Lines.Select(line => line.Key)).ToList();
Check(windowKeys.Count == SpellCatalog.Settings.Count && windowKeys.Distinct().Count() == windowKeys.Count, $"the window's two tabs have each of the 219 settings once ({windowKeys.Count} lines, {windowKeys.Distinct().Count()} different)");
Check(SpellCatalog.Settings.All(setting => windowKeys.Contains(setting.Key)), "no setting is missing from the window");
Check(string.Join(", ", SpellCatalog.WindowSections.Select(section => section.Name)) == "General, Fire, Ice, Lightning, Shroud, Summons, Others, Items", "the sections are General, Fire, Ice, Lightning, Shroud, Summons, Others, Items");
Check(SpellCatalog.WindowSpells.Where(spell => spell.IconItem != null).All(spell => spell.Lines[0].Key == SpellCatalog.CostKey(spell.Spell)), "every spell's card starts with its Mana Cost");
Check(SpellCatalog.CastTimeKey("Fire Bolt") == "fireBoltCastTimeMultiplier" && SpellCatalog.LowerIsBetter(SpellCatalog.GroupCastTime), "cast time keys, and a shorter cast time is the better one");
Check(SpellCatalog.CastTimeSpellNames.All(name => SpellCatalog.WindowSpells.Any(spell => spell.Spell == name && spell.Lines.Any(line => line.Key == SpellCatalog.CastTimeKey(name)))), "every Cast Time setting is on its card");
Check(SpellCatalog.CostKey("Fire Bolt") == "fireBoltCostMultiplier" && SpellCatalog.CostKey("Fireball") == "fireballCostMultiplier", "cost keys are the name in camel case");
Check(SpellCatalog.CostSpellNames.OrderBy(name => name).SequenceEqual(SpellCatalog.WindowSpells.Where(spell => spell.IconItem != null).Select(spell => spell.Spell).OrderBy(name => name)), "every spell card has a cost setting, and no other");

// General: Multiply multiplies, At Least keeps the better of the two (lower for a cost).
Check(SpellRules.Combine(3, 2, false, false) == 6, "Multiply: x3 and x2 give x6");
Check(SpellRules.Combine(1.5, 2, true, false) == 2 && SpellRules.Combine(3, 2, true, false) == 3, "At Least: x2 lifts x1.5, x3 stays");
Check(SpellRules.Combine(1, 0.5, true, true) == 0.5 && SpellRules.Combine(0.25, 0.5, true, true) == 0.25, "At Least for a cost: x0.5 lowers x1, x0.25 stays");
Check(SpellRules.ManaCost(26, 0.5) == 13 && SpellRules.ManaCost(26, 0.01) == 1 && SpellRules.ManaCost(70, 2) == 140, "mana cost is whole and never below 1");
foreach (SettingInfo setting in SpellCatalog.Settings)
{
    string? general = SpellCatalog.GeneralKeyFor(setting);
    bool expected = setting.Kind == SettingKind.Multiplier && (setting.Group is SpellCatalog.GroupCost or SpellCatalog.GroupCastTime or SpellCatalog.GroupDamage or SpellCatalog.GroupDuration or SpellCatalog.GroupRadius or SpellCatalog.GroupHealth or SpellCatalog.GroupGemDamage);
    Check((general != null) == expected, $"{setting.Key} {(expected ? "has" : "has no")} General multiplier");
}
Check(SpellCatalog.WindowSpells.Count == SpellCatalog.WindowSections.Sum(section => section.Spells.Length), "the flat list of spells is the sections' spells");

// Gems: a tab of their own, General first, a card per gem (its Damage when it deals any, then its stats), the Gems tab's own General and mode.
Check(SpellCatalog.GemSpells.Count == 29 && SpellCatalog.GemSpells.Select(gem => gem.ItemName).Distinct().Count() == 29, "29 gems, each once");
Check(SpellCatalog.GemSpells.Count(gem => gem.HasDamage) == 16, "16 gems deal damage");
Check(SpellCatalog.GemSpells.Single(gem => gem.Name == "Storm's Eye").DamageKey == "stormsEyeGemDamageMultiplier", "a gem's key drops its apostrophe: stormsEyeGemDamageMultiplier");
Check(SpellCatalog.GemSpells.Single(gem => gem.Name == "Meteoric Impact").DamageConfigs.Length == 3, "Meteoric Impact scales its hits, its meteor and its trail");
Check(string.Join(", ", SpellCatalog.GemSections.Select(section => section.Name)) == "General, Tier 1, Tier 2, Tier 3, Tier 4, Tier 5, Tier 6, Tier 7", "the Gems tab's rows are General and Tier 1 to 7");
Check(SpellCatalog.GemWindowSpells[0].Spell == SpellCatalog.GemGeneralSpell && SpellCatalog.GemWindowSpells.Count == 30, "the Gems tab: All Gems, then a card per gem");
foreach (GemSpell gem in SpellCatalog.GemSpells)
{
    WindowSpell card = SpellCatalog.GemWindowSpells.Single(spell => spell.Spell == gem.DisplayName);
    Check(card.IconItem != null && card.Lines.Length > 0, $"{gem.Name} has a card with an icon and settings");
    Check((card.Lines[0].Label == "Damage") == gem.HasDamage && card.Lines.Count(line => line.Label == "Damage") == (gem.HasDamage ? 1 : 0), $"{gem.Name}'s card has a Damage line only when it deals damage");
    Check(card.Lines.Select(line => line.Label).Distinct().Count() == card.Lines.Length, $"{gem.Name}'s card has no label twice");
}
Check(SpellCatalog.GemStats.Count == 65 && SpellCatalog.GemStats.All(stat => SpellCatalog.GemSpells.Any(gem => gem.Name == stat.Gem)), "65 gem stats, each of a known gem");
Check(SpellCatalog.GemStats.Select(stat => stat.Key).Distinct().Count() == 65, "no gem stat twice");
Check(SpellCatalog.GemStats.Single(stat => stat.Gem == "Storm's Eye" && stat.Label == "Mark Duration").Key == "stormsEyeGemMarkDurationMultiplier", "a gem stat's key: stormsEyeGemMarkDurationMultiplier");
Check(SpellCatalog.GemStats.Single(stat => stat.Gem == "Toxic Betrayal").PerLevelKey == "toxicBetrayalGemPoisonDurationPerLevelMultiplier", "Base + Per Level has a second multiplier");
Check(SpellCatalog.GemStats.Where(stat => stat.Kind == GemStatKind.LevelBonus).All(stat => stat.Program != null && stat.SpawnTemplate != null && SpellCatalog.Settings.Any(setting => setting.Key == stat.SyncKey && setting.IsToggle)), "each summon level has its program, template and Sync With Player toggle");
Check(SpellCatalog.GemStats.Where(stat => stat.Kind == GemStatKind.SetValue).All(stat => !double.IsNaN(stat.Minimum) && !double.IsNaN(stat.Maximum) && !double.IsNaN(stat.Step) && !double.IsNaN(stat.Default)), "each Set Value stat has its range and default");
Check(SpellCatalog.Settings.Where(setting => setting.Group == SpellCatalog.GroupGemStats).All(setting => SpellCatalog.GeneralKeyFor(setting) == null), "gem stats have no General multiplier");
SettingInfo burnTick = SpellCatalog.Setting("burningGemTickInterval");
Check(SpellRules.Snap(burnTick, 0.333) == 0.333 && burnTick.Format(0.333) == "0.333 s", "Burning's 0.333 s tick interval is its default, kept off the steps");
Check(SpellRules.Stepped(burnTick, 0.333, 1) == 0.35 && SpellRules.Stepped(burnTick, 0.333, -1) == 0.3, "from 0.333 s a click goes to 0.35 s up, 0.3 s down");
Check(SpellRules.Stepped(burnTick, 0.35, -1) == 0.3 && SpellRules.Stepped(burnTick, 0.35, 2) == 0.45 && SpellRules.Stepped(burnTick, 1, 1) == 1, "from a step a click moves one step, stopping at the end");
Check(SpellRules.Snap(burnTick, 0.34) == 0.35 && SpellRules.Snap(burnTick, 0.3331) == 0.35, "a value near but not at the default lands on a step");
SettingInfo coldSpeed = SpellCatalog.Setting("coldComposureGemMovementSpeed");
Check(coldSpeed.Format(-29.99) == "-29.99%" && SpellRules.Stepped(coldSpeed, -29.99, 1) == -25 && SpellRules.Stepped(coldSpeed, -29.99, -1) == -30, "Cold Composure's -29.99% steps to -25% and -30%");
Check(SpellRules.GemSeconds(6, 0.01) == 0.1 && SpellRules.GemSeconds(6, 2) == 12 && SpellRules.GemSeconds(0.05, 2) == 0.1 && SpellRules.GemSeconds(0.05, 0.5) == 0.05, "a gem time never goes below 0.1 s (one already shorter stays)");
Check(SpellRules.GemMilliseconds(10000, 0.1) == 1000 && SpellRules.GemMilliseconds(10000, 60) == 600000 && SpellRules.GemMilliseconds(500, 0.1) == 100, "Regeneration Pause: 10 s goes from 1 s to 600 s, never below 0.1 s");
Check(SpellRules.GemChance(0.25, 10) > 0.3499 && SpellRules.GemChance(0.25, 10) < 0.3501 && SpellRules.GemChance(0.95, 10) == 1 && SpellRules.GemChance(0.05, -10) == 0 && SpellRules.GemChance(0.25, 0) == 0.25, "a chance takes percentage points, inside 0 and 100%");
Check(SpellRules.GemLevel(5, 3) == 8 && SpellRules.GemLevel(5, 0) == 5, "a summon level takes added levels");
Check(SpellCatalog.Setting("skullsGemTriggerChancePoints").Format(10) == "+10%" && SpellCatalog.Setting("skullsGemTriggerChancePoints").Format(-5) == "-5%" && SpellCatalog.Setting("skullsGemSkullLevelBonus").Format(3) == "+3", "points read +10%, levels +3");
Check(SpellCatalog.Setting("battleManiaGemRegenerationPauseMultiplier").Maximum == 60, "Regeneration Pause goes up to x60 (600 s)");
foreach (GemSpell gem in SpellCatalog.GemSpells.Where(gem => gem.HasDamage))
{
    SettingInfo gemSetting = SpellCatalog.Setting(gem.DamageKey);
    Check(SpellCatalog.GeneralKeyFor(gemSetting) == SpellCatalog.GemGeneralDamageKey && SpellCatalog.AtLeastKeyFor(gemSetting) == SpellCatalog.GemGeneralAtLeastKey, $"{gem.Name} goes with the Gems tab's General Damage and mode");
}
Check(SpellCatalog.AtLeastKeyFor(damage) == SpellCatalog.GeneralAtLeastKey && SpellCatalog.GeneralKeyFor(damage) == SpellCatalog.GeneralDamageKey, "spells keep the spells' General and mode");
Check(SpellRules.GemDamage(7, 10) == 70 && SpellRules.GemDamage(3, 0.1) == 1 && SpellRules.GemDamage(3, 1.25) == 4 && SpellRules.GemDamage(0, 10) == 0, "gem damage is whole, never below 1, 0 stays");
Check(SpellCatalog.WindowSpells.Select(spell => spell.Spell).Distinct().Count() == SpellCatalog.WindowSpells.Count, "no spell has two cards");
foreach (WindowSpell spell in SpellCatalog.WindowSpells)
{
    Check(spell.Lines.Length > 0, $"{spell.Spell}'s card has a setting");
    Check(spell.IconItem != null || spell.Spell == SpellCatalog.GeneralSpell, $"{spell.Spell} names the items its icon comes from");
    Check(spell.Lines.Select(line => line.Label).Distinct().Count() == spell.Lines.Length, $"{spell.Spell}'s card has no label twice");
}
Check(SpellCatalog.WindowSections.Single(section => section.Name == "Summons").Spells.Select(spell => spell.Spell).SequenceEqual(new[] { "Totems", "Skeleton", "Lightning Wisp" }), "Summons holds Totems, Skeleton and Lightning Wisp");

// Against the game's own data: the dump names its files <name>__<guid>.json.
if (args.Length > 0)
{
    string[] Names(string folder) => Directory.GetFiles(Path.Combine(args[0], folder), "*__*.json").Select(path => Path.GetFileName(path).Split("__")[0]).ToArray();
    string[] items = Names("ItemInfo").Where(name => SpellCatalog.AnySpellItem.IsMatch(name)).ToArray();
    string[] templates = Names("ecs.TemplateResource");
    string[] casts = Names("actor.ActorSequenceResource");
    int ItemCount(Regex pattern) => items.Count(name => pattern.IsMatch(name));
    Check(items.Length == 102, $"the game has 102 spell items (found {items.Length})");

    var durationItems = new Dictionary<string, int>
    {
        ["totemDurationMultiplier"] = 10, ["skeletonDurationMultiplier"] = 4, ["magicShieldDurationMultiplier"] = 2, ["lightningWispDurationMultiplier"] = 4, ["blizzardDurationMultiplier"] = 4,
        ["lightningBallDurationMultiplier"] = 3, ["rootDurationMultiplier"] = 4, ["firePillarDurationMultiplier"] = 4, ["acidConeDurationMultiplier"] = 3,
    };
    foreach (DurationSpell spell in SpellCatalog.DurationSpells)
    {
        Check(ItemCount(spell.ItemName) == durationItems[spell.SettingKey], $"{spell.SettingKey}: {durationItems[spell.SettingKey]} items (found {ItemCount(spell.ItemName)})");
    }
    Check(SpellCatalog.DurationSpells.Sum(spell => ItemCount(spell.ItemName)) == 38, "the Duration settings cover 38 items");

    var damageItems = new Dictionary<string, int>
    {
        ["fireBoltDamageMultiplier"] = 8, ["fireballDamageMultiplier"] = 7, ["iceBoltDamageMultiplier"] = 8, ["acidConeDamageMultiplier"] = 3, ["firePillarDamageMultiplier"] = 4, ["blizzardDamageMultiplier"] = 4,
        ["lightningBallDamageMultiplier"] = 3, ["chainLightningDamageMultiplier"] = 5, ["shroudMeteorDamageMultiplier"] = 2, ["rootDamageMultiplier"] = 4, ["lightBurstDamageMultiplier"] = 3, ["totemDamageMultiplier"] = 10,
        ["lightningWispDamageMultiplier"] = 4, ["skeletonDamageMultiplier"] = 4, ["boneChannelDamageMultiplier"] = 7, ["shroudBeamDamageMultiplier"] = 2, ["lightningChannelDamageMultiplier"] = 3,
    };
    foreach (DamageSpell spell in SpellCatalog.DamageSpells)
    {
        Check(ItemCount(spell.ItemName) == damageItems[spell.SettingKey], $"{spell.SettingKey}: {damageItems[spell.SettingKey]} items (found {ItemCount(spell.ItemName)})");
    }
    Check(items.Count(name => SpellCatalog.DamageSpells.Count(spell => spell.ItemName.IsMatch(name)) > 1) == 0, "no item has two Damage settings");

    var radiusCounts = new Dictionary<string, (int Items, int Templates)>
    {
        ["fireballRadiusMultiplier"] = (7, 14), ["iceBoltRadiusMultiplier"] = (8, 14), ["rootRadiusMultiplier"] = (4, 4), ["firePillarRadiusMultiplier"] = (4, 4),
        ["blizzardRadiusMultiplier"] = (4, 4), ["shroudMeteorRadiusMultiplier"] = (2, 1), ["lightBurstRadiusMultiplier"] = (3, 2),
    };
    foreach (RadiusSpell spell in SpellCatalog.RadiusSpells)
    {
        int templateCount = spell.Templates.Sum(area => templates.Count(name => area.TemplateName.IsMatch(name)));
        Check((ItemCount(spell.ItemName), templateCount) == radiusCounts[spell.SettingKey], $"{spell.SettingKey}: {radiusCounts[spell.SettingKey]} items and templates (found {ItemCount(spell.ItemName)} and {templateCount})");
    }
    Check(SpellCatalog.RadiusSpells.Sum(spell => ItemCount(spell.ItemName)) == 32, "the Radius settings cover 32 items");
    string[] areaPrefixes = { "Explosion_", "Zone_", "Projectile_" };
    Check(SpellCatalog.RadiusSpells.SelectMany(spell => spell.Templates).All(area => templates.Where(name => area.TemplateName.IsMatch(name)).All(name => areaPrefixes.Any(name.StartsWith))),
        "every area template's name starts with Explosion_, Zone_ or Projectile_ (the mod only looks at those)");

    Check(templates.Count(name => SpellCatalog.BallTemplate.IsMatch(name)) == 3, "Ball Lightning has 3 ball templates");
    Check(casts.Contains(SpellCatalog.BallCastSequence), "Ball Lightning's cast is in the game's data");
    Check(casts.Contains(SpellCatalog.ChainCastSequence), "Chain Lightning's cast is in the game's data");
    string[] allItems = Names("ItemInfo");
    var itemCounts = new Dictionary<string, int> { ["Skull Vessel"] = 5, ["Fire Wisp Scroll"] = 1, ["Meteor Shower Scroll"] = 1, ["Crawler Bomb"] = 1 };
    foreach (ItemSpell spell in SpellCatalog.ItemSpells)
    {
        int found = allItems.Count(name => spell.Items.IsMatch(name));
        Check(found == itemCounts[spell.Name], $"{spell.Name}: {itemCounts[spell.Name]} items (found {found})");
        Check(spell.FixedTemplate == null || templates.Contains(spell.FixedTemplate), $"{spell.Name}'s template {spell.FixedTemplate} is in the game's data");
    }
    foreach (string player in SpellCatalog.PlayerTemplates)
    {
        string? file = Directory.GetFiles(Path.Combine(args[0], "ecs.TemplateResource"), player + "__*.json").FirstOrDefault();
        string text = file == null ? "" : File.ReadAllText(file);
        Check(text.Contains("keen::ecs::MaxTotemCount") && text.Contains("keen::ecs::MaxSkeletonSpellCount"), $"the player template {player} carries MaxTotemCount and MaxSkeletonSpellCount");
    }
    foreach ((string key, Regex itemName) in SpellCatalog.HealthSpells)
    {
        Check(ItemCount(itemName) > 0, $"{key} finds its spell's items");
    }
    foreach (SummonCast cast in SpellCatalog.SummonCasts)
    {
        Check(casts.Contains(cast.SequenceName), $"the cast {cast.SequenceName} is in the game's data");
    }
    Check(ItemCount(SpellCatalog.ChainLightningItem) == 5, "Chain Lightning has 5 items");
    foreach (WindowSpell spell in SpellCatalog.WindowSpells.Where(spell => spell.IconItem != null))
    {
        Check(Names("ItemInfo").Any(name => spell.IconItem!.IsMatch(name)), $"{spell.Spell} finds an item for its icon");
    }
    foreach (PerceptionSpell spell in SpellCatalog.PerceptionSpells)
    {
        Check(allItems.Any(name => spell.ItemName.IsMatch(name)), $"{spell.SettingKey} finds its summon's items");
        foreach (string name in spell.ExtraTemplates.Concat(spell.Shots))
        {
            Check(templates.Contains(name), $"{spell.SettingKey}: the template {name} is in the game's data");
        }
    }
    foreach (GemSpell gem in SpellCatalog.GemSpells)
    {
        string? file = Directory.GetFiles(Path.Combine(args[0], "ItemInfo"), gem.ItemName + "__*.json").FirstOrDefault();
        string text = file == null ? "" : File.ReadAllText(file);
        Check(text.Contains("\"WeaponGem\""), $"{gem.ItemName} is a weapon gem in the game's data");
        int scaledAt = text.IndexOf("\"scaled\"", StringComparison.Ordinal);
        foreach (uint config in gem.DamageConfigs)
        {
            bool found = scaledAt >= 0 && Regex.IsMatch(text.Substring(scaledAt), "ScaledSint32ImpactConfig\"[^$]*?\"configId\":\\s*\\{\\s*\"value\":\\s*" + config + "\\s*\\}");
            Check(found, $"{gem.ItemName} has the scaled whole-number config 0x{config:x8}");
        }
        foreach (GemStat stat in SpellCatalog.GemStats.Where(stat => stat.Gem == gem.Name))
        {
            bool found = Regex.IsMatch(text, "\"configId\":\\s*\\{\\s*\"value\":\\s*" + stat.Config + "\\s*\\}");
            Check(found, $"{gem.ItemName} has the config of {stat.DisplayName}, 0x{stat.Config:x8}");
        }
    }
    Console.WriteLine($"Checked against the game's data: {items.Length} spell items, {templates.Length} templates, {casts.Length} casts.");
}

Console.WriteLine(failures == 0 ? "All spell rule checks passed." : failures + " check(s) failed.");
return failures == 0 ? 0 : 1;
