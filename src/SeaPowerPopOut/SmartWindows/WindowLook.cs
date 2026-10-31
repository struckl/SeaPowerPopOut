using System;
using System.Collections.Generic;
using System.Globalization;
using Noesis;
using UnityEngine;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Applies the look of a remembered window (see <see cref="LookStore"/>): the see-through backgrounds, the scale of the
/// content, the outline around the text, and the level above or below the other windows. The game rebuilds parts of a
/// window's content now and then (tabs), so the look is re-applied once a second while it is not the default.
/// </summary>
internal static class WindowLook
{
    /// <summary>Elements whose name starts like this (the menu, the settings popup) are never dimmed or scaled.</summary>
    public const string SkipPrefix = "SmartWindows";
    private const float RecheckSeconds = 1f;
    private const int WalkBudget = 6000;
    private const float HaloBlur = 1.2f;

    private static LookStore store;
    private static readonly List<State> Windows = new List<State>();

    private sealed class State
    {
        public WeakReference<GameWindow> Window;
        public string Pattern;
        public float Next;
        public bool Applied;            // something other than the default is currently on the window
    }

    public static LookStore Store => store ?? (store = LookStore.Parse(SmartSettings.WindowLooks.Value, SmartSettings.WindowLevels.Value));

    public static int LevelOf(GameWindow window)
    {
        string pattern = WindowMemory.PatternOf(window);
        return pattern == null ? 0 : Store.LevelOf(pattern);
    }

    public static void WindowLoaded(GameWindow window, string pattern)
    {
        if (pattern == null)
            return;
        Windows.Add(new State { Window = new WeakReference<GameWindow>(window), Pattern = pattern, Next = Time.unscaledTime + 0.2f });
        if (SmartSettings.LookButton.Value)
            LookMenu.AddButton(window, pattern);
    }

    /// <summary>Called every frame.</summary>
    public static void Update()
    {
        if (Windows.Count == 0)
            return;
        float now = Time.unscaledTime;
        for (int i = Windows.Count - 1; i >= 0; i--)
        {
            State s = Windows[i];
            if (!s.Window.TryGetTarget(out GameWindow window) || !window.IsLoaded)
            {
                Windows.RemoveAt(i);
                continue;
            }
            if (now < s.Next)
                continue;
            s.Next = now + RecheckSeconds;
            try
            {
                LookValues look = Store.Get(s.Pattern);
                if (!look.IsDefault || s.Applied)
                    Apply(window, s, look);
                if (Store.LevelOf(s.Pattern) != 0)
                    ZOrder.EnforceLevels(window);
            }
            catch (Exception ex)
            {
                ModLog.Debug($"look: {ex.Message}");
            }
        }
    }

    public static void SetLook(string pattern, LookValues look)
    {
        Store.Set(pattern, look);
        SmartSettings.WindowLooks.Value = Store.SerializeLooks();
        Refresh(pattern);
    }

    public static void SetLevel(string pattern, int level)
    {
        Store.SetLevel(pattern, level);
        SmartSettings.WindowLevels.Value = Store.SerializeLevels();
        Refresh(pattern);
    }

    /// <summary>Applies the stored look of <paramref name="pattern"/> to every open window of it, now.</summary>
    private static void Refresh(string pattern)
    {
        foreach (State s in Windows)
        {
            if (s.Pattern != pattern || !s.Window.TryGetTarget(out GameWindow window) || !window.IsLoaded)
                continue;
            try
            {
                Apply(window, s, Store.Get(pattern));
                ZOrder.EnforceLevels(window);
            }
            catch (Exception ex)
            {
                ModLog.Debug($"look: {ex.Message}");
            }
            s.Next = Time.unscaledTime + RecheckSeconds;
        }
    }

    // ---- applying ------------------------------------------------------------------------------------------------

    private static void Apply(GameWindow window, State state, LookValues look)
    {
        state.Applied = !look.IsDefault;
        float opacity = look.Opacity;
        bool dim = opacity < 0.999f;

        // Several backgrounds lie on top of each other: scale each so the whole is `opacity` as opaque as before.
        float factor = 1f;
        if (dim)
        {
            var best = new List<float>();
            int budget = WalkBudget;
            DeepestLayers(window, new List<float>(), best, ref budget);
            factor = LookMath.SolveFactor(best, opacity);
        }
        int walk = WalkBudget;
        Dim(window, opacity, factor, look.Outline && dim, ref walk);
        ApplyScale(window, look.Scale);
    }

