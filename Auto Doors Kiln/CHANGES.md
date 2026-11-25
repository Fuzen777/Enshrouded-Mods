# Auto Doors (Kiln)

A rebuild of Auto Doors (SellTheWorldGames, closed source) as a Kiln mod, from its described behaviour, not its code. Doors are toggled by sending the game's own `ToggleInteractionEvent {guest, host, offer}` from the mod's ECS program `door_toggle` (`src\kiln.programs.json`); `interaction_toggle_new` flips `ServerToggleState` and `interaction_directional` swings the door away from the guest.

## 1.2.2

- Locked doors, second try (Fuzen: 1.2.1 still opened them). 1.2.1's `World.Has(door, "InteractionLock")` was false for every door: the lock is template data, not a component the door entity carries, so nothing was ever seen as locked. A door is now locked by its template (`*_Locked_*`, `*_Key`, which are exactly the door templates whose `InteractionLock` has `DestroyLock` in the dump) until it is seen open, which it can only be once picked or opened with its key. Unlocked doors are kept by position in the world's door file (`unlocked`), so a door picked once stays unlocked after a restart. A locked-kind door unlocked before 1.2.2 is left alone until it is opened by hand once. The log names the first 20 locked-kind doors with their template and the sizes of `InteractionLock` and `ClientInteractionLock` on the door, and each unlock.

## 1.2.1

- Locked doors (Fuzen: outside bases the mod opened doors that need a lockpick). The game's locked doors (`*_Locked_Pickable_*`, `*_Locked_Key`, from the template dump) carry `InteractionLock`, and its lockMask `DestroyLock` removes it once the lock is picked or opened with the key. The mod's toggle went around the lock. Doors with `InteractionLock` are now left alone; the door scan (every 5 s) notices the lock is gone and the door works again. The log names the first 20 doors found locked or unlocked.

## 1.2.0

- Nexus release of 1.1.1, no code change (same DLL; only the version in `kiln.mod.json` moved). Player notes in `Changelog.mdbbcode`.

## 1.1.1

- Menus closing by themselves (a user's report, reproduced by Fuzen: a survivor walking through a door closed the player's backpack, storage or crafting menu). After the survivor had gone, the close accepted the door's offer with the nearest player as guest, and an accept as a player ends that player's open interaction and its menu. Closes now toggle with the door's last accepted guest (the survivor) while it exists, with no new accept. No door event names a player whose `InFullScreenUi` or `ClientUiIsInLootingMenu` flag is set: the move waits 0.5 s and tries again, or uses another player; the build camera's guest skips such players too. The log says so for the first 10 waits.

## 1.1.0

