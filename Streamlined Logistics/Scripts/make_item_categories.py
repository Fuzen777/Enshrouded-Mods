"""Writes data\\item_categories.json: where Streamlined Logistics files each item in its category tree, and in which order.

Run it after a game update, once the workspace's dump is made for the new build:

    py -3.14 make_item_categories.py [--build 26430625]

Everything is read from the game's data, not from item names where the data can say it:
- the item's game category (`category`: Weapons, Equipment, Tools, Consumables, Ammunition, Materials, BuildTools...);
- its equipment slot (`equipment.slot`: Armour_Head ... Armour_Ring, Shield, Backpack, MeleeWeapon, RangeWeapon, MidRangeWeapon
  for throwables, Spell, Arrow, FishingBait, TerrainMaterial, BlueprintMaterial_*);
- for a consumable, the sequence its use plays (UseItem_Food... is food or drink, the rest are potions, bandages, scrolls);
- for a placeable, the components of the object it places (`equipment.placedEntity`): a crafting station or machine, something
  that works without crafting (a well or water barrel, a grappling anchor, a ladder, a water barrier), storage, a seat or a bed,
  a door, a showcase, an animal.
Keys carry the same item tag as lockpicks; some keys are filed by the game as materials.

Materials are left to the player's hand-made category list (chestsort_categories.json): they are written as "Materials" and
the mod picks their sub-category at run time. Inside a category, items are ordered by a kind (an armour slot, a weapon type,
a bed or a seat, a potion's purpose...), then their tier, then their name, so that like items sit together.
"""

import argparse
import json
import os
import re
import sys

WORKSPACE_DUMP = r"D:\Dev\Enshrouded\Dump"
NO_GUID = "00000000-0000-0000-0000-000000000000"
KEY_TAG = "20716a18-10ab-40cd-a7a0-054be8ea1bd9"

# The tree, in the order the screen shows it. Materials come last; their sub-categories are the player's list.
SECTIONS = [
    "Gear/Weapons", "Gear/Shields", "Gear/Armor", "Gear/Cosmetics", "Gear/Rings", "Gear/Tools",
    "Consumables/Meals", "Consumables/Raw Food", "Consumables/Potions & General Consumables", "Consumables/Ammo", "Consumables/Spells",
    "Placeables/Stations", "Placeables/Functional", "Placeables/Storage", "Placeables/Furniture", "Placeables/Doors", "Placeables/Showcases",
    "Placeables/Animals & Pets", "Placeables/Decor",
    "Building/Blocks", "Building/Terrain",
    "Other/Weapon Gems", "Other/Lore", "Other/Dyes", "Other/Miscellaneous",
    "Materials",
]

ARMOUR_ORDER = {"Armour_Head": 1, "Armour_UpperBody": 2, "Armour_Arms": 3, "Armour_LowerBody": 4, "Armour_Feet": 5}
# The game's weapon types (ItemInfo.weaponCategoryReference), named from the weapons that carry them, in the screen's order.
WEAPON_TYPES = {
    "d52db28f-dcba-4319-b4dc-24630fe53cd0": "01 Sword",
    "3d6f5e19-3778-4c11-9565-a85a006fe916": "02 Greatsword",
    "0eb3ab88-f305-41f9-bb15-a9c90b10672f": "03 Axe",
    "b2ee33c7-f1ee-4e5c-b430-1ecc15000d26": "04 Greataxe",
    "9dbc667c-83a9-4a1b-98e0-2f177e101b55": "05 Mace",
    "fcd49b88-c31f-4368-b771-d52293c7f1d7": "06 Hammer",
    "4806aefc-6261-49d9-92f1-a3387ed4712c": "06 Hammer",
    "2a0af7d9-0baa-45fd-bfc1-6b39906b3ee8": "07 Daggers",
    "8ed1ee4c-b762-4d3b-b4d4-adf12013dd6a": "08 Bow",
    "15ab545f-a83d-4a59-b3d5-a65b0fba0614": "09 Staff",
    "13ebbbb7-9a61-4ed8-865d-d1954a52a19c": "10 Wand",
}
THROWABLE_TYPE = "9af5aad2-bff6-4b5a-885d-546fee56b343"   # bombs and grenades: ammunition, not weapons
TORCH_TYPE = "d607e356-f17c-450a-aac1-dbc0bd726069"       # torches (and the water hose): tools
FISTS_TYPE = "1fb46153-86da-4098-83c1-c4b03cd916af"
TOOL_KINDS = ["Pickaxe", "Axe", "Glider", "FishingRod", "Instrument", "Drum", "Flute", "Lute", "Key", "Lockpick", "Backpack", "Camera", "Grapple"]
POTION_KINDS = ["Health", "Mana", "Stamina", "Bandage", "Antidote", "Lotion", "Flask", "Scroll", "RepairKit"]
SPELL_KINDS = ["Fire", "Ice", "Lightning", "Fog", "Shroud", "Heal", "Skeleton", "Summon", "Acid", "Poison"]
DECOR_KINDS = ["Bed", "Chair", "Stool", "Bench", "Throne", "Sofa", "Couch", "Table", "Desk", "Shelf", "Cabinet", "Wardrobe", "Drawer",
               "Chandelier", "Lamp", "Lantern", "Candle", "Torch", "Brazier", "Banner", "Flag", "Rug", "Carpet", "Curtain", "Pillow",
               "Plant", "Flower", "Pot", "Vase", "Basket", "Barrel", "Crate", "Box", "Statue", "Bust", "Trophy", "Painting", "Picture",
               "Mirror", "Clock", "Book", "Fence", "Window", "Pillar", "Column", "Shrine", "Sign"]


