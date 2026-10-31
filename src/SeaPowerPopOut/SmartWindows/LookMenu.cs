using System;
using System.Globalization;
using Noesis;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// The button in the title bar (next to the gear) that opens the look menu of a window: opacity, scale, outline and level.
/// The choices are kept per window pattern (see <see cref="LookStore"/>) and applied by <see cref="WindowLook"/>.
/// </summary>
internal static class LookMenu
{
    private const string ButtonName = "SmartWindowsLookButton";
    private const string MenuName = "SmartWindowsLookMenu";
    // Two overlapping discs: a window that is see-through.
    private const string Icon = "M0,6 A4,4 0 1 1 8,6 A4,4 0 1 1 0,6 Z M4,6 A4,4 0 1 1 12,6 A4,4 0 1 1 4,6 Z";
    private const string Ns = "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'";

    public static void AddButton(GameWindow window, string pattern)
    {
        Panel buttons = TitleBar.FindButtonsPanel(window);
        if (buttons == null)
        {
            ModLog.Debug("look menu: no title bar buttons, button skipped");
            return;
        }
        foreach (UIElement child in buttons.Children)
        {
            if (child is FrameworkElement existing && existing.Name == ButtonName)
                return;
        }

        var button = (Button)GUI.ParseXaml(
            $"<Button {Ns} Width='20' Focusable='False' Padding='0' Margin='0'>" +
            "<Path Width='12' Height='12' StrokeThickness='1.2' HorizontalAlignment='Center' VerticalAlignment='Center' " +
            $"Data='{Icon}' Stroke='{{Binding Foreground, RelativeSource={{RelativeSource AncestorType=Button}}}}' /></Button>");
        button.Name = ButtonName;
        button.ToolTip = Loc.Get("look_tip");
        button.Click += (sender, args) =>
        {
            args.Handled = true;
            Open(button, pattern);
        };
        buttons.Children.Insert(0, button);
    }

    private static void Open(UIElement anchor, string pattern)
    {
        Loc.Reload();
        if (!(VisualTreeHelper.GetParent(anchor) is Panel host))
            return;
        var frame = new Border
        {
            Name = MenuName,
            Background = Brush("#F0202020"),
            BorderBrush = Brush("#FF808080"),
            BorderThickness = new Thickness(1f),
            Padding = new Thickness(10f),
            MinWidth = 230f,
        };
        Build(frame, pattern);

        // Noesis only shows a popup that is part of the visual tree: it lives next to the button while it is open.
        var popup = new Popup
        {
            Child = frame,
            PlacementTarget = anchor,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
        };
        popup.Closed += (sender, args) => host.Children.Remove(popup);
        host.Children.Add(popup);
        popup.IsOpen = true;
    }

    /// <summary>Fills the menu from what is stored now; called again after every change.</summary>
    private static void Build(Border frame, string pattern)
    {
        LookValues look = WindowLook.Store.Get(pattern);
        int level = WindowLook.Store.LevelOf(pattern);

        var body = new StackPanel();
        body.Children.Add(Text(Loc.Get("look_title"), bold: true));
        body.Children.Add(Stepper("look_opacity", Percent(look.Opacity),
            dir => Change(frame, pattern, look.WithOpacity(LookMath.StepOpacity(look.Opacity, dir)))));
        body.Children.Add(Stepper("look_scale", Percent(look.Scale),
            dir => Change(frame, pattern, look.WithScale(LookMath.StepScale(look.Scale, dir)))));

        var outline = new CheckBox
        {
            Content = Loc.Get("look_outline"),
            Foreground = Brush("#FFFFFFFF"),
            IsChecked = look.Outline,
            Margin = new Thickness(0f, 4f, 0f, 2f),
        };
        outline.Click += (s, e) => Change(frame, pattern, look.WithOutline(outline.IsChecked == true));
        body.Children.Add(outline);

        body.Children.Add(Text(Loc.Get("look_level")));
        var levels = new StackPanel { Orientation = Orientation.Horizontal };
        levels.Children.Add(LevelButton("look_level_bottom", -1, level, frame, pattern));
        levels.Children.Add(LevelButton("look_level_default", 0, level, frame, pattern));
        levels.Children.Add(LevelButton("look_level_top", 1, level, frame, pattern));
        body.Children.Add(levels);

        Button reset = Plain(Loc.Get("look_reset"), 0f);
        reset.Margin = new Thickness(0f, 8f, 0f, 0f);
        reset.HorizontalAlignment = HorizontalAlignment.Left;
        reset.Click += (s, e) =>
        {
            WindowLook.SetLevel(pattern, 0);
            Change(frame, pattern, LookValues.Default);
        };
        body.Children.Add(reset);
        frame.Child = body;
    }

    private static void Change(Border frame, string pattern, LookValues look)
    {
        try
        {
            WindowLook.SetLook(pattern, look);
            Build(frame, pattern);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"look menu: {ex.Message}");
        }
    }

    private static Button LevelButton(string key, int level, int current, Border frame, string pattern)
    {
        Button button = Plain(Loc.Get(key), 0f);
        button.Margin = new Thickness(0f, 2f, 4f, 0f);
        if (level == current)
            button.Foreground = Brush("#FF7FD0FF");
        button.Click += (s, e) =>
        {
            WindowLook.SetLevel(pattern, level);
            Build(frame, pattern);
        };
        return button;
    }

    /// <summary>A row: the label, a minus button, the value, a plus button.</summary>
    private static StackPanel Stepper(string key, string value, Action<int> step)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0f, 3f, 0f, 1f) };
        row.Children.Add(new TextBlock
        {
            Text = Loc.Get(key),
            Foreground = Brush("#FFFFFFFF"),
            Width = 90f,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Button minus = Plain("-", 24f);
        minus.Click += (s, e) => step(-1);
        row.Children.Add(minus);
        row.Children.Add(new TextBlock
        {
            Text = value,
            Foreground = Brush("#FFFFFFFF"),
            Width = 50f,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Button plus = Plain("+", 24f);
        plus.Click += (s, e) => step(1);
        row.Children.Add(plus);
        return row;
    }

    private static Button Plain(string text, float width)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(6f, 1f, 6f, 1f),
            Focusable = false,
        };
        if (width > 0f)
            button.Width = width;
        return button;
    }

    private static TextBlock Text(string text, bool bold = false) => new TextBlock
    {
        Text = text,
        Foreground = Brush("#FFFFFFFF"),
        FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
        FontSize = 13f,
        Margin = new Thickness(0f, 4f, 0f, 2f),
    };

    private static string Percent(float v) => Math.Round(v * 100f).ToString("0", CultureInfo.InvariantCulture) + " %";

    private static Brush Brush(string argb)
    {
        ColorText.TryParse(argb, out byte a, out byte r, out byte g, out byte b);
        return new SolidColorBrush(Color.FromArgb(a, r, g, b));
    }
}
