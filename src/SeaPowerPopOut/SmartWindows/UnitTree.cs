using HarmonyLib;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// The order of the Unit Reference tree: the classes of each unit type sorted by date (<see cref="TreeOrder"/>), then the
/// loadout variants under each class labelled and grouped (<see cref="VariantGroups"/>). One hook for both, so the two
/// always run in that order.
/// <para>The constructor fills the tree before anyone knows which window the browser is for, so it is arranged once the
/// constructor is done; later refills (the date filter, a mod reload) go through the setter.</para>
/// </summary>
internal static class UnitTree
{
    public static readonly Feature Feature = new Feature
    {
        Switch = () => SmartSettings.SortTree.Value || SmartSettings.GroupVariants.Value,
        Hook = harmony => Encyclopedia.Feature.Active && Apply(harmony),
    };

    // Read once, with the hooks: both settings need a restart.
    private static bool sort, group;

    private static bool Apply(Harmony harmony)
    {
        sort = SmartSettings.SortTree.Value;
        group = SmartSettings.GroupVariants.Value;
        var setter = AccessTools.PropertySetter(typeof(UnitTreeBrowserViewModel), nameof(UnitTreeBrowserViewModel.MuseumEntries));
        if (setter == null || Encyclopedia.BrowserConstructor == null)
        {
            if (sort)
                ModLog.Warn("Unit Reference tree entries not found (game updated?) - the tree stays in the game's order.");
            if (group)
                ModLog.Warn("Unit Reference tree entries not found (game updated?) - loadout variants stay as the game labels them.");
            return false;
        }
        harmony.Patch(Encyclopedia.BrowserConstructor, postfix: new HarmonyMethod(typeof(UnitTree), nameof(Constructed)));
        harmony.Patch(setter, postfix: new HarmonyMethod(typeof(UnitTree), nameof(EntriesSet)));
        if (sort)
            ModLog.Debug("smart windows: Unit Reference tree sorted by date");
        if (group)
            ModLog.Debug("smart windows: Unit Reference loadout variants labelled and grouped");
        return true;
    }

    private static void Constructed(UnitTreeBrowserViewModel __instance, string stateKey)
    {
        if (stateKey == Encyclopedia.Key)
            Arrange(__instance);
    }

    private static void EntriesSet(UnitTreeBrowserViewModel __instance)
    {
        if (Encyclopedia.Is(__instance))
            Arrange(__instance);
    }

    private static void Arrange(UnitTreeBrowserViewModel browser)
    {
        if (browser.MuseumEntries == null)
            return;
        if (sort)
            TreeOrder.Sort(browser);
        if (group)
            VariantGroups.ArrangeAll(browser);
    }
}
