using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using HarmonyLib;
using SeaPower;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Unit Reference tree: the variants under a class (Kidd-class ...) of a unit that has loadout variants are labelled
/// "name [loadout]" instead of only a year, and ordered by the unit's .ini, then by date, then by the order of the
/// loadouts in the .ini (an "Early" loadout first). A unit whose .ini has loadouts but no "Default" one does not get a
/// row of its own for the plain unit: its loadouts stand in for it.
/// </summary>
internal static class VariantGroups
{
    private static readonly string[] UnitCategories = { "Vessels", "Submarines", "Aircrafts", "Helicopters", "LandUnits", "Biologic" };

    /// <summary>One unit (one .ini) in a class: its plain entry, if shown, and its loadout entries.</summary>
    private sealed class UnitRows
    {
        public string Ini = "";
        public MuseumEntry Plain;
        public readonly List<MuseumEntry> Loadouts = new List<MuseumEntry>();
        public Dictionary<string, Loadout> Defs;
        public string Qualifier = "";
        public int Date = int.MaxValue;
    }

    public static bool Apply(Harmony harmony)
    {
        var setter = AccessTools.PropertySetter(typeof(UnitTreeBrowserViewModel), nameof(UnitTreeBrowserViewModel.MuseumEntries));
        ConstructorInfo constructor = AccessTools.FirstConstructor(typeof(UnitTreeBrowserViewModel),
            c => c.GetParameters().Length > 0 && c.GetParameters()[0].ParameterType == typeof(string));
        if (setter == null || constructor == null)
        {
            ModLog.Warn("Unit Reference tree entries not found (game updated?) - loadout variants stay as the game labels them.");
            return false;
        }
        harmony.Patch(constructor, postfix: new HarmonyMethod(typeof(VariantGroups), nameof(Constructed)));
        harmony.Patch(setter, postfix: new HarmonyMethod(typeof(VariantGroups), nameof(EntriesSet)));
        return true;
    }

    private static void Constructed(UnitTreeBrowserViewModel __instance, string stateKey)
    {
        if (stateKey == BrowserMemory.EncyclopediaKey)
            ArrangeAll(__instance);
    }

    private static void EntriesSet(UnitTreeBrowserViewModel __instance)
    {
        if (BrowserMemory.IsEncyclopedia(__instance))
            ArrangeAll(__instance);
    }

    private static void ArrangeAll(UnitTreeBrowserViewModel browser)
    {
        if (browser.MuseumEntries == null)
            return;
        try
        {
            Dictionary<string, Dictionary<string, Loadout>> loadoutsByIni = null;
            int arranged = 0;
            foreach (MuseumEntry category in browser.MuseumEntries)
            {
                if (Array.IndexOf(UnitCategories, category?.CategoryKey) < 0 || category.MuseumEntries == null)
                    continue;
                foreach (MuseumEntry folder in category.MuseumEntries)
                {
                    if (folder == null || folder.GetType() != typeof(MuseumEntry) || folder.MuseumEntries == null)
                        continue;
                    foreach (MuseumEntry group in folder.MuseumEntries)
                    {
                        if (group != null && group.IsGroupNode && HasLoadoutUnit(group.MuseumEntries))
                        {
                            loadoutsByIni ??= LoadoutsByIni();
                            if (Arrange(group, loadoutsByIni))
                                arranged++;
                        }
                    }
                }
            }
            ModLog.Debug($"smart windows: {arranged} class group(s) with loadout variants arranged");
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Unit Reference loadout variants not arranged: {ex.Message}");
        }
    }

    private static bool HasLoadoutUnit(ObservableCollection<MuseumEntry> entries)
    {
        if (entries == null)
            return false;
        foreach (MuseumEntry entry in entries)
            if (IsLoadout(entry))
                return true;
        return false;
    }

    private static bool IsLoadout(MuseumEntry entry) => entry is MuseumObjectEntry { IsLoadoutUnit: true };

    private static string VariantOf(MuseumEntry entry) => (entry as MuseumObjectEntry)?.PredefinedLoadoutVariant;

