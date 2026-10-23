using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeaPowerPopOut;

/// <summary>
/// The window Unity shows an activated display in: borderless, covering the whole monitor. A window region cut to
/// the game windows on it (and their open tooltips, menus and drop-downs) makes the rest of it see-through – the
/// desktop and other programs show there and take the clicks, so the popped-out windows float on the monitor like
/// windows of their own instead of sitting on a black screen.
/// </summary>
internal sealed class MonitorWindow
{
    // A display can't be deactivated again, so its window lives as long as the game: one object per display, kept
    // across missions together with what was done to the window.
    private static readonly Dictionary<int, MonitorWindow> All = new Dictionary<int, MonitorWindow>();

    private const float SearchSeconds = 5f;

    private readonly int display;
    private IntPtr hwnd;
    private HashSet<IntPtr> before;               // the game's windows right before the display was activated
    private float searchUntil;

    private readonly List<Native.RECT> parts = new List<Native.RECT>();
    private Native.RECT[] applied;                // null = no region set (the whole window shows)
    private bool shapeFailed;
    private bool topmost;
    private float nextForegroundCheck;

    private MonitorWindow(int display) => this.display = display;

    public static MonitorWindow For(int display)
    {
        if (!All.TryGetValue(display, out MonitorWindow window))
            All[display] = window = new MonitorWindow(display);
        return window;
    }

    /// <summary>
    /// A monitor whose window was left see-through after a mission, but which the game now draws its own world view
    /// or map on (its options changed meanwhile): give the game the whole window back.
    /// </summary>
    public static void YieldTo(Func<int, bool> gameDrawsOn)
    {
        foreach (MonitorWindow window in All.Values)
        {
            if (window.applied != null && gameDrawsOn(window.display))
                window.Unshape();
        }
    }

    public bool Found => hwnd != IntPtr.Zero;

    /// <summary>Right before the display is activated: the window that appears with it is its window.</summary>
    public void BeforeActivate()
    {
        before = Native.OwnWindows();
        searchUntil = Time.unscaledTime + SearchSeconds;
    }

    /// <summary>
    /// Looks for the display's window until found: the new game window of the monitor's size that appeared with the
    /// display, or else – a display the game activated earlier – the game window under the mouse on that monitor.
    /// </summary>
    public void Search(Display screen)
    {
        if (Found)
            return;
        IntPtr candidate = IntPtr.Zero;
        if (before != null)
        {
            int matches = 0;
            foreach (IntPtr h in Native.OwnWindows())
            {
                if (before.Contains(h) || !Native.GetWindowRect(h, out Native.RECT r))
                    continue;
                if (Near(r, screen.systemWidth, screen.systemHeight) || Near(r, screen.renderingWidth, screen.renderingHeight))
                {
                    candidate = h;
                    matches++;
                }
            }
            if (matches != 1)
                candidate = IntPtr.Zero;
            if (candidate == IntPtr.Zero && Time.unscaledTime > searchUntil)
            {
                before = null;
                ModLog.Info($"Window of monitor {display + 1} not found yet - it turns see-through once the mouse visits it.");
            }
        }
        if (candidate == IntPtr.Zero)
        {
            Vector3 p = Display.RelativeMouseAt(Input.mousePosition);
            if (p != Vector3.zero && (int)p.z == display)
                candidate = Native.OwnWindowUnderCursor();
        }
        if (candidate == IntPtr.Zero)
            return;

        hwnd = candidate;
        before = null;
        applied = null;
        Native.GetWindowRect(hwnd, out Native.RECT rect);
        ModLog.Info($"Window of monitor {display + 1}: {Native.ClassName(hwnd)} at {rect}");
    }

    /// <summary>
    /// Keeps only these parts of the window (view pixels of a <paramref name="viewWidth"/> ×
    /// <paramref name="viewHeight"/> view); an empty list hides the window entirely. Only touches the window when
    /// the parts changed.
    /// </summary>
    public void Shape(List<Rect> keep, int viewWidth, int viewHeight)
    {
        if (!Alive() || shapeFailed || viewWidth < 1 || viewHeight < 1)
            return;
        Native.GetWindowRect(hwnd, out Native.RECT window);
        Native.GetClientRect(hwnd, out Native.RECT client);
        var origin = new Native.POINT();
        Native.ClientToScreen(hwnd, ref origin);
        float sx = client.Width / (float)viewWidth, sy = client.Height / (float)viewHeight;
        int ox = origin.X - window.Left, oy = origin.Y - window.Top;

        parts.Clear();
        foreach (Rect r in keep)
        {
            // A pixel more on every side, so rounding never shaves off a window's edge.
            var part = new Native.RECT
            {
                Left = ox + Mathf.Max(0, Mathf.FloorToInt(r.xMin * sx) - 1),
                Top = oy + Mathf.Max(0, Mathf.FloorToInt(r.yMin * sy) - 1),
                Right = ox + Mathf.Min(client.Width, Mathf.CeilToInt(r.xMax * sx) + 1),
                Bottom = oy + Mathf.Min(client.Height, Mathf.CeilToInt(r.yMax * sy) + 1),
            };
            if (part.Right > part.Left && part.Bottom > part.Top)
                parts.Add(part);
        }
        if (applied != null && Same(applied, parts))
            return;
        if (!Native.SetShape(hwnd, parts))
        {
            shapeFailed = true;
            ModLog.Warn($"Monitor {display + 1} can't be made see-through (SetWindowRgn failed) - it stays dark.");
            return;
        }
        applied = parts.ToArray();
    }

    /// <summary>The whole window shows again (see-through turned off, or the game draws on that monitor).</summary>
    public void Unshape()
    {
        if (applied == null || !Alive())
            return;
        Native.ClearShape(hwnd);
        applied = null;
    }

    /// <summary>
    /// Above other programs' windows while the game is the program in use, so a click into the game doesn't leave
    /// the popped-out windows behind a browser on that monitor; a normal window again while you're elsewhere.
    /// </summary>
    public void KeepOnTop(bool wanted)
    {
        if (!Alive() || Time.unscaledTime < nextForegroundCheck)
            return;
        nextForegroundCheck = Time.unscaledTime + 0.5f;
        bool top = wanted && Native.GameInForeground();
        if (top == topmost)
            return;
        topmost = top;
        Native.SetTopmost(hwnd, top);
    }

    /// <summary>Mission over: a see-through monitor shows nothing of the game until the next pop-out.</summary>
    public void Release()
    {
        if (!Alive())
            return;
        if (applied != null && applied.Length > 0 && Native.SetShape(hwnd, Array.Empty<Native.RECT>()))
            applied = Array.Empty<Native.RECT>();
        if (topmost)
            Native.SetTopmost(hwnd, false);
        topmost = false;
    }

    private bool Alive()
    {
        if (hwnd == IntPtr.Zero)
            return false;
        if (Native.IsWindow(hwnd))
            return true;
        hwnd = IntPtr.Zero;
        applied = null;
        return false;
    }

    private static bool Near(Native.RECT r, int width, int height) =>
        Math.Abs(r.Width - width) <= 2 && Math.Abs(r.Height - height) <= 2;

    private static bool Same(Native.RECT[] a, List<Native.RECT> b)
    {
        if (a.Length != b.Count)
            return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].Left != b[i].Left || a[i].Top != b[i].Top || a[i].Right != b[i].Right || a[i].Bottom != b[i].Bottom)
                return false;
        }
        return true;
    }
}
