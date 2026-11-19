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
    public static readonly Feature Feature = new Feature
    {
        Switch = () => SmartSettings.DesktopMode.Value,
        LiveSwitch = () => SmartSettings.DesktopMode.Value,
        Hook = Apply,
        WindowLoaded = WindowLoaded,
        Update = Update,
    };

    private const float FindInterval = 0.25f;
    private const float SettleSeconds = 10f;

    private static readonly MethodInfo NotifyPip = AccessTools.Method(typeof(MainGameViewModel), "NotifyPipChanged");

    private static MainGameViewModel missionVm;
    private static bool mapKnown;
    private static WeakReference<GameWindow> worldWindow;
    private static WeakReference<GameWindow> mapWindow;
    private static Pip3DView pipView;
    private static float nextFind;
    private static float startedAt;

    /// <summary>Desktop mode is switched on and its hooks are in place.</summary>
    public static bool On => Feature.Live;

    /// <summary>The desktops were started in this mission (the map window is there).</summary>
    public static bool Started { get; private set; }

    private static bool Apply(Harmony harmony)
    {
        bool hooked = false;
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
        return hooked;
    }

    private static void WindowLoaded(GameWindow window)
    {
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

    private static void Update()
    {
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
            StartWhenMapKnown();
            KeepMapWindowed(vm);
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

    /// <summary>The map window often loads before the mission's view model has its map: catch up once it has, then start the desktops.</summary>
    private static void StartWhenMapKnown()
    {
        if (!mapKnown && mapWindow != null && mapWindow.TryGetTarget(out GameWindow map) && map.IsLoaded)
            MapWindowLoaded();
        if (Started || !mapKnown)
            return;
        Started = true;
        startedAt = Time.unscaledTime;
        DesktopSets.Start();
    }

    /// <summary>
    /// While the mission settles, the game may put the map back on the whole screen: keep it a window. Not with the World
    /// View on another monitor: then the game holds the map on the whole screen every frame (its design).
    /// </summary>
    private static void KeepMapWindowed(MainGameViewModel vm)
    {
        if (!Started || vm.WorldViewDetached || Time.unscaledTime - startedAt >= SettleSeconds || !vm.Map.IsFullscreen)
            return;
        vm.Map.IsFullscreen = false;
        NotifyPip?.Invoke(vm, null);
        ModLog.Debug("desktop mode: map put back into its window");
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
        bool bottom = window.AnchorBottom, right = window.AnchorRight;
        if (HostArea.ReleaseAnchors(window))
            ModLog.Debug($"desktop mode: {what} window anchors (bottom {bottom}, right {right}) released");
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
        if (!MostlyOff(now, screen))
            return;
        WinRect want = WantedOnScreen(now, screen);
        var fixedRect = new WinRect(
            Math.Max(screen.X, Math.Min(want.X, screen.Right - w)),
            Math.Max(screen.Y, Math.Min(want.Y, screen.Bottom - h)), w, h);
        window.X = fixedRect.X;
        window.Y = fixedRect.Y;
        ModLog.Debug($"desktop mode: World View window moved onto the screen ({now.X:0},{now.Y:0} -> {fixedRect.X:0},{fixedRect.Y:0})");
    }

    /// <summary>Only a window that is mostly off the screen is moved; then all of it comes back (as far as it fits).</summary>
    private static bool MostlyOff(WinRect r, WinRect screen) =>
        r.Y > screen.Bottom - r.H / 2f || r.X > screen.Right - r.W / 2f || r.Y < -r.H / 2f || r.X < -r.W / 2f;

    /// <summary>Where the active desktop wants the window, if that is on the screen; else where it is (pulled up as far as it fits).</summary>
    private static WinRect WantedOnScreen(WinRect now, WinRect screen)
    {
        if (WindowMemory.SnapshotLayouts().TryGet(PatternList.Squash(GameWindows.WorldView), out SavedLayout saved)
            && !float.IsNaN(saved.X) && !float.IsNaN(saved.Y) && saved.Y < screen.Bottom - HostArea.TitleBarReach)
            return new WinRect(saved.X, saved.Y, now.W, now.H);
        return now;
    }

    private static Pip3DView FindView()
    {
        if (pipView != null && pipView.IsLoaded)
            return pipView;
        if (worldWindow == null || !worldWindow.TryGetTarget(out GameWindow window) || !window.IsLoaded
            || Time.unscaledTime < nextFind)
            return null;
        nextFind = Time.unscaledTime + FindInterval;
        pipView = VisualSearch.First<Pip3DView>(window);
        return pipView;
    }

    private static bool IsMap(GameWindow window) => GameWindows.MapOf(window) != null;

    private static bool IsWorldView(GameWindow window)
    {
        foreach (string name in WindowNames.Of(window))
        {
            string squashed = PatternList.Squash(name);
            if (squashed == PatternList.Squash(GameWindows.WorldView) || squashed == PatternList.Squash(GameWindows.WorldViewModel))
                return true;
        }
        return false;
    }
}
