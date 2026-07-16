using UnityEditor;
using UnityEngine;

namespace SceneSwift
{
    public static class SceneManagerSettingsProvider
    {
        const string SettingsPath = "Assets/SceneSwift/Settings/SceneManagerSettings.asset";

        public static SceneManagerSettings GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<SceneManagerSettings>(SettingsPath);
            if (settings != null) return settings;

            if (!AssetDatabase.IsValidFolder("Assets/SceneSwift"))
                AssetDatabase.CreateFolder("Assets", "SceneSwift");
            if (!AssetDatabase.IsValidFolder("Assets/SceneSwift/Settings"))
                AssetDatabase.CreateFolder("Assets/SceneSwift", "Settings");

            settings = ScriptableObject.CreateInstance<SceneManagerSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }
    }
}
