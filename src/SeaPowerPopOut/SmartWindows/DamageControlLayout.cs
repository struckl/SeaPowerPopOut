using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Noesis;
using SeaPower;
using SeapowerUI;
using UnityEngine;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Unit Status window, Damage Control tab: the game gives the whole tab a fixed minimum height, so in a short window
/// the "Auto Damage Control Status" panel at the bottom is pushed out. This lets the section panels (Aft ... Forward)
/// shrink down to <c>Sections min height</c> pixels instead, so the status panel stays in the window.
/// <para>The tab is a grid with a minimum height; its third row holds the section panels. The grid's minimum is
/// dropped and the row gets the wanted minimum. 0 puts the game's values back.</para>
/// </summary>
internal static class DamageControlLayout
{
    private const int SectionRow = 2;
    private const float RetryEvery = 0.1f;
    private const int MaxTries = 30;

    private sealed class Setup
    {
        public Grid Grid;
        public RowDefinition Row;
        public float GridMinHeight;           // the game's values, to put back
        public float RowMinHeight;
        public float RowMargin;               // top + bottom margin of the element in the section row
    }

    private sealed class Attempt
    {
        public WeakReference<DamageControlView> View;
        public float Next;
        public int Tries;
    }

    private static readonly List<Attempt> Pending = new List<Attempt>();
    private static readonly List<WeakReference<DamageControlView>> Views = new List<WeakReference<DamageControlView>>();
    private static readonly ConditionalWeakTable<DamageControlView, Setup> Done = new ConditionalWeakTable<DamageControlView, Setup>();

    public static bool Apply(Harmony harmony)
    {
        var constructors = AccessTools.GetDeclaredConstructors(typeof(DamageControlView));
        if (constructors.Count == 0)
        {
            ModLog.Warn("Damage Control view not found (game updated?) - its minimum height stays as the game has it.");
            return false;
        }
        foreach (ConstructorInfo constructor in constructors)
            harmony.Patch(constructor, postfix: new HarmonyMethod(typeof(DamageControlLayout), nameof(Created)));
        SmartSettings.SectionsMinHeight.SettingChanged += (s, e) => ApplyToAll();
        return true;
    }

    private static void Created(DamageControlView __instance)
    {
        Views.Add(new WeakReference<DamageControlView>(__instance));
        Pending.Add(new Attempt { View = new WeakReference<DamageControlView>(__instance) });
    }

    /// <summary>Called every frame: tries the views that were created but whose template is not built yet.</summary>
    public static void Update()
    {
        for (int i = Pending.Count - 1; i >= 0; i--)
        {
            Attempt attempt = Pending[i];
            if (Time.unscaledTime < attempt.Next)
                continue;
            attempt.Next = Time.unscaledTime + RetryEvery;
            if (!attempt.View.TryGetTarget(out DamageControlView view) || ++attempt.Tries > MaxTries)
            {
                Pending.RemoveAt(i);
                continue;
            }
            if (!view.IsVisible)
                continue;                              // set up when the tab is shown
            if (Prepare(view))
                Pending.RemoveAt(i);
        }
    }

    /// <summary>True when finished (set up, or nothing to do); false to try again later.</summary>
    private static bool Prepare(DamageControlView view)
    {
        if (Done.TryGetValue(view, out _))
            return true;
        Grid grid = FindRootGrid(view);
        if (grid == null)
            return false;
        if (grid.RowDefinitions.Count <= SectionRow)
        {
            ModLog.Debug("Damage Control: not the expected rows, left alone");
            return true;
        }

        UIElement sections = ElementInRow(grid, SectionRow);
        if (sections == null)
            return false;

        var setup = new Setup
        {
            Grid = grid,
            Row = grid.RowDefinitions[SectionRow],
            GridMinHeight = grid.MinHeight,
            RowMinHeight = grid.RowDefinitions[SectionRow].MinHeight,
            RowMargin = sections is FrameworkElement fe ? fe.Margin.Top + fe.Margin.Bottom : 0f,
        };
        Done.Add(view, setup);
        Set(view, setup);
        return true;
    }

    private static void ApplyToAll()
    {
        for (int i = Views.Count - 1; i >= 0; i--)
        {
            if (!Views[i].TryGetTarget(out DamageControlView view))
            {
                Views.RemoveAt(i);
                continue;
            }
            if (Done.TryGetValue(view, out Setup setup))
                Set(view, setup);
            else if (view.IsVisible)
                Pending.Add(new Attempt { View = new WeakReference<DamageControlView>(view) });
        }
    }

    private static void Set(DamageControlView view, Setup setup)
    {
        float wanted = SmartSettings.SectionsMinHeight.Value;
        if (wanted <= 0f)
        {
            setup.Grid.MinHeight = setup.GridMinHeight;
            setup.Row.MinHeight = setup.RowMinHeight;
            return;
        }
        setup.Grid.MinHeight = 0f;
        setup.Row.MinHeight = wanted / ScreenScale(view) + setup.RowMargin;
        ModLog.Debug($"smart windows: Damage Control sections at least {wanted:0} px");
    }

    /// <summary>The first grid with a minimum height in the top levels of the tab (lists and scrollers are not entered).</summary>
    private static Grid FindRootGrid(DependencyObject view)
    {
        var level = new List<DependencyObject> { view };
        for (int depth = 0; depth <= 5 && level.Count > 0; depth++)
        {
            var next = new List<DependencyObject>();
            foreach (DependencyObject node in level)
            {
                if (node is Grid grid && grid.MinHeight > 0f)
                    return grid;
                if (node is ItemsControl || node is ItemsPresenter || node is ScrollViewer)
                    continue;
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                    next.Add(VisualTreeHelper.GetChild(node, i));
            }
            level = next;
        }
        return null;
    }

    private static UIElement ElementInRow(Grid grid, int row)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(grid); i++)
        {
            if (VisualTreeHelper.GetChild(grid, i) is UIElement element && Grid.GetRow(element) == row)
                return element;
        }
        return null;
    }

    /// <summary>Screen pixels per layout unit at this element (the UI is scaled to the screen's height).</summary>
    private static float ScreenScale(Visual element)
    {
        try
        {
            if (VisualTreeHelper.GetParent(element) != null)
            {
                float scale = (element.PointToScreen(new Point(0f, 100f)).Y - element.PointToScreen(new Point(0f, 0f)).Y) / 100f;
                if (scale > 0.05f && scale < 20f)
                    return scale;
            }
        }
        catch (Exception)
        {
            // fall through to the setting-based value
        }
        float ui = Singleton<OptionsManager>.InstanceExists() ? Singleton<OptionsManager>.Instance.UIScale : 1f;
        return Math.Max(0.05f, ui * Screen.height / 1080f);
    }
}
