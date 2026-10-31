using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Optional (off by default): at start, deletes the game's old session logs in <c>Sea Power_Data/Logs</c>. The logs of
/// this run and anything written in the last minute before it started are kept.
/// </summary>
internal static class LogCleanup
{
    private static readonly string[] CurrentLogProperties = { "CurrentLogFileName", "CurrentEventLogFileName" };

    public static void Run()
    {
        if (!SmartSettings.DeleteOldLogs.Value)
            return;
        try
        {
            string folder = Path.Combine(Application.dataPath, "Logs");
            if (!Directory.Exists(folder))
                return;

            DateTime cutoff = Process.GetCurrentProcess().StartTime.AddMinutes(-1);
            HashSet<string> current = CurrentLogs();
            int deleted = 0;
            foreach (string path in Directory.GetFiles(folder))
            {
                if (!ShouldDelete(Path.GetFileName(path), File.GetLastWriteTime(path), cutoff, current.Contains(Path.GetFullPath(path))))
                    continue;
                try
                {
                    File.Delete(path);
                    deleted++;
                }
                catch (IOException)
                {
                    // in use or locked: leave it
                }
            }
            ModLog.Debug($"smart windows: {deleted} old log file(s) deleted from {folder}");
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Old logs not deleted: {ex.Message}");
        }
    }

    /// <summary>A session log that is not one of this run's and was last written before <paramref name="cutoff"/>.</summary>
    internal static bool ShouldDelete(string fileName, DateTime lastWrite, DateTime cutoff, bool isCurrentLog) =>
        !isCurrentLog
        && fileName.IndexOf("_session", StringComparison.OrdinalIgnoreCase) >= 0
        && lastWrite < cutoff;

    private static HashSet<string> CurrentLogs()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Type log = AccessTools.TypeByName("SeaPower.Log");
        foreach (string name in CurrentLogProperties)
        {
            string path = log == null ? null : AccessTools.Property(log, name)?.GetValue(null, null) as string;
            if (!string.IsNullOrEmpty(path))
                result.Add(Path.GetFullPath(path));
        }
        return result;
    }
}
