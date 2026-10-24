using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using AnchorChain;
using BepInEx;
using Noesis;
using MapControl;
using SeaPower;
using SeaPower.LiveMap;
using SeapowerUI;
using SeapowerUI.ViewModels;
using UnityEngine;
using GameWindow = SeapowerUI.Window;
using Object = UnityEngine.Object;
using Path = System.IO.Path;

namespace ShotDirector;

/// <summary>
/// Dev tool for the Workshop screenshots, never shipped. Polls <c>BepInEx/shotdirector.cmd</c>: every line written
/// there is a command, run in order (one per frame, <c>wait</c> pauses the queue). Results and errors go to
/// <c>BepInEx/shotdirector.log</c>. The pictures themselves are grabbed from outside with <c>grab.ps1</c> (the game's
/// own ScreenCapture fails while the pop-out monitor is active). Commands:
/// <code>
/// load missions/NATO/Trollfjorden.ini   start a mission (from the main menu)
/// click X Y | move X Y | key tab        mouse / keyboard (screen pixels, key names or hex)
/// wait SECONDS
/// mapfull | mapoff                       map full screen (Tab) / map closed
/// center LAT LON [ZOOM] | where          map centre and zoom level through the map's view model
/// select NAME | deselect | units         select a unit (substring of its name), list the player's units
/// tc FACTOR | pause | unpause            time compression
/// theme light|dark|auto | hud on|off
/// open KIND | close KIND                 a game window: formation, log, status, camera (or a MainGameViewModel
///                                        property name), opened like its bottom-bar button does
/// popout KIND | popin KIND               move it to the pop-out monitor / back, like its title-bar button
/// place KIND X Y [W H]                   put it there (window units of its screen)
/// hover KIND                             mouse onto its pop-out button (main screen), so the tooltip shows
/// windows                                log every open window: screen, kind, place, size
/// get PATH | set PATH VALUE | call PATH ARGS…   reflection, e.g. SeaPower.Globals._mainGameViewModel.MapVisible
/// quit
/// </code>
/// </summary>
[ACPlugin("dev.sewerlabs.shotdirector", "Shot Director (dev)", "1.0.0")]
public class Plugin : IAnchorChainMod
{
    public void TriggerEntryPoint()
    {
        var host = new GameObject("ShotDirector");
        Object.DontDestroyOnLoad(host);
        host.hideFlags = HideFlags.HideAndDontSave;
        host.AddComponent<Director>();
    }
}

internal sealed class Director : MonoBehaviour
{
    private static readonly string CommandFile = Path.Combine(Paths.BepInExRootPath, "shotdirector.cmd");
    private static readonly string LogFile = Path.Combine(Paths.BepInExRootPath, "shotdirector.log");

    private readonly Queue<string> queue = new Queue<string>();
    private float nextPoll, waitUntil;

    private void Start() => Log("ready");

    private void Update()
    {
        if (Time.unscaledTime >= nextPoll)
        {
            nextPoll = Time.unscaledTime + 0.25f;
            Poll();
        }
        if (queue.Count == 0 || Time.unscaledTime < waitUntil)
            return;
        string line = queue.Dequeue();
        try
        {
            Run(line);
        }
        catch (Exception ex)
        {
            Log($"FAILED {line}: {ex.GetBaseException().GetType().Name}: {ex.GetBaseException().Message}");
        }
    }

