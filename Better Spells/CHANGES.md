# Better Spells

Shroudforge Lua mod that makes spells that last a while last longer and spells hit harder, with one setting per spell (until 2.0.8 it also widened spells' areas, which its companion does since). Called Lasting Spells until 1.0.6. Source in `src\better-spells`, Vortex zip in `dist`, tests in `tests`. `Scripts\Build.ps1` runs the tests and builds the zips; with `-Install` it writes the mods over the files the game reads, keeping the saved F9 values (`-Mod` picks one of the two).

Its companion **Better Spells Live** (`src\better-spells-live`, id `better-spells-live`) changes how many summons a cast places, Chain Lightning's lock-on range, Ball Lightning's speed, radius and hit rate, and the radius of seven area spells, in the loaded world, so its settings apply while playing. Better Spells' settings are moving there in stages (Radius in Live 1.0.5); a setting must never be in both, or it is multiplied twice. It has its own version: its entries below are headed Live, its tags are `better-spells-live/vX.Y.Z`.

Since 3.0.0 Better Spells is one **Kiln mod** (`src\better-spells-kiln`, id `better-spells`, C#) with every setting of the two Shroudforge mods above, all applied while playing. The Shroudforge mods (2.0.9 and Live 1.0.5) are retired. Their entries stay here as the record; their sources, tests, probes and build script were removed from the folder on 21 Nov 2026 on the user's request and are in the repository's history under the tags `better-spells/v2.0.9` and `better-spells-live/v1.0.5`, so paths below such as `src\better-spells`, `tests\test_live.py` or `Scripts\Build.ps1` name files of that history. `Scripts\Build-Kiln.ps1` runs the tests (`tests\SpellRulesTests`) and builds the mod into `dist\kiln\better-spells`; with `-Install` it copies it to `<game>\kiln\mods\better-spells` (game closed). The settings are in Kiln's profile (`%LOCALAPPDATA%\Kiln\profiles\my-mods\config\better-spells.json`), so an install never touches them. Tags are `better-spells/vX.Y.Z` as before. The Kiln Proof entry below is the small mod that tested the move.

## 1.3.1

- **Nexus upload** (asked 26 Nov 2026, as 1.3.1 by the user's choice, not a minor bump): DLL of 2185942. `Changelog.mdbbcode` has a 1.3.1 entry; the Nexus page says the Skills list searches names, settings and descriptions.

- **Skills search also looks through descriptions** (the user's ask, 26 Nov 2026): a skill shows when the search text is in its name, in its card's line labels, or in its game description as the card shows it (with its numbers); a group shows when it is in one of its members' names.

## 1.3.0

- **Nexus upload of 1.2.2-dev.7** (asked 26 Nov 2026): no code change, the DLL is dev.7's (25074d8, sha 2f58f2c6); only kiln.mod.json's version changes. `Changelog.mdbbcode` covers 1.2.1 and 1.2.2. The Nexus page describes the Skills tab and asks for Kiln 0.2.0-dev.31.16 (docking, and the build references Kiln.Assets and Kiln.Client). Not yet confirmed in game: the skill icons, and dev.7's Radiant Aura description number.

## 1.2.2 (not uploaded)

- **Skills tab** (asked 26 Nov 2026; the user's picks in the Skill Stat Picker): a third tab that changes skill-tree effects. Each skill node of `SkillTreeResource` has an impact program and `configValues` (simple and scaled by level, like gem items); `ApplySkillStats` writes them through `Content.Skills.Get("0x<node>")` (`configValues.0x<config>` and its `.scaleFactor`) at every world load and settings change. The game copies a node's configs into the skill's effect when the effect starts at world load, so **a change applies after returning to the main menu and loading the world again** (seen in game with Radiant Aura: writes made while playing never reached the running aura, and a world load puts the game's node data back, which is why it rewrites them at each load).
- `SpellCatalog.SkillStats`: 99 stats from 107 picks (Arsonist, Dark Arts, Iceman and Thunder have one Damage knob for their three configs; Ranger's Value is Endurance and Value1 Dexterity, from its program's attribute references). Kinds: a multiplier of the value at every level (x0.1 to x100), **Base + Per Level** (Radiant Aura and Water Aura range), **Per Level** set as a whole number (Totem Mastery, Advanced Necromancy) and a whole **bonus per node** for the six attributes (their config is a Sint32). Deferred picks (v2) are left out.
- **Groups** (the user's choice): Stats, Damage Bonuses (the 27 "% damage per level" skills), Terrain Damage, Critical Chance, Resistances, Armor, Mana and Stamina Gains, Healing. Each has an **At Least | Override** switch: At Least gives each member the higher of its own value and the group's, Override the group's (the member's line and list entry are greyed, "Set By" the group). 337 settings.
- **The tab's layout**: a search box, a list of full skill names (the game's, in its language) under Groups and the classes, with the game's icon for the 43 active skills (passive and attribute nodes have none in the game), and the chosen skill's card: the game's description with its numbers as the skill has them at max level with the settings, the numbers bold and green. Names and descriptions come from the node's loca ids (`Content.Text` on `Client.Post`); the %k are the node's numeric configs in reverse order (Radiant Aura's damage is Damage Per Scale times Base Damage at the level, as the game shows it). Icons are read at run time from the game's own textures (`SkillIcons.cs`: the node's `iconActive` UiTextureResource, its content blob through Kiln.Assets, `GameTexture.DecodeMip`, a PNG for `Ui.LoadImage`); nothing of the game's is shipped. Needs Kiln.Assets and Kiln.Client.
- Each value shows what it gives ("= 15% Per Level", "= 3 Per Node"). The F9 skill probe of the dev builds is gone.
- Seen in game by the user (26 Nov 2026): Radiant Aura's node values reach the aura after a rejoin (dev.2 probe), and the Skills tab, groups and descriptions show. The game's tooltip read 30 damage per Intelligence at level 3 with Base Damage x10 while dev.6 showed 10; dev.7 uses the game's formula, not re-checked in game yet.

## 1.2.1 (not uploaded)

- **Kiln's window docking replaces WindowPair** (asked 26 Nov 2026; Kiln 0.2.0-dev.31.16, GitHub issue 149). The settings window accepts docks (`AcceptDock`, `PlacementId = "settings"`) and Grimoire 1.1.1 docks its window on its right; Kiln keeps the two together in the same frame: no lag, no flash when one key opens both, **Shift** while dragging moves one alone, dropping it back within 16 px docks it again, and the pair stays on the screen. A place a window only got from the dock is never remembered. The settings window itself never waits.
- **Kiln remembers the window** (`RememberPlacement`, in Kiln's `ui-placement.json`). `window.json` only gives its first place and size, until Kiln has one of its own, so players keep the place they had. `WindowPair.cs` and `WindowPairRules.cs` are gone; `WindowPlacement` only reads `window.json`, and keeps its old tracking for an older Kiln.
- **Older Kiln** (before 0.2.0-dev.31.16): the new members are used only in a `[MethodImpl(NoInlining)]` helper, whose `MissingMethodException` or `TypeLoadException` gives a plain window that opens where it was left (`window.json`, as before), with one line in kiln.log; Grimoire does not dock to it. Seen in game by the user on 26 Nov 2026 (Kiln 0.2.0-dev.31.16): fully working.

## 1.2.0

- **Nexus upload of 1.1.6** (asked 26 Nov 2026): no code change, the DLL is 1.1.6's (2512b74); only kiln.mod.json's version changes. `Changelog.mdbbcode` covers 1.1.1 to 1.1.6. The Nexus page now describes the Gems tab (gem damage, gem stats, summon levels and Sync With Player), Perception and Shot Lifetime, the Default button and the value shown next to a multiplier, Cast Time as the charge time, and asks Kiln 0.2.0-dev.31.11 for docking with Grimoire; the settings path is in a code block of its own (Nexus has no inline code).
- **Folded in before the upload** (the user's choice, 26 Nov 2026, seen in game): Gem of Necromancy had no icon and its settings did not apply, because `T5_Gem_GemOfNecromancy` is missing from Kiln's list of item data in build 26430625 although the game has it ("is not in this world's data" at every load). A gem the list misses is now looked up by its name (`FindGemItems`, logged once), and the gem cards take their icons from those items. Still no icon in game, and the wiki lists Gem of Necromancy as not normally obtainable: the Gems tab now hides gems missing from the world's item list (`HiddenGems`), so its card is gone (its settings stay in the file, unused).
- **Right-click resets** a value (on -, the value or +), a checkbox or a mode button to its default; the Default button is gone, so the "= result" sits right after +. The hint line and each tooltip say so. Confirmed in game (26 Nov 2026).
- **Shots Through Walls** (the user's ask, planned since 1.1.0): a toggle on the Totems, Lightning Wisp and Fire Wisp Scroll cards (Perception group). The shot templates' collider flags (`ColliderResourceComponent.colliders[i]` shapes' `flags`, `combinedFlags` and `joltObjects[j].flags`, `checkAgainst`) lose `World` and `Fluid` and keep `Combat`, written as a relative flag list (`["-World", "-Fluid"]`). Ball Lightning got the same toggle (Through Walls, on its card) for the ball templates `Projectile_Player_Staff_LightningBall_*`. 220 settings. Confirmed in game (26 Nov 2026): the shots and the ball go through walls, so the game uses these flags for walls.
- **Corruption's Explosion Delay fixed** (the user's test: lowered, no explosion at all). Each mark buff (3.2 s) runs program cf3c2b8c, which puts a timer buff `0x9216226a` (3 s, ResetTime) on the enemy and explodes when that timer ends; a mark that ends first takes its program with it, so no explosion. 1.1.6 scaled only the marks. The setting now scales the timer buff and gives the marks the timer's new time plus their game margin (0.2 s), never less. The gem's own 3 s config 0xbee6d4a9 is read by no program. Confirmed in game (26 Nov 2026).

## 1.1.6 (not uploaded)

- **Gems tab: every gem and its other numbers** (the user's picks in the Gem Stat Picker, 26 Nov 2026). The tab now has all 29 gems, with a Tier 7 row; a gem that deals no damage has no Damage line. `SpellCatalog.GemStats` lists 65 stats: durations, cooldowns, tick intervals, chances, summon levels, regeneration, armor, orb healing and more, each with the config of the gem item it changes and the tooltip row (by loca id) that shows it.
- Four kinds of settings: a multiplier (one for the value at every level, or for buff effects); **Base + Per Level**, two multipliers for durations that grow with the gem's level (the base value and what each level adds, e.g. Toxic Betrayal's poison); **Set Value**, a value of its own (Bleeding's and Burning's tick interval, Aerobatics' volley wait, Fracturing's extra shard chance, Cold Composure's movement speed); and **chance points** added to a chance, kept inside 0 and 100%.
- Times never go below 0.1 s. Battle Mania's Regeneration Pause (10 s, kept in milliseconds) goes from x0.1 to x60 (1 to 600 s). Puncturing's debuff duration also sets its Punctured buff, which the Gem of Retaliation shares; Corruption's Explosion Delay sets its five mark buffs.
- **Summon levels**: levels added to the skull's, Wretch's, Damned's and Reaper's, and a **Sync With Player** toggle per gem: the summon takes the player's level plus those levels (read when the weapon is equipped or switched to; holds only those gem programs for `ImpactStartingEvent`). Experimental.
- The window shows what a multiplier gives next to it ("= 6 s", "= +0.5 s Per Level", "= 35%") for values that do not grow with the gem's level, from the gem's own values (after a world has loaded).
- A **Default** button beside a changed value (both tabs), and +/- from a value off the steps go to the next step (Burning's 0.333 s goes to 0.35 s up, 0.3 s down). A default off the steps stays as it is.
- Not tested in game yet: the Time configs' writes, the buff lifetimes, the tooltip rows and Sync With Player.

## 1.1.5 (not uploaded)

- Same `WindowPair` as Grimoire 1.0.4 (the grimoire no longer flashes at its own place when one key opens both windows). Nothing changes for Better Spells' own window, which never waits.

## 1.1.4 (not uploaded)

- **F3 opens the settings window by default** (was Shift+F3; the user's ask, 26 Nov 2026): it now opens with the grimoire on the same key, side by side (1.1.3). Players who already chose a key in Kiln's Keys page keep theirs (keybinds.json wins over the default).

## 1.1.3 (not uploaded)

- **The settings window and Grimoire's window keep together** (the user's report and asks, 26 Nov 2026, with Grimoire 1.0.3, whose CHANGES has the details): `WindowToGrimoire` is replaced by `WindowPair`, the same file as Grimoire's (with `WindowPairRules` and `WindowPlacement`, now identical to Grimoire's but for the namespace).
- The settings window opens beside the grimoire, on its left, when the grimoire is open first. Opened by the same key as the grimoire (the user has both on F3), it stays where it opens and the grimoire goes beside it.
- While the two touch side by side, moving or resizing one moves the other; Shift while dragging moves one alone, and a window dropped close to the other's side snaps back against it.
- A place the window got beside the grimoire is not saved as the player's: opened alone, it opens where the player left it.
- The settings messages to Grimoire now carry two more numbers (seconds since the window opened, and since the player last moved it); Grimoire 1.0.1 and 1.0.2 only take four numbers, so next to them the grimoire no longer opens beside it: use Grimoire 1.0.3. Not tested in game yet.

## 1.1.2 (not uploaded)

- **Gems tab: weapon gem damage** (a Nexus request the user took up, 25 Nov 2026: gem powers deal small flat numbers that do nothing against high-HP enemies). The settings window has two tabs, Spells and Gems. The Gems tab has its own General card (Damage, and its own Multiply / At Least mode; the spells' General does not touch gems) and a Damage card per gem, in a row per tier. `SpellCatalog.GemSpells` lists the 16 gems that deal damage and which configs are their damage, read from their Impact programs: `0x5e34f09b` for most; Meteoric Impact also has `0xa8338df8` (the meteor's explosion) and `0xfaf3b41b` (its fire trail), which the spawned templates read from the gem. Gems whose numbers are healing (Soul Reaping), stamina, armour, resistance, chances or summon levels are left out.
- How: each config is a `ScaledSint32` on the gem item (`impactValues`): a base value plus a `scaleFactor` that the game multiplies by a curve of the gem's level. Both are multiplied (the base whole and never below 1), so the damage grows by the setting at every level; Kiln's docs note that changing only the base does not. The tooltip rows that show those numbers (scaled rows with the same base and scaleFactor) follow.
- A gem copies its configs when its impact starts: a gem already running takes a new value when its weapon is next equipped or switched to on the hotbar. The tab's tooltip and each setting's description say so. The damage is decided on the host; tooltips show the new numbers on Kiln clients only.
- Not tested in game yet.

## 1.1.1 (not uploaded)

- **Grimoire opens beside the settings window** (asked 25 Nov 2026): while the window is drawn, `WindowToGrimoire` sends its edges to Grimoire (`Mods.Send("grimoire", "better-spells.window", [left, top, right, bottom])`, fractions of the viewport) when they change and every half second, and null when it closes (Escape or the key). Nothing is sent when Grimoire is not loaded. Grimoire 1.0.1+ moves its own window there with Kiln 0.2.0-dev.31.11 (#146); Better Spells' window still opens where the player left it.

## 1.1.0

- **Nexus release of 1.0.1 to 1.0.5** (the user's ask, 25 Nov 2026, after checking 1.0.5 in game). Same spell code as 1.0.5. `Changelog.mdbbcode` gets the 1.1.0 entry (Cast Time no longer breaks casts, settings changes no longer freeze the game) and takes the `## X.Y.0` heading and code block layout Auto Doors uses.
- **Settings window: resizable, and it opens where the player left it** (the user's ask, 25 Nov 2026, in 1.1.0 without a version bump). It is a Kiln window (`Ui.Window`, Interactive) instead of a Kiln screen, which always opens centred with a fixed size. `WindowPlacement` (the same class as Grimoire's) follows the window's place and size while drawing and writes them to `kiln\data\better-spells\window.json` once they stay put for half a second; the next game start creates the window there. Escape still closes it (claimed in the window's draw).
- **Perception per summon, and Shot Lifetime for the wisps** (the user's asks, 25 Nov 2026, in 1.1.0). Perception (x0.1 to x5) on the Totems, Skeleton, Lightning Wisp, Skull Vessel and Fire Wisp Scroll cards multiplies how far the summon notices enemies and goes for them: its sight (`Enemy01BehaviorState` aggroSetup.viewRange, combatCommitmentSetup.view.range, combatSetup.maxNonPlayerTargetDistance), the detection sphere wisps and totems shoot into (20 units; spheres of 5 units or more, so the body colliders stay), Lightning Wisp's shoot attachment's sphere, and the lifetime of their shots so they still reach. Shot Lifetime (1 to 20 s, 5 s by default) on the Lightning Wisp and Fire Wisp Scroll cards replaces their shots' 2 s: a wisp picks targets up to 20 units away, but its shots (`Projectile_Companion_ShockWisp`, `Projectile_Companion_FireWisp`) lived 2 s at a random 5 to 10 units/s and fly out sideways or up before curving to the target, so they faded before reaching targets past about 10 units. Totems' shots (3 s at 8 to 12 units/s) reach and keep their own lifetime, times Perception. A new setting kind, Seconds, reads "5 s". Replaces the fixed 4 s bolt lifetime of commit ad20281.

## 1.0.5 (not uploaded)

- **Cast Time changes Charge Time only; the cast lock keeps the game's value** (the user's test and choice, 25 Nov 2026). With 1.0.4 (Charge Time at x0.1, lock held at the floor worked out from the cast's events) the user saw: Blizzard (0.2 s) and Fire Pillar (0.1 s) cast, but only just; Light Burst and Ball Lightning did not cast, their animation cut before the spell went off; Shroud Meteor dropped about 2 meteors before the cast was cut. So Charge Time is only how long the staff is held before the spell can go and can be as low as wanted, while the lock (`0x41a7d373`) is the cast itself (its animation) and must not be shortened. Making a cast shorter would mean speeding its animation up, out of scope.
- Removed with it: the floors (`SpellRules.MinCastTimeMultiplier`, `NeedsShooting`, the cast event scan, the keep-alive mask log line, the tooltip note) and their tests. A run puts back the game's lock where an earlier version had changed it.
- Fewer values written per Cast Time change (the lock is no longer one of them).

## 1.0.4 (not uploaded)

- **Cast Time: only the cast lock has a floor now** (the user's choice, 25 Nov 2026). Charge Time and its tooltip line take the setting all the way to x0.1 for every spell; the cast lock (`0x41a7d373`) takes the same multiplier but never below the spell's floor (the lowest at which its cast still releases the spell, see 1.0.1). In 1.0.1 to 1.0.3 the whole setting stopped at the floor, so each spell's lowest Charge Time looked unrelated to the others (Light Burst 0.5 s to 0.1 s, Shroud Meteor 4 s to 1.4 s): the floor comes from the cast's animation and lock, not from the Charge Time.
- Why this should hold: both reported breakpoints (Chain Lightning x0.55, Lightning Wisp x0.3) match the cast lock exactly; nothing points at Charge Time. Charge Time below a spell's floor is not tested in game yet.
- The log line says when the lock is held: `Chain Lightning Cast Time x0.3, cast lock held at x0.55: 5 items`; the tooltip says the same.

## 1.0.3 (not uploaded)

- **Fix: the Cast Time floors were still too high in 1.0.2** (seen in game: Light Burst x0.4, Blizzard x0.8, Chain Lightning x0.8, Shroud Meteor x0.45, Lightning Wisp x0.95). 1.0.2's mask line showed what Kiln 0.2.0-dev.31.4 gives for a keep-alive mask: a description of the field, `{"path", "type", "value": ["InRangeCombat"], "original", "writable", "flags": [every StateFlag name]}`. 1.0.2 looked into every list of it, and `flags` names `InRangeCombatShooting` for every mask, so the first keep-alive still counted. Only `value` is read now (`SpellRules.NeedsShooting`, moved there so the tests cover it with that exact shape). Expected floors are the ones listed under 1.0.1.
- The once-per-world mask line shows the set flags only.
- 1.0.2 measured in game (Kiln 0.2.0-dev.31.4): a run that writes nothing takes 55 to 70 ms; one spell's Cast Time step writes 10 values in 120 to 140 ms; a General Cast Time step writes 164 to 184 values in 760 to 990 ms, so about 5 ms per Cast Time value written. Kiln report 28 asks about that cost.

## 1.0.2 (not uploaded)

- **Fix: changing a setting froze the game for about a second** (the "freeze" a player reported, reproduced with skeletons out by changing Cast Time). Each change re-ran every setting on the world's thread and wrote all 880 values again, changed or not; Kiln's step stats show the mod holding one step for 0.85 to 1 s in 1.0.0 and 1.3 to 3.7 s in 1.0.1, after which the game catches up. The Cast Time values (Charge Time and the cast lock) took most of it, 25 to 125 ms per spell. Now a value is written only when it differs from what is in place (`PutNumber`, `PutTime`, `PutFlag`), so a change writes the few values it changes.
- Changes made in a row (a value clicked several times, a slider dragged) wait for one run instead of one each; the run's line names every key that changed.
- The last line of a run says how many values were written and how long it took: `880 values in place (6 written), 0 refused, in 40 ms`.
- **Fix: 1.0.1's Cast Time floors were too high** in game (Chain Lightning x0.8, Lightning Wisp x0.95, and floors on thrown items that need none). Kiln's text for a keep-alive mask evidently names `InRangeCombatShooting` even where the flag is off (most likely every flag with on or off; not seen yet), and 1.0.1 took the name anywhere in the text for set, so the cast's first keep-alive (`InRangeCombat`) counted as its shooting part. The mask is now read by its structure (a list of set flags, an object of flags with on/off, flag names or bits), and the masks of the first cast read are logged once per world (`... keep-alive masks: 0.2 s [...] shooting False; 0.4 s [...] shooting True`) to confirm the format. Expected floors are the ones listed under 1.0.1.
- The casts' floors are read once per world load and kept, not at every change.

## 1.0.1 (not uploaded)

- **Fix: a Cast Time set too low made some spells fail to cast** (reported on Nexus: Chain Lightning under x0.55, Lightning Wisp under x0.3). Cast Time multiplies the cast lock (config `0x41a7d373`), and the cast only stays in its shooting part for that long. A cast's sequence needs its shooting part held from its `InRangeCombatShooting` keep-alive until it releases the spell, or it ends with no spell: Chain Lightning's runs 0.467 s (0.4 to 0.867 s) under a 0.85 s lock, so x0.55 is its lowest; Lightning Wisp's 0.267 s (0.867 to 1.133 s) under 1 s, so x0.3. Both match what players found.
- Each spell's Cast Time now stops at that floor, worked out at every world load from the game's own data: the item's cast lock and its casts' events (the first `KeepAliveEvent` needing `InRangeCombatShooting`, then the later of the first spawn, `SpawnImpact` or `SpawnEntityEvent`, and the first `PayUsageCost`), rounded up to the setting's 0.05 steps. The saved value is kept; a lower one applies as the floor, and the log line says so (`Chain Lightning Cast Time x0.55, raised from x0.3 (lowest that casts x0.55): 5 items`). The General card's Cast Time is held to each spell's floor the same way.
- Floors in the game data of build 26430625: Light Burst, Ice Bolt x0.2; Fireball, Ball Lightning x0.25; Lightning Wisp, Magic Shield x0.3; Fire Bolt, Shroud Meteor x0.35; Acid Cone, Root x0.4; Fire Pillar x0.5; Chain Lightning x0.55; Blizzard, Skeleton, totems x0.6. Channels (Lightning Channel, Shroud Beam, Bone Channel) and thrown items have no shooting part to hold and keep x0.1.
- A Cast Time setting's tooltip in the settings window names its floor once a world has loaded.
- A failed cast was followed, for the player who reported it, by the game freezing on Escape until alt-tab. The mod does not take Escape (its key listener only takes mouse wheel notches over its own window), so this looks like the game's reaction to the cast that ended early; with no more failed casts it should not happen. Not reproduced here.
- Tests: the floor for Chain Lightning, Lightning Wisp, an exact step, a cast without a limit, and the maximum.

## 1.0.0

- **First release on Nexus, numbered 1.0.0** (the user's choice, 24 Nov 2026). It is what was being prepared as 3.2.2; the 2.x to 3.x numbers below are the mod's history before its first public release. Needs Kiln 0.2.0-dev.31 or newer (mod API 0.2.17).
- **The spellbook moved to its own mod, Grimoire - Spells and Arrows Slots** (asked 24 Nov 2026: Better Spells stays on spell settings, and the book will hold arrows too). Gone from Better Spells: `Spellbook.cs`, `SpellbookStorage.cs`, the Spellbook card and its two settings (`spellbookEnabled`, `spellbookSlots`; a saved settings file keeps them, unread), the book's column beside the settings (the screen is 370 wide again), and the player storage `better-spells:spellbook`, which the user emptied on 24 Nov 2026 at 05:41. The 3.3.0-dev entries below are the spellbook's history; `..\Grimoire\CHANGES.md` goes on from there.
- **The settings window opens with Shift+F3**: F3 is Grimoire's now (the user's choice).
- Kept from 3.3.0-dev.11: every setting's `[Range]` carries its step (Kiln 0.2.0-dev.31, mod API 0.2.17).

## 3.3.0-dev.11 (not released)

- **Kiln player storage test** (Kiln 0.2.0-dev.31, mod API 0.2.17): the book can also live in Kiln's player storage (`Items.RegisterPlayerStorage`, key `better-spells:spellbook`, 32 slots, spells only, kept on death), drawn as a "Storage test" part of the F3 column while **Spellbook** is off: move the book's spells in, store the backpack's spells, take everything back. Kiln 0.2.0-dev.30.7 to 30.9 could not build the storage on a hosted world (kiln#135, fixed in 0.2.0-dev.31); the user saw it ready and spells going in and out on 24 Nov 2026.
- Equipping a stored spell (`Items.Equip` with its storage slot) swaps it with the equipped one: the picked stack goes to the Equipment page's Spell cell and the old spell into the picked slot. In dev.10 the HUD list followed the storage's slots, so its order changed on every pick and next or previous bounced between two spells (seen by the user on 24 Nov 2026). dev.11 keeps the spells' order with the character (`StorageOrder` in the book's data); the equipped spell keeps its place in the list, and picking it again does nothing.
- Every setting's `[Range]` now carries its step (Kiln's `[Range(min, max, step)]`, new in 0.2.0-dev.31): 0.05 for multipliers, 1 for counts and Spellbook Slots, 1.0 for Totem Spread. It changes how Kiln's own sliders move, not saved values.

## 3.3.0-dev.8

- **Spellbook** (asked 23 Nov 2026, replacing Quiver & Spellbook): the spells are kept by the mod with the character (`CharacterData.Carried`, key `spellbook`), one slot per spell up to **Spellbook Slots** (F3, new Spellbook card, 1 to 32, default 12), in the player's order. No book spell takes a backpack slot, the equipped one included (it sits in the Equipment page's Spell cell). **Spellbook** on that card turns it off (the game's own list; the book keeps its spells).
- The HUD's spell list is replaced by the book in its order, then the backpack's other spells (`Ui.AmmoList`, Replace). Picking a book spell equips it with `Items.GiveEquip`; the book spell it replaces goes straight back into its slot (`Replaced.Take`, no free slot needed).
- The book is a column on the right of the settings window (F3): drag a slot to reorder, click to equip, right-click (or drag it to the backpack list) to take it out, Ctrl+click a spell in the backpack (or click or drag it from the panel's list) to add it; Take All Out empties the book into the backpack. A spell already in the book tops up its slot. In dev.7 it was a panel beside the backpack (`Ui.Inventory.AddPanel`); the user found it got in the way of the inventory and character screens and asked for it beside F3 (dev.8).
- Running out: the slot is topped up from what the book still holds of it, else removed and the next book spell equipped.
- Works in a world you host (single player or the host in co-op), as Streamlined Logistics.
- The probe's book (3.3.0-dev.1 to dev.6, key `spellbook-probe`) moves into the spellbook once.

Found by the probe builds (dev.1 to dev.6, 23 Nov 2026, in game):

- `Items.GiveEquip` with `Replaced.Take` swaps eternal and charged spells and keeps their charges.
- A spell equipped from the book (`Items.GiveEquip`, eternal or charged) goes into the Spell cell of the player's Equipment page (`12994:7` in the dev.8 log, next to boots, rings and glider), not into the backpack. So no book spell takes a backpack slot, and swaps work with a full backpack (seen by the user on dev.8 with eternal and charged spells). The probe builds (dev.2, dev.3) saw equipped stacks in backpack pages; probably stacks the game equipped straight from the backpack, not confirmed. `Items.CaptureSet` names it (`StackEquipment.Source`) for a charged spell; an eternal one is a slot marked equipped in `Items.Read`.
- `Ui.AmmoList` in Append mode made the list change shape as book spells entered the backpack (wrap jumps, red rows at 0): Replace with a stable order fixed it.
- Known limit: the game keeps a row at count 0 in its open list for a book spell that left the backpack, and Kiln hands a pick of it to the game, which empties the Spell slot (reopen the list). Kiln's `pick()` treats any item of the game's original rows as the game's own; drafted as bug 20 for Kiln. Keeping visited spells in the backpack instead (dev.4) worked but took one backpack slot per spell passed, which the user turned down. Since the built book (dev.8) its spells never enter the backpack, so the game keeps no such row: the user goes back to visited spells fine, eternal and charged. Bug 20 now only matters for a spell equipped from the backpack.

## 3.2.1

- **Every default is now the game's own** before publishing, as the user asked ("make sure every multiplier starts at its vanilla value"). Were not: Totem, Skeleton, Magic Shield and Lightning Wisp Duration (x10), Blizzard and Ball Lightning Duration (x4), Root, Fire Pillar and Acid Cone Duration (x2), the seven area Radius settings (x1.5), and Invulnerable Totems (on). All are now x1 or off.
- Kept as they were: Max Totems and Max Skeletons default to 1, the game's own cap; Totem Spread (2 units) only places a cast's extra totems, so it does nothing at 1 per cast; Shroud Meteor Visual and Light Burst Visual (on) only make the look follow a Radius other than x1.
- Saved settings files keep their values: Kiln writes every entry to the file, so only a new install gets the new defaults. The user's own file was backed up and is unchanged.

## 3.2.0

- **Cast Time** per spell card and on the General card (x0.1 to x10, step 0.05, lower is better under At Least, like Mana Cost), as the user asked; "cast time" is the spell's Charge Time only. It multiplies the items' Charge Time (config `0x579b2657`) with its tooltip line, and the cast lock (`0x41a7d373`), the values the old Summon Recast Time changed: the spell is ready again sooner or later; the cast's animation keeps its length. On the 18 spells, Skull Vessel and Crawler Bomb (the two scrolls have no Charge Time).
- **Every multiplier goes both ways**: Duration, Damage, Radius, Chain Radius, Lock-On, Ball Lightning's speed and hit rate, Health, Mana Cost and Cast Time now start at x0.1, so players can nerf spells as well as buff them. A lifetime below x1 is the game's lifetime times the multiplier (above x1 the tooltip's share is still added).
- **0.05 steps for every multiplier** (Shift and Ctrl give 10 and 100 steps, so 0.5 and 5).
- The settings screen is 760 high (was 720) for the extra Cast Time line.
- 113 settings.

> [!note]
> Hot-reloaded into the user's running game on 22 Nov 2026 at 08:06 (880 values in place, 0 refused), not yet seen.

## 3.1.6

- **Shift and Ctrl make a click or a wheel notch worth more steps**: Shift 10, Ctrl 100, on − and + and on the wheel over a value (the result stays inside the setting's range). Asked by the user, mostly for Mana Cost's 0.05 steps: Shift moves it by 0.5, Ctrl by 5. Kiln's key listener reports a wheel notch with its modifiers, and the mod follows Shift and Ctrl's own presses and releases for the clicks (the game still gets them). The hint line says so.

> [!note]
> Hot-reloaded into the user's running game on 22 Nov 2026 at 07:57 (631 values in place, 0 refused), not yet seen.

## 3.1.5

- **Mana Cost goes up to x10** (per spell and General; still from x0.1, step 0.05), so a cost can follow a larger Per Cast, as the user asked (5 Lightning Wisps per cast with x5 cost).
- 3.1.4 seen in game: the user reports Keep Older Wisps working.

> [!note]
> Hot-reloaded into the user's running game on 22 Nov 2026 at 07:55 (631 values in place, 0 refused).

## 3.1.4

- **Keep Older Wisps** (`keepOlderWisps`, off by default, on the Lightning Wisp card). The user asked for a Max setting for Lightning Wisps, whose game cap is 1. The game keeps no count: the cast's script (`bb5a73d7`) first destroys the caster's wisps its 20-unit sphere touches, then spawns the new wisp. Offered a toggle that stops the removal or a real cap the mod would keep by despawning the oldest wisps, the user chose the toggle for now and asked for the cap in `TODO.md`.
- While on, each of the cast's four `SpawnImpact` spheres gets a 0.05-unit radius and a new physics shape key (as Chain Lightning Radius does). It still touches the caster, whose collision lets the script go on to the spawn, but no older wisp. This should also keep Lightning Wisps Per Cast from removing its own wisps.
- 92 settings.

> [!warning]
> Hot-reloaded into the user's running game on 22 Nov 2026 at 07:52 (`reloaded better-spells 3.1.4`, 631 values in place, 0 refused), not yet seen. Not known until seen: whether the script still spawns the wisp with the tiny sphere (it spawns after handling its first collisions, and the caster is expected to be one of them).

## 3.1.3

- **Lightning Wisps Per Cast and Fire Wisps Per Use** (`lightningWispsPerCast`, `fireWispsPerUse`, 1 to 5, default 1, on the Lightning Wisp and Fire Wisp Scroll cards), asked by the user.
  - The Fire Wisp scroll's cast (`UseItem_Scroll_Companion_FireWisp`) spawns its wisp with one `SpawnEntityEvent`: its counts are raised like the summon casts'.
  - The Lightning Wisp's cast (`Attack_Player_Staff_Overlay_ShockWisp`) has no spawn event: four `SpawnImpact` events, one per tier (each runs only for its tier), place a script (`bb5a73d7`) that spawns the tier's wisp and destroys the caster's older wisps its sphere touches (`filterEntityTag`, `destroyEntity`). The setting raises the counts of all four events. Not known until seen: whether the wisps of one cast remove each other.
  - Both get the fixed 1-unit spread, since wisps move by themselves.
- `SummonCast` now names the kind of event and how many of them a cast has. 91 settings.

> [!info]
> Hot-reloaded into the user's running game on 22 Nov 2026 at 07:45 (`reloaded better-spells 3.1.3`, 623 values in place, 0 refused). Seen in game: with Lightning Wisps Per Cast at 5, a cast places 5 wisps, and a new cast removes the earlier ones and places 5 again. So one cast's wisps don't remove each other: the script removes what its sphere touches before the cast's new wisps exist. The user found that good enough and dropped the exact cap from `TODO.md`.

## 3.1.2

- **Skull Vessels Per Use** (`skullVesselsPerCast`, 1 to 5, default 1, on the Skull Vessel card as "Per Use"), asked by the user. The vessel is thrown by the cast `UseItem_Consumable_Tool_SkullSummon_Throw` (shared by every tier), whose one `SpawnEntityEvent` throws one vessel; the vessel's projectile spawns the item's skull (its `0xf6a785cd` template) where it lands. The setting raises that event's counts like Skeletons Per Cast, with a fixed 1-unit spread, so one use throws several vessels for one vessel's mana.

## 3.1.1

- **An Items row for items that act like spells**, asked by the user, who approved the four I proposed (every non-spell item that costs mana, without the two prayer scrolls, which are buffs):

  | Card | Items | Settings |
  | --- | --- | --- |
  | Skull Vessel | `Consumable_Necromancer_Grenade_T2` to `T6` | Mana Cost, Duration, Damage, Health |
  | Fire Wisp Scroll | `Consumable_Scroll_Fire_Wisp_T4_Lvl27` | Mana Cost, Duration, Damage, Health |
  | Meteor Shower Scroll | `Consumable_Scroll_Fog_Meteor_Shower` | Mana Cost, Damage |
  | Crawler Bomb | `Consumable_CritterCrawler_Bomb_Grenade_T5` | Mana Cost, Duration, Damage, Health |

  Mana Cost as for spells; Damage on the items' `damageSetup.dmgMod`; Duration on the items' time value (config `0x663d391d`, which the summoning scripts read as `LifeTime`), its tooltip line, and the summoned template's `LifeTime`; Health on the summoned template, as for the summon spells (the skulls', crawlers' and wisp's level scaling factor). The summoned template is the one the item's `0xf6a785cd` config names (`Player_Companion_FlyingSkull_Throwable_T2` to `T6`, `Enemy_Companion_CritterCrawler`), and for the Fire Wisp scroll, whose cast spawns it, `Enemy_Companion_FireWisp`. The General card covers them too.
- **Fix: the Mode buttons' labels were cut off** (the user's screenshot of 3.1.0). Each button is now as wide as the longer label plus its margin.
- The screen is 720 high (3.1.0 measured its contents at most 651 with the Totems card; the Items row adds one row).
- 88 settings; the tests check each Items card's item count and the Fire Wisp's template.

> [!note]
> 3.1.1 and 3.1.2 hot-reloaded into the user's running game on 22 Nov 2026 at 07:26 and 07:27 (608 values in place, 0 refused at the defaults), not yet seen.

## 3.1.0

- **Mana Cost for every spell.** Each spell's card starts with Mana Cost (x0.1 to x2, step 0.05, default x1; key: the spell's name in camel case + `CostMultiplier`, such as `fireBoltCostMultiplier`). It sets the items' `impactValues.ManaCost` (Kiln's alias for config `0xaf236ece`), whole and never below 1, and the tooltip line `locaId` 3193644544. The heal spells and Mana Leech have no card and keep their cost.
- **A General card for every spell at once.** A "General" row above the element rows opens it: Mana Cost, Damage, Duration, Radius and Health, each applying to every per-spell setting of that kind (keys `generalCostMultiplier`, `generalDamageMultiplier`, `generalDurationMultiplier`, `generalRadiusMultiplier`, `generalHealthMultiplier`), with the per-spell ranges. Lock-On, Speed, Hit Rate, Per Cast, Max and Spread stay per spell.
- **Mode, Multiply | At Least** (`generalAtLeast`, off = Multiply): Multiply applies both (General x2 and Fireball x3 give x6); At Least keeps the better of the two (General x2 lifts anything below x2, Fireball stays x3), where for a cost the better is the lower (General x0.5 lowers any spell above x0.5). Kiln has no segmented control, so it is two buttons side by side, the chosen one in the accent color.
- All asked by the user; asked for the details, the user approved the proposal as it stood (per-spell cost range, a General card, one mode for the card, cheaper wins for cost, the per-spell ranges).
- The screen is 800 high at scale 1 for the General row and the longer cards (Totems has 9 lines). 74 settings.

> [!note]
> Hot-reloaded into the user's running game on 22 Nov 2026 at about 07:25 (`reloaded better-spells 3.1.0`, 553 values in place, 0 refused at the defaults), not yet seen.

## 3.0.19

- **Five new settings, asked by the user.** 50 settings now.

  | Setting | Key | Card | Range, Default | What It Changes |
  | --- | --- | --- | --- | --- |
  | Max Totems | `maxTotems` | Totems | 1 to 10, 1 | `MaxTotemCount.dataStorage[0]` of the player's templates (`1_Player`, `1_Player_AG2`) |
  | Max Skeletons | `maxSkeletons` | Skeleton | 1 to 10, 1 | `MaxSkeletonSpellCount.dataStorage[0]` of the same |
  | Totem Health | `totemHealthMultiplier` | Totems | x1 to x10, step 0.5, x1 | `Health.dataStorage[2]` (Health_Max_Base, 662) of the totems' placed templates |
  | Skeleton Health | `skeletonHealthMultiplier` | Skeleton | x1 to x10, step 0.5, x1 | `EnemyScaling.enemyLevelScaling.healthScaleFactor` (2.5) of the skeletons' placed templates |
  | Magic Shield Health | `magicShieldHealthMultiplier` | Magic Shield | x1 to x10, step 0.5, x1 | `Health.dataStorage[0]` and `[2]` (1200 or 1500) of the shield's placed templates, and the item's tooltip line that shows it |

- Where the values come from: Kiln's records of the game's programs name `limited_entity_spawn`, which reads the player's `MaxTotemCount` and `MaxSkeletonSpellCount` attributes; the player's templates hold 1 in each, and skills add to it, so the settings set the value before skills, as the user asked. The attributes are copied into the player when it spawns, so Max Totems and Max Skeletons apply at the next world load. In a Health attribute, slot 0 is the health an entity starts with and slot 2 its base maximum (Kiln's notes); skeletons have neither, their health comes from their level, so their level scaling factor is multiplied instead, which is not shown to scale health in proportion.
- **The settings screen is wider**: 370 x 700 at scale 1. 3.0.18's 290 cut off the fourth icon of the Fire and Others rows: the measurement didn't count the icon buttons' frames. The mod now measures the room its contents take from the items actually drawn (the widest item's right edge plus the left margin) and logs it as `settings screen contents: W x H needed`.
- The cards are no longer padded to the longest card's height: a screen has a fixed size anyway.

> [!warning]
> Hot-reloaded into the user's running game on 22 Nov 2026 at 07:09 (`reloaded better-spells 3.0.19`, 387 values in place, 0 refused; at x1 and 1 every new value is the game's). Not yet seen: whether Max Totems and Max Skeletons take effect after a world load (the game's `maxtotemcount_init` program might set the attribute from elsewhere), whether Skeleton Health scales health in proportion, and the screen's width.

## 3.0.18

- **The settings screen is sized to its contents**: 290 x 645 at scale 1 (was 420 x 660). 3.0.17's log measured its contents at 242 x 601 at scale 1 (user's overlay scale 1.5); the width adds the screen's padding, the height a third line for the hint, which wraps more at the narrower width.
- **Built against Kiln 0.2.0-dev.30.4**, which the user installed (30.3 added most of what we asked Kiln's author for). It compiles and the tests pass unchanged. Read for Better Spells in the 30.2 to 30.4 notes: nothing that changes it. No step for config sliders and no way to place a window yet; 30.3's pointer and drag calls (`UiDraw.Pointer`, `ButtonHeld`, `DragSource`, `DropTarget`) aren't needed here; 30.4 only fixes kiln mode for ShroudForge mods. The notes promise "richer UI windows" for 0.2.0-dev.31.

> [!note]
> Installed on 22 Nov 2026 at about 04:35 with the game closed, for the user's next start on Kiln 30.4; not yet seen.

## 3.0.17

- **The settings window is now a Kiln screen, so it opens centered.** The user asked for the window to open centered (it opened top left). A Kiln window's place is kept by a number Kiln gives windows in the order they are made (`###kiln.w<N>` in `kiln\data\kiln.ui\imgui.ini`), and its "start centered" option only applies to a number with no kept place, so it rarely did. Offered a Kiln screen or a window made again at every opening, the user chose to try the screen.
- A screen opens centered every time, closes with Escape or its close button, and has the mouse and keyboard while open (so a click can't also swing the staff). The background is not dimmed. F3 and the button on the mod's page open it as before.
- A screen has a fixed size: 420 x 660 at scale 1, meant to fit the longest card (Totems) under the six icon rows. The hint line now wraps to the width. The mod logs the room its contents take (`settings screen contents: W x H …`) whenever it changes, to tune that size.

> [!note]
> Hot-reloaded into the user's running game on 22 Nov 2026 at 03:52 (`reloaded better-spells 3.0.17`, 363 values in place, 0 refused), not yet seen; the content size is logged at the first opening.

## 3.0.16

- **Chain Lightning Lock-On Range is capped at x2.** The user tried x10: no difference, so the game enforces the 42 the developers' reflection gives `itemTargetingSetup.targetingRange` (24 at x1; x1.75 already reaches it). The user chose x2 as the cap. A saved value above x2 lands on x2.

> [!note]
> Copied over the drop-in on 22 Nov 2026 at 03:50; the hot reload waits in `kiln\control` until the game is unpaused (the user had set x1.75 by then).

## 3.0.15

- **Chain Lightning Radius is no longer marked experimental**: the user says it works.
- **Chain Lightning Lock-On Range goes up to x10** (step 0.25), so the user can see more plainly whether it works; the user isn't sure it does. Its description says "units".
- What the game's data says about it: the staff's target selection (`keen::ecs::TargetSelectionSettings`, `staffTargetSelectionParams`) drops candidates outside the spell's own range (`discardOutsideAmmoRange` true), and the staff's sticky aim uses that range too, so `itemTargetingSetup.targetingRange` is the field that matters. But the developers' reflection gives that field an editor range of 0 to 42 (24 at x1, 72 at the user's x3): if the game also holds it to 42 at run time, anything above x1.75 does no more.

> [!note]
> Hot-reloaded into the user's running game on 22 Nov 2026 at 03:46 (`reloaded better-spells 3.0.15`, 363 values in place, 0 refused; Lock-On Range x3: 24 -> 72).

## 3.0.14

- **Chain Lightning Radius goes up to x10** (step 0.25, still marked experimental). The user, after the hit log: keep it, since it clearly reaches farther even if the full stated distance isn't proven, and allow x10 for players who want even more range. Its description now says "units", like Totem Spread.
- **The temporary hit log is removed**, with the references to Kiln.Combat and Kiln.World it needed, so Kiln no longer holds game hits for it.

> [!note]
> Hot-reloaded into the user's running game on 22 Nov 2026 at 03:44 (`reloaded better-spells 3.0.14`, 363 values in place, 0 refused).

## 3.0.13

- **The temporary hit log groups hits by cast.** 3.0.12's first test showed that the hit's source is the caster's staff for every cast (the same entity, with no position), so "distance from the source" can't be measured. Hits less than a second apart now count as one cast, and each line gives the cast's number, the hit's number in it, the Chain Lightning Radius in force, and how far apart that cast's hits landed so far (`hit log: cast N hit K (chain radius xR): … the cast's hits so far span D units`). For Chain Lightning that span is at most the sphere's diameter plus the enemies' size: about 14 to 16 units at x1, about 70 at x5.
- What 3.0.12's test showed (8 hits): at x5, one cast hit one enemy (03:32:32); the user then went down to x1 with the wheel (03:32:49). At x1 one cast hit 4 enemies within 0.35 s, one after another (the jumps), up to 15.8 units from the first one, which fits a 7-unit sphere around a point between them. There is no x5 cast into a group yet.

> [!info]
> Loaded on 22 Nov 2026 at 03:39 once the game was unpaused (Kiln ran the waiting reload). The user's second test, 8 casts: at x1 a cast's hits landed at most 3 units apart; at x5 two casts' hits spread over up to 19.3 units, more than a 7-unit sphere allows (about 16 with the enemies' size), so the area grows. Whether it reaches the full 35 units can't be told from where these enemies stood. The locked target is hit twice per cast (once directly by the script, once through the sphere), which is the game's script. The user's earlier x5 casts were most likely made before 3.0.12 loaded at 03:30.

## 3.0.12

- **Temporary hit log, to measure Chain Lightning's reach.** The user reports that Chain Lightning Radius at x5 reaches farther and still jumps (the Shroudforge attempt stopped the jumps), but nothing shows how far. Offered a hit log, a debug sphere or a test by eye, the user chose the log.
- Every hit whose direct source is not the attacker itself (a spell, a projectile, a weapon) is logged on the world's thread with the target's distance from the source (for Chain Lightning, the impact placed on the target) and from the first target that source hit: `hit log: source … hit … for N: D units from the source, E from the source's first target`. Up to 400 lines per load. Kiln's `EntityHurtEvent`; the mod now references Kiln.Combat and Kiln.World.
- While someone subscribes to `EntityHurtEvent`, Kiln holds the game's hits one step before they apply (its notes). That is about 16 ms, and it goes with the log.

> [!warning]
> Temporary: remove the log (and the two references) once the reach is measured. Hot-reloaded into the user's running game on 22 Nov 2026 at 03:30 (`hit log on`, 363 values in place, 0 refused).

## 3.0.11

- **Totem Spread goes up to 25, in steps of 1, and reads in units.** The user: the distance doesn't seem to be in metres, so "m" tells the player little; "units" says it is whatever Enshrouded counts as a metre. Range 0 to 25, step 1, default 2 (1.5 is not on a step any more; a saved 1.5 lands on 2). The skeletons' fixed spread is 1 unit. The value's room in the window is as wide as "00 units".

> [!note]
> Hot-reloaded into the user's running game on 22 Nov 2026 at 03:12 (`reloaded better-spells 3.0.11`, 363 values in place, 0 refused): skeletons 4 per cast at 1 unit, totems 4 per cast at the user's 5 units. The same log also answers part of 3.0.10: the user had set Chain Lightning Radius to x5, and Kiln took the new shape key (`1744623269 -> 1053016604`) with no refusal. Whether the area widened is the user's to say.

## 3.0.10

- **New experimental setting Chain Lightning Radius** (key `chainLightningRadiusMultiplier`, x1 to x5, step 0.25, default x1, on the Chain Lightning card as "Radius (Experimental)"). Asked whether Chain Lightning's jumps can be changed now that the game's data can be read, I answered from the dump and the user said yes.
- What the data shows: all five Chain Lightning items use the cast `Attack_Player_Staff_FixedAnim_ChainLightning`, whose one `SpawnImpact` event places the spell's script (program `e28f0d79`) with a Combat sphere of its own, radius 7 m, on the aimed or locked target. The script hits every enemy in that sphere once (`filterConsecutiveHits`, a faction mask) and the locked target; the arcs are effects spawned per hit. There is no jump range and no jump count: a wider sphere is the only way to reach more enemies.
- Why the Shroudforge attempt (Live 1.0.1 and 1.0.2, radius only) likely failed: next to the sphere, `joltObjects[0].hash` (1744623269) is the key the game keeps the built physics shape under, and Kiln's notes say a shape is reused by its key (`docs\kiln\modding\world-blocks.md`, for props). A new radius under the old key gets the old 7 m shape back. Live 1.0.2 gave the shape a new key but was never tried in game.
- What the setting does: the sphere's radius times the setting, and a new key for each size (FNV-1a of the game's key and the multiplier, never the game's own; `SpellRules.ShapeKey`). At x1 both are the game's.
- 45 settings.

> [!warning]
> Hot-reloaded into the user's running game on 22 Nov 2026 at 03:10 (`reloaded better-spells 3.0.10`, 363 values in place, 0 refused). At x1 the log shows Kiln reads both values (`shape key 1744623269 -> 1744623269`, radius 7). Not known until set above x1: whether Kiln takes a new key, whether the game builds a new shape for a key it has never seen, and whether the area really widens.

## 3.0.9

- **The mouse wheel steps a value**: a notch up over a setting's "− value +" raises it by one step, a notch down lowers it. The user asked for it, since the window doesn't scroll. Toggles are left to clicks.
- How: Kiln's key listener (`KeyInput.Listen`, with the wheel's notches) runs on the game's window thread before the game gets the notch. While the window was drawn in the last 250 ms with the mouse over a value, the notch is counted for that setting and kept from the game (so the camera doesn't zoom); otherwise it goes on to the game. The window applies the counted notches at its next frame, through the same path as a click.
- The window's hint line says so.

> [!info]
> Hot-reloaded into the user's running game on 22 Nov 2026 at 03:06 (`reloaded better-spells 3.0.9`, 361 values in place, 0 refused). Seen in game: "Yes, working."

## 3.0.8

- **Fix: the changed-value dots were drawn over the icons' tooltips.** The user reported it from 3.0.7 (hovering Fire Bolt, the dot of an icon in the row below showed through its tooltip). Cause, read in Kiln's native UI: the plain `DrawRect` and `DrawText` draw on ImGui's foreground list, over every window and tooltip. The dots, the frame on the chosen spell and every text drawn into a line's slot (names, labels, values, since 3.0.4) now use the layered forms with `UiLayer.Window`, which the window's own drawing order covers. The frame is now 1.5 units thick.
- Everything else in the user's screenshot looked as designed: the frame on the chosen spell, the cards.

> [!info]
> Hot-reloaded into the user's running game on 22 Nov 2026 at 03:02 (`reloaded better-spells 3.0.8`, 361 values in place, 0 refused). Seen in game: the user confirmed the fix, and also that Enemies Ignore Totems (3.0.6) works.

## 3.0.7

- **The settings window is grouped by element, as icon tabs.** The user asked for the spells by element (Fire, Ice, Lightning, Shroud, Others), with Summons and Casting folded into their spells. Shown a table layout, spell cards and expanding rows, then a mockup of the cards, the user proposed icons that work like tabs, approved a clickable mockup of it, and said to build it.
  - Each section is one row of spell icons: Fire, Ice, Lightning, Shroud, then **Summons** (Totems, Skeleton, Lightning Wisp: the user put Totems and Skeleton there because what they do counts more than their element; Lightning Wisp is a summoned companion too) and Others (Acid Cone, Root, Bone Channel, Magic Shield).
  - Clicking an icon opens that spell's card below the rows, with one "Label − value +" line per setting; the window keeps the height of the longest card (Totems, six lines), so it does not jump. The chosen spell is framed, a dot marks a spell with a value changed from its default, and hovering an icon shows the spell's name. The chosen spell is kept while the game runs.
  - The former Summons and Casting settings are on their spells' cards: Lock-On Range on Chain Lightning, Speed and Hit Rate on Ball Lightning, the Visual toggles on Shroud Meteor and Light Burst, Per Cast on Totems and Skeleton, Invulnerable and Enemies Ignore Them on Totems.
  - The icons are image buttons (`ImageButtonTrimmed` with `Ui.ItemImage`). Before a world has loaded there are no icons, so the buttons show the spell's initials.
  - `SpellCatalog.WindowSections` defines the window; the tests check that every setting is on exactly one card, the section order, and that every spell finds an icon in the game data.
- **Summon Spread is the totems' only** (label Totem Spread, key `summonSpread` kept). The user: skeletons move by themselves, so a fixed 1 m keeps them from landing bunched up. Skeletons now get 1 m whenever more than one is placed per cast.
- **Summon Recast Time is removed.** The user: it was a stopgap from before Summons Per Cast. The Skeleton and totem items' Charge Time, its tooltip line and the cast lock keep the game's own values again (42 values fewer: 361 in place). Kiln keeps the unknown key in `better-spells.json` (the saved x0.75) and logs `unknown key(s) kept: summonRecastMultiplier` at every load; deleting that line from the file silences it.
- 44 settings.

> [!warning]
> Hot-reloaded into the user's running game on 22 Nov 2026 at 02:42 (`reloaded better-spells 3.0.7`, 361 values in place, 0 refused, skeletons 4 per cast at 1 m, totems 4 per cast at 1.5 m), not yet seen. Not known until seen: the frame and the dot drawn over the icon buttons, the icons' size, and whether the card's area really keeps one height.

## 3.0.6

- **New toggle Enemies Ignore Totems** (key `enemiesIgnoreTotems`, off by default, in the window's second table under Invulnerable Totems): enemies no longer pick totems as targets, so totems stop working as decoys. It is separate from Invulnerable Totems and applies to totems placed afterwards. Skeletons and wisps stay targets.
- Why: the user asked whether what Lasting Spells 1.0.2 to 1.0.4 tried by editing and removing targeting components (and never achieved) is doable now that the game's code and data can be read. Asked with the plan below, the user said to go ahead.
- How enemies choose, from the game's code (Kiln `docs\re\npc-internals.md`, and the client's functions decompiled in `D:\Dev\Enshrouded\Ghidra\out`): for each candidate they see (filter `0x37d2f0`, called from the sight gather `0x380700`), and for each entity behind an event such as a hit (`0x380200`, in `enemy_combat_targets` `0x37ee90`), an enemy drops factions 0, 1 and 9 (Environment, HitAll, HitOthers) and then asks "would my faction fight yours" (`Hostile`, `0xea2e50`). The components the old attempts changed are not asked. Totems are `PlayerAlly`, like skeletons and wisps, and enemies fight `PlayerAlly`.
- What the toggle does:
  1. The 10 totems' placed templates get `Faction.faction` = `AutomatedPlayer`. In the game's own rule that faction is on the players' side with Player and PlayerAlly, so a totem and its shots (which copy its faction) still treat players and summons as friends and enemies as enemies; one template of the game uses it, `Player_Vukah_Melee`.
  2. Kiln's creature AI service (`Factions.Relate`, kiln.npcai, which answers the game's faction checks) makes the nine factions the totems' own script shoots at (Fogger, Vukah, Scavenger, Wildlife, WildBeast, Skeleton, Cyclops, Drak, Ancients) neutral toward `AutomatedPlayer`, one way. It is set again at every world load, since Kiln drops the table when a world stops.
  - Off: the templates' faction is given back and the nine relations go back to the game's. Without Kiln's service the faction is left alone and the log says so.
- 45 settings now; the tests' counts follow.

> [!warning]
> Seen in game on 22 Nov 2026: "totems ignore works" (the user, with Invulnerable Totems off). Kiln took the faction and the relations at the first run (403 values in place, 0 refused). The original note: installed on 21 Nov 2026 at 12:26 with the game closed, never run: whether Kiln takes a faction's name for `Faction.faction` and the nine relations is in the first log (`Totem Duration …, enemies ignore totems on`, and no warning line). Not known until seen: whether enemies really leave totems alone, also after a totem hit them (the code says both paths ask the same check), and whether anything else treats `AutomatedPlayer` differently from `PlayerAlly` (enemy area attacks, heals, the player's lock-on).

## 3.0.5

- **The toggles' checkboxes are centered horizontally in their cells**, on the user's request: a checkbox sits in the middle of the room a "− value +" control takes, so under the values of the other rows. Like its vertical place, this starts with the second frame, once the checkbox's size has been read.
- Asked whether the mod is now feature-complete relative to the HANDOFF file: yes. Its goal was one live mod in which every setting applies while playing, with the saved values kept and no startup mod left, and its Steps Left were the Duration group with Invulnerable Totems, the Damage group, Summon Recast Time, and retiring the startup mod. All 44 settings are live since 3.0.0 with the saved values, and neither Shroudforge mod runs. Left over from retiring: `mods\better-spells-live.7z` in the game folder, the disabled Better Spells in Vortex, and the Shroudforge sources, tests and probes in this folder (`TODO.md`).

> [!note]
> Hot-reloaded into the user's running game on 21 Nov 2026 at 12:02 (`reloaded better-spells 3.0.5`, 393 values in place, 0 refused), not yet seen. The request itself came after 3.0.4 was in the game, without a remark on its rows.
>
> Seen in the log: the first run after a world load or a reload takes about 2 s on the world's thread (1.8 s at the 11:45 load, 2.4 s after this reload), the second 26 ms. At a load it is hidden by the loading; after a hot reload the game stalls for that long.

## 3.0.4

- **The pause menu entry is removed**, on the user's request. The window opens with F3 or with the button on the mod's page in Kiln's menu.
- **Rows are centered vertically, and − and + are squares of the row's height.** The user reported the row texts sitting at the top of their rows and the − and + sitting high inside their buttons, and asked for square buttons that use the full row height.
  - A row is 26 units of the overlay's scale high. The icon and the − and + buttons are squares of that height (`Button` with a width and height, whose label the overlay centers).
  - Each text (spell name, setting name, value) is drawn into the middle of a slot of the row's height: a spacer takes the place (`Space`), and the text is drawn at the slot's rectangle (`LastItem`, `DrawText`). A text widget can't be used: it sits at the top of the row, or lower by an amount that depends on what was drawn before it on the line.
  - A checkbox is moved down to the row's middle from the second frame on, once its own height is known.
  - The tooltip of a value or a setting name now covers its whole slot.
- The two tables have new ids again (`spell-rows-4`, `other-settings-4`), since the columns' contents changed width.

> [!warning]
> Hot-reloaded into the user's running game on 21 Nov 2026 at 11:55 (`reloaded better-spells 3.0.4`, 393 values in place again, 0 refused), not yet seen. Not known until seen: whether the texts drawn into their slots look centered, and whether a tooltip shows over a slot.

## 3.0.3

- **Fix: the settings window's controls did not line up.** In 3.0.2, as the user's screenshot showed, the − button could not be seen, the values looked boxed, and the values and + buttons did not read as one "− value +" group; Lightning Channel's name was also cut off.
- Cause, read from Kiln's bundled ImGui: 3.0.2 placed the value and the + with `SameLine(offset)`, taking the offset to count from the cell's left edge. Inside a table it counts from the cell's edge minus the window's indent, so both landed too far left: the value was drawn on top of the − button (the box seen around it was that button), and the + over the value's end.
- Now nothing is placed by an offset. The cell is "−", then the value centered in the room of the widest value by the space left before and after it (`SameLine(0, spacing)`), then "+". The + buttons of a column line up because every value takes the same room.
- The two tables have new ids (`spell-rows-3`, `other-settings-3`). Kiln's tables are resizable and the overlay keeps a table's column widths by id, so widths kept from the earlier layouts no longer apply; this is the likely cause of the cut-off name, not confirmed.
- Not changed: the blank area right of the Summons And Casting table. Kiln's tables always take the window's width, and that table has two columns.

> [!warning]
> Hot-reloaded into the user's running game on 21 Nov 2026 at 06:18 (`reloaded better-spells 3.0.3`). Seen in game: "Better Spells menu works now." The screenshot itself could not be opened from this session: the diagnosis rested on the coordinator's description of it and on ImGui's source.

## 3.0.2

- **The settings window shows each spell's icon**, in a new first column. Asked whether that is possible; Kiln draws the game's own item icons (`UiDraw.ItemIconTrimmed`, as Remote Chests does). A row's icon is the icon of the spell's first item by name, so its lowest tier; Totems shows the Fire Totem's. The items are found when a world loads, so the column is empty until then.
- **A setting's − and + are no longer columns of their own.** Asked whether they have to be: no, 3.0.1 only used table columns to be sure of the alignment. Each setting is now one cell holding "− value +", with the value and the + at fixed places in the cell, so a column's + buttons still line up. The buttons are small ones, as tall as a line of text, so the value sits level with them. The window goes from ten columns to five.
- The tests also check that every row finds an item for its icon in the game data dump.

> [!warning]
> Installed on 21 Nov 2026 at 05:30 with the game closed. Seen in game by the user: the icons show, but the controls don't line up. The assumption that `SameLine` counts from the cell's left edge was wrong (see 3.0.3).

## 3.0.1

- **A dedicated settings window** replaces the − and + rows on the mod's page in Kiln's menu, which worked but looked messy: Kiln's config page is very raw. Asked for the layout (a row per spell, or tabs by group) and how it opens, the user chose a row per spell, and a button on the mod's page plus a pause menu entry plus the hotkey **F3** (free in Kiln's key bindings: F1 is Kiln's menu, F2 Remote Chests, F4 the auto loot menu; it can be changed in the Kiln menu's Keys tab, binding `better-spells.window`).
- The window (`Ui.Window`, sized to its content): a table with a row per spell and its Duration, Damage and Radius controls side by side, then a second table, Summons And Casting, for the ten settings that are none of those three. A value that differs from its default is drawn in the accent color, and hovering a control shows the setting's description and default.
- `SpellCatalog.WindowRows` and `WindowOtherSettings` say what the window shows; the tests check that it has each of the 44 settings once and that each column holds a setting of its group.
- The mod's page in Kiln's menu keeps Kiln's free sliders and now only has an Open Settings Window button under them.
- Installed over the running game through Kiln's own hot reload (`reload better-spells` in `kiln\control`): 323 fields put back, 3.0.1 loaded, and 393 values in place again with 0 refused, without a restart.

> [!note]
> The user's reaction to 3.0.1 was two questions (an icon per spell, and whether − and + need their own columns), answered by 3.0.2. Whether F3 and the pause menu entry open the window was not reported.

## 3.0.0

- **Better Spells is now a Kiln mod, and every setting applies while playing**, to what is cast afterwards: the 28 settings of Better Spells 2.0.9 and the 16 of Better Spells Live 1.0.5, with the keys, ranges, defaults and steps they had in F9.
- Why: the Kiln proof passed in game, and the user approved the full port (name Better Spells 3.0.0 with the proof mod removed, everything in one step, the saved F9 values copied over once).
- **Settings page with steps.** Kiln has no step for its sliders: a ranged number is a free slider with a field to type in, which the user found hardly usable. Asked how the steps should work (snap only, own page with − and + buttons, own page with dropdowns), the user chose the buttons. The mod adds its own page under Kiln's (`Ui.AddConfigPage`): one row per setting, a step per click, in the groups Summons, Duration, Damage, Radius and Casting. Kiln's own sliders stay above it, and a value set there lands on the nearest step and is written back.
- What each group changes, through `Content.ItemData`, `Content.Templates` and `Content.Sequences`, always from the game's own value (`Original`), and given back (`Reset`) at a setting's neutral value:

  | Group | Values |
  | --- | --- |
  | Summons Per Cast, Summon Spread | `minSpawnCount`, `maxSpawnCount` and `randomSpawnRadius.maxRadius` of the one `SpawnEntityEvent` of the four summon casts |
  | Duration (9) | The item's tooltip line 609674671, and `LifeTime.lifeTime` of the template the item places, which gains the time the tooltip gains (Acid Cone keeps its 1 s gap) |
  | Invulnerable Totems | `CombatFlags.immune` and `handleHits` of the totems' placed templates |
  | Damage (17) | `damageSetup.dmgMod` of the spell's items |
  | Radius (7), 2 Visual toggles | Every sphere's or capsule's radius in the spell's templates; the VFX word that holds a zone's size, where it equals the area; Root's reach (config `0x6c2fad6d`); the transforms' scales while the toggle is on; on the items the tooltip line 3386387946 and the ground cursor (config `0x5258131e`, not Shroud Meteor's) |
  | Ball Lightning | `spawnVelocity.z` of its cast, the larger sphere of each ball template, and the wait between two hits (config `0xe21b4b72`, divided) |
  | Summon Recast Time | Charge Time (config `0x579b2657`) with its tooltip line 1551270780, and the cast lock (config `0x41a7d373`), of the Skeleton and totem items |
  | Chain Lightning Lock-On Range | `itemTargetingSetup.targetingRange` of its items |

- **Changed against the Shroudforge mods**, from the review of the HANDOFF against the game's data:
  1. Root Duration and Blizzard Duration also multiply the zone's `DebuffDuration` (config `0x130a0ef2`), which the zone's script passes to `applyBuff`: the hold and the slow now last as long as the zone. The user's decision.
  2. The items' Time and Float configs with id 1674724000 are no longer written. Nothing reads them; the tooltip lines are written directly.
- **No GUID lists.** Items, templates and casts are found by name (`SpellCatalog.cs`), and each item names the template it places. `tests\make_radius_spell_list.py` and the All Spells Probe are no longer needed for it.
- The log (`<game>\kiln\logs\kiln.log`, lines of `better-spells`) has one line per setting: its value, what was found, how many values are in place, and the first of them as `game's own -> now`. Anything Kiln refuses or the mod leaves alone is a warning line of its own. It ends with `N values in place, M refused`.
- Tests (`tests\SpellRulesTests`): the steps and every calculation; that the 44 entries of the settings file and the settings list agree in key, kind, range, default and label; and every name pattern against the game data dump (102 spell items; 38 duration items; the 17 damage patterns; 32 radius items and 43 templates; 3 balls; 5 casts; 5 Chain Lightning items; 14 summon items).
- The user's saved F9 values were written to `better-spells.json` once (from the staged Better Spells 2.0.9 and the last settings line Better Spells Live logged): Totem, Skeleton, Magic Shield and Wisp duration x10, Blizzard and Lightning Ball x4, Root, Fire Pillar and Acid Cone x2; Light Burst damage x10, Wisp and Skeleton x2; radii Fireball x5, Ice Bolt x2, Root x3, Fire Pillar x4, Blizzard x3, Shroud Meteor x5, Light Burst x5, Ball Lightning x2; 4 skeletons and 4 totems per cast, spread 5 m; recast x0.25; lock-on x3; ball speed x4 and hit rate x2; all toggles on.
- The proof mod (drop-in `better-spells-kiln` and its settings) was moved to `%APPDATA%\Vortex\enshrouded-mod-backups\2026-11-21 Better Spells Kiln Proof 0.1.0 removed`. The user had already disabled Better Spells in Vortex and packed `mods\better-spells-live` into a `.7z`, so neither Shroudforge mod runs.

> [!info]
> First run, read from the log (game started 21 Nov 2026 00:14, world loaded 00:15:44): 104 spell items (the game's 102 and two that a content mod adds, which no pattern takes) and 46 area and ball templates found; every setting found what the tests expect (38 duration items and 36 placed templates, 32 radius items and 43 templates, 3 balls, 4 summon casts, 14 summon items, 5 Chain Lightning items); **393 values in place, 0 refused**, no warning line, at the saved values. Kiln took every kind of value that was new for it: a cast's spawn event (1 to 4 per cast, ball speed 2.5 to 10), a VFX word, the transforms' scales, and config values by id. The first run took 1.4 s, the second (Kiln calls `Content.OnReady` once per world of a hosting game) 24 ms.
>
> Seen in that log: Skeleton's Charge Time is 0.5 s before this mod, not the game's 2 s, because Unified Tweaks' Spell Cast Time x0.25 is applied at startup. Summon Recast Time multiplies what it finds (0.5 to 0.125 s), as the Shroudforge mod did.

> [!info]
> Seen in game on 21 Nov 2026, the user's answers to four checks:
> 1. The − and + rows of the mod's page work, but the page looks messy: Kiln's config page is very raw. The user asks for a dedicated window, which can be laid out properly.
> 2. A value set on one of Kiln's sliders lands on the nearest step.
> 3. The spells work (summons per cast, radii with their look). Shroud Meteor's ground cursor stays small, as intended since its cursor range was rolled back (the crater's script reads it and the terrain destruction hitched the game). The user wants that looked into as a follow-up.
> 4. Root Duration now also lengthens the hold on an enemy.

> [!note]
> Installed on 21 Nov 2026 at 00:15, with the user's game started seconds before: the install script refused, the folder was copied into `kiln\mods` by hand, and that start loaded it (`loaded 3.0.0: 44 settings` at 00:15:09). Not proven on Kiln before this version, and all taken in that run: fields of a cast's spawn event (summons per cast, spread, ball speed), a VFX word, a transform's scale, config values addressed by id, and the mod's own settings page. A value Kiln refuses only affects its own setting.

## Kiln Proof 0.1.0

- **New Kiln mod with three of Better Spells' settings**, applied while playing to totems placed afterwards. They are in the Kiln menu (Mods, in the pause menu) and in `%LOCALAPPDATA%\Kiln\profiles\my-mods\config\better-spells-kiln.json`, which Kiln reloads when saved:

  | Setting | Key | Range | Default | What It Changes |
  | --- | --- | --- | --- | --- |
  | Totem Duration | `totemDurationMultiplier` | x1 to x10 | x10 | `LifeTime.lifeTime` of the template each totem item places, and the item's tooltip line `locaId` 609674671 |
  | Invulnerable Totems | `invulnerableTotems` | on / off | on | `CombatFlags.immune` true and `CombatFlags.handleHits` false on those templates |
  | Totem Damage | `totemDamageMultiplier` | x1 to x50 | x1 | `damageSetup.dmgMod` of the totem items |

- Why: the HANDOFF was reviewed against the offline game data in `D:\Dev\Enshrouded`, and Kiln's game data service (`kiln.gamedata`, already running in the game next to Shroudforge) does by design what the live Lua mod does by hand. Asked whether to port now, stay on Shroudforge, or prove it first on a small mod, the user chose the proof, with Totem Damage as its Damage setting.
- How it works: at every world load and after every settings change it finds the totem spells by item name (`^Ammo_T\d+_Spell_[A-Za-z]+_Totem_`, 10 in the game's data), follows each item's `equipment.placedEntity` to its template, and sets each value from the game's own (`Original`) through `Content.ItemData` and `Content.Templates`. A setting at x1 or off gives the value back (`Reset`). Kiln keeps the changes as the mod's layer and takes them off when the mod unloads. There is no GUID list.
- Left out on purpose: the item's Time config (`configId` 1674724000). The review found that nothing reads it: no Impact program does, Kiln's notes call it a tooltip-only slot, and Lasting Spells 1.0.0 changed it in game without effect.
- The log (`<game>\kiln\logs\kiln.log`, lines of `better-spells-kiln`) names the settings, the items and templates found, each value as `game's own -> now`, and anything Kiln refused, ending with `N values in place, M refused`. Expected at the defaults: 10 items, 10 templates, 50 values in place, 0 refused.
- `tests\SpellRulesTests` checks the name rule, the ranges and the three calculations, and the name rule against the 102 spell items of the game data dump.
- Kiln multiplies what the game loaded, so a setting must not be applied by the Shroudforge startup mod at the same time. The user disabled Better Spells 2.0.9 in Vortex for the proof; Better Spells Live 1.0.5 stays on and touches none of these values.

> [!info]
> Seen in game on 20 Nov 2026 (game started 23:25, world loaded 23:27). The user reports that Totem Duration and Totem Damage work and that Invulnerable Totems works when on. So the proof passes, and Better Spells moves to Kiln.
>
> The log of that run agrees: the mod loaded in 64 ms with side `both`; at the world load it found 10 totem items and 10 placed templates and put 50 values in place with 0 refused, twice (Kiln calls `Content.OnReady` once for the hosted world and once for the client's); the first tier's Ice Totem reads lifetime 8 s to 80 s, immune false to true, handleHits true to false, tooltip 8000 to 80000 ms. At 23:30:38 Totem Damage was set to x50 in the Kiln menu and all 50 values were in place 8 ms later (damage modifier 0.33 to 16.5), with no restart.

> [!warning]
> Not seen yet: Invulnerable Totems turned off again (the user didn't try it), and Totem Duration changed in the Kiln menu between two casts (the log has one change from the menu, Totem Damage). An earlier save from the menu at 23:29:43 counted 2 changes but changed no value.
>
> Found in that run: Kiln's generated settings page has no steps. A number with a range is a free slider (rounded to 4 decimals) with a field to type in, which the user finds hardly usable. And the log's lifetime line ends in `s s` (Kiln already writes the unit).

## Live 1.0.5

- **The seven Radius settings and the two Visual toggles move here from Better Spells**, in a new F9 group Radius, with the names, ranges (x1 to x5, step 0.25) and defaults (x1.5, toggles on) they had. They now apply while playing, to spells cast afterwards.
- Why: Better Spells' settings are moving to live in stages. Ball Lightning Radius and Hit Rate (Live 1.0.4) were the test for it, and both passed in game, also when changed between two casts. Asked by AskUserQuestion what moves next, the user chose the Radius group (over the Duration group, both, or everything at once).
- What each setting changes, the same values Better Spells changed at startup:

  | Value | Where | Spells |
  |---|---|---|
  | Area | The radius of every sphere or capsule in the spell's templates | All seven |
  | VFX Size | The VFX parameter that equals the area's radius | Ice Bolt's zone (9th), Root (4th), Fire Pillar (1st), Blizzard (1st) |
  | Reach | Float config 1815063917 in the template | Root |
  | Tooltip | The Radius line and the item's Float config that matches it | All but Light Burst, which shows none |
  | Ground Preview | Float config 1381503774 in the item | Root, Fire Pillar, Blizzard (not Shroud Meteor: its crater's script reads it) |
  | Explosion's Scale | The scale of the template's transforms, while the toggle is on | Shroud Meteor, Light Burst |

- Shroud Meteor's blast is still sized from its tooltip: 5 m times the setting, so 5 m at x1, where the game's own is 3 m. Turning the mod off puts 3 m back.
- How:
  - `RADIUS_SPELLS` in `mod.lua` lists each spell's items and templates by guid (32 items, 43 templates), because the mod looks them up by guid in the loaded world. `tests\make_radius_spell_list.py` writes that list from the All Spells Probe's export (`--write` puts it into `mod.lua`); it finds a spell's templates the way Better Spells found them at startup.
  - A value is now a field with its own rule. A template's first area works out how many times the template grows; its other areas, its VFX size, its reach and its transforms grow as much. A VFX size is written as float bits, a tooltip as a whole number, a transform only while its toggle is on.
  - All of a spell's items and templates are needed before any is changed, so the tiers stay alike.
  - A float the game already holds as closely as a float32 can is not written again.
  - After a write the log names only the casts and settings whose values changed (it named all of them), and the effect report counts settings: `4 of 4 summon casts and 11 of 11 other settings found`.
- Left alone, with one log line:
  - The whole spell: an item or a template that isn't loaded (looked up again after 2 s), a template without a sphere or capsule, an area that can't be bound.
  - Only that value: a VFX size that doesn't match its area, a tooltip radius that two configs match, a following value that can't be bound.
- `Scripts\Build.ps1 -Install` now carries a setting that moved between the two mods over with its saved value, as long as both are installed in one run. The user's radii came from Better Spells' saved settings that way: Fireball x5, Ice Bolt x2, Root x3, Fire Pillar x4, Blizzard x3, Shroud Meteor x5, Light Burst x5, both toggles on.
- Installed with Better Spells 2.0.9 on 20 Nov 2026 at 21:03. The user had started the game 40 s earlier, so that run loaded 2.0.8 and Live 1.0.4 (its log shows 2.0.8's radius lines and Live 1.0.4's settings line): the radii of that run are the startup mod's, once, and the new pair loads at the next start. Until then, F9 changes to Better Spells Live don't reach the loaded mod.
- `tests\test_live.py` builds every listed item and template in its fake world and checks each setting against them: which values change and which stay, the limits, the toggles, x1 and unloading, a missing item or template, what is left alone, a new world, and the float bits without `string.pack`.

> [!danger]
> Needs Better Spells 2.0.9 or later. With 2.0.8 or older both mods multiply each radius.

> [!note]
> First run with both versions (20 Nov 2026, game started 21:19, world loaded 20:21:13 UTC), from the log:
> - Startup: no `pregame phase failed`, no `Asset mods failed`; Better Spells 2.0.9 logged no radius line.
> - Every setting was found with the game's own sizes (Fireball 2.6 to 6.2 m, Shroud Meteor 3 m, and so on), so nothing is multiplied twice.
> - Every value could be bound, the VFX size words, the transform scales and the tooltip lines included: no value was refused and no note was logged.
> - All 196 values (24 of the older settings, 172 of the radius spells) were written within 13.3 s of the world loading, in 36 updates, without an uncertain write. The areas ended at the saved multipliers (Fireball 13 to 31 m, Ice Bolt 2 to 5.6 m, Root 4.5 to 9 m, Fire Pillar 6 to 12 m, Blizzard 9 to 18 m, Shroud Meteor 25 m, Light Burst 30, 13 and 35 m).
> - Nothing was written again in the 21 minutes that followed, so no value is rewritten at every update.

> [!note]
> Seen in game in that run: asked whether the areas, their visuals and the tooltips follow, the user answered that all is working. So a zone's VFX size and an explosion's scale written in the loaded world are used by what is spawned afterwards, and the tooltips show the multiplied radius.

> [!warning]
> That run's log has no F9 save, up to 21:45: every value was the one saved before the start, written once after the world loaded. So a radius changed in F9 between two casts is not shown for these seven spells yet (Ball Lightning Radius showed it for an area in Live 1.0.4, not for a VFX size, a scale or a tooltip). Stutter was not asked about.

> [!note]
> Before the user chose, I said the live mod can't search spells by name. That was wrong: `game.assets.get_loaded_resources_by_type` lists what a world has loaded (the Chain Lightning probe used it). The fixed list was kept for this version, as the approved plan and with no new unknown; searching by name stays an option for the last stage, where it would spare the list.

## Live 1.0.4

- **Three Ball Lightning settings**, in a new F9 group Ball Lightning, all multipliers with x1 (the game's own) as default, applied while playing to balls cast afterwards:

  | Setting | Range | Game's Own | Changes |
  |---|---|---|---|
  | Ball Lightning Speed | x0.5 to x10, step 0.25 | 2.5 | `spawnVelocity.z` in the spawn event of the cast shared by the three Ball Lightning items |
  | Ball Lightning Radius | x1 to x5, step 0.25 | 4 m | The radius of the Combat-only sphere in the three ball templates |
  | Ball Lightning Hit Rate | x1 to x10, step 0.25 | one hit per 0.5 s | The wait between two hits (Time config 3793439602) in the three ball templates, divided by the setting |

- Why: the user wants a faster ball that keeps its damage per distance travelled, and all three settings to mix and match. An enemy on the ball's path is inside the area for 2 x radius / speed, so Speed and Radius at the same value keep the number of hits it takes, and so do Speed and Hit Rate.
- Where the values come from:
  - Speed: read in game with `tests\cast-probe` (lines in `tests\ball-lightning-cast-output.txt`). The cast's one `keen::actor::SpawnEntityEvent`, at 0.65 s, has `spawnVelocity {0, 0, 2.5}` and `ignoreSpawnVelocity` true. The items also hold `placedEntityLocalSpawnVelocity {0, 0, 28}`, which fits Fireball's speed and not this slow ball, so the cast's value is taken to be the one in use.
  - Radius and wait: the All Spells Probe. Each ball (`Projectile_Player_Staff_LightningBall_1`, `_2`, `_3`) has a sphere of radius 4 that checks against Combat only (the area its script hits enemies in) and one of 0.3 against the world (the ball itself, left alone), and its `keen::ecs::ImpactConfiguration` holds the script's wait of 0.5 s.
- The user chose, by AskUserQuestion: all three in this mod rather than Radius and the hit setting in the startup mod; a rate (x2 = twice as often) rather than an interval; and the ranges above.
- How:
  - The list of multiplied values got three entries; an entry can now divide instead of multiply, write whole numbers, and log its values in another unit (the wait is kept in nanoseconds and logged in seconds).
  - The two template values are found by type in each ball's components and bound with `runtime.overrides.resource` on the loaded template. Nothing is changed in the balls until all three are loaded, so the tiers stay alike.
  - Finding a cast's one spawn event is now one function shared by the summons and the ball's speed.
- Left alone, with one log line: a cast with another name, without exactly one spawn event or without a forward speed; a ball without exactly one such sphere or wait; a value that can't be bound.
- The effect report now reads `4 of 4 summon casts and 12 of 12 other values found`.
- `tests\test_live.py` covers the three settings against the fake runtime (which values, the limits, whole nanoseconds, putting values back, missing balls, odd balls and casts, refused values).

> [!note]
> Seen in game (20 Nov 2026, Speed x2 and Radius x2 saved before the start, Hit Rate at x1): all three values were found and bound, templates included (`components[6]...shape.radius` and `components[12]...simple[1].value.value.value` in the three balls); 5 and 8 m were written within four seconds of the world loading, and the user reports the faster ball and the wider area working. So 2.5 is the speed in use, and a ball spawned after a template value was written in the loaded world takes it.

> [!note]
> Seen in game in a second run the same day (game started 19:01 UTC, world loaded 19:11): the user changed the settings in F9 between casts and reports both Hit Rate and a Radius changed between two casts working. The log shows each change written within two seconds of the save: Radius x2 to x5 (8 to 20 m), Hit Rate x1 to x5 (0.5 to 0.1 s) and back, Radius x4 (16 m), Speed x2 to x4 (5 to 10), Hit Rate x2 (0.25 s), Radius x2 (8 m). So a ball's radius and wait can be changed again after balls have been cast in the same session, and the shape key left as it is does not hold the first size.

> [!info]
> Radius and Hit Rate were the test planned for moving Better Spells to live settings: a value of an object spawned later, changed in the loaded world, and changed again. It passed, so the other Radius and Duration settings can be tried the same way.

## Live 1.0.3

- **Chain Lightning Range removed**, on the user's decision. Chain Lightning Lock-On Range stays, and the F9 group Radius goes with the setting.
- Why: the area never clearly grew in game, and the spell's jumps are unreliable in the game as it is (it sometimes fails to jump between very close enemies), so the user judged it not worth digging further.
- What was seen:
  - First run (1.0.1, sizes changed while playing): the radius was written and kept (35 m at x5), with no clear change in the area.
  - Second run (1.0.1, 35 m and lock-on 72 in place one second after the world loaded): Chain Lightning stopped jumping. Later in the same run it jumped again while the mod's other changes were still in place (casts still gave 4 summons), so the 35 m sphere is not shown to be the cause.
  - The shape key of 1.0.2 was never tried in game.
- Removed from `mod.lua`: the lookup of the cast's impact event, the radius and shape key overrides. The list of multiplied values now has one entry, the lock-on range.
- Log and effect report now read `Chain Lightning's lock-on range` and `5 of 5 Chain Lightning items found`.
- `tests\test_live.py` rewritten without the range; it checks that the setting is gone from the manifest and from `mod.lua`.

> [!note]
> Installing a new version over the running game stops the loaded live mod from taking F9 changes until the next start (Shroudforge logs "Mod changes require the next game start"). The 1.0.2 install did that during the second run, so settings changed after it were saved but not applied.

## Live 1.0.2

- **Chain Lightning Range now also changes the key of the sphere's physics shape**, so the game builds a shape of the new size instead of reusing the 7 m one.
- Why: in game (1.0.1), the log showed the radius written and kept (35 m at x5), but the user saw no clear change in the area.
- What the data shows: a collider lists its physics shapes in `joltObjects`, each a `keen::ecs::JoltObjectSetup` (`flags`, `hash`, `shapeIndexList`). In the All Spells Probe data that hash is different for every sphere radius, so it is made from the collider's content, and most likely it is the key the game keeps a built shape under. A radius changed after loading keeps the old key. Better Spells changes radii at startup, before any shape is built, and leaves the key as it is, which works.
- How: the sphere's one shape entry (the one whose `shapeIndexList` names the sphere) is found, and `...colliderData.joltObjects[1].hash.value` is bound next to the radius. Its value is the game's own key moved by a fixed odd step (2654435761) per hundredth of the multiplier above 1, modulo 2^32: every size has its own key, the same size always the same key, and x1 the game's own.
- The radius is written before the key, so the game never sees a new key with the old size.
- Left alone, with one log line: a sphere without exactly one shape key, a key that can't be bound (the radius is then left alone too, since a larger radius under the old key changes nothing).
- Log lines now end with the key: `chain lightning range: now 35 m, shape key 123456789`.
- The user chose this over a safer two-step test (pointing the sphere at an existing shape's key first), a restart-only check and dropping the setting.
- `tests\test_live.py` covers the key: written after the radius, one per size, back at x1 and on unload, odd shape lists, a refused key.

> [!warning]
> Never checked in game: the setting was removed in Live 1.0.3 before this version was tried. Whether the game builds a shape for a key it doesn't know, and whether collider sizes can be changed while playing, stay open; that matters for moving the Radius settings of Better Spells to live settings.

## Live 1.0.1

- **Two Chain Lightning settings**, both multipliers of the game's own value, default x1, applied while playing:

  | Setting | F9 Group | Range | Changes |
  |---|---|---|---|
  | Chain Lightning Range | Radius | x1 to x5, step 0.25 | The radius (7 m) of the sphere in the cast's `keen::actor::SpawnImpact` event |
  | Chain Lightning Lock-On Range | Casting | x1 to x3, step 0.25 | `itemTargetingSetup.targetingRange` (24) of the five Chain Lightning items |

- Why these two: a read-only in-game probe (`tests\chain-lightning-probe`, its lines in `tests\chain-lightning-probe-output.txt`) showed the spell is an area hit made to look like a chain. Its cast places the spell's script at the aimed spot with a 7 m sphere, and the script hits every enemy in that sphere once: there is no jump range and no jump count to set. The user chose the names (the area is called Range, next to the other Radius settings) and multipliers over metres.
- The sphere sits inside a cast event, so only this mod can change it; the lock-on range is a plain item value and went here too, as the first test of a live item value.
- How:
  - Range: the cast (`12f3cda4-...`, shared by the five items) is looked up in the loaded world, its one `SpawnImpact` event and that event's one `keen::ecs::SphereColliderData` are found by type, and `subSequences[1].events[n].value.colliderData.dataArray[1].value.shape.radius` is bound with `runtime.overrides.resource`.
  - Lock-on: each item is taken from the game's item registry (`game.assets.resolve_loaded_resource("g38_items::ItemRegistry", itemId)`, as Shroudforge's SF Unlimited Flight does), or by guid when the registry doesn't answer, and checked by name. Nothing is changed until all five are loaded, so the tiers stay alike.
  - Both are set to the game's own value times the setting, so x1 puts the game's own back; so does turning the mod off.
- Left alone, with one log line saying why: a cast with another name or without exactly one sphere, a value that can't be bound. A cast or an item that isn't loaded yet is looked up again every 2 s.
- Log lines: `chain lightning range: found at subSequences[1].events[19]... of Attack_Player_Staff_FixedAnim_ChainLightning, the game's own value: 7 m`, `chain lightning lock-on range: found in 5 spell items, the game's own value: 24`, `chain lightning range: now 14 m`. The settings line and the effect report now name the two multipliers as well.
- `tests\test_live.py` covers both settings against the fake runtime (which fields, the limits, putting values back, the registry and the guid lookup, missing items, odd casts).
- This is the first step of moving Better Spells to live settings, on the user's choice: next a placed-object setting as the test for objects spawned later, then the rest group by group.

> [!note]
> Seen in game: `runtime.overrides.resource` accepts the path through a variant inside a cast event's variant, and both values stay as written (the log read 35 m and 72 at the highest settings). The lock-on seems to reach beyond the game's 24, with the game's own line-of-sight check (the user's impression, not measured). The area showed no clear change: see Live 1.0.2.

## Live 1.0.0

- **New mod: Better Spells Live**, for what the startup pass can't write (see 2.0.8). It only runs in a loaded world (`capabilities: ["runtime"]`), so it takes no part in Shroudforge's startup asset pass and can't make it fail. On the user's choices: a separate small mod, summons per cast only (no cast time), separate settings for skeletons and totems.
- Three settings in one F9 group, Summons, all applied while playing:

  | Setting | Range | Default | Changes |
  |---|---|---|---|
  | Skeletons Per Cast | 1 to 5 | 1 | `minSpawnCount` and `maxSpawnCount` of the Skeleton cast's spawn event |
  | Totems Per Cast | 1 to 5 | 1 | The same on the three totem casts |
  | Summon Spread | 0 to 5 m, step 0.5 | 1.5 m | `randomSpawnRadius.maxRadius` of those events, only while their count is above 1 |

- How: each of the four casts (`keen::actor::ActorSequenceResource`, by guid) has one `keen::actor::SpawnEntityEvent`. The mod finds it in the loaded world (`game.assets.get_loaded_resource`), binds the three numbers with `runtime.overrides.resource(resource, "subSequences[1].events[16].value.maxSpawnCount")` and sets them with `overrides:apply`, the way Shroudforge's own SF Crafting Essentials sets the `consume` flag of the same kind of event. Shroudforge keeps the game's own values and puts them back when the mod is turned off or unloaded; at 1 per cast the mod puts them back itself.
- The maximum is raised first and lowered last, so the minimum is never above it between two writes.
- A cast is left alone, with one log line saying why, when its sequence has another name, has no spawn event or more than one, or when a field can't be bound. A sequence that isn't loaded yet is looked up again every 2 s. A new world is searched again. An uncertain write stops all further writes.
- Log lines (`[better-spells-live]` in `shroudforge-client.log`): `settings: 3 skeletons and 1 totems per cast, spread 1.5 m`, `skeleton: spawn event found at subSequences[1].events[16] of ..., the game's own values: 1 to 1 per cast, spread 0 m`, `3 values written`, `skeleton: now 3 to 3 per cast, spread 1.5 m`.
- `tests\test_live.py` runs the mod against a fake runtime built from Shroudforge's API notes: it checks the mod's own logic (which fields, which casts, the order, the limits, putting values back), not that the game accepts the writes.
- `Scripts\Build.ps1` now builds both mods (`-Mod` picks one) and creates a mod's folder in the game's mods folder when the game doesn't have it yet. Better Spells Live is installed that way, not through Vortex; `dist\Better Spells Live 1.0.0.zip` is there for a Vortex install.
- The cast pays its mana once whatever the count. The game's limit on summons is not changed.

> [!note]
> Checked in game on 2026-11-19 at 14:50, with 4 skeletons and 3 totems per cast and a spread of 1.5 m: the user reports it works, so the game does use a count changed after loading. The start was clean (asset pass completed, no failure). The settings were changed at the main menu and applied without a restart (`Runtime settings applied live`). In the world all four spawn events were found, at `events[18]`, `[14]`, `[14]` and `[18]` (the loaded order differs from the startup pass's 16, 12, 12, 16, which is why they are found by type), and the 12 values were written over two updates, 4 then 8, about 0.8 s apart, with no uncertain write. Not reported yet: what happens when a cast would go over the summon limit. It only works where the player's game decides the spawn (solo or hosting).

> [!note]
> Test protocol agreed with the user: saves copied first (`enshrouded-mod-backups\2026-11-19 1445 saves before Better Spells Live`), start the game and stop at the main menu, log checked, load the world, writes checked, then cast. Installed with every setting at the game's own value.

---

## 2.0.9

- **The Radius settings and the two Visual toggles are no longer in this mod.** They are in Better Spells Live 1.0.5, where they apply while playing. This mod no longer changes a collider, a VFX size, Root's reach, a tooltip radius, a ground preview or a transform.
- Removed from `mod.lua`: the list of radius spells, the walk from an item to the templates it leads to, the scaling of templates and items, the float-bits helpers and the radius lines of the log (535 lines). The header keeps what was found about each spell's area, since Better Spells Live builds on it.
- The F9 groups are Duration, Damage and Casting, with 28 settings.
- `tests\test_mod.py`: the Radius checks are replaced by one that saves the old settings at x3 and expects nothing of a spell's area to change, and no log line to name a radius.

> [!danger]
> Install with Better Spells Live 1.0.5 or later: without it there is no radius setting at all. `Scripts\Build.ps1 -Install` installs both and moves the saved radii over.

---

## 2.0.8

- **Fix: 2.0.7 stopped every asset mod from loading, so it is withdrawn.** The mod's code is 2.0.6's again: Summon Recast Time shortens the cooldown and the cast lock, and the cast itself keeps its length.
- What happened at the 14:25 start: 2.0.7 multiplied the times of the cast sequences' events and wrote the events back the way a template's components are written. Its own read-back passed, but when Shroudforge saved the asset pass it failed with `pregame phase failed: at subSequences.0.events.0: expected keen::BlobVariant<keen::SequenceEvent> but found userdata`, then `Asset mods failed; saved baseline restored. Continuing game startup without asset patches`. Sequence events are `keen::BlobVariant` values, which this asset pass can't take back from Lua. The event logging was not the cause: it only read.
- The game then ran without any asset mod for that launch. On the user's request the saves were rolled back with the game closed: `world1.ksd` from `backup-3` (revision 10112, 14:19:34) and `character1.ksd` from `backup-2` (revision 10032, 14:21:05), the newest ones from the launch before. The saves as they were, all their backups and that launch's log are in `%APPDATA%\Vortex\enshrouded-mod-backups\2026-11-19 1432 saves and logs after cast sequence launch`.
- The 83 logged events are kept in `tests\cast-sequence-events.txt`. What they show, the same for Skeleton and the three totems:

  | Time | Event | Meaning |
  |---|---|---|
  | 0 s | `EnableAbilityEvent`, `ShowAmmunition`, `SetAnimationOverlayEvent` | The cast starts |
  | 0.2 s | `LoopEvent` for 0.2 s (`InRangeCombatPreparing`) | Loops while the cast is held |
  | 0.4 s | `DisableAbilityEvent`, `SetAnimationEvent`, movement set to 0 | The caster is locked, the animation starts |
  | 1 s | `PayUsageCost` twice, `SpawnEntityEvent` | Mana is paid and the summon appears at the cursor |
  | 1.08 to 1.27 s | `LoopEvent` for 0.2 s (`InRangeCombatShooting`) | Loops while the button stays held |
  | 1.73 s | `EnableAbilityEvent` (`CanMove`, `CanEvade`, ...) | The caster is free again |
  | 2.35 s | | The sequence ends |

- **Several summons per cast is a pair of numbers:** each of the four `SpawnEntityEvent`s has `minSpawnCount` 1, `maxSpawnCount` 1 and `randomSpawnRadius` 0 to 0. They sit inside the same `keen::BlobVariant` events, so they can't be changed in the asset pass either.
- A possible way for both, not tried: Shroudforge's in-game `game.assets.write_loaded_field`, whose paths go through `BlobVariant.value` and which changes one number at a time in a loaded world. A failed write there can't drop the asset mods. Not known: whether the game uses a changed sequence once it is loaded.

> [!danger]
> Never write sequence events (or anything else that reads as `keen::BlobVariant`) in the startup asset pass. And after installing a changed Shroudforge mod, the log has to be checked for `Asset mods failed` while the game is still at the main menu, before a world is loaded: that check was skipped for 2.0.7.

---

## 2.0.7

> [!danger]
> Withdrawn: it made Shroudforge drop every asset mod at startup, see 2.0.8.

- **Summon Recast Time now also shortens the cast itself** of the Skeleton and totem spells. 2.0.6 in game (x0.25): it helps, but the cast keeps its own length, which limits how fast summons can be filled up.
- A spell item names its cast in `sequences[].sequence`, a `keen::actor::ActorSequenceResource`: sub sequences with a name, a length and a list of events, each event a variant with a `time` and a `duration` in nanoseconds. The setting multiplies the length and every event's time and duration.

  | Spell | Cast Sequence | Game's Own |
  |---|---|---|
  | Skeleton | `Attack_Player_Staff_FixedAnim_Summon_Skeleton` (`11c073c0-9978-44cc-b0bc-f1628cfc55ed`) | 2.35 s, 21 events |
  | Fire Totem | `Attack_Player_Staff_FixedAnim_Totem_Fire` (`7f98e2d6-02e9-482a-acc4-653d0b6c1991`) | 2.35 s, 21 events |
  | Ice Totem | `Attack_Player_Staff_FixedAnim_Totem_Ice` (`f269c36a-ebcd-417e-950a-fc00fb7ba59d`) | 2.35 s, 20 events |
  | Lightning Totem | `Attack_Player_Staff_FixedAnim_Totem_Lightning` (`a3d33eb7-5880-4de5-8326-632efdb7d7db`) | 2.35 s, 21 events |

- Only those 14 spell items use the four sequences (from the all-spells probe's data). A sequence another player spell shares would be changed all the same and named in the log.
- **Every event of those four sequences is written to the log** as the game has it (`cast sequence ...` and `cast event ...` lines after the mod's other lines, about 87 lines per start, also at x1), because what the events hold isn't known yet: the old probe only counted them. The lines are there to learn which event plays the animation, which one places the summon and when the caster may act again; they go away once read (`LOG_CAST_EVENTS` in `mod.lua`).
- Left alone: an event's other times (some events have a follow-up time), since their meaning isn't known.
- The log line becomes `summon recast x0.25: 14 spells (cooldown 0.7 -> 0.175, 2 -> 0.5 s; cast lock 1 -> 0.25); 4 cast sequences (2.35 -> 0.588 s, 83 events)`.
- The test covers the length and the events' times (each sequence once, also when two spells share it), the follow-up time and another spell's sequence staying, a shared sequence being named, a missing one counting as an error, and the logged events.

> [!note]
> Several summons per cast: the game's spawn events have `minSpawnCount`, `maxSpawnCount` and `randomSpawnRadius` fields (`keen::actor::SpawnEntityBaseEvent`). Whether the Skeleton's cast uses such an event will show in the logged events.

---

## 2.0.6

- **New Summon Recast Time setting**, in a new Casting group of the F9 menu: x0.1 to x1 in steps of 0.05, default x1 (the game's own timing), applied on the next start. It makes the Skeleton and totem spells ready again sooner, so filling up your summons takes less time. Their maximum stays as it is.
- It multiplies two values on each of those spell items:

  | Value | Where | Game's Own |
  |---|---|---|
  | Cooldown | Float config `1469785687` (seconds), shown by the tooltip line with locaId `1551270780` (milliseconds) | 2 s for Skeleton, 0.7 s for the totems |
  | Cast lock | Float config `1101517683`, the timing 2.0.3 took for damage: in game, raising it kept the caster locked in the cast for longer | 1 for all of them |

- New log line: `summon recast x0.25: 14 spells (cooldown 0.7 -> 0.175, 2 -> 0.5 s; cast lock 1 -> 0.25)`, or `summon recast x1: unchanged`.
- What was looked at for summoning several skeletons per cast, which this replaces for now: a spell item places exactly one entity per cast (`equipment.placedEntity`), so several per cast would need a spawner built from the game's scripts. The limit itself is a player attribute (`keen::ecs::MaxSkeletonSpellCount`, and `MaxTotemCount` for totems) on the player template (`c3db66d6-89a5-4a6c-a8e3-d9a1f6009812`), with the summons tagged by a `keen::ecs::SpawnLimit` pool; it's left alone.
- The test covers the cooldown, its tooltip line and the cast lock for Skeleton and totem spells only, and the 0.1 and 1 limits.

> [!note]
> Checked in game at x0.25: it helps, and the log shows `summon recast x0.25: 14 spells (cooldown 0.7 -> 0.175, 2 -> 0.5 s; cast lock 1 -> 0.25)` with no errors. The cast itself keeps its length, which 2.0.7 takes on.

> [!note]
> Installed with the setting at x0.25 in the game's own settings file, to save a restart; the mod's default stays x1.

---

## 2.0.5

- **Fix: Shroud Meteor no longer hitches the game.** Since 2.0.4 each meteor froze the game for about a second, like a script catching up. 2.0.4 multiplied the item's Float config `1381503774` to widen the ground preview, and for the meteor that config is also what its crater's script (`Terraform_Explosion_Meteor`) reads as its range. The item's value reaches that script, so at Shroud Meteor Radius x5 every meteor ran a terrain operation over 25 m instead of 5 m. The meteor's item value is left alone again, so its crater and its ground preview keep the game's own 5 m. Its blast still follows the setting.
- The other three area spells (Root, Fire Pillar, Blizzard) keep their widened previews: none of their scripts reads that config.
- **The Damage settings now go up to x50** (x10 before). Light Burst's damage modifier is 0.12, so x10 only brought it to about a Fireball's.
- The test checks that the meteor's item value stays at every setting, and the new limit.

> [!note]
> This also corrects 2.0.0's notes: the crater's size is the item's value (5 m, the tooltip's radius), not the 3 m on the crater's own template.

> [!warning]
> Not yet checked in game: that the hitch is gone. Shroud Meteor's preview shows 5 m while its blast reaches the setting's radius (25 m at x5).

---

## 2.0.4

- **Fix: the Damage settings now change damage.** 2.0.3 multiplied a Float config no program names (configId `1101517683`). In game, damage stayed the same and the caster stayed locked in the cast for longer instead: about 2 s after Light Burst at x5. So that value is a cast timing (0.75 for Fire Bolt, 1 for Fireball, Blizzard, Lightning Ball and Light Burst, 1.25 for Fire Pillar and Ice Bolt, 1.9 for Shroud Meteor), and it's left alone again.
- The probe's data holds the real value: each spell item has a `damageSetup` like a weapon's, with `dmgMod`, `speedMod` and a share for each damage type. Each Damage setting now multiplies `dmgMod`:

  | Spell | Game's `dmgMod` |
  |---|---|
  | Fire Bolt | 0.91 |
  | Fireball | 1.08 |
  | Ice Bolt | 0.82 |
  | Acid Cone | 0.85 |
  | Fire Pillar | 0.2 |
  | Blizzard | 0.1 |
  | Lightning Ball | 0.82 |
  | Chain Lightning | 1.12 to 1.22 |
  | Shroud Meteor | 0.67 |
  | Root | 1 |
  | Light Burst | 0.12 |
  | Fire, Ice and Lightning Totem | 0.35, 0.33 and 0.3 |
  | Lightning Wisp | 0.36 |
  | Skeleton | 1 |
  | Bone Channel | 0.63 |
  | Shroud Beam | 0.43 |
  | Lightning Channel | 0.56 to 0.61 |

- **Three new Damage settings** for the beam spells, which have a `dmgMod` too: Bone Channel, Shroud Beam and Lightning Channel (x1 by default). Mana Leech deals no damage (`dmgMod` 0) and the heals keep theirs.
- **Cast preview, second try.** 2.0.3 grew the item's copy of its placed entity's colliders; all 17 copies were written and the ground preview stayed the game's own, so that change is gone. The four area spells aimed at the ground (Root, Fire Pillar, Blizzard, Shroud Meteor) are the only spells whose item carries Float config `1381503774`, and it equals their tooltip's radius. Programs call that id RangeTerraforming, but none of these spells' scripts reads it from the item, so it's most likely the ground cursor's radius, shared with the terrain tools. Each Radius setting now multiplies it. The same config on a template, such as Shroud Meteor's crater, still stays.
- Log lines: a damage line now reads `fire bolt damage x2: 8 spells (0.91 -> 1.82)`, and `N cast previews` counts the items whose cursor range changed.
- The test covers `dmgMod` for cast spells and a beam, that the cast timing config stays, and the cursor range for every radius case while a template's stays.

> [!note]
> Checked in game after release (19 Nov 2026): the Damage settings raise damage and the ground previews show the widened area, with Light Burst Damage x5 (`0.12 -> 0.6`), Lightning Wisp x2, Skeleton x2 and the Radius settings at x3 to x5 (the log shows 4 cast previews each for Root, Fire Pillar and Blizzard and 2 for Shroud Meteor, and no errors). So `damageSetup.dmgMod` is the spell's damage modifier, also for what a spell summons, and item config `1381503774` is the ground cursor's radius. Found afterwards: each Shroud Meteor hitched the game for about a second, because its crater's script reads the same value. Fixed in 2.0.5.

> [!note]
> Installed with `Scripts\Build.ps1 -Install` while the game ran, so it applies at the next start. The saved Damage values (Light Burst x5, Lightning Ball x2, Skeleton x2) are kept and now multiply `dmgMod`.
>
> `Build.ps1 -Install` now reads the saved F9 values from the game's own `mods\better-spells\extended.mod.json` and writes both that file and Vortex's staged copy. Shroudforge replaces the game's file when a setting changes in F9, which breaks its hardlink to the staged one; the first 2.0.4 install only wrote the staged copy, so the game kept 2.0.3's settings file.

---

## 2.0.3

- **New Damage settings**, one per spell, in a new Damage group of the F9 menu: x1 to x10 in steps of 0.25, default x1 (the game's own damage), applied on the next start. Fire Bolt, Fireball, Ice Bolt, Acid Cone, Fire Pillar, Blizzard, Lightning Ball, Chain Lightning, Shroud Meteor, Root, Light Burst, Totem (the three totems share one), Lightning Wisp and Skeleton.
  - Each multiplies the spell item's damage modifier: a Float config (configId `1101517683`) that every cast spell has exactly once and that no impact program names. The scripts take their damage from the weapon, so it's most likely the spell's share of the staff's damage:

    | Game's Modifier | Spells |
    |---|---|
    | 0.75 | Fire Bolt |
    | 0.85 | Chain Lightning |
    | 1 | Fireball, Blizzard, Lightning Ball, Light Burst, the totems, Lightning Wisp, Skeleton |
    | 1.25 | Ice Bolt, Acid Cone, Fire Pillar, Root |
    | 1.9 | Shroud Meteor |

  - The number in a spell's tooltip is its mana cost (configId `2938334926`), not its damage, so no tooltip changes.
  - Beam spells (Bone Channel, Shroud Beam, the heal beams, Lightning Channel, Mana Leech) have no such value and no setting. Heal Chain (1.5) and Magic Shield (1) have one but deal no damage, so they have no setting either.
- **New Light Burst Radius setting** (x1 to x5, default x1.5) in the Radius group. 2.0.0 missed Light Burst because its tooltip shows no radius. It places `Explosion_Player_Spell_LightBurst_Explosion1` (level 17) or `..._Explosion2` (the two Eternal ones) at the caster: two spheres, 6 or 7 m and 2.6 m. Both are multiplied.
- **New Light Burst Visual (Experimental) setting**, on by default: Light Burst's effect has no size parameter, so the explosion's transforms are scaled by as much as its area grew, as Shroud Meteor Visual does.
- **Shroud Meteor Visual** is no longer marked experimental: in game, the explosion looks bigger with it on.
- **Cast preview:** every spell item keeps a copy of its placed entity's colliders (`equipment.placementColliders`, same radii as the template), and the spells aimed at the ground (`crosshairType` Staff_Area) share one cursor effect. The ground preview is most likely sized from that copy, which the Radius settings didn't touch, so it still showed the game's own area. Now, when a Radius setting widens the entity a spell places (Root, Fire Pillar, Blizzard, Shroud Meteor, Light Burst), the item's copy grows by as much. Fireball and Ice Bolt place a projectile, which keeps its size.
- New log lines: one per damage setting, for example `fire bolt damage x2: 8 spells (0.75 -> 1.5)` or `x1: unchanged`, and a note naming spells that don't have exactly one damage modifier. Each radius line adds how many items' copies changed, for example `blizzard radius x3: 4 spells (...); 4 templates (...), 4 VFX sizes, 4 cast previews`. Light Burst's reads `light burst radius x1.5: no tooltip shows a radius; 2 templates (2.6 m -> 3.9 m, 6 m -> 9 m, 7 m -> 10.5 m), visual x1.5, 3 cast previews`.
- All of this comes from a fourth read-only probe mod, kept this time in `tests\all-spells-probe` with its own test (`tests\test_all_spells_probe.py`). It writes every spell item, the templates they place or spawn, the impact programs those run (with the names they give config ids), the buff types and the staffs to `shroudforge\exports\all-spells-probe\`. Its id sorts before `better-spells`, and Shroudforge runs asset mods in that order, so it reads the game's own values.
- The test covers the damage settings (their spells only, the 1 and 10 limits, spells without a modifier), Light Burst's two spheres and its visual on and off, and the items' collider copies for every radius case, the meteor's included.

> [!warning]
> Checked in game after release (19 Nov 2026), with Light Burst Radius x4, Light Burst Damage x5, Lightning Ball and Skeleton Damage x2:
> 1. Light Burst Radius works.
> 2. The Damage settings don't change damage. Config `1101517683` is a cast timing: at x5, Light Burst locked the caster in its cast for about 2 s. Fixed in 2.0.4.
> 3. The ground preview still shows the game's own area, though the log shows all 17 collider copies written. Second try in 2.0.4.

> [!note]
> Installed with the new `Scripts\Build.ps1 -Install`, in place over the staged files (`<Vortex staging>\enshrouded\Better Spells 2.0.1`, hardlinked): the 17 saved F9 values are kept and the 16 new settings start at their defaults. Vortex still lists 2.0.1. Backup in `%APPDATA%\Vortex\enshrouded-mod-backups\2026-11-19 Better Spells 2.0.2 before 2.0.3`.

---

## 2.0.2

- **Shroud Meteor's blast matches its tooltip:** the game's blast is a 3 m sphere while the tooltip says 5 m, and Shroud Meteor Radius multiplied both, so at x5 the tooltip said 25 m while the blast reached 15 m. The blast is now the tooltip's radius times the setting: 5 m at x1 (wider than the game's 3 m), 25 m at x5. The tooltip is read from the spells before it's scaled; if the spells that lead to the blast ever show different radii, it grows from its own radius as before and the log says so.
- **New Shroud Meteor Visual (Experimental) setting**, on by default, in the Radius group: the blast's effect has no size parameter, so the explosion's transforms (`CurrentTransform`, and `RenderTransform` with the two it interpolates between) are scaled by as much as the blast grew (x8.33 at x5, from 3 m to 25 m), in the hope that its effect follows.
- The log line reads, for example, `shroud meteor radius x5: 2 spells (5 m -> 25 m); 1 templates (3 m -> 25 m), blast from the tooltip's 5 m, visual x8.33 (experimental)`. At x1 it no longer says "unchanged", since the blast still grows to 5 m.
- The test covers the blast from the tooltip at x1.5, x1 and x5, the fallback when the tooltips differ, Visual on and off, and that no other template's transform changes.

> [!note]
> Checked in game (19 Nov 2026): with Shroud Meteor Visual on, the explosion looks bigger. Still unknown: whether the scale also widens its collider. If it does, the blast becomes that much larger again (around 200 m at x5): enemies far beyond 25 m get hit. Then turn Shroud Meteor Visual off and the collider has to be divided by the scale instead.

> [!note]
> Installed in place over 2.0.1's staged files (`<Vortex staging>\enshrouded\Better Spells 2.0.1`, hardlinked), keeping the F9 values, since installing a zip in Vortex resets them. Vortex still lists 2.0.1. Backup in `enshrouded-mod-backups\2026-11-18 Better Spells 2.0.1 before 2.0.2`.

---

## 2.0.1

- **Fix:** the Radius settings now really widen each spell's area. 2.0.0 looked for a collider's radius in `dataArray[i].value.radius`, but the game keeps it one level deeper, in `value.shape.radius` (`{radius}` for a sphere, `{radius, length}` for Fire Pillar's capsule). So every one of the 41 templates failed with "has no sphere or capsule collider", and only the tooltips grew. In game, Blizzard at x5 showed 15 m in its tooltip while its area stayed vanilla.
- The test's fake colliders now nest their geometry the way the probe showed. The test passes with the fix and fails on 2.0.0's code.

> [!note]
> Confirmed in game after release (2026-11-16): the Radius settings widen the area, checked with Blizzard. The game builds the shape from the collider's radius; the `joltObjects` hash doesn't hold a cached shape.

> [!note]
> Installing 2.0.1 over 2.0.0 in Vortex brings the settings back to their defaults.

---

## 2.0.0

- **Lasting Spells is now Better Spells**, with a new Shroudforge id, `better-spells`. Vortex sees it as a new mod, so remove Lasting Spells first. Settings keys are unchanged, but a new install starts from the defaults.
- The F9 menu groups the settings: **Duration** (the nine durations and Invulnerable Totems) and **Radius** (the six new settings).
- New **Radius** settings, x1 to x5 in steps of 0.25, default x1.5, applied on the next start, for the six spells whose tooltip shows a radius:

  | Setting | What gets wider | Normal radius | Visual size follows |
  |---|---|---|---|
  | Fireball Radius | The explosion (`Explosion_Player_Spell_Fireball_1` to `_7`) and the one it makes on water | 2.6 to 6.2 m | No size parameter found |
  | Ice Bolt Radius | The burst (`Explosion_Player_Spell_IceBolt_<n>`) and the chill zone it leaves (`Zone_Player_Spell_IceBolt_<n>`) | 1 to 2.8 m | Chill zone: yes |
  | Root Radius | The zone (`Zone_Spell_Rooting_<n>`) and its range for holding enemies | 1.5 to 3 m (range 2 to 3 m) | Yes |
  | Fire Pillar Radius | The pillar (`Zone_Spell_Fire_Pillar_<n>`, a capsule), not its height | 1.5 to 3 m | Yes |
  | Blizzard Radius | The zone (`Zone_Spell_Blizzard_<n>`) | 3 to 6 m | Yes |
  | Shroud Meteor Radius | The explosion (`Explosion_Player_Spell_Meteor_Explosion`, shared by both tiers) | 3 m (the tooltip says 5 m) | No size parameter found |

- What was found, with a third read-only probe mod (`spell-radius-probe`):
  - A spell's area is the collider of an entity it places or spawns: a sphere (a capsule for Fire Pillar) in the template's `ColliderResourceComponent`. It equals the tooltip's radius for every spell but Shroud Meteor.
  - Fireball and Ice Bolt place a projectile whose `ImpactConfiguration` names its explosion in a `TemplateImpactConfig`. Ice Bolt's explosion names its chill zone the same way, and the Fireball item also names its explosion for water. The mod follows those links from each spell item, three deep, and changes each template once.
  - Each zone's `VfxParametersTemplateComponent` has a parameter equal to its radius: Blizzard's and Fire Pillar's 1st, Root's 4th, the chill zone's 9th. It's scaled only where it matches the collider, and logged otherwise.
  - Root's program finds the enemies to hold within a range config (configId `1815063917`). It's scaled too.
- The tooltip's Radius line (locaId `3386387946`, metres x 100) and the Float config it shows change with the area.
- What stays:
  - Projectiles.
  - `RangeTerraforming` (configId `1381503774`), the terrain effect. That includes Shroud Meteor's crater, which keeps its 3 m.
  - Each zone's noise range.
- New log line per setting, for example `blizzard radius x1.5: 4 spells (3 m -> 4.5 m, ...); 4 templates (3 m -> 4.5 m, ...), 4 VFX sizes`, or `x1: unchanged`. Notes follow for spells that show no radius, for tooltips that couldn't be matched, and for VFX sizes left as they were.

> [!warning]
> Not yet checked in game. Each collider also carries a physics hash (`joltObjects`), so if the game builds the shape from a cache instead of from the radius, the area won't grow. The tooltip will show the new radius either way, so check by hitting enemies just outside the old area. Fireball's, Ice Bolt's burst and Shroud Meteor's blasts may also look the same size while hitting wider.

---

## 1.0.6

- **Untouchable Totems** becomes **Invulnerable Totems** (key `invulnerableTotems`, on by default): totems take no damage, but enemies attack them as usual, so they act as decoys.
- Only the Lightning Wisp's `CombatFlags` stay (`immune` true, `handleHits` false). 1.0.2 to 1.0.4's attempts to stop enemies targeting totems are undone, since none worked in game: target importance None on `PlayerCombatTarget` and `ServerTarget`, no hard lock, `EnemyTargetSelectionBonus` 0, and removing `PlayerCombatTarget`, `EnemyTargetSelectionBonus`, `HardLockTarget`, `Destructible` and `ImpactHierarchy`.
- New log line: `invulnerable totems: 10 placed entities`, or `invulnerable totems: off`.

> [!warning]
> Not yet checked in game: `CombatFlags` alone should keep totems from taking damage, as it's the change that made them invulnerable in 1.0.2.

> [!note]
> Installing 1.0.6 over 1.0.5 in Vortex brings all settings back to their defaults.

---

## 1.0.5

- New defaults, taken from the settings in use:

  | Setting | Default |
  |---|---|
  | Totem, Skeleton, Magic Shield and Lightning Wisp Duration | x10 |
  | Blizzard and Lightning Ball Duration | x4 |
  | Root, Fire Pillar and Acid Cone Duration | x2 |
  | Untouchable Totems | on (unchanged) |

- Each spell's fallback in `mod.lua`, used only if Shroudforge's settings can't be read, now matches its default instead of a shared x2. The test checks that the two agree.

> [!note]
> Installing 1.0.5 over 1.0.4 in Vortex brings all settings back to their defaults, which are now these values.

---

## 1.0.4

- In 1.0.3's game the log showed the three components removed from all 10 totem entities, but enemies still walked up to totems and attacked them in melee. **Untouchable Totems** now also removes `Destructible` (`maxHealth` 10) and `ImpactHierarchy`, the remaining components a totem has and a Lightning Wisp doesn't that could plausibly matter. They go in the same removal write.
- What a third read-only probe (`totem-target-probe`) ruled out:
  - Entity tags only group templates. One tag is on all 15 totem templates, one per element is on that element's totems, one is on the 5 wisp templates, and the skeleton's are shared with enemy skeletons and 315 enemy templates. No tag resource type gives their names; the only type with "Tag" in its name is `keen::LocaTagCollectionResource`.
  - `PacifiedState` is also on skeletons, which enemies do attack.
  - The totem's and the wisp's `ImpactConfiguration` hold the same nine configs, and neither impact program mentions aggro or taunting.
- The log line's removed part now lists all five: `removed PlayerCombatTarget 10, EnemyTargetSelectionBonus 10, HardLockTarget 10, Destructible 10, ImpactHierarchy 10`.

> [!warning]
> Checked in game after release: totems still shot, but enemies still attacked them. What's left is the totem's faction (PlayerAlly, like the wisp) and how the enemy AI picks targets, which Shroudforge's Lua can't reach, so 1.0.6 keeps totems as invulnerable decoys instead.

> [!note]
> Installing 1.0.4 over 1.0.3 in Vortex brings all settings back to their defaults.

---

## 1.0.3

- In 1.0.2's game, totems were invulnerable but enemies still attacked them. **Untouchable Totems** now also removes the three components a totem has and a Lightning Wisp doesn't: `PlayerCombatTarget`, `EnemyTargetSelectionBonus` and `HardLockTarget`.
- The removal is a second write after 1.0.2's value changes, read back to check those three are gone and every other component is still there. If the game refuses it, the failure is logged and the totem stays invulnerable.
- The log line adds what was removed: `untouchable totems: 10 placed entities (CombatFlags 10, ...); removed PlayerCombatTarget 10, EnemyTargetSelectionBonus 10, HardLockTarget 10`.

> [!warning]
> Checked in game after release: the removal worked on all 10 totem entities with no errors, but enemies still attacked totems. Continued in 1.0.4.

> [!note]
> Installing 1.0.3 over 1.0.2 in Vortex brings all settings back to their defaults.

---

## 1.0.2

- New **Untouchable Totems** setting (F9 toggle, on by default, applies on the next start): totems can't be hit and enemies don't target them, like Lightning Wisps. It applies to the entity every Fire, Ice and Lightning Totem places, whatever Totem Duration is set to.
- How the totem differs from the wisp in the second probe's output, and what the setting changes on the totem:

  | Component | Totem | Wisp | Untouchable Totems |
  |---|---|---|---|
  | `CombatFlags` | `immune` false, `handleHits` true | `immune` true, `handleHits` false | as the wisp |
  | `PlayerCombatTarget` | `targetImportance` Normal | none | None |
  | `ServerTarget` | `hardLockTarget` true, `targetImportance` Normal | false, None | as the wisp |
  | `EnemyTargetSelectionBonus` | `[1]` | none | `[0]` |
  | `HardLockTarget` | present (empty) | none | kept |

- Values first rather than removing the three components the wisp lacks, since no mod is known to remove components from a template yet. `TargetImportance` is an enum from None (0) to VeryHigh (5); like other enums it's written as its name.
- Each totem entity is changed once and read back; one without `CombatFlags` is reported as a failure. New log line: `untouchable totems: 10 placed entities (CombatFlags 10, PlayerCombatTarget 10, ServerTarget 10, EnemyTargetSelectionBonus 10)`, or `untouchable totems: off`.

> [!warning]
> Checked in game after release: the log showed all 10 totem entities changed, and totems took no damage, but enemies still attacked them. Fixed in 1.0.3 by removing the target components.

> [!note]
> Installing 1.0.2 over 1.0.1 in Vortex brings all settings back to their defaults.

---

## 1.0.1

- In 1.0.0 the tooltips showed the longer durations, but an Ice Totem at x10 still disappeared after its normal 8 s. Every spell places an entity (its `placedEntity`, a `keen::ecs::TemplateResource`) with its own `keen::ecs::LifeTime` component, and that lifetime removes it whatever the spell item says.
- Each of those lifetimes now gains the same time as its spell's main duration, so the placed entity lasts as long as the tooltip says:

  | Setting | Placed entity | Lifetime |
  |---|---|---|
  | Totem Duration | `Prop_Player_Ally_Totem_Projectile_<Element>_<n>` (10 templates) | 8 to 11 s |
  | Skeleton Duration | `Player_Companion_Lesser_Skeleton_Weakling_Necromancy_Spell_<n>` | 60 to 120 s |
  | Magic Shield Duration | `Zone_Spell_MagicShield_<n>` | 45 to 60 s |
  | Lightning Wisp Duration | `Player_Companion_ShockWisp_<n>` | 12 to 18 s |
  | Blizzard Duration | `Zone_Spell_Blizzard_<n>` | 9 to 12 s |
  | Lightning Ball Duration | `Projectile_Player_Staff_LightningBall_<n>` | 6 to 8 s |
  | Root Duration | `Zone_Spell_Rooting_<n>` | 5 to 8 s |
  | Fire Pillar Duration | `Zone_Spell_Fire_Pillar_<n>` | 4.5 to 6 s |
  | Acid Cone Duration | `Explosion_Player_Spell_AcidCone` (one for all three) | 4 s |

- For 35 of the 38 spells the lifetime equals the main duration, so adding the same time multiplies it by the setting. The three Acid Cones share one explosion that lasts 4 s while the tooltip says 5 s; it keeps that 1 s gap (9 s at x2).
- Each placed entity is changed once, even when several spells place it. A placed entity shared by spells that gain different times, one with no lifetime limit (0) or none at all is left as it is and logged. At x1 nothing is written.
- The log line for each setting adds the placed entities and their old and new lifetimes, for example `totems x10: 10 spells (8 s -> 80 s, ...); 10 placed entities (8 s -> 80 s, ...)`. Further lines name spells that place no entity and changed entities that a spell without a setting also places.
- `tostring` on a template shows its component list only as `MappedVariantValue: <address>`, which is why the first probe concluded the lifetime was out of reach. Each component reads as `{type, value}` when indexed. Found with a second temporary read-only probe mod (`template-probe`).

> [!note]
> Installing 1.0.1 over 1.0.0 in Vortex brings all nine settings back to their defaults.

> [!note]
> Confirmed in game after release: the log showed all 38 spells and 36 placed entities changed at x2 with no errors, and an Ice Totem lasted longer than its normal time.

---

## 1.0.0

- Nine settings in Shroudforge's F9 menu, each 1 to 10 in steps of 0.5, default 2, applied on the next start. Each multiplies that spell's main duration and its tooltip line:

  | Setting | Spells | Normal duration |
  |---|---|---|
  | Totem Duration | Fire, Ice and Lightning Totems | 8 to 11 s |
  | Skeleton Duration | Skeleton | 60 to 120 s |
  | Magic Shield Duration | Magic Shield | 45 to 60 s |
  | Lightning Wisp Duration | Lightning Wisp | 12 to 18 s |
  | Blizzard Duration | Blizzard | 9 to 12 s |
  | Lightning Ball Duration | Lightning Ball | 6 to 8 s |
  | Root Duration | Root | 5 to 8 s |
  | Fire Pillar Duration | Fire Pillar | 4.5 to 6 s |
  | Acid Cone Duration | Acid Cone | 5 s |

- Second durations keep their length: Fire Pillar's 9 to 12 s, Root's 9 s, Acid Cone's 10 s and Ice Totem's 5 s, which look like the effect on enemies. Ice Bolt only has such a duration (8 to 14 s), so it has no setting. Cast cooldowns (the 0.3 to 4 s value every spell has) don't change either.
- Spells are `keen::ItemInfo` items named `Ammo_T<n>_Spell_<Spell>_...`. Each keeps its durations as `keen::impact::TimeImpactConfig` values in nanoseconds (value format Duration) in `impactValues.simple`, and shows them in `uiValues` in milliseconds. The main duration is the tooltip line with locaId 609674671; the second one is 2789026239 and the cooldown 1551270780. Every Time config shares configId 1674724000, so the mod finds the main one by matching its value to the main tooltip line. Found with a temporary read-only probe mod.
- One pass over the items, each in its own `pcall`, so a bad item can't fail the asset pass and roll back every other asset mod. Each change is read back after writing.
- Logs under `[lasting-spells]` in `shroudforge\logs\shroudforge-client.log`: one line per setting with how many spells changed and from what to what, plus spells that couldn't be matched and other spells with a duration that have no setting.
- `tests\test_mod.py` runs the mod against a fake Shroudforge asset API (needs lupa).

> [!warning]
> Enshrouded Patcher (Nexus 91), which changes the same durations, also edits the `keen::ecs::LifeTime` component of the totem, skeleton or zone a spell places, and for Fire Pillar its zones, a visual effect and its debuff. 1.0.0 doesn't, and in game the placed entities still disappeared at their normal time. Fixed in 1.0.1.
