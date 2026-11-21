using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Noesis;
using Noesis.Interactivity;
using SeaPower;
using SeapowerUI;
using UnityEngine;
using ContextMenu = Noesis.ContextMenu;
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut;

/// <summary>
/// The pop-out monitor: a second Noesis view on another Unity display, built the way the game builds its own
/// second-monitor map (<c>SecondDisplayMapHandler</c>: a camera with <c>targetDisplay</c> plus a NoesisView).
/// <para>Its root carries a second instance of the game's own <see cref="WindowHost"/> and the main game view's
/// resources, so a window's content (FormationManagerViewModel, EventLogViewModel, UnitStatusViewModel ...) renders
/// over there with the game's own templates, title bar, dragging and resizing. Popping a window out only moves its
/// content object from the main host's <c>Windows</c> to ours; its close button routes to our host and the main
/// host is patched to know about it (see <see cref="Patches"/>).</para>
/// <para>The screen is created the first time a window pops out in a mission (activating a Unity display can't be
/// undone, so it never happens unasked) and thrown away when the mission unloads.</para>
/// <para>Unity shows a display in a window covering the whole monitor. <see cref="MonitorWindow"/> cuts that window
/// down to the popped-out windows (with a thin frame that is their resize border) and whatever tooltip, context menu
/// or drop-down they have open, so around them the desktop shows through and the windows float freely.</para>
/// </summary>
[DefaultExecutionOrder(10000)]   // LateUpdate after the NoesisViews: the cut-out follows this frame's layout
internal sealed class PopOutScreen : RigHost
{
    internal static PopOutScreen Instance => (PopOutScreen)Rig.Mod.Host;

    // The map and its 3D inset stay on the main screen: they lean on its layout, and the game has its own options
    // to put the map or the world view on another monitor.
    private static readonly HashSet<string> StayHome = new HashSet<string> { SmartWindows.GameWindows.MapViewModel, SmartWindows.GameWindows.WorldViewModel };

    private const float SaveInterval = 3f;
    private const float YieldCheckInterval = 1f;       // while no screen is open: does the game want a monitor back?
    private const int ReleaseAfterFrames = 3;          // a release the screen does get arrives a frame or two late
    private const int ShapeErrorsLogged = 3;           // the cut-out runs every frame: an error is not logged each time
    private const float FrameWidth = 5f;          // resize border around popped-out windows (window units)
    private const string FrameBrushKey = "Brush.Window.Header";

    private static readonly MethodInfo CreateView = AccessTools.Method(typeof(NoesisView), "CreateView", new[] { typeof(FrameworkElement) });
    private static readonly MethodInfo SetCurrentHost = AccessTools.PropertySetter(typeof(WindowHostManager), nameof(WindowHostManager.CurrentHost));

    private const string RootXaml = @"
<local:ScalableUserControl
    xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
    xmlns:i='http://schemas.microsoft.com/expression/2010/interactivity'
    xmlns:local='clr-namespace:SeapowerUI'>
    <i:Interaction.Behaviors>
        <local:WindowHost x:Name='PopOutHost' />
    </i:Interaction.Behaviors>
    <Grid x:Name='Backdrop' Background='#FF0A0F14'>
        <TextBlock x:Name='Hint' HorizontalAlignment='Center' VerticalAlignment='Center' TextAlignment='Center'
                   TextWrapping='Wrap' MaxWidth='640' FontSize='18' Foreground='#66FFFFFF' />
        <Grid>
            <Grid.LayoutTransform>
                <ScaleTransform x:Name='Scale' ScaleX='1' ScaleY='1' />
            </Grid.LayoutTransform>
            <ItemsControl x:Name='Items' ItemsSource='{Binding ElementName=PopOutHost, Path=Windows}'>
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <Grid />
                    </ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <ContentControl Content='{Binding}' HorizontalAlignment='Stretch' VerticalAlignment='Stretch' />
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
            <Canvas x:Name='MenuCanvas' />
        </Grid>
    </Grid>
</local:ScalableUserControl>";

    internal static bool DisplayAvailable => Display.displays.Length > 1;