    private void Poll()
    {
        if (!File.Exists(CommandFile))
            return;
        string[] lines;
        try
        {
            lines = File.ReadAllLines(CommandFile);
            File.Delete(CommandFile);
        }
        catch (IOException)
        {
            return;                                   // still being written; next poll
        }
        foreach (string l in lines.Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")))
            queue.Enqueue(l);
    }

    private void Run(string line)
    {
        string[] a = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string rest = line.Length > a[0].Length ? line.Substring(a[0].Length).Trim() : "";
        switch (a[0].ToLowerInvariant())
        {
            case "load":
                Globals.currentMissionFilePath = Singleton<FileManager>.Instance.GetFile(rest);
                Log($"load {Globals.currentMissionFilePath}");
                MissionManager.DoLoad();
                break;
            case "click":
                Native.Click(I(a[1]), I(a[2]));
                Log(line);
                break;
            case "move":
                Native.Move(I(a[1]), I(a[2]));
                Log(line);
                break;
            case "key":
                Native.Key(rest);
                Log(line);
                break;
            case "wait":
                waitUntil = Time.unscaledTime + F(a[1]);
                break;
            case "mapfull":
                MapFull(on: true);
                break;
            case "mapoff":
                MapFull(on: false);
                break;
            case "center":
                // The map control is bound to the scene map's view model; setting it there sticks.
                Globals._mainGameViewModel.Map.DisplayMap.Center = new GeoPosition(D(a[1]), D(a[2]));
                if (a.Length > 3)
                    Globals._mainGameViewModel.Map.DisplayMap.Zoom = F(a[3]);
                Log(line);
                break;
            case "where":
                GeoPosition c = Globals._mainGameViewModel.Map.DisplayMap.Center;
                Log($"where {c.Latitude:0.000},{c.Longitude:0.000} zoom {Globals._mainGameViewModel.Map.DisplayMap.Zoom:0.00}");
                break;
            case "deselect":
                Singleton<RenderPosition>.Instance.deselectObjectAndDetachCamera();
                Log(line);
                break;
            case "get":
                Log($"get {a[1]} = {Reflect.Get(a[1])}");
                break;
            case "set":
                Reflect.Set(a[1], rest.Substring(a[1].Length).Trim());
                Log($"set {a[1]} = {Reflect.Get(a[1])}");
                break;
            case "call":
                Log($"call {a[1]} -> {Reflect.Call(a[1], a.Skip(2).ToArray())}");
                break;
            case "theme":
                Object.FindFirstObjectByType<MapThemeSelector>().Theme =
                    (ThemeMode)Enum.Parse(typeof(ThemeMode), a[1], ignoreCase: true);
                Log(line);
                break;
            case "units":
                Units();
                break;
            case "select":
                Select(Find(rest));
                break;
            case "tc":
                GameTime.TimeCompression = F(a[1]);
                Log(line);
                break;
            case "pause":
                GameTime.Pause(muteSounds: false);
                Log(line);
                break;
            case "unpause":
                GameTime.UnPause();
                Log(line);
                break;
            case "hud":
                MainGameViewModel.SetHudVisible(a[1] == "on");
                Log(line);
                break;
            case "open":
                WindowHostCommands.OpenWindowCommand.Execute(Content(a[1]), MainGameView.Instance);
                Log(line);
                break;
            case "close":
                WindowHostCommands.CloseWindowCommand.Execute(Content(a[1]), MainGameView.Instance);
                Log(line);
                break;
            case "popout":
                PopOut(Content(a[1]), toPopOut: true);
                Log(line);
                break;
            case "popin":
                PopOut(Content(a[1]), toPopOut: false);
                Log(line);
                break;
            case "place":
                Place(Content(a[1]), F(a[2]), F(a[3]), a.Length > 5 ? F(a[4]) : float.NaN, a.Length > 5 ? F(a[5]) : float.NaN);
                Log(line);
                break;
            case "windows":
                Windows();
                break;
            case "hover":
                Hover(Content(a[1]));
                break;
            case "quit":
                Log("quit");
                Application.Quit();
                break;
            default:
                Log($"unknown command: {line}");
                break;
        }
    }

    private void MapFull(bool on)
    {
        MainGameViewModel vm = Globals._mainGameViewModel;
        if (vm?.Map == null)
        {
            Log("map: no map yet");
            return;
        }
        if (on && (!vm.Map.IsFullscreen || !vm.MapVisible))
            vm.ToggleMapFullscreen();
        else if (!on && vm.MapVisible)
            vm.ToggleMapFullscreen();
        Log($"map: visible {vm.MapVisible}, fullscreen {vm.Map.IsFullscreen}");
    }

    // ---- windows ------------------------------------------------------------------------------------------------

    private static readonly Dictionary<string, string> Kinds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["formation"] = nameof(MainGameViewModel.FormationManager),
        ["log"] = nameof(MainGameViewModel.EventLog),
        ["status"] = nameof(MainGameViewModel.UnitStatus),
        ["camera"] = nameof(MainGameViewModel.CameraControls),
    };

