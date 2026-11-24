using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PerennialCrops;

// Runs the planner on Kiln's offline snapshot of the game's data (the records Mod.OnBoot reads), through snapshot_reader.py, and checks what it would
// change. Usage: CropPlannerTests [game folder]. The snapshot is the one Kiln's launcher made in <game>\kiln\data\boot-gamedata.
string game = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? @"D:\SteamLibrary\steamapps\common\Enshrouded";
string snapshots = Path.Combine(game, "kiln", "data", "boot-gamedata");
DirectoryInfo? snapshot = Directory.Exists(snapshots)
    ? new DirectoryInfo(snapshots).GetDirectories().Where(d => File.Exists(Path.Combine(d.FullName, "index.json"))).OrderByDescending(d => d.LastWriteTimeUtc).FirstOrDefault()
    : null;
if (snapshot == null)
{
    Console.WriteLine($"FAIL: no boot snapshot in {snapshots} (start the game once through Kiln's launcher with a boot mod enabled)");
    return 1;
}

int failures = 0;
void Check(bool condition, string what)
{
    if (!condition)
    {
        failures++;
        Console.WriteLine("FAIL: " + what);
    }
}

var start = new ProcessStartInfo("py", new[] { "-3.14", "-I", Path.Combine(AppContext.BaseDirectory, "snapshot_reader.py"), snapshot.FullName })
{
    RedirectStandardInput = true,
    RedirectStandardOutput = true,
    UseShellExecute = false,
};
using Process reader = Process.Start(start) ?? throw new InvalidOperationException("py could not start");
// A hard ceiling: a reader that stops answering fails the run instead of hanging it.
var watchdog = Task.Delay(TimeSpan.FromMinutes(3)).ContinueWith(_ =>
{
    if (!reader.HasExited)
    {
        Console.WriteLine("FAIL: the snapshot reader took longer than 3 minutes");
        reader.Kill(entireProcessTree: true);
        Environment.Exit(1);
    }
});

var cache = new Dictionary<string, GameRecord?>(StringComparer.Ordinal);
GameRecord? Read(string query)
{
    if (cache.TryGetValue(query, out GameRecord? known))
    {
        return known;
    }
    reader.StandardInput.WriteLine(query);
    string line = reader.StandardOutput.ReadLine() ?? throw new InvalidOperationException("the snapshot reader stopped");
    JsonElement answer = JsonDocument.Parse(line).RootElement.Clone();
    GameRecord? record = answer.GetProperty("ok").GetBoolean()
        ? new GameRecord(answer.GetProperty("resourceId").ValueKind == JsonValueKind.String ? Guid.Parse(answer.GetProperty("resourceId").GetString()!) : Guid.Empty,
            answer.GetProperty("value"))
        : null;
    cache[query] = record;
    return record;
}

Guid TemplateGuid(string name) => Read("template:" + name)?.ResourceId ?? Guid.Empty;
Guid hazelnut = TemplateGuid(CropPlanner.HazelnutDonor);
Guid banana = TemplateGuid(CropPlanner.BananaDonor);
Guid chest = TemplateGuid(CropPlanner.ChestDonor);
Guid chandelier = TemplateGuid(CropPlanner.InvertToggleDonor);
Check(hazelnut != Guid.Empty && banana != Guid.Empty && chest != Guid.Empty && chandelier != Guid.Empty, "the donor templates are in the snapshot");

CropPlan plan = CropPlanner.Plan(Read, 600, 600, true);
Console.WriteLine(plan.Summary);

