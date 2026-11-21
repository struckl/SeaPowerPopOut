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
/// Every game method the mod hooks, declared by feature in Rig.Hooks (R.A.T.S. Rigging): a feature's targets are all resolved
/// before it is patched, and a required one that is missing leaves the mod off.
/// - <c>WindowHost.OpenNewWindow / CanCloseWindow / CloseWindow</c> - the main screen's window host learns that
/// a window living on the pop-out screen is open there (the bottom-bar toggles stay lit and close it), and windows
/// remembered as popped out open over there straight away.
/// - <c>Window.OnInitialized</c> - runs on every window's Loaded: adds the pop-out / pop-in button to the title
/// bar and puts popped-out windows where they were last time.
/// - <c>MouseControlState.OnUpdate</c> - while the mouse is on the pop-out monitor it counts as "over the UI",
/// so a click there doesn't also select, deselect or order something in the world.
/// - <c>NoesisView.MousePosition</c> - puts the pointer of views on other monitors where the mouse really is (see
/// <see cref="PointerHooks"/>). Optional: if it's missing, everything else still runs.
/// - <c>MainGameViewModel.OnStart</c> / <c>GameMain.OnDestroy</c> - mission ready / mission unloading (Rig.Mission).
/// - <c>FormationManagerViewModel.SelectedTabIndex</c> - counts every tab, as the TabControl does (see
/// <see cref="FormationTabs"/>). Optional.
/// <para>Two stages. The window hooks must wait until NoesisGUI runs: patching a method JIT-compiles it, which runs
/// its class's static constructor, and <c>WindowHost</c> / <c>Window</c> register Noesis dependency properties
/// there - before Noesis is initialised that is a native crash on startup. The host (RigHost) applies them
/// once the first game window host exists (the main menu has one).</para>
/// </summary>
internal static class Patches
{
    /// <summary>The window hooks' feature name: <see cref="PopOutScreen"/> goes on from there once they are in.</summary>
    public const string WindowsFeature = "Windows";

    /// <summary>Declares every hook (Rig.Start calls it at plugin load).</summary>
    public static void Declare()
    {
        Rig.Mission.Declare();
        Rig.Hooks.Add("Mouse", HookStage.Load, "mod inactive")
            .Hook("SeaPower.MouseControlState", "OnUpdate", Type.EmptyTypes, typeof(Patches), postfix: nameof(MouseUpdated));

        // A fix of the game's own formation manager tabs (see FormationTabs), on both screens. Without it everything
        // else still runs.
        if (FormationTabs.Resolve())
            Rig.Hooks.Add("Formation tabs", HookStage.Load, "its tabs work as the game has them", optional: true)
                .Hook(FormationTabs.TypeName, "get_SelectedTabIndex", Type.EmptyTypes, typeof(Patches), prefix: nameof(GetTabIndex))
                .Hook(FormationTabs.TypeName, "set_SelectedTabIndex", new[] { typeof(int) }, typeof(Patches), prefix: nameof(SetTabIndex));
        else
            Rig.Log.Warn("Formation manager tab fix not applied (game updated?) - its tabs work as the game has them.");

        Rig.Hooks.Add(WindowsFeature, HookStage.AfterNoesis, "pop-out inactive")
            .Hook("SeapowerUI.WindowHost", "OpenNewWindow", null, typeof(Patches), prefix: nameof(OpenNewWindow))
            .Hook("SeapowerUI.WindowHost", "CanCloseWindow", null, typeof(Patches), postfix: nameof(CanCloseWindow))
            .Hook("SeapowerUI.WindowHost", "CloseWindow", null, typeof(Patches), prefix: nameof(CloseWindow))
            .Hook("SeapowerUI.Window", "OnInitialized", Type.EmptyTypes, typeof(Patches), postfix: nameof(WindowLoaded));

        // NoesisView flips the mouse position of another monitor with its camera's pixelHeight, which is not that
        // monitor's height when it differs from the game window's (or Windows scales it differently): the pointer sits
        // off - no resize cursor at the edges, drags jump, tooltips far from the cursor. Clicks go another way and land
        // right. Fixed for every view on another monitor (pop-out screen and the game's own second-monitor map). Without
        // this hook everything else still runs.
        Rig.Hooks.Add("Pointer", HookStage.AfterNoesis, "on a monitor of another height than the game's the mouse sits off", optional: true)
            .Hook("NoesisView", "MousePosition", Type.EmptyTypes, typeof(Patches), postfix: nameof(MousePosition));
    }

    // From the moment the game closes nothing here touches Noesis (see Rig.Mod.Quitting): the game's own code runs as without the mod.
    private static bool OpenNewWindow(WindowHost __instance, ExecutedCustomCommandArgs args) =>
        Rig.Mod.Quitting || (PopOutScreen.Instance?.HostOpening(__instance, args.Parameter) ?? true);

    private static void CanCloseWindow(WindowHost __instance, CanExecuteCustomCommandArgs args, ref bool __result)
    {
        if (!__result && !Rig.Mod.Quitting && PopOutScreen.Instance != null && PopOutScreen.Instance.HoldsFor(__instance, args.Parameter))
            __result = true;
    }

    private static bool CloseWindow(WindowHost __instance, ExecutedCustomCommandArgs args) =>
        Rig.Mod.Quitting || (PopOutScreen.Instance?.HostClosing(__instance, args.Parameter) ?? true);

    private static void WindowLoaded(GameWindow __instance)
    {
        if (Rig.Mod.Quitting)
            return;
        PopOutScreen.Instance?.Decorate(__instance);
        SmartWindows.SmartFeatures.WindowLoaded(__instance, PopOutScreen.Instance == null || PopOutScreen.Instance.OnMainScreen(__instance));
    }

    private static void MouseUpdated(MouseControlState __instance) => PopOutScreen.Instance?.ClaimMouse(__instance);

    private static void MousePosition(NoesisView __instance, ref Vector3 __result)
    {
        if (!Rig.Mod.Quitting)
            PointerFix.Apply(__instance, ref __result);
    }

    private static bool GetTabIndex(object __instance, ref int __result) => FormationTabs.Get(__instance, ref __result);

    private static bool SetTabIndex(object __instance, int value) => FormationTabs.Set(__instance, value);
}
