<#
.SYNOPSIS
    A neutral stand-in desktop behind the pop-out windows, so the screenshots show no real desktop.

.DESCRIPTION
    Fills one monitor's working area (by its place from left to right, 0 = leftmost) with a calm blue-grey gradient,
    in a window that never takes the focus and stays out of the taskbar and Alt+Tab. It sits above other programs on
    that monitor, and once a second it lifts the game's window there (the see-through pop-out display) above itself,
    so the popped-out windows float over it like over a desktop. Runs until the PowerShell process ends.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\shot-director\backdrop.ps1 -Screen 2
#>
param([int]$Screen = 2)

$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Windows.Forms, System.Drawing @'
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class Backdrop : Form {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr lParam);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }

    static readonly IntPtr Topmost = new IntPtr(-1);
    const uint NoMoveSizeActivate = 0x0001 | 0x0002 | 0x0010;
    readonly Rectangle area;
    readonly Timer lift = new Timer { Interval = 1000 };

    public Backdrop(Rectangle area) {
        this.area = area;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = area;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        lift.Tick += (s, e) => LiftGame();
        lift.Start();
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams {
        get {
            CreateParams p = base.CreateParams;
            p.ExStyle |= 0x08000000 | 0x00000080 | 0x00000008;   // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST
            return p;
        }
    }

    // The game's window on this monitor (its display window) goes above the backdrop.
    void LiftGame() {
        Process[] games = Process.GetProcessesByName("Sea Power");
        if (games.Length == 0) return;
        uint game = (uint)games[0].Id;
        EnumWindows((hwnd, _) => {
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            RECT r;
            if (pid == game && IsWindowVisible(hwnd) && GetWindowRect(hwnd, out r) &&
                r.Left < area.Right && r.Right > area.Left && r.Top < area.Bottom && r.Bottom > area.Top)
                SetWindowPos(hwnd, Topmost, 0, 0, 0, 0, NoMoveSizeActivate);
            return true;
        }, IntPtr.Zero);
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
