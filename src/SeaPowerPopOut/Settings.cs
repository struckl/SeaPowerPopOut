using System.IO;
using BepInEx;
using BepInEx.Configuration;

namespace SeaPowerPopOut;

/// <summary>
/// BepInEx config at <c>BepInEx/config/dev.sewerlabs.seapowerpopout.cfg</c>. Sections are numbered so they sort in
/// the order a player would look for them. Section 3 is written by the mod itself (which windows are popped out and
/// where they sit); editing it by hand works too.
/// </summary>
internal static class Settings
{
    public static ConfigEntry<bool> Enabled;
    public static ConfigEntry<bool> VerboseLog;

    public static ConfigEntry<int> Monitor;
    public static ConfigEntry<float> Scale;
    public static ConfigEntry<bool> UnlimitedSize;

    public static ConfigEntry<string> PoppedWindows;
    public static ConfigEntry<string> WindowPlacement;

    public static void Init()
    {
        var file = new ConfigFile(Path.Combine(Paths.ConfigPath, Plugin.Guid + ".cfg"), saveOnInit: true);

        Enabled = file.Bind("1. General", "Enable mod", true,
            "Master switch. Needs a restart.");
        VerboseLog = file.Bind("1. General", "Verbose log", false,
            "Write debug detail to BepInEx/LogOutput.log.");

        Monitor = file.Bind("2. Pop-out screen", "Monitor", 2,
            new ConfigDescription("Monitor the windows pop out to. 1 is the monitor the game runs on, 2 the next one " +
                                  "Unity finds, and so on - BepInEx/LogOutput.log lists them with their resolutions " +
                                  "at startup. The monitor is taken over by the game the first time a window pops " +
                                  "out and stays so until the game closes.",
                                  new AcceptableValueRange<int>(2, 8)));
        Scale = file.Bind("2. Pop-out screen", "UI scale", 1.0f,
            new ConfigDescription("Size of the windows on the pop-out monitor relative to the game's own UI scale " +
                                  "(which already adapts to that monitor's resolution).",
                                  new AcceptableValueRange<float>(0.5f, 3f)));
        UnlimitedSize = file.Bind("2. Pop-out screen", "Unlimited window size", true,
            "On the pop-out monitor a window can be resized beyond the game's maximum size, up to the whole monitor.");

        PoppedWindows = file.Bind("3. Remembered layout", "Popped-out windows", "",
            "Windows that open on the pop-out monitor (written by the mod when you use the pop-out button). " +
            "Clear it to bring every window back to the main screen.");
        WindowPlacement = file.Bind("3. Remembered layout", "Window placement", "",
            "Where each window sits on the pop-out monitor: Name=x,y,width,height (written by the mod).");
    }
}
