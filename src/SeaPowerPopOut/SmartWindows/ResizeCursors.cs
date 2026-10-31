using System;
using System.Collections.Generic;
using Noesis;
using SeaPower;
using SeapowerUI;
using UnityEngine;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Outside a mission the game shows the plain arrow on the resize edges of its windows (News, Task Force Builder): the
/// cursor textures are not loaded and no Noesis cursor handler is installed (only a mission installs one). Here the
/// textures are read from the game's tactical UI ini and the same handler is installed in the main menu and on the
/// campaign screen. A mission replaces it with its own handler when it starts. Works with the wide resize corners,
/// which only choose the Noesis cursor; this turns it into the game's picture.
/// </summary>
internal static class ResizeCursors
{
    private const float CheckEvery = 1f;

    private static Dictionary<string, CursorInfo> cursors;
    private static float next;
    private static bool loggedInstalled;

    public static void Update()
    {
        if (!SmartSettings.FixResizeCursors.Value || Time.unscaledTime < next)
            return;
        next = Time.unscaledTime + CheckEvery;
        try
        {
            if (Globals._mainGameViewModel != null)         // in a mission: its own handler is installed
                return;
            Load();
            if (cursors.Count == 0)
                return;
            GUI.SetCursorCallback(OnCursor);
            if (!loggedInstalled)
            {
                loggedInstalled = true;
                ModLog.Debug($"smart windows: resize cursors installed outside a mission ({cursors.Count} cursors)");
            }
        }
        catch (Exception ex)
        {
            if (!loggedInstalled)
            {
                loggedInstalled = true;
                ModLog.Warn("Resize cursors unavailable: " + ex.Message);
            }
        }
    }

    private static void Load()
    {
        if (cursors != null)
            return;
        cursors = new Dictionary<string, CursorInfo>();
        // The default skin first, then the active one on top of it.
        var folders = new List<string> { "Default" };
        string skin = Globals.UI_Folder;
        if (!string.IsNullOrEmpty(skin) && skin != "Default")
            folders.Add(skin);
        foreach (string folder in folders)
        {
            IniHandler ini = Utils.openIniFile("/ui/" + folder, "/Settings_UI_Tactical.ini", false, false);
            if (ini == null)
            {
                ModLog.Debug($"smart windows: /ui/{folder}/Settings_UI_Tactical.ini not found, no cursors from it");
                continue;
            }
            Dictionary<string, string> section = ini.GetSectionKeyValues("Cursors");
            ini.close();
            if (section == null)
                continue;
            foreach (KeyValuePair<string, string> entry in section)
            {
                var info = new CursorInfo(entry.Value);
                if (info.Texture != null)
                    cursors[entry.Key.ToLowerInvariant()] = info;
            }
        }
    }

    /// <summary>Same lookup as the game's mission handler: the cursor's type name, or its file for custom ones.</summary>
    private static void OnCursor(View view, Noesis.Cursor cursor)
    {
        try
        {
            string name = cursor.Type != CursorType.Custom ? cursor.Type.ToString() : cursor.Filename?.OriginalString;
            if ((name != null && cursors.TryGetValue(name.ToLowerInvariant(), out CursorInfo info)) || cursors.TryGetValue("default", out info))
                UnityEngine.Cursor.SetCursor(info.Texture, info.Offset, CursorMode.Auto);
        }
        catch (Exception ex)
        {
            ModLog.Debug("resize cursor: " + ex.Message);
        }
    }
}
