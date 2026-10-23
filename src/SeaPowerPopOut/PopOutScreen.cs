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
using GameWindow = SeapowerUI.Window;

namespace SeaPowerPopOut;

/// <summary>
/// The pop-out monitor: a second Noesis view on another Unity display, built the way the game builds its own
/// second-monitor map (<c>SecondDisplayMapHandler</c>: a camera with <c>targetDisplay</c> plus a NoesisView).
/// <para>Its root carries a second instance of the game's own <see cref="WindowHost"/> and the main game view's
/// resources, so a window's content (FormationManagerViewModel, EventLogViewModel, UnitStatusViewModel …) renders
/// over there with the game's own templates, title bar, dragging and resizing. Popping a window out only moves its
/// content object from the main host's <c>Windows</c> to ours; its close button routes to our host and the main
/// host is patched to know about it (see <see cref="Patches"/>).</para>
/// <para>The screen is created the first time a window pops out in a mission (activating a Unity display can't be
/// undone, so it never happens unasked) and thrown away when the mission unloads.</para>
/// </summary>
internal sealed class PopOutScreen : MonoBehaviour
{
    internal static PopOutScreen Instance { get; private set; }

    // The map and its 3D inset stay on the main screen: they lean on its layout, and the game has its own options
    // to put the map or the world view on another monitor.
    private static readonly HashSet<string> StayHome = new HashSet<string> { "MapViewModel", "WorldViewWindowViewModel" };

    private const string ButtonName = "PopOutButton";
    private const float SaveInterval = 3f;
    private const float DesignHeight = 1080f;

    private static readonly MethodInfo CreateView = AccessTools.Method(typeof(NoesisView), "CreateView", new[] { typeof(FrameworkElement) });
    private static readonly MethodInfo MarkUserPositioned = AccessTools.Method(typeof(GameWindow), "MarkUserPositioned");
    private static readonly MethodInfo SetCurrentHost = AccessTools.PropertySetter(typeof(WindowHostManager), nameof(WindowHostManager.CurrentHost));
    private static readonly AccessTools.FieldRef<MouseControlState, MouseControlState.State> MouseState =
        AccessTools.FieldRefAccess<MouseControlState, MouseControlState.State>("_currentState");

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
            <ItemsControl ItemsSource='{Binding ElementName=PopOutHost, Path=Windows}'>
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
        </Grid>
    </Grid>
</local:ScalableUserControl>";

    // Box with an arrow leaving it (pop out) or coming back into it (pop in), 12×12.
    private const string OutIcon = "M1,4 L1,11 L8,11 L8,8 M5,7 L11,1 M7,1 L11,1 L11,5";
    private const string InIcon = "M1,4 L1,11 L8,11 L8,8 M11,1 L5,7 M5,3 L5,7 L9,7";

    private static int DisplayIndex => Settings.Monitor.Value - 1;
    internal static bool DisplayAvailable => DisplayIndex >= 1 && DisplayIndex < Display.displays.Length;

    // The mission's main game view and its window host.
    private MainGameView mainView;
    private WindowHost mainHost;

    // The pop-out screen, while open.
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

    private readonly Queue<Action> deferred = new Queue<Action>();
    private float nextSave;

    private void Awake() => Instance = this;

    private void Update()
    {
        while (deferred.Count > 0)
        {
            try { deferred.Dequeue()(); }
            catch (Exception ex) { ModLog.Error($"{ex}"); }
        }
        if (host != null)
        {
            try { UpdateScreen(force: false); }
            catch (Exception ex) { ModLog.Error($"screen update: {ex.Message}"); }
        }
        if (Time.unscaledTime >= nextSave)
        {
            nextSave = Time.unscaledTime + SaveInterval;
            Layout.Save();
        }
    }

    private void OnApplicationQuit() => Layout.Save();

    // ---- mission lifecycle -------------------------------------------------------------------------------------

