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

        /// <summary>One line of the companion roster: the id→name key the log rows deliberately
        /// do not carry. Deliberately NOT the whole LearnerProfile — the roster exists to name
        /// a learner, and dumping their progress into it would put the same measures in two
        /// files that could then disagree.</summary>
        [Serializable]
        private class RosterEntry
        {
            public string learnerId;
            public string displayName;
            public int unlockedSession;
            public int storiesCompleted;
        }

        [Serializable]
        private class Roster { public List<RosterEntry> learners = new(); }

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
            // No learner means no file name: the row landed in a file literally called
            // ".jsonl", which ExportLogs' "*.jsonl" glob then folded into the researcher's
            // export. That only happens with no active profile — a scene played directly in
            // the editor — so this is test traffic and must not reach the study data.
            if (log == null || string.IsNullOrEmpty(log.learnerId)) return;

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

        /// <summary>
        /// Combines every learner's log into one timestamped file at the root of
        /// persistentDataPath and returns its full path, so the researcher can pull a single
        /// file per device over USB. Returns null when there is nothing to export.
        /// </summary>
        public string ExportLogs()
        {
            try
            {
                var dir = PathFor(PrefKeys.LogsFolder);
                if (!Directory.Exists(dir)) return null;

                // One file per learner (AppendLog names it after the learnerId), so a tablet
                // shared by two children exports both — the rows stay separable because every
                // row carries its own learnerId.
                var files = Directory.GetFiles(dir, "*.jsonl");
                if (files.Length == 0) return null;

                var stamp = string.Format("{0:yyyyMMdd_HHmm}", DateTime.Now);
                var target = PathFor("export_" + stamp + ".jsonl");
                using (var writer = new StreamWriter(target, false))
                    foreach (var file in files)
                        foreach (var line in File.ReadAllLines(file))
                            if (!string.IsNullOrWhiteSpace(line)) writer.WriteLine(line);

                // Every log row is keyed by learnerId (a guid) and deliberately carries no
                // name, so the rows stay pseudonymised at rest. That left the export
                // unusable on its own: 40 devices of "learnerId":"3f2b9c1e-..." with no way
                // back to a child. Write the roster as a SEPARATE companion file so the
                // researcher gets the mapping without de-pseudonymising the data itself.
                WriteRoster(PathFor("export_" + stamp + "_learners.json"));

                return target;
            }
            catch (Exception e)
            {
                EventBus.Raise(new SaveFailed { reason = "export: " + e.Message });
                return null;
            }
        }

        /// <summary>Writes the learnerId → displayName roster beside an export — one row per
        /// learner on this tablet, so a shared device can be split back into individuals.
        /// Best-effort: a missing roster must never cost the researcher the logs themselves.</summary>
        private void WriteRoster(string path)
        {
            try
            {
                var profiles = LoadProfiles();
                if (profiles.Count == 0) return;

                var roster = new Roster();
                foreach (var profile in profiles)
                {
                    if (profile == null || string.IsNullOrEmpty(profile.id)) continue;
                    int completed = 0;
                    foreach (var progress in profile.progress) if (progress.completed) completed++;
                    roster.learners.Add(new RosterEntry
                    {
                        learnerId = profile.id,
                        displayName = profile.displayName,
                        unlockedSession = profile.unlockedSession,
                        storiesCompleted = completed,
                    });
                }

                File.WriteAllText(path, JsonUtility.ToJson(roster, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SaveManager: could not write the export roster ({e.Message}).");
            }
        }

        /// <summary>Post-study wipe: removes profiles and all logs (GDD §9.4).</summary>
        public void DeleteAllData()
        {
            try
            {
                var profiles = PathFor(PrefKeys.ProfilesFile);
                if (File.Exists(profiles)) File.Delete(profiles);

                var dir = PathFor(PrefKeys.LogsFolder);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            catch (Exception e)
            {
                EventBus.Raise(new SaveFailed { reason = "delete: " + e.Message });
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
