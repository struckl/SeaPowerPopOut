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
    public static readonly Feature Feature = new Feature
    {
        Switch = () => SmartSettings.MessagesClickThrough.Value,
        Hook = _ => Register(),
    };

    private const string AlertsBinding = "TopAlerts";

    private static readonly ConditionalWeakTable<ItemsControl, object> Done = new ConditionalWeakTable<ItemsControl, object>();

    private static bool Register()
    {
        try
        {
            EventManager.RegisterClassHandler(typeof(ItemsControl), FrameworkElement.LoadedEvent, new RoutedEventHandler(Loaded));
            return true;
        }
        catch (Exception ex)
        {
            Rig.Log.Warn($"Message click-through not available: {ex.Message}");
            return false;
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
            Rig.Log.Debug("smart windows: the messages list no longer blocks clicks");
        }
        catch (Exception ex)
        {
            Rig.Log.Debug($"message click-through: {ex.Message}");
        }
    }
}
