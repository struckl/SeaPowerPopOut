using System;
using System.Collections.Generic;
using System.IO;
using SeapowerUI.ViewModels;
using FrameworkElement = Noesis.FrameworkElement;
using UnityEngine;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Remembers the position and size of the windows named in the settings and puts them back when they open again.
/// Several windows of one kind open shifted, so they don't cover each other. Windows on the pop-out monitor are
/// left to <see cref="Layout"/>.
/// </summary>
internal static class WindowMemory
{
    private const float TitleBarHeight = 30f;
    private const float SaveDelay = 1f;

    private static PatternList patterns;
    private static SavedLayouts layouts;
    private static readonly List<Entry> Open = new List<Entry>();
    private static float dirtySince = -1f;

    private sealed class Entry
    {
        public WeakReference<GameWindow> Window;
        public string Pattern;
    }

    public static bool Active => patterns != null;

    /// <summary>The remembered-window pattern of a window (the key of its layout and look), or null.</summary>
    public static string PatternOf(GameWindow window) => Active ? patterns.Match(WindowNames.Of(window)) : null;

    public static void Init()
    {
        if (!SmartSettings.Enabled.Value)
            return;
        Loc.Init(Path.GetDirectoryName(typeof(WindowMemory).Assembly.Location));
        LogCleanup.Run();
        TreeDump.Init();
        patterns = new PatternList(SmartSettings.WindowPatterns.Value);
        layouts = SavedLayouts.Parse(SmartSettings.Layouts.Value);
    }

    /// <summary>Called when a game window has loaded.</summary>
    public static void WindowLoaded(GameWindow window)
    {
        if (!Active || window == null)
            return;
        TreeDump.WindowLoaded(window);
        DesktopMode.WindowLoaded(window);
        UnitReferenceLayout.WindowLoaded(window);
        LoadoutPanelOrder.WindowLoaded(window);
        if (SmartSettings.StatusReference.Value)
            UnitStatusReference.WindowLoaded(window);
        SnapController.Attach(window);
        WindowBehaviors.Attach(window);
        WindowBorder.Apply(window);
        if (window.CanResize)
            SettingsPanel.AddGear(window);      // not on fixed-size dialogs (Confirm Exit, Confirm Task Force ...)
        UpgradeShip.WindowLoaded(window);
        ColorIniFiles.WindowLoaded(window);

        string[] names = WindowNames.Of(window);
        string pattern = patterns.Match(names);
        ModLog.Debug($"window loaded: {string.Join(" / ", names)} -> {pattern ?? "not remembered"}");
        WindowLook.WindowLoaded(window, pattern);
        if (pattern == null)
            return;

        int alreadyOpen = CountOpen(pattern);
        Open.Add(new Entry { Window = new WeakReference<GameWindow>(window), Pattern = pattern });

        // Only the first window of a pattern keeps the saved layout; the shifted ones must not overwrite it.
        bool primary = alreadyOpen == 0;
        if (layouts.TryGet(pattern, out SavedLayout saved) && !FullscreenMap(window))
            Apply(window, saved, alreadyOpen);

        // The map on the whole screen is the game's own mode, not a size the player chose: it is never remembered.
        window.PositionChanged += (s, e) => { if (primary && !FullscreenMap(window)) Remember(window, pattern); };
        window.SizeChanged += (s, e) => { if (primary && !FullscreenMap(window)) Remember(window, pattern); };
        window.WindowMaximized += (s, e) => { if (primary && !FullscreenMap(window)) RememberMaximized(pattern, true); };
        window.WindowUnmaximized += (s, e) => { if (primary && !FullscreenMap(window)) RememberMaximized(pattern, false); };
    }

    /// <summary>Called every frame: writes the layouts to the config a moment after the last change.</summary>
    public static void Update()
    {
        if (!Active)
            return;
        SmartSettings.Flush();
        WindowLook.Update();
        TreeDump.Update();
        UnitReferenceLayout.Update();
        TreeScroller.Update();
        DamageControlLayout.Update();
        TrackState.Update();
        if (SmartSettings.StatusReference.Value)
            UnitStatusReference.Update();
        UnitPanelMenu.Update();
        LoadoutPanelOrder.Update();
        UpgradeShip.Update();
        BackgroundReroll.Update();
        ColorIniFiles.Update();
        ResizeCursors.Update();
        TimePanelDrag.Update();
        DesktopMode.Update();
        if (dirtySince >= 0f && Time.unscaledTime - dirtySince >= SaveDelay)
            Save();
    }

    public static void Save()
    {
        if (!Active)
            return;
        if (!layouts.Dirty)
        {
            dirtySince = -1f;
            SmartSettings.Flush(now: true);
            return;
        }
        SmartSettings.Layouts.Value = layouts.Serialize();
        layouts.MarkSaved();
        dirtySince = -1f;
        SmartSettings.Flush(now: true);
        ModLog.Debug("smart windows: layouts saved");
    }

