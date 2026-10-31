using System;
using System.Text;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>Searching inside the game's XAML-formatted texts without hitting the markup.</summary>
internal static class PlainText
{
    /// <summary>The text with every <c>&lt;tag ...&gt;</c> removed and runs of white space made single spaces.</summary>
    public static string StripMarkup(string markup)
    {
        if (string.IsNullOrEmpty(markup))
            return "";
        var sb = new StringBuilder(markup.Length);
        bool inTag = false, space = false;
        foreach (char c in markup)
        {
            if (c == '<')
            {
                inTag = true;
                continue;
            }
            if (c == '>' && inTag)
            {
                inTag = false;
                continue;
            }
            if (inTag)
                continue;
            if (char.IsWhiteSpace(c))
            {
                space = sb.Length > 0;
                continue;
            }
            if (space)
            {
                sb.Append(' ');
                space = false;
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>True if <paramref name="markup"/>'s visible text contains <paramref name="needleLower"/> (already lower case).</summary>
    public static bool Contains(string markup, string needleLower) =>
        StripMarkup(markup).IndexOf(needleLower, StringComparison.OrdinalIgnoreCase) >= 0;
}
