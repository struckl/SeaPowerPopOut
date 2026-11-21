using System;
using System.Collections.Generic;
using System.Globalization;
using Noesis;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

using static PopupFrame;

/// <summary>
/// The list under the "Upgrade ship" button: one card per ship that can be replaced, with what the swap costs and
/// returns, and an Upgrade button that does it. A popup next to the button (Noesis shows a popup only while it is
/// part of the visual tree).
/// </summary>
internal static class UpgradeMenu
{
    private static Popup open;

    public static void Open(Button anchor, LinearCampaignTaskForceBuilderViewModel vm)
    {
        Loc.Reload();
        UpgradeGame.Unavailable(vm, out List<UpgradeCandidate> candidates);
        if (candidates.Count == 0 || !(VisualTreeHelper.GetParent(anchor) is Panel))
            return;
        Close();

        string newShip = vm.SelectedRosterItem?.InspectionTitle ?? "";
        var rows = new StackPanel { Margin = new Thickness(2f) };
        rows.Children.Add(Text(Loc.Get("up_title") + " - " + newShip, "#FFFFFFFF", 14f, bold: true));
        rows.Children.Add(Text(Loc.Get("up_warn"), "#FFFFC060", 11f, wrap: true));
        foreach (UpgradeCandidate c in candidates)
            rows.Children.Add(Card(c, vm, newShip));

        Border frame = Frame(8f, 420f);
        frame.MaxWidth = 560f;
        frame.Child = new ScrollViewer { MaxHeight = 560f, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = rows };
        var popup = new Popup { Child = frame, PlacementTarget = anchor, Placement = PlacementMode.Top, StaysOpen = false };
        open = popup;
        Show(popup, anchor, () =>
        {
            if (ReferenceEquals(open, popup))
                open = null;
        });
    }

    public static void Close()
    {
        if (open != null)
            open.IsOpen = false;
        open = null;
    }

    private static UIElement Card(UpgradeCandidate c, LinearCampaignTaskForceBuilderViewModel vm, string newShip)
    {
        var text = new StackPanel { Margin = new Thickness(0f, 0f, 10f, 0f) };
        text.Children.Add(Text(c.Name + "  ->  " + newShip, "#FFFFFFFF", 13f, bold: true, wrap: true));
        text.Children.Add(Text(Format(c.Decommission ? "up_sale_decom" : "up_sale_dismiss", c.Refund), "#FFE0E0E0", 12f, wrap: true));
        text.Children.Add(Text(Format(c.Surcharge > 0 ? "up_price_sur" : "up_price", c.Cost, c.Surcharge), "#FFE0E0E0", 12f, wrap: true));
        text.Children.Add(Text($"{Loc.Get("up_crew")}: {UpgradeGame.SkillName(c.OldSkill)} -> {UpgradeGame.SkillName(c.NewSkill)}"
            + (c.NewSkill == c.OldSkill ? $" ({Loc.Get("up_min")})" : ""), c.NewSkill < c.OldSkill ? "#FFFFC060" : "#FFA0A0A0", 12f));
        int after = UpgradeRules.PointsAfter(c.Available, c.RefundBase, c.Cost, c.Surcharge);
        text.Children.Add(Text(Format("up_points2", c.Available, after), after >= 0 ? "#FF80E080" : "#FFFF7070", 12f));
        if (c.Cap > 0)
        {
            int fleet = UpgradeRules.FleetAfter(c.FleetValue, c.OldCap, c.NewCap);
            text.Children.Add(Text(Format("up_fleet", c.FleetValue, fleet, c.Cap), fleet <= c.Cap ? "#FF80E080" : "#FFFF7070", 12f));
        }
        if (c.Aircraft > 0)
            text.Children.Add(Text(Format("up_aircraft", c.Aircraft), "#FFFFC060", 12f, wrap: true));
        string block = UpgradeGame.BlockKey(c.Block);
        if (block != null)
            text.Children.Add(Text(Loc.Get(block), "#FFFF7070", 12f, bold: true, wrap: true));

        var go = new Button
        {
            Content = Loc.Get("up_do"),
            MinWidth = 90f,
            Height = 30f,
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = block == null,
        };
        go.Click += (s, e) =>
        {
            e.Handled = true;
            Close();
            try
            {
                string problem = UpgradeGame.Perform(vm, c);
                if (problem != null)
                    Rig.Log.Warn($"Upgrade ship: {Loc.Get(problem)}");
            }
            catch (Exception ex)
            {
                Rig.Log.Warn($"Upgrade ship failed: {ex.Message}");
                Rig.Log.Debug($"upgrade ship: {ex}");
            }
        };

        var row = new Grid { Opacity = block == null ? 1f : 0.65f };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1f, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(text);
        Grid.SetColumn(go, 1);
        row.Children.Add(go);
        return new Border
        {
            Child = row,
            Background = Brush("#40FFFFFF"),
            BorderBrush = Brush("#FF606060"),
            BorderThickness = new Thickness(1f),
            Padding = new Thickness(8f),
            Margin = new Thickness(0f, 6f, 0f, 0f),
        };
    }

    private static string Format(string key, params object[] args) => string.Format(CultureInfo.InvariantCulture, Loc.Get(key), args);

    private static TextBlock Text(string text, string colour, float size, bool bold = false, bool wrap = false) => new TextBlock
    {
        Text = text,
        Foreground = Brush(colour),
        FontSize = size,
        FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
        TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
        Margin = new Thickness(0f, 1f, 0f, 1f),
    };
}
