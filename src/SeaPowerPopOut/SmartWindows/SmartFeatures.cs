using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// The Smart Windows features, in the order they hook the game and hear about windows and frames. The order matters
/// in three places: desktop mode releases a window's anchors before the window memory places it, the Unit Reference
/// layout knows its tree before the memory saves the window, and the encyclopedia detector hooks the unit browser before
/// the features that ask it. The window memory goes last: it saves what the others changed.
/// <para>"Enable window memory" is the switch of all of them: off, none starts.</para>
/// </summary>
internal static class SmartFeatures
{
    private static readonly List<Feature> On = new List<Feature>();

    private static IEnumerable<Feature> All()
    {
        yield return LogCleanup.Feature;
        yield return TreeDump.Feature;
        yield return DesktopMode.Feature;
        yield return UnitReferenceLayout.Feature;
        yield return TreeScroller.Feature;
        yield return DamageControlLayout.Feature;
        yield return TrackState.Feature;
        yield return UnitStatusNames.Feature;
        yield return UnitStatusReference.Feature;
        yield return UnitPanelMenu.Feature;
        yield return CampaignLoadouts.Feature;
        yield return LoadoutPanelOrder.Feature;
        yield return CampaignSwapPrice.Feature;
        yield return StrictLoadouts.Feature;
        yield return CampaignDismiss.Feature;
        yield return UpgradeShip.Feature;
        yield return ToolTipFrame.Feature;
        yield return MessageClickThrough.Feature;
        yield return LinkHelp.Feature;
        yield return MenuFixes.HideCustomMission;
        yield return MenuFixes.RememberMissionFolder;
        yield return ColorIniFiles.Feature;
        yield return BackgroundFilter.Feature;
        yield return BackgroundReroll.Feature;
        yield return Encyclopedia.Feature;
        yield return BrowserMemory.Feature;
        yield return CountryList.Feature;
        yield return EarliestVariant.Feature;
        yield return BrowserFilters.Feature;
        yield return UnitTree.Feature;
        yield return KeepOpenedUnit.Feature;
        yield return LoadoutList.Feature;
        yield return ExtendedSearch.Feature;
        yield return ResizeCursors.Feature;
        yield return TimePanelDrag.Feature;
        yield return SnapController.Feature;
        yield return WindowBehaviors.Feature;
        yield return WindowBorder.Feature;
        yield return SettingsPanel.Feature;
        yield return WindowLook.Feature;
        yield return WindowMemory.Feature;
    }

    /// <summary>At plugin load.</summary>
    public static void Init()
    {
        if (!SmartSettings.Enabled.Value)
            return;
        Loc.Init(Path.GetDirectoryName(typeof(SmartFeatures).Assembly.Location));
        foreach (Feature feature in All())
        {
            if (!feature.Wanted)
                continue;
            On.Add(feature);
            feature.Begin();
        }
    }

    /// <summary>Once NoesisGUI runs (with the window hooks).</summary>
    public static void Apply(Harmony harmony)
    {
        foreach (Feature feature in On)
        {
            if (feature.Hook != null)
                feature.Apply(harmony);
        }
    }

    public static void WindowLoaded(GameWindow window, bool onMainScreen)
    {
        if (window == null)
            return;
        foreach (Feature feature in On)
        {
            System.Action<GameWindow> loaded = onMainScreen ? feature.WindowLoaded : feature.PoppedWindowLoaded;
            if (loaded != null && feature.Live)
                loaded(window);
        }
    }

    public static void Update()
    {
        if (On.Count == 0)
            return;
        SmartSettings.Flush();
        foreach (Feature feature in On)
        {
            if (feature.Update != null && feature.Live)
                feature.Update();
        }
    }

    /// <summary>The game closes.</summary>
    public static void Save()
    {
        foreach (Feature feature in On)
        {
            if (feature.Save != null && feature.Active)
                feature.Save();
        }
    }
}
