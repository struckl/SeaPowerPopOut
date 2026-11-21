using System;
using System.Reflection;
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
    public static readonly Feature Feature = new Feature { Hook = harmony => Encyclopedia.Feature.Active && Apply(harmony) };

    private static FieldInfo stateField;
    private static FieldInfo nationField, dateField, categoryField;

    /// <summary>Resolves the members and hooks them. Returns false (and hooks nothing) if the game changed.</summary>
    private static bool Apply(Harmony harmony)
    {
        Type browser = typeof(UnitTreeBrowserViewModel);
        stateField = AccessTools.Field(browser, "_state");
        Type state = stateField?.FieldType;
        nationField = state == null ? null : AccessTools.Field(state, "LastNationId");
        dateField = state == null ? null : AccessTools.Field(state, "LastFilterByDate");
        categoryField = state == null ? null : AccessTools.Field(state, "LastCategoryName");

        MethodInfo finish = AccessTools.Method(browser, nameof(UnitTreeBrowserViewModel.FinishInitialization));
        string[] changing = { "SelectedNation", "SelectedCategory", "FilterByDate", "SearchByNameOnly", "SearchText" };

        var setters = new MethodBase[changing.Length];
        for (int i = 0; i < changing.Length; i++)
            setters[i] = AccessTools.PropertySetter(browser, changing[i]);

        if (stateField == null || nationField == null || dateField == null || categoryField == null || finish == null
            || Array.IndexOf(setters, null) >= 0)
        {
            Rig.Log.Warn("Unit Reference browser members not found (game updated?) - its choices are not remembered.");
            return false;
        }

        harmony.Patch(Encyclopedia.BrowserConstructor, postfix: new HarmonyMethod(typeof(BrowserMemory), nameof(Constructed)));
        harmony.Patch(finish, postfix: new HarmonyMethod(typeof(BrowserMemory), nameof(Finished)));
        foreach (MethodBase setter in setters)
            harmony.Patch(setter, postfix: new HarmonyMethod(typeof(BrowserMemory), nameof(Changed)));
        return true;
    }

    private static void Constructed(UnitTreeBrowserViewModel __instance, string stateKey)
    {
        if (stateKey != Encyclopedia.Key || !SmartSettings.RememberBrowser.Value)
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
        if (!Remembered(__instance) || Encyclopedia.IsInitializing(__instance))
            return;
        SmartSettings.BrowserCountry.Value = __instance.SelectedNation?.NationId ?? "";
        SmartSettings.BrowserCategory.Value = __instance.SelectedCategory?.DisplayName ?? "";
        SmartSettings.BrowserByDate.Value = __instance.FilterByDate;
        SmartSettings.BrowserNameOnly.Value = __instance.SearchByNameOnly;
        SmartSettings.BrowserSearch.Value = __instance.SearchText ?? "";
    }

    private static bool Remembered(UnitTreeBrowserViewModel browser) =>
        SmartSettings.RememberBrowser.Value && Encyclopedia.Is(browser);

    private static void SetIfNotEmpty(FieldInfo field, object target, string value)
    {
        if (!string.IsNullOrEmpty(value))
            field.SetValue(target, value);
    }
}
