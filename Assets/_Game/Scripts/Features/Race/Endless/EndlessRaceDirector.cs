using System.Collections;
using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;

namespace SummaRace.Features.Race.Endless
{
    /// <summary>
    /// SummaRace's SWBST collection layered onto the Trash Dash endless runner.
    /// Exists only in MainSummaRace.unity. While alive it sets EndlessRaceMode.Active
    /// (suppressing their coins/premium/powerups), skips their Loadout/FTUE, places
    /// one answer gate at a time along the generated track (TDD §11.4: sequential
    /// scheduling — a wrong pick or a missed gate re-offers the glowing correct card
    /// until the learner physically collects it, so they always leave holding the 5
    /// correct pieces), resolves picks, then builds a RaceResult and exits to Arrange.
    /// Their scripts are untouched beyond the one TrackManager guard.
    /// NOTE: their GameManager collides with ours by name — everything of ours is
    /// fully qualified (SummaRace.Core.*), do not add `using SummaRace.Core;`.
    /// </summary>
    public class EndlessRaceDirector : MonoBehaviour
    {
        public static EndlessRaceDirector Instance { get; private set; }

        [Header("Visuals (null = grey-box fallback)")]
        [SerializeField] private Sprite worldCardSprite;       // Hyper_Casual_UI rounded rect
        [SerializeField] private Sprite goldPillSprite;        // kit yellow pill — briefing title banner
        [SerializeField] private Sprite greenPillSprite;       // kit glossy green pill — START button
        [SerializeField] private TMP_FontAsset worldLabelFont; // Fredoka-SemiBold SDF
        [SerializeField] private GameObject patrolPrefab;      // _Game/Prefabs/PatrolCop (or PatrolCharacter)
        [SerializeField] private GameObject collectSparkleFxPrefab; // Hovl Star hit — sparkle on a correct pick (TDD §11.4)

        // Roadside greenery for the worlds that name country neither theme contains (F48; see
        // EndlessWorldDressing). These are the THEME'S OWN tree and grass — already vertex
        // coloured, already on Trash Dash's curved unlit shader — so they bend with the world
        // instead of detaching from the bent horizon the way an imported prop does. Serialised
        // here because the dressing component is added at runtime and cannot carry references;
        // all six are optional, and a missing one simply means no greenery.
        [Header("World greenery (F48; null = bare verge)")]
        [SerializeField] private Mesh treeMeshDay;    // Models/Daytime/Tree01
        [SerializeField] private Mesh treeMeshNight;  // Models/NightTime/Tree01Night
        [SerializeField] private Mesh grassMeshDay;   // Models/Daytime/GrassClump01
        [SerializeField] private Mesh grassMeshNight; // Models/NightTime/GrassClumpNight
        [SerializeField] private Material sceneryLeafMaterial;   // Materials/VCOL (submesh 0)
        [SerializeField] private Material sceneryBranchMaterial; // Materials/TreeBranch (submesh 1)
        [SerializeField] private Shader skyTintShader;           // _Game/Art/Shaders/SkyTint

        // Warm gold the collect sparkle is retinted to, matching the story-treasure look.
        private static readonly Color StoryGold = new Color(1f, 0.85f, 0.45f);
        private UnityEngine.UI.Image _vignette; // amber screen-edge danger vignette (TDD §11.5)
        private Sprite _vignetteSprite;        // generated per race entry; freed in OnDestroy

        // Distance to gate 1, and the reason it is not 80 any more, live on
        // GameRules.RaceFirstGateDistance (80m from a standing start was 7.4s — the shortest
        // approach in the race, on the one gate whose options the learner has never seen).
        /// <summary>
        /// How long the run-out to FINISH should last, in SECONDS at the speed actually being
        /// run — not the 30 METRES it used to be.
        ///
        /// Every other gap moved to a seconds-derived formula (see <see cref="NextGateGap"/>)
        /// precisely because their track accelerates: 10 m/s at the start, ~29 m/s by gate 5.
        /// This one was missed, so a fixed 30m shrank from a comfortable run-out into roughly one
        /// second. That is not merely abrupt — <c>FinishRoutine</c> sets <c>_finished</c>, and
        /// the answer-reveal loop exits on it, so a learner who got the LAST element wrong saw
        /// the correct answer for well under its intended <see cref="AnswerRevealSeconds"/>.
        /// The final slot is "Then", which is the one the written summary most depends on.
        ///
        /// Long enough to read the reveal and still feel like an ending; the floor keeps it sane
        /// at the slow speeds an early wrong pick produces.
        /// </summary>
        private const float FinishSeconds = 3.4f;
        private const float FinishMinGap = 30f;
        private const float MissGrace = 5f;          // metres past a gate before it counts as missed
        // Seconds of runway a re-presented gold card gets, rather than a fixed 18 metres. 18m
        // was under a second of warning: the runner is already 2m into the segment, so the card
        // landed ~16m ahead and at 15-28 m/s it appeared and was gone in 0.6-1.0s. The learner
        // could not read it, let alone change lane into it — it read as a gold flicker, three
        // times, and then the element was silently written off.
        private const float RepresentSeconds = 3.2f;
        private const float RepresentMinGap = 45f;   // never closer than this, however slow the run
        private const int MaxRepresentMisses = 6;    // consecutive dodges before anti-frustration auto-resolve
        private const float CardY = 0.5f;
        // Watchdog: seconds with nothing in the world and nothing scheduled before the run is
        // treated as stranded and FINISH is forced back. Long enough that no legitimate
        // placement gap can trip it (placements are scheduled in the same frame they clear).
        private const float StrandedSeconds = 2f;
        // Depth of an answer card's catch volume, along the run. The character is moved in
        // TrackManager.Update, so triggers are sampled once per RENDERED frame, and their
        // MusicPlayer.Awake pins targetFrameRate to 30 — at the scene's maxSpeed of 30 that is
        // a full metre of travel per sample. The old 0.5m (≈1.4m of window once the runner's
        // own collider is counted) left barely one frame of margin at the last gates, and a
        // single dropped frame on the 2GB floor device stepped the card over. That reads as a
        // WRONG ANSWER in the study data (HandleMissedActiveGate records first-pick false),
        // so it is a measurement problem, not just a feel one. The card's visible size is
        // unchanged — F30 sized the card, not the trigger.
        // 1.6, not 2.5: with the runner's own ~0.93m collider that is a ~2.5m catch window,
        // still 2.5 frames of margin at the worst case (30fps x maxSpeed 30 = 1.0m per sample),
        // but the card is "collected" only ~0.8m before contact instead of ~1.25m, and the
        // window in which two lanes' cards can both fire is correspondingly smaller.
        // FINISH got 3m in F43 for exactly this reason; the answer gates were left at 1.6m, which
        // is a 1.27m catch window against 1.0m of travel per frame at 30fps/maxSpeed — a 27%
        // margin. One long frame tunnels the card, and a tunnelled answer gate is recorded as a
        // WRONG ANSWER (HandleMissedActiveGate), i.e. a rendering hitch silently becomes study
        // data. Still well inside the half-lane, so it cannot credit two lanes at once.
        private const float TriggerDepth = 3f;
        // Catch volume WIDTH, deliberately narrower than the visible card (1.425).
        // Measured: half a lane is 0.75, but card 1.425/2 + runner 0.576/2 = 1.0005, so a
        // full-width trigger leaves a 0.5m band each side of every lane centre where the
        // runner is inside TWO cards at once — about one frame of every lane change, which is
        // how a learner steering toward the right card could be credited with the distractor
        // they were leaving. At 0.85: 0.425 + 0.288 = 0.713 < 0.75, so overlapping two lanes
        // is geometrically impossible. A settled runner sits exactly on the lane centre, so
        // 0.85 is still ample; a late swipe now gets a friendly miss and a re-present instead
        // of a phantom wrong answer. The visible card keeps its playtested 1.55x0.85 size.
        private const float TriggerWidth = 0.85f;

