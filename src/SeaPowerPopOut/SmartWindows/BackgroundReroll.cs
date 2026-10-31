using System;
using System.Reflection;
using HarmonyLib;
using SeaPower;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// The game picks the very first loading background before the mod is running, so the mod-backgrounds filter has not
/// taken effect yet. Once, as soon as the load screen exists and shows a picture, the game's own picker runs again
/// (the filter hooks drop the game's pictures first when mods brought their own).
/// </summary>
internal static class BackgroundReroll
{
    private const int MaxTries = 600;

    private static MethodInfo changeBackground;
    private static bool armed;
    private static bool done;
    private static int tries;

    /// <summary>Called after <see cref="BackgroundFilter.Apply"/>; arms the one-time reroll.</summary>
    public static void Apply()
    {
        changeBackground = AccessTools.Method(typeof(LoadScreen), "ChangeBackground");
        if (changeBackground == null)
        {
            ModLog.Warn("LoadScreen.ChangeBackground not found (game updated?) - the first background is not rerolled.");
            return;
        }
        armed = true;
    }

    /// <summary>Called every frame until it has run once (or given up).</summary>
    public static void Update()
    {
        if (!armed || done)
            return;
        if (++tries > MaxTries)
        {
            done = true;
            return;
        }
        try
        {
            if (!Singleton<LoadScreen>.InstanceExists())
                return;
            LoadScreen screen = Singleton<LoadScreen>.Instance;
            if (screen.BackgroundImage == null)
                return;
            done = true;
            changeBackground.Invoke(screen, null);
            ModLog.Debug("smart windows: first background picked again");
        }
        catch (Exception ex)
        {
            done = true;
            ModLog.Debug($"background reroll: {ex.Message}");
        }
    }
}