    private static bool Arrange(MuseumEntry group, Dictionary<string, Dictionary<string, Loadout>> loadoutsByIni)
    {
        ObservableCollection<MuseumEntry> entries = group.MuseumEntries;
        string className = group.DisplayName ?? "";

        List<UnitRows> units = SplitByUnit(entries, loadoutsByIni);
        foreach (UnitRows unit in units)
            Prepare(unit, className);
        units.RemoveAll(u => u.Plain == null && u.Loadouts.Count == 0);
        MoveSort(units, (a, b) =>
        {
            int byDate = a.Date.CompareTo(b.Date);
            return byDate != 0 ? byDate : string.Compare(a.Qualifier, b.Qualifier, StringComparison.OrdinalIgnoreCase);
        });

        var order = new List<MuseumEntry>();
        foreach (UnitRows unit in units)
            AddRows(unit, order);
        return Reorder(entries, order);
    }

    private static void MoveSort<T>(List<T> list, Comparison<T> compare) =>
        StableOrder.Sort(list, compare, (from, to) =>
        {
            T item = list[from];
            list.RemoveAt(from);
            list.Insert(to, item);
        });

    /// <summary>The rows of a class grouped by .ini; a second plain row of the same .ini is a unit of its own.</summary>
    private static List<UnitRows> SplitByUnit(IEnumerable<MuseumEntry> entries, Dictionary<string, Dictionary<string, Loadout>> loadoutsByIni)
    {
        var units = new List<UnitRows>();
        var byIni = new Dictionary<string, UnitRows>(StringComparer.OrdinalIgnoreCase);
        foreach (MuseumEntry entry in entries)
        {
            if (entry == null)
                continue;
            string ini = entry.IniRef ?? "";
            if (ini.Length == 0 || !byIni.TryGetValue(ini, out UnitRows unit))
            {
                unit = new UnitRows { Ini = ini };
                if (ini.Length > 0)
                {
                    loadoutsByIni.TryGetValue(ini, out unit.Defs);
                    byIni[ini] = unit;
                }
                units.Add(unit);
            }
            if (IsLoadout(entry))
                unit.Loadouts.Add(entry);
            else if (unit.Plain == null)
                unit.Plain = entry;
            else
                units.Add(new UnitRows { Ini = ini, Plain = entry });
        }
        return units;
    }

    /// <summary>Label stem, date and the order of the loadouts of one unit.</summary>
    private static void Prepare(UnitRows unit, string className)
    {
        if (unit.Plain != null && unit.Loadouts.Count > 0 && unit.Defs != null && !unit.Defs.ContainsKey("Default"))
            unit.Plain = null;
        string name = unit.Plain != null ? unit.Plain.DisplayName : PlainName(unit);
        unit.Qualifier = UnitTreeGrouping.GetClassQualifier(name ?? "", className);
        if (unit.Qualifier.Length == 0)
            unit.Qualifier = className;

        unit.Date = unit.Plain != null ? DateKey(unit.Plain) : int.MaxValue;
        if (unit.Plain == null)
            foreach (MuseumEntry loadout in unit.Loadouts)
                unit.Date = Math.Min(unit.Date, DateKey(loadout));

        var rank = new Dictionary<MuseumEntry, int>();
        foreach (MuseumEntry loadout in unit.Loadouts)
            rank[loadout] = Rank(unit, VariantOf(loadout));
        MoveSort(unit.Loadouts, (a, b) =>
        {
            int byDate = DateKey(a).CompareTo(DateKey(b));
            return byDate != 0 ? byDate : rank[a].CompareTo(rank[b]);
        });
    }

