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
