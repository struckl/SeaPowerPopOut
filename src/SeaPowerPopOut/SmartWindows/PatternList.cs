using System;
using System.Collections.Generic;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// A comma-separated list of window name patterns, e.g. <c>Unit Reference*,Event Log</c>. Matching ignores case and
/// spaces; <c>*</c> matches any text (also nothing). A pattern must match a name from its beginning, so use
/// <c>*text*</c> to match anywhere.
/// </summary>
internal sealed class PatternList
{
    private readonly List<string> patterns = new List<string>();

    public PatternList(string commaSeparated)
    {
        foreach (string raw in (commaSeparated ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string p = Squash(raw);
            if (p.Length > 0)
                patterns.Add(p);
        }
    }

    public int Count => patterns.Count;

    /// <summary>The first pattern matching one of <paramref name="names"/>, or null. The pattern text is the key
    /// under which the layout of all windows matching it is shared.</summary>
    public string Match(params string[] names)
    {
        foreach (string pattern in patterns)
        foreach (string name in names)
        {
            if (!string.IsNullOrEmpty(name) && Glob(pattern, Squash(name)))
                return pattern;
        }
        return null;
    }

    /// <summary>A pattern or name as it is compared and stored (the key of a remembered layout).</summary>
    internal static string Squash(string s) => s.Replace(" ", "").Trim().ToLowerInvariant();

    /// <summary>Wildcard match of the whole text; <c>*</c> only.</summary>
    internal static bool Glob(string pattern, string text)
    {
        int p = 0, t = 0, star = -1, mark = 0;
        while (t < text.Length)
        {
            if (p < pattern.Length && pattern[p] == '*')
            {
                star = p++;
                mark = t;
            }
            else if (p < pattern.Length && pattern[p] == text[t])
            {
                p++;
                t++;
            }
            else if (star >= 0)
            {
                p = star + 1;
                t = ++mark;
            }
            else
            {
                return false;
            }
        }
        while (p < pattern.Length && pattern[p] == '*')
            p++;
        return p == pattern.Length;
    }
}
