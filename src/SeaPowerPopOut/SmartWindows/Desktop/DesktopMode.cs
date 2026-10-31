using System;
using System.Reflection;
using HarmonyLib;
using Noesis;
using SeaPower;
using SeapowerUI;
using SeapowerUI.ViewModels;
using UnityEngine;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Desktop mode, the conductor. Behind one master setting (off by default) the game's map becomes an ordinary window
/// (the Tactical Display), the 3D world a window of its own (the World View, drawn from a texture), the screen behind
/// them a plain backdrop, and the game's minimap key switches between desktops of window layouts. This class finds the
/// two windows, starts the desktops once per mission and runs the per-frame steps of the parts; the parts are
/// <see cref="WorldViewTexture"/>, <see cref="DesktopBackdrop"/>, <see cref="DesktopSets"/>, <see cref="WorldViewKey"/>
/// and the hooks in <see cref="DesktopPatches"/>.
/// </summary>
internal static class DesktopMode
{
    private const float FindInterval = 0.25f;
    private const float SettleSeconds = 10f;

    private static readonly MethodInfo NotifyPip = AccessTools.Method(typeof(MainGameViewModel), "NotifyPipChanged");

    private static bool hooked;
    private static MainGameViewModel missionVm;
    private static bool mapKnown;
    private static WeakReference<GameWindow> worldWindow;
    private static WeakReference<GameWindow> mapWindow;
    private static Pip3DView pipView;
    private static float nextFind;
    private static float startedAt;

    /// <summary>Desktop mode is switched on and its hooks are in place.</summary>
    public static bool On => hooked && SmartSettings.DesktopMode.Value;

    /// <summary>The desktops were started in this mission (the map window is there).</summary>
    public static bool Started { get; private set; }

    public static void Apply(Harmony harmony)
    {
        if (!SmartSettings.DesktopMode.Value)
            return;
        try
        {
            hooked = DesktopPatches.Apply(harmony);
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Desktop mode not available: {ex.Message}");
        }
        if (hooked)
            ModLog.Info("Desktop mode is on: the 3D world is drawn into the World View window.");
    }

    /// <summary>A game window finished loading.</summary>
    public static void WindowLoaded(GameWindow window)
    {
        if (!On)
            return;
        try
        {
            if (IsMap(window))
            {
                ReleaseAnchors(window, "map");
                mapWindow = new WeakReference<GameWindow>(window);
                MapWindowLoaded();
            }
            else if (IsWorldView(window))
            {
                ReleaseAnchors(window, "World View");
                worldWindow = new WeakReference<GameWindow>(window);
                pipView = null;
                nextFind = 0f;
            }
        }
        catch (Exception ex)
        {
            ModLog.Debug($"desktop mode, window: {ex.Message}");
        }
    }

    /// <summary>Every frame.</summary>
    public static void Update()
    {
        if (!On)
            return;
        try
        {
            MainGameViewModel vm = DesktopGame.Vm;
            bool mission = vm != null && vm.Map != null;
            // No backdrop when the game's own World View is on another monitor: the map covers this screen.
            DesktopBackdrop.Update(mission && !vm.WorldViewDetached);
            if (!mission)
            {
                if (WorldViewTexture.Holds)
                    WorldViewTexture.Release(DesktopGame.Cameras);
                Reset(null);
                return;
            }
            if (vm != missionVm)
                Reset(vm);
            // The map window often loads before the mission's view model has its map: catch up once it has.
            if (!mapKnown && mapWindow != null && mapWindow.TryGetTarget(out GameWindow map) && map.IsLoaded)
                MapWindowLoaded();
            if (!Started && mapKnown)
            {
                Started = true;
                startedAt = Time.unscaledTime;
                DesktopSets.Start();
            }
            // While the mission settles, the game may put the map back on the whole screen: keep it a window. Not with the
            // World View on another monitor: then the game holds the map on the whole screen every frame (its design).
            if (Started && !vm.WorldViewDetached && Time.unscaledTime - startedAt < SettleSeconds && vm.Map.IsFullscreen)
            {
                vm.Map.IsFullscreen = false;
                NotifyPip?.Invoke(vm, null);
                ModLog.Debug("desktop mode: map put back into its window");
            }
            KeepWorldViewOnScreen(vm);
            Pip3DView view = FindView();
            if (view != null)
                WorldViewTexture.Step(view, DesktopGame.Cameras);
            WorldViewKey.Update();
        }
        catch (Exception ex)
        {
            ModLog.Debug($"desktop mode, update: {ex.Message}");
        }
    }

