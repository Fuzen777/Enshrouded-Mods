# Better Spells: Handoff For The Move To Live Settings

> [!abstract]
> Better Spells is two Shroudforge Lua mods for Enshrouded. Its settings are being moved from the startup mod (applies at game start) to the live mod (applies while playing, from the F9 menu), group by group. Summons, Chain Lightning, Ball Lightning and the Radius group are live. Left to do: the Duration group with Invulnerable Totems, the Damage group, Summon Recast Time, then retiring the startup mod. This file is written on 20 Nov 2026 for a new thread that has none of the earlier conversation.

## Corrections From The 20 Nov 2026 Review

> [!warning]
> This file was checked against the offline game data and sources in `D:\Dev\Enshrouded` (build 26430625). Its counts and values hold. The points below correct it; where they disagree with the text further down, they win. The move to live settings described below was not finished on Shroudforge: since 3.0.0 Better Spells is a Kiln mod in which every setting is live (`CHANGES.md`, 3.0.0). This file stays as the record of the Shroudforge mods; its Steps Left no longer apply.

1. Shroudforge's source is available: `D:\Dev\Enshrouded\Reference\shroudforge`. The overrides are in `modules\mod-runtime\lua\ingame\overrides`, and `create_override_targets.lua` takes booleans, so an on/off value is not the unknown Step 2 says.
2. The item's Time config (`configId` 1674724000) is a tooltip-only slot that nothing reads, and Floats, a Uint32 and a Bool share that id. A duration is the tooltip line plus the placed template's LifeTime; the first row of Step 2's table changes nothing in game.
3. Root's and Blizzard's zone templates carry a `DebuffDuration` config (`0x130a0ef2`; 5 s and 9 s on the first tier, equal to the main duration) that the zone's script passes to `applyBuff`. The Duration settings must scale it too (the user's decision).
4. Float config 1469785687 is what the game's tooltip calls Charge Time, and 1101517683 has no known reader. Their effects were seen in game; "cooldown" and "cast lock" are this project's names, and what reads them in the exe is not identified.
5. Float config 1381503774 is named `RangeTerraforming` in the Impact programs and is read by 18 terraforming programs. Live 1.0.5 multiplies it for Root, Fire Pillar and Blizzard as the ground cursor's radius; whether it is that, the ground those spells change, or both is not verified.
6. Damage is worked out at each cast as (staff power + spell power) times the element's share, with `dmgMod` inside the spell's power (Kiln `docs\re\abilities.md`, from disassembly). So a `dmgMod` changed in a loaded world reaches the next cast, and a Damage setting multiplies the spell's share only, not the staff's. LifeTime is copied into the entity when it spawns, so a duration change reaches the next cast.

## Goal

One live mod in which every Better Spells setting applies while playing, with the user's saved values kept, and no startup mod left.

A setting must never be in both mods at once: each would multiply the value.

## Where Things Stand

| Mod | Id | Version | Runs | Settings It Has Now |
|---|---|---|---|---|
| Better Spells | `better-spells` | 2.0.9 | In Shroudforge's startup asset pass; a change needs a restart | 9 Duration, Invulnerable Totems, 17 Damage, Summon Recast Time (28) |
| Better Spells Live | `better-spells-live` | 1.0.5 | In the loaded world; a change in F9 applies at once | Skeletons Per Cast, Totems Per Cast, Summon Spread, Chain Lightning Lock-On Range, Ball Lightning Speed, Radius and Hit Rate, 7 Radius, 2 Visual toggles (16) |

- Both versions are installed in the game with the user's saved values.
- Last code commit: `6206bd0`, tags `better-spells/v2.0.9` and `better-spells-live/v1.0.5`. Two later commits (`6c1bff2`, `69e29da`) only add what was seen in game to `CHANGES.md`.
- `CHANGES.md` in this folder has the full history, one entry per version, with what was seen in game and what was not.

### Seen In Game

- Summons per cast, Summon Spread, Chain Lightning Lock-On Range: work, also when changed in F9 while playing.
- Ball Lightning Speed, Radius and Hit Rate: work, also when changed between two casts in one session.
- The seven Radius settings and the two Visual toggles (Live 1.0.5): the user reports areas, zone and explosion visuals and tooltips all working at the saved multipliers. The log shows all 196 values found at the game's own sizes, bound, and written within 13.3 s of the world loading, with no refused value and no failed write.

### Not Seen Yet

