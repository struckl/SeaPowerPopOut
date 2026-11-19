using System.Collections.Generic;
using UnityEngine;

namespace SeaPowerPopOut;

/// <summary>The pointer position of game views on other monitors (hook on <c>NoesisView.MousePosition</c>).</summary>
internal static class PointerFix
{
    private static readonly HashSet<int> Logged = new HashSet<int>();

    /// <summary>
    /// <c>NoesisView.MousePosition</c> just returned the mouse position relative to the monitor it is on, bottom up,
    /// in the monitor's own pixels. NoesisView then flips it with its camera's <c>pixelHeight</c> - which for a camera
    /// on another display is not that display's height, so on a monitor of another height (or a Windows scaling
    /// other than the game's) the pointer sat off by the difference: no resize cursor at the edges, drags that jump,
    /// tooltips far from the cursor, right-clicks landing on the wrong thing. Clicks take another way and land right.
    /// <para>Corrected for every view on another monitor - the pop-out screen and the game's own second-monitor map /
    /// world view - so that the flip comes out at the monitor's real height. What the flip uses is read from the
    /// camera right here, so the correction is zero wherever the game already gets it right.</para>
    /// </summary>
    public static void Apply(NoesisView view, ref Vector3 position)
    {
        if (position == Vector3.zero || view == null)
            return;
        int index = (int)position.z;
        Display[] displays = Display.displays;
        if (index < 1 || index >= displays.Length)
            return;
        UnityEngine.Camera camera = view.GetComponent<UnityEngine.Camera>();
        if (camera == null || camera.targetDisplay != index)
            return;

        Display display = displays[index];
        float real = display.renderingHeight, mouse = display.systemHeight;
        if (real < 1f || mouse < 1f)
            return;
        float k = real / mouse;                       // mouse pixels -> pixels of the picture (1 unless scaled)
        float seen = camera.pixelHeight;              // what NoesisView flips with
        if (Logged.Add(index))
            ModLog.Info($"Pointer on monitor {index + 1}: camera {camera.pixelWidth}x{camera.pixelHeight}, monitor " +
                        $"{display.systemWidth}x{display.systemHeight} (rendering {display.renderingWidth}x{display.renderingHeight}), " +
                        $"game window {Screen.width}x{Screen.height}; first mouse position {position.x:0},{position.y:0}");
        position.x *= k;
        position.y = position.y * k + seen - real;
    }
}
