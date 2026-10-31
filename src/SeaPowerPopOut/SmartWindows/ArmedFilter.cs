using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SeaPower;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Unit Reference "Armed only": leaves out units without weapons (rafts, targets, tankers, civilian ships ...). Decoys,
/// sonobuoys, fuel tanks and the like do not count as weapons. Switched in the gear panel.
/// </summary>
internal static class ArmedFilter
{
    // Ammunition types that are not weapons.
    private static readonly string[] NotWeapons = { "Noisemaker", "Chaff", "None", "Sonobuoy", "Fueltank", "Paratrooper", "MOSS" };

    private static MethodInfo updateFiltered, saveExpansion, isSearching, setFiltered;

    /// <summary>Looks up the game members used; false if one is missing (the shared hook is then not installed).</summary>
    public static bool Resolve()
    {
        updateFiltered = AccessTools.Method(typeof(UnitTreeBrowserViewModel), "UpdateFilteredMuseumEntries", Type.EmptyTypes);
        saveExpansion = AccessTools.Method(typeof(UnitTreeBrowserViewModel), "SaveExpansionState");
        isSearching = AccessTools.PropertyGetter(typeof(UnitTreeBrowserViewModel), "IsSearching");
        setFiltered = AccessTools.PropertySetter(typeof(UnitTreeBrowserViewModel), nameof(UnitTreeBrowserViewModel.FilteredMuseumEntries));
        if (updateFiltered == null || isSearching == null || setFiltered == null)
        {
            ModLog.Warn("Unit Reference filter update not found (game updated?) - 'Armed only' is not available.");
            return false;
        }
        return true;
    }

    /// <summary>Switches the filter and refreshes the tree of every open Unit Reference window.</summary>
    public static void SetEnabled(bool on)
    {
        SmartSettings.ArmedOnly.Value = on;
        if (updateFiltered == null)
            return;
        foreach (UnitTreeBrowserViewModel browser in BrowserMemory.LiveBrowsers())
        {
            try
            {
                if (saveExpansion != null && !(bool)isSearching.Invoke(browser, null))
                    saveExpansion.Invoke(browser, new object[] { browser.FilteredMuseumEntries });
                updateFiltered.Invoke(browser, null);
            }
            catch (Exception ex)
            {
                ModLog.Warn($"Unit Reference not refreshed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }
    }

    /// <summary>Leaves the unarmed units out of the browser's filtered tree (called after the game filtered it).</summary>
    public static void Filter(UnitTreeBrowserViewModel browser)
    {
        if (browser.FilteredMuseumEntries == null)
            return;
        try
        {
            var kept = new ObservableCollection<MuseumEntry>(browser.FilteredMuseumEntries.Where(Keep));
            setFiltered.Invoke(browser, new object[] { kept });
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Unit Reference 'Armed only' failed: {ex.Message}");
        }
    }

    /// <summary>Folders keep their armed contents (and disappear when nothing is left); units are kept if they are armed.</summary>
    private static bool Keep(MuseumEntry entry)
    {
        if (entry == null)
            return false;
        if (entry.GetType() != typeof(MuseumEntry))
            return IsArmed(entry);

        ObservableCollection<MuseumEntry> children = entry.FilteredMuseumEntries;
        if (children == null || children.Count == 0)
            return true;
        var kept = new ObservableCollection<MuseumEntry>(children.Where(Keep));
        if (kept.Count == children.Count)
            return true;
        entry.FilteredMuseumEntries = kept;               // a folder's own list is public to set
        return kept.Count > 0;
    }

    /// <summary>True for a unit with at least one weapon, and for everything that is not a unit.</summary>
    internal static bool IsArmedEntry(MuseumEntry entry) => IsArmed(entry);

    private static bool IsArmed(MuseumEntry entry)
    {
        if (!(entry is MuseumObjectEntry unit))
            return true;                      // weapons, sensors and the like are not units
        return unit.WeaponEntries != null && unit.WeaponEntries.Any(w => w != null && IsWeaponType(w.Type.ToString()));
    }

    /// <summary>True unless the ammunition type is one of the non-weapons.</summary>
    internal static bool IsWeaponType(string ammunitionType) => Array.IndexOf(NotWeapons, ammunitionType) < 0;
}
