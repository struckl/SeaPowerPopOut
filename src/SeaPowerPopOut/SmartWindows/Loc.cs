using System;
using System.Collections.Generic;
using System.IO;
using SeaPower;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// The mod's texts. They live in <c>lang/&lt;code&gt;.ini</c> next to the DLL (one <c>key=text</c> per line, UTF-8,
/// <c>#</c> starts a comment), one file per game language: en, ru, cn, de, es, fr, ko, ja, vn. Edit them to translate.
/// A missing key falls back to English, then to the key itself.
/// </summary>
internal static class Loc
{
    private static Dictionary<string, string> chosen = new Dictionary<string, string>();
    private static Dictionary<string, string> english = new Dictionary<string, string>();
    private static string langDir;

    public static void Init(string modFolder)
    {
        langDir = Path.Combine(modFolder, "lang");
        english = Load("en");
        Reload();
    }

    /// <summary>Picks the language: the config's, else the game's, else English.</summary>
    public static void Reload()
    {
        string code = SmartSettings.Language.Value?.Trim();
        if (string.IsNullOrEmpty(code))
            code = GameLanguage();
        chosen = code == "en" ? english : Load(code);
    }

    public static string Get(string key)
    {
        if (chosen.TryGetValue(key, out string text) || english.TryGetValue(key, out text))
            return text;
        return key;
    }

    private static string GameLanguage() =>
        Singleton<LanguageResourceHandler>.InstanceExists() ? Singleton<LanguageResourceHandler>.Instance.Language : "en";

    private static Dictionary<string, string> Load(string code)
    {
        string path = langDir == null ? null : Path.Combine(langDir, code + ".ini");
        if (path == null || !File.Exists(path))
            return new Dictionary<string, string>();
        try
        {
            return IniText.Parse(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Language file {path}: {ex.Message}");
            return new Dictionary<string, string>();
        }
    }
}
