using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SceneSwift
{
    public static class SceneLoader
    {
        public static bool IsSceneInReadOnlyPackage(string path)
        {
            if (string.IsNullOrEmpty(path)) return true;
            return path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase);
        }

        public static bool SaveCurrentIfUserWantsTo()
        {
            return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        public static bool OpenScene(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath)) return false;
            if (!SaveCurrentIfUserWantsTo()) return false;
            try
            {
                EditorSceneManager.OpenScene(scenePath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"SceneSwift: Failed to open scene '{scenePath}': {e.Message}");
                return false;
            }
        }

        public static bool OpenScenesMulti(List<string> scenePaths, bool additive = true)
        {
            if (scenePaths == null || scenePaths.Count == 0) return false;
            if (!SaveCurrentIfUserWantsTo()) return false;

            try
            {
                Scene first = default;
                for (int i = 0; i < scenePaths.Count; i++)
                {
                    string path = scenePaths[i];
                    if (string.IsNullOrEmpty(path)) continue;
                    if (i == 0)
                        first = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    else
                        EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }
                return first.IsValid();
            }
            catch (Exception e)
            {
                Debug.LogError($"SceneSwift: Failed to open scenes: {e.Message}");
                return false;
            }
        }

        public static string GetStartupScenePath()
        {
            var settings = EditorBuildSettings.scenes;
            for (int i = 0; i < settings.Length; i++)
            {
                if (settings[i].enabled)
                    return settings[i].path;
            }
            return "";
        }

        public static void SetStartupSceneInBuild(string scenePath)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int found = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].path == scenePath) { found = i; break; }
            }
            if (found < 0)
            {
                list.Insert(0, new EditorBuildSettingsScene(scenePath, true));
                found = 0;
            }
            var startup = list[found];
            list.RemoveAt(found);
            for (int i = 0; i < list.Count; i++)
                list[i] = new EditorBuildSettingsScene(list[i].path, list[i].enabled);
            list.Insert(0, new EditorBuildSettingsScene(startup.path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
