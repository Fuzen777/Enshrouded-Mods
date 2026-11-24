using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PerennialCrops;

/// <summary>One record of the game's data: what Mod.OnBoot's boot.GameData.Read answers (the resource and its value as camelCase JSON).</summary>
public sealed record GameRecord(Guid ResourceId, JsonElement Value);

/// <summary>The changes to one game template, as a Kiln resource patch makes them: components removed, components copied whole from another template,
/// then numbers, bools and enum names set (on the template's own components or on the copies).</summary>
public sealed class TemplatePatch
{
    public TemplatePatch(string key, Guid resource, string templateName)
    {
        Key = key;
        Resource = resource;
        TemplateName = templateName;
    }

    /// <summary>The name the patch is staged under: lower case, as Kiln's boot definitions want.</summary>
    public string Key { get; }

    public Guid Resource { get; }

    public string TemplateName { get; }

    public List<string> RemoveComponents { get; } = new List<string>();

    public Dictionary<string, Guid> CopyComponents { get; } = new Dictionary<string, Guid>(StringComparer.Ordinal);

    /// <summary>Path to value: a long, a double, a bool or an enum member's name.</summary>
    public Dictionary<string, object> Set { get; } = new Dictionary<string, object>(StringComparer.Ordinal);
}

/// <summary>Which item a refill slot of a ripe crop gives. Resource patches cannot set a reference, so the mod sets these through Kiln's game data
/// once the world's content is ready.</summary>
public sealed record ItemAssignment(Guid Template, string TemplateName, int Slot, Guid Item);

/// <summary>Everything the mod changes, and a line per growth root saying what and why.</summary>
public sealed class CropPlan
{
    public List<TemplatePatch> Patches { get; } = new List<TemplatePatch>();

    public List<ItemAssignment> Items { get; } = new List<ItemAssignment>();

    public List<string> Report { get; } = new List<string>();

    /// <summary>The ripe stages that refill (made perennial or already perennial), by template name: what area harvest takes.</summary>
    public List<string> PerennialStages { get; } = new List<string>();

    public int GrowthRoots { get; set; }

    public int MadePerennial { get; set; }

    public int AlreadyPerennial { get; set; }

    public int GrowthOnly { get; set; }

    public int Skipped { get; set; }

    public string Summary =>
        $"{GrowthRoots} planted growth roots: {MadePerennial} crops made perennial, {AlreadyPerennial} already perennial, " +
        $"{GrowthOnly} with growth time only, {Skipped} skipped";
}

/// <summary>
/// Works out how to make every planted crop perennial from the game's data, as the game's own perennials (banana, coconut, coffee, date palm, hazelnut)
/// are made: their ripe stage keeps its inventory and refills it on a timer (InteractionLootRefill and the inventory and interaction parts it works
/// with) instead of being destroyed when looted. No Kiln types: the tests run it on Kiln's offline snapshot of the game's data.
/// </summary>
public static class CropPlanner
{
    public const string HazelnutDonor = "Scatter_Loot_DeepWoods_Hazelnut_d_nuts_harvest_Playerplanted_Refill";
    public const string BananaDonor = "Scatter_Loot_Wetland_Above_Bananaplant_01_e_fruits_harvest_PlayerPlanted_Refill";
    public const string ChestDonor = "Base_Chest_Player_Storage";
    public const string InvertToggleDonor = "Prop_Wetland_Lighting_Chandelier_01_trigger";

    private const string TemplateType = "keen::ecs::TemplateResource";
    private const string InventoryType = "keen::ecs::DefaultInventoryResource";
    private const string ItemType = "keen::ItemInfo";
    private const string Refill = "InteractionLootRefill";
    private const long NanosecondsPerSecond = 1_000_000_000L;

    /// <summary>The parts a perennial's ripe stage has and a one-shot crop's hasn't, besides the refill itself (copied from the hazelnut bush).</summary>
    public static readonly string[] RefillParts =
    {
        "InteractionLock", "ClientInteractionLock", "InteractionOfferRuntime", "ClientInteractionOffer",
        "InventorySetup", "InventorySetupInit", "InventorySetupOverwrite", "InventoryCraftingStock",
    };

