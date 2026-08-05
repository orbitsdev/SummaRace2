namespace SummaRace.Core
{
    // Event payload structs — the full catalog lives in TDD §8.

    public struct AppReady { }

    public struct StoryStarted { public string storyId; }

    public struct PageAnswered
    {
        public int pageIndex;
        public int chosenIndex;
        public bool correct;
    }

    public struct ReadingCompleted { }

    /// <summary>
    /// One answer card touched in the race — raised for every pick, right or wrong, at any gate
    /// (including a re-presented correct card). This is study data, not just juice:
    /// SessionLogService counts wrong picks per SWBST element from it (GDD §8.2), and because a
    /// gate the learner steers past raises nothing at all, its absence is what separates a
    /// wrong ANSWER from a missed GATE. Keep raising it on every pick.
    /// </summary>
    public struct ElementCollected
    {
        public int elementIndex;
        public bool wasCorrect;
    }

    public struct PlayerCaught { }

    public struct RaceCompleted { public RaceResult result; }

    public struct ArrangeVerified
    {
        public bool correct;
        public int attemptCount;
        /// <summary>True when the remaining pieces were placed FOR the learner by the
        /// anti-frustration assist rather than solved. The researcher must be able to tell
        /// those apart directly; inferring it from the attempt count would silently break
        /// the moment the threshold is retuned.</summary>
        public bool assisted;
    }

    public struct SummarySubmitted
    {
        public string text;
        public int nudgeCount;
    }

    public struct StoryCompleted
    {
        public string storyId;
        public int stars;
    }

    public struct SessionUnlocked { public int sessionNumber; }

    /// <summary>
    /// The tablet was handed to a different learner (teacher-gated, GDD §8.3). Raised AFTER
    /// GameManager.CurrentLearner has moved, so listeners read the learner who is playing now.
    /// A log row already in flight keeps the id it was opened with — see SessionLogService.
    /// </summary>
    public struct LearnerChanged { public string learnerId; }

    public struct SaveFailed { public string reason; }

    /// <summary>Result of one race run (built at the finish line, TDD §11.6).</summary>
    [System.Serializable]
    public class RaceResult
    {
        public string[] collectedPieces = new string[5]; // correct text of each element in order
        public bool[] firstPickCorrect = new bool[5];
        public int timesCaught;
        public float runSeconds;

        public int CountFirstPickCorrect()
        {
            int n = 0;
            foreach (var ok in firstPickCorrect) if (ok) n++;
            return n;
        }
    }
}
