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
    private const string Dots = "M6,1.5 L6,1.6 M6,6 L6,6.1 M6,10.4 L6,10.5";
    private const string Ns = "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'";

    private sealed class Entry
    {
        public string Id, Label;
        public int Order;
        public Action<UIElement> Run;
    }

    private static readonly ConditionalWeakTable<Button, List<Entry>> Entries = new ConditionalWeakTable<Button, List<Entry>>();

    /// <summary>Adds or updates one entry of the window's menu; the button is made on the first call. Lower order is higher up.</summary>
    public static void Set(GameWindow window, string id, int order, string label, Action<UIElement> run)
    {
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
                "<Path Width='12' Height='12' StrokeThickness='2' StrokeStartLineCap='Round' StrokeEndLineCap='Round' " +
                "HorizontalAlignment='Center' VerticalAlignment='Center' " +
                $"Data='{Dots}' Stroke='{{Binding Foreground, RelativeSource={{RelativeSource AncestorType=Button}}}}' /></Button>");
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
        entry.Run = run;
        list.Sort((a, b) => a.Order.CompareTo(b.Order));
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
                Content = new TextBlock { Text = entry.Label, FontSize = 13f },
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
        popup.Child = new Border
        {
            Background = Brush("#F0202020"),
            BorderBrush = Brush("#FF808080"),
            BorderThickness = new Thickness(1f),
            Padding = new Thickness(4f),
            MinWidth = 140f,
            Child = body,
        };
        // Noesis only shows a popup that is part of the visual tree: it lives next to the button while it is open.
        popup.Closed += (sender, args) => host.Children.Remove(popup);
        host.Children.Add(popup);
        popup.IsOpen = true;
        PopOutScreen.Instance?.PopupOpened(popup, kebab);
    }

    private static Brush Brush(string argb)
    {
        ColorText.TryParse(argb, out byte a, out byte r, out byte g, out byte b);
        return new SolidColorBrush(Color.FromArgb(a, r, g, b));
    }
}
