using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Noesis;
using SeapowerUI;
using SeapowerUI.ViewModels;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// What hiding the unit tree of a Unit Reference window does besides hiding the tree: the search panel above it goes
/// too (the rows below move up into its place), the window becomes narrower by the tree's width and moves right by the
/// same amount, so the unit information stays where it was. Showing the tree reverses all of it.
/// </summary>
internal sealed class TreeCollapse
{
    private const float MinWindowWidth = 20f;

    private static readonly MethodInfo MarkUserPositioned = AccessTools.Method(typeof(GameWindow), "MarkUserPositioned");

    private readonly GameWindow window;
    private readonly UnitTreeBrowserView view;
    private readonly FrameworkElement block;
    private readonly ObjectInformationViewModel vm;

    private float offset;                     // how much narrower the window is while the tree is hidden
    private HiddenPanel hidden;

    private sealed class HiddenPanel
    {
        public FrameworkElement Panel;
        public float Opacity;
        public bool HitTestVisible;
        public readonly List<(UIElement Element, int Row, int Span)> Moved = new List<(UIElement, int, int)>();
    }

    public TreeCollapse(GameWindow window, UnitTreeBrowserView view, FrameworkElement block, ObjectInformationViewModel vm)
    {
        this.window = window;
        this.view = view;
        this.block = block;
        this.vm = vm;
    }

    /// <summary>How much the window is narrower than the layout that should be saved (0 while the tree is shown).</summary>
    public float Offset => offset;

    public void Apply(bool treeVisible)
    {
        try
        {
            if (!window.Maximized)
                ResizeWindow(treeVisible);
            if (treeVisible)
                ShowPanel();
            else
                HidePanel();
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Unit Reference: tree {(treeVisible ? "show" : "hide")} adjustments failed: {ex.Message}");
        }
    }

    // ---- window size --------------------------------------------------------------------------------------------

    private void ResizeWindow(bool treeVisible)
    {
        float width = float.IsNaN(window.Width) || window.Width <= 0f ? window.ActualWidth : window.Width;
        if (width <= 0f)
            return;

        if (!treeVisible)
        {
            if (offset > 0f)
                return;
            float blockWidth = block.ActualWidth + block.Margin.Left + block.Margin.Right;
            float newWidth = Math.Max(Math.Max(window.MinWidth, MinWindowWidth), width - blockWidth);
            float delta = width - newWidth;
            if (delta < 1f)
                return;
            window.Width = newWidth;
            window.X += delta;
            offset = delta;
        }
        else
        {
            float restore = offset;
            offset = 0f;
            if (restore <= 0f)
                return;
            WinRect wanted = WindowGeometry.KeepReachable(
                new WinRect(window.X - restore, window.Y, width + restore, window.ActualHeight),
                HostArea.Of(window), SmartSettings.MinVisiblePixels.Value, 28f);
            window.Width = wanted.W;
            window.X = wanted.X;
            window.Y = wanted.Y;
        }
        MarkUserPositioned?.Invoke(window, null);       // keeps windows anchored to a corner from snapping back
    }

    // ---- search panel -------------------------------------------------------------------------------------------

    private void HidePanel()
    {
        if (hidden != null)
            return;
        FrameworkElement anchor = FindSearchBox();
        if (anchor == null)
        {
            ModLog.Debug("Unit Reference: search box not found, search panel stays");
            return;
        }

        // The panel is the part of the window above the tree that holds the search box: the child, on the search
        // box's side, of the first ancestor it shares with the tree.
        var treePath = new HashSet<DependencyObject>();
        for (DependencyObject n = view; n != null; n = VisualTreeHelper.GetParent(n))
        {
            treePath.Add(n);
            if (n == window)
                break;
        }

        DependencyObject panel = anchor;
        DependencyObject common = null;
        for (DependencyObject n = VisualTreeHelper.GetParent(anchor); n != null; panel = n, n = VisualTreeHelper.GetParent(n))
        {
            if (treePath.Contains(n))
            {
                common = n;
                break;
            }
            if (n == window)
                break;
        }
        if (common == null || !(panel is FrameworkElement panelElement) || ContainsToggle(panelElement))
        {
            ModLog.Debug("Unit Reference: search panel cannot be separated from the tree, it stays");
            return;
        }

        hidden = new HiddenPanel { Panel = panelElement, Opacity = panelElement.Opacity, HitTestVisible = panelElement.IsHitTestVisible };
        panelElement.Opacity = 0f;
        panelElement.IsHitTestVisible = false;
        if (common is Grid grid)
            MoveUp(grid, panelElement, hidden);
    }

