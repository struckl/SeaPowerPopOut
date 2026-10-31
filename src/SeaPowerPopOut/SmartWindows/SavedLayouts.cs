using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>Where a remembered window sits. NaN width / height = the window sizes itself.</summary>
internal struct SavedLayout
{
    public float X, Y, W, H;
    public bool Maximized;

    public bool SameAs(SavedLayout o) =>
        Near(X, o.X) && Near(Y, o.Y) && Near(W, o.W) && Near(H, o.H) && Maximized == o.Maximized;

    private static bool Near(float a, float b) => (float.IsNaN(a) && float.IsNaN(b)) || Math.Abs(a - b) < 0.5f;
}

/// <summary>
/// The remembered layouts, one per window pattern. Kept in memory; <see cref="Serialize"/> / <see cref="Parse"/>
/// turn them into the text stored in the config (<c>pattern=x,y,w,h[,max];...</c>).
/// </summary>
internal sealed class SavedLayouts
{
    private readonly Dictionary<string, SavedLayout> byPattern = new Dictionary<string, SavedLayout>();

    public bool Dirty { get; private set; }

    public IEnumerable<KeyValuePair<string, SavedLayout>> All => byPattern;

    public bool TryGet(string pattern, out SavedLayout layout) => byPattern.TryGetValue(pattern, out layout);

    public void Set(string pattern, SavedLayout layout)
    {
        if (byPattern.TryGetValue(pattern, out SavedLayout old) && old.SameAs(layout))
            return;
        byPattern[pattern] = layout;
        Dirty = true;
    }

    /// <summary>Marks the remembered window of a pattern as maximized or restored; the rectangle stays as it was.</summary>
    public void SetMaximized(string pattern, bool maximized)
    {
        if (!byPattern.TryGetValue(pattern, out SavedLayout layout) || layout.Maximized == maximized)
            return;
        layout.Maximized = maximized;
        byPattern[pattern] = layout;
        Dirty = true;
    }

    public void MarkSaved() => Dirty = false;

    public string Serialize() =>
        string.Join(";", byPattern.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p =>
            $"{p.Key}={Fmt(p.Value.X)},{Fmt(p.Value.Y)},{Fmt(p.Value.W)},{Fmt(p.Value.H)}{(p.Value.Maximized ? ",max" : "")}"));

    public static SavedLayouts Parse(string text)
    {
        var result = new SavedLayouts();
        foreach (string entry in (text ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = entry.LastIndexOf('=');
            if (eq <= 0)
                continue;
            string[] n = entry.Substring(eq + 1).Split(',');
            if (n.Length < 4)
                continue;
            result.byPattern[entry.Substring(0, eq).Trim()] = new SavedLayout
            {
                X = Num(n[0]), Y = Num(n[1]), W = Num(n[2]), H = Num(n[3]),
                Maximized = n.Length > 4 && n[4].Trim() == "max",
            };
        }
        return result;
    }

    private static float Num(string s) =>
        float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : float.NaN;

    private static string Fmt(float v) => float.IsNaN(v) ? "auto" : v.ToString("0", CultureInfo.InvariantCulture);
}