    /// <summary>Puts the unit's rows into <paramref name="order"/> and sets their labels.</summary>
    private static void AddRows(UnitRows unit, List<MuseumEntry> order)
    {
        int count = unit.Loadouts.Count + (unit.Plain != null ? 1 : 0);
        var rows = new List<MuseumEntry>(unit.Loadouts);
        if (unit.Plain != null)
        {
            // The plain unit goes before the first loadout that came later (an "Early" loadout of the same year goes before it).
            int at = 0;
            int date = DateKey(unit.Plain);
            while (at < rows.Count && (DateKey(rows[at]) < date || (DateKey(rows[at]) == date && IsEarly(rows[at]))))
                at++;
            rows.Insert(at, unit.Plain);
        }
        foreach (MuseumEntry row in rows)
        {
            if (unit.Loadouts.Count > 0 || count > 1)
            {
                string label = unit.Qualifier;
                if (count > 1 && IsLoadout(row))
                    label += " [" + LoadoutName(unit.Defs, VariantOf(row)) + "]";
                row.TreeLabelOverride = label;
            }
            order.Add(row);
        }
    }

    private static int DateKey(MuseumEntry entry) => entry.ServiceEntryDate > 0 ? entry.ServiceEntryDate : int.MaxValue;

    private static bool IsEarly(MuseumEntry entry) =>
        string.Equals(VariantOf(entry), "Early", StringComparison.OrdinalIgnoreCase);

    /// <summary>Position of the loadout in the .ini; "Early" comes before all, unknown ones last.</summary>
    private static int Rank(UnitRows unit, string loadout)
    {
        if (string.Equals(loadout, "Early", StringComparison.OrdinalIgnoreCase))
            return -1;
        int position = 0;
        if (unit.Defs != null)
        {
            foreach (string key in unit.Defs.Keys)
            {
                if (string.Equals(key, loadout, StringComparison.OrdinalIgnoreCase))
                    return position;
                position++;
            }
        }
        return int.MaxValue;
    }

    /// <summary>The unit's name without the " [loadout]" the game appends to a loadout row's name.</summary>
    private static string PlainName(UnitRows unit)
    {
        if (unit.Loadouts.Count == 0)
            return "";
        MuseumEntry first = unit.Loadouts[0];
        string name = first.DisplayName ?? "";
        string suffix = " [" + LoadoutName(unit.Defs, VariantOf(first)) + "]";
        return name.EndsWith(suffix, StringComparison.Ordinal) ? name.Substring(0, name.Length - suffix.Length) : name;
    }

    private static string LoadoutName(Dictionary<string, Loadout> defs, string loadout)
    {
        loadout ??= "";
        string name = defs != null && defs.TryGetValue(loadout, out Loadout def) ? def?.DisplayName : null;
        return string.IsNullOrWhiteSpace(name) ? loadout : name.Trim();
    }

    /// <summary>The loadouts of every plain unit, by .ini name.</summary>
    private static Dictionary<string, Dictionary<string, Loadout>> LoadoutsByIni()
    {
        var result = new Dictionary<string, Dictionary<string, Loadout>>(StringComparer.OrdinalIgnoreCase);
        if (!Singleton<UnitMetadataProvider>.InstanceExists())
            return result;
        var data = Singleton<UnitMetadataProvider>.Instance.UnitData;
        if (data == null)
            return result;
        foreach (var value in data.Values)
        {
            SimpleObjectParameters unit = value.Item2;
            if (unit != null && !unit.IsLoadoutUnit && !string.IsNullOrEmpty(unit._objectIniName) && unit._loadouts != null)
                result[unit._objectIniName] = unit._loadouts;    // a unit seen twice keeps the last
        }
        return result;
    }

    /// <summary>Brings the collection into <paramref name="order"/> with moves; rows not in it are removed.</summary>
    private static bool Reorder(ObservableCollection<MuseumEntry> entries, List<MuseumEntry> order)
    {
        bool changed = false;
        var wanted = new HashSet<MuseumEntry>(order);
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(entries[i]))
            {
                entries.RemoveAt(i);
                changed = true;
            }
        }
        for (int target = 0; target < order.Count; target++)
        {
            int at = entries.IndexOf(order[target]);
            if (at >= 0 && at != target)
            {
                entries.Move(at, target);
                changed = true;
            }
        }
        return changed;
    }
}
