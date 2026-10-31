using System.Collections.Generic;
using Noesis;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// The names a window can be matched by: its title (the header text, e.g. "Unit Reference"), its name, and the type
/// names of its header, content and view models. The window patterns are written with titles, so the title comes first.
/// </summary>
internal static class WindowNames
{
    public static string[] Of(GameWindow window)
    {
        var names = new List<string>();
        Add(names, TextOf(window.Header));
        Add(names, window.Name);
        Add(names, TypeName(window.Header));
        object content = window.Content;
        Add(names, TypeName(content));
        if (content is FrameworkElement element)
        {
            Add(names, element.Name);
            Add(names, TypeName(element.DataContext));
        }
        Add(names, TypeName(window.DataContext));
        return names.ToArray();
    }

    /// <summary>The text shown by a header: a string, a text block or the content of a content control.</summary>
    private static string TextOf(object header, int depth = 0)
    {
        switch (header)
        {
            case string text:
                return text;
            case TextBlock block:
                return block.Text;
            case ContentControl control when depth < 3:
                return TextOf(control.Content, depth + 1);
            default:
                return null;
        }
    }

    private static string TypeName(object value) => value?.GetType().Name;

    private static void Add(List<string> names, string name)
    {
        if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name))
            names.Add(name);
    }
}