    private static void Reset(MainGameViewModel vm)
    {
        missionVm = vm;
        mapKnown = false;
        Started = false;
        worldWindow = null;
        pipView = null;
    }

    private static void MapWindowLoaded()
    {
        MainGameViewModel vm = DesktopGame.Vm;
        if (vm?.Map == null)
            return;
        // The map is an ordinary window here; the world view then stays up through the ShowPip3D hook.
        if (!mapKnown && vm.Map.IsFullscreen && !vm.WorldViewDetached)
        {
            vm.Map.IsFullscreen = false;
            NotifyPip?.Invoke(vm, null);
        }
        mapKnown = true;
        ModLog.Debug($"desktop mode: map window found (world view detached: {vm.WorldViewDetached}, open: {vm.PipOpen})");
    }

    /// <summary>
    /// The game anchors the map and World View windows to the bottom of their (taller than the screen) viewport, which
    /// turns any position into "bottom edge", below the screen. As desktop windows they are placed like the others.
    /// </summary>
    private static void ReleaseAnchors(GameWindow window, string what)
    {
        if (DesktopGame.Vm?.WorldViewDetached ?? false)
            return;
        if (!window.AnchorBottom && !window.AnchorRight)
            return;
        ModLog.Debug($"desktop mode: {what} window anchors (bottom {window.AnchorBottom}, right {window.AnchorRight}) released");
        window.AnchorBottom = false;
        window.AnchorRight = false;
    }

    /// <summary>
    /// The World View window lives in a container taller than the screen (the game's second-display layout), so a
    /// position can put it below the visible area: pull it back so its title bar is on the screen.
    /// </summary>
    private static void KeepWorldViewOnScreen(MainGameViewModel vm)
    {
        if (vm.WorldViewDetached || worldWindow == null || !worldWindow.TryGetTarget(out GameWindow window)
            || !window.IsLoaded || window.Maximized)
            return;
        WinRect screen = HostArea.OfScreen();
        float w = float.IsNaN(window.Width) ? window.ActualWidth : window.Width;
        float h = float.IsNaN(window.Height) ? window.ActualHeight : window.Height;
        var now = new WinRect(window.X, window.Y, w, h);
        // Only a window that is mostly off the screen is moved; then all of it comes back (as far as it fits).
        bool mostlyOff = now.Y > screen.Bottom - h / 2f || now.X > screen.Right - w / 2f || now.Y < -h / 2f || now.X < -w / 2f;
        if (!mostlyOff)
            return;
        // Where the active desktop wants it, if that is on the screen; else as far up as it fits.
        WinRect want = now;
        if (WindowMemory.SnapshotLayouts().TryGet(PatternList.Squash("World View"), out SavedLayout saved)
            && !float.IsNaN(saved.X) && !float.IsNaN(saved.Y) && saved.Y < screen.Bottom - 30f)
            want = new WinRect(saved.X, saved.Y, w, h);
        var fixedRect = new WinRect(
            Math.Max(screen.X, Math.Min(want.X, screen.Right - w)),
            Math.Max(screen.Y, Math.Min(want.Y, screen.Bottom - h)), w, h);
        window.X = fixedRect.X;
        window.Y = fixedRect.Y;
        ModLog.Debug($"desktop mode: World View window moved onto the screen ({now.X:0},{now.Y:0} -> {fixedRect.X:0},{fixedRect.Y:0})");
    }

    private static Pip3DView FindView()
    {
        if (pipView != null && pipView.IsLoaded)
            return pipView;
        if (worldWindow == null || !worldWindow.TryGetTarget(out GameWindow window) || !window.IsLoaded
            || Time.unscaledTime < nextFind)
            return null;
        nextFind = Time.unscaledTime + FindInterval;
        pipView = Descendant<Pip3DView>(window, 0);
        return pipView;
    }

    private static T Descendant<T>(DependencyObject node, int depth) where T : class
    {
        if (node is T found)
            return found;
        if (depth > 40)
            return null;
        for (int i = 0, n = VisualTreeHelper.GetChildrenCount(node); i < n; i++)
        {
            T hit = Descendant<T>(VisualTreeHelper.GetChild(node, i), depth + 1);
            if (hit != null)
                return hit;
        }
        return null;
    }

    private static bool IsMap(GameWindow window) =>
        window.DataContext is SceneMapViewModel || (window.Content as FrameworkElement)?.DataContext is SceneMapViewModel;

    private static bool IsWorldView(GameWindow window)
    {
        foreach (string name in WindowNames.Of(window))
        {
            string squashed = name.Replace(" ", "");
            if (squashed.Equals("WorldView", StringComparison.OrdinalIgnoreCase)
                || squashed.Equals("WorldViewWindowViewModel", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