- Nexus release of 1.0.1 and 1.0.2, no code change (the DLL is 1.0.2's; only the version in `kiln.mod.json` moved). From now on a Nexus upload of a published mod takes the next minor version and a build that is not uploaded takes a patch; `Changelog.mdbbcode` holds the player-facing notes of each upload.

## 1.0.2

- Lighter on the world's thread (Fuzen's FPS check: the hosted world ran at 49-57 steps/s and ~17% of its thread was Kiln's whole-world entity pass, much of it from this mod). Every search the mod makes (`Creatures.Near`, the door query, `Spawn.FindByName`) walks the world's whole entity table: Kiln has no spatial index, so a centre and radius do not make one cheaper, and one search per base would cost more than one for the world.
- Survivors and animals: one search every 0.5 s (was two every 4 steps, 30 a second), and only for a player within 60 m of a door a survivor or an animal could open (a base door in Everyone or Players & Survivors mode, or any door with Bases Only off). Between searches the creatures found are followed by their position, so a survivor walking up to a door still opens it at once; one that arrives from beyond 60 m is seen within 0.5 s.
- Doors: the door query runs every 5 s (was 2.5 s), every 1 s while no door is known (was every 4 steps).
- Secret pieces: the name search runs every 30 s and only with a base (was every 2.5 s); in between the pieces found are kept while they exist. Pieces outside a base are no longer kept at all (the mod never moved them). A secret piece placed in a base works within 30 s.

## 1.0.1

- Tall doors and the build camera (Fuzen: the Round Ancient Door only opened for the camera near its foot). Height Reach was measured from a door's foot only, and the Round Ancient Door is 6 m tall. The build camera now also counts anywhere up to the top of a doorway taller than the Height Reach. Door heights are the top of each door template's solid colliders, from the game data (Large doors 3.5 m, Round doors 3 m, Round Ancient 6 m, ...). Players, survivors and animals keep the plain Height Reach, so a floor above a tall door does not open it.

## 1.0.0

- First Nexus release: 0.3.0 with its version set to 1.0.0, no code change. Fuzen tested secret doors (0.2.11) and the settings window (0.3.0).

## 0.3.0

- Release build. Needs Kiln 0.2.0-dev.31 or newer (mod API 0.2.17).
- Distances are in metres: Open Distance, Close Distance and Height Reach are decimals with `[Range(min, max, 0.1)]`, so Kiln's sliders move in steps of 0.1 m. New config keys `openDistance`, `closeDistance`, `heightReach`; the old `...Dm` keys are not read (they stay in the file, marked Unknown key, and can be deleted), so distances set before go back to their defaults once. The settings window's - and + move 0.1 m (Shift: 0.5 m).
- Secret pieces, from 0.2.11's log (Fuzen: all work now): the toggle query found none of them, the name search found them all. The mod now finds them by name only, and the diagnostic log is gone.
- Door label and Kiln dev.31's menu hiding: dev.31 hides world-position draws (`RenderFrameEvent.World`) while a game menu is open, but never pixel draws (`Draw`). The label is a pixel draw under the crosshair, so it keeps its own check (HUD in game, no Kiln menu or window) and nothing changes for it.

## 0.2.11

- Secret pieces (Fuzen: 0.2.10 gave no message and no change on a secret door, a bookcase and a secret floor). Secret pieces are now also found by template name ("Secret" in the name), not only through the toggle components. For its first three scans with a base loaded, the log lists what it finds: `auto doors: secret pieces: N found; ...` with each candidate's id, template, whether it toggles and whether it is in a base.

## 0.2.10

- Secret pieces (Fuzen): Close Only is their default; the Door Mode key cycles Close Only, Players (opens and closes for players and the build camera, like a door), Manual. They only ever work inside a base, also with Bases Only off. The world's door list stores Players and Manual for them; Close Only is not listed.
- 0.2.9 was never installed (published only).

## 0.2.9

- Build camera swing, from 0.2.8's log: a toggle only moves a door when its guest is the door's last accepted guest (InteractionOfferRuntime.lastAcceptData.guestId, +24). Survivors' toggles did nothing until accepted, then the door swung away from them; the player toggled doors from 25 m away. So the mod now accepts with the guest first whenever the door's last accepted guest is someone else (no more 15-step wait on a failed toggle), for every open and close. Camera guests: a player, survivor or animal on the camera's side at any distance, else another door on that side at any distance (0.2.8's 8 m found none: neighbouring doors share the wall's plane). A door guest that is not accepted falls back to you, and that kind is skipped after two failures.
- Secret pieces (Fuzen): Base_Secret_Door, Shelf, Floor, Stair and Mechanism in a base. Manual by default; the Door Mode key switches between Manual and Close Only (never opens by itself, closes once the players have left, also when opened by hand or a mechanism). Message only, no label. Saved as CloseOnly in the world's door list.

## 0.2.8

- Build camera swing (Fuzen: still wrong in 0.2.7). 0.2.7's log: the camera in front got the player as guest (nobody on its side), state 1; the camera behind got a door 396 m away as guest, whose toggle did nothing (24 steps, past the 15-step check), so the fallback accepted and toggled with the player, state 1 again: every camera open swung away from the player. Now the guest is a player, survivor or animal on the camera's side, else another door, within 8 m of the door (players at any distance). If the toggle with a non-player guest does nothing, the door is accepted with that guest and toggled again; only then with you. A guest kind that never works is skipped after two failures.
- The log says, for 30 camera opens, where you stand, the guest and its kind, how the door was moved and whether it swung away from the camera or toward it.

## 0.2.7

- Labels in Title Case, no sentences (Fuzen): Mod Enabled; Bases Only (checked = base doors only, the default; replaces Work outside bases); Who Opens Doors, Build Camera, Open Distance, Close Distance, Height Reach, Close Delay, Doors Used By Hand, Respect Hand Toggles, Close Doors Opened By Hand, On Screen, Door Label; key hints as "Door Mode: Shift + Middle Mouse / LB + Y" and "Settings: Shift + F7"; "No World Loaded". The Shift hint moved into the distance tooltips.
- 0.2.6 was never installed (the game was running).

## 0.2.6

- Shift + F7 opens the mod's settings window (Fuzen; it switched the mod on and off): the on/off switch at the top, then Where, Who opens doors, Distances, Doors used by hand and On screen. Distances in metres with - and + buttons (0.1 m, Shift 0.5 m), changed values in the accent colour, a tooltip on every setting, Defaults (keeps on/off and door modes). Also opened from a button on the mod's page of the Kiln menu. The window draws from a copy of the settings and writes each change on the tick thread.
- Work outside bases (the Only Inside Bases setting, inverted in the window): doors everywhere open and close by themselves. Door modes stay for base doors: the Door Mode keys and the label only work on doors inside a flame altar's area, whatever this setting, and a door outside a base always counts as Everyone.

## 0.2.5

- Door Mode is on Shift + middle mouse button by default (Fuzen; was F7). The Door Mode keys (mouse and LB + Y) now fire, and keep the press from the game, only while you look at a door the mod works on (inside a base, or any door with Only Inside Bases off); everywhere else the press goes to the game as usual. A press with no such door says nothing (0.2.4 showed "look at a door").
- A Door Mode key changed in Kiln's Keys page stays as chosen: only the default moved.

## 0.2.4

- Build camera (Fuzen): it opens doors, but they always swung the same way. interaction_directional swings a door away from the toggle's guest, read from the guest's transform, and the guest was the player, wherever the character stood. The guest is now an entity on the camera's side of the door (the door's +Z is its front): a player, a survivor or another door, the one nearest to the door; the player if there is none. Any entity with a transform does, the toggle checks the offer and not the guest (survivors as guests worked since 0.1.0).
- The log says, for the first 12 openings, which side the one who came stood on and the door's DirectionalInteraction state, to confirm that front is +Z; and for the first 20 build-camera openings, the camera's side and the guest chosen.

