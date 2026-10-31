using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Noesis;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Campaign "Strict loadouts": a check box on the campaign start screen (beside "Limit unit dates"). When it is on, the
/// Task Force Builder offers only ships the mission lists as available (or that the player owns), and only loadouts that are
/// already open; the game otherwise also offers every ship sharing the encyclopedia tag.
/// The choice is stored as one extra text entry, <see cref="DataKey"/>, in the campaign's own PersistentData dictionary (the
/// game saves that dictionary as a plain key=value section and ignores keys it does not know), so a save loads fine without
/// the mod. A campaign without the entry counts as "off".
/// </summary>
internal static class StrictLoadouts
{
    /// <summary>Same key as the original Smart Windows, so its saves keep working.</summary>
    public const string DataKey = "SmartWindows_StrictLoadouts";

    private const string LimitDatesBinding = "TaskForceLimitUnitDates";

    private static bool staged;                         // the box on the start screen, until the campaign is created
    private static LinearCampaignTaskForceBuilderViewModel builder;
    private static FieldInfo restrictionField;
    private static PropertyInfo allowAllProperty;
    private static MethodInfo hasFixedLoadout, ownedKeys;
    private static PropertyInfo campaignInstance, persistentData;
    private static readonly ConditionalWeakTable<Panel, CheckBox> Boxes = new ConditionalWeakTable<Panel, CheckBox>();

    public static bool Apply(Harmony harmony)
    {
        Type vm = typeof(LinearCampaignTaskForceBuilderViewModel);
        MethodInfo load = AccessTools.Method(vm, "LoadCampaignTaskForceData");
        MethodInfo merge = AccessTools.Method(vm, "MergeRelatedClassGroups");
        MethodInfo family = AccessTools.Method(typeof(LinearCampaignTaskForceLoadoutsViewModel), "GetFamilyVersions");
        MethodInfo seed = AccessTools.Method("SeapowerUI.ViewModels.LinearCampaignTaskForceDifficultyRules:SeedPersistentDataForNewCampaign");
        restrictionField = AccessTools.Field(vm, "_missionDefaultRestriction");
        allowAllProperty = AccessTools.Property(vm, "AllowAllRosterUnits");
        hasFixedLoadout = AccessTools.PropertyGetter(typeof(LinearCampaignTaskForceBuilderRosterItem), "HasFixedLoadout");
        ownedKeys = AccessTools.Method("SeapowerUI.ViewModels.LinearCampaignTaskForceSaveManager:ReadCurrentTaskForceUnitKeys", Type.EmptyTypes);
        Type campaign = AccessTools.TypeByName("SeaPower.LinearCampaign");
        campaignInstance = campaign?.BaseType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        persistentData = campaign == null ? null : AccessTools.Property(campaign, "PersistentData");

        if (load == null || merge == null || family == null || seed == null || restrictionField == null || allowAllProperty == null ||
            hasFixedLoadout == null || campaignInstance == null || persistentData == null)
        {
            ModLog.Warn("Task Force Builder members not found (game updated?) - Strict loadouts not available.");
            return false;
        }

        harmony.Patch(load, prefix: new HarmonyMethod(typeof(StrictLoadouts), nameof(BuilderLoading)));
        harmony.Patch(merge, postfix: new HarmonyMethod(typeof(StrictLoadouts), nameof(GroupsMerged)));
        harmony.Patch(family, postfix: new HarmonyMethod(typeof(StrictLoadouts), nameof(FamilyVersions)));
        harmony.Patch(seed, postfix: new HarmonyMethod(typeof(StrictLoadouts), nameof(CampaignSeeded)));
        try
        {
            EventManager.RegisterClassHandler(typeof(ToggleButton), FrameworkElement.LoadedEvent, new RoutedEventHandler(ToggleLoaded));
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Strict loadouts check box not available: {ex.Message}");
        }
        return true;
    }

