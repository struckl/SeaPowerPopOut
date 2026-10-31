using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using Noesis;
using SeaPower;
using SeapowerUI;
using SeapowerUI.ViewModels;
using UnityEngine;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// The game hooks of desktop mode. The first group is what the rendering needs: if one of them is missing in this
/// game build none is applied and desktop mode stays off. The second group refines it (mouse, key, bookkeeping) and
/// each hook there is optional. Every hook returns at once while desktop mode is off or no mission runs.
/// </summary>
internal static class DesktopPatches
{
    private static readonly AccessTools.FieldRef<MouseControlState, MouseControlState.State> MouseState =
        AccessTools.FieldRefAccess<MouseControlState, MouseControlState.State>("_currentState");

    private static bool Live => DesktopMode.On && DesktopGame.InMission;

    public static bool Apply(Harmony harmony)
    {
        MethodBase showPip = AccessTools.PropertyGetter(typeof(MainGameViewModel), nameof(MainGameViewModel.ShowPip3D));
        MethodBase paused = AccessTools.Method(typeof(CameraManager), nameof(CameraManager.SetRenderingPaused));
        MethodBase rects = AccessTools.Method(typeof(CameraManager), "SetCameraRects");
        MethodBase render = AccessTools.Method(typeof(Pip3DView), "OnRender");
        if (showPip == null || paused == null || rects == null || render == null || MouseState == null
            || AccessTools.Method(typeof(CameraManager), nameof(CameraManager.RequestPipFrame)) == null
            || AccessTools.Field(typeof(CameraManager), "_camera") == null)
        {
            ModLog.Warn("Desktop mode not available: the game's world view members were not found (game updated?).");
            return false;
        }

        harmony.Patch(showPip, postfix: Hook(nameof(ShowPipPostfix)));
        harmony.Patch(paused, prefix: Hook(nameof(PausedPrefix)));
        harmony.Patch(rects, prefix: Hook(nameof(RectsPrefix)));
        harmony.Patch(render, prefix: Hook(nameof(RenderPrefix)));

        Optional(harmony, AccessTools.Method(typeof(Pip3DView), nameof(Pip3DView.RemapMouse)), "mouse mapping", prefix: nameof(RemapPrefix));
        Optional(harmony, AccessTools.Method(typeof(MouseControlState), nameof(MouseControlState.OnUpdate)), "mouse state", postfix: nameof(MouseStatePostfix));
        Optional(harmony, AccessTools.Method(typeof(MainGameViewModel), nameof(MainGameViewModel.ToggleMapFullscreen)), "desktop switch key", prefix: nameof(ToggleMapPrefix));
        Optional(harmony, AccessTools.PropertySetter(typeof(MainGameViewModel), nameof(MainGameViewModel.PipOpen)), "World View memory", postfix: nameof(PipOpenPostfix));
        Optional(harmony, AccessTools.Method(typeof(BlitToPipImage), "OnRenderImage"), "pip copy", prefix: nameof(BlitPrefix));
        return true;
    }

    private static HarmonyMethod Hook(string name) => new HarmonyMethod(typeof(DesktopPatches), name);

    private static void Optional(Harmony harmony, MethodBase target, string what, string prefix = null, string postfix = null)
    {
        if (target == null)
        {
            ModLog.Warn($"Desktop mode: {what} hook not found (game updated?) - that part is left as the game has it.");
            return;
        }
        harmony.Patch(target, prefix: prefix == null ? null : Hook(prefix), postfix: postfix == null ? null : Hook(postfix));
    }

    // ---- hooks ----------------------------------------------------------------------------------------------------

    /// <summary>The map is a window, not fullscreen: the world view still counts as shown while it is open.</summary>
    private static void ShowPipPostfix(MainGameViewModel __instance, ref bool __result)
    {
        if (__result || !Live)
            return;
        try
        {
            if (!__instance.WorldViewDetached && __instance.PipOpen)
                __result = true;
        }
        catch (Exception ex)
        {
            ModLog.Debug($"desktop mode, ShowPip3D: {ex.Message}");
        }
    }

    private static void PausedPrefix(ref bool paused)
    {
        if (!Live)
            return;
        MainGameViewModel vm = DesktopGame.Vm;
        paused = !vm.ShowPip3D && !DesktopGame.PhotoMode;
    }

    private static bool RectsPrefix(UnityEngine.Rect rect) => !Live || WorldViewTexture.AllowRect(rect);

    private static bool RenderPrefix(Pip3DView __instance, DrawingContext context)
    {
        if (!Live)
            return true;
        try
        {
            WorldViewTexture.Render(__instance, context);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"desktop mode, World View draw: {ex.Message}");
        }
        return false;
    }

    private static bool RemapPrefix(Vector3 mousePos, ref Vector3 __result)
    {
        if (!Live)
            return true;
        __result = WorldViewTexture.RemapMouse(mousePos);
        return false;
    }

    /// <summary>A mouse button pressed outside the World View never reaches the world (selecting, ordering ...).</summary>
    private static void MouseStatePostfix(MouseControlState __instance)
    {
        if (!Live || DesktopGame.PhotoMode)
            return;
        try
        {
            if (WorldViewTexture.MouseHandedToWorld())
                return;
            MouseState(__instance) = MouseControlState.State.UI;
            DesktopGame.Cameras?.setMouseInteractionFlag(false);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"desktop mode, mouse state: {ex.Message}");
        }
    }

    /// <summary>The game's "Toggle fullscreen minimap" key (called from its input handler) switches desktops instead.</summary>
    private static bool ToggleMapPrefix()
    {
        if (!Live || !DesktopMode.Started)
            return true;
        try
        {
            var trace = new StackTrace(false);
            int frames = Math.Min(trace.FrameCount, 8);
            for (int i = 1; i < frames; i++)
            {
                if (trace.GetFrame(i)?.GetMethod()?.DeclaringType == typeof(InputHandler))
                {
                    DesktopSets.Switch();
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            ModLog.Debug($"desktop mode, switch key: {ex.Message}");
        }
        return true;
    }

    private static void PipOpenPostfix(bool value)
    {
        if (Live && DesktopMode.Started)
            DesktopSets.WorldViewChanged(value);
    }

    /// <summary>The game copies the pip's camera frame into a small image of its own; the window shows our texture, so a plain pass-through.</summary>
    private static bool BlitPrefix(MonoBehaviour __instance, RenderTexture source, RenderTexture destination)
    {
        if (!Live || source == null)
            return true;
        try
        {
            CameraManager cm = DesktopGame.Cameras;
            Camera camera = __instance.GetComponent<Camera>();
            if (cm != null && cm.IsPipActive && camera != null && cm.IsPipSource(camera))
            {
                Graphics.Blit(source, destination);
                return false;
            }
        }
        catch (Exception ex)
        {
            ModLog.Debug($"desktop mode, pip copy: {ex.Message}");
        }
        return true;
    }
}
