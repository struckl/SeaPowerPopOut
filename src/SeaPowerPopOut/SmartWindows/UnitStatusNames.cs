using System;
using System.Reflection;
using HarmonyLib;
using SeaPower;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Unit Status window: the header shows the unit's full name (<c>DisplayNameFull</c>, e.g. with its class and hull
/// number) instead of the short name the game puts there.
/// </summary>
internal static class UnitStatusNames
{
    public static readonly Feature Feature = new Feature
    {
        Switch = () => SmartSettings.FullUnitName.Value,
        Hook = Apply,
        Hooked = "Unit Status shows full unit names",
    };

    private static bool Apply(Harmony harmony)
    {
        MethodInfo switchUnit = AccessTools.Method(typeof(UnitStatusViewModel), "SwitchUnit");
        if (switchUnit == null || StatusTabHost.UnitField == null || StatusTabHost.SetUnitName == null)
        {
            ModLog.Warn("Unit Status members not found (game updated?) - the header keeps the short unit name.");
            return false;
        }
        harmony.Patch(switchUnit, postfix: new HarmonyMethod(typeof(UnitStatusNames), nameof(UnitSwitched)));
        return true;
    }

    private static void UnitSwitched(UnitStatusViewModel __instance)
    {
        try
        {
            ObjectBase unit = StatusTabHost.UnitOf(__instance);
            string full = unit == null ? null : unit.DisplayNameFull;
            if (!string.IsNullOrEmpty(full) && !string.Equals(__instance.UnitName, full))
                StatusTabHost.SetUnitName.Invoke(__instance, new object[] { full });
        }
        catch (Exception ex)
        {
            ModLog.Debug($"full unit name: {ex.Message}");
        }
    }
}
