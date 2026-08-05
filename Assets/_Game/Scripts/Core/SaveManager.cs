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

            /// <summary>
            /// The researcher's own participant id, written on that child's paper booklet
            /// (e.g. "P07"). This is the column the results chapter joins on: learnerId is a
            /// guid that appears nowhere on paper, and displayName was typed by a nine-year-old.
            /// Empty means the teacher never set one on this tablet — those rows have no
            /// reliable route back to a pretest score, so an empty value here is a finding,
            /// not a formatting detail.
            /// </summary>
            public string participantCode;

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
                var json = ReadWithRecovery(PrefKeys.SettingsFile);
                if (!string.IsNullOrEmpty(json))
                    return JsonUtility.FromJson<AppSettings>(json) ?? new AppSettings();
            }
            catch (Exception e)
            {
                // Losing settings costs the teacher PIN, so this is worth shouting about even
                // though defaults keep the app running.
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
                var json = ReadWithRecovery(PrefKeys.ProfilesFile);
                if (!string.IsNullOrEmpty(json))
                {
                    var list = JsonUtility.FromJson<ProfileList>(json);
                    if (list != null && list.profiles != null) return list.profiles;
                }
            }
            catch (Exception e)
            {
                // Returning empty here is what makes a corrupt file look like a factory-fresh
                // tablet: the learner is re-created, renamed "Runner" and starts at session 1.
                // The atomic write + .bak recovery above exist so this path stays theoretical.
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

        /// <summary>Writes the learnerId → participantCode → displayName roster beside an
        /// export — one row per learner on this tablet, so a shared device can be split back
        /// into individuals AND every row can be tied to a paper pretest/posttest booklet in a
        /// single lookup. Best-effort: a missing roster must never cost the researcher the logs
        /// themselves.</summary>
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
                        // Canonical form, so the spreadsheet join does not miss on a stray space
                        // or a lower-case letter that survived a hand-edited save.
                        participantCode = ParticipantCodes.Of(profile),
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
                // The .bak/.tmp siblings TryWrite leaves behind hold the SAME learner data, so a
                // wipe that skipped them would leave named children and their progress on a tablet
                // the researcher believes is clean — and this is the post-study erase, which is
                // also the consent promise. Delete every copy of the profiles, not just the primary.
                var profiles = PathFor(PrefKeys.ProfilesFile);
                foreach (var variant in new[] { profiles, profiles + ".bak", profiles + ".tmp" })
                    if (File.Exists(variant)) File.Delete(variant);

                // settings.json itself is deliberately KEPT — it holds the teacher PIN hash, and a
                // post-study data wipe must not hand the tablet back in the un-PINned state, which
                // is the one state a learner can claim the gate from. Its stale copies do go: a
                // .bak could otherwise resurrect a PIN the teacher has since changed or cleared.
                var settings = PathFor(PrefKeys.SettingsFile);
                foreach (var variant in new[] { settings + ".bak", settings + ".tmp" })
                    if (File.Exists(variant)) File.Delete(variant);

                var dir = PathFor(PrefKeys.LogsFolder);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);

                // The runner kit keeps its OWN save beside ours and nothing here knew about it.
                // Trash Dash's PlayerData writes persistentDataPath/save.bin during play (every
                // 300m of a run), carrying that child's rank, coins, highscores and mission
                // state. It survived this wipe entirely — so a tablet the researcher had just
                // erased still held ten sessions of one learner's play. Pseudonymous, so not a
                // disclosure, but this is the one action whose entire purpose is to keep that
                // promise. Deleted by name rather than through their API because PlayerData is
                // their code and may not be alive when a teacher wipes from the menu.
                var runnerSave = PathFor("save.bin");
                if (File.Exists(runnerSave)) File.Delete(runnerSave);
            }
            catch (Exception e)
            {
                EventBus.Raise(new SaveFailed { reason = "delete: " + e.Message });
            }
        }

        /// <summary>
        /// Writes atomically: full content to a temp file, flushed to disk, then swapped into
        /// place in one filesystem operation.
        ///
        /// A plain File.WriteAllText truncates the real file first and then streams into it, so a
        /// tablet that dies, is force-quit, or is yanked off charge mid-write leaves profiles.json
        /// truncated. The read path catches the parse failure and hands back an EMPTY list, so the
        /// learner silently boots as a brand-new child: every star, every unlock and their name
        /// gone, with nothing on screen to say so. In a classroom of 40 tablets over 10 sessions
        /// that is not a hypothetical — and progress is what tells the teacher which session a
        /// learner is on.
        ///
        /// The swap keeps the previous good copy as a .bak, which <see cref="ReadWithRecovery"/>
        /// falls back to, so a corrupt primary costs at most the last write instead of everything.
        /// </summary>
        private void TryWrite(string file, string json)
        {
            var path = PathFor(file);
            var tmp = path + ".tmp";
            var bak = path + ".bak";
            try
            {
                File.WriteAllText(tmp, json);

                if (File.Exists(path))
                {
                    // Replace keeps the old content as the backup in one operation. It throws if
                    // the destination is missing, hence the first-write branch below.
                    File.Replace(tmp, path, bak, true);
                }
                else
                {
                    File.Move(tmp, path);
                }
            }
            catch (Exception e)
            {
                // Last resort: a direct write is still better than losing the change entirely,
                // and it is what this method did before.
                try { File.WriteAllText(path, json); }
                catch (Exception inner)
                {
                    EventBus.Raise(new SaveFailed { reason = file + ": " + inner.Message });
                    return;
                }
                EventBus.Raise(new SaveFailed { reason = file + " (non-atomic fallback): " + e.Message });
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best effort */ }
            }
        }

        /// <summary>Reads a save file, falling back to the .bak written by <see cref="TryWrite"/>
        /// when the primary is missing or unparseable. Returns null when neither yields content,
        /// which every caller already treats as "start fresh".</summary>
        private string ReadWithRecovery(string file)
        {
            var path = PathFor(file);
            try { if (File.Exists(path)) return File.ReadAllText(path); }
            catch (Exception e) { EventBus.Raise(new SaveFailed { reason = "read " + file + ": " + e.Message }); }

            var bak = path + ".bak";
            try
            {
                if (File.Exists(bak))
                {
                    EventBus.Raise(new SaveFailed { reason = file + ": primary unreadable, recovered from backup" });
                    return File.ReadAllText(bak);
                }
            }
            catch { /* fall through — a missing/broken backup is "start fresh", never a crash */ }
            return null;
        }
    }
}