    /// <summary>
    /// The monitor windows pop out to (Unity display index, -1 = none): the configured one, unless the game draws its
    /// world view or second-screen map there - then, as with "automatic", the first monitor the game leaves alone.
    /// Only when every other monitor is taken do the windows float over the game's own view.
    /// </summary>
    private static int ChooseDisplay()
    {
        int count = Display.displays.Length;
        int wanted = Settings.Monitor.Value - 1;
        if (wanted >= 1 && wanted < count && !GameDrawsOn(wanted))
            return wanted;
        for (int i = 1; i < count; i++)
        {
            if (!GameDrawsOn(i))
                return i;
        }
        if (wanted >= 1 && wanted < count)
            return wanted;
        return count > 1 ? 1 : -1;
    }

    /// <summary>Monitor number (1-based, as in the config) the next pop-out goes to.</summary>
    private int TargetMonitor => (screenDisplay >= 1 ? screenDisplay : ChooseDisplay()) + 1;

    // The mission's main game view and its window host.
    private MainGameView mainView;
    private WindowHost mainHost;

    // The pop-out screen, while open.
    private int screenDisplay = -1;               // its Unity display index
    private MainGameView builtFor;
    private GameObject screen;
    private UnityEngine.Camera screenCamera;
    private ScalableUserControl root;
    private WindowHost host;
    private Grid backdrop;
    private TextBlock hint;
    private ScaleTransform scale;
    private float appliedScale;
    private bool shared;                          // the game draws its own map or world view on that monitor too
    private SolidColorBrush backdropBrush;
    private NoesisView screenView;
    private ItemsControl items;

    // See-through: only the windows (and their open popups) of the screen show on the monitor.
    private MonitorWindow monitor;
    private bool seeThrough;
    private readonly ScreenCutOut cutOut = new ScreenCutOut();
    private static bool overlayHooks;

    private UnitMenuCopy unitMenu;

    // A press on the screen whose release went to another program (the pointer had left the see-through cut-out).
    private bool pressed;
    private int releasedFrames;
    private Vector2 pointer;
    private int shapeErrors;

    private readonly Queue<Action> deferred = new Queue<Action>();
    private float nextSave, nextYieldCheck;

    // Stage two of the patches (see Patches) goes in once the game's first window host exists; the Smart Windows hooks follow.
    private Harmony smartHarmony;

    // Until the game's first window host exists.
    protected override float HookPollSeconds => 0.5f;

    protected override void Tick()
    {
        SmartWindows.SmartFeatures.Update();
        if (smartHarmony == null && Rig.Hooks.IsActive(Patches.WindowsFeature))
            ApplySmartHooks();
        while (deferred.Count > 0)
        {
            try { deferred.Dequeue()(); }
            catch (Exception ex) { Rig.Log.Error($"{ex}"); }
        }
        if (host != null)
        {
            try
            {
                unitMenu?.Return();
                unitMenu?.CloseElsewhere(screenDisplay);
                UpdateScreen(force: false);
                ReleaseLostClick();
            }
            catch (Exception ex) { Rig.Log.Error($"screen update: {ex.Message}"); }
        }
        else if (Time.unscaledTime >= nextYieldCheck)
        {
            nextYieldCheck = Time.unscaledTime + YieldCheckInterval;
            MonitorWindow.YieldTo(GameDrawsOn);
        }
        if (Time.unscaledTime >= nextSave)
        {
            nextSave = Time.unscaledTime + SaveInterval;
            Layout.Save();
        }
    }

    private void LateUpdate()
    {
        if (host == null || !seeThrough)
            return;
        try
        {
            monitor.Shape(cutOut.Collect(pressed), screenCamera.pixelWidth, screenCamera.pixelHeight);
        }
        catch (Exception ex)
        {
            if (shapeErrors++ < ShapeErrorsLogged)
                Rig.Log.Error($"see-through: {ex}");
        }
    }

    /// <summary>
    /// The game closes (RigHost calls this from OnApplicationQuit, before any OnDestroy): the pop-out screen goes now, while
    /// every Noesis view is still alive. The game's GameMain.OnDestroy (the mission-end hook) used to tear the screen down later,
    /// in the middle of the shutdown, when Noesis was being taken apart already - an access violation in Noesis.dll on quit
    /// (Keel report #6).
    /// </summary>
    protected override void Teardown()
    {
        Layout.Save();
        SmartWindows.SmartFeatures.Save();
        if (host != null)
            TearDownScreen();
        mainView = null;
        mainHost = null;
    }

