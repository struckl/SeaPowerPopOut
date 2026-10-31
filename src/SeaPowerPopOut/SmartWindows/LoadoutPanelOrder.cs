using System;
using System.Collections.Generic;
using HarmonyLib;
using Noesis;
using SeaPower;
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
    private const float Every = 0.4f;

    private static readonly List<WeakReference<GameWindow>> Windows = new List<WeakReference<GameWindow>>();
    private static float next;

    public static bool Enabled { get; private set; }

    private static Func<string, string, int> yearOf;

    public static void Apply()
    {
        // LinearCampaignTaskForceUnitLookup is internal to the game assembly.
        var method = AccessTools.Method("SeapowerUI.ViewModels.LinearCampaignTaskForceUnitLookup:GetAuthoredClassServiceYear");
        if (method == null)
        {
            ModLog.Warn("Task Force Builder year lookup not found (game updated?) - the loadout panel keeps the game's order.");
            return;
        }
        yearOf = (Func<string, string, int>)Delegate.CreateDelegate(typeof(Func<string, string, int>), method);
        Enabled = true;
    }

    private static int YearOf(string folder, string type) => yearOf(folder, type);

    public static void WindowLoaded(GameWindow window)
    {
        if (Enabled)
            Windows.Add(new WeakReference<GameWindow>(window));
    }

    public static void Update()
    {
        if (!Enabled || Windows.Count == 0 || Time.unscaledTime < next)
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
        ItemsControl versions = FindItems(window, tab.VersionOptions);
        if (loadouts == null || versions == null || !(VisualTreeHelper.GetParent(loadouts) is Panel panel))
            return;

        // The versions list may sit deeper than the loadouts list: take its ancestor that is a child of the same panel.
        DependencyObject versionsBlock = versions;
        while (versionsBlock != null && VisualTreeHelper.GetParent(versionsBlock) != panel)
            versionsBlock = VisualTreeHelper.GetParent(versionsBlock);
        if (!(versionsBlock is UIElement versionsElement))
            return;

        int at = panel.Children.IndexOf(loadouts);
        int versionsAt = panel.Children.IndexOf(versionsElement);
        if (at < 0 || versionsAt < 0)
            return;

        bool ownFirst = OwnVersionFirst(tab, item.UnitFolder, item.UnitType);
        if (ownFirst == at < versionsAt)
            return;
        panel.Children.RemoveAt(at);
        panel.Children.Insert(versionsAt, loadouts);
        ModLog.Debug(ownFirst ? "loadouts: own versions shown first" : "loadouts: other versions shown first");
    }

    /// <summary>True when no other version of the class is older than the selected unit (those without a year do not count).</summary>
    private static bool OwnVersionFirst(LinearCampaignTaskForceLoadoutsViewModel tab, string folder, string type)
    {
        int own = YearOf(folder, type);
        if (own <= 0)
            return false;
        int oldestOther = int.MaxValue;
        foreach (var row in tab.VersionOptions)
        {
            int year = YearOf(folder, row.UnitType);
            if (!string.Equals(row.UnitType, type, StringComparison.OrdinalIgnoreCase) && year > 0 && year < oldestOther)
                oldestOther = year;
        }
        return own <= oldestOther;
    }

    private static ItemsControl FindItems(DependencyObject root, object source)
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        for (int budget = 20000; stack.Count > 0 && budget > 0; budget--)
        {
            DependencyObject node = stack.Pop();
            if (node is ItemsControl control && control.ItemsSource == source)
                return control;
            for (int i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--)
                stack.Push(VisualTreeHelper.GetChild(node, i));
        }
        return null;
    }
}
