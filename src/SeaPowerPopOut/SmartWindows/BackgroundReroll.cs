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
    public static readonly Feature Feature = new Feature
    {
        Switch = () => SmartSettings.ModBackgroundsOnly.Value,
        Hook = _ => Resolve(),
        Update = Update,
    };

    private const int MaxTries = 600;

    private static MethodInfo changeBackground;
    private static bool done;
    private static int tries;

    private static bool Resolve()
    {
        changeBackground = BackgroundFilter.ChangeBackground;
        if (changeBackground == null)
            ModLog.Warn("LoadScreen.ChangeBackground not found (game updated?) - the first background is not rerolled.");
        return changeBackground != null;
    }

    // Every frame until it has run once (or given up).
    private static void Update()
    {
        if (done)
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