    private static bool Skipped(DependencyObject e) =>
        e is FrameworkElement fe && fe.Name != null && fe.Name.StartsWith(SkipPrefix, StringComparison.Ordinal);

    private static List<DependencyObject> Children(DependencyObject e)
    {
        var list = new List<DependencyObject>();
        int n = VisualTreeHelper.GetChildrenCount(e);
        for (int i = 0; i < n; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(e, i);
            if (child != null && !Skipped(child))
                list.Add(child);
        }
        return list;
    }

    private static Brush BackgroundOf(DependencyObject e) =>
        e is Border b ? b.Background : e is Panel p ? p.Background : null;

    private static void SetBackground(DependencyObject e, Brush brush)
    {
        if (e is Border b)
            b.Background = brush;
        else if (e is Panel p)
            p.Background = brush;
    }

    /// <summary>The stack of background alphas on the path through the tree that covers the most.</summary>
    private static void DeepestLayers(DependencyObject e, List<float> path, List<float> best, ref int budget)
    {
        if (--budget < 0)
            return;
        Brush brush = BackgroundOf(e);
        bool layer = brush != null && AlphaOf(e, brush) > 0.02f;
        if (layer)
            path.Add(AlphaOf(e, brush));
        List<DependencyObject> children = Children(e);
        foreach (DependencyObject child in children)
            DeepestLayers(child, path, best, ref budget);
        if ((children.Count == 0 || !layer) && path.Count > 0 && LookMath.Coverage(path, 1f) > LookMath.Coverage(best, 1f))
        {
            best.Clear();
            best.AddRange(path);
        }
        if (layer)
            path.RemoveAt(path.Count - 1);
    }

    /// <summary>The alpha a background had before we dimmed it.</summary>
    private static float AlphaOf(DependencyObject e, Brush brush)
    {
        if (!(e is FrameworkElement fe) || Foreign(fe))
            return 0f;
        float opacity = ReadTag(fe, out float original, out float applied) && Math.Abs(brush.Opacity - applied) < 0.002f
            ? original
            : brush.Opacity;
        float colour = 1f;
        if (brush is SolidColorBrush solid)
        {
            colour = Convert.ToSingle(solid.Color.A);
            if (colour > 1.0001f)
                colour /= 255f;
        }
        return colour * opacity;
    }

    private static void Dim(DependencyObject e, float opacity, float factor, bool outline, ref int budget)
    {
        if (--budget < 0)
            return;
        if (e is FrameworkElement fe && !Foreign(fe))
        {
            if (e is Image image)
                DimElement(image, opacity);
            else if (BackgroundOf(e) != null || fe.Tag != null)
                DimBackground(fe, factor);
        }
        if (e is TextBlock text)
            Outline(text, outline);
        foreach (DependencyObject child in Children(e))
            Dim(child, opacity, factor, outline, ref budget);
    }

    // What an element looked like before we dimmed it is kept in its Tag ("SWdim|original|applied"), not in a table
    // keyed by the wrapper object: Noesis wrappers are recreated, a Tag is not lost with them. A Tag we did not write
    // belongs to the game: such an element is left alone.
    private const string TagPrefix = "SWdim|";

    private static bool Foreign(FrameworkElement fe) =>
        fe.Tag != null && !(fe.Tag is string text && text.StartsWith(TagPrefix, StringComparison.Ordinal));

