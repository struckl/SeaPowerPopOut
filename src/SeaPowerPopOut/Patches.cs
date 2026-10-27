using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SeaPower;
using SeapowerUI;
using UnityEngine;
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
/// <item><c>NoesisView.MousePosition</c> — puts the pointer of the pop-out view where the mouse really is (see
/// <see cref="PointerHooks"/>). Optional: if it's missing, everything else still runs.</item>
/// <item><c>MainGameViewModel.OnStart</c> / <c>GameMain.OnDestroy</c> — mission ready / mission unloading.</item>
/// <item><c>FormationManagerViewModel.SelectedTabIndex</c> — counts every tab, as the TabControl does (see
/// <see cref="FormationTabs"/>). Optional.</item>
/// </list>
/// <para>Two stages. The window hooks must wait until NoesisGUI runs: patching a method JIT-compiles it, which runs
/// its class's static constructor, and <c>WindowHost</c> / <c>Window</c> register Noesis dependency properties
/// there – before Noesis is initialised that is a native crash on startup. <see cref="PopOutScreen"/> applies them
/// once the first game window host exists (the main menu has one).</para>
/// </summary>
internal static class Patches
{
    private sealed class Hook
    {
        public string Type, Method, Prefix, Postfix;
        public Type[] Args;                   // null = unique by name
    }

    private static readonly Hook[] GameHooks =
    {
        new Hook { Type = "SeaPower.MouseControlState", Method = "OnUpdate", Args = Type.EmptyTypes, Postfix = nameof(MouseUpdated) },
        new Hook { Type = "SeapowerUI.ViewModels.MainGameViewModel", Method = "OnStart", Args = Type.EmptyTypes, Postfix = nameof(MissionReady) },
        new Hook { Type = "SeaPower.GameMain", Method = "OnDestroy", Args = Type.EmptyTypes, Prefix = nameof(MissionUnloading) },
    };

    private static readonly Hook[] WindowHooks =
    {
        new Hook { Type = "SeapowerUI.WindowHost", Method = "OpenNewWindow", Prefix = nameof(OpenNewWindow) },
        new Hook { Type = "SeapowerUI.WindowHost", Method = "CanCloseWindow", Postfix = nameof(CanCloseWindow) },
        new Hook { Type = "SeapowerUI.WindowHost", Method = "CloseWindow", Prefix = nameof(CloseWindow) },
        new Hook { Type = "SeapowerUI.Window", Method = "OnInitialized", Args = Type.EmptyTypes, Postfix = nameof(WindowLoaded) },
    };

    // NoesisView turns the mouse position on another monitor into view coordinates with the main window's height
    // (Screen.height), so on a monitor of another height the pointer sits off by the difference – 400 px on a
    // 2560 px portrait monitor next to a 2160 px game window: no resize cursor at the edges, drags jump. Clicks go
    // another way and land right. Without this hook the pop-out still works, just with that offset.
    private static readonly Hook[] PointerHooks =
    {
        new Hook { Type = "NoesisView", Method = "MousePosition", Args = Type.EmptyTypes, Postfix = nameof(MousePosition) },
    };

    // A fix of the game's own formation manager tabs (see FormationTabs), on both screens. Without it everything
    // else still runs.
    private static readonly Hook[] TabHooks =
    {
        new Hook { Type = "SeapowerUI.ViewModels.FormationManagerViewModel", Method = "get_SelectedTabIndex", Args = Type.EmptyTypes, Prefix = nameof(GetTabIndex) },
        new Hook { Type = "SeapowerUI.ViewModels.FormationManagerViewModel", Method = "set_SelectedTabIndex", Args = new[] { typeof(int) }, Prefix = nameof(SetTabIndex) },
    };

    private static Harmony harmony;

    /// <summary>Stage one, at plugin load: hooks on plain game classes.</summary>
    public static bool ApplyGameHooks(Harmony instance)
    {
        harmony = instance;
        if (!Apply(GameHooks))
            return false;
        if (!FormationTabs.Resolve() || !Apply(TabHooks, optional: true))
            ModLog.Warn("Formation manager tab fix not applied (game updated?) - its tabs work as the game has them.");
        return true;
    }

    /// <summary>Stage two, once NoesisGUI is up: hooks on the game's Noesis window classes.</summary>
    public static bool ApplyWindowHooks()
    {
        if (harmony == null || !Apply(WindowHooks))
            return false;
        if (!Apply(PointerHooks, optional: true))
            ModLog.Warn("Pointer fix not applied - on a monitor of another height than the game's the mouse sits off.");
        return true;
    }

    private static bool Apply(Hook[] hooks, bool optional = false)
    {
        var resolved = new List<(Hook hook, MethodBase target)>();
        foreach (Hook hook in hooks)
        {
            Type type = AccessTools.TypeByName(hook.Type);
            MethodBase target = type == null ? null : AccessTools.Method(type, hook.Method, hook.Args);
            if (target == null)
            {
                if (optional)
                    ModLog.Warn($"{hook.Type}.{hook.Method} not found (game updated?).");
                else
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

    private static void MousePosition(NoesisView __instance, ref Vector3 __result) =>
        PopOutScreen.Instance?.FixPointer(__instance, ref __result);

    private static bool GetTabIndex(object __instance, ref int __result) => FormationTabs.Get(__instance, ref __result);

    private static bool SetTabIndex(object __instance, int value) => FormationTabs.Set(__instance, value);

    private static void MissionReady() => PopOutScreen.Instance?.MissionReady();

    private static void MissionUnloading() => PopOutScreen.Instance?.MissionEnded();
}
