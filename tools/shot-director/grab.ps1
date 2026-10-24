<#
.SYNOPSIS
    Screenshot for the Workshop set: one monitor without its taskbar, or the whole game monitor.

.DESCRIPTION
    Per-monitor DPI aware, so it grabs real pixels on a scaled monitor too. -Screen picks a monitor by its place
    from left to right (0 = leftmost) and grabs its working area (the taskbar left out); -Game grabs the client area
    of the primary monitor, which the game fills. Saves a PNG.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\shot-director\grab.ps1 -Screen 2 -Out shots\right.png
#>
param(
    [int]$Screen = -1,
    [switch]$Game,
    [Parameter(Mandatory = $true)][string]$Out
)

$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class GrabNative {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hwnd, ref POINT point);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
}
'@
[GrabNative]::SetProcessDpiAwarenessContext([IntPtr]-4) | Out-Null    # per monitor v2, before any window code
Add-Type -AssemblyName System.Windows.Forms, System.Drawing

if ($Game) {
    # The game window fills the primary monitor. (Its process's "main window" can be the pop-out display's window.)
    $rect = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
}
else {
    $screens = [System.Windows.Forms.Screen]::AllScreens | Sort-Object { $_.Bounds.X }
    $rect = $screens[$Screen].WorkingArea
}

$bitmap = New-Object System.Drawing.Bitmap($rect.Width, $rect.Height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($rect.X, $rect.Y, 0, 0, $rect.Size)
$folder = Split-Path -Parent $Out
if ($folder) { New-Item -ItemType Directory -Force $folder | Out-Null }
$bitmap.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()
Write-Host "$Out ($($rect.Width)x$($rect.Height) at $($rect.X),$($rect.Y))"
