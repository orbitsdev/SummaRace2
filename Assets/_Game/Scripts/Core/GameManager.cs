using System;
using System.Collections.Generic;
using SummaRace.Constants;
using SummaRace.Data;
using UnityEngine;

namespace SummaRace.Core
{
    /// <summary>Outcome of trying to set a learner's participant code — see
    /// <see cref="GameManager.SetParticipantCode"/>. Every failure is reported rather than
    /// silently corrected: this is the researcher's join key, and a code the app "fixed" is
    /// worse than one it refused.</summary>
    public enum ParticipantCodeResult
    {
        Saved,
        /// <summary>No learner to write it to (no [Core], a scene played directly).</summary>
        NoLearner,
        /// <summary>Not 2–12 letters or numbers.</summary>
        Invalid,
        /// <summary>Another learner on this tablet already has it.</summary>
        Duplicate,
        /// <summary>The code was accepted but did not reach disk. Distinct from every outcome
        /// above, because those are all "try again with a different code" and this one is "the
        /// code was fine and is nonetheless not saved". It used to be indistinguishable from
        /// <see cref="Saved"/> — the screen said "Saved" whatever the write did — and this is the
        /// key that joins a child's logs to their paper pretest, so a silent loss here surfaces
        /// during analysis, when nothing can be done about it.</summary>
        SaveFailed,
    }

    /// <summary>
    /// The app's brain — holds shared state across scenes (TDD §7.2).
    /// Created by Bootstrapper, survives scene loads.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public StoryData CurrentStory { get; private set; }

        /// <summary>
        /// Whose stars, unlocks and log rows this play-through belongs to. Read-only from the
        /// outside: it used to be a public setter that nothing ever called, so every device was
        /// permanently profile[0] and a shared tablet merged two children into one unusable
        /// record. Move it with <see cref="SetActiveLearner"/> so the id is persisted and the
        /// in-flight log is closed in the same step.
        /// </summary>
        public LearnerProfile CurrentLearner { get; private set; }

        private readonly List<LearnerProfile> _profiles = new();

        /// <summary>Every learner saved on this tablet, in save order — the teacher's picker
        /// reads this. Read-only: profiles are made by <see cref="CreateLearner"/> so id
        /// generation and persistence stay in one place.</summary>
        public IReadOnlyList<LearnerProfile> Learners => _profiles;

        private int _selectedSession = 1;

