using System;
using System.Collections.Generic;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>Stable ordering of a list that has to be reordered in place (an observable collection the UI is bound to).</summary>
internal static class StableOrder
{
    /// <summary>
    /// The items in the order <paramref name="compare"/> gives; items that compare equal keep their current order.
    /// Returns the old index of each item in its new place.
    /// </summary>
    public static List<int> NewOrder<T>(IList<T> items, Comparison<T> compare)
    {
        var order = new List<int>(items.Count);
        for (int i = 0; i < items.Count; i++)
            order.Add(i);
        order.Sort((a, b) =>
        {
            int c = compare(items[a], items[b]);
            return c != 0 ? c : a.CompareTo(b);
        });
        return order;
    }

    /// <summary>
    /// Brings <paramref name="items"/> into the order <paramref name="compare"/> gives, using only moves, so a UI bound
    /// to the collection sees moves and not a removal and re-insertion of every item. Returns true if anything moved.
    /// </summary>
    public static bool Sort<T>(IList<T> items, Comparison<T> compare, Action<int, int> move)
    {
        List<int> order = NewOrder(items, compare);
        var current = new List<int>(order.Count);      // current[i] = original index of the item now at position i
        for (int i = 0; i < order.Count; i++)
            current.Add(i);

        bool moved = false;
        for (int target = 0; target < order.Count; target++)
        {
            int at = current.IndexOf(order[target]);
            if (at == target)
                continue;
            move(at, target);
            int value = current[at];
            current.RemoveAt(at);
            current.Insert(target, value);
            moved = true;
        }
        return moved;
    }
}
