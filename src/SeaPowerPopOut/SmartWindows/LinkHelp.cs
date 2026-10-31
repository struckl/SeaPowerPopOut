using System;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Noesis;
using SeaPower;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Unit Reference: the highlighted links to a weapon, sensor or system get a tooltip with the start of its description
/// (the game's short help), when it has one. Units themselves (<see cref="MuseumObjectEntry"/>) are left alone.
/// </summary>
internal static class LinkHelp
{
    private const int MaxLength = 480;
    private const int MinCut = 200;
    private const float MaxWidth = 380f;

    private static readonly Regex Breaks = new Regex(@"<\s*(LineBreak|br)\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Tags = new Regex("<[^>]*>", RegexOptions.Compiled);
    private static readonly Regex Blanks = new Regex(@"[ \t]+", RegexOptions.Compiled);
    private static readonly Regex Paragraphs = new Regex(@"\s*\n\s*(\n\s*)+", RegexOptions.Compiled);

    /// <summary>The entry each button was last given a tip for.</summary>
    private static readonly ConditionalWeakTable<Button, object> Tipped = new ConditionalWeakTable<Button, object>();

    public static void Register()
    {
        try
        {
            EventManager.RegisterClassHandler(typeof(Button), FrameworkElement.LoadedEvent, new RoutedEventHandler(Check));
            EventManager.RegisterClassHandler(typeof(Button), UIElement.MouseEnterEvent, new MouseEventHandler(Check));
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Link tooltips not available: {ex.Message}");
        }
    }

    private static void Check(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button button)
                Tip(button);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"link help: {ex.Message}");
        }
    }

    private static void Tip(Button button)
    {
        if (!(button.CommandParameter is MuseumEntry entry) || entry is MuseumObjectEntry)
            return;
        // Buttons are recycled with a new entry: look again only when it changed.
        if (Tipped.TryGetValue(button, out object last) && ReferenceEquals(last, entry))
            return;
        Tipped.Remove(button);
        Tipped.Add(button, entry);

        string text = HelpText(entry.Description);
        if (text == null)
        {
            ToolTipService.SetToolTip(button, null);
            return;
        }
        ToolTipService.SetToolTip(button, new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = MaxWidth });
    }

    /// <summary>The description as plain text with its paragraphs, cut at a word after about <see cref="MaxLength"/> characters.</summary>
    public static string HelpText(string markup)
    {
        if (string.IsNullOrWhiteSpace(markup))
            return null;
        string text = Tags.Replace(Breaks.Replace(markup, "\n"), "");
        text = text.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&apos;", "'");
        text = Paragraphs.Replace(Blanks.Replace(text, " "), "\n\n").Trim();
        if (text.Length == 0)
            return null;
        if (text.Length > MaxLength)
        {
            int cut = text.LastIndexOfAny(new[] { ' ', '\n' }, MaxLength);
            text = text.Substring(0, cut > MinCut ? cut : MaxLength).TrimEnd() + " ...";
        }
        return text;
    }
}
