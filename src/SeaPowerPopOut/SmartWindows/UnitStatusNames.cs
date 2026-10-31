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
    private static FieldInfo unitField;
    private static MethodInfo setName;

    public static bool Apply(Harmony harmony)
    {
        MethodInfo switchUnit = AccessTools.Method(typeof(UnitStatusViewModel), "SwitchUnit");
        unitField = AccessTools.Field(typeof(UnitStatusViewModel), "_unit");
        setName = AccessTools.PropertySetter(typeof(UnitStatusViewModel), nameof(UnitStatusViewModel.UnitName));
        if (switchUnit == null || unitField == null || setName == null)
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
            var unit = unitField.GetValue(__instance) as ObjectBase;
            string full = unit == null ? null : unit.DisplayNameFull;
            if (!string.IsNullOrEmpty(full) && !string.Equals(__instance.UnitName, full))
                setName.Invoke(__instance, new object[] { full });
        }
        catch (Exception ex)
        {
            ModLog.Debug($"full unit name: {ex.Message}");
        }
    }
}
