using System;
using System.IO;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Config of the Smart Windows features, in its own file next to Pop-Out's:
/// <c>BepInEx/config/dev.sewerlabs.seapowerpopout.smartwindows.cfg</c>. The saved layouts live in section 9 and are
/// written by the mod; editing them by hand works too.
/// </summary>
internal static class SmartSettings
{
    public static ConfigEntry<bool> Enabled;
    public static ConfigEntry<string> WindowPatterns;
    public static ConfigEntry<float> CascadeOffset;
    public static ConfigEntry<bool> AllowDragOutside;
    public static ConfigEntry<float> MinVisiblePixels;
    public static ConfigEntry<string> Language;
    public static ConfigEntry<bool> RememberBrowser;
    public static ConfigEntry<bool> ImprovedReference;
    public static ConfigEntry<float> TreeWidth;
    public static ConfigEntry<bool> TreeShown;
    public static ConfigEntry<bool> HideCustomMission;
    public static ConfigEntry<bool> RememberMissionFolder;
    public static ConfigEntry<string> LastMissionFolder;
    public static ConfigEntry<bool> ModBackgroundsOnly;
    public static ConfigEntry<bool> ColorIniFiles;
    public static ConfigEntry<string> IniColor;
    public static ConfigEntry<bool> UnitPanelMenu;
    public static ConfigEntry<bool> TooltipFrame;
    public static ConfigEntry<bool> MessagesClickThrough;
    public static ConfigEntry<bool> SortLoadouts;
    public static ConfigEntry<bool> LoadoutPanelOrder;
    public static ConfigEntry<bool> FixResizeCursors;
    public static ConfigEntry<bool> DismissDamaged;
    public static ConfigEntry<bool> FixSwapPrice;
    public static ConfigEntry<bool> StrictLoadouts;
    public static ConfigEntry<bool> UpgradeShip;
    public static ConfigEntry<bool> FullUnitName;
    public static ConfigEntry<bool> StatusReference;
    public static ConfigEntry<float> SectionsMinHeight;
    public static ConfigEntry<bool> TrackState;
    public static ConfigEntry<bool> EarliestVariantOnClick;
    public static ConfigEntry<bool> ScrollToSelected;
    public static ConfigEntry<bool> TreeWheelOnlyOver;
    public static ConfigEntry<bool> HideEmptyCountries;
    public static ConfigEntry<bool> ArmedOnly;
    public static ConfigEntry<bool> SortTree;
    public static ConfigEntry<bool> GroupVariants;
    public static ConfigEntry<bool> ExtendedSearch;
    public static ConfigEntry<bool> LinkHelp;
    public static ConfigEntry<bool> KeepOpenedUnit;
    public static ConfigEntry<bool> LoadoutList;
    public static ConfigEntry<string> BrowserCountry;
    public static ConfigEntry<string> BrowserCategory;
    public static ConfigEntry<bool> BrowserByDate;
    public static ConfigEntry<bool> BrowserNameOnly;
    public static ConfigEntry<string> BrowserSearch;
    public static ConfigEntry<bool> SnapEnabled;
    public static ConfigEntry<float> SnapDistance;
    public static ConfigEntry<bool> BorderEnabled;
    public static ConfigEntry<float> BorderThickness;
    public static ConfigEntry<string> BorderColor;
    public static ConfigEntry<bool> SettingsButton;
    public static ConfigEntry<bool> LookButton;
    public static ConfigEntry<bool> DeleteOldLogs;
    public static ConfigEntry<bool> DragTimePanel;
    public static ConfigEntry<string> TimePanelPosition;
    public static ConfigEntry<string> DumpWindows;
    public static ConfigEntry<string> Layouts;
    public static ConfigEntry<string> WindowLooks;
    public static ConfigEntry<string> WindowLevels;
    public static ConfigEntry<bool> DesktopMode;
    public static ConfigEntry<int> DesktopCount;
    public static ConfigEntry<int> DesktopActive;
    public static readonly ConfigEntry<string>[] DesktopSlots = new ConfigEntry<string>[6];   // 1 - 5
    public static ConfigEntry<string> WorldViewOpen;
    public static ConfigEntry<KeyCode> WorldViewKey;

