# Pocket Light

A Kiln mod (id `pocket-light`, C#, `src\`, side "client") that puts a light on your character with nothing to equip. Made after Hands-Free Lantern (Nexus 235) had to be disabled: its startup step and the content mod of Elixir Well Infestations both rewrite the game's English texts, and ShroudForge's startup pass fails when two mods do that. Pocket Light takes no part in that pass, adds no item and writes nothing to saves.

How it works: it plays the game's own small lantern light effect (`ca49de18-616f-4b87-8c00-19efe3f027bb`, the lit `Prop_Buildings_Lantern_01_A`, the effect Hands-Free Lantern hangs on the belt) on the local player's character through Kiln's `Vfx.PlayOn`, which attaches it so it moves with the character. The effect's own parameters carry the settings: `light_radius`, `light_intensity`, `light_color`, `light_position_offset` (set to zero) and `scaleMultiplier` (the flame's size).

`Scripts\Build.ps1` runs the tests, builds against Kiln's assemblies in `<game>\kiln\host` and puts the mod in `dist\pocket-light`; `-Install` copies it to `<game>\kiln\mods\pocket-light`, where Kiln loads it as a drop-in (game closed).

## 1.0.0

- A light follows your character while you are in a world. **K** switches it on or off; the key is Toggle Light in the Kiln menu's Keys tab and can be changed there.
- On or off is remembered: the light starts as you left it.
- Settings, in the Kiln menu or in `%LOCALAPPDATA%\Kiln\profiles\my-mods\config\pocket-light-client.json` (Kiln reloads it when saved); all apply while playing:

  | Setting | Range | Default | Notes |
  | --- | --- | --- | --- |
  | Light On | on / off | on | What the key switches |
  | Brightness | 0 to 50 | 10 | The game's small lantern is 10, a hand torch 15 |
  | Radius (m) | 1 to 100 | 25 | The small lantern is 25, a hand torch 45 |
  | Color | `#rrggbb` | `#f7995c` | The lantern's orange; a color that can't be read gives this one |
  | Height (m) | 0 to 3 | 1.4 | Above the character's feet |
  | Forward (m) | -2 to 2 | 0.3 | In front of the character; negative is behind |
  | Flame Size | 0 to 2 | 0 | 0 shows no flame, only the light; 2 is a placed lantern's flame |

- A changed setting restarts the light with the new values (at most five times a second).
- The light is put back after a respawn or a new world: when it goes out by itself, it is started again within a second.
- Only your own game shows the light; other players don't see it.
- `tests\LightRulesTests` checks the colors it reads, values outside their ranges, the defaults and the toggle key.

> [!note]
> Not yet checked in game. Installed by hand in `<game>\kiln\mods\pocket-light` while the game ran; Kiln loads it at the next start.
>
> To check in `kiln\logs\kiln.log`: `[mod:pocket-light] loaded 1.0.0: K switches the light`, then in a world `light on character …: brightness 10, radius 25 m, …`. "the light went out (…)" names why an effect ended; repeated every second, it means the game refuses the effect.
>
> Not known until tried: whether the game takes the effect's parameters by name through Kiln (if not, the light is the plain lantern's: orange, radius 25, with its flame), whether Flame Size 0 hides the flame, and whether Forward points the way the character faces.