## 0.2.3

- Door label (Fuzen): only the door's mode, on doors inside a flame altar's area whose mode is not Everyone (Players, Players & Survivors, Manual). No name, no open or closed; nothing on other doors.

## 0.2.2

- Door label (Fuzen): only doors inside a flame altar's area get one, and it no longer says Open or Closed. It shows the door's name, and under it the mode when that is not Everyone (Players, Players & Survivors, Manual). Doors outside bases show nothing.
- Build camera: 0.2.0's log (13:17) shows the ordinary third-person camera 5 to 6 m from the character's feet with a 2.5 m boom, so "3 m beyond the boom" flapped while walking (and the game's free-cam flag stayed False), and the 30 log lines were used up before build mode. The camera now counts as away from 6 m beyond the boom and back below 4 m beyond it; changes of the free-cam flag are logged too (60 lines in all).
- 0.2.1 was never installed (the game was running); 0.2.2 carries its door modes.

## 0.2.1

- Door modes (Fuzen): Everyone (no label, as before), Players, Players & Survivors, Manual. The label and the message show these names. Survivors Only is now Players & Survivors in what it does too: players, their build camera and survivors open the door, animals don't. Manual (was Off) leaves the door alone.
- The per-world file reads the old names (PlayersOnly, SurvivorsOnly, Off) and writes the new ones (Players, PlayersAndSurvivors, Manual) at the next change.

## 0.2.0

- 0.1.0 in game (Fuzen, 23 Nov 2026): doors opened and closed for players and survivors, the Door Mode key and Players Only worked; the build camera opened nothing. 0.1.0 took the camera the client sends with its input, and only while the game's IsInFreeCam flag was set.
- Build camera: the player of this game now uses the camera the game renders with (`Render.Camera`). It counts as the build camera while the flag is set or while it is more than 3 m beyond the third-person boom from your character. Each switch is logged (`auto doors: build camera away/back`, with the flag, both cameras' distances; at most 30 lines) so a miss can be told apart. A joined player still has only the flag and the input camera.
- Door Label (setting `doorLabel`, on by default): looking at a door shows its name (from its template: `Prop_Door_Small_Steel_Crafted` is "Small Steel Door") and Open or Closed under the crosshair, with its mode when not Everyone, or "outside bases". Drawn with Kiln's render frame, only while the game's HUD is up.
- Door Mode on a controller: LB + Y (`Pad:LB+Y`, its own binding on Kiln's Keys page). Both keys now pick the door along the rendered camera's view, so they also work from the build camera.
- Close Delay defaults to 0 (an existing settings file keeps its value).

## 0.1.0

- The real mod, built on what probe 4 showed. Inside a base, a door opens when a player, a survivor or the build camera comes within the Open Distance and closes once everyone is beyond the Close Distance for the Close Delay. A toggle names the door's last accepted offer; a door that never had one (or ignores the toggle) gets an accept from the nearest player first, then the toggle. A door that still doesn't move is left alone for 3 s. Up to 20 openings, closings and failures are logged (`auto doors:` in `kiln.log`).
- Settings (Kiln menu, Insert, Mods, Auto Doors; file `kiln\config\auto-doors-kiln.json`): Enabled, Open Distance (default 4 m), Close Distance (4.6 m), Height Reach (2.5 m, so a door on another floor stays shut), Close Delay (1 s), Only Inside Bases, Open For Players, Survivors, Build Camera and Animals (off), Close Doors Opened By Hand, Respect Hand Toggles, Messages. Distances are whole decimetres (40 = 4 m) because Kiln draws whole numbers as sliders with steps and decimals as free sliders.
- Beyond the original: configurable distances, height and delay; animals; Respect Hand Toggles (a door you open or close by hand stays so while you're near, where the original fought you); no hijacked hold-interact, so doors pick up as usual.
- Door Mode key (F7, rebindable in Kiln's Keys page): look at a door and press it to cycle Everyone, Players Only, Survivors Only, Off. Saved per world in `kiln\data\worlds\<world>\config\auto-doors-kiln-doors-world.json`, by position. Shift+F7 switches the mod on and off.
- The keys work in a world you host. On a joined server the server needs the mod; the keys of a joined client don't reach it yet.
- Not yet tested: survivors as guests (the swing direction comes from the guest), the build camera, a joined player seeing the door move, a door far from every player (the accept fallback uses the nearest player).

## 0.1.0-probe.4 result

- It works. F7 on door 22192 (`Prop_Door_Small_Steel_Crafted`): accept at step 14689, ToggleInteractionEvent {player, door, accepted offer 1} in the same step, door opened at 14692.
- Alt+F7 (toggle only, no new accept, accepted offer 1 while the runtime offerId had run on to 62) closed the door at 14825 and opened it again at 15638. So once a door has been accepted once, a single ToggleInteractionEvent naming the last accepted offer (+36) opens or closes it. A door never accepted needs the accept first (F7 path).
- 40 doors, 38 in bases, 9 bases; 3 events sent, 0 refused.

## 0.1.0-probe.4

- Probe 3 recorded the game's own door opening (door 2109 `_Prop_Door_HollowHalls_Large`, by hand): step 3744 AcceptOfferedInteractionEvent {guest 1, host 2109, offer 0x1a5} (the door's offerId then), 3745 InteractionAcceptedEvent, 3770 ToggleInteractionEvent {1, 2109, 0x1a5} with timeStamp 0, and the door opened in that step. After an accept the runtime stores the accepted guest at +24 and offer at +36 (lastAcceptData), and its own offerId (+40) runs on by one per step for about 4 s. The toggle names the ACCEPTED offer, not the current offerId.
- Probe 3's accept worked (InteractionAcceptedEvent seen) but its toggle named the current offerId (3 instead of 1), so nothing moved. Probe 4 names the accepted offer (F7), and Alt+F7 tries a toggle with the last accepted offer and no new accept. The watch log ignores the offerId counter.

## 0.1.0-probe.3

- Probe 2 bound the program (`door probe: ECS program door_toggle bound`) and sent ToggleInteractionEvent with the door's offer id, but no door moved (Prop_Door_Small_Steel_Crafted 2065, offer 1, also with a wrong offer). A toggle alone is not enough: in the game's own sequence the door first accepts the guest (AcceptOfferedInteractionEvent -> interaction_attempt -> InteractionAcceptedEvent), then the guest's interaction sequence sends the toggle.
- Probe 3 sends both, accept then toggle (F7), or the accept only (Ctrl+F7) or the toggle only (Alt+F7, replaces auto mode). Its program also reads every AcceptOffered, InteractionAccepted, DeclinedInteraction and ToggleInteraction event and logs them, and logs each change of a nearby door with its InteractionOfferRuntime bytes, so opening a door by hand shows the game's real sequence.

## 0.1.0-probe.2

- The ECS program is now bound on the tick thread at the first world step. In probe.1 the bind ran in `OnLoad`, which a game client runs on its main thread in the main menu, so Kiln refused it (`cannot bind ECS program 'door_toggle' (4)`, KILN_ERR_WRONG_THREAD), no event was ever sent, and no door moved. Doors were found correctly (39 in the world, 37 in bases, 9 bases).
- Auto mode waits 3 s before trying a door again when a toggle changed nothing (probe.1 resent it every 15 steps).

## 0.1.0-probe.1

- Probe only. F7 toggles the door nearest to you (8 m), Shift+F7 lists doors, bases, free camera and camera position in `kiln.log`, Ctrl+F7 toggles with a wrong offer id, Alt+F7 turns a players-only auto mode on and off (open within 4 m inside a base, close beyond 4.6 m). Log lines start with `door probe`.
- Built in the cloud container against `kiln\host` of Kiln 0.2.0-dev.30.6 and installed as the drop-in `kiln\mods\auto-doors-kiln`.