        private static readonly System.Reflection.FieldInfo SpeedField =
            typeof(TrackManager).GetField("m_Speed",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        private SummaRace.Data.StoryData _story;
        private float _spawnedDistance;      // cumulative worldLength of spawned segments
        private bool _subscribed;
        private bool _finished;
        private float _runStartTime = -1f;
        private float _lastWorldDistance;

        // Every segment ever spawned, so a scheduled distance can be located inside it.
        // Entries whose segment has been recycled/destroyed are pruned lazily.
        private readonly List<(TrackSegment seg, float start, float end)> _spans = new();

        // One pending placement at a time — resolved into an active gate by TryPlacePending.
        private float _pendingGateDistance = -1f;
        private int _pendingElement;
        private bool _pendingIsRepresent;

        // The upcoming gate's three options, DECIDED WHEN THE GATE IS SCHEDULED rather than when
        // it is placed. The shuffle used to live in PlaceAnswerGate, which meant nothing knew the
        // options until TrackManager had spawned the segment covering the gate — and it only
        // spawns ~137m ahead, so the reading panel could not be shown earlier than that however
        // long the gap was (measured: 6.68s of reading at gate 3 of a real run). Deciding here
        // lets the panel open on a clock instead of on the spawn horizon. The gate builder
        // consumes these arrays verbatim, so panel and road cannot disagree.
        private readonly string[] _gateTexts = new string[3];
        private readonly bool[] _gateCorrect = new bool[3];
        private readonly int[] _gateOptionIndex = new int[3];
        private int _preparedElement = -1;      // which element the arrays above describe
        private bool _preparedIsRepresent;

        // "You are here" marker on the live gate — see BuildLaneSelector.
        private Transform _laneSelector;
        private SpriteRenderer _laneSelectorSr;
        private bool _selectorCentreOnly; // a re-present gate has a card in the centre lane only

        // One active gate at a time.
        private Transform _activeGateRoot;
        private int _activeElement = -1;
        private bool _activeIsRepresent;
        private float _activeGateDistance;
        private float _strandedTimer; // seconds the run has had nothing to present
        // Identity of the live gate. A monotonic serial, never reused, because the element
        // index cannot tell a stale gate from its own re-present (a wrong pick re-presents the
        // SAME element synchronously, so both carry that index). 0 = resolved/none.
        private int _gateSerial;
        private int _activeGateId;

        // Lane-change lean, fed into the runLoop blend tree's `LaneSwitch` parameter.
        private float _laneLean;
        private float? _lastRunnerX;
        // A lane change takes laneOffset/laneChangeSpeed = 1.5/14 = 0.107s. At 4 units/sec the
        // lean could only ever reach 0.43 of the blend range, and it peaked AFTER the kid had
        // already arrived — so the 3-point LaneSwitch tree never left its centre child and the
        // kid slid sideways with an unchanged forward run. 20 swings fully in ~0.05s.
        private const float LeanResponse = 20f; // units/sec toward the target lean
        private float _worldReapplyTimer;      // throttles the pre-race world re-assert

        private readonly bool[] _firstPickDone = new bool[5];
        private readonly bool[] _firstPickCorrect = new bool[5];

        private float _slowTimer;
        private float _savedMaxSpeed = -1f;

        // No continuous danger meter on this path. F39 replaced it with "appear only on a
        // bump": the chaser sits hidden behind the camera through a clean run and rushes
        // into view for _menaceTimer seconds after a wrong pick. The meter it replaced was
        // kept as a running float for a while afterwards but nothing ever read it, so the
        // story's mission.dangerPerSecond/startingDanger are inert here (they are still
        // live on the legacy RaceController). Difficulty on this path is gate spacing.
        // The chaser never ends the run either way — pressure the learner can SEE, never a
        // fail state (GDD D7, and RaceResult.timesCaught stays 0).
        private Transform _patrol;
        private Animator _patrolAnim;
        private float _menaceTimer;
        private float _patrolGroundY;      // fixed run-height captured on activation
        private bool _patrolGrounded;      // has _patrolGroundY been captured yet
        private float _patrolGap = 30f;    // metres the cop trails behind the player (constant once running)
        private float _patrolMoveVel;      // SmoothDamp velocity for his slide in/out of frame
        private float _patrolSide = 1f;    // which shoulder he takes: +1 right, -1 left
        private bool _wasSurging;          // so the shoulder is chosen once per surge, never mid-slide
        private Renderer[] _patrolRenderers; // cached for the per-frame body measurement
        private Renderer[] _kidRenderers;

        private TextMeshProUGUI _bannerText;
        private TextMeshProUGUI _feedbackText;
        private UnityEngine.UI.Image _feedbackPill; // dark backing so the line is readable over the world
        private float _feedbackTimer;

        // Screen-space reading surface for the three options (see GameRules.RaceFirstGateDistance
        // for why the world cards cannot be the reading surface). Same three strings, same lane
        // order, identical styling — it must add no cue the world cards do not have.
        private GameObject _previewRoot;
        private readonly TextMeshProUGUI[] _previewLabel = new TextMeshProUGUI[3];
        private readonly UnityEngine.UI.Image[] _previewPlaque = new UnityEngine.UI.Image[3];
        private readonly RectTransform[] _previewColumn = new RectTransform[3];
        private bool _previewWanted;
        // Armed = there is an upcoming gate whose options are known but whose reading window has
        // not opened yet. The window opens RacePreviewLeadSeconds before the gate (UpdatePreviewWindow).
        private bool _previewArmed;
        private readonly string[] _previewTexts = new string[3];
        private bool _previewIsRepresent;
        private UnityEngine.UI.Image _previewGlow;   // border-only attention cue; never the words
        private Coroutine _previewCue;

        // The answer reveal that replaced the re-presented pickup. A wrong pick used to bring the
        // correct card back alone for the learner to drive into — which is not a choice, teaches
        // nothing, lengthens the run for the learner who is already struggling, and (the owner's
        // words) "doesn't make sense": one lone card rendered as three columns with two dimmed
        // reads as a question that has lost its options, not as an answer. Nothing downstream
        // needed it either — ArrangeController falls back to the story's own text for any piece
        // that was never collected — and it could never count for the measure, because the wrong
        // pick has already closed _firstPickDone. So the answer is simply SHOWN for a beat.
        private Coroutine _answerReveal;
        private bool _revealing;                     // suppresses the normal window while shown
        private const float AnswerRevealSeconds = 2.2f;

        /// <summary>The option preview board's underside, as a fraction of screen height. Derived
        /// in BuildOptionPreview (the horizon sits at 0.7375, so nothing on the road can reach
        /// this band); named here because the HUD banner hangs off it and the two must not be
        /// allowed to drift apart — they were overlapping until F55.</summary>
        private const float PreviewBandBottom = 0.745f;
        /// <summary>The same board's top edge; the SWBST tracker's underside must stay above it.</summary>
        private const float PreviewBandTop = 0.885f;

        /// <summary>
        /// Where the SWBST tracker's UNDERSIDE sits, as a fraction of canvas height.
        ///
        /// The tracker and the pause chip used to be pinned in PIXELS to the top of the screen
        /// (-78 and -72) while the reading band is anchored in FRACTIONS. With
        /// matchWidthOrHeight = 0 the canvas is always 1080 units wide but its height tracks the
        /// aspect, so a fixed pixel offset is a different fraction on every device — and the two
        /// systems collide on anything squarer than 9:16:
        ///
        ///   aspect          canvas H   band top   tracker bottom   pause chip bottom
        ///   9:16  (ref)       1920      220.8       200  clear        216  clear
        ///   16:10 tablet      1728      198.7       200  -1px         216  -17px
        ///   4:3   tablet      1440      165.6       200  -34px        216  -50px
        ///   20:9  phone       2400      276.0       200  clear        216  clear
        ///
        /// The preview board is built after the tracker, so it DRAWS OVER it: on a 4:3 tablet it
        /// covered the bottom 34px of the board, which is the bottom ~22% of every plaque — the
        /// tracker's five words clipped through the middle of their descenders, on the widget
        /// that teaches the framework. It also buried 50px of the pause chip, which is the only
        /// way out of a run. Cheap Android 8 tablets are commonly 4:3 or 16:10, and the study
        /// device is a tablet.
        ///
        /// Anchoring the BOTTOM edge to a fraction makes the relationship aspect-independent: the
        /// clearance to the band is now a constant fraction of the canvas rather than a number
        /// that happened to work at one aspect ratio. Both values are the old pixel positions
        /// expressed against the 1920 reference, so at 9:16 the layout is byte-for-byte what it
        /// was — this can only improve other aspects, never regress the one that was playtested.
        /// (200/1920; the board grows upward from here, away from the band.)
        /// </summary>
        private const float TrackerBottomY = 1f - 200f / 1920f;   // 0.89583

        /// <summary>The pause chip's underside, on the same reasoning as
        /// <see cref="TrackerBottomY"/>. 216/1920 — slightly lower than the tracker because the
        /// chip is 144 tall against the board's 122, and it must clear the band by itself.</summary>
        private const float PauseChipBottomY = 1f - 216f / 1920f; // 0.8875

        // Pause. Never a fail state and never an ending — see OpenPause.
        private GameObject _pauseRoot;      // full-screen overlay, its own canvas above everything
        private GameObject _pauseChip;      // the small control that opens it
        private RectTransform _pauseChipRect;
        private TextMeshProUGUI _leaveLabel;
        private bool _paused;
        private bool _leaving;              // exit in flight; nothing may run after this
        private bool _leaveArmed;
        private float _leaveArmedAt;
        private bool _wasMovingBeforePause;


        // Persistent SWBST inventory tracker (top strip): 5 slots, current pulses, collected
        // fill in-order. Teaches the framework and answers "what to collect next" (F40).
        private readonly UnityEngine.UI.Image[] _slotBg = new UnityEngine.UI.Image[5];
        private readonly TextMeshProUGUI[] _slotLabel = new TextMeshProUGUI[5];
        private readonly RectTransform[] _slotRect = new RectTransform[5];

        // Briefing gate: the world is held still until the learner taps START and the
        // 3-2-1-GO! lands. _briefingDismissed blocks a double tap; _runReleased is what
        // Update() checks, so the track stays stopped through the countdown too.
        private GameObject _briefingRoot;
        private bool _briefingDismissed;
        private bool _runReleased;
        // START stays locked until Start() has finished hiding their Loadout/HUD. The
        // briefing goes up on frame one but HideTheirChrome runs ~1.25s later, so a fast
        // tap used to pull the scrim away and expose the runner-kit menus underneath.
        private bool _bootReady;
        private UnityEngine.UI.Button _startButton;
        private TextMeshProUGUI _startLabel;
        private TextMeshProUGUI _briefingBody;

        /// <summary>
        /// How long the boot chain gets before the briefing offers a way out.
        ///
        /// Both of the waits this bounds used to be `while (x == null) yield return null` — no
        /// timeout, on a screen that has NO working control until they finish. The briefing's
        /// START is non-interactable until MarkBriefingReady, the pause chip is not shown until
        /// the run is released, the touch input no-ops without a TrackManager, and Android BACK
        /// is swallowed app-wide. So a stall there is a total dead end: the only exit is an adult
        /// force-quitting from Recents, which files the run as abandoned.
        ///
        /// And it is not a remote possibility. TrackManager's GameObject is activated only at the
        /// END of their Begin(), which `yield break`s early if the Addressables character load
        /// returns null — and Addressables content has never been built for Android, so the FIRST
        /// APK takes exactly that path on every learner's first race. The same wait also covers a
        /// failed load on the 2GB floor device.
        ///
        /// Generous, because a slow first Addressables load on a cheap tablet is normal and
        /// bailing out early would be its own bug. The neighbouring ThemeDatabase wait was
        /// already bounded for the same reason; these two were simply missed.
        /// </summary>
        private const float BootWaitSeconds = 8f;
        private GameState _gameState; // cached by HideTheirChrome for the Update() re-hide guard

        private void Awake()
        {
            Instance = this;
            EndlessRaceMode.Active = true;
            MaskLoadoutFlash(); // before any OnEnable, incl. GameManager -> LoadoutState.Enter()
        }

        private void OnDestroy()
        {
            EndlessRaceMode.Active = false;
            // Close an open pause in the log rather than losing it: the row's paused-seconds
            // must add up even if the scene ends while the pause screen is still on top.
            if (_paused)
            {
                _paused = false;
                SummaRace.Core.EventBus.Raise(new SummaRace.Core.RacePauseChanged { paused = false });
            }
            if (Instance == this) Instance = null;
            if (_subscribed && TrackManager.instance != null)
                TrackManager.instance.newSegmentCreated -= OnNewSegment;

            // The vignette sprite is generated fresh on every race entry (unlike the wood
            // plaque, which is statically cached), and its texture is owned by no asset, so
            // nothing reclaims it until an incidental Resources.UnloadUnusedAssets. Three
            // stories a session across ten sessions adds up on the 2GB floor device, where
            // texture memory is the real pressure. Free it explicitly.
            if (_vignetteSprite != null)
            {
                var tex = _vignetteSprite.texture;
                Destroy(_vignetteSprite);
                if (tex != null) Destroy(tex);
                _vignetteSprite = null;
            }

            // Never hand the rest of the app a frozen clock. Their GameState.Pause sets
            // Time.timeScale = 0 and AudioListener.pause = true on focus loss, and the only
            // thing that undid it was our OnApplicationFocus — which dies with this scene.
            // SceneLoader's fade runs on unscaledDeltaTime, so a scene change completes
            // perfectly well while paused: background the tablet during the victory beat or
            // the load and the learner returns to Arrange with timeScale still 0 — clicks
            // register, nothing animates, everything is silent, for the rest of the session
            // until the app is restarted.
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        private IEnumerator Start()
        {
            // LoadoutState.Enter() (GameManager.OnEnable, which runs after our Awake but
            // before this coroutine body) unconditionally re-shows TutorialOverlay via
            // tutorialBlocker.SetActive(!tutorialDone) — true on a fresh save since we
            // haven't forced tutorialDone yet at that point. Re-mask before anything else
            // so the two hides collapse into zero visible flash (nothing renders between them).
            MaskLoadoutFlash();

            var ourGm = SummaRace.Core.GameManager.Instance;
            _story = (ourGm != null && ourGm.CurrentStory != null)
                ? ourGm.CurrentStory
                : SummaRace.Data.StoryLoader.Load("s01_easy");

            // One visual recipe per session day, shifted by difficulty, so no two of the 30
            // races look alike (RaceWorlds / AssetGeneration README §1-2).
            SummaRace.Features.Race.RaceWorlds.Apply(_story.world, _story.difficulty);

            SilenceOurMenuMusic();
            DeduplicateTheirMusicPlayer();

            _pendingGateDistance = SummaRace.Constants.GameRules.RaceFirstGateDistance;
            _pendingElement = 0;
            _pendingIsRepresent = false;
            BuildHud();
            // After BuildHud, because arming fills the panel it just built.
            PrepareGateOptions(0, false);
            UpdateBanner();
            // Up before their Loadout/track boot so the learner never sees the spin-up:
            // the scrim doubles as the mask for it (the old MaskLoadoutFlash only hides
            // their canvas children, not the frames where the track is assembling).
            BuildBriefing();

            // Skip their FTUE/tutorial run.
            float pdWait = 0f;
            while (PlayerData.instance == null && pdWait < BootWaitSeconds)
            { pdWait += Time.deltaTime; yield return null; }
            if (PlayerData.instance == null) { ShowBriefingEscape("PlayerData"); yield break; }
            PlayerData.instance.tutorialDone = true;
            if (PlayerData.instance.ftueLevel < 2) PlayerData.instance.ftueLevel = 2;

            // WHERE this race happens, not just what colour the light is (F48). Both halves have
            // to land before their Begin() runs: it reads the theme once and never looks again,
            // and its very first Update spawns ten segments from whatever zone is current.
            var place = SummaRace.Features.Race.RaceWorlds.For(_story.world);
            // Bounded, because a race that never starts is far worse than a race in the wrong
            // theme: if their database is slow, SelectTheme leaves their default alone and the
            // world still gets its light, fog, sky and greenery.
            float themeWait = 0f;
            while (!ThemeDatabase.loaded && themeWait < 2f) { themeWait += Time.deltaTime; yield return null; }
            EndlessWorldDressing.SelectTheme(place);

            var dressing = GetComponent<EndlessWorldDressing>();
            if (dressing == null) dressing = gameObject.AddComponent<EndlessWorldDressing>();
            dressing.Configure(place, new EndlessWorldDressing.WorldArt
            {
                treeDay = treeMeshDay,
                treeNight = treeMeshNight,
                grassDay = grassMeshDay,
                grassNight = grassMeshNight,
                leaf = sceneryLeafMaterial,
                branch = sceneryBranchMaterial,
                skyTint = skyTintShader,
            }, _story.id != null ? _story.id.GetHashCode() : 0);

            // Jump their Loadout menu straight into the run. Their TrackManager GameObject
            // stays inactive until GameState.Enter -> StartGame -> Begin() activates it,
            // so the instance wait MUST come after this call (waiting first deadlocks).
            yield return new WaitForSeconds(0.75f); // let Loadout.Enter settle
            var loadout = FindAnyObjectByType<LoadoutState>();
            if (loadout != null && loadout.isActiveAndEnabled) loadout.StartGame();

            // Safe to subscribe here: instance is set by Awake on activation, and the first
            // newSegmentCreated fires at least a frame later (Addressables instantiation).
            float tmWait = 0f;
            while (TrackManager.instance == null && tmWait < BootWaitSeconds)
            { tmWait += Time.deltaTime; yield return null; }
            if (TrackManager.instance == null) { ShowBriefingEscape("TrackManager"); yield break; }
            TrackManager.instance.newSegmentCreated += OnNewSegment;
            _subscribed = true;
            SeedExistingSegments();
            SpawnPatrol();
            // PC: WASD alongside the arrow keys their controller already binds.
            if (GetComponent<EndlessKeyboardInput>() == null)
                gameObject.AddComponent<EndlessKeyboardInput>();
            // Touch: tap a third of the screen to go to that lane. Their swipe path still
            // works; see EndlessTouchInput for why the two cannot claim the same gesture.
            var tap = GetComponent<EndlessTouchInput>();
            if (tap == null) tap = gameObject.AddComponent<EndlessTouchInput>();
            tap.SetBlockers(_pauseChipRect);

            yield return new WaitForSeconds(0.5f);
            HideTheirChrome();
            EnsureSingleAudioListener();
            MarkBriefingReady(); // only now is it safe to take the scrim away
        }

        private void Update()
        {
            // Unscaled and ahead of every other guard: the LEAVE chip is armed while the world
            // is frozen at timeScale 0, so a scaled clock would leave it armed for ever and the
            // next stray tap on that corner would end the run.
            if (_leaveArmed &&
                Time.unscaledTime - _leaveArmedAt > SummaRace.Constants.GameRules.RaceLeaveConfirmSeconds)
                DisarmLeave();

            // Paused: the world, their scripts and ours are all held. Nothing below this line
            // may run — the pass-by check, the stranded watchdog and the pending placement all
            // reason about a moving track.
            if (_paused || _leaving) return;

            if (_feedbackTimer > 0f && _feedbackText != null)
            {
                _feedbackTimer -= Time.deltaTime;
                if (_feedbackTimer <= 0f)
                {
                    _feedbackText.text = "";
                    if (_feedbackPill != null) _feedbackPill.gameObject.SetActive(false);
                }
            }

            var track = TrackManager.instance;
            if (track == null || _finished) return;

            // Hold the world at the start line until the countdown finishes. Re-asserted
            // every frame rather than stopped once, because their StartGame -> WaitToStart
            // coroutine sets isMoving on its own schedule after we would have stopped it.
            if (!_runReleased)
            {
                // Their theme system writes RenderSettings.fogColor when the track's theme
                // loads, which lands AFTER our world is applied and repainted a night world's
                // fog near-white. Re-assert until the run starts, by which point their theme
                // has settled. Throttled: Apply does a GameObject.Find for the sun, and the
                // briefing can sit on screen for many seconds on the 2GB floor device — four
                // times a second is plenty to win a race against a one-shot theme load.
                _worldReapplyTimer -= Time.deltaTime;
                if (_worldReapplyTimer <= 0f)
                {
                    _worldReapplyTimer = 0.25f;
                    SummaRace.Features.Race.RaceWorlds.Apply(_story.world, _story.difficulty);
                }

                if (track.isMoving) track.StopMove();
                // Idle-hold is done in LateUpdate (it must be the final word before the frame
                // renders — their WaitToStart coroutine flips to run AFTER Update).
                return;
            }

            // Their death popup's Run Again (or Loadout->RUN!) rebuilds the track from zero
            // and destroys every gate with its segments — reload the race so the learner
            // never runs a gate-less road (never a dead end).
            if (track.worldDistance < _lastWorldDistance - 1f)
            {
                _finished = true; // block double-triggering while the load happens
                SummaRace.Core.SceneLoader.Go(SummaRace.Constants.SceneNames.RaceEndless);
                return;
            }
            _lastWorldDistance = track.worldDistance;

            // Never punish (GDD D7): obstacle hits still stumble + blink (their
            // friendly 2s invincibility beat) but can never stack to a game over.
            var runner = track.characterController;
            if (runner != null && runner.currentLife < runner.maxLife)
                runner.currentLife = runner.maxLife;

            if (_runStartTime < 0f && track.isMoving) _runStartTime = Time.time;

            // Patrol placement moved to LateUpdate: it reads the runner's transform, which
            // TrackManager writes in ITS Update, and no script execution order is defined
            // between them. Running first meant placing the cop against LAST frame's player
            // position — a rubber-band while it is on screen, and a one-frame ~100m teleport
            // on the floating-origin recenter that happens roughly every 100m of run.
            UpdateRunnerLean(track);

            // TDD §11.4 slow-on-wrong: public-API clamp, decays back to their own max.
            if (_slowTimer > 0f)
            {
                _slowTimer -= Time.deltaTime;
                if (_slowTimer <= 0f && _savedMaxSpeed > 0f)
                {
                    track.maxSpeed = _savedMaxSpeed;
                    _savedMaxSpeed = -1f;
                }
            }

            // Pass-by: the learner ran past the active gate/re-present without collecting it.
            // FINISH is included: it is a trigger like any other and can be tunnelled through
            // at top speed, and unlike an answer gate a missed FINISH ends the run nowhere.
            if (_activeGateRoot != null && _activeElement >= 0 &&
                track.worldDistance > _activeGateDistance + MissGrace)
            {
                if (_activeElement >= 5) RescheduleFinish(track);
                else HandleMissedActiveGate(track);
            }

            // Keep the "you are here" marker on whichever card the runner is lined up with.
            // Not for FINISH (element 5): that card spans all three lanes, so there is nothing
            // to choose between and a marker would only add noise to the last beat of the run.
            if (_activeGateRoot != null && _activeElement >= 0 && _activeElement < 5)
                UpdateLaneSelector(track);

            TryPlacePending();
            // After TryPlacePending, so a gate that was just placed can open its own window in
            // the same frame — that is the case where the track spawned late and the learner is
            // already inside the reading distance.
            UpdatePreviewWindow(track);
            CheckStranded(track);

            // Their Resume() unconditionally re-shows the pause button after a
            // focus-loss pause cycle — keep it hidden.
            if (_gameState != null && _gameState.pauseButton != null &&
                _gameState.pauseButton.gameObject.activeSelf)
                _gameState.pauseButton.gameObject.SetActive(false);
        }

        // ---------- gate spawning / scheduling ----------

        /// <summary>
        /// Adopt any segments that already existed when we subscribed.
        ///
        /// `_spawnedDistance` assumes we witness EVERY segment from the first one. We subscribe
        /// after loadout.StartGame(), and today that happens to be safe only by ordering — their
        /// Begin() activates the TrackManager from a coroutine, so its first Update (which starts
        /// the spawn coroutines) is a frame later. If that ordering ever shifts and we miss one
        /// segment, every gate is placed L metres beyond where the miss-check believes it is, so
        /// gates are destroyed and rescheduled before the learner can reach them — forever, and
        /// CheckStranded never fires because something is always pending. Cheap insurance.
        /// </summary>
        private void SeedExistingSegments()
        {
            var track = TrackManager.instance;
            if (track == null || track.segments == null) return;
            foreach (var seg in track.segments)
            {
                if (seg == null) continue;
                float start = _spawnedDistance;
                _spawnedDistance = start + seg.worldLength;
                _spans.Add((seg, start, _spawnedDistance));
            }
        }

        private void OnNewSegment(TrackSegment segment)
        {
            float segStart = _spawnedDistance;
            float segEnd = segStart + segment.worldLength;
            _spawnedDistance = segEnd;
            _spans.Add((segment, segStart, segEnd));

            TryPlacePending();
        }

        /// <summary>Resolves the single pending placement (if any) into an active gate as
        /// soon as a spawned segment covers it. If the covering segment was recycled before
        /// we got here, or the target is behind the farthest still-alive span, clamp to the
        /// farthest span's end minus 2 m so the gate is never left unplaceable. If the target
        /// is still ahead of everything spawned so far, wait for the next segment.</summary>
        private void TryPlacePending()
        {
            for (int i = _spans.Count - 1; i >= 0; i--)
                if (_spans[i].seg == null) _spans.RemoveAt(i);

            if (_pendingGateDistance < 0f || _activeGateRoot != null || _spans.Count == 0) return;

            TrackSegment farthestSeg = null;
            float farthestStart = 0f, farthestEnd = float.MinValue;
            TrackSegment coveringSeg = null;
            float coveringStart = 0f;

            foreach (var span in _spans)
            {
                if (span.end > farthestEnd)
                {
                    farthestEnd = span.end;
                    farthestStart = span.start;
                    farthestSeg = span.seg;
                }
                if (_pendingGateDistance >= span.start && _pendingGateDistance < span.end)
                {
                    coveringSeg = span.seg;
                    coveringStart = span.start;
                }
            }

            TrackSegment placeSeg;
            float placeStart, placeDist;
            if (coveringSeg != null)
            {
                placeSeg = coveringSeg;
                placeStart = coveringStart;
                placeDist = _pendingGateDistance;
            }
            else if (_pendingGateDistance < farthestEnd)
            {
                placeSeg = farthestSeg;
                placeStart = farthestStart;
                placeDist = farthestEnd - 2f;
            }
            else
            {
                return; // beyond spawned track — retry on the next segment
            }

            int element = _pendingElement;
            bool isRepresent = _pendingIsRepresent;
            _activeGateDistance = placeDist;
            _activeElement = element;
            _activeIsRepresent = isRepresent;
            _activeGateId = ++_gateSerial; // fresh identity; every card below is stamped with it
            _pendingGateDistance = -1f;

            float local = placeDist - placeStart;
            // No re-present branch any more: a wrong pick or a missed gate now shows the answer
            // on the panel (ShowAnswerReveal) instead of sending a lone gold card back down the
            // road, so every gate placed here is a real three-option gate.
            if (element >= 5) PlaceFinishGate(placeSeg, local);
            else PlaceAnswerGate(placeSeg, local, element);
        }

        private void PlaceAnswerGate(TrackSegment segment, float localDist, int elementIndex)
        {
            Vector3 pos; Quaternion rot;
            segment.GetPointAtInWorldUnit(localDist, out pos, out rot);

            var root = new GameObject("SwbstGate_" + elementIndex).transform;
            root.SetParent(segment.transform, true); // dies with the segment on recycle
            root.SetPositionAndRotation(pos, rot);
            root.gameObject.AddComponent<EndlessCurveDip>();
            _activeGateRoot = root;

            float laneOffset = TrackManager.instance.laneOffset;
            float cardWidth = Mathf.Min(1.55f, laneOffset * 0.95f);

            // The lane shuffle happened when this gate was SCHEDULED (PrepareGateOptions), so the
            // reading panel could open long before the segment carrying the gate existed. Re-run
            // it here only if something scheduled a gate without preparing it — the arrays are
            // the single source of truth for both the road and the panel.
            if (_preparedElement != elementIndex || _preparedIsRepresent)
                PrepareGateOptions(elementIndex, false);

            var texts = _gateTexts;
            var correctFlags = _gateCorrect;
            var optionIndices = _gateOptionIndex;

            for (int lane = 0; lane < 3; lane++)
            {
                bool isCorrect = correctFlags[lane];
                string text = texts[lane];
                var card = BuildCard(root, new Vector3((lane - 1) * laneOffset, CardY, 0f),
                    new Vector2(cardWidth, 0.85f), text, Color.black, Color.white, 2.4f);

                var trigger = card.gameObject.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(TriggerWidth, 2.2f, TriggerDepth);
                // Card sits on the road now — lift the catch volume over the character's body.
                trigger.center = new Vector3(0f, 0.6f, 0f);

                var pickup = card.gameObject.AddComponent<EndlessOptionPickup>();
                pickup.elementIndex = elementIndex;
                pickup.gateId = _activeGateId;
                pickup.isCorrect = isCorrect;
                pickup.optionIndex = optionIndices[lane];
                pickup.lane = lane;
                pickup.optionText = text;
            }
            BuildLaneSelector(root, elementIndex, new Vector2(cardWidth, 0.85f), laneOffset, false);
            // The cards themselves are physically unreadable at any distance worth reading them
            // at (the derivation is on GameRules.RaceFirstGateDistance) — the HUD preview is
            // where the learner actually reads the three options. It was ARMED when this gate was
            // scheduled and may already be on screen; arming again is a no-op that keeps the
            // panel correct if a gate ever reaches the world without having been scheduled.
            ArmOptionPreview(texts, false);
            // No in-world type pill: the top SWBST tracker now shows the current element (F40).
        }

        /// <summary>
        /// Decides the three options for a gate — WHICH lane holds the correct answer and which
        /// distractor sits where — at SCHEDULING time, so the reading panel can be filled long
        /// before TrackManager has spawned the segment the gate will live in.
        ///
        /// The shuffle itself is unchanged and must stay: it used to be a fixed 1,0,2,1,0 pattern,
        /// identical for every story and every run, which let a learner score 5/5 by position
        /// without reading a card. The race is a measure (raceFirstPickCorrect is logged research
        /// data), so position must carry no information. Same Fisher-Yates the legacy
        /// RaceController used. `_gateOptionIndex` carries which option of the STORY JSON each
        /// lane ended up holding (0 = correct, 1/2 = distractors) through the same swaps: without
        /// it the log can say the learner was wrong at "But" but never which wrong idea they took.
        /// </summary>
        private void PrepareGateOptions(int elementIndex, bool isRepresent)
        {
            _preparedElement = elementIndex;
            _preparedIsRepresent = isRepresent;
            if (_story == null || elementIndex < 0 || elementIndex >= _story.elements.Length)
            {
                _gateTexts[0] = _gateTexts[1] = _gateTexts[2] = null;
                return;
            }
            var element = _story.elements[elementIndex];

            if (isRepresent)
            {
                // A re-present is ONE card in the centre lane. The panel says so honestly rather
                // than pretending there are still three options (see ArmOptionPreview).
                _gateTexts[0] = null; _gateTexts[1] = element.correct; _gateTexts[2] = null;
                _gateCorrect[0] = false; _gateCorrect[1] = true; _gateCorrect[2] = false;
                _gateOptionIndex[0] = -1; _gateOptionIndex[1] = 0; _gateOptionIndex[2] = -1;
                ArmOptionPreview(_gateTexts, true);
                return;
            }

            _gateTexts[0] = element.correct;
            _gateTexts[1] = element.distractors[0];
            _gateTexts[2] = element.distractors[1];
            _gateCorrect[0] = true; _gateCorrect[1] = false; _gateCorrect[2] = false;
            _gateOptionIndex[0] = 0; _gateOptionIndex[1] = 1; _gateOptionIndex[2] = 2;
            for (int i = 2; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var tmpText = _gateTexts[i]; _gateTexts[i] = _gateTexts[j]; _gateTexts[j] = tmpText;
                var tmpFlag = _gateCorrect[i]; _gateCorrect[i] = _gateCorrect[j]; _gateCorrect[j] = tmpFlag;
                var tmpOpt = _gateOptionIndex[i]; _gateOptionIndex[i] = _gateOptionIndex[j]; _gateOptionIndex[j] = tmpOpt;
            }
            ArmOptionPreview(_gateTexts, false);
        }

        // PlaceRepresentGate is GONE (F55). It built the "one gold card in the centre lane" that
        // came back after a wrong pick. See HitWrong for why that mechanic could not justify
        // itself; the answer is now shown on the reading panel instead of staged as a pickup
        // nobody could get wrong.

        /// <summary>
        /// The "you are here" marker: a SWBST-coloured frame that sits behind whichever card is
        /// in the runner's lane and slides between cards exactly as fast as the kid changes lane.
        ///
        /// Nothing in the race told the learner which of the three cards they were lined up with.
        /// The road carries no lane markings at all (measured on screen — it is a plain street
        /// slab), the camera stays on the track centre while the kid moves across it, and at 30m
        /// the three cards sit inside 16% of the screen width, so alignment is unreadable until
        /// the last second. Since running straight on collects the centre card by itself, a
        /// learner could take an answer without ever understanding they had chosen it — which is
        /// the "collecting an item seems not logical" the owner reported.
        ///
        /// It shows POSITION ONLY, never correctness: it is the same colour whichever card it is
        /// behind, so it cannot be used to pass a gate without reading (the race is the study's
        /// measure — see the F44 card-width finding).
        /// </summary>
        private void BuildLaneSelector(Transform root, int elementIndex, Vector2 cardSize,
            float laneOffset, bool centreOnly)
        {
            var go = new GameObject("LaneSelector");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0f, CardY, 0.03f); // just behind the card
            var color = SummaRace.Constants.SwbstPalette.ForIndex(elementIndex);

            // Sideways there is nowhere to grow: whatever is left of the 0.075m inter-card gap,
            // and no more, or the halo disappears behind the neighbouring card. The readable
            // part is therefore the band above and below the card.
            float halo = SummaRace.Constants.GameRules.RaceLaneSelectorHalo;
            float widthPad = Mathf.Min(halo, Mathf.Max(0f, laneOffset - cardSize.x) * 0.8f);
            var size = new Vector2(cardSize.x + widthPad, cardSize.y + halo);

            var sr = go.AddComponent<SpriteRenderer>();
            if (worldCardSprite != null)
            {
                sr.sprite = worldCardSprite;
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = size;
                sr.color = color;
                // Behind the card it marks, never over its text.
                sr.sortingOrder = -1;
            }
            else
            {
                // Grey-box fallback, same shape as BuildCard's.
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(go.transform, false);
                quad.transform.localScale = new Vector3(size.x, size.y, 1f);
                quad.GetComponent<Renderer>().material.color = color;
                Destroy(sr);
                sr = null;
            }

            _laneSelector = go.transform;
            _laneSelectorSr = sr;
            _selectorCentreOnly = centreOnly;
        }

        /// <summary>Slides the marker onto the card in the runner's lane. A re-present gate only
        /// has a centre card, so there the marker hides when the runner is not in that lane —
        /// "you will not collect this where you are", which is the nudge that gets them back
        /// across, rather than a marker parked on a card they are going to miss.</summary>
        private void UpdateLaneSelector(TrackManager track)
        {
            if (_laneSelector == null) return;
            var runner = track.characterController;
            var bodyT = runner != null && runner.characterCollider != null
                ? runner.characterCollider.transform : null;
            if (bodyT == null) return;

            float laneOffset = track.laneOffset;
            if (laneOffset <= 0.01f) return;
            float kidX = bodyT.position.x;

            bool onACard = !_selectorCentreOnly || Mathf.Abs(kidX) < laneOffset * 0.5f;
            if (_laneSelector.gameObject.activeSelf != onACard)
                _laneSelector.gameObject.SetActive(onACard);
            if (!onACard) return;

            int lane = _selectorCentreOnly ? 0 : Mathf.Clamp(Mathf.RoundToInt(kidX / laneOffset), -1, 1);
            var p = _laneSelector.localPosition;
            float target = lane * laneOffset;
            // Exactly the kid's own lane-change speed, so the marker arrives with him instead of
            // trailing or leading — anything else reads as lag on a screen this small.
            p.x = runner.laneChangeSpeed > 0.01f
                ? Mathf.MoveTowards(p.x, target, runner.laneChangeSpeed * Time.deltaTime)
                : target;
            _laneSelector.localPosition = p;
        }

        private void PlaceFinishGate(TrackSegment segment, float localDist)
        {
            Vector3 pos; Quaternion rot;
            segment.GetPointAtInWorldUnit(localDist, out pos, out rot);

            var root = new GameObject("FinishGate").transform;
            root.SetParent(segment.transform, true);
            root.SetPositionAndRotation(pos, rot);
            root.gameObject.AddComponent<EndlessCurveDip>();
            _activeGateRoot = root;

            float laneOffset = TrackManager.instance.laneOffset;
            // DEEP BROWN ON GOLD, NOT WHITE ON GOLD. The card was white text on amber, which
            // measures 1.74:1 — WCAG AA wants 3:1 even for large text, so this was the least
            // legible thing in the race and it is the one card the learner has to spot at top
            // speed, at distance, through fog, on a bent horizon. The gold stays exactly as it
            // was (gold IS "this is the finish", and F20's treasure metaphor rests on it); only
            // the ink changes, to the same deep brown the briefing already puts on its gold
            // title pill (F35), which measures 6.8:1 — AA at any size, AAA at this one.
            var card = BuildCard(root, new Vector3(0f, 1.6f, 0f), new Vector2(3.4f, 0.9f),
                SummaRace.Constants.GameText.RaceFinishCard,
                new Color(0.32f, 0.19f, 0.02f), new Color(1f, 0.72f, 0.15f), 3.2f);

            var trigger = card.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            // 3m deep, not 0.6m. At the scene's maxSpeed of 30 the run covers 0.6m in a single
            // 0.02s physics step, so a 0.6m-deep trigger could be stepped straight over — and
            // FINISH is the one trigger whose miss the learner cannot recover from in play.
            // Answer gates keep their playtested depth; they already re-present when missed.
            trigger.size = new Vector3(laneOffset * 3f + 1f, 3.2f, 3f);
            trigger.center = new Vector3(0f, -0.4f, 0f);

            card.gameObject.AddComponent<EndlessOptionPickup>().isFinishGate = true;
            // Nothing to read at FINISH — one card spanning all three lanes.
            HideOptionPreview();
        }

        /// <summary>Rounded kit-sprite card with auto-sized TMP text (F11 style); quad fallback when unwired.</summary>
        private Transform BuildCard(Transform parent, Vector3 localPos, Vector2 size,
            string text, Color textColor, Color cardColor, float maxFontSize)
        {
            var go = new GameObject("Card");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            if (worldCardSprite != null)
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = worldCardSprite;
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = size;
                sr.color = cardColor;
            }
            else
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(go.transform, false);
                quad.transform.localScale = new Vector3(size.x, size.y, 1f);
                quad.GetComponent<Renderer>().material.color = cardColor;
            }

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            var tmp = textGo.AddComponent<TextMeshPro>();
            if (worldLabelFont != null) tmp.font = worldLabelFont;
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = textColor;
            tmp.rectTransform.sizeDelta = new Vector2(size.x - 0.15f, size.y - 0.12f);
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.2f;
            tmp.fontSizeMax = maxFontSize;
            return go.transform;
        }