    /// <summary>What destroys a one-shot crop when it is looted.</summary>
    public static readonly string[] DestroyParts =
    {
        "DestroyOnLoot", "DestroyOnLootActivated", "DestroyEntityOnLoot", "RemoveInteractionOnEmptyInventory",
    };

    /// <summary>The game's chest toggle that shows an inventory's empty state: copied with the chest's values (reason LootUi).</summary>
    public static readonly string[] EmptyToggleParts = { "ClientToggle", "ClientToggleState", "InventoryEmptyState" };

    private static readonly Regex GrowthRootName = new Regex(@"^T\d+_Plant_", RegexOptions.CultureInvariant);
    private static readonly Regex KeyUnsafe = new Regex("[^a-z0-9_.-]+", RegexOptions.CultureInvariant);

    /// <param name="growthSeconds">The growth time of every planted growth root; null keeps the game's own growth times.</param>
    public static CropPlan Plan(Func<string, GameRecord?> read, double? growthSeconds, double regrowthSeconds, bool hideWhileRegrowing)
    {
        if (growthSeconds is double wanted && !(wanted >= 1) || !(regrowthSeconds >= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(growthSeconds), "growth and regrowth times are at least 1 second");
        }
        var plan = new CropPlan();
        GameRecord hazelnut = Require(read, HazelnutDonor);
        GameRecord banana = Require(read, BananaDonor);
        foreach (string part in RefillParts.Append(Refill))
        {
            RequireComponent(hazelnut, HazelnutDonor, part);
        }
        RequireComponent(banana, BananaDonor, Refill);
        JsonElement hazelnutSetup = Component(hazelnut.Value, "InventorySetup")!.Value;

        GameRecord? chest = null;
        GameRecord? invertToggle = null;
        if (hideWhileRegrowing)
        {
            chest = read("template:" + ChestDonor);
            invertToggle = read("template:" + InvertToggleDonor);
            bool usable = chest != null && EmptyToggleParts.All(part => Component(chest.Value, part) != null)
                && invertToggle != null && Component(invertToggle.Value, "ModelRenderHint") is JsonElement hint
                && hint.TryGetProperty("toggleModifier", out JsonElement modifiers) && modifiers.ValueKind == JsonValueKind.Array
                && modifiers.EnumerateArray().Any(m => m.ValueKind == JsonValueKind.String && m.GetString() == "InvertToggle");
            if (!usable)
            {
                plan.Report.Add($"WARNING: {ChestDonor} or {InvertToggleDonor} no longer has the empty-inventory toggle: crops stay visible while they regrow");
                chest = null;
                invertToggle = null;
            }
        }

        GameRecord? names = read("names:templates");
        if (names == null || !names.Value.TryGetProperty("names", out JsonElement nameList))
        {
            throw new InvalidOperationException("the game's template names could not be read");
        }
        List<string> roots = nameList.EnumerateArray()
            .Select(n => n.GetString() ?? "")
            .Where(n => GrowthRootName.IsMatch(n) && n.IndexOf("DEPRECATED", StringComparison.Ordinal) < 0)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        var patchedStages = new HashSet<Guid>();

        foreach (string rootName in roots)
        {
            GameRecord? root = read("template:" + rootName);
            if (root == null)
            {
                continue;
            }
            JsonElement? growth = Component(root.Value, "Growth");
            JsonElement? upgrade = Component(root.Value, "Upgrade");
            if (growth == null || upgrade == null || !upgrade.Value.TryGetProperty("levels", out JsonElement levels) || levels.GetArrayLength() == 0)
            {
                continue;
            }
            plan.GrowthRoots++;

            // The growth time is the root's minDuration times the sum of the stages' growth factors: the whole time is spread over them.
            double factorSum = 0;
            JsonElement finalLevel = default;
            long finalLevelNumber = long.MinValue;
            foreach (JsonElement level in levels.EnumerateArray())
            {
                factorSum += level.TryGetProperty("growthFactor", out JsonElement factor) && factor.ValueKind == JsonValueKind.Number ? factor.GetDouble() : 1.0;
                long number = level.TryGetProperty("level", out JsonElement n) && n.ValueKind == JsonValueKind.Number ? n.GetInt64() : 0;
                if (number > finalLevelNumber)
                {
                    finalLevelNumber = number;
                    finalLevel = level;
                }
            }
            if (!(factorSum > 0))
            {
                plan.Skipped++;
                plan.Report.Add($"SKIPPED {rootName}: its growth stages have no growth factor");
                continue;
            }
            string nativeGrowth = Seconds(NativeSeconds(growth.Value, "minDuration") * factorSum);
            string timing = "growth " + nativeGrowth + " (the game's)";
            if (growthSeconds is double synced)
            {
                var rootPatch = new TemplatePatch(KeyFor("growth", rootName), root.ResourceId, rootName);
                rootPatch.Set["Growth.minDuration.value"] = (long)Math.Round(synced * NanosecondsPerSecond / factorSum);
                rootPatch.Set["Growth.randomDurationScale.min"] = 1.0;
                rootPatch.Set["Growth.randomDurationScale.max"] = 1.0;
                plan.Patches.Add(rootPatch);
                timing = $"growth {Seconds(synced)} (game: {nativeGrowth})";
            }

            Guid? stageGuid = Reference(finalLevel, "upgradeEntity");
            GameRecord? stage = stageGuid == null ? null : read($"resource:{stageGuid.Value:D}/{TemplateType}/0");
            if (stage == null)
            {
                plan.Skipped++;
                plan.Report.Add($"SKIPPED {rootName}: {timing}; its ripe stage could not be read");
                continue;
            }
            string stageName = stage.Value.TryGetProperty("name", out JsonElement sn) ? sn.GetString() ?? stageGuid.ToString()! : stageGuid.ToString()!;
            if (!patchedStages.Add(stage.ResourceId))
            {
                plan.Report.Add($"PLANT {rootName}: {timing}; ripe stage {stageName} already changed for another plant");
                continue;
            }

            JsonElement? nativeRefill = Component(stage.Value, Refill);
            if (nativeRefill != null)
            {
                var stagePatch = new TemplatePatch(KeyFor("refill", stageName), stage.ResourceId, stageName);
                stagePatch.Set["InteractionLootRefill.waitTime.value"] = Nanoseconds(regrowthSeconds);
                AddEmptyToggle(stagePatch, chest, invertToggle);
                plan.Patches.Add(stagePatch);
                plan.AlreadyPerennial++;
                plan.PerennialStages.Add(stageName);
                plan.Report.Add($"PLANT {rootName}: {timing}; already perennial ({stageName}), regrowth {Seconds(regrowthSeconds)} (game: {Seconds(NativeSeconds(nativeRefill.Value, "waitTime"))})");
                continue;
            }
            if (Component(stage.Value, "DestroyOnLoot") == null)
            {
                plan.GrowthOnly++;
                plan.Report.Add($"PLANT {rootName}: {timing}; not looted (felled or chopped), growth time only");
                continue;
            }

            List<(Guid Item, long Count)> yield;
            try
            {
                yield = Harvest(read, stage.Value);
            }
            catch (UnsupportedHarvestException unsupported)
            {
                plan.GrowthOnly++;
                plan.Report.Add($"PLANT {rootName}: {timing}; stays one-shot ({unsupported.Message})");
                continue;
            }
            GameRecord refillDonor = yield.Count == 1 ? hazelnut : banana;

            var patch = new TemplatePatch(KeyFor("refill", stageName), stage.ResourceId, stageName);
            foreach (string part in DestroyParts)
            {
                if (Component(stage.Value, part) != null)
                {
                    patch.RemoveComponents.Add(part);
                }
            }
            foreach (string part in RefillParts)
            {
                patch.CopyComponents[part] = hazelnut.ResourceId;
            }
            patch.CopyComponents[Refill] = refillDonor.ResourceId;
            patch.Set["InteractionLootRefill.waitTime.value"] = Nanoseconds(regrowthSeconds);
            for (int slot = 0; slot < yield.Count; slot++)
            {
                patch.Set[$"InteractionLootRefill.items[{slot}].refillCount"] = yield[slot].Count;
                patch.Set[$"InteractionLootRefill.items[{slot}].maxCount"] = yield[slot].Count;
                plan.Items.Add(new ItemAssignment(stage.ResourceId, stageName, slot, yield[slot].Item));
            }
            patch.Set["InventorySetup.genericSlotCount"] = Math.Max(Int(hazelnutSetup, "genericSlotCount"), yield.Count);
            patch.Set["InventorySetup.availableSlotCount"] = Math.Max(Int(hazelnutSetup, "availableSlotCount"), yield.Count);
            AddEmptyToggle(patch, chest, invertToggle);
            plan.Patches.Add(patch);
            plan.MadePerennial++;
            plan.PerennialStages.Add(stageName);
            string produce = string.Join(", ", yield.Select(y => $"{y.Count} x {ItemName(read, y.Item)}"));
            plan.Report.Add($"PLANT {rootName}: {timing}; made perennial ({stageName}): {produce} every {Seconds(regrowthSeconds)}");
        }
        return plan;
    }

