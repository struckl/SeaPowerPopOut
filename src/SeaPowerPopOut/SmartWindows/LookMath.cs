using System;
using System.Collections.Generic;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>The arithmetic of the look menu. Pure logic, no game types.</summary>
internal static class LookMath
{
    public const float MinOpacity = 0.1f;
    public const float MinScale = 0.5f;
    public const float MaxScale = 2f;
    public const float OpacityStep = 0.1f;
    public const float ScaleStep = 0.1f;

    public static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;

    /// <summary>One step up (+1) or down (-1) in opacity, on the 10 % grid.</summary>
    public static float StepOpacity(float current, int direction) =>
        Snap(current + direction * OpacityStep, OpacityStep, MinOpacity, 1f);

    public static float StepScale(float current, int direction) =>
        Snap(current + direction * ScaleStep, ScaleStep, MinScale, MaxScale);

    private static float Snap(float v, float step, float min, float max) =>
        Clamp((float)Math.Round(v / step) * step, min, max);

    /// <summary>
    /// A window's background is made of layers, one above the other, each with its own alpha. Scaling every layer's alpha by
    /// the same factor f makes the window <paramref name="opacity"/> times as opaque as before: this solves for f on the
    /// given stack of layer alphas (0..1). Returns <paramref name="opacity"/> itself when there is nothing to solve.
    /// </summary>
    public static float SolveFactor(IReadOnlyList<float> layerAlphas, float opacity)
    {
        if (opacity >= 0.999f)
            return 1f;
        float total = Coverage(layerAlphas, 1f);
        if (layerAlphas.Count == 0 || total < 0.01f)
            return opacity;
        float target = opacity * total;
        float lo = 0f, hi = 1f;
        for (int i = 0; i < 24; i++)
        {
            float mid = (lo + hi) / 2f;
            if (Coverage(layerAlphas, mid) < target)
                lo = mid;
            else
                hi = mid;
        }
        return (lo + hi) / 2f;
    }

    /// <summary>How much of what lies behind the stack is covered when every alpha is scaled by <paramref name="factor"/>.</summary>
    public static float Coverage(IReadOnlyList<float> layerAlphas, float factor)
    {
        float see = 1f;
        foreach (float a in layerAlphas)
            see *= 1f - a * factor;
        return 1f - see;
    }
}
