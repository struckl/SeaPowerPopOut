using System;
using System.Collections.Generic;
using Noesis;
using SeapowerUI;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Unit Reference: the mouse wheel scrolls the unit tree only while the cursor is over it. The game scrolls the tree
/// wherever the cursor is in the window, so the unit information next to it could not be scrolled with the wheel. The
/// wheel is passed on to whatever scrollable area is really under the cursor.
/// </summary>
internal static class TreeWheelGuard
{
    private const float WheelStep = 0.4f;

    public static void Attach(GameWindow window, UnitTreeBrowserView view)
    {
        TreeView tree = FindTree(view);
        if (tree == null)
        {
            ModLog.Debug("Unit Reference: no tree view found for the wheel guard");
            return;
        }
        var win = new WeakReference<GameWindow>(window);
        tree.PreviewMouseWheel += (sender, e) =>
        {
            try
            {
                if (SmartSettings.TreeWheelOnlyOver.Value && win.TryGetTarget(out GameWindow w))
                    Guard(tree, w, e);
            }
            catch (Exception ex)
            {
                ModLog.Debug($"tree wheel: {ex.Message}");
            }
        };
    }

    private static void Guard(TreeView tree, GameWindow window, MouseWheelEventArgs e)
    {
        if (CursorOver(tree, window, e))
            return;

        e.Handled = true;
        HitTestResult hit = VisualTreeHelper.HitTest(window, e.GetPosition(window));
        for (DependencyObject node = hit.VisualHit; node != null && node != tree && node != window; node = VisualTreeHelper.GetParent(node))
        {
            if (node is ScrollViewer viewer && viewer.ScrollableHeight > 0f)
            {
                float target = viewer.VerticalOffset - e.Delta * WheelStep;
                viewer.ScrollToVerticalOffset(Math.Max(0f, Math.Min(viewer.ScrollableHeight, target)));
                return;
            }
        }
    }

    /// <summary>True when the cursor is inside the tree and inside every ancestor up to the window (a clipped part counts as outside).</summary>
    private static bool CursorOver(TreeView tree, GameWindow window, MouseWheelEventArgs e)
    {
        for (DependencyObject node = tree; node != null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is FrameworkElement element && element.ActualWidth > 0f && element.ActualHeight > 0f)
            {
                Point p = e.GetPosition(element);
                if (p.X < 0f || p.Y < 0f || p.X > element.ActualWidth || p.Y > element.ActualHeight)
                    return false;
            }
            if (node == window)
                break;
        }
        return true;
    }

    private static TreeView FindTree(DependencyObject root)
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        for (int budget = 3000; stack.Count > 0 && budget > 0; budget--)
        {
            DependencyObject node = stack.Pop();
            if (node is TreeView tree)
                return tree;
            for (int i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--)
                stack.Push(VisualTreeHelper.GetChild(node, i));
        }
        return null;
    }
}