def newest_build(dump_root):
    builds = sorted(name for name in os.listdir(dump_root) if name.isdigit())
    if not builds:
        sys.exit(f"no build folder in {dump_root}")
    return builds[-1]


def read_index(dump):
    index = {}
    with open(os.path.join(dump, "index.tsv"), encoding="utf-8") as lines:
        next(lines)
        for line in lines:
            guid, resource_type, _part, _size, name, file_name = line.rstrip("\n").split("\t")
            index[guid] = (resource_type, name, file_name)
    return index


def tier(name):
    match = re.search(r"(?:^|_)T(\d{1,2})(?:_|$|-)", name) or re.search(r"lvl(\d{1,3})", name)
    return int(match.group(1)) if match else 99


# The tools in the screen's order (since 0.10.20): gathering tools together, then farming, building, fishing, travel, the rest.
# Matched in this order, by a part of the name (Pickaxe before Axe).
TOOL_ORDER = [("Pickaxe", 2), ("Axe", 1), ("Sieve", 3), ("Rake", 4), ("WateringCan", 5), ("Hose", 6), ("Quickbuilder", 7),
              ("Building", 8), ("FishingRod", 9), ("FishingBait", 10), ("Glider", 11), ("Grappling", 12), ("Grapple", 12),
              ("ClimbingClaws", 13), ("Torch", 14), ("Lockpick", 15), ("Key", 16), ("Camera", 17), ("Backpack", 18),
              ("Instrument", 19), ("Drum", 19), ("Flute", 19), ("Lute", 19)]


def tool_kind(name):
    low = name.lower()
    for pattern, order in TOOL_ORDER:
        if pattern.lower() in low:
            return f"{order:02d} {pattern}"
    return "99 ~"


def kind_in(name, kinds):
    tokens = name.replace("-", "_").split("_")
    for kind in kinds:
        if kind in tokens or any(token.startswith(kind) for token in tokens):
            return kind
    return "~"


def component_names(template_json_text):
    return set(re.findall(r'"keen::ecs::([A-Za-z]+)"', template_json_text))


def placeable(components, name):
    """The placeables' sub-category and kind, from what the placed object is made of."""
    if "Animal" in components:
        return "Placeables/Animals & Pets", "Animal"
    if components & {"CraftingInteraction", "FactoryStation", "CraftingStation", "RotationalEnergySource", "WaterDispenser", "WaterDrain"}:
        if "NPC_" in name and "Prop_NPC" not in name:
            return "Other/Miscellaneous", "NPC"
        return "Placeables/Stations", "Factory" if "FactoryStation" in components else "Workbench"
    # What works without crafting (asked by the user on 24 Nov 2026): wells and water barrels (water as crafting stock), grappling
    # hook anchors, ladders and scaffolds, water barriers (they hold water back but don't open, unlike doors and water gates).
    if "FuelCraftingStock" in components:
        return "Placeables/Functional", "1 Water"
    if "HookShotAnchor" in components:
        return "Placeables/Functional", "2 Grappling Anchor"
    if "Ladder" in components:
        return "Placeables/Functional", "3 Ladder"
    if "BlockWater" in components and "InteractionToggle" not in components:
        return "Placeables/Functional", "4 Water Barrier"
    # Pets use many decorations (a bucket, a fireplace, a garden gnome, a pumpkin carry the game's AnimalTarget too): only the
    # game's Animal_ items are animal furniture, the feeders and the pet and farm animal beds. 0.9.0 to 0.10.35 filed every
    # AnimalTarget object there.
    if "AnimalTarget" in components and name.startswith("Animal_"):
        return "Placeables/Animals & Pets", "Animal Furniture"
    if "ItemCollectionComponent" in components:
        return "Placeables/Showcases", "Showcase"
    if "InventorySetup" in components or name.startswith("Prop_Storage_Loot_Drop_Station"):
        return "Placeables/Storage", "Magic" if "InventoryCraftingStock" in components else "Chest"
    if components & {"NpcBed", "InteractionAttachment"}:
        return "Placeables/Furniture", "Bed" if ("NpcBed" in components or "Bed" in name) else "Seat"
    if "InteractionToggle" in components and components & {"BlockWater", "DirectionalInteraction"}:
        return "Placeables/Doors", kind_in(name, ["Gate", "Door", "Hatch"])
    return "Placeables/Decor", kind_in(name, DECOR_KINDS)


