using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>How a window looks: how see-through its background is, how big its content is, whether its text has an outline.</summary>
internal readonly struct LookValues
{
    public static readonly LookValues Default = new LookValues(1f, 1f, false);

    public readonly float Opacity;
    public readonly float Scale;
    public readonly bool Outline;

    public LookValues(float opacity, float scale, bool outline)
    {
        Opacity = LookMath.Clamp(opacity, 0f, 1f);
        Scale = LookMath.Clamp(scale, LookMath.MinScale, LookMath.MaxScale);
        Outline = outline;
    }

    public bool IsDefault => Opacity >= 0.999f && Math.Abs(Scale - 1f) < 0.001f && !Outline;
    public LookValues WithOpacity(float v) => new LookValues(v, Scale, Outline);
    public LookValues WithScale(float v) => new LookValues(Opacity, v, Outline);
    public LookValues WithOutline(bool v) => new LookValues(Opacity, Scale, v);
}

/// <summary>
/// The looks and levels of the remembered windows, by window pattern. Stored as <c>pattern|opacity|scale|outline;...</c>
/// and <c>pattern|level;...</c> (level -1 below the other windows, 1 above them, 0 = not listed). Pure logic, no game types.
/// </summary>
internal sealed class LookStore
{
    private readonly Dictionary<string, LookValues> looks = new Dictionary<string, LookValues>();
    private readonly Dictionary<string, int> levels = new Dictionary<string, int>();

    public static LookStore Parse(string looksText, string levelsText)
    {
        var store = new LookStore();
        foreach (string entry in (looksText ?? "").Split(';'))
        {
            string[] f = entry.Split('|');
            if (f.Length < 3 || f.Length > 4 || Normalize(f[0]).Length == 0)
                continue;
            if (!TryFloat(f[1], out float opacity) || !TryFloat(f[2], out float scale))
                continue;
            bool outline = f.Length == 4 && TryFloat(f[3], out float o) && o > 0.5f;
            store.Set(f[0], new LookValues(opacity, scale, outline));
        }
        foreach (string entry in (levelsText ?? "").Split(';'))
        {
            string[] f = entry.Split('|');
            if (f.Length == 2 && Normalize(f[0]).Length > 0 && int.TryParse(f[1].Trim(), out int level))
                store.SetLevel(f[0], level);
        }
        return store;
    }

    public LookValues Get(string pattern) =>
        pattern != null && looks.TryGetValue(Normalize(pattern), out LookValues v) ? v : LookValues.Default;

    public void Set(string pattern, LookValues values)
    {
        string key = Normalize(pattern);
        if (key.Length == 0)
            return;
        if (values.IsDefault)
            looks.Remove(key);
        else
            looks[key] = values;
    }

    /// <summary>-1 below the other windows, 0 normal, 1 above them.</summary>
    public int LevelOf(string pattern) =>
        pattern != null && levels.TryGetValue(Normalize(pattern), out int level) ? level : 0;

    public void SetLevel(string pattern, int level)
    {
        string key = Normalize(pattern);
        if (key.Length == 0)
            return;
        if (level == 0)
            levels.Remove(key);
        else
            levels[key] = level < 0 ? -1 : 1;
    }

    public string SerializeLooks()
    {
        var sb = new StringBuilder();
        foreach (var pair in looks)
        {
            if (sb.Length > 0)
                sb.Append(';');
            sb.Append(pair.Key).Append('|').Append(Fmt(pair.Value.Opacity)).Append('|').Append(Fmt(pair.Value.Scale))
              .Append('|').Append(pair.Value.Outline ? '1' : '0');
        }
        return sb.ToString();
    }

    public string SerializeLevels()
    {
        var sb = new StringBuilder();
        foreach (var pair in levels)
        {
            if (sb.Length > 0)
                sb.Append(';');
            sb.Append(pair.Key).Append('|').Append(pair.Value);
        }
        return sb.ToString();
    }

    /// <summary>The same normalisation as <see cref="PatternList"/> applies to its patterns: no spaces, lower case.</summary>
    internal static string Normalize(string pattern) => (pattern ?? "").Replace(" ", "").Trim().ToLowerInvariant();

    private static bool TryFloat(string text, out float value) =>
        float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static string Fmt(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
