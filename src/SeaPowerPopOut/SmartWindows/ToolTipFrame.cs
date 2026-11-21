using System;
using System.Runtime.CompilerServices;
using Noesis;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// The same thin frame as the windows get, around the game's tooltips (pop-up hints), on all four sides. A tooltip
/// whose own look already has a border is left alone.
/// </summary>
internal static class ToolTipFrame
{
    public static readonly Feature Feature = new Feature
    {
        Switch = () => SmartSettings.TooltipFrame.Value && SmartSettings.BorderEnabled.Value,
        Hook = _ => Register(),
    };

    private static readonly ConditionalWeakTable<Border, object> Ours = new ConditionalWeakTable<Border, object>();

    private static bool Register()
    {
        try
        {
            EventManager.RegisterClassHandler(typeof(ToolTip), ToolTip.OpenedEvent, new RoutedEventHandler(Opened));
            return true;
        }
        catch (Exception ex)
        {
            Rig.Log.Warn($"Tooltip frames not available: {ex.Message}");
            return false;
        }
    }

    private static void Opened(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is ToolTip tip && VisualTreeHelper.GetChildrenCount(tip) > 0 && WindowBorder.TryColor(out Color color))
                Frame(VisualTreeHelper.GetChild(tip, 0), new SolidColorBrush(color), SmartSettings.BorderThickness.Value);
        }
        catch (Exception ex)
        {
            Rig.Log.Debug($"tooltip frame: {ex.Message}");
        }
    }

    private static void Frame(DependencyObject root, Brush brush, float thickness)
    {
        if (root is Border border)
        {
            // The tooltip's own look is a border: use it, unless the game gave it a border of its own.
            bool ours = Ours.TryGetValue(border, out _);
            Thickness own = border.BorderThickness;
            if (!ours && (own.Left > 0f || own.Top > 0f || own.Right > 0f || own.Bottom > 0f))
                return;
            border.BorderBrush = brush;
            border.BorderThickness = new Thickness(thickness);
            if (!ours)
                Ours.Add(border, border);
            return;
        }

        if (!(root is Panel panel))
            return;
        Border frame = null;
        foreach (UIElement child in panel.Children)
        {
            if (child is Border existing && Ours.TryGetValue(existing, out _))
            {
                frame = existing;
                break;
            }
        }
        if (frame == null)
        {
            frame = new Border
            {
                IsHitTestVisible = false,
                Focusable = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            if (panel is Grid grid)
            {
                Grid.SetRowSpan(frame, Math.Max(1, grid.RowDefinitions.Count));
                Grid.SetColumnSpan(frame, Math.Max(1, grid.ColumnDefinitions.Count));
            }
            Panel.SetZIndex(frame, 100000);
            panel.Children.Add(frame);
            Ours.Add(frame, frame);
        }
        frame.BorderBrush = brush;
        frame.BorderThickness = new Thickness(thickness);
    }
}
