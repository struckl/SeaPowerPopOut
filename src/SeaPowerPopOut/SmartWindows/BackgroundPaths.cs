using System;
using System.Collections.Generic;
using System.Linq;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>Which background pictures are the game's own and which come from mods. No game types.</summary>
internal static class BackgroundPaths
{
    /// <summary>A folder path with forward slashes and one trailing slash, for prefix comparisons.</summary>
    public static string Normalize(string path) => path.Replace('\\', '/').TrimEnd('/') + "/";

    /// <summary>True if <paramref name="listedFolder"/> is the game's <c>ui/backgrounds</c> folder.</summary>
    public static bool IsBackgroundFolder(string listedFolder) =>
        listedFolder != null && listedFolder.Replace('\\', '/').Trim('/').Equals("ui/backgrounds", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Takes the game's pictures out of <paramref name="pictures"/>, but only if mods brought pictures of their own:
    /// without any, the game's stay (there would be nothing to show otherwise). Returns how many were removed.
    /// </summary>
    public static int DropGamePictures(List<string> pictures, string gameFolder)
    {
        if (pictures == null || pictures.Count == 0 || string.IsNullOrEmpty(gameFolder))
            return 0;
        string game = Normalize(gameFolder);
        int fromGame = pictures.Count(p => Normalize(p).StartsWith(game, StringComparison.OrdinalIgnoreCase));
        if (fromGame == 0 || fromGame == pictures.Count)
            return 0;                                   // nothing to drop, or nothing from mods to show instead
        return pictures.RemoveAll(p => Normalize(p).StartsWith(game, StringComparison.OrdinalIgnoreCase));
    }
}
