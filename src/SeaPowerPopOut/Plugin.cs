using System.Runtime.CompilerServices;
using AnchorChain;

namespace SeaPowerPopOut;

/// <summary>
/// Entry point. R.A.T.S. Keel finds the class by its attribute and calls TriggerEntryPoint. The start itself is R.A.T.S. Rigging's:
/// the plugin names it as a dependency, so Keel starts it first and keeps this mod out when it is missing or too old.
/// </summary>
[ACPlugin(Guid, Name, Version)]
[ACDependency("dev.sewerlabs.seapowerrigging", "1.0.0", null)]
public class Plugin : IAnchorChainMod
{
    public const string Guid = "dev.sewerlabs.seapowerpopout";
    public const string Name = "R.A.T.S. Pop-Out Windows";
    public const string Version = "1.4.0";

    public void TriggerEntryPoint() => Start();

    // Apart from the entry point, so this class has no Rigging type in it and loads on its own.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Start() => Rig.Start<PopOutScreen>(Settings.Bind, Patches.Declare, Steps);

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
