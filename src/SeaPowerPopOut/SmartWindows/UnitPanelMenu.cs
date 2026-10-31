using System;
using System.Linq;
using Noesis;
using SeaPower;
using SeapowerUI;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Mission screen: a right click on the flag or the name of the selected unit in the unit panel at the bottom opens the
/// same menu as a right click on the unit on the map. The panel's flag and name are found by what they are bound to.
/// </summary>
internal static class UnitPanelMenu
{
    private const string FlagBinding = "ObjectNation.Value";
    private const string NameBinding = "ObjectName.Value";
    private const int ParentsToCheck = 4;

    private static bool enabled;
    private static WeakReference<MainGameView> hooked = new WeakReference<MainGameView>(null);

    public static void Register() => enabled = true;

    /// <summary>Called every frame: hooks each new mission view (Noesis does not take a class handler for it).</summary>
    public static void Update()
    {
        if (!enabled)
            return;
        MainGameView view = MainGameView.Instance;
        if (view == null || (hooked.TryGetTarget(out MainGameView done) && ReferenceEquals(done, view)))
            return;
        hooked = new WeakReference<MainGameView>(view);
        try
        {
            view.PreviewMouseRightButtonDown += RightDown;
            ModLog.Debug("unit panel menu: mission view hooked");
        }
        catch (Exception ex)
        {
            enabled = false;
            ModLog.Warn($"Unit panel menu not available: {ex.Message}");
        }
    }

    private static void RightDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement view && OverFlagOrName(view, e))
                OpenMenu(e);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"unit panel menu: {ex.Message}");
        }
    }

    /// <summary>True if the click landed on the unit panel's flag or name (or something just inside one of them).</summary>
    private static bool OverFlagOrName(FrameworkElement view, MouseButtonEventArgs e)
    {
        DependencyObject hit = VisualTreeHelper.HitTest(view, e.GetPosition(view)).VisualHit ?? e.Source as DependencyObject;
        var seen = new System.Text.StringBuilder();
        for (int i = 0; hit != null && i < ParentsToCheck; i++, hit = VisualTreeHelper.GetParent(hit))
        {
            string path = hit is Image ? BindingPath(hit, Image.SourceProperty)
                : hit is TextBlock ? BindingPath(hit, TextBlock.TextProperty) : null;
            seen.Append(hit.GetType().Name).Append(path == null ? "" : $"[{path}]").Append(" < ");
            if ((hit is Image && path == FlagBinding) || (hit is TextBlock && path == NameBinding))
                return true;
        }
        ModLog.Debug($"unit panel menu: right click on {seen}");
        return false;
    }

    private static void OpenMenu(MouseButtonEventArgs e)
    {
        // The game opens a unit's menu on this canvas (MainGameViewModel.OpenContextMenuCommand).
        ContextMenu menu = MainGameView.Instance?.ContextMenuCanvas?.ContextMenu;
        ISelectableObject subject = Globals._mainGameViewModel?.SelectedObject?.Value;
        if (subject == null || menu == null)
        {
            ModLog.Debug($"unit panel menu: no subject ({subject == null}) or no menu");
            return;
        }

        IContextMenuObject source = MenuSource(subject);
        var items = source?.GetContextMenuItems();
        if (items == null)
        {
            ModLog.Debug($"unit panel menu: no menu for {subject.GetType().Name} (source {source?.GetType().Name ?? "none"})");
            return;
        }

        menu.ItemsSource = items;
        menu.IsOpen = true;
        e.Handled = true;
    }

    /// <summary>
    /// Whose menu to show. The player's own units give their own menu; other objects give theirs; a contact (a vehicle
    /// seen by the player) gives the menu of its marker on the map.
    /// </summary>
    private static IContextMenuObject MenuSource(ISelectableObject subject)
    {
        var vehicle = subject as Vehicle;
        ObjectBase obj = vehicle != null ? vehicle.BaseObject : subject as ObjectBase;
        if (obj == null)
            return null;

        bool isUnit = obj.isUnit();
        if (isUnit && obj.AcceptsOrdersFromPlayer && obj.IsPlayerObject)
            return obj.ViewModel as IContextMenuObject;
        if (vehicle == null && !isUnit)
            return obj.ViewModel as IContextMenuObject;
        return MapMarkerOf(subject, obj);
    }

    private static IContextMenuObject MapMarkerOf(ISelectableObject subject, ObjectBase obj)
    {
        var markers = Globals._mainGameViewModel?.Map?.MapKnownUnits?.AllUnits;
        if (markers == null)
            return null;
        foreach (MapUnitViewModel marker in markers)
        {
            object unit = marker.Unit;
            if (ReferenceEquals(unit, subject) || ReferenceEquals(unit, obj) || ReferenceEquals((unit as Vehicle)?.BaseObject, obj))
                return marker;
        }
        return null;
    }

    private static string BindingPath(DependencyObject element, DependencyProperty property)
    {
        try
        {
            return BindingOperations.GetBindingExpression(element, property)?.ParentBinding?.Path?.Path;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
