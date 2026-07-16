namespace SummaRace.Constants
{
    /// <summary>All gameplay tuning numbers (GDD §4). Never hard-code these in behaviors.</summary>
    public static class GameRules
    {
        // Race
        public const int MaxLanes = 3;
        public const float LaneWidth = 2.5f;
        public const float LaneSwitchSeconds = 0.15f;
        public const float BoostSeconds = 2f;
        public const float SlowSeconds = 1.5f;
        public const float DangerOnWrong = 10f;
        public const float DangerRelief = 15f;   // danger -= on correct pickup
        public const float DangerMax = 100f;
        public const float DangerAfterCaught = 50f;

        // Stars (GDD §4.2): 3★ = 5/5 first picks, 2★ = 4/5, 1★ = 3 or fewer
        public const int StarsThreeMin = 5;
        public const int StarsTwoMin = 4;

        // Summary light checks (GDD §4.5)
        public const int SummaryMinWords = 5;
        public const int SummaryMaxChars = 200;
        public const int SummaryMaxNudges = 2;

        // App
        public const int TargetFrameRate = 60;
    }
}
