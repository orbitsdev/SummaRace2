namespace SummaRace.Constants
{
    /// <summary>All learner-facing strings — tone-editable in one place (GDD §7.4).</summary>
    public static class GameText
    {
        public const string TapToStart = "TAP TO START";

        /// <summary>Subtitle under the game title on the Boot splash (TMP rich text —
        /// one playful color per word; shown on Boot and MainMenu).</summary>
        public const string BootTagline =
            "<color=#E84855>Read!</color> <color=#1F8A3B>Race!</color> <color=#7B4FD8>Summarize!</color>";

        // Story select
        public const string StorySelectTitle = "Pick a Story";
        public const string DifficultyEasy = "EASY";
        public const string DifficultyAverage = "AVERAGE";
        public const string DifficultyHard = "HARD";
        public const string LockedLabel = "Locked";
        public const string LockedHint = "Coming in a later session!";

        /// <summary>Ms. Lumi's cheer on the race mission briefing.</summary>
        public const string RaceBriefingLumi = "Ready, runner?";

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

        // Praise lines by star count (index 1..3) — one picked at random per result
        public static readonly string[][] PraiseByStars =
        {
            new string[0],                          // unused
            new[]                                   // 1 star
            {
                "You finished the story!",
                "You made it to the end!",
                "You stayed with it — well done!",
            },
            new[]                                   // 2 stars
            {
                "Wow, you really know this story!",
                "You found most of the story parts!",
                "That was strong reading!",
            },
            new[]                                   // 3 stars
            {
                "Amazing! You found every story part!",
                "Perfect run — every part, first try!",
                "You know this story inside out!",
            },
        };

        // ---------- Ms. Lumi's praise (GDD §7.4 tone) ----------
        // She is the app's voice, so every "correct!" moment draws from here.
        // Deliberately PROCESS praise ("you read carefully") rather than ABILITY
        // praise ("you're so smart") — ability praise makes learners avoid harder
        // tasks, which is the opposite of what a 10-session study wants.
        // Kept short: these render inside a small feedback pill.
        public static readonly string[] PraiseGeneric =
        {
            "Nice one!",
            "That's it!",
            "You got it!",
            "Well spotted!",
            "Exactly right!",
            "Good thinking!",
            "You read that carefully!",
            "That's the one!",
            "Sharp eyes!",
            "You found it!",
            "Yes! Keep going!",
            "Great reading!",
            "You're on a roll!",
            "Spot on!",
            "You figured it out!",
            "Way to go!",
        };

        /// <summary>Praise that names the SWBST part just collected, indexed
        /// S=0 W=1 B=2 S=3 T=4. Reinforces the framework while it encourages.</summary>
        public static readonly string[][] PraiseByElement =
        {
            new[] { "You found the Somebody!", "That's who it's about!" },
            new[] { "That's what they wanted!", "You found the Wanted!" },
            new[] { "You spotted the problem!", "That's what got in the way!" },
            new[] { "That's what they did!", "You found the plan!" },
            new[] { "That's how it turned out!", "You found the ending!" },
        };

        /// <summary>Shown when every Arrange slot is right.</summary>
        public static readonly string[] ArrangePerfectPool =
        {
            "Perfect order!",
            "Every part in its place!",
            "You lined up the whole story!",
            "That's the story, start to finish!",
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
        public const string ReaderWrongFeedback = "Not quite — the green one is the answer!";

        /// <summary>Progress line above the reading card, e.g. "Page 1 / 5".</summary>
        public static string PageProgress(int current, int total) => $"Page {current} / {total}";

        /// <summary>Same badge during the page's question — the learner is no longer
        /// on the page, so it would read as stale.</summary>
        public static string QuestionProgress(int current, int total) => $"Question {current} / {total}";

        /// <summary>Answer-option prefixes. The researcher's source doc writes every
        /// processing question's choices as "A. / B. / C.", so the Reader matches it.</summary>
        public static readonly string[] OptionLetters = { "A.", "B.", "C." };

        // Race briefing — the "get ready" beat between the Reader and the run.
        public const string RaceBriefingTitle = "Your Mission";
        public const string RaceStartLabel = "START!";

        /// <summary>Shown on the START button while the race world is still assembling —
        /// tapping through before then would reveal the runner-kit menus underneath.</summary>
        public const string RaceBriefingWait = "Getting ready...";

        /// <summary>Briefing body. Names the story so the learner knows the run is
        /// about what they just read.</summary>
        public static string RaceBriefingBody(string storyTitle) =>
            $"Collect the 5 story parts of\n\"{storyTitle}\" in order.\n\nSwipe or tap left and right to move!";

        /// <summary>3-2-1-GO! steps. Last entry is treated as the "go" beat.</summary>
        public static readonly string[] RaceCountdown = { "3", "2", "1", "GO!" };

        // Race feedback + banner
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