- A radius of the seven spells changed in F9 between two casts. The log of the run the user reported on has no F9 save. Ball Lightning Radius showed it for an area, not for a zone's VFX size, an explosion's scale or a tooltip.
- Whether the game stutters with about 200 values kept in place. It was not asked about.

## Steps Left

> [!info]
> The user asked for everything that isn't live yet. Whether that is done in one step or in several was not decided. I had recommended the Duration group first; the user's answer was to hand off. Ask before starting.

### 1. Close The Open Check Of Live 1.0.5

1. Ask the user to change one radius in F9 between two casts and say whether the area, its look and the tooltip follow, and whether anything stutters.
2. Confirm it in the log: a line `[better-spells-live] Save mod settings: Succeeded`, then a `settings:` line with the new multiplier, then `N values written` and a `now` line for that spell.
3. Record the result in `CHANGES.md` under Live 1.0.5.

### 2. Duration Group And Invulnerable Totems

What Better Spells does at startup today, to be done in the loaded world instead:

| Value | Where | Rule |
|---|---|---|
| Main duration | The spell item's `keen::impact::TimeImpactConfig` in `impactValues.simple`, in nanoseconds | Times the setting, rounded to a whole number |
| Tooltip line | The item's `uiValues` line with `locaId` 609674671 and `valueFormat` `Duration`, in milliseconds | The new duration in whole milliseconds |
| Lifetime | The `keen::ecs::LifeTime` component (`lifeTime.value`, nanoseconds) of the template the item places (`placedEntity`) | Gains the time the spell's main duration gained. It is added, not multiplied: Acid Cone's explosion lasts 4 s under a 5 s tooltip and keeps that 1 s gap |
| Invulnerable Totems | `keen::ecs::CombatFlags` of every totem's placed template | `immune` true and `handleHits` false, whatever the totem duration setting |

- Every Time config shares `configId` 1674724000, so the main one is found by matching its value to the tooltip line. A second tooltip line (`locaId` 2789026239) is the effect on enemies and stays.
- A lifetime of 0 means no limit and is left alone.
- The settings, with the user's saved values (all at their defaults):

  | Setting | Key | Item Name Pattern | Saved |
  |---|---|---|---|
  | Totem Duration | `totemDurationMultiplier` | `^Ammo_T%d+_Spell_%a+_Totem_` | x10 |
  | Skeleton Duration | `skeletonDurationMultiplier` | `^Ammo_T%d+_Spell_Skeleton_` | x10 |
  | Magic Shield Duration | `magicShieldDurationMultiplier` | `^Ammo_T%d+_Spell_MagicShield_` | x10 |
  | Lightning Wisp Duration | `lightningWispDurationMultiplier` | `^Ammo_T%d+_Spell_Lightning_Wisp_` | x10 |
  | Blizzard Duration | `blizzardDurationMultiplier` | `^Ammo_T%d+_Spell_Ice_Blizzard_` | x4 |
  | Lightning Ball Duration | `lightningBallDurationMultiplier` | `^Ammo_T%d+_Spell_Lightning_Ball_` | x4 |
  | Root Duration | `rootDurationMultiplier` | `^Ammo_T%d+_Spell_Root_` | x2 |
  | Fire Pillar Duration | `firePillarDurationMultiplier` | `^Ammo_T%d+_Spell_Fire_Pillar_` | x2 |
  | Acid Cone Duration | `acidConeDurationMultiplier` | `^Ammo_T%d+_Spell_Acid_Cone_` | x2 |
  | Invulnerable Totems | `invulnerableTotems` | The totems' placed templates | On |

- Range x1 to x10, step 0.5.
- In the game's data today: 38 items and 36 placed templates change (totems 10 and 10, skeleton 4 and 4, Magic Shield 2 and 2, Lightning Wisp 4 and 4, Blizzard 4 and 4, Lightning Ball 3 and 3, Root 4 and 4, Fire Pillar 4 and 4, Acid Cone 3 items sharing 1 template). The startup log names no item with an unclear duration and no template shared by spells that gain different times.
- Blizzard, Root and Fire Pillar items and templates are also in the Radius list, so a resource will hold fields of two settings. That is fine: each field belongs to one setting.
- Not known until tried in game: an on/off value written in a loaded world (Invulnerable Totems would be the first), and whether a duration changed between two casts is used. Ball Lightning's hit wait, a Time config in a template, was.
- A change applies to what is cast afterwards. A totem or skeleton already out keeps its time.

### 3. Damage Group

