using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SceneSwift
{
    public class SceneQuickMenuPopup : EditorWindow
    {
        SceneManagerSettings _settings;
        List<string> _favorites;
        List<string> _recent;
        List<string> _allScenes;
        Action<string> _openScene;
        Vector2 _scroll;

        public static void Show(Rect buttonScreenRect, SceneManagerSettings settings,
            List<string> favorites, List<string> recent, List<string> allScenes,
            Action<string> openScene)
        {
            var win = CreateInstance<SceneQuickMenuPopup>();
            win._settings = settings;
            win._favorites = favorites ?? new List<string>();
            win._recent = recent ?? new List<string>();
            win._allScenes = allScenes ?? new List<string>();
            win._openScene = openScene;

            bool showFavorites = ProBridge.IsProAvailable && win._favorites.Count > 0;
            int rows = (showFavorites ? win._favorites.Count : 0) + win._recent.Count + win._allScenes.Count;
            int headers = (showFavorites ? 1 : 0) + (win._recent.Count > 0 ? 1 : 0) + (win._allScenes.Count > 0 ? 1 : 0);
            float h = Mathf.Clamp(20f + (rows * 22f) + (headers * 20f) + 40f, 180f, 420f);
            var size = new Vector2(320f, h);

            win.ShowAsDropDown(buttonScreenRect, size);
        }

        static string SceneName(string path) => System.IO.Path.GetFileNameWithoutExtension(path);

        void DrawSection(string title, List<string> scenes, bool favoriteStar)
        {
            if (scenes == null || scenes.Count == 0) return;
            GUILayout.Label(title, EditorStyles.boldLabel);
            foreach (var path in scenes)
            {
                var row = EditorGUILayout.GetControlRect(false, 20f);

                float textX = row.x + 4;

                if (ProBridge.IsProAvailable)
                {
                    var iconRect = new Rect(row.x + 4, row.y + 3, 14, 14);
                    textX = row.x + 24;

                    if (_settings != null && _settings.TryGetSceneColor(path, out var c))
                        DrawGlossySwatch(iconRect, c);
                    else
                        EditorGUI.DrawRect(new Rect(row.x + 4, row.y + 3, 14, 14), new Color(0.45f, 0.45f, 0.45f, 1f));
                }

                var textRect = new Rect(textX, row.y, row.width - textX + row.x, row.height);
                string label = favoriteStar ? "\u2605  " + SceneName(path) : SceneName(path);
                if (GUI.Button(textRect, label, EditorStyles.label))
                {
                    _openScene?.Invoke(path);
                    Close();
                }
            }
            EditorGUILayout.Space(6);
        }

        static void DrawGlossySwatch(Rect rect, Color color)
        {
            if (ProBridge.IsProAvailable)
            {
                var getMethod = GetSwatchTextureMethod();
                if (getMethod != null)
                {
                    var tex = getMethod(color);
                    if (tex != null)
                    {
                        GUI.DrawTexture(rect, tex);
                        return;
                    }
                }
            }
            EditorGUI.DrawRect(rect, color);
        }

        static Func<Color, Texture2D> _swatchTexMethod;
        static bool _swatchTexMethodResolved;

        static Func<Color, Texture2D> GetSwatchTextureMethod()
        {
            if (_swatchTexMethodResolved) return _swatchTexMethod;
            _swatchTexMethodResolved = true;

            var type = Type.GetType("SceneSwift.SwatchTextureUtil, Assembly-CSharp");
            if (type == null)
                type = Type.GetType("SceneSwift.SwatchTextureUtil, Assembly-CSharp-firstpass");
            if (type == null) return null;

            var method = type.GetMethod("Get", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (method == null) return null;

            _swatchTexMethod = (Color c) => (Texture2D)method.Invoke(null, new object[] { c });
            return _swatchTexMethod;
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            if (ProBridge.IsProAvailable)
                DrawSection("Favorites", _favorites, true);

            DrawSection("Recent", _recent, false);
            DrawSection("All Scenes", _allScenes, false);

            EditorGUILayout.Space(4);
            if (ProBridge.IsProAvailable)
            {
                if (GUILayout.Button("Open SceneSwift...", GUILayout.Height(22)))
                {
                    ProBridge.OpenSceneManagerWindow?.Invoke();
                    Close();
                }
            }
            else
            {
                if (GUILayout.Button("\u2605 Get SceneSwift Pro", GUILayout.Height(22)))
                    Application.OpenURL("https://assetstore.unity.com/packages/tools/utilities/sceneswift-pro");
            }

            EditorGUILayout.EndScrollView();
        }

        void OnLostFocus()
        {
            Close();
        }
    }
}
