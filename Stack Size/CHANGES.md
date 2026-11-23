# Stack Size

Shroudforge Lua mod that raises the max stack size of every stackable item, with a bigger size for items that already stack high, such as building blocks. Replaces the stack size part of Auto Loot Plus, whose Lua crashed Shroudforge's startup asset pass. Source in `src\stack-size`, Vortex zip in `dist`, test in `tests`.

## 1.1.0

- New **Large Stack Size** setting (F9 menu, 2 to 65535, default 65535): items that stack to the Large Stack Threshold or more in the game go to this size instead of Max Stack Size.
- New **Large Stack Threshold** setting (2 to 65535, default 1000): the game's own stack size from which an item counts as large. There's no building block item category (blocks are Materials), so the game's own stack size is what picks them. 65535 turns large stacks off, since no item stacks that high in the game.
- Why 65535 and not 99999: `keen::ItemInfo.maxStackSize` is a 16-bit number (`uint16`), and so are the amounts the game moves when you pick up or transfer items. Unified EML Tweaks caps at 65535 for the same reason.
- The game's own size is read in the same pass, just before the item is changed. Hands-Free Lantern is the only mod that runs earlier and touches stack sizes, and it only sets lanterns to 1.
- The log line now gives both groups, as `<raised> of <items> items raised: <normal> to 9999, <large> large ones (1000 or more in the game) to 65535 (<single> single, <kept> already at or above their new size)`. It's followed by lines listing every large item by its game size, 20 names a line (`large, <game size> in the game (<count> items): <names>`).
- New `tests\test_mod.py` that runs the Lua against fake items (needs lupa).

> [!warning]
> Stacks above 9999 haven't been tried in game yet. To check: the 5-digit count shows in full in the slot, and splitting, dropping and crafting from a big stack work. If anything goes wrong above about 32000, lower Large Stack Size (30000 stays clear of it).

> [!note]
> Installing 1.1.0 over 1.0.0 in Vortex brings the settings back to their defaults.

---

## 1.0.0

- Every item whose `keen::ItemInfo.maxStackSize` is above 1 is raised to the **Max Stack Size** setting (Shroudforge F9 menu, 2 to 65535, default 9999, applies on the next start). Single items stay single, and items that already stack higher keep their size.
- One pass over the items, each in its own `pcall`, so a bad item can't fail the asset pass and roll back every other asset mod.
- Logs one line in `shroudforge\logs\shroudforge-client.log` under `[stack-size]`: how many items were raised.

> [!note]
> Changing the setting in F9 saves it into `extended.mod.json` without replacing the file, so the Vortex hardlink holds and Vortex's staging copy gets the new value too. Reinstalling the mod in Vortex resets the setting to its default.