- Each setting multiplies `damageSetup.dmgMod` of its spell items (0.91 for Fire Bolt, 1.08 for Fireball, 0.1 per tick for Blizzard, 0.12 for Light Burst). An item without a `dmgMod` above 0 is skipped.
- Range x1 to x50, step 0.25, default x1.
- Seventeen settings. The key is the name below plus `DamageMultiplier` (`fireBoltDamageMultiplier`):

  | Key Prefix | Item Name Pattern | Saved |
  |---|---|---|
  | `fireBolt` | `^Ammo_T%d+_Spell_Fire_Bolt_` | x1 |
  | `fireball` | `^Ammo_T%d+_Spell_Fire_Ball_` | x1 |
  | `iceBolt` | `^Ammo_T%d+_Spell_Ice_Bolt_` | x1 |
  | `acidCone` | `^Ammo_T%d+_Spell_Acid_Cone_` | x1 |
  | `firePillar` | `^Ammo_T%d+_Spell_Fire_Pillar_` | x1 |
  | `blizzard` | `^Ammo_T%d+_Spell_Ice_Blizzard_` | x1 |
  | `lightningBall` | `^Ammo_T%d+_Spell_Lightning_Ball_` | x1 |
  | `chainLightning` | `^Ammo_T%d+_Spell_Lightning_Chain_` | x1 |
  | `shroudMeteor` | `^Ammo_T%d+_Spell_Fog_Meteor_` | x1 |
  | `root` | `^Ammo_T%d+_Spell_Root_` | x1 |
  | `lightBurst` | `^Ammo_T%d+_Spell_Fire_Lightburst_` | x10 |
  | `totem` | `^Ammo_T%d+_Spell_%a+_Totem_` | x1 |
  | `lightningWisp` | `^Ammo_T%d+_Spell_Lightning_Wisp_` | x2 |
  | `skeleton` | `^Ammo_T%d+_Spell_Skeleton_` | x2 |
  | `boneChannel` | `^Ammo_T%d+_Spell_Bone_Channel_` | x1 |
  | `shroudBeam` | `^Ammo_T%d+_Spell_Fog_Beam_Channel_` | x1 |
  | `lightningChannel` | `^Ammo_T%d+_Spell_Lightning_Channel_` | x1 |

- Not known until tried in game: whether a `dmgMod` changed in a loaded world is used by the next cast, and whether a summon already out follows it. At startup the damage follows, for summons too.
- Do not use Float config 1101517683 for damage: it is a cast timing (2.0.3 made that mistake).

### 4. Summon Recast Time

- One setting, `summonRecastMultiplier`, x0.1 to x1, step 0.05, default x1, saved x0.25.
- For the 14 Skeleton and totem items (`^Ammo_T%d+_Spell_Skeleton_`, `^Ammo_T%d+_Spell_%a+_Totem_`) it multiplies three values:
  1. The cooldown: Float config 1469785687 in `impactValues.simple`, in seconds (2 s for Skeleton, 0.7 s for totems).
  2. Its tooltip line: `locaId` 1551270780, `valueFormat` `Duration`, in whole milliseconds.
  3. The cast lock: Float config 1101517683 (1 for all of them), how long the caster stays locked in the cast.

### 5. Retire The Startup Mod

1. Decide with the user which mod remains. Two ways:
   1. Keep `better-spells-live` as the one mod, and have the user remove Better Spells in Vortex.
   2. Make the live mod the new `better-spells` (its folder is the one Vortex staged) and remove the `better-spells-live` folder from the game's mods folder. Its saved values then have to move the other way.
2. Bring up to date: the two opening paragraphs of `CHANGES.md`, `$projectMods` in `Scripts\Build.ps1`, the tests (`tests\test_mod.py` goes with the startup mod), and the header comment of the live `mod.lua`.
3. In that header, the Radius paragraph still ends with "Not known until tried in game". It is known now (see Seen In Game). It was left as it is so that the source stays equal to the installed 1.0.5; fix it with the next version.
4. Optional: replace the fixed spell lists by a search by name. `game.assets.get_loaded_resources_by_type` lists what a world has loaded, so the list could be built in game and would follow a game patch by itself. What listing every `keen::ItemInfo` costs is not measured.

### For Each Stage

1. Propose the plan with its alternatives, ask with a question the user can click, and wait for the answer.
2. Add the settings to the live mod and remove them from the startup mod in the same step.
3. Extend the spell list and the tests, and run all tests.
4. Write the `CHANGES.md` entries, raise both versions by one patch (Live 1.0.6, Better Spells 2.0.10).
5. Check whether the game is running, then build and install both mods in one run of `Build.ps1 -Install`.
6. Commit and tag (`better-spells/vX.Y.Z`, `better-spells-live/vX.Y.Z`).
7. The user restarts and stops at the main menu. Check the log, then let the user load the world, check the log again, and ask what the user sees.
8. Record what was seen, and what was not, in `CHANGES.md`, and commit.

