using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SeaPower;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Tells the Unit Reference window's unit browser from the mission editor's (both are a
/// <see cref="UnitTreeBrowserViewModel"/>): the game builds the encyclopedia's with the state key "Encyclopedia". Every
/// Unit Reference feature asks here, so it hooks the browser's constructor before them (see <see cref="SmartFeatures"/>).
/// Also the browser members several of them call.
/// </summary>
internal static class Encyclopedia
{
    public const string Key = "Encyclopedia";

    public static readonly Feature Feature = new Feature { Hook = Apply };

    public static readonly ConstructorInfo BrowserConstructor = AccessTools.FirstConstructor(typeof(UnitTreeBrowserViewModel),
        c => c.GetParameters().Length > 0 && c.GetParameters()[0].ParameterType == typeof(string));

    /// <summary>The game's filter update of the tree (date, country, search).</summary>
    public static readonly MethodInfo UpdateFiltered =
        AccessTools.Method(typeof(UnitTreeBrowserViewModel), "UpdateFilteredMuseumEntries", Type.EmptyTypes);

    /// <summary>The game's text match of one unit (name, class, weapons, sensors, aircraft).</summary>
    public static readonly MethodInfo MatchesText = AccessTools.Method(typeof(UnitTreeBrowserViewModel), "MatchesTextSearch",
        new[] { typeof(MuseumObjectEntry), typeof(string), typeof(bool) });

    private static readonly FieldInfo Initializing = AccessTools.Field(typeof(UnitTreeBrowserViewModel), "_isInitializing");

    private static readonly ConditionalWeakTable<UnitTreeBrowserViewModel, object> Ours =
        new ConditionalWeakTable<UnitTreeBrowserViewModel, object>();

    private static readonly List<WeakReference<UnitTreeBrowserViewModel>> Live = new List<WeakReference<UnitTreeBrowserViewModel>>();

    private static bool Apply(Harmony harmony)
    {
        if (BrowserConstructor == null || Initializing == null)
        {
            Rig.Log.Warn("Unit Reference browser members not found (game updated?) - its choices are not remembered.");
            return false;
        }
        harmony.Patch(BrowserConstructor, postfix: new HarmonyMethod(typeof(Encyclopedia), nameof(Constructed)));
        return true;
    }

    private static void Constructed(UnitTreeBrowserViewModel __instance, string stateKey)
    {
        if (stateKey != Key)
            return;
        Ours.Add(__instance, stateKey);
        Live.Add(new WeakReference<UnitTreeBrowserViewModel>(__instance));
    }

    /// <summary>True for the Unit Reference window's browser (not the mission editor's).</summary>
    public static bool Is(UnitTreeBrowserViewModel browser) => Ours.TryGetValue(browser, out _);

    /// <summary>True while the browser is still setting itself up (its choices must not be saved or filtered yet).</summary>
    public static bool IsInitializing(UnitTreeBrowserViewModel browser) => (bool)Initializing.GetValue(browser);

    /// <summary>The Unit Reference browsers that are still alive.</summary>
    public static IEnumerable<UnitTreeBrowserViewModel> LiveBrowsers()
    {
        for (int i = Live.Count - 1; i >= 0; i--)
        {
            if (Live[i].TryGetTarget(out UnitTreeBrowserViewModel browser))
                yield return browser;
            else
                Live.RemoveAt(i);
        }
    }

    /// <summary>
    /// The unit-type folders of the tree (the folders under Vessels, Aircraft ...). A folder is a plain
    /// <see cref="MuseumEntry"/>; units and loadouts are subclasses.
    /// </summary>
    public static IEnumerable<MuseumEntry> UnitFolders(UnitTreeBrowserViewModel browser)
    {
        if (browser.MuseumEntries == null)
            yield break;
        foreach (MuseumEntry category in browser.MuseumEntries)
        {
            if (Array.IndexOf(UnitCategories, category?.CategoryKey) < 0 || category.MuseumEntries == null)
                continue;
            foreach (MuseumEntry folder in category.MuseumEntries)
            {
                if (folder != null && folder.GetType() == typeof(MuseumEntry) && folder.MuseumEntries != null)
                    yield return folder;
            }
        }
    }

    /// <summary>
    /// The entries from <paramref name="level"/> down to <paramref name="target"/> (both ends included) into
    /// <paramref name="path"/>, following <paramref name="children"/> (the whole tree or what the filters left).
    /// </summary>
    public static bool PathTo(IEnumerable<MuseumEntry> level, MuseumEntry target, List<MuseumEntry> path,
        Func<MuseumEntry, IEnumerable<MuseumEntry>> children)
    {
        if (level == null)
            return false;
        foreach (MuseumEntry entry in level)
        {
            if (entry == null)
                continue;
            path.Add(entry);
            if (entry == target || PathTo(children(entry), target, path, children))
                return true;
            path.RemoveAt(path.Count - 1);
        }
        return false;
    }

    private static readonly string[] UnitCategories = { "Vessels", "Submarines", "Aircrafts", "Helicopters", "LandUnits", "Biologic" };
}
