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
        public LearnerProfile CurrentLearner { get; set; }

        private readonly List<LearnerProfile> _profiles = new();

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
        /// Loads the saved learners and activates one, creating a first profile when the device
        /// has none so progress always has somewhere to persist. Name Entry lets the learner set
        /// their real name and avatar on that same profile.
        /// </summary>
        public void InitProfiles()
        {
            _profiles.Clear();
            if (SaveManager.Instance != null)
                _profiles.AddRange(SaveManager.Instance.LoadProfiles());

            if (_profiles.Count == 0)
            {
                _profiles.Add(new LearnerProfile
                {
                    id = Guid.NewGuid().ToString(),
                    displayName = GameText.DefaultLearnerName,
                });
                PersistProfiles();
            }

            CurrentLearner = _profiles[0];
        }

        /// <summary>Adds a learner created at Name Entry, activates it, and saves.</summary>
        public void RegisterLearner(LearnerProfile learner)
        {
            if (learner == null) return;
            if (!_profiles.Contains(learner)) _profiles.Add(learner);
            CurrentLearner = learner;
            PersistProfiles();
        }

        public void PersistProfiles()
        {
            if (SaveManager.Instance != null) SaveManager.Instance.SaveProfiles(_profiles);
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
