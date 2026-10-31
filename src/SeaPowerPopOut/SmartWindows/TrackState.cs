using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Noesis;
using SeaPower;
using SeapowerUI;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Unit Status window: while a contact of another side (a track, not your own unit) is selected, the window gets a
/// "Track" tab that shows the contact's Track State (the game's own Vehicle Info content: status, confidence,
/// detections, signatures) instead of staying empty. Selecting one of your units goes back to the normal tabs.
/// <para>The game has five tabs; this one is a sixth, with a kind the game does not know (so it never builds content
/// for it), shown through a host element next to the other tabs' content (<see cref="StatusTabHost"/>).</para>
/// </summary>
internal static class TrackState
{
    // A kind the game's UnitStatusTab enum does not have: its EnsureContent switch ignores it.
    private const UnitStatusTab TrackKind = (UnitStatusTab)7;

    private const string BodyXaml =
        "<Grid xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" Margin=\"6\">" +
        "<Grid.RowDefinitions><RowDefinition Height=\"*\"/><RowDefinition Height=\"Auto\"/></Grid.RowDefinitions>" +
        "<ItemsControl ItemsSource=\"{Binding StateComponents}\">" +
        "<ItemsControl.ItemsPanel><ItemsPanelTemplate><DockPanel LastChildFill=\"True\"/></ItemsPanelTemplate></ItemsControl.ItemsPanel>" +
        "<ItemsControl.ItemContainerStyle><Style TargetType=\"{x:Type ContentPresenter}\"><Setter Property=\"DockPanel.Dock\" Value=\"Top\"/></Style></ItemsControl.ItemContainerStyle>" +
        "<ItemsControl.ItemTemplate><DataTemplate><ContentControl Content=\"{Binding}\" Padding=\"5\" HorizontalAlignment=\"Stretch\" VerticalAlignment=\"Stretch\"/></DataTemplate></ItemsControl.ItemTemplate>" +
        "</ItemsControl>" +
        "<Button x:Name=\"ResetBtn\" Grid.Row=\"1\" Command=\"{Binding ResetTma}\" HorizontalAlignment=\"Right\" Margin=\"5\" Padding=\"10,3\"/></Grid>";

    private sealed class State
    {
        public UnitStatusTabViewModel Tab;
        public Vehicle Vehicle;
        public VehicleInfoViewModel Info;
        public FrameworkElement Body;
        public Border Host;
        public bool TookOver;           // the window had no unit of its own: this tab opened it
        public bool Failed;             // the body could not be built for this contact
        public bool HostFailed;         // no place found for the host: give up on this window
    }

    private static readonly ConditionalWeakTable<UnitStatusViewModel, State> States = new ConditionalWeakTable<UnitStatusViewModel, State>();
    private static readonly List<WeakReference<UnitStatusView>> Views = new List<WeakReference<UnitStatusView>>();

    private static FieldInfo unitField;
    private static MethodInfo setHasUnit;
    private static MethodInfo setUnitName;
    private static MethodInfo selectFirstTab;

    public static bool Apply(Harmony harmony)
    {
        var constructors = AccessTools.GetDeclaredConstructors(typeof(UnitStatusView));
        unitField = AccessTools.Field(typeof(UnitStatusViewModel), "_unit");
        setHasUnit = AccessTools.PropertySetter(typeof(UnitStatusViewModel), nameof(UnitStatusViewModel.HasUnit));
        setUnitName = AccessTools.PropertySetter(typeof(UnitStatusViewModel), nameof(UnitStatusViewModel.UnitName));
        selectFirstTab = AccessTools.Method(typeof(UnitStatusViewModel), "SelectFirstAvailableTab");
        if (constructors.Count == 0 || unitField == null || setHasUnit == null || setUnitName == null || selectFirstTab == null)
        {
            ModLog.Warn("Unit Status members not found (game updated?) - no Track State tab.");
            return false;
        }
        foreach (ConstructorInfo constructor in constructors)
            harmony.Patch(constructor, postfix: new HarmonyMethod(typeof(TrackState), nameof(ViewCreated)));
        return true;
    }

    private static void ViewCreated(UnitStatusView __instance) =>
        Views.Add(new WeakReference<UnitStatusView>(__instance));

    /// <summary>Called every frame (cheap when no Unit Status window is open).</summary>
    public static void Update()
    {
        for (int i = Views.Count - 1; i >= 0; i--)
        {
            if (!Views[i].TryGetTarget(out UnitStatusView view))
            {
                Views.RemoveAt(i);
                continue;
            }
            if (!(view.DataContext is UnitStatusViewModel status))
                continue;
            try
            {
                State state = States.GetValue(status, AddTab);
                Switch(status, state);
                Show(view, status, state);
            }
            catch (Exception ex)
            {
                ModLog.Debug($"track state: {ex.Message}");
            }
        }
    }

    private static State AddTab(UnitStatusViewModel status)
    {
        var tab = new UnitStatusTabViewModel(status, TrackKind, Loc.Get("tab_track")) { IsAvailable = false };
        status.Tabs.Add(tab);
        return new State { Tab = tab };
    }

