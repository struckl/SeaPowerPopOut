using System;
using System.Reflection;
using HarmonyLib;
using Noesis;
using SeaPower;
using SeapowerUI;
using UnityEngine;
using Point = Noesis.Point;
using Rect = UnityEngine.Rect;
using Color = UnityEngine.Color;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Desktop mode, the 3D world as a texture. The game's world cameras draw into a render texture that the World View
/// window shows (in place of the game's own pip image), so the world never covers the screen behind the windows.
/// The texture follows the window's size; when the game's supersampling is on the cameras draw bigger and are shrunk
/// into the shown texture after each frame (<see cref="TextureResolve"/>).
/// </summary>
internal static class WorldViewTexture
{
    private const int MaxSide = 8192;
    private const double MaxPixels = 16000000;
    private const float SettleSeconds = 0.15f;

    private static readonly AccessTools.FieldRef<CameraManager, Camera> MainCamera =
        AccessTools.FieldRefAccess<CameraManager, Camera>("_camera");
    private static readonly AccessTools.FieldRef<CameraManager, Camera> ParticleCamera =
        AccessTools.FieldRefAccess<CameraManager, Camera>("_particleCamera");
    private static readonly MethodInfo SetRects = AccessTools.Method(typeof(CameraManager), "SetCameraRects");
    private static readonly MethodInfo Supersample = AccessTools.PropertyGetter(typeof(CameraManager), "PipSupersampleFactor");

    private static RenderTexture camTexture, shownTexture;
    private static float backgroundAlpha = -1f;
    private static int pendingW, pendingH;
    private static float pendingSince;
    private static TextureSource source;
    private static ImageBrush brush;
    private static RenderTexture brushOf;
    private static SolidColorBrush clearBrush;
    private static bool selfRects;

    /// <summary>The World View's rectangle on the screen in pixels (x0, y0, x1, y1; y down), valid while <see cref="Alive"/>.</summary>
    public static readonly float[] ScreenPixels = new float[4];
    public static int AliveFrame;

    public static bool Alive => Time.frameCount - AliveFrame <= 2 && ScreenPixels[2] > ScreenPixels[0];

    public static bool Holds => camTexture != null || shownTexture != null;

    public static bool ResolveNeeded =>
        camTexture != null && shownTexture != null && camTexture != shownTexture && camTexture.IsCreated() && shownTexture.IsCreated();

    // ---- every frame, once the World View window and its Pip3DView are known --------------------------------------

    public static void Step(Pip3DView view, CameraManager cm)
    {
        if (DesktopGame.PhotoMode)
        {
            SetCameraTarget(cm, null);
            return;
        }
        if (view == null || cm == null || !cm.IsPipActive)
            return;

        float width = view.ActualWidth, height = view.ActualHeight;
        if (width < 4f || height < 4f)
            return;
        Point a = view.PointToScreen(new Point(0f, 0f));
        Point b = view.PointToScreen(new Point(width, height));
        ScreenPixels[0] = Math.Min(a.X, b.X);
        ScreenPixels[1] = Math.Min(a.Y, b.Y);
        ScreenPixels[2] = Math.Max(a.X, b.X);
        ScreenPixels[3] = Math.Max(a.Y, b.Y);
        AliveFrame = Time.frameCount;

        EnsureTextures(cm, ScreenPixels[2] - ScreenPixels[0], ScreenPixels[3] - ScreenPixels[1]);
        SetCameraTarget(cm, camTexture);

        // The game narrows the cameras' rectangle for its own pip image; ours is a texture of its own: full again.
        Camera main = MainCamera(cm);
        if (main != null)
        {
            UnityEngine.Rect rect = main.rect;
            if (rect.width < 0.999f || rect.height < 0.999f || rect.x > 0.001f || rect.y > 0.001f)
                SetFullRect(cm);
        }
    }

    private static void SetFullRect(CameraManager cm)
    {
        selfRects = true;
        try
        {
            SetRects.Invoke(cm, new object[] { new Rect(0f, 0f, 1f, 1f) });
        }
        finally
        {
            selfRects = false;
        }
    }

    private static Camera[] Cameras(CameraManager cm)
    {
        Camera particle = ParticleCamera(cm);
        return particle == null ? new[] { MainCamera(cm) } : new[] { MainCamera(cm), particle };
    }

    private static void SetCameraTarget(CameraManager cm, RenderTexture target)
    {
        foreach (Camera cam in Cameras(cm))
        {
            if (cam != null && cam.targetTexture != target && (target != null || cam.targetTexture == camTexture))
                cam.targetTexture = target;
        }
    }

