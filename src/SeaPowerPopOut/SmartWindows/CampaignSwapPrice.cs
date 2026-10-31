using System;
using System.Reflection;
using HarmonyLib;
using SeaPower;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Campaign Task Force Builder, Loadouts tab: swapping a ship for another hull version of its class is priced like
/// buying that hull from the campaign's roster. The game prices the new hull from the unit's ini and the ship you have
/// from the roster, so a different roster price turned an upgrade into a refund (or the other way round). After the
/// game's evaluation the replacement is rebuilt with the roster price and the price difference and the "can I
/// afford it" verdict are worked out again. The game's own checks (repairs, hull available, historical campaign) come
/// first and are never overridden: a rejected swap has no replacement and is left alone.
/// Internal game members are reached by reflection, resolved once; if one is missing the feature stays off.
/// </summary>
internal static class CampaignSwapPrice
{
    private static MethodInfo openIni, countsAgainstCap, rosterCost, copyEmptyState, capHeadroom, formatText;
    private static FieldInfo capPoints, availablePoints;

    public static bool Apply(Harmony harmony)
    {
        Type status = typeof(LinearCampaignTaskForceStatusViewModel);
        Type item = typeof(LinearCampaignTaskForceBuilderRosterItem);
        MethodInfo evaluate = AccessTools.Method(status, "EvaluateUnitVersionSwap");
        Type rules = AccessTools.TypeByName("SeapowerUI.ViewModels.LinearCampaignTaskForceModeRules");   // internal class
        rosterCost = AccessTools.Method(rules, "ReadRosterTaskForceCost");
        openIni = AccessTools.Method(rules, "OpenCampaignIni");
        countsAgainstCap = AccessTools.Method(rules, "CountsAgainstFleetCap");
        copyEmptyState = AccessTools.Method(item, "CopyEmptyPurchaseStateFrom");
        capHeadroom = AccessTools.Method(status, "ReadFleetCapHeadroom");
        formatText = AccessTools.Method(status, "FormatText");
        capPoints = AccessTools.Field(status, "_capPoints");
        availablePoints = AccessTools.Field(status, "_availablePoints");
        if (evaluate == null || rules == null || openIni == null || countsAgainstCap == null || rosterCost == null || copyEmptyState == null || capHeadroom == null || formatText == null ||
            capPoints == null || availablePoints == null)
        {
            ModLog.Warn("Task Force hull swap pricing not found (game updated?) - swap prices stay as the game computes them.");
            return false;
        }
        harmony.Patch(evaluate, postfix: new HarmonyMethod(typeof(CampaignSwapPrice), nameof(Evaluated)));
        return true;
    }

    private static void Evaluated(LinearCampaignTaskForceStatusViewModel __instance, LinearCampaignTaskForceBuilderRosterItem item,
        string newUnitType, ref int costDelta, ref LinearCampaignTaskForceBuilderRosterItem replacement,
        ref string blockedReason, ref bool __result)
    {
        if (item == null || replacement == null)
            return;
        try
        {
            object campaignIni = openIni.Invoke(null, null);
            int roster = (int)rosterCost.Invoke(null, new object[] { campaignIni, item.UnitFolder, newUnitType, item.Category, replacement.VariantReference });
            if (roster == replacement.BaseCostPerUnit)
                return;

            var fixedItem = new LinearCampaignTaskForceBuilderRosterItem(item.UnitFolder, newUnitType, item.Category,
                replacement.VariantReference, roster, item.IsFlagshipEligible, replacement.SelectedLoadoutVariant,
                Math.Max(1, item.QuantityPurchased), string.Empty, item.InstanceId, null, item.CrewSkill,
                item.CrewSkillSurvivalCredits, (IniHandler)campaignIni);
            copyEmptyState.Invoke(fixedItem, new object[] { item });

            int perUnitChange = fixedItem.CostPerUnit - replacement.CostPerUnit;
            costDelta += perUnitChange;
            replacement = fixedItem;
            Verdict(__instance, item, costDelta, ref blockedReason, ref __result);
            ModLog.Debug($"hull swap {item.UnitType} -> {newUnitType}: roster price {roster}, price difference now {costDelta}");
        }
        catch (Exception ex)
        {
            ModLog.Debug($"hull swap price not fixed: {ex.Message}");
        }
    }

    /// <summary>The game's affordability checks again (fleet value cap, then available points) for the new price difference.</summary>
    private static void Verdict(LinearCampaignTaskForceStatusViewModel vm, LinearCampaignTaskForceBuilderRosterItem item,
        int costDelta, ref string blockedReason, ref bool result)
    {
        blockedReason = string.Empty;
        result = true;
        if (costDelta <= 0)
            return;
        int cap = (int)capPoints.GetValue(vm);
        if (cap > 0 && (bool)countsAgainstCap.Invoke(null, new object[] { item }))
        {
            float headroom = (float)capHeadroom.Invoke(vm, null);
            if (costDelta > headroom + 0.01f)
            {
                blockedReason = Format("LoadoutOverFleetCapFormat", "Would exceed the fleet value cap by {0} pts", (int)Math.Ceiling(costDelta - headroom));
                result = false;
                return;
            }
        }
        int available = (int)availablePoints.GetValue(vm);
        if (costDelta > available)
        {
            blockedReason = Format("LoadoutNeedsPointsFormat", "Requires {0} pts - {1} available", costDelta, available);
            result = false;
        }
    }

    private static string Format(string key, string fallback, params object[] args) =>
        (string)formatText.Invoke(null, new object[] { key, fallback, args });
}
