using System;
using System.Reflection;
using HarmonyLib;
using SeaPower;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Unit Reference tree: a click on a class (a node with several variants) selects its earliest variant. The game
/// selects the one in service in the mission year, otherwise the latest.
/// </summary>
internal static class EarliestVariant
{
    public static bool Apply(Harmony harmony)
    {
        MethodInfo pick = AccessTools.Method(typeof(UnitTreeBrowserViewModel), "SelectBestGroupEntry");
        if (pick == null)
        {
            ModLog.Warn("Unit Reference variant choice not found (game updated?) - a class click selects as the game does.");
            return false;
        }
        harmony.Patch(pick, postfix: new HarmonyMethod(typeof(EarliestVariant), nameof(Picked)));
        return true;
    }

    private static void Picked(UnitTreeBrowserViewModel __instance, MuseumEntry group, ref MuseumEntry __result)
    {
        if (group?.MuseumEntries == null || !BrowserMemory.IsEncyclopedia(__instance))
            return;
        MuseumEntry earliest = Earliest(group.MuseumEntries);
        if (earliest != null)
            __result = earliest;
    }

    /// <summary>The entry with the smallest positive service entry date; null if none has a date.</summary>
    private static MuseumEntry Earliest(System.Collections.Generic.IEnumerable<MuseumEntry> entries)
    {
        MuseumEntry best = null;
        int bestDate = int.MaxValue;
        foreach (MuseumEntry entry in entries)
        {
            if (entry != null && entry.ServiceEntryDate > 0 && entry.ServiceEntryDate < bestDate)
            {
                best = entry;
                bestDate = entry.ServiceEntryDate;
            }
        }
        return best;
    }
}
