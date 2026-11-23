# Streamlined Logistics

A Kiln mod (id `streamlined-logistics`, called Remote Chests with id `remote-chests` up to 0.7.1, C#, `src\`, side "both") that shows what the linked storage chests hold in one list, from anywhere, and moves items between them and the backpack. A rewrite of the idea of XHL-Remote Chest (Nexus 222, closed source), made so the list can surface what matters (favorites first, noisy categories hidden) and so XHL-Core.dll is no longer needed. It works in a world you host.

How it works: the screen is a Kiln screen (`Ui.Screen`) drawn in the player's game. While it is open, the hosted world's thread reads the chests about twice a second (`Items.FindInventories`, `Items.Read`) into a snapshot the screen draws. Buttons post their work to that thread, where every move is `Items.Move`: Kiln's slot move inside the game's own inventory transaction, so gear keeps its level and upgrades. The character's own pages are read from its `InventorySetup` and `SlotSelection` components, to tell the backpack from the hotbar and from what the character wears. The backpack is in two places: item pages of the character's own (those that are not the hotbar's), and the pages carried by the backpack item it wears, which the character's setup reaches through a Virtual link. A link is the chest's position, because the game gives a chest a new entity id at every load; a chest within 5 cm of a kept position is that link's chest. Links, favorites, the hidden categories and the screen's switches are in `remote-chests.json` in the mod's Kiln data folder (kiln.log names the path at the first opening). Worlds are not told apart yet. Item categories come from `chestsort_categories.json` next to the game's exe, the file Quick Deposit and shroudlink's Assistants use; it is read again when it changes (`src\ChestCategories.cs` follows Quick Deposit's `categories.cpp`). The file in the game folder is the one in use; `data\chestsort_categories.json` is a copy kept here after each change, for its history.

`Scripts\Build.ps1` runs the tests (`tests\ChestRulesTests`), builds against Kiln's assemblies in `<game>\kiln\host` and puts the mod in `dist\streamlined-logistics`; `-Install` copies it to `<game>\kiln\mods\streamlined-logistics`, where Kiln loads it as a drop-in (game closed).


## 0.10.34

0.10.33 seen in game (kiln.log, 23 Nov 2026 19:37): a Magic Chest was named Auto-Stations and got 20 Bones for its Bonemeal station, then again at each pass. The user: "since it only stores 10 crafts, if I queue [n] crafts for a station, I have to wait 30s until the next batch arrives ... Any way we could have some kind of \"restock immediately if less than 50%\" feature without tanking perfs ?"

- **Restock at half:** each full pass keeps what every base's stations need for the set crafts; between passes, once a second, the base's stock of those items is read (`Bases.Stock`, a few reads, no station or chest scan), and an item under half of its need brings the next pass forward (at most every 3 s).
- **Crafting menus:** they count the linked chests since Kiln 0.2.0-dev.30.8 but crafting with them does nothing, with no payment or refusal in kiln.log: Kiln's side, draft 18 in the Kiln document.

> [!warning]
> Not yet seen in game.

## 0.10.33

Kiln 0.2.0-dev.30.8 answered draft 17 in part (kiln#132): crafting menus count the linked chests, linked dyes sort with the player's, and a production station's screen shows the game's own counts again, since its +1 / +10 is the game's transfer from the bag and the base's Magic Chests; fills from registered sources are not built (docs/re/stations.md 10.8). The user chose to keep the base's Magic Chests stocked instead, so that the game's buttons and Busy Hands (which keeps 2 crafts loaded per station from the base's Magic Chests) find the inputs: "Work only with chests called Auto-Stations or something like that, working with auto-rename. If trying to add with no chest named such and/or auto-rename turned off, show a toast saying so. Add a setting for \"how many crafts\" to keep the chest topped up with.", then a continuous top-up with a setting for how often, counting the base's whole Magic Chest stock.

- **Auto-Stations:** every Auto-Stations Every seconds (setting `autoStationsSeconds`, 5 to 300, default 30), for each base with automated stations that have a selected recipe: their inputs for Auto-Stations Crafts crafts (setting `autoStationsCrafts`, 0 to 100, default 10, 0 off), summed per item, less the base's crafting stock (`Bases.Stock`), are moved from linked chests in other bases (or ordinary linked chests in the base) into the base's Magic Chest named Auto-Stations. Favorites never move. With Auto-Name Chests on and no such chest, the base's default-named Magic Chest closest to its stations with a free slot is named Auto-Stations; else a toast says what to do, once per base per session. `ChestRules.AutoStationsShortfall`, tested.
- **Auto-Stations chests are kept out** of Quick Deposit and Re-Sort (like chests named with -- in front), so their stock stays for the stations. The name is in the Chests tab's name list.

> [!warning]
> Not yet seen in game. Busy Hands may also collect finished items into the Auto-Stations chest, as into any Magic Chest of the base.

## 0.10.32

Kiln 0.2.0-dev.30.6 (mod API 0.2.15, installed 23 Nov 2026) shipped draft 16, both parts (kiln#125): `Crafting.AddSource` and the dyes of linked sources. The user: "Kiln released 0.2.0-dev.30.6, see what we can use." The mod asks for API 0.2.15.

- **Craft From Linked Chests** (setting `craftFromLinkedChests`, on): every linked chest standing in the world is a crafting source of the local player, in any base or none: crafting at a station, an NPC or from the inventory counts and uses what they hold, after the backpack and the crafting base's own chests, a recipe paid all or nothing (Kiln's own transaction); the tinting screen offers their dyes. The sources follow the links (checked when the saved state changes and every 600 steps, since a chest gets a new entity at every load) and end with the session, the setting, or a world change.

> [!warning]
> Not yet seen in game. Kiln notes crafting sources and their dyes are not checked in a real game yet.

## 0.10.31

- **Fix, nothing salvaged after a start:** the user: "Ultimate Ring of Stamina and Morningdew Greatsword (enemy drops I think) failed to be salvaged." kiln.log (23 Nov 2026 02:20 to 02:22): both pickups seen as loot, then "the table has no salvage balancing: nothing is salvaged" on every step until 02:22:35, when a Rusty Short Sword was salvaged (68 runes). The salvage balancing is read from the category table, which is loaded by the tree only once the F2 screen or a deposit needs it; until then Auto Salvage dropped what it was given. It now loads the categories and the tree first.

> [!warning]
> Not yet seen in game.

## 0.10.30

0.10.29's log answered it (23 Nov 2026 02:01:57): the user: "Just happened again with two items from the same dungeon chest." Both pickups (Eagle Eye Trousers, Coalescent Wand, from entity 3131312605 into 12796:1 and :2) were seen as loot, then 13 ms later "kept: no longer in the slot it landed in". Rings picked up from the ground the same minute were found and salvaged. A container's loot is reported before its items are in their slots.

- **Fix, loot from a chest not salvaged:** a looted piece not yet in its slot is looked for again at the next steps, up to 40 times (about a second), before it is given up ("kept: never found in the slot it was reported in"); when found late, kiln.log says after how many steps.

> [!warning]
> Not yet seen in game.

## 0.10.29

0.10.26 seen in game (kiln.log, 23 Nov 2026 01:22 to 01:46): Auto Salvage on with the rules Any / Epic or worse; Golden Bulwark Shield (Epic, 40) gave 224 runes, Ultimate Ring of Mana (Common, 40) 28, Rusty Short Sword (Uncommon, 50) 136. The user: "Three items weren't salvaged : Elder Trousers, Circlet of Lightning and Tomb Raider's Bow." The log has nothing of the mod about them (Kiln Auto Loot saw them as Armor Common 40, Armor Epic 41, Weapons Epic 41, the last two in one step): every check before the salvage was silent.

- **Loot from chests outside a base:** a pickup from a storage-like chest counted as taking from the player's own chest; now only a chest in a base or a linked chest does, so loot from a dungeon's or a ruin's chest is salvaged and can start Auto Quick Deposit. (A likely cause, not proven: the world listed no regular chests.)
- **Why gear is kept, in kiln.log:** with Auto Salvage on, every gear pickup is logged with its source and slot ("auto salvage: picked up ..."), and a looted piece a rule takes but the mod keeps says why ("kept: worn", "not in the backpack", "a favorite", "a gem in it", "its socket can't be read", "its perk slots can't be read", "no longer in the slot it landed in").

> [!warning]
> Not yet seen in game. The next miss names its reason in kiln.log.

## 0.10.28

- **A taller screen** (the user: "Make the F2 menu taller by default so it fits the whole settings menu (assuming three rules for each)"): the screen opens 1060 high (was 760; at UI scale 1, so 2120 on a 4K screen at scale 2), counted from the Settings tab's 30 rows (5 headers, 14 settings, two rule lists of a title, three rules and Add) at about 30 each, plus the summary, the status and the tabs. Still resizable.

> [!warning]
> Not yet seen in game: whether it fits exactly depends on the game look's row height.

## 0.10.27

- **No cosmetics in the rules** (the user: "Let's remove Cosmetics from the deposit/salvage rules."): the rules cover weapons, shields, armor and rings; cosmetics are always deposited and never salvaged. A 0.10.26 rule for cosmetics is dropped when the data file is read (`GearKind` keeps Rings at 5, so rings rules stay). Tested.
- **Marking without the borders** (the user: "R3 and Shift + RMB should work in inventory even if the Favorites in inventory settings is turned off."): Shift + RMB, L3 and now R3 on a slot of the game's screens mark or unmark its item whatever Favorites In The Game's Screens says; the setting only switches the gold borders.

> [!warning]
> Not yet seen in game.

## 0.10.26

Deposit and salvage rules, as the user asked ("we use a rules system like Auto Loot ... Deposit Rule ⇒ [Any item, weapon, etc.] of [rarity] and at least [level] is eligible for quick deposit ... Salvage Rule ⇒ ... of [rarity] and up to [level, 0 = unlimited] gets auto salvaged AFTER first reaching the inventory"), with the readings the user agreed to (gear only; rarity or better for deposit, or worse for salvage; never favorites, worn gear, gear with a gem; loot only; no Tools kind).

- **Deposit rules** replace 0.10.5's rarity filters (their config keys are gone): Quick Deposit puts a piece of gear (weapons, shields, armor, cosmetics, rings) into chests only when a rule takes it: its kind (or Any), its rarity or better, its level or more. Every other item always goes. Default: Any, Legendary, any level. The report counts "gear no deposit rule takes".
- **Salvage rules and Auto Salvage** (setting `autoSalvage`, off by default): looted gear a rule takes (its kind, its rarity or worse, its level or less, 0 any) is salvaged the step after it lands in the backpack, so the game's own unlocks from looting happen first. The yield is the game's Trash yield in runes, ported from Kiln Auto Loot's formula (MIT, Frostdev): the upgrade cost of the perk slots the piece carries, plus a share of its unlocked slots' cost at the world's recycling factor (`perkCostFactor`, `perkUpgradeRecyclingFactor` read through Kiln.Settings). The runes must fit first (a dry run), then the piece is taken and the runes given. Never salvaged: favorites, worn gear, gear in the hotbars, gear with a gem (or whose socket can't be read). Default rule: Any, Common, any level. A toast lists what was salvaged.
- **The rules in the Settings tab:** under Quick Deposit and Salvage, one row per rule ([Kind] [Rarity] [Level] Remove) and Add Rule, at most 12 each; kept in the mod's data file (`depositRules`, `salvageRules`), a file without them reads the defaults.
- **The table** (`data\item_categories.json`) carries the game's salvage balancing (`keen::BalancingTable`: seed 8, creep 0.15, trash 5..20 %, factors 1 / 0.75 / 0.5, the rune item), written by `make_item_categories.py`.
- `GearRule`, `ChestRules.DepositAllowed`, `SalvageWanted`, `SalvageRunes`, `ParseSalvage`, tested.

> [!warning]
> Not yet seen in game. Kiln Auto Loot's own salvage rules should be off while Auto Salvage is on: the first mod to act wins.

## 0.10.25

The user approved the other two Kiln 0.2.0-dev.30.5 requests ("Yes, go ahead."):

- **L3 marks a favorite:** with a controller, L3 on the slot the game's selection is on (in the inventory, a chest, a station) marks or unmarks its item, as Shift + RMB does (`SlotClickOptions.Pad` with `Pad:LS`, Kiln's `Ui.Inventory.Selected`). It follows Favorites In The Game's Screens.
- **The game's item detail in the screen's tooltips:** an item's tooltip in the F2 screen shows the game's own detail panel (damage, perks, gems, the description) as the game drew it when the item was last looked at in its own screens this session (`Ui.Values.ItemDetail`, matched by item, and for gear by level and rarity). An item not looked at yet keeps the rows read from the game's data. Kiln keeps the last 64; its capture starts with the mod, so items looked at before the F2 screen opens count too. Kiln draws no panel on demand: this is the panel's text, not the panel.

> [!warning]
> Not yet seen in game. Kiln notes the controller's selected slot is not checked in a game yet.

## 0.10.24

Kiln 0.2.0-dev.30.5 (mod API 0.2.14, installed by the user on 22 Nov 2026) shipped five of the user's requests: controller keys for mod UIs and the selected slot, the game's item detail (`Ui.Values.ItemDetail`), the rarity and level in `VisibleSlot` (draft 14), `OutlineOptions.ThroughWalls`, and the left stick's drift fix (draft 15, kiln#123). Not in it: marks over the game's item menu (draft 10) and `UseItem` for the repair kit (draft 11). The mod now asks for API 0.2.14 and uses two of them:

- **Through walls, no boxes:** the outlines of a quick deposit's, a re-sort's and a clicked chest's chests are drawn through walls where Kiln can (`Ui.World.ThroughWallsAvailable`), and then the boxes are left out (the user, 0.10.13: "If we can't have it do that, we should keep the box"). The labels and the arrow to a clicked chest stay.
- **Favorite gear by the slot's rarity:** the gold border on favorite gear reads the slot's own rarity, so chest screens get it too (0.10.6 to 0.10.23 matched the player's own pieces by entity), and Shift + RMB on gear the mod can't find by its entity (a chest farther away) uses the slot's rarity. Older Kilns keep the entity match.

> [!warning]
> Not yet seen in game. The controller keys (L3 to mark a favorite) and the game's item detail in the screen are not used yet.

## 0.10.23

- **Auto Quick Deposit** (the user: "when looting an item that causes the inventory to become full, trigger a quick deposit. (Only by looting, not taking from chest or any other situation.) This would be an opt in setting under quick deposit, off by default."): a setting under Quick Deposit (config `autoQuickDeposit`, off). It watches Kiln's `ItemPickupEvent` (the game's "+N"): items for the local player from a source that is not one of the player's storage chests (taking from a chest) and not none (crafting's output), while the mod itself moves nothing. At the next step, if the backpack has no free unlocked slot left (`ChestRules.BackpackFreeSlots`, tested), a quick deposit runs by the usual rules, at most every 10 s. Outside a base it needs Quick Deposit Outside A Base, and without it does nothing (logged, no toast).

> [!warning]
> Not yet seen in game. Which pickups the game reports with which source (a dungeon chest's loot, a building refund) is not checked.

## 0.10.22

- **Search Every Tab** (the user: "Add a setting to LS so search can ignore the current tab category." and "On by default."): a setting on the Settings tab (config `searchAllTabs`, on by default). While a search is typed, the grouped list shows what it finds in every category, whatever tab is open; off, the search stays in the open tab as before.

> [!warning]
> Not yet seen in game.

## 0.10.21

- **Overgrowth and Decay tools** (the user: "Also give Overgrowth and Decay tools the same treatment as construction hammer and summoning staff."): their slots hold the brush they use (`Building_Overgrowth_Shape_Big/Medium/Small`, `Building_Decay_Shape_*`, the game's data: category Blueprints, flagged IsBuildingOvergrowthTool / IsBuildingDecayTool), so they get their own keys (`ChestRules.OvergrowthToolKey`, `DecayToolKey`): Shift + RMB favorites the tool whatever brush it holds, the border stays, Deposit All keeps its slot, a quick deposit leaves it, and the screen lists it under the tool's name and icon, with the tools. Basic and improved tools count as one. Names come from the game's tool items. Tested.

> [!warning]
> Not yet seen in game. That their slots hold the brush is read from the game's data, not seen.

## 0.10.20

0.10.19 seen in game (the user): Shift + RMB favorites the Construction Hammer and the summoning staff, and the border stays whatever they hold.

- **Fix, tools out of place:** the user: "hotbars aren't being sorted properly in the F2 menu : in my case, I get a wand, a felling axe, three meals, a wisp scroll, a skull summoning vessel, a spell, a flame altar and then a pickaxe and a sieve. If it worked, the axe, the pickaxe and the sieve should at least be next to each other since they're all tools. The same seems to hold true for the inventory and the favorites too." The order was the tree's, but the game files pickaxes, sieves, rakes and the building tools as BuildTools and the Construction Hammer as Blueprints, so the table put them in Placeables > Decor (or nowhere). Every `Tool_` item of those categories is now in Gear > Tools (93 items), and the tools have an order of their own: axes, pickaxes, sieves, rakes, watering cans, hoses, the Construction Hammer, the building tools, fishing rods and baits, gliders, grappling hooks, climbing claws, torches, lockpicks, keys, the camera, backpacks, instruments.
- **The hammer and the staff in the screen** (the user: "We should readd these to the F2 menu since that now works."): the Hotbars and Inventory lists show them again (0.4.1 to 0.10.19 left them out), as Construction Hammer and Summoning Staff, with the hammer's icon, filed with the tools, their favorite the tool's (`FavoriteSet` maps a tool entry to its tool, tested). A quick deposit still leaves them in the backpack.

> [!warning]
> Not yet seen in game.

## 0.10.19

The user confirmed the gold borders on favorite gear (Favorites In The Game's Screens had been off).

- **Favorite Construction Hammer and summoning staff** (the user: "Construction hammer and Summoning staff still can't be favorited this way though - there is the latent issue of hooking their \"content\" instead of the actual item."): the slot of either tool holds what it has selected, not the tool (Kiln's docs/re/building.md: the client shows an `IsBlueprint` item as the Construction Hammer; the staff shows its `NPC_...` helper), so a favorite followed the selection. Now every shape (`Blueprint...`, `_no_blueprint`) stands for the hammer and every helper (`NPC_...`) for the staff, read once from Kiln's item catalog (`Items.All`): Shift + RMB on the slot marks the tool itself (stored as `ChestRules.ConstructionHammerKey` / `SummoningStaffKey`, ids no item has), the gold border shows whatever is selected, and the game's Deposit All keeps the tool's slot (a slot rule, as for favorite gear). Both hammers (basic and improved) are one favorite: their slots look the same. `ChestRules.ToolKeyOf`, tested.
- **Re-Sort names chests too** (the user: "Re-Sort should also rename chests if the setting is toggled on."): with Auto-Name Chests on, a stack in a chest that doesn't ask for it, when no chest named for it has room, gets the closest default-named chest named for it, its own chest included; a toast lists them.

> [!warning]
> Not yet seen in game.

## 0.10.18

- **Marking a category's chests** (the user: "LMB a category to highlight all chests with the relevant name. LMB a parent to highlight all chests with the relevant name alongside its children."): LMB on a sub-category of the Categories tab marks the chests named for it in the world, outline and box, as after a quick deposit (while the screen is open, then for the Highlight Time); LMB on a group marks the chests named for the group and for each of its sub-categories. Every chest the Chests tab lists counts, linked or not, within the marking distance; the status says how many. It does not depend on Highlight Chests After A Deposit.

> [!warning]
> Not yet seen in game.

## 0.10.17

The user confirmed the workaround for Kiln's scrolling with a controller (LB or RB once, stick released).

- **Auto-Name Chests** (the user: "auto-rename chests, as an opt-in setting. If the setting is toggled on, there are chests with default names available and the user tries to deposit an item that has no associated chest, automatically rename the closest one to the relevant category. Setting has three modes : Off / Children / Parent." and "Likewise, if all chests with relevant names are full, rename a chest under the same restrictions."): a setting in the Quick Deposit part of the Settings tab, Off by default (config `autoNameChests`, 0 to 2). During a quick deposit, when no chest named for an item has room for it (none is named for it, or all of those are full), the closest chest of the deposit that still has its default name and a free slot is named for the item's sub-category (Children) or its group (Parent), and the item goes in. A toast lists the chests named. Quick Deposit only: Re-Sort names chests too since 0.10.19.
- **A full chest gives way:** when every chest of the best name is full, Quick Deposit and Re-Sort use the next rank's chests with room ("Gear" after a full "Weapons"), `ChestRules.BestNamed`, tested.

> [!warning]
> Not yet seen in game.

## 0.10.16

Chest names reworked, as the user asked: "we should scrap alternative names entirely - it's too confusing without having a list right at hand -, and add a column in Categories with a checkbox for available/unavailable chests named after categories + remaining spaces in these chests. Likewise, a chest named after a children (e.g. Weapons) should take priority over a chest named after a parent (e.g. Gear). Finally, I want to revisit how rename works : the name column should have dropdowns to easily rename chests to category names. RMB would turn it into a typing field for custom names."

- **Exact names only:** a chest asks for a sub-category only when its name is exactly that sub-category's name ("Weapons", "Raw Food", "Ores"), case and outer spaces aside, and for a whole group when it is exactly the group's name ("Gear", "Materials"). No more single words, singular or plural forms, or the hand-made categories' words (those still serve when the tree's table is missing). Materials' sub-categories (the hand-made material categories) are named the same way. `CategoryTree.SectionRanksOfChestName`, tests rewritten.
- **Sub-category before group:** a chest named for the sub-category ranks above one named for its group, so Quick Deposit and Re-Sort fill "Weapons" before "Gear" (kept from 0.10.15's ranks).
- **Categories tab:** two columns, Chest Named For It (a read-only box, ticked when a linked chest has that exact name, with x2, x3 when several do) and Free Slots (in those chests). A group's row counts the chests named for the group.
- **Chests tab:** the name column is a list of the tree's names (each group, then its sub-categories); choosing one renames the chest. A name that is no category's is shown first, as it is. RMB on the list turns it into the text field of 0.10.12 (Enter saves, Escape cancels). The Rename button is gone; Show, in its place, marks the chest in the world (what LMB on the name did).

> [!warning]
> Not yet seen in game. Chests named with old alternative names ("Food", "Tool") no longer ask for anything: rename them from the list.

## 0.10.15

The user restored the world to 10:01 and the character to 09:52 (chests destroyed by accident, unrelated to the mod); the saves from before are in `enshrouded-mod-backups\2026-11-22 1010 Saves before restoring (after 0.10.13 test)`.

- **Fix, potions in Meals:** the user: "many potions ended up in the meals category of the F2 menu." The game names some potions with a leading underscore (`_Consumable_Potion_Health_T7_HoT`, the Mana ones too), which 0.10.3's `Consumable_` test missed, and 0.10.3 put every crafted consumable in Meals, food or not. Only food (named Food, or eaten through the game's food sequences) is now a meal or raw food; the rest, the water pouch included, is Potions & General Consumables. Meals 100, Raw Food 68.
- **Exact names first:** the user: "A chest named \"food\" takes precedence over a chest named Meals. Categories exact names should always take precedence over alternative names." A chest's name asks for a sub-category at one of three ranks (`CategoryTree.SectionRanksOfChestName`): every word of the sub-category's name ("Meals", "Raw Food"), then every word of its group's ("Consumables"), then one shared word or a hand-made category ("Food"). Quick Deposit and Re-Sort use only the chests of the highest rank (`ChestRules.BestNamed`), top-ups included, so a meal goes to Meals even when a Food chest holds some; with no chest named for it, as before. Re-Sort leaves a stack where it is only in a chest of the highest rank. A sub-category named in full only as part of another one the chest names ("Weapons" in "Weapon Gems") counts as a shared word. Tested.

> [!warning]
> Not yet seen in game.

## 0.10.14

0.10.13 seen in game (kiln.log, 22 Nov 2026 09:57 to 10:08): a chest renamed from the screen ("Meals"), the deposit's chests lit with outline and box (2 of 2, then 14 of 14 shown), Re-Sort moved 69,529 items into 14 of 35 chests. The user then asked for the world as it was before that start (being decided).

- **Fix, drops on the hotbars refused:** the user's screenshot: "Took 0 x Fell Thunderbrute Greatsword. The game answered Refused: toSlot is on the player's action bar (MoveOptions.ActionBar allows it)." A move into a hotbar slot now passes Kiln's `MoveOptions.ActionBar`.
- **Between the hotbars and the inventory:** the user: "Dragging from hotbar to inventory or vice versa does nothing." A stack dragged from the Hotbars list onto the Inventory list goes to the backpack, and one from the Inventory onto the Hotbars goes to hotbar 1, then 2. Kiln moves between two owners only (`to` may not be `from`), so the stack passes through a linked chest with a free slot, as the stranded-stack rescue does; what can't come back stays in that chest and the status says so. One Kiln drag kind now serves every stack, so the right side takes drops from both sides.
- **Cosmetics:** the user: "Quick Deposit is missing a category for Cosmetics." The game's vanity pieces (`Vanity_*`, 196 items, worn over armour for the look) have their own sub-category, Gear > Cosmetics, after Armor, so a chest named Cosmetics takes them. Armor keeps 603.

> [!warning]
> Not yet seen in game.

## 0.10.13

- **Boxes with the outline:** the user: "the chests outline glow is now working, but two issues : It still draws the box, which is not needed anymore. The outline isn't showing through walls. If we can't have it do that, we should keep the box. alongside the glow." Kiln's `OutlineOptions` has colour, pulse, priority and duration, nothing to show it through walls, so the box stays. A chest clicked in the Chests list already had both; the chests of a quick deposit or a re-sort now get the box too, not only when the outline fails.

> [!warning]
> Not yet seen in game.

## 0.10.12

- **Fix, renaming a chest:** the user: "Clicking Rename allows me to rename the related chest - it should automatically select the input instead. ... once I have typed a name, I have no way to confirm. Using Enter doesn't confirm." Rename now gives the name field the keyboard at once (`UiDraw.FocusKeyboardHere`), Enter saves and Escape cancels (`KeyPressed`, claimed from ImGui's navigation). Save and Cancel moved to the last column, where Rename is: 0.10.0 drew them after the field, past the Name column's edge, out of sight. The field fills the Name column.

> [!warning]
> Not yet seen in game. Whether Escape also closes the screen (Kiln's own handling) is not known.

## 0.10.11

- **Re-Sort Chests** (the user: "Are we able to add a \"re-sort\" button to the F2 UI that would put all the existing items into the properly named chests ?"): a button next to Quick Deposit on the Items tab. Each stack in a linked chest whose name does not ask for it (by the tree's sub-categories and groups, and the hand-made categories, as Quick Deposit reads names) goes to a linked chest whose name does: that chest's stacks of the item are topped up first, then the one holding the most of it, then the nearest, with a free slot. A stack already in a chest that asks for it stays, as does one no chest asks for, and chests named with -- in front are left alone. The status counts the items as they move; the end is reported as a quick deposit is (toast, chat, quick-deposit-report.txt, the chests that received lit), with what had no room listed after "Stayed:".

> [!warning]
> Not yet seen in game. Each move takes one or two world steps, so a large re-sort takes a while; the screen's other buttons wait until it ends.

## 0.10.10

0.10.9 loaded in game (kiln.log, 22 Nov 2026 09:32): the bundled categories were used while the game folder's file was renamed ("24 categories read from the categories bundled with the mod"), nine gear favorites of an older version moved to their highest rarity and six slot keep rules followed. The six "favorite gear mark check" lines were all written before the favorite pieces were known (server entity 0, client entity 12623 and 12624, among 0 pieces), so they told nothing.

- **Dropping on the hotbars:** the user: "Drag to hotbars should drop onto hotbar, first try hotbar 1, then 2 then if both are full, drop in inventory. If both hotbars and the inventory are full, reject the move." A chest stack dropped over the Hotbars list goes to hotbar 1 (a stack of the item that is not full, else the first empty slot), then hotbar 2, then the backpack; with all three full nothing moves and the status says "Your hotbars and backpack are full." The Hotbars list's top and bottom on screen are noted while it is drawn and compared with the pointer at the drop. `ChestRules.ChooseHotbarSlot` and `PlayerLayout.HotbarPagesInOrder`, tested.
- **Gear mark check:** the lines are written only once the favorite pieces are known, and the pieces' item entities are logged when they change ("favorite gear carried").

> [!warning]
> Not yet seen in game. Whether the game takes every item in a hotbar slot is not known; one it refuses is reported with the game's answer.

## 0.10.9

- **Drag and drop between the two sides** (the user: "can you get started on drag and drop ?"): with Kiln's `UiDraw.DragSource` and `DropTarget` (Kiln 0.2.0-dev.30.3). A stack's grid cell, or its icon or name in the list view, can be dragged with LMB. Dropped on the right side (Hotbars and Inventory), a stack from the chests is taken into the backpack: one stack, every stack of the row when Shift is held at the release. Dropped on the left side, a stack from the backpack or hotbars is stored. Both are the same moves as RMB. A drop on the side it came from does nothing. The name and count follow the pointer. The cells' tooltips say so.

> [!warning]
> Not yet seen in game: Kiln's drag and drop is used for the first time here.

## 0.10.8

- **Missing chest names after a quick deposit** (asked on 21 Nov 2026, built on the user's reminder): when items stay in the backpack, a toast (8 s) says which chest name would take them, grouped by name: "No chest named Ores (Iron Ore, Copper Ore), Weapons (Iron Sword)." The name is the item's sub-category in the tree (the hand-made category for materials), the word a chest's name must hold. When a chest of that name exists, its items are listed as "Full: ...". An item with no category is "No chest for ...". Up to four names, three items each. The chat report and quick-deposit-report.txt say the same after "Stayed:". `ChestRules.MissingChestNames`, tested.

> [!warning]
> Not yet seen in game.

## 0.10.7

Defaults for publishing, as the user asked: "Yep, move the open key to F2. View : grid by default, hotbars on, ungrouped off. Hand-made categories should be bundled."

- **Open key:** F2 by default (was F8). A key already chosen in Kiln's `keybinds.json` stays.
- **View:** a new player starts with the grid and Include Hotbars on, grouped. A state file without these switches reads them as on; switches turned off stay off.
- **Bundled categories:** `data\chestsort_categories.json` is now inside the mod. A `chestsort_categories.json` next to the game's exe (Quick Deposit's, or one the player edits) is still read first and followed when it changes; without one, the bundled copy is used, so the category tree, chest names and Quick Deposit work without Quick Deposit installed.

> [!warning]
> Not yet seen in game: the bundled copy is only used where the game folder has no file of its own.

## 0.10.6

- **Fix, gear favorites by rarity:** the user: "Favorites ignore rarity ; I just got the same staff at uncommon rarity as I have at legendary, the uncommon one got favorited." Favorites were item ids. Gear is now a favorite at one rarity (state key `gearFavorites`, [item, rarity] pairs); plain items stay favorites by id. A piece can't be told from an identical one of the same rarity.
  - **The screen and Quick Deposit:** rows and the deposit test the stack's rarity. RMB with Shift and the star mark the row's rarity.
  - **The game's own deposits** (Deposit All, Deposit Stacks, Quick Stack Station): plain favorites keep Kiln's rule per item; favorite gear gets Kiln's rule per slot (`Items.Transfers.Keep(SlotId)`), one for each of the player's slots holding it, re-read at the rule checks (every 60 steps), so gear moved within the backpack is kept again a moment later.
  - **Gold borders in the game's screens:** Kiln's slot view has no rarity, so a slot of an item with a favorite rarity is marked when its item entity (`VisibleSlot.ServerEntity` or `ClientEntity`) is one of the favorite pieces the player carries, read at the same checks. kiln.log has up to six "favorite gear mark check" lines saying whether the entities match; if they don't, favorite gear gets no border. Chest screens: no border for favorite gear (Kiln maps only the local player's slots). Draft 14 in the Kiln document asks Kiln for the rarity in the slot view.
  - **Shift + RMB in the game's screens:** the clicked item is looked up by its entity in the player's inventory, then in the containers within 8 m, for its rarity. Gear found nowhere is left as it was, with a toast pointing to the mod's screen.
  - **Favorites of older versions:** the user chose the highest rarity owned. A gear favorite of an older version (an item id) becomes a favorite of the highest rarity among the linked chests, hotbars and backpack the first time the screen is opened; until then it still matches every rarity. Logged.
  - `FavoriteSet`, `ChestRules.SetFavorite` and `ChestRules.MoveOldGearFavorites`, tested.

> [!info]
> Kiln.Saves has per-item data that follows one item through moves and saves (`InstanceData`), so favorites per copy would be possible. It is not used: Kiln says its matching of items in a player's inventory after a load is not yet proven, and two identical items can be ambiguous.

> [!warning]
> Not yet seen in game. Check kiln.log's "favorite gear mark check" lines.

## 0.10.5

- **Quick Deposit, minimum rarity per gear type:** the user asked that Quick Deposit only deposit gear above a given rarity, per type, defaulting to Legendary, with one entry per type made of an on/off switch and a rarity list. The Settings tab's Quick Deposit part has Weapons, Shields, Armor and Rings rows: ticked, only that gear of the chosen rarity or better goes into chests (at or above, so Legendary deposits Legendary); the rest stays in the backpack. Unticked, all of it goes, as before. All four start ticked at Legendary. A type is told by the item's sub-category (Gear > Weapons and so on); tools, ammo and other items are not filtered. The deposit report counts the gear kept ("Kept: 2 favorite stack(s), 5 piece(s) of gear below their minimum rarity."). Config keys `depositRarityFilter<Type>` and `depositMinimumRarity<Type>` (0 Common to 4 Legendary); `ChestRules.StaysBelowRarity`, tested.

> [!warning]
> Not yet seen in game. Kiln's `UiDraw.Combo` is used for the first time.

## 0.10.4

- **Favorites first on the right side:** the user asked for favorites at the top of each list there. The Hotbars and the Inventory each list their favorites first, then the rest in the tree's order (`ChestRules.FavoritesFirst`, tested).
- **Hotbars:** the header and the Include Hotbars switch are plural, since the game has two hotbars.

> [!warning]
> Not yet seen in game.

## 0.10.3

The user's notes on the categories, and how they are answered:

- **Meals and raw food:** Food is split into Consumables > Meals and Consumables > Raw Food. A food is a meal when a recipe of the game makes it (cooking or any other station) and its name is not marked raw; the rest is raw food (fish, meat, fruit, mushrooms). 112 meals and 69 raw foods. Three crafted foods named raw (honey, fish cutlets and fillets) stay with raw food.
- **Flasks:** the game names its flasks, potions, bandages and scrolls `Consumable_*`, and those now go to the renamed **Potions & General Consumables**, even the flasks that fill a food slot (Diver's Endurance Flask, Flask of the Fallen, Shroud Survival Flask). An item the table does not know whose game category is Consumables goes there too.
- **Fishing baits:** crafted baits and critter baits move from Ammo to Gear > Tools, right after the fishing rods. Animal baits (for taming) stay in Placeables > Animals & Pets.
- **Eternal spells:** listed before the regular spells.
- **Order inside a sub-category:** it was kind (weapon type, potion kind...), then tier (from the item's debug name), then debug name, so ranks of one item of different tiers were apart (Skull Summoning Vessel I and II). Now one item's ranks sit together: kind, then family (the shown name without its last numeral, I to C or a number), placed at the family's lowest tier, then tier and name. `ChestRules.NameFamily`, tested.

> [!note]
> Hidden or collapsed Consumables > Food and Potions sections are not carried over to the new names.

> [!warning]
> Not yet seen in game.

## 0.10.2

- **Fix, favorites' borders on loading screens:** the user saw the gold borders of the hotbar's favorites stay on screen during loading screens. Kiln still reports the hotbar's slots there, so the mod's mark rule now gives no mark while the game is not in play (Kiln's game screen: not the Play state, or a loading screen or cinematic). The screen is read at most ten times a second. The borders over the game's item menu (RMB on an item) still need Kiln (TODO.md).

> [!warning]
> Not yet seen in game.

## 0.10.1

0.10.0 seen in game (the user and kiln.log, 2026-11-22 06:27 to 06:40, Kiln 0.2.0-dev.30.4): it loads, took over Remote Chests' settings, links, favorites and F2, and MMB works ("Started" for soups, smoothies, a potion, from chests and from the inventory; once "Busy" while the previous use played). The user's notes and how they are answered here:

- **Weapons by the game's own weapon types:** the game does give each weapon a type (`weaponCategoryReference`). Wands (Ice Beam, Luminous, Coalescent, Voidcaller...) were filed with bombs and grenades because both use the same "mid range" slot; they are now Weapons, and only bombs and grenades are Ammo. Torches are Tools. Inside Weapons the order is the game's types: sword, greatsword, axe, greataxe, mace, hammer, daggers, bow, staff, wand.
- **Shields:** shields and wards have their own category, Gear > Shields (they were with the weapons).
- **Dyes:** their own category, Other > Dyes (they were in Miscellaneous).
- **Tabs:** the category bar is a row of tabs (All, Favorites, then the groups).
- **Favorites in every tab:** a group's tab shows that group's favorites at the top. A new setting, All Favorites In Every Tab (`allFavoritesInTabs`, off), shows all of them instead.
- **The repair kit:** Kiln's `UseItem` uses what a sequence consumes at a consume event (food, drinks, potions); the repair kit pays for its use as a cost instead, so Kiln answers that it is not a consumable. Its Use goes away for the session, one taken from a chest goes back to it, and a notice says to use it from the hotbar.
- Not done here: the gold border drawn over the game's item menu (RMB on an item) and a controller button (L3) to mark a favorite in the game's inventory. Both need something from Kiln (see TODO.md).

> [!warning]
> Not yet seen in game.

## 0.10.0

Kiln 0.2.0-dev.30.3 (the user runs 30.4) added most of what the user asked its author for (issues #107 to #114, from the drafts of 21 Nov 2026). Asked which workarounds to drop for Kiln's own calls, the user answered "Yes, go ahead." to all four proposed; drag and drop comes later. The mod now asks for mod API 0.2.13.

- **The chests' outline is Kiln's** (`Ui.World.Outline`): the chests a quick deposit filled pulse gold with the game's own highlight, and the chest clicked in the Chests list too. The native companion (Streamlined Logistics Glow, 0.7.0 to 0.9.1) is gone: its sources and build script are removed (they stay in the history), and `Scripts\Build.ps1 -Install` removes its folder from `greeneye`. Where Kiln can't outline, or none shows a second and a half in, the boxes and labels take over as before.
- **Use is the game's own use** (`Abilities.UseItem`): MMB, or the Use button, makes the character use the item as from the hotbar. The animation plays, the game takes one item at the moment it would, and the buff carries the item's own values and time, so the buff panel shows the food and its effects as for a hand use. Every consumable can be used now (potions, bandages, scrolls), not only food and drink. An item in a chest is moved into the backpack first (a stack of it with room, else a free slot); when the backpack is full, nothing happens and a notice says so. When the character is busy (another action), the item is not used, and one taken from a chest stays in the backpack. The buff the mod wrote itself (0.6.0 to 0.9.1), its list of usable items and its script are removed.
- **Favorites in the game's screens use Kiln's marks and clicks again** (`Ui.Inventory.MarkWhen` and `OnClick`), now that Kiln places them right at every resolution. The mod's own placement and its polling of the mouse (0.5.3 to 0.9.1) are removed; Kiln keeps Shift + RMB from reaching the game.
- **Rename a chest:** the Chests tab has a Rename button per chest. The name is set through the game's own rename (`Items.SetContainerName`): it is kept in the save, everyone sees it, and Quick Deposit reads it. 48 characters at most; an empty name clears it.

> [!warning]
> Not yet seen in game. To see: the outline on the chests after a quick deposit (kiln.log: "highlight: N of M outline(s) shown"); MMB on a food in a chest and in the backpack plays the eating and shows the food in the buff panel; a potion is used the same way; the gold borders and Shift + RMB in the game's inventory at 4K; a renamed chest keeps its name after a restart.

## 0.9.1

The user tested the use experiment again on 2026-11-22 (with 0.7.1 installed): "It gives the right buffs and lasts the right duration. Only thing missing is that the current buffs menu doesn't state where it's coming from (only the type, e.g. liquid and duration) or what it buffs." And: "it should also work on inventory/hotbars-side items."

- **Use from the backpack and the hotbar:** MMB on a grid cell of either side, and a Use button beside Store in the backpack's list and for a selected backpack item. The same rules as from a chest; the item is taken from the slot it is in.
- **To find out what the buff panel names a food by:** the use log's buff lines also give each buff's level, so a food eaten the game's way and one used from the screen can be compared. Nothing the game replicates to clients ties a buff to its item, as far as the reflected types show (`BuffNetworkData` holds the buff type, its times and a level); whether the level is where the game keeps the item is what this shows.

> [!warning]
> Not yet seen in game.

## 0.9.0

The category rework the user asked for on 2026-11-22: categories in a natural order with sub-categories instead of the category file's order, a way to jump to one, favorites both at the top and in their category, headers that fold and stay folded, sturdier categorizing (keys, lockpicks and chests landed in Misc), and, added the same day, like items next to each other inside a category ("beds in furniture are grouped together"). Their answers to the proposal: keys and backpacks join Tools, Consumables are Food, Potions (bandages too), Ammo and Spells, Placeables before Building, Materials last and kept on the hand-made list.

- **The category tree:** Favorites; Gear (Weapons, Armor, Rings, Tools); Consumables (Food, Potions, Ammo, Spells); Placeables (Stations, Storage, Furniture, Doors, Showcases, Animals & Pets, Decor); Building (Blocks, Terrain); Other (Weapon Gems, Lore, Miscellaneous); Materials, last, under the hand-made categories that answer to "materials" (Alchemy, Gems, Ingredients, Monster, Ores, Plants, Shroud, Stone, Textiles, Wood) and Other Materials.
- **From the game's data, not names:** `Scripts\make_item_categories.py` files each of the game's 3,942 holdable items from its game category, its equipment slot, the sequence a consumable plays, and the components of the object a placeable places (a crafting station or machine, storage, a seat or a bed, a door, a showcase, an animal). The game has no Stations category of its own. Keys carry the lockpicks' item tag, so all of them land in Tools, though the game files some keys as materials. The table (`data\item_categories.json`) is built into the mod; an item it does not know (a later game version, another mod's item) goes by the game's own category.
- **Order inside a category:** by kind (an armor slot from head to feet, melee then ranged weapons then shields, beds then seats, a potion's purpose, a spell's element, a placeable's kind such as tables or lamps), then tier, then name.
- **The grouped view:** a header per group and per category, with the number of items shown; LMB on one folds or unfolds it, and the folded ones are kept in the state file across launches. A bar above the list shows one group at a time (All, Favorites, Gear, ...). A favorite is listed at the top under Favorites and again in its category. The per-chest view and the backpack side follow the same order without headers.
- **Hidden categories:** the Categories tab lists the tree, groups and their categories, each with its checkbox and item count; a group hides all of its categories. At the first start Materials and Building are hidden (what is only used through the craft menu or the Construction Hammer); the hidden list of the category file's categories is no longer used for the list.
- **Quick Deposit:** a chest named for a tree category or group also takes it ("Tools" takes keys and lockpicks, "Gear" everything of Gear, "Station" the crafting stations), besides what the category file's words already asked for. Materials keep the category file's words.
- **Tests:** the table's reading, an item's place (the table, the hand-made materials, the game's category for unknown items), hidden by key or group, chest names, the tree's order with favorites twice, and the saved hidden and folded lists.

> [!warning]
> Not yet seen in game. To see: the order of the groups and categories, where keys, lockpicks, chests, stations and beds land, folding a header and finding it folded after a restart, the category bar, the Categories tab's checkboxes, and a Quick Deposit into a chest named "Tools".

## 0.8.1

Two asks of the user on 2026-11-22: the screen shows none of the game's item information ("hovering food doesn't tell the player what buff it gives ... If taking a weapon, we don't know what damage it deals"), answered with "Let's try building our own first and see how it goes"; and "can we add a settings tab so we don't have to go to Kiln's settings to change them?".

- **Item details on hover:** hovering an item's icon or name in the list, a grid cell, or the selected item shows the rows of the game's own tooltip for that item type with the game's labels in the player's language, and the item's description. For food and drink these are its effects and its duration (for example "+40" max health for "20min"); for spells and tools their costs and numbers. Kiln reads the rows (`Content.TooltipValues`, the item's `uiValues`) on the hosted world's tick, 24 items per pass of the chests and only once each, and the texts (`Content.Text`) on the game client's main thread, again when the game's language changes. Not in them: damage and its element (the game computes them from `damageSetup`), a stack's level and rarity, gems and perks.
- **Settings tab:** the window has a fourth tab with every setting of Kiln's mod page (linking, favorites, quick deposit, highlight), grouped, each with its explanation on hover. A change is written by Kiln as from its own page (`TrySet`, which saves the file); a slider's value is written once it has stayed the same for 0.4 s.
- **Rules and tests:** durations as the game writes them ("45s", "20min", "1h 30min"), a detail line, the game's markup taken out of a text.

> [!warning]
> Not yet seen in game. To see: hovering a food in the list and the grid shows its effects, duration and description in your language; the Settings tab's changes apply and are kept after a restart; kiln.log's "stats: streamlined-logistics" while the screen is open.

## 0.8.0

Renamed **Streamlined Logistics**, the user's choice on 2026-11-22 ("since it describes the intent rather than the concept"), as the mod grows into an all-in-one storage mod (the roadmap is in `..\TODO.md`). Nothing else changes in what it does; 0.7.1's experiments are in as they were.

- **New names:** mod id `streamlined-logistics` (was `remote-chests`), `StreamlinedLogistics.dll`, namespace `StreamlinedLogistics`, the screen's title and its key's name. The project folder is `Game Mods\Enshrouded\Streamlined Logistics`. The native companion is **Streamlined Logistics Glow** 0.3.0 (`greeneye\streamlined-logistics-glow`, exports `StreamlinedLogisticsGlow*`), unchanged otherwise.
- **What the player had is taken over once**, at the first start: the settings (`remote-chests-client.json` copied to `streamlined-logistics-client.json` in the profile's config folder), the links and favorites (`kiln\data\remote-chests\remote-chests.json` copied to `kiln\data\streamlined-logistics\streamlined-logistics.json`), and the key (Remote Chests' key in `keybinds.json`, F2 here, given to the new binding and saved there). Each is copied only while the new place is empty; the former files stay as a backup. kiln.log says what was taken over.
- **Install:** `Scripts\Build.ps1 -Install` also removes the former drop-in `kiln\mods\remote-chests`, and `Scripts\Build-Glow.ps1 -Install` the former `greeneye\remote-chests-glow`, so neither loads twice.
- Git tags from now on: `streamlined-logistics/vX.Y.Z` (the earlier ones stay `remote-chests/vX.Y.Z`).

> [!warning]
> Not yet seen in game. To see: the screen opens with F2 and shows the same links and favorites; the settings are as before; kiln.log names what was taken over.

## 0.7.1

0.7.0 seen in game (the user and the logs, 2026-11-21 20:28 to 20:51).

**The chest outline:** the user got boxes, not the outline. The companion started ("glow: ready (4 iterator calls in 2 render systems)", 6 hooks through the loader, no fault) and took every pulse, but lit no model: kiln.log says "the outline is on 0 model(s)" each time, "a view came without a registry". The render systems' iterators carry no registry at all, which Quick Deposit's own comment hints at and its registry list covers; 0.1.0 had only "the last registry a render system carried", which never came. The boxes showed over the game's menus (the map) and from any distance.

**The use experiment:** in the first world three uses went through: the buff's program started, the item's values were written before its first run (for the omelette 0x4bf0438d = 0.12 and 0xc24ecbbb = 9, none refused), the buff landed and one item left the chest. The user saw only the category and the time on the buff, no effect lines, and did not look at the character's numbers; the three foods give regeneration and magic damage, which the log's max health, stamina and mana can't show. The log also reads 1,195 s left on a buff asked for 2,400 s, and 1,349 s for 2,700 s: half, not explained. After a change of world and character, every use stopped with Kiln's error "a Kiln program with this GUID is registered already": Kiln 0.2.0-dev.30.1 does not put its own programs back into the game's program list when the new world's list has the old one's address (`refresh_programs` skips a program whose `inserted_in` equals the registry), so its `ApplyBuff` fails for the rest of the game session. Kiln's own log shows the same refusal for its asset program at that world start.

- **Outline, Remote Chests Glow 0.2.0:** the registries are learned as Quick Deposit learns them, from the calls to ViewInit in six client systems (redirected only; a registry is noted with the time while something is to be lit, and one not seen for 3 s is no longer used). At the end of a render iteration, the registry in which a chest to light has a model the renderer knows is the one used. The state the mod logs now also says how many registries were seen and whether none held the chests' models.
- **Boxes:** drawn only in the play view, not while a menu is open (the same for the Chests list's mark), and only on chests within 200 m, both when the deposit ends and while the character moves away.
- **Use, after a world change:** the mod says in a notice that using items from a chest needs a game restart after a change of world, and uses nothing, in place of the bare "Stopped:" line.

> [!warning]
> Not yet seen in game. Built, not installed while the game was running. To see: the outline on the chests after a deposit (kiln.log: "the outline is on N model(s)"); no box over the map; a food that raises max health used from a chest, and the log's "health max" before and after; what time the buff shows against the food's own.

> [!note]
> Open on the use experiment: why the buff's tooltip has no effect lines (the game's client seems to learn the food from the eating action itself, which a mod's buff does not have: a reading of the data, not verified), whether the numbers really change, and the halved time.

## 0.7.0

A second experiment, asked for on 2026-11-21 right after the first ("Build the chest highlight experiment too so I can test both at once"): the chests a quick deposit put items into are lit, as Quick Deposit lit them, and not with a drawn box. 0.6.0's use experiment is in this build unchanged and still unseen.

- **Chests lit after a quick deposit:** every chest that received items is lit while an inventory or this mod's screen is open (60 s at most), then for the Highlight Time. A new setting, Highlight Chests After A Deposit (`highlightDepositedChests`, on), switches it off.
- **The chests themselves, by a native companion:** Kiln has no call that outlines an entity, and the game's own highlight component is on no chest. So the outline comes from a second, native mod, **Remote Chests Glow** (`native\remote_chests_glow.cpp`, built by `Scripts\Build-Glow.ps1` into `dist\remote-chests-glow`, installed in `<game>\greeneye\remote-chests-glow`, loaded at game start by Kiln's Greeneye host like Quick Deposit itself). It is derived from Quick Deposit's `glow.cpp` (MIT, its license is beside the DLL): the same renderer calls, found by the same patterns and redirected through the Greeneye loader, set highlight group 6 on a chest's render instance and put the old group back when the time is up; group 6 pulses in the mod's gold while chests are lit. Remote Chests finds the DLL in the process and calls its four exports (ready, pulse, stop, state).
- **What the companion leaves out of Quick Deposit's glow:** its own reading of the game's registries (1,400 lines with their own hooks) and the silhouettes it draws through walls. A chest is told by its entity id alone, in the registry the render system's iterator carries (or the last one a render system carried), and a lit chest shows where the game draws an outline, not through walls.
- **Boxes when the outline can't be had:** without the companion, when its hooks could not be placed, or when it lights no model within a second and a half, each chest gets the box and name the Chests list's mark uses.
- **Chests list:** the chest clicked there is lit the same way once the screen closes, besides its box, label and arrow.
- **Log:** kiln.log has "highlight:" lines (how many chests, outline or boxes, how many models the outline is on); the companion writes `remote-chests-glow.log` in its folder ("glow: ready" or what was not found, and the first 20 pulses).

> [!warning]
> Not yet seen in game, and the native part is new: it runs inside the game's render systems. It compiled against Quick Deposit's sources of 1.3.25, whose glow worked in this game until Quick Deposit was switched off on 2026-11-21. To see: the game starts and loads a world as before; after G twice in a base the chests that received items pulse gold once the inventory closes, for the Highlight Time, and then look as before; `remote-chests-glow.log` says "glow: ready".

> [!note]
> `Scripts\Build.ps1 -Install` installs the Kiln mod and `Scripts\Build-Glow.ps1 -Install` the companion; the companion's build needs `..\Quick Deposit\build\source`, which `..\Quick Deposit\Scripts\Build.ps1` makes. To take the companion out, delete `<game>\greeneye\remote-chests-glow`: the mod then draws boxes.

## 0.6.0

An experiment the user chose on 2026-11-21 ("Let's try 2 first"), after the first idea for using a stored consumable (apply the item's buff, remove one) turned out not to hold. What the game's data says (build 26430625): the foods and drinks whose use is "apply the item's buff and remove one item" share 16 buff types, and a buff type holds no numbers. A food's numbers (for example +40 max health for 20 minutes) are the item's `impactValues`, which the game copies into the buff's program when it starts, because a buff made by eating knows the item that was used (`ImpactItemConfig.usedItem`). Kiln's `Abilities.ApplyBuff` names no used item, and `Abilities.PlaySequence` refuses a sequence that consumes one.

- **Use a stored food or drink (experiment):** in the grid, MMB on a stored item, and a Use button beside Take Stack in the list and for the selected cell. The mod applies the item's buff with the item's own time, holds the buff's program for one step at its start (`Abilities.HoldPrograms`, `ImpactStartingEvent`) and writes the item's numbers into the program's variables by their config ids before its first run. It then waits up to 45 world steps for the buff to show on the character (`Abilities.BuffsOf`), and only then removes one item from the chest, from the smallest stack. A buff that lands without its numbers is taken off again and the item stays; a buff that does not land (no free food slot, the same kind still active) leaves everything as it was, with a notice.
- **Which items:** 148, listed in `data\usable_items.json`, which `Scripts\make_usable_items.py` writes from the workspace's dump and the build puts inside the DLL. An item is in when it is a consumable whose primary sequence has exactly one ApplyBuffEvent that consumes the item and applies the item's own buff, with no event the mod can't stand in for (spawning an entity, paying a cost, repairing), and whose values are whole numbers, floats and one time. Four flasks are left out (their values name an attribute). Potions, bandages, antidotes and scrolls are not in: their sequences heal, spawn or pay a cost.
- **Not done by the mod:** the eating animation and its wait, and the instant change of health, stamina or mana a food's sequence may add through a second program.
- **Log:** each use writes "use:" lines to kiln.log: the buff and the food buffs before, what was written into the program, whether the buff landed, the food buffs after, and max health, stamina and mana before and after.
- **Tests:** the usable items list (entries left out when unreadable), whether a buff landed, the stack a used item comes from.

> [!warning]
> Not yet seen in game, and not known to work. To see: the buff shows in the game's own buff bar with the right icon, name, numbers and time; max health or the other numbers change as the food says; one item leaves the chest; eating the same kind twice and eating with every food slot full leave the chest untouched; the buff is still right after leaving the world and loading it again.

## 0.5.4

0.5.3 seen working in game (the user, 2026-11-21 13:06: "Favorites border in inventory and shift RMB work, same in chests"; kiln.log shows eight Shift + RMB, each on the slot drawn under the pointer, in the backpack and in a chest, and "stats: remote-chests" at pre 0.0015 ms per step). Not said: what the game itself does on Shift + RMB.

- **Fix, the screen's Quick Deposit button inside a base:** it sent the backpack to the chests of the base the player stood in. It now always sends to the linked chests, in a base or not, and without the setting Quick Deposit Outside A Base, which is about the inventory's own key. Sort Inventory twice and a Quick Stack Station keep sending to the current base's chests.

> [!warning]
> Not yet seen in game. Built, not installed while the game was running.

## 0.5.3

0.5.2 seen in game (kiln.log and a screenshot, 2026-11-21 12:35 to 12:38, a 3840x2160 screen): Kiln now has the slots (80 in the backpack, 192 in a chest, all with rectangles, favorites among them), draws the borders and delivers Shift + RMB, but at the wrong place. The seven borders on screen are twice the slots' size and at twice their position: a favorite in the first column of the second row has its border around the third column of the fourth row. The cause is in Kiln 0.2.0-dev.30.1 (`map_frame` in `native\core\src\services\ui_values_inventory.cpp`): a slot's pixel is viewport + unit x pixels per unit (2 at 4K), and Kiln multiplies that again by the target's size over the canvas's, but the canvas is in UI units (1920x1080), so at 4K a unit becomes 4 pixels. At 1920x1080 both are equal and nothing shows. The same function places the clicks, so the two Shift + RMB the log shows marked the items at half the pointer's position (items 0x07e67b5b and 0x56b0f8fe became favorites), and in the chest screen no press landed on anything.

- **Fix, favorites in the game's own screens:** the mod no longer uses Kiln's marks. Every overlay frame, on the game's screens that show item slots (inventory menu, backpack, chests, crafting stations), it reads the slots the game drew (`Ui.Values.Slots`) and draws the gold border itself at viewport + unit x pixels per unit, scaled to the overlay (`ChestRules.SlotPixels`). The play view's hotbar gets no border: nothing is read while playing.
- **Fix, Shift + RMB there:** the mod tests the pointer against the same rectangles itself, on the frame where RMB goes down with Shift held (and neither Ctrl nor Alt) while the game's window is in front, and toggles the favorite of the slot under it. Kiln's click handler stays registered only so that Kiln keeps the press from the game where it sees a slot; until Kiln's placement is fixed the game gets the press too, and may show its own Actions for the slot.
- **Log:** the per-second "game screens:" lines of 0.5.2 are gone. One line per session gives the numbers the slots are placed with (viewport, pixels per unit, overlay size, the first slot in units and in pixels), and the first ten Shift + RMB give the pointer, the item and the slot's rectangle.
- **Tests:** a slot's pixels at 4K, in a window half that size, with bars at the viewport's sides, and numbers that can't be right.

> [!warning]
> Not yet seen in game. Built, not installed while the game was running. To see: the borders sit on the favorites' slots in the backpack and in a chest; Shift + RMB marks the item under the pointer; what the game itself does on Shift + RMB. The two items marked by mistake in 0.5.2's session are still favorites until unmarked.

> [!note]
> The bug is Kiln's and worth reporting to its author: in `map_frame`, `kx = tw / canvas_width` should divide by the viewport's width in pixels (the game's picture), and the same for `ky`. Its marks, clicks, hovered slot and the placement of docked panels all go through it.

## 0.5.2

0.5.1 seen in game (kiln.log, 2026-11-21 11:44 to 12:16): the lag is gone ("stats: remote-chests" reads pre 0.0015 ms and post 0 ms per step), and a Quick Stack Station starts the quick deposit by the new way (five uses logged, 63 items into 6 chests at the first). But favorites show no border in the game's own inventory and Shift + RMB there does nothing, although both registered: Kiln logged its slot hooks installed and "slot clicks on".

- **Diagnostic for the game's own screens:** about once a second the mod reads what Kiln sees of the game's slots (`Ui.Values.Slots`) and writes a "game screens:" line to kiln.log whenever the picture changes: whether there is a usable slot frame, whether slots have rectangles, whether favorites are among them, the game's screen, whether the pointer is on a slot, and the viewport and scale Kiln maps with. Each Shift + RMB that reaches the mod is logged too. At most 30 and 10 lines a session. The marks and the clicks both stand on those frames, so this says which part is missing.

> [!warning]
> Nothing is fixed by this version: it only finds out. Built, not installed while the game was running.

## 0.5.1

- **Fix, the whole game lagging with 0.4.0 and 0.4.1:** the character rubber-banded while moving and actions such as spending a skill point took a few hundred milliseconds. To notice a Quick Stack Station, those versions asked Kiln after every world step for the game's deposit events (`GameEvents.Each`), and each such call costs about 60 ms: kiln.log's "stats: remote-chests" lines show post 48 to 67 ms per step in both sessions, and the hosted world ran at 12 to 14 steps a second instead of 60. Nothing is asked per step any more. A station is now noticed when the game answers the player's interaction with it (`InteractionResultEvent`, the station told by its template), and the quick deposit follows 0.6 s later.
- **Tests:** which templates are Quick Stack Stations.

> [!warning]
> Not yet seen in game: that the lag is gone (kiln.log's "stats: remote-chests" should read well under 1 ms per step), and that a Quick Stack Station still starts a quick deposit by the new way (kiln.log: "quick deposit: Quick Stack Station N used").

> [!note]
> What still runs per step is a few comparisons. While the screen is open the chests are read twice a second, which costs a few milliseconds each time.

## 0.5.0

Asked for after 0.4.1 was seen in game (kiln.log, 06:23: a Quick Stack Station ran a quick deposit of 830 items into 6 of the base's 35 chests, favorites kept).

- **Favorites in the game's own screens:** a favorite has a gold border in the game's inventory and chest screens, and Shift + RMB on a slot there marks or unmarks its item (Kiln 0.2.0-dev.30's `Ui.Inventory.MarkWhen` and `OnClick`). Setting Favorites In The Game's Screens, on. The mod now needs Kiln's mod API 0.2.12.
- **Quick Deposit button** on the Items tab.
- **Category headers:** with the chests grouped, the items are under headers: Favorites, then each category in the category file's order, then Other. Chest by chest, the lines take the same order without headers.
- **Right side:** Hotbar (when included) and Inventory are two headers, Hotbar first.
- **Chests tab:** a click on a chest's name marks it in the world once the screen closes (a box, a label with the distance, an arrow at the screen's edge), for Highlight Time seconds (setting, 15 by default), when it is within 200 m.
- **Tests:** the headers and their order.

> [!warning]
> Built and tested offline, never installed by itself: the game was running, and 0.5.1 followed. Nothing of it seen in game.

> [!note]
> Not done yet: using a stored consumable with MMB. Eating is the game applying the item's buff and removing one (`docs\re\player-state.md`), which Kiln can do with `Abilities.ApplyBuff` and `Items.Take` for food and drink; what the game does when the food slots are full has to be seen in game first.

## 0.4.1

After the fourth test in game (0.3.0 and 0.4.0, 2026-11-21). kiln.log of 0.4.0: the sort key is seen (Sort Inventory names inventory 1, the character), and the old Quick Deposit says "disabled in quick-deposit.ini". No quick deposit ran yet: the tries were outside a base.

---

- **Category list, three corrections** (in `chestsort_categories.json` in the game folder, which took effect at once; the file before is in `enshrouded-mod-backups\2026-11-21 chestsort_categories before Remote Chests 0.4.1`):
  - New category **Ingredients** (answers to "food", "cooking" and "materials"): the 23 cooking ingredients that were under Food, such as the five Ectoplasms, Salt, Flour, Spice, the raw skewers, rice and coffee beans. Being a materials category it is hidden. A chest named Food still takes them. Animal feed stays under Food.
  - New category **Weapon Gems** (answers to "gems" and "sockets"): the 29 gems that go into a weapon, taken out of Gems, which keeps the 17 raw stones and stays hidden. A chest named Gems still takes both.
  - The five ancient blocks listed under Decor (Moonstone and Ancient Stone Block among them) are under Blocks. Raw Wool moved from Food to Textiles.
- **A category added to the list later** starts out hidden when it is of the hidden sort (it answers to "materials", or is Blocks or Terrain), without undoing what the player chose for the others: the state file now also remembers which categories it has seen.
- **Fix, icons staying on placeholders:** Kiln reads a trimmed icon at twice the size drawn and keeps at most 256 icons in 24 MB. A grid cell of 58 units is more than 64 pixels on a large screen, which asks for the 256-pixel pictures, and only about 90 of those fit. Cells are now buttons of the same size with the icon drawn on them at 64 pixels at most, which stays on the 128-pixel pictures.
- **Grid layout:** cells are placed by hand, each at its own spot, in place of 0.2.2's widest-cell rule.
- **Fix, tool entries:** a slot holding the Construction Hammer or the summoning staff reads as what the tool has selected (a `Blueprint_...` shape, an `NPC_...` helper). With Include Hotbar on those showed as items. They are left out of the Backpack list, and a quick deposit skips them.
- **Quick Deposit Outside A Base** (setting, off by default): on, a quick deposit outside a base goes to the linked chests, wherever they are, by the same rules.
- **Tests:** new categories, tool entries.

> [!warning]
> Not yet seen in game: all of 0.4.1, and a quick deposit inside a base.

## 0.4.0

The deposit side: Quick Deposit's part (GreeneyeDS, Nexus 195, MIT), so that mod can go and one category list is left to maintain. The user picked which of its features to port: G twice, the top-up, the category and same-item rules, the Quick Stack Station, what is never moved, and the chat report, with favorites in place of its Keep marks. Left out on purpose: the nearest-chest fallback, since it can put items where they are not wanted, and the hold-R pull.

- **Quick Deposit:** inside a base, Sort Inventory pressed twice within half a second (G by default; the first press still sorts) puts the backpack's items into the base's storage chests. A Quick Stack Station does the same after its own deposit.
- **Where a stack goes:** first it tops up the stacks of its item that the base's chests already hold and that are not full, the chest holding the most first. What is left goes to a chest named for the item's category ("Ores & Gems", "Ammo 2", read as Quick Deposit read them, from the same `chestsort_categories.json`), else to a chest that already holds the item; among several, the one holding the most of it, then the nearest. The chest needs a free slot. **What no chest asks for stays in the backpack.**
- **Never moved:** the hotbar, worn things, favorites. Chests whose name starts with `--` get nothing, and neither do Magic Chests with Receive Quick Stacking off. Regular chests of the base are used like Magic Chests, linked or not: linking is for the F8 screen only.
- **Report:** a notice with the total, your chat with one line per chest and what stayed (at most 8 chests, then a count), and the whole of it in `quick-deposit-report.txt` in the mod's Kiln data folder. Setting Deposit Report In Chat, on by default.
- **Setting Quick Deposit**, on by default. While the Greeneye Quick Deposit is loaded and switched on, this part stands down and kiln.log says so: set `enabled=0` in `greeneye\quick-deposit\quick-deposit.ini` (game closed) to hand G twice and the stations over.
- **Tests:** chest names to categories (words, plurals, shared words), the `--` rule, the top-up order and the choice of a chest, including that an empty chest nobody named gets nothing.

How it is done: the sort key is seen as the client's `sortInventoryAction` command (`PlayerClientActionEvent`; Kiln holds each of the player's commands one step while this is watched), on the character's own inventory only. A station's deposit is the game's `keen::ecs::AreaDepositEvent` by the local player, read after each step. The base is `Bases.Of(player)`, and a chest belongs to it when `Bases.At(its position)` is the same base. Every move is `Items.Move` from a backpack slot into the chosen chest.

> [!warning]
> Not yet seen in game. To watch first in kiln.log: "quick deposit: Sort Inventory on inventory N" (whether G on the backpack names the character), and the "quick deposit (...)" line after a run.

> [!note]
> Quick Deposit's Keep marks (J on this PC) are not read. Its kept items need a favorite mark here before that mod is switched off: on 2026-11-21 its ini kept Freeze Resistance, two lockpicks, Thyme Tea and five Eternal spells.

## 0.3.0

What makes the list usable with many linked chests: the categories that are only noise are left out, and the chests can be looked at one by one.

- **Hidden categories:** an item whose categories are all hidden is left out of the Linked Chests list. Hidden at first: the 11 categories that are only ever used through the craft menu or the Construction Hammer (those that answer to the word "materials": Alchemy, Gems, Monster, Ores, Plants, Shroud, Stone, Textiles, Wood; plus Blocks and Terrain). A favorite always shows. A search looks through everything, hidden or not. Show Hidden Categories on the Items tab shows everything for the time being. Under the list, a line says how many items the categories left out. The Backpack side is never filtered.
- **Categories tab:** every category with a Shown checkbox and the number of items of it in the linked chests, and Show All, Hide All and Reset To Default. The choice is kept.
- **Group / Ungroup:** Ungroup shows the content chest by chest, each under its name and its used slots, chests in name order; Group goes back to one list. It works in list and grid view, with the same filters, and a chest with nothing to show is left out. In this view Take takes from that chest. The choice is kept.
- **Tests:** the category rules (names without case, `*`, ids, `-` entries in order, the rest category), the defaults, hiding, and the kept choices. The rules were also run over the real file and the game's 4,268 items: 526 in no category and none in two, with the same count per category as Quick Deposit's own test.

> [!note]
> Renaming a chest from the Chests list was asked for with this and is not in it: Kiln can read a chest's name but has no call to set one (its notes say "Kiln does not rename"; the game's own rename goes through a message Kiln does not send). Open: ask Kiln's author for it, or give chests a name of the mod's own that only this screen shows.

> [!warning]
> Not yet seen in game.

## 0.2.2

After the third test in game (0.2.1, 2026-11-21): takes use the whole backpack (kiln.log: a take into the backpack item's page 10354), and the layout line shows the worn item as `Backpack_T3_NEW` with five pages and 40 unlocked slots.

- **Fix, grid cells overflowing their pane:** some cells ran past the right edge until the Backpack side was scrolled down. Kiln draws an icon button that is scrolled out of view as bare space, which is narrower than a button, and the grid counted its columns from the width of the last cell drawn, often such a hidden one at the bottom of the Backpack side. The columns are now counted from the widest cell seen, and a narrow cell gets the difference as extra space after it.
- **Built against Kiln 0.2.0-dev.30.1**, installed that day.

> [!note]
> The Active Mycelium lost by 0.1.0 was not found. kiln.log's list of everything outside the backpack and hotbar (armor, rings, ward, grappling hook, glider, backpack, spells) holds no material, so it is not in an equipment slot now. Two explanations are left, and the log of that session is gone: it merged into a stack already in the backpack (two different items are both named Active Mycelium, the material and the terrain kind, and the stack sizes are 9999), or it sat in an equipment slot and the game dropped it at the next load. Since 0.2.0 a take names its backpack slot, so neither can happen again.

## 0.2.1

After the second test in game (0.2.0, 2026-11-21): chests are told apart correctly (37 Magic Chests, 1 regular, no crops), takes land in the backpack with a notice, favorites got their keep rules. But the stranded list showed ordinary backpack items, and Take said the backpack was full when it was not.

---

- **Fix, the whole backpack is seen:** 0.2.0 took the pages behind the character's Virtual link for a tool's inventory. kiln.log's layout line showed what they are: the character's own setup has five item pages (two of them the hotbar, 40 slots, all unlocked), and its Virtual link leads to the backpack item it wears, which carries the rest of the backpack with its own count of unlocked slots. Both parts are the backpack now, so Take uses all of it and the Backpack list shows all of it.
- **Fix, stranded items:** what 0.2.0 listed as stranded was the larger part of the backpack. Its Move To Backpack did nothing: Kiln refuses a move within one owner. The lost Active Mycelium is now looked for where Kiln's own slot search could have put it before any backpack page: a plain material in an equipment or currency slot. Move To Backpack passes it through a linked chest with a free slot, since Kiln only moves between two owners.
- **Log:** at the first opening, kiln.log has the character's links, each carried inventory with its template and unlocked count, and everything outside the backpack and hotbar.

---

- **Grid, mouse:** LMB selects an item on either side. RMB moves one stack to the other side (take from the chests, store from the backpack). Shift + RMB marks or unmarks a favorite. RMB on empty space lets go of the selection. The selected item's line has Take Stack and Take All, or Store, and Favorite.
- **Favorites have a gold border** instead of teal, and the Backpack list has a Favorite column too.
- **Labels** are in title case unless they are sentences, and name the buttons LMB and RMB.
- **Link Radius** is a whole number of metres.

> [!note]
> Dragging with RMB between the two sides was asked for and is not possible yet: Kiln's drawing calls report a click on a widget, but neither a held button nor the pointer's position. RMB on an item moves it to the other side in one click instead.

> [!warning]
> Not yet seen in game. 0.2.0's notes about tools' inventories below were wrong, as explained above; the entries the Construction Hammer and the summoning staff showed in 0.1.0's list were hotbar slots.

## 0.2.0

After the first test in game (0.1.0, 2026-11-21): the screen, the icons, the search, favorites and moves worked, also from a base far away. One taken stack of Active Mycelium could not be found afterwards, and the lists showed things that are not chests or not carried.

---

- **Fix, taken items always land in the backpack:** 0.1.0 let Kiln choose the slot a taken stack goes to, and Kiln looks through every inventory the character owns, including the ones tools carry (the Construction Hammer's shapes, the summoning staff's helpers). Now each piece of a stack goes to a backpack slot chosen by the mod: a stack of the same item that is not full, else the first empty unlocked slot, and never more than that slot has room for. The move's own report is checked afterwards, and anything that still lands elsewhere is reported on screen and in kiln.log.
- **Fix, only the backpack is listed for storing:** the Backpack list no longer shows what the Construction Hammer and the summoning staff hold.
- **Fix, only chests are listed:** 0.1.0 told chests by a component that only exists in the player's game, so in game it found none and listed all 940 containers, crops and fruit trees included. A chest is now told by the template it was made from (`Spawn.Inspect`, the 12 storage chest templates by id, a new one by name).
- **Stranded items:** anything in a tool's inventory that is not a shape (`Blueprint...`) or a helper (`NPC...`) is listed at the top of the Items tab with a Move To Backpack button. At the first opening kiln.log lists the character's inventory pages and everything the tools' inventories hold.

---

- **Pickup notice:** a notice says what arrived in the backpack ("+4 Active Mycelium") and what was stored.
- **Grid view:** a Grid View / List View button switches both lists, and the choice is kept. A cell shows the icon and, always, the count; a favorite has a teal border. Hovering shows the name, the count and the chests holding it, by name. Right-click takes a stack, Shift + right-click takes all, a click selects the item: its line above the grid has Take Stack, Take All and Favorite. In the Backpack grid, right-click stores.
- **List view, Chests column:** the chest's name when one chest holds the item, else the number of chests with their names on hover.
- **Favorites stay in the backpack:** the game's own Deposit Stacks, Deposit All and Quick Stack Station leave favorite items in the backpack (Kiln's keep rule, one per favorite item, at most 200). Setting Favorites Stay In Backpack, on by default. Store in this screen still moves them.
- **Regular chests:** only Magic Chests are offered for linking unless the setting Link Regular Chests is on (off by default). A chest that is already linked stays listed either way. The Chests tab has a Kind column.
- **Hotbar:** the Include Hotbar switch on the Items tab adds the hotbar's items to the Backpack list, and they can then be stored too. Off by default; the choice is kept.
- **Tests:** the chest kinds, the character's page layout, the choice of a backpack slot (never a tool's page, never the hotbar, never a locked slot), what counts as stranded, and the short counts.

> [!warning]
> Not yet seen in game: all of the above. To watch first: the kiln.log line "player N's inventories" (it shows whether the backpack's pages and its unlocked slots are read as assumed: a slot's saved number is 8 x the item pages before it + its index, and slots numbered below the unlocked count are usable), and whether other mods that hook quick stack (Quick Deposit, Smart Quick Stack) still work with Kiln's keep rules active.

> [!note]
> Later, asked for on 2026-11-20 and 21: hiding materials, blocks and terrain by category (`chestsort_categories.json`); a Use action for stored consumables; Shift and Ctrl turning Take Stack into x10 and x100; in the grid, middle-click to use; Quick Deposit's features, merging stacks across chests and auto-receive in one mod. Quick Deposit's Keep marks (K) are still its own until then.

## 0.1.0

A proof, to find out in game whether Kiln's screens, item icons and moves hold up, and whether chests far from the player can be read.

- **F8** opens and closes the screen in a world you host; the key is Open Remote Chests in the Kiln menu's Keys tab. Escape closes it too.
- **Chests tab:** every storage chest the world holds, nearest first, with its name (the one typed with Rename), slots used and distance. A checkbox links or unlinks one; Link Nearby Chests links every chest within the Link Radius setting (100 m by default, 5 to 500); Unlink All clears the links. Linking moves nothing.
- **Items tab, Linked Chests:** one line per item across all linked chests, with its icon, total and the number of chests holding it. Gear has one line per level and rarity. Take Stack moves one stack to the backpack, Take All every stack of the line.
- **Items tab, Backpack:** one line per item you carry (not what you wear). Store moves it into the linked chest that already holds the most of it, else the nearest one with a free slot, and goes on to the next chest when one is full.
- **Favorites:** the checkbox at the start of a line marks the item; favorites come first, and Favorites Only hides the rest.
- **Search:** every word typed must be in the item's name, in any order and any case. It filters both lists.
- A storage chest is told from stations and loot chests by its components (`ClientToggle` without `RemoveInteractionOnEmptyInventory`, checked against the 594 templates with an inventory in the game's data). If that test names no chest in the world, every placed container is listed instead, and kiln.log says so.
- kiln.log has one line per move ("take: 250 of 250 x Wood Logs from chest 1651: Done") and, at the first opening, how many containers and storage chests the world holds.

> [!warning]
> Not yet started in game. Unknown until then: whether the screen and the icons draw, whether the moves reach the backpack and the chests as the game's own do, whether the chest test works in the hosted world, and whether chests far from the player stay listed.

> [!note]
> XHL-Remote Chest stays installed next to it. Both read the same chests; neither knows the other's links.
