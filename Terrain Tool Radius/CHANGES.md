# Terrain Tool Radius

Native Shroudforge mod: the pickaxe digs and the rake flattens a bigger area. It replaces the two Advanced Terrain Edit (ATE) features in use, without ATE's other tools and its roughly 20 other hooks. Source in `src`, Vortex zip in `dist`, tests in `tests`, built with `Scripts\Build.ps1`.

## 1.0.0

- Two settings in Shroudforge's F9 menu, in steps of 0.5:

  | Setting | Range | Default | What it multiplies |
  |---|---|---|---|
  | Pickaxe Radius | x1 to x5 | x3 | The radius of every dig hit from a player's tool, and the hit's voxel budgets by its cube |
  | Rake Radius | x1 to x4 | x4 | The radius of flatten hits (terraform types 2 and 3) |

- Bombs and enemy attacks keep their normal size. At x1 a setting leaves its hits exactly as the game's own.
- It changes the same two places in the game's terraform function as ATE 1.5.1a: the radius load (`exe+0x3ED150` in build `6aeb144c`) and the point where every terraform type meets again (`exe+0x3ED1A6`). The code caves do what ATE's do there, learned by disassembling ATE.
- The first site is found with a 112-byte pattern that holds every stack offset the pickaxe cave relies on, and the second through the switch's jump to the meeting point. If the code isn't found, or another mod's jump is already there, nothing changes and the log says why.
- Differences from ATE:
  - Rake Radius applies to every player's rake in a world you host, where ATE only enlarges the host's own.
  - ATE's pickaxe radius went from 0.1 to 30; here it's 1 to 5.
- The values are read from the mod's installed `extended.mod.json`, where Shroudforge keeps what's set in F9, and the file is checked for changes every second.
- Log in `mods\terrain-tool-radius\terrain-tool-radius.log` (the previous start's is `terrain-tool-radius.prev.log`): version and build stamp, settings, `pickaxe hook active @ exe+0x3ED150`, `rake hook active @ exe+0x3ED1A6`, then `settings changed: ...` for each change.
- `Scripts\Build.ps1` runs both tests before packing:
  - `test_main.exe` checks the settings parser, that the manifest's default, min and max match the DLL's, and that both sites are found exactly once in the installed `enshrouded.exe` (at ATE's addresses for build `6aeb144c`), and that another mod's jump at either site is recognised.
  - `check_caves.py` disassembles both caves and site jumps with capstone and checks them instruction by instruction.

> [!warning]
> Not yet checked in game. Disable Advanced Terrain Edit in Vortex first: with both active, whichever loads second finds the other's jump and changes nothing. Still to check: the pickaxe digs wider and still gives dirt, the rake flattens wider, bombs stay normal, and whether F9 changes apply right away or only on the next start.
