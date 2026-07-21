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

        // Patrol chaser (visual pressure only — it never catches, GDD D7).
        // The camera sits a few metres behind the runner, so the patrol is only on
        // screen inside a narrow band; these are measured from the live camera at
        // runtime rather than hard-coded to a world z (see EndlessRaceDirector).
        public const float PatrolMenaceSeconds = 1.6f;  // after a wrong pick it closes in this long
        public const float PatrolMenaceDanger = 0.85f;  // ...acting as if danger were this high
        // Measured against the race camera (0,4,-3) pitched 15 deg with a 58.7 fov, runner
        // at z=+3 — so the camera is 6m back and 4m up, and its view cone bottoms out
        // 44.4 deg below horizontal. A chaser closer than ~4.1m in front of the camera
        // drops out of frame BELOW it; further than ~2.2m behind it is off-screen entirely.
        // 4.5 puts the whole patrol in the bottom of the frame at max danger.
        public const float PatrolCloseInFront = 4.5f;   // metres in front of the camera at max danger
        public const float PatrolFarBehindCamera = 3f;  // metres behind the camera at zero danger
        public const float PatrolFollowX = 5f;          // lane-match smoothing
        public const float PatrolFollowZ = 3f;          // closing/receding smoothing
        public const float DangerOnWrong = 10f;
        public const float DangerRelief = 15f;   // danger -= on correct pickup
        public const float DangerMax = 100f;
        public const float DangerAfterCaught = 50f;

        // Run pacing (Trash Dash-style): the run gently speeds up so the finish feels fast.
        public const float RaceAccelPerSecond = 0.005f; // +0.5% base speed per second...
        public const float RaceAccelMaxBonus = 0.25f;   // ...capped at +25%

        // Stars (GDD §4.2): 3★ = 5/5 first picks, 2★ = 4/5, 1★ = 3 or fewer
        public const int StarsThreeMin = 5;
        public const int StarsTwoMin = 4;

        // Summary light checks (GDD §4.5)
        public const int SummaryMinWords = 5;
        public const int SummaryMaxChars = 200;
        public const int SummaryMaxNudges = 2;

        // App
        public const int TargetFrameRate = 60;
        public const float SplashSeconds = 2f;
    }
}
