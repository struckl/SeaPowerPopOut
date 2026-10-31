using System;
using System.Reflection;
using HarmonyLib;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Campaign Task Force Builder, Loadouts tab: the ship's loadouts are listed by service entry year, oldest first,
/// instead of in the order the game builds them. Loadouts without a year go last.
/// </summary>
internal static class CampaignLoadouts
{
    public static bool Apply(Harmony harmony)
    {
        MethodInfo rebuild = AccessTools.Method(typeof(LinearCampaignTaskForceLoadoutsViewModel), "RebuildOptions");
        if (rebuild == null)
        {
            ModLog.Warn("Task Force Builder loadout list not found (game updated?) - loadouts stay in the game's order.");
            return false;
        }
        harmony.Patch(rebuild, postfix: new HarmonyMethod(typeof(CampaignLoadouts), nameof(Rebuilt)));
        return true;
    }

    private static void Rebuilt(LinearCampaignTaskForceLoadoutsViewModel __instance)
    {
        try
        {
            var options = __instance.Options;
            if (options != null && options.Count >= 2)
                StableOrder.Sort(options, (a, b) => YearOf(a).CompareTo(YearOf(b)), options.Move);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"loadouts not sorted: {ex.Message}");
        }
    }

    private static int YearOf(LinearCampaignTaskForceLoadoutsViewModel.LoadoutOptionRow row)
    {
        int year = row?.Option?.ServiceEntryYear ?? 0;
        return year > 0 ? year : int.MaxValue;
    }
}
