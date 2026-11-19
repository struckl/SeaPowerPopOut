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
/// <para>A tab of a kind the game does not know, with its page in a host next to the other tabs' content
/// (<see cref="StatusTabHost"/>), shown while the Reference tab is selected.</para>
/// </summary>
internal static class UnitStatusReference
{
    public static readonly Feature Feature = new Feature
    {
        Switch = () => SmartSettings.StatusReference.Value,
        LiveSwitch = () => SmartSettings.StatusReference.Value,
        Hook = Apply,
        Hooked = "Unit Status has a Reference tab",
        WindowLoaded = WindowLoaded,
        Update = Update,
    };

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
    private static MethodInfo ensureInnerTab;

    private static bool Apply(Harmony harmony)
    {
        ensureInnerTab = AccessTools.Method(typeof(ObjectInformationViewModel), "EnsureSelectedTabAvailable");
        MethodInfo switchUnit = AccessTools.Method(typeof(UnitStatusViewModel), "SwitchUnit");
        MethodInfo ensureContent = AccessTools.Method(typeof(UnitStatusViewModel), "EnsureContent");
        MethodInfo disposeContent = AccessTools.Method(typeof(UnitStatusViewModel), "DisposeOwnedContent");
        var constructors = AccessTools.GetDeclaredConstructors(typeof(UnitStatusViewModel));
        if (StatusTabHost.UnitField == null || switchUnit == null || ensureContent == null || disposeContent == null || constructors.Count == 0)
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
            var tab = new UnitStatusTabViewModel(__instance, StatusTabHost.ReferenceKind, Loc.Get("ref_tab"));
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
        bool available = HasReference(StatusTabHost.UnitOf(vm));
        state.Tab.IsAvailable = available;
        if (!available && state.Tab.IsSelected)
        {
            state.Tab.IsSelected = false;
            StatusTabHost.SelectFirstTab?.Invoke(vm, null);
        }
    }

    private static void BuildContent(UnitStatusViewModel vm, State state)
    {
        UnitStatusTabViewModel tab = state.Tab;
        if (!tab.IsAvailable || tab.HasContentError || tab.Content != null)
            return;
        ObjectBase unit = StatusTabHost.UnitOf(vm);
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

    /// <summary>Remembers the Unit Status windows.</summary>
    private static void WindowLoaded(GameWindow window)
    {
        if (!IsUnitStatus(window))
            return;
        Places.Add(new Place { Window = new WeakReference<GameWindow>(window) });
    }

    private static bool IsUnitStatus(GameWindow window) => GameWindows.Is(window, GameWindows.UnitStatus);

    /// <summary>Puts the page of the selected Reference tab into its window.</summary>
    private static void Update()
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
        UnitStatusView view = FindView(place, window);
        if (view == null || !(view.DataContext is UnitStatusViewModel vm) || !States.TryGetValue(vm, out State state))
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
        StatusTabHost.Show(place.Host, page, show);
    }

    /// <summary>The window's Unit Status view; looked for a few times a second until it is there (or given up).</summary>
    private static UnitStatusView FindView(Place place, GameWindow window)
    {
        if (place.View.TryGetTargetOrNull(out UnitStatusView view) && VisualTreeHelper.GetParent(view) != null)
            return view;
        if (Time.unscaledTime < place.Next)
            return null;
        place.Next = Time.unscaledTime + RetryEvery;
        view = VisualSearch.First<UnitStatusView>(window, budget: SearchBudget);
        if (view == null)
        {
            if (++place.Tries > MaxTries)
                place.Failed = true;
            return null;
        }
        place.View = new WeakReference<UnitStatusView>(view);
        place.Tries = 0;
        return view;
    }

    private static Border MakeHost(UnitStatusView view, UnitStatusViewModel vm)
    {
        Border host = StatusTabHost.Create(view, vm, bindingFirst: true, out FrameworkElement anchor);
        if (anchor == null)
            ModLog.Warn("Reference tab: no place of another tab's content found in the Unit Status window (list 'Unit Status*' under Dump window trees to see its layout).");
        else if (host == null)
            ModLog.Warn("Reference tab: no panel above another tab's content.");
        else
            ModLog.Debug($"reference tab: host added next to {anchor.GetType().Name}");
        return host;
    }

    private static bool TryGetTargetOrNull<T>(this WeakReference<T> reference, out T target) where T : class
    {
        target = null;
        return reference != null && reference.TryGetTarget(out target);
    }
}
