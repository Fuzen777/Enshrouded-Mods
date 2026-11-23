# Focus Gain

Shroudforge Lua mod that makes felling axes, pickaxes and, if you want, weapons give more focus per hit, so their special abilities are ready sooner. Called Tool Focus until 1.0.1; its id is still `tool-focus`. Source in `src\tool-focus`, Vortex zip in `dist`, test in `tests`, Nexus page text in `Nexus Page.txt`.

## 1.0.2

- **Weapon Focus Multiplier** now defaults to 2, so weapons give 6 to 10 focus per hit instead of 3 to 5. Tool Focus Multiplier stays at 3.
- What 1.0.1's log showed in game: besides the 8 felling axes and 8 pickaxes, 496 items carry focus per hit, 110 at 3, 279 at 4 and 107 at 5. They're 343 `Weapon_...` items, 152 `Craft_Weapon_...` lore variants, and the unarmed fists.

> [!note]
> Installing 1.0.2 over 1.0.1 in Vortex brings both settings back to their defaults.

---

## 1.0.1

- Renamed from Tool Focus to Focus Gain, since it no longer covers only tools. The id stays `tool-focus`, so the `mods\tool-focus` folder and the `[tool-focus]` log tag don't change.
- New **Weapon Focus Multiplier** setting (F9 menu, 1 to 20, default 1, applies on the next start): every item other than felling axes and pickaxes that has a focus per hit value gets this many times its normal focus. At 1 the game's own values stay.
- The tool setting is now labeled **Tool Focus Multiplier**. It's the same setting as before.
- A second log line covers those other items: how many carry focus per hit, each original value with what it became, how many items have it and a few example names, then how many items there are per name prefix. The probe read only the tools, so this line is the first look at which items give focus and how much.

> [!warning]
> In Vortex, remove Tool Focus 1.0.0 before installing Focus Gain 1.0.1, since both install to `mods\tool-focus`. Installing brings both settings back to their defaults (Tool Focus Multiplier 3, Weapon Focus Multiplier 1), so set them again in F9.

---

## 1.0.0

- The focus per hit of the 8 felling axes (`Tool_T0_Axe_...` to `Tool_T7_Axe_...`) and the 8 pickaxes (`Tool_T0_Pickaxe_...` to `Tool_T7_Pickaxe_...`) is multiplied by the **Focus Multiplier** setting (Shroudforge F9 menu, 1 to 20, default 3, applies on the next start). Felling axes and pickaxes give 3 focus per hit, so 9 at the default. Combat weapons keep their own focus gain.
- Focus is the game's `SpecialAbility` attribute. Each weapon and tool carries its focus per hit in `keen::ItemInfo.impactValues.simple` as a `keen::impact::Sint32ImpactConfig` with configId 1236200858, which the hit programs read through their `SpecialAbilityGain::value` variable. Found with a temporary read-only probe mod.
- One pass over the items, each in its own `pcall`, so a bad item can't fail the asset pass and roll back every other asset mod. Each change is read back after writing.
- Logs under `[tool-focus]` in `shroudforge\logs\shroudforge-client.log`: how many axes and pickaxes were raised and from what to what, plus any tool that has no focus per hit value.
- `tests\test_mod.py` runs the mod against a fake Shroudforge asset API (needs lupa).

> [!note]
> The probe read only the felling axes' value. The pickaxes' 3 was confirmed in game after release: the log showed all 8 axes and 8 pickaxes going from 3 to 9.

> [!note]
> Changing the setting in F9 saves it into `extended.mod.json` without replacing the file, so the Vortex hardlink holds and Vortex's staging copy gets the new value too. Reinstalling the mod in Vortex resets the setting to its default.
