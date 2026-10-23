using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace SeaPowerPopOut;

/// <summary>The few Win32 calls the see-through pop-out monitor needs.</summary>
internal static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
        public override string ToString() => $"{Left},{Top} {Width}x{Height}";
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X, Y;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    private const int RGN_OR = 2;
    private const uint GA_ROOT = 2;
    private const int SM_SWAPBUTTON = 23;
    private const int VK_LBUTTON = 0x01, VK_RBUTTON = 0x02;
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1), HWND_NOTOPMOST = new IntPtr(-2);
    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010, SWP_NOOWNERZORDER = 0x0200;

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hwnd, ref POINT point);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] private static extern int CombineRgn(IntPtr dest, IntPtr a, IntPtr b, int mode);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr gdiObject);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentProcessId();

    private static readonly uint Pid = GetCurrentProcessId();

    /// <summary>The visible top-level windows of this process (the game's window and one per activated display).</summary>
    public static HashSet<IntPtr> OwnWindows()
    {
        var found = new HashSet<IntPtr>();
        EnumWindows((hwnd, _) =>
        {
            if (IsOwn(hwnd) && IsWindowVisible(hwnd))
                found.Add(hwnd);
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static bool IsOwn(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out uint pid);
        return pid == Pid;
    }

    /// <summary>The top-level window of this process under the mouse pointer, or zero.</summary>
    public static IntPtr OwnWindowUnderCursor()
    {
        if (!GetCursorPos(out POINT p))
            return IntPtr.Zero;
        IntPtr hwnd = GetAncestor(WindowFromPoint(p), GA_ROOT);
        return hwnd != IntPtr.Zero && IsOwn(hwnd) ? hwnd : IntPtr.Zero;
    }

    /// <summary>One of the game's windows has the keyboard (the player is in the game, not in another program).</summary>
    public static bool GameInForeground()
    {
        IntPtr hwnd = GetForegroundWindow();
        return hwnd != IntPtr.Zero && IsOwn(hwnd);
    }

    /// <summary>
    /// Limits where the window shows and takes the mouse to these rectangles (window coordinates); everywhere else
    /// the desktop and other programs show through and get the clicks. Empty = the whole window is see-through.
    /// </summary>
    public static bool SetShape(IntPtr hwnd, IReadOnlyList<RECT> rects)
    {
        IntPtr region = CreateRectRgn(0, 0, 0, 0);
        foreach (RECT r in rects)
        {
            IntPtr part = CreateRectRgn(r.Left, r.Top, r.Right, r.Bottom);
            CombineRgn(region, region, part, RGN_OR);
            DeleteObject(part);
        }
        // On success the window owns the region; it must not be deleted.
        if (SetWindowRgn(hwnd, region, true) != 0)
            return true;
        DeleteObject(region);
        return false;
    }

    /// <summary>The whole window shows again.</summary>
    public static void ClearShape(IntPtr hwnd) => SetWindowRgn(hwnd, IntPtr.Zero, true);

    public static void SetTopmost(IntPtr hwnd, bool topmost) =>
        SetWindowPos(hwnd, topmost ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0,
                     SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);

    /// <summary>The (logical) left mouse button is held right now, wherever the pointer is.</summary>
    public static bool LeftButtonHeld() =>
        (GetAsyncKeyState(GetSystemMetrics(SM_SWAPBUTTON) != 0 ? VK_RBUTTON : VK_LBUTTON) & 0x8000) != 0;

    public static string ClassName(IntPtr hwnd)
    {
        var name = new StringBuilder(128);
        return GetClassName(hwnd, name, name.Capacity) > 0 ? name.ToString() : "?";
    }
}
