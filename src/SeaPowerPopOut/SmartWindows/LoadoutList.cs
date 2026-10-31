using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SeaPower;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Unit Reference: the loadout selector is shown for ships, submarines and land units with several loadouts too (the
/// game shows it for aircraft only). A unit that is itself a fixed loadout of a class lists its siblings' loadouts as well.
/// </summary>
internal static class LoadoutList
{
    private static readonly HashSet<object> completed = new HashSet<object>();
    private static MethodInfo managerInstance;

    public static bool Apply(Harmony harmony)
    {
        MethodInfo getter = AccessTools.PropertyGetter(typeof(ObjectInformationViewModel), nameof(ObjectInformationViewModel.HasMultipleLoadouts));
        if (getter == null)
        {
            ModLog.Warn("Unit Reference loadout list not found (game updated?) - loadouts are shown for aircraft only.");
            return false;
        }
        harmony.Patch(getter, postfix: new HarmonyMethod(typeof(LoadoutList), nameof(HasMultiple)));
        MethodInfo update = AccessTools.Method(typeof(ObjectInformationViewModel), "UpdateLoadoutsForEntry");
        if (update != null)
            harmony.Patch(update, prefix: new HarmonyMethod(typeof(LoadoutList), nameof(BeforeUpdate)),
                finalizer: new HarmonyMethod(typeof(LoadoutList), nameof(AfterUpdate)));
        else
            ModLog.Debug("UpdateLoadoutsForEntry not found: a loadout unit lists only its own loadout");
        return true;
    }

    private static void HasMultiple(ObjectInformationViewModel __instance, ref bool __result)
    {
        if (!__result && __instance.AvailableLoadouts != null && __instance.AvailableLoadouts.Count > 1)
            __result = true;
    }

    /// <summary>Gives a loadout unit its siblings' loadout data and lets the game list every loadout (IsLoadoutUnit off).</summary>
    private static void BeforeUpdate(MuseumEntry entry, out MuseumObjectEntry __state)
    {
        __state = null;
        try
        {
            if (entry is MuseumObjectEntry unit && unit.IsLoadoutUnit)
            {
                Complete(unit);
                unit.IsLoadoutUnit = false;
                __state = unit;
            }
        }
        catch (Exception ex)
        {
            ModLog.Debug($"loadout list: {ex.Message}");
        }
    }

    private static Exception AfterUpdate(Exception __exception, MuseumObjectEntry __state)
    {
        if (__state != null)
            __state.IsLoadoutUnit = true;
        return __exception;
    }

    /// <summary>Copies the per-loadout data of all other entries with the same ini into this one (once).</summary>
    private static void Complete(MuseumObjectEntry unit)
    {
        if (string.IsNullOrEmpty(unit.IniRef) || completed.Contains(unit.LoadoutWeaponSystemEntries))
            return;
        // EncyclopedicDataManager is internal: reach its singleton by reflection.
        managerInstance ??= AccessTools.PropertyGetter(AccessTools.TypeByName("SeaPower.EncyclopedicDataManager"), "Instance");
        if (managerInstance == null)
            return;
        object manager = managerInstance.Invoke(null, null);
        var entries = manager == null ? null
            : AccessTools.Property(manager.GetType(), "MuseumObjectEntries")?.GetValue(manager) as IEnumerable;
        if (entries == null)
            return;
        foreach (object item in entries)
        {
            if (item is MuseumObjectEntry other && other != unit
                && string.Equals(other.IniRef, unit.IniRef, StringComparison.Ordinal))
            {
                Merge(unit.LoadoutSensorsToRemove, other.LoadoutSensorsToRemove);
                Merge(unit.LoadoutWeaponSystemEntries, other.LoadoutWeaponSystemEntries);
                Merge(unit.LoadoutPodAmmunition, other.LoadoutPodAmmunition);
            }
        }
        completed.Add(unit.LoadoutWeaponSystemEntries);
    }

    private static void Merge<T>(Dictionary<string, T> into, Dictionary<string, T> from)
    {
        if (into == null || from == null || ReferenceEquals(into, from))
            return;
        foreach (var pair in from)
            if (!into.ContainsKey(pair.Key))
                into[pair.Key] = pair.Value;
    }
}
