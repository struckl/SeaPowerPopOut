using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Noesis;
using SeapowerUI.ViewModels;
using UnityEngine;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Campaign Task Force Builder: an "Upgrade ship" button under "Open Unit Reference". With a ship selected in the roster it
/// offers the ships of the task force that it can replace; the old ship is sold (dismiss or decommission price), the new one
/// is bought and confirmed with the crew one skill level lower, and the old ship's aircraft move over. Off by default: it
/// changes the campaign's task force.
/// </summary>
internal static class UpgradeShip
{
    private const float Every = 0.4f;
    private const float SearchEvery = 1f;

    private sealed class State
    {
        public Button Button;
        public float Next;
        public float NextSearch;
        public string Tip;
    }

    private static readonly List<WeakReference<GameWindow>> Windows = new List<WeakReference<GameWindow>>();
    private static readonly ConditionalWeakTable<GameWindow, State> States = new ConditionalWeakTable<GameWindow, State>();
    private static bool enabled, warned;

    public static void Register()
    {
        if (!UpgradeGame.Available)
            return;
        enabled = true;
    }

    /// <summary>Called when a game window has loaded.</summary>
    public static void WindowLoaded(GameWindow window)
    {
        if (enabled)
            Windows.Add(new WeakReference<GameWindow>(window));
    }

    /// <summary>Called every frame.</summary>
    public static void Update()
    {
        if (!enabled)
            return;
        for (int i = Windows.Count - 1; i >= 0; i--)
        {
            if (!Windows[i].TryGetTarget(out GameWindow window) || !window.IsLoaded)
            {
                Windows.RemoveAt(i);
                continue;
            }
            if (!(window.DataContext is LinearCampaignTaskForceBuilderViewModel vm))
                continue;
            State state = States.GetOrCreateValue(window);
            if (Time.unscaledTime < state.Next)
                continue;
            state.Next = Time.unscaledTime + Every;
            try
            {
                Refresh(window, vm, state);
            }
            catch (Exception ex)
            {
                if (!warned)
                    ModLog.Warn($"Upgrade ship: {ex.Message}");
                warned = true;
                ModLog.Debug($"upgrade ship: {ex}");
            }
        }
    }

    private static void Refresh(GameWindow window, LinearCampaignTaskForceBuilderViewModel vm, State state)
    {
        if (state.Button == null || VisualTreeHelper.GetParent(state.Button) == null)
        {
            state.Button = null;
            if (Time.unscaledTime < state.NextSearch)
                return;
            state.NextSearch = Time.unscaledTime + SearchEvery;
            state.Button = AddButton(window, vm);
            if (state.Button == null)
                return;
        }

        string why = UpgradeGame.Unavailable(vm, out _);
        state.Button.IsEnabled = why == null;
        state.Button.Opacity = why == null ? 1f : 0.5f;
        string tip = why == null ? Loc.Get("up_tip") : Loc.Get("up_tip") + "\n" + Loc.Get(why);
        if (tip == state.Tip)
            return;
        state.Tip = tip;
        state.Button.ToolTip = tip;
    }

    /// <summary>Puts the button after the one that opens the Unit Reference (found by its command).</summary>
    private static Button AddButton(GameWindow window, LinearCampaignTaskForceBuilderViewModel vm)
    {
        Button reference = FindByCommand(window, vm.OpenSelectedUnitReferenceCommand, 0);
        if (reference == null || !(VisualTreeHelper.GetParent(reference) is Panel panel))
            return null;
        Loc.Reload();
        var button = new Button { Content = Loc.Get("up_button"), Height = 34f, Margin = new Thickness(0f, 8f, 0f, 0f) };
        try
        {
            button.Style = reference.Style;
        }
        catch (Exception ex)
        {
            ModLog.Debug($"upgrade ship: button style not copied: {ex.Message}");
        }
        button.Click += (s, e) =>
        {
            try
            {
                UpgradeMenu.Open(button, vm);
            }
            catch (Exception ex)
            {
                ModLog.Warn($"Upgrade ship (menu): {ex.Message}");
            }
        };
        panel.Children.Insert(panel.Children.IndexOf(reference) + 1, button);
        ModLog.Debug("upgrade ship: button added");
        return button;
    }

    private static Button FindByCommand(DependencyObject node, System.Windows.Input.ICommand command, int depth)
    {
        if (command == null || depth > 60)
            return null;
        if (node is Button button && ReferenceEquals(button.Command, command))
            return button;
        for (int i = 0, n = VisualTreeHelper.GetChildrenCount(node); i < n; i++)
        {
            Button found = FindByCommand(VisualTreeHelper.GetChild(node, i), command, depth + 1);
            if (found != null)
                return found;
        }
        return null;
    }
}
