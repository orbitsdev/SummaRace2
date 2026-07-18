namespace SummaRace.Constants
{
    /// <summary>All learner-facing strings — tone-editable in one place (GDD §7.4).</summary>
    public static class GameText
    {
        public const string TapToStart = "TAP TO START";

        /// <summary>Subtitle under the game title on the Boot splash.</summary>
        public const string BootTagline = "Read! Race! Summarize!";

        // Story select
        public const string StorySelectTitle = "Pick a Story";
        public const string DifficultyEasy = "EASY";
        public const string DifficultyAverage = "AVERAGE";
        public const string DifficultyHard = "HARD";
        public const string LockedLabel = "Locked";
        public const string LockedHint = "Coming in a later session!";

        public const string SummaryHint =
            "Example: Somebody wanted ___, but ___, so ___, then ___.";

        public const string SummaryTitle = "Write your summary!";
        public const string SummaryPlaceholder = "Type your one-sentence summary here...";
        public const string SubmitLabel = "SUBMIT";

        // Gentle nudges shown when a summary needs another try (GDD §4.5)
        public static readonly string[] SummaryNudges =
        {
            "Try writing a little more — use the story parts above!",
            "Almost! Can you say it in one sentence about the Somebody?",
        };

        // Praise lines by star count (index 1..3)
        public static readonly string[] PraiseByStars =
        {
            "",                                     // unused
            "You finished the story! Great job!",   // 1 star
            "Wow, you really know this story!",     // 2 stars
            "Amazing! You found every story part!", // 3 stars
        };

        /// <summary>Small header above the tip card on the loading overlay.</summary>
        public const string LoadingLabel = "Loading...";

        // Results screen
        public const string MainIdeaHeader = "Main Idea";
        public const string NextMissionLabel = "NEXT MISSION";

        // Reader narration toggle
        public const string VoiceOn = "VOICE ON";
        public const string VoiceOff = "VOICE OFF";

        // Reader page flow
        public const string NextLabel = "NEXT";
        public const string NextPageLabel = "NEXT PAGE";
        public const string StartRaceLabel = "START RACE!";
        public const string ReaderCorrectFeedback = "Great job!";
        public const string ReaderWrongFeedback = "Not quite — the green one is the answer!";

        /// <summary>Progress line above the reading card, e.g. "Page 1 / 5".</summary>
        public static string PageProgress(int current, int total) => $"Page {current} / {total}";

        // Race feedback + banner
        public const string RaceCollectFeedback = "You got it!";
        public const string RaceWrongFeedback = "Not quite — the glowing one!";
        public const string RaceFinishBanner = "FINISH!";
        public const string RaceFinishCard = "FINISH";
        public const string RaceRunToFinish = "Run to the FINISH!";

        /// <summary>Race HUD banner, e.g. "Collect: SOMEBODY  1/5".</summary>
        public static string RaceCollectBanner(string elementType, int number, int total) =>
            $"Collect: {elementType}  {number}/{total}";

        // Arrange screen
        public const string ArrangeTitle = "Put the story parts in order!";
        public const string UndoLabel = "UNDO";
        public const string VerifyLabel = "VERIFY ORDER";
        public const string ArrangeIntroStatus = "Tap a story part, then tap its place in the order.";
        public const string ArrangeFillFirst = "Fill every slot first!";
        public const string ArrangePerfect = "Perfect order! Great job!";
        public const string ArrangeHintPrefix = "Hint: ";
        public const string ArrangeAlmost = "Almost! The green ones are locked in — try the others again.";

        // SWBST loading tips (SceneLoader shows one at random, GDD §11.5)
        public static readonly string[] LoadingTips =
        {
            "SOMEBODY is who the story is about.",
            "WANTED tells what the character wished for.",
            "BUT is the problem that got in the way.",
            "SO is what the character did about it.",
            "THEN is how everything turned out.",
        };
    }
}
