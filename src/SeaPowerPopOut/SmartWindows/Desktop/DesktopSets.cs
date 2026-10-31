using System;
using UnityEngine;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// The desktops: which one is active, where they are stored (the config, one text per desktop while it is not the
/// active one) and the switch. A switch stores the live arrangement in the leaving desktop's slot, then applies the next
/// desktop's, or its built-in arrangement the first time (<see cref="DesktopPresets"/>).
/// </summary>
internal static class DesktopSets
{
    /// <summary>The game shows the World View on another monitor (its own option): the desktops only arrange the windows here.</summary>
    private static bool WorldElsewhere => DesktopGame.Vm?.WorldViewDetached ?? false;

    public static int Count => Math.Max(2, Math.Min(5, SmartSettings.DesktopCount.Value));

    public static int Active => Math.Max(1, Math.Min(Count, SmartSettings.DesktopActive.Value));

    /// <summary>First time in a mission: store the built-in desktops if there are none, else restore the World View state.</summary>
    public static void Start()
    {
        bool any = false;
        for (int i = 1; i <= 5; i++)
            any |= !string.IsNullOrEmpty(SmartSettings.DesktopSlots[i].Value);
        if (!any)
        {
            for (int i = 1; i <= 5; i++)
                SmartSettings.DesktopSlots[i].Value = Preset(i).ToText();
            SmartSettings.DesktopActive.Value = 1;
            Apply(DesktopSet.Parse(SmartSettings.DesktopSlots[1].Value));
            ModLog.Debug("desktop mode: built-in desktops stored, desktop 1 applied");
        }
        else
        {
            string open = SmartSettings.WorldViewOpen.Value;
            if (!WorldElsewhere && (open == "open" || open == "closed"))
                DesktopGame.SetWorldViewOpen(open == "open");
        }
    }

    public static void Switch()
    {
        try
        {
            int from = Active;
            int to = from % Count + 1;
            SmartSettings.DesktopSlots[from].Value = Snapshot().ToText();
            SmartSettings.DesktopActive.Value = to;
            string stored = SmartSettings.DesktopSlots[to].Value;
            bool first = string.IsNullOrEmpty(stored);
            Apply(first ? Preset(to) : DesktopSet.Parse(stored));
            ModLog.Debug($"desktop mode: desktop {to} of {Count}" + (first ? " (built-in arrangement)" : ""));
        }
        catch (Exception ex)
        {
            ModLog.Warn($"desktop mode, switch: {ex.Message}");
        }
    }

    /// <summary>The World View was opened or closed (by the key or the game): remember it for the next start.</summary>
    public static void WorldViewChanged(bool open) => SmartSettings.WorldViewOpen.Value = open ? "open" : "closed";

    private static DesktopSet Snapshot() => new DesktopSet
    {
        Layouts = WindowMemory.SnapshotLayouts(),
        WorldViewOpen = WorldElsewhere ? null : DesktopGame.WorldViewOpen,
    };

    private static void Apply(DesktopSet set)
    {
        WindowMemory.ApplyLayouts(set.Layouts);
        if (set.WorldViewOpen.HasValue && !WorldElsewhere)
        {
            DesktopGame.SetWorldViewOpen(set.WorldViewOpen.Value);
            WorldViewChanged(set.WorldViewOpen.Value);
        }
    }

    private static DesktopSet Preset(int index)
    {
        WinRect host = HostArea.OfScreen();
        return DesktopPresets.Build(index, Screen.width, Screen.height, host.W, host.H, WorldElsewhere);
    }
}
