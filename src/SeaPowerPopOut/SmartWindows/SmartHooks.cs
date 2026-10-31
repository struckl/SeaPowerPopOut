using HarmonyLib;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>Every game hook of the Smart Windows features that is not a window event. Each one is optional.</summary>
internal static class SmartHooks
{
    /// <summary>Called once NoesisGUI runs (with the window hooks).</summary>
    public static void Apply(Harmony harmony)
    {
        if (!WindowMemory.Active)
            return;
        if (SmartSettings.FullUnitName.Value && UnitStatusNames.Apply(harmony))
            ModLog.Debug("smart windows: Unit Status shows full unit names");
        if (SmartSettings.StatusReference.Value && UnitStatusReference.Apply(harmony))
            ModLog.Debug("smart windows: Unit Status has a Reference tab");
        if (SmartSettings.SectionsMinHeight.Value > 0f && DamageControlLayout.Apply(harmony))
            ModLog.Debug("smart windows: Damage Control tab minimum height set");
        if (SmartSettings.TrackState.Value && TrackState.Apply(harmony))
            ModLog.Debug("smart windows: Unit Status shows the Track State of a selected contact");
        if (SmartSettings.SortLoadouts.Value && CampaignLoadouts.Apply(harmony))
            ModLog.Debug("smart windows: campaign loadouts sorted by date");
        if (SmartSettings.LoadoutPanelOrder.Value)
        {
            LoadoutPanelOrder.Apply();
            ModLog.Debug("smart windows: campaign loadout panel order");
        }
        if (SmartSettings.FixSwapPrice.Value && CampaignSwapPrice.Apply(harmony))
            ModLog.Debug("smart windows: hull swap priced from the campaign roster");
        if (SmartSettings.StrictLoadouts.Value && StrictLoadouts.Apply(harmony))
            ModLog.Debug("smart windows: campaign Strict loadouts option available");
        if (SmartSettings.DismissDamaged.Value && CampaignDismiss.Apply(harmony))
            ModLog.Debug("smart windows: damaged ships can be dismissed");
        if (SmartSettings.UpgradeShip.Value)
            UpgradeShip.Register();
        if (SmartSettings.TooltipFrame.Value && SmartSettings.BorderEnabled.Value)
            ToolTipFrame.Register();
        if (SmartSettings.UnitPanelMenu.Value)
            UnitPanelMenu.Register();
        if (SmartSettings.MessagesClickThrough.Value)
            MessageClickThrough.Register();
        if (SmartSettings.LinkHelp.Value)
            LinkHelp.Register();
        MenuFixes.Apply(harmony);
        if (SmartSettings.ColorIniFiles.Value && ColorIniFiles.Register())
            ModLog.Debug("smart windows: ini files coloured in the mission editor pickers");
        DesktopMode.Apply(harmony);
        if (SmartSettings.ModBackgroundsOnly.Value)
        {
            BackgroundFilter.Apply(harmony);
            BackgroundReroll.Apply();
        }
        // The browser hooks also tell the encyclopedia's browser from the mission editor's, for the tree order.
        bool browser = BrowserMemory.Apply(harmony);
        if (browser && SmartSettings.HideEmptyCountries.Value && !CountryList.Apply(harmony))
            SmartSettings.HideEmptyCountries.Value = false;     // not available in this game build: leave the list alone
        if (browser && SmartSettings.EarliestVariantOnClick.Value && EarliestVariant.Apply(harmony))
            ModLog.Debug("smart windows: Unit Reference selects the earliest variant of a class");
        if (browser && BrowserFilters.Apply(harmony))
            ModLog.Debug("smart windows: Unit Reference filters ('Armed only', country list) active");
        if (browser && SmartSettings.SortTree.Value && TreeOrder.Apply(harmony))
            ModLog.Debug("smart windows: Unit Reference tree sorted by date");
        if (browser && SmartSettings.KeepOpenedUnit.Value && KeepOpenedUnit.Apply(harmony))
            ModLog.Debug("smart windows: Unit Reference keeps the opened unit in the tree");
        if (browser && SmartSettings.LoadoutList.Value && LoadoutList.Apply(harmony))
            ModLog.Debug("smart windows: Unit Reference lists loadouts for all units");
        if (browser && SmartSettings.GroupVariants.Value && VariantGroups.Apply(harmony))
            ModLog.Debug("smart windows: Unit Reference loadout variants labelled and grouped");
        if (SmartSettings.ExtendedSearch.Value && ExtendedSearch.Apply(harmony))
            ModLog.Debug("smart windows: Unit Reference search also reads ids and descriptions");
    }
}
