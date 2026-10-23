using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SeaPower;
using SeapowerUI;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut;

/// <summary>
/// Every game method the mod hooks. All targets are resolved before anything is patched: if a game update renamed
/// one, the mod stays out of the way entirely instead of half-working.
/// <list type="bullet">
/// <item><c>WindowHost.OpenNewWindow / CanCloseWindow / CloseWindow</c> — the main screen's window host learns that
/// a window living on the pop-out screen is open there (the bottom-bar toggles stay lit and close it), and windows
/// remembered as popped out open over there straight away.</item>
/// <item><c>Window.OnInitialized</c> — runs on every window's Loaded: adds the pop-out / pop-in button to the title
/// bar and puts popped-out windows where they were last time.</item>
/// <item><c>MouseControlState.OnUpdate</c> — while the mouse is on the pop-out monitor it counts as "over the UI",
/// so a click there doesn't also select, deselect or order something in the world.</item>
/// <item><c>MainGameViewModel.OnStart</c> / <c>GameMain.OnDestroy</c> — mission ready / mission unloading.</item>
/// </list>
/// </summary>
internal static class Patches
{
    private sealed class Hook
    {
        public string Type, Method, Prefix, Postfix;
    }

    private static readonly Hook[] Hooks =
    {
        new Hook { Type = "SeapowerUI.WindowHost", Method = "OpenNewWindow", Prefix = nameof(OpenNewWindow) },
        new Hook { Type = "SeapowerUI.WindowHost", Method = "CanCloseWindow", Postfix = nameof(CanCloseWindow) },
        new Hook { Type = "SeapowerUI.WindowHost", Method = "CloseWindow", Prefix = nameof(CloseWindow) },
        new Hook { Type = "SeapowerUI.Window", Method = "OnInitialized", Postfix = nameof(WindowLoaded) },
        new Hook { Type = "SeaPower.MouseControlState", Method = "OnUpdate", Postfix = nameof(MouseUpdated) },
        new Hook { Type = "SeapowerUI.ViewModels.MainGameViewModel", Method = "OnStart", Postfix = nameof(MissionReady) },
        new Hook { Type = "SeaPower.GameMain", Method = "OnDestroy", Prefix = nameof(MissionUnloading) },
    };

    public static bool Apply(Harmony harmony)
    {
        var resolved = new List<(Hook hook, MethodBase target)>();
        foreach (Hook hook in Hooks)
        {
            Type type = AccessTools.TypeByName(hook.Type);
            MethodBase target = type == null ? null : AccessTools.Method(type, hook.Method, Type.EmptyTypes)
                                                      ?? AccessTools.Method(type, hook.Method);
            if (target == null)
            {
                ModLog.Error($"{hook.Type}.{hook.Method} not found (game updated?) - mod inactive.");
                return false;
            }
            resolved.Add((hook, target));
        }

        foreach ((Hook hook, MethodBase target) in resolved)
        {
            harmony.Patch(target,
                prefix: hook.Prefix == null ? null : new HarmonyMethod(AccessTools.Method(typeof(Patches), hook.Prefix)),
                postfix: hook.Postfix == null ? null : new HarmonyMethod(AccessTools.Method(typeof(Patches), hook.Postfix)));
        }
        return true;
    }

    private static bool OpenNewWindow(WindowHost __instance, ExecutedCustomCommandArgs args) =>
        PopOutScreen.Instance?.HostOpening(__instance, args.Parameter) ?? true;

    private static void CanCloseWindow(WindowHost __instance, CanExecuteCustomCommandArgs args, ref bool __result)
    {
        if (!__result && PopOutScreen.Instance != null && PopOutScreen.Instance.HoldsFor(__instance, args.Parameter))
            __result = true;
    }

    private static bool CloseWindow(WindowHost __instance, ExecutedCustomCommandArgs args) =>
        PopOutScreen.Instance?.HostClosing(__instance, args.Parameter) ?? true;

    private static void WindowLoaded(GameWindow __instance) => PopOutScreen.Instance?.Decorate(__instance);

    private static void MouseUpdated(MouseControlState __instance) => PopOutScreen.Instance?.ClaimMouse(__instance);

    private static void MissionReady() => PopOutScreen.Instance?.MissionReady();

    private static void MissionUnloading() => PopOutScreen.Instance?.MissionEnded();
}
