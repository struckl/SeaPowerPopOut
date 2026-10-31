using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SeaPower;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Unit Reference search: unless "search by name only" is on, a unit also matches when the search text is in its
/// description or in its id (the name of its .ini file). The game looks at names, class names, weapons, sensors and
/// aircraft only.
/// </summary>
internal static class ExtendedSearch
{
    private static readonly ConditionalWeakTable<MuseumEntry, string> Plain = new ConditionalWeakTable<MuseumEntry, string>();

    public static bool Apply(Harmony harmony)
    {
        var matcher = AccessTools.Method(typeof(UnitTreeBrowserViewModel), "MatchesTextSearch",
            new[] { typeof(MuseumObjectEntry), typeof(string), typeof(bool) });
        if (matcher == null)
        {
            ModLog.Warn("Unit Reference text match not found (game updated?) - search stays as the game has it.");
            return false;
        }
        harmony.Patch(matcher, postfix: new HarmonyMethod(typeof(ExtendedSearch), nameof(Matched)));
        return true;
    }

    private static void Matched(MuseumObjectEntry entry, string searchLower, bool nameOnly, ref bool __result)
    {
        if (__result || nameOnly || string.IsNullOrEmpty(searchLower))
            return;
        if (Contains(entry.IniRef, searchLower) || PlainDescription(entry).IndexOf(searchLower, StringComparison.OrdinalIgnoreCase) >= 0)
            __result = true;
    }

    private static bool Contains(string text, string needleLower) =>
        !string.IsNullOrEmpty(text) && text.IndexOf(needleLower, StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>The description without markup, worked out once per entry (a search visits every unit).</summary>
    private static string PlainDescription(MuseumEntry entry)
    {
        if (!Plain.TryGetValue(entry, out string text))
        {
            text = PlainText.StripMarkup(entry.Description);
            Plain.Add(entry, text);
        }
        return text;
    }
}
