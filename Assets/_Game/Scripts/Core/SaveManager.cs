using System;
using System.Collections.Generic;
using System.IO;
using SummaRace.Constants;
using SummaRace.Data;
using UnityEngine;

namespace SummaRace.Core
{
    /// <summary>
    /// Reads/writes JSON save files under persistentDataPath (TDD §7.5).
    /// Every write is wrapped — failures raise SaveFailed, never crash.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        [Serializable]
        private class ProfileList { public List<LearnerProfile> profiles = new(); }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private static string PathFor(string file) =>
            Path.Combine(Application.persistentDataPath, file);

        public AppSettings LoadSettings()
        {
            try
            {
                var path = PathFor(PrefKeys.SettingsFile);
                if (File.Exists(path))
                    return JsonUtility.FromJson<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SaveManager: could not read settings ({e.Message}); using defaults.");
            }
            return new AppSettings();
        }

        public void SaveSettings(AppSettings settings)
        {
            TryWrite(PrefKeys.SettingsFile, JsonUtility.ToJson(settings, true));
        }

        public List<LearnerProfile> LoadProfiles()
        {
            try
            {
                var path = PathFor(PrefKeys.ProfilesFile);
                if (File.Exists(path))
                {
                    var list = JsonUtility.FromJson<ProfileList>(File.ReadAllText(path));
                    if (list != null) return list.profiles;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SaveManager: could not read profiles ({e.Message}).");
            }
            return new List<LearnerProfile>();
        }

        public void SaveProfiles(List<LearnerProfile> profiles)
        {
            TryWrite(PrefKeys.ProfilesFile, JsonUtility.ToJson(new ProfileList { profiles = profiles }, true));
        }

        /// <summary>Appends one SessionLog line to logs/&lt;learnerId&gt;.jsonl and flushes now.</summary>
        public void AppendLog(SessionLog log)
        {
            try
            {
                var dir = PathFor(PrefKeys.LogsFolder);
                Directory.CreateDirectory(dir);
                File.AppendAllText(
                    Path.Combine(dir, log.learnerId + ".jsonl"),
                    JsonUtility.ToJson(log) + Environment.NewLine);
            }
            catch (Exception e)
            {
                EventBus.Raise(new SaveFailed { reason = "log: " + e.Message });
            }
        }

        private void TryWrite(string file, string json)
        {
            try
            {
                File.WriteAllText(PathFor(file), json);
            }
            catch (Exception e)
            {
                EventBus.Raise(new SaveFailed { reason = file + ": " + e.Message });
            }
        }
    }
}
