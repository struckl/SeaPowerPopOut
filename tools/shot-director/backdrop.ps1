<#
.SYNOPSIS
    A neutral stand-in desktop behind the pop-out windows, so the screenshots show no real desktop.

.DESCRIPTION
    Fills one monitor's working area (by its place from left to right, 0 = leftmost) with a calm blue-grey gradient,
    in a window that never takes the focus and stays out of the taskbar and Alt+Tab. The see-through pop-out windows
    float over it like over a desktop. Runs until the PowerShell process ends (close it, or Ctrl+C).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\shot-director\backdrop.ps1 -Screen 2
#>
param([int]$Screen = 2)

$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Windows.Forms, System.Drawing @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class Backdrop : Form {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    public Backdrop(Rectangle area) {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = area;
        ShowInTaskbar = false;
        DoubleBuffered = true;
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams {
        get {
            CreateParams p = base.CreateParams;
            p.ExStyle |= 0x08000000 | 0x00000080;   // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
            return p;
        }
    }

    protected override void OnPaint(PaintEventArgs e) {
        Rectangle r = ClientRectangle;
        using (var fill = new LinearGradientBrush(r, Color.FromArgb(58, 78, 98), Color.FromArgb(22, 30, 40), LinearGradientMode.Vertical))
            e.Graphics.FillRectangle(fill, r);
        // A soft light from the upper left, like a wallpaper's.
        var glow = new Rectangle(-r.Width / 2, -r.Height / 4, r.Width * 3 / 2, r.Height * 3 / 4);
        using (var path = new GraphicsPath()) {
            path.AddEllipse(glow);
            using (var light = new PathGradientBrush(path)) {
                light.CenterColor = Color.FromArgb(70, 140, 170, 200);
                light.SurroundColors = new[] { Color.FromArgb(0, 140, 170, 200) };
                e.Graphics.FillPath(light, path);
            }
        }
    }
}
'@
[Backdrop]::SetProcessDpiAwarenessContext([IntPtr]-4) | Out-Null
$screens = [System.Windows.Forms.Screen]::AllScreens | Sort-Object { $_.Bounds.X }
$area = $screens[$Screen].WorkingArea
Write-Host "Backdrop on $($screens[$Screen].DeviceName) $area"
[System.Windows.Forms.Application]::Run((New-Object Backdrop($area)))
