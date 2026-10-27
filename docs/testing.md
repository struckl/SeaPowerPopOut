# In-game test checklist

Log: `<Sea Power>\BepInEx\LogOutput.log` (overwritten on every game start). Config:
`<Sea Power>\BepInEx\config\dev.sewerlabs.seapowerpopout.cfg`. Installed mod (local build):
`<Sea Power>\Sea Power_Data\StreamingAssets\SeaPowerPopOut\`. Needs a second monitor connected before the game
starts. Anchor Chain loads the mod whether or not it is ticked; the mod itself checks `usersettings.ini` (LocalLow\Triassic Games\Sea Power) and logs `Unticked in the Mod Manager - inactive.` – verify by unticking and restarting.

## Pop-Out Windows – free-floating windows on another monitor (0.2.0)

Monitor numbers below are an example: game on monitor 1, the game's detached world view on monitor 2, monitor 3
free.

1. **Start Sea Power**. In the log once the main menu is up: `Sea Power Pop-Out Windows 0.2.0 loaded`, the
   monitor list (`Monitor 1: 3840x2160 (game)`, `Monitor 2: …`) and `Window hooks in place. Pop-out monitor: 3` – a
   monitor that shows the game's world view or second-screen map is skipped. No `Pointer fix not applied` warning.
2. Start any mission. Every window opened from the bottom bar (formation manager, message log, camera controls,
   unit status) and the sensor / doctrine windows have a new button left of the **X** in their title bar (a box
   with an arrow). The map has none – it stays on the main screen.
3. Click it on the **formation manager**:
   - The other monitor does **not** turn black: only the formation manager shows there, with a thin frame around
     it; the desktop (or whatever program is on that monitor) shows everywhere else, and clicks there go to it.
   - Log: `Window of monitor 3: … at …`, `Pop-out screen open on monitor 3 (…)`, `FormationManagerViewModel
     popped out to monitor 3`; on a monitor of another height than the game's also `Pointer on monitor 3 shifted
     by … px`.
   - Hover the window's edges: the resize cursor appears right at the frame. Resize from every edge – it follows
     the mouse without jumping, also beyond the game's usual maximum size. Near the ends of an edge the cursor
     turns diagonal and both sides resize at once.
   - Drag it by the title bar: no jump when you grab it.
   - Click units in it, reorder, rename: everything works as on the main screen. Clicks there do **not** select,
     deselect or order anything in the world or on the map.
   - Keyboard shortcuts (time compression, pause) still work while the mouse is on the other monitor.
   - The bottom-bar button for it stays lit; clicking it closes the window over there, clicking again opens it
     there again.
4. Pop out the **message log** and **unit status** too. Drag one next to another: within a few pixels it snaps
   beside / below it with one frame width between them, edges in line; also to the monitor's edges.
5. Tooltips over items in a popped-out window, right-click menus and drop-down lists show in full, also where they
   reach past the window's edge.
6. Drag a window fast so the mouse runs off it onto the desktop and release there: the window must not stick to
   the mouse afterwards.
7. Click into another program, then back into the game: while in the game the popped-out windows stay above other
   programs on that monitor; while in the other program they are normal windows (it can cover them).
8. On the **main screen**, resize any window from near a corner along an edge: diagonal cursor, both sides resize.
9. The button in a popped-out window's title bar points back in: click it – the window returns to the main screen.
10. Leave two windows popped out, **restart the mission** (or load another): they open on the other monitor again,
    where you left them. Back in the main menu, the other monitor shows nothing of the game.
11. Close the game normally. No `[Error :SeaPowerPopOut]` lines in the log.

Worth reporting: a black strip or frame anywhere on the other monitor besides the thin window frame, parts of
tooltips or menus missing, the pointer off (hover highlights not under the mouse), windows that don't get the
button, the game losing keyboard focus oddly or pausing/muting after a click on the other monitor, sizes or fonts
that look different over there (config `UI scale`). Config `See-through`, `Always on top`, `Snap windows` and
`Wide resize corners` switch the newer parts off.

## 1.0.0 – release check (2026-10-24)

Same build as 0.2.0 apart from the version and the remembered size of self-sizing windows (the formation manager
came back at its automatic width). Run during the screenshot session on Sea Power 0.8.7: 1.0.0 loaded, four windows
popped out, arranged, back in and out again twice, no errors.

1. Log: `Sea Power Pop-Out Windows 1.0.0 loaded.`, the monitor list and `Window hooks in place. Pop-out monitor: …`.
2. In any mission pop out the formation manager and the message log, arrange them, pop one back in: steps 3, 4 and 9
   of the 0.2.0 check above – no `[Error :SeaPowerPopOut]` lines.

## Unit menu and formation manager tabs (unreleased)

Needs a mission with surface ships and aircraft but **no submarines** (so the "Subs" tab is missing in front of
"Air"), e.g. any carrier mission without a sub.

1. Pop out the formation manager. Right-click a unit (a formation, a ship, a plane, a station inside a formation):
   the unit menu opens **on the other monitor**, at the mouse, at the size of the windows there, in full (also where
   it reaches past the window's edge). Pick an entry: it works as on the main screen.
2. With that menu open, click on the main screen: the menu closes. Right-click a unit on the map: its menu opens on
   the main screen; click on the other monitor: it closes.
3. Pop the formation manager back in: right-clicking a unit opens the menu on the main screen, as before.
4. Formation manager (either screen): click the **Air** tab, double-click a plane, then a station inside an air
   formation: the camera goes to it, the list **stays on Air** with the plane selected. Same on the Surface and Helos
   tabs with their units. Double-clicking a unit on **All** stays on All.
5. Log: no `Formation manager tab fix not applied` warning, no `[Error :SeaPowerPopOut]` lines.
