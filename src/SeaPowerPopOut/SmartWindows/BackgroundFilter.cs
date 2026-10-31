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
    private static FieldInfo loadScreenImages;

    public static void Apply(Harmony harmony)
    {
        MethodInfo getAllFiles = AccessTools.Method(typeof(FileManager), nameof(FileManager.GetAllFiles));
        MethodInfo change = AccessTools.Method(typeof(LoadScreen), "ChangeBackground");
        loadScreenImages = AccessTools.Field(typeof(LoadScreen), "imagePaths");
        if (getAllFiles == null)
        {
            ModLog.Warn("FileManager.GetAllFiles not found (game updated?) - mod backgrounds do not replace the game's.");
            return;
        }
        harmony.Patch(getAllFiles, postfix: new HarmonyMethod(typeof(BackgroundFilter), nameof(FilesListed)));
        if (change != null && loadScreenImages != null)
            harmony.Patch(change, prefix: new HarmonyMethod(typeof(BackgroundFilter), nameof(LoadScreenPicking)));
    }

    // 'path' is a parameter of GetAllFiles in both game builds (the globe build has one more, optional, parameter).
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
                ModLog.Debug($"smart windows: {dropped} game background(s) left out, {pictures.Count} from mods");
        }
        catch (Exception ex)
        {
            ModLog.Debug($"backgrounds: {ex.Message}");
        }
    }
}