    private void ApplySmartHooks()
    {
        smartHarmony = new Harmony(Plugin.Guid);
        SmartWindows.SmartFeatures.Apply(smartHarmony);

        // Listed here rather than at plugin load, where Unity reported only the game's monitor.
        Display[] displays = Display.displays;
        for (int i = 0; i < displays.Length; i++)
            Rig.Log.Info($"Monitor {i + 1}: {displays[i].systemWidth}x{displays[i].systemHeight}" + (i == 0 ? " (game)" : ""));
        Rig.Log.Info(DisplayAvailable
            ? $"Window hooks in place. Pop-out monitor: {ChooseDisplay() + 1}"
            : "Window hooks in place. Only one monitor - the pop-out buttons stay hidden.");
    }

    protected override void OnMissionReady()
    {
        try
        {
            // A screen left over from another mission goes; one opened while this mission's UI was loading stays.
            if (host != null && !ReferenceEquals(builtFor, MainGameView.Instance))
                TearDownScreen();
            if (MainHost == null)
            {
                Rig.Log.Warn("The main screen's window host wasn't found - pop-out is off for this mission.");
                return;
            }
            // Windows the game opened while the mission UI was loading (the formation manager, if it starts open)
            // went to the main screen; the ones remembered as popped out move over once the mission runs.
            deferred.Enqueue(() =>
            {
                if (!DisplayAvailable || MainHost == null)
                    return;
                foreach (object content in MainHost.Windows.Where(w => Layout.IsPopped(Kind(w)) && !StayHome.Contains(Kind(w))).ToList())
                    Move(content, toPopOut: true, remember: false);
            });
        }
        catch (Exception ex)
        {
            Rig.Log.Error($"mission start: {ex}");
        }
    }

    protected override void OnMissionEnded()
    {
        try
        {
            Layout.Save();
            TearDownScreen();
            mainView = null;
            mainHost = null;
        }
        catch (Exception ex)
        {
            Rig.Log.Error($"mission end: {ex}");
        }
    }

    /// <summary>The main game view's own window host; re-resolved whenever the game builds a new main view.</summary>
    private WindowHost MainHost
    {
        get
        {
            MainGameView current = MainGameView.Instance;
            if (!ReferenceEquals(current, mainView))
            {
                mainView = current;
                mainHost = current == null ? null : FindHost(current);
            }
            return mainHost;
        }
    }

    /// <summary>A window is opening in <paramref name="opening"/>. False = we took it (the game skips its own code).</summary>
    internal bool HostOpening(WindowHost opening, object content)
    {
        try
        {
            if (content == null || ReferenceEquals(opening, host) || !ReferenceEquals(opening, MainHost))
                return true;
            if (host != null && host.Windows.Contains(content))
                return false;                         // already open on the pop-out screen
            string kind = Kind(content);
            if (!Layout.IsPopped(kind) || StayHome.Contains(kind) || !Open())
                return true;
            CopyNewResources();
            AddTo(host, content);
            WindowHostCommands.CloseWindowCommand.UpdateCanExecute();
            Rig.Log.Debug($"{kind} opened on the pop-out screen");
            return false;
        }
        catch (Exception ex)
        {
            Rig.Log.Error($"open window: {ex}");
            return true;
        }
    }

    /// <summary>The main host is asked whether it can close <paramref name="content"/>: yes, if it's popped out.</summary>
    internal bool HoldsFor(WindowHost asked, object content) =>
        content != null && host != null && ReferenceEquals(asked, MainHost) && host.Windows.Contains(content);

    /// <summary>The main host is told to close <paramref name="content"/> (its bottom-bar toggle was clicked).</summary>
    internal bool HostClosing(WindowHost closing, object content)
    {
        try
        {
            if (!HoldsFor(closing, content))
                return true;
            host.Windows.Remove(content);
            (content as IDisposeOnClose)?.Dispose();
            WindowHostCommands.CloseWindowCommand.UpdateCanExecute();
            return false;
        }
        catch (Exception ex)
        {
            Rig.Log.Error($"close window: {ex}");
            return true;
        }
    }

    /// <summary>True unless the window's content lives on the pop-out screen (where <see cref="Layout"/> places it).</summary>
    internal bool OnMainScreen(GameWindow window) => host == null || !host.Windows.Contains(window.DataContext);

