using System;
using SeaPower;
using UnityEngine;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>The area the game's windows live in, in the UI's own coordinates (not the screen's pixels).</summary>
internal static class HostArea
{
    /// <summary>The visible UI area, scaled as the game scales its windows (<see cref="UiScale"/>).</summary>
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
        float scale = UiScale(Screen.height);
        return new WinRect(0f, 0f, Screen.width / scale, Screen.height / scale);
    }

    /// <summary>
    /// Pixels per UI unit on a screen this many pixels high: the game's UI scale option times height / 1080 (its
    /// <c>Window.GetHostSize</c>).
    /// </summary>
    public static float UiScale(float pixelHeight) =>
        (Singleton<OptionsManager>.InstanceExists() ? Singleton<OptionsManager>.Instance.UIScale : 1f) * pixelHeight / DesignHeight;

    private const float DesignHeight = 1080f;

    /// <summary>
    /// How much of a window stays on screen at the bottom edge so it can still be grabbed: the title bar (20 units in the
    /// game's window template) and a little of the window below it.
    /// </summary>
    public const float TitleBarReach = 30f;

    /// <summary>
    /// The game anchors some windows (map, World View, unit status) to the bottom / right of their container and turns
    /// the position it finds at load into the gap to that edge. A place the mod sets is absolute, so the anchor must not
    /// act on it. True when an anchor was released.
    /// </summary>
    public static bool ReleaseAnchors(GameWindow window)
    {
        if (!window.AnchorBottom && !window.AnchorRight)
            return false;
        window.AnchorBottom = false;
        window.AnchorRight = false;
        return true;
    }
}
