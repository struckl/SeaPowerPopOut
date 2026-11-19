using System;
using HarmonyLib;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// One Smart Windows feature as the rest of the mod sees it: its switch, what it does at start, its game hooks and what
/// it wants to hear about (a window loaded, every frame, the game closing). Each feature class declares its own;
/// <see cref="SmartFeatures"/> holds the list and is the only place that checks the switches.
/// </summary>
internal sealed class Feature
{
    /// <summary>Read once at start: off = the feature does nothing this run ("Needs a restart" settings).</summary>
    public Func<bool> Switch { get; set; }

    /// <summary>Read before every call: off = the feature skips that window or frame (settings that apply at once).</summary>
    public Func<bool> LiveSwitch { get; set; }

    /// <summary>At plugin load, after the settings are read.</summary>
    public Action Start { get; set; }

    /// <summary>
    /// Once NoesisGUI runs: patches and class handlers. False = a game member is missing and the feature stays off; it
    /// says so in the log itself. A feature without hooks is on from the start.
    /// </summary>
    public Func<Harmony, bool> Hook { get; set; }

    /// <summary>A debug log line once the hooks are in.</summary>
    public string Hooked { get; set; }

    /// <summary>A game window finished loading on the main screen.</summary>
    public Action<GameWindow> WindowLoaded { get; set; }

    /// <summary>A game window finished loading on the pop-out monitor.</summary>
    public Action<GameWindow> PoppedWindowLoaded { get; set; }

    public Action Update { get; set; }

    /// <summary>The game closes: write what is not saved yet.</summary>
    public Action Save { get; set; }

    /// <summary>Switched on and its hooks (if any) are in place.</summary>
    public bool Active { get; private set; }

    /// <summary>Off for the rest of the run (a feature that found it cannot work).</summary>
    public void Stop() => Active = false;

    internal bool Wanted => Switch?.Invoke() ?? true;

    internal bool Live => Active && (LiveSwitch?.Invoke() ?? true);

    internal void Begin()
    {
        Start?.Invoke();
        Active = Hook == null;
    }

    internal void Apply(Harmony harmony)
    {
        Active = Hook(harmony);
        if (Active && Hooked != null)
            ModLog.Debug("smart windows: " + Hooked);
    }
}