    /// <summary>
    /// A window finished loading on either screen. Windows of the main host get a pop-out button, windows on the
    /// pop-out screen a pop-in button, their remembered place and size, and no size limit.
    /// </summary>
    internal void Decorate(GameWindow window)
    {
        try
        {
            CornerResize.Attach(window);
            object content = window.DataContext;
            if (content == null || !DisplayAvailable || StayHome.Contains(Kind(content)) || !TopLevel(window))
                return;
            // A window's content sits in exactly one of the two hosts (Move takes it out of one before adding it to
            // the other), so membership says which screen the window is on.
            bool popped = host != null && host.Windows.Contains(content);
            if (!popped && (MainHost == null || !MainHost.Windows.Contains(content)))
                return;                               // not one of the host's windows (a dialog of some other view)

            AddButton(window, popped);
            if (popped)
            {
                Place(window, Kind(content));
                Track(window);
                // A loading window hands the game's window commands its own keyboard; the main screen's is meant.
                if (mainView != null)
                    GameWindow.CloseWindowCommand.Keyboard = mainView.Keyboard;
            }
        }
        catch (Exception ex)
        {
            Rig.Log.Error($"window decoration: {ex}");
        }
    }

    // A box with an arrow leaving it (pop out) or coming back into it (pop in).
    private const string OutIcon = SmartWindows.TitleMenu.Mark;
    private const string InIcon = "M1,4 L1,11 L8,11 L8,8 M11,1 L5,7 M5,3 L5,7 L9,7";

    private void AddButton(GameWindow window, bool popped)
    {
        string label = popped ? "Back to the main screen" : $"Pop out to monitor {TargetMonitor}";
        SmartWindows.TitleMenu.Set(window, "popout", 0, label, popped ? InIcon : OutIcon, anchor =>
        {
            object content = window.DataContext;
            // Not while the click is still being routed through the window that's about to be unloaded.
            deferred.Enqueue(() => Move(content, toPopOut: !popped, remember: true));
        });
    }

    /// <summary>Moves a window's content between the main host and ours; the window is rebuilt on the other screen.</summary>
    private void Move(object content, bool toPopOut, bool remember)
    {
        if (content == null)
            return;
        if (toPopOut && !Open())
            return;
        WindowHost from = toPopOut ? MainHost : host;
        WindowHost to = toPopOut ? host : MainHost;
        if (from == null || to == null)
            return;
        int index = from.Windows.IndexOf(content);
        if (index < 0)
            return;
        from.Windows.RemoveAt(index);
        if (toPopOut)
            CopyNewResources();
        AddTo(to, content);
        string kind = Kind(content);
        if (remember)
            Layout.SetPopped(kind, toPopOut);
        WindowHostCommands.CloseWindowCommand.UpdateCanExecute();
        Rig.Log.Info(toPopOut ? $"{kind} popped out to monitor {screenDisplay + 1}" : $"{kind} back on the main screen");
    }

    private static void AddTo(WindowHost target, object content)
    {
        if (content is IHostedAtBack)
            target.Windows.Insert(0, content);
        else
            target.Windows.Add(content);
    }

    private void Place(GameWindow window, string kind)
    {
        if (Settings.UnlimitedSize.Value)
        {
            window.MaxWidth = float.PositiveInfinity;
            window.MaxHeight = float.PositiveInfinity;
        }
        // The resize border lies outside the window; on a see-through monitor it shows, as a thin frame.
        if (window.ResizeHandleWidth > FrameWidth)
            window.ResizeHandleWidth = FrameWidth;
        if (Layout.TryGetPlacement(kind, out SmartWindows.SavedLayout p))
        {
            // Stops windows anchored to the bottom right (unit status) from snapping back to their corner.
            GameMouse.MarkUserPositioned?.Invoke(window, null);
            if (!float.IsNaN(p.X)) window.X = p.X;
            if (!float.IsNaN(p.Y)) window.Y = p.Y;
            // A size is only remembered once it was resized, also for windows that size themselves (formation
            // manager): then it is the player's.
            if (!float.IsNaN(p.W)) window.Width = p.W;
            if (!float.IsNaN(p.H)) window.Height = p.H;
        }
        window.PositionChanged += (sender, args) => Remember(window);
        window.SizeChanged += (sender, args) => Remember(window);
    }

