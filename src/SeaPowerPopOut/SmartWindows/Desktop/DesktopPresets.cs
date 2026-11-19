namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// The built-in desktops, used the first time a desktop is switched to: 1 = the World View (3D) on top, 2 = the
/// Tactical Display (map) on top, 3 = the map on the whole screen, 4 = the 3D view on the whole screen, 5 = both
/// side by side. Rectangles are fractions of the UI area; very wide screens get a three-column arrangement.
/// </summary>
internal static class DesktopPresets
{
    private const string Map = GameWindows.TacticalDisplay;
    private const string World = GameWindows.WorldView;
    private const string Status = GameWindows.UnitStatus;
    private const string Reference = GameWindows.UnitReference;
    private const string Log = GameWindows.EventLog;
    private const string Formation = GameWindows.FormationManager;

    /// <summary>One desktop being filled: rectangles as fractions of the UI area.</summary>
    private sealed class Arrangement
    {
        public readonly DesktopSet Set = new DesktopSet { WorldViewOpen = true };
        public float HostWidth, HostHeight;
        public bool WorldElsewhere;

        public void Put(string pattern, float x, float y, float w, float h)
        {
            if (WorldElsewhere && pattern == World)
                return;
            // Stored under the same key the window memory uses for the window ("tacticaldisplay").
            Set.Layouts.Set(PatternList.Squash(pattern), new SavedLayout
            {
                X = Round(x * HostWidth), Y = Round(y * HostHeight), W = Round(w * HostWidth), H = Round(h * HostHeight),
            });
        }
    }

    /// <param name="worldElsewhere">The game shows the World View on another monitor: the map takes its place here.</param>
    public static DesktopSet Build(int index, float screenWidth, float screenHeight, float hostWidth, float hostHeight,
        bool worldElsewhere = false)
    {
        var a = new Arrangement { HostWidth = hostWidth, HostHeight = hostHeight, WorldElsewhere = worldElsewhere };
        bool ultrawide = screenHeight > 0f && screenWidth / screenHeight >= 2.1f;
        if (worldElsewhere)
            OverTheMap(a, index, ultrawide);
        else if (index <= 2)
            OneOnTop(a, index, ultrawide);
        else
            Large(a, index);
        return a.Set;
    }

    /// <summary>
    /// The game's own World View is on another monitor and it keeps the map on this whole screen: the desktops only
    /// arrange the windows over the map (1: on the right, 2: along the bottom, 3+: Unit Status only).
    /// </summary>
    private static void OverTheMap(Arrangement a, int index, bool ultrawide)
    {
        a.Set.WorldViewOpen = null;
        if (index == 1)
        {
            a.Put(Reference, ultrawide ? 0.7f : 0.62f, 0f, ultrawide ? 0.3f : 0.38f, 0.52f);
            a.Put(Status, ultrawide ? 0.7f : 0.62f, 0.52f, ultrawide ? 0.3f : 0.38f, 0.48f);
        }
        else if (index == 2)
        {
            a.Put(Reference, 0f, 0.6f, 0.49f, 0.4f);
            a.Put(Status, 0.49f, 0.6f, 0.51f, 0.4f);
        }
        else
        {
            a.Put(Status, 0.62f, 0.58f, 0.38f, 0.42f);
        }
    }

    /// <summary>1: the World View on top, 2: the map on top, the other one and the unit windows around it.</summary>
    private static void OneOnTop(Arrangement a, int index, bool ultrawide)
    {
        string main = index == 2 ? Map : World;
        string side = index == 2 ? World : Map;
        if (ultrawide)
        {
            a.Put(main, 0.3f, 0f, 0.4f, 1f);
            a.Put(side, 0f, 0f, 0.3f, 0.55f);
            a.Put(Status, 0.7f, 0f, 0.3f, 0.52f);
            a.Put(Reference, 0.7f, 0.52f, 0.3f, 0.48f);
            a.Put(Log, 0f, 0.55f, 0.3f, 0.22f);
            a.Put(Formation, 0f, 0.55f, 0.3f, 0.45f);
            return;
        }
        a.Put(main, 0f, 0f, 1f, 0.6f);
        a.Put(side, 0f, 0.6f, 0.49f, 0.4f);
        a.Put(Status, 0.49f, 0.6f, 0.51f, 0.4f);
        a.Put(Reference, 0.53f, 0.46f, 0.47f, 0.54f);
        a.Put(Log, 0f, 0f, 0.28f, 0.19f);
        a.Put(Formation, 0.02f, 0.03f, 0.18f, 0.45f);
    }

    /// <summary>3: the map on the whole screen, 4: the 3D view on the whole screen, 5: both side by side.</summary>
    private static void Large(Arrangement a, int index)
    {
        switch (index)
        {
            case 3:
                a.Put(Map, 0f, 0f, 1f, 1f);
                a.Put(World, 0f, 0.6f, 0.49f, 0.4f);
                a.Set.WorldViewOpen = false;
                break;
            case 4:
                a.Put(World, 0f, 0f, 1f, 1f);
                a.Put(Map, 0f, 0.6f, 0.49f, 0.4f);
                break;
            default:
                a.Put(Map, 0f, 0f, 0.5f, 1f);
                a.Put(World, 0.5f, 0f, 0.5f, 1f);
                break;
        }
        a.Put(Status, 0.62f, 0.58f, 0.38f, 0.42f);
    }

    private static float Round(float v) => (float)System.Math.Round(v);
}