    /// <summary>The remembered layouts as they are now (a copy, for a desktop snapshot).</summary>
    public static SavedLayouts SnapshotLayouts() => SavedLayouts.Parse(layouts == null ? "" : layouts.Serialize());

    /// <summary>Makes the given layouts the remembered ones and moves the open windows there (desktop switch).</summary>
    public static void ApplyLayouts(SavedLayouts incoming)
    {
        if (!Active)
            return;
        foreach (KeyValuePair<string, SavedLayout> entry in incoming.All)
            layouts.Set(entry.Key, entry.Value);
        if (layouts.Dirty && dirtySince < 0f)
            dirtySince = Time.unscaledTime;

        var done = new HashSet<string>();
        foreach (Entry open in Open)
        {
            if (!open.Window.TryGetTarget(out GameWindow window) || !window.IsLoaded || FullscreenMap(window)
                || !done.Add(open.Pattern))
                continue;
            if (!layouts.TryGet(open.Pattern, out SavedLayout saved))
                continue;
            if (!saved.Maximized && window.Maximized)
                window.Maximized = false;
            Apply(window, saved, 0);
        }
    }

    private static void Apply(GameWindow window, SavedLayout saved, int alreadyOpen)
    {
        // The map and World View windows are anchored to the bottom / right of their (taller than the screen) container.
        // The game applies that anchor in the window's Loaded handler, right AFTER this hook ran (Window.OnInitialized is
        // the first Loaded handler), and takes the position it finds then - ours - as the "gap" to the container's edge:
        // the remembered place was turned into "container height - window height - place", usually below the screen, and
        // remembered again. A window the game rebuilds (the World View when the map goes fullscreen <-> windowed) was
        // gone after that. The remembered place is absolute, so the anchor must not act on it.
        if (window.AnchorBottom || window.AnchorRight)
        {
            ModLog.Debug($"smart windows: anchors of {window.Name} released (bottom {window.AnchorBottom}, right {window.AnchorRight})");
            window.AnchorBottom = false;
            window.AnchorRight = false;
        }
        WinRect screen = HostArea.Of(window);
        float width = float.IsNaN(saved.W) ? window.ActualWidth : saved.W;
        var target = new WinRect(saved.X, saved.Y, width, float.IsNaN(saved.H) ? window.ActualHeight : saved.H);

        float minVisible = SmartSettings.AllowDragOutside.Value ? SmartSettings.MinVisiblePixels.Value : width;
        target = WindowGeometry.Cascade(target, alreadyOpen, SmartSettings.CascadeOffset.Value, screen, minVisible, TitleBarHeight);

        if (!float.IsNaN(target.X)) window.X = target.X;
        if (!float.IsNaN(target.Y)) window.Y = target.Y;
        if (!float.IsNaN(saved.W)) window.Width = saved.W;
        if (!float.IsNaN(saved.H)) window.Height = saved.H;
        if (saved.Maximized && alreadyOpen == 0 && window.CanResize)
            window.Maximized = true;
    }

    private static void RememberMaximized(string pattern, bool maximized)
    {
        layouts.SetMaximized(pattern, maximized);
        if (layouts.Dirty && dirtySince < 0f)
            dirtySince = Time.unscaledTime;
    }

    private const float MinRememberedSize = 40f;

    private static bool FullscreenMap(GameWindow window) =>
        (window.DataContext as SceneMapViewModel ?? (window.Content as FrameworkElement)?.DataContext as SceneMapViewModel)
            ?.IsFullscreen == true;

    private static void Remember(GameWindow window, string pattern)
    {
        if (window.Maximized)
            return;
        // A window that is being built or hidden reports no size: that is not a layout.
        if ((!float.IsNaN(window.Width) && window.Width < MinRememberedSize)
            || (!float.IsNaN(window.Height) && window.Height < MinRememberedSize))
            return;
        // A Unit Reference window with its tree hidden is narrower and further right: save it as if the tree were shown.
        float hiddenTree = UnitReferenceLayout.HiddenTreeOffset(window);
        layouts.Set(pattern, new SavedLayout
        {
            X = window.X - hiddenTree,
            Y = window.Y,
            W = float.IsNaN(window.Width) ? float.NaN : window.Width + hiddenTree,
            H = float.IsNaN(window.Height) ? float.NaN : window.Height,
        });
        if (layouts.Dirty && dirtySince < 0f)
            dirtySince = Time.unscaledTime;
    }

    private static int CountOpen(string pattern)
    {
        int count = 0;
        for (int i = Open.Count - 1; i >= 0; i--)
        {
            if (!Open[i].Window.TryGetTarget(out GameWindow w) || !w.IsLoaded)
            {
                Open.RemoveAt(i);
                continue;
            }
            if (Open[i].Pattern == pattern)
                count++;
        }
        return count;
    }
}
