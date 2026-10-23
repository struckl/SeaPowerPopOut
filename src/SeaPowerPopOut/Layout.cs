using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SeaPowerPopOut;

/// <summary>
/// Which kinds of window open on the pop-out monitor, and where each sits there. Keyed by the window content's view
/// model type ("FormationManagerViewModel"), so every sensor window or every unit status window shares one entry.
/// Kept in memory and written to the config at most every few seconds – a window drag changes it every frame.
/// </summary>
internal static class Layout
{
    internal struct Placement
    {
        public float X, Y, Width, Height;   // Width/Height NaN = the window sizes itself
    }

    private static readonly HashSet<string> Popped = new HashSet<string>();
    private static readonly Dictionary<string, Placement> Placements = new Dictionary<string, Placement>();
    private static bool dirty;

    public static void Load()
    {
        Popped.Clear();
        foreach (string name in Settings.PoppedWindows.Value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            Popped.Add(name.Trim());

        Placements.Clear();
        foreach (string entry in Settings.WindowPlacement.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string[] kv = entry.Split('=');
            string[] n = kv.Length == 2 ? kv[1].Split(',') : Array.Empty<string>();
            if (n.Length != 4)
                continue;
            Placements[kv[0].Trim()] = new Placement
            {
                X = Parse(n[0]), Y = Parse(n[1]), Width = Parse(n[2]), Height = Parse(n[3]),
            };
        }
        dirty = false;
    }

    public static bool IsPopped(string kind) => Popped.Contains(kind);

    public static void SetPopped(string kind, bool popped)
    {
        if (popped ? Popped.Add(kind) : Popped.Remove(kind))
            dirty = true;
    }

    public static bool TryGetPlacement(string kind, out Placement placement) => Placements.TryGetValue(kind, out placement);

    public static void SetPlacement(string kind, Placement placement)
    {
        if (Placements.TryGetValue(kind, out Placement old) && Same(old, placement))
            return;
        Placements[kind] = placement;
        dirty = true;
    }

    /// <summary>Writes the config file if anything changed since the last save.</summary>
    public static void Save()
    {
        if (!dirty)
            return;
        dirty = false;
        Settings.PoppedWindows.Value = string.Join(",", Popped.OrderBy(k => k));
        Settings.WindowPlacement.Value = string.Join(";", Placements.OrderBy(p => p.Key).Select(p =>
            $"{p.Key}={Format(p.Value.X)},{Format(p.Value.Y)},{Format(p.Value.Width)},{Format(p.Value.Height)}"));
        ModLog.Debug($"layout saved: popped [{Settings.PoppedWindows.Value}]");
    }

    private static bool Same(Placement a, Placement b) =>
        Near(a.X, b.X) && Near(a.Y, b.Y) && Near(a.Width, b.Width) && Near(a.Height, b.Height);

    private static bool Near(float a, float b) => (float.IsNaN(a) && float.IsNaN(b)) || Math.Abs(a - b) < 0.5f;

    private static float Parse(string s) =>
        float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : float.NaN;

    private static string Format(float v) => float.IsNaN(v) ? "auto" : v.ToString("0", CultureInfo.InvariantCulture);
}
