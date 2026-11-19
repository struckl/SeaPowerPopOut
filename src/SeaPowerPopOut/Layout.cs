using System;
using System.Collections.Generic;
using System.Linq;
using SeaPowerPopOut.SmartWindows;

namespace SeaPowerPopOut;

/// <summary>
/// Which kinds of window open on the pop-out monitor, and where each sits there. Keyed by the window content's view
/// model type ("FormationManagerViewModel"), so every sensor window or every unit status window shares one entry. The
/// places use the same store and text as the main screen's remembered layouts (<see cref="SavedLayouts"/>).
/// Kept in memory and written to the config at most every few seconds - a window drag changes it every frame; a hand
/// edit of the config while the game runs is overwritten by the next change.
/// </summary>
internal static class Layout
{
    private static readonly HashSet<string> Popped = new HashSet<string>();
    private static SavedLayouts placements = new SavedLayouts();
    private static bool poppedChanged;

    public static void Load()
    {
        Popped.Clear();
        foreach (string name in Settings.PoppedWindows.Value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            Popped.Add(name.Trim());
        placements = SavedLayouts.Parse(Settings.WindowPlacement.Value);
        poppedChanged = false;
    }

    public static bool IsPopped(string kind) => Popped.Contains(kind);

    public static void SetPopped(string kind, bool popped)
    {
        if (popped ? Popped.Add(kind) : Popped.Remove(kind))
            poppedChanged = true;
    }

    /// <summary>NaN width / height = the window sizes itself.</summary>
    public static bool TryGetPlacement(string kind, out SavedLayout placement) => placements.TryGet(kind, out placement);

    public static void SetPlacement(string kind, SavedLayout placement) => placements.Set(kind, placement);

    /// <summary>Writes the config file if anything changed since the last save.</summary>
    public static void Save()
    {
        if (!poppedChanged && !placements.Dirty)
            return;
        poppedChanged = false;
        placements.MarkSaved();
        Settings.PoppedWindows.Value = string.Join(",", Popped.OrderBy(k => k));
        Settings.WindowPlacement.Value = placements.Serialize();
        ModLog.Debug($"layout saved: popped [{Settings.PoppedWindows.Value}]");
    }
}