    private static void Remember(GameWindow window)
    {
        object content = window.DataContext;
        if (content == null || window.Maximized)
            return;
        Layout.SetPlacement(Kind(content), new SmartWindows.SavedLayout
        {
            X = window.X,
            Y = window.Y,
            W = float.IsNaN(window.Width) ? float.NaN : window.Width,
            H = float.IsNaN(window.Height) ? float.NaN : window.Height,
        });
    }

    private bool Open()
    {
        if (host != null)
            return true;
        if (Mod.Quitting)
            return false;
        int index = ChooseDisplay();
        if (index < 1 || MainHost == null)
            return false;
        try
        {
            Display display = ActivateDisplay(index);
            BuildRoot();
            BuildView(index);
            // Our host and view grabbed the game's window commands for their own keyboard; give it back.
            WindowHostCommands.OpenWindowCommand.Keyboard = mainView.Keyboard;
            WindowHostCommands.CloseWindowCommand.Keyboard = mainView.Keyboard;
            UpdateScreen(force: true);
            LogOpened(index, display);
            return true;
        }
        catch (Exception ex)
        {
            Rig.Log.Error($"Could not open the pop-out screen: {ex}");
            TearDownScreen();
            return false;
        }
    }

    /// <summary>Unity shows the display as a window over the whole monitor; activating it cannot be undone.</summary>
    private Display ActivateDisplay(int index)
    {
        screenDisplay = index;
        Display display = Display.displays[index];
        monitor = MonitorWindow.For(index);
        if (!display.active)
        {
            monitor.BeforeActivate();
            display.Activate();
        }
        SecondaryDisplayBlanker.Register(index);
        return display;
    }

    /// <summary>The screen's XAML root: our window host, the backdrop, the scaled layer of the windows and the unit menu.</summary>
    private void BuildRoot()
    {
        root = (ScalableUserControl)Noesis.GUI.ParseXaml(RootXaml);
        host = FindHost(root);
        if (host == null)
            throw new InvalidOperationException("pop-out window host missing");
        // WindowHost's constructor makes itself the game's "current" host; the game means the main one by that.
        SetCurrentHost?.Invoke(Singleton<WindowHostManager>.Instance, new object[] { mainHost });
        backdrop = (Grid)root.FindName("Backdrop");
        hint = (TextBlock)root.FindName("Hint");
        scale = (ScaleTransform)root.FindName("Scale");
        items = (ItemsControl)root.FindName("Items");
        root.PreviewMouseLeftButtonDown += (sender, args) => pressed = true;
        root.PreviewMouseLeftButtonUp += (sender, args) => pressed = false;
        root.PreviewMouseRightButtonDown += (sender, args) => unitMenu?.Lend();
        root.PreviewMouseRightButtonUp += (sender, args) => unitMenu?.Lend();
        root.GotMouseCapture += OnCapture;
        HookOverlays();
        backdropBrush = backdrop.Background as SolidColorBrush;
        hint.Text = "Pop-out screen\n\nThe pop-out button in a window's title bar moves it here, " +
                    "the same button on this screen sends it back.";
        root.Resources = ScreenResources.Flatten(mainView.Resources);
        root.DataContext = mainView.DataContext;
        builtFor = mainView;
        unitMenu = UnitMenuCopy.Build(mainView, (Noesis.Canvas)root.FindName("MenuCanvas"));
    }

    /// <summary>
    /// A camera on the display and a NoesisView of the root, built the way the game builds its own second-monitor map
    /// (<c>SecondDisplayMapHandler</c>).
    /// </summary>
    private void BuildView(int index)
    {
        screen = new GameObject("SeaPowerPopOut screen");
        UnityEngine.Object.DontDestroyOnLoad(screen);
        screenCamera = screen.AddComponent<UnityEngine.Camera>();
        screenCamera.cullingMask = 0;
        screenCamera.depth = 90f;
        screenCamera.targetDisplay = index;
        screenCamera.backgroundColor = UnityEngine.Color.black;
        screenCamera.useOcclusionCulling = false;
        screenCamera.allowHDR = false;
        screenCamera.allowMSAA = false;
        var view = screen.AddComponent<NoesisView>();
        view.UseRealTimeClock = true;             // windows keep animating while the game is paused
        if (DM._noesisView != null)
            view.DPIScale = DM._noesisView.DPIScale;
        CreateView.Invoke(view, new object[] { root });
        screenView = view;
    }

