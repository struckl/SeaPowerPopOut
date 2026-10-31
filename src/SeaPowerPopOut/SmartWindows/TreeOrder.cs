using System;
using System.Collections.ObjectModel;
using System.Reflection;
using HarmonyLib;
using SeaPower;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Unit Reference tree: the classes of each unit type (the folders under Vessels, Aircraft ...) are sorted by their
/// service entry date, units without a date last; equal dates by name. The game lists them in file order.
/// </summary>
internal static class TreeOrder
{
    private static readonly string[] UnitCategories = { "Vessels", "Submarines", "Aircrafts", "Helicopters", "LandUnits", "Biologic" };

    public static bool Apply(Harmony harmony)
    {
        var setter = AccessTools.PropertySetter(typeof(UnitTreeBrowserViewModel), nameof(UnitTreeBrowserViewModel.MuseumEntries));
        ConstructorInfo constructor = AccessTools.FirstConstructor(typeof(UnitTreeBrowserViewModel),
            c => c.GetParameters().Length > 0 && c.GetParameters()[0].ParameterType == typeof(string));
        if (setter == null || constructor == null)
        {
            ModLog.Warn("Unit Reference tree entries not found (game updated?) - the tree stays in the game's order.");
            return false;
        }
        // The constructor fills the tree before anyone knows which window the browser is for, so it is sorted once the
        // constructor is done; later refills (the date filter, a mod reload) go through the setter.
        harmony.Patch(constructor, postfix: new HarmonyMethod(typeof(TreeOrder), nameof(Constructed)));
        harmony.Patch(setter, postfix: new HarmonyMethod(typeof(TreeOrder), nameof(EntriesSet)));
        return true;
    }

    private static void Constructed(UnitTreeBrowserViewModel __instance, string stateKey)
    {
        if (stateKey == BrowserMemory.EncyclopediaKey)
            Sort(__instance);
    }

    private static void EntriesSet(UnitTreeBrowserViewModel __instance)
    {
        if (BrowserMemory.IsEncyclopedia(__instance))
            Sort(__instance);
    }

    private static void Sort(UnitTreeBrowserViewModel browser)
    {
        if (browser.MuseumEntries == null)
            return;
        try
        {
            int sorted = 0;
            foreach (MuseumEntry category in browser.MuseumEntries)
            {
                if (Array.IndexOf(UnitCategories, category?.CategoryKey) < 0 || category.MuseumEntries == null)
                    continue;
                foreach (MuseumEntry folder in category.MuseumEntries)
                {
                    // A folder is a plain MuseumEntry; units and loadouts are subclasses and are left alone.
                    if (folder != null && folder.GetType() == typeof(MuseumEntry) && folder.MuseumEntries != null
                        && SortByDate(folder.MuseumEntries))
                        sorted++;
                }
            }
            ModLog.Debug($"smart windows: {sorted} unit type(s) sorted by date");
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Unit Reference tree not sorted: {ex.Message}");
        }
    }

    private static bool SortByDate(ObservableCollection<MuseumEntry> entries) =>
        StableOrder.Sort(entries, CompareByDate, entries.Move);

    private static int CompareByDate(MuseumEntry a, MuseumEntry b)
    {
        int byDate = DateKey(a).CompareTo(DateKey(b));
        return byDate != 0 ? byDate : string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The service entry year; units without one sort after all dated ones.</summary>
    private static int DateKey(MuseumEntry entry) => entry.ServiceEntryDate > 0 ? entry.ServiceEntryDate : int.MaxValue;
}
