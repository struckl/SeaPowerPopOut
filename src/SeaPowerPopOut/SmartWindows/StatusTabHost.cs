using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Noesis;
using SeaPower;
using SeapowerUI;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// The Unit Status window's tabs as the mod's own tabs (Reference, Track) use them. The game binds its tab list to
/// <c>UnitStatusViewModel.Tabs</c> and knows five kinds (<see cref="UnitStatusTab"/> 0-4): a tab model of another kind gets
/// a header by itself, and the game's content switch ignores it. Its content needs a place of its own: a host (a
/// <see cref="Border"/>) next to where the window shows the game tabs' content, hidden until the mod's tab is selected.
/// </summary>
internal static class StatusTabHost
{
    /// <summary>The kinds of the mod's tabs, outside the game's enum and apart from each other.</summary>
    public const UnitStatusTab ReferenceKind = (UnitStatusTab)5;
    public const UnitStatusTab TrackKind = (UnitStatusTab)7;

    private const int SearchBudget = 30000;

    public static readonly FieldInfo UnitField = AccessTools.Field(typeof(UnitStatusViewModel), "_unit");
    public static readonly MethodInfo SelectFirstTab = AccessTools.Method(typeof(UnitStatusViewModel), "SelectFirstAvailableTab");
    public static readonly MethodInfo SetUnitName =
        AccessTools.PropertySetter(typeof(UnitStatusViewModel), nameof(UnitStatusViewModel.UnitName));

    /// <summary>The unit the window shows (null without one, e.g. while a contact is selected).</summary>
    public static ObjectBase UnitOf(UnitStatusViewModel status) => UnitField.GetValue(status) as ObjectBase;

    public static bool HasUnit(UnitStatusViewModel status) => UnitField.GetValue(status) != null;

    /// <summary>
    /// The host for a mod tab's content, or null when the window's tree has no recognisable content area (game updated?).
    /// <paramref name="anchor"/>: the game element the host was put next to; null when no tab content was found at all.
    /// The Reference tab looks for the element bound to a tab's <c>IsSelected</c> first, the Track tab for the element
    /// holding a tab's content first; both land in the panel of the tab contents.
    /// </summary>
    public static Border Create(UnitStatusView view, UnitStatusViewModel status, bool bindingFirst, out FrameworkElement anchor)
    {
        anchor = bindingFirst
            ? FindByVisibilityBinding(view) ?? FindByContent(view, status)
            : FindByContent(view, status) ?? FindByVisibilityBinding(view);
        if (anchor == null)
            return null;

        // Climb to the direct child of the panel that holds the tab contents.
        DependencyObject parent = VisualTreeHelper.GetParent(anchor);
        while (parent != null && !(parent is Panel) && parent != view)
        {
            if (!(parent is FrameworkElement up))
                return null;
            anchor = up;
            parent = VisualTreeHelper.GetParent(up);
        }
        if (!(parent is Panel panel))
            return null;

        var host = new Border
        {
            Visibility = Visibility.Collapsed,
            Margin = anchor.Margin,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        Grid.SetRow(host, Grid.GetRow(anchor));
        Grid.SetColumn(host, Grid.GetColumn(anchor));
        Grid.SetRowSpan(host, Grid.GetRowSpan(anchor));
        Grid.SetColumnSpan(host, Grid.GetColumnSpan(anchor));
        int at = panel.Children.IndexOf(anchor);
        panel.Children.Insert(at < 0 ? panel.Children.Count : at + 1, host);
        return host;
    }

    /// <summary>Shows <paramref name="page"/> in the host, or hides the host (and lets go of a page that is gone).</summary>
    public static void Show(Border host, UIElement page, bool show)
    {
        if (show)
        {
            if (host.Child != page)
            {
                if (VisualTreeHelper.GetParent(page) is Border old && old != host)
                    old.Child = null;
                host.Child = page;
            }
            host.Visibility = Visibility.Visible;
            return;
        }
        if (host.Visibility != Visibility.Collapsed)
            host.Visibility = Visibility.Collapsed;
        if (page == null && host.Child != null)
            host.Child = null;
    }

    /// <summary>The element that shows one of the game tabs' content (a content control with that content, outside the tab strip).</summary>
    private static FrameworkElement FindByContent(DependencyObject root, UnitStatusViewModel status)
    {
        var contents = new[] { status.EngagementTab, status.SensorsTab, status.DamageControlTab, status.FlightDeckTab, status.SeekerTab }
            .Select(tab => tab?.Content).Where(content => content != null).ToList();
        if (contents.Count == 0)
            return null;
        return VisualSearch.First<FrameworkElement>(root, e =>
        {
            object content = e is ContentControl cc ? cc.Content : e is ContentPresenter cp ? cp.Content : null;
            return content != null && contents.Contains(content) && !(VisualTreeHelper.GetParent(e) is TabPanel);
        }, SearchBudget);
    }

    /// <summary>
    /// The element whose visibility is bound to a game tab's <c>IsSelected</c> (e.g. <c>SensorsTab.IsSelected</c>): the
    /// game shows each tab's content that way.
    /// </summary>
    private static FrameworkElement FindByVisibilityBinding(DependencyObject root) =>
        VisualSearch.First<FrameworkElement>(root, e =>
        {
            string path = Bindings.PathOf(e, UIElement.VisibilityProperty);
            return path != null && path.EndsWith("Tab.IsSelected", StringComparison.Ordinal) && !path.StartsWith("Reference", StringComparison.Ordinal);
        }, SearchBudget);
}
