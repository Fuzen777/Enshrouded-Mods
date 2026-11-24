# Perennial Crops

A Kiln mod (id `perennial-crops`, C#, `src\`) that makes every planted crop keep producing after harvest, the way the game makes its own perennials. It replaces Eternal Harvest 0.3.0 (a Shroudforge asset mod) with Kiln's own tools.

`Scripts\Build.ps1` runs the planner tests, builds against Kiln's assemblies in `<game>\kiln\host` and puts the mod in `dist\perennial-crops`; `-Install` copies it to `<game>\kiln\mods\perennial-crops`, where Kiln loads it as a drop-in (game closed).

## How The Game Makes A Perennial

There is no perennial flag. A planted crop is a growth root (`T<tier>_Plant_...`, with `Growth` and `Upgrade`) whose last `Upgrade` level points at the ripe stage's template. The game's five perennials (banana, coconut, coffee, date palm, hazelnut) ripen into a `..._PlayerPlanted_Refill` template whose components differ from a one-shot crop's ripe stage:

| Perennial Ripe Stage Has | One-Shot Ripe Stage Has |
|---|---|
| `InteractionLootRefill` (wait time, verb, sequence, items with refill and max counts) | `DestroyOnLoot` |
| `InteractionLock`, `ClientInteractionLock`, `InventorySetup`, `InventorySetupInit`, `InventorySetupOverwrite`, `InventoryCraftingStock` | `DefaultInventory` or a single `ResourceNodePickupDrops` drop |

The 64 planted growth roots of game build 26430625: 5 perennials, 40 one-shot crops, 19 trees and bushes that are felled or chopped. 17 of the one-shot crops ripen into a `Scatter_Loot_...` template that wild plants share (cattails, reed, pumpkin, yellow fog mushroom, the wetland and Albaneve crops).

---

## 1.0.0

- Every one-shot planted crop with a fixed harvest (39 of 40) becomes perennial: its ripe stage loses `DestroyOnLoot` and gets the hazelnut bush's refill parts, copied whole (Kiln resource patches, `copyComponents`), with the banana plant's refill for the three crops with two kinds of produce (plant fibre, wheat, algae). Each regrowth gives the crop's full harvest (the highest count where it varies). The crop keeps its own inventory for the first harvest.
- Artichoke stays one-shot: its harvest is four weighted random drops, which a fixed refill can't repeat.
- The game's five perennials get the regrowth time too.
- Settings (kiln config `perennial-crops.json`, read before the game starts, so a change applies at the next start): Mod Enabled (on), Growth Time (600 s, for all 64 growth roots, trees included, with the game's random stretch removed), Regrowth Time (600 s), Hide While Regrowing (on: the ripe model hides until the crop has produce again, through the chest's empty-inventory toggle and an inverted model toggle copied from the game's templates).
- The refill's produce is a reference, which resource patches cannot set: the patches copy the hazelnut's or banana's slots, and the running mod points each slot at the crop's own produce through Kiln's game data when the world's content is ready.
- Changed in place: the ripe stages that wild plants share are changed too, so wild crops of those kinds refill instead of disappearing. A private copy per planted crop needs Kiln to copy components onto a template clone, which it can't yet (requested).
- `tests\CropPlannerTests` runs the planner on Kiln's offline snapshot of the game's data (`snapshot_reader.py` answers the same queries `OnBoot` makes) and checks the counts against Eternal Harvest's report on the same build (64 roots, 44 refilling, 20 growth only), the tomato, wheat, cabbage, red mushroom and coffee changes, growth per stage, Kiln's limits (staged names, one patch per template, 4,096 changes) and both settings of Hide While Regrowing.

> [!note]
> Not yet checked in game.