    private void LogOpened(int index, Display display)
    {
        if (Screen.fullScreenMode == FullScreenMode.ExclusiveFullScreen)
            Rig.Log.Warn("The game runs in exclusive fullscreen: with a second monitor in use the picture of the world view " +
                        "can freeze there (Unity). Borderless windowed (Options > Graphics) avoids it.");
        Rig.Log.Info($"Pop-out screen open on monitor {index + 1} ({display.renderingWidth}x{display.renderingHeight})" +
                    (index + 1 != Settings.Monitor.Value && GameDrawsOn(Settings.Monitor.Value - 1)
                        ? $" - monitor {Settings.Monitor.Value} shows the game's world view or map" : "") +
                    (shared ? ", over the game's own view (no free monitor)" : ""));
    }

    private void UpdateScreen(bool force)
    {
        ApplyScale(force);
        ApplySeeThrough(force);
        hint.Visibility = !shared && !seeThrough && host.Windows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        monitor.KeepOnTop(seeThrough && Settings.OnTop.Value);
        if (GameMouse.OnDisplay(screenDisplay, out Vector3 p))
            pointer = new Vector2(p.x, screenCamera.pixelHeight - p.y);
        if (mainView != null && !ReferenceEquals(root.DataContext, mainView.DataContext))
            root.DataContext = mainView.DataContext;
    }

    /// <summary>
    /// The game scales its UI by the screen's height. Same here, by the short side, so a portrait monitor gets the size a
    /// landscape one of that resolution would (by its long side the windows would come out huge).
    /// </summary>
    private void ApplyScale(bool force)
    {
        int shortSide = Mathf.Max(1, Mathf.Min(screenCamera.pixelWidth, screenCamera.pixelHeight));
        float s = SmartWindows.HostArea.UiScale(shortSide) * Settings.Scale.Value;
        if (!force && Mathf.Abs(s - appliedScale) <= 0.001f)
            return;
        scale.ScaleX = s;
        scale.ScaleY = s;
        appliedScale = s;
    }

    /// <summary>
    /// Sharing the monitor with the game's own map / world view: the windows are drawn over it. On a monitor of our own
    /// made see-through, the backdrop only shows in the frame around each window; else it covers the monitor.
    /// </summary>
    private void ApplySeeThrough(bool force)
    {
        bool gameThere = GameDrawsOn(screenDisplay);
        if (!gameThere && Settings.SeeThrough.Value)
            monitor.Search(Display.displays[screenDisplay]);
        bool see = !gameThere && Settings.SeeThrough.Value && monitor.Found;
        if (!force && gameThere == shared && see == seeThrough)
            return;
        shared = gameThere;
        seeThrough = see;
        screenCamera.clearFlags = shared ? CameraClearFlags.Depth : CameraClearFlags.SolidColor;
        if (shared)
            backdrop.Background = null;
        else if (seeThrough)
            backdrop.SetResourceReference(Panel.BackgroundProperty, FrameBrushKey);
        else
            backdrop.Background = backdropBrush;
        if (!seeThrough)
            monitor.Unshape();
    }

    /// <summary>The game's second-monitor map or detached world view uses that display too.</summary>
    private static bool GameDrawsOn(int index)
    {
        if (!Singleton<OptionsManager>.InstanceExists())
            return false;
        OptionsManager o = Singleton<OptionsManager>.Instance;
        int mapDisplay = o.WorldViewMonitor > 0 ? o.WorldViewMonitor : o.SecondMonitorIndex;
        bool map = (o.SecondMonitorEnabled || o.WorldViewMonitor > 0) && mapDisplay == index;
        return map || o.WorldViewMonitor == index;
    }

    /// <summary>On the pop-out monitor (over one of our windows, when the game draws there too) the mouse belongs to the UI.</summary>
    internal void ClaimMouse(MouseControlState state)
    {
        if (screen == null)
            return;
        try
        {
            if (!GameMouse.OnDisplay(screenDisplay, out _) || (shared && !root.IsMouseOver))
                return;
            GameMouse.ClaimForUi(state);
        }
        catch (Exception ex)
        {
            Rig.Log.Debug($"mouse: {ex.Message}");
        }
    }

