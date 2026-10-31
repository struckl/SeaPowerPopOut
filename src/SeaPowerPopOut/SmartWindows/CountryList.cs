using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SeaPower;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Unit Reference: the country list only offers countries that have units for the current search, date filter and
/// "Armed only" (the game always lists them all). The full list is taken once the game has built it.
/// </summary>
internal static class CountryList
{
    private const int MaxDepth = 12;

    private static readonly ConditionalWeakTable<UnitTreeBrowserViewModel, List<EncyclopediaNation>> Masters =
        new ConditionalWeakTable<UnitTreeBrowserViewModel, List<EncyclopediaNation>>();

    private static MethodInfo matchUnit, matchWeapon, matchSensor;

    public static bool Apply(Harmony harmony)
    {
        MethodInfo setup = AccessTools.Method(typeof(UnitTreeBrowserViewModel), "SetupNations");
        matchUnit = AccessTools.Method(typeof(UnitTreeBrowserViewModel), "MatchesTextSearch", new[] { typeof(MuseumObjectEntry), typeof(string), typeof(bool) });
        matchWeapon = AccessTools.Method(typeof(UnitTreeBrowserViewModel), "MatchesWeaponTextSearch", new[] { typeof(MuseumWeaponEntry), typeof(string) });
        matchSensor = AccessTools.Method(typeof(UnitTreeBrowserViewModel), "MatchesSensorTextSearch", new[] { typeof(MuseumSensorEntry), typeof(string) });
        if (setup == null || matchUnit == null || matchWeapon == null || matchSensor == null)
        {
            ModLog.Warn("Unit Reference country list members not found (game updated?) - all countries stay listed.");
            return false;
        }
        harmony.Patch(setup, postfix: new HarmonyMethod(typeof(CountryList), nameof(NationsBuilt)));
        return true;
    }

    private static void NationsBuilt(UnitTreeBrowserViewModel __instance)
    {
        Masters.Remove(__instance);
        Masters.Add(__instance, new List<EncyclopediaNation>(__instance.Nations));
    }

    public static void Sync(UnitTreeBrowserViewModel browser)
    {
        try
        {
            if (!Masters.TryGetValue(browser, out List<EncyclopediaNation> all) || all.Count == 0)
                return;
            string text = (browser.SearchText ?? "").Trim();
            bool needed = text.Length > 0 || browser.FilterByDate || SmartSettings.ArmedOnly.Value;

            HashSet<string> present = null;
            if (needed)
            {
                present = new HashSet<string>();
                Collect(browser, browser.MuseumEntries, text.ToLowerInvariant(), present, UnitTreeBrowserViewModel.GetCurrentMissionYear(), 0);
            }
            NationSync.Sync(browser.Nations, all, n => n.NationId, browser.SelectedNation, present);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"country list: {ex.Message}");
        }
    }

    /// <summary>Adds the countries of every unit (and weapon, sensor ...) that passes the filters, folders entered.</summary>
    private static void Collect(UnitTreeBrowserViewModel browser, IEnumerable level, string text, ISet<string> present, int year, int depth)
    {
        if (level == null || depth > MaxDepth)
            return;
        foreach (MuseumEntry entry in level)
        {
            if (entry == null)
                continue;
            if (entry.MuseumEntries != null && entry.MuseumEntries.Count > 0)
                Collect(browser, entry.MuseumEntries, text, present, year, depth + 1);
            if (entry.GetType() == typeof(MuseumEntry))
                continue;                                  // a folder
            if (browser.FilterByDate && !ServiceCovers(entry, year))
                continue;
            if ((text.Length == 0 || MatchesText(browser, entry, text)) && (!SmartSettings.ArmedOnly.Value || ArmedFilter.IsArmedEntry(entry)))
                AddNations(entry, present);
        }
    }

    private static bool ServiceCovers(MuseumEntry entry, int year) =>
        entry.ServiceEntryDate <= 0 || (entry.ServiceEntryDate <= year && (entry.ServiceExitDate <= 0 || entry.ServiceExitDate >= year));

    private static bool MatchesText(UnitTreeBrowserViewModel browser, MuseumEntry entry, string text)
    {
        try
        {
            switch (entry)
            {
                case MuseumObjectEntry unit:
                    return (bool)matchUnit.Invoke(browser, new object[] { unit, text, browser.SearchByNameOnly });
                case MuseumWeaponEntry weapon:
                    return (bool)matchWeapon.Invoke(browser, new object[] { weapon, text });
                case MuseumSensorEntry sensor:
                    return (bool)matchSensor.Invoke(browser, new object[] { sensor, text });
            }
        }
        catch (Exception)
        {
            return true;                                   // when in doubt keep the country
        }
        return Contains(entry.DisplayName, text) || Contains(AccessTools.Property(entry.GetType(), "LongName")?.GetValue(entry) as string, text);
    }

    private static bool Contains(string value, string lower) => value != null && value.ToLowerInvariant().Contains(lower);

    private static void AddNations(MuseumEntry entry, ISet<string> into)
    {
        if (entry is MuseumObjectEntry unit && !string.IsNullOrEmpty(unit.Nation))
            into.Add(unit.Nation);
        if (AccessTools.Property(entry.GetType(), "Nations")?.GetValue(entry) is IEnumerable nations && !(nations is string))
        {
            foreach (object nation in nations)
            {
                if (nation is string id && id.Length > 0)
                    into.Add(id);
            }
        }
        if (entry.IniRef != null && entry.IniRef.StartsWith("civ_", StringComparison.OrdinalIgnoreCase))
            into.Add("Civilian");
    }
}
