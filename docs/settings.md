# R.A.T.S. Pop-Out Windows: all settings

The settings that matter most are in the [README](../README.md). This is the full list.

The mod has two config files in `<Sea Power>\BepInEx\config\`. Both are created on the first start. Many settings say "needs a restart".

- `dev.sewerlabs.seapowerpopout.cfg`: the pop-out monitor and the mod itself.
- `dev.sewerlabs.seapowerpopout.smartwindows.cfg`: the Smart Windows features. Most of them are also in the settings panel (gear button).

## Pop-out monitor

| Section | Setting | Default | What it does |
| --- | --- | --- | --- |
| 1. General | Enable mod | on | Master switch for the whole mod |
| 1. General | Verbose log | off | More lines in `LogOutput.log` |
| 2. Pop-out screen | Monitor | 0 | 0 = first monitor the game does not use itself; 2 = the next one after the game's, 3 the one after that. The log lists them. |
| 2. Pop-out screen | UI scale | 1.0 | Size of the windows there, relative to the game's UI scale |
| 2. Pop-out screen | Unlimited window size | on | Resize beyond the game's maximum |
| 2. Pop-out screen | See-through | on | Only the windows show; off = the whole monitor turns dark |
| 2. Pop-out screen | Always on top | on | Windows stay above other programs while you are in the game |
| 2. Pop-out screen | Snap windows | on | Snapping on the pop-out monitor |
| 3. Remembered layout | Popped-out windows, Window placement | empty | Written by the mod; clear "Popped-out windows" to bring every window back |
| 4. All windows | Wide resize corners | on | Diagonal resizing from the ends of every edge, on both screens |

## Smart Windows: master switch and feature switches

"Enable window memory" (`1. General`, default on, needs a restart) is the master switch: off, no Smart Windows feature starts.
Every feature below also has its own switch.

| Feature | Setting | Default |
| --- | --- | --- |
| Remembered window places (desktop mode needs it too) | 2. Remembered windows > Remember window places | on |
| Click raises a window, double-click maximizes | 3. Placement > Window clicks | on |
| Snapping on the main screen (distance: `Snap distance`, 6 px) | 3. Placement > Snap windows | on |
| Frame around windows | 4. Window border > Enabled | on |
| Frame around tooltips (needs the frame on) | 4. Window border > Tooltip frame | on |
| Gear button for the settings panel | 5. Settings window > Gear button | on |
| Window looks | 5. Settings window > Apply window looks | on |
| Look button (shown with the looks) | 5. Settings window > Look button | on |
| Unit Reference: remember choices | 6. Unit Reference > Remember choices | on |
| Unit Reference: splitter and layout | 6. Unit Reference > Improved layout | on |
| Unit Reference: earliest variant on click | 6. Unit Reference > Earliest variant on click | on |
| Unit Reference: scroll to selected | 6. Unit Reference > Scroll to selected | on |
| Unit Reference: wheel only over the tree | 6. Unit Reference > Wheel only over the tree | on |
| Unit Reference: only countries with units | 6. Unit Reference > Only countries with units | on |
| Unit Reference: armed only | 6. Unit Reference > Armed only | off |
| Unit Reference: loadout list for all units | 6. Unit Reference > Loadout list for all units | on |
| Unit Reference: sort tree by date | 6. Unit Reference > Sort tree by date | on |
| Unit Reference: group loadout variants | 6. Unit Reference > Group loadout variants | on |
| Unit Reference: search ids and descriptions | 6. Unit Reference > Search ids and descriptions | on |
| Unit Reference: link tooltips | 6. Unit Reference > Link tooltips | on |
| Unit Reference: keep the opened unit | 6. Unit Reference > Keep the opened unit | on |
| Unit Status: full unit name | 7. Unit Status > Full unit name | on |
| Unit Status: Reference tab | 7. Unit Status > Reference tab | on |
| Unit Status: Damage Control section height (0 = the game's) | 7. Unit Status > Sections min height | 278 |
| Unit Status: Track tab | 7. Unit Status > Track State | on |
| Unit panel menu | 8. Housekeeping > Unit panel menu | on |
| Messages do not block clicks | 8. Housekeeping > Messages do not block clicks | off |
| Resize cursors outside missions | 8. Housekeeping > Resize cursors in menus | on |
| Mod backgrounds only | 8. Housekeeping > Mod backgrounds only | off |
| Hide "_Custom Mission" | 8. Housekeeping > Hide _Custom Mission | on |
| Remember mission folder | 8. Housekeeping > Remember mission folder | on |
| Colour `.ini` files (colour: `Ini file color`) | 8. Housekeeping > Color ini files | on |
| Delete old game logs | 8. Housekeeping > Delete old game logs | off |
| Campaign: dismiss damaged ships | A. Campaign > Dismiss damaged ships | on |
| Campaign: fix hull swap price | A. Campaign > Fix hull swap price | on |
| Campaign: "Upgrade ship" button | A. Campaign > Upgrade ship | off |
| Campaign: strict loadouts | A. Campaign > Strict loadouts | on |
| Campaign: sort loadouts by date | A. Campaign > Sort loadouts by date | on |
| Campaign: loadout panel order | A. Campaign > Loadout panel order | on |
| Desktop mode | B. Desktop mode > Enabled | off |
| Draggable time panel | B. Mission > Draggable time panel | on |

Other Smart Windows settings:

- `1. General > Language`: a game language code (en, ru, cn, de, es, fr, ko, ja, vn); empty = the game's own.
- `2. Remembered windows > Window patterns`: which windows are remembered.
- `3. Placement`: `Cascade offset`, `Allow dragging outside`, `Minimum visible pixels`.
- `4. Window border`: `Thickness`, `Color`.
- `B. Desktop mode`: `Desktops` (2 to 5) and `World View key` (no key by default).
- Sections 9 and the "Written by the mod" entries of 6, 8 and B hold what the mod saves itself.
