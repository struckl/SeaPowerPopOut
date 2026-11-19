using System.Reflection;
using HarmonyLib;
using SeaPower;
using UnityEngine;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut;

/// <summary>The game's mouse and window members that the pop-out screen and the Smart Windows features both use.</summary>
internal static class GameMouse
{
    private static readonly AccessTools.FieldRef<MouseControlState, MouseControlState.State> MouseState =
        AccessTools.FieldRefAccess<MouseControlState, MouseControlState.State>("_currentState");

    /// <summary>Stops the game from putting a window back into its anchored corner (unit status: bottom right).</summary>
    public static readonly MethodInfo MarkUserPositioned = AccessTools.Method(typeof(GameWindow), "MarkUserPositioned");

    public static bool Resolved => MouseState != null;

    /// <summary>
    /// Runs after the game decided where the mouse is: this frame the mouse belongs to the UI, so a click does not also
    /// select, deselect or order something in the world or on the map.
    /// </summary>
    public static void ClaimForUi(MouseControlState state)
    {
        MouseState(state) = MouseControlState.State.UI;
        if (Singleton<CameraManager>.InstanceExists())
            Singleton<CameraManager>.Instance.setMouseInteractionFlag(false);
    }

    /// <summary>The mouse is on Unity display <paramref name="display"/> now; <paramref name="position"/> is where, in its pixels from the bottom.</summary>
    public static bool OnDisplay(int display, out Vector3 position)
    {
        position = Display.RelativeMouseAt(Input.mousePosition);
        return position != Vector3.zero && (int)position.z == display;
    }
}