    // A window's content: the view model the bottom-bar button opens.
    private static object Content(string kind)
    {
        string property = Kinds.TryGetValue(kind, out string known) ? known : kind;
        PropertyInfo p = typeof(MainGameViewModel).GetProperty(property, BindingFlags.Public | BindingFlags.Instance)
                         ?? throw new InvalidOperationException($"no window kind '{kind}'");
        return p.GetValue(Globals._mainGameViewModel) ?? throw new InvalidOperationException($"{property} is null");
    }

    // The pop-out mod's own move, as its title-bar button does it (remembered for the next mission).
    private static void PopOut(object content, bool toPopOut)
    {
        Type screen = AppDomain.CurrentDomain.GetAssemblies()
            .Select(asm => asm.GetType("SeaPowerPopOut.PopOutScreen", throwOnError: false))
            .FirstOrDefault(t => t != null) ?? throw new InvalidOperationException("SeaPowerPopOut not loaded");
        object instance = screen.GetProperty("Instance", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).GetValue(null);
        MethodInfo move = screen.GetMethod("Move", BindingFlags.NonPublic | BindingFlags.Instance);
        move.Invoke(instance, new[] { content, toPopOut, (object)true });
    }

    private void Place(object content, float x, float y, float width, float height)
    {
        GameWindow window = OpenWindows().Select(w => w.window).FirstOrDefault(w => ReferenceEquals(w.DataContext, content))
                            ?? throw new InvalidOperationException("window not open");
        typeof(GameWindow).GetMethod("MarkUserPositioned", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(window, null);
        window.X = x;
        window.Y = y;
        if (!float.IsNaN(width))
            window.Width = width;
        if (!float.IsNaN(height))
            window.Height = height;
    }

    // The pop-out button's centre in screen pixels: view pixels from Noesis, plus where the game window's client
    // area sits on the desktop. Two moves, as a tooltip only opens on a real mouse move.
    private void Hover(object content)
    {
        GameWindow window = OpenWindows().Where(w => w.screen != "SeaPowerPopOut screen").Select(w => w.window)
                                .FirstOrDefault(w => ReferenceEquals(w.DataContext, content))
                            ?? throw new InvalidOperationException("window not open on the main screen");
        FrameworkElement button = Descendants(window).OfType<FrameworkElement>().FirstOrDefault(e => e.Name == "PopOutButton")
                                  ?? throw new InvalidOperationException("no pop-out button");
        Point centre = button.PointToScreen(new Point(button.ActualWidth / 2f, button.ActualHeight / 2f));
        Native.ClientOrigin(out int ox, out int oy);
        int x = ox + (int)centre.X, y = oy + (int)centre.Y;
        Native.Move(x + 6, y + 4);
        Native.Move(x, y);
        Log($"hover {x},{y}");
    }

    private static IEnumerable<DependencyObject> Descendants(Visual root)
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            DependencyObject d = stack.Pop();
            yield return d;
            if (d is Visual v)
            {
                int n = VisualTreeHelper.GetChildrenCount(v);
                for (int i = 0; i < n; i++)
                    stack.Push(VisualTreeHelper.GetChild(v, i));
            }
        }
    }

    private void Windows()
    {
        foreach ((string screen, GameWindow w) in OpenWindows())
            Log($"window {screen} {w.DataContext?.GetType().Name} at {w.X:0},{w.Y:0} size {w.ActualWidth:0}x{w.ActualHeight:0}");
    }

    // Every top-level game window in every Noesis view: which view (game object) and the window.
    private static IEnumerable<(string screen, GameWindow window)> OpenWindows()
    {
        foreach (NoesisView view in Object.FindObjectsByType<NoesisView>(FindObjectsSortMode.None))
        {
            if (view.Content == null)
                continue;
            var stack = new Stack<DependencyObject>();
            stack.Push(view.Content);
            int seen = 0;
            while (stack.Count > 0 && seen++ < 60000)
            {
                DependencyObject d = stack.Pop();
                if (d is GameWindow window)
                {
                    yield return (view.gameObject.name, window);
                    continue;
                }
                if (!(d is Visual v))
                    continue;
                int n = VisualTreeHelper.GetChildrenCount(v);
                for (int i = 0; i < n; i++)
                    stack.Push(VisualTreeHelper.GetChild(v, i));
            }
        }
    }

    // ---- units --------------------------------------------------------------------------------------------------

    private static List<ObjectBase> AllUnits() =>
        Object.FindObjectsByType<ObjectBase>(FindObjectsSortMode.None)
            .Where(u => u != null && !u.IsDestroyed && u._taskforce != null)
            .ToList();

    private void Units()
    {
        foreach (ObjectBase u in AllUnits().Where(u => u.IsPlayerObject))
        {
            float m = Utils.unityHeightToMeter(u.transform.position.y);
            Log($"unit '{u.getName()}' [{u.gameObject.name}] {u.GetType().Name} air={u.IsAirUnit} " +
                $"at {u._geoPosition.Latitude:0.000},{u._geoPosition.Longitude:0.000} y={m:0} m");
        }
    }

    private ObjectBase Find(string name)
    {
        ObjectBase unit = AllUnits().FirstOrDefault(u => u.IsPlayerObject && u.getName().IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                       ?? AllUnits().FirstOrDefault(u => u.gameObject.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
        if (unit == null)
            throw new InvalidOperationException($"no unit '{name}'");
        return unit;
    }

    private void Select(ObjectBase unit)
    {
        Singleton<RenderPosition>.Instance.switchToObject(unit, performSmoothMove: false);
        Log($"selected '{unit.getName()}'");
    }

    private static int I(string s) => int.Parse(s, CultureInfo.InvariantCulture);
    private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
    private static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);

    private static void Log(string message)
    {
        string line = $"{DateTime.Now:HH:mm:ss} {message}";
        try { File.AppendAllText(LogFile, line + System.Environment.NewLine); }
        catch (IOException) { }
        UnityEngine.Debug.Log("[ShotDirector] " + message);
    }
}

