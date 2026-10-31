using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Remembers the choices in the Unit Reference (encyclopedia) window between game runs: country, category, the
/// date filter, the name-only switch and the search text.
/// <para>The game already keeps country, category and date filter in a per-session table keyed by the browser's
/// state key. That table is seeded from the config when the encyclopedia's browser is created, and the config is
/// updated whenever one of the choices changes. The search text and name-only switch are not part of the game's
/// state, so they are set again once the browser has finished initializing.</para>
/// </summary>
internal static class BrowserMemory
{
    public const string EncyclopediaKey = "Encyclopedia";

    private static readonly ConditionalWeakTable<UnitTreeBrowserViewModel, string> Keys =
        new ConditionalWeakTable<UnitTreeBrowserViewModel, string>();

    private static readonly List<WeakReference<UnitTreeBrowserViewModel>> Live = new List<WeakReference<UnitTreeBrowserViewModel>>();

    private static FieldInfo stateField, initializingField;
    private static FieldInfo nationField, dateField, categoryField;

    /// <summary>Resolves the members and hooks them. Returns false (and hooks nothing) if the game changed.</summary>
    public static bool Apply(Harmony harmony)
    {
        Type browser = typeof(UnitTreeBrowserViewModel);
        stateField = AccessTools.Field(browser, "_state");
        initializingField = AccessTools.Field(browser, "_isInitializing");
        Type state = stateField?.FieldType;
        nationField = state == null ? null : AccessTools.Field(state, "LastNationId");
        dateField = state == null ? null : AccessTools.Field(state, "LastFilterByDate");
        categoryField = state == null ? null : AccessTools.Field(state, "LastCategoryName");

        ConstructorInfo constructor = AccessTools.FirstConstructor(browser, c => c.GetParameters().Length > 0
            && c.GetParameters()[0].ParameterType == typeof(string));
        MethodInfo finish = AccessTools.Method(browser, nameof(UnitTreeBrowserViewModel.FinishInitialization));
        string[] changing = { "SelectedNation", "SelectedCategory", "FilterByDate", "SearchByNameOnly", "SearchText" };

        var setters = new MethodBase[changing.Length];
        for (int i = 0; i < changing.Length; i++)
            setters[i] = AccessTools.PropertySetter(browser, changing[i]);

        if (stateField == null || initializingField == null || nationField == null || dateField == null
            || categoryField == null || constructor == null || finish == null || Array.IndexOf(setters, null) >= 0)
        {
            ModLog.Warn("Unit Reference browser members not found (game updated?) - its choices are not remembered.");
            return false;
        }

        harmony.Patch(constructor, postfix: new HarmonyMethod(typeof(BrowserMemory), nameof(Constructed)));
        harmony.Patch(finish, postfix: new HarmonyMethod(typeof(BrowserMemory), nameof(Finished)));
        foreach (MethodBase setter in setters)
            harmony.Patch(setter, postfix: new HarmonyMethod(typeof(BrowserMemory), nameof(Changed)));
        return true;
    }

    private static void Constructed(UnitTreeBrowserViewModel __instance, string stateKey)
    {
        if (stateKey != EncyclopediaKey)
            return;
        Keys.Add(__instance, stateKey);
        Live.Add(new WeakReference<UnitTreeBrowserViewModel>(__instance));
        if (!SmartSettings.RememberBrowser.Value)
            return;

        // A browser built earlier this run already has the player's latest choices; only a fresh table is seeded.
        object state = stateField.GetValue(__instance);
        if (!string.IsNullOrEmpty((string)nationField.GetValue(state)) || !string.IsNullOrEmpty((string)categoryField.GetValue(state)))
            return;
        SetIfNotEmpty(nationField, state, SmartSettings.BrowserCountry.Value);
        SetIfNotEmpty(categoryField, state, SmartSettings.BrowserCategory.Value);
        dateField.SetValue(state, SmartSettings.BrowserByDate.Value);
    }

    private static void Finished(UnitTreeBrowserViewModel __instance)
    {
        if (!Remembered(__instance))
            return;
        __instance.SearchByNameOnly = SmartSettings.BrowserNameOnly.Value;
        if (!string.IsNullOrEmpty(SmartSettings.BrowserSearch.Value))
            __instance.SearchText = SmartSettings.BrowserSearch.Value;
    }

    private static void Changed(UnitTreeBrowserViewModel __instance)
    {
        if (!Remembered(__instance) || (bool)initializingField.GetValue(__instance))
            return;
        SmartSettings.BrowserCountry.Value = __instance.SelectedNation?.NationId ?? "";
        SmartSettings.BrowserCategory.Value = __instance.SelectedCategory?.DisplayName ?? "";
        SmartSettings.BrowserByDate.Value = __instance.FilterByDate;
        SmartSettings.BrowserNameOnly.Value = __instance.SearchByNameOnly;
        SmartSettings.BrowserSearch.Value = __instance.SearchText ?? "";
    }

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

    /// <summary>True while the browser is still setting itself up (its choices must not be saved or filtered yet).</summary>
    public static bool IsInitializing(UnitTreeBrowserViewModel browser) => (bool)initializingField.GetValue(browser);

    /// <summary>True for the Unit Reference window's browser (not the mission editor's).</summary>
    public static bool IsEncyclopedia(UnitTreeBrowserViewModel browser) => Keys.TryGetValue(browser, out _);

    private static bool Remembered(UnitTreeBrowserViewModel browser) =>
        SmartSettings.RememberBrowser.Value && Keys.TryGetValue(browser, out _);

    private static void SetIfNotEmpty(FieldInfo field, object target, string value)
    {
        if (!string.IsNullOrEmpty(value))
            field.SetValue(target, value);
    }
}
