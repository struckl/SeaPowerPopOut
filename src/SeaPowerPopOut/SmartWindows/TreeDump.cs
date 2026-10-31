using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using Noesis;
using UnityEngine;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Developer aid: writes the layout (visual tree) of the windows named in <c>Dump window trees</c> to
/// <c>BepInEx/SmartWindows.tree.txt</c> a moment after they open. Off while the setting is empty.
/// </summary>
internal static class TreeDump
{
    private const float Delay = 1.5f;     // the template needs a moment to be applied
    private const int MaxDepth = 40;

    private static PatternList patterns;
    private static readonly List<Pending> Queue = new List<Pending>();
    private static string file;

    private sealed class Pending
    {
        public WeakReference<GameWindow> Window;
        public float Due;
        public string Label;
    }

    public static void Init()
    {
        string setting = SmartSettings.DumpWindows.Value;
        if (string.IsNullOrWhiteSpace(setting))
            return;
        patterns = new PatternList(setting);
        file = System.IO.Path.Combine(Paths.BepInExRootPath, "SmartWindows.tree.txt");
        File.WriteAllText(file, $"Window trees, {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n");
    }

    public static void WindowLoaded(GameWindow window)
    {
        if (patterns == null)
            return;
        string type = window.DataContext?.GetType().Name;
        string match = patterns.Match(WindowNames.Of(window));
        if (match == null)
            return;
        Queue.Add(new Pending { Window = new WeakReference<GameWindow>(window), Due = Time.unscaledTime + Delay, Label = type ?? window.Name });
    }

    public static void Update()
    {
        for (int i = Queue.Count - 1; i >= 0; i--)
        {
            Pending p = Queue[i];
            if (Time.unscaledTime < p.Due)
                continue;
            Queue.RemoveAt(i);
            if (p.Window.TryGetTarget(out GameWindow window))
                Write(window, p.Label);
        }
    }

    private static void Write(GameWindow window, string label)
    {
        var sb = new StringBuilder();
        sb.AppendLine().AppendLine($"=== {label}  {window.ActualWidth:0}x{window.ActualHeight:0}  at {window.X:0},{window.Y:0}");
        Walk(window, 0, sb);
        File.AppendAllText(file, sb.ToString());
        ModLog.Info($"Window tree of {label} written to {file}");
    }

    private static void Walk(DependencyObject node, int depth, StringBuilder sb)
    {
        if (depth > MaxDepth)
            return;
        sb.Append(' ', depth * 2).Append(node.GetType().Name);
        if (node is FrameworkElement element)
        {
            if (!string.IsNullOrEmpty(element.Name))
                sb.Append(" '").Append(element.Name).Append('\'');
            sb.Append($"  {element.ActualWidth:0}x{element.ActualHeight:0}");
            if (element.Visibility != Visibility.Visible)
                sb.Append("  ").Append(element.Visibility);
            if (element.Parent is Grid)
                sb.Append($"  r{Grid.GetRow(element)} c{Grid.GetColumn(element)}");
            if (element is TextBlock text && !string.IsNullOrEmpty(text.Text))
                sb.Append("  \"").Append(text.Text.Length > 40 ? text.Text.Substring(0, 40) : text.Text).Append('"');
            if (element.DataContext != null && depth <= 2)
                sb.Append("  ctx=").Append(element.DataContext.GetType().Name);
        }
        sb.AppendLine();

        int count = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(node, i) is DependencyObject child)
                Walk(child, depth + 1, sb);
        }
    }
}
