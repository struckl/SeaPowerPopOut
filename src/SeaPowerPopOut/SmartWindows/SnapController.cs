using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Noesis;
using UnityEngine;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Window snapping, on both screens. Shift held: nothing snaps.
/// - Main screen (a Smart Windows feature): while the player moves or resizes a window with the mouse its edges snap to
///   the screen edges and to the edges of the windows next to it. Watches the window's own position and size events;
///   what was dragged is worked out from what changed. Settings "3. Placement": Snap windows, Snap distance.
/// - Pop-out monitor (part of the pop-out screen, also with Smart Windows off): a dragged window snaps to the monitor's
///   edges and to the other windows there, side by side or stacked with one resize frame between them, edges in line.
///   Pop-Out's setting "2. Pop-out screen": Snap windows; the distance is fixed at the main screen's default.
/// </summary>
internal sealed class SnapController
{
    public static readonly Feature Feature = new Feature
    {
        LiveSwitch = () => SmartSettings.SnapEnabled.Value,
        WindowLoaded = window => new SnapController(window),
    };

    private const float Epsilon = 0.5f;
    private const float PopOutDistance = 6f;      // window units

    private static readonly ConditionalWeakTable<GameWindow, object> PoppedHooked = new ConditionalWeakTable<GameWindow, object>();

    private readonly GameWindow window;
    private WinRect last;
    private bool applying;

    private SnapController(GameWindow window)
    {
        this.window = window;
        last = Current();
        window.PositionChanged += (s, e) => Changed();
        window.SizeChanged += (s, e) => Changed();
    }

    private static bool Suspended => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

    /// <summary>
    /// A window on the pop-out monitor: it snaps while dragged by its title bar, to the edges of the area and to the
    /// other windows there.
    /// </summary>
    public static void OnPopOutMonitor(GameWindow window, IReadOnlyList<GameWindow> windows, Func<FrameworkElement> area)
    {
        if (window.HeaderThumb == null || PoppedHooked.TryGetValue(window, out _))
            return;
        PoppedHooked.Add(window, new object());
        // Runs after the game's own handler, which has just moved the window with the mouse.
        window.HeaderThumb.DragDelta += (sender, args) => SnapPopped(window, windows, area());
    }

    /// <summary>
    /// The game's drag handler has just set the unsnapped position (the header thumb measures the mouse from where it was
    /// grabbed), so a snap never accumulates.
    /// </summary>
    private static void SnapPopped(GameWindow window, IReadOnlyList<GameWindow> windows, FrameworkElement area)
    {
        if (!Settings.Snap.Value || area == null || window.Maximized || Suspended)
            return;
        var now = new WinRect(window.X, window.Y, window.ActualWidth, window.ActualHeight);
        if (now.W <= 0f || now.H <= 0f)
            return;
        var others = new List<(WinRect, float)>();
        foreach (GameWindow other in windows)
        {
            if (!ReferenceEquals(other, window) && other.Visibility == Visibility.Visible)
                others.Add((new WinRect(other.X, other.Y, other.ActualWidth, other.ActualHeight),
                    Math.Max(window.ResizeHandleWidth, other.ResizeHandleWidth)));
        }
        WinRect snapped = WindowGeometry.SnapApart(now, others, new WinRect(0f, 0f, area.ActualWidth, area.ActualHeight), PopOutDistance);
        if (snapped.X != now.X)
            window.X = snapped.X;
        if (snapped.Y != now.Y)
            window.Y = snapped.Y;
    }

    private void Changed()
    {
        if (applying)
            return;
        WinRect now = Current();
        try
        {
            if (SmartSettings.SnapEnabled.Value && Input.GetMouseButton(0) && !window.Maximized && !Suspended)
                Snap(now);
        }
        finally
        {
            last = Current();
        }
    }

    private void Snap(WinRect now)
    {
        bool resized = Math.Abs(now.W - last.W) > Epsilon || Math.Abs(now.H - last.H) > Epsilon;
        bool moved = Math.Abs(now.X - last.X) > Epsilon || Math.Abs(now.Y - last.Y) > Epsilon;
        if (!resized && !moved)
            return;

        IList<WinRect> others = Neighbours();
        WinRect screen = HostArea.Of(window);
        float distance = SmartSettings.SnapDistance.Value;

        WinRect snapped;
        if (!resized)
        {
            snapped = WindowGeometry.SnapMove(now, others, screen, distance);
        }
        else
        {
            bool left = Math.Abs(now.X - last.X) > Epsilon;
            bool top = Math.Abs(now.Y - last.Y) > Epsilon;
            bool right = !left && Math.Abs(now.W - last.W) > Epsilon;
            bool bottom = !top && Math.Abs(now.H - last.H) > Epsilon;
            snapped = WindowGeometry.SnapResize(now, left, top, right, bottom, others, screen, distance);
        }

        if (Same(snapped, now))
            return;
        applying = true;
        try
        {
            window.X = snapped.X;
            window.Y = snapped.Y;
            if (resized)
            {
                window.Width = snapped.W;
                window.Height = snapped.H;
            }
        }
        finally
        {
            applying = false;
        }
    }

    private WinRect Current() => new WinRect(window.X, window.Y,
        float.IsNaN(window.Width) ? window.ActualWidth : window.Width,
        float.IsNaN(window.Height) ? window.ActualHeight : window.Height);

    /// <summary>The visible windows next to this one in the same container.</summary>
    private IList<WinRect> Neighbours()
    {
        var result = new List<WinRect>();
        if (!(VisualTreeHelper.GetParent(window) is Panel parent))
            return result;
        for (int i = 0; i < parent.Children.Count; i++)
        {
            if (parent.Children[i] is GameWindow other && other != window && !other.Transient && other.IsVisible && !other.Maximized)
                result.Add(new WinRect(other.X, other.Y, other.ActualWidth, other.ActualHeight));
        }
        return result;
    }

    private static bool Same(WinRect a, WinRect b) =>
        Math.Abs(a.X - b.X) < Epsilon && Math.Abs(a.Y - b.Y) < Epsilon
        && Math.Abs(a.W - b.W) < Epsilon && Math.Abs(a.H - b.H) < Epsilon;
}
