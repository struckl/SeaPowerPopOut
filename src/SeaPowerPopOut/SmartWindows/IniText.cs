using System;
using System.Collections.Generic;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>Parses <c>key=text</c> lines; blank lines and lines starting with <c>#</c> or <c>;</c> are skipped.</summary>
internal static class IniText
{
    public static Dictionary<string, string> Parse(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            int eq = line.IndexOf('=');
            if (eq <= 0 || line[0] == '#' || line[0] == ';')
                continue;
            result[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
        }
        return result;
    }
}
