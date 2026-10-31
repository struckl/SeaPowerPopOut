using System;
using System.Collections.Generic;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>Keeps a shown list of countries in step with the countries that have something to show. No game types.</summary>
internal static class NationSync
{
    /// <summary>
    /// Makes <paramref name="shown"/> hold, in the order of <paramref name="all"/>, the first entry (the "All" choice), the
    /// selected entry, and every entry whose id is in <paramref name="present"/>. Entries are inserted and removed in
    /// place so a list the UI is bound to does not reset. <paramref name="present"/> null = show everything.
    /// </summary>
    public static void Sync<T>(IList<T> shown, IList<T> all, Func<T, string> idOf, T selected, ISet<string> present)
        where T : class
    {
        int index = 0;
        for (int i = 0; i < all.Count; i++)
        {
            T nation = all[i];
            string id = idOf(nation);
            bool wanted = i == 0 || present == null || nation == selected || (id != null && present.Contains(id));
            bool isShownHere = index < shown.Count && shown[index] == nation;
            if (wanted)
            {
                if (!isShownHere)
                    shown.Insert(index, nation);
                index++;
            }
            else if (isShownHere)
            {
                shown.RemoveAt(index);
            }
        }
    }
}
