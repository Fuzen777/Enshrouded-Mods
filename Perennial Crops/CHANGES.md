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
> First start in game on 24 Nov 2026: the boot generation committed the mod (108 templates patched, 0 issues) and the running mod set the produce on 42 slots of 39 crops at each world load. Ripe Eternal Harvest crops in the save came back as the game's own crops.

---

## 1.0.1

- Sync Growth Time (new, off by default): off, every plant keeps the game's own growth time; on, all 64 growth roots take Growth Time, as 1.0.0 always did.
- Area harvest, merged from Area Harvest for Eternal Harvest (retired; its crop rule only knew Eternal Harvest's copies, which no longer exist): while a world is hosted, harvesting a perennial crop by hand also harvests the ripe ones of the same kind (the same ripe stage template) in a square around it, through the game's own loot request for that player (`Gameplay.Inject.Loot`). Harvest Any Crop takes every ripe perennial crop in the square instead. Regrowing crops (empty inventory) and crops with no harvest offered are skipped. The perennial ripe stages come from the plan OnBoot writes (44 of them; artichoke stays out). Its settings are in `perennial-crops-server.json`: Area Harvest (on), Harvest Any Crop (off), Harvest Area Size (30 m, 1 to 100).
- A settings window, Shift+F6 (a Kiln key binding, so it can be changed on Kiln's Keys page): the crop settings (marked Restart The Game To Apply when they differ from the ones this start was made with) and the area harvest settings, which apply at once.
- The plan in the mod's data folder is now `plan.json`: the settings OnBoot used, the refill produce and the perennial ripe stages.
- Tests: Sync Growth Time off (no growth root patched, the same 39 + 5 crops), the 44 area harvest stages, same kind against Harvest Any Crop, growing stages never taken, the area limits and the square's edges.

> [!note]
> In game on 24 Nov 2026, area harvest never ran: `area harvest can't run here: queries.overlap_origins (crops in an area) not available`. The mod checked Kiln's services in `OnLoad`, which for a both-sides mod runs in the main menu, before the hosted world's services are up.

---

## 1.0.2

- Area harvest checks Kiln's services at a hand harvest in a hosted world instead of at load, asks again at the next harvest while one is missing (saying so once per world), and logs `area harvest ready: 44 perennial crop kinds` once it can run.
