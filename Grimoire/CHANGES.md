# Grimoire - Spells and Arrows Slots

Kiln mod (C#, id `grimoire`) that keeps spells and arrows out of the backpack in slots of their own, equipped and listed in the HUD in the player's own order (the equipped stack takes one backpack slot). Source in `src`, tests in `tests\GrimoireRulesTests` (the rules without the game). `Scripts\Build.ps1` runs the tests and builds the mod into `dist\grimoire`; with `-Install` it copies it to `<game>\kiln\mods\grimoire` (game closed). The settings are in Kiln's profile (`%LOCALAPPDATA%\Kiln\profiles\my-mods\config\grimoire.json`), so an install never touches them. Tags are `grimoire/vX.Y.Z`.

It was the spellbook of Better Spells (3.3.0-dev.1 to dev.11, 23 and 24 Nov 2026); that history, with what was found in game, is in `..\Better Spells\CHANGES.md`.

## 0.2.0 (not released)

- **Arrows** (asked 24 Nov 2026, after the user found spells working in 0.1.3): a second book on the same code, storage `grimoire:arrows` (32 slots, arrows only, kept on death), order key `order-arrows`, the bow's HUD arrow list in the player's order, and **Arrow Slots** (1 to 32, default 12). Same plain-moves-only rules as spells: the equipped arrows sit in one backpack cell, a pick swaps them with the stored ones, arrows that aren't the grimoire's stay in the backpack, and running out equips more of the same kind first, else the next in the order.
- **Spells and Arrows tabs in F3**, with the settings below them (Mod Enabled for both, and the shown tab's Slots). The screen opens on **Arrows** when no spell is loose in the backpack and either arrows are, or the grimoire holds arrows but no spells; on **Spells** otherwise (the user's rule; `GrimoireRules.OpenOnArrows`, tested). A tab clicked while the screen is open stays until it closes. The screen is 780 high (was 760) for the tab bar.
- Seen in game with 0.1.3 (the user, 24 Nov 2026): Grimoire spells work.

## 0.1.3 (not released)

- **Fix: no spell can be lost any more; the equipped spell takes one backpack slot again** (the user's choice of three, 24 Nov 2026, with a Kiln report so it can go back to none). In 0.1.2 the user found switching broken (the HUD snapped back to the first backpack spell) and Eternal Pillar Of Flames gone. kiln.log: the Pillar was equipped in the Equipment page cell `10385:7` when the game was quit at 06:35, and at the next load it was nowhere (not equipped, not stored, not in the backpack). The game does not keep a spell in that cell across a reload, or does not keep a stack Kiln's GiveEquip made there; the log can't tell which. The snap-back has the same cause: the game doesn't see a spell in that cell as equipped, so with any spell in the backpack it equipped that one within about 60 ms.
- Every step is now a plain move: no GiveEquip, no TakeSlot, nothing created or destroyed. A pick swaps the stored spell with the equipped one when that is a grimoire spell (the picked spell takes its backpack cell, as in Better Spells dev.11); a spell that isn't the grimoire's is unequipped and stays in the backpack, and the picked spell moves into a free backpack cell and is equipped there (backpack full: nothing happens).
- **Bringing back what 0.1.0 to 0.1.2 left in the Equipment page**: at every pick, take out, add and settle (and 5 s after the storage is restored at load), an equipped spell in an Equipment page cell is unequipped into the backpack by the game's own unequip and equipped again there, and one that isn't equipped is filed into the storage.
- A grimoire spell the game switched away from (a backpack spell picked from the game's own rows) stays in the backpack and is filed back 5 s later; the player's own backpack spells stay. A spell is the grimoire's while it is in the player's order or stored; Take Out removes it from the order.
- The four-times-a-second count check of 0.1.1 is gone: with the equipped spell in the backpack, the game's own rows carry its live count again.

## 0.1.2 (not released)

> [!danger]
> Withdrawn by 0.1.3: a spell equipped when the game is quit is lost (see 0.1.3).

- **Fix: a spell the game equipped from the backpack no longer joins the grimoire.** Seen by the user in 0.1.0: a looted Shroud Beam joined it during a switch. kiln.log, 24 Nov 2026 06:17: the game switched to the Shroud Beam in the backpack (`10546:5`) by itself, leaving Light Burst unequipped in the Equipment page's Spell cell; a pick 2 s later tried to move the Shroud Beam to the Equipment page, but GiveEquip found that cell taken ("no empty unselected equipment slot is available"), so the mod swapped the picked spell in, and the swap put the Shroud Beam into the storage. Now a pick first files back a stack left in the Equipment page (as the 5 s settle does, which worked: Light Burst filed back at 06:17:17), and an equipped backpack spell the grimoire doesn't hold is unequipped where it lies instead of moved; if it stays equipped, nothing is swapped.
- To clear the Shroud Beam: right-click it in the F3 screen (Take Out).

## 0.1.1 (not released)

- **Fix: the HUD count of the equipped spell follows casts.** Seen by the user in 0.1.0: Ball Lightning stayed at x10 while casting and showed the right count only after leaving the HUD and coming back. The equipped stack sits in the Equipment page, which the game's own rows don't list (kiln.log: `game rows []`), so a cast changed nothing the HUD list is rebuilt for, and the count came from the mod's last picture. The mod now looks at the equipped stack's count four times a second and refreshes the list when it changed.

Seen in game with 0.1.0 (the user, 24 Nov 2026: "everything seems to work"): the equipped spell takes no backpack slot. kiln.log: the equipped Hollow Wretch in backpack cell `10525:2` was unequipped, given into the Equipment page (`10526:7`) and its backpack stack taken; every later pick from the HUD list swapped into `10526:7`.

## 0.1.0 (not released)

- **Moved out of Better Spells** (asked 24 Nov 2026: Better Spells stays on spell settings, and the book will hold arrows too). Built on what Better Spells 3.3.0-dev.11 tested: Kiln's player storage (`Items.RegisterPlayerStorage`, Kiln 0.2.0-dev.31), here `grimoire:spells`, 32 slots, spells only, kept on death. The book kept in character data (dev.8) is dropped. Nothing moves over from Better Spells: its storage `better-spells:spellbook` was emptied by the user on 24 Nov 2026 at 05:41 (kiln.log).
- **F3 opens the grimoire** (Better Spells moved to Shift+F3, the user's choice). The screen lists the spells in the player's order: click to equip, drag to reorder, right-click (or drag onto the backpack list) to take out; the backpack's spells below it, click or drag them in to add; Ctrl+click a spell in the game's inventory adds it; Add All and Take All Out. **Mod Enabled** and **Spell Slots** (1 to 32, default 12) are at the bottom of the screen and on the mod's page in Kiln's menu.
- **The HUD's spell list follows the grimoire's order** (`Ui.AmmoList`, Replace), then the game's other rows. The order is kept with the character (`CharacterData.Carried`, key `order-spells`), because a swap from the storage puts the old spell into the picked slot and the storage's own order changes on every switch (found in dev.10, fixed in dev.11).
- **Fix: the equipped spell no longer takes a backpack slot.** Seen by the user in dev.11. The log showed why: the game equips a spell where it lies, and a swap from the storage puts the new spell into the cell of the spell equipped before (every pick went to the same backpack cell, `10582:2` or `10582:6`). Kiln's GiveEquip only creates a stack in the Equipment page while the slot is empty (`give_equip_run` in Kiln's native items service reuses an equipped cell, Generic cells included for arrows, spells and bait). So when the equipped spell lies in a backpack cell, the mod unequips it, gives the same stack into the Equipment page and takes the backpack stack (Reseat); an empty slot gets the picked spell given there and its stored stack taken; later swaps then land in the Equipment page's cell.
- **Running out**: when a charged spell runs out, more of it from the grimoire is equipped, else the next spell in the order.
- **A spell the game switched away from** (a backpack spell picked from the game's own rows) is left in the Equipment page where nothing shows it; five seconds after the last change of the slot it is filed back into the grimoire.
- Works in a world you host (single player or the host in co-op), as before.

> [!warning]
> Not seen in game yet. Not known until tried: whether the game's unequip keeps the spell in the backpack and empties the slot (Reseat needs both), whether a swap from the storage then lands in the Equipment page's cell, and whether a stack left in an Equipment page moves into the storage (`MoveOptions.Equipped`). kiln.log has every step under "spells:".