    // ---- which contact the tab follows ------------------------------------------------------------------------------

    /// <summary>The selected contact when it is not the player's side; null for own units and anything else.</summary>
    private static Vehicle SelectedContact()
    {
        var vehicle = Globals._mainGameViewModel?.SelectedObject?.Value as Vehicle;
        return vehicle != null && vehicle.CurrentRelationship() != RelationsState.Friendly ? vehicle : null;
    }

    private static void Switch(UnitStatusViewModel status, State state)
    {
        bool noUnit = unitField.GetValue(status) == null;
        Vehicle contact = noUnit && SmartSettings.TrackState.Value ? SelectedContact() : null;
        if (contact != null)
        {
            bool changed = state.Vehicle != contact;
            if (changed)
                Drop(state);
            state.Vehicle = contact;
            if (!status.HasUnit)
            {
                // Nothing of the player's is selected: the window shows only this tab.
                state.TookOver = true;
                setHasUnit.Invoke(status, new object[] { true });
                status.EngagementTab.IsSelected = false;
                status.EngagementTab.IsAvailable = false;
                state.Tab.IsAvailable = true;
                state.Tab.IsSelected = true;
            }
            else
            {
                state.Tab.IsAvailable = true;
                if (state.TookOver && changed)
                    state.Tab.IsSelected = true;
            }
            return;
        }

        if (state.Vehicle == null && !state.Tab.IsAvailable)
        {
            state.TookOver = false;
            return;
        }
        bool wasSelected = state.Tab.IsSelected;
        Drop(state);
        state.Vehicle = null;
        state.Tab.IsSelected = false;
        state.Tab.IsAvailable = false;
        if (state.TookOver)
        {
            state.TookOver = false;
            if (noUnit)
            {
                setHasUnit.Invoke(status, new object[] { false });
                setUnitName.Invoke(status, new object[] { "" });
                status.EngagementTab.IsAvailable = true;
                status.EngagementTab.IsSelected = true;
            }
        }
        else if (wasSelected)
        {
            selectFirstTab.Invoke(status, null);
        }
    }

    // ---- the content -----------------------------------------------------------------------------------------------

    private static void Show(UnitStatusView view, UnitStatusViewModel status, State state)
    {
        if (state.HostFailed || VisualTreeHelper.GetParent(view) == null)
            return;
        bool wanted = state.Tab.IsSelected && state.Tab.IsAvailable && state.Vehicle != null;
        if (state.Host == null || VisualTreeHelper.GetParent(state.Host) == null)
        {
            if (!wanted)
                return;
            state.Host = StatusTabHost.Create(view, status);
            if (state.Host == null)
            {
                state.HostFailed = true;
                ModLog.Warn("Unit Status: no place for the Track State tab found (game updated?) - the tab stays empty.");
                return;
            }
        }
        if (wanted && state.Info == null && !state.Failed)
            Build(status, state);
        if (wanted && state.Body != null)
        {
            if (state.Host.Child != state.Body)
                state.Host.Child = state.Body;
            state.Host.Visibility = Visibility.Visible;
        }
        else if (state.Host.Visibility != Visibility.Collapsed)
        {
            state.Host.Visibility = Visibility.Collapsed;
        }
    }

    private static void Build(UnitStatusViewModel status, State state)
    {
        try
        {
            var info = new VehicleInfoViewModel(state.Vehicle);
            if (!(GUI.ParseXaml(BodyXaml) is FrameworkElement body))
                throw new InvalidOperationException("the Track State body was not made");
            if (body.FindName("ResetBtn") is Button reset)
                reset.Content = GameText("windowsvehicleinforesettma", "Reset TMA");
            body.DataContext = info;
            state.Info = info;
            state.Body = body;
            if (state.TookOver && !string.IsNullOrEmpty(info.Name))
                setUnitName.Invoke(status, new object[] { info.Name });
            ModLog.Debug($"track state: {info.Name} shown in Unit Status");
        }
        catch (Exception ex)
        {
            ModLog.Debug($"track state: {ex}");
            state.Failed = true;
        }
    }

    /// <summary>Removes the shown content and releases the contact's view model (it links itself into the contact's entity).</summary>
    private static void Drop(State state)
    {
        if (state.Host != null)
            state.Host.Child = null;
        state.Body = null;
        VehicleInfoViewModel info = state.Info;
        state.Info = null;
        state.Failed = false;
        try
        {
            info?.Dispose();
        }
        catch (Exception ex)
        {
            ModLog.Debug($"track state dispose: {ex.Message}");
        }
    }

    private static string GameText(string key, string fallback)
    {
        if (!Singleton<LanguageResourceHandler>.InstanceExists())
            return fallback;
        var dictionary = Singleton<LanguageResourceHandler>.Instance.LanguageDictionary;
        return dictionary != null && dictionary.ContainsKey(key) ? dictionary[key] : fallback;
    }
}
