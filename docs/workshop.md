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

## Upload

Start Sea Power → Mods → Create Mod:

| Field | Value |
| --- | --- |
| Existing item | first upload: none (creates the item); later: pick *Sea Power Pop-Out Windows* |
| Folder | `StreamingAssets\SeaPowerPopOut` (the folder `package.ps1 -Install` filled) |
| Name | Sea Power Pop-Out Windows |
| Description | [workshop/description.bbcode](../workshop/description.bbcode) |
| Preview image | `StreamingAssets\SeaPowerPopOut\preview.jpg` (put there by `package.ps1`: the picker only browses StreamingAssets and only takes .jpg; Steam wants it under 1 MB) |
| Tags | Scripting |
| Dependencies | `3380210757` (Anchor Chain) – shows as a required item on the Workshop page |
| Visibility | first upload **Hidden** (or Friends only); Public once the subscribed copy is tested |
| Change log | the CHANGELOG entry |

After the first upload, add the Workshop link to the README and the description, and more screenshots on the
Workshop page itself (Steam web → Edit → Add/edit images).

## Testing the Workshop copy

Subscribe, then **remove your local `StreamingAssets\SeaPowerPopOut` folder** (or rename it): with two copies only
the first one Anchor Chain finds starts, and the log says `Already loaded from another folder`. The subscribed copy
lives in `steamapps\workshop\content\1286220\<item id>\`. Start a mission and run the short checklist; then switch
the item to Public.

## Still to do for the first release

- Preview and screenshots: `workshop/preview.jpg` (Workshop preview; the game's Mod Manager shows no pictures) and
  a few screenshots for the Workshop page, taken in game – a second monitor with two or three popped-out windows
  arranged side by side, the desktop around them; the pop-out button in a title bar close up.
- Test matrix: one and two extra monitors, landscape and portrait, a monitor of another resolution or height than
  the game's, Windows display scaling (125 %, 150 %), the game's own second-monitor map and detached world view,
  windowed / borderless / exclusive fullscreen, the game's UI scale.
- Settings inside the game (most players won't edit the BepInEx config).
