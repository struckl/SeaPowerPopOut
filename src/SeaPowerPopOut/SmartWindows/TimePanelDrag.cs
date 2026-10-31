using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Noesis;
using SeapowerUI;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Mission screen: the time panel (date / time, speed, play / pause) can be dragged with the mouse and keeps its place
/// between missions and game runs; a double click puts it back in the corner. The panel is the first child of the canvas
/// whose DataContext is the <see cref="TimeCompressionViewModel"/>; it is placed with Canvas.Right / Canvas.Top.
/// </summary>
internal static class TimePanelDrag
{
    private const float CheckInterval = 0.5f;
    private const float RetryInterval = 5f;
    private const int SearchBudget = 6000;

    private static readonly ConditionalWeakTable<Grid, object> Attached = new ConditionalWeakTable<Grid, object>();
    private static WeakReference<Grid> panel = new WeakReference<Grid>(null);
    private static float nextCheck;
    private static bool dragging;

    /// <summary>Called every frame (cheap): finds the panel in a new mission view and keeps it where it was saved.</summary>
    public static void Update()
    {
        if (!SmartSettings.DragTimePanel.Value || UnityEngine.Time.unscaledTime < nextCheck)
            return;
        nextCheck = UnityEngine.Time.unscaledTime + CheckInterval;
        try
        {
            if (!panel.TryGetTarget(out Grid grid) || !grid.IsLoaded)
            {
                MainGameView view = MainGameView.Instance;
                if (view == null || !view.IsLoaded)
                    return;
                grid = Find(view);
                if (grid == null)
                    return;
                panel = new WeakReference<Grid>(grid);
            }
            if (!Attached.TryGetValue(grid, out _))
            {
                Attached.Add(grid, grid);
                Attach(grid);
                ModLog.Debug("smart windows: time panel is draggable");
            }
            if (!dragging)
                Place(grid);
        }
        catch (Exception ex)
        {
            nextCheck = UnityEngine.Time.unscaledTime + RetryInterval;
            ModLog.Debug($"time panel: {ex.Message}");
        }
    }

    private static Grid Find(FrameworkElement root)
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        int budget = SearchBudget;
        while (stack.Count > 0 && --budget > 0)
        {
            DependencyObject node = stack.Pop();
            if (node is Canvas canvas && canvas.DataContext is TimeCompressionViewModel && VisualTreeHelper.GetChildrenCount(canvas) > 0
                && VisualTreeHelper.GetChild(canvas, 0) is Grid grid)
                return grid;
            for (int i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--)
            {
                DependencyObject child = VisualTreeHelper.GetChild(node, i);
                if (child != null)
                    stack.Push(child);
            }
        }
        return null;
    }

    private static FrameworkElement Host(Grid grid) => VisualTreeHelper.GetParent(grid) as FrameworkElement;

    private static float Zero(float v) => float.IsNaN(v) ? 0f : v;

    /// <summary>The saved place, as the left edge and the top of the panel inside its canvas.</summary>
    private static bool Saved(out float x, out float y)
    {
        x = y = 0f;
        string[] parts = (SmartSettings.TimePanelPosition.Value ?? "").Split(',');
        return parts.Length == 2
            && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
            && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
    }

    private static void Place(Grid grid)
    {
        FrameworkElement host = Host(grid);
        if (host == null || host.ActualWidth < 1f)
            return;
        float right = Zero(Canvas.GetRight(grid));
        float top = Zero(Canvas.GetTop(grid));
        if (Saved(out float x, out float y))
        {
            right = host.ActualWidth - x - grid.ActualWidth;
            top = y;
        }
        Clamp(grid, host, ref right, ref top);
        if (Math.Abs(Zero(Canvas.GetRight(grid)) - right) > 0.01f)
            Canvas.SetRight(grid, right);
        if (Math.Abs(Zero(Canvas.GetTop(grid)) - top) > 0.01f)
            Canvas.SetTop(grid, top);
    }

    private static void Clamp(Grid grid, FrameworkElement host, ref float right, ref float top)
    {
        right = Math.Max(0f, Math.Min(right, Math.Max(0f, host.ActualWidth - grid.ActualWidth)));
        top = Math.Max(0f, Math.Min(top, Math.Max(0f, host.ActualHeight - grid.ActualHeight)));
    }

    private static void Attach(Grid grid)
    {
        Point start = default;
        float right0 = 0f, top0 = 0f;

        grid.MouseLeftButtonDown += (s, e) =>
        {
            try
            {
                FrameworkElement host = Host(grid);
                if (host == null)
                    return;
                if (e.ClickCount >= 2)
                {
                    SmartSettings.TimePanelPosition.Value = "";
                    Canvas.SetRight(grid, 0f);
                    Canvas.SetTop(grid, 0f);
                    Place(grid);
                }
                else
                {
                    dragging = true;
                    start = e.GetPosition(host);
                    right0 = Zero(Canvas.GetRight(grid));
                    top0 = Zero(Canvas.GetTop(grid));
                    grid.CaptureMouse();
                }
                e.Handled = true;
            }
            catch (Exception ex) { ModLog.Debug($"time panel down: {ex.Message}"); }
        };
        grid.MouseMove += (s, e) =>
        {
            if (!dragging)
                return;
            try
            {
                FrameworkElement host = Host(grid);
                if (host == null)
                    return;
                Point now = e.GetPosition(host);
                float right = right0 - (now.X - start.X);
                float top = top0 + (now.Y - start.Y);
                Clamp(grid, host, ref right, ref top);
                Canvas.SetRight(grid, right);
                Canvas.SetTop(grid, top);
            }
            catch (Exception ex) { ModLog.Debug($"time panel move: {ex.Message}"); }
        };
        grid.MouseLeftButtonUp += (s, e) =>
        {
            if (!dragging)
                return;
            dragging = false;
            try
            {
                grid.ReleaseMouseCapture();
                e.Handled = true;
                FrameworkElement host = Host(grid);
                if (host == null)
                    return;
                float x = (float)Math.Round(host.ActualWidth - Zero(Canvas.GetRight(grid)) - grid.ActualWidth);
                float y = (float)Math.Round(Zero(Canvas.GetTop(grid)));
                SmartSettings.TimePanelPosition.Value =
                    x.ToString(CultureInfo.InvariantCulture) + "," + y.ToString(CultureInfo.InvariantCulture);
            }
            catch (Exception ex) { ModLog.Debug($"time panel up: {ex.Message}"); }
        };
    }
}
