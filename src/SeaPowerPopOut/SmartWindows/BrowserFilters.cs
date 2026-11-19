using System.Reflection;
using HarmonyLib;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// The one hook on the game's filter update of the Unit Reference browser. After the game has filtered the tree, the
/// "Armed only" filter runs first and the country list is brought in step with what is left.
/// </summary>
internal static class BrowserFilters
{
    public static readonly Feature Feature = new Feature
    {
        Hook = harmony => Encyclopedia.Feature.Active && Apply(harmony),
        Hooked = "Unit Reference filters ('Armed only', country list) active",
    };

    private static bool Apply(Harmony harmony)
    {
        MethodInfo update = Encyclopedia.UpdateFiltered;
        if (update == null || !ArmedFilter.Resolve())
        {
            ModLog.Warn("Unit Reference filter update not found (game updated?) - 'Armed only' and the country list stay as the game has them.");
            return false;
        }
        harmony.Patch(update, postfix: new HarmonyMethod(typeof(BrowserFilters), nameof(Updated)));
        return true;
    }

    private static void Updated(UnitTreeBrowserViewModel __instance)
    {
        if (Encyclopedia.IsInitializing(__instance) || !Encyclopedia.Is(__instance))
            return;
        if (SmartSettings.ArmedOnly.Value)
            ArmedFilter.Filter(__instance);
        if (SmartSettings.HideEmptyCountries.Value && CountryList.Feature.Active)
            CountryList.Sync(__instance);
    }
}
