using System;
using System.Runtime.CompilerServices;
using Noesis;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// The game's messages in the bottom right corner ("top alerts") no longer catch the mouse, so the map and buttons under
/// them can be clicked. Their unit and track links can then not be clicked either: the dark box is the message lines
/// themselves, there is no background to clear separately. The list is found when it loads, by its items' binding.
/// </summary>
internal static class MessageClickThrough
{
    private const string AlertsBinding = "TopAlerts";

    private static readonly ConditionalWeakTable<ItemsControl, object> Done = new ConditionalWeakTable<ItemsControl, object>();

    public static void Register()
    {
        try
        {
            EventManager.RegisterClassHandler(typeof(ItemsControl), FrameworkElement.LoadedEvent, new RoutedEventHandler(Loaded));
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Message click-through not available: {ex.Message}");
        }
    }

    private static void Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!(sender is ItemsControl list) || Done.TryGetValue(list, out _))
                return;
            if (!(BindingOperations.GetBinding(list, ItemsControl.ItemsSourceProperty) is Binding binding) || binding.Path?.Path != AlertsBinding)
                return;
            Done.Add(list, list);
            list.IsHitTestVisible = false;
            ModLog.Debug("smart windows: the messages list no longer blocks clicks");
        }
        catch (Exception ex)
        {
            ModLog.Debug($"message click-through: {ex.Message}");
        }
    }
}
