using System;
using System.Collections.Generic;
using Noesis;
using SeapowerUI;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Finds the place where the Unit Status window shows the content of its tabs and puts a host (a <see cref="Border"/>)
/// next to it, so a tab the mod adds can show its own content. The game only knows its five tabs: any other needs a
/// place of its own, hidden until the mod's tab is selected.
/// </summary>
internal static class StatusTabHost
{
    private const int MaxNodes = 30000;

    /// <summary>The host Border, or null when the window's tree has no recognisable content area (game updated?).</summary>
    public static Border Create(UnitStatusView view, UnitStatusViewModel status)
    {
        FrameworkElement content = FindByContent(view, status) ?? FindByVisibilityBinding(view);
        if (content == null)
            return null;

        // Climb to the direct child of the panel that holds the tab contents.
        FrameworkElement child = content;
        DependencyObject parent = VisualTreeHelper.GetParent(child);
        while (parent != null && !(parent is Panel) && parent != view)
        {
            if (!(parent is FrameworkElement fe))
                return null;
            child = fe;
            parent = VisualTreeHelper.GetParent(child);
        }
        if (!(parent is Panel panel))
            return null;

        var host = new Border
        {
            Visibility = Visibility.Collapsed,
            Margin = child.Margin,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        Grid.SetRow(host, Grid.GetRow(child));
        Grid.SetColumn(host, Grid.GetColumn(child));
        Grid.SetRowSpan(host, Grid.GetRowSpan(child));
        Grid.SetColumnSpan(host, Grid.GetColumnSpan(child));
        int at = panel.Children.IndexOf(child);
        panel.Children.Insert(at < 0 ? panel.Children.Count : at + 1, host);
        return host;
    }

    /// <summary>The element that shows one of the game tabs' content (a content control with that content, outside the tab strip).</summary>
    private static FrameworkElement FindByContent(DependencyObject root, UnitStatusViewModel status)
    {
        var contents = new List<object>();
        foreach (UnitStatusTabViewModel tab in new[] { status.EngagementTab, status.SensorsTab, status.DamageControlTab, status.FlightDeckTab, status.SeekerTab })
            if (tab?.Content != null)
                contents.Add(tab.Content);
        if (contents.Count == 0)
            return null;
        return Find(root, e =>
        {
            object content = e is ContentControl cc ? cc.Content : e is ContentPresenter cp ? cp.Content : null;
            return content != null && contents.Contains(content) && !(VisualTreeHelper.GetParent(e) is TabPanel);
        });
    }

    /// <summary>The element whose visibility is bound to a tab's <c>IsSelected</c> (the game shows each tab's content that way).</summary>
    private static FrameworkElement FindByVisibilityBinding(DependencyObject root) =>
        Find(root, e =>
        {
            string path = null;
            try
            {
                path = BindingOperations.GetBindingExpression(e, UIElement.VisibilityProperty)?.ParentBinding?.Path?.Path;
            }
            catch (Exception)
            {
            }
            return path != null && path.EndsWith("Tab.IsSelected", StringComparison.Ordinal);
        });

    private static FrameworkElement Find(DependencyObject root, Func<FrameworkElement, bool> match)
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        for (int budget = MaxNodes; stack.Count > 0 && budget > 0; budget--)
        {
            DependencyObject node = stack.Pop();
            if (node != root && node is FrameworkElement fe && match(fe))
                return fe;
            for (int i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--)
            {
                DependencyObject child = VisualTreeHelper.GetChild(node, i);
                if (child != null)
                    stack.Push(child);
            }
        }
        return null;
    }
}
