using Noesis;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// A thin frame drawn over a remembered window, so its edges stay visible against a busy map. Sits on the left, right
/// and bottom edge (a top side would run under the title bar), ignores the mouse and does not change the window's size.
/// </summary>
internal static class WindowBorder
{
    private const string FrameName = "SmartWindowsFrame";

    public static void Apply(GameWindow window)
    {
        if (!SmartSettings.BorderEnabled.Value)
            return;
        if (!ColorText.TryParse(SmartSettings.BorderColor.Value, out byte a, out byte r, out byte g, out byte b))
        {
            ModLog.Warn($"Window border colour '{SmartSettings.BorderColor.Value}' is not #RRGGBB or #AARRGGBB.");
            return;
        }

        Grid root = TitleBar.FindRootGrid(window);
        if (root == null)
        {
            ModLog.Debug($"{window.DataContext?.GetType().Name}: no root grid, no border");
            return;
        }
        foreach (UIElement child in root.Children)
        {
            if (child is FrameworkElement existing && existing.Name == FrameName)
                return;
        }

        float t = SmartSettings.BorderThickness.Value;
        var frame = new Border
        {
            Name = FrameName,
            IsHitTestVisible = false,
            BorderBrush = new SolidColorBrush(Color.FromArgb(a, r, g, b)),
            BorderThickness = new Thickness(t, 0f, t, t),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        Grid.SetRowSpan(frame, System.Math.Max(1, root.RowDefinitions.Count));
        Grid.SetColumnSpan(frame, System.Math.Max(1, root.ColumnDefinitions.Count));
        root.Children.Add(frame);
    }
}
