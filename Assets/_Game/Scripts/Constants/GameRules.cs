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

        // ------------------------------------------------------------------------------
        // RACE LEGIBILITY. The world cards cannot be read, and the numbers say so.
        //
        // Re-derived from the shipped values, not from a rule of thumb:
        //   camera FOV 58.7 vertical, local (0,4,-3) pitched 14.947deg (MainSummaRace.unity),
        //   parented to the runner 5m ahead of it; laneOffset 1.5 => card 1.425x0.85 with a
        //   1.275x0.73 text box; Fredoka-SemiBold CapLine 63 / PointSize 90 = 0.700em cap;
        //   TMP 3D scales fontSize by 0.1, so cap height = 0.07 * fontSize in metres.
        //   An average 35-character answer auto-sizes to ~1.95pt => a 0.137m cap.
        //   Frame height at distance d is 2*d*tan(29.35deg) = 1.124*d, so on a 10.1" tablet
        //   (21.75cm tall in portrait) viewed at 40cm that cap subtends 227/d arcmin.
        //   The ergonomic floor for comfortable reading is ~20 arcmin => d <= 11.35m from the
        //   LENS; the card sits 3.5m below it, so that is 10.80m along the road, i.e. only
        //   5.8m ahead of the runner.
        //   The run starts at 10 m/s and accelerates 0.2 m/s^2, so it passes gate 1 at
        //   11.5 m/s and gate 5 at ~20.4 m/s:
        //       LEGIBLE TIME PER GATE = 0.50s at gate 1, 0.28s at gate 5.
        //   The three cards of one gate carry ~19 words / ~102 characters, which is ~11.6s of
        //   decoding at 100wpm and ~16.5s at the 70wpm a struggling Grade-4 reader manages.
        //
        // That is a VALIDITY failure, not a polish item: every gate is a forced choice among
        // three lanes and a run-past is recorded as first-pick-incorrect, so if nobody can read
        // the cards then strong and weak summarisers both converge on 1-in-3 and
        // raceFirstPickCorrect — the study's headline measure and the star count — cannot
        // discriminate between them. (F44 removed the card-WIDTH tell that let learners score
        // without reading; that tell was the only thing making the race scoreable, so removing
        // it exposed that the cards had never been legible.)
        //
        // The fix is EndlessRaceDirector's screen-space option preview: the three options are
        // also drawn as normal HUD text, in lane order, identical in weight, in the sky band
        // above the horizon (so it can occlude no road, card, halo, runner or tracker). Reading
        // then costs the whole approach instead of half a second. Speed is deliberately NOT
        // touched — F40c records the owner's steer that the only pacing change asked for was
        // track LENGTH, never speed.
        //
        // Distance to the first gate. 80m was "clear of their starting safe segments" and it
        // still is at 130 — but 80m from a standing start is only 7.4s, the shortest approach
        // of the whole race, on the one gate where the learner has never seen a card before.
        // 130m is 11.6s, which is exactly the 100wpm budget above. This is a track-length
        // change, which is the kind F40c sanctioned.
        public const float RaceFirstGateDistance = 130f;

        // Seconds the race's LEAVE chip stays armed before it disarms itself. Same two-tap
        // shape as the Reader's back button and the teacher screen's destructive actions: one
        // tap can never leave a run. Long enough for a teacher to read the confirm, short
        // enough that it cannot sit armed under a child's next stray tap.
        public const float RaceLeaveConfirmSeconds = 4f;

        // Tap-to-move (EndlessTouchInput). A tap is only a tap if the finger stayed inside this
        // fraction of the screen WIDTH and lifted within this long; anything larger belongs to
        // Trash Dash's own swipe handler, whose threshold is 1% of the screen width — so at 2%
        // the two paths cannot both claim one gesture.
        public const float RaceTapMaxDrag = 0.02f;
        public const float RaceTapMaxSeconds = 0.45f;

        // The "you are here" lane marker is a coloured halo standing out behind the answer card
        // the runner is lined up with. This is the extra HEIGHT (metres, split top and bottom):
        // the marker can only grow upwards and downwards, because sideways there is nowhere to
        // go — lanes are 1.5m apart and a card is 1.425 wide, leaving 0.0375m each side before
        // the marker would be hidden behind the neighbouring card. UpdateLaneSelector pads the
        // width by whatever that gap really allows and no more.
        public const float RaceLaneSelectorHalo = 0.34f;

        // ------------------------------------------------------------------------------
        // RACE WORLD DRESSING (F48). A world used to be light only, so all thirty races ran
        // the same street. RaceWorlds now also names a theme, a zone family and a sky dome;
        // these are the numbers that hold that in place against Trash Dash's own track code.
        //
        // HOLDING A ZONE. Their SpawnNewSegment rotates to the next zone family once the run
        // has covered ThemeData.zones[n].length (500m in both themes), and a five-gate race is
        // ~840m, so a world's family would be swapped out from under it two thirds of the way
        // through. Their only public lever is ChangeZone(), which steps forward one family AND
        // resets the distance counter — so calling it exactly zones.Length times is a no-op on
        // the family and a reset on the counter. Doing that on this interval keeps the counter
        // under 5 x 30 m/s = 150m, well inside the 500m rotation, so it can never fire. Cheap:
        // three integer increments, twelve times a minute.
        public const float RaceZoneHoldSeconds = 5f;

        // REPAINTING THE SKY. Their sky is a vertex-coloured MESH with two variants in the whole
        // project (Day, NightTime), so eight of the ten worlds were showing the identical bright
        // blue sky however the recipe was written — overcast, misty, golden and sunset were all
        // the same clear noon. SummaRace/SkyTint lerps the dome towards the world's sky colour,
        // which is how a blue gradient can become grey or gold at all (a multiply can only ever
        // darken it). At this much the dome keeps its own top-to-horizon gradient — enough shape
        // that it still reads as sky and not as a flat coloured wall behind the road.
        public const float RaceSkyTintDay = 0.55f;
        // The night dome is already the colour it should be, and a night world's sky colour is
        // near-black, so the same strength would flatten it to a void. Just a nudge.
        public const float RaceSkyTintNight = 0.22f;

        // ROADSIDE GREENERY. Three of the ten worlds (bright_park, golden_fields, autumn_lane)
        // name country that neither theme contains — there is no park, field or lane in Trash
        // Dash, only Industrial, Suburbs and Urban. What both themes DO contain is Tree01 and
        // GrassClump, already vertex-coloured and already on the curved unlit shader, so they
        // bend with the world instead of detaching from the bent horizon the way an imported
        // prop would (the F29/F27 trap). Scattering those is not a park, but it is the honest
        // best available without new art.
        //
        // The two lateral bands are the art's OWN convention, read off their prefabs rather
        // than invented: SuburbsHouse01 puts its tree at x = 8.1, and the Urban pieces put
        // their grass clumps at x = 4.0 - 5.4. Lanes are 1.5m apart and an answer card is
        // 1.425 wide, so the nearest greenery is still ~1.5m clear of the outermost card.
        public const float RaceTreeSideMin = 7.2f;
        public const float RaceTreeSideMax = 10.5f;
        public const float RaceGrassSideMin = 3.6f;
        public const float RaceGrassSideMax = 6.2f;
        // Tree01 is 7.68m wide and 11.65m tall at scale 1 — enough to swallow the road if it
        // lands wrong, which is why placement is rejected against the segment's own geometry.
        public const float RaceTreeScaleMin = 0.70f;
        public const float RaceTreeScaleMax = 1.05f;
        public const float RaceGrassScaleMin = 1.0f;
        public const float RaceGrassScaleMax = 2.2f;
        // Tries per prop before giving up on a segment that has no room. 6 is enough that a
        // half-empty verge fills, and low enough that a fully built-up segment costs ~100
        // bounds tests once, on the frame it spawns, and is then done with.
        public const int RaceSceneryPlacementTries = 6;
        // Hard cap per segment whatever the recipe asks for, keeping the recipe's tree:grass
        // ratio. Ten segments are live at once and a tree is two submeshes (leaf + branch), so
        // the busiest recipe (autumn_lane, 6 trees + 8 grass) is bounded at 20 renderers per
        // segment = 200 extra draw calls. That is DRAW CALLS, not memory: Tree01 is 684 tris and
        // GrassClump 78, both already resident because the theme's own segments use them
        // (SuburbsHouse01 ships a Tree01), and both share two materials already in the scene —
        // so the dressing adds ~0 to the RAM figure that matters on the 2GB floor device.
        // UNMEASURED ON DEVICE: no APK has ever been built (see CLAUDE.md Build blockers), so
        // if the race misses 30fps on the tablet, this is the first number to halve.
        public const int RaceMaxSceneryPerSegment = 14;

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