def crafted_items(index, dump):
    """The items a recipe makes: a food made by one is a meal, the others are raw food."""
    made = set()
    for guid, (resource_type, _name, file_name) in index.items():
        if resource_type != "keen::RecipeRegistryResource":
            continue
        with open(os.path.join(dump, file_name), encoding="utf-8") as source:
            for recipe in json.load(source).get("recipes", []):
                for output in recipe.get("output", []):
                    made.add(output["item"]["value"])
    return made


CRAFTED = set()


def classify(item, index, dump, template_cache):
    """(section, kind) of one item, or None for what never sits in an inventory."""
    name = item["debugName"]
    category = item.get("category")
    equipment = item.get("equipment") or {}
    slot = equipment.get("slot", "Invalid")
    tags = json.dumps(item.get("tags", []))

    if KEY_TAG in tags or name.startswith("Key_") or name.startswith("Lockpick"):
        return "Gear/Tools", tool_kind("Key" if "Key" in name else "Lockpick")
    if name.lstrip("_").startswith("Tool_") and category in ("BuildTools", "Blueprints"):
        # The Construction Hammer (category Blueprints) and the building tools: tools, not blueprints or placeables.
        return "Gear/Tools", tool_kind(name)
    if category == "Weapons":
        weapon_type = item.get("weaponCategoryReference", NO_GUID)
        if weapon_type == THROWABLE_TYPE:
            return "Consumables/Ammo", "Throwable"
        if weapon_type == TORCH_TYPE:
            return "Gear/Tools", tool_kind("Torch")
        if weapon_type == FISTS_TYPE or weapon_type == NO_GUID:
            return None
        return "Gear/Weapons", WEAPON_TYPES.get(weapon_type, "99 Other")
    if category == "Equipment":
        if slot == "Armour_Ring":
            return "Gear/Rings", "Ring"
        if slot in ARMOUR_ORDER:
            # Vanity pieces only change the look (worn over armour): Cosmetics, asked by the user on 22 Nov 2026.
            return ("Gear/Cosmetics" if name.startswith("Vanity") else "Gear/Armor"), str(ARMOUR_ORDER[slot])
        if slot == "Shield":
            return "Gear/Shields", "Ward" if "Ward" in name else "Shield"
        if slot == "Backpack":
            return "Gear/Tools", tool_kind("Backpack")
        return None
    if category in ("Tools", "FishingRod", "Instrument"):
        return "Gear/Tools", tool_kind(name if category == "Tools" else category)
    if category == "Ammunition":
        if slot == "Spell":
            return "Consumables/Spells", ("0 Eternal " if "Eternal" in name else "1 ") + kind_in(name, SPELL_KINDS)
        if slot == "FishingBait":
            return "Gear/Tools", tool_kind("FishingBait")   # right after the fishing rods
        return "Consumables/Ammo", "Arrow"
    if category == "Consumables":
        # The game names its flasks, potions, bandages and scrolls Consumable_*: those are potions and general consumables, even
        # the flasks that fill a food slot. Food is a meal when a recipe makes it (and it is not named raw), else raw food.
        # Only food (named Food, or eaten through the game's food sequences) is a meal or raw food; 0.10.3 to 0.10.14 also put
        # the "_Consumable_Potion_*" potions (a leading underscore) and the water pouch there.
        bare = name.lstrip("_")
        sequence_names = [index.get(entry.get("sequence"), ("", ""))[1] for entry in item.get("sequences", []) if entry.get("triggeredBy") == "Primary"]
        is_food = bare.startswith("Food") or any(n.startswith("UseItem_Food") or "Fish" in n for n in sequence_names)
        if bare.startswith("Consumable_") or bare.startswith("Tool_") or not is_food:
            return "Consumables/Potions & General Consumables", kind_in(name, POTION_KINDS)
        if item["itemId"]["value"] in CRAFTED and "_raw_" not in name:
            return "Consumables/Meals", "Meal"
        return "Consumables/Raw Food", "Raw"
    if category == "AnimalFood":
        return "Placeables/Animals & Pets", "Bait"
    if category == "BuildTools":
        # Pickaxes, sieves, rakes, the Construction Hammer and the building tools are the game's BuildTools too: tools, not
        # placeables (0.9.0 to 0.10.19 filed them in Placeables > Decor).
        if name.lstrip("_").startswith("Tool_"):
            return "Gear/Tools", tool_kind(name)
        if slot.startswith("BlueprintMaterial") or slot == "OvergrowthMaterial":
            return "Building/Blocks", slot
        if slot == "BuildTool":
            template = equipment.get("placedEntity", NO_GUID)
            if template == NO_GUID or template not in index:
                return "Placeables/Decor", kind_in(name, DECOR_KINDS)
            if template not in template_cache:
                with open(os.path.join(dump, index[template][2]), encoding="utf-8") as source:
                    template_cache[template] = component_names(source.read())
            return placeable(template_cache[template], name)
        if name.startswith("Collection_"):
            return "Other/Lore", "Collection"
        return None
    if category == "Materials":
        if slot == "TerrainMaterial":
            return "Building/Terrain", "Terrain"
        if name.startswith("Ability_"):
            return None
        return "Materials", ""
    if category == "WeaponGem":
        return "Other/Weapon Gems", "Gem"
    if category == "Collectibles":
        return "Other/Lore", "Lore"
    if category == "ColorPalette":
        return "Other/Dyes", "Dye"
    if category == "Currency":
        return "Other/Miscellaneous", category
    return None


