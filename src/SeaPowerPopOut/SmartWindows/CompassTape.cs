using System;
using System.Collections.Generic;
using Noesis;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// The heading tape at the top of the mission screen (a 480x64 clipped, opacity-masked Canvas in the game's XAML; no
/// C# type of its own, so it is found by shape). Building block for the desktop mode: <see cref="Attach"/> moves the tape
/// into another panel, <see cref="Paint"/> makes its text and lines white or black (for a map behind it that is dark or
/// light), <see cref="Detach"/> puts everything back. Nothing calls it yet and there is no setting: outside the desktop
/// mode the tape stays on the game's own map, where it needs none of this.
/// </summary>
internal sealed class CompassTape
{
    private readonly List<TextBlock> texts = new List<TextBlock>();
    private readonly List<Path> paths = new List<Path>();
    private WeakReference<Canvas> tape;
    private WeakReference<Panel> home;
    private int homeIndex;
    private bool white = true;

    public bool Attached { get; private set; }

    /// <summary>The tape inside <paramref name="view"/> (the main game view), or null while it is not built.</summary>
    public static Canvas Find(FrameworkElement view)
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(view);
        for (int budget = 4000; stack.Count > 0 && budget > 0; budget--)
        {
            DependencyObject node = stack.Pop();
            if (node is Canvas c && Math.Abs(c.Width - 480f) < 0.5f && Math.Abs(c.Height - 64f) < 0.5f
                && c.OpacityMask != null && c.ClipToBounds)
                return c;
            for (int i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--)
            {
                DependencyObject child = VisualTreeHelper.GetChild(node, i);
                if (child != null)
                    stack.Push(child);
            }
        }
        return null;
    }

    /// <summary>Moves the tape into <paramref name="host"/>, top centre, with the game's view model as its data.</summary>
    public bool Attach(FrameworkElement view, Panel host, object dataContext)
    {
        Canvas c = Current();
        if (c == null)
        {
            c = Find(view);
            if (c == null)
                return false;
            tape = new WeakReference<Canvas>(c);
        }
        var parent = VisualTreeHelper.GetParent(c) as Panel;
        if (parent == host)
            return true;
        if (parent == null)
            return false;
        if (home == null)
        {
            home = new WeakReference<Panel>(parent);
            homeIndex = parent.Children.IndexOf(c);
        }
        parent.Children.Remove(c);
        c.HorizontalAlignment = HorizontalAlignment.Center;
        c.VerticalAlignment = VerticalAlignment.Top;
        host.Children.Add(c);
        if (dataContext != null)
            c.DataContext = dataContext;
        Collect(c);
        Paint(white);
        Attached = true;
        return true;
    }

    /// <summary>White or black text and lines.</summary>
    public void Paint(bool toWhite)
    {
        white = toWhite;
        var brush = new SolidColorBrush(toWhite ? Color.FromRgb(255, 255, 255) : Color.FromRgb(0, 0, 0));
        foreach (TextBlock t in texts)
            t.Foreground = brush;
        foreach (Path p in paths)
        {
            p.Stroke = brush;
            if (p.Fill != null)
                p.Fill = brush;
        }
    }

    /// <summary>Back to the game's own panel, with the game's own colours and data.</summary>
    public void Detach()
    {
        Canvas c = Current();
        if (c != null)
        {
            if (home != null && home.TryGetTarget(out Panel h) && VisualTreeHelper.GetParent(c) is Panel now && now != h)
            {
                now.Children.Remove(c);
                h.Children.Insert(Math.Max(0, Math.Min(homeIndex, h.Children.Count)), c);
                c.HorizontalAlignment = HorizontalAlignment.Center;
                c.VerticalAlignment = VerticalAlignment.Top;
            }
            c.ClearValue(FrameworkElement.DataContextProperty);
            foreach (TextBlock t in texts)
                t.ClearValue(TextBlock.ForegroundProperty);
            foreach (Path p in paths)
            {
                p.ClearValue(Shape.StrokeProperty);
                p.ClearValue(Shape.FillProperty);
            }
        }
        texts.Clear();
        paths.Clear();
        home = null;
        Attached = false;
    }

    private Canvas Current() => tape != null && tape.TryGetTarget(out Canvas c) ? c : null;

    private void Collect(Canvas root)
    {
        texts.Clear();
        paths.Clear();
        var stack = new Stack<UIElement>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            UIElement e = stack.Pop();
            if (e is TextBlock t)
                texts.Add(t);
            if (e is Path p)
                paths.Add(p);
            if (e is Canvas c)
                for (int i = 0; i < c.Children.Count; i++)
                    stack.Push(c.Children[i]);
        }
    }
}
