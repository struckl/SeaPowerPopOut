using System;
using System.Reflection;
using HarmonyLib;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// The campaign Task Force members that several features use (hull swap price, dismiss damaged ships, upgrade ship).
/// The rules class and these members are internal to the game, so they are resolved here once; each feature checks
/// the ones it needs and stays off when one is missing.
/// <para>"Upgrade ship" reads the game's own <c>CanDismiss</c> and <c>CalculateDismissRefund</c> inside
/// <see cref="Plain"/>, where <see cref="CampaignDismiss"/> does not widen them, and applies the damaged-ship rule
/// itself, so its list is the same whether "Dismiss damaged ships" is on or off.</para>
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

    private static int plain;

    /// <summary>True while <see cref="Plain"/> runs: the dismiss patches leave the game's answer as it is.</summary>
    public static bool PlainRules => plain > 0;

    /// <summary>Reads something from the game's campaign rules as the game itself answers it.</summary>
    public static T Plain<T>(Func<T> read)
    {
        plain++;
        try
        {
            return read();
        }
        finally
        {
            plain--;
        }
    }

    public static object OpenIni() => OpenIniMethod.Invoke(null, null);

    public static bool CountsAgainstCap(object item) => (bool)CountsAgainstCapMethod.Invoke(null, new[] { item });

    public static bool UsesHistoricalReinforcements(object status) => Historical != null && (bool)Historical.GetValue(status);
}
