using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Kiln;

namespace PerennialCrops;

/// <summary>
/// Makes every planted crop perennial the way the game makes its own (banana, coconut, coffee, date palm, hazelnut): the ripe stage keeps its
/// inventory and refills it on a timer instead of being destroyed when looted. The changes are Kiln resource patches on the game's own templates,
/// staged in OnBoot from the settings and written before the game reads its data. A crop's ripe stage is often the template its wild version uses too,
/// so wild crops of those kinds refill as well (Kiln cannot copy the refill parts onto a private clone yet).
/// Which item a refill gives is a reference, which resource patches cannot set: OnBoot keeps the list in the mod's data folder and the running mod
/// sets those through Kiln's game data once the world's content is ready.
/// </summary>
public sealed class PerennialCropsMod : Mod
{
    private const string ItemsFile = "refill-items.json";
    private const string ResourceType = "keen::ecs::TemplateResource";

    public override void OnBoot(BootContext boot)
    {
        CropSettings settings = Config.Register<CropSettings>(ConfigKind.Common, null).Value;
        string itemsPath = Path.Combine(Context.DataDirectory, ItemsFile);
        Directory.CreateDirectory(Context.DataDirectory);
        if (!settings.Enabled)
        {
            File.WriteAllText(itemsPath, "[]");
            Log.Info("Mod Enabled is off: every crop stays the game's own");
            return;
        }

        CropPlan plan = CropPlanner.Plan(query => Read(boot, query), settings.GrowthSeconds, settings.RegrowthSeconds, settings.HideWhileRegrowing);
        var definitions = BootDefinitions.For(boot);
        foreach (TemplatePatch patch in plan.Patches)
        {
            var block = new ResourcePatchBlock
            {
                Resource = patch.Resource,
                Type = ResourceType,
                RemoveComponents = patch.RemoveComponents.Count > 0 ? new List<string>(patch.RemoveComponents) : null,
                CopyComponents = patch.CopyComponents.Count > 0 ? new Dictionary<string, Guid>(patch.CopyComponents) : null,
            };
            foreach (KeyValuePair<string, object> set in patch.Set)
            {
                block.Set[set.Key] = set.Value switch
                {
                    long whole => whole,
                    int whole => whole,
                    double number => number,
                    bool flag => flag,
                    string name => name,
                    _ => throw new InvalidOperationException($"{patch.TemplateName}: {set.Key} has a value of type {set.Value.GetType().Name}"),
                };
            }
            var staged = definitions.Define(patch.Key, block);
            if (!staged.Ok)
            {
                // A refusal keeps the whole mod out rather than half of the crops changed.
                throw new InvalidOperationException($"{patch.TemplateName}: {staged.Reason}");
            }
        }

        File.WriteAllText(itemsPath, JsonSerializer.Serialize(plan.Items.Select(i => new ItemRecord(i.Template, i.TemplateName, i.Slot, i.Item)).ToList()));
        foreach (string line in plan.Report)
        {
            Log.Info(line);
        }
        Log.Info($"{plan.Summary}; growth {settings.GrowthSeconds} s, regrowth {settings.RegrowthSeconds} s, " +
                 $"hide while regrowing {(settings.HideWhileRegrowing ? "on" : "off")}; {plan.Patches.Count} templates patched");
    }

    public override void OnLoad()
    {
        Config.Register<CropSettings>(ConfigKind.Common, null);
        BootModReport boot = Mods.BootState(Context.Info.Id);
        if (boot.State != BootModState.Committed)
        {
            string issues = string.Join("; ", boot.Issues.Select(i => i.ToString()));
            Log.Warn($"the crop changes are not in the game ({boot.State}){(issues.Length > 0 ? ": " + issues : "")}; restart the game after fixing it");
            return;
        }
        List<ItemRecord> items;
        try
        {
            items = JsonSerializer.Deserialize<List<ItemRecord>>(File.ReadAllText(Path.Combine(Context.DataDirectory, ItemsFile))) ?? new List<ItemRecord>();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            Log.Error($"the refill items could not be read ({e.Message}): perennial crops would refill the hazelnut's or banana's produce; restart the game");
            return;
        }
        if (items.Count == 0)
        {
            return;
        }
        if (!Content.Available || !Content.GameDataAvailable)
        {
            Log.Error("kiln.content or kiln.gamedata is not available: perennial crops would refill the hazelnut's or banana's produce");
            return;
        }
        Content.OnReady(_ => SetRefillItems(items));
    }

    /// <summary>Points each refill slot of the patched crops at the crop's own produce (the copies came from the hazelnut bush or the banana plant).</summary>
    private void SetRefillItems(List<ItemRecord> items)
    {
        int set = 0;
        var failures = new List<string>();
        foreach (ItemRecord item in items)
        {
            try
            {
                GameDataEntry? template = Content.Templates.Get(item.Template);
                if (template == null)
                {
                    failures.Add($"{item.TemplateName}: not found");
                    continue;
                }
                template.Set($"InteractionLootRefill.items[{item.Slot}].lootableItemRef", item.Item);
                set++;
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or KilnUnavailableException)
            {
                failures.Add($"{item.TemplateName} slot {item.Slot}: {e.Message}");
            }
        }
        if (failures.Count == 0)
        {
            Log.Info($"refill produce set on {set} slots of {items.Select(i => i.Template).Distinct().Count()} crops");
        }
        else
        {
            Log.Error($"refill produce set on {set} of {items.Count} slots; these refill the hazelnut's or banana's produce instead: {string.Join("; ", failures)}");
        }
    }

    private static GameRecord? Read(BootContext boot, string query)
    {
        BootDataResult result = boot.GameData.Read(query);
        return result.Ok && result.Data != null ? new GameRecord(result.Data.ResourceId, result.Data.Value) : null;
    }

    private sealed record ItemRecord(Guid Template, string TemplateName, int Slot, Guid Item);
}
