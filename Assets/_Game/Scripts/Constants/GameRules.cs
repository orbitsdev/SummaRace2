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
        // Subway-Surfers model: hidden during a clean run, rushes into view close behind on a
        // wrong pick (the "bump") for PatrolMenaceSeconds, then recedes. Placed as a GAP behind
        // the LIVE player each frame (see EndlessRaceDirector.UpdatePatrol) — recenter-proof.
        public const float PatrolMenaceSeconds = 2f;    // after a wrong pick the cop is on-screen this long
        public const float PatrolHiddenBehind = 3f;     // rest gap = camBack + this (behind camera = hidden)
        // The geometry here is tight and worth writing down. The camera sits camBack=5m behind the
        // runner, 4m up, pitched ~15deg with a 58.7deg vertical FOV, so the bottom of the frustum
        // is 44.4deg below horizontal. A cop standing on the ground is only in frame while
        // atan(2.2 / (camBack - gap)) <= 44.4deg, i.e. gap <= ~2.75m. Below ~2.2m he intersects the
        // runner instead: at the old 1.8 he rendered as an unreadable blob growing out of the kid's
        // back with his head cut off. 2.5 is the sweet spot — clear of the runner, head and
        // shoulders still inside the frame. Do not raise past 2.75 or he drops out of the bottom.
        // Measured on screen: 2.5 puts only a sliver of his head in frame, 1.8 grows him out of
        // the kid's back as an unreadable blob. 2.1 is the best available compromise. The real
        // constraint is the camera, so if the chaser ever needs to read properly it needs a
        // camera pull-back, not another number here.
        public const float PatrolSurgeGap = 2.1f;       // bump gap = this far behind the player (close, on-screen)
        public const float PatrolGapSmoothTime = 0.4f;  // SmoothDamp time — eases the cop in/out (natural, not snappy)
        public const float PatrolFollowX = 5f;          // lane-match smoothing
        public const float DangerOnWrong = 10f;
        public const float DangerRelief = 15f;   // danger -= on correct pickup
        public const float DangerMax = 100f;
        public const float DangerAfterCaught = 50f;

        // Run pacing (Trash Dash-style): the run gently speeds up so the finish feels fast.
        public const float RaceAccelPerSecond = 0.005f; // +0.5% base speed per second...
        public const float RaceAccelMaxBonus = 0.25f;   // ...capped at +25%

        // Thinking time between answer gates. The gate distance is derived from this against
        // the run's top speed, then clamped, so every item has enough runway to appear and be
        // read/collected (endless race; tuned per playtest). Run speed itself is left alone.
        public const float RaceSecondsPerGate = 12f;

        // Difficulty in a reading game is how long you get to read before choosing, so the
        // thinking time above is scaled per difficulty. This is the ONLY thing that made
        // easy/average/hard differ beyond the chaser's climb rate: the story's playerSpeed and
        // startingDanger are not read on the endless race path, and its checkpointSpacing (45)
        // is always beaten by RaceMinGateGap. The results stay inside the clamped
        // [RaceMinGateGap, RaceMaxGateGap] band, so no setting can make a card unreadable.
        public const float GateTimeEasy = 1.25f;
        public const float GateTimeAverage = 1f;
        public const float GateTimeHard = 0.8f;
        // The floor was 150, which at the run's real starting speed (10 m/s) swallowed all three
        // difficulties — easy/average/hard every one clamped to 150 for the early gates, so the
        // one thing that makes them differ was inert exactly where it is most felt. 110 lets them
        // separate (15 / 12 / 11s at the start) while still leaving room for a card to appear.
        public const float RaceMinGateGap = 110f; // never shorter than this (always room to appear)
        public const float RaceMaxGateGap = 300f; // never absurdly long

        // Stars (GDD §4.2): 3★ = 5/5 first picks, 2★ = 4/5, 1★ = 3 or fewer
        public const int StarsThreeMin = 5;
        public const int StarsTwoMin = 4;

        // Arrange (GDD §4.4). Correct slots lock, so every verify makes progress — but the
        // story cannot continue until the order is right, which makes this the one screen
        // where being wrong could trap a learner. Two ladders keep it moving:
        //   a hint appears after this many misses on the same piece...
        public const int ArrangeHintAfterMisses = 3;
        //   ...and after this many failed verifies the screen finishes the order WITH the
        //   learner and continues. 4 rather than the 3 used elsewhere (race auto-resolve,
        //   summary accept) because the hint needs 3 misses to fire — so a 4th attempt is
        //   the first one the learner makes with the hint in front of them.
        public const int ArrangeMaxAttempts = 4;

        // Summary light checks (GDD §4.5)
        public const int SummaryMinWords = 5;
        public const int SummaryMaxChars = 200;
        public const int SummaryMaxNudges = 2;

        // Content (GDD §3.1): 10 sessions x 3 difficulties = 30 stories.
        public const int SessionCount = 10;

        // App
        public const int TargetFrameRate = 60;
        public const float SplashSeconds = 2f;
    }
}