    internal void MissionReady()
    {
        try
        {
            // A screen left over from another mission goes; one opened while this mission's UI was loading stays.
            if (host != null && !ReferenceEquals(builtFor, MainGameView.Instance))
                Teardown();
            if (MainHost == null)
            {
                ModLog.Warn("The main screen's window host wasn't found - pop-out is off for this mission.");
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
            ModLog.Error($"mission start: {ex}");
        }
    }

    internal void MissionEnded()
    {
        try
        {
            Layout.Save();
            Teardown();
            mainView = null;
            mainHost = null;
        }
        catch (Exception ex)
        {
            ModLog.Error($"mission end: {ex}");
        }
    }

    // ---- the main screen's window host (patched) ---------------------------------------------------------------

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
            AddTo(host, content);
            WindowHostCommands.CloseWindowCommand.UpdateCanExecute();
            ModLog.Debug($"{kind} opened on the pop-out screen");
            return false;
        }
        catch (Exception ex)
        {
            ModLog.Error($"open window: {ex}");
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
            ModLog.Error($"close window: {ex}");
            return true;
        }
    }

    // ---- windows -----------------------------------------------------------------------------------------------

    /// <summary>
    /// A window finished loading on either screen. Windows of the main host get a pop-out button, windows on the
    /// pop-out screen a pop-in button, their remembered place and size, and no size limit.
    /// </summary>
    internal void Decorate(GameWindow window)
    {
        try
        {
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
                // A loading window hands the game's window commands its own keyboard; the main screen's is meant.
                if (mainView != null)
                    GameWindow.CloseWindowCommand.Keyboard = mainView.Keyboard;
            }
        }
        catch (Exception ex)
        {
            ModLog.Error($"window decoration: {ex}");
        }
    }

    private void AddButton(GameWindow window, bool popped)
    {
        Panel panel = FindButtonsPanel(window);
        if (panel == null)
        {
            ModLog.Debug($"{Kind(window.DataContext)}: no title bar buttons found");
            return;
        }
        for (int i = 0; i < panel.Children.Count; i++)
        {
            if (panel.Children[i] is FrameworkElement existing && existing.Name == ButtonName)
                return;
        }

        var button = (Button)Noesis.GUI.ParseXaml(
            "<Button xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Width='20' Focusable='False' " +
            "Padding='0' Margin='0'><Path Width='12' Height='12' StrokeThickness='1.5' HorizontalAlignment='Center' " +
            "VerticalAlignment='Center' Data='" + (popped ? InIcon : OutIcon) + "' " +
            "Stroke='{Binding Foreground, RelativeSource={RelativeSource AncestorType=Button}}' /></Button>");
        button.Name = ButtonName;
        button.ToolTip = popped ? "Back to the main screen" : $"Pop out to monitor {Settings.Monitor.Value}";
        button.Click += (sender, args) =>
        {
            args.Handled = true;
            object content = window.DataContext;
            // Not while the click is still being routed through the window that's about to be unloaded.
            deferred.Enqueue(() => Move(content, toPopOut: !popped, remember: true));
        };
        panel.Children.Insert(0, button);
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
        AddTo(to, content);
        string kind = Kind(content);
        if (remember)
            Layout.SetPopped(kind, toPopOut);
        WindowHostCommands.CloseWindowCommand.UpdateCanExecute();
        ModLog.Info(toPopOut ? $"{kind} popped out to monitor {Settings.Monitor.Value}" : $"{kind} back on the main screen");
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
        if (Layout.TryGetPlacement(kind, out Layout.Placement p))
        {
            // Stops windows anchored to the bottom right (unit status) from snapping back to their corner.
            MarkUserPositioned?.Invoke(window, null);
            if (!float.IsNaN(p.X)) window.X = p.X;
            if (!float.IsNaN(p.Y)) window.Y = p.Y;
            if (!float.IsNaN(p.Width) && !float.IsNaN(window.Width)) window.Width = p.Width;
            if (!float.IsNaN(p.Height) && !float.IsNaN(window.Height)) window.Height = p.Height;
        }
        window.PositionChanged += (sender, args) => Remember(window);
        window.SizeChanged += (sender, args) => Remember(window);
    }

    private static void Remember(GameWindow window)
    {
        object content = window.DataContext;
        if (content == null || window.Maximized)
            return;
        Layout.SetPlacement(Kind(content), new Layout.Placement
        {
            X = window.X,
            Y = window.Y,
            Width = float.IsNaN(window.Width) ? float.NaN : window.Width,
            Height = float.IsNaN(window.Height) ? float.NaN : window.Height,
        });
    }

    // ---- the screen --------------------------------------------------------------------------------------------

    private bool Open()
    {
        if (host != null)
            return true;
        if (!DisplayAvailable || MainHost == null)
            return false;
        try
        {
            Display display = Display.displays[DisplayIndex];
            if (!display.active)
                display.Activate();
            SecondaryDisplayBlanker.Register(DisplayIndex);

            root = (ScalableUserControl)Noesis.GUI.ParseXaml(RootXaml);
            host = FindHost(root);
            if (host == null)
                throw new InvalidOperationException("pop-out window host missing");
            // WindowHost's constructor makes itself the game's "current" host; the game means the main one by that.
            SetCurrentHost?.Invoke(Singleton<WindowHostManager>.Instance, new object[] { mainHost });
            backdrop = (Grid)root.FindName("Backdrop");
            hint = (TextBlock)root.FindName("Hint");
            scale = (ScaleTransform)root.FindName("Scale");
            backdropBrush = backdrop.Background as SolidColorBrush;
            hint.Text = "Pop-out screen\n\nThe pop-out button in a window's title bar moves it here, " +
                        "the same button on this screen sends it back.";
            // The window templates (and everything they use) live in the main game view's resources. A flat copy,
            // so the main view's own dictionaries never get a second owner.
            root.Resources = Flatten(mainView.Resources);
            root.DataContext = mainView.DataContext;
            builtFor = mainView;

            screen = new GameObject("SeaPowerPopOut screen");
            UnityEngine.Object.DontDestroyOnLoad(screen);
            screenCamera = screen.AddComponent<UnityEngine.Camera>();
            screenCamera.cullingMask = 0;
            screenCamera.depth = 90f;
            screenCamera.targetDisplay = DisplayIndex;
            screenCamera.backgroundColor = UnityEngine.Color.black;
            screenCamera.useOcclusionCulling = false;
            screenCamera.allowHDR = false;
            screenCamera.allowMSAA = false;
            var view = screen.AddComponent<NoesisView>();
            view.UseRealTimeClock = true;             // windows keep animating while the game is paused
            if (DM._noesisView != null)
                view.DPIScale = DM._noesisView.DPIScale;
            CreateView.Invoke(view, new object[] { root });

            // Our host and view grabbed the game's window commands for their own keyboard; give it back.
            WindowHostCommands.OpenWindowCommand.Keyboard = mainView.Keyboard;
            WindowHostCommands.CloseWindowCommand.Keyboard = mainView.Keyboard;

            UpdateScreen(force: true);
            ModLog.Info($"Pop-out screen open on monitor {Settings.Monitor.Value} " +
                        $"({display.renderingWidth}x{display.renderingHeight})");
            return true;
        }
        catch (Exception ex)
        {
            ModLog.Error($"Could not open the pop-out screen: {ex}");
            Teardown();
            return false;
        }
    }

    private void UpdateScreen(bool force)
    {
        float ui = Singleton<OptionsManager>.InstanceExists() ? Singleton<OptionsManager>.Instance.UIScale : 1f;
        float s = ui * Mathf.Max(1, screenCamera.pixelHeight) / DesignHeight * Settings.Scale.Value;
        if (force || Mathf.Abs(s - appliedScale) > 0.001f)
        {
            scale.ScaleX = s;
            scale.ScaleY = s;
            appliedScale = s;
        }

        bool gameThere = GameDrawsOn(DisplayIndex);
        if (force || gameThere != shared)
        {
            // Sharing the monitor with the game's own map / world view: draw the windows over it, see-through.
            shared = gameThere;
            screenCamera.clearFlags = shared ? CameraClearFlags.Depth : CameraClearFlags.SolidColor;
            backdrop.Background = shared ? null : backdropBrush;
        }
        hint.Visibility = !shared && host.Windows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (mainView != null && !ReferenceEquals(root.DataContext, mainView.DataContext))
            root.DataContext = mainView.DataContext;
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

    /// <summary>
    /// Runs after the game decided where the mouse is. On the pop-out monitor (over one of our windows, when the
    /// game draws there too) the mouse belongs to the UI, so clicks don't reach the world or the map.
    /// </summary>
    internal void ClaimMouse(MouseControlState state)
    {
        if (screen == null)
            return;
        try
        {
            Vector3 p = Display.RelativeMouseAt(UnityEngine.Input.mousePosition);
            if (p == Vector3.zero || (int)p.z != DisplayIndex)
                return;
            if (shared && !root.IsMouseOver)
                return;
            MouseState(state) = MouseControlState.State.UI;
            if (Singleton<CameraManager>.InstanceExists())
                Singleton<CameraManager>.Instance.setMouseInteractionFlag(false);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"mouse: {ex.Message}");
        }
    }

    private void Teardown()
    {
        deferred.Clear();
        try
        {
            host?.Windows.Clear();
        }
        catch (Exception ex)
        {
            ModLog.Debug($"teardown: {ex.Message}");
        }
        if (screen != null)
            Destroy(screen);
        builtFor = null;
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

    // ---- helpers -----------------------------------------------------------------------------------------------

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

    /// <summary>Every entry of a dictionary and its merged dictionaries, in lookup precedence, in one new dictionary.</summary>
    private static ResourceDictionary Flatten(ResourceDictionary source)
    {
        var flat = new ResourceDictionary();
        CopyInto(source, flat, 0);
        ModLog.Debug($"{flat.Count} resources copied for the pop-out screen");
        return flat;
    }

    private static void CopyInto(ResourceDictionary from, ResourceDictionary to, int depth)
    {
        if (from == null || depth > 16)
            return;
        ResourceDictionaryCollection merged = from.MergedDictionaries;
        for (int i = 0; merged != null && i < merged.Count; i++)
            CopyInto(merged[i], to, depth + 1);   // later merged dictionaries win over earlier ones,
        foreach (object key in from.Keys)         // the dictionary's own entries over all of them
            to[key] = from[key];
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

    /// <summary>
    /// The panel holding the title bar buttons (the close button), from the window's <c>ButtonsTemplate</c>.
    /// Breadth first and without descending into nested windows, so the header is found before the content.
    /// </summary>
    private static Panel FindButtonsPanel(GameWindow window)
    {
        DataTemplate template = window.ButtonsTemplate;
        var queue = new Queue<Visual>();
        queue.Enqueue(window);
        while (queue.Count > 0)
        {
            Visual v = queue.Dequeue();
            if (v is ContentPresenter presenter && template != null && Equals(presenter.ContentTemplate, template)
                && VisualTreeHelper.GetChildrenCount(presenter) > 0
                && VisualTreeHelper.GetChild(presenter, 0) is Panel templated)
                return templated;
            if (v is Button button && button.Name == "CloseButton" && VisualTreeHelper.GetParent(button) is Panel named)
                return named;
            int n = VisualTreeHelper.GetChildrenCount(v);
            for (int i = 0; i < n; i++)
            {
                if (VisualTreeHelper.GetChild(v, i) is Visual child && !(child is GameWindow))
                    queue.Enqueue(child);
            }
        }
        return null;
    }
}
