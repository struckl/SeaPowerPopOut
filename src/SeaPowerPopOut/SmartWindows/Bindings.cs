using System;
using Noesis;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>Reading the game's bindings, to find its elements by what they show.</summary>
internal static class Bindings
{
    /// <summary>The path an element's property is bound to, or null (not bound, or not a path binding).</summary>
    public static string PathOf(DependencyObject element, DependencyProperty property)
    {
        try
        {
            return BindingOperations.GetBindingExpression(element, property)?.ParentBinding?.Path?.Path;
        }
        catch (Exception)
        {
            // A binding Noesis cannot describe counts as none.
            return null;
        }
    }
}
