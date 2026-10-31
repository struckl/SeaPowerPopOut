using System;
using System.Collections.Generic;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>A window rectangle in screen pixels, origin top-left.</summary>
internal struct WinRect
{
    public float X, Y, W, H;

    public WinRect(float x, float y, float w, float h)
    {
        X = x;
        Y = y;
        W = w;
        H = h;
    }

    public float Right => X + W;
    public float Bottom => Y + H;

    public WinRect Moved(float dx, float dy) => new WinRect(X + dx, Y + dy, W, H);
}

/// <summary>
/// Pure window geometry: snapping, keeping windows reachable and cascading. No game types, so it can be checked
/// without starting the game.
/// </summary>
internal static class WindowGeometry
{
    /// <summary>
    /// Moves <paramref name="moving"/> so that one of its edges lands on an edge of a neighbour or of the screen when
    /// it is within <paramref name="distance"/> pixels. Each axis snaps on its own.
    /// </summary>
    public static WinRect SnapMove(WinRect moving, IList<WinRect> others, WinRect screen, float distance)
    {
        float dx = BestOffset(moving.X, moving.Right, others, screen, distance, horizontal: true);
        float dy = BestOffset(moving.Y, moving.Bottom, others, screen, distance, horizontal: false);
        return moving.Moved(dx, dy);
    }

    /// <summary>
    /// Snaps the edges being dragged while resizing. Only the flagged edges move; the opposite edge stays where it is.
    /// </summary>
    public static WinRect SnapResize(WinRect r, bool moveLeft, bool moveTop, bool moveRight, bool moveBottom,
        IList<WinRect> others, WinRect screen, float distance)
    {
        if (moveLeft)
        {
            float d = NearestDelta(r.X, others, screen, distance, horizontal: true);
            r.X += d;
            r.W -= d;
        }
        else if (moveRight)
        {
            r.W += NearestDelta(r.Right, others, screen, distance, horizontal: true);
        }

        if (moveTop)
        {
            float d = NearestDelta(r.Y, others, screen, distance, horizontal: false);
            r.Y += d;
            r.H -= d;
        }
        else if (moveBottom)
        {
            r.H += NearestDelta(r.Bottom, others, screen, distance, horizontal: false);
        }

        return r;
    }

    /// <summary>
    /// Keeps the window reachable: at least <paramref name="minVisible"/> pixels of its width stay on screen at the
    /// left and right edge, and its title bar (the top <paramref name="titleHeight"/> pixels) stays on screen.
    /// </summary>
    public static WinRect KeepReachable(WinRect r, WinRect screen, float minVisible, float titleHeight)
    {
        float minX = screen.X - r.W + minVisible;
        float maxX = screen.Right - minVisible;
        float minY = screen.Y;
        float maxY = screen.Bottom - titleHeight;
        r.X = Clamp(r.X, minX, maxX);
        r.Y = Clamp(r.Y, minY, maxY);
        return r;
    }

    /// <summary>
    /// Position of a window opened while <paramref name="alreadyOpen"/> windows of its kind are open: shifted right and
    /// down by <paramref name="offset"/> per window already open, then kept reachable.
    /// </summary>
    public static WinRect Cascade(WinRect saved, int alreadyOpen, float offset, WinRect screen, float minVisible, float titleHeight)
    {
        if (alreadyOpen <= 0 || offset <= 0f)
            return KeepReachable(saved, screen, minVisible, titleHeight);
        float shift = offset * alreadyOpen;
        return KeepReachable(saved.Moved(shift, shift), screen, minVisible, titleHeight);
    }

    private static float BestOffset(float start, float end, IList<WinRect> others, WinRect screen, float distance, bool horizontal)
    {
        float best = 0f;
        float bestAbs = distance + 1f;
        foreach (float target in Targets(others, screen, horizontal))
        {
            float a = Math.Abs(target - start);
            if (a <= distance && a < bestAbs)
            {
                best = target - start;
                bestAbs = a;
            }

            float b = Math.Abs(target - end);
            if (b <= distance && b < bestAbs)
            {
                best = target - end;
                bestAbs = b;
            }
        }
        return best;
    }

    private static float NearestDelta(float edge, IList<WinRect> others, WinRect screen, float distance, bool horizontal)
    {
        float best = 0f;
        float bestAbs = distance + 1f;
        foreach (float target in Targets(others, screen, horizontal))
        {
            float a = Math.Abs(target - edge);
            if (a <= distance && a < bestAbs)
            {
                best = target - edge;
                bestAbs = a;
            }
        }
        return best;
    }

    private static IEnumerable<float> Targets(IList<WinRect> others, WinRect screen, bool horizontal)
    {
        yield return horizontal ? screen.X : screen.Y;
        yield return horizontal ? screen.Right : screen.Bottom;

        foreach (WinRect o in others)
        {
            yield return horizontal ? o.X : o.Y;
            yield return horizontal ? o.Right : o.Bottom;
        }
    }

    private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
}

/// <summary>
/// Double-click maximize. Remembers the rectangle before maximizing so a second double-click restores it. Holds no
/// game objects: the caller applies the rectangle this returns.
/// </summary>
internal sealed class MaximizeToggle
{
    private WinRect? restore;

    public bool IsMaximized => restore.HasValue;

    /// <summary>Returns the rectangle to show after the toggle.</summary>
    public WinRect Toggle(WinRect current, WinRect screen)
    {
        if (restore.HasValue)
        {
            WinRect back = restore.Value;
            restore = null;
            return back;
        }

        restore = current;
        return screen;
    }

    /// <summary>Forgets the maximized state, e.g. when the window is resized by hand.</summary>
    public void Forget() => restore = null;
}
