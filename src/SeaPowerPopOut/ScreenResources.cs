using Noesis;

namespace SeaPowerPopOut;

/// <summary>
/// The pop-out screen's copy of the main game view's resources (the window templates and everything they use). A flat
/// copy, so the main view's own dictionaries never get a second owner.
/// </summary>
internal static class ScreenResources
{
    // Merged dictionaries nest a few levels in the game; the cap only guards against a loop.
    private const int MaxDepth = 16;

    /// <summary>Every entry of a dictionary and its merged dictionaries, in lookup precedence, in one new dictionary.</summary>
    public static ResourceDictionary Flatten(ResourceDictionary source)
    {
        var flat = new ResourceDictionary();
        CopyInto(source, flat, 0);
        ModLog.Debug($"{flat.Count} resources copied for the pop-out screen");
        return flat;
    }

    /// <summary>
    /// Brings over what was added to <paramref name="from"/> since the copy was made. A window remembered as popped out
    /// opens the screen while the mission UI is still loading, and other mods add the templates of their windows after
    /// that; without its template a window's content draws nothing.
    /// </summary>
    public static void CopyMissing(ResourceDictionary from, ResourceDictionary to) => CopyMissing(from, to, 0);

    private static void CopyInto(ResourceDictionary from, ResourceDictionary to, int depth)
    {
        if (from == null || depth > MaxDepth)
            return;
        ResourceDictionaryCollection merged = from.MergedDictionaries;
        for (int i = 0; merged != null && i < merged.Count; i++)
            CopyInto(merged[i], to, depth + 1);   // later merged dictionaries win over earlier ones,
        foreach (object key in from.Keys)         // the dictionary's own entries over all of them
            to[key] = from[key];
    }

    // Lookup precedence again, but the first hit counts: own entries, then the merged dictionaries from the last.
    private static void CopyMissing(ResourceDictionary from, ResourceDictionary to, int depth)
    {
        if (from == null || depth > MaxDepth)
            return;
        foreach (object key in from.Keys)
        {
            if (!to.Contains(key))
                to[key] = from[key];
        }
        ResourceDictionaryCollection merged = from.MergedDictionaries;
        for (int i = (merged?.Count ?? 0) - 1; i >= 0; i--)
            CopyMissing(merged[i], to, depth + 1);
    }
}
