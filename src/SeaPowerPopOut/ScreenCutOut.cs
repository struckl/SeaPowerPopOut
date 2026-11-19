using System;
using System.Collections.Generic;
using Noesis;
using UnityEngine;
using ContextMenu = Noesis.ContextMenu;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut;

/// <summary>
/// What of the pop-out monitor stays visible when it is see-through: every window on it with its frame, and whatever
/// tooltip, context menu, drop-down or popup of the mod they have open.
/// </summary>
internal sealed class ScreenCutOut
{
    // A tooltip or menu that just closed may still be fading out.
    private const float ClosedGrace = 0.5f;

    private readonly List<(UIElement element, float since)> overlays = new List<(UIElement, float)>();
    private readonly List<UnityEngine.Rect> keep = new List<UnityEngine.Rect>();
    private readonly List<UnityEngine.Rect> lastKeep = new List<UnityEngine.Rect>();

    /// <summary>The windows on the pop-out screen (also what a dragged window snaps to).</summary>
    public List<GameWindow> Windows { get; } = new List<GameWindow>();

    public void Track(GameWindow window)
    {
        if (!Windows.Contains(window))
            Windows.Add(window);
    }

    /// <summary>A popup of its own (tooltip, menu, drop-down) that may reach past its window: part of the cut-out while open.</summary>
    public void TrackOverlay(UIElement overlay)
    {
        for (int i = 0; i < overlays.Count; i++)
        {
            if (ReferenceEquals(overlays[i].element, overlay))
            {
                overlays[i] = (overlay, Time.unscaledTime);
                return;
            }
        }
        overlays.Add((overlay, Time.unscaledTime));
    }

    /// <summary>
    /// The visible parts now. While a window is dragged or resized (<paramref name="pressed"/>) the cut-out and the
    /// picture may be a frame apart; last frame's parts as well cover that.
    /// </summary>
    public List<UnityEngine.Rect> Collect(bool pressed)
    {
        keep.Clear();
        CollectWindows();
        CollectOverlays();
        int count = keep.Count;
        if (pressed)
            keep.AddRange(lastKeep);
        lastKeep.Clear();
        for (int i = 0; i < count; i++)
            lastKeep.Add(keep[i]);
        return keep;
    }

    public void Clear()
    {
        Windows.Clear();
        overlays.Clear();
        lastKeep.Clear();
    }

    private void CollectWindows()
    {
        for (int i = Windows.Count - 1; i >= 0; i--)
        {
            GameWindow window = Windows[i];
            if (VisualTreeHelper.GetParent(window) == null)
            {
                Windows.RemoveAt(i);                  // closed or sent back (loading again adds it again)
                continue;
            }
            // The template root: the window plus the resize border around it.
            if (window.Visibility == Visibility.Visible && VisualTreeHelper.GetChildrenCount(window) > 0)
                AddBounds(VisualTreeHelper.GetChild(window, 0) as FrameworkElement);
        }
    }

    private void CollectOverlays()
    {
        for (int i = overlays.Count - 1; i >= 0; i--)
        {
            (UIElement element, float since) = overlays[i];
            if (IsOpen(element))
                AddBounds(element is Popup popup ? popup.Child as FrameworkElement : element as FrameworkElement);
            else if (Time.unscaledTime - since > ClosedGrace)
                overlays.RemoveAt(i);
        }
    }

    private void AddBounds(FrameworkElement element)
    {
        if (element == null || element.ActualWidth <= 0f || element.ActualHeight <= 0f)
            return;
        Point a = element.PointToScreen(new Point(0f, 0f));
        Point b = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
        keep.Add(UnityEngine.Rect.MinMaxRect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));
    }

    private static bool IsOpen(UIElement overlay) => overlay switch
    {
        Popup popup => popup.IsOpen,
        ToolTip tip => tip.IsOpen,
        ContextMenu menu => menu.IsOpen,
        _ => false,
    };
}
