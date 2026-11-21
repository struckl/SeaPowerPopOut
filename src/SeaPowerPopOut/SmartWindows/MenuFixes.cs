using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using HarmonyLib;
using SeapowerUI.ViewModels;
using UnityEngine;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Two small menu fixes:
/// - the editor's default file "_Custom Mission" is left out of the SCENARIOS list of the main menu;
/// - the mission editor's file picker for opening a mission starts in the folder of the mission opened last.
/// </summary>
internal static class MenuFixes
{
    public static readonly Feature HideCustomMission = new Feature
    {
        Switch = () => SmartSettings.HideCustomMission.Value,
        Hook = HideCustom,
    };

    public static readonly Feature RememberMissionFolder = new Feature
    {
        Switch = () => SmartSettings.RememberMissionFolder.Value,
        Hook = RememberFolder,
    };

    private static MethodInfo pickerMode;

    private static bool HideCustom(Harmony harmony)
    {
        MethodInfo getMissions = AccessTools.Method(typeof(ScenarioMenuViewModel), nameof(ScenarioMenuViewModel.GetMissions));
        if (getMissions == null)
        {
            Rig.Log.Warn("ScenarioMenuViewModel.GetMissions not found (game updated?) - '_Custom Mission' stays in the list.");
            return false;
        }
        harmony.Patch(getMissions, postfix: new HarmonyMethod(typeof(MenuFixes), nameof(MissionsListed)));
        return true;
    }

    private static bool RememberFolder(Harmony harmony)
    {
        pickerMode = AccessTools.PropertyGetter(typeof(FilePickerViewModel), "FilePickerModeSetting");
        MethodInfo initialise = AccessTools.Method(typeof(FilePickerViewModel), nameof(FilePickerViewModel.Initialise), Type.EmptyTypes);
        MethodInfo load = AccessTools.Method(typeof(MissionEditorViewModel), nameof(MissionEditorViewModel.LoadMission));
        if (pickerMode == null || initialise == null || load == null)
        {
            Rig.Log.Warn("Mission editor file picker members not found (game updated?) - its folder is not remembered.");
            return false;
        }
        harmony.Patch(load, prefix: new HarmonyMethod(typeof(MenuFixes), nameof(MissionOpened)));
        harmony.Patch(initialise, postfix: new HarmonyMethod(typeof(MenuFixes), nameof(PickerOpened)));
        return true;
    }

    private static void MissionsListed(ObservableCollection<IMissionTreeViewItem> __result)
    {
        if (__result == null)
            return;
        for (int i = __result.Count - 1; i >= 0; i--)
        {
            if (__result[i] is Mission mission && MissionPaths.IsCustomMission(mission.FileName))
                __result.RemoveAt(i);
        }
    }

    private static void MissionOpened(string filename)
    {
        try
        {
            string folder = MissionPaths.FolderToRemember(filename, Application.streamingAssetsPath);
            if (folder != null && Directory.Exists(folder))
                SmartSettings.LastMissionFolder.Value = folder;
        }
        catch (Exception ex)
        {
            Rig.Log.Debug($"mission folder not remembered: {ex.Message}");
        }
    }

    private static void PickerOpened(FilePickerViewModel __instance)
    {
        try
        {
            var mode = (FilePickerViewModel.FilePickerMode)pickerMode.Invoke(__instance, null);
            string saved = SmartSettings.LastMissionFolder.Value;
            if (mode != FilePickerViewModel.FilePickerMode.OpenFile
                || !MissionPaths.IsMissionPickerStart(__instance.initialPath?.FullName)
                || !Directory.Exists(saved)
                || !MissionPaths.InsideHome(saved, __instance.homePath?.FullName))
                return;
            __instance.SelectedFolder = new DirectoryInfo(saved);
        }
        catch (Exception ex)
        {
            Rig.Log.Debug($"mission picker folder not applied: {ex.Message}");
        }
    }
}
