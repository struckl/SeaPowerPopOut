using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Noesis;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// One kebab button (three dots) in a window's title bar instead of a row of our own buttons: it opens a short list of
/// what the mod offers for that window (pop out, look, settings). The features register their entry with <see cref="Set"/>;
/// the first one adds the button. A click on an entry closes the list and runs the entry, which gets the kebab button as
/// the anchor for whatever it opens next.
/// </summary>
internal static class TitleMenu
{
    private const string ButtonName = "SmartWindowsMenuButton";
    private const string MenuName = "SmartWindowsMenu";
    // The mod's own mark: a box with an arrow leaving it, as on the pop-out entry.
    public const string Mark = "M1,4 L1,11 L8,11 L8,8 M5,7 L11,1 M7,1 L11,1 L11,5";
    private const string BackIcon = "M7,1 L2,6 L7,11 M2,6 H11";
    private const string Ns = "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'";

    private sealed class Entry
    {
        public string Id, Label, Icon;
        public int Order;
        public Action<UIElement> Run;
    }

    private static readonly ConditionalWeakTable<Button, List<Entry>> Entries = new ConditionalWeakTable<Button, List<Entry>>();

    /// <summary>Adds or updates one entry of the window's menu; the button is made on the first call. Lower order is higher up.</summary>
    public static void Set(GameWindow window, string id, int order, string label, string icon, Action<UIElement> run)
    {
        if (!Ours(window))
            return;
        Panel panel = TitleBar.FindButtonsPanel(window);
        if (panel == null)
            return;

        Button kebab = null;
        foreach (UIElement child in panel.Children)
        {
            if (child is Button existing && existing.Name == ButtonName)
            {
                kebab = existing;
                break;
            }
        }
        if (kebab == null)
        {
            kebab = (Button)GUI.ParseXaml(
                $"<Button {Ns} Width='20' Focusable='False' Padding='0' Margin='0'>" +
                "<Path Width='14' Height='14' Stretch='Uniform' StrokeThickness='1.7' StrokeLineJoin='Round' StrokeStartLineCap='Round' StrokeEndLineCap='Round' " +
                "HorizontalAlignment='Center' VerticalAlignment='Center' " +
                $"Data='{Mark}' Stroke='{{Binding Foreground, RelativeSource={{RelativeSource AncestorType=Button}}}}' /></Button>");
            kebab.Name = ButtonName;
            Button self = kebab;
            kebab.Click += (sender, args) =>
            {
                args.Handled = true;
                Open(self);
            };
            panel.Children.Insert(0, kebab);
        }
        kebab.ToolTip = Loc.Get("menu_tip");

        List<Entry> list = Entries.GetOrCreateValue(kebab);
        Entry entry = list.Find(e => e.Id == id);
        if (entry == null)
        {
            entry = new Entry { Id = id };
            list.Add(entry);
        }
        entry.Order = order;
        entry.Label = label;
        entry.Icon = icon;
        entry.Run = run;
        list.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    // Our own R.A.T.S. mods: their windows are built like the game's, so they pop out and take a look like the game's.
    private static readonly HashSet<string> OwnMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "SeaPowerSensorViews", "SeaPowerSensorViewsFlat", "SeaPowerRadio", "SeaPowerRadioFlat", "SeaPowerTopo", "SeaPowerTopoFlat",
        "SeaPowerBorders", "SeaPowerBordersFlat", "SeaPowerCommandTools", "SeaPowerCommandToolsFlat", "SeaPowerKeel", "SeaPowerPopOut",
    };
    private static readonly HashSet<string> LeftAlone = new HashSet<string>();

    /// <summary>
    /// A window of the game or of one of our mods. Other authors' windows keep their title bar as their author made it: one of
    /// them (Acoustic ID) has its own menu there, so a click on our button opened theirs (board post #1), and a window built
    /// from another mod's resources may not draw on the pop-out monitor at all.
    /// </summary>
    private static bool Ours(GameWindow window)
    {
        Type type = window.DataContext?.GetType();
        if (type == null)
            return true;
        System.Reflection.Assembly assembly = type.Assembly;
        if (assembly == typeof(GameWindow).Assembly || assembly == typeof(SeaPower.GameMain).Assembly)
            return true;
        string name = assembly.GetName().Name ?? "";
        if (OwnMods.Contains(name))
            return true;
        if (LeftAlone.Add(type.FullName ?? type.Name))
            ModLog.Info($"{type.Name} ({name}) is another mod's window: its title bar is left as it is (no pop-out menu).");
        return false;
    }

    /// <summary>Opens the menu again under the button (the Back row of a sub menu).</summary>
    public static void Show(UIElement anchor)
    {
        if (anchor is Button kebab && kebab.Name == ButtonName)
            Open(kebab);
    }

    /// <summary>A "Back" row for the top of a sub menu: closes it and shows the main menu again. The frame's Tag holds its popup.</summary>
    public static Button BackRow(Border frame)
    {
        var back = new Button
        {
            Focusable = false,
            Padding = new Thickness(6f, 2f, 10f, 2f),
            Margin = new Thickness(0f, 0f, 0f, 6f),
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = Row(BackIcon, Loc.Get("back")),
        };
        back.Click += (sender, args) =>
        {
            args.Handled = true;
            if (frame.Tag is Popup popup)
            {
                UIElement anchor = popup.PlacementTarget;
                popup.IsOpen = false;
                Show(anchor);
            }
        };
        return back;
    }

    /// <summary>An icon (stroke path markup, drawn in the button's colour) and a text, side by side.</summary>
    private static UIElement Row(string icon, string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (!string.IsNullOrEmpty(icon) && GUI.ParseXaml(
                $"<Path {Ns} Width='12' Height='12' StrokeThickness='1.2' StrokeLineJoin='Round' StrokeStartLineCap='Round' StrokeEndLineCap='Round' " +
                $"VerticalAlignment='Center' Data='{icon}' Stroke='{{Binding Foreground, RelativeSource={{RelativeSource AncestorType=Button}}}}'/>") is UIElement path)
            row.Children.Add(path);
        row.Children.Add(new TextBlock { Text = text, FontSize = 13f, Margin = new Thickness(icon == null ? 0f : 8f, 0f, 0f, 0f), VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    private static void Open(Button kebab)
    {
        if (!(VisualTreeHelper.GetParent(kebab) is Panel host) || !Entries.TryGetValue(kebab, out List<Entry> list))
            return;
        Loc.Reload();

        // A click on the button while the list is open only closes it (the popup's own light dismiss does that).
        foreach (UIElement child in host.Children)
        {
            if (child is Popup open && open.Name == MenuName && open.IsOpen)
                return;
        }

        var body = new StackPanel();
        var popup = new Popup
        {
            Name = MenuName,
            PlacementTarget = kebab,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
        };
        foreach (Entry entry in list.ToArray())
        {
            Entry chosen = entry;
            var row = new Button
            {
                Focusable = false,
                Padding = new Thickness(8f, 3f, 12f, 3f),
                Margin = new Thickness(0f, 1f, 0f, 1f),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Content = Row(entry.Icon, entry.Label),
            };
            row.Click += (sender, args) =>
            {
                args.Handled = true;
                popup.IsOpen = false;
                try
                {
                    chosen.Run(kebab);
                }
                catch (Exception ex)
                {
                    ModLog.Debug($"title menu: {ex.Message}");
                }
            };
            body.Children.Add(row);
        }
        Border frame = PopupFrame.Frame(4f, 140f);
        frame.Child = body;
        popup.Child = frame;
        PopupFrame.Show(popup, kebab);
    }
}
