using System.Collections.Generic;
using SeaPower;
using UnityEngine;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// The key that opens and closes the World View window. It never takes a key the game uses: the game's bindings (also
/// the ones the player changed) and a few keys it reads directly are checked on every press and a clash is logged once.
/// </summary>
internal static class WorldViewKey
{
    // Keys the game reads without going through its bindings.
    private static readonly KeyCode[] DirectKeys =
    {
        KeyCode.Escape, KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Backspace, KeyCode.Tab, KeyCode.Space,
        KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.Mouse2,
    };

    private static readonly KeyCode[] Modifiers =
    {
        KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt,
    };

    private static string warned;

    public static void Update()
    {
        KeyCode key = SmartSettings.WorldViewKey.Value;
        if (key == KeyCode.None || !Input.GetKeyDown(key) || !DesktopGame.InMission)
            return;
        foreach (KeyCode modifier in Modifiers)
        {
            if (Input.GetKey(modifier))
                return;
        }
        InputHandler input = Singleton<InputHandler>.InstanceExists() ? Singleton<InputHandler>.Instance : null;
        if (input == null || input.consoleOpen || input.menuOpen || input.typingActive || input.RebindMode.Value)
            return;

        string clash = Conflict(input, key);
        if (clash != null)
        {
            string message = $"World View key {key} is used by the game ({clash}): ignored. Choose another key in the config (Desktop mode).";
            if (warned != message)
            {
                warned = message;
                ModLog.Warn(message);
            }
            return;
        }
        bool? open = DesktopGame.WorldViewOpen;
        if (!open.HasValue)
            return;
        DesktopGame.SetWorldViewOpen(!open.Value);
        DesktopSets.WorldViewChanged(!open.Value);
        ModLog.Debug($"desktop mode: World View {(open.Value ? "closed" : "opened")} by the key {key}");
    }

    /// <summary>What the game uses the key for, or null when it is free.</summary>
    public static string Conflict(InputHandler input, KeyCode key)
    {
        if (System.Array.IndexOf(DirectKeys, key) >= 0)
            return "a key the game reads directly";
        IEnumerable<KeyBinding> bindings = input.KeyBindings;
        if (bindings == null)
            return null;
        foreach (KeyBinding binding in bindings)
        {
            if (binding?.keyCodes != null && binding.keyCodes.Contains(key))
                return binding.KeyAction.ToString();
        }
        return null;
    }
}
