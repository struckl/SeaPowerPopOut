# R.A.T.S. Pop-Out Windows: building and how it works

Short form in the [README](../README.md).

## Building

- Needs the .NET SDK 8 and Sea Power installed (the build references the game's assemblies).
- Needs the R.A.T.S. Core repo next to this one: the shared skeleton (entry point, host, log, settings, hook registry) is `SeaPowerCore`.
  It is found through the workspace folder that holds `mods.json`; for a clone elsewhere use `-p:RatsWorkspace=<folder>`.
- Anchor Chain's library comes from Keel's copy.

```bash
dotnet build src/SeaPowerPopOut/SeaPowerPopOut.csproj -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Sea Power"
```

- The build copies the mod into `<Sea Power>\Sea Power_Data\StreamingAssets\SeaPowerPopOut\`; `-p:Deploy=false` skips that.
- `GameDir` can also be an environment variable.
- Packaging and the Workshop upload: [docs/workshop.md](docs/workshop.md). In-game checklists: [docs/testing.md](docs/testing.md).

## How it works

- The other monitor is a second NoesisView on another Unity display, built like the game's own `SecondDisplayMapHandler` builds its map.
  - Its root holds a second `WindowHost` and a flat copy of the main view's resources, so templates, dragging and resizing are the game's own.
  - Popping out moves a window's view model from the main host's `Windows` to the second host's.
- Patches on the game's Noesis classes go in only once NoesisGUI runs; patching earlier runs their static constructors before Noesis is set up and crashes the game.
- See-through: Unity shows an activated display in a borderless window over the whole monitor.
  - The mod cuts it down with `SetWindowRgn` to the popped-out windows, their resize frame and any open tooltip, menu or drop-down list, every frame.
  - It is topmost only while one of the game's windows is in the foreground.
  - A drag released over another program never reaches Unity, so a press whose button is up (`GetAsyncKeyState`) gets its release sent to the view.
- Unit menu: the pop-out screen has its own copy of the game's unit menu canvas; a right-click there points the game's field at it until the next frame.
- Templates: a window remembered as popped out opens the screen before other mods add their window templates, so new resources are copied over each time a window opens there or pops out.
- Formation manager tabs: the TabControl holds six tabs with empty ones collapsed, while `SelectedIndex` of the view model counts only visible ones. A prefix on its getter and setter counts every tab.
- Pointer fix: `NoesisView` flips the mouse of another display with its camera's `pixelHeight`, which differs from the display's height in some setups, so the pointer sat off by the difference (400 px on a 2560 px portrait monitor).
  A postfix on `NoesisView.MousePosition` corrects it for every view on another monitor. The log has one `Pointer on monitor N` line.

## Layout

```
src/SeaPowerPopOut/
  Plugin.cs           entry point (the shared ModEntry starts it)
  ModInfo.cs          what the shared skeleton reads: GUID, name, version, log source
  Patches.cs          every hook, declared in the shared HookRegistry
  PopOutScreen.cs     the pop-out monitor, title-bar buttons, moving windows between screens; the mod's host
  ScreenCutOut.cs     what of the see-through monitor stays visible
  UnitMenuCopy.cs     the game's unit menu on the pop-out monitor
  ScreenResources.cs  the pop-out screen's copy of the main view's resources
  PointerFix.cs       the pointer of views on other monitors
  GameMouse.cs        the game's mouse state, shared with Smart Windows
  MonitorWindow.cs    the Unity window of the pop-out display: found, cut, kept on top
  CornerResize.cs     wide resize corners
  FormationTabs.cs    formation manager tab index over every tab
  Native.cs           the Win32 calls
  Layout.cs           remembered popped-out windows and their placement
  Settings.cs         config
  SmartWindows/       the Smart Windows features: each declares its switch, hooks and handlers (Feature.cs);
                      SmartFeatures.cs runs them in order; SnapController.cs snaps on both screens
  mod/                _info.ini and lang/*.ini
tools/package.ps1     release build -> dist/SeaPowerPopOut/
tools/shot-director/  dev-only plugin and scripts that took the Workshop screenshots
workshop/             Workshop description, preview.jpg, screenshots/
```
