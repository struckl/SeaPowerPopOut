using Noesis;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// A thin frame drawn over a remembered window, so its edges stay visible against a busy map. Sits on the left, right
/// and bottom edge (a top side would run under the title bar), ignores the mouse and does not change the window's size.
/// </summary>
internal static class WindowBorder
{
    public static readonly Feature Feature = new Feature
    {
        LiveSwitch = () => SmartSettings.BorderEnabled.Value,
        WindowLoaded = Apply,
    };

    private const string FrameName = WindowLook.SkipPrefix + "Frame";

    /// <summary>The frame colour of the windows and tooltips (setting "Color"); false when it is not a colour.</summary>
    public static bool TryColor(out Color color)
    {
        bool ok = ColorText.TryParse(SmartSettings.BorderColor.Value, out byte a, out byte r, out byte g, out byte b);
        color = Color.FromArgb(a, r, g, b);
        return ok;
    }

    private static void Apply(GameWindow window)
    {
        if (!TryColor(out Color color))
        {
            Rig.Log.Warn($"Window border colour '{SmartSettings.BorderColor.Value}' is not #RRGGBB or #AARRGGBB.");
            return;
        }

        Grid root = TitleBar.FindRootGrid(window);
        if (root == null)
        {
            Rig.Log.Debug($"{window.DataContext?.GetType().Name}: no root grid, no border");
            return;
        }
        Grid host = FindChrome(root);
        foreach (UIElement child in host.Children)
        {
            if (child is FrameworkElement existing && existing.Name == FrameName)
                return;
        }

        float t = SmartSettings.BorderThickness.Value;
        var frame = new Border
        {
            Name = FrameName,
            IsHitTestVisible = false,
            BorderBrush = new SolidColorBrush(color),
            BorderThickness = new Thickness(t, 0f, t, t),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        Grid.SetRowSpan(frame, System.Math.Max(1, host.RowDefinitions.Count));
        Grid.SetColumnSpan(frame, System.Math.Max(1, host.ColumnDefinitions.Count));
        host.Children.Add(frame);
    }

    /// <summary>
    /// The visible chrome (header + body) inside the template's root grid. The game's Window.xaml root is a 3x3 grid whose
    /// outer rows and columns are the invisible resize-thumb bands; the header and body sit in the inner grid at row 1,
    /// column 1. A frame across the whole root lands in those bands, outside the visible edge.
    /// </summary>
    private static Grid FindChrome(Grid root)
    {
        if (root.RowDefinitions.Count == 3 && root.ColumnDefinitions.Count == 3)
        {
            foreach (UIElement child in root.Children)
            {
                if (child is Grid inner && Grid.GetRow(inner) == 1 && Grid.GetColumn(inner) == 1)
                    return inner;
            }
        }
        return root;
    }
}
