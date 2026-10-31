using System;
using System.Collections.Generic;
using Noesis;
using SeaPower;
using SeapowerUI;
using SeapowerUI.ViewModels;
using UnityEngine;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Unit Reference: scrolls the unit tree so the selected unit is visible, centred when the window opens or the tree is
/// shown, and by the least amount when the selection changes later. The tree is built a little at a time, so the
/// scroll is tried every 0.1 s until the selected unit's row exists (and gives up after a few seconds).
/// </summary>
internal sealed class TreeScroller
{
    private const float Every = 0.1f;
    private const int MaxTries = 60;

    private static readonly List<TreeScroller> All = new List<TreeScroller>();

    private readonly WeakReference<UnitTreeBrowserView> view;
    private readonly WeakReference<ObjectInformationViewModel> vm;

    private float next;
    private bool shown;                       // the tree was visible at the last look
    private object items;                     // the tree's items list at the last look: a new one means a new tree
    private MuseumEntry done;                 // the selection already scrolled to
    private bool centre = true;
    private int tries;

    private TreeScroller(UnitTreeBrowserView view, ObjectInformationViewModel vm)
    {
        this.view = new WeakReference<UnitTreeBrowserView>(view);
        this.vm = new WeakReference<ObjectInformationViewModel>(vm);
    }

    public static void Track(UnitTreeBrowserView view, ObjectInformationViewModel vm) => All.Add(new TreeScroller(view, vm));

    public static void Update()
    {
        for (int i = All.Count - 1; i >= 0; i--)
        {
            if (!All[i].Step())
                All.RemoveAt(i);
        }
    }

    /// <summary>False when the window is gone.</summary>
    private bool Step()
    {
        if (Time.unscaledTime < next)
            return true;
        next = Time.unscaledTime + Every;
        if (!view.TryGetTarget(out UnitTreeBrowserView v) || !vm.TryGetTarget(out ObjectInformationViewModel model)
            || VisualTreeHelper.GetParent(v) == null)
            return false;
        try
        {
            Scroll(v, model);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"tree scroll: {ex.Message}");
            done = model.Browser?.SelectedEntry;
        }
        return true;
    }

    private void Scroll(UnitTreeBrowserView v, ObjectInformationViewModel model)
    {
        if (!model.ToggleTreeCommandVisible || !model.MuseumEntriesVisible)
        {
            shown = false;
            return;
        }

        UnitTreeBrowserViewModel browser = model.Browser;
        MuseumEntry selected = browser?.SelectedEntry;
        object currentItems = browser?.TreeItems;
        if (!shown || !ReferenceEquals(currentItems, items))
        {
            centre = true;                    // freshly shown tree or a new list: put the selection in the middle
            done = null;
            tries = 0;
        }
        shown = true;
        items = currentItems;
        if (selected == null || selected == done)
            return;

        if (tries == 0 && done != null)
            centre = false;                   // a later selection: scroll only as far as needed
        if (++tries > MaxTries)
        {
            Finish(selected);
            return;
        }

        TreeView tree = FindTree(v);
        if (tree == null || tree.ActualHeight <= 0f)
            return;
        var path = new List<MuseumEntry>();
        if (!PathTo(browser.TreeItems, selected, path))
        {
            Finish(selected);
            return;
        }

        TreeViewItem row = RowFor(tree, path, selected);
        if (row == null || row.ActualHeight <= 0f)
        {
            tree.UpdateLayout();
            return;
        }
        ScrollViewer scroller = Ancestor<ScrollViewer>(row, tree) ?? FindDescendant<ScrollViewer>(tree);
        if (scroller == null || scroller.ViewportHeight <= 0f)
            return;

        if (scroller.CanContentScroll)
            row.BringIntoView();
        else
            ScrollTo(scroller, row);
        Finish(selected);
    }

    private void ScrollTo(ScrollViewer scroller, TreeViewItem row)
    {
        var header = row.Template?.FindName("PART_Header", row) as FrameworkElement;
        FrameworkElement target = header != null && header.ActualHeight > 0f ? header : row;
        float top = target.TranslatePoint(new Point(0f, 0f), scroller).Y;
        float height = Math.Min(target.ActualHeight, scroller.ViewportHeight);
        float viewport = scroller.ViewportHeight;
        float offset = scroller.VerticalOffset;
        float wanted = offset;

        if (centre)
        {
            if (top < 0f || top + height > viewport)
                wanted = offset + top - (viewport - height) / 2f;
        }
        else if (top < 0f)
        {
            wanted = offset + top;
        }
        else if (top + height > viewport)
        {
            wanted = offset + top + height - viewport;
        }
        wanted = Math.Max(0f, Math.Min(wanted, scroller.ScrollableHeight));
        if (Math.Abs(wanted - offset) > 0.5f)
            scroller.ScrollToVerticalOffset(wanted);
    }

    private void Finish(MuseumEntry selected)
    {
        done = selected;
        tries = 0;
        centre = false;
    }

    // ---- finding things -----------------------------------------------------------------------------------------

    /// <summary>The rows from the top of the tree down to the entry: the entry's own row, or the deepest collapsed one above it.</summary>
    private static TreeViewItem RowFor(TreeView tree, List<MuseumEntry> path, MuseumEntry selected)
    {
        ItemsControl level = tree;
        TreeViewItem row = null;
        foreach (MuseumEntry entry in path)
        {
            var container = level.ItemContainerGenerator.ContainerFromItem(entry) as TreeViewItem;
            if (container == null)
                break;
            row = container;
            if (entry != selected && !container.IsExpanded)
                break;
            level = container;
        }
        return row;
    }

    private static bool PathTo(IEnumerable<MuseumEntry> entries, MuseumEntry wanted, List<MuseumEntry> path)
    {
        if (entries == null)
            return false;
        foreach (MuseumEntry entry in entries)
        {
            if (entry == null)
                continue;
            path.Add(entry);
            if (entry == wanted || PathTo(entry.FilteredMuseumEntries, wanted, path))
                return true;
            path.RemoveAt(path.Count - 1);
        }
        return false;
    }

    private static TreeView FindTree(DependencyObject root) => FindDescendant<TreeView>(root);

    private static T FindDescendant<T>(DependencyObject root) where T : class
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        for (int budget = 20000; stack.Count > 0 && budget > 0; budget--)
        {
            DependencyObject node = stack.Pop();
            if (node != root && node is T found)
                return found;
            for (int i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--)
                stack.Push(VisualTreeHelper.GetChild(node, i));
        }
        return null;
    }

    private static T Ancestor<T>(DependencyObject element, DependencyObject stop) where T : class
    {
        for (DependencyObject node = VisualTreeHelper.GetParent(element); node != null && node != stop; node = VisualTreeHelper.GetParent(node))
        {
            if (node is T found)
                return found;
        }
        return null;
    }
}
