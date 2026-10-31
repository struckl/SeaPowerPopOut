using Noesis;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Mouse behaviour added to the remembered windows: a double-click on the title bar maximizes / restores the window,
/// a click anywhere in it brings it in front of the other windows.
/// </summary>
internal static class WindowBehaviors
{
    public static void Attach(GameWindow window)
    {
        window.PreviewMouseLeftButtonDown += (s, e) => ZOrder.Raise(window);

        Thumb header = window.HeaderThumb;
        if (header == null)
        {
            ModLog.Debug($"{window.DataContext?.GetType().Name}: no title bar thumb, double-click maximize skipped");
            return;
        }
        header.PreviewMouseLeftButtonDown += (s, e) =>
        {
            if (e.ClickCount >= 2 && window.CanResize)
            {
                window.Maximized = !window.Maximized;
                e.Handled = true;
            }
        };
    }
}

/// <summary>
/// Brings a clicked window in front of its siblings by raising its <c>Panel.ZIndex</c> one above the highest of the
/// others. Transient windows (the game's pop-up messages) are not counted, so they are never covered by a click.
/// </summary>
internal static class ZOrder
{
    public static void Raise(GameWindow window)
    {
        if (!(VisualTreeHelper.GetParent(window) is Panel parent))
            return;

        int top = int.MinValue;
        for (int i = 0; i < parent.Children.Count; i++)
        {
            if (parent.Children[i] is GameWindow other && other != window && !other.Transient)
                top = System.Math.Max(top, Panel.GetZIndex(other));
        }
        if (top != int.MinValue && Panel.GetZIndex(window) <= top && WindowLook.LevelOf(window) >= 0)
            Panel.SetZIndex(window, top + 1);
        EnforceLevels(window);
    }

    /// <summary>
    /// Keeps the windows set to "top" above all the normal ones and those set to "bottom" below them (the level of the
    /// look menu), whatever order clicks put the normal windows in. Windows the game marks transient are left alone.
    /// </summary>
    public static void EnforceLevels(GameWindow window)
    {
        if (!(VisualTreeHelper.GetParent(window) is Panel parent))
            return;
        int maxNormal = int.MinValue, minNormal = int.MaxValue;
        for (int i = 0; i < parent.Children.Count; i++)
        {
            if (!(parent.Children[i] is GameWindow w) || w.Transient || WindowLook.LevelOf(w) != 0)
                continue;
            int z = Panel.GetZIndex(w);
            maxNormal = System.Math.Max(maxNormal, z);
            minNormal = System.Math.Min(minNormal, z);
        }
        if (maxNormal == int.MinValue)
            return;
        for (int i = 0; i < parent.Children.Count; i++)
        {
            if (!(parent.Children[i] is GameWindow w) || w.Transient)
                continue;
            int level = WindowLook.LevelOf(w);
            int current = Panel.GetZIndex(w);
            if (level > 0 && current <= maxNormal)
                Panel.SetZIndex(w, maxNormal + 1);
            else if (level < 0 && current >= minNormal)
                Panel.SetZIndex(w, minNormal - 1);
        }
    }
}
