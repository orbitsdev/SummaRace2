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
                SceneLoader.Instance.Load(SceneNames.StorySelect);
                return;
            }

            CurrentStory = story;
            LastRaceResult = null;
            EventBus.Raise(new StoryStarted { storyId = storyId });
            SceneLoader.Instance.Load(SceneNames.Reader);
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

        /// <summary>Updates profile progress; SessionLog write comes with Phase I.</summary>
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
                if (stars > progress.bestStars) progress.bestStars = stars;
            }

            EventBus.Raise(new StoryCompleted { storyId = CurrentStory.id, stars = stars });
        }
    }
}
