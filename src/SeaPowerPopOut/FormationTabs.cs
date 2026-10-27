using System;
using System.Reflection;
using HarmonyLib;

namespace SeaPowerPopOut;

/// <summary>
/// A fix of the game's formation manager tabs (All, Surface, Subs, Air, Helos, Ground), on both screens. Its
/// TabControl always holds all six tabs, an empty category's collapsed, so the selected index counts those too –
/// <c>FormationManagerViewModel.SelectedTabIndex</c> counts only the visible ones. With a tab missing in front of it
/// (no submarines), "Air" stood for the category after it, and double-clicking a plane jumped the list to "All": the
/// game shows "All" unless the selected category is the plane's own. Here the index counts every tab, as the
/// TabControl does.
/// </summary>
internal static class FormationTabs
{
    private const string TypeName = "SeapowerUI.ViewModels.FormationManagerViewModel";

    private static FieldInfo selected;        // FormationManagerCategory _selectedCategory
    private static FieldInfo tabOrder;        // static FormationManagerCategory[] TabOrder: the TabControl's order
    private static MethodInfo notify;
    private static Array order;

    /// <summary>False if a game update changed what the fix relies on; the game's own code runs then.</summary>
    public static bool Resolve()
    {
        Type type = AccessTools.TypeByName(TypeName);
        if (type == null)
            return false;
        selected = AccessTools.Field(type, "_selectedCategory");
        tabOrder = AccessTools.Field(type, "TabOrder");
        notify = AccessTools.Method(type, "NotifyPropertyChanged", new[] { typeof(string) });
        return selected != null && tabOrder != null && notify != null && tabOrder.IsStatic
               && tabOrder.FieldType == selected.FieldType.MakeArrayType();
    }

    // Read on first use, not at plugin load: it runs the view model's static constructor.
    private static Array Order => order ??= (Array)tabOrder.GetValue(null);

    /// <summary>Getter prefix: the selected category's place among all tabs. False = the game's getter is skipped.</summary>
    public static bool Get(object model, ref int index)
    {
        int i = Array.IndexOf(Order, selected.GetValue(model));
        if (i < 0)
            return true;
        index = i;
        return false;
    }

    /// <summary>Setter prefix: the tab the player picked, counted among all tabs.</summary>
    public static bool Set(object model, int index)
    {
        if (index < 0 || index >= Order.Length)
            return false;
        object category = Order.GetValue(index);
        if (!Equals(selected.GetValue(model), category))
        {
            selected.SetValue(model, category);
            notify.Invoke(model, new object[] { "SelectedTabIndex" });
        }
        return false;
    }
}
