# Changelog

## 1.1.1 – 2026-10-31

- Fixed: a player reported that the World View window disappeared after the map was switched between fullscreen and
  windowed. Tested in the game on 0.8.7 (several switches, also after moving the window); the globe build was not run.
  Cause: the game rebuilds the window then. The game anchors the map and World View windows to the bottom of a container taller than the screen
  and read the remembered place as the distance to that edge, which put the window below the screen and remembered
  that place again. A remembered place is now final: the mod releases those anchors when it puts a window there. The
  Tactical Display had the same problem. A window already lost this way comes back onto the screen by itself.

## 1.1.0 – 2026-10-30

Fixes after a report from a 3440x1440 main monitor with a 1080p second monitor (not yet tested in the game).

- Pointer on a monitor of another height: also fixed on the game's own second-monitor map and world view (it only
  covered the pop-out windows), and now measured from what the game really does instead of assumed. Should cure the
  map dragging off, the height/depth tooltip far above the cursor, windows jumping when resized, and right-clicks
  landing wrong there. Also corrects a Windows scaling that differs from the main monitor's.
- Log: warns when the game runs in exclusive fullscreen (the world view can freeze with a second monitor; borderless
  windowed avoids it) and prints one `Pointer on monitor N` line with the monitor numbers.

**Smart Windows features.**
Window handling based on rgreat's Smart Windows mod, rewritten as separate small classes under `SmartWindows/`.
`modtool compat` finds every game member in the globe build too, but only 0.8.7 was run in the game. Every feature has a switch in
`BepInEx/config/dev.sewerlabs.seapowerpopout.smartwindows.cfg`.

Run in the game on 0.8.7 (2026-10-30): frame, gear panel, double-click maximize, snapping, window memory across a
restart, look menu (opacity, reset), all Unit Reference features listed below, Unit Status name / Reference tab /
Damage Control height, unit panel menu, time panel drag, campaign loadout order and the Strict loadouts check box,
"_Custom Mission" hidden, resize cursors, link tooltips, keep opened unit, ini colours in the file picker, message
click-through, the unit panel menu for a contact. Not run in the game: the globe build (left for later), Track State
(the Unit Status window could not be opened by simulated input for a contact), dismissing damaged ships, Upgrade ship,
hull-swap price (need a played campaign), mod-only backgrounds (need mod background pictures). Desktop mode was run both with the game's World View on another monitor (Tab switches the
desktops, layouts per desktop) and without it (the 3D world live in the World View window, map window, backdrop, both
desktops).

- Windows: remembers position, size and maximized state of the windows named in the settings and opens several windows of
  one kind shifted; a double-click on a title bar maximizes / restores; a click brings a window to the front; snapping to
  the screen edges and to other windows; dragging windows partly off screen; a thin frame around windows and tooltips.
- Settings panel (gear button in the title bar of a remembered window), with texts in the game's nine languages
  (`lang/*.ini`, editable).
- Unit Reference: the choices (country, category, date filter, name-only, search text) are kept between runs; a splitter at
  the right edge of the unit tree with its width kept; hiding the tree also hides the search panel and narrows the window;
  classes sorted by service date; the tree scrolls to the selected unit; the wheel scrolls the tree only over it; the
  country list shows only countries with units for the current search; "Armed only"; search also reads ids and
  descriptions; a click on a class selects its earliest variant.
- Unit Status: the full unit name in the header; an adjustable minimum height of the Damage Control sections.
- Mission: right click on the flag or name in the unit panel opens the unit's map menu; the corner messages no longer
  block clicks.
- Campaign Task Force: loadouts sorted by date; a damaged ship that can be repaired can be dismissed (refund minus repair
  cost).
- Campaign Task Force Builder: optional "Upgrade ship" button (off by default, setting `Upgrade ship`): sells a ship at the
  Dismiss / Decommission price and buys the selected one with the crew one skill level lower, aircraft move over.
- Menus: "_Custom Mission" hidden in the SCENARIOS list; the mission editor's file picker starts in the last mission's
  folder; optional mod-only background pictures and old-log cleanup (both off by default).
  The mission editor's file pickers show *.ini file names in light blue (colour configurable).
- Look menu (button next to the gear in a remembered window): opacity, scale, text outline, level (bottom / normal /
  top), reset.
- Unit Reference: loadout variants labelled and grouped under their class ("Long-Hull [Early]"); the loadout list for
  ships, submarines and land units; the opened unit kept in the tree; tooltips on reference links.
- Unit Status: a Reference tab (the unit's reference with its loadouts) and a Track State tab for a selected contact.
- Mission: the time panel can be dragged; resize cursors also outside a mission.
- Campaign: "Strict loadouts" check box on the start screen; the loadout panel lists the own loadouts before other
  versions; hull-swap price taken from the campaign roster.
- Menus: the first background is picked again once the mod-only filter is in place.
- Gear button only on resizable windows (not on Confirm dialogs).
- Desktop mode (config only, off by default): the minimap key (Tab) switches between 2-5 desktops, each remembering
  the window layouts. With the game's World View on another monitor the map keeps the whole screen and the desktops
  arrange the windows over it; on a single monitor the 3D world is drawn into the World View window and a backdrop
  fills the screen. The World View key has no default (other mods use Insert).
- The map on the whole screen is never remembered as a window layout; windows reporting no size are not remembered;
  the window area is never larger than the visible screen.

## 1.0.2 – 2026-10-27

- Right-clicking a unit in the formation manager on the other monitor opens its menu there (it opened on the main
  screen). A click on one monitor closes a unit menu open on the other.
- Formation manager tabs (a game bug, on both screens): with a category missing, e.g. no submarines, a tab stood for
  the category after it, and double-clicking a plane in the "Air" tab jumped the list to "All". Now it stays.

## 1.0.1 – 2026-10-25

Fixes after a bug report (clicks on the main screen went to the desktop, also with the mod unticked).

- Unticking the mod in the Mod Manager now really switches it off (it reads the Mod Manager's choice; Anchor Chain
  loads every mod folder regardless).
- Safeguard: the game's own main window is never cut to the see-through shape, even if it is mistaken for the
  pop-out monitor's window (a bug report: clicks on the main screen went to the desktop).

## 1.0.0 – 2026-10-24 (first Workshop release)

Tested on Sea Power 0.8.7.

- Pop-out button in the title bar of every window (formation manager, message log, camera controls, unit status,
  sensor and doctrine windows): moves it to another monitor and back. Bottom-bar toggles keep working.
- Free-floating windows: only the popped-out windows show on that monitor, the desktop shows around them. Snapping
  to each other and to the monitor's edges; above other programs while the game is in use.
- Windows can be made larger than the game allows on the other monitor; popped-out windows and their places are
  remembered for the next mission.
- Monitors the game uses for its own second-screen map or world view are skipped.
- Mouse pointer lines up on a monitor of another height than the game's (resizing works there).
- A window resized on the other monitor comes back at that size in the next mission, also the formation manager,
  which otherwise sizes itself.
- Wide resize corners on every window, on both screens.
