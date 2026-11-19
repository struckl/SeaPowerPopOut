using System;
using System.Reflection;
using HarmonyLib;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// The campaign Task Force members that several features use (hull swap price, dismiss damaged ships, upgrade ship).
/// The rules class and these members are internal to the game, so they are resolved here once; each feature checks
/// the ones it needs and stays off when one is missing.
/// <para>"Upgrade ship" sells the old ship through the game's own <c>CanDismiss</c> and <c>CalculateDismissRefund</c>.
/// With "Dismiss damaged ships" on, <see cref="CampaignDismiss"/> has widened both (a damaged, repairable ship may go,
/// for less): the upgrade list then offers such ships at that price. Off, it offers what the game allows.</para>
/// </summary>
internal static class CampaignGame
{
    public static readonly Type Status = typeof(LinearCampaignTaskForceStatusViewModel);
    public static readonly Type Item = typeof(LinearCampaignTaskForceBuilderRosterItem);
    public static readonly Type Rules = AccessTools.TypeByName("SeapowerUI.ViewModels.LinearCampaignTaskForceModeRules");

    public static readonly MethodInfo OpenIniMethod = Rules == null ? null : AccessTools.Method(Rules, "OpenCampaignIni", Type.EmptyTypes);
    public static readonly MethodInfo CountsAgainstCapMethod =
        Rules == null ? null : AccessTools.Method(Rules, "CountsAgainstFleetCap", new[] { Item });
    public static readonly MethodInfo CanDismissMethod = AccessTools.Method(Status, "CanDismiss", new[] { Item });
    public static readonly MethodInfo DismissRefundMethod = AccessTools.Method(Status, "CalculateDismissRefund", new[] { Item, typeof(int) });

    // Not in every game build: without it no campaign uses historical reinforcements.
    private static readonly PropertyInfo Historical = AccessTools.Property(Status, "UsesHistoricalReinforcements");

    public static object OpenIni() => OpenIniMethod.Invoke(null, null);

    public static bool CountsAgainstCap(object item) => (bool)CountsAgainstCapMethod.Invoke(null, new[] { item });

    public static bool UsesHistoricalReinforcements(object status) => Historical != null && (bool)Historical.GetValue(status);
}
