using System;
using SeaPower;
using UnityEngine;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>The area the game's windows live in, in the UI's own coordinates (not the screen's pixels).</summary>
internal static class HostArea
{
    /// <summary>
    /// The visible UI area, scaled as the game scales its windows: screen size divided by the UI scale and by
    /// height / 1080.
    /// </summary>
    public static WinRect Of(GameWindow window)
    {
        WinRect screen = OfScreen();
        float width = screen.W;
        float height = screen.H;

        // Inside a container that is bigger than the window the game uses the container's size instead.
        var parent = Noesis.VisualTreeHelper.GetParent(window);
        while (parent != null)
        {
            if (parent is Noesis.FrameworkElement element
                && element.ActualWidth > window.ActualWidth + 1f && element.ActualHeight > window.ActualHeight + 1f)
            {
                width = element.ActualWidth;
                height = element.ActualHeight;
                break;
            }
            parent = Noesis.VisualTreeHelper.GetParent(parent);
        }
        // Never larger than what is visible: the map and World View windows sit in a container laid out for the
        // game's second display, which can be taller than the screen (a window placed by it ended up below the screen).
        return new WinRect(0f, 0f, Math.Min(width, screen.W), Math.Min(height, screen.H));
    }

    /// <summary>The whole screen in UI coordinates.</summary>
    public static WinRect OfScreen()
    {
        float scale = (Singleton<OptionsManager>.InstanceExists() ? Singleton<OptionsManager>.Instance.UIScale : 1f)
                      * Screen.height / 1080f;
        return new WinRect(0f, 0f, Screen.width / scale, Screen.height / scale);
    }
}