    private static void EnsureTextures(CameraManager cm, float pixelsW, float pixelsH)
    {
        int shownW = Math.Max(16, Math.Min(MaxSide, (int)Math.Round(pixelsW)));
        int shownH = Math.Max(16, Math.Min(MaxSide, (int)Math.Round(pixelsH)));
        double factor = 1;
        if (Supersample != null && Supersample.Invoke(null, null) is int configured)
            factor = Math.Max(1, configured);
        if (shownW * (double)shownH * factor * factor > MaxPixels)
            factor = Math.Max(1d, Math.Sqrt(MaxPixels / ((double)shownW * shownH)));
        int camW = factor <= 1d ? shownW : Math.Min(MaxSide, (int)Math.Round(shownW * factor));
        int camH = factor <= 1d ? shownH : Math.Min(MaxSide, (int)Math.Round(shownH * factor));

        if (shownW != pendingW || shownH != pendingH)
        {
            pendingW = shownW;
            pendingH = shownH;
            pendingSince = Time.unscaledTime;
        }
        bool fits = shownTexture != null && shownTexture.width == shownW && shownTexture.height == shownH
                    && camTexture != null && camTexture.width == camW && camTexture.height == camH;
        if (fits || (shownTexture != null && Time.unscaledTime - pendingSince < SettleSeconds))
            return;

        RenderTexture oldCam = camTexture, oldShown = shownTexture;
        int msaa = Math.Max(1, QualitySettings.antiAliasing);
        RenderTexture newCam, newShown;
        if (camW == shownW && camH == shownH)
        {
            newCam = newShown = new RenderTexture(shownW, shownH, 24, RenderTextureFormat.ARGB32) { antiAliasing = msaa };
        }
        else
        {
            newCam = new RenderTexture(camW, camH, 24, RenderTextureFormat.ARGB32) { antiAliasing = msaa };
            newShown = new RenderTexture(shownW, shownH, 0, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        }
        foreach (RenderTexture texture in new[] { newCam, newShown })
        {
            texture.useMipMap = false;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
        }
        newCam.name = "PopOut_WorldView_Camera";
        newShown.name = "PopOut_WorldView";
        newCam.Create();
        if (newShown != newCam)
            newShown.Create();
        camTexture = newCam;
        shownTexture = newShown;

        foreach (Camera cam in Cameras(cm))
        {
            if (cam == null)
                continue;
            if (backgroundAlpha < 0f)
                backgroundAlpha = cam.backgroundColor.a;
            Color background = cam.backgroundColor;
            background.a = 1f;
            cam.backgroundColor = background;
            cam.targetTexture = newCam;
            if (cam.GetComponent<TextureResolve>() == null)
                cam.gameObject.AddComponent<TextureResolve>();
        }
        Free(oldCam, oldShown);
        ModLog.Debug($"desktop mode: World View texture {shownW}x{shownH}" + (newCam != newShown ? $" (cameras draw {camW}x{camH})" : ""));
    }

    /// <summary>Back to the game's own drawing (desktop mode off, mission over).</summary>
    public static void Release(CameraManager cm)
    {
        if (cm != null)
        {
            foreach (Camera cam in Cameras(cm))
            {
                if (cam == null || cam.targetTexture != camTexture)
                    continue;
                cam.targetTexture = null;
                if (backgroundAlpha >= 0f)
                {
                    Color background = cam.backgroundColor;
                    background.a = backgroundAlpha;
                    cam.backgroundColor = background;
                }
            }
        }
        backgroundAlpha = -1f;
        brush = null;
        source = null;
        brushOf = null;
        Free(camTexture, shownTexture);
        camTexture = shownTexture = null;
        AliveFrame = 0;
    }

    private static void Free(RenderTexture cam, RenderTexture shown)
    {
        if (cam != null && cam != shown)
        {
            cam.Release();
            UnityEngine.Object.Destroy(cam);
        }
        if (shown != null)
        {
            shown.Release();
            UnityEngine.Object.Destroy(shown);
        }
    }

    // ---- hooks ----------------------------------------------------------------------------------------------------

    /// <summary>The game narrows the cameras for its pip image: only full rectangles (and our own) go through.</summary>
    public static bool AllowRect(Rect rect) => selfRects || (rect.width > 0.999f && rect.height > 0.999f);

    /// <summary>Pip3DView.OnRender: keep the game's pip bookkeeping alive, then draw our texture instead of its image.</summary>
    public static void Render(FrameworkElement view, DrawingContext context)
    {
        try
        {
            DesktopGame.Cameras?.RequestPipFrame(16, 16);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"desktop mode, pip frame request: {ex.Message}");
        }

        if (view.ActualWidth <= 0f || view.ActualHeight <= 0f)
            return;
        var full = new Noesis.Rect(0f, 0f, view.ActualWidth, view.ActualHeight);
        if (shownTexture != null && shownTexture.IsCreated())
        {
            if (brush == null || brushOf != shownTexture)
            {
                source = new TextureSource(shownTexture);
                brush = new ImageBrush(source) { Stretch = Stretch.Fill };
                brushOf = shownTexture;
            }
            // Land the texture on whole screen pixels so it stays sharp.
            Point origin = view.PointToScreen(new Point(0f, 0f));
            Point corner = view.PointToScreen(new Point(view.ActualWidth, view.ActualHeight));
            float scaleX = (corner.X - origin.X) / view.ActualWidth;
            float scaleY = (corner.Y - origin.Y) / view.ActualHeight;
            if (scaleX > 0.01f && scaleY > 0.01f)
            {
                float dx = ((float)Math.Round(origin.X) - origin.X) / scaleX;
                float dy = ((float)Math.Round(origin.Y) - origin.Y) / scaleY;
                context.DrawRectangle(brush, null, new Noesis.Rect(dx, dy, shownTexture.width / scaleX, shownTexture.height / scaleY));
            }
            else
            {
                context.DrawRectangle(brush, null, full);
            }
            return;
        }
        // No texture yet: an invisible rectangle, so the view still takes the mouse.
        clearBrush ??= new SolidColorBrush(Noesis.Color.FromArgb(0, 0, 0, 0));
        context.DrawRectangle(clearBrush, null, full);
    }

