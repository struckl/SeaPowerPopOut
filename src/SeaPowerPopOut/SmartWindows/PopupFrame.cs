using System;
using Noesis;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// The look and the opening of the mod's own popups (title bar menu, settings, look menu, upgrade list): one dark frame
/// with a grey edge, shown next to the element that opened it.
/// </summary>
internal static class PopupFrame
{
    public static Brush Brush(string argb)
    {
        ColorText.TryParse(argb, out byte a, out byte r, out byte g, out byte b);
        return new SolidColorBrush(Color.FromArgb(a, r, g, b));
    }

    public static Border Frame(float padding, float minWidth) => new Border
    {
        Background = Brush("#F0202020"),
        BorderBrush = Brush("#FF808080"),
        BorderThickness = new Thickness(1f),
        Padding = new Thickness(padding),
        MinWidth = minWidth,
    };

    /// <summary>
    /// Opens <paramref name="popup"/> beside <paramref name="anchor"/>. Noesis only shows a popup that is part of the
    /// visual tree, so it lives in the anchor's panel while it is open. On the pop-out monitor it must also be part of the
    /// see-through cut-out, or it is cut off. False when the anchor has no panel to hold it.
    /// </summary>
    public static bool Show(Popup popup, UIElement anchor, Action closed = null)
    {
        if (!(VisualTreeHelper.GetParent(anchor) is Panel host))
            return false;
        popup.Closed += (sender, args) =>
        {
            host.Children.Remove(popup);
            closed?.Invoke();
        };
        host.Children.Add(popup);
        popup.IsOpen = true;
        PopOutScreen.Instance?.PopupOpened(popup, anchor);
        return true;
    }
}
