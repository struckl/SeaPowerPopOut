using System;
using System.Collections.Generic;
using System.ComponentModel;
using Noesis;
using SeapowerUI;
using SeapowerUI.ViewModels;
using UnityEngine;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Unit Reference window layout: a draggable splitter at the right edge of the unit tree, whose width is kept between
/// runs, and the tree's shown / hidden state kept as well.
/// <para>The window's template is only filled a moment after it loads, so the setup is tried a few times
/// (<see cref="Update"/> drives that) until the tree and its grid are there.</para>
/// </summary>
internal static class UnitReferenceLayout
{
    private const float MinTree = 100f;
    private const float MinInfo = 150f;           // the unit information next to the tree never gets narrower than this
    private const float ExtraWidth = 35f;         // the tree starts this much wider than the game makes it
    private const float RetryEvery = 0.1f;
    private const int MaxTries = 30;

    private const string ThumbXaml =
        "<Thumb xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
        "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' HorizontalAlignment='Right' Cursor='SizeWE' Focusable='False'>" +
        "<Thumb.Template><ControlTemplate TargetType='Thumb'><Border x:Name='Bar' Background='Transparent'/>" +
        "<ControlTemplate.Triggers>" +
        "<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Bar' Property='Background' Value='#30FFFFFF'/></Trigger>" +
        "<Trigger Property='IsDragging' Value='True'><Setter TargetName='Bar' Property='Background' Value='#50FFFFFF'/></Trigger>" +
        "</ControlTemplate.Triggers></ControlTemplate></Thumb.Template></Thumb>";

    private sealed class Attempt
    {
        public WeakReference<GameWindow> Window;
        public float Next;
        public int Tries;
    }

    private static readonly List<Attempt> Pending = new List<Attempt>();
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GameWindow, TreeCollapse> Collapses =
        new System.Runtime.CompilerServices.ConditionalWeakTable<GameWindow, TreeCollapse>();

    /// <summary>How much narrower this window is than its "tree shown" layout (0 if the tree is shown or it isn't a Unit Reference).</summary>
    public static float HiddenTreeOffset(GameWindow window) =>
        Collapses.TryGetValue(window, out TreeCollapse collapse) ? collapse.Offset : 0f;

    public static void WindowLoaded(GameWindow window)
    {
        if (!SmartSettings.ImprovedReference.Value || InfoViewModel(window) == null)
            return;
        Pending.Add(new Attempt { Window = new WeakReference<GameWindow>(window), Next = 0f });
    }

    public static void Update()
    {
        for (int i = Pending.Count - 1; i >= 0; i--)
        {
            Attempt attempt = Pending[i];
            if (Time.unscaledTime < attempt.Next)
                continue;
            attempt.Next = Time.unscaledTime + RetryEvery;
            if (!attempt.Window.TryGetTarget(out GameWindow window) || ++attempt.Tries > MaxTries)
            {
                Pending.RemoveAt(i);
                continue;
            }
            if (TrySetup(window))
                Pending.RemoveAt(i);
        }
    }

    /// <summary>The window's Unit Reference view model, or null if this is another kind of window.</summary>
    private static ObjectInformationViewModel InfoViewModel(GameWindow window)
    {
        var content = window.Content as FrameworkElement;
        foreach (object candidate in new[] { window.DataContext, window.Content, content?.DataContext })
        {
            if (candidate is ObjectInformationViewModel vm && vm.ToggleTreeCommandVisible)
                return vm;
        }
        return null;
    }

    /// <summary>True when finished (set up, or nothing to do); false to try again a moment later.</summary>
    private static bool TrySetup(GameWindow window)
    {
        ObjectInformationViewModel vm = InfoViewModel(window);
        if (vm == null)
            return true;

        UnitTreeBrowserView view = FindBrowserView(window, vm);
        if (view == null)
            return false;

        if (!FindBlock(window, view, vm, out FrameworkElement block, out Grid container))
        {
            ModLog.Debug("Unit Reference: no grid holding the tree and its toggle button found");
            return true;
        }

        window.UpdateLayout();
        if (block.ActualWidth < 1f && vm.MuseumEntriesVisible)
            return false;

        var collapse = new TreeCollapse(window, view, block, vm);
        Collapses.Remove(window);
        Collapses.Add(window, collapse);
        new TreeSplitter(vm, block, container, collapse);
        TreeWheelGuard.Attach(window, view);
        if (SmartSettings.ScrollToSelected.Value)
            TreeScroller.Track(view, vm);
        return true;
    }