    private static bool ReadTag(FrameworkElement fe, out float original, out float applied)
    {
        original = applied = 0f;
        if (!(fe.Tag is string text) || !text.StartsWith(TagPrefix, StringComparison.Ordinal))
            return false;
        string[] f = text.Split('|');
        return f.Length == 3
            && float.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out original)
            && float.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out applied);
    }

    private static void WriteTag(FrameworkElement fe, float original, float applied) =>
        fe.Tag = TagPrefix + original.ToString("0.####", CultureInfo.InvariantCulture) + "|" +
                 applied.ToString("0.####", CultureInfo.InvariantCulture);

    private static void DimBackground(FrameworkElement e, float factor)
    {
        Brush brush = BackgroundOf(e);
        bool tagged = ReadTag(e, out float recorded, out float applied);
        if (brush == null)
        {
            if (tagged)
                e.Tag = null;
            return;
        }
        bool ours = tagged && Math.Abs(brush.Opacity - applied) < 0.002f;
        if (factor >= 0.999f)
        {
            if (ours)
                brush.Opacity = recorded;
            if (tagged)
                e.Tag = null;
            return;
        }
        float original = ours ? recorded : brush.Opacity;
        Brush target = ours ? brush : (Brush)brush.Clone();     // never change a brush the game shares
        target.Opacity = original * factor;
        SetBackground(e, target);
        WriteTag(e, original, original * factor);
    }

    private static void DimElement(UIElement e, float opacity)
    {
        var fe = (FrameworkElement)e;
        bool tagged = ReadTag(fe, out float recorded, out float applied);
        bool ours = tagged && Math.Abs(e.Opacity - applied) < 0.002f;
        if (opacity >= 0.999f)
        {
            if (ours)
                e.Opacity = recorded;
            if (tagged)
                fe.Tag = null;
            return;
        }
        float original = ours ? recorded : e.Opacity;
        e.Opacity = original * opacity;
        WriteTag(fe, original, e.Opacity);
    }

    /// <summary>A thin halo around the text keeps it readable on a see-through window.</summary>
    private static void Outline(TextBlock text, bool on)
    {
        bool ours = text.Effect is DropShadowEffect e && Math.Abs(e.BlurRadius - HaloBlur) < 0.001f;
        if (!on)
        {
            if (ours)
                text.Effect = null;
            return;
        }
        if (text.Effect != null && !ours)
            return;
        bool dark = text.Foreground is SolidColorBrush b && Brightness(b) < 0.5f;
        text.Effect = new DropShadowEffect
        {
            BlurRadius = HaloBlur,
            ShadowDepth = 0f,
            Opacity = 1f,
            Color = dark ? Noesis.Color.FromRgb(255, 255, 255) : Noesis.Color.FromRgb(0, 0, 0),
        };
    }

    private static float Brightness(SolidColorBrush b)
    {
        float r = Convert.ToSingle(b.Color.R), g = Convert.ToSingle(b.Color.G), bl = Convert.ToSingle(b.Color.B);
        if (Math.Max(r, Math.Max(g, bl)) > 1.0001f)
        {
            r /= 255f;
            g /= 255f;
            bl /= 255f;
        }
        return 0.299f * r + 0.587f * g + 0.114f * bl;
    }

    // ---- scale ---------------------------------------------------------------------------------------------------

    private static void ApplyScale(GameWindow window, float scale)
    {
        ContentPresenter presenter = FindContentPresenter(window);
        if (presenter == null)
            return;
        var current = presenter.LayoutTransform as ScaleTransform;
        if (Math.Abs(scale - 1f) < 0.001f)
        {
            if (current != null)
                presenter.LayoutTransform = null;
        }
        else if (current == null || Math.Abs(current.ScaleX - scale) > 0.001f)
        {
            presenter.LayoutTransform = new ScaleTransform(scale, scale);
        }
    }

    /// <summary>The (largest) presenter that shows the window's content.</summary>
    private static ContentPresenter FindContentPresenter(GameWindow window)
    {
        object content = window.Content;
        if (content == null)
            return null;
        ContentPresenter best = null;
        float height = 0f;
        var stack = new Stack<DependencyObject>();
        stack.Push(window);
        int budget = 3000;
        while (stack.Count > 0 && --budget > 0)
        {
            DependencyObject e = stack.Pop();
            if (e is ContentPresenter p && ReferenceEquals(p.Content, content) && p.ActualHeight > height)
            {
                best = p;
                height = p.ActualHeight;
            }
            foreach (DependencyObject child in Children(e))
                stack.Push(child);
        }
        return best;
    }
}
