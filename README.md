# Sea Power: R.A.T.S. Pop-Out Windows

Puts the game's windows on a second monitor, and adds window handling on the main screen: remembered places, snapping, a better Unit Reference.

| | |
| --- | --- |
| Version | `1.2.3` (2026-11-12) |
| Game | Sea Power 0.9.1 (build 261112) and 0.8.7 tested in the game; one DLL for both |
| Needs | a mod loader: [R.A.T.S. Keel](https://steamcommunity.com/sharedfiles/filedetails/?id=3835527459) or [Anchor Chain](https://steamcommunity.com/sharedfiles/filedetails/?id=3380210757), either one |
| Get it | [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3826396311) (package about 250 KB) |
| Settings | the gear button in a window, or `BepInEx\config\dev.sewerlabs.seapowerpopout.cfg` and `...smartwindows.cfg` |
| Feedback | [R.A.T.S. feedback board](https://hub.sewerlabs.dev/s/popout) |
| Source | R.A.T.S. Source License, see `LICENSE` |

![The map on the main screen, the game's windows on the second monitor](workshop/screenshots/01-two-monitors.jpg)

## Features

- A pop-out mark in the title bar sends a window to the second monitor; it floats there and the desktop shows around it.
- Works for the formation manager, message log, camera controls, unit status, sensor and doctrine windows.
- Windows snap to each other and to the monitor edges (Shift moves one freely) and resize beyond the game's maximum.
- Popped-out windows come back in the same place in the next mission.
- Wide resize corners on every window, on both screens.
- Remembered window positions, sizes and maximized state; a thin frame, double-click to maximize, a gear settings panel in nine languages.
- A better Unit Reference: sorted by date, loadout variants, search in descriptions, armed only, splitter, remembered choices.
- Unit Status: full unit name, a Reference tab and a Track tab.
- Campaign Task Force Builder fixes: loadouts oldest first, strict loadouts, dismiss damaged ships, hull swap price.
- Optional desktop mode: the minimap key switches between 2 to 5 window layouts.
- Window handling is based on rgreat's [Smart Windows](https://steamcommunity.com/sharedfiles/filedetails/?id=3813683442).

Full detail: [docs/features.md](docs/features.md).

## Install

1. Install a mod loader, either one.
   - R.A.T.S. Keel: subscribe, switch it on in the Mods menu, copy its one stub file once.
   - Anchor Chain: follow its [installation guide](https://seapower-modders.github.io/AnchorChain/install-anchor-chain.html).
2. Subscribe to [R.A.T.S. Pop-Out Windows](https://steamcommunity.com/sharedfiles/filedetails/?id=3826396311) on the Workshop.
3. Connect the second monitor before you start the game.
4. In a mission, click the pop-out mark left of the X in a window's title bar and choose "Pop out to monitor".

Manual install: put `SeaPowerPopOut.dll` and `_info.ini` into `<Sea Power>\Sea Power_Data\StreamingAssets\SeaPowerPopOut\`.

## Settings

| Setting | Default | What |
| --- | --- | --- |
| 1. General > Enable mod | on | master switch for the whole mod |
| 1. General > Enable window memory | on | master switch for every Smart Windows feature (needs a restart) |
| 2. Pop-out screen > Monitor | 0 | 0 = first monitor the game does not use; 2, 3 = the next ones |
| 2. Pop-out screen > See-through | on | only the windows show; off = the whole monitor turns dark |
| 2. Pop-out screen > Always on top | on | windows stay above other programs while you are in the game |
| 2. Pop-out screen > UI scale | 1.0 | size of the windows there |
| 3. Remembered layout > Popped-out windows | empty | clear it to bring every window back |
| B. Desktop mode > Enabled | off | the minimap key switches window layouts |
| 6. Unit Reference > Armed only | off | leave out units without weapons |

- Many settings need a restart. Every Smart Windows feature has its own switch; all of them: [docs/settings.md](docs/settings.md).

## Needs to know

- Needs a second monitor for the pop-out part; the Smart Windows features work without one.
- Exclusive fullscreen with a second monitor can freeze the world view: use borderless windowed (Options > Graphics).
- No game files are changed. Uninstall by unsubscribing or deleting the folder.
- Under Anchor Chain it was run on 0.8.7 only; Anchor Chain does not start on the globe builds yet.

## Building

- Needs the .NET SDK 8, Sea Power installed and the [SeaPowerKeel](https://github.com/struckl/SeaPowerKeel) repo next to this one.

```bash
dotnet build src/SeaPowerPopOut/SeaPowerPopOut.csproj -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Sea Power"
```

Details, how it works and the source layout: [docs/building.md](docs/building.md). Releases: [docs/workshop.md](docs/workshop.md); tests: [docs/testing.md](docs/testing.md).

## Credits and license

- [R.A.T.S. Source License](LICENSE): read it, fork it, send fixes; no re-uploads, no reuse in other projects.
- Window handling is based on rgreat's Smart Windows (link above); thanks to rgreat for the original.
- Not affiliated with Triassic Games or MicroProse. Nothing of the game is included in this repository or in the mod.
