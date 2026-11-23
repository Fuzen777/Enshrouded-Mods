using System;
using System.Collections.Generic;
using EhAreaHarvest;

// Checks which template names the mod harvests: Eternal Harvest's ripe planted crops and the game's planted fruit trees, and none of the crops
// XHL Planting Assistant (Kiln) 0.1.0 harvests itself (its crop families' harvest stages, from its AreaData), so no crop is harvested twice.
int failures = 0;
void Check(bool condition, string what)
{
    if (!condition)
    {
        failures++;
        Console.WriteLine("FAIL: " + what);
    }
}

// Eternal Harvest 0.3.0's report names 44 planted crops; the mushrooms, coconut and banana among them, plus two Planting Assistant has no family for.
string[] eternalHarvest =
{
    "EternalHarvest_Planted_T1_Plant_Growth_Mushrooms_Red",
    "EternalHarvest_Planted_T2_Plant_Growth_AzureRussula_GlowShroom",
    "EternalHarvest_Planted_T6_Plant_Growth_Shiitake",
    "EternalHarvest_Planted_T5_Plant_YellowFogMushroom",
    "EternalHarvest_Planted_T6_Plant_Growth_Coconut",
    "EternalHarvest_Planted_T6_Plant_Growth_Banana",
    "EternalHarvest_Planted_T7_Plant_Growth_Cabbage",
    "EternalHarvest_Planted_T3_Plant_Growth_Flax",
};
foreach (string name in eternalHarvest)
{
    Check(CropRules.IsCovered(name), name + " is harvested");
}

// The game's planted fruit trees: Planting Assistant lists them as families but never harvests them (not on its whitelist).
string[] plantedFruitTrees =
{
    "Scatter_Loot_Wetland_Above_Bananaplant_01_e_fruits_harvest_PlayerPlanted_Refill",
    "Scatter_Loot_Wetland_Coffeetree_d_fruits_harvest_Playerplanted_Refill",
    "Scatter_Loot_DeepWoods_Hazelnut_d_nuts_harvest_Playerplanted_Refill",
    "Scatter_Loot_Desert_Above_Datepalm_01_e_fruits_harvest_PlayerPlanted_Refill",
    "Scatter_Loot_Wetland_Above_Coconutpalm_01_e_fruits_harvest_PlayerPlanted_Refill",
};
foreach (string name in plantedFruitTrees)
{
    Check(CropRules.IsCovered(name), name + " is harvested");
}

// Planting Assistant's own crops (its 37 destroy families' harvest stages): left to it.
string[] plantingAssistant =
{
    "T3_Plant_Growth_SugarCane_Seedling_2_harvest", "T2_Plant_Growth_Beets_Seedling_2_harvest", "T2_Plant_Growth_Strawberry_Seedling_2_harvest",
    "T2_Plant_Growth_Nettle_Seedling_2_harvest", "T3_Plant_Growth_Pepper_Seedling_2_harvest", "T4_Plant_Growth_Saffron_Seedling_2_harvest",
    "T1_Plant_Growth_Mushroom_2_harvest", "T2_Plant_Growth_Indigo_Seedling_2_harvest", "T3_Plant_Growth_Flax_Seedling_2_harvest",
    "T1_Plant_Growth_Tomato_Seedling_2_harvest", "T4_Plant_Growth_DevilsClaw_Seedling_2_harvest", "T3_Plant_Growth_Sage_Seedling_2_harvest",
    "T1_Plant_Growth_Corn_2_harvest", "T1_Plant_Growth_St_Johns_Worth_Seedling_2_harvest", "T2_Plant_Growth_BerryBush_3_harvest",
    "T2_Plant_Growth_AzureRussula_GlowShroom_Seedling_2_harvest", "T5_Plant_Growth_Tentacle_ShroudAdhesive_3_harvest_animated",
    "T2_Plant_Growth_Chamomille_Seedling_2_harvest", "T1_Plant_Growth_PlantFibre_Bush_Seedling_3_harvest",
    "T4_Plant_Growth_Desert_Flower_Seedling_2_harvest", "T5_Plant_Growth_Wolfsbane_2_harvest", "T3_Plant_Growth_Rooibos_Seedling_2_harvest",
    "T5_Plant_Growth_Yellow_Gentian_2_harvest", "T6_Plant_Growth_Algae_Seedling_1", "T6_Plant_Growth_Rice_01_d_Harvest",
    "Scatter_Loot_Grassland_Reed_01_growth_a", "Scatter_Loot_Wetland_Shiitake_01_a", "Scatter_Loot_Wetland_OrangeDaylily_01_a",
    "Scatter_Loot_Wetland_Ginger_01_c_Harvest", "Scatter_Loot_Wetland_Taro_a_Harvest", "Scatter_Loot_AncientLand_Plant_Thyme_01_d",
    "Scatter_Loot_AncientLand_Plant_Bean_01_d", "Scatter_Loot_AncientLand_Plant_Pomegranate_01_d", "Scatter_Loot_coldheights_Pumpkin_01a",
    "Scatter_Loot_Grassland_Cattails_01_growth_a", "Scatter_Loot_Wetland_Papyrus_01_01_a_1", "Plant_Wheat_Grown_Step03_Harvest",
};
Check(plantingAssistant.Length == 37, "37 Planting Assistant crops listed");
foreach (string name in plantingAssistant)
{
    Check(!CropRules.IsCovered(name), name + " is left to Planting Assistant");
}

// Growing crops, wild plants and anything else.
var others = new List<string?>
{
    "T6_Plant_Growth_Banana", "T6_Plant_Growth_Coconut", "Scatter_Loot_Wetland_Above_Coconutpalm_01_e_fruits_harvest_Refill",
    "eternalharvest_planted_T6_Plant_Growth_Banana", "PlayerPlanted_Refill_Something", "", null,
};
foreach (string? name in others)
{
    Check(!CropRules.IsCovered(name), (name ?? "null") + " is not harvested");
}

// The area setting: 1 to 100 m, the default when it isn't a number.
Check(CropRules.AreaMeters(30) == 30, "30 m stays 30");
Check(CropRules.AreaMeters(0.5) == 1, "0.5 m becomes 1");
Check(CropRules.AreaMeters(250) == 100, "250 m becomes 100");
Check(CropRules.AreaMeters(double.NaN) == CropRules.DefaultAreaMeters, "NaN becomes the default");
Check(CropRules.AreaMeters(double.PositiveInfinity) == CropRules.DefaultAreaMeters, "infinity becomes the default");

// The square around the harvested crop, edges included, any height.
Check(CropRules.InSquare(100, -40, 115, -25, 30), "corner of a 30 m square is inside");
Check(CropRules.InSquare(100, -40, 85, -55, 30), "opposite corner is inside");
Check(!CropRules.InSquare(100, -40, 115.01, -40, 30), "just past the east edge is outside");
Check(!CropRules.InSquare(100, -40, 100, -55.01, 30), "just past the south edge is outside");
Check(CropRules.InSquare(0, 0, 0.5, -0.5, 1), "1 m square holds 0.5 m off centre");

Console.WriteLine(failures == 0 ? "OK" : failures + " failure(s)");
return failures == 0 ? 0 : 1;