    /// <summary>What one harvest of a one-shot crop gives: its default inventory (every stack certain, the highest count), or its single pickup drop.</summary>
    public static List<(Guid Item, long Count)> Harvest(Func<string, GameRecord?> read, JsonElement stage)
    {
        var counts = new Dictionary<Guid, long>();
        var order = new List<Guid>();
        void Add(Guid item, long count)
        {
            if (count <= 0)
            {
                throw new UnsupportedHarvestException("an item count is not positive");
            }
            if (!counts.ContainsKey(item))
            {
                counts[item] = 0;
                order.Add(item);
            }
            counts[item] += count;
        }

        if (Component(stage, "DefaultInventory") is JsonElement inventoryPart)
        {
            Guid inventoryGuid = Reference(inventoryPart, "defaultInventory") ?? throw new UnsupportedHarvestException("its inventory has no contents");
            GameRecord inventory = read($"resource:{inventoryGuid:D}/{InventoryType}/0") ?? throw new UnsupportedHarvestException("its inventory could not be read");
            if (!inventory.Value.TryGetProperty("rootGroup", out JsonElement rootGroup))
            {
                throw new UnsupportedHarvestException("its inventory has no contents");
            }
            Collect(rootGroup, 0, Add);
        }
        else if (Component(stage, "ResourceNodePickupDrops") is JsonElement node)
        {
            JsonElement[] drops = node.TryGetProperty("drops", out JsonElement d) ? d.EnumerateArray().ToArray() : Array.Empty<JsonElement>();
            if (drops.Length != 1)
            {
                throw new UnsupportedHarvestException($"{drops.Length} weighted random drops");
            }
            Guid? item = Reference(drops[0], "item");
            if (item == null && Reference(drops[0], "entity") is Guid pickupGuid
                && read($"resource:{pickupGuid:D}/{TemplateType}/0") is GameRecord pickup
                && Component(pickup.Value, "PickupItem") is JsonElement pickupItem)
            {
                item = Reference(pickupItem, "item");
            }
            long stack = Int(drops[0], "stackAmount");
            long times = Math.Max(1, Int(node, "numberOfDrops"));
            Add(item ?? throw new UnsupportedHarvestException("its drop's item could not be read"), stack * times);
        }
        else
        {
            throw new UnsupportedHarvestException("no inventory or pickup drop");
        }
        if (order.Count == 0)
        {
            throw new UnsupportedHarvestException("its harvest is empty");
        }
        if (order.Count > 2)
        {
            throw new UnsupportedHarvestException($"{order.Count} kinds of produce, at most 2 are supported");
        }
        return order.Select(item => (item, counts[item])).ToList();
    }

