# Rats Pop-Out Windows – release and promotion

Workshop: WORKSHOP_LINK (after the upload: https://steamcommunity.com/sharedfiles/filedetails/?id=<item id>)
Source: https://github.com/struckl/SeaPowerPopOut
Images: `workshop/screenshots/` – 01-two-monitors, 02-floating-windows, 03-pop-out-button, 04-before, 05-after

---

## 0. Steam Workshop upload (Main menu → Mods → Create Mod)

| Field | Value |
| --- | --- |
| Folder | `StreamingAssets\SeaPowerPopOut` – double-click into it before confirming |
| Name | Sea Power: Rats Pop-Out Windows |
| Description | [description.bbcode](description.bbcode) |
| Preview image | `StreamingAssets\SeaPowerPopOut\preview.jpg` |
| Tags | Scripting (plus a UI / quality-of-life tag if the picker offers one) |
| Dependencies | `3380210757` (Anchor Chain) |
| Visibility | Hidden first, Public once the subscribed copy works |
| Change log | First release: pop-out button in every window's title bar, free-floating windows on a second monitor, snapping, remembered layout, wide resize corners. |

After the upload: add the screenshots on the Workshop page (Steam web → Edit → Add/edit images) in the order 01, 04,
05, 02, 03, and put the Workshop link into the README, the description and the posts below.

---

## 1. Reddit – r/SeaPower_NCMA (image gallery post)

Images, in this order: 01-two-monitors.jpg, 04-before.jpg, 05-after.jpg, 02-floating-windows.jpg,
03-pop-out-button.jpg. No flair needed (the sub has none).

**Title**

I made a mod that moves Sea Power's windows to your second monitor, so the map stays free

**Body**

The map and the 3D view are what I want to look at, but the formation manager, message log and unit status kept covering them. The game can already put the map or the world view on another monitor – just not its windows. So I added a pop-out button:

- **One click** on the new button in a window's title bar moves it to your second monitor; the same button brings it back
- **Free-floating**: only the windows show over there – your desktop stays visible around them and takes the clicks
- **Arrange them as you like**: windows snap to each other and to the monitor's edges, and can be made bigger than the game normally allows
- **Remembered**: popped-out windows open on the second monitor in the next mission too, where you left them
- Formation manager, message log, unit status, camera controls, sensor and doctrine windows; the bottom-bar buttons keep working
- **Bonus on every screen**: grab the end of any window edge to resize diagonally

It's the game's own window over there – same look and controls – and clicks on the second monitor never reach the world. Monitors the game already uses for its own map or world view are skipped. Needs Anchor Chain and a second monitor connected before you start the game.

Workshop: WORKSHOP_LINK
Source (MIT): https://github.com/struckl/SeaPowerPopOut

Screenshots: 4K main screen with a 1440×2560 portrait monitor next to it, mission Trollfjorden. Feedback welcome – especially from other monitor setups (landscape, different resolutions, display scaling).

---

## 2. Steam – Sea Power Modding sub-forum, new thread

https://steamcommunity.com/app/1286220/discussions/3/ → Start a New Discussion

**Title**

[Mod] Rats Pop-Out Windows – the game's windows on your second monitor

**Body** (Steam forum BBCode)

The game can put the map or the 3D view on a second monitor, but not its windows. This mod adds a [b]pop-out button[/b] to the title bar of every window:

[list]
[*][b]One click[/b] moves the formation manager, message log, unit status, camera controls, sensor or doctrine window to your second monitor – the same button brings it back
[*][b]Free-floating[/b]: only the windows show there; your desktop stays visible around them and takes the clicks
[*][b]Arrange them as you like[/b]: windows snap to each other and to the monitor's edges, and can be made larger than the game normally allows
[*][b]Remembered[/b]: popped-out windows open on the second monitor in the next mission too, where you left them
[*][b]Wide resize corners[/b] on every window, on both screens
[/list]

It's the game's own window over there – same look and controls – and clicks on the second monitor never reach the world or the map. Monitors the game already uses for its own map or world view are skipped.

Requires [url=https://steamcommunity.com/sharedfiles/filedetails/?id=3380210757]Anchor Chain[/url] and a second monitor connected before you start the game.

[url=WORKSHOP_LINK]Workshop page[/url] · [url=https://github.com/struckl/SeaPowerPopOut]Source code (MIT)[/url]

Bug reports and suggestions welcome here or on the Workshop page – please attach BepInEx\LogOutput.log and mention your monitor setup.

---

## 3. Discord – BuOrd: Naval Modding Community, `#sp-mods` (Naval Technicians)

https://discord.gg/vc8uAVH6Sh – attach 01-two-monitors.jpg and 02-floating-windows.jpg. The `< >` around the GitHub link
keeps Discord from adding a second preview box.

```
## Rats Pop-Out Windows – new Sea Power mod
Put the game's windows on your second monitor, so the main screen stays free for the map:
- **Pop-out button** in every window's title bar – one click moves it to the other monitor, one click brings it back
- **Free-floating** – only the windows show there, your desktop stays visible around them
- **Arrange them** – windows snap to each other and to the monitor's edges, and can be larger than the game allows
- **Remembered** – popped-out windows come back where you left them in the next mission
- **Wide resize corners** on every window, on both screens

Formation manager, message log, unit status, camera controls, sensor and doctrine windows. Requires **Anchor Chain** and a second monitor.

**Workshop:** WORKSHOP_LINK
**Source (MIT):** <https://github.com/struckl/SeaPowerPopOut>
Feedback welcome!
```

## 4. Discord – MicroProse (Official), Sea Power mods or showcase channel

https://discord.gg/CTDqUUJAVC – a mods / showcase channel if there is one, not the general chat. Attach
01-two-monitors.jpg.

```
Made a small Sea Power mod for multi-monitor setups: **Rats Pop-Out Windows** puts the formation manager, message log, unit status and the other windows on your second monitor with one click, floating freely next to your desktop – the main screen stays free for the map. Windows snap together and are remembered for the next mission. Needs Anchor Chain.
Workshop: WORKSHOP_LINK
```
