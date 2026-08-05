using System;
using System.Collections.Generic;
using SummaRace.Constants;
using SummaRace.Data;
using UnityEngine;

namespace SummaRace.Core
{
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

        public void PersistProfiles()
        {
            if (SaveManager.Instance != null) SaveManager.Instance.SaveProfiles(_profiles);
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
