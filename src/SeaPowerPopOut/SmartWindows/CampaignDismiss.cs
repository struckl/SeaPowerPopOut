using System;
using System.Reflection;
using HarmonyLib;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Campaign Task Force status: a damaged ship that can be repaired may be dismissed, and its refund is reduced by the
/// repair cost. The game lets a ship go only when it is undamaged. The members involved are internal to the game, so
/// they are reached by reflection, all resolved once; if any is missing the feature stays off. The rule itself
/// (<see cref="AllowsDamaged"/>, <see cref="ReducedRefund"/>) is public: "Upgrade ship" applies it on its own and so
/// does not depend on this feature's switch.
/// </summary>
internal static class CampaignDismiss
{
    public static readonly Feature Feature = new Feature
    {
        Switch = () => SmartSettings.DismissDamaged.Value,
        Hook = Apply,
        Hooked = "damaged ships can be dismissed",
    };

    private static MethodInfo repairEvaluation, logisticsState;

    private static bool resolved;

    /// <summary>Resolves the repair members once; false when the game changed (nothing here works then).</summary>
    public static bool Resolve()
    {
        if (!resolved)
        {
            resolved = true;
            repairEvaluation = AccessTools.Method(CampaignGame.Status, "GetRepairEvaluation", new[] { CampaignGame.Item });
            logisticsState = AccessTools.Method(CampaignGame.Status, "GetLogisticsState", new[] { CampaignGame.Item });
        }
        return repairEvaluation != null && logisticsState != null;
    }

    private static bool Apply(Harmony harmony)
    {
        MethodInfo canDismiss = CampaignGame.CanDismissMethod;
        MethodInfo refund = CampaignGame.DismissRefundMethod;
        if (canDismiss == null || refund == null || !Resolve())
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
        if (__result || item == null || CampaignGame.PlainRules)
            return;
        try
        {
            __result = AllowsDamaged(__instance, item);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"dismiss damaged (allowed): {ex.Message}");
        }
    }

    private static void RefundCalculated(object __instance, object item, ref int __result)
    {
        if (item == null || __result <= 0 || CampaignGame.PlainRules)
            return;
        try
        {
            __result = ReducedRefund(__instance, item, __result);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"dismiss damaged (refund): {ex.Message}");
        }
    }

    /// <summary>True when the ship is damaged and the rule lets it go anyway (a ship the game lets go already is not).</summary>
    public static bool AllowsDamaged(object status, object item)
    {
        object logistics = logisticsState.Invoke(status, new[] { item });
        if (logistics == null || !Repair(status, item, out bool repairable, out _))
            return false;
        bool historical = CampaignGame.UsesHistoricalReinforcements(status);
        string airbase = Read<string>(item, "CampaignAirbaseId");
        return DismissRules.MayDismissDamaged(Read<bool>(item, "IsAirUnit"), !string.IsNullOrWhiteSpace(airbase),
            historical, Read<bool>(logistics, "IsDestroyed"), repairable);
    }

    /// <summary>The game's dismiss refund of a ship, less the repair cost when it is damaged.</summary>
    public static int ReducedRefund(object status, object item, int refund) =>
        Repair(status, item, out _, out int cost) ? DismissRules.RefundAfterRepair(refund, cost) : refund;

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