    private static void Collect(JsonElement group, int depth, Action<Guid, long> add)
    {
        if (depth > 12)
        {
            throw new UnsupportedHarvestException("its inventory is nested too deep");
        }
        if (group.TryGetProperty("isSelector", out JsonElement selector) && selector.ValueKind == JsonValueKind.True)
        {
            throw new UnsupportedHarvestException("its inventory picks at random");
        }
        if (group.TryGetProperty("lootLabels", out JsonElement labels)
            && (Count(labels, "labels") > 0 || Count(labels, "childGroups") > 0))
        {
            throw new UnsupportedHarvestException("its inventory uses loot labels");
        }
        if (group.TryGetProperty("stacks", out JsonElement stacks))
        {
            foreach (JsonElement stack in stacks.EnumerateArray())
            {
                Guid item = Reference(stack, "itemRef") ?? throw new UnsupportedHarvestException("a stack has no item");
                if (!stack.TryGetProperty("spawnProbability", out JsonElement probability) || probability.GetDouble() != 1.0)
                {
                    throw new UnsupportedHarvestException("a stack is not certain");
                }
                add(item, Math.Max(Int(stack, "countMin"), Int(stack, "countMax")));
            }
        }
        if (group.TryGetProperty("groups", out JsonElement groups))
        {
            foreach (JsonElement child in groups.EnumerateArray())
            {
                Collect(child, depth + 1, add);
            }
        }
    }