/// <summary>
/// Dotted member paths for get / set / call: a type's full name followed by static and then instance members,
/// e.g. <c>SeaPower.Globals._mainGameViewModel.Map.DisplayMap.Zoom</c>. <c>Singleton</c> types are reached as
/// <c>SeaPower.RenderPosition.@</c> (the singleton instance).
/// </summary>
internal static class Reflect
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

    public static object Get(string path) => Walk(path.Split('.'), out _, out _, last: false);

    public static void Set(string path, string value)
    {
        string[] parts = path.Split('.');
        object owner = Walk(parts, out Type ownerType, out string member, last: true);
        MemberInfo m = ownerType.GetMember(member, Any).First();
        Type type = m is PropertyInfo p ? p.PropertyType : ((FieldInfo)m).FieldType;
        object parsed = Parse(value, type);
        if (m is PropertyInfo prop)
            prop.SetValue(owner, parsed);
        else
            ((FieldInfo)m).SetValue(owner, parsed);
    }

    public static object Call(string path, string[] args)
    {
        string[] parts = path.Split('.');
        object owner = Walk(parts, out Type ownerType, out string member, last: true);
        MethodInfo method = ownerType.GetMethods(Any).First(x => x.Name == member && x.GetParameters().Length >= args.Length &&
                                                                 x.GetParameters().Skip(args.Length).All(pp => pp.IsOptional));
        ParameterInfo[] ps = method.GetParameters();
        object[] values = ps.Select((pp, i) => i < args.Length ? Parse(args[i], pp.ParameterType) : pp.DefaultValue).ToArray();
        return method.Invoke(owner, values);
    }

    // Resolves the longest type-name prefix, then walks the members. With last = true the final member is left
    // for the caller and the object owning it is returned.
    private static object Walk(string[] parts, out Type ownerType, out string member, bool last)
    {
        Type type = null;
        int typeParts = 0;
        for (int n = parts.Length; n > 0 && type == null; n--)
        {
            type = FindType(string.Join(".", parts.Take(n)));
            typeParts = n;
        }
        if (type == null)
            throw new InvalidOperationException($"no type in {string.Join(".", parts)}");
        object current = null;
        int end = last ? parts.Length - 1 : parts.Length;
        for (int k = typeParts; k < end; k++)
        {
            if (parts[k] == "@")
            {
                current = type.GetProperty("Instance", Any)?.GetValue(null)
                          ?? FindType("Singleton`1")?.MakeGenericType(type).GetProperty("Instance", Any)?.GetValue(null);
                continue;
            }
            MemberInfo m = type.GetMember(parts[k], Any).FirstOrDefault()
                           ?? throw new InvalidOperationException($"no member {parts[k]} on {type.Name}");
            current = m is PropertyInfo p ? p.GetValue(current) : m is FieldInfo f ? f.GetValue(current) : throw new InvalidOperationException($"{parts[k]} is a method");
            if (current == null)
                throw new InvalidOperationException($"{parts[k]} is null");
            type = current.GetType();
        }
        ownerType = type;
        member = last ? parts[parts.Length - 1] : null;
        return current;
    }

    private static Type FindType(string name) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Select(asm => { try { return asm.GetType(name, false); } catch { return null; } })
            .FirstOrDefault(t => t != null);

    private static object Parse(string s, Type type)
    {
        if (type == typeof(string))
            return s;
        if (type.IsEnum)
            return Enum.Parse(type, s, true);
        if (type == typeof(GeoPosition))
        {
            string[] ll = s.Split(',');
            return new GeoPosition(double.Parse(ll[0], CultureInfo.InvariantCulture), double.Parse(ll[1], CultureInfo.InvariantCulture));
        }
        return Convert.ChangeType(s, type, CultureInfo.InvariantCulture);
    }
}

