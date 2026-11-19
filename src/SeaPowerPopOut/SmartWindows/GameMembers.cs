using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Properties of game types that are only known at run time (a base class's subtypes, internal rule objects), looked up
/// once per type and name: the callers run for every unit or every frame.
/// </summary>
internal static class GameMembers
{
    private static readonly Dictionary<KeyValuePair<Type, string>, PropertyInfo> Properties =
        new Dictionary<KeyValuePair<Type, string>, PropertyInfo>();

    /// <summary>The property, or null when the type has none (a missing one is cached too).</summary>
    public static PropertyInfo Property(Type type, string name)
    {
        var key = new KeyValuePair<Type, string>(type, name);
        if (!Properties.TryGetValue(key, out PropertyInfo property))
        {
            property = AccessTools.Property(type, name);
            Properties[key] = property;
        }
        return property;
    }

    /// <summary>The property's value, or null when the object has no such property.</summary>
    public static object Read(object target, string name) => Property(target.GetType(), name)?.GetValue(target);
}
