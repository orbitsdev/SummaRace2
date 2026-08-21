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

        /// <summary>
        /// Whether the 3D patrol cop spawns at all. OFF: three separate placements each fixed one
        /// property and broke another (behind = intersected the runner; shoulder = read as a
        /// jogging companion and sat half off a portrait screen; bounds-following = chased its own
        /// run animation and slid back and forth). The cause is the approved chase camera — 5m
        /// back, 4m up, ~15deg — under which a ground-level figure behind the runner is either
        /// inside him or below the frame, and that camera is not up for change.
        /// It carries no rule (it never catches — GDD D7, timesCaught stays 0) and the wrong-answer
        /// beat is already carried by the amber vignette and the feedback line, so nothing is lost.
        /// Turn on only together with a camera change; the tuning constants below are kept so that
        /// work does not start from nothing.
        /// </summary>
        /// <remarks>static readonly, not const: a const false makes every line after the guard
        /// provably unreachable, and the compiler warns on all of it — which would bury the real
        /// warnings in this file behind noise about code we are deliberately keeping.</remarks>
        public static readonly bool RacePatrolEnabled = false;

        /// <summary>
        /// The patrol CAMEO (F59) — a different thing from the chase above, which stays off.
        ///
        /// The chase failed three times for one reason, and it is worth stating precisely
        /// because it is what this design avoids: every version of it tried to hold the cop at a
        /// small offset from the runner, and at a small offset the approved camera (5m back, 4m
        /// up, ~15deg down, 58.7 FOV, portrait) leaves nowhere to put him. Behind = inside the
        /// kid or under the frame; level on the shoulder = a jogging companion, half off a
        /// portrait screen; bounds-following = chasing his own stride.
        ///
        /// The cameo does not hold an offset at all. He appears WELL AHEAD on the shoulder,
        /// draws in to PatrolCameoNearAhead, then falls back out — so the two bodies are never
        /// less than five metres apart ALONG THE RUN and an overlap is impossible by
        /// construction rather than by tuning. That single property is what all three chase
        /// attempts were fighting for and never got.
        ///
        /// Solved against the camera in the scene, not eyeballed. With the cop at
        /// x = PatrolCameoLateralX and z between the two Ahead values, his worst projected
        /// corner sits at 0.81 of the frame half-extent — fully inside, with ~19% margin — for
        /// the whole sweep. He is also outside the outermost answer card (|x| 2.2) and inside
        /// the corridor F54 verified clear of scenery (|x| &lt; 3), so he can neither be mistaken
        /// for something to dodge nor spawn inside a wall.
        ///
        /// He still never catches anybody: timesCaught stays 0 (D7/L3), he carries no rule, and
        /// he is AHEAD of the runner throughout, so there is nothing for him to catch. This is
        /// the kill-switch the blueprint's L4 asks to keep — turn it off and the wrong-answer
        /// beat falls back to the amber vignette and the feedback line exactly as it does today.
        /// </summary>
        public static readonly bool RacePatrolCameoEnabled = true;

        /// <summary>How far out on the shoulder the cameo runs. Outside the outermost answer
        /// card (2.2) and inside the scenery-free corridor (3.0).</summary>
        public const float PatrolCameoLateralX = 2.5f;

        /// <summary>Closest he comes, in metres AHEAD of the runner. Never less than this, which
        /// is what makes a body overlap impossible.</summary>
        public const float PatrolCameoNearAhead = 5f;

        /// <summary>Where he enters and leaves, in metres ahead of the runner.</summary>
        public const float PatrolCameoFarAhead = 10f;

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
        // ⚠️ F55: PatrolSurgeScreenX (0.80 of the half-width AT HIS DEPTH) was measured in play
        // mode and it is the reason the owner kept reporting the cop "inside" something. At the
        // then-shipping gap of 1.75m he sat 3.25m from the lens, where the visible half-width is
        // only 1.03m — so "80% of the half-width" is x = 0.82m, INSIDE the kid's own arm swing
        // (his live bounds are +/-0.84m). Measured during a surge, before the fix:
        //     cop bounds x[0.43,1.25] vs kid x[-0.87,0.87]  => 0.44m of LATERAL OVERLAP
        //     separation along the run: 0.22m  (Bounds.Intersects false, but only just)
        //     screen rects overlapped by 14.4% of the width and 28.7% of the height
        //     only ~27% of the cop was on screen at all (21% off the right edge, 58% below)
        // Two bodies drawn 22cm apart with overlapping silhouettes read as one fused body. The
        // placement is now anchored on the KID (with clearance from live bounds) and on his own
        // SCREEN EDGE, never on a fraction of the road's half-width — see UpdatePatrol.
        //
        // Floor under the along-run gap: the two half-depths plus this, worked out from the live
        // bounds each frame; also the lateral floor (kid half-width + cop half-width + this), so
        // the two can never intersect at any lane, stride or lateral offset.
        public const float PatrolBodyMargin = 0.15f;
        // Rest position, still a fraction of the view half-width at his depth (read off the LIVE
        // camera, so a camera retune carries him with it). 2.20 parked him 3.7m off the centre
        // line at the new depth, which is out among the roadside fences and bins; 1.35 is clear
        // of the frame (1.0 is the edge) while staying on the road surface he runs on.
        public const float PatrolRestScreenX = 1.35f;
        // Surge position: instead of a fraction of the half-width, he is placed so his NEAR
        // SILHOUETTE EDGE clears the kid's far silhouette edge by this much, in normalised
        // half-width units (0.05 = 2.5% of the screen width). Expressed on the screen because
        // the defect was a screen-space one; the world-space floor above is the safety net.
        public const float PatrolScreenClearance = 0.05f;
        // Where his FEET should land in the frame, as a viewport y (0 = bottom edge). The camera
        // pitches 15deg down, so anything closer than ~4.1m in front of the lens is cropped off
        // the bottom — the old 1.75m gap put him at 3.25m and cut 58% of him away. Solving the
        // camera's own projection for "feet at this viewport y" gives a gap of ~0.65m at the
        // shipped camera, i.e. he runs almost abreast of the kid but well out to one side, and
        // the whole cop is in frame instead of a floating head. Derived live, never hard-coded.
        public const float PatrolFeetScreenY = 0.04f;
        public const float PatrolMinGap = 0.30f;        // never in front of the kid's own body
        public const float PatrolMaxGap = 3.00f;        // never so far back he is at the lens
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
        // F55: 12 -> 20. This is a track-LENGTH change (the kind F40c sanctioned); run speed is
        // untouched. It exists to buy the quiet stretch the owner asked for — "only show the hint
        // options when near collection so player can enjoy the game". With the reading window
        // fixed at RacePreviewLeadSeconds, a gate every 12s left 12s of reading in a 12s gap, i.e.
        // the panel was up for the whole run. 20s leaves the window intact AND gives the learner
        // real running: see RaceQuietRunSeconds.
        public const float RaceSecondsPerGate = 20f;

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
        // 300 was sized for 12-second gates. With 20-second gates at the run's top speed a gap is
        // 20 x 1.25 x 30 = 750m, so a 300m clamp would bind on every late gate and silently undo
        // both the reading window and the quiet stretch. The seconds formula already bounds this;
        // 800 is only a backstop against a nonsensical speed.
        public const float RaceMaxGateGap = 800f; // never absurdly long

        // RHYTHM (F57). Every gap in a race used to be the same number, because the formula above
        // is a pure function of difficulty and current speed — so a race was a metronome, and all
        // three races of a session shared the same beat. The gap is now multiplied by a per-gate
        // factor drawn from the story's stable seed, which makes the pacing part of a story's
        // identity instead of a constant.
        //
        // WHY THIS CANNOT COST READING TIME, which is the one thing about gate spacing that is a
        // validity concern and not a taste one. NextGateGap clamps its result to a floor of
        // (RacePreviewLeadSeconds + RaceQuietRunSeconds) seconds of runway, and the clamp is
        // applied AFTER this multiplier. A draw below 1.0 therefore cannot produce a gate that
        // arrives before its options have been readable for the full window — it is absorbed by
        // the floor. Centred on 1.0 so a race is not systematically longer either.
        public const float RaceGateRhythmSpread = 0.20f;

        // ------------------------------------------------------------------------------
        // THE READING WINDOW (F55). The option preview used to appear the moment its gate was
        // PLACED and stay up until the gate resolved. Measured live over a real s01_easy run:
        //     gate 1  panel up 15.02s   (revealed 13.00s before the gate)
        //     gate 2  panel up 10.61s   (revealed 11.87s before the gate)
        //     gate 3  panel up 11.24s   (revealed  6.68s before the gate)
        //     gate 4                    (revealed  9.77s before the gate)
        // — 71% of the whole run with the panel up, which is the owner's complaint; and the lead
        // time was NOT under anyone's control. Placement waits for TrackManager to spawn the
        // segment covering the gate, and it only spawns ~137m ahead, so at gate 3 the learner got
        // 6.68s against the ~11.6s that 102 characters need at 100wpm. The panel was up almost
        // always and still sometimes too briefly.
        //
        // So the preview is now armed when the gate is SCHEDULED (its three options are shuffled
        // then, not at placement) and revealed on a distance derived from the live run speed.
        // That decouples it from the spawn horizon, which is what makes a guaranteed window
        // possible at all.
        //
        // 12s is the 100wpm budget (11.6s) with a little margin. It does NOT meet the ~16.5s a
        // 70wpm reader needs — that remains the owner's open call, but it is now a ONE-CONSTANT
        // change: raise this and the gap floor below follows automatically.
        public const float RacePreviewLeadSeconds = 12f;
        // Seconds of running with NOTHING to read, guaranteed between one gate resolving and the
        // next preview arriving. Enforced as a floor on the gate gap (lead + quiet), so no
        // difficulty setting and no speed can take the breather away — at hard the multiplier
        // would otherwise drop the gap below the reading window itself.
        public const float RaceQuietRunSeconds = 6f;

        /// <summary>
        /// How many seconds before a gate arrives the countdown chip becomes visible. The chip
        /// counts down the REAL arrival of the next SWBST part ("Next part in 12s"), integrated
        /// against the runner's acceleration - it is not a clock on the learner.
        ///
        /// This is the honest replacement for the prototype's 90-second race countdown, which
        /// stays out for good (L1): a global clock scores reading SPEED and pressures exactly
        /// the strugglers the study is about, and it implies a time-out fail state the game does
        /// not have. This one cannot fail anybody - at 0 the gate is simply there and the normal
        /// pick/miss rules apply, unchanged.
        ///
        /// Default matches the reading window, so the chip appears with the options and counts
        /// them down together. THE PLAYTEST LEVER: if anxious readers fixate on it, drop this to
        /// 5 and it only appears for the last five seconds. Set it to 0 to remove the chip
        /// entirely without touching any other code.
        /// </summary>
        public const float RaceGateTimerVisibleSeconds = RacePreviewLeadSeconds;

        // The option panel's arrival cue is NOT tunable from here. EndlessRaceDirector's
        // PulseArrivalGlow writes its four legs out longhand so the pulse frequency stays
        // checkable by reading it (1.85Hz, against a 3Hz photosensitivity ceiling). A constant
        // here duplicated that — and duplicated it wrong, claiming 1.63Hz — while nothing read
        // it, so tuning it would have silently changed nothing. Removed 2026-08-19.

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
        //
        // F55: 130 -> 200. At 130 the whole approach to gate 1 WAS the reading window, so the
        // race opened with the panel already up — the learner's first frame of the race was a
        // wall of text. 200m from a standing start is 17.1s, so the run opens with ~5s of pure
        // running before the first window opens. Still a length change, not a speed change.
        public const float RaceFirstGateDistance = 200f;

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

        /// <summary>
        /// Ceiling on a single frame's delta while the race is running (seconds). Restored when
        /// the race scene ends.
        ///
        /// This is a STUDY-DATA guard, not a feel tweak. TrackManager moves the runner by
        /// `speed * Time.deltaTime`, and Unity's project-wide Maximum Allowed Timestep is
        /// 0.33333334 — so one long frame could advance the world 0.333 x maxSpeed 30 = 10m,
        /// against an answer card's catch window of TriggerDepth 3m plus the runner's ~0.93m
        /// collider, about 3.9m. A single GC spike or Addressables segment instantiation on the
        /// 2GB floor device therefore stepped clean over all three cards, and a tunnelled gate is
        /// recorded by HandleMissedActiveGate as first-pick INCORRECT — a rendering hitch turned
        /// into a research datum on raceFirstPickCorrect, which is the headline measure.
        ///
        /// 0.10 x 30 = 3.0m per frame worst case, inside the 3.9m window with margin, and three
        /// frames' worth at the 30fps target so it never bites a healthy frame. The cost when a
        /// hitch does happen is that the world briefly runs in slow motion instead of teleporting
        /// — which for a runner is the better failure anyway.
        /// </summary>
        public const float RaceMaxDeltaTime = 0.10f;

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

        // MIXING ZONE FAMILIES (F49). Holding one family for the whole run was right about place
        // and wrong about variety: the families are 3 / 4 / 14 prefabs, a ~840m race lays about
        // 55 segments, and five of the ten worlds were drawing all 55 from the same four Suburbs
        // pieces. RaceWorlds now gives each world a primary family plus one or two accents, laid
        // as alternating BLOCKS (see RaceWorlds.Mix for why blocks and not a per-segment draw).
        //
        // The block length in a recipe is a target, not a constant — every block is jittered by
        // this fraction so a race never falls into an audible rhythm, and the three stories of a
        // session (which share a world) do not lay the same street in the same order. The jitter
        // is drawn from the story-seeded RNG, so any one story is still perfectly reproducible,
        // which matters for a thesis instrument: two learners on the same story see the same run.
        public const float RaceZoneMixJitter = 0.28f;
        // Floor under a jittered block, so a bad draw can never produce a one-piece "block" that
        // would read as the per-segment flicker the block design exists to avoid. 27m is the
        // longest single segment in either theme (IndustrialWarehouse02), i.e. one piece minimum.
        public const float RaceZoneMixMinBlockMetres = 28f;

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

        // Stars: 3 stars = 5/5 first picks, 2 stars = 3/5 or 4/5, 1 star = 2 or fewer.
        //
        // The GDD (4.2) sets the 2-star gate at 4 of 5. It is deliberately 3 here, per the owner
        // decision recorded as D4 in SummaRace_Owner_Handover.md. At 4, everything from 3/5
        // down to 0/5 showed the identical one-star screen -- so a learner who summarised
        // three of the five parts correctly got exactly the same feedback as one who got
        // none, thirty times over the study, on the screen whose whole job is telling a
        // child how they did.
        //
        // Display only. Stars are not a logged measure: raceFirstPickCorrect is, it is
        // written straight from the run, and it does not move. Reverting is this one
        // constant plus the matching assertion in ProgressionRuleTests.
        public const int StarsThreeMin = 5;
        public const int StarsTwoMin = 3;

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
        /// <summary>
        /// 30, matching the acceptance target and the race, not 60.
        ///
        /// The device floor is a 2GB Android 8 tablet and the acceptance criterion is 30fps
        /// IN THE RACE — which is the heaviest scene by a wide margin and the only one that
        /// matters. Asking for 60 everywhere else does not make the menus better: nothing on
        /// them moves faster than a tween, and the cost is real. A tablet that renders Boot,
        /// the Session Map, the Reader and Story Select at 60 has been running the SoC hot for
        /// several minutes before the learner ever taps into a story, and a passively-cooled
        /// tablet answers that by throttling — so the frames get spent on the screens that do
        /// not need them and are then unavailable to the one that does.
        ///
        /// It was also already a half-measure: Trash Dash's MusicPlayer forces
        /// Application.targetFrameRate = 30 the moment the race scene loads, and never puts it
        /// back. So the app has been running at two different frame rates depending on which
        /// scene you were in, with the switch owned by a third-party script nobody wired.
        /// This makes the intended rate the one the app actually asks for, everywhere.
        /// </summary>
        public const int TargetFrameRate = 30;
        public const float SplashSeconds = 2f;
    }
}
