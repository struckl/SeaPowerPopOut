using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using SeaPower;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Background pictures of the main menu and the loading screens: when mods bring their own (<c>ui/backgrounds</c>),
/// only those are shown instead of one picked at random from the game's and the mods' together. Without any mod
/// pictures the game's stay.
/// </summary>
internal static class BackgroundFilter
{
    public static readonly Feature Feature = new Feature
    {
        Switch = () => SmartSettings.ModBackgroundsOnly.Value,
        Hook = Apply,
    };

    private static FieldInfo loadScreenImages;

    /// <summary>The load screen's picker of a background picture.</summary>
    public static readonly MethodInfo ChangeBackground = AccessTools.Method(typeof(LoadScreen), "ChangeBackground");

    private static bool Apply(Harmony harmony)
    {
        MethodInfo getAllFiles = AccessTools.Method(typeof(FileManager), nameof(FileManager.GetAllFiles));
        MethodInfo change = ChangeBackground;
        loadScreenImages = AccessTools.Field(typeof(LoadScreen), "imagePaths");
        if (getAllFiles == null)
        {
            Rig.Log.Warn("FileManager.GetAllFiles not found (game updated?) - mod backgrounds do not replace the game's.");
            return false;
        }
        harmony.Patch(getAllFiles, postfix: new HarmonyMethod(typeof(BackgroundFilter), nameof(FilesListed)));
        if (change != null && loadScreenImages != null)
            harmony.Patch(change, prefix: new HarmonyMethod(typeof(BackgroundFilter), nameof(LoadScreenPicking)));
        return true;
    }

    // Harmony fills 'path' by name from GetAllFiles' own parameter.
    private static void FilesListed(string path, List<string> __result)
    {
        if (BackgroundPaths.IsBackgroundFolder(path))
            Drop(__result);
    }

    private static void LoadScreenPicking(LoadScreen __instance) => Drop(loadScreenImages.GetValue(__instance) as List<string>);

    private static void Drop(List<string> pictures)
    {
        try
        {
            if (!Singleton<FileManager>.InstanceExists())
                return;
            DirectoryInfo[] special = Singleton<FileManager>.Instance.specialDirectories;
            if (special == null || special.Length < 2)
                return;
            int dropped = BackgroundPaths.DropGamePictures(pictures, special[1].FullName);
            if (dropped > 0)
                Rig.Log.Debug($"smart windows: {dropped} game background(s) left out, {pictures.Count} from mods");
        }
        catch (Exception ex)
        {
            Rig.Log.Debug($"backgrounds: {ex.Message}");
        }
    }
}