    private void ShowPanel()
    {
        HiddenPanel panel = hidden;
        if (panel == null)
            return;
        hidden = null;
        for (int i = panel.Moved.Count - 1; i >= 0; i--)
        {
            (UIElement element, int row, int span) = panel.Moved[i];
            Grid.SetRowSpan(element, span);
            Grid.SetRow(element, row);
        }
        panel.Panel.Opacity = panel.Opacity;
        panel.Panel.IsHitTestVisible = panel.HitTestVisible;
    }

    /// <summary>The rows right below the hidden panel grow upwards into its place.</summary>
    private static void MoveUp(Grid grid, UIElement panel, HiddenPanel state)
    {
        int row = Grid.GetRow(panel);
        int span = Math.Max(1, Grid.GetRowSpan(panel));
        int below = row + span;
        for (int i = VisualTreeHelper.GetChildrenCount(grid) - 1; i >= 0; i--)
        {
            if (!(VisualTreeHelper.GetChild(grid, i) is UIElement element) || element == panel || Grid.GetRow(element) != below)
                continue;
            int oldSpan = Grid.GetRowSpan(element);
            state.Moved.Add((element, below, oldSpan));
            Grid.SetRow(element, row);
            Grid.SetRowSpan(element, oldSpan + span);
        }
    }

    /// <summary>
    /// The visible search text box outside the tree: the one whose text is bound to a property named "...SearchText".
    /// Falls back to the first element that has the tree browser as its data context.
    /// </summary>
    private FrameworkElement FindSearchBox()
    {
        FrameworkElement fallback = null;
        var stack = new Stack<(DependencyObject Node, bool UnderBrowser)>();
        stack.Push((window, false));
        for (int budget = 20000; stack.Count > 0 && budget > 0; budget--)
        {
            (DependencyObject node, bool underBrowser) = stack.Pop();
            if (node == view)
                continue;
            if (node is FrameworkElement element && element.Visibility != Visibility.Visible)
                continue;

            if (node is TextBox box)
            {
                if (box.IsVisible && BindsToSearchText(box))
                    return box;
                continue;
            }

            bool isBrowser = node is FrameworkElement fe && ReferenceEquals(fe.DataContext, vm.Browser);
            if (isBrowser && !underBrowser && fallback == null)
                fallback = node as FrameworkElement;
            for (int i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--)
                stack.Push((VisualTreeHelper.GetChild(node, i), isBrowser));
        }
        return fallback;
    }

    private static bool BindsToSearchText(TextBox box) =>
        BindingOperations.GetBinding(box, TextBox.TextProperty) is Binding binding
        && binding.Path?.Path != null
        && binding.Path.Path.EndsWith("SearchText", StringComparison.Ordinal);

    private bool ContainsToggle(DependencyObject root)
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        for (int budget = 5000; stack.Count > 0 && budget > 0; budget--)
        {
            DependencyObject node = stack.Pop();
            if (node is ButtonBase button && ReferenceEquals(button.Command, vm.ToggleTreeCommand))
                return true;
            for (int i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--)
                stack.Push(VisualTreeHelper.GetChild(node, i));
        }
        return false;
    }
}
