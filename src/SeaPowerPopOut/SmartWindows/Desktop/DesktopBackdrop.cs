using System;
using System.IO;
using UnityEngine;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Desktop mode, the screen behind the windows. With the world drawn into a texture the game leaves the screen
/// uncleared, so a camera of our own paints it: plain grey, or the picture <c>PopOut_Backdrop.jpg / .png</c> from the
/// mod's folder (scaled to cover the screen). It lives only while a mission runs in desktop mode.
/// </summary>
internal static class DesktopBackdrop
{
    private const int PictureLayer = 31;
    private static readonly string[] Extensions = { ".jpg", ".jpeg", ".png" };

    private static GameObject root;
    private static Transform picture;
    private static float pictureAspect;
    private static int fittedW, fittedH;

    public static void Update(bool wanted)
    {
        if (wanted && root == null)
            Create();
        else if (wanted && picture != null)
            Fit();
        else if (!wanted && root != null)
        {
            UnityEngine.Object.Destroy(root);
            root = null;
            picture = null;
            ModLog.Debug("desktop mode: backdrop removed");
        }
    }

    private static void Create()
    {
        root = new GameObject("PopOut_Backdrop");
        UnityEngine.Object.DontDestroyOnLoad(root);
        Camera cam = root.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.57f, 0.6f, 1f);
        cam.cullingMask = 0;
        cam.depth = -1000f;
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.useOcclusionCulling = false;
        AddPicture(cam);
        ModLog.Debug("desktop mode: backdrop camera created");
    }

    private static void AddPicture(Camera cam)
    {
        try
        {
            string folder = Path.GetDirectoryName(typeof(DesktopBackdrop).Assembly.Location) ?? "";
            string file = null;
            foreach (string extension in Extensions)
            {
                string candidate = Path.Combine(folder, "PopOut_Backdrop" + extension);
                if (File.Exists(candidate))
                {
                    file = candidate;
                    break;
                }
            }
            if (file == null)
                return;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(file)))
            {
                ModLog.Warn($"desktop mode: backdrop picture {file} cannot be read - plain grey.");
                return;
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = 4;
            var holder = new GameObject("PopOut_BackdropPicture") { layer = PictureLayer };
            holder.transform.SetParent(root.transform, false);
            holder.transform.localPosition = new Vector3(0f, 0f, 10f);
            holder.AddComponent<SpriteRenderer>().sprite = Sprite.Create(
                texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            picture = holder.transform;
            pictureAspect = (float)texture.width / texture.height;
            // Far from the world, so no other camera of the game sees it; only this one looks at layer 31.
            root.transform.position = new Vector3(50000f, 0f, 0f);
            cam.cullingMask = 1 << PictureLayer;
            fittedW = 0;
            Fit();
            ModLog.Debug($"desktop mode: backdrop picture {Path.GetFileName(file)} {texture.width}x{texture.height}");
        }
        catch (Exception ex)
        {
            ModLog.Warn($"desktop mode: backdrop picture: {ex.Message}");
        }
    }

    /// <summary>Scales the picture to cover the screen (the camera sees 10 units of height).</summary>
    private static void Fit()
    {
        if (Screen.height <= 0 || (fittedW == Screen.width && fittedH == Screen.height))
            return;
        fittedW = Screen.width;
        fittedH = Screen.height;
        float visibleWidth = 10f * Screen.width / Screen.height;
        float pictureHeight = picture.GetComponent<SpriteRenderer>().sprite.rect.height / 100f;
        float pictureWidth = pictureHeight * pictureAspect;
        float scale = Mathf.Max(visibleWidth / pictureWidth, 10f / pictureHeight);
        picture.localScale = new Vector3(scale, scale, 1f);
    }
}