        // ---------- pick resolution ----------

        public void OnPickupHit(EndlessOptionPickup pickup)
        {
            if (_finished) return;
            if (pickup.isFinishGate) { StartCoroutine(FinishRoutine()); return; }
            // ONE PICK PER GATE, by identity rather than by element index.
            //
            // The old `elementIndex != _activeElement` test looked sufficient but had a hole on
            // the path that actually mattered. A wrong pick calls HitWrong -> ScheduleRepresent
            // -> TryPlacePending *synchronously, inside this same call stack*, which re-arms
            // _activeElement to THE SAME index for the new re-present gate. So when the second
            // trigger event of the dead gate dispatched moments later, it matched, and the
            // learner got a wrong pick and a correct pick counted back to back — the second one
            // destroying the re-present gate 18m before they could ever see it. (A simple
            // "already resolved" latch has the identical hole: the same synchronous call resets
            // it.) The runner is genuinely inside two lanes' triggers for about one frame of
            // every lane change, so this fires in normal play, not at the margins.
            //
            // A monotonic id is never reused, so every card of a retired gate is permanently
            // stale no matter what is placed afterwards.
            if (pickup.gateId != _activeGateId) return;
            _activeGateId = 0; // this gate is spent; its other cards can no longer register

            // Only the FIRST pick at each gate counts for stars (GDD §4.2). A re-present
            // never changes this — it was already recorded false by the pick/miss that
            // triggered the re-present.
            if (!_firstPickDone[_activeElement])
            {
                _firstPickDone[_activeElement] = true;
                _firstPickCorrect[_activeElement] = pickup.isCorrect;
            }

            if (pickup.isCorrect) CollectCorrect(pickup);
            else HitWrong(pickup);

            // WHICH card, not merely right/wrong. Everything below is captured at gate-build
            // time (PlaceAnswerGate), before the lane shuffle scrambles the order, so it costs
            // nothing here and cannot be wrong. A play-through cannot be repeated: a wrong pick
            // whose distractor was not recorded is a misconception nobody can ever recover.
            SummaRace.Core.EventBus.Raise(new SummaRace.Core.ElementCollected
            {
                elementIndex = pickup.elementIndex,
                wasCorrect = pickup.isCorrect,
                chosenOptionIndex = pickup.optionIndex,
                chosenText = pickup.optionText,
                lane = pickup.lane,
                wasRepresent = pickup.isRepresent,
            });
        }

        private void CollectCorrect(EndlessOptionPickup pickup)
        {
            var track = TrackManager.instance;
            bool wasRepresent = _activeIsRepresent;
            int element = pickup.elementIndex;

            if (SummaRace.Core.AudioManager.Instance != null)
                SummaRace.Core.AudioManager.Instance.PlaySfx(SummaRace.Constants.AudioKeys.SfxCollect);

            // Sparkle VFX at the collected card (TDD §11.4) — the visible "you got it".
            SpawnCollectSparkle(pickup.transform.position);
            // The word lifts off and flies up into its SWBST slot (F40 collect-to-inventory).
            FlyCollectedToSlot(element, pickup.transform.position);

            ShowFeedback(SummaRace.Core.Praise.ForRace(element), new Color(0.55f, 1f, 0.55f));

            // The collected card flies up and pops away; the rest of the gate goes now.
            var cardT = pickup.transform;
            var root = _activeGateRoot;

            // Kill the WHOLE gate's colliders this instant, not just the card that was hit.
            // Destroy() below only takes effect at end of frame, so the other two lanes' cards
            // stayed pickable for the rest of the frame — and a learner is wide enough to
            // overlap two lanes' triggers at once while changing lane. That is the "double
            // collection" the owner saw. Disabling only `pickup`'s own collider (as this did)
            // left exactly that hole.
            if (root != null)
                foreach (var c in root.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            var col = pickup.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // Keep the flying card parented to the segment: a floating-origin recenter
            // (~every 100m) would teleport a world-space orphan mid-celebration.
            cardT.SetParent(root != null ? root.parent : null, true);
            Tween.PositionY(cardT, cardT.position.y + 2.2f, 0.45f, Ease.OutQuad);
            Tween.Scale(cardT, Vector3.zero, 0.5f, Ease.InBack)
                .OnComplete(() => { if (cardT != null) Destroy(cardT.gameObject); });

            if (root != null) Destroy(root.gameObject);
            _laneSelector = null; // child of the gate root — it went with it
            _laneSelectorSr = null;
            HideOptionPreview(); // this gate's options are answered; the next placement re-fills it

            // TDD §11.4: the boost bundle (sfx + speed) is reserved for a first-hit correct
            // pick — collecting a re-presented gold card still resolves the element, just
            // without the extra reward.
            if (!wasRepresent && track != null)
            {
                if (SummaRace.Core.AudioManager.Instance != null)
                    SummaRace.Core.AudioManager.Instance.PlaySfx(SummaRace.Constants.AudioKeys.SfxBoost);
                BoostSpeed(track); // original one-shot nudge, same as Trash Dash
            }

            if (track != null) AdvanceToNext(track, element);
            else { _activeGateRoot = null; _activeElement = -1; _activeIsRepresent = false; }
        }

        /// <summary>
        /// A WRONG PICK NOW SHOWS THE ANSWER INSTEAD OF STAGING A FAKE ONE.
        ///
        /// Until F55 this scheduled a "re-present": the correct card came back alone, in gold, in
        /// the centre lane, and the learner drove into it. The owner asked twice what it was for,
        /// and it could not answer:
        ///   * it is not a choice — one card, and it is the answer, so steering into it decides
        ///     and teaches nothing;
        ///   * nothing downstream needs it. RaceResult.collectedPieces[i] is filled with
        ///     _story.elements[i].correct at the finish line unconditionally (see FinishRoutine),
        ///     and ArrangeController falls back to the same string anyway, so the learner reaches
        ///     Arrange holding all five pieces whether or not a card was ever collected;
        ///   * it cannot count for the measure — _firstPickDone is already closed by this very
        ///     pick — so it was ceremony;
        ///   * and it added distance to the run for exactly the learner already struggling.
        ///
        /// What replaces it is the honest version of the same intention: the answer is SHOWN,
        /// clearly, for a beat, and the run moves on to the next gate. Unchanged, deliberately:
        /// _firstPickDone/_firstPickCorrect (the study's headline measure), the friendly slow, the
        /// chaser beat, and the rule that nothing here can ever end the run (GDD D7).
        /// </summary>
        private void HitWrong(EndlessOptionPickup pickup)
        {
            var track = TrackManager.instance;
            if (SummaRace.Core.AudioManager.Instance != null)
                SummaRace.Core.AudioManager.Instance.PlaySfx(SummaRace.Constants.AudioKeys.SfxNotQuite);

            // The answer itself, in the feedback pill. It is story content, not a UI string, so
            // it needs no GameText constant — and GameText.RaceWrongFeedback ("Not quite - get the
            // glowing card!") is now false, since there is no card to get. See the report: the
            // framing wording is a GameText change for whoever owns that file.
            ShowFeedback(_story.elements[pickup.elementIndex].correct, new Color(1f, 0.85f, 0.45f));

            // Surge the chaser into view for a beat — the visible half of "not quite".
            _menaceTimer = SummaRace.Constants.GameRules.PatrolMenaceSeconds;

            if (track != null)
            {
                if (_savedMaxSpeed < 0f) _savedMaxSpeed = track.maxSpeed;
                // Never clamp maxSpeed to exactly minSpeed: their speedRatio is
                // (speed - minSpeed) / (maxSpeed - minSpeed) (TrackManager.cs:72), so an equal
                // pair divides by zero and returns NaN. At gates 1-3 the run is still under
                // 16.7 (accel 0.2/s from minSpeed 10), so speed*0.6 falls below minSpeed and
                // the old Mathf.Max hit exactly that case on essentially every early wrong
                // pick. NaN then reached jump/slide lengths and the character's localPosition
                // in CharacterInputController, freezing lane control and spamming the console
                // for the whole slow window.
                track.maxSpeed = Mathf.Max(track.minSpeed + 1f, track.speed * 0.6f);
                _slowTimer = SummaRace.Constants.GameRules.SlowSeconds;
            }

            int element = pickup.elementIndex;
            DestroyActiveGate();               // the whole gate goes; nothing replaces it
            if (track != null) AdvanceToNext(track, element);
            else { _activeGateRoot = null; _activeElement = -1; }
            ShowAnswerReveal(element);         // after AdvanceToNext, so it overrides the re-arm
        }

        /// <summary>
        /// The answer, shown plainly for a beat on the panel the learner already reads from: one
        /// full-width card in the gold the re-presented pickup used to wear, so the moment still
        /// says "here is the right one" without pretending to be a question. The next gate is
        /// already scheduled and armed underneath; when the beat ends the panel returns to
        /// waiting, and its normal reading window opens on its own clock.
        /// </summary>
        private void ShowAnswerReveal(int element)
        {
            if (_previewRoot == null || _story == null || element < 0 || element >= _story.elements.Length) return;
            if (_answerReveal != null) StopCoroutine(_answerReveal);
            _answerReveal = StartCoroutine(AnswerRevealRoutine(_story.elements[element].correct));
        }

        private IEnumerator AnswerRevealRoutine(string correct)
        {
            _revealing = true;
            PaintPreview(new string[] { null, correct, null }, true);
            _previewWanted = true;
            ApplyPreviewVisibility();
            if (_previewRoot.activeSelf)
            {
                if (_previewCue != null) StopCoroutine(_previewCue);
                _previewCue = StartCoroutine(PulseArrivalGlow());
            }

            float t = 0f;
            while (t < AnswerRevealSeconds && !_finished && !_leaving)
            {
                if (!_paused) t += Time.deltaTime;
                yield return null;
            }

            _revealing = false;
            _previewWanted = false;             // back to "armed, waiting for its window"
            ApplyPreviewVisibility();
            _answerReveal = null;
        }

        /// <summary>A gate or re-present the player ran past without any hit. First-pick is
        /// recorded false if this is the first miss for the element; either way the correct
        /// answer comes back as a re-present, up to <see cref="MaxRepresentMisses"/> dodges
        /// before an anti-frustration auto-resolve moves on.</summary>
        private void HandleMissedActiveGate(TrackManager track)
        {
            int element = _activeElement;
            if (!_firstPickDone[element])
            {
                _firstPickDone[element] = true;
                _firstPickCorrect[element] = false;
            }

            DestroyActiveGate();
            // Same treatment as a wrong pick: show what the answer was and move on. There is no
            // second attempt to dodge any more, so the old dodge counter and its anti-frustration
            // auto-resolve have nothing left to count.
            AdvanceToNext(track, element);
            ShowAnswerReveal(element);
        }

        /// <summary>FINISH was run past instead of collected. Nothing about the story is
        /// affected — no first pick, no stars — so it simply comes back a little way ahead.
        /// Without this the gate dies with its segment and the learner runs forever.</summary>
        private void RescheduleFinish(TrackManager track)
        {
            DestroyActiveGate();
            _activeElement = -1;
            _pendingElement = 5;
            _pendingIsRepresent = false;
            _pendingGateDistance = track.worldDistance + RepresentDistance(track);
            _preparedElement = -1;   // FINISH has no options and no panel
            TryPlacePending();
        }

        /// <summary>Runway for a card that is coming back — enough seconds to be seen, read and
        /// steered into at the speed actually being run, never less than <see cref="RepresentMinGap"/>.</summary>
        private float RepresentDistance(TrackManager track)
        {
            float runSpeed = track != null ? Mathf.Max(track.speed, track.minSpeed) : 10f;
            return Mathf.Max(RepresentMinGap, runSpeed * RepresentSeconds);
        }

        /// <summary>Run-out from the last answer gate to FINISH — see <see cref="FinishSeconds"/>
        /// for why this is a time and not a distance.</summary>
        private float FinishRunway(TrackManager track)
        {
            float runSpeed = track != null ? Mathf.Max(track.speed, track.minSpeed) : 10f;
            return Mathf.Max(FinishMinGap, runSpeed * FinishSeconds);
        }

        /// <summary>Last-resort backstop for "never a dead end" (GDD): if the run ever has no
        /// gate in the world and none scheduled, there is nothing left that can end it, so
        /// bring FINISH back. Every normal transition schedules its successor in the same
        /// frame it clears the old gate, so this should never fire — it exists because the
        /// failure it catches is unrecoverable for the learner without killing the app.</summary>
        private void CheckStranded(TrackManager track)
        {
            if (_finished || !_runReleased || _activeGateRoot != null || _pendingGateDistance >= 0f)
            {
                _strandedTimer = 0f;
                return;
            }

            _strandedTimer += Time.deltaTime;
            if (_strandedTimer < StrandedSeconds) return;

            _strandedTimer = 0f;

            // Recover to the element the run was actually on. Always jumping to FINISH would
            // end the story with the remaining elements unanswered and near-zero stars, which
            // for a strand at gate 1 quietly throws away four fifths of the measure.
            // _activeElement survives a gate dying with its segment, so it is still valid.
            if (_activeElement >= 0 && _activeElement < 5)
            {
                Debug.LogWarning("EndlessRaceDirector: nothing to present — resolving element "
                    + _activeElement + " and moving on.");
                int element = _activeElement;
                _activeElement = -1;
                // Close the first pick as a MISS. A strand is a fault on our side and the learner
                // never got to choose, so it must not be credited: raceFirstPickCorrect is the
                // thesis measure and the star count. Same treatment as a run-past gate.
                if (!_firstPickDone[element])
                {
                    _firstPickDone[element] = true;
                    _firstPickCorrect[element] = false;
                }
                AdvanceToNext(track, element);
                ShowAnswerReveal(element);
                return;
            }

            Debug.LogWarning("EndlessRaceDirector: nothing to present — recovering to FINISH.");
            RescheduleFinish(track);
        }

        private void DestroyActiveGate()
        {
            if (_activeGateRoot != null)
            {
                // Disable the colliders NOW. Destroy only takes effect at end of frame, and the
                // replacement gate is scheduled with the same element index in this same frame,
                // so a second card of the dead gate firing later in the frame would still pass
                // the `elementIndex != _activeElement` guard. That is reachable: adjacent lane
                // triggers sit 0.075m apart while the runner's collider is 0.576m wide, so a
                // learner mid-lane-change overlaps two cards at identical z and both
                // OnTriggerEnter land in one step — double slow, double menace, or a
                // re-present destroyed 18m before it was ever seen.
                foreach (var col in _activeGateRoot.GetComponentsInChildren<Collider>(true))
                    col.enabled = false;
                Destroy(_activeGateRoot.gameObject);
            }
            _activeGateRoot = null;
            _laneSelector = null; // child of the gate root — it went with it
            _laneSelectorSr = null;
            HideOptionPreview();
        }

        /// <summary>Element fully resolved (correct first hit, re-present collected, or
        /// resolved either way — clears the active slot and schedules the next answer gate, or
        /// FINISH once element 4 is done, through the same pending mechanism.</summary>
        private void AdvanceToNext(TrackManager track, int completedElement)
        {
            _activeGateRoot = null;
            _activeElement = -1;
            _activeIsRepresent = false;

            int next = completedElement + 1;
            if (next < 5)
            {
                _pendingElement = next;
                _pendingIsRepresent = false;
                _pendingGateDistance = track.worldDistance + NextGateGap(track);
                // Options decided now, not at placement — this is what lets the reading window
                // open on a clock instead of when the track happens to spawn far enough.
                PrepareGateOptions(next, false);
            }
            else
            {
                _pendingElement = 5;
                _pendingIsRepresent = false;
                _pendingGateDistance = track.worldDistance + FinishRunway(track);
                _preparedElement = -1;   // FINISH has no options and no panel
            }

            UpdateBanner();
            TryPlacePending();
        }

        /// <summary>Metres to the next answer gate. Derived from <see cref="SummaRace.Constants.GameRules.RaceSecondsPerGate"/>
        /// against the run's top speed so the learner always gets at least that long to read
        /// and choose (the run never exceeds maxSpeed, so real time is >= the target). The
        /// story's checkpointSpacing stays a hard floor.</summary>
        /// <summary>
        /// The menu music follows us in from MainMenu and keeps looping under their race
        /// track, so two pieces of music play at once. Their MusicPlayer owns audio in this
        /// scene (it is the race music), so ours stands down here.
        /// </summary>
        private static void SilenceOurMenuMusic()
        {
            if (SummaRace.Core.AudioManager.Instance != null)
                SummaRace.Core.AudioManager.Instance.StopMusic();
        }

        /// <summary>
        /// Their MusicPlayer is DontDestroyOnLoad. It DOES have a singleton guard — Awake
        /// destroys the newcomer — but Destroy is deferred to end of frame, so for one frame
        /// after a re-entry two exist and both are audible.
        ///
        /// This used to drop everything after index 0 of an explicitly UNORDERED
        /// FindObjectsByType. When the already-doomed instance sorted first, that destroyed
        /// the live MusicPlayer.instance and kept the one about to delete itself — leaving
        /// MusicPlayer.instance referencing a destroyed object. TrackManager.Update then
        /// dereferences it (UpdateVolumes, TrackManager.cs:470) and their GameState.Enter
        /// does too, so the track never starts, our "wait for TrackManager" loop never exits,
        /// and the briefing sits on "Getting ready…" forever with no pause and no way back.
        /// Race 2 or 3 of a session, roughly one time in two.
        ///
        /// Keep the one that IS the singleton and drop any other.
        /// </summary>
        private static void DeduplicateTheirMusicPlayer()
        {
            var keep = MusicPlayer.instance;
            if (keep == null) return; // their Awake has not run yet; its own guard will handle it

            var players = UnityEngine.Object.FindObjectsByType<MusicPlayer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var p in players)
                if (p != null && p != keep) Destroy(p.gameObject);
        }