## How The Live Mod Works

`src\better-spells-live\src\mod.lua`, 891 lines, one file.

- `MULTIPLIERS` is the list of settings that multiply values. Each entry has a key, a label, limits, an optional default, a unit for the log, and a `find` function.
- A `find` function returns the fields of one setting, a text saying where they were found, whether to look again later, and optional notes for the log. It is all or nothing: every item and template of a setting must be loaded before any value is changed, so the tiers stay alike. A missing one is looked up again every 2 s.
- A field is one value: `target` (from `runtime.overrides.resource(resource, path)`) plus options that say how it follows the setting:

  | Option | Meaning |
  |---|---|
  | `group`, `leads`, `sizedTo` | The fields of one template share a group. The field that leads works out how many times the template grows (the factor, or what brings the area to `sizedTo` metres times the factor); the others grow as much |
  | `floatBits` | The value is a float32 kept as its 32 bits (a VFX parameter) |
  | `wholeNumbers` | Written as a whole number (a tooltip line, nanoseconds) |
  | `toggleKey` | Only changed while that toggle is on |
  | `quiet` | Left out of the values the log names |

- On a multiplier, `divides` makes the values the game's own divided by the setting (Hit Rate), `wholeNumbers` and `logScale` apply to all its fields.
- `applyMultiplier` runs at every update (every 250 ms) and calls `overrides:apply` for each field with a function that works out the wanted value from the game's own (`baseline`). At x1 the game's own value is put back; turning the mod off or leaving the world puts everything back.
- `RADIUS_SPELLS` is the list of the radius spells' items and templates by guid, between two marker lines. `tests\make_radius_spell_list.py --write` writes it from the All Spells Probe's export. For the stages left, that script and the list are the place to add the Duration, Damage and Recast items; a lifetime that gains a time rather than a factor needs a new field rule.
- The summon casts (`CASTS`, `applyCast`) are a separate, older path with their own write order.
- After a write the log names only what changed: `fireball radius: now 13 and 16 m`. The effect report reads `4 of 4 summon casts and 11 of 11 other settings found`.

### What Is Known About Shroudforge's Runtime

- `runtime.overrides.resource` binds one scalar of a loaded resource. Seen working on a `keen::actor::ActorSequenceResource` (a value inside a `BlobVariant` event), a `keen::ItemInfo` and a `keen::ecs::TemplateResource`.
- Paths are one-based and go through `.value` of a variant. Seen working:

  | Path | Kind |
  |---|---|
  | `subSequences[1].events[22].value.spawnVelocity.z` | Float in a cast event |
  | `itemTargetingSetup.targetingRange` | Float in an item |
  | `uiValues[n].value` | Tooltip line of an item |
  | `impactValues.simple[n].value.value` | Float config of an item |
  | `components[n].value.colliders[1].dataArray[1].value.shape.radius` | Float in a template |
  | `components[n].value.configValues.simple[m].value.value` | Float config of a template |
  | `components[n].value.configValues.simple[m].value.value.value` | Time config of a template, 8-byte nanoseconds |
  | `components[n].value.parameters.data[k]` | 32-bit word, path ending in an array index |
  | `components[n].value.transform.scale.x` | Float in a transform |

- By analogy and not tried: `impactValues.simple[n].value.value.value` for an item's Time config, `damageSetup.dmgMod`, `components[n].value.lifeTime.value`, and the two booleans `components[n].value.immune` and `.handleHits`.
- An object spawned after a template or cast value was written takes the new value, also when it is changed again later in the session. An object already out keeps what it was spawned with.
- Items are fetched with `game.assets.resolve_loaded_resource("g38_items::ItemRegistry", itemId)` first and `get_loaded_resource(guid, type)` as the fallback; templates and casts by guid. Every listed resource was loaded as soon as the world was.
- Writes are paced by Shroudforge: several fields of one resource go through over several updates. 196 values took 13.3 s after a world loaded; an F9 change of three values was written within two seconds. How long a radius change of 20 to 35 values takes is not measured.
- Shroudforge's own API notes are in `shroudforge\cache\lua\base.lua` in the game folder. Its source is not available.

## Rules That Must Be Kept