// Eternal Harvest 0.3.0's report on the same game build: 64 plants, 44 refill plants of which 5 native, 20 growth only.
Check(plan.GrowthRoots == 64, $"64 planted growth roots (got {plan.GrowthRoots})");
Check(plan.MadePerennial == 39, $"39 crops made perennial (got {plan.MadePerennial})");
Check(plan.AlreadyPerennial == 5, $"5 already perennial: banana, coconut, coffee, date palm, hazelnut (got {plan.AlreadyPerennial})");
Check(plan.GrowthOnly == 20, $"20 growth only: 19 felled or chopped, artichoke's random drops (got {plan.GrowthOnly})");
Check(plan.Skipped == 0, $"nothing skipped (got {plan.Skipped}): " + string.Join(" | ", plan.Report.Where(l => l.StartsWith("SKIPPED", StringComparison.Ordinal))));
Check(plan.Report.Any(l => l.Contains("T7_Plant_Growth_Artichoke", StringComparison.Ordinal) && l.Contains("stays one-shot", StringComparison.Ordinal)), "artichoke stays one-shot");

TemplatePatch Stage(string name) => plan.Patches.Single(p => p.TemplateName == name);
List<ItemAssignment> ItemsOf(string name) => plan.Items.Where(i => i.TemplateName == name).OrderBy(i => i.Slot).ToList();

// Tomato: its default inventory gives 2 tomatoes; the refill parts come from the hazelnut bush.
TemplatePatch tomato = Stage("T1_Plant_Growth_Tomato_Seedling_2_harvest");
Check(tomato.RemoveComponents.Contains("DestroyOnLoot"), "tomato: DestroyOnLoot removed");
Check(CropPlanner.RefillParts.All(p => tomato.CopyComponents.GetValueOrDefault(p) == hazelnut), "tomato: refill parts copied from the hazelnut bush");
Check(tomato.CopyComponents.GetValueOrDefault("InteractionLootRefill") == hazelnut, "tomato: one produce, refill copied from the hazelnut bush");
Check(Equals(tomato.Set.GetValueOrDefault("InteractionLootRefill.items[0].maxCount"), 2L), "tomato: 2 per regrowth");
Check(Equals(tomato.Set.GetValueOrDefault("InteractionLootRefill.waitTime.value"), 600_000_000_000L), "tomato: regrowth 600 s in nanoseconds");
Check(ItemsOf(tomato.TemplateName).Select(i => i.Item).SequenceEqual(new[] { Guid.Parse("50f6d8b9-5b07-4f5b-b35c-d54bd2a315d2") }), "tomato: refills tomatoes");
Check(tomato.CopyComponents.GetValueOrDefault("ModelRenderHint") == chandelier && tomato.CopyComponents.GetValueOrDefault("ClientToggle") == chest,
    "tomato: hides while regrowing (the chest's empty toggle, the chandelier's inverted model)");
Check(Equals(tomato.Set.GetValueOrDefault("InventoryEmptyState.toggleVFX"), false), "tomato: no chest VFX on the empty toggle");

// Wheat gives two kinds of produce: the banana plant's refill has two slots.
TemplatePatch wheat = Stage("Plant_Wheat_Grown_Step03_Harvest");
Check(wheat.CopyComponents.GetValueOrDefault("InteractionLootRefill") == banana, "wheat: two produce, refill copied from the banana plant");
Check(ItemsOf(wheat.TemplateName).Count == 2 && wheat.Set.ContainsKey("InteractionLootRefill.items[1].maxCount"), "wheat: both slots set");
Check(Equals(wheat.Set.GetValueOrDefault("InventorySetup.availableSlotCount"), 2L), "wheat: room for 2 stacks");

// Cabbage has no inventory, one pickup drop of 2, dropped 5 times; its item comes from the pickup's template.
TemplatePatch cabbage = Stage("Scatter_Loot_AncientLand_Plant_Cabbage_01_d");
Check(Equals(cabbage.Set.GetValueOrDefault("InteractionLootRefill.items[0].maxCount"), 10L), "cabbage: 10 per regrowth");
Check(ItemsOf(cabbage.TemplateName).Count == 1 && ItemsOf(cabbage.TemplateName)[0].Item != Guid.Empty, "cabbage: its produce found through the pickup");

// Red mushrooms give 1 to 2: the refill gives the highest.
Check(Equals(Stage("T1_Plant_Growth_Mushroom_2_harvest").Set.GetValueOrDefault("InteractionLootRefill.items[0].maxCount"), 2L), "red mushrooms: 2 per regrowth");

