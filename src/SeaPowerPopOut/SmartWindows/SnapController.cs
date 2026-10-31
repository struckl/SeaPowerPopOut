using System;
using System.Collections.Generic;
using Noesis;
using UnityEngine;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Snaps one window to the screen edges and to the other windows while the player moves or resizes it with the
/// mouse. Watches the window's own position and size events; what was dragged is worked out from what changed.
/// </summary>
internal sealed class SnapController
{
    private const float Epsilon = 0.5f;

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

    public static void Attach(GameWindow window)
    {
        if (SmartSettings.SnapEnabled.Value)
            new SnapController(window);
    }

    private void Changed()
    {
        if (applying)
            return;
        WinRect now = Current();
        try
        {
            if (SmartSettings.SnapEnabled.Value && Input.GetMouseButton(0) && !window.Maximized)
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
