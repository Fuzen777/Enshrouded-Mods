# Area Harvest for Eternal Harvest

> [!warning]
> Retired on 24 Nov 2026: Eternal Harvest was replaced by Perennial Crops, whose 1.0.1 has this area harvest built in (same kind by default, Harvest Any Crop for every kind). This mod only knew Eternal Harvest's crop copies, so it no longer finds any crop.

A Kiln mod (id `eh-area-harvest`, C#, `src\`) that brings XHL Planting Assistant's range harvest to the crops it leaves out. Planting Assistant (Kiln edition 0.1.0, Nexus 128) harvests the crops around one you harvest by hand, but only crops whose game template is on its fixed list of 72. Eternal Harvest 0.3.0 makes each planted crop ripen into a copy with a template of its own (`EternalHarvest_Planted_<growth root>`, a new GUID), and the game's planted fruit trees (`..._PlayerPlanted_Refill`: banana, coconut, coffee, hazelnut, date palm) were never on the list, so Planting Assistant chain-harvests none of them.

`Scripts\Build.ps1` runs the tests, builds against Kiln's assemblies in `<game>\kiln\host` and puts the mod in `dist\eh-area-harvest`; `-Install` copies it to `<game>\kiln\mods\eh-area-harvest`, where Kiln loads it as a drop-in (game closed).

## 1.0.0

- Harvesting one Eternal Harvest crop or planted fruit tree by hand also harvests every other ripe one in the square around it, whatever the kind: mushrooms, coconuts, bananas and the other 41 Eternal Harvest crops, including Yellow Fog Mushroom and Cabbage, which Planting Assistant has no entry for. Planted fruit trees from before Eternal Harvest (still the game's own templates) are covered too.
- Crops are recognized by template name, so crops Eternal Harvest adds later are covered without an update. Planting Assistant's own crops never match, so no crop is harvested by both.
- Each crop gets the game's own loot request for the player who harvested (`Gameplay.Inject.Loot`, the request Planting Assistant sends), so the game hands out the produce as for a manual harvest. Crops that are regrowing (empty) or offer no harvest are skipped.
- Its own settings, in `%LOCALAPPDATA%\Kiln\profiles\my-mods\config\eh-area-harvest-server.json` (Kiln reloads it when saved): Range Harvesting on/off (on) and Harvest Area Size, 1 to 100 m (30 m).
- Side "server": Kiln loads it while your game hosts the world (or on a dedicated server); players who join don't need it.
- `tests\CropRulesTests` checks the names it harvests: Eternal Harvest's crops and the planted fruit trees, and none of Planting Assistant's 37 crops, growing crops or wild fruit trees; plus the area limits and the square's edges.

> [!note]
> Checked in game on 19 Nov 2026: Kiln loaded it while hosting (`[mod:eh-area-harvest] loaded 1.0.0`, 0 faults). Harvesting one crop by hand sent the ripe ones around it, and the game harvested them: Corn 109, Yellow Fog Mushroom 181 then 31 (137 regrowing skipped), Chamomile 36, Wheat 53. The game declined none. Planted fruit trees (banana, coconut) not tried yet.
>
> In `kiln\logs\kiln.log`, each hand harvest logs "player … harvested EternalHarvest_Planted_…: N more ripe crop(s) in the 30 m square sent for harvest"; "the game declined harvesting …" would mean the game refused the loot request for that crop.
