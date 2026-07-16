using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneSwift
{
    [Serializable]
    public class SceneGroup
    {
        public string groupName = "New Group";
        public List<string> scenePaths = new List<string>();
    }

    [Serializable]
    public class SceneColorTag
    {
        public string scenePath;
        public Color color = Color.gray;
    }

    [CreateAssetMenu(fileName = "SceneManagerSettings", menuName = "SceneSwift/Settings")]
    public class SceneManagerSettings : ScriptableObject
    {
        public const int MaxRecentCount = 10;

        [SerializeField] List<string> _favorites = new List<string>();
        [SerializeField] List<string> _recent = new List<string>();
        [SerializeField] List<SceneGroup> _groups = new List<SceneGroup>();
        [SerializeField] List<SceneColorTag> _sceneColors = new List<SceneColorTag>();

        [Header("Startup & Play Mode")]
        [SerializeField] string _startupScenePath = "";
        [SerializeField] string _playModeOverrideScenePath = "";
        [SerializeField] bool _enablePlayModeOverride;

        public List<string> Favorites => _favorites;
        public List<string> Recent => _recent;
        public List<SceneGroup> Groups => _groups;
        public List<SceneColorTag> SceneColors => _sceneColors;
        public string StartupScenePath { get => _startupScenePath; set => _startupScenePath = value ?? ""; }
        public string PlayModeOverrideScenePath { get => _playModeOverrideScenePath; set => _playModeOverrideScenePath = value ?? ""; }
        public bool EnablePlayModeOverride { get => _enablePlayModeOverride; set => _enablePlayModeOverride = value; }

        public void AddRecent(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath)) return;
            _recent.Remove(scenePath);
            _recent.Insert(0, scenePath);
            while (_recent.Count > MaxRecentCount)
                _recent.RemoveAt(_recent.Count - 1);
        }

        public void ToggleFavorite(string scenePath)
        {
            if (_favorites.Contains(scenePath))
                _favorites.Remove(scenePath);
            else
                _favorites.Add(scenePath);
        }

        public bool IsFavorite(string scenePath) => _favorites.Contains(scenePath);

        public bool TryGetSceneColor(string scenePath, out Color color)
        {
            var entry = _sceneColors.Find(e => e.scenePath == scenePath);
            if (entry != null)
            {
                color = entry.color;
                return true;
            }
            color = default;
            return false;
        }

        public Color GetSceneColor(string scenePath)
        {
            var entry = _sceneColors.Find(e => e.scenePath == scenePath);
            return entry != null ? entry.color : Color.gray;
        }

        public void SetSceneColor(string scenePath, Color color)
        {
            var entry = _sceneColors.Find(e => e.scenePath == scenePath);
            if (entry != null)
                entry.color = color;
            else
                _sceneColors.Add(new SceneColorTag { scenePath = scenePath, color = color });
        }

        public void ClearSceneColor(string scenePath)
        {
            int index = _sceneColors.FindIndex(e => e.scenePath == scenePath);
            if (index >= 0)
                _sceneColors.RemoveAt(index);
        }
    }
}