// The game's own perennials only get the regrowth time (and the hiding), nothing copied over their refill.
TemplatePatch coffee = Stage("Scatter_Loot_Wetland_Coffeetree_d_fruits_harvest_Playerplanted_Refill");
Check(!coffee.CopyComponents.ContainsKey("InteractionLootRefill") && Equals(coffee.Set.GetValueOrDefault("InteractionLootRefill.waitTime.value"), 600_000_000_000L),
    "coffee: its own refill, at the regrowth time");
Check(ItemsOf(coffee.TemplateName).Count == 0, "coffee: its produce untouched");

// Growth: strawberry has three stages of factor 1, so 600 s is 200 s per stage, with no random stretch.
TemplatePatch strawberry = plan.Patches.Single(p => p.TemplateName == "T2_Plant_Growth_Strawberry");
Check(Equals(strawberry.Set.GetValueOrDefault("Growth.minDuration.value"), 200_000_000_000L), "strawberry: 200 s per stage");
Check(Equals(strawberry.Set.GetValueOrDefault("Growth.randomDurationScale.max"), 1.0), "strawberry: no random stretch");
Check(plan.Patches.Count(p => p.Key.StartsWith("growth-", StringComparison.Ordinal)) == 64, "every growth root gets the growth time");

// What Kiln accepts: staged names, one patch per template, the 4,096 changes of a start.
var keyPattern = new Regex("^[a-z][a-z0-9_.-]{0,95}$");
Check(plan.Patches.All(p => keyPattern.IsMatch(p.Key) && !p.Key.Contains("..", StringComparison.Ordinal)), "every staged name is one Kiln accepts");
Check(plan.Patches.Select(p => p.Key).Distinct().Count() == plan.Patches.Count, "staged names are unique");
Check(plan.Patches.Select(p => p.Resource).Distinct().Count() == plan.Patches.Count, "one patch per template");
int changes = plan.Patches.Sum(p => p.RemoveComponents.Count + p.CopyComponents.Count + p.Set.Count);
Console.WriteLine($"{plan.Patches.Count} templates, {changes} changes, {plan.Items.Count} refill items");
Check(changes <= 4096, $"at most 4,096 changes in one start (got {changes})");
Check(plan.Patches.All(p => p.Set.Keys.All(path => !path.Contains("lootableItemRef", StringComparison.Ordinal))), "no reference is set by a resource patch");

// Hiding off: nothing of the toggle is copied or set.
CropPlan shown = CropPlanner.Plan(Read, 600, 600, false);
Check(shown.Patches.All(p => !p.CopyComponents.ContainsKey("ModelRenderHint") && !p.Set.Keys.Any(k => k.StartsWith("ClientToggle", StringComparison.Ordinal))),
    "Hide While Regrowing off: crops stay visible");
Check(shown.MadePerennial == plan.MadePerennial, "Hide While Regrowing changes no crop's refill");

// Other times reach the patches.
CropPlan fast = CropPlanner.Plan(Read, 30, 45, true);
Check(Equals(fast.Patches.Single(p => p.TemplateName == "T2_Plant_Growth_Strawberry").Set["Growth.minDuration.value"], 10_000_000_000L), "growth 30 s: 10 s per strawberry stage");
Check(Equals(fast.Patches.Single(p => p.TemplateName == tomato.TemplateName).Set["InteractionLootRefill.waitTime.value"], 45_000_000_000L), "regrowth 45 s");

bool refused = false;
try
{
    CropPlanner.Plan(Read, 0, 600, true);
}
catch (ArgumentOutOfRangeException)
{
    refused = true;
}
Check(refused, "a growth time under 1 s is refused");

reader.StandardInput.Close();
reader.WaitForExit(10_000);
if (args.Contains("--report"))
{
    plan.Report.ForEach(Console.WriteLine);
}
Console.WriteLine(failures == 0 ? "All crop planner checks passed." : $"{failures} check(s) failed.");
return failures == 0 ? 0 : 1;
