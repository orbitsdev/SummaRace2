namespace SummaRace.Constants
{
    /// <summary>All learner-facing strings — tone-editable in one place (GDD §7.4).</summary>
    public static class GameText
    {
        public const string TapToStart = "TAP TO START";

        public const string SummaryHint =
            "Example: Somebody wanted ___, but ___, so ___, then ___.";

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