        private float NextGateGap(TrackManager track)
        {
            // Against the speed we are ACTUALLY running, not maxSpeed. Their track starts at
            // minSpeed (10) and accelerates at 0.2 m/s², so it only reaches maxSpeed (30) about
            // 100s in — i.e. past the end of the race. Costing the gap at 30 made every gap ~2.3x
            // too long: 12 intended seconds became 20-28 real ones, and the learner ran down an
            // empty corridor between gates wondering if the game had stopped.
            float runSpeed = Mathf.Max(track.speed, track.minSpeed);
            float bySpeed = SummaRace.Constants.GameRules.RaceSecondsPerGate
                * DifficultyGateTime() * runSpeed;
            // THE LEGIBILITY FLOOR, in seconds and therefore in metres at the speed being run:
            // the reading window plus the quiet stretch. Difficulty scales thinking time, but it
            // may never scale it below the window itself — at hard (x0.8) it otherwise would, and
            // a gate that arrives before its options have been readable for 12s is the F47
            // validity failure returning through the difficulty setting.
            float secondsFloor = (SummaRace.Constants.GameRules.RacePreviewLeadSeconds
                + SummaRace.Constants.GameRules.RaceQuietRunSeconds) * runSpeed;
            float floor = Mathf.Max(Mathf.Max(_story.mission.checkpointSpacing,
                SummaRace.Constants.GameRules.RaceMinGateGap), secondsFloor);
            return Mathf.Clamp(Mathf.Max(bySpeed, floor), floor,
                SummaRace.Constants.GameRules.RaceMaxGateGap);
        }

        /// <summary>Easy gets more runway to read a card, hard gets less.</summary>
        private float DifficultyGateTime()
        {
            switch (_story.difficulty)
            {
                case "easy": return SummaRace.Constants.GameRules.GateTimeEasy;
                case "hard": return SummaRace.Constants.GameRules.GateTimeHard;
                default: return SummaRace.Constants.GameRules.GateTimeAverage;
            }
        }

        // ScheduleRepresent lived here and is deliberately GONE, not merely unreferenced.
        //
        // The owner's objection to the re-present was that it made no sense as a game: get an
        // answer wrong and a single lone card appears in the middle of the road carrying the
        // right answer, which is a chance offered in a shape nobody would recognise as a chance.
        // It was replaced by a 2.2s answer reveal, and TryPlacePending's represent branch went
        // with it — so this method had no call sites left.
        //
        // Left on disk it was a loaded gun: calling it would place a full three-option gate for
        // an element whose first pick is already CLOSED, and a collection there would be logged
        // against a question the learner had already answered. raceFirstPickCorrect is the star
        // count and the study's headline measure, so that is a data defect, not a gameplay one.
        // RepresentDistance survives because FinishRunway is derived the same way.

        private void BoostSpeed(TrackManager track)
        {
            // TDD §11.4: correct pick = boost. Their protected m_Speed self-clamps to
            // maxSpeed in Update, so an over-write is safe; reflection is runtime-only
            // state — their code stays untouched.
            if (SpeedField != null)
                SpeedField.SetValue(track, Mathf.Min(track.maxSpeed, track.speed * 1.35f));
        }

        private IEnumerator FinishRoutine()
        {
            if (_finished) yield break;
            _finished = true;

            // Nothing to pause or read any more — the run is over and Arrange is next.
            if (_pauseChip != null) _pauseChip.SetActive(false);
            HideOptionPreview();

            var track = TrackManager.instance;
            if (track != null)
            {
                track.StopMove();
                // Stand idle for the finish beat (no dance — idle is enough).
                var runner = track.characterController;
                if (runner != null && runner.character != null && runner.character.animator != null)
                {
                    runner.character.animator.SetBool("Moving", false);
                    runner.character.animator.Play("Start");
                }
            }

            // Their music is DontDestroyOnLoad — silence it before Arrange.
            var mp = MusicPlayer.instance;
            if (mp != null)
                foreach (var src in mp.GetComponentsInChildren<AudioSource>()) src.Stop();

            var result = new SummaRace.Core.RaceResult
            {
                // The patrol looms but never catches (GDD D7) — stars must not depend on it.
                timesCaught = 0,
                runSeconds = _runStartTime < 0f ? 0f : Time.time - _runStartTime
            };
            for (int i = 0; i < 5; i++)
            {
                result.collectedPieces[i] = _story.elements[i].correct;
                result.firstPickCorrect[i] = _firstPickCorrect[i];
            }

            if (SummaRace.Core.GameManager.Instance != null)
                SummaRace.Core.GameManager.Instance.SetRaceResult(result);
            SummaRace.Core.EventBus.Raise(new SummaRace.Core.RaceCompleted { result = result });

            if (SummaRace.Core.AudioManager.Instance != null)
                SummaRace.Core.AudioManager.Instance.PlaySfx(SummaRace.Constants.AudioKeys.SfxStar);
            if (_bannerText != null) _bannerText.text = SummaRace.Constants.GameText.RaceFinishBanner;

            yield return new WaitForSeconds(2.2f); // victory beat

            SummaRace.Core.SceneLoader.Go(SummaRace.Constants.SceneNames.Arrange);
        }

        // ---------- HUD ----------

        private void BuildHud()
        {
            var canvasGo = new GameObject("SummaRaceHud");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            // MATCH = 0 IS DELIBERATE HERE, AND MUST STAY 0 — it is also the field's default, so
            // it is written out only to stop a future pass "harmonising" it with the eleven
            // authored scenes, which use 0.5.
            //
            // At match 0 the canvas is ALWAYS 1080 units wide and its height tracks the aspect
            // (1920 at 9:16, 2400 at 20:9). Every number in this HUD is a width-driven pixel
            // measurement against that 1080: the tracker board is 880 wide with slots measured to
            // the pixel against "SOMEBODY", and the pause chip takes the 144px of gutter that
            // leaves. At match 0.5 on a 1080x2400 phone the canvas narrows to 966x2147, and that
            // same top row breaks outright: the board (880 in 966) runs 17px OFF the left edge
            // and the chip lands 53px on top of the fifth slot. The 0.5 scenes are laid out on
            // fractional anchors and do not care; this one is the opposite case.
            //
            // The cost is real and accepted: race type renders ~11% smaller than the rest of the
            // app on a 20:9 phone. The two are never on screen together, and a consistent-but-
            // overlapping HUD is not an improvement on an inconsistent-but-legible one.
            scaler.matchWidthOrHeight = 0f;
            // The HUD had no raycaster because nothing on it was ever tappable; the pause chip
            // is. Everything else on this canvas sets raycastTarget = false (MakeHudText does it
            // for every label), so adding one cannot start swallowing taps meant for the road —
            // and their swipe handler reads Input directly rather than through the EventSystem,
            // so it is unaffected either way.
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Amber danger vignette (TDD §11.5): darkest at the screen edges, clear in the
            // middle, alpha driven by danger in UpdatePatrol. Drawn behind the text.
            var vgo = new GameObject("DangerVignette");
            vgo.transform.SetParent(canvasGo.transform, false);
            _vignette = vgo.AddComponent<UnityEngine.UI.Image>();
            _vignetteSprite = MakeVignetteSprite();
            _vignette.sprite = _vignetteSprite;
            _vignette.raycastTarget = false;
            _vignette.color = new Color(1f, 0.42f, 0.05f, 0f); // amber, invisible until danger rises
            var vrt = _vignette.rectTransform;
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;

            BuildTracker(canvasGo.transform);

            // THE BANNER WAS ENTIRELY INSIDE THE OPTION PREVIEW BOARD, AND THEY SHARE THE LAST
            // GATE. At (0,-300) from the top it occupied y 0.781-0.844 on a 1920 reference,
            // inside the preview board's 0.745-0.885 — and the board is opaque (alpha 0.94) and
            // a later sibling, so it drew over the banner. The banner's only line is
            // RaceRunToFinish, set by UpdateBanner the moment _pendingElement reaches 5; a wrong
            // or missed element 5 runs AdvanceToNext -> UpdateBanner and then ShowAnswerReveal,
            // which puts the board up for 2.2s. So on the one gate where "Run to the FINISH!" is
            // the whole instruction, it was behind a wooden board.
            //
            // It is anchored to the BOARD'S UNDERSIDE rather than given a bigger fixed offset:
            // with matchWidthOrHeight = 0 the canvas is always 1080 units wide but its HEIGHT
            // tracks the aspect (1920 at 9:16, 2400 at 20:9), so the board's fractional anchors
            // move in pixels while a fixed top offset does not. A flat (0,-500) reads correctly
            // at 1920 and lands back inside the board at 1080x2400 (board bottom 612px from the
            // top, banner top 500). Hung off the anchor it is 12px below the board at every
            // aspect: y 0.676-0.739 at 9:16 (53px of clearance above the feedback pill, which
            // tops out at 0.648) and y 0.690-0.740 at 20:9 (171px of clearance).
            _bannerText = MakeHudText(canvasGo.transform, new Vector2(0.5f, 1f), Vector2.zero, 64f);
            var bannerRt = _bannerText.rectTransform;
            bannerRt.anchorMin = bannerRt.anchorMax = new Vector2(0.5f, PreviewBandBottom);
            bannerRt.pivot = new Vector2(0.5f, 1f);
            bannerRt.anchoredPosition = new Vector2(0f, -12f);
            bannerRt.sizeDelta = new Vector2(1000f, 120f);

            // Feedback sits on a dark pill, as it does in the legacy race (F16). Bare coloured
            // text over the world is barely legible: "Not quite — the glowing one!" was landing in
            // amber on top of a sunlit street, which is the one line the learner most needs to read.
            var pillGo = new GameObject("FeedbackPill");
            pillGo.transform.SetParent(canvasGo.transform, false);
            _feedbackPill = pillGo.AddComponent<UnityEngine.UI.Image>();
            _feedbackPill.sprite = WoodPlaqueSprite();
            _feedbackPill.type = UnityEngine.UI.Image.Type.Sliced;
            _feedbackPill.color = new Color(0.10f, 0.12f, 0.16f, 0.88f);
            _feedbackPill.raycastTarget = false;
            var prt = _feedbackPill.rectTransform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = new Vector2(0f, 220f);
            prt.sizeDelta = new Vector2(880f, 130f);
            pillGo.SetActive(false);

            _feedbackText = MakeHudText(pillGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, 56f);
            _feedbackText.rectTransform.sizeDelta = new Vector2(840f, 120f);
            _feedbackText.enableAutoSizing = true;
            _feedbackText.fontSizeMin = 30f;
            _feedbackText.fontSizeMax = 56f;

            BuildOptionPreview(canvasGo.transform);
            BuildPauseChip(canvasGo.transform);
            BuildPauseOverlay();
        }

        // ---------- the reading surface ----------

        /// <summary>
        /// THE THREE OPTIONS, AS READABLE TEXT. The world cards are the thing you steer into;
        /// this is the thing you read. They are not the same job and the geometry says they
        /// cannot both be done by one object:
        ///
        /// with the shipped camera (58.7deg vertical FOV, 5m behind the runner, 4m up, pitched
        /// 14.947deg) and the shipped card (1.425x0.85 with a 1.275x0.73 text box, Fredoka cap
        /// 0.700em, TMP's 0.1 world scale for 3D text), an average 35-character answer auto-sizes
        /// to ~1.95pt = a 0.137m cap height, which on a 10.1" tablet at 40cm subtends 227/d
        /// arcmin. Clearing the ~20 arcmin comfortable-reading floor needs d &lt;= 11.35m from the
        /// lens = 5.8m ahead of the runner, and the run passes gate 1 at 11.5 m/s and gate 5 at
        /// ~20.4 m/s. That is 0.50s and 0.28s of legible time — against three cards carrying
        /// ~19 words, i.e. ~11.6s of reading at 100wpm. The cards were never readable.
        ///
        /// That is a measurement failure rather than a comfort one: every gate is a forced
        /// choice among three lanes and a run-past is logged as first-pick-incorrect, so if the
        /// cards cannot be read then a strong and a weak summariser both land on a 1-in-3 guess
        /// and raceFirstPickCorrect — the star count AND the study's headline measure — stops
        /// discriminating. (F44 removed the card-width tell that had been letting learners score
        /// without reading; that tell was the only thing making the race scoreable, so its
        /// removal is what exposed this.)
        ///
        /// Rules this panel must keep:
        ///  * ALL THREE options, in LANE ORDER left/centre/right, identical plaque, identical
        ///    colour, identical type. Nothing may distinguish the correct one — the whole point
        ///    of F44 was that a surface cue is worth 84.7% against 33% for guessing.
        ///  * It occludes nothing. It lives in the SKY BAND: screen y 0.745-0.885. The horizon
        ///    sits at 0.738 (15deg above the camera axis / tan(29.35deg)), so no ground card,
        ///    lane halo or runner can ever reach it, and the SWBST tracker's underside is at
        ///    0.896, so it does not touch that either.
        /// </summary>
        private void BuildOptionPreview(Transform parent)
        {
            var board = new GameObject("OptionPreview");
            board.transform.SetParent(parent, false);
            var bimg = board.AddComponent<UnityEngine.UI.Image>();
            bimg.sprite = WoodPlaqueSprite();
            bimg.type = UnityEngine.UI.Image.Type.Sliced;
            bimg.color = new Color(0.26f, 0.17f, 0.09f, 0.94f); // same wood language as the tracker
            bimg.raycastTarget = false;
            var brt = bimg.rectTransform;
            brt.anchorMin = new Vector2(0.03f, PreviewBandBottom);
            brt.anchorMax = new Vector2(0.97f, PreviewBandTop);
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;

            // ATTENTION CUE, AND WHY IT IS A BORDER. Now that the panel only appears near its
            // gate, its arrival has to be noticed — a child watching the road can miss a silent
            // fade, and missing the start of the window costs exactly the reading seconds the
            // panel exists to provide. It is deliberately NOT the words that move: the three
            // options must be legible and motionless from their first rendered frame, or the cue
            // destroys the thing it is announcing. So this is a rim BEHIND the board (first
            // sibling, inset negative so it sticks out), pulsed twice and gone. It is one ring
            // around all three columns, so it can never favour one option — the F44 validity
            // crisis was a surface cue worth 84.7% against 33% for guessing, and a cue that
            // landed differently on the correct column would be that failure in a new costume.
            var glow = new GameObject("ArrivalGlow");
            glow.transform.SetParent(board.transform, false);
            var gimg = glow.AddComponent<UnityEngine.UI.Image>();
            gimg.sprite = WoodPlaqueSprite();
            gimg.type = UnityEngine.UI.Image.Type.Sliced;
            gimg.color = new Color(1f, 0.84f, 0.35f, 0f); // warm gold, invisible at rest
            gimg.raycastTarget = false;
            var grt = gimg.rectTransform;
            grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one;
            grt.offsetMin = new Vector2(-16f, -16f); grt.offsetMax = new Vector2(16f, 16f);
            _previewGlow = gimg;

            // Three equal columns: left/centre/right, exactly the lanes they stand for.
            const float pad = 0.014f;
            float w = (1f - 4f * pad) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var col = new GameObject("Option_" + i);
                col.transform.SetParent(board.transform, false);
                var img = col.AddComponent<UnityEngine.UI.Image>();
                img.sprite = worldCardSprite != null ? worldCardSprite : WoodPlaqueSprite();
                img.type = UnityEngine.UI.Image.Type.Sliced;
                img.color = new Color(0.98f, 0.97f, 0.93f); // the world card's white, so the
                img.raycastTarget = false;                  // mapping is obvious at a glance
                var rt = img.rectTransform;
                float x0 = pad + i * (w + pad);
                rt.anchorMin = new Vector2(x0, 0.07f);
                rt.anchorMax = new Vector2(x0 + w, 0.93f);
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                _previewPlaque[i] = img;
                _previewColumn[i] = rt;

                var lblGo = new GameObject("Text");
                lblGo.transform.SetParent(col.transform, false);
                var lbl = lblGo.AddComponent<TextMeshProUGUI>();
                if (worldLabelFont != null) lbl.font = worldLabelFont;
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.color = new Color(0.10f, 0.10f, 0.12f);  // the world card's black
                lbl.raycastTarget = false;
                lbl.enableAutoSizing = true;
                // A 295x215px box on a 1080x1920 reference. The longest card string the content
                // pipeline allows is 53 characters (flag.py MAX_CARD_CHARS), which lands at
                // ~41pt = a 28.7px cap = ~28 arcmin on a 10.1" tablet at 40cm; a typical 34-char
                // option renders at the 44 ceiling, ~30 arcmin. Both clear the 20-arcmin floor
                // with margin, on a 7" device as well. 26 is a backstop, not a working value.
                lbl.fontSizeMin = 26f;
                lbl.fontSizeMax = 44f;
                lbl.overflowMode = TextOverflowModes.Ellipsis;
                var lrt = lbl.rectTransform;
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(12f, 8f); lrt.offsetMax = new Vector2(-12f, -8f);
                _previewLabel[i] = lbl;
            }

