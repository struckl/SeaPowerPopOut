using System;
using System.Reflection;
using HarmonyLib;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Campaign Task Force status: a damaged ship that can be repaired may be dismissed, and its refund is reduced by the
/// repair cost. The game lets a ship go only when it is undamaged. The members involved are internal to the game, so
/// they are reached by reflection, all resolved once; if any is missing the feature stays off.
/// </summary>
internal static class CampaignDismiss
{
    private static MethodInfo repairEvaluation, logisticsState;
    private static PropertyInfo historicalReinforcements;

    public static bool Apply(Harmony harmony)
    {
        Type status = AccessTools.TypeByName("SeapowerUI.ViewModels.LinearCampaignTaskForceStatusViewModel");
        Type item = AccessTools.TypeByName("SeapowerUI.ViewModels.LinearCampaignTaskForceBuilderRosterItem");
        if (status == null || item == null)
        {
            ModLog.Warn("Task Force status not found (game updated?) - damaged ships cannot be dismissed.");
            return false;
        }

        MethodInfo canDismiss = AccessTools.Method(status, "CanDismiss", new[] { item });
        MethodInfo refund = AccessTools.Method(status, "CalculateDismissRefund", new[] { item, typeof(int) });
        repairEvaluation = AccessTools.Method(status, "GetRepairEvaluation", new[] { item });
        logisticsState = AccessTools.Method(status, "GetLogisticsState", new[] { item });
        historicalReinforcements = AccessTools.Property(status, "UsesHistoricalReinforcements");   // not in every game build
        if (canDismiss == null || refund == null || repairEvaluation == null || logisticsState == null)
        {
            ModLog.Warn("Task Force dismiss members not found (game updated?) - damaged ships cannot be dismissed.");
            return false;
        }

        harmony.Patch(canDismiss, postfix: new HarmonyMethod(typeof(CampaignDismiss), nameof(CanDismissed)));
        harmony.Patch(refund, postfix: new HarmonyMethod(typeof(CampaignDismiss), nameof(RefundCalculated)));
        return true;
    }

    private static void CanDismissed(object __instance, object item, ref bool __result)
    {
        if (__result || item == null)
            return;
        try
        {
            object logistics = logisticsState.Invoke(__instance, new[] { item });
            if (logistics == null || !Repair(__instance, item, out bool repairable, out _))
                return;
            bool historical = historicalReinforcements != null && (bool)historicalReinforcements.GetValue(__instance);
            string airbase = Read<string>(item, "CampaignAirbaseId");
            __result = DismissRules.MayDismissDamaged(Read<bool>(item, "IsAirUnit"), !string.IsNullOrWhiteSpace(airbase),
                historical, Read<bool>(logistics, "IsDestroyed"), repairable);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"dismiss damaged (allowed): {ex.Message}");
        }
    }

    private static void RefundCalculated(object __instance, object item, ref int __result)
    {
        if (item == null || __result <= 0)
            return;
        try
        {
            if (Repair(__instance, item, out _, out int cost))
                __result = DismissRules.RefundAfterRepair(__result, cost);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"dismiss damaged (refund): {ex.Message}");
        }
    }

    /// <summary>The repair evaluation of a ship: false if it is no ship or has no damage.</summary>
    private static bool Repair(object status, object item, out bool repairable, out int cost)
    {
        repairable = false;
        cost = 0;
        object evaluation = repairEvaluation.Invoke(status, new[] { item });
        if (evaluation == null || !Read<bool>(evaluation, "IsShipOrSubmarine") || !Read<bool>(evaluation, "HasDamage"))
            return false;
        cost = Read<int>(evaluation, "Cost");
        repairable = DismissRules.IsRepairable(Read<bool>(evaluation, "FacilitiesAvailable"), Read<bool>(evaluation, "IsAllowedDamage"),
            Read<bool>(evaluation, "IsDisallowedDamage"), Read<bool>(evaluation, "HasStateSection"));
        return true;
    }

    private static T Read<T>(object target, string property)
    {
        object value = AccessTools.Property(target.GetType(), property)?.GetValue(target);
        return value is T typed ? typed : default;
    }
}
