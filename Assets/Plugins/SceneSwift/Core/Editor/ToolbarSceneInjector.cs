using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SceneSwift
{
    [InitializeOnLoad]
    public static class ToolbarSceneInjector
    {
        static string[] _scenePaths;
        static int _setupAttempts;
        const int MaxSetupAttempts = 200;

        static IMGUIContainer _imguiContainer;

        static ToolbarSceneInjector()
        {
            EditorApplication.update -= TryInject;
            EditorApplication.update += TryInject;
            EditorApplication.projectChanged += () => { _scenePaths = null; };
        }

        static void TryInject()
        {
            _setupAttempts++;
            bool injected = TryInjectUnity6MainToolbar() || TryInjectLegacyToolbar();
            if (injected)
            {
                RefreshScenes();
                EditorApplication.update -= TryInject;
                return;
            }

            if (_setupAttempts > MaxSetupAttempts)
            {
                Debug.LogWarning("[SceneSwift] Toolbar injection not available in this Unity/editor layout. Use Tools > SceneSwift.");
                EditorApplication.update -= TryInject;
            }
        }

        static bool TryInjectUnity6MainToolbar()
        {
            var mainToolbarType = typeof(Editor).Assembly.GetType("UnityEditor.MainToolbarWindow");
            if (mainToolbarType == null) return false;

            UnityEngine.Object[] toolbars = Resources.FindObjectsOfTypeAll(mainToolbarType);
            if (toolbars.Length == 0) return false;

            var toolbarWindow = (EditorWindow)toolbars[0];
            VisualElement root = toolbarWindow.rootVisualElement;
            if (root == null) return false;

            VisualElement middleContainer = root.Q(className: "unity-overlay-container__middle-container");
            if (middleContainer == null) return false;
            if (root.Q("SceneSwiftToolbar") != null) return true;

            AddToolbarVisualBlock(middleContainer, 0);
            return true;
        }

        static bool TryInjectLegacyToolbar()
        {
            var toolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
            if (toolbarType == null) return false;

            var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
            if (toolbars == null || toolbars.Length == 0) return false;

            var toolbar = toolbars[0];
            var rootField = toolbarType.GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance);
            if (rootField == null) return false;
            var root = rootField.GetValue(toolbar) as VisualElement;
            if (root == null) return false;

            if (root.Q("SceneSwiftToolbar") != null) return true;

            var playButton = root.Q("Play") ?? root.Q(className: "unity-editor-toolbar__play-button");
            if (playButton != null && playButton.parent != null)
            {
                AddToolbarVisualBlock(playButton.parent, 0);
                return true;
            }

            var playModesZone = root.Q("ToolbarZonePlayModes");
            if (playModesZone != null)
            {
                AddToolbarVisualBlock(playModesZone, 0);
                return true;
            }

            var leftZone = root.Q("ToolbarZoneLeftAlign");
            if (leftZone != null)
            {
                AddToolbarVisualBlock(leftZone, leftZone.childCount);
                return true;
            }

            return false;
        }

        static void AddToolbarVisualBlock(VisualElement parent, int index)
        {
            float totalWidth = ProBridge.IsProAvailable ? 210 : 120;

            var ourBlock = new VisualElement
            {
                name = "SceneSwiftToolbar",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexGrow = 0,
                    flexShrink = 0,
                    paddingLeft = 5,
                    paddingRight = 5
                }
            };

            _imguiContainer = new IMGUIContainer(DrawToolbarSceneUI);
            _imguiContainer.style.width = totalWidth;
            _imguiContainer.style.height = 21;

            ourBlock.Add(_imguiContainer);
            parent.Insert(Mathf.Clamp(index, 0, parent.childCount), ourBlock);
        }

        static void RefreshScenes()
        {
            _scenePaths = AssetDatabase.FindAssets("t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !SceneLoader.IsSceneInReadOnlyPackage(p))
                .OrderBy(p => System.IO.Path.GetFileNameWithoutExtension(p))
                .ToArray();
        }

        static string GetSceneName(string path)
        {
            return string.IsNullOrEmpty(path) ? "" : System.IO.Path.GetFileNameWithoutExtension(path);
        }

        static void OpenSceneFromToolbar(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!SceneLoader.OpenScene(path)) return;

            var settings = SceneManagerSettingsProvider.GetOrCreateSettings();
            if (settings != null)
            {
                settings.AddRecent(path);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }

        static Rect ComputeButtonScreenRect(Rect localButtonRect)
        {
            if (_imguiContainer != null)
            {
                Rect containerWorldBound = _imguiContainer.worldBound;
                Rect toolbarScreenPos = GetToolbarWindowScreenPosition();

                if (toolbarScreenPos.width > 0)
                {
                    float x = toolbarScreenPos.x + containerWorldBound.x + localButtonRect.x;
                    float y = toolbarScreenPos.y + containerWorldBound.y + localButtonRect.yMax + 20f;
                    return new Rect(x, y, localButtonRect.width, 1f);
                }
            }

            Vector2 sp = GUIUtility.GUIToScreenPoint(new Vector2(localButtonRect.x, localButtonRect.yMax));
            return new Rect(sp.x, sp.y + 20f, localButtonRect.width, 1f);
        }

        static Rect GetToolbarWindowScreenPosition()
        {
            var mainToolbarType = typeof(Editor).Assembly.GetType("UnityEditor.MainToolbarWindow");
            if (mainToolbarType != null)
            {
                var wins = Resources.FindObjectsOfTypeAll(mainToolbarType);
                if (wins.Length > 0 && wins[0] is EditorWindow ew)
                    return ew.position;
            }

            var toolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
            if (toolbarType != null)
            {
                var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
                if (toolbars.Length > 0)
                {
                    Rect? found = TryGetRectProperty(toolbars[0], "screenPosition");
                    if (found.HasValue) return found.Value;

                    found = TryGetRectProperty(toolbars[0], "position");
                    if (found.HasValue) return found.Value;
                }
            }

            return Rect.zero;
        }

        static Rect? TryGetRectProperty(UnityEngine.Object obj, string propertyName)
        {
            var type = obj.GetType();
            while (type != null && type != typeof(object))
            {
                var prop = type.GetProperty(propertyName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (prop != null && prop.PropertyType == typeof(Rect) && prop.CanRead)
                {
                    try { return (Rect)prop.GetValue(obj); }
                    catch { /* reflection failure, continue */ }
                }
                type = type.BaseType;
            }
            return null;
        }

        static void ShowSceneDropdownMenu(Rect localButtonRect)
        {
            RefreshScenes();
            var settings = SceneManagerSettingsProvider.GetOrCreateSettings();
            var validPaths = _scenePaths ?? Array.Empty<string>();

            var favorites = ProBridge.IsProAvailable && settings != null
                ? settings.Favorites.Where(p => validPaths.Contains(p)).ToList()
                : new List<string>();
            var recent = settings != null
                ? settings.Recent.Where(p => validPaths.Contains(p)).Take(10).ToList()
                : new List<string>();
            var allScenes = validPaths.ToList();

            Rect screenRect = ComputeButtonScreenRect(localButtonRect);

            SceneQuickMenuPopup.Show(screenRect, settings, favorites, recent, allScenes,
                OpenSceneFromToolbar);
        }

        static void DrawToolbarSceneUI()
        {
            if (_scenePaths == null || _scenePaths.Length == 0) RefreshScenes();

            string currentPath = SceneManager.GetActiveScene().path;
            string currentName = GetSceneName(currentPath);
            if (string.IsNullOrEmpty(currentName)) currentName = "(No scene)";

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent(currentName + " \u25BE", "Switch scene (Recent, All)"), EditorStyles.toolbarButton, GUILayout.Width(110)))
                ShowSceneDropdownMenu(GUILayoutUtility.GetLastRect());

            if (ProBridge.IsProAvailable)
            {
                if (GUILayout.Button("SceneSwift", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    ProBridge.OpenSceneManagerWindow?.Invoke();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
