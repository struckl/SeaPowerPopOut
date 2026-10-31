using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Noesis;
using SeaPower;
using SeapowerUI;
using SeapowerUI.ViewModels;
using UnityEngine;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Unit Status window: an extra "Reference" tab that shows the Unit Reference page (<see cref="ObjectInformationView"/>)
/// of the selected unit, on its own variant and loadout.
/// <para>The game's tab list is bound to <c>UnitStatusViewModel.Tabs</c>, so a fifth tab model (kind 5, which the game's
/// enum does not have) gets a header by itself. The view has no place for its content though: a host border is put into
/// the same panel as another tab's content, and shown while the Reference tab is selected.</para>
/// </summary>
internal static class UnitStatusReference
{
    private const UnitStatusTab ReferenceKind = (UnitStatusTab)5;
    private const int SearchBudget = 30000;
    private const float RetryEvery = 0.25f;
    private const int MaxTries = 40;

    /// <summary>What belongs to one Unit Status view model.</summary>
    private sealed class State
    {
        public UnitStatusTabViewModel Tab;
        public ObjectInformationViewModel Vm;
        public ObjectInformationView View;
        public int InnerTab = -1;                 // the page's tab (Overview, Weapons ...) kept when the content is rebuilt
    }

    /// <summary>What belongs to one Unit Status window.</summary>
    private sealed class Place
    {
        public WeakReference<GameWindow> Window;
        public WeakReference<UnitStatusView> View;
        public Border Host;
        public bool Failed;
        public float Next;
        public int Tries;
    }

    private static readonly ConditionalWeakTable<UnitStatusViewModel, State> States = new ConditionalWeakTable<UnitStatusViewModel, State>();
    private static readonly List<Place> Places = new List<Place>();
    private static FieldInfo unitField;
    private static MethodInfo selectFirstTab;
    private static MethodInfo ensureInnerTab;

    public static bool Apply(Harmony harmony)
    {
        unitField = AccessTools.Field(typeof(UnitStatusViewModel), "_unit");
        selectFirstTab = AccessTools.Method(typeof(UnitStatusViewModel), "SelectFirstAvailableTab");
        ensureInnerTab = AccessTools.Method(typeof(ObjectInformationViewModel), "EnsureSelectedTabAvailable");
        MethodInfo switchUnit = AccessTools.Method(typeof(UnitStatusViewModel), "SwitchUnit");
        MethodInfo ensureContent = AccessTools.Method(typeof(UnitStatusViewModel), "EnsureContent");
        MethodInfo disposeContent = AccessTools.Method(typeof(UnitStatusViewModel), "DisposeOwnedContent");
        var constructors = AccessTools.GetDeclaredConstructors(typeof(UnitStatusViewModel));
        if (unitField == null || switchUnit == null || ensureContent == null || disposeContent == null || constructors.Count == 0)
        {
            ModLog.Warn("Unit Status members not found (game updated?) - the Reference tab is not available.");
            return false;
        }
        foreach (ConstructorInfo constructor in constructors)
            harmony.Patch(constructor, postfix: new HarmonyMethod(typeof(UnitStatusReference), nameof(Created)));
        harmony.Patch(switchUnit, postfix: new HarmonyMethod(typeof(UnitStatusReference), nameof(UnitSwitched)));
        harmony.Patch(ensureContent, postfix: new HarmonyMethod(typeof(UnitStatusReference), nameof(ContentWanted)));
        harmony.Patch(disposeContent, prefix: new HarmonyMethod(typeof(UnitStatusReference), nameof(ContentDropped)));
        return true;
    }

    // ---- game hooks -------------------------------------------------------------------------------------------

