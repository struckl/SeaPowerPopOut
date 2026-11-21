using System;
using System.Collections.ObjectModel;
using SeaPower;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Unit Reference tree: the classes of each unit type (the folders under Vessels, Aircraft ...) are sorted by their
/// service entry date, units without a date last; equal dates by name. The game lists them in file order.
/// </summary>
internal static class TreeOrder
{
    public static void Sort(UnitTreeBrowserViewModel browser)
    {
        try
        {
            int sorted = 0;
            foreach (MuseumEntry folder in Encyclopedia.UnitFolders(browser))
            {
                if (SortByDate(folder.MuseumEntries))
                    sorted++;
            }
            Rig.Log.Debug($"smart windows: {sorted} unit type(s) sorted by date");
        }
        catch (Exception ex)
        {
            Rig.Log.Warn($"Unit Reference tree not sorted: {ex.Message}");
        }
    }

    private static bool SortByDate(ObservableCollection<MuseumEntry> entries) =>
        StableOrder.Sort(entries, CompareByDate, entries.Move);

    private static int CompareByDate(MuseumEntry a, MuseumEntry b)
    {
        int byDate = ServiceYear.SortKey(a.ServiceEntryDate).CompareTo(ServiceYear.SortKey(b.ServiceEntryDate));
        return byDate != 0 ? byDate : string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
    }
}