    private static bool Active()
    {
        try
        {
            object campaign = campaignInstance.GetValue(null);
            if (campaign == null || !(persistentData.GetValue(campaign) is Dictionary<string, string> data))
                return false;
            return data.TryGetValue(DataKey, out string value) && string.Equals(value, "True", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void BuilderLoading(LinearCampaignTaskForceBuilderViewModel __instance) => builder = __instance;

    /// <summary>A new campaign is created: write the box into its data (not for a campaign the game did not seed).</summary>
    private static void CampaignSeeded(IDictionary<string, string> persistentData, bool __result)
    {
        if (!__result || persistentData == null)
            return;
        persistentData[DataKey] = staged ? "True" : "False";
        staged = false;
    }

    /// <summary>The Task Force Builder's list: leave out ships the mission does not offer, and loadouts not opened yet.</summary>
    private static void GroupsMerged(List<LinearCampaignTaskForceBuilderRosterGroup> __result)
    {
        if (builder == null || __result == null || __result.Count == 0 || !Active())
            return;
        try
        {
            object restriction = null;
            MethodInfo allows = null;
            if (!(bool)allowAllProperty.GetValue(builder))
            {
                restriction = restrictionField.GetValue(builder);
                if (restriction != null)
                {
                    Type type = restriction.GetType();
                    if (!(bool)type.GetProperty("IsAllowAll").GetValue(restriction))
                        allows = type.GetMethod("Allows", new[] { typeof(string), typeof(string) });
                }
            }
            int left = 0;
            for (int g = __result.Count - 1; g >= 0; g--)
            {
                var variants = __result[g].Variants;
                for (int i = variants.Count - 1; i >= 0; i--)
                {
                    var item = variants[i];
                    if (item.IsCurrentTaskForceUnit)
                        continue;
                    if (LoadoutLocked(item) ||
                        (allows != null && !(bool)allows.Invoke(restriction, new object[] { item.UnitType, item.VariantReference })))
                    {
                        variants.RemoveAt(i);
                        left++;
                    }
                }
                if (variants.Count == 0)
                    __result.RemoveAt(g);
            }
            ModLog.Debug($"strict loadouts: {left} variant(s) left out of the builder list, {__result.Count} group(s) kept");
        }
        catch (Exception ex)
        {
            ModLog.Debug($"strict loadouts (list): {ex.Message}");
        }
    }

    /// <summary>A ship with one fixed loadout whose loadout is not open (or has only locked ones).</summary>
    private static bool LoadoutLocked(LinearCampaignTaskForceBuilderRosterItem item)
    {
        if (!(bool)hasFixedLoadout.Invoke(item, null))
            return false;
        string selected = item.SelectedLoadoutVariant;
        var options = item.LoadoutOptions;
        if (string.IsNullOrEmpty(selected) || options == null || options.Count == 0)
            return false;
        int locked = 0;
        foreach (var option in options)
        {
            if (string.Equals(option.Reference, selected, StringComparison.OrdinalIgnoreCase))
                return option.IsLocked;
            if (option.IsLocked)
                locked++;
        }
        return locked == options.Count;
    }

    /// <summary>The Loadouts tab's list of related versions: only the unit's own, the mission's and the owned ones.</summary>
    private static void FamilyVersions(LinearCampaignTaskForceBuilderRosterItem item, List<(string Type, string Name, int Year)> __result)
    {
        if (builder == null || item == null || __result == null || __result.Count == 0 || !Active())
            return;
        try
        {
            object restriction = restrictionField.GetValue(builder);
            if (restriction == null)
                return;
            Type type = restriction.GetType();
            if ((bool)type.GetProperty("IsAllowAll").GetValue(restriction))
                return;
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (object name in (IEnumerable)type.GetProperty("UnitTypes").GetValue(restriction))
                allowed.Add((string)name);
            if (ownedKeys?.Invoke(null, null) is IEnumerable owned)
            {
                foreach (object key in owned)
                {
                    if (key is string text)
                    {
                        int colon = text.IndexOf(':');
                        allowed.Add(colon < 0 ? text : text.Substring(0, colon));
                    }
                }
            }
            __result.RemoveAll(v => !string.Equals(v.Type, item.UnitType, StringComparison.OrdinalIgnoreCase) && !allowed.Contains(v.Type));
        }
        catch (Exception ex)
        {
            ModLog.Debug($"strict loadouts (versions): {ex.Message}");
        }
    }

    /// <summary>Adds the check box beside the game's "Limit unit dates" box on the campaign start screen.</summary>
    private static void ToggleLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!(sender is ToggleButton limit) ||
                !(BindingOperations.GetBinding(limit, ToggleButton.IsCheckedProperty) is Binding binding) ||
                binding.Path?.Path != LimitDatesBinding)
                return;
            if (!(VisualTreeHelper.GetParent(limit) is Panel panel))
                return;
            if (Boxes.TryGetValue(panel, out CheckBox existing))
            {
                existing.IsChecked = staged;
                return;
            }
            var box = new CheckBox { Content = Loc.Get("strict"), IsChecked = staged, Margin = limit.Margin, Style = limit.Style };
            box.ToolTip = Loc.Get("strict_tip");
            box.Click += (s, a) => staged = box.IsChecked == true;
            panel.Children.Insert(panel.Children.IndexOf(limit) + 1, box);
            Boxes.Add(panel, box);
            ModLog.Debug("smart windows: Strict loadouts check box added to the campaign start screen");
        }
        catch (Exception ex)
        {
            ModLog.Debug($"strict loadouts check box: {ex.Message}");
        }
    }
}
