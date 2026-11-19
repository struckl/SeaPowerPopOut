using System;
using System.Collections.Generic;
using HarmonyLib;
using Noesis;
using SeapowerUI.ViewModels;
using UnityEngine;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Campaign Task Force Builder, Loadouts tab: the panel stacks the unit's own loadouts and the "other versions" list.
/// The list that matters more goes on top: when the selected unit is the oldest version of its class, its own loadouts
/// come first and the other versions below; for a later version the other versions come first. (The loadouts inside
/// the list are ordered by <see cref="CampaignLoadouts"/>, the two do not touch each other's items.)
/// <para>Both lists are ItemsControls in one panel, so the order is changed by moving the loadouts list within that
/// panel. The game rebuilds the lists when another unit is selected, so the panel is looked at again every 0.4 s.</para>
/// </summary>
internal static class LoadoutPanelOrder
{
    public static readonly Feature Feature = new Feature
    {
        Switch = () => SmartSettings.LoadoutPanelOrder.Value,
        Hook = _ => Apply(),
        Hooked = "campaign loadout panel order",
        WindowLoaded = window => Windows.Add(new WeakReference<GameWindow>(window)),
        Update = Update,
    };

    private const float Every = 0.4f;

    private static readonly List<WeakReference<GameWindow>> Windows = new List<WeakReference<GameWindow>>();
    private static float next;

    private static Func<string, string, int> yearOf;

    private static bool Apply()
    {
        // LinearCampaignTaskForceUnitLookup is internal to the game assembly.
        var method = AccessTools.Method("SeapowerUI.ViewModels.LinearCampaignTaskForceUnitLookup:GetAuthoredClassServiceYear");
        if (method == null)
        {
            ModLog.Warn("Task Force Builder year lookup not found (game updated?) - the loadout panel keeps the game's order.");
            return false;
        }
        yearOf = (Func<string, string, int>)Delegate.CreateDelegate(typeof(Func<string, string, int>), method);
        return true;
    }

    private static void Update()
    {
        if (Windows.Count == 0 || Time.unscaledTime < next)
            return;
        next = Time.unscaledTime + Every;
        for (int i = Windows.Count - 1; i >= 0; i--)
        {
            if (!Windows[i].TryGetTarget(out GameWindow window) || VisualTreeHelper.GetParent(window) == null && !window.IsLoaded)
            {
                Windows.RemoveAt(i);
                continue;
            }
            try
            {
                if (window.DataContext is LinearCampaignTaskForceBuilderViewModel builder)
                    Reorder(window, builder.LoadoutsTab);
            }
            catch (Exception ex)
            {
                ModLog.Debug($"loadout panel order: {ex.Message}");
            }
        }
    }

    private static void Reorder(GameWindow window, LinearCampaignTaskForceLoadoutsViewModel tab)
    {
        var item = tab?.SelectedUnit?.Item;
        if (item == null || tab.Options == null || tab.VersionOptions == null || tab.VersionOptions.Count == 0)
            return;
        ItemsControl loadouts = FindItems(window, tab.Options);
        if (loadouts == null || !(VisualTreeHelper.GetParent(loadouts) is Panel panel) || !(BlockIn(panel, FindItems(window, tab.VersionOptions)) is UIElement versions))
            return;

        int at = panel.Children.IndexOf(loadouts);
        int versionsAt = panel.Children.IndexOf(versions);
        if (at < 0 || versionsAt < 0)
            return;
        bool ownFirst = OwnVersionFirst(tab, item.UnitFolder, item.UnitType);
        if (ownFirst == at < versionsAt)
            return;
        panel.Children.RemoveAt(at);
        panel.Children.Insert(versionsAt, loadouts);
        ModLog.Debug(ownFirst ? "loadouts: own versions shown first" : "loadouts: other versions shown first");
    }

    /// <summary>
    /// The versions list may sit deeper than the loadouts list: its ancestor that is a child of the same panel, or null.
    /// </summary>
    private static DependencyObject BlockIn(Panel panel, DependencyObject element)
    {
        while (element != null && VisualTreeHelper.GetParent(element) != panel)
            element = VisualTreeHelper.GetParent(element);
        return element;
    }

    /// <summary>True when no other version of the class is older than the selected unit (those without a year do not count).</summary>
    private static bool OwnVersionFirst(LinearCampaignTaskForceLoadoutsViewModel tab, string folder, string type)
    {
        int own = yearOf(folder, type);
        if (own <= 0)
            return false;
        int oldestOther = int.MaxValue;
        foreach (var row in tab.VersionOptions)
        {
            int year = yearOf(folder, row.UnitType);
            if (!string.Equals(row.UnitType, type, StringComparison.OrdinalIgnoreCase) && year > 0 && year < oldestOther)
                oldestOther = year;
        }
        return own <= oldestOther;
    }

    private static ItemsControl FindItems(DependencyObject root, object source) =>
        VisualSearch.First<ItemsControl>(root, control => control.ItemsSource == source);
}