internal static class Native
{
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hwnd, ref POINT point);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    private delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X, Y;
    }

    private const uint LeftDown = 0x02, LeftUp = 0x04, KeyUp = 0x02;

    private static readonly Dictionary<string, byte> Keys = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
    {
        ["tab"] = 0x09, ["enter"] = 0x0D, ["esc"] = 0x1B, ["space"] = 0x20, ["m"] = 0x4D,
    };

    public static void Move(int x, int y) => SetCursorPos(x, y);

    /// <summary>
    /// Where the game window's client area (the main view's pixel 0,0) sits on the desktop: the game's window whose
    /// client area has the main screen's size. (The process's "main window" can be a pop-out display's window.)
    /// </summary>
    public static void ClientOrigin(out int x, out int y)
    {
        uint own = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        IntPtr main = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == own && GetClientRect(hwnd, out RECT r) && r.Right == Screen.width && r.Bottom == Screen.height)
            {
                main = hwnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        var p = new POINT();
        ClientToScreen(main, ref p);
        x = p.X;
        y = p.Y;
    }

    public static void Click(int x, int y)
    {
        SetCursorPos(x, y);
        mouse_event(LeftDown, 0, 0, 0, UIntPtr.Zero);
        mouse_event(LeftUp, 0, 0, 0, UIntPtr.Zero);
    }

    public static void Key(string name)
    {
        byte vk = Keys.TryGetValue(name, out byte k) ? k : Convert.ToByte(name, 16);
        keybd_event(vk, 0, 0, UIntPtr.Zero);
        keybd_event(vk, 0, KeyUp, UIntPtr.Zero);
    }
}
