# Stack Size

Shroudforge Lua mod that raises the max stack size of every stackable item. Replaces the stack size part of Auto Loot Plus, whose Lua crashed Shroudforge's startup asset pass. Source in `src\stack-size`, Vortex zip in `dist`.

## 1.0.0

- Every item whose `keen::ItemInfo.maxStackSize` is above 1 is raised to the **Max Stack Size** setting (Shroudforge F9 menu, 2 to 65535, default 9999, applies on the next start). Single items stay single, and items that already stack higher keep their size.
- One pass over the items, each in its own `pcall`, so a bad item can't fail the asset pass and roll back every other asset mod.
- Logs one line in `shroudforge\logs\shroudforge-client.log` under `[stack-size]`: how many items were raised.

> [!note]
> Changing the setting in F9 saves it into `extended.mod.json` without replacing the file, so the Vortex hardlink holds and Vortex's staging copy gets the new value too. Reinstalling the mod in Vortex resets the setting to its default.