            _previewRoot = board;
            board.SetActive(false);
        }

        /// <summary>Remembers a gate's three options, indexed BY LANE (0 left, 1 centre, 2 right)
        /// so the panel and the road agree, and ARMS the reading window. It does not put anything
        /// on screen: UpdatePreviewWindow opens the window RacePreviewLeadSeconds before the gate.
        /// Idempotent, because both scheduling and placement call it for the same gate.</summary>
        private void ArmOptionPreview(string[] textsByLane, bool isRepresent)
        {
            if (textsByLane == null) return;
            for (int i = 0; i < 3; i++)
                _previewTexts[i] = i < textsByLane.Length ? textsByLane[i] : null;
            _previewIsRepresent = isRepresent;
            _previewArmed = true;
        }

        /// <summary>
        /// Opens the reading window when the gate is near enough — the whole point of F55.
        ///
        /// The trigger is a DISTANCE derived from the live run speed rather than a timer, so a
        /// boost, a wrong-pick slow or a pause cannot eat the window. It reads the pending gate's
        /// distance when the gate has not been placed yet, which is the case for most of the
        /// window: TrackManager only spawns ~137m ahead, and at 20 m/s that is under 7 seconds.
        /// </summary>
        private void UpdatePreviewWindow(TrackManager track)
        {
            if (!_previewArmed || _previewWanted || track == null) return;
            if (!_runReleased || _finished || _paused || _leaving) return;
            // An answer reveal owns the panel for its beat. The next gate is already scheduled and
            // armed underneath, and at the speeds this runs at its window can open while the
            // reveal is still on screen — which would swap the answer the learner is reading for
            // the next question mid-sentence. The reveal clears _revealing when it ends, and the
            // window then opens on its own clock.
            if (_revealing) return;

            float target = _activeGateRoot != null ? _activeGateDistance : _pendingGateDistance;
            if (target < 0f) return;

            float speed = Mathf.Max(track.speed, track.minSpeed);
            float lead = speed * SummaRace.Constants.GameRules.RacePreviewLeadSeconds;
            if (target - track.worldDistance > lead) return;

            RevealOptionPreview();
        }

        /// <summary>Paints the armed options onto the panel, shows it, and plays the arrival cue
        /// (border pulse + the tracker's own SWBST pulse + a pop). The cue teaches WHICH part of
        /// the framework is coming, not merely that something is — the tracker slot for this
        /// element is what pulses, in its own SWBST colour.</summary>
        private void RevealOptionPreview()
        {
            if (_previewRoot == null) return;
            PaintPreview(_previewTexts, false);
            _previewWanted = true;
            ApplyPreviewVisibility();

            if (_previewRoot.activeSelf)
            {
                if (_previewCue != null) StopCoroutine(_previewCue);
                _previewCue = StartCoroutine(PulseArrivalGlow());
                RefreshTracker();   // re-pulses the upcoming element's slot in its SWBST colour
                if (SummaRace.Core.AudioManager.Instance != null)
                    SummaRace.Core.AudioManager.Instance.PlaySfx(SummaRace.Constants.AudioKeys.SfxPop);
            }
        }

        /// <summary>Fills the three columns. `single` is the answer-reveal form: one full-width
        /// card in the gold the world's correct card wears, with the other two columns switched
        /// OFF rather than ghosted — a nine-year-old reads two faded columns as a question that
        /// has lost two options, which is precisely the confusion the re-present used to cause.
        /// In the normal form all three columns are identical in size, colour and type: nothing
        /// may mark the correct one (F44 — a surface cue was worth 84.7% against 33% guessing).</summary>
        private void PaintPreview(string[] texts, bool single)
        {
            const float pad = 0.014f;
            float w = (1f - 4f * pad) / 3f;
            for (int i = 0; i < 3; i++)
            {
                string text = i < texts.Length ? texts[i] : null;
                bool has = !string.IsNullOrEmpty(text);
                if (_previewLabel[i] != null) _previewLabel[i].text = has ? text : "";
                if (_previewColumn[i] != null)
                {
                    _previewColumn[i].gameObject.SetActive(has);
                    float x0 = single ? pad : pad + i * (w + pad);
                    float x1 = single ? 1f - pad : x0 + w;
                    _previewColumn[i].anchorMin = new Vector2(x0, 0.07f);
                    _previewColumn[i].anchorMax = new Vector2(x1, 0.93f);
                    _previewColumn[i].offsetMin = Vector2.zero;
                    _previewColumn[i].offsetMax = Vector2.zero;
                }
                if (_previewPlaque[i] != null)
                    _previewPlaque[i].color = single
                        ? new Color(1f, 0.85f, 0.35f)          // "this is the answer" gold
                        : new Color(0.98f, 0.97f, 0.93f);      // the answer card's white
            }
        }

        /// <summary>Two soft pulses on the panel's border and then gone. Deliberately a coroutine
        /// with the shape written out, so the frequency is checkable rather than taken on trust:
        /// peaks at 0.24s and 0.78s = a 0.54s period = <b>1.85Hz</b>, and only two of them. The
        /// photosensitivity ceiling is 3Hz; nothing in this game may ever approach a strobe, and a
        /// gentle beat reads better to a nine-year-old than an alarm anyway (GDD D7 — nothing here
        /// is a warning). The words are untouched throughout.</summary>
        private IEnumerator PulseArrivalGlow()
        {
            if (_previewGlow == null) yield break;
            const float peak = 0.85f;
            // (from, to, seconds) — 0.24 up, 0.30 down, 0.24 up, 0.45 out = 1.23s total.
            float[,] legs = { { 0f, peak, 0.24f }, { peak, 0.18f, 0.30f },
                              { 0.18f, peak, 0.24f }, { peak, 0f, 0.45f } };
            for (int leg = 0; leg < 4; leg++)
            {
                float t = 0f, dur = legs[leg, 2];
                while (t < dur)
                {
                    t += Time.deltaTime;
                    var c = _previewGlow.color;
                    c.a = Mathf.Lerp(legs[leg, 0], legs[leg, 1], Mathf.Clamp01(t / dur));
                    _previewGlow.color = c;
                    yield return null;
                }
            }
            var end = _previewGlow.color; end.a = 0f; _previewGlow.color = end;
            _previewCue = null;
        }

        private void HideOptionPreview()
        {
            _previewWanted = false;
            _previewArmed = false;
            if (_previewCue != null) { StopCoroutine(_previewCue); _previewCue = null; }
            if (_previewGlow != null)
            {
                var c = _previewGlow.color; c.a = 0f; _previewGlow.color = c;
            }
            ApplyPreviewVisibility();
        }

        /// <summary>The panel is only ever up while there is something to read AND the world is
        /// actually running: gate 1 is placed during the briefing, and showing its answers
        /// underneath the mission card or the 3-2-1 would be noise, not reading time.</summary>
        private void ApplyPreviewVisibility()
        {
            if (_previewRoot == null) return;
            bool show = _previewWanted && _runReleased && !_finished && !_paused && !_leaving;
            if (_previewRoot.activeSelf != show) _previewRoot.SetActive(show);
        }

        // ---------- pause ----------

        /// <summary>
        /// The control that opens the pause screen. Deliberately small and in the top-right
        /// gutter beside the SWBST tracker: a race is played with swipes and taps across the
        /// middle and lower screen, so this is the one corner a nine-year-old's hand does not
        /// visit — while a teacher scanning the screen finds a pause glyph immediately. It is a
        /// drawn glyph rather than a word so it needs no reading and no translation.
        /// Accidentally opening it costs nothing (the world simply waits); the destructive half
        /// is the LEAVE chip inside, which arms before it acts.
        /// </summary>
        private void BuildPauseChip(Transform parent)
        {
            var chip = new GameObject("PauseChip");
            chip.transform.SetParent(parent, false);
            var img = chip.AddComponent<UnityEngine.UI.Image>();
            img.sprite = WoodPlaqueSprite();
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.color = new Color(0.30f, 0.20f, 0.10f, 0.92f);
            var rt = img.rectTransform;
            // Anchored to the TOP OF THE READING BAND by its own bottom edge, not to the top of
            // the screen. See BuildTracker for the derivation — on a 4:3 tablet a top-anchored
            // chip put 50 of its 144px underneath the reading panel, which draws over it.
            rt.anchorMin = rt.anchorMax = new Vector2(1f, PauseChipBottomY);
            rt.pivot = new Vector2(1f, 0f);
            // SIZED AT THE ANDROID 48dp MINIMUM, AND THE TRACKER MOVED TO MAKE ROOM FOR IT.
            // This was 88x88 at (-8,-95) = 29.3dp at xxhdpi with its right edge 8px (2.7dp)
            // from the screen, i.e. buried inside Android's ~20dp (60px) back-gesture exclusion
            // strip — on the ONLY way out of a run.
            //
            // 144px = 48dp exactly at xxhdpi (1080-wide screen, and with matchWidthOrHeight = 0
            // the canvas is ALWAYS 1080 units wide, so this arithmetic holds on every device).
            // Where it goes is forced, because the top row has no spare width:
            //   * the tracker board cannot shrink. Tools/StoryPipeline/fit.py measures "SOMEBODY"
            //     at 26.2pt needing 140px in a 140px slot — ZERO headroom — so one pixel off the
            //     board ellipsises the first slot of all 30 races (the F47(f) bug).
            //   * board(880) + chip(144) + a 60px gesture margin = 1084 > 1080, so a chip that is
            //     both 48dp AND fully clear of the strip AND clear of the board does not exist on
            //     a 1080 reference. Something had to give, and the strip is the cheapest: F51's
            //     BackButtonGuard already swallows Android BACK app-wide, so a tap that drifts
            //     into the strip does nothing rather than dropping the child out of the app.
            //   * down is not available either — the option preview board (the F47(a) reading
            //     surface) starts at y 0.885 and is not negotiable.
            // So the tracker slides 52px left (BuildTracker) and the chip takes the widened
            // gutter: slots now end at x=908, the chip runs x[908,1052] over the board's 20px of
            // decorative right padding only, with a 28px (9.3dp) margin to the screen edge and
            // its centre 104px (34.7dp) in from it.
            rt.anchoredPosition = new Vector2(-28f, 0f);
            rt.sizeDelta = new Vector2(144f, 144f);
            _pauseChipRect = rt;

            for (int i = 0; i < 2; i++) // the two bars of a pause glyph
            {
                var bar = new GameObject("Bar_" + i);
                bar.transform.SetParent(chip.transform, false);
                var bimg = bar.AddComponent<UnityEngine.UI.Image>();
                bimg.color = new Color(1f, 0.97f, 0.90f);
                bimg.raycastTarget = false;
                var brt = bimg.rectTransform;
                brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
                // Scaled with the plaque (x144/88) so the glyph keeps its proportions.
                brt.sizeDelta = new Vector2(24f, 72f);
                brt.anchoredPosition = new Vector2(i == 0 ? -21f : 21f, 0f);
            }

            var button = chip.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(OpenPause);

            _pauseChip = chip;
            chip.SetActive(false); // only once the world is actually running
        }

        /// <summary>
        /// The pause screen itself, on its own canvas above everything — including their chrome,
        /// which must never be visible behind ours. Two choices only: come back, or leave. It is
        /// worded as a rest, never as a failure, and leaving is the small quiet option rather
        /// than the loud one (GDD D7: the race never punishes and never ends by itself).
        /// </summary>
        private void BuildPauseOverlay()
        {
            var canvasGo = new GameObject("SummaRacePause");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70; // above the briefing (60) and our HUD (40)
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            // Match 0, as the HUD — see BuildHud for the derivation. This screen draws over the
            // HUD through a 0.72 dim, so the tracker behind it is still visible and a different
            // scale on the two would be seen side by side.
            scaler.matchWidthOrHeight = 0f;
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            _pauseRoot = canvasGo;

            // Opaque enough to hide anything of theirs, and it swallows taps so a swipe meant
            // for the menu cannot also reach the world behind it.
            var dim = new GameObject("Dim");
            dim.transform.SetParent(canvasGo.transform, false);
            var dimImg = dim.AddComponent<UnityEngine.UI.Image>();
            dimImg.color = new Color(0.03f, 0.05f, 0.09f, 0.72f);
            var drt = dimImg.rectTransform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;

            var card = new GameObject("PauseCard");
            card.transform.SetParent(canvasGo.transform, false);
            var cardImg = card.AddComponent<UnityEngine.UI.Image>();
            var gold = Resources.Load<Sprite>("UI/panel_gold");
            if (gold != null)
            {
                cardImg.sprite = gold;
                cardImg.type = UnityEngine.UI.Image.Type.Sliced;
                cardImg.pixelsPerUnitMultiplier = 0.6f;
            }
            else cardImg.color = new Color(0.98f, 0.93f, 0.80f);
            var crt = cardImg.rectTransform;
            crt.anchorMin = new Vector2(0.08f, 0.40f);
            crt.anchorMax = new Vector2(0.92f, 0.70f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;

            var title = MakeHudText(card.transform, new Vector2(0.5f, 0.70f), Vector2.zero, 76f);
            title.text = SummaRace.Constants.GameText.RacePauseTitle;
            title.color = new Color(0.32f, 0.19f, 0.02f);
            title.fontStyle = FontStyles.Bold;

            var body = MakeHudText(card.transform, new Vector2(0.5f, 0.34f), Vector2.zero, 42f);
            body.text = SummaRace.Constants.GameText.RacePauseBody;
            body.color = new Color(0.35f, 0.25f, 0.10f);
            body.rectTransform.sizeDelta = new Vector2(700f, 140f);

            // Coming back is the big, obvious, green thing — the same CTA treatment as START.
            var ring = new GameObject("ResumeRing");
            ring.transform.SetParent(canvasGo.transform, false);
            var ringImg = ring.AddComponent<UnityEngine.UI.Image>();
            ringImg.sprite = greenPillSprite != null ? greenPillSprite : worldCardSprite;
            if (ringImg.sprite != null) ringImg.type = UnityEngine.UI.Image.Type.Sliced;
            ringImg.color = new Color(0.10f, 0.30f, 0.14f);
            ringImg.raycastTarget = false;
            var ringRt = ringImg.rectTransform;
            ringRt.anchorMin = ringRt.anchorMax = ringRt.pivot = new Vector2(0.5f, 0.28f);
            ringRt.sizeDelta = new Vector2(648f, 206f);

            var resume = new GameObject("ResumeButton");
            resume.transform.SetParent(canvasGo.transform, false);
            var rImg = resume.AddComponent<UnityEngine.UI.Image>();
            rImg.sprite = greenPillSprite != null ? greenPillSprite : worldCardSprite;
            if (rImg.sprite != null) rImg.type = UnityEngine.UI.Image.Type.Sliced;
            rImg.color = greenPillSprite != null ? Color.white : new Color(0.30f, 0.75f, 0.35f);
            var rRt = rImg.rectTransform;
            rRt.anchorMin = rRt.anchorMax = rRt.pivot = new Vector2(0.5f, 0.28f);
            rRt.sizeDelta = new Vector2(620f, 180f);
            var rBtn = resume.AddComponent<UnityEngine.UI.Button>();
            rBtn.targetGraphic = rImg;
            rBtn.onClick.AddListener(ResumeFromPause);

            var rLabel = MakeHudText(resume.transform, new Vector2(0.5f, 0.5f), Vector2.zero, 58f);
            rLabel.text = SummaRace.Constants.GameText.RaceResumeLabel;
            rLabel.color = Color.white;
            rLabel.fontStyle = FontStyles.Bold;
            rLabel.rectTransform.sizeDelta = new Vector2(580f, 150f);

            // Leaving is small, quiet and two-tap — the same shape as the Reader's back chip and
            // the teacher screen's destructive actions. One tap can never end a run.
            var leave = new GameObject("LeaveChip");
            leave.transform.SetParent(canvasGo.transform, false);
            var lImg = leave.AddComponent<UnityEngine.UI.Image>();
            lImg.sprite = WoodPlaqueSprite();
            lImg.type = UnityEngine.UI.Image.Type.Sliced;
            lImg.color = new Color(0.24f, 0.16f, 0.09f, 0.95f);
            var lRt = lImg.rectTransform;
            lRt.anchorMin = lRt.anchorMax = lRt.pivot = new Vector2(0.5f, 0.135f);
            lRt.sizeDelta = new Vector2(380f, 110f);
            var lBtn = leave.AddComponent<UnityEngine.UI.Button>();
            lBtn.targetGraphic = lImg;
            lBtn.onClick.AddListener(OnLeaveTapped);

            _leaveLabel = MakeHudText(leave.transform, new Vector2(0.5f, 0.5f), Vector2.zero, 40f);
            _leaveLabel.text = SummaRace.Constants.GameText.RaceLeaveLabel;
            _leaveLabel.color = new Color(1f, 0.94f, 0.84f);
            _leaveLabel.fontStyle = FontStyles.Bold;
            _leaveLabel.rectTransform.sizeDelta = new Vector2(350f, 100f);

            canvasGo.SetActive(false);
        }

        /// <summary>
        /// Freeze everything, safely. Three things have to move together or the race half-stops:
        /// their TrackManager (the world), Time.timeScale (animation, tweens, their own Update
        /// arithmetic) and AudioListener.pause.
        /// <para>
        /// AudioListener.pause is not decoration — it is Trash Dash's own "we are paused"
        /// singleton (CharacterInputController reads it, GameState.Pause early-returns on it).
        /// Setting it here means their focus-loss Pause() becomes a no-op while our screen is
        /// up, so their pause menu, their wholeUI hide and their Quit-to-Loadout can never
        /// appear over ours.
        /// </para>
        /// EVERY exit from this state restores timeScale and audio: ResumeFromPause, the leave
        /// path, OnApplicationFocus, and OnDestroy unconditionally — a frozen timeScale escaping
        /// this scene once left the whole app silent and motionless until it was restarted
        /// (F44), and that must not come back.
        /// </summary>
        private void OpenPause()
        {
            if (_paused || _finished || _leaving || !_runReleased) return;

            // Before the listener is muted, or it is a silent button.
            if (SummaRace.Core.AudioManager.Instance != null)
                SummaRace.Core.AudioManager.Instance.PlaySfx(SummaRace.Constants.AudioKeys.SfxClick);

            _paused = true;
            var track = TrackManager.instance;
            _wasMovingBeforePause = track != null && track.isMoving;
            if (track != null && track.isMoving) track.StopMove();

            Time.timeScale = 0f;
            AudioListener.pause = true;

            DisarmLeave();
            if (_pauseRoot != null) _pauseRoot.SetActive(true);
            if (_pauseChip != null) _pauseChip.SetActive(false);
            ApplyPreviewVisibility();

            SummaRace.Core.EventBus.Raise(new SummaRace.Core.RacePauseChanged { paused = true });
        }

        /// <summary>Back into the run exactly where it stopped. StartMove(false) keeps the speed
        /// the run had reached — StartMove(true) would reset it to minSpeed, which would turn a
        /// pause into a penalty.</summary>
        private void ResumeFromPause()
        {
            if (!_paused || _leaving) return;
            _paused = false;

            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (SummaRace.Core.AudioManager.Instance != null)
                SummaRace.Core.AudioManager.Instance.PlaySfx(SummaRace.Constants.AudioKeys.SfxClick);

            if (_pauseRoot != null) _pauseRoot.SetActive(false);
            DisarmLeave();
            if (_pauseChip != null && !_finished) _pauseChip.SetActive(true);
            ApplyPreviewVisibility();

            var track = TrackManager.instance;
            if (_wasMovingBeforePause && track != null && !_finished) track.StartMove(false);

            SummaRace.Core.EventBus.Raise(new SummaRace.Core.RacePauseChanged { paused = false });
        }

        /// <summary>Two taps to leave a run. The first only arms it.</summary>
        private void OnLeaveTapped()
        {
            if (_leaving) return;
            if (!_leaveArmed) { ArmLeave(); return; }
            LeaveRace();
        }

        private void ArmLeave()
        {
            _leaveArmed = true;
            _leaveArmedAt = Time.unscaledTime;
            if (_leaveLabel != null)
            {
                _leaveLabel.text = SummaRace.Constants.GameText.RaceLeaveConfirm;
                _leaveLabel.color = new Color(1f, 0.86f, 0.35f);
            }
        }

        private void DisarmLeave()
        {
            _leaveArmed = false;
            if (_leaveLabel != null)
            {
                _leaveLabel.text = SummaRace.Constants.GameText.RaceLeaveLabel;
                _leaveLabel.color = new Color(1f, 0.94f, 0.84f);
            }
        }

        /// <summary>
        /// The learner chose to leave. Three obligations, in this order:
        ///  1. TELL THE STUDY. The run is partial, and SessionLogService already knows how to
        ///     write a partial run (empty finishedIso) — so this routes through that mechanism
        ///     rather than inventing a second one, and names the reason so a deliberate exit is
        ///     never read as a device that died mid-story.
        ///  2. HAND BACK A CLEAN ENGINE. timeScale and AudioListener are restored here as well
        ///     as in OnDestroy, because the scene load itself runs on unscaled time and would
        ///     otherwise complete with the app still frozen.
        ///  3. LEAVE THROUGH OUR OWN DOOR. SceneLoader.Go, never their QuitToLoadout — that
        ///     ends in the runner kit's menus, which is not a place this game has.
        /// </summary>
        private void LeaveRace()
        {
            if (_leaving) return;
            _leaving = true;
            _finished = true; // stops the gate/miss/stranded machinery for good

            SummaRace.Core.EventBus.Raise(new SummaRace.Core.RunAbandoned
            {
                reason = "race_left_by_learner"
            });

            Time.timeScale = 1f;
            AudioListener.pause = false;
            _paused = false;

            var track = TrackManager.instance;
            if (track != null && track.isMoving) track.StopMove();

            // Their music is DontDestroyOnLoad and would otherwise follow the learner out.
            var mp = MusicPlayer.instance;
            if (mp != null)
                foreach (var src in mp.GetComponentsInChildren<AudioSource>()) src.Stop();

            if (_pauseRoot != null) _pauseRoot.SetActive(false);
            HideOptionPreview();

            // Back to the cards they came from, so choosing again is one tap (the Reader's back
            // button lands in the same place).
            SummaRace.Core.SceneLoader.Go(SummaRace.Constants.SceneNames.StorySelect);
        }

        /// <summary>The 5-slot SWBST inventory strip across the top, styled as wooden plaques on
        /// a wooden board. Slot state (empty "?" / current pulsing / collected word) is set by
        /// RefreshTracker; a collected pick flies into its slot (FlyCollectedToSlot).</summary>
        private void BuildTracker(Transform parent)
        {
            var row = new GameObject("SwbstTracker");
            row.transform.SetParent(parent, false);
            var rrt = row.AddComponent<RectTransform>();
            // Anchored by its BOTTOM edge to a fraction of the canvas, not by its top edge to the
            // top of the screen — see TrackerBottomY.
            rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, TrackerBottomY);
            rrt.pivot = new Vector2(0.5f, 0f);
            // Sized down from 1052x168: on a 1080-wide reference that was running edge to edge
            // and dominating the screen, which matters more here than on a HUD because the
            // learner has to watch the road and read a card at the same time.
            // -26 put the board 26px from the top of a 1920-tall reference, i.e. underneath the
            // Android status bar and inside the notch cutout on most phones. Dropped clear of it.
            //
            // x = -52 (was 0, dead centre): the pause chip is the only way out of a run and had
            // to reach the 48dp minimum, which needs 144px of gutter beside this board — the
            // centred board left only 100px, and this board CANNOT be narrowed (see the slot
            // arithmetic below: zero headroom). So the row shifts left by 52 instead, putting
            // its slots at x[68,908] and its left margin at 48px, and the chip takes x[908,1052].
            // Nothing here is interactive, so the shift costs composition only, not reach.
            rrt.anchoredPosition = new Vector2(-52f, 0f);
            // 880 wide, not 774 — see the slot arithmetic below. Still 100px of margin each
            // side of a 1080-wide reference.
            rrt.sizeDelta = new Vector2(880f, 122f);

            var wood = WoodPlaqueSprite();

            // Wooden backing board behind the five slots (darker tint = the frame/board).
            var board = new GameObject("Board");
            board.transform.SetParent(row.transform, false);
            var bimg = board.AddComponent<UnityEngine.UI.Image>();
            bimg.sprite = wood; bimg.type = UnityEngine.UI.Image.Type.Sliced;
            bimg.color = new Color(0.34f, 0.22f, 0.11f);
            bimg.raycastTarget = false;
            var brt = bimg.rectTransform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;

            // SIZED AGAINST THE LONGEST ELEMENT NAME — do not shrink this back.
            // The plaques carry the five SWBST names, a fixed known set: SOMEBODY, WANTED, BUT,
            // SO, THEN. "SOMEBODY" needs 128.3px at the 24pt autosize floor (and more with
            // synthetic bold); at the old slotW 138 the label box was 138 - 2*10 = 118px, so the
            // first slot of all 30 races ellipsised for every learner in every session. The fix
            // is the plaque, never the font floor — a 7-8" tablet is already the binding case
            // for small type in this game.
            //   5*160 + 4*10 = 840 inside an 880-wide board => 140px label box vs 128.3 needed.
            //   The other four are far shorter (WANTED ~104px is the next longest), so
            //   SOMEBODY is the only binding case and 140px clears all five.
            const float slotW = 160f, slotH = 96f, gap = 10f;
            float total = 5f * slotW + 4f * gap;
            float startX = -total * 0.5f + slotW * 0.5f;

            for (int i = 0; i < 5; i++)
            {
                var slot = new GameObject("Slot_" + i);
                slot.transform.SetParent(row.transform, false);
                var img = slot.AddComponent<UnityEngine.UI.Image>();
                img.sprite = wood; img.type = UnityEngine.UI.Image.Type.Sliced;
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(slotW, slotH);
                rt.anchoredPosition = new Vector2(startX + i * (slotW + gap), 0f);
                _slotBg[i] = img;
                _slotRect[i] = rt;

                var lblGo = new GameObject("Label");
                lblGo.transform.SetParent(slot.transform, false);
                var lbl = lblGo.AddComponent<TextMeshProUGUI>();
                if (worldLabelFont != null) lbl.font = worldLabelFont;
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.fontStyle = FontStyles.Bold;
                lbl.enableAutoSizing = true;
                // 18pt on a 1080-wide reference is unreadable on a phone, and Overflow let long
                // text escape the plaque entirely. Floor it at a legible size and clip instead,
                // so no future content can spill across the board again.
                lbl.fontSizeMin = 24f;
                lbl.fontSizeMax = 50f;
                lbl.overflowMode = TextOverflowModes.Ellipsis;
                // One word per plaque, so never break it: with wrapping on, "SOMEBODY" split as
                // "SOMEBO / DY". Off, autosize shrinks it to fit on a single line instead.
                lbl.textWrappingMode = TextWrappingModes.NoWrap;
                lbl.raycastTarget = false;
                var lrt = lbl.rectTransform;
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(10f, 8f); lrt.offsetMax = new Vector2(-10f, -8f);
                _slotLabel[i] = lbl;
            }
        }

        /// <summary>Repaints the SWBST slots. Elements resolve in order: slot i is collected once
        /// the current target has passed it. Empty slots show a faded "?" (something to collect),
        /// the current target is a vivid SWBST-coloured plaque that pulses, collected slots turn
        /// their SWBST colour and show the word.</summary>
        private void RefreshTracker()
        {
            if (_story == null || _slotBg[0] == null) return;
            int current = _activeGateRoot != null ? _activeElement : _pendingElement;

            for (int i = 0; i < 5; i++)
            {
                if (_slotBg[i] == null) continue;
                var lbl = _slotLabel[i];
                string type = _story.elements[i].type;
                string letter = string.IsNullOrEmpty(type) ? "?" : type.Substring(0, 1).ToUpper();

                if (i < current) // collected
                {
                    _slotBg[i].color = SummaRace.Constants.SwbstPalette.ForIndex(i);
                    // The element NAME, never the answer sentence. A collected answer can be 50+
                    // characters ("Molly used an 'I message' to tell Bella how she felt") and this
                    // plaque is 138x96 — autosizing bottomed out at the 18pt floor and the text
                    // spilled over the neighbouring slots, unreadable. The answer itself already
                    // flies into this slot (FlyCollectedToSlot), which is what ties it to the
                    // element; what the tracker has to keep showing is the framework.
                    lbl.text = string.IsNullOrEmpty(type) ? letter : type.ToUpperInvariant();
                    lbl.color = Color.white;
                    _slotRect[i].localScale = Vector3.one;
                }
                else if (i == current) // current target — vivid + pulse
                {
                    _slotBg[i].color = Color.Lerp(SummaRace.Constants.SwbstPalette.ForIndex(i), Color.white, 0.12f);
                    lbl.text = letter;
                    lbl.color = Color.white;
                    _slotRect[i].localScale = Vector3.one * 1.12f;
                    Tween.PunchScale(_slotRect[i], Vector3.one * 0.1f, 0.45f);
                }
                else // upcoming / empty — natural wood + faded "?"
                {
                    _slotBg[i].color = new Color(0.62f, 0.44f, 0.25f);
                    lbl.text = "?";
                    lbl.color = new Color(1f, 0.96f, 0.85f, 0.5f);
                    _slotRect[i].localScale = Vector3.one;
                }
            }
        }

        /// <summary>Light beveled wooden plaque (tintable, 9-sliced). Generated once: a warm
        /// cream base with a raised frame, a recessed centre, and subtle grain — so tinting it
        /// with an SWBST colour reads as a coloured wooden block.</summary>
        private static Sprite _woodPlaque;
        private static Sprite WoodPlaqueSprite()
        {
            if (_woodPlaque != null) return _woodPlaque;
            const int S = 100, R = 22, FRAME = 10;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float half = S * 0.5f;
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float px = x - (S - 1) * 0.5f, py = y - (S - 1) * 0.5f;
                float qx = Mathf.Abs(px) - (half - R);
                float qy = Mathf.Abs(py) - (half - R);
                float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                float dist = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - R; // <=0 inside
                float depth = -dist;
                if (depth <= 0f) { tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f)); continue; }

                float grain = (Mathf.PerlinNoise(x * 0.12f, y * 0.5f) - 0.5f) * 0.10f;
                float lum;
                if (depth < FRAME) lum = 0.86f + (FRAME - depth) / FRAME * 0.10f; // raised bright frame
                else lum = 0.66f + (y / (float)S) * 0.10f;                        // recessed centre, top a touch darker
                lum = Mathf.Clamp01(lum + grain);
                // warm cream tint so an SWBST colour multiply still shows through
                tex.SetPixel(x, y, new Color(lum, lum * 0.93f, lum * 0.80f, 1f));
            }
            tex.Apply();
            _woodPlaque = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(R, R, R, R));
            return _woodPlaque;
        }

        /// <summary>Collected word lifts off the card and flies up into its SWBST slot — the
        /// "into the inventory" beat (F40). A screen-space token so it lands exactly on the slot.</summary>
        private void FlyCollectedToSlot(int element, Vector3 worldPos)
        {
            if (element < 0 || element >= 5 || _slotRect[element] == null) return;
            var hud = transform.Find("SummaRaceHud");
            if (hud == null) return;

            var cam = Camera.main;
            Vector3 startScreen = cam != null ? cam.WorldToScreenPoint(worldPos) : _slotRect[element].position;
            startScreen.z = 0f;

            // The token is a PILL, not bare text. Measured before: the answer was set at a fixed
            // 60pt in a 420x130 box with no autosizing, and a 52-character SO/THEN line ("Molly
            // used an "I message" to tell Bella how she felt") measured 1581x363 — five lines of
            // white text overflowing its own box by 233px, unbacked, sweeping across the middle
            // of the screen for most of a second right when the learner needs to see the road.
            // Autosized inside a dark plaque it stays one readable object at any answer length.
            var tokenGo = new GameObject("CollectToken");
            tokenGo.transform.SetParent(hud, false);
            var pill = tokenGo.AddComponent<UnityEngine.UI.Image>();
            pill.sprite = WoodPlaqueSprite();
            pill.type = UnityEngine.UI.Image.Type.Sliced;
            pill.color = new Color(0.10f, 0.12f, 0.16f, 0.88f); // same backing as the feedback line
            pill.raycastTarget = false;
            var pillRt = pill.rectTransform;
            pillRt.sizeDelta = new Vector2(600f, 180f);
            pillRt.position = startScreen;

            var textGo = new GameObject("Word");
            textGo.transform.SetParent(tokenGo.transform, false);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            if (worldLabelFont != null) tmp.font = worldLabelFont;
            tmp.text = _story.elements[element].correct;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 26f;
            tmp.fontSizeMax = 58f;
            var trt = tmp.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(18f, 14f); trt.offsetMax = new Vector2(-18f, -14f);

            Vector3 slotPos = _slotRect[element].position; slotPos.z = 0f;
            tokenGo.transform.localScale = Vector3.one * 0.5f;
            Tween.Scale(tokenGo.transform, Vector3.one * 1.25f, 0.28f, Ease.OutBack);
            Tween.Position(tokenGo.transform, slotPos, 0.45f, Ease.InQuad, startDelay: 0.42f);
            Tween.Scale(tokenGo.transform, Vector3.one * 0.4f, 0.45f, Ease.InQuad, startDelay: 0.42f)
                .OnComplete(() =>
                {
                    if (tokenGo != null) Destroy(tokenGo);
                    if (_slotRect[element] != null) Tween.PunchScale(_slotRect[element], Vector3.one * 0.25f, 0.35f);
                });
        }

        /// <summary>Radial-alpha sprite: transparent centre, opaque edges — a soft vignette frame.</summary>
        private static Sprite MakeVignetteSprite()
        {
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var c = (S - 1) * 0.5f;
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c; // 0 centre → ~1.41 corner
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.55f) / 0.45f)); // clear inside 0.55r
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        /// <summary>Spawns the collect sparkle, retinted warm gold, and auto-cleans it.</summary>
        private void SpawnCollectSparkle(Vector3 worldPos)
        {
            if (collectSparkleFxPrefab == null) return;
            var fx = Instantiate(collectSparkleFxPrefab, worldPos, Quaternion.identity);
            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                var col = main.startColor.color;
                main.startColor = new Color(StoryGold.r, StoryGold.g, StoryGold.b, col.a);
            }
            Destroy(fx, 2f);
        }

        /// <summary>
        /// The chaser. Not parented to anything: the track uses a floating origin that
        /// recenters roughly every 100m, so a world-space follower would be left behind.
        /// Its position is recomputed from the runner's CURRENT transform every frame,
        /// which is recenter-proof.
        /// </summary>
        private void SpawnPatrol()
        {
            // The 3D chaser is off. Three attempts each fixed one property and broke another, and
            // the owner reported it broken after every one:
            //   behind the runner  -> its bounds intersected the kid and it drew as a blob
            //                         growing out of his back;
            //   out on the shoulder-> it ran level with him and read as a jogging companion,
            //                         and sat half off the right edge of a portrait screen;
            //   placed from live renderer bounds -> the bounds are produced BY the run animation
            //                         and swing with the stride, so the placement chased its own
            //                         animation and it visibly slid back and forth.
            // The geometry is the root cause and it is not tunable: the approved chase camera is
            // 5m back and 4m up at ~15deg, so a ground-level figure behind the runner is either
            // inside him or under the frame, and the camera is off-limits.
            // Nothing is lost by cutting it. It never catches anyone (D7 — timesCaught stays 0),
            // it carries no rule, and the wrong-answer beat is already carried by the amber
            // vignette and the feedback line. Two days from a study, a character that reads as a
            // rendering fault is worse than no character. Flip this to true only alongside a
            // camera change, and re-read the three failures above first.
            if (!SummaRace.Constants.GameRules.RacePatrolEnabled) return;

            var runner = TrackManager.instance != null ? TrackManager.instance.characterController : null;
            if (runner == null) return;

            GameObject go;
            if (patrolPrefab != null)
            {
                go = Instantiate(patrolPrefab);
                HideCopAccessories(go);
            }
            else
            {
                // Grey-box fallback so the race still reads correctly with nothing wired.
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = "Patrol (greybox)";
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col); // never collides — it is pressure, not an obstacle
                var rend = go.GetComponent<Renderer>();
                if (rend != null) rend.material.color = new Color(0.25f, 0.35f, 0.85f);
            }

            _patrol = go.transform;
            _patrolAnim = go.GetComponentInChildren<Animator>();
            if (_patrolAnim != null) _patrolAnim.SetBool("Running", true);

            // Station him out of shot at the depth he will run at for the whole race: the bump
            // moves him sideways, never forward, so there is nothing to ease in from behind.
            var p = runner.transform.position;
            _patrolGap = SummaRace.Constants.GameRules.PatrolBodyMargin + 1f;
            // Well outside the frame; UpdatePatrol re-derives both axes from the live camera and
            // the live bounds on its first frame, before he is ever visible.
            _patrol.position = new Vector3(p.x + _patrolSide * 6f, p.y, p.z - _patrolGap);
            _patrol.rotation = Quaternion.identity; // faces down the road, same as the runner
            go.SetActive(_runReleased);
        }

        /// <summary>Hides the BitGem cop's weapons and its two detached hand props. The
        /// rifle + doughnut are STATIC children of the root (not skinned, not bone-parented),
        /// so the animated skeleton runs ~1.4m ahead of them and they float behind the body;
        /// the holstered gun rides the thigh bone. An armed cop chasing a Grade-4 kid is also
        /// tonally wrong (GDD D7). Null-safe: a renamed/missing child is simply skipped.</summary>
        private static void HideCopAccessories(GameObject cop)
        {
            string[] hide = { "rifle", "holster_w_gun", "doughnut" };
            foreach (var t in cop.GetComponentsInChildren<Transform>(true))
                for (int i = 0; i < hide.Length; i++)
                    if (t.name == hide[i]) { t.gameObject.SetActive(false); break; }
        }

        /// <summary>
        /// "Appear only on a bump" chaser, arriving from the SIDE instead of from behind. A
        /// wrong pick sets _menaceTimer; while that runs he slides in beside the kid's shoulder,
        /// then slides back out of shot. He never catches (GDD D7). His position is recomputed
        /// from the LIVE player every frame (never a lerp of world-Z toward a target), so a
        /// floating-origin recenter is absorbed with no stall.
        ///
        /// He arrives sideways because arriving from behind is geometrically impossible in this
        /// frame — the full measurement is on GameRules.PatrolSurgeGap. In short: a chaser behind
        /// the kid sits between the kid and the lens, so closing the gap only drags him into the
        /// camera and out of the bottom of the screen (at 2.9m only his hat was in frame, at 2.1m
        /// his mesh was inside the kid's), and portrait's 35deg horizontal FOV leaves him sharing
        /// the kid's screen column at every gap. Holding him at a constant depth and moving him
        /// ACROSS the frame instead puts a whole, readable second runner at the kid's shoulder,
        /// and keeps him from sweeping through the camera plane on his way in.
        /// </summary>
        private void UpdatePatrol(TrackManager track)
        {
            if (_patrol == null || track == null) return;
            var runner = track.characterController;
            if (runner == null) return;

            if (!_patrol.gameObject.activeSelf)
            {
                if (!_runReleased) return;
                _patrol.gameObject.SetActive(true);
                // Set Running only now: a bool set on an Animator whose GameObject was
                // inactive at spawn is reset to its default (false) when the object enables,
                // so the cop stood still. Setting it post-activation makes the run loop play.
                if (_patrolAnim != null)
                {
                    _patrolAnim.SetBool("Running", true);
                    // The cop spends every clean run hidden behind the camera. Its Animator
                    // ships as CullUpdateTransforms, which keeps the state machine ticking but
                    // does NOT write bone transforms while the renderers are invisible — and
                    // visibility is resolved from the PREVIOUS frame's culling. So the first
                    // frame it rushed into view it drew in its authored bind pose and snapped
                    // into the run only on the next frame. That reads as badly here as it
                    // possibly could: the BitGem cop is rigid-part, not skinned (19 separate
                    // mesh renderers), so an unposed frame is limbs scattered at bind offsets.
                    _patrolAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
                // Lock the cop to the runner's ground height once, so it no longer floats:
                // following playerPos.y live made it rise with the runner's jumps / any
                // character y-offset. The road is flat and recentres only in X/Z, so a
                // captured constant is stable for the whole run.
                _patrolGroundY = runner.transform.position.y;
                _patrolGrounded = true;

                // Snap to his station before the first frame he is visible in. SmoothDamp from
                // wherever he was left would walk him across the frame in full view of the
                // learner, which is the one thing "appear only on a bump" must never do.
                var startPos = runner.transform.position;
                _patrol.position = new Vector3(startPos.x + _patrolSide * 6f,
                    _patrolGroundY, startPos.z - _patrolGap);
                _patrolMoveVel = 0f;
            }

            bool surging = _menaceTimer > 0f;
            if (surging) _menaceTimer -= Time.deltaTime;

            var playerPos = runner.transform.position;
            var cam = Camera.main;
            // The kid's LANE lives on the character, not on the controller's own transform:
            // characterController.transform.position.x is 0 for the whole run (measured), the
            // 1.5m lane offset is on characterCollider/character. So the old "lane-match
            // smoothing" was lerping toward a constant — the cop ran down the centre line
            // whatever lane the kid was in, which is half of why he read as misplaced.
            var bodyT = runner.characterCollider != null
                ? runner.characterCollider.transform
                : runner.transform;
            float kidX = bodyT.position.x;

            // Which shoulder: the one the kid is NOT on, so the two never share a screen column.
            // Sticky through the middle lane, or he would slide across and back on every pass.
            //
            // CHOSEN AT REST OR ON THE FIRST FRAME OF A SURGE, NEVER MID-SURGE. He now runs almost
            // abreast of the kid (see the framing gap below), so a side flip while he is on screen
            // would sweep him straight THROUGH the runner. Off screen the choice is free; once he
            // is in shot he keeps his shoulder and simply slides outward if the kid changes lane
            // into him.
            if (!surging || !_wasSurging)
            {
                if (kidX > SummaRace.Constants.GameRules.PatrolSideDeadband) _patrolSide = -1f;
                else if (kidX < -SummaRace.Constants.GameRules.PatrolSideDeadband) _patrolSide = 1f;
            }
            _wasSurging = surging;

            // EVERYTHING BELOW POSITIONS HIS VISIBLE BODY, NOT HIS PIVOT. His rendered mass is not
            // centred on his transform and the offset MOVES: measured live it reaches 0.79m
            // forward and 0.65m sideways as the run clip translates his 19-part rigid rig while
            // the transform stays put. So a gap set on the transform was up to 0.8m tighter than
            // it read, always toward the kid — at a nominal 1.5m gap the two models' bounds still
            // overlapped by 0.81m along the run. Reading the offset off the live bounds each frame
            // fixes it for every stride, and for any future character swap, instead of for one
            // hand-picked number.
            Bounds copB, kidB;
            bool haveCop = BodyBounds(_patrol, ref _patrolRenderers, out copB);
            bool haveKid = BodyBounds(bodyT, ref _kidRenderers, out kidB);
            Vector3 bodyOffset = haveCop ? copB.center - _patrol.position : Vector3.zero;

            // ---- DEPTH: how far back he runs, solved from the camera's own projection ----
            //
            // The old rule was "hold him a fixed 1.75m behind". Measured in play mode that put his
            // body 3.25m from the lens, where the camera's 15deg downward pitch crops everything
            // nearer than ~4.1m off the BOTTOM of the frame: 58% of him was below the screen and
            // 21% off the right edge, i.e. ~27% of the cop was visible, and what was visible was
            // drawn on top of the runner. Solving "put his feet just inside the bottom edge"
            // instead gives ~0.6m at the shipped camera — he runs almost abreast of the kid but
            // well out to the side, and the WHOLE cop is in frame. Separation is then guaranteed
            // sideways (below) rather than along the run, which is the axis the frame can afford.
            _patrolGap = FramingGap(cam, playerPos, copB, haveCop);

            // ---- SIDEWAYS: anchored on the KID and on his own SCREEN EDGE ----
            float targetX = LateralTarget(cam, playerPos, kidX, copB, kidB, haveCop && haveKid, surging);
            // SmoothDamp so he slides in on a bump and drifts back out after — natural, not snappy.
            float bodyX = _patrol.position.x + bodyOffset.x;
            float newBodyX = Mathf.SmoothDamp(bodyX, targetX, ref _patrolMoveVel,
                SummaRace.Constants.GameRules.PatrolMoveSmoothTime);

            // HARD CLAMP, not a tuned gap. With the two of them nearly level along the run, the
            // ONLY thing keeping their bounds apart is this: he may never come within the two
            // half-widths of the kid, whatever the smoothing lag, however fast the lane change,
            // and whatever any of the numbers above are retuned to. Applied on the side he is
            // actually on (not the side he is heading for), so it can never teleport him across.
            if (haveCop && haveKid)
            {
                float minSep = kidB.extents.x + copB.extents.x
                             + SummaRace.Constants.GameRules.PatrolBodyMargin;
                float side = newBodyX >= kidX ? 1f : -1f;
                newBodyX = side > 0f ? Mathf.Max(newBodyX, kidX + minSep)
                                     : Mathf.Min(newBodyX, kidX - minSep);
            }

            float groundY = _patrolGrounded ? _patrolGroundY : playerPos.y;
            // y is deliberately NOT offset-corrected: his feet belong on the road, and the
            // transform is the thing that stands on it.
            _patrol.position = new Vector3(newBodyX - bodyOffset.x, groundY,
                playerPos.z - _patrolGap - bodyOffset.z);
            // He runs straight down the road. He is NOT yawed at the kid, however much better
            // that would read: turning him swings that forward mesh offset sideways (a 37deg yaw
            // moved his body 0.65m across, straight back into the kid's column) and inflates his
            // footprint from 1.2m wide to 1.8m, which is what put him back on top of the runner.

            // Amber vignette rides the surge, not the (now cop-independent) danger meter.
            if (_vignette != null)
            {
                var vc = _vignette.color;
                vc.a = Mathf.Lerp(vc.a, surging ? 0.35f : 0f, 6f * Time.deltaTime);
                _vignette.color = vc;
                // Disable it outright when invisible. A full-screen alpha-blended Image still
                // costs a full screen of overdraw at alpha 0, and on a tile-based mobile GPU
                // that is pure bandwidth for every frame of an otherwise clean run — which is
                // most frames, since the vignette only shows during a menace surge.
                bool visible = vc.a > 0.004f;
                if (_vignette.enabled != visible) _vignette.enabled = visible;
            }
        }

        /// <summary>
        /// How far back he runs. Solved from the camera's own projection rather than tuned: find
        /// the distance at which his FEET land on viewport y = PatrolFeetScreenY, so the whole cop
        /// is inside the frame instead of a head floating over the bottom edge. Anything nearer
        /// than that is cropped away by the camera's downward pitch — which is what the shipped
        /// 1.75m gap was doing (58% of him below the screen, measured).
        ///
        /// Derived live off the camera basis, so the three camera retunes this race has already
        /// had would each have carried him with them instead of silently re-breaking this.
        /// </summary>
        private float FramingGap(Camera cam, Vector3 playerPos, Bounds copB, bool haveCop)
        {
            float fallback = SummaRace.Constants.GameRules.PatrolBodyMargin
                           + (haveCop ? copB.extents.z : 1f);
            if (cam == null || !haveCop) return fallback;

            var f = cam.transform.forward;
            var u = cam.transform.up;
            float k = (2f * SummaRace.Constants.GameRules.PatrolFeetScreenY - 1f)
                    * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float denom = u.z - k * f.z;
            if (Mathf.Abs(denom) < 0.0001f) return fallback;

            float dy = copB.min.y - cam.transform.position.y;   // his feet, relative to the lens
            float dz = dy * (k * f.y - u.y) / denom;            // …the depth that frames them
            float gap = playerPos.z - cam.transform.position.z - dz;
            return Mathf.Clamp(gap, SummaRace.Constants.GameRules.PatrolMinGap,
                                    SummaRace.Constants.GameRules.PatrolMaxGap);
        }

        /// <summary>
        /// Where the chaser belongs sideways: parked outside the frame at rest, beside the kid
        /// during a bump.
        ///
        /// THIS IS THE FIX FOR "HE APPEARS INSIDE". It used to be a fraction of the view's
        /// half-width AT HIS OWN DEPTH, anchored on the camera. At his depth the half-width is
        /// barely a metre, so 0.80 of it is x = 0.82m — inside the kid's own arm swing (live
        /// bounds +/-0.84m). Measured mid-surge: 0.44m of lateral overlap, 0.22m of separation
        /// along the run, and screen rects overlapping by 14.4% x 28.7%. Two bodies drawn that
        /// close with crossing silhouettes read as one fused body, whatever Bounds.Intersects says.
        ///
        /// Now the surge position is whichever is further out of:
        ///   * SCREEN: his near silhouette edge clears the kid's far silhouette edge by
        ///     PatrolScreenClearance — computed at each body's OWN depth, because they are not at
        ///     the same distance from the lens and the kid is much wider in frame than in metres;
        ///   * WORLD: the two half-widths plus PatrolBodyMargin, so their bounds cannot touch.
        /// Both are read from the live camera and the live bounds, so a camera retune or a
        /// character swap carries them instead of stranding him.
        /// </summary>
        private float LateralTarget(Camera cam, Vector3 playerPos, float kidX,
                                    Bounds copB, Bounds kidB, bool haveBodies, bool surging)
        {
            if (cam == null)
                return kidX + _patrolSide * (surging ? 1.6f : 3f); // mid-swap: sane and off centre

            float camX = cam.transform.position.x;
            float tanHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float copHalfWidth = Mathf.Max(0.1f,
                tanHalf * cam.aspect * DepthAlongAxis(cam, playerPos.z - _patrolGap, copB, haveBodies));

            if (!surging)
                return camX + _patrolSide * SummaRace.Constants.GameRules.PatrolRestScreenX * copHalfWidth;

            if (!haveBodies)
                return camX + _patrolSide * 1.0f * copHalfWidth;

            float kidHalfWidth = Mathf.Max(0.1f,
                tanHalf * cam.aspect * DepthAlongAxis(cam, kidB.center.z, kidB, true));

            // The kid's far silhouette edge, in normalised half-width units at HIS depth.
            float kidEdge = (kidX + _patrolSide * kidB.extents.x - camX) / kidHalfWidth;
            // Put the cop's near edge that far out plus the clearance, then convert his own near
            // edge back into a body centre at HIS depth.
            float byScreen = camX
                + (kidEdge + _patrolSide * SummaRace.Constants.GameRules.PatrolScreenClearance) * copHalfWidth
                + _patrolSide * copB.extents.x;
            float byWorld = kidX + _patrolSide * (kidB.extents.x + copB.extents.x
                + SummaRace.Constants.GameRules.PatrolBodyMargin);

            return _patrolSide > 0f ? Mathf.Max(byScreen, byWorld) : Mathf.Min(byScreen, byWorld);
        }

        /// <summary>Distance from the lens along the camera's own axis for a body whose centre
        /// sits at world z <paramref name="centreZ"/>. Not simply (z - camZ): the camera is
        /// pitched, so height matters, and using the flat difference understates the depth (and
        /// therefore the frame width) by ~20% at this pitch.</summary>
        private static float DepthAlongAxis(Camera cam, float centreZ, Bounds b, bool haveBounds)
        {
            var f = cam.transform.forward;
            float dy = (haveBounds ? b.center.y : 1f) - cam.transform.position.y;
            float dz = centreZ - cam.transform.position.z;
            return Mathf.Max(0.5f, dy * f.y + dz * f.z);
        }

        /// <summary>World-space bounds of a rigged model's VISIBLE renderers — what is actually on
        /// screen, as opposed to where its transform happens to sit. The array is cached because
        /// this runs every frame over a 19-part rigid rig and GetComponentsInChildren allocates;
        /// disabled renderers are skipped so the cop's hidden weapons (HideCopAccessories) cannot
        /// inflate it. Pass the CHARACTER for the runner, never the controller's own transform —
        /// that subtree returns nonsense bounds hundreds of metres across.</summary>
        private static bool BodyBounds(Transform root, ref Renderer[] cache, out Bounds bounds)
        {
            bounds = default(Bounds);
            if (root == null) return false;
            if (cache == null || cache.Length == 0) cache = root.GetComponentsInChildren<Renderer>(true);

            bool any = false;
            for (int i = 0; i < cache.Length; i++)
            {
                var r = cache[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        /// <summary>
        /// The "get ready" beat between the last Reader question and the run — the
        /// endless race had none, so the learner was dropped straight into a moving
        /// world (Race.unity's RaceController has had this since Phase E; this is the
        /// same idea rebuilt in code, since MainSummaRace is a copy of their scene).
        /// </summary>
        private void BuildBriefing()
        {
            var canvasGo = new GameObject("SummaRaceBriefing");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60; // above our HUD (40) and anything of theirs
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            // Match 0, as the HUD — and here it is load-bearing for a fix that already had to be
            // made once. F46(k) placed Ms. Lumi's speech bubble by working out that START is
            // "centred and 520 wide on a 1080 reference, i.e. x 0.259-0.741", and stopped the
            // bubble at x 0.235. Those are pixel widths converted to fractions of 1080; at match
            // 0.5 on a 1080x2400 phone the canvas is 966 wide, START's dark ring becomes
            // x 0.216-0.784, and it eats the bubble again — the exact "only 'Ready, ru' showed"
            // bug. Do not change this without re-deriving that block.
            scaler.matchWidthOrHeight = 0f;
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            _briefingRoot = canvasGo;

            // Opaque backdrop on OUR art, not a grey scrim: this screen is the learner's
            // first frame of the race, and behind it sits the runner kit's loadout menu.
            // bg_splash is the same backdrop as Boot and the loading overlay, so the race
            // arrives looking like the rest of the game.
            var scrim = new GameObject("Backdrop");
            scrim.transform.SetParent(canvasGo.transform, false);
            var scrimImg = scrim.AddComponent<UnityEngine.UI.Image>();
            var bg = Resources.Load<Sprite>("UI/bg_splash");
            if (bg != null) { scrimImg.sprite = bg; scrimImg.color = Color.white; }
            else scrimImg.color = new Color(0.55f, 0.83f, 0.98f);
            var srt = scrimImg.rectTransform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;

            // Gold-bordered mission card, same panel the loading tips use.
            var card = new GameObject("MissionCard");
            card.transform.SetParent(canvasGo.transform, false);
            var cardImg = card.AddComponent<UnityEngine.UI.Image>();
            var gold = Resources.Load<Sprite>("UI/panel_gold");
            if (gold != null)
            {
                cardImg.sprite = gold;
                cardImg.type = UnityEngine.UI.Image.Type.Sliced;
                cardImg.pixelsPerUnitMultiplier = 0.6f;
            }
            else cardImg.color = new Color(0.98f, 0.93f, 0.80f);
            var crt = cardImg.rectTransform;
            crt.anchorMin = new Vector2(0.06f, 0.34f);
            crt.anchorMax = new Vector2(0.94f, 0.84f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;

            // Gold banner pill overlapping the card's top edge — the game's title language
            // (StorySelect banner, race HUD, old briefing all speak it).
            var titlePill = new GameObject("TitlePill");
            titlePill.transform.SetParent(card.transform, false);
            var tpImg = titlePill.AddComponent<UnityEngine.UI.Image>();
            tpImg.sprite = goldPillSprite != null ? goldPillSprite : worldCardSprite;
            if (tpImg.sprite != null) tpImg.type = UnityEngine.UI.Image.Type.Sliced;
            if (goldPillSprite == null) tpImg.color = new Color(1f, 0.78f, 0.16f);
            var tpRt = tpImg.rectTransform;
            tpRt.anchorMin = new Vector2(0.09f, 0.915f);
            tpRt.anchorMax = new Vector2(0.91f, 1.045f);
            tpRt.offsetMin = Vector2.zero; tpRt.offsetMax = Vector2.zero;

            var title = MakeHudText(titlePill.transform, new Vector2(0.5f, 0.5f), Vector2.zero, 74f);
            title.text = SummaRace.Constants.GameText.RaceBriefingTitle;
            title.color = new Color(0.32f, 0.19f, 0.02f); // deep brown on gold
            title.fontStyle = FontStyles.Bold;

            var body = MakeHudText(card.transform, new Vector2(0.5f, 0.56f), Vector2.zero, 42f);
            body.text = SummaRace.Constants.GameText.RaceBriefingBody(_story.title);
            body.color = new Color(0.35f, 0.25f, 0.10f);
            body.rectTransform.sizeDelta = new Vector2(760f, 300f);
            // Autosized, because the story TITLE is interpolated into this line and titles are
            // content: at a pinned 42pt the longest of the thirty ("Baba Yaga, the Girl, and the
            // Hedgehog") wraps to ~6 lines / ~328px in a 300px box and overflows it. The floor is
            // 32pt so a long title costs a little size rather than the last line, and the max
            // stays 42 so nothing renders differently for the other 29.
            body.enableAutoSizing = true;
            body.fontSizeMin = 32f;
            body.fontSizeMax = 42f;
            _briefingBody = body;   // rewritten by ShowBriefingEscape if the kit never boots

            // The five parts, in the colours they will wear on the gates (F18 palette),
            // so the run's cards are already familiar when the first one arrives.
            var row = new GameObject("Chips", typeof(RectTransform));
            row.transform.SetParent(card.transform, false);
            var rowRt = (RectTransform)row.transform;
            rowRt.anchorMin = rowRt.anchorMax = rowRt.pivot = new Vector2(0.5f, 0.20f);
            rowRt.sizeDelta = new Vector2(760f, 130f);
            for (int i = 0; i < 5 && i < _story.elements.Length; i++)
                MakeChip(rowRt, i, _story.elements[i].type);

            // Ms. Lumi gives the mission — she is the app's voice everywhere else.
            var lumi = Resources.Load<Sprite>("UI/mslumi_wave");
            if (lumi != null)
            {
                var teacher = new GameObject("MsLumi");
                teacher.transform.SetParent(canvasGo.transform, false);
                var timg = teacher.AddComponent<UnityEngine.UI.Image>();
                timg.sprite = lumi;
                timg.preserveAspect = true;
                var trt = timg.rectTransform;
                // mslumi_wave.png is a 2048x1024 (2:1) canvas but Lumi herself only occupies the
                // middle 44% of its width (x 507-1405) and 95% of its height — the rest is
                // transparent padding. preserveAspect fits the 2:1 IMAGE to the box, so a portrait
                // box shrank the visible girl to a fraction of it: she rendered thumbnail-sized in
                // the corner. Size the box for the padding instead, so the girl lands where we want.
                // Girl ~240px wide on a 1080 reference => box 240/0.44 = 547 wide, 273 tall, shifted
                // left by the padding so her left edge sits at the screen margin and she stays clear
                // of the START button (centred, 520 wide => x 0.26-0.74).
                trt.anchorMin = new Vector2(-0.111f, 0.045f);
                trt.anchorMax = new Vector2(0.395f, 0.187f);
                trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
                teacher.transform.localScale = Vector3.one * 0.7f;
                Tween.Scale(teacher.transform, Vector3.one, 0.35f, Ease.OutBack, startDelay: 0.30f);

                // Her cheer, in a little white bubble above her.
                var bubble = new GameObject("LumiBubble");
                bubble.transform.SetParent(canvasGo.transform, false);
                var bImg = bubble.AddComponent<UnityEngine.UI.Image>();
                bImg.sprite = worldCardSprite;
                if (worldCardSprite != null) bImg.type = UnityEngine.UI.Image.Type.Sliced;
                bImg.color = new Color(1f, 1f, 1f, 0.97f);
                var bRt = bImg.rectTransform;
                // THE BUBBLE WAS BEING CUT IN HALF BY THE START BUTTON — only "Ready, ru" showed.
                // The comment two blocks up works out that START is centred and 520 wide on a 1080
                // reference, i.e. x 0.259-0.741 (its dark ring is 548 wide => 0.246-0.754), and
                // that rule was applied to Lumi and then not to her own speech bubble, which ran
                // to x 0.42 and y 0.196-0.252 — straight under the ring, and built earlier in the
                // hierarchy so the ring draws over it.
                //
                // Lumi's rect is x -0.111..0.395, but her ART is only the middle 44% of a 2:1
                // canvas, so the VISIBLE girl stands at x 0.031-0.253 with her head at y ~0.183.
                // The bubble is placed against her visible head, not her rect: it sits directly
                // on top of her and stops at x 0.235, which is 12px clear of the START ring's
                // left edge on a 1080 reference. Taller and narrower than before, so the line
                // wraps to two like a real speech bubble instead of being clipped.
                bRt.anchorMin = new Vector2(0.020f, 0.192f);
                bRt.anchorMax = new Vector2(0.235f, 0.300f);
                bRt.offsetMin = Vector2.zero; bRt.offsetMax = Vector2.zero;
                var bubbleText = MakeHudText(bubble.transform, new Vector2(0.5f, 0.5f), Vector2.zero, 38f);
                bubbleText.text = SummaRace.Constants.GameText.RaceBriefingLumi;
                bubbleText.color = new Color(0.35f, 0.25f, 0.10f);
                bubbleText.fontStyle = FontStyles.Bold;
                // Autosized inside the narrower bubble: the string is content (GameText) and may
                // be re-worded or translated, so it must fit itself rather than be sized by hand.
                bubbleText.enableAutoSizing = true;
                bubbleText.fontSizeMin = 22f;
                bubbleText.fontSizeMax = 38f;
                bubbleText.textWrappingMode = TextWrappingModes.Normal;
                var btRt = bubbleText.rectTransform;
                btRt.anchorMin = Vector2.zero; btRt.anchorMax = Vector2.one;
                // Offsets last: assigning sizeDelta after these would overwrite them.
                btRt.offsetMin = new Vector2(16f, 12f); btRt.offsetMax = new Vector2(-16f, -12f);
                bubble.transform.localScale = Vector3.zero;
                Tween.Scale(bubble.transform, Vector3.one, 0.3f, Ease.OutBack, startDelay: 0.55f);
            }

            // Dark-green ring behind the glossy pill — the game's CTA treatment.
            var ring = new GameObject("StartRing");
            ring.transform.SetParent(canvasGo.transform, false);
            var ringImg = ring.AddComponent<UnityEngine.UI.Image>();
            ringImg.sprite = greenPillSprite != null ? greenPillSprite : worldCardSprite;
            if (ringImg.sprite != null) ringImg.type = UnityEngine.UI.Image.Type.Sliced;
            ringImg.color = new Color(0.10f, 0.30f, 0.14f);
            var ringRt = ringImg.rectTransform;
            ringRt.anchorMin = ringRt.anchorMax = ringRt.pivot = new Vector2(0.5f, 0.18f);
            ringRt.sizeDelta = new Vector2(548f, 196f);

            var button = new GameObject("StartButton");
            button.transform.SetParent(canvasGo.transform, false);
            var btnImg = button.AddComponent<UnityEngine.UI.Image>();
            btnImg.sprite = greenPillSprite != null ? greenPillSprite : worldCardSprite;
            if (btnImg.sprite != null) btnImg.type = UnityEngine.UI.Image.Type.Sliced;
            btnImg.color = greenPillSprite != null ? Color.white : new Color(0.30f, 0.75f, 0.35f);
            var brt = btnImg.rectTransform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.18f);
            brt.sizeDelta = new Vector2(520f, 170f);
            _startButton = button.AddComponent<UnityEngine.UI.Button>();
            _startButton.targetGraphic = btnImg;
            _startButton.onClick.AddListener(DismissBriefing);
            _startButton.interactable = false; // until the world is built — see MarkBriefingReady

            _startLabel = MakeHudText(button.transform, new Vector2(0.5f, 0.5f), Vector2.zero, 54f);
            _startLabel.text = SummaRace.Constants.GameText.RaceBriefingWait;
            _startLabel.color = Color.white;
            _startLabel.fontStyle = FontStyles.Bold;
            _startLabel.rectTransform.sizeDelta = new Vector2(500f, 150f);

            card.transform.localScale = Vector3.one * 0.85f;
            Tween.Scale(card.transform, Vector3.one, 0.4f, Ease.OutBack);

            // The briefing read aloud — the one screen that explains how the race is CONTROLLED
            // (tap or swipe to change lane). A learner who cannot read that line does not
            // mis-summarise, they mis-steer, and every gate they drift past is logged as a wrong
            // first pick. The clip speaks the instruction only, never the story title above it.
            // Queued so the loading overlay's tip finishes first.
            if (SummaRace.Core.AudioManager.Instance != null)
                SummaRace.Core.AudioManager.Instance.PlayVoice(
                    SummaRace.Constants.AudioKeys.VoRaceBriefing, true);
        }

        /// <summary>Their Loadout/HUD is hidden and the track exists — the learner may now
        /// dismiss the briefing without seeing the runner kit's menus behind it.</summary>
        private void MarkBriefingReady()
        {
            _bootReady = true;
            if (_startLabel != null) _startLabel.text = SummaRace.Constants.GameText.RaceStartLabel;
            if (_startButton != null)
            {
                _startButton.interactable = true;
                _startLabel.fontSize = 62f;
                Tween.PunchScale(_startButton.transform, Vector3.one * 0.14f, 0.45f);
            }
        }

        /// <summary>
        /// The runner kit never finished booting, so this race cannot start. Turn the briefing's
        /// dead START button into a working way back to Story Select.
        ///
        /// Deliberately NOT "enable START anyway": with no TrackManager there is nothing to
        /// start, so that would trade a frozen screen for a frozen screen that also lies. The
        /// learner is offered the one thing that is true — go and pick a story again — in the
        /// game's own voice, because from where they are sitting nothing has gone wrong that is
        /// their fault. The reason is logged for whoever is holding the tablet.
        /// </summary>
        private void ShowBriefingEscape(string what)
        {
            Debug.LogError("EndlessRaceDirector: " + what + " never appeared within " +
                BootWaitSeconds + "s — the runner kit did not finish booting. Offering the " +
                "learner a way back to Story Select. If this is a device build, the most likely " +
                "cause is that Addressables content was never built for this platform.");

            // FIRST, before anything is shown. This method is reached by `yield break`ing out of
            // Start() ahead of the line that normally does this, so without it the learner is
            // looking at the runner kit's own HUD — coins, gems, score, distance, multiplier,
            // life hearts and their pause button — under our "this race is not ready" card. The
            // per-frame re-hide in Update() only engages once _gameState is resolved, so it
            // never catches up on its own. EnsureSingleAudioListener is skipped for the same
            // reason and is cheap to do here.
            HideTheirChrome();
            EnsureSingleAudioListener();

            _bootReady = true;   // the briefing is no longer waiting on anything

            if (_startLabel != null)
            {
                _startLabel.text = SummaRace.Constants.GameText.RaceBootFailedButton;
                _startLabel.fontSize = 46f;
            }
            if (_startButton != null)
            {
                _startButton.onClick.RemoveAllListeners();
                _startButton.onClick.AddListener(() =>
                {
                    if (SummaRace.Core.AudioManager.Instance != null)
                        SummaRace.Core.AudioManager.Instance.PlaySfx(SummaRace.Constants.AudioKeys.SfxClick);
                    SummaRace.Core.SceneLoader.Go(SummaRace.Constants.SceneNames.StorySelect);
                });
                _startButton.interactable = true;
                Tween.PunchScale(_startButton.transform, Vector3.one * 0.14f, 0.45f);
            }
            if (_briefingBody != null)
                _briefingBody.text = SummaRace.Constants.GameText.RaceBootFailedBody;
        }

        private void MakeChip(RectTransform row, int index, string type)
        {
            var chip = new GameObject("Chip_" + index);
            chip.transform.SetParent(row, false);
            var img = chip.AddComponent<UnityEngine.UI.Image>();
            img.sprite = worldCardSprite;
            if (worldCardSprite != null) img.type = UnityEngine.UI.Image.Type.Sliced;
            img.color = SummaRace.Constants.SwbstPalette.ForIndex(index);
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(index * 0.2f + 0.02f, 0f);
            rt.anchorMax = new Vector2((index + 1) * 0.2f - 0.02f, 1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var letter = MakeHudText(chip.transform, new Vector2(0.5f, 0.5f), Vector2.zero, 58f);
            letter.text = string.IsNullOrEmpty(type) ? "?" : type.Substring(0, 1);
            letter.color = Color.white;
            letter.fontStyle = FontStyles.Bold;
            letter.rectTransform.sizeDelta = new Vector2(150f, 140f);

            // The five parts introduce themselves one by one.
            chip.transform.localScale = Vector3.zero;
            Tween.Scale(chip.transform, Vector3.one, 0.25f, Ease.OutBack, startDelay: 0.35f + index * 0.09f);
        }

        /// <summary>
        /// Their GameState pauses silently on focus loss (guarded Pause(false) — no menu,
        /// no Quit-to-Loadout dead end). We own the resume: unfreeze time/audio and, if
        /// the run was already released, set the world moving again.
        /// </summary>
        private void OnApplicationFocus(bool focus)
        {
            if (!focus || !EndlessRaceMode.Active) return;

            // OUR pause outranks the OS one. If the learner paused, then backgrounded the
            // tablet, coming back must NOT resume the race — the pause screen is still on
            // screen and the world has to stay where they left it. But their focus-loss
            // Pause() may have run before ours took effect, so re-assert the frozen state
            // rather than trusting it: this is the one path that could otherwise return with
            // the world moving under an opaque menu, or with the audio listener muted for the
            // rest of the session because nothing else ever unmutes it.
            if (_paused)
            {
                Time.timeScale = 0f;
                AudioListener.pause = true;
                if (TrackManager.instance != null && TrackManager.instance.isMoving)
                    TrackManager.instance.StopMove();
                return;
            }

            if (Time.timeScale != 0f) return; // nothing was paused

            Time.timeScale = 1f;
            AudioListener.pause = false;
            // Not once the run is over: resuming during the victory beat would restart the
            // world underneath it.
            if (_runReleased && !_finished && TrackManager.instance != null)
                TrackManager.instance.StartMove(false);
        }

        /// <summary>Some Android devices and split-screen deliver only the pause callback,
        /// never the focus one, so route it to the same recovery.</summary>
        private void OnApplicationPause(bool paused)
        {
            if (!paused) OnApplicationFocus(true);
        }

        private void DismissBriefing()
        {
            if (_briefingDismissed || !_bootReady) return;
            _briefingDismissed = true;
            if (SummaRace.Core.AudioManager.Instance != null)
            {
                // The learner has read enough to tap START, so the briefing's voice goes with
                // the briefing itself — it must not still be explaining the controls over the
                // 3-2-1, when the world is about to move.
                SummaRace.Core.AudioManager.Instance.StopNarration();
                SummaRace.Core.AudioManager.Instance.PlaySfx(SummaRace.Constants.AudioKeys.SfxClick);
            }
            if (_briefingRoot != null) _briefingRoot.SetActive(false);
            StartCoroutine(CountdownRoutine());
        }

        /// <summary>3-2-1-GO! with a cinematic camera: while the kid does his funny dance the
        /// camera orbits him from a high front angle, descending, then on GO! it swoops smoothly
        /// into the normal chase pose as the world releases.</summary>
        /// <summary>The actual "GO!": banner, runner into its run cycle, world released.
        /// Shared by the countdown and by its malformed-array fallback, so there is exactly one
        /// definition of what releasing the run means.</summary>
        private void ReleaseRun(Transform hud, string label)
        {
            bool firstRelease = !_runReleased;
            ShowBigCount(hud, label, true);
            _runReleased = true; // Update()/LateUpdate() stop holding the pre-race dance
            if (firstRelease) RestartTheirMusic();
            // Both only exist once there is a run: pausing a world that has not started would
            // stack two "hold the track still" mechanisms, and gate 1's options showing under
            // the mission card or the 3-2-1 would be noise rather than reading time.
            if (_pauseChip != null) _pauseChip.SetActive(true);
            ApplyPreviewVisibility();
            if (TrackManager.instance != null)
            {
                StartRunnerRun(TrackManager.instance);
                // StartMove(true) seeds m_Speed = minSpeed. With false it stays whatever it
                // was, and it is 0 until THEIR WaitToStart coroutine fires ~3.3s after Begin().
                // Our briefing can be dismissed much sooner than that, so any learner who taps
                // START promptly used to get "GO!" followed by a second or so of standing
                // still, then a lurch to full speed when their timer caught up.
                TrackManager.instance.StartMove(true);
            }
            UpdateBanner();
        }

        /// <summary>
        /// STORIES 2 AND 3 OF EVERY SESSION RAN IN SILENCE. Their MusicPlayer is a
        /// DontDestroyOnLoad singleton whose stems are started exactly once per APP LAUNCH (its
        /// own Start), and FinishRoutine stops those sources so the victory sting owns Results —
        /// correct, but nothing ever started them again. Their one other restart site,
        /// GameState.Enter, is guarded by <c>GetStem(0) != gameTheme</c>, which is true only the
        /// first time. So the learner heard music on story 1 and nothing on stories 2 and 3, in
        /// all ten sessions.
        ///
        /// Restarted here rather than in Start() because this is the frame the world actually
        /// begins moving: the music comes in on GO!, not under the mission briefing. Called only
        /// on the FIRST release (a pause/resume goes nowhere near this), so the stems can never
        /// be double-started, and it is ahead of the FINISH silence rather than fighting it.
        /// Coroutine is run on their own component — it is the object that owns the sources, and
        /// it outlives every scene load, so nothing can strand it half-faded.
        /// </summary>
        private static void RestartTheirMusic()
        {
            var mp = MusicPlayer.instance;
            if (mp == null || !mp.isActiveAndEnabled) return;   // grey-box / editor-direct run
            if (mp.stems == null || mp.stems.Length == 0) return;
            mp.StartCoroutine(mp.RestartAllStems());
        }

        private IEnumerator CountdownRoutine()
        {
            var steps = SummaRace.Constants.GameText.RaceCountdown;
            var hud = transform.Find("SummaRaceHud");
            var cam = Camera.main;

            // A one-entry (or empty) countdown array would index steps[-1] below and kill this
            // coroutine — and since the briefing is already hidden and _runReleased is still
            // false at that point, the learner would be left staring at a frozen world with no
            // way out. GameText is content, so it is allowed to change; this is not.
            if (steps == null || steps.Length < 2)
            {
                Debug.LogWarning("EndlessRaceDirector: RaceCountdown needs at least 2 entries; " +
                    "releasing the run without a countdown.");
                ReleaseRun(hud, steps != null && steps.Length > 0 ? steps[steps.Length - 1] : "GO!");
                yield break;
            }

            // The resting gameplay pose the swing must settle back onto (camera is parented).
            Vector3 gpPos = cam != null ? cam.transform.localPosition : Vector3.zero;
            Quaternion gpRot = cam != null ? cam.transform.localRotation : Quaternion.identity;

            const float stepTime = 0.7f, goTime = 0.6f;
            int n = steps.Length;
            float orbitDur = Mathf.Max(0.01f, (n - 1) * stepTime); // the 3-2-1 window (before GO!)

            // 3-2-1 with the orbit.
            float elapsed = 0f; int shown = -1;
            Vector3 orbitEndPos = gpPos; Quaternion orbitEndRot = gpRot;
            while (elapsed < orbitDur)
            {
                int idx = Mathf.Min(n - 2, (int)(elapsed / stepTime));
                if (idx != shown) { shown = idx; ShowBigCount(hud, steps[idx], false); }
                if (cam != null)
                {
                    OrbitStartCamera(cam, elapsed / orbitDur);
                    orbitEndPos = cam.transform.localPosition;
                    orbitEndRot = cam.transform.localRotation;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            // GO! — release the world, kid switches to run, banner reads GO!.
            ReleaseRun(hud, steps[n - 1]);

            // Swoop the camera from the orbit back onto the exact chase pose.
            float t = 0f;
            while (t < goTime)
            {
                if (cam != null)
                {
                    float e = Mathf.SmoothStep(0f, 1f, t / goTime);
                    cam.transform.localPosition = Vector3.Lerp(orbitEndPos, gpPos, e);
                    cam.transform.localRotation = Quaternion.Slerp(orbitEndRot, gpRot, e);
                }
                t += Time.deltaTime;
                yield return null;
            }
            if (cam != null) { cam.transform.localPosition = gpPos; cam.transform.localRotation = gpRot; }
        }

        /// <summary>
        /// One 3-2-1 (or GO!) beat: ONE big centre number and a tick.
        ///
        /// "I notice the countdown doesn't start, there are 2 countdowns?" — and there were, both
        /// of them ours. This wrote the same digit into the HUD BANNER as well as the giant gold
        /// number, so every beat drew a small white "2" at screen y 0.81 and a huge gold "2" in
        /// the middle. Captured mid-countdown to be sure it was not Trash Dash's 5-4-3-2-1: the
        /// banner rect is normY[0.781,0.844] and their countdownText was inactive with a
        /// zero-size rect at the time. Two sizes of the same digit in two colours read as two
        /// countdowns, and the small one reads as the "real" one failing to start.
        ///
        /// The banner has been finish-only since F40 (the SWBST tracker owns progress); this was
        /// the one site still writing to it. UpdateBanner is called by ReleaseRun immediately
        /// after, so the banner returns to its normal state with no gap.
        /// </summary>
        private void ShowBigCount(Transform hud, string text, bool isGo)
        {
            if (hud != null)
            {
                var big = MakeHudText(hud, new Vector2(0.5f, 0.55f), Vector2.zero, isGo ? 230f : 320f);
                big.text = text;
                big.color = new Color(1f, 0.83f, 0.20f);
                big.fontStyle = FontStyles.Bold;
                big.rectTransform.sizeDelta = new Vector2(1000f, 420f);
                big.transform.localScale = Vector3.one * 1.6f;
                Tween.Scale(big.transform, Vector3.one, 0.25f, Ease.OutBack);
                Destroy(big.gameObject, isGo ? 0.6f : 0.72f);
            }
            if (SummaRace.Core.AudioManager.Instance != null)
                SummaRace.Core.AudioManager.Instance.PlaySfx(isGo
                    ? SummaRace.Constants.AudioKeys.SfxBoost
                    : SummaRace.Constants.AudioKeys.SfxPop);
        }

        /// <summary>Cinematic pre-race sweep that ALWAYS looks forward down the corridor. The
        /// world is only built ahead of the kid, so any angle pointing sideways/back/up shows the
        /// skybox (the blue "no background"). Staying behind him and facing forward keeps the alley
        /// filling the frame. Arcs from a low behind-left angle up into (near) the chase pose as p
        /// goes 0..1; the GO! swoop then locks onto the exact chase pose.</summary>
        private void OrbitStartCamera(Camera cam, float p)
        {
            // -3.2 lateral was tuned before the world became a narrow alley (F27/F29): 3.2m to the
            // left puts the camera at the kerb, so the near wall filled the frame. Worse, aiming
            // 4m PAST the kid swung him to the right-hand edge — the shot was of a brick wall with
            // a runner half out of frame. Keep the drift small and aim just above him, so he stays
            // framed for the whole sweep while the corridor still runs away ahead.
            float x = Mathf.Lerp(-1.2f, 0f, p);   // slightly behind-left -> centred
            float y = Mathf.Lerp(1.6f, 4f, p);    // low -> chase height
            float z = Mathf.Lerp(-3.0f, -5f, p);  // close behind -> chase distance
            Vector3 localPos = new Vector3(x, y, z);
            Vector3 dir = new Vector3(0f, 1.0f, 2.2f) - localPos; // look forward, down the corridor
            cam.transform.localPosition = localPos;
            if (dir.sqrMagnitude > 0.0001f)
                cam.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        /// <summary>Holds the runner in Idle ("Start") through the briefing + countdown (the
        /// cinematic camera is the star; idle is enough). Called from BOTH Update and LateUpdate
        /// so it is the final word each frame: their WaitToStart flips the character to run on its
        /// own timer, and re-asserting idle after that (guarded by IsName so it loops, not
        /// restarts) removes the run-back blip. Null-safe for grey-box / mid-boot frames.</summary>
        private void HoldRunnerPreRace(TrackManager track)
        {
            var runner = track != null ? track.characterController : null;
            if (runner == null || runner.character == null || runner.character.animator == null) return;
            var anim = runner.character.animator;
            if (!anim.GetCurrentAnimatorStateInfo(0).IsName("Start"))
            {
                // Their WaitToStart fires StartRunning (flip to run) once on its own timer; if that
                // lands in the visible countdown it shows for one frame ("run-back"). Re-assert idle
                // AND force an immediate re-evaluation so THIS frame renders idle, not the run pose.
                anim.Play("Start");
                anim.Update(0f);
            }
            anim.SetBool("Moving", false);
        }

        /// <summary>
        /// Feeds the runLoop blend tree so a lane change actually LEANS.
        ///
        /// The tree (KidCharacterAnimation runLoop) blends run-left / run-forward / run-right
        /// on a `LaneSwitch` float at thresholds -1 / 0 / +1 — but nothing in the project ever
        /// wrote that parameter, so it sat at 0 forever and the tree always played the centre
        /// child. Their ChangeLane only sets a target position; the actual motion is a
        /// MoveTowards on the collider's localPosition. So the kid slid 1.5m sideways in 0.107s
        /// with a completely unchanged forward run — no lean, no weight shift.
        ///
        /// Derive the lean from the movement that is really happening: lateral speed as a
        /// fraction of their laneChangeSpeed, damped so it eases in and out instead of
        /// snapping between the three clips.
        /// </summary>
        private void UpdateRunnerLean(TrackManager track)
        {
            var runner = track != null ? track.characterController : null;
            if (runner == null || runner.character == null || runner.character.animator == null) return;
            var body = runner.characterCollider != null ? runner.characterCollider.transform : null;
            if (body == null) return;

            float x = body.localPosition.x;
            float dt = Time.deltaTime;
            float target = 0f;
            // laneChangeSpeed lives on their CharacterInputController, not on TrackManager.
            if (dt > 0f && _lastRunnerX.HasValue && runner.laneChangeSpeed > 0.01f)
                target = Mathf.Clamp((x - _lastRunnerX.Value) / dt / runner.laneChangeSpeed, -1f, 1f);
            _lastRunnerX = x;

            // Ease toward the target; the raw value is a step function (full speed or zero).
            _laneLean = Mathf.MoveTowards(_laneLean, target, LeanResponse * dt);
            runner.character.animator.SetFloat("LaneSwitch", _laneLean);
        }

        /// <summary>Idle -> Run in one clean step on GO! (their default state is runStart).</summary>
        private void StartRunnerRun(TrackManager track)
        {
            var runner = track != null ? track.characterController : null;
            if (runner == null || runner.character == null || runner.character.animator == null) return;
            var anim = runner.character.animator;
            anim.SetBool("Moving", true);
            anim.Play("runStart");
        }

        private void LateUpdate()
        {
            KeepTheirCountdownHidden();

            // While paused the cop holds station: his placement is recomputed from the LIVE
            // player every frame, and the player has not moved, so skipping is a no-op that
            // cannot strand him — and on resume the very next frame re-derives him from the
            // runner, exactly as it does after a floating-origin recenter.
            if (_paused || _leaving) return;

            var track = TrackManager.instance;

            // After TrackManager.Update has moved the runner, so the cop is placed against
            // THIS frame's player position rather than last frame's.
            if (_runReleased && track != null && !_finished) UpdatePatrol(track);

            if (_runReleased) return;
            if (track != null) HoldRunnerPreRace(track); // final word each frame -> no run-back blip
        }

        /// <summary>
        /// TRASH DASH'S OWN 5-4-3-2-1, held down for good.
        ///
        /// `GameState.UpdateUI` (GameState.cs:357-361) does this EVERY FRAME while their timer
        /// runs:  if (trackManager.timeToStart >= 0) { countdownText.gameObject.SetActive(true); … }
        /// Their timer runs 5/1.5 = 3.33s from Begin() and our START unlocks at ~1.25s, so a
        /// learner who taps promptly gets their white 5-4-3-2-1 running under our gold 3-2-1-GO!.
        /// HideTheirChrome's one-shot hide is undone on the next frame; so is a hide in our
        /// Update(), because their state machine is updated after us — measured live: with the
        /// re-hide in Update and their condition re-armed, countdownText was still active=True.
        ///
        /// So this runs in LateUpdate (after every Update in the frame) AND clears the component's
        /// own `enabled`, which they never touch — that half is immune to script order entirely.
        /// Cheap: two reference compares in the steady state.
        /// </summary>
        private void KeepTheirCountdownHidden()
        {
            if (_gameState == null || _gameState.countdownText == null) return;
            if (_gameState.countdownText.enabled) _gameState.countdownText.enabled = false;
            if (_gameState.countdownText.gameObject.activeSelf)
                _gameState.countdownText.gameObject.SetActive(false);
        }

        private TextMeshProUGUI MakeHudText(Transform parent, Vector2 anchor, Vector2 offset, float size)
        {
            var go = new GameObject("HudText");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (worldLabelFont != null) tmp.font = worldLabelFont;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            // Labels are never the tap target — the button's own Image is. Left on (TMP's
            // default) a label would sit in front of its own button and swallow the press.
            tmp.raycastTarget = false;
            var rt = tmp.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(1000f, 120f);
            return tmp;
        }

        private void UpdateBanner()
        {
            RefreshTracker(); // the SWBST strip now shows "what to collect / what's collected"
            if (_bannerText == null) return;
            int element = _activeGateRoot != null ? _activeElement : _pendingElement;
            // The tracker owns the per-element prompt; the banner only calls the final dash.
            _bannerText.text = element >= 5 ? SummaRace.Constants.GameText.RaceRunToFinish : "";
        }

        private void ShowFeedback(string message, Color color)
        {
            if (_feedbackText == null) return;
            _feedbackText.text = message;
            _feedbackText.color = color;
            if (_feedbackPill != null)
            {
                _feedbackPill.gameObject.SetActive(true);
                Tween.PunchScale(_feedbackPill.transform, Vector3.one * 0.18f, 0.35f);
            }
            Tween.PunchScale(_feedbackText.transform, Vector3.one * 0.3f, 0.35f);
            _feedbackTimer = 1.4f;
        }

        /// <summary>The learner should never see the Trash Dash loadout screen — hide the
        /// 13 direct-child visual roots of the Loadout canvas on frame one. LoadoutState
        /// itself (and its GameObject) stays alive — StartGame() is still called on it
        /// later. Audit-verified: LoadoutState lives directly on the "Loadout" canvas
        /// GameObject, so its own transform's children ARE the 13 audited elements
        /// (StartButton, LoadoutGrid, CharZone, ThemeZone, AccessoriesSelector, PowerupZone,
        /// OpenLeaderboard, StoreButton, MissionButton, SettingButton, SettingPopup,
        /// MissionPopup, TutorialOverlay) — hiding each also hides everything nested under
        /// it (Settings' DeleteData/OpenURL links, Missions popup, tutorial FTUE, etc.).
        /// Must run twice — see the two call sites (Awake + top of Start) for why.</summary>
        private void MaskLoadoutFlash()
        {
            var loadout = FindAnyObjectByType<LoadoutState>();
            if (loadout == null) return;
            var loadoutRoot = loadout.transform; // never SetActive(false) this one itself
            for (int i = 0; i < loadoutRoot.childCount; i++)
                loadoutRoot.GetChild(i).gameObject.SetActive(false);
        }

        /// <summary>Trash Dash chrome that means nothing in SummaRace's SWBST race: the
        /// coin/score/distance/multiplier readouts (text, already-existing hide) plus the
        /// zone badge backgrounds behind them (upgrade — the text-only hide left empty
        /// floating icons), the powerup bank + inventory slot (never populated here since
        /// no consumable is ever granted), manual pause (PauseMenu/Resume stays wired —
        /// the OS focus-loss auto-pause can still open it even with the button gone), the
        /// pause menu's Exit button (dead-ends into the now-masked Loadout menu), and the
        /// fake sample-game leaderboard (defense-in-depth; its two opening buttons are
        /// already unreachable via the Loadout mask + the GameOver-path exemption below).
        /// Deliberately leaves the whole GameOver path (DeathPopup, its Premium/Ad buttons,
        /// the GameOver canvas) untouched per controller ruling — Task 2 makes death
        /// unreachable, and hiding it now would create a blank dead end if it ever fired.
        /// Handles come from the UI audit.</summary>
        private void HideTheirChrome()
        {
            var gs = FindAnyObjectByType<GameState>();
            _gameState = gs;
            if (gs != null)
            {
                if (gs.coinText != null) gs.coinText.gameObject.SetActive(false);
                if (gs.premiumText != null) gs.premiumText.gameObject.SetActive(false);
                if (gs.scoreText != null) gs.scoreText.gameObject.SetActive(false);
                if (gs.distanceText != null) gs.distanceText.gameObject.SetActive(false);
                if (gs.multiplierText != null) gs.multiplierText.gameObject.SetActive(false);
                // Their own 5-4-3-2-1 writes to this while timeToStart >= 0, which overlaps our
                // gold 3-2-1 whenever START is tapped early — two countdowns on screen at once.
                if (gs.countdownText != null)
                {
                    gs.countdownText.gameObject.SetActive(false);
                    // Belt and braces, and the half that actually holds: they re-show the
                    // GAMEOBJECT every frame but never touch the component, so disabling the
                    // Text itself survives their SetActive(true) whatever the script order is.
                    gs.countdownText.enabled = false;
                }

                // Zone backgrounds survive the text-only hide above (CoinZone/PremiumZone
                // are the text's direct parent; DistanceZone likewise; ScoreZone is two
                // levels up via ScoreLabel — audit-verified hierarchy).
                if (gs.coinText != null && gs.coinText.transform.parent != null)
                    gs.coinText.transform.parent.gameObject.SetActive(false); // CoinZone
                if (gs.premiumText != null && gs.premiumText.transform.parent != null)
                    gs.premiumText.transform.parent.gameObject.SetActive(false); // PremiumZone
                if (gs.distanceText != null && gs.distanceText.transform.parent != null)
                    gs.distanceText.transform.parent.gameObject.SetActive(false); // DistanceZone
                if (gs.scoreText != null && gs.scoreText.transform.parent != null &&
                    gs.scoreText.transform.parent.parent != null)
                    gs.scoreText.transform.parent.parent.gameObject.SetActive(false); // ScoreZone

                if (gs.powerupZone != null) gs.powerupZone.gameObject.SetActive(false); // PowerUpBank
                if (gs.inventoryIcon != null && gs.inventoryIcon.transform.parent != null)
                    gs.inventoryIcon.transform.parent.gameObject.SetActive(false); // Inventory
                if (gs.pauseButton != null) gs.pauseButton.gameObject.SetActive(false);
                if (gs.lifeRectTransform != null) gs.lifeRectTransform.gameObject.SetActive(false); // hearts

                // PauseMenu/Resume must stay reachable for the focus-loss auto-pause; only
                // its Exit button (-> QuitToLoadout, a dead end now) gets hidden.
                if (gs.pauseMenu != null)
                {
                    var exit = gs.pauseMenu.Find("Exit");
                    if (exit != null) exit.gameObject.SetActive(false);
                }
            }

            var loadout = FindAnyObjectByType<LoadoutState>();
            if (loadout != null && loadout.leaderboard != null)
                loadout.leaderboard.gameObject.SetActive(false);
        }

        /// <summary>Entered from Boot, two AudioListeners coexist (persistent Core + scene camera).
        /// Keep Core's — it's the only listener the post-race scenes have.</summary>
        private void EnsureSingleAudioListener()
        {
            var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude);
            if (listeners.Length <= 1) return;
            var keep = System.Array.Find(listeners, l => l.gameObject.scene.name == "DontDestroyOnLoad");
            if (keep == null) keep = listeners[0];
            foreach (var listener in listeners)
                if (listener != keep) listener.enabled = false;
        }
    }
}