    /// <summary>Hides the ripe model while the inventory is empty: the chest's empty-state toggle, read inverted by the model.</summary>
    private static void AddEmptyToggle(TemplatePatch patch, GameRecord? chest, GameRecord? invertToggle)
    {
        if (chest == null || invertToggle == null)
        {
            return;
        }
        foreach (string part in EmptyToggleParts)
        {
            patch.CopyComponents[part] = chest.ResourceId;
        }
        patch.CopyComponents["ModelRenderHint"] = invertToggle.ResourceId;
        patch.Set["InventoryEmptyState.toggleVFX"] = false;
        patch.Set["InventoryEmptyState.isEmpty"] = false;
        patch.Set["ClientToggle.state"] = false;
        patch.Set["ClientToggleState.state"] = false;
    }

    public static JsonElement? Component(JsonElement template, string type)
    {
        if (!template.TryGetProperty("components", out JsonElement components) || components.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        foreach (JsonElement component in components.EnumerateArray())
        {
            if (component.TryGetProperty("$type", out JsonElement t) && t.GetString() == type)
            {
                return component;
            }
        }
        return null;
    }

    /// <summary>A reference member as a GUID; null for none.</summary>
    public static Guid? Reference(JsonElement element, string member)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(member, out JsonElement value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        return Guid.TryParse(value.GetString(), out Guid guid) && guid != Guid.Empty ? guid : null;
    }

    /// <summary>The staged name of a patch: Kiln wants ^[a-z][a-z0-9_.-]{0,95}$.</summary>
    public static string KeyFor(string kind, string templateName)
    {
        string key = kind + "-" + KeyUnsafe.Replace(templateName.ToLowerInvariant(), "-").Trim('-');
        return key.Length > 96 ? key.Substring(0, 96) : key;
    }

    /// <summary>An item's debug name for the log (Food_Tomato), else its GUID.</summary>
    public static string ItemName(Func<string, GameRecord?> read, Guid item)
    {
        GameRecord? info = read($"resource:{item:D}/{ItemType}/0");
        return info != null && info.Value.TryGetProperty("debugName", out JsonElement name) && name.GetString() is string text && text.Length > 0 ? text : item.ToString("D");
    }

    public static long Nanoseconds(double seconds) => (long)Math.Round(seconds * NanosecondsPerSecond);

    private static GameRecord Require(Func<string, GameRecord?> read, string templateName)
    {
        return read("template:" + templateName) ?? throw new InvalidOperationException($"the game's perennial {templateName} could not be read");
    }

    private static void RequireComponent(GameRecord record, string templateName, string type)
    {
        if (Component(record.Value, type) == null)
        {
            throw new InvalidOperationException($"the game's perennial {templateName} has no {type} any more");
        }
    }

    private static long Int(JsonElement element, string member)
    {
        return element.TryGetProperty(member, out JsonElement value) && value.ValueKind == JsonValueKind.Number ? (long)Math.Round(value.GetDouble()) : 0;
    }

    private static int Count(JsonElement element, string member)
    {
        return element.TryGetProperty(member, out JsonElement value) && value.ValueKind == JsonValueKind.Array ? value.GetArrayLength() : 0;
    }

    /// <summary>A time member of the snapshot (seconds), or 0.</summary>
    private static double NativeSeconds(JsonElement element, string member)
    {
        return element.TryGetProperty(member, out JsonElement value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : 0;
    }

    private static string Seconds(double seconds) => seconds.ToString("0.#", CultureInfo.InvariantCulture) + " s";
}

/// <summary>A crop whose harvest a fixed refill can't repeat (random drops, loot labels): it stays one-shot.</summary>
public sealed class UnsupportedHarvestException : Exception
{
    public UnsupportedHarvestException(string message) : base(message)
    {
    }
}