    private void TearDownScreen()
    {
        deferred.Clear();
        try
        {
            unitMenu?.Return();
            host?.Windows.Clear();
            monitor?.Release();
        }
        catch (Exception ex)
        {
            Rig.Log.Debug($"teardown: {ex.Message}");
        }
        if (screen != null)
            Destroy(screen);
        unitMenu = null;
        monitor = null;
        seeThrough = false;
        screenView = null;
        items = null;
        cutOut.Clear();
        pressed = false;
        builtFor = null;
        screenDisplay = -1;
        screen = null;
        screenCamera = null;
        root = null;
        host = null;
        backdrop = null;
        hint = null;
        scale = null;
        appliedScale = 0f;
        WindowHostCommands.CloseWindowCommand.UpdateCanExecute();
    }

    /// <summary>A window on the pop-out screen: part of the cut-out, and it snaps while dragged.</summary>
    private void Track(GameWindow window)
    {
        cutOut.Track(window);
        SmartWindows.SnapController.OnPopOutMonitor(window, cutOut.Windows, () => items);
    }

    /// <summary>
    /// Tooltips and context menus open in popups of their own, which may reach past the window: while open they are
    /// part of the cut-out. Class handlers are for good, so registered once per game.
    /// </summary>
    private static void HookOverlays()
    {
        if (overlayHooks)
            return;
        overlayHooks = true;
        try
        {
            var opened = new RoutedEventHandler((sender, args) => Instance?.OverlayOpened(sender as UIElement));
            EventManager.RegisterClassHandler(typeof(ToolTip), ToolTip.OpenedEvent, opened, true);
            EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent, opened, true);
        }
        catch (Exception ex)
        {
            Rig.Log.Warn($"Tooltips and context menus may be cut off at the window edge on the pop-out monitor: {ex.Message}");
        }
    }

    private void OverlayOpened(UIElement overlay)
    {
        try
        {
            if (overlay == null || root == null)
                return;
            UIElement target = overlay is ToolTip tip ? tip.PlacementTarget
                : overlay is ContextMenu menu ? menu.PlacementTarget : null;
            if (target != null ? root.IsAncestorOf(target) : ReferenceEquals(overlay.View, root.View))
                cutOut.TrackOverlay(overlay);
        }
        catch (Exception ex)
        {
            Rig.Log.Debug($"overlay: {ex.Message}");
        }
    }

    /// <summary>A popup of the mod's own (settings, look menu) opened: on the pop-out monitor it is part of the cut-out.</summary>
    internal void PopupOpened(Popup popup, UIElement anchor)
    {
        if (popup != null && anchor != null && root != null && root.IsAncestorOf(anchor))
            cutOut.TrackOverlay(popup);
    }

    // A drop-down list holds the mouse while open; its popup is part of its template.
    private void OnCapture(object sender, MouseEventArgs e)
    {
        if (e.Source is ComboBox box && SmartWindows.VisualSearch.First<Popup>(box, budget: 200) is Popup popup)
            cutOut.TrackOverlay(popup);
    }

    /// <summary>
    /// A press on the screen released over another program (the pointer ran out of the cut-out faster than the
    /// window followed, or onto another monitor) never reaches the screen, and the window would stick to the mouse.
    /// </summary>
    private void ReleaseLostClick()
    {
        if (!pressed || screenView == null || Native.LeftButtonHeld())
        {
            releasedFrames = 0;
            return;
        }
        if (++releasedFrames < ReleaseAfterFrames)
            return;
        pressed = false;
        releasedFrames = 0;
        screenView.MouseButtonUp((int)pointer.x, (int)pointer.y, MouseButton.Left);
        Rig.Log.Debug("Mouse button released outside the pop-out windows");
    }

    private static string Kind(object content) => content?.GetType().Name ?? "";

    private static WindowHost FindHost(DependencyObject element)
    {
        BehaviorCollection behaviors = Interaction.GetBehaviors(element);
        if (behaviors != null)
        {
            for (int i = 0; i < behaviors.Count; i++)
            {
                if (behaviors[i] is WindowHost found)
                    return found;
            }
        }
        return null;
    }

    private void CopyNewResources()
    {
        if (root?.Resources != null && mainView != null)
            ScreenResources.CopyMissing(mainView.Resources, root.Resources);
    }

    /// <summary>Not a window inside another window's content.</summary>
    private static bool TopLevel(GameWindow window)
    {
        for (Visual v = VisualTreeHelper.GetParent(window) as Visual; v != null; v = VisualTreeHelper.GetParent(v) as Visual)
        {
            if (v is GameWindow)
                return false;
        }
        return true;
    }
}
