using System;
using System.IO;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>The path rules behind the scenario list and the mission editor's file picker. No game types.</summary>
internal static class MissionPaths
{
    private const string CustomMission = "_Custom Mission.ini";

    /// <summary>The mission editor's default file, which is not a scenario to play.</summary>
    public static bool IsCustomMission(string fileName) =>
        !string.IsNullOrEmpty(fileName)
        && string.Equals(Path.GetFileName(fileName), CustomMission, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The folder of a mission that was opened, if it is worth remembering: inside the game's StreamingAssets and not a
    /// temporary folder. Null otherwise.
    /// </summary>
    public static string FolderToRemember(string missionFile, string streamingAssets)
    {
        if (string.IsNullOrEmpty(missionFile) || string.IsNullOrEmpty(streamingAssets))
            return null;
        string folder = Path.GetDirectoryName(Path.GetFullPath(missionFile));
        if (folder == null || folder.IndexOf("_temp", StringComparison.OrdinalIgnoreCase) >= 0)
            return null;
        string root = Path.GetFullPath(streamingAssets);
        return folder.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? folder : null;
    }

    /// <summary>
    /// True if a picker that would start in <paramref name="pickerStart"/> is the one for opening missions: the
    /// editor's own folder, <c>user/missions</c>.
    /// </summary>
    public static bool IsMissionPickerStart(string pickerStart) =>
        !string.IsNullOrEmpty(pickerStart)
        && pickerStart.Replace('\\', '/').TrimEnd('/').EndsWith("user/missions", StringComparison.OrdinalIgnoreCase);

    /// <summary>True if the remembered folder may be used: it lies inside the picker's home folder.</summary>
    public static bool InsideHome(string folder, string home) =>
        !string.IsNullOrEmpty(folder) && (string.IsNullOrEmpty(home) || folder.StartsWith(home, StringComparison.OrdinalIgnoreCase));
}
