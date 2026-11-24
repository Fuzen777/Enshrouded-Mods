# Enshrouded Mods

Source of Fuzen777's Enshrouded mods, shared so other modders can read how they work. Each folder is one mod, with its own `CHANGES.md` that records what was changed and what was seen in game, including what was learned about the game along the way.

| Mod | Loader | What it does |
|---|---|---|
| [Area Harvest for Eternal Harvest](Area%20Harvest%20for%20Eternal%20Harvest) | Kiln (C#) | Retired: merged into Perennial Crops 1.0.1 |
| [Auto Doors Kiln](Auto%20Doors%20Kiln) | Kiln (C#) | Doors open and close by themselves, by sending the game's own `ToggleInteractionEvent` from an ECS program |
| [Better Spells](Better%20Spells) | Kiln (C#) | Per-spell duration, damage, area and summon settings |
| [Focus Gain](Focus%20Gain) | Shroudforge (Lua) | More focus per hit from felling axes, pickaxes and weapons, and a wider felling axe special |
| [Perennial Crops](Perennial%20Crops) | Kiln (C#) | Planted crops keep producing after harvest, the way the game's own perennials do, with area harvest of the same kind and a Shift+F6 settings window |
| [Pocket Light](Pocket%20Light) | Kiln (C#) | A light on your character with nothing to equip |
| [Streamlined Logistics](Streamlined%20Logistics) | Kiln (C#) | One list of what the linked chests hold, from anywhere, with moves between them and the backpack |

## Building

Each mod builds with its own `Scripts\Build.ps1` (PowerShell 7, Windows). Kiln mods compile against Kiln's assemblies in `<game>\kiln\host`; the scripts and project files default to the author's game folder (`D:\SteamLibrary\steamapps\common\Enshrouded`), so pass `-GamePath`/`-GameFolder` to the script, or `-p:KilnHost=<game>\kiln\host` to `dotnet build`, for yours.

This repository is a mirror of the `My Mods` folder of the author's own workspace, published from there; history starts when that folder was created (2026-11-23).

## License

MIT, see [LICENSE](LICENSE). Third-party code and data:

- Streamlined Logistics ports the salvage yield formula from [Kiln Auto Loot](https://github.com/frostdev-ops/kiln-auto-loot) (MIT), see `Streamlined Logistics\THIRD-PARTY-NOTICES.txt`.
- Streamlined Logistics' `data\chestsort_categories.json` is the chest category list of shroudlink's chestsort.
