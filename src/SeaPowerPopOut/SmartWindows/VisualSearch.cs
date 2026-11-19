using System;
using System.Collections.Generic;
using Noesis;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Searches in the visual tree of a window, depth first in the order the elements are drawn. Every walk has a budget of
/// elements: a game window's tree has a few thousand, a list can hold many more, and the walks run while the game
/// draws.
/// </summary>
internal static class VisualSearch
{
    /// <summary>Enough for any game window's whole tree.</summary>
    public const int WholeWindow = 20000;

    public enum Step
    {
        Into,       // go on below this element
        Over,       // leave out what is below it
        Stop,       // done
    }

    /// <summary>The first element below <paramref name="root"/> (not the root itself) of type T that matches.</summary>
    public static T First<T>(DependencyObject root, Func<T, bool> match = null, int budget = WholeWindow) where T : DependencyObject
    {
        T found = null;
        Walk(root, budget, node =>
        {
            if (node != root && node is T hit && (match == null || match(hit)))
            {
                found = hit;
                return Step.Stop;
            }
            return Step.Into;
        });
        return found;
    }

    /// <summary>Visits <paramref name="root"/> and the elements below it, as long as the budget lasts.</summary>
    public static void Walk(DependencyObject root, int budget, Func<DependencyObject, Step> visit)
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        for (; stack.Count > 0 && budget > 0; budget--)
        {
            DependencyObject node = stack.Pop();
            Step step = visit(node);
            if (step == Step.Stop)
                return;
            if (step == Step.Over)
                continue;
            for (int i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--)
            {
                DependencyObject child = VisualTreeHelper.GetChild(node, i);
                if (child != null)
                    stack.Push(child);
            }
        }
    }
}
