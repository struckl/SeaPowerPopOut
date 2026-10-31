using System.Collections.Generic;
using Noesis;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>Finds the parts of a game window's template that the mod adds to.</summary>
internal static class TitleBar
{
    /// <summary>The panel that holds the title bar buttons (minimize, maximize, close ...), or null.</summary>
    public static Panel FindButtonsPanel(GameWindow window)
    {
        DataTemplate template = window.ButtonsTemplate;
        var queue = new Queue<Visual>();
        queue.Enqueue(window);
        while (queue.Count > 0)
        {
            Visual v = queue.Dequeue();
            if (v is ContentPresenter presenter && template != null && Equals(presenter.ContentTemplate, template)
                && VisualTreeHelper.GetChildrenCount(presenter) > 0
                && VisualTreeHelper.GetChild(presenter, 0) is Panel templated)
                return templated;
            if (v is Button button && button.Name == "CloseButton" && VisualTreeHelper.GetParent(button) is Panel named)
                return named;

            int n = VisualTreeHelper.GetChildrenCount(v);
            for (int i = 0; i < n; i++)
            {
                if (VisualTreeHelper.GetChild(v, i) is Visual child && !(child is GameWindow))
                    queue.Enqueue(child);
            }
        }
        return null;
    }

    /// <summary>The grid at the root of the window's template, or null if it is built differently.</summary>
    public static Grid FindRootGrid(GameWindow window) =>
        VisualTreeHelper.GetChildrenCount(window) > 0 ? VisualTreeHelper.GetChild(window, 0) as Grid : null;
}
