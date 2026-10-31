using System;
using System.Collections.Generic;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// One desktop: where the remembered windows sit and whether the World View is open. Stored as text in the config:
/// sections joined by <c>~</c>, <c>L:</c> = the layouts (same text as the saved layouts), <c>W:</c> = open / closed.
/// A missing section means "leave that part as it is".
/// </summary>
internal sealed class DesktopSet
{
    public SavedLayouts Layouts = new SavedLayouts();
    public bool? WorldViewOpen;

    public bool IsEmpty => !WorldViewOpen.HasValue && !HasLayouts;

    private bool HasLayouts
    {
        get
        {
            foreach (var _ in Layouts.All)
                return true;
            return false;
        }
    }

    public static DesktopSet Parse(string text)
    {
        var set = new DesktopSet();
        foreach (string section in (text ?? "").Split(new[] { '~' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (section.Length < 2 || section[1] != ':')
                continue;
            string body = section.Substring(2);
            switch (section[0])
            {
                case 'L':
                    set.Layouts = SavedLayouts.Parse(body);
                    break;
                case 'W':
                    set.WorldViewOpen = body == "open" ? true : body == "closed" ? false : (bool?)null;
                    break;
            }
        }
        return set;
    }

    public string ToText()
    {
        var sections = new List<string>();
        if (HasLayouts)
            sections.Add("L:" + Layouts.Serialize());
        if (WorldViewOpen.HasValue)
            sections.Add("W:" + (WorldViewOpen.Value ? "open" : "closed"));
        return string.Join("~", sections);
    }
}
