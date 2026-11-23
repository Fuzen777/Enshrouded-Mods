# Auto Doors (Kiln)

Doors in your bases open as you, a survivor or the build camera come near, and close again once everyone has left. A Kiln mod, rebuilt from the behaviour of SellTheWorldGames' Auto Doors (closed source), not from its code.

- Install: copy `dist\auto-doors-kiln` (or the built files) to `<game>\kiln\mods\auto-doors-kiln`. Turn the old Auto Doors off in Vortex.
- Settings: Kiln menu (Insert), Mods, Auto Doors. Distances are in decimetres (40 = 4 m).
- Keys (Kiln menu, Keys): F7 (LB + Y on a controller) cycles the mode of the door you look at (Everyone, Players, Players & Survivors, Manual); Shift+F7 switches the mod on and off.
- Build: `dotnet build src -c Release` (references `kiln\host` of the game install; override with `-p:KilnHost=...`).
- How it works: `CHANGES.md`, probe entries, and the comment at the top of `src\AutoDoorsMod.cs`.
