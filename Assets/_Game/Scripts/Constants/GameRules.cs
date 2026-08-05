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
        // "Appear only on a bump": out of frame through a clean run, closes in for
        // PatrolMenaceSeconds after a wrong pick, then drops back out. Placed relative to the
        // LIVE player every frame (see EndlessRaceDirector.UpdatePatrol) — recenter-proof.
        public const float PatrolMenaceSeconds = 2f;    // after a wrong pick the cop is on-screen this long
        //
        // WHY HE COMES IN FROM THE SIDE AND NEVER FROM DIRECTLY BEHIND. All measured in play
        // mode, because the obvious placement cannot be made to work in this frame:
        //   * the camera sits 5m behind the runner (runner z=18, camera z=13), 4m up, pitched
        //     14.95deg with a 58.7deg vertical FOV, so the bottom of the frame is 44.3deg below
        //     horizontal — anything nearer than ~2.2m in FRONT OF THE LENS is cropped away under
        //     the bottom of the screen;
        //   * a chaser behind the runner is BETWEEN the runner and that lens, so closing the gap
        //     drags him toward the camera: nearer, lower and bigger, not more visible. Verified
        //     on screen — at a 2.9m gap only the top of his hat is in frame;
        //   * portrait leaves only a 35deg horizontal FOV, so at those depths he is 500-650px
        //     wide on a 1080 screen and shares the kid's screen column whatever the gap — and,
        //     being nearer the lens than the kid, he DRAWS OVER him.
        // There is therefore no gap at which he is both clear of the kid and inside the frame.
        // So he never travels along the kid's column at all: he holds station at a fixed depth
        // beside the road and moves ACROSS the frame — parked out of shot, sliding in to the
        // kid's shoulder on a bump and back out afterwards. That also stops him sweeping through
        // the camera plane at enormous size, which the old back-to-front approach did every surge.
        //
        // The second half of the bug was subtler and is now handled in code rather than by a
        // number: the cop is placed by his TRANSFORM but his rendered mass is not centred on it.
        // Measured live, the offset runs to 0.79m forward and 0.65m sideways and MOVES through
        // the run cycle (the clip translates a 19-part rigid rig; the transform never moves), so
        // every gap set on the transform was up to 0.8m tighter than it read — and always in the
        // direction of the kid. UpdatePatrol now positions the measured BODY, so these numbers
        // mean what they say. Anything derivable from live bounds is no longer a constant here.
        public const float PatrolSurgeGap = 1.75f;      // metres between BODY CENTRES, along the run
        // Floor under that gap: the two half-depths plus this, worked out from the live bounds
        // each frame. Separated along the run by more than their own depths, the cop and the kid
        // cannot intersect at any lane, stride or lateral offset — so the chase can never clip
        // through the learner's character however anything else is retuned.
        public const float PatrolBodyMargin = 0.15f;
        // Where he sits ACROSS the frame, as a fraction of the view's half-width at his depth —
        // read off the LIVE camera, so a camera retune (this one has had three) carries him with
        // it instead of silently stranding him off screen or on top of the kid.
        public const float PatrolSurgeScreenX = 0.80f;  // in shot, at the kid's shoulder
        public const float PatrolRestScreenX = 2.20f;   // parked well outside the frame
        // Which shoulder: whichever side the kid is NOT on. Lanes are 1.5m apart, so below this
        // much lane offset the kid counts as centred and the cop keeps the side he already had —
        // otherwise a lane change would send him sliding across the screen and back.
        public const float PatrolSideDeadband = 0.6f;
        public const float PatrolMoveSmoothTime = 0.35f; // SmoothDamp time for the slide in and out
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

        // The "you are here" lane marker is a coloured halo standing out behind the answer card
        // the runner is lined up with. This is the extra HEIGHT (metres, split top and bottom):
        // the marker can only grow upwards and downwards, because sideways there is nowhere to
        // go — lanes are 1.5m apart and a card is 1.425 wide, leaving 0.0375m each side before
        // the marker would be hidden behind the neighbouring card. UpdateLaneSelector pads the
        // width by whatever that gap really allows and no more.
        public const float RaceLaneSelectorHalo = 0.34f;

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
