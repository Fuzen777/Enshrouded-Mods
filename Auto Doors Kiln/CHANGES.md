# Auto Doors (Kiln)

A rebuild of Auto Doors (SellTheWorldGames, closed source) as a Kiln mod, from its described behaviour, not its code. Doors are toggled by sending the game's own `ToggleInteractionEvent {guest, host, offer}` from the mod's ECS program `door_toggle` (`src\kiln.programs.json`); `interaction_toggle_new` flips `ServerToggleState` and `interaction_directional` swings the door away from the guest.

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