    private static void Created(UnitStatusViewModel __instance)
    {
        try
        {
            var tab = new UnitStatusTabViewModel(__instance, ReferenceKind, Loc.Get("ref_tab"));
            __instance.Tabs.Add(tab);
            States.Add(__instance, new State { Tab = tab });
            UpdateAvailability(__instance);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"reference tab: {ex}");
        }
    }

    private static void UnitSwitched(UnitStatusViewModel __instance)
    {
        try
        {
            UpdateAvailability(__instance);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"reference tab: {ex.Message}");
        }
    }

    private static void ContentWanted(UnitStatusViewModel __instance, UnitStatusTabViewModel tab)
    {
        try
        {
            if (tab != null && States.TryGetValue(__instance, out State state) && state.Tab == tab)
                BuildContent(__instance, state);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"reference tab: {ex.Message}");
        }
    }

    private static void ContentDropped(UnitStatusViewModel __instance)
    {
        try
        {
            if (States.TryGetValue(__instance, out State state))
                DropContent(state);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"reference tab: {ex.Message}");
        }
    }

    // ---- tab model --------------------------------------------------------------------------------------------

    /// <summary>The page exists for every unit with its own parameters (ships, subs, aircraft ...), but not for weapons.</summary>
    private static bool HasReference(ObjectBase unit) => unit != null && !(unit is WeaponBase) && unit._obp != null;

    private static void UpdateAvailability(UnitStatusViewModel vm)
    {
        if (!States.TryGetValue(vm, out State state))
            return;
        bool available = HasReference(unitField.GetValue(vm) as ObjectBase);
        state.Tab.IsAvailable = available;
        if (!available && state.Tab.IsSelected)
        {
            state.Tab.IsSelected = false;
            selectFirstTab?.Invoke(vm, null);
        }
    }

    private static void BuildContent(UnitStatusViewModel vm, State state)
    {
        UnitStatusTabViewModel tab = state.Tab;
        if (!tab.IsAvailable || tab.HasContentError || tab.Content != null)
            return;
        ObjectBase unit = unitField.GetValue(vm) as ObjectBase;
        if (!HasReference(unit))
            return;
        try
        {
            string ini = unit.getObjectIniName();
            if (string.IsNullOrEmpty(ini))
                return;
            int variant = ObjectInformationViewModel.GetReferenceVariantIndex(unit);
            string loadout = ObjectInformationViewModel.GetReferenceLoadoutVariant(unit);
            var page = new ObjectInformationViewModel(MainGameView.Instance, ini, variant, false, loadout);
            if (state.InnerTab > 0)
            {
                page.SelectedTabIndex = state.InnerTab;
                ensureInnerTab?.Invoke(page, null);
            }
            state.Vm = page;
            state.View = new ObjectInformationView { DataContext = page };
            tab.Content = state.View;
            ModLog.Debug($"reference tab: {ini} #{variant}{(string.IsNullOrEmpty(loadout) ? "" : $" [{loadout}]")}");
        }
        catch (Exception ex)
        {
            ModLog.Debug($"reference tab: {ex}");
            DropContent(state);
            tab.ContentError = Loc.Get("ref_tab_error");
        }
    }

    private static void DropContent(State state)
    {
        if (state.Vm == null && state.View == null)
            return;
        ObjectInformationViewModel page = state.Vm;
        if (page != null)
            state.InnerTab = page.SelectedTabIndex;
        state.Tab.Content = null;
        state.Vm = null;
        state.View = null;
        try
        {
            page?.Dispose();
        }
        catch (Exception ex)
        {
            ModLog.Debug($"reference tab dispose: {ex.Message}");
        }
    }

    // ---- window side ------------------------------------------------------------------------------------------

    /// <summary>Called when a game window has loaded: remembers the Unit Status windows.</summary>
    public static void WindowLoaded(GameWindow window)
    {
        if (!IsUnitStatus(window))
            return;
        Places.Add(new Place { Window = new WeakReference<GameWindow>(window) });
    }

    private static bool IsUnitStatus(GameWindow window) =>
        WindowNames.Of(window).Any(n => n.Replace(" ", "").StartsWith("UnitStatus", StringComparison.OrdinalIgnoreCase));

    /// <summary>Called every frame: puts the page of the selected Reference tab into its window.</summary>
    public static void Update()
    {
        for (int i = Places.Count - 1; i >= 0; i--)
        {
            Place place = Places[i];
            if (!place.Window.TryGetTarget(out GameWindow window) || !window.IsLoaded)
            {
                Places.RemoveAt(i);
                continue;
            }
            if (place.Failed)
                continue;
            try
            {
                Step(place, window);
            }
            catch (Exception ex)
            {
                place.Failed = true;
                ModLog.Debug($"reference tab: {ex}");
            }
        }
    }

    private static void Step(Place place, GameWindow window)
    {
        if (!place.View.TryGetTargetOrNull(out UnitStatusView view) || VisualTreeHelper.GetParent(view) == null)
        {
            if (Time.unscaledTime < place.Next)
                return;
            place.Next = Time.unscaledTime + RetryEvery;
            view = FindFirst<UnitStatusView>(window);
            if (view == null)
            {
                if (++place.Tries > MaxTries)
                    place.Failed = true;
                return;
            }
            place.View = new WeakReference<UnitStatusView>(view);
            place.Tries = 0;
        }
        if (!(view.DataContext is UnitStatusViewModel vm) || !States.TryGetValue(vm, out State state))
            return;

        UIElement page = state.View;
        bool show = state.Tab.IsSelected && state.Tab.IsAvailable && page != null;
        if (place.Host == null || VisualTreeHelper.GetParent(place.Host) == null)
        {
            // A page that already has a parent is displayed by the game itself.
            if (!show || VisualTreeHelper.GetParent(page) != null)
                return;
            place.Host = MakeHost(view, vm);
            if (place.Host == null)
            {
                place.Failed = true;
                return;
            }
        }
        Border host = place.Host;
        if (show)
        {
            if (host.Child != page)
            {
                if (VisualTreeHelper.GetParent(page) is Border old && old != host)
                    old.Child = null;
                host.Child = page;
            }
            host.Visibility = Visibility.Visible;
        }
        else
        {
            host.Visibility = Visibility.Collapsed;
            if (page == null && host.Child != null)
                host.Child = null;
        }
    }

    /// <summary>A border next to the content of another tab (same panel, same grid cell), to hold the Reference page.</summary>
    private static Border MakeHost(UnitStatusView view, UnitStatusViewModel vm)
    {
        FrameworkElement anchor = FindTabContent(view, vm);
        if (anchor == null)
        {
            ModLog.Warn("Reference tab: no place of another tab's content found in the Unit Status window (list 'Unit Status*' under Dump window trees to see its layout).");
            return null;
        }
        DependencyObject parent = VisualTreeHelper.GetParent(anchor);
        while (parent != null && !(parent is Panel) && parent != view)
        {
            if (!(parent is FrameworkElement up))
                break;
            anchor = up;
            parent = VisualTreeHelper.GetParent(up);
        }
        if (!(parent is Panel panel))
        {
            ModLog.Warn("Reference tab: no panel above another tab's content.");
            return null;
        }

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
        ModLog.Debug($"reference tab: host added next to {anchor.GetType().Name}");
        return host;
    }

    /// <summary>
    /// The element that shows another tab's content: the first one whose visibility is bound to a tab's
    /// <c>IsSelected</c> (e.g. <c>SensorsTab.IsSelected</c>), else the first content control holding another tab's content.
    /// </summary>
    private static FrameworkElement FindTabContent(UnitStatusView view, UnitStatusViewModel vm)
    {
        var others = new[] { vm.EngagementTab, vm.SensorsTab, vm.DamageControlTab, vm.FlightDeckTab, vm.SeekerTab }
            .Select(t => t.Content).Where(c => c != null).ToList();
        return FindFirst<FrameworkElement>(view, e => IsTabVisibilityBinding(e))
            ?? FindFirst<FrameworkElement>(view, e => !(VisualTreeHelper.GetParent(e) is TabPanel) && others.Contains(ContentOf(e)));
    }

    private static bool IsTabVisibilityBinding(DependencyObject element)
    {
        string path;
        try
        {
            path = BindingOperations.GetBindingExpression(element, UIElement.VisibilityProperty)?.ParentBinding?.Path?.Path;
        }
        catch (Exception)
        {
            return false;
        }
        return path != null && path.EndsWith("Tab.IsSelected", StringComparison.Ordinal) && !path.StartsWith("Reference", StringComparison.Ordinal);
    }

    private static object ContentOf(DependencyObject element) =>
        element is ContentControl control ? control.Content : element is ContentPresenter presenter ? presenter.Content : null;

    /// <summary>Depth-first search in the visual tree below <paramref name="root"/> (not the root itself).</summary>
    private static T FindFirst<T>(DependencyObject root, Func<T, bool> match = null) where T : DependencyObject
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        for (int budget = SearchBudget; stack.Count > 0 && budget > 0; budget--)
        {
            DependencyObject node = stack.Pop();
            if (node != root && node is T found && (match == null || match(found)))
                return found;
            for (int i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--)
            {
                DependencyObject child = VisualTreeHelper.GetChild(node, i);
                if (child != null)
                    stack.Push(child);
            }
        }
        return null;
    }

    private static bool TryGetTargetOrNull<T>(this WeakReference<T> reference, out T target) where T : class
    {
        target = null;
        return reference != null && reference.TryGetTarget(out target);
    }
}