def salvage_balancing(index, dump):
    """The game's Trash yield of gear in runes (keen::BalancingTable), for the mod's salvage rules (since 0.10.26)."""
    for guid, (resource_type, _name, file_name) in index.items():
        if resource_type != "keen::BalancingTable":
            continue
        with open(os.path.join(dump, file_name), encoding="utf-8") as source:
            table = json.load(source)
        rune = table["perkCostItem"]
        rune_file = next(name for name in os.listdir(os.path.join(dump, "ItemInfo")) if name.endswith("__" + rune + ".json"))
        with open(os.path.join(dump, "ItemInfo", rune_file), encoding="utf-8") as source:
            rune_item = json.load(source)["itemId"]["value"]
        return {"seed": table["perkUpgradeCostSeed"], "creep": table["perkCostCreep"], "trashStart": table["trashStart"],
                "trashEnd": table["trashEnd"], "weaponsShield": table["perkCostFactorWeaponsShield"],
                "armor": table["perkCostFactorArmor"], "rings": table["perkCostFactorRings"], "runeItem": rune_item}
    raise SystemExit("no keen::BalancingTable in the dump")


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--build", help="the Steam build id of the dump to read (default: the newest one)")
    arguments = parser.parse_args()
    build = arguments.build or newest_build(WORKSPACE_DUMP)
    dump = os.path.join(WORKSPACE_DUMP, build)
    index = read_index(dump)

    template_cache = {}
    CRAFTED.update(crafted_items(index, dump))
    rows = []
    counts = {}
    folder = os.path.join(dump, "ItemInfo")
    for file_name in sorted(os.listdir(folder)):
        with open(os.path.join(folder, file_name), encoding="utf-8") as source:
            item = json.load(source)
        placed = classify(item, index, dump, template_cache)
        if placed is None:
            continue
        section, kind = placed
        name = item["debugName"]
        order = f"{kind}|{tier(name):02d}|{name}"
        rows.append([item["itemId"]["value"], SECTIONS.index(section), order])
        counts[section] = counts.get(section, 0) + 1

    rows.sort(key=lambda row: row[0])
    salvage = salvage_balancing(index, dump)
    target = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "data", "item_categories.json"))
    with open(target, "w", encoding="utf-8", newline="\n") as output:
        output.write('{\n "build": "' + build + '",\n "sections": ' + json.dumps(SECTIONS) + ',\n "items": [\n')
        output.write(",\n".join("  " + json.dumps(row, separators=(",", ":")) for row in rows))
        output.write("\n ],\n \"salvage\": " + json.dumps(salvage) + "\n}\n")
    print(f"{len(rows)} items of build {build} written to {target}")
    for section in SECTIONS:
        print(f"  {counts.get(section, 0):5}  {section}")


if __name__ == "__main__":
    main()
