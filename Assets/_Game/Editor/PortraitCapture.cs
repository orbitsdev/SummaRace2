using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SummaRace.EditorTools
{
    /// <summary>
    /// Renders the open scene at true portrait 1080x1920 and writes a PNG to
    /// &lt;project&gt;/Captures/, so a portrait layout can actually be LOOKED at.
    ///
    /// This is the technique CLAUDE.md has recorded since P2b ("render the canvas through an
    /// offscreen camera into a RenderTexture for a true portrait check") made permanent. It
    /// exists because every P2b/P3b layout bug -- the overflowing session board, the duplicated
    /// PIN label, two indistinguishable gold avatars, a low-contrast pill -- was invisible in
    /// code review and invisible in the Editor's landscape Game view, and only showed up in a
    /// portrait render.
    ///
    /// A ScreenSpaceOverlay canvas draws straight to the backbuffer and cannot be captured by
    /// a camera, so each one is switched to ScreenSpaceCamera for the render and put back
    /// afterwards. The restore is in a finally block and the scene is never saved, so a failure
    /// mid-render cannot leave a canvas rewired.
    ///
    /// LIMIT, and it is a real one: Unity does not run Awake/Start in edit mode, so anything a
    /// controller BUILDS at runtime is absent -- the race HUD (SWBST tracker, option board,
    /// pause chip), StorySelect's card contents, and every tween. What this shows is authored
    /// layout, proportion and colour. Feel still needs Play mode.
    /// </summary>
    public static class PortraitCapture
    {
        private const int Width = 1080;
        private const int Height = 1920;

        private struct CanvasState
        {
            public Canvas Canvas;
            public RenderMode Mode;
            public Camera WorldCamera;
            public float PlaneDistance;
        }

        [MenuItem("SummaRace/Capture Portrait Screen", false, 21)]
        public static void Capture()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var restore = new List<CanvasState>();
            RenderTexture rt = null;
            Camera temp = null;
            var previousActive = RenderTexture.active;

            try
            {
                rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                { antiAliasing = 1 };

                var cam = Camera.main;
                if (cam == null)
                {
                    // A UI-only scene may carry no camera at all. One is made for the render
                    // and destroyed with everything else in the finally.
                    var go = new GameObject("~PortraitCaptureCamera");
                    temp = go.AddComponent<Camera>();
                    temp.clearFlags = CameraClearFlags.SolidColor;
                    temp.backgroundColor = new Color(0.06f, 0.07f, 0.10f);
                    temp.nearClipPlane = 0.1f;
                    temp.farClipPlane = 1000f;
                    cam = temp;
                }

                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;

                    restore.Add(new CanvasState
                    {
                        Canvas = canvas,
                        Mode = canvas.renderMode,
                        WorldCamera = canvas.worldCamera,
                        PlaneDistance = canvas.planeDistance,
                    });

                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = cam;
                    canvas.planeDistance = 5f;   // well inside the near/far range of any of our cameras
                }

                Canvas.ForceUpdateCanvases();

                var previousTarget = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = previousTarget;

                RenderTexture.active = rt;
                var shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                shot.Apply();

                // Outside Assets/ deliberately: these are throwaway diagnostics, and writing
                // them into Assets would import 2MB textures and churn the asset database.
                var dir = Path.Combine(Directory.GetCurrentDirectory(), "Captures");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, scene.name + ".png");
                File.WriteAllBytes(path, shot.EncodeToPNG());
                Object.DestroyImmediate(shot);

                Debug.Log("[PortraitCapture] " + scene.name + " -> Captures/" + scene.name + ".png  (" +
                          Width + "x" + Height + ", " + restore.Count + " overlay canvas(es) captured)");
            }
            finally
            {
                RenderTexture.active = previousActive;

                foreach (var s in restore)
                {
                    if (s.Canvas == null) continue;
                    s.Canvas.renderMode = s.Mode;
                    s.Canvas.worldCamera = s.WorldCamera;
                    s.Canvas.planeDistance = s.PlaneDistance;
                }

                if (temp != null) Object.DestroyImmediate(temp.gameObject);
                if (rt != null)
                {
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }
            }
        }
    }
}