    private const float SaveDelay = 1f;
    private static ConfigFile file;
    private static float changedAt = -1f;

    public static void Init()
    {
        file = new ConfigFile(Path.Combine(Paths.ConfigPath, Plugin.Guid + ".smartwindows.cfg"), saveOnInit: true);
        // Typing in a text box changes a value on every key: write the file a moment after the last change instead.
        file.SaveOnConfigSet = false;
        file.SettingChanged += (sender, args) => changedAt = Time.unscaledTime;

        Enabled = file.Bind("1. General", "Enable window memory", true,
            "Remember the position and size of the windows listed below. Needs a restart.");
        WindowPatterns = file.Bind("2. Remembered windows", "Window patterns",
            "Tactical Display,World View,Unit Reference*,Unit Status*,Event Log,Formation Manager,Task Force Builder,Weapon Reference",
            "Comma-separated window names to remember. Ignores case and spaces; * matches any text. A pattern must " +
            "match from the start of the name (the window's content type, e.g. UnitStatusViewModel), so use *text* to " +
            "match anywhere. Windows matching the same pattern share one saved layout.");
        CascadeOffset = file.Bind("3. Placement", "Cascade offset", 15f,
            "Several windows of one pattern open at once: each further one opens shifted right and down by this many " +
            "pixels per window already open. 0 = all at the saved place.");
        AllowDragOutside = file.Bind("3. Placement", "Allow dragging outside", true,
            "Allow windows to be dragged partly outside the visible area.");
        MinVisiblePixels = file.Bind("3. Placement", "Minimum visible pixels", 25f,
            "With 'Allow dragging outside': at least this many pixels of the window's width stay visible at the left " +
            "and right edge. The title bar always stays on screen at the bottom.");
        SnapEnabled = file.Bind("3. Placement", "Snap windows", true,
            "While a window is dragged or resized with the mouse, its edges snap to the edges of the screen and of " +
            "the other windows when they come closer than the snap distance.");
        SnapDistance = file.Bind("3. Placement", "Snap distance", 10f,
            new ConfigDescription("How close an edge must come to snap, in pixels.", new AcceptableValueRange<float>(1f, 50f)));
        Language = file.Bind("1. General", "Language", "",
            "Language of the mod's texts: a game language code (en, ru, cn, de, es, fr, ko, ja, vn). Empty = the " +
            "game's own. The texts are in the lang folder next to the DLL - edit them to translate.");
        BorderEnabled = file.Bind("4. Window border", "Enabled", true,
            "A thin frame around every remembered window, on the edge of its visible part. Drawn over the window; " +
            "it does not change its size or catch the mouse. Applies to windows opened afterwards.");
        BorderThickness = file.Bind("4. Window border", "Thickness", 1f,
            new ConfigDescription("Width of the frame in pixels. The top side is left out (it would run under the title bar).",
                new AcceptableValueRange<float>(0.5f, 10f)));
        BorderColor = file.Bind("4. Window border", "Color", "#A0808080",
            "Colour of the frame: #RRGGBB or #AARRGGBB (AA = opacity, 00 transparent - FF solid).");
        SettingsButton = file.Bind("5. Settings window", "Gear button", true,
            "Show the gear button in the title bar of remembered windows; it opens the settings panel.");
        LookButton = file.Bind("5. Settings window", "Look button", true,
            "Show the look button in the title bar of remembered windows; its menu sets the opacity, scale, text outline " +
            "and level (below / above the other windows) of the window. Needs a restart.");
        RememberBrowser = file.Bind("6. Unit Reference", "Remember choices", true,
            "Unit Reference window: remember the country, category, date filter, name-only switch and search text " +
            "between game runs.");
        ImprovedReference = file.Bind("6. Unit Reference", "Improved layout", true,
            "Unit Reference window: a draggable splitter at the right edge of the unit tree, with its width and its " +
            "shown / hidden state kept between runs. Applies to windows opened afterwards.");
        TreeWidth = file.Bind("6. Unit Reference", "Tree width", 0f,
            "Written by the mod when the splitter is dragged: the tree's width in pixels. 0 = the game's width + 35.");
        TreeShown = file.Bind("6. Unit Reference", "Tree shown", true,
            "Written by the mod: whether the unit tree is shown; a new window opens the same way.");
        DismissDamaged = file.Bind("A. Campaign", "Dismiss damaged ships", true,
            "Campaign Task Force status: a damaged ship that can be repaired may be dismissed; the refund is reduced by the " +
            "repair cost. Needs a restart.");
        FixSwapPrice = file.Bind("A. Campaign", "Fix hull swap price", true,
            "Campaign Task Force Builder, Loadouts tab: a swap to another hull version of the class is priced from the campaign's " +
            "roster like buying it (the game uses the unit's ini price for the new hull and the roster price for the ship you have, " +
            "so a different roster price turned an upgrade into a refund). Needs a restart.");
        UpgradeShip = file.Bind("A. Campaign", "Upgrade ship", false,
            "Campaign Task Force Builder: an \"Upgrade ship\" button replaces a ship of the task force by the ship selected in the " +
            "roster: the old ship is sold at the Dismiss / Decommission price, the new one is bought and confirmed with the crew " +
            "one skill level lower, and the old ship's aircraft move over. It changes the campaign's task force and points, so it " +
            "is off by default. Needs a restart.");
        UnitPanelMenu = file.Bind("8. Housekeeping", "Unit panel menu", true,
            "Mission: a right click on the flag or the name of the selected unit in the unit panel at the bottom opens the same " +
            "menu as a right click on the unit on the map. Needs a restart.");
        TooltipFrame = file.Bind("4. Window border", "Tooltip frame", true,
            "A frame around the game's tooltips (pop-up hints) too, on all four sides, with the width and colour above. Needs a restart.");
        MessagesClickThrough = file.Bind("8. Housekeeping", "Messages do not block clicks", true,
            "The game's messages in the bottom right corner no longer catch the mouse, so the map and buttons under them can be " +
            "clicked. Their unit and track links can then not be clicked. Needs a restart.");
        StrictLoadouts = file.Bind("A. Campaign", "Strict loadouts", true,
            "Campaign start screen: adds a 'Strict loadouts' check box. When ticked for a new campaign, the Task Force Builder offers " +
            "only ships the mission lists as available (or that you own) and only loadouts already open. Saved with the campaign " +
            "(one extra entry in its data; harmless without the mod). Needs a restart.");
        FixResizeCursors = file.Bind("8. Housekeeping", "Resize cursors in menus", true,
            "Outside a mission the game shows the plain arrow on the resize edges of its windows. On: the mod loads the game's cursors and installs the handler the missions use.");
        SortLoadouts = file.Bind("A. Campaign", "Sort loadouts by date", true,
            "Campaign Task Force Builder, Loadouts tab: the loadouts are listed by service entry year, oldest first. Needs a restart.");
        LoadoutPanelOrder = file.Bind("A. Campaign", "Loadout panel order", true,
            "Campaign Task Force Builder, Loadouts tab: the unit's own loadouts are listed above the other versions when it is the oldest version " +
            "of its class, below them otherwise. Needs a restart.");
        FullUnitName = file.Bind("7. Unit Status", "Full unit name", true,
            "Unit Status window: the header shows the unit's full name instead of the short one. Needs a restart.");
        StatusReference = file.Bind("7. Unit Status", "Reference tab", true,
            "Unit Status window: an extra Reference tab shows the Unit Reference page (stats, weapons, sensors ...) of the selected unit, " +
            "on its own variant and loadout. Not for weapons. Needs a restart.");
        SectionsMinHeight = file.Bind("7. Unit Status", "Sections min height", 278f,
            "Unit Status window, Damage Control tab: the section panels (Aft ... Forward) shrink down to this many pixels; " +
            "below it the 'Auto Damage Control Status' panel at the bottom moves out of a short window. 0 = as the game has it. " +
            "Needs a restart to be set up, then changes apply at once.");
        TrackState = file.Bind("7. Unit Status", "Track State", true,
            "Unit Status window: while a contact of another side (a track, not your own unit) is selected, the window gets a " +
            "Track tab with its Track State (status, confidence, detections, signatures) instead of staying empty. Needs a restart.");
        ModBackgroundsOnly = file.Bind("8. Housekeeping", "Mod backgrounds only", false,
            "Main menu and loading screen pictures: when mods bring their own (ui/backgrounds), show only theirs instead of " +
            "one picked at random from the game's and the mods' together. Without mod pictures the game's stay. Needs a restart.");
        HideCustomMission = file.Bind("8. Housekeeping", "Hide _Custom Mission", true,
            "Main menu: the mission editor's default file \"_Custom Mission\" is not shown in the SCENARIOS list. Needs a restart.");
        RememberMissionFolder = file.Bind("8. Housekeeping", "Remember mission folder", true,
            "Mission editor: the file picker for opening a mission starts in the folder of the mission opened last. Needs a restart.");
        ColorIniFiles = file.Bind("8. Housekeeping", "Color ini files", true,
            "Mission editor file pickers: the names of *.ini files (the missions) are shown in the colour below. Needs a restart.");
        IniColor = file.Bind("8. Housekeeping", "Ini file color", "#6EB8FF",
            "Colour of the ini file names, #RRGGBB (or #AARRGGBB). Needs a restart.");
        LastMissionFolder = file.Bind("8. Housekeeping", "Last mission folder", "", "Written by the mod.");
        EarliestVariantOnClick = file.Bind("6. Unit Reference", "Earliest variant on click", true,
            "Unit Reference tree: a click on a class with several variants selects its earliest variant (the game picks the " +
            "one in service in the mission year, otherwise the latest). Needs a restart.");
        ScrollToSelected = file.Bind("6. Unit Reference", "Scroll to selected", true,
            "Unit Reference tree: scroll so the selected unit is visible (centred when the window opens or the tree is shown). " +
            "Applies to windows opened afterwards.");
        TreeWheelOnlyOver = file.Bind("6. Unit Reference", "Wheel only over the tree", true,
            "Unit Reference: the mouse wheel scrolls the unit tree only while the cursor is over it. Applies to windows opened afterwards.");
        HideEmptyCountries = file.Bind("6. Unit Reference", "Only countries with units", true,
            "Unit Reference: the country list only offers countries that have units for the current search, date filter and " +
            "'Armed only'. Needs a restart.");
        ArmedOnly = file.Bind("6. Unit Reference", "Armed only", false,
            "Unit Reference: leave out units without weapons (rafts, targets, tankers, civilian ships ...). Decoys, " +
            "sonobuoys and fuel tanks do not count as weapons. Also switched in the gear panel.");
        LoadoutList = file.Bind("6. Unit Reference", "Loadout list for all units", true,
            "Unit Reference: the loadout selector is shown for ships, submarines and land units with several loadouts too " +
            "(the game shows it for aircraft only); the unit's information follows the chosen loadout. Needs a restart.");
        SortTree = file.Bind("6. Unit Reference", "Sort tree by date", true,
            "Unit Reference tree: the classes of each unit type are sorted by service entry date (units without a " +
            "date last) instead of the game's file order. Needs a restart.");
        GroupVariants = file.Bind("6. Unit Reference", "Group loadout variants", true,
            "Unit Reference tree: under a class, the variants of a unit with several loadouts are labelled \"name [loadout]\" " +
            "(not only a year) and ordered by date and by the loadout order of the unit's .ini. Needs a restart.");
        ExtendedSearch = file.Bind("6. Unit Reference", "Search ids and descriptions", true,
            "Unit Reference search: without 'search by name only', a unit also matches when the text is in its " +
            "description or in its id (the name of its .ini file). Needs a restart.");
        LinkHelp = file.Bind("6. Unit Reference", "Link tooltips", true,
            "Unit Reference: a tooltip with the short help (the start of the description) of the weapon, sensor or " +
            "system on the highlighted links, when it has one. Needs a restart.");
        KeepOpenedUnit = file.Bind("6. Unit Reference", "Keep the opened unit", true,
            "Unit Reference opened for a unit: the unit stays in the tree, with its class, even when the country, date, " +
            "search or 'Armed only' filters would hide it. Needs a restart.");
        BrowserCountry = file.Bind("6. Unit Reference", "Country", "",
            "Written by the mod: the country's id (as the game names it). Empty = the game's default.");
        BrowserCategory = file.Bind("6. Unit Reference", "Category", "", "Written by the mod: the category's name.");
        BrowserByDate = file.Bind("6. Unit Reference", "Filter by date", false, "Written by the mod.");
        BrowserNameOnly = file.Bind("6. Unit Reference", "Search by name only", false, "Written by the mod.");
        BrowserSearch = file.Bind("6. Unit Reference", "Search text", "", "Written by the mod.");
        DeleteOldLogs = file.Bind("8. Housekeeping", "Delete old game logs", false,
            "At start, delete the game's old session logs in Sea Power_Data/Logs (this run's logs are kept). Off by default.");
        DragTimePanel = file.Bind("B. Mission", "Draggable time panel", true,
            "Mission: the time panel (date / time, speed, play / pause) can be dragged with the mouse by its frame, the speed " +
            "text or the gaps between its buttons; a double click puts it back in the corner. Applies at once.");
        TimePanelPosition = file.Bind("9. Saved by the mod", "Time panel position", "",
            "Written by the mod: left,top of the dragged time panel in pixels. Empty = the game's place.");
        DumpWindows = file.Bind("7. Developer", "Dump window trees", "",
            "Window names (same patterns as 'Window patterns') whose layout is written to BepInEx/SmartWindows.tree.txt " +
            "when they open. Empty = off. Needs a restart.");
        Layouts = file.Bind("9. Saved by the mod", "Layouts", "",
            "Written by the mod: pattern=x,y,width,height[,max];...");
        WindowLooks = file.Bind("9. Saved by the mod", "Window looks", "",
            "Written by the mod: pattern|opacity|scale|outline;... (opacity 0-1, scale 0.5-2, outline 0/1)");
        WindowLevels = file.Bind("9. Saved by the mod", "Window levels", "Tactical Display|-1;World View|-1",
            "Level of the remembered windows: pattern|level;... (-1 below the other windows, 1 above them). " +
            "The Tactical Display and the World View start below the others.");
        DesktopMode = file.Bind("B. Desktop mode", "Enabled", false,
            "Desktop mode: the game's 3D world is drawn into the World View window (the screen behind the windows is cleared by " +
            "the mod), the Tactical Display is an ordinary window, and the game's 'Toggle fullscreen minimap' key switches " +
            "between desktops (each remembers the layout of the remembered windows and whether the World View is open). It " +
            "changes how the game draws its map, so it is off by default. With the game's World View on another monitor the " +
            "map keeps the whole screen and the desktops only arrange the windows over it. Needs a restart.");
        DesktopCount = file.Bind("B. Desktop mode", "Desktops", 2,
            new ConfigDescription("Number of desktops. They start as built-in arrangements (1: World View on top, 2: Tactical " +
                "Display on top, 3: map on the whole screen, 4: 3D view on the whole screen, 5: map and 3D view side by side).",
                new AcceptableValueRange<int>(2, 5)));
        DesktopActive = file.Bind("B. Desktop mode", "Active desktop", 1,
            new ConfigDescription("Written by the mod: the desktop in use.", new AcceptableValueRange<int>(1, 5)));
        for (int i = 1; i <= 5; i++)
            DesktopSlots[i] = file.Bind("B. Desktop mode", "Desktop " + i, "", "Written by the mod: desktop " + i + " while another is active.");
        WorldViewOpen = file.Bind("B. Desktop mode", "World View open", "", "Written by the mod: open / closed.");
        // No default key: other mods use free keys too (VopSound opens its settings with Insert).
        WorldViewKey = file.Bind("B. Desktop mode", "World View key", KeyCode.None,
            "The key that opens and closes the World View window in desktop mode. A key the game already uses (its bindings, also " +
            "the ones you changed) is never taken: it is ignored and the log says so. None = no key. It does nothing while the " +
            "console, a menu or a text box has the keyboard, or while Shift / Ctrl / Alt is held.");
    }

    /// <summary>Writes the config if something changed more than a second ago (or <paramref name="now"/>).</summary>
    public static void Flush(bool now = false)
    {
        if (file == null || changedAt < 0f || (!now && Time.unscaledTime - changedAt < SaveDelay))
            return;
        changedAt = -1f;
        try
        {
            file.Save();
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Smart Windows config not saved: {ex.Message}");
        }
    }
}
