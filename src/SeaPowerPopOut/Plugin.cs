using System.Runtime.CompilerServices;
using AnchorChain;

namespace SeaPowerPopOut;

/// <summary>
/// Entry point. A mod loader (Keel or Anchor Chain) finds the class by its attribute and calls TriggerEntryPoint; the
/// start itself is the shared ModEntry.
/// </summary>
[ACPlugin(Guid, Name, Version)]
public class Plugin : IAnchorChainMod
{
    public const string Guid = "dev.sewerlabs.seapowerpopout";
    public const string Name = "R.A.T.S. Pop-Out Windows";
    public const string Version = "1.2.3";

    public void TriggerEntryPoint() => ModEntry.Trigger(Start);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Start() => ModEntry.Start<PopOutScreen>(Settings.Bind, Patches.Declare, Steps);

    // After the settings and the load-stage hooks: only the hooks on plain game classes so far, the window hooks follow once
    // NoesisGUI runs (see Patches).
    private static void Steps()
    {
        Native.CaptureMainWindow();
        Layout.Load();
        SmartWindows.SmartSettings.Init();
        SmartWindows.SmartFeatures.Init();
    }
}
