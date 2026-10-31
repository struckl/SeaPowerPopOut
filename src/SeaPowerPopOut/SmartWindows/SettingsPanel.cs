using System;
using System.Globalization;
using System.Security;
using BepInEx.Configuration;
using Noesis;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// The gear button in the title bar of a remembered window and the settings panel it opens. Every change is written
/// to the config at once; options that shape a window (the frame) apply to windows opened afterwards.
/// </summary>
internal static class SettingsPanel
{
    private const string GearName = "SmartWindowsGear";
    private const string GearIcon = "M6,0 L7,2 L9,1 L10,3 L12,4 L11,6 L12,8 L10,9 L9,11 L7,10 L6,12 L5,10 L3,11 L2,9 L0,8 L1,6 L0,4 L2,3 L3,1 L5,2 Z";
    private const string Ns = "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'";

    public static void AddGear(GameWindow window)
    {
        if (!SmartSettings.SettingsButton.Value)
            return;
        Panel buttons = TitleBar.FindButtonsPanel(window);
        if (buttons == null)
            return;
        Loc.Reload();                                   // the game's language is known by now
        foreach (UIElement child in buttons.Children)
        {
            if (child is FrameworkElement existing && existing.Name == GearName)
                return;
        }

        var gear = (Button)GUI.ParseXaml(
            $"<Button {Ns} Width='20' Focusable='False' Padding='0' Margin='0'>" +
            "<Path Width='12' Height='12' StrokeThickness='1.2' HorizontalAlignment='Center' VerticalAlignment='Center' " +
            $"Data='{GearIcon}' Stroke='{{Binding Foreground, RelativeSource={{RelativeSource AncestorType=Button}}}}' /></Button>");
        gear.Name = GearName;
        gear.ToolTip = Loc.Get("gear_tip");
        gear.Click += (sender, args) =>
        {
            args.Handled = true;
            Open(gear);
        };
        buttons.Children.Insert(0, gear);
    }

    private static void Open(UIElement anchor)
    {
        Loc.Reload();
        var body = new StackPanel();
        body.Children.Add(Label("title", bold: true));
        body.Children.Add(Check("frame", SmartSettings.BorderEnabled));
        body.Children.Add(Field("frame_width", SmartSettings.BorderThickness));
        body.Children.Add(Field("frame_color", SmartSettings.BorderColor, ValidColour));
        body.Children.Add(Check("drag_outside", SmartSettings.AllowDragOutside));
        body.Children.Add(Field("visible_px", SmartSettings.MinVisiblePixels));
        body.Children.Add(Field("cascade", SmartSettings.CascadeOffset));
        body.Children.Add(Check("snap", SmartSettings.SnapEnabled));
        body.Children.Add(Field("snap_distance", SmartSettings.SnapDistance));
        body.Children.Add(Check("remember_browser", SmartSettings.RememberBrowser));
        body.Children.Add(Check("armed_only", SmartSettings.ArmedOnly, ArmedFilter.SetEnabled));
        body.Children.Add(Check("search_extended", SmartSettings.ExtendedSearch));
        body.Children.Add(Check("link_help", SmartSettings.LinkHelp));
        body.Children.Add(Check("loadout_list", SmartSettings.LoadoutList));
        body.Children.Add(Field("language", SmartSettings.Language, text => true, Loc.Reload));
        body.Children.Add(Label("windows"));
        body.Children.Add(Field(null, SmartSettings.WindowPatterns, text => text.Trim().Length > 0, wide: true));
        body.Children.Add(Label("note", dim: true));

        var frame = new Border
        {
            Background = Brush("#F0202020"),
            BorderBrush = Brush("#FF808080"),
            BorderThickness = new Thickness(1f),
            Padding = new Thickness(10f),
            MinWidth = 280f,
            Child = body,
        };
        // Noesis only shows a popup that is part of the visual tree: it lives next to the gear while it is open.
        if (!(VisualTreeHelper.GetParent(anchor) is Panel host))
            return;
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

    // ---- rows ---------------------------------------------------------------------------------------------------

    private static TextBlock Label(string key, bool bold = false, bool dim = false) => new TextBlock
    {
        Text = Loc.Get(key),
        Foreground = Brush(dim ? "#FFA0A0A0" : "#FFFFFFFF"),
        FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
        FontSize = dim ? 11f : 13f,
        Margin = new Thickness(0f, 4f, 0f, 2f),
        TextWrapping = TextWrapping.Wrap,
    };

    private static CheckBox Check(string key, ConfigEntry<bool> entry, Action<bool> changed = null)
    {
        var box = new CheckBox
        {
            Content = Loc.Get(key),
            Foreground = Brush("#FFFFFFFF"),
            IsChecked = entry.Value,
            Margin = new Thickness(0f, 3f, 0f, 1f),
        };
        box.Click += (s, e) =>
        {
            entry.Value = box.IsChecked == true;
            changed?.Invoke(entry.Value);
        };
        return box;
    }

    private static StackPanel Field(string key, ConfigEntry<float> entry) =>
        Row(key, Text(entry.Value.ToString("0.##", CultureInfo.InvariantCulture), 60f, text =>
        {
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                entry.Value = v;
            return entry.Value.ToString("0.##", CultureInfo.InvariantCulture);
        }));

    private static StackPanel Field(string key, ConfigEntry<string> entry, Func<string, bool> valid,
        Action changed = null, bool wide = false) =>
        Row(key, Text(entry.Value, wide ? 260f : 100f, text =>
        {
            if (valid(text))
            {
                entry.Value = text.Trim();
                changed?.Invoke();
            }
            return entry.Value;
        }));

    private static StackPanel Row(string key, TextBox box)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0f, 2f, 0f, 2f) };
        if (key != null)
        {
            row.Children.Add(new TextBlock
            {
                Text = Loc.Get(key),
                Foreground = Brush("#FFFFFFFF"),
                Width = 150f,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        row.Children.Add(box);
        return row;
    }

    /// <summary>A text box that stores its text when it loses focus: <paramref name="store"/> returns what to show.</summary>
    private static TextBox Text(string text, float width, Func<string, string> store)
    {
        var box = new TextBox { Text = text, Width = width };
        box.LostFocus += (s, e) => box.Text = store(box.Text);
        return box;
    }

    private static Brush Brush(string argb)
    {
        ColorText.TryParse(argb, out byte a, out byte r, out byte g, out byte b);
        return new SolidColorBrush(Color.FromArgb(a, r, g, b));
    }

    private static bool ValidColour(string text) => ColorText.TryParse(text, out _, out _, out _, out _);
}
