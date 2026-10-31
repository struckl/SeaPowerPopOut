using System;
using System.Collections.Generic;
using HarmonyLib;
using Noesis;
using SeapowerUI;
using SeapowerUI.ViewModels;
using UnityEngine;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// The mission editor's file pickers show the names of *.ini files in a colour of their own (light blue by default), so
/// missions stand out between the folders and other files. The list is virtualised: rows come and go while it scrolls,
/// so the text blocks are looked at every 0.1 s and the colour put on the ones that show an ini file.
/// </summary>
internal static class ColorIniFiles
{
    private const float PickerCheckEvery = 0.4f;
    private const float PickerGiveUpAfter = 5f;
    private const float WalkEvery = 0.1f;
    private const int WalkBudget = 6000;
    private const string Extension = ".ini";

    private static readonly List<Tracked> All = new List<Tracked>();
    private static System.Reflection.MethodInfo pickerContext;
    private static SolidColorBrush brush;
    private static byte brushA, brushR, brushG, brushB;

    private sealed class Tracked
    {
        public WeakReference<GameWindow> Window;
        public WeakReference<ListView> List;
        public float Born;
        public float NextCheck;
        public float NextWalk;
        public bool Known;
        public bool Picker;
    }

    public static bool Register()
    {
        pickerContext = AccessTools.PropertyGetter(typeof(FilePickerViewModel), "Context");
        if (pickerContext == null)
        {
            ModLog.Warn("FilePickerViewModel.Context not found (game updated?) - ini files are not coloured.");
            return false;
        }
        if (!ColorText.TryParse(SmartSettings.IniColor.Value, out brushA, out brushR, out brushG, out brushB))
            ColorText.TryParse((string)SmartSettings.IniColor.DefaultValue, out brushA, out brushR, out brushG, out brushB);
        return true;
    }

    /// <summary>Called when a game window has loaded.</summary>
    public static void WindowLoaded(GameWindow window)
    {
        if (pickerContext == null || window == null)
            return;
        All.Add(new Tracked { Window = new WeakReference<GameWindow>(window), Born = Time.unscaledTime });
    }

    /// <summary>Called every frame.</summary>
    public static void Update()
    {
        if (pickerContext == null || All.Count == 0)
            return;
        float now = Time.unscaledTime;
        for (int i = All.Count - 1; i >= 0; i--)
        {
            Tracked t = All[i];
            if (!t.Window.TryGetTarget(out GameWindow window) || !window.IsLoaded)
            {
                All.RemoveAt(i);
                continue;
            }
            try
            {
                if (!Step(t, window, now))
                    All.RemoveAt(i);
            }
            catch (Exception ex)
            {
                ModLog.Debug($"ini colour: {ex.Message}");
                All.RemoveAt(i);
            }
        }
    }

    /// <summary>False when the window is no picker of the mission editor (or is gone).</summary>
    private static bool Step(Tracked t, GameWindow window, float now)
    {
        if (!t.Known)
        {
            if (now < t.NextCheck)
                return true;
            t.NextCheck = now + PickerCheckEvery;
            FilePickerViewModel picker = PickerOf(window);
            if (picker == null)
                return now - t.Born < PickerGiveUpAfter;     // the data context may still be on its way
            t.Known = true;
            t.Picker = pickerContext.Invoke(picker, null) is MissionEditorView;
            if (!t.Picker)
                return false;
        }
        if (!window.IsVisible || now < t.NextWalk)
            return true;
        t.NextWalk = now + WalkEvery;

        ListView list = null;
        if (t.List == null || !t.List.TryGetTarget(out list) || VisualTreeHelper.GetParent(list) == null)
        {
            list = FindList(window);
            t.List = list == null ? null : new WeakReference<ListView>(list);
            if (list == null)
                return true;
        }
        Colour(list);
        return true;
    }

    private static FilePickerViewModel PickerOf(GameWindow window)
    {
        object content = window.Content;
        return window.DataContext as FilePickerViewModel
            ?? content as FilePickerViewModel
            ?? (content as FrameworkElement)?.DataContext as FilePickerViewModel;
    }

    private static ListView FindList(DependencyObject root)
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        for (int budget = 20000; stack.Count > 0 && budget > 0; budget--)
        {
            DependencyObject node = stack.Pop();
            if (node is ListView found)
                return found;
            for (int i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--)
                stack.Push(VisualTreeHelper.GetChild(node, i));
        }
        return null;
    }

    private static void Colour(ListView list)
    {
        brush ??= new SolidColorBrush(Noesis.Color.FromArgb(brushA, brushR, brushG, brushB));
        var stack = new Stack<DependencyObject>();
        stack.Push(list);
        for (int budget = WalkBudget; stack.Count > 0 && budget > 0; budget--)
        {
            DependencyObject node = stack.Pop();
            if (node is TextBlock text)
            {
                bool ini = text.Text != null && text.Text.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);
                bool ours = IsOurs(text.Foreground);
                if (ini && !ours)
                    text.Foreground = brush;
                else if (!ini && ours)        // the row was recycled for another file
                    text.ClearValue(TextBlock.ForegroundProperty);
                continue;
            }
            for (int i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--)
            {
                DependencyObject child = VisualTreeHelper.GetChild(node, i);
                if (child != null)
                    stack.Push(child);
            }
        }
    }

    private static bool IsOurs(Brush foreground)
    {
        if (ReferenceEquals(foreground, brush))
            return true;
        if (!(foreground is SolidColorBrush solid))
            return false;
        Noesis.Color c = solid.Color;
        return c.A == brushA && c.R == brushR && c.G == brushG && c.B == brushB;
    }
}
