namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// The built-in desktops, used the first time a desktop is switched to: 1 = the World View (3D) on top, 2 = the
/// Tactical Display (map) on top, 3 = the map on the whole screen, 4 = the 3D view on the whole screen, 5 = both
/// side by side. Rectangles are fractions of the UI area; very wide screens get a three-column arrangement.
/// </summary>
internal static class DesktopPresets
{
    private const string Map = "Tactical Display";
    private const string World = "World View";
    private const string Status = "Unit Status*";
    private const string Reference = "Unit Reference*";
    private const string Log = "Event Log";
    private const string Formation = "Formation Manager";

    /// <param name="worldElsewhere">The game shows the World View on another monitor: the map takes its place here.</param>
    public static DesktopSet Build(int index, float screenWidth, float screenHeight, float hostWidth, float hostHeight,
        bool worldElsewhere = false)
    {
        var set = new DesktopSet { WorldViewOpen = true };
        void Put(string pattern, float x, float y, float w, float h)
        {
            if (worldElsewhere && pattern == World)
                return;
            // Stored under the same key the window memory uses for the window ("tacticaldisplay").
            set.Layouts.Set(PatternList.Squash(pattern), new SavedLayout
            {
                X = Round(x * hostWidth), Y = Round(y * hostHeight), W = Round(w * hostWidth), H = Round(h * hostHeight),
            });
        }

        bool ultrawide = screenHeight > 0f && screenWidth / screenHeight >= 2.1f;
        if (worldElsewhere)
        {
            // The game's own World View is on another monitor and it keeps the map on this whole screen: the desktops
            // only arrange the windows over the map (1: on the right, 2: along the bottom, 3+: Unit Status only).
            set.WorldViewOpen = null;
            if (index == 1)
            {
                Put(Reference, ultrawide ? 0.7f : 0.62f, 0f, ultrawide ? 0.3f : 0.38f, 0.52f);
                Put(Status, ultrawide ? 0.7f : 0.62f, 0.52f, ultrawide ? 0.3f : 0.38f, 0.48f);
            }
            else if (index == 2)
            {
                Put(Reference, 0f, 0.6f, 0.49f, 0.4f);
                Put(Status, 0.49f, 0.6f, 0.51f, 0.4f);
            }
            else
            {
                Put(Status, 0.62f, 0.58f, 0.38f, 0.42f);
            }
            return set;
        }
        if (index <= 2)
        {
            string main = index == 2 ? Map : World;
            string side = index == 2 ? World : Map;
            if (ultrawide)
            {
                Put(main, 0.3f, 0f, 0.4f, 1f);
                Put(side, 0f, 0f, 0.3f, 0.55f);
                Put(Status, 0.7f, 0f, 0.3f, 0.52f);
                Put(Reference, 0.7f, 0.52f, 0.3f, 0.48f);
                Put(Log, 0f, 0.55f, 0.3f, 0.22f);
                Put(Formation, 0f, 0.55f, 0.3f, 0.45f);
            }
            else
            {
                Put(main, 0f, 0f, 1f, 0.6f);
                Put(side, 0f, 0.6f, 0.49f, 0.4f);
                Put(Status, 0.49f, 0.6f, 0.51f, 0.4f);
                Put(Reference, 0.53f, 0.46f, 0.47f, 0.54f);
                Put(Log, 0f, 0f, 0.28f, 0.19f);
                Put(Formation, 0.02f, 0.03f, 0.18f, 0.45f);
            }
            return set;
        }

        switch (index)
        {
            case 3:
                Put(Map, 0f, 0f, 1f, 1f);
                Put(World, 0f, 0.6f, 0.49f, 0.4f);
                set.WorldViewOpen = false;
                break;
            case 4:
                Put(World, 0f, 0f, 1f, 1f);
                Put(Map, 0f, 0.6f, 0.49f, 0.4f);
                break;
            default:
                Put(Map, 0f, 0f, 0.5f, 1f);
                Put(World, 0.5f, 0f, 0.5f, 1f);
                break;
        }
        Put(Status, 0.62f, 0.58f, 0.38f, 0.42f);
        return set;
    }

    private static float Round(float v) => (float)System.Math.Round(v);
}