    private static bool downInside;

    /// <summary>
    /// Whether the world may see the mouse: over the World View, and a press that began there stays with the world
    /// until it is released (a drag may leave the window).
    /// </summary>
    public static bool MouseHandedToWorld()
    {
        Vector3 mouse = Input.mousePosition;
        float y = Screen.height - mouse.y;
        bool inside = Alive && mouse.x >= ScreenPixels[0] && mouse.x <= ScreenPixels[2] && y >= ScreenPixels[1] && y <= ScreenPixels[3];
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
            downInside = inside;
        bool held = Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);
        return held ? downInside : inside;
    }

    /// <summary>The mouse over the World View as pixels of the cameras' texture.</summary>
    public static Vector3 RemapMouse(Vector3 mouse)
    {
        if (DesktopGame.PhotoMode || camTexture == null || ScreenPixels[2] <= ScreenPixels[0] || ScreenPixels[3] <= ScreenPixels[1])
            return mouse;
        float u = (mouse.x - ScreenPixels[0]) / (ScreenPixels[2] - ScreenPixels[0]);
        float v = (Screen.height - mouse.y - ScreenPixels[1]) / (ScreenPixels[3] - ScreenPixels[1]);
        return new Vector3(u * camTexture.width, (1f - v) * camTexture.height, mouse.z);
    }

    /// <summary>Shrinks the cameras' (supersampled) texture into the one the window shows, after the camera has drawn.</summary>
    internal sealed class TextureResolve : MonoBehaviour
    {
        private void OnPostRender()
        {
            if (!ResolveNeeded)
                return;
            Camera self = GetComponent<Camera>();
            CameraManager cm = DesktopGame.Cameras;
            if (self == null || cm == null || self.targetTexture != camTexture)
                return;
            // Only the camera that draws last (the particle camera when it is on) resolves.
            Camera particle = ParticleCamera(cm);
            Camera last = particle != null && particle.enabled ? particle : MainCamera(cm);
            if (last == null || last.gameObject != gameObject)
                return;

            RenderTexture from = camTexture;
            FilterMode keep = from.filterMode;
            from.filterMode = FilterMode.Bilinear;
            while (from.width > shownTexture.width * 2 || from.height > shownTexture.height * 2)
            {
                RenderTexture half = RenderTexture.GetTemporary(Math.Max(shownTexture.width, from.width / 2),
                    Math.Max(shownTexture.height, from.height / 2), 0, camTexture.format);
                half.filterMode = FilterMode.Bilinear;
                Graphics.Blit(from, half);
                if (from != camTexture)
                    RenderTexture.ReleaseTemporary(from);
                from = half;
            }
            Graphics.Blit(from, shownTexture);
            if (from != camTexture)
                RenderTexture.ReleaseTemporary(from);
            camTexture.filterMode = keep;
        }
    }
}