        /// <summary>Which session's three stories Story Select shows. Session Map sets it.</summary>
        public int SelectedSession
        {
            get => _selectedSession;
            set => _selectedSession = Mathf.Clamp(value, 1, GameRules.SessionCount);
        }
        public RaceResult LastRaceResult { get; private set; }
        public int LastArrangeAttempts { get; private set; }
        public string LastSummaryText { get; set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>Loads a story JSON, stores it, and routes to the Reader.</summary>
        public void StartStory(string storyId)
        {
            var story = StoryLoader.Load(storyId);
            if (story == null)
            {
                // Friendly fail — never a crash, never a dead end (GDD §11.6).
                Debug.LogWarning($"GameManager: story '{storyId}' failed to load; returning to StorySelect.");
                SceneLoader.Go(SceneNames.StorySelect);
                return;
            }

            CurrentStory = story;
            LastRaceResult = null;
            EventBus.Raise(new StoryStarted { storyId = storyId });
            SceneLoader.Go(SceneNames.Reader);
        }

        public void SetRaceResult(RaceResult result) => LastRaceResult = result;

        public void SetArrangeResult(int attemptCount) => LastArrangeAttempts = attemptCount;

        /// <summary>Stars from first-pick race accuracy (GDD §4.2). Minimum is always 1.</summary>
        public int CalculateStars()
        {
            if (LastRaceResult == null) return 1;
            int correct = LastRaceResult.CountFirstPickCorrect();
            if (correct >= GameRules.StarsThreeMin) return 3;
            if (correct >= GameRules.StarsTwoMin) return 2;
            return 1;
        }

        /// <summary>Best star count earned on a story — 0 when never completed (or no learner yet).</summary>
        public int GetBestStars(string storyId)
        {
            var progress = CurrentLearner?.progress.Find(p => p.storyId == storyId);
            return progress != null ? progress.bestStars : 0;
        }

        /// <summary>Has this story been finished before? Drives replay logging and unlocks.</summary>
        public bool IsCompleted(string storyId)
        {
            var progress = CurrentLearner?.progress.Find(p => p.storyId == storyId);
            return progress != null && progress.completed;
        }

        /// <summary>All three difficulties of a session finished (GDD §3.1).</summary>
        public bool IsSessionComplete(int session)
        {
            for (int i = 0; i < StoryIds.Difficulties.Length; i++)
                if (!IsCompleted(StoryIds.For(session, i))) return false;
            return true;
        }

        /// <summary>
        /// Set when the third story of a session lands, read once by the Session Map so the
        /// celebration plays on arrival and never repeats on a later visit.
        /// </summary>
        private int _justCompletedSession;

        public int ConsumeJustCompletedSession()
        {
            int session = _justCompletedSession;
            _justCompletedSession = 0;
            return session;
        }

        /// <summary>
        /// Loads the saved learners and re-activates whoever was last playing, creating a first
        /// profile when the device has none so progress always has somewhere to persist. Name
        /// Entry lets the learner set their real name and avatar on that same profile.
        /// </summary>
        public void InitProfiles()
        {
            _profiles.Clear();
            if (SaveManager.Instance != null)
                _profiles.AddRange(SaveManager.Instance.LoadProfiles());

            // The id is the only thread tying an exported log row back to a child: SaveManager
            // names the .jsonl after it and the export roster maps it to a name. A blank or
            // duplicated id silently folds two learners into one file, and nothing downstream
            // can undo that — so repair the list on load rather than trusting the file.
            bool repaired = false;
            var seen = new HashSet<string>();
            for (int i = 0; i < _profiles.Count; i++)
            {
                var profile = _profiles[i];
                if (string.IsNullOrEmpty(profile.id) || !seen.Add(profile.id))
                {
                    profile.id = NewLearnerId();
                    seen.Add(profile.id);
                    repaired = true;
                }
            }

            // Canonicalise the participant codes once on load so every later comparison — the
            // duplicate check, the roster, the picker rows — sees one spelling of one code.
            // Deliberately never DEDUPES: a duplicate is the researcher's to resolve against
            // the paper booklets, and a code this app quietly renamed would join to nothing.
            for (int i = 0; i < _profiles.Count; i++)
            {
                var profile = _profiles[i];
                string canonical = ParticipantCodes.Normalize(profile.participantCode);
                if (string.Equals(profile.participantCode, canonical, StringComparison.Ordinal)) continue;
                profile.participantCode = canonical;
                repaired = true;
            }

            if (_profiles.Count == 0)
            {
                _profiles.Add(NewProfile());
                repaired = true;
            }

            // Restore the learner who was holding the tablet before the app was closed. An
            // unknown id (a wipe deleted the profiles, or the file was hand-edited) is never an
            // error — fall back to the first profile and rewrite the pointer below.
            string savedId = LoadActiveLearnerId();
            var saved = string.IsNullOrEmpty(savedId) ? null : _profiles.Find(p => p.id == savedId);
            CurrentLearner = saved ?? _profiles[0];

            if (repaired) PersistProfiles();
            SaveActiveLearnerId(CurrentLearner.id);
        }

        /// <summary>Adds a learner, activates it, and saves. Used by the teacher's picker when a
        /// second child starts on this tablet.</summary>
        public void RegisterLearner(LearnerProfile learner)
        {
            if (learner == null) return;
            if (!_profiles.Contains(learner)) _profiles.Add(learner);
            PersistProfiles();
            SetActiveLearner(learner);
        }

        /// <summary>
        /// Starts another learner on this tablet and makes them active. A profile that was never
        /// named and never played is handed back instead of adding a second one: a teacher who
        /// taps "new learner" twice (or backs out of Name Entry) would otherwise leave empty
        /// rows in the export roster that the researcher has to explain away.
        /// </summary>
        public LearnerProfile CreateLearner()
        {
            var blank = _profiles.Find(p => !p.named && (p.progress == null || p.progress.Count == 0));
            var learner = blank ?? NewProfile();
            RegisterLearner(learner);
            return learner;
        }

        /// <summary>
        /// Hands the tablet to another learner. Teacher-gated on purpose (GDD §8.3): a learner
        /// who could switch themselves could also play as someone else, and every star, unlock
        /// and log row would land on the wrong child — unrecoverable once exported.
        /// </summary>
        public void SetActiveLearner(LearnerProfile learner)
        {
            if (learner == null || !_profiles.Contains(learner)) return;

            if (CurrentLearner == learner)
            {
                SaveActiveLearnerId(learner.id);   // still worth pinning after a fresh install
                return;
            }

            CurrentLearner = learner;
            SaveActiveLearnerId(learner.id);

            // Nothing of the previous learner's play-through may survive the handover: every
            // screen from here reads progress, unlocks and results off whoever is active now.
            CurrentStory = null;
            LastRaceResult = null;
            LastArrangeAttempts = 0;
            LastSummaryText = null;
            _selectedSession = 1;
            _justCompletedSession = 0;

            // SessionLogService listens and closes the run in flight. It never re-stamps that
            // row — the learnerId was written when the story started — so the learner who was
            // just playing keeps their own data, in their own file.
            EventBus.Raise(new LearnerChanged { learnerId = learner.id });
        }

        /// <summary>Writes every profile. Returns false if the change did not reach disk — see
        /// <see cref="SaveManager.SaveProfiles"/>. Callers that TELL SOMEONE the save happened
        /// must check it; callers that merely keep the file current need not.</summary>
        public bool PersistProfiles()
        {
            return SaveManager.Instance != null && SaveManager.Instance.SaveProfiles(_profiles);
        }

        // ---------- The participant code: the study's join key ----------
        // Everything here is called only from the PIN-gated teacher screen. The learner's own
        // screens never read or write it, and Name Entry is untouched: a child still types a
        // name and picks a runner, and the code is the adult's field beside it.

        /// <summary>
        /// Writes the researcher's participant code onto a learner, in canonical form.
        /// Refuses anything unusable and refuses a code another learner on this tablet already
        /// holds — a shared code makes two children indistinguishable in the export, which is
        /// precisely the failure the field exists to prevent, and it cannot be undone once the
        /// tablets are collected. <paramref name="normalized"/> is what was (or would have
        /// been) stored; <paramref name="clashName"/> names the learner already holding it.
        /// </summary>
        public ParticipantCodeResult SetParticipantCode(
            LearnerProfile learner, string raw, out string normalized, out string clashName)
        {
            normalized = ParticipantCodes.Normalize(raw);
            clashName = null;

            if (learner == null || !_profiles.Contains(learner)) return ParticipantCodeResult.NoLearner;
            if (!ParticipantCodes.IsAcceptable(raw)) return ParticipantCodeResult.Invalid;

            var clash = FindLearnerByParticipantCode(normalized, learner);
            if (clash != null)
            {
                clashName = clash.displayName;
                return ParticipantCodeResult.Duplicate;
            }

            // Written to the profile first, then persisted — and the write is CHECKED, because
            // this is the one field whose loss cannot be repaired after the tablets are
            // collected. On failure the in-memory value is rolled back so the screen and the
            // disk cannot disagree: a teacher who is told it did not save must not find it
            // apparently set when they look again.
            string previous = learner.participantCode;
            learner.participantCode = normalized;
            if (!PersistProfiles())
            {
                learner.participantCode = previous;
                return ParticipantCodeResult.SaveFailed;
            }
            return ParticipantCodeResult.Saved;
        }

        /// <summary>The learner holding this code on this tablet, ignoring <paramref name="ignore"/>
        /// (so re-saving a learner's own code is not a clash with themselves). Null when free.</summary>
        public LearnerProfile FindLearnerByParticipantCode(string code, LearnerProfile ignore)
        {
            string wanted = ParticipantCodes.Normalize(code);
            if (wanted.Length == 0) return null;

            for (int i = 0; i < _profiles.Count; i++)
            {
                var profile = _profiles[i];
                if (profile == null || profile == ignore) continue;
                if (string.Equals(ParticipantCodes.Of(profile), wanted, StringComparison.Ordinal))
                    return profile;
            }
            return null;
        }

        /// <summary>How many learners on this tablet have no participant code yet. Non-zero at
        /// export time means rows the researcher cannot tie to a booklet.</summary>
        public int CountLearnersWithoutParticipantCode()
        {
            int missing = 0;
            for (int i = 0; i < _profiles.Count; i++)
                if (!ParticipantCodes.IsSet(_profiles[i])) missing++;
            return missing;
        }

        /// <summary>A code held by two learners on this tablet, or null when all are distinct.
        /// Codes are refused at entry, so this catches only what predates the check or arrived
        /// from a hand-edited save — but it is checked again at export because the cost of
        /// missing it is unrecoverable. Cross-TABLET duplicates are invisible here by
        /// construction: only the researcher's own roster can catch "P07" on two devices.</summary>
        public string FirstDuplicateParticipantCode()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < _profiles.Count; i++)
            {
                string code = ParticipantCodes.Of(_profiles[i]);
                if (code.Length == 0) continue;
                if (!seen.Add(code)) return code;
            }
            return null;
        }

        private static LearnerProfile NewProfile() => new LearnerProfile
        {
            id = NewLearnerId(),
            displayName = GameText.DefaultLearnerName,
        };

        private static string NewLearnerId() => Guid.NewGuid().ToString();

        private static string LoadActiveLearnerId() =>
            SaveManager.Instance != null ? SaveManager.Instance.LoadSettings().activeLearnerId : null;

        /// <summary>Pins the active learner so the next launch resumes the same child rather
        /// than silently reverting to the first profile.</summary>
        private static void SaveActiveLearnerId(string id)
        {
            if (SaveManager.Instance == null) return;
            var settings = SaveManager.Instance.LoadSettings();
            if (string.Equals(settings.activeLearnerId, id, StringComparison.Ordinal)) return;
            settings.activeLearnerId = id;
            SaveManager.Instance.SaveSettings(settings);
        }

        /// <summary>Updates profile progress and writes it to disk.</summary>
        public void CompleteStory(int stars)
        {
            if (CurrentStory == null) return;

            if (CurrentLearner != null)
            {
                var progress = CurrentLearner.progress.Find(p => p.storyId == CurrentStory.id);
                if (progress == null)
                {
                    progress = new StoryProgress { storyId = CurrentStory.id };
                    CurrentLearner.progress.Add(progress);
                }
                progress.completed = true;
                if (stars > progress.bestStars) progress.bestStars = stars;   // never decreases
                PersistProfiles();

                // Was that the third story of the session? The Session Map celebrates it.
                if (IsSessionComplete(CurrentStory.session))
                    _justCompletedSession = CurrentStory.session;
            }

            EventBus.Raise(new StoryCompleted { storyId = CurrentStory.id, stars = stars });
        }
    }
}
