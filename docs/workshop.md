# Releasing on the Steam Workshop

The game uploads Workshop items itself: **Main menu → Mods → Create Mod**, signed in with the Steam account that
owns the item. No SteamCMD needed.

## Before a release

1. Bump the version in `src/SeaPowerPopOut/Plugin.cs` (`Version`) and `src/SeaPowerPopOut/SeaPowerPopOut.csproj`
   (`<Version>`), add the entry to [CHANGELOG.md](../CHANGELOG.md).
2. Check `src/SeaPowerPopOut/mod/_info.ini`: Mod Manager name and description, and `[Compatibility]`
   (`ApproximateVersion` = the game version tested against; it accepts later patch versions of that minor
   version).
3. Run the checklist in [testing.md](testing.md) on the current game version.
4. Close Sea Power, then stage and install the release folder:

   ```powershell
   powershell -ExecutionPolicy Bypass -File tools\package.ps1 -Install
   ```

   This replaces `<Sea Power>\Sea Power_Data\StreamingAssets\SeaPowerPopOut\` with exactly what players get.

## Upload without the game's screen

With Steam running and signed in as the item's owner (game closed), after `package.ps1`:

```powershell
dotnet build tools\workshop-upload -c Release
tools\workshop-upload\bin\Release\net8.0\WorkshopUpload.exe 3826396311 dist\SeaPowerPopOut workshop\changelog-<version>.txt
```

It sends the content folder and the change note through the Steam client (Steamworks.NET from the game's own
folder); title, description, tags, visibility and preview stay as they are. `Result: k_EResultOK` = done; the
change shows on the item's Change Notes page. (1.0.1 went out this way, 2026-10-25; 1.0.2 on 2026-10-27.)

## Upload in the game


Start Sea Power → Mods → Create Mod:

| Field | Value |
| --- | --- |
| Existing item | pick *Sea Power: Rats Pop-Out Windows* – Workshop item [3826396311](https://steamcommunity.com/sharedfiles/filedetails/?id=3826396311), first uploaded 2026-10-24 as 1.0.0 |
| Folder | `StreamingAssets\SeaPowerPopOut` (the folder `package.ps1 -Install` filled) |
| Name | Sea Power: Rats Pop-Out Windows (the naming of "Sea Power: Rats Topographic Map") |
| Description | [workshop/description.bbcode](../workshop/description.bbcode) |
| Preview image | `StreamingAssets\SeaPowerPopOut\preview.jpg` (put there by `package.ps1`: the picker only browses StreamingAssets and only takes .jpg; Steam wants it under 1 MB) |
| Tags | Scripting |
| Dependencies | `3380210757` (Anchor Chain) – shows as a required item on the Workshop page |
| Visibility | first upload **Hidden** (or Friends only); Public once the subscribed copy is tested |
| Change log | the CHANGELOG entry |

Posts for Reddit, the Steam forum and Discord are in [workshop/promotion.md](../workshop/promotion.md), with the
Workshop link. Add the screenshots from [workshop/screenshots/](../workshop/screenshots) on the Workshop page itself:
open the item while signed in as its owner (Steam client or browser) → right column **Owner Controls** →
**Add/edit images & videos** → choose the files, then drag them into this order: 01 (both monitors), 04 and 05
(before / after), 02 (the arranged windows), 03 (the button).

## Remaking the screenshots

[tools/shot-director](../tools/shot-director) holds a dev-only Anchor Chain plugin (never shipped) that takes commands
from `<Sea Power>\BepInEx\shotdirector.cmd` – load a mission, centre the map, open a window like its bottom-bar
button, pop it out or back in, place it, hover a pop-out button – and logs to `shotdirector.log`; the full command
list is at the top of `Director.cs`. Next to it:

- `grab.ps1` grabs a monitor without its taskbar (`-Screen N`, 0 = leftmost) or the game monitor (`-Game`) at full
  resolution – the game's own ScreenCapture fails while the pop-out monitor is active.
- `backdrop.ps1 -Screen N` fills the pop-out monitor with a neutral stand-in desktop above other programs and keeps
  the game's window there above it, so no real desktop shows through the see-through monitor.
- `make_images.py RAW_DIR` turns the raw grabs into `workshop/screenshots/*.jpg` and `workshop/preview.jpg`.

Build the plugin with `dotnet build tools/shot-director -c Release` (deploys to `StreamingAssets\ShotDirector\`,
game closed), back up `dev.sewerlabs.seapowerpopout.cfg`, start `backdrop.ps1`, start the game. The current set was
taken in the mission *Trollfjorden* (NATO), map `center 70.25 21.3 9.1`, on a 4K main monitor with a 1440 × 2560
portrait monitor on the right. Pop-out monitor (window units, 1080 × 1884 above the taskbar): `place formation 20
20 330 800`, `place log 355 20 705 240` (short enough to leave out the game's "Open Modal Window" line),
`place camera 355 265 300 360`, `place status 20 825 1040 520`. Main screen for the "before" shot: `place formation
12 60 300 640`, `place log 330 30 640 236`, `place camera 1608 110 300 360`, `place status 930 560 980 470`; the
pop-out button of the formation manager sits at pixel 564,140 (`move 570 146`, `move 564 140` shows its tooltip).
Afterwards: `quit`, restore the config, remove `StreamingAssets\ShotDirector\`, close `backdrop.ps1`.

## Testing the Workshop copy

Subscribe, then **remove your local `StreamingAssets\SeaPowerPopOut` folder** (or rename it): with two copies only
the first one Anchor Chain finds starts, and the log says `Already loaded from another folder`. The subscribed copy
lives in `steamapps\workshop\content\1286220\<item id>\`. Start a mission and run the short checklist; then switch
the item to Public.

## Still to do for the first release

- Test matrix: one and two extra monitors, landscape and portrait, a monitor of another resolution or height than
  the game's, Windows display scaling (125 %, 150 %), the game's own second-monitor map and detached world view,
  windowed / borderless / exclusive fullscreen, the game's UI scale.
- Settings inside the game (most players won't edit the BepInEx config).