> [!danger]
> Never write a cast sequence's events, or anything else that reads as `keen::BlobVariant`, in the startup asset pass. Better Spells 2.0.7 did, and Shroudforge dropped every asset mod for that launch.

> [!danger]
> After a changed asset mod (Better Spells) is installed, the user must stop at the main menu at the next start. Check `shroudforge-client.log` for `pregame phase failed` and `Asset mods failed` before a world is loaded. Say so in the same message as the install.

> [!warning]
> Installing over a running game: the game keeps the versions it loaded, and from then on F9 changes to the loaded live mod are saved but no longer reach it until the restart. Check whether the game runs before installing, and tell the user plainly.

> [!warning]
> Install both mods in one run of `Build.ps1 -Install`. Only then does a setting that moved between them keep its saved value.

- No broad probes in the startup asset pass. A read-only probe belongs in a runtime mod, in `tests\`.
- Shroud Meteor's ground cursor (Float config 1381503774 in its items) must stay as it is: its crater's script reads it, and 25 m of terrain per meteor hitched the game.
- Six `restart-required` warnings for the asset mods are in the log at every start. They are not a failure sign.
- Before telling the user that a value or a function does not exist, search every saved file for the bare word without regard to case, and read the notes. Twice a value was called missing that was there, and once a function.
- When the user says a live setting works, look for `Save mod settings` in the log before recording that a change while playing was seen.
- Do not offer again: Chain Lightning Range (dropped by the user, the spell's jumps are unreliable in the game itself) and a faster summon cast.

## Paths And Commands

| What | Where |
|---|---|
| This project | `C:\Google Drive\Coding\Game Mods\Enshrouded\My Mods\Better Spells` (git repository at `C:\Google Drive\Coding`, commits go to `main`) |
| Workspace rules | `C:\Google Drive\Coding\AGENTS.md` |
| Game's mods folder | `D:\SteamLibrary\steamapps\common\Enshrouded\mods` |
| Folder Vortex staged for Better Spells | `D:\Vortex Mods\enshrouded\Better Spells 2.0.1\better-spells` |
| Log of the current game start | `D:\SteamLibrary\steamapps\common\Enshrouded\shroudforge\logs\shroudforge-client.log` (earlier starts next to it, with a number in the name) |
| All Spells Probe's export | `D:\SteamLibrary\steamapps\common\Enshrouded\shroudforge\exports\all-spells-probe` |
| Backups made at each install | `%APPDATA%\Vortex\enshrouded-mod-backups` |

Build, test and install both mods, keeping the saved F9 values:

```powershell
pwsh -NoProfile -File "C:\Google Drive\Coding\Game Mods\Enshrouded\My Mods\Better Spells\Scripts\Build.ps1" -Install
```

- The tests need lupa (Lua for Python), which is not installed for `py` on this PC. Either install it (`py -m pip install lupa`) or pass `-LupaPath` with a folder that holds it. The folder used so far was a temporary one and may be gone.
- Without `-Install` the script only runs the tests and builds the zips in `dist`. `-Mod` picks one of the two mods.
- One test alone: `py tests\test_live.py`, with `PYTHONPATH` set to the lupa folder if needed. It prints `OK` at the end.

## Working Agreements With The User

- Confirm the design before writing code: propose a plan with its alternatives, ask with a clickable question, and wait. Once the approach is confirmed, do not ask again for permission to edit.
- Be concise. Number points and subpoints. Title Case for labels.
- `CHANGES.md` is the project's record: one entry per version, a patch bump for every change, and for each version what was seen in game and what was not. There is no `FEATURES.md` here.
- A commit message is one paragraph that starts with `Better Spells Live | 1.0.6:` or `Better Spells | 2.0.10:` and says what changed, why, what the user chose, and what is not checked yet.
- Test and probe scripts go in `tests\`.
- Markdown as in `AGENTS.md`: no line breaks to limit width, one `#` per file, admonitions and tables where they help.

## Files To Read First

1. `CHANGES.md`, the entries Live 1.0.5, Live 1.0.4 and 2.0.9.
2. `src\better-spells-live\src\mod.lua`, the header comment and `applyMultiplier`.
3. `src\better-spells\src\mod.lua`, 690 lines: what is left to move. The header says how each value was found; `writeDuration`, `extendLifeTime`, `makeInvulnerable`, `scaleItemDamage` and `scaleItemRecast` are the five things to redo in the loaded world.
4. `tests\test_live.py`: the fake runtime and how a fake world is built from the live mod's own list.
5. `tests\make_radius_spell_list.py`: how the spell list is written.
6. `Scripts\Build.ps1`.