    /// <summary>The unit tree view of this window (its data context is the view model's browser).</summary>
    private static UnitTreeBrowserView FindBrowserView(DependencyObject root, ObjectInformationViewModel vm)
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        for (int budget = 3000; stack.Count > 0 && budget > 0; budget--)
        {
            DependencyObject node = stack.Pop();
            if (node is UnitTreeBrowserView view && ReferenceEquals(view.DataContext, vm.Browser))
                return view;
            for (int i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--)
                stack.Push(VisualTreeHelper.GetChild(node, i));
        }
        return null;
    }

    /// <summary>
    /// Walks up from the tree view to the first grid that also holds the toggle button: the grid's child on that path
    /// is the tree block, the grid is the container the splitter goes into.
    /// </summary>
    private static bool FindBlock(GameWindow window, UnitTreeBrowserView view, ObjectInformationViewModel vm,
        out FrameworkElement block, out Grid container)
    {
        block = null;
        container = null;
        DependencyObject child = view;
        for (DependencyObject parent = VisualTreeHelper.GetParent(child); parent != null && parent != window;
             child = parent, parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is Grid grid && child is FrameworkElement element && HoldsToggle(grid, vm, element))
            {
                block = element;
                container = grid;
                return true;
            }
        }
        return false;
    }

    private static bool HoldsToggle(Grid grid, ObjectInformationViewModel vm, UIElement except)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(grid); i++)
        {
            if (VisualTreeHelper.GetChild(grid, i) is ButtonBase button && button != except && ReferenceEquals(button.Command, vm.ToggleTreeCommand))
                return true;
        }
        return false;
    }

    // ---- the splitter -------------------------------------------------------------------------------------------

    /// <summary>One window's splitter: takes over the tree block's width, adds the thumb, keeps the width and shown state.</summary>
    private sealed class TreeSplitter
    {
        private readonly FrameworkElement block;
        private readonly FrameworkElement info;
        private readonly Thumb thumb;

        private readonly TreeCollapse collapse;

        public TreeSplitter(ObjectInformationViewModel vm, FrameworkElement block, Grid container, TreeCollapse collapse)
        {
            this.block = block;
            this.collapse = collapse;
            info = VisibleScrollViewer(container);

            float natural = block.ActualWidth;
            FreeColumn(container, Grid.GetColumn(block));
            block.MinWidth = 0f;
            block.MaxWidth = float.PositiveInfinity;
            float saved = SmartSettings.TreeWidth.Value;
            block.Width = Math.Max(MinTree, saved > 0f ? saved : natural + ExtraWidth);

            thumb = (Thumb)GUI.ParseXaml(ThumbXaml);
            thumb.Width = Math.Max(5f, block.Margin.Right);
            Grid.SetRow(thumb, Grid.GetRow(block));
            Grid.SetRowSpan(thumb, Grid.GetRowSpan(block));
            Grid.SetColumn(thumb, Grid.GetColumn(block));
            Grid.SetColumnSpan(thumb, Grid.GetColumnSpan(block));
            thumb.Visibility = vm.MuseumEntriesVisible ? Visibility.Visible : Visibility.Collapsed;
            thumb.DragDelta += Dragged;
            thumb.DragCompleted += (s, e) => SaveWidth();
            container.Children.Add(thumb);

            // The tree is shown or hidden as last time, and the choice is kept from now on. Subscribing first lets the
            // change notification of the line below hide the panel and narrow the window.
            vm.PropertyChanged += (s, e) => TreeToggled(vm, e);
            if (!SmartSettings.TreeShown.Value && vm.MuseumEntriesVisible)
                vm.MuseumEntriesVisible = false;
            ModLog.Debug($"Unit Reference: tree {block.Width:0} px, splitter added");
        }

        private void Dragged(object sender, DragDeltaEventArgs e)
        {
            float width = float.IsNaN(block.Width) || block.Width <= 0f ? block.ActualWidth : block.Width;
            float max = info != null && info.IsVisible
                ? Math.Max(MinTree, width + info.ActualWidth - MinInfo)
                : float.PositiveInfinity;
            float target = Math.Max(MinTree, Math.Min(max, width + e.HorizontalChange));
            if (Math.Abs(target - width) >= 0.5f)
                block.Width = target;
        }

        private void SaveWidth()
        {
            if (float.IsNaN(block.Width) || block.Width <= 0f)
                return;
            SmartSettings.TreeWidth.Value = (float)Math.Round(block.Width);
        }

        private void TreeToggled(ObjectInformationViewModel vm, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ObjectInformationViewModel.MuseumEntriesVisible))
                return;
            thumb.Visibility = vm.MuseumEntriesVisible ? Visibility.Visible : Visibility.Collapsed;
            SmartSettings.TreeShown.Value = vm.MuseumEntriesVisible;
            collapse.Apply(vm.MuseumEntriesVisible);
        }

        /// <summary>The block's column sizes itself to the block, whatever the game bound its width to.</summary>
        private static void FreeColumn(Grid container, int index)
        {
            if (index < 0 || index >= container.ColumnDefinitions.Count)
                return;
            ColumnDefinition column = container.ColumnDefinitions[index];
            BindingOperations.ClearBinding(column, ColumnDefinition.WidthProperty);
            column.Width = GridLength.Auto;
            column.MinWidth = 0f;
            column.MaxWidth = float.PositiveInfinity;
        }

        private static FrameworkElement VisibleScrollViewer(Grid container)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(container); i++)
            {
                if (VisualTreeHelper.GetChild(container, i) is ScrollViewer viewer && viewer.IsVisible)
                    return viewer;
            }
            return null;
        }
    }
}
