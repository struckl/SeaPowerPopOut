using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SeaPower;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Unit Reference opened for a unit (from the unit's context menu): the unit stays in the tree, with its class, even
/// when the country, date, search or "Armed only" filters would hide it. The game rebuilds the filtered tree on every
/// filter change, so the unit is put back after each rebuild.
/// </summary>
internal static class KeepOpenedUnit
{
    private static readonly ConditionalWeakTable<UnitTreeBrowserViewModel, MuseumEntry> Pinned =
        new ConditionalWeakTable<UnitTreeBrowserViewModel, MuseumEntry>();

    /// <summary>True while the game builds an information view model for one given unit.</summary>
    private static bool openingForUnit;

    public static bool Apply(Harmony harmony)
    {
        MethodInfo reveal = AccessTools.Method(typeof(UnitTreeBrowserViewModel), nameof(UnitTreeBrowserViewModel.RevealEntry));
        MethodInfo update = AccessTools.Method(typeof(UnitTreeBrowserViewModel), "UpdateFilteredMuseumEntries", Type.EmptyTypes);
        ConstructorInfo opening = AccessTools.FirstConstructor(typeof(ObjectInformationViewModel),
            c => c.GetParameters().Length >= 2 && c.GetParameters()[1].Name == "initialIniRef");
        if (reveal == null || update == null || opening == null)
        {
            ModLog.Warn("Unit Reference tree members not found (game updated?) - the opened unit is not kept in the tree.");
            return false;
        }

        harmony.Patch(opening, prefix: new HarmonyMethod(typeof(KeepOpenedUnit), nameof(OpeningPrefix)),
            finalizer: new HarmonyMethod(typeof(KeepOpenedUnit), nameof(OpeningFinalizer)));
        harmony.Patch(reveal, postfix: new HarmonyMethod(typeof(KeepOpenedUnit), nameof(Revealed)));
        // Last, so the unit is also put back after 'Armed only' and the other filters of this mod have run.
        harmony.Patch(update, postfix: new HarmonyMethod(typeof(KeepOpenedUnit), nameof(Updated) ) { priority = Priority.Last });
        return true;
    }

    private static void OpeningPrefix(string initialIniRef) => openingForUnit = initialIniRef != null;

    private static void OpeningFinalizer() => openingForUnit = false;

    private static void Revealed(UnitTreeBrowserViewModel __instance, MuseumEntry __result)
    {
        if (!openingForUnit || __result == null || !SmartSettings.KeepOpenedUnit.Value || !BrowserMemory.IsEncyclopedia(__instance))
            return;
        try
        {
            Pinned.Remove(__instance);
            Pinned.Add(__instance, __result);
            Inject(__instance);
        }
        catch (Exception ex)
        {
            ModLog.Debug("keep opened unit: " + ex.Message);
        }
    }

    private static void Updated(UnitTreeBrowserViewModel __instance)
    {
        if (!SmartSettings.KeepOpenedUnit.Value || BrowserMemory.IsInitializing(__instance))
            return;
        try
        {
            Inject(__instance);
        }
        catch (Exception ex)
        {
            ModLog.Debug("keep opened unit: " + ex.Message);
        }
    }

    /// <summary>Makes the pinned entry and every class above it visible in the filtered tree.</summary>
    private static void Inject(UnitTreeBrowserViewModel browser)
    {
        if (!Pinned.TryGetValue(browser, out MuseumEntry pinned) || browser.MuseumEntries == null || browser.FilteredMuseumEntries == null)
            return;
        var path = new List<MuseumEntry>();
        if (!PathTo(browser.MuseumEntries, pinned, path))
            return;

        ObservableCollection<MuseumEntry> visible = browser.FilteredMuseumEntries;
        ObservableCollection<MuseumEntry> all = browser.MuseumEntries;
        bool narrowed = false;      // the levels below are lists made here, holding just the path
        for (int i = 0; i < path.Count; i++)
        {
            MuseumEntry node = path[i];
            bool added = EnsureIn(visible, all, node);
            if (i == path.Count - 1)
                break;
            ObservableCollection<MuseumEntry> children = node.MuseumEntries;
            if (added || narrowed)
            {
                var only = new ObservableCollection<MuseumEntry> { path[i + 1] };
                node.FilteredMuseumEntries = only;
                visible = only;
                narrowed = true;
            }
            else
            {
                // Unfiltered below this node: everything is shown already.
                if (node.FilteredMuseumEntries == null || node.FilteredMuseumEntries == children)
                    return;
                visible = node.FilteredMuseumEntries;
            }
            all = children;
        }
    }

    private static bool PathTo(IEnumerable<MuseumEntry> level, MuseumEntry target, List<MuseumEntry> path)
    {
        if (level == null)
            return false;
        foreach (MuseumEntry entry in level)
        {
            if (entry == null)
                continue;
            path.Add(entry);
            if (entry == target || PathTo(entry.MuseumEntries, target, path))
                return true;
            path.RemoveAt(path.Count - 1);
        }
        return false;
    }

    /// <summary>Adds the entry to the list at the place its position in the complete list gives it. False if it was there.</summary>
    private static bool EnsureIn(ObservableCollection<MuseumEntry> list, ObservableCollection<MuseumEntry> all, MuseumEntry entry)
    {
        if (list.Contains(entry))
            return false;
        int rank = all.IndexOf(entry);
        int index = list.Count;
        for (int i = 0; i < list.Count; i++)
        {
            if (all.IndexOf(list[i]) > rank)
            {
                index = i;
                break;
            }
        }
        list.Insert(index, entry);
        return true;
    }
}
