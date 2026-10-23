using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Noesis;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut;

/// <summary>
/// Diagonal resizing that is easy to hit. The game's window template has a resize corner only in the 10 × 10 square
/// diagonally outside each corner, so coming along an edge you always get the straight resize. Here the last stretch
/// of every edge resizes the corner too, with the diagonal cursor – like the edges of an operating system window.
/// Works on every game window, on both screens.
/// </summary>
internal static class CornerResize
{
    private const float Reach = 20f;          // window units along an edge, from its end, that resize the corner

    // ResizeSides: Top 1, Left 2, Bottom 4, Right 8.
    private const int Top = 1, Left = 2, Bottom = 4, Right = 8;

    private static readonly Type ThumbType = AccessTools.TypeByName("SeapowerUI.ResizeThumb");
    private static readonly PropertyInfo SidesInt = ThumbType?.GetProperty("SidesInt");
    private static readonly ConditionalWeakTable<Thumb, Edge> Hooked = new ConditionalWeakTable<Thumb, Edge>();
    private static bool loggedMissing;

    /// <summary>Adds the corner stretches to the edges of a window that just loaded.</summary>
    public static void Attach(GameWindow window)
    {
        if (!Settings.WideCorners.Value)
            return;
        if (SidesInt == null)
        {
            if (!loggedMissing)
            {
                loggedMissing = true;
                ModLog.Warn("SeapowerUI.ResizeThumb not found (game updated?) - no wider resize corners.");
            }
            return;
        }
        // The resize thumbs sit directly in the template's root grid, around the window body.
        if (VisualTreeHelper.GetChildrenCount(window) == 0 || !(VisualTreeHelper.GetChild(window, 0) is Panel container))
            return;
        for (int i = 0; i < container.Children.Count; i++)
        {
            if (!(container.Children[i] is Thumb thumb) || !ThumbType.IsInstanceOfType(thumb) || Hooked.TryGetValue(thumb, out _))
                continue;
            int sides = (int)SidesInt.GetValue(thumb);
            if (sides == Top || sides == Bottom || sides == Left || sides == Right)
                Hooked.Add(thumb, new Edge(thumb, sides));
        }
    }

    /// <summary>One edge's resize thumb: which sides it resizes depends on where along the edge it is grabbed.</summary>
    private sealed class Edge
    {
        private readonly Thumb thumb;
        private readonly int sides;
        private int hover;
        private bool dragging;

        public Edge(Thumb thumb, int sides)
        {
            this.thumb = thumb;
            this.sides = sides;
            hover = sides;
            thumb.MouseMove += OnMouseMove;
            thumb.MouseLeave += (sender, args) => { if (!dragging) Show(sides); };
            // Subscribed after the thumb's own DragDelta handler, which reads Sides on every step: set for the drag.
            thumb.DragStarted += (sender, args) =>
            {
                dragging = true;
                if (hover != sides)
                    SidesInt.SetValue(thumb, hover);
            };
            thumb.DragCompleted += (sender, args) =>
            {
                dragging = false;
                SidesInt.SetValue(thumb, sides);
            };
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
                return;
            Point p = e.GetPosition(thumb);
            bool across = sides == Top || sides == Bottom;
            float along = across ? p.X : p.Y;
            float length = across ? thumb.ActualWidth : thumb.ActualHeight;
            float reach = Math.Min(Reach, length / 3f);
            int corner = sides;
            if (along < reach)
                corner |= across ? Left : Top;
            else if (along > length - reach)
                corner |= across ? Right : Bottom;
            Show(corner);
        }

        private void Show(int corner)
        {
            if (corner == hover)
                return;
            hover = corner;
            thumb.Cursor = corner switch
            {
                Top | Left or Bottom | Right => Cursors.SizeNWSE,
                Top | Right or Bottom | Left => Cursors.SizeNESW,
                Top or Bottom => Cursors.SizeNS,
                _ => Cursors.SizeWE,
            };
        }
    }
}
