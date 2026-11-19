namespace SeaPowerPopOut.SmartWindows;

/// <summary>Service entry years as the sorting features use them.</summary>
internal static class ServiceYear
{
    /// <summary>For sorting oldest first: a unit or loadout without a year goes after all dated ones.</summary>
    public static int SortKey(int year) => year > 0 ? year : int.MaxValue;
}
