using System.Collections;
using System.Collections.Generic;
using PrimeTween;
using TMPro;
using SummaRace.Constants;
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
        private static readonly Color StoryGold = Theme.StoryGold;
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
        /// <summary>The finish celebration clip, wired in MainSummaRace.unity to
        /// Art/Characters/Animations/HipHopDancing.fbx (a Mixamo humanoid clip, so it retargets
        /// onto Aj for free - both are mixamorig skeletons).
        ///
        /// A SERIALIZED reference rather than a name lookup, for the reason this project has
        /// been bitten by twice (F54's SkyTint shader, and RaceController's three surviving
        /// Shader.Find calls): an asset reached only by name is not a build dependency, so it
        /// resolves perfectly in the Editor and returns null on the tablet - a failure that by
        /// construction cannot be seen until the APK is on a device. (Resources.Load would have
        /// been build-safe too; a serialized field is simply the cheaper of the two and does not
        /// need the clip moved into Resources/.)
        ///
        /// Null is fine and is the designed fallback: the finish then holds idle exactly as it
        /// did before, and nothing else about the beat changes.</summary>
        [SerializeField] private AnimationClip finishDanceClip;

        /// <summary>
        /// OPTIONAL second finish dance, so the celebration is not identical all thirty times a
        /// learner finishes a story. Drag Art/Characters/Animations/HipHopDancing2 into this slot
        /// in the Inspector on the race scene's director; it needs nothing else.
        ///
        /// Left unwired rather than guessed: HipHopDancing2.fbx has no named clip (no
        /// clipAnimations block in its .meta), so its AnimationClip sub-asset id cannot be
        /// derived from the file the way HipHopDancing's could, and writing a wrong id into the
        /// scene would produce a null reference that silently disables the dance we already have.
        /// Null here is completely safe - see PickFinishDance.
        /// </summary>
        [SerializeField] private AnimationClip finishDanceClipAlt;

        /// <summary>Finish fireworks. The same CFXR prefab the legacy race has used since F7 —
        /// reused rather than re-picked so the two races celebrate identically, and so this is a
        /// prefab that has already been seen working rather than a fresh guess. Null-safe: no
        /// prefab simply means no burst, and the rest of the beat is unchanged.</summary>
        [SerializeField] private GameObject finishFxPrefab;
        private UnityEngine.AnimatorOverrideController _danceOverride;

        /// <summary>Whether the chip's last-painted text used the fifth gate's wording. Part of
        /// the repaint cache key alongside the second, so the two forms cannot stick.</summary>
        private bool _gateTimerLastShown;

        private Transform _patrol;
        private Animator _patrolAnim;
        /// <summary>The run state on PatrolAnimator. A name, because the cop's controller is
        /// ours (_Game/Animation/PatrolAnimator.controller) and its states are Idle / Run /
        /// Stumble / Dance. If it is ever renamed, Play() no-ops and the cop falls back to the
        /// transition - visibly worse, but not broken.</summary>
        private const string PatrolRunState = "Run";
        private float _menaceTimer;

        /// <summary>Project-wide Time.maximumDeltaTime, saved on race entry and restored in
        /// OnDestroy. -1 = never taken (so a failed Start cannot restore a bogus value).</summary>
        private float _savedMaxDeltaTime = -1f;

        /// <summary>Whether a wrong-pick menace surge is running this frame. Owned by LateUpdate
        /// so it ticks whether or not the (currently disabled) patrol cop exists.</summary>
        private bool _menaceSurging;
        private float _patrolGroundY;      // fixed run-height captured on activation
        private bool _patrolGrounded;      // has _patrolGroundY been captured yet
        private float _patrolGap = 30f;    // metres the cop trails behind the player (constant once running)
        private float _patrolMoveVel;      // SmoothDamp velocity for his slide in/out of frame
        private float _patrolSide = 1f;    // which shoulder he takes: +1 right, -1 left
        private bool _wasSurging;          // so the shoulder is chosen once per surge, never mid-slide
        private Renderer[] _patrolRenderers; // cached for the per-frame body measurement
        private float _patrolFootFix;      // pivot-to-feet correction, measured once per beat
        private bool _patrolFootFixed;     // has _patrolFootFix been measured for this beat
        // The chase camera's authored resting pose, captured by CountdownRoutine (it already has
        // to know it to swoop back onto it after GO!), and the surge dolly that borrows it.
        private Vector3 _camChaseLocalPos;
        private bool _camChaseCaptured;
        private float _camDolly;           // 0 = authored chase pose, 1 = fully pulled back
        private bool _camDollyApplied;     // so the dolly writes the camera only when it must
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
        // Which SWBST slot the armed panel belongs to, so the question line cannot drift from
        // the three options under it. Captured at ARM time from _preparedElement rather than
        // read at reveal time from _pendingElement/_activeElement: those two swap over as a
        // gate goes live, and the panel is armed once and shown later, so reading them at
        // reveal time would ask the wrong question for exactly one frame class of gate.
        private int _previewElement = -1;
        private UnityEngine.UI.Image _previewGlow;   // border-only attention cue; never the words
        private TextMeshProUGUI _questionLabel;      // "Who is this story about?" - names the SLOT
        private GameObject _questionRoot;
        private GameObject _coachRoot;          // first-race steering coach; null after it is used
        private Coroutine _previewCue;
        /// <summary>True while the panel is showing a QUESTION (three options, or the one option a
        /// re-present carries) rather than the answer-reveal beat. Only in that state may a column
        /// be tapped, and only in that state do the columns claim taps away from the road.</summary>
        private bool _previewTappable = true;
        private EndlessTouchInput _tapInput;
        private RectTransform[] _tapBlockersPlain;      // pause chip only
        private RectTransform[] _tapBlockersWithPanel;  // pause chip + the three panel columns

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
        private const float PreviewBandTop = 0.861f;

        // The SWBST question line sits in the gap the board just gave up, between the board's
        // new top edge and the tracker's underside. BOTH ends are fractions of the canvas, so
        // the three stay locked together at every aspect - the F56 lesson, where a pixel-anchored
        // tracker and a fractional band only agreed at 9:16 and buried each other at 4:3.
        private const float QuestionBandBottom = 0.8625f;
        private const float QuestionBandTop = 0.8945f;

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

        /// <summary>The gate-arrival countdown (F59). Lives in the banner band, which is
        /// free by construction: SetBanner writes text there only for element 5, and this
        /// chip only ever shows for elements 0-4, so the two can never be on screen at
        /// once.</summary>
        private GameObject _gateTimerChip;
        private TextMeshProUGUI _gateTimerText;
        private int _gateTimerShown = -1;   // last whole second painted, so we repaint once a second
        /// <summary>Stands in for "far away" in _gateTimerShown, so the numberless form is
        /// repainted once on entry like any other value rather than every frame.</summary>
        private const int FarSentinel = int.MinValue;
        private RectTransform _pauseChipRect;
        private TextMeshProUGUI _leaveLabel;
        private bool _paused;
        private bool _leaving;              // exit in flight; nothing may run after this
        private bool _leaveArmed;
        private float _leaveArmedAt;
        private GameObject _leaveConfirmRoot;   // the real confirm panel, not a label swap
        private bool _wasMovingBeforePause;


        // Persistent SWBST inventory tracker (top strip): 5 slots, current pulses, collected
        // fill in-order. Teaches the framework and answers "what to collect next" (F40).
        private readonly UnityEngine.UI.Image[] _slotBg = new UnityEngine.UI.Image[5];
        private readonly UnityEngine.UI.Image[] _slotAccent = new UnityEngine.UI.Image[5];
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
            // Global, so it must come back with us however this scene ends — including the
            // briefing escape and a mid-run LEAVE RACE, not just a completed run.
            if (_savedMaxDeltaTime > 0f)
            {
                Time.maximumDeltaTime = _savedMaxDeltaTime;
                _savedMaxDeltaTime = -1f;
            }
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

            // StoryLoader.Load returns null on a missing file, a parse failure or ANY validation
            // failure — and the s01_easy fallback can fail the same way. Without this guard the
            // next line dereferenced null, the exception killed this coroutine before BuildHud
            // and BuildBriefing, and MaskLoadoutFlash had already hidden the kit's own UI: the
            // learner sat in an empty scene with no HUD, no briefing, no pause chip and Android
            // BACK swallowed app-wide. This is the one race path that had no escape at all, so
            // it takes the same "never a dead end" exit the four story screens use.
            if (_story == null)
            {
                Debug.LogError("EndlessRaceDirector: no story could be loaded (neither the selected " +
                               "story nor the s01_easy fallback) — leaving for Story Select rather " +
                               "than stranding the learner in an empty race.");
                SummaRace.Core.SceneLoader.Go(SummaRace.Constants.SceneNames.StorySelect);
                yield break;
            }

            // Cap a single frame's advance so a hitch cannot step OVER an answer gate and be
            // written to the study as a wrong answer. See GameRules.RaceMaxDeltaTime.
            _savedMaxDeltaTime = Time.maximumDeltaTime;
            Time.maximumDeltaTime = SummaRace.Constants.GameRules.RaceMaxDeltaTime;

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
            var place = SummaRace.Features.Race.RaceWorlds.ForRace(_story.world, _story.difficulty);
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
            }, SummaRace.Features.Race.RaceWorlds.StableSeed(_story.id), _story.world);

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
            _tapInput = GetComponent<EndlessTouchInput>();
            if (_tapInput == null) _tapInput = gameObject.AddComponent<EndlessTouchInput>();
            ApplyTapBlockers();

            yield return new WaitForSeconds(0.5f);
            HideTheirChrome();
            EnsureSingleAudioListener();
            MarkBriefingReady(); // only now is it safe to take the scrim away
        }

        private void Update()
        {
            // THE AUTO-DISARM IS GONE, AND ITS REASON WENT WITH IT (2026-08-21).
            //
            // It existed because arming used to reword the leave chip in place: the "armed"
            // state was invisible after a glance, so it timed itself out or the next stray tap
            // on that same corner ended the run. The confirmation is now a modal panel whose
            // only leave control is a separate button inside it, so there is no stray tap to
            // guard against - and a panel that closes itself while a nine-year-old is still
            // reading the question is worse than one that waits. _leaveArmedAt is kept only as
            // a record of when the question was asked.

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
            UpdateGateTimer(track);
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
        /// <param name="elementIndex">Kept although the marker is no longer tinted per element:
        /// the parameter documents which gate this marker belongs to at every call site, and
        /// removing it would make a future "colour it by element" change look like a one-line
        /// addition rather than the validity regression it is.</param>
        private void BuildLaneSelector(Transform root, int elementIndex, Vector2 cardSize,
            float laneOffset, bool centreOnly)
        {
            var go = new GameObject("LaneSelector");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0f, CardY, 0.03f); // just behind the card
            // NEUTRAL, never the element's SWBST colour. This marks WHERE THE RUNNER IS, not
            // which answer is right, and it used to say the second thing by accident — see
            // GameRules.RaceLaneSelectorColor for the full reasoning and the standing warning.
            var color = SummaRace.Constants.GameRules.RaceLaneSelectorColor;

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

            // The "collect" half of GDD 11.4's tiny vibration. This is THE collect beat the
            // constant is named for, and on a muted classroom tablet it is the only non-visual
            // confirmation the learner gets.
            SummaRace.Core.Haptics.Play(SummaRace.Core.Haptics.Light);

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
            else { _activeGateRoot = null; _activeElement = -1; _activeGateId = 0; _activeIsRepresent = false; }
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

            // The FRAMING line, in the feedback pill; the answer itself follows a beat later on
            // the reading panel (ShowAnswerReveal, below). The pill used to carry the answer too,
            // so the moment said the same sentence twice and never said what was actually wrong
            // with the pick. These lines do: the distractors are usually TRUE of the story, and
            // what makes one wrong is that it is not the PART being collected.
            //
            // Drawn from a shuffle bag rather than fixed, because a learner having a hard run
            // can see this up to five times in one race. Amber, never red (D7); the answer keeps
            // the gold, so the two beats stay visually distinct.
            ShowFeedback(SummaRace.Core.Praise.RaceNotQuite(), Theme.AmberWarn);

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
            else { _activeGateRoot = null; _activeElement = -1; _activeGateId = 0; }
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
            // No card of a retired gate may ever match again. This was left holding the spent
            // gate's serial while _activeElement went to -1, so a surviving card reaching
            // OnPickupHit would pass the identity guard and then index _firstPickDone[-1] —
            // an IndexOutOfRangeException thrown inside a physics callback, mid-measured-run.
            // Disabling the colliders above makes that hard to reach, but the previous
            // "already resolved" latch was defeated by exactly this kind of same-frame
            // reasoning, so the identity is cleared rather than argued about.
            _activeGateId = 0;
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
            _activeGateId = 0;   // see DestroyActiveGate: -1 must never be indexable
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
            // THE `bySpeed` TERM IS GONE (owner playtest 2026-08-22, "waiting next too long").
            //
            // It was `RaceSecondsPerGate * difficulty * rhythm * runSpeed` - the naive
            // seconds-to-metres multiply that the integrating floor below already replaced
            // and corrected. Keeping both meant the naive one WON whenever the difficulty
            // multiplier was above 1, so easy's gaps were set by the wrong formula: 22-23s
            // gates around a reading window that stayed at a flat 12s, i.e. ~10s of empty
            // corridor five times over, 47s of a 107s race.
            //
            // The gap is now exactly what it should always have been: the seconds the learner
            // needs, converted to metres correctly. Difficulty is applied to the READING
            // WINDOW instead (see GameRules.ReadWindowEasy), so easy buys thinking time rather
            // than empty road.
            // THE LEGIBILITY FLOOR, in seconds and therefore in metres at the speed being run:
            // the reading window plus the quiet stretch. Difficulty scales thinking time, but it
            // may never scale it below the window itself — at hard (x0.8) it otherwise would, and
            // a gate that arrives before its options have been readable for 12s is the F47
            // validity failure returning through the difficulty setting.
            // seconds -> METRES, accounting for the fact that the runner ACCELERATES across the
            // gap. `seconds * runSpeed` is the distance only if the speed never changes; their
            // track adds k_Acceleration (0.2 m/s^2) every second up to maxSpeed, so the runner
            // eats that distance faster than the floor assumes and the window silently comes in
            // short. Measured before this fix: hard delivered 16.1s where the floor claimed 18s,
            // i.e. it missed the ~16.5s a 70wpm reader needs on the one difficulty that has the
            // least slack. Integrating instead: d = v*T + 0.5*a*T^2 while below maxSpeed, then
            // the remainder at maxSpeed.
            const float trackAccel = 0.2f;   // TrackManager.k_Acceleration (protected const there)
            // RHYTHM NOW VARIES THE BREATHER, NEVER THE READING.
            //
            // GateRhythm exists so the thirty races do not all share one metronome, and that is
            // worth keeping. It used to multiply the whole gap, which the old comment had to
            // hedge about ("a short draw is absorbed by the floor") - and once the gap IS the
            // floor, a rhythm on the whole gap would be a rhythm on the reading window, i.e.
            // some gates giving a struggling reader less time than others for no reason the
            // child could perceive. Applied to the quiet stretch only, the variety is
            // preserved exactly and the reading budget is identical at every gate.
            float window = ReadWindowSeconds()
                + SummaRace.Constants.GameRules.RaceQuietRunSeconds * GateRhythm(_pendingElement);
            float secondsFloor;
            float toTop = trackAccel > 0f ? (track.maxSpeed - runSpeed) / trackAccel : float.MaxValue;
            if (toTop >= window)
            {
                secondsFloor = runSpeed * window + 0.5f * trackAccel * window * window;
            }
            else
            {
                // accelerate to maxSpeed, then cruise for what is left of the window
                secondsFloor = runSpeed * toTop + 0.5f * trackAccel * toTop * toTop
                             + track.maxSpeed * (window - toTop);
            }
            float floor = Mathf.Max(Mathf.Max(_story.mission.checkpointSpacing,
                SummaRace.Constants.GameRules.RaceMinGateGap), secondsFloor);
            return Mathf.Clamp(floor, floor,
                SummaRace.Constants.GameRules.RaceMaxGateGap);
        }

        /// <summary>
        /// This story's beat for this gate (F57). A race used to hand out the same gap five
        /// times, because NextGateGap is a pure function of difficulty and current speed, so the
        /// pacing of every race in the game was identical and only its length differed. The
        /// factor is derived from the story's stable seed and the gate index, which makes it
        /// fixed for a given story forever (every learner runs the same s04_hard) while giving
        /// each of the thirty races its own rhythm.
        ///
        /// Centred on 1.0 and applied BEFORE NextGateGap's floor clamp, so a short draw is
        /// absorbed by the reading-window floor rather than eating into it — see
        /// GameRules.RaceGateRhythmSpread.
        /// </summary>
        private float GateRhythm(int gateIndex)
        {
            float spread = SummaRace.Constants.GameRules.RaceGateRhythmSpread;
            if (spread <= 0f) return 1f;

            // A cheap integer hash of (story, gate) rather than a stored System.Random: this is
            // called from gate scheduling, which also runs on the re-present path, and a stream
            // would then advance differently depending on how many mistakes the learner made —
            // i.e. the track would change shape in response to the child's answers.
            int seed = SummaRace.Features.Race.RaceWorlds.StableSeed(_story.id) + gateIndex * 7919;
            unchecked
            {
                uint h = (uint)seed;
                h ^= h >> 16; h *= 2246822519u;
                h ^= h >> 13; h *= 3266489917u;
                h ^= h >> 16;
                float unit = (h & 0xffffff) / (float)0x1000000;   // [0,1)
                return 1f + (unit * 2f - 1f) * spread;
            }
        }

        /// <summary>
        /// How many seconds the option panel is up before its gate arrives, for this story's
        /// difficulty. This is the reading budget, and it is now the ONLY thing difficulty
        /// changes about pacing - see GameRules.ReadWindowEasy for the measurement that moved
        /// it here from the gate gap.
        /// </summary>
        private float ReadWindowSeconds()
        {
            float mult;
            switch (_story != null ? _story.difficulty : null)
            {
                case "easy": mult = SummaRace.Constants.GameRules.ReadWindowEasy; break;
                case "hard": mult = SummaRace.Constants.GameRules.ReadWindowHard; break;
                default: mult = SummaRace.Constants.GameRules.ReadWindowAverage; break;
            }
            return SummaRace.Constants.GameRules.RacePreviewLeadSeconds * mult;
        }

        // DifficultyGateTime() was here and is deleted. It scaled the whole gate GAP, which is
        // the pacing bug the 2026-08-22 pass fixed: difficulty now scales the reading WINDOW
        // (ReadWindowSeconds above). Its three constants are marked superseded in GameRules
        // rather than removed, so the numbers and the reason stay together.

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
            // Hidden HERE and not by UpdateGateTimer: Update returns early once _finished
            // is set, so the countdown would freeze on screen showing whatever second it
            // last painted, for the whole victory beat.
            HideGateTimer();
            HidePatrolCameo();
            HideOptionPreview();

            var track = TrackManager.instance;
            if (track != null)
            {
                track.StopMove();
                var runner = track.characterController;
                if (runner != null && runner.character != null && runner.character.animator != null)
                    PlayFinishCelebration(runner.character.animator);
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

            // FIREWORKS, and then the story read back. See BuildFinishCard for why the readback
            // is the part that actually matters.
            StartCoroutine(FinishFireworks());
            BuildFinishCard();

            // 2.2 -> RaceFinishBeatSeconds (3.2). The beat used to hold a still character, so
            // 2.2s was ample; a dance needs long enough to read as a dance rather than as a
            // glitch on the way out. Owner, 2026-08-21: "if game finish at least add animation,
            // instead of pausing all character movement, make sure the player dance".
            yield return new WaitForSeconds(SummaRace.Constants.GameRules.RaceFinishBeatSeconds);

            SummaRace.Core.SceneLoader.Go(SummaRace.Constants.SceneNames.Arrange);
        }

        /// <summary>
        /// Chooses which dance plays, from whichever clips are actually wired.
        ///
        /// Deliberately tolerant: one clip, two clips or none all behave sensibly, so the second
        /// slot can be filled later without touching code and a missing reference can never take
        /// the working dance down with it. Random rather than alternating, because alternating
        /// needs state that would have to survive a scene load to mean anything.
        /// </summary>
        private AnimationClip PickFinishDance()
        {
            if (finishDanceClip == null) return finishDanceClipAlt;      // may also be null
            if (finishDanceClipAlt == null) return finishDanceClip;
            return UnityEngine.Random.value < 0.5f ? finishDanceClip : finishDanceClipAlt;
        }

        /// <summary>
        /// Three bursts around the runner, staggered, so the finish reads as an event rather
        /// than a single pop. Positions are relative to the LIVE runner because their track
        /// floats its origin; heights and offsets are small so the bursts frame him instead of
        /// covering him.
        /// </summary>
        private IEnumerator FinishFireworks()
        {
            if (finishFxPrefab == null) yield break;
            var track = TrackManager.instance;
            var runner = track != null ? track.characterController : null;
            if (runner == null) yield break;

            Vector3[] at =
            {
                new Vector3(-2.2f, 3.4f, 6f),
                new Vector3( 2.4f, 4.2f, 8f),
                new Vector3( 0.0f, 2.8f, 4f),
            };
            for (int i = 0; i < at.Length; i++)
            {
                if (_leaving) yield break;
                var go = Instantiate(finishFxPrefab, runner.transform.position + at[i],
                                     Quaternion.identity);
                // CFXR prefabs self-destruct, but not all of them do it on every code path, and
                // this scene is about to be unloaded anyway - a hard lifetime costs nothing and
                // guarantees no orphan survives into Arrange.
                Destroy(go, 4f);
                if (SummaRace.Core.AudioManager.Instance != null)
                    SummaRace.Core.AudioManager.Instance.PlaySfx(SummaRace.Constants.AudioKeys.SfxStar);
                yield return new WaitForSeconds(0.28f);
            }
        }

        /// <summary>
        /// THE FINISH READS THE STORY BACK.
        ///
        /// Owner, 2026-08-22: <i>"when finish at least add something celebration or effect rather
        /// than stop"</i>. Fireworks answer the letter of that. This answers the substance.
        ///
        /// The race is five identical beats and then it ends. The child collects SOMEBODY, then
        /// WANTED, then BUT - each one feeling like the last - and is then handed to Arrange to
        /// order five parts they were never once shown AS A SET, and to Summary to write a
        /// sentence from parts that never behaved like a sentence. The finish is the one moment
        /// where all five exist at once, and it was spending that moment on the word "FINISH!".
        ///
        /// So the card shows what they actually built: the five parts, in S-W-B-S-T order,
        /// popping in one at a time. It costs nothing in validity - these are the learner's own
        /// collected answers, every pick is already logged by the time this runs, and it names no
        /// choice as right or wrong. What it does is make the next two screens make sense.
        ///
        /// Always the CORRECT five, deliberately: the collected set is what Arrange receives
        /// (FinishRoutine fills result.collectedPieces from _story.elements regardless of picks),
        /// so showing anything else here would preview a different story from the one the next
        /// screen is about.
        /// </summary>
        private void BuildFinishCard()
        {
            if (_story == null || _story.elements == null || _story.elements.Length < 5) return;
            var hud = transform.Find("SummaRaceHud");
            if (hud == null) return;

            var card = new GameObject("FinishCard");
            card.transform.SetParent(hud, false);
            var img = card.AddComponent<UnityEngine.UI.Image>();
            img.sprite = WoodPlaqueSprite();
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.color = Theme.Alpha(Theme.Wood, 0.95f);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            // Below the tracker and above the road, in the band the option panel vacates at the
            // finish - so it lands where the learner has been reading all race.
            rt.anchorMin = new Vector2(0.05f, 0.50f);
            rt.anchorMax = new Vector2(0.95f, 0.87f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var title = MakeHudText(card.transform, new Vector2(0.5f, 0.90f), Vector2.zero, 46f);
            title.text = SummaRace.Constants.GameText.RaceFinishCardTitle;
            title.color = Theme.Gold;
            title.fontStyle = FontStyles.Bold;
            title.rectTransform.sizeDelta = new Vector2(880f, 90f);

            for (int i = 0; i < 5; i++)
            {
                var row = new GameObject("Part_" + i);
                row.transform.SetParent(card.transform, false);
                var rimg = row.AddComponent<UnityEngine.UI.Image>();
                rimg.sprite = WoodPlaqueSprite();
                rimg.type = UnityEngine.UI.Image.Type.Sliced;
                // CREAM ROW, COLOURED TEXT — not the element's full colour behind its own ink.
                //
                // Measured on the device build: ink is Lerp(colour, black, 0.45), i.e. exactly
                // 55% of the background in every channel, so every row landed at 2.5-2.9:1 and
                // the five answers were effectively invisible (owner screenshot, 2026-08-22).
                // The Ink variant is measured against CREAM (5.2-7.4:1) and that is the pairing
                // it exists for; putting it on the element's own colour threw the measurement
                // away. This is now the exact pairing the tracker's collected slot uses, which
                // is the one part of the HUD the same screenshots show reading cleanly.
                rimg.color = Theme.Cream;
                rimg.raycastTarget = false;
                var rrt = rimg.rectTransform;
                float top = 0.80f - i * 0.155f;
                rrt.anchorMin = new Vector2(0.035f, top - 0.135f);
                rrt.anchorMax = new Vector2(0.965f, top);
                rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;

                // The element's colour survives as a strip down the row's leading edge, so the
                // five parts are still colour-coded without the text sitting on top of the code.
                var strip = new GameObject("Strip");
                strip.transform.SetParent(row.transform, false);
                var simg = strip.AddComponent<UnityEngine.UI.Image>();
                simg.sprite = WoodPlaqueSprite();
                simg.type = UnityEngine.UI.Image.Type.Sliced;
                simg.color = SummaRace.Constants.SwbstPalette.ForIndex(i);
                simg.raycastTarget = false;
                var srt = simg.rectTransform;
                srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(0f, 1f);
                srt.pivot = new Vector2(0f, 0.5f);
                srt.offsetMin = new Vector2(8f, 8f); srt.offsetMax = new Vector2(0f, -8f);
                srt.sizeDelta = new Vector2(16f, srt.sizeDelta.y);

                var lbl = MakeHudText(row.transform, new Vector2(0.5f, 0.5f), Vector2.zero, 34f);
                lbl.text = _story.elements[i].correct;
                // Ink on cream: 5.2-7.4:1, the pairing this variant was measured for.
                lbl.color = SummaRace.Constants.SwbstPalette.InkForIndex(i);
                lbl.fontStyle = FontStyles.Bold;
                lbl.enableAutoSizing = true;
                lbl.fontSizeMin = 20f;
                lbl.fontSizeMax = 34f;
                lbl.overflowMode = TextOverflowModes.Ellipsis;
                var lrt = lbl.rectTransform;
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(36f, 4f); lrt.offsetMax = new Vector2(-18f, -4f);

                // One at a time, so the child watches the story assemble rather than being shown
                // a finished list. 0.16s apart puts the last one at 0.8s, well inside the beat.
                row.transform.localScale = Vector3.zero;
                Tween.Scale(row.transform, Vector3.one, 0.28f, Ease.OutBack,
                            startDelay: 0.25f + i * 0.16f);
            }

            card.transform.localScale = Vector3.one * 0.85f;
            Tween.Scale(card.transform, Vector3.one, 0.3f, Ease.OutBack);
        }

        /// <summary>
        /// The finish celebration. Reverses F42d ("no dance - idle is enough"), which was itself
        /// an owner steer and has now been steered back: "if game finish at least add animation,
        /// instead of pausing all character movement, make sure the player dance, we have dance
        /// asset there if needed" (device playtest, 2026-08-21). He is right that a runner who
        /// simply stops is the weakest moment in the loop - it is the one place the game says
        /// "well done" and it said it with a character standing still.
        ///
        /// HOW, AND WHY NOT A NEW ANIMATOR STATE. KidCharacterAnimation is a 1:1 clone of Trash
        /// Dash's own graph, kept structurally identical because THEIR code drives its
        /// parameters (Moving/Jumping/Sliding/Hit/Dead/RandomIdle...). Adding a Dance state and
        /// a transition into it means editing that graph, which is the one asset in the race
        /// where a divergence from their expectations shows up as a character stuck in a pose.
        ///
        /// So the clip is swapped into the state the finish ALREADY plays. "Start" is their idle
        /// and it is an orphan with no outbound transitions (F40e measured this), so once we
        /// Play() it nothing can move the character off it - which makes it the safest possible
        /// host for a one-shot celebration. An AnimatorOverrideController re-points that one
        /// state at the dance clip and touches nothing else in the graph.
        ///
        /// It is applied only at the finish, so the pre-race idle hold (HoldRunnerPreRace, which
        /// plays the same state) is unaffected: by the time this runs the run is over, _finished
        /// is set, and the next thing that happens is a scene load.
        /// </summary>
        private void PlayFinishCelebration(Animator anim)
        {
            if (anim == null) return;

            anim.SetBool("Moving", false);

            var dance = PickFinishDance();
            if (dance != null && anim.runtimeAnimatorController != null)
            {
                var rac = anim.runtimeAnimatorController;
                // Guard against re-wrapping our own override if this ever runs twice.
                if (_danceOverride == null || _danceOverride.runtimeAnimatorController != rac)
                {
                    var alreadyOverridden = rac as UnityEngine.AnimatorOverrideController;
                    var baseController = alreadyOverridden != null
                        ? alreadyOverridden.runtimeAnimatorController
                        : rac;
                    _danceOverride = new UnityEngine.AnimatorOverrideController(baseController);
                }

                // Re-point whatever clip the idle state currently uses. Found by NAME rather
                // than assumed, because the graph is theirs and its clip names are not ours to
                // rely on; if the idle clip cannot be found the override is simply skipped and
                // the finish holds idle exactly as it did before.
                var pairs = new System.Collections.Generic.List<
                    System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
                _danceOverride.GetOverrides(pairs);
                bool wired = false;
                for (int i = 0; i < pairs.Count; i++)
                {
                    var key = pairs[i].Key;
                    if (key == null) continue;
                    if (key.name.IndexOf("idle", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    _danceOverride[key] = dance;
                    wired = true;
                }

                if (wired)
                {
                    anim.runtimeAnimatorController = _danceOverride;
                    anim.applyRootMotion = false;   // celebrate on the spot, never travel
                }
            }

            // START ON A RANDOM BEAT OF THE LOOP, not always on frame zero.
            //
            // Both clips are loops (loopTime: 1), so entering at a random phase costs nothing and
            // means two consecutive finishes do not open on the same pose even when the same clip
            // is drawn. Cheap variety on a beat the learner sees thirty times - which is the
            // actual problem with a one-clip celebration, more than which clip it is.
            anim.Play("Start", 0, UnityEngine.Random.value);
            anim.Update(0f);   // so the very first rendered frame of the beat is already posed
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
            _feedbackPill.color = Theme.Alpha(Theme.Ink, 0.88f);
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

            // ORDER IS LOAD-BEARING: BuildOptionPreview captures _pauseChipRect into both tap-blocker
            // arrays, and BuildPauseChip is what assigns it. Built the other way round (as it was),
            // both arrays held a permanent null in the chip slot, IsOverBlocker skipped it, and the
            // chip was never a blocker in either state — so a tap on the chip at x~980/1080 also
            // resolved to lane 2 and steered the runner right. That put the learner in a lane they
            // never chose, on the measure that IS the star count, and it silently disarmed the whole
            // documented point of ApplyTapBlockers ("one gesture can never register twice").
            BuildPauseChip(canvasGo.transform);
            BuildGateTimer(canvasGo.transform);
            BuildOptionPreview(canvasGo.transform);
            BuildPauseOverlay();

            // ANDROID BACK OPENS THE PAUSE SCREEN, rather than a confirmation of its own.
            //
            // That screen already asks the question - KEEP RUNNING against a two-step LEAVE -
            // and stacking a generic "Leave this screen?" over it would be two dialogs for one
            // press, with the second one able to skip the first one's rules. OpenPause is also
            // already guarded on _finished / _leaving / !_runReleased, so BACK during the
            // briefing, the countdown or the victory beat is a safe no-op by construction.
            SummaRace.Core.BackButtonGuard.RegisterAction(OpenPause);
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
            bimg.color = Theme.Alpha(Theme.Wood, 0.94f); // same wood language as the tracker
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
            // SIBLING OF THE BOARD, NOT A CHILD. uGUI draws a child after its parent's own
            // graphic, so parenting this to the board put the gold OVER the panel's wood and
            // only the columns (added later) covered it — a wash across the interior, which is
            // the opposite of what both comments here promise and what the negative inset below
            // is for. Inserting it at the board's own sibling index puts it behind, so the only
            // gold that ever shows is the 16px rim sticking out past the edge. Every other
            // element keeps its relative order, which matters: the HUD banner has already been
            // fixed once for drawing under this board.
            glow.transform.SetParent(parent, false);
            glow.transform.SetSiblingIndex(board.transform.GetSiblingIndex());
            var gimg = glow.AddComponent<UnityEngine.UI.Image>();
            gimg.sprite = WoodPlaqueSprite();
            gimg.type = UnityEngine.UI.Image.Type.Sliced;
            gimg.color = Theme.Alpha(Theme.Gold, 0f); // warm gold, invisible at rest
            gimg.raycastTarget = false;
            var grt = gimg.rectTransform;
            grt.anchorMin = brt.anchorMin; grt.anchorMax = brt.anchorMax;
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
                // The world card's white, so the mapping panel -> road is obvious at a glance.
                img.color = new Color(0.98f, 0.97f, 0.93f);
                img.raycastTarget = true;   // this column IS the control now — see below
                var rt = img.rectTransform;
                float x0 = pad + i * (w + pad);
                rt.anchorMin = new Vector2(x0, 0.07f);
                rt.anchorMax = new Vector2(x0 + w, 0.93f);
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                _previewPlaque[i] = img;
                _previewColumn[i] = rt;

                // THE COLUMN THE LEARNER READS IS THE CONTROL THEY USE.
                //
                // Without this the panel is a reading surface only: the learner reads an answer in
                // the sky band, works out that it is the left one, then finds the left third of
                // the road and taps that. For an ESL nine-year-old inside a ~12s gate that mapping
                // is a working-memory tax laid on top of the comprehension task the race exists to
                // measure. Tapping the column collapses the two into one act.
                //
                // SIZE, at every aspect the canvas can take (match 0 => always 1080 wide, height
                // tracks the aspect). The board spans x 0.03-0.97 = 1015.2 units, of which a
                // column is (1 - 4*0.014)/3 = 31.47% => 319.5 units wide, the same on every
                // device. Its height is 86% of the board's 0.14 of canvas height = 0.1204*H:
                //
                //   aspect  canvas H   column px    at xxhdpi (3px/dp)
                //   4:3       1440      319 x 173      106 x 58 dp
                //   16:10     1728      319 x 208      106 x 69 dp
                //   9:16      1920      319 x 231      106 x 77 dp
                //   20:9      2400      319 x 289      106 x 96 dp
                //
                // Both axes clear the Android 48dp minimum at the worst aspect with room over.
                //
                // Transition.None is deliberate and load-bearing: Selectable's ColorTint would
                // give the pressed AND the still-selected column a different tint from its
                // neighbours, and after F44 nothing on this panel may look different from anything
                // else on it — a surface cue that tracked the correct option was worth 84.7%
                // against 33% for guessing. PaintPreview owns these colours outright.
                var colBtn = col.AddComponent<UnityEngine.UI.Button>();
                colBtn.transition = UnityEngine.UI.Selectable.Transition.None;
                colBtn.targetGraphic = img;
                // Navigation off: the race is steered by WASD/arrows read straight off the device,
                // and a navigable Selectable lets the UI module take a selection here — which on a
                // desktop test would make Enter/Space fire a lane change nobody asked for.
                var nav = colBtn.navigation;
                nav.mode = UnityEngine.UI.Navigation.Mode.None;
                colBtn.navigation = nav;
                int lane = i;   // captured per column; i is the loop variable
                colBtn.onClick.AddListener(() => OnPreviewColumnTapped(lane));

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

            // ---- THE QUESTION THE GATE IS ASKING ----------------------------------------
            //
            // The race never said what it wanted. The learner saw a pulsing letter on the
            // tracker and three sentences, and had to remember what "B" stands for while
            // steering - which is a working-memory tax on top of the comprehension task the
            // race exists to measure, and it falls hardest on exactly the readers the study is
            // about. The web prototype the researcher signed off does say it ("Who is the story
            // about? (Somebody)"), and the owner asked for it back on 2026-08-21.
            //
            // WHY THIS IS SAFE FOR VALIDITY, and it is checkable rather than asserted: the line
            // is drawn from GameText.ReaderSlotHints, so it is IDENTICAL for all three options
            // and IDENTICAL across all thirty stories. It names the SLOT, never the answer, so
            // it cannot be used to choose an option without reading one - which is the exact
            // property F44 and F47 spent two passes restoring after the card-width tell. The
            // support-removal ladder is untouched: the page text is still gone.
            //
            // Kept OUT of the board rather than inside it so the three columns stay a set of
            // three equal things. Nothing about the question may ever land differently on one
            // column than another.
            var qGo = new GameObject("GateQuestion");
            qGo.transform.SetParent(parent, false);
            var qImg = qGo.AddComponent<UnityEngine.UI.Image>();
            qImg.sprite = WoodPlaqueSprite();
            qImg.type = UnityEngine.UI.Image.Type.Sliced;
            qImg.color = Theme.Alpha(Theme.Ink, 0.86f);
            qImg.raycastTarget = false;   // the road behind it is a steering surface
            var qrt = qImg.rectTransform;
            qrt.anchorMin = new Vector2(0.06f, QuestionBandBottom);
            qrt.anchorMax = new Vector2(0.94f, QuestionBandTop);
            qrt.offsetMin = Vector2.zero; qrt.offsetMax = Vector2.zero;

            var qLbl = new GameObject("Text");
            qLbl.transform.SetParent(qGo.transform, false);
            _questionLabel = qLbl.AddComponent<TextMeshProUGUI>();
            if (worldLabelFont != null) _questionLabel.font = worldLabelFont;
            _questionLabel.alignment = TextAlignmentOptions.Center;
            _questionLabel.fontStyle = FontStyles.Bold;
            _questionLabel.color = new Color(1f, 0.96f, 0.88f);
            _questionLabel.raycastTarget = false;
            _questionLabel.enableAutoSizing = true;
            // The five questions are 24-38 characters. The band is 61px tall at 9:16 and 46px at
            // 4:3 (both fractions of the canvas, so this holds on every device), which lands the
            // longest of them at ~34pt and ~30pt respectively - above the readability audit's
            // floor at both. NoWrap so a long one shrinks rather than becoming two half-lines in
            // a one-line box.
            _questionLabel.fontSizeMin = 22f;
            _questionLabel.fontSizeMax = 38f;
            _questionLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _questionLabel.overflowMode = TextOverflowModes.Ellipsis;
            var qlrt = _questionLabel.rectTransform;
            qlrt.anchorMin = Vector2.zero; qlrt.anchorMax = Vector2.one;
            qlrt.offsetMin = new Vector2(18f, 4f); qlrt.offsetMax = new Vector2(-18f, -4f);
            _questionRoot = qGo;
            qGo.SetActive(false);

            _previewRoot = board;
            board.SetActive(false);

            _tapBlockersPlain = new RectTransform[] { _pauseChipRect };
            _tapBlockersWithPanel = new RectTransform[]
                { _pauseChipRect, _previewColumn[0], _previewColumn[1], _previewColumn[2] };
        }

        /// <summary>
        /// A tap on one of the three reading-panel columns. Routes to the SAME MoveToLane the road
        /// tap uses, so one code path decides what a lane request means.
        ///
        /// IT DOES NOT MAKE A PICK. It steers, exactly as a road tap steers; the pick is still made
        /// only when the runner's collider enters an answer card's trigger, so racePicks,
        /// raceFirstOutcome and raceFirstPickCorrect reach SessionLogService by the one route they
        /// always have. Nothing here may ever grow a shortcut to the log — a pick that could arrive
        /// by two routes is a corrupted measure, which is worse than the split-attention problem
        /// this closes.
        ///
        /// The guards mirror ApplyPreviewVisibility: the board is inactive during the briefing, the
        /// 3-2-1, a pause, an exit and after FINISH, so a click cannot physically be raised in any
        /// of those states. They are restated rather than assumed because a Button that is only
        /// safe by virtue of its parent's active flag is one refactor away from not being safe.
        /// _previewTappable additionally rules out the answer-reveal beat, where the panel carries
        /// one gold card that is a statement, not a choice.
        /// </summary>
        private void OnPreviewColumnTapped(int lane)
        {
            if (lane < 0 || lane > 2) return;
            if (_paused || _leaving || _finished || !_runReleased) return;
            if (!_previewTappable || _revealing) return;
            if (_previewRoot == null || !_previewRoot.activeInHierarchy) return;
            if (_previewColumn[lane] == null || !_previewColumn[lane].gameObject.activeInHierarchy) return;
            if (_tapInput == null) return;   // grey-box / editor-direct: nothing to steer
            _tapInput.SelectLane(lane);
        }

        /// <summary>
        /// Tells the road-tap handler which screen rects own their own taps, so ONE gesture can
        /// never register twice.
        ///
        /// EndlessTouchInput reads the pointer directly rather than through the EventSystem (their
        /// swipe path does the same), so a tap on a panel column would otherwise be seen twice: once
        /// as the Button's onClick, and once as "x / (Screen.width / 3)". Those two do not even
        /// agree — the board runs x 0.03-0.97 with 1.4% gutters, so its column boundaries sit ~0.6%
        /// off the screen thirds, and a tap in that sliver would ask for two different lanes in the
        /// same frame. Listing the columns as blockers makes the Button the single winner while the
        /// panel is up; SetBlockers is the mechanism the pause chip already uses, so this adds no
        /// second way of suppressing a tap.
        ///
        /// It is re-applied rather than set once because the ANSWER-REVEAL beat must not create a
        /// dead strip: there the panel is one full-width column that is not a choice, so the
        /// columns stop blocking and a tap in that band falls through to the road mapping exactly
        /// as it did before this existed. (IsOverBlocker already ignores inactive rects, so a hidden
        /// panel needs no swap — only the reveal does.)
        /// </summary>
        private void ApplyTapBlockers()
        {
            if (_tapInput == null) return;
            bool panelClaimsTaps = _previewTappable && _previewRoot != null;
            _tapInput.SetBlockers(panelClaimsTaps && _tapBlockersWithPanel != null
                ? _tapBlockersWithPanel
                : _tapBlockersPlain);
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
            _previewElement = _preparedElement;
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

            // Costed with the SAME number NextGateGap reserved the metres for - and, since
            // 2026-08-22, with the same CONVERSION. Reading RacePreviewLeadSeconds directly here
            // would have easy paying for a 16.2s window and then opening a 12s one, putting the
            // 4 seconds it bought straight back into dead running.
            //
            // ⚠️ This line used to read `speed * ReadWindowSeconds()`, and the comment above it
            // claimed parity with NextGateGap that the arithmetic did not deliver. Same seconds,
            // different conversion: NextGateGap reserves its metres by INTEGRATING the track's
            // 0.2 m/s^2 (`d = v*T + 0.5*a*T^2`), while this opened the panel on a naive
            // `v * T`. The runner accelerates across the lead, so the naive product is short,
            // and the panel therefore went up LATER than the runway that had been bought for it:
            // easy delivered ~14.2-15.3s against the 16.2s ReadWindowEasy was raised to buy, and
            // average/hard ~10.8-11.5s against the ~11.6s a 100wpm reader needs per gate.
            //
            // That is the exact class of bug this file has already fixed twice - in NextGateGap
            // and in SecondsToCover - and reading time is the one place it actually costs the
            // study something, because the race is the headline measure and a window that comes
            // up short pressures the slow readers the whole D2 decision exists to protect.
            // Reuse SecondsToCover rather than re-deriving: one conversion, one place to be
            // wrong, and it is the exact inverse of the one NextGateGap reserved with.
            if (SecondsToCover(track, target - track.worldDistance) > ReadWindowSeconds()) return;

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
            SetQuestionForElement(_previewElement);
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
            // `single` is the answer reveal: a statement, not a question. Its one column must not
            // be tappable, and must not claim taps away from the road either — see ApplyTapBlockers.
            _previewTappable = !single;
            ApplyTapBlockers();

            const float pad = 0.014f;
            float w = (1f - 4f * pad) / 3f;
            for (int i = 0; i < 3; i++)
            {
                string text = i < texts.Length ? texts[i] : null;
                bool has = !string.IsNullOrEmpty(text);
                if (_previewLabel[i] != null)
                {
                    _previewLabel[i].text = has ? text : "";
                    // Cream on the reveal's wood, near-black on a white option column.
                    _previewLabel[i].color = single
                        ? new Color(1f, 0.96f, 0.88f)
                        : new Color(0.10f, 0.10f, 0.12f);
                }
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
                        // WOOD, NOT GOLD (owner device playtest 2026-08-21: "instead of yellow
                        // border you can make it gray or brown like back button gray or some our
                        // brown, more like game feel rather than colour yellow"). The reveal was
                        // a full-width slab of Theme.Gold, which is the single loudest thing on
                        // the race screen and reads as an alert rather than as the game's own
                        // furniture. Deep wood keeps it plainly distinct from the three white
                        // option columns - which is the only job the colour actually had.
                        ? Theme.Wood
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

        /// <summary>Points the question line at a SWBST slot. Out of range is not an error
        /// here - the reveal and the finish both run with no live element - so it simply blanks
        /// rather than throwing inside a coroutine nobody is watching.</summary>
        private void SetQuestionForElement(int element)
        {
            if (_questionLabel == null) return;
            var hints = SummaRace.Constants.GameText.ReaderSlotHints;
            if (element < 0 || hints == null || element >= hints.Length)
            {
                _questionLabel.text = string.Empty;
                return;
            }

            // THE FIVE WORDS WERE ABSENT FROM THE ENTIRE RACE, and this line is where they
            // belong. Before this the run showed a pulsing "S" on the tracker and asked "Who is
            // this story about?", and never once said that the S stands for SOMEBODY or that the
            // two are the same slot. Both prototypes name the framework - the canva banner reads
            // "Collect: SOMEBODY", the web one "Who is the story about? (Somebody)" - and the
            // race is the rung of the ladder where the support is gone, so a learner who has
            // lost the label loses the gate for a VOCABULARY reason rather than a comprehension
            // one. That is measurement error, not difficulty.
            //
            // The question stays and the word is appended, rather than swapping to the canva's
            // "Collect: X" alone: the short form reads faster but assumes the learner already
            // knows what SOMEBODY means, which is the assumption the run is testing.
            //
            // Validity is untouched, by the same argument as the question itself: the word is
            // identical for all three options and identical across all thirty stories, so it
            // cannot be used to choose without reading. It names the SLOT, never the answer.
            //
            // Pastel, not the full palette colour - this plaque is Theme.Ink and the full
            // colours fall to 3.6:1 over a bright sky. See SwbstPalette.PastelHexForIndex.
            var type = (_story != null && _story.elements != null && element < _story.elements.Length)
                ? _story.elements[element].type : null;
            if (string.IsNullOrEmpty(type)) { _questionLabel.text = hints[element]; return; }

            _questionLabel.text = hints[element] + "   <color=#"
                + SummaRace.Constants.SwbstPalette.PastelHexForIndex(element) + ">" + type + "</color>";
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
            // The question rides with the panel exactly: it is the question those three columns
            // are the answers to, and a question left on screen with nothing under it would be
            // asking about a gate that has already gone.
            if (_questionRoot != null && _questionRoot.activeSelf != (show && !_revealing))
                _questionRoot.SetActive(show && !_revealing);
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

        // ---------- gate-arrival countdown (F59) ----------

        /// <summary>
        /// How long, in seconds, until the runner covers <paramref name="metres"/> - integrating
        /// the track's acceleration instead of assuming a constant speed.
        ///
        /// This is the exact inverse of the seconds-to-metres conversion in NextGateGap, and it
        /// exists for the same reason that one does: their track adds k_Acceleration (0.2 m/s^2)
        /// every second up to maxSpeed, so metres / speed OVERSTATES the time - by enough to
        /// matter. A chip that says "8s" and delivers the gate in 6 is worse than no chip,
        /// because the whole point of showing it is that it is honest.
        /// </summary>
        private static float SecondsToCover(TrackManager track, float metres)
        {
            if (track == null || metres <= 0f) return 0f;

            const float accel = 0.2f;   // TrackManager.k_Acceleration (protected const there)
            float v = Mathf.Max(track.speed, track.minSpeed);
            float vMax = Mathf.Max(track.maxSpeed, v);

            // Distance available before the runner tops out.
            float toTop = accel > 0f ? (vMax - v) / accel : 0f;
            float dTop = v * toTop + 0.5f * accel * toTop * toTop;

            if (metres <= dTop || accel <= 0f)
            {
                // 0.5*a*t^2 + v*t - d = 0, positive root.
                float disc = v * v + 2f * accel * metres;
                if (accel <= 0f) return v > 0f ? metres / v : 0f;
                return (Mathf.Sqrt(Mathf.Max(disc, 0f)) - v) / accel;
            }

            return toTop + (metres - dTop) / Mathf.Max(vMax, 0.01f);
        }

        /// <summary>
        /// Paints the countdown to the next SWBST part. Shown only for ANSWER gates (0-4): the
        /// finish has no options to read, so it has no reading window to count down, and the
        /// banner owns that band for the final stretch anyway.
        ///
        /// Deliberately NOT a race clock (L1). It counts toward an arrival, not a deadline -
        /// nothing happens at zero except that the cards are there, and every pick/miss rule is
        /// exactly what it was without it. It also hides itself during a pause, a leave, the
        /// answer reveal and the finish, so it can never tick at a learner who is not running.
        /// </summary>
        private void UpdateGateTimer(TrackManager track)
        {
            if (_gateTimerChip == null) return;

            bool show = false;
            int seconds = 0;
            // MOCKUP 19 PAIRS THE LAST GATE WITH A BIG "FINISH LINE!" while the THEN cards are
            // still on screen. We had no equivalent: the banner stays empty for the whole of the
            // fifth gate and only speaks AFTER it resolves, so the one beat of a 90-107s run
            // that most wants a "nearly there" was the one beat that said nothing.
            //
            // It goes in the CHIP rather than the banner deliberately. Both hang off the same
            // anchor under the reading board - banner at -12px, chip at -14px - and they only
            // coexist today because the banner speaks solely at element 5, where the chip is
            // hidden. A fifth-gate line in the banner would land on top of the countdown that is
            // still running. Same plaque, same moment, no new geometry.
            bool lastGate = false;

            if (track != null && _runReleased && !_finished && !_paused && !_leaving && !_revealing
                && SummaRace.Constants.GameRules.RaceGateTimerVisibleSeconds > 0f)
            {
                bool active = _activeGateRoot != null;
                int element = active ? _activeElement : _pendingElement;
                float target = active ? _activeGateDistance : _pendingGateDistance;
                lastGate = element == 4;

                if (element >= 0 && element < 5 && target >= 0f)
                {
                    float remaining = SecondsToCover(track, target - track.worldDistance);
                    if (remaining <= SummaRace.Constants.GameRules.RaceGateTimerVisibleSeconds)
                    {
                        show = true;
                        // Ceil, so the chip never shows "0s" while the gate is still ahead - it
                        // reads 1s until the part actually arrives, then disappears.
                        // FarSentinel outside the counting window: same chip, no number, so the
                        // readout is never missing and never a race clock. See
                        // GameRules.RaceGateTimerCountdownSeconds for why it is split.
                        seconds = remaining > SummaRace.Constants.GameRules.RaceGateTimerCountdownSeconds
                            ? FarSentinel
                            : Mathf.CeilToInt(remaining);
                    }
                }
            }

            if (_gateTimerChip.activeSelf != show) _gateTimerChip.SetActive(show);
            if (!show) { _gateTimerShown = -1; return; }

            // Repaint on the second, not every frame: TMP rebuilds its mesh on every text set.
            // The last-gate flag is part of the cache key - without it, a fifth gate arriving on
            // the same second reading as the fourth would keep the fourth's wording.
            if (seconds == _gateTimerShown && lastGate == _gateTimerLastShown) return;
            _gateTimerShown = seconds;
            _gateTimerLastShown = lastGate;
            if (_gateTimerText != null)
                _gateTimerText.text = seconds == FarSentinel
                    ? (lastGate ? SummaRace.Constants.GameText.RaceLastGateTimerFar
                                : SummaRace.Constants.GameText.RaceGateTimerFar)
                    : (lastGate ? SummaRace.Constants.GameText.RaceLastGateTimer(seconds)
                                : SummaRace.Constants.GameText.RaceGateTimer(seconds));
        }

        /// <summary>
        /// The countdown chip itself. Same wood plaque as the feedback pill and the pause chip,
        /// so it reads as part of the same HUD, and centred in the banner band directly under
        /// the reading panel - the clock belongs with the thing it is timing.
        ///
        /// raycastTarget is off on both the plaque and its label: the three preview columns and
        /// the screen thirds below are steering surfaces, and a readout that swallowed a tap
        /// would cost the learner a lane change at exactly the moment they are about to need one.
        /// </summary>
        /// <summary>Hides the countdown and forgets the second it was showing, so it
        /// repaints cleanly when the run resumes.</summary>
        private void HideGateTimer()
        {
            if (_gateTimerChip != null) _gateTimerChip.SetActive(false);
            _gateTimerShown = -1;
        }

        /// <summary>Takes the patrol cameo off screen and ends the beat. Needed at every exit
        /// that stops LateUpdate: it returns on _paused/_leaving, and the cameo is skipped once
        /// _finished, so a wrong pick in the closing seconds of a run would otherwise leave the
        /// cop standing in frame through the victory beat, or frozen over the pause menu.</summary>
        private void HidePatrolCameo()
        {
            if (_patrol != null && _patrol.gameObject.activeSelf) _patrol.gameObject.SetActive(false);
            _menaceTimer = 0f;
            _menaceSurging = false;
            _wasSurging = false;

            // AND PUT THE CAMERA BACK. Every caller of this method is a state that stops
            // LateUpdate reaching the dolly: FinishRoutine sets _finished, OpenPause sets
            // _paused, and both return before UpdateChaseCameraDolly. A wrong pick in the
            // closing seconds of a run would otherwise hold the pulled-back framing through the
            // whole victory beat, or freeze it behind the pause menu - the same class of bug as
            // the countdown chip left showing a second that never ticks.
            if (_camChaseCaptured && _camDollyApplied)
            {
                var cam = Camera.main;
                if (cam != null) cam.transform.localPosition = _camChaseLocalPos;
                _camDollyApplied = false;
            }
            _camDolly = 0f;
        }

        private void BuildGateTimer(Transform parent)
        {
            var chip = new GameObject("GateTimerChip");
            chip.transform.SetParent(parent, false);
            var img = chip.AddComponent<UnityEngine.UI.Image>();
            img.sprite = WoodPlaqueSprite();
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.color = Theme.Alpha(Theme.Ink, 0.82f);
            img.raycastTarget = false;

            var rt = img.rectTransform;
            // Hung off the reading band's underside exactly as the banner is, so the two stay
            // together at every aspect ratio (see BuildHud for why a fixed pixel offset does not).
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, PreviewBandBottom);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -14f);
            // 560x112 @ 56pt max (was 420x86 @ 42) — owner playtest 2026-08-21: at the old
            // size the chip did not register at all while steering ("I don't see the timer").
            rt.sizeDelta = new Vector2(560f, 112f);

            _gateTimerText = MakeHudText(chip.transform, new Vector2(0.5f, 0.5f), Vector2.zero, 56f);
            _gateTimerText.rectTransform.sizeDelta = new Vector2(536f, 102f);
            _gateTimerText.enableAutoSizing = true;
            _gateTimerText.fontSizeMin = 32f;   // above the readability audit's acuity floor
            _gateTimerText.fontSizeMax = 56f;
            _gateTimerText.color = Theme.Gold;  // 12.2:1 on Ink, per Theme's contrast table

            _gateTimerChip = chip;
            chip.SetActive(false);   // only while an answer gate is actually on its way
        }

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

            // Same wood-and-cream panel as the briefing: these two and the leave confirmation
            // are the race's three cards and a learner sees them in one sitting, so one of them
            // staying gold would read as a different game.
            var card = new GameObject("PauseCard");
            card.transform.SetParent(canvasGo.transform, false);
            var cardImg = MakeRacePanel(card);
            var crt = cardImg.rectTransform;
            crt.anchorMin = new Vector2(0.08f, 0.40f);
            crt.anchorMax = new Vector2(0.92f, 0.70f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;

            var title = MakeHudText(card.transform, new Vector2(0.5f, 0.70f), Vector2.zero, 76f);
            title.text = SummaRace.Constants.GameText.RacePauseTitle;
            title.color = Theme.TextBrownDeep;   // on the cream face, not on gold
            title.fontStyle = FontStyles.Bold;

            var body = MakeHudText(card.transform, new Vector2(0.5f, 0.34f), Vector2.zero, 42f);
            body.text = SummaRace.Constants.GameText.RacePauseBody;
            body.color = Theme.TextBrown;
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
            lImg.color = Theme.Alpha(Theme.Wood, 0.95f);
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

            BuildLeaveConfirm(canvasGo.transform);

            canvasGo.SetActive(false);
        }

        /// <summary>
        /// The confirmation that leaving a run actually asks for.
        ///
        /// It used to be a LABEL SWAP: the first tap on the leave chip changed its text from
        /// "LEAVE RACE" to "LEAVE?" and armed a second tap. Owner device playtest 2026-08-21:
        /// "when I click exit button, the back button of the exit panel or confirmation not
        /// appear, and make sure when leave game make sure it has confirmation." He is right on
        /// both counts. A word changing inside a small chip is not a confirmation - there is
        /// nothing on screen that says a question was asked - and there is no control that
        /// means "no", only the unrelated-looking KEEP RUNNING behind it. A nine-year-old who
        /// taps once and hesitates has no way to read what state they are in.
        ///
        /// So: a panel, a question, and two named ways out, with the way BACK as the large
        /// obvious one. Leaving stays deliberately the small quiet option (D7 - the race never
        /// ends by itself and never punishes), and it is still two taps, so one stray press
        /// still cannot end a run.
        /// </summary>
        private void BuildLeaveConfirm(Transform parent)
        {
            var root = new GameObject("LeaveConfirm");
            root.transform.SetParent(parent, false);
            var rrt = root.AddComponent<RectTransform>();
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;

            // Its own dim over the pause screen, so the question owns the frame and a tap that
            // misses both buttons hits nothing behind it.
            var dim = new GameObject("Dim");
            dim.transform.SetParent(root.transform, false);
            var dimImg = dim.AddComponent<UnityEngine.UI.Image>();
            dimImg.color = new Color(0.03f, 0.05f, 0.09f, 0.80f);
            var drt = dimImg.rectTransform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;

            var card = new GameObject("Card");
            card.transform.SetParent(root.transform, false);
            var cardImg = MakeRacePanel(card);
            var crt = cardImg.rectTransform;
            crt.anchorMin = new Vector2(0.08f, 0.42f);
            crt.anchorMax = new Vector2(0.92f, 0.68f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;

            var title = MakeHudText(card.transform, new Vector2(0.5f, 0.68f), Vector2.zero, 66f);
            title.text = SummaRace.Constants.GameText.RaceLeaveConfirmTitle;
            title.color = Theme.TextBrownDeep;
            title.fontStyle = FontStyles.Bold;
            title.rectTransform.sizeDelta = new Vector2(720f, 130f);

            var body = MakeHudText(card.transform, new Vector2(0.5f, 0.32f), Vector2.zero, 40f);
            body.text = SummaRace.Constants.GameText.RaceLeaveConfirmBody;
            body.color = Theme.TextBrown;
            body.rectTransform.sizeDelta = new Vector2(700f, 130f);

            // GOING BACK IS THE BIG ONE. Same CTA treatment as KEEP RUNNING and as START, so
            // the safe answer is the one the thumb finds first.
            var backRing = new GameObject("BackRing");
            backRing.transform.SetParent(root.transform, false);
            var brImg = backRing.AddComponent<UnityEngine.UI.Image>();
            brImg.sprite = greenPillSprite != null ? greenPillSprite : worldCardSprite;
            if (brImg.sprite != null) brImg.type = UnityEngine.UI.Image.Type.Sliced;
            brImg.color = new Color(0.10f, 0.30f, 0.14f);
            brImg.raycastTarget = false;
            var brRt = brImg.rectTransform;
            // SIDE BY SIDE (owner, 2026-08-22). NO on the left, YES on the right — the fork
            // shape a yes/no actually is, and the ordering Android itself uses, so muscle memory
            // from anywhere else on the tablet still works. See Core/BackButtonGuard for the
            // full reasoning; the two confirmations in this game must not disagree about which
            // side means what.
            brRt.anchorMin = brRt.anchorMax = brRt.pivot = new Vector2(0.5f, 0.28f);
            brRt.sizeDelta = new Vector2(438f, 176f);
            brRt.anchoredPosition = new Vector2(-227f, 0f);

            var back = new GameObject("BackButton");
            back.transform.SetParent(root.transform, false);
            var bImg = back.AddComponent<UnityEngine.UI.Image>();
            bImg.sprite = greenPillSprite != null ? greenPillSprite : worldCardSprite;
            if (bImg.sprite != null) bImg.type = UnityEngine.UI.Image.Type.Sliced;
            bImg.color = greenPillSprite != null ? Color.white : new Color(0.30f, 0.75f, 0.35f);
            var bRt = bImg.rectTransform;
            bRt.anchorMin = bRt.anchorMax = bRt.pivot = new Vector2(0.5f, 0.28f);
            bRt.sizeDelta = new Vector2(410f, 150f);
            bRt.anchoredPosition = new Vector2(-227f, 0f);
            var bBtn = back.AddComponent<UnityEngine.UI.Button>();
            bBtn.targetGraphic = bImg;
            bBtn.onClick.AddListener(DisarmLeave);

            var bLabel = MakeHudText(back.transform, new Vector2(0.5f, 0.5f), Vector2.zero, 54f);
            bLabel.text = SummaRace.Constants.GameText.RaceLeaveCancelLabel;
            bLabel.color = Color.white;
            bLabel.fontStyle = FontStyles.Bold;
            bLabel.rectTransform.sizeDelta = new Vector2(380f, 120f);
            bLabel.enableAutoSizing = true; bLabel.fontSizeMin = 30f; bLabel.fontSizeMax = 44f;

            var go = new GameObject("ConfirmLeaveButton");
            go.transform.SetParent(root.transform, false);
            var gImg = go.AddComponent<UnityEngine.UI.Image>();
            gImg.sprite = WoodPlaqueSprite();
            gImg.type = UnityEngine.UI.Image.Type.Sliced;
            gImg.color = new Color(0.52f, 0.34f, 0.20f);   // warm, but not the green of "stay"
            var gRt = gImg.rectTransform;
            gRt.anchorMin = gRt.anchorMax = gRt.pivot = new Vector2(0.5f, 0.28f);
            gRt.sizeDelta = new Vector2(410f, 150f);
            gRt.anchoredPosition = new Vector2(227f, 0f);
            var gBtn = go.AddComponent<UnityEngine.UI.Button>();
            gBtn.targetGraphic = gImg;
            gBtn.onClick.AddListener(LeaveRace);

            var gLabel = MakeHudText(go.transform, new Vector2(0.5f, 0.5f), Vector2.zero, 40f);
            gLabel.text = SummaRace.Constants.GameText.RaceLeaveConfirm;
            gLabel.color = new Color(1f, 0.94f, 0.84f);
            gLabel.fontStyle = FontStyles.Bold;
            gLabel.rectTransform.sizeDelta = new Vector2(380f, 120f);
            gLabel.enableAutoSizing = true; gLabel.fontSizeMin = 30f; gLabel.fontSizeMax = 44f;

            _leaveConfirmRoot = root;
            root.SetActive(false);
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
            // Same reason as the finish: Update returns on _paused before it reaches the
            // countdown, so a chip left visible would sit there counting nothing while the
            // world is frozen - which is the one thing a timer must never look like.
            HideGateTimer();
            HidePatrolCameo();
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

        /// <summary>Still two taps to leave a run - but the first opens a question rather than
        /// silently rewording a chip. See BuildLeaveConfirm.</summary>
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
            if (_leaveConfirmRoot != null) _leaveConfirmRoot.SetActive(true);
            if (SummaRace.Core.AudioManager.Instance != null)
                SummaRace.Core.AudioManager.Instance.PlaySfx(SummaRace.Constants.AudioKeys.SfxClick);
        }

        /// <summary>Backs out of the confirmation and returns to the pause screen. Wired to the
        /// panel's own large GO BACK button, and also called by OpenPause/ResumeFromPause so the
        /// question can never still be open the next time the screen is used.</summary>
        private void DisarmLeave()
        {
            bool wasOpen = _leaveArmed;
            _leaveArmed = false;
            if (_leaveConfirmRoot != null && _leaveConfirmRoot.activeSelf)
                _leaveConfirmRoot.SetActive(false);
            if (wasOpen && SummaRace.Core.AudioManager.Instance != null)
                SummaRace.Core.AudioManager.Instance.PlaySfx(SummaRace.Constants.AudioKeys.SfxClick);
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
            bimg.color = Theme.Wood;
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

                // THE SWBST COLOUR, AS A STRIP RATHER THAN AS THE WHOLE PLAQUE.
                //
                // Owner device playtest 2026-08-21: "I don't like the different color background
                // swbst". Five saturated blocks across the top of a moving 3D scene read as
                // candy, not as part of the game - and they are the loudest thing on screen
                // while the learner is meant to be watching the road and reading three answers.
                //
                // But the palette is a TEACHING device (F18): the same five colours carry the
                // framework on the briefing chips, the Arrange slots and the Summary list, and
                // deleting them here would break the one thing that ties those screens together.
                // So the colour stays and stops being the background. The plaque face is uniform
                // dark wood in every state; this strip along its foot carries the element's
                // colour. Colour is never the only channel either way - the letter is right
                // there, and filled-vs-dark distinguishes collected from not (F49).
                var accGo = new GameObject("Accent");
                accGo.transform.SetParent(slot.transform, false);
                var acc = accGo.AddComponent<UnityEngine.UI.Image>();
                acc.sprite = wood; acc.type = UnityEngine.UI.Image.Type.Sliced;
                acc.raycastTarget = false;
                var art = acc.rectTransform;
                art.anchorMin = new Vector2(0f, 0f); art.anchorMax = new Vector2(1f, 0f);
                art.pivot = new Vector2(0.5f, 0f);
                art.offsetMin = new Vector2(12f, 9f); art.offsetMax = new Vector2(-12f, 0f);
                art.sizeDelta = new Vector2(art.sizeDelta.x, 14f);
                _slotAccent[i] = acc;

                var lblGo = new GameObject("Label");
                lblGo.transform.SetParent(slot.transform, false);
                var lbl = lblGo.AddComponent<TextMeshProUGUI>();
                if (worldLabelFont != null) lbl.font = worldLabelFont;
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.fontStyle = FontStyles.Bold;
                lbl.enableAutoSizing = true;
                // 24-50 -> 40-72, BECAUSE THE PLAQUE NOW CARRIES A LETTER, NOT A WORD.
                //
                // "The font is too small" (owner, 2026-08-21) had a structural cause, not a
                // timid number: the board is 880 units wide on a 1080 reference because the
                // pause chip needs the rest of that row to reach Android's 48dp minimum, which
                // leaves a 140-unit label box - and "SOMEBODY" needs 128 of those AT THE 24pt
                // FLOOR. The word fit only by being small, on every gate of all thirty races.
                // No font change could fix that; only removing the word could.
                //
                // WHERE THE WORD WENT: onto the new question line under the tracker, in a full
                // sentence at sentence size ("Who is this story about?"). That is strictly more
                // than the plaque was saying - the tracker's job is to show the five slots and
                // which one is live, and a large letter does that better than a shrunken word.
                lbl.fontSizeMin = 40f;
                lbl.fontSizeMax = 72f;
                lbl.overflowMode = TextOverflowModes.Ellipsis;
                lbl.textWrappingMode = TextWrappingModes.NoWrap;
                lbl.raycastTarget = false;
                var lrt = lbl.rectTransform;
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                // Bottom inset clears the accent strip so a descender never sits on it.
                lrt.offsetMin = new Vector2(8f, 22f); lrt.offsetMax = new Vector2(-8f, -6f);
                _slotLabel[i] = lbl;
            }
        }

        /// <summary>Repaints the SWBST slots. Elements resolve in order: slot i is collected
        /// once the current target has passed it. All three states share ONE uniform dark wood
        /// plaque (owner 2026-08-21 — five coloured backgrounds read as candy over a moving 3D
        /// scene); what changes is the FILL and the letter, with the element's colour carried by
        /// the accent strip along the plaque's foot:
        ///   upcoming  — dark plaque, dimmed strip, faded "?"
        ///   current   — dark plaque lifted 12%, full-colour strip, white letter, pulse
        ///   collected — cream FILLED plaque, full-colour strip, letter in the element's ink
        /// Fill-vs-dark is the collected/not signal and does not depend on colour vision.</summary>
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

                var acc = _slotAccent[i];
                var swbst = SummaRace.Constants.SwbstPalette.ForIndex(i);

                if (i < current) // collected — the plaque FILLS
                {
                    // Filled cream face with the element's own ink for the letter. Fill-vs-dark
                    // is what separates collected from not, and it is perceivable without colour
                    // vision (F49); the colour on top of it is the framework, not the signal.
                    // InkForIndex rather than DeepForIndex: Ink is the variant measured against
                    // a cream card (5.18-7.31:1), Deep is a background colour and drops WANTED
                    // and SO under AA on exactly this background.
                    _slotBg[i].color = Theme.Cream;
                    if (acc != null) acc.color = swbst;
                    lbl.text = letter;
                    lbl.color = SummaRace.Constants.SwbstPalette.InkForIndex(i);
                    _slotRect[i].localScale = Vector3.one;
                }
                else if (i == current) // current target — lifted, bright letter, pulse
                {
                    _slotBg[i].color = Theme.WoodLight;
                    if (acc != null) acc.color = swbst;
                    lbl.text = letter;
                    lbl.color = Color.white;
                    _slotRect[i].localScale = Vector3.one * 1.12f;
                    Tween.PunchScale(_slotRect[i], Vector3.one * 0.1f, 0.45f);
                }
                else // upcoming / empty — dark wood, dimmed strip, faded "?"
                {
                    _slotBg[i].color = new Color(0.40f, 0.29f, 0.18f);   // lifted 2026-08-22
                    if (acc != null) acc.color = Theme.Alpha(Color.Lerp(swbst, Color.black, 0.45f), 0.75f);
                    lbl.text = "?";
                    lbl.color = new Color(1f, 0.96f, 0.85f, 0.5f);
                    _slotRect[i].localScale = Vector3.one;
                }
            }
        }

        /// <summary>
        /// THE RACE'S OWN PANEL: a dark wood frame around a cream reading face.
        ///
        /// Owner device playtest 2026-08-21, callout anchored over the briefing screenshot:
        /// "instead of yellow border you can make it gray or brown like back button gray or some
        /// our brown, more like game feel rather than colour yellow... if you can improve this
        /// scene or panel please improve."
        ///
        /// The three race cards (mission briefing, pause, leave confirmation) were all on
        /// Resources "UI/panel_gold" - a gold-bordered card with a yellow title pill over it.
        /// Tinting that sprite brown was not available: one Image cannot darken the border
        /// without darkening the cream interior with it, and the body text on these cards is
        /// Theme.TextBrown, which on a browned interior stops being readable. So the frame and
        /// the face become two graphics: the outer takes the wood the race HUD already speaks
        /// (tracker board, feedback pill, pause chip, gate chip), the inner keeps the cream.
        ///
        /// The inner face is added FIRST so uGUI draws it behind everything the caller adds
        /// afterwards, and it is not a raycast target, so nothing about hit-testing changes.
        ///
        /// Deliberately NOT applied to the gold title lockups on Boot, Main Menu, Story Select
        /// and Results: that gold is the app's brand across five screens and the owner's note is
        /// about the race's panels. One place to change it back if that call is ever revisited.
        /// </summary>
        private static UnityEngine.UI.Image MakeRacePanel(GameObject card)
        {
            var frame = card.AddComponent<UnityEngine.UI.Image>();
            frame.sprite = WoodPlaqueSprite();
            frame.type = UnityEngine.UI.Image.Type.Sliced;
            frame.color = Theme.Wood;

            var inner = new GameObject("Inner");
            inner.transform.SetParent(card.transform, false);
            var innerImg = inner.AddComponent<UnityEngine.UI.Image>();
            innerImg.sprite = WoodPlaqueSprite();
            innerImg.type = UnityEngine.UI.Image.Type.Sliced;
            innerImg.color = Theme.Cream;
            innerImg.raycastTarget = false;
            var irt = innerImg.rectTransform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(18f, 18f); irt.offsetMax = new Vector2(-18f, -18f);
            return frame;
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
            pill.color = Theme.Alpha(Theme.Ink, 0.88f); // same backing as the feedback line
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
            // ...but the CAMEO (RacePatrolCameoEnabled) does spawn him. It never holds an
            // offset from the runner, which is the property all three failures above were
            // fighting for — see the constant for the geometry and how it was solved.
            if (!SummaRace.Constants.GameRules.RacePatrolEnabled
                && !SummaRace.Constants.GameRules.RacePatrolCameoEnabled) return;

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
            // NO SetBool HERE. The object is spawned INACTIVE (the cameo is hidden until a wrong
            // pick), and Unity resets an Animator when its GameObject is enabled - so this call
            // was silently discarded every time. Worse, it read like the run had been started,
            // which is half of why the real entry below was left relying on a transition.
            // UpdatePatrolCameo enters the run state directly on the frame he becomes visible.

            // Station him out of shot at the depth he will run at for the whole race: the bump
            // moves him sideways, never forward, so there is nothing to ease in from behind.
            var p = runner.transform.position;
            _patrolGap = SummaRace.Constants.GameRules.PatrolBodyMargin + 1f;
            // Well outside the frame; UpdatePatrol re-derives both axes from the live camera and
            // the live bounds on its first frame, before he is ever visible.
            _patrol.position = new Vector3(p.x + _patrolSide * 6f, p.y, p.z - _patrolGap);
            _patrol.rotation = Quaternion.identity; // faces down the road, same as the runner
            // The cameo starts hidden ALWAYS and shows itself only for the wrong-answer
            // beat; only the retired chase wanted him on screen for the whole run.
            go.SetActive(_runReleased && !SummaRace.Constants.GameRules.RacePatrolCameoEnabled);
        }


        /// <summary>
        /// The patrol CAMEO: a TAIL. He runs in the runner's own lane, a constant
        /// GameRules.PatrolChaseGap behind him, for the length of the wrong-answer surge, and
        /// the chase camera dollies back to make room for him. Hidden for the whole of a clean
        /// run.
        ///
        /// WHY A HELD GAP (owner device playtest 2026-08-21: "look buggy floating and flying to
        /// player... subway usually if obstacle was hit it appear running at the back of player,
        /// same movement and animation of payer but put it the back of player same x-y-z of
        /// wehre the paleyr postion, and then move the player camera focuse a llitbe back so
        /// that the patrol has space to see").
        ///
        /// The overtake this replaces swept him 4m-behind to 26m-ahead in 2s: 15 m/s ON TOP of
        /// the runner's own 10-30 m/s, against a run clip at 1.35x. Motion and animation
        /// disagreed by about a factor of two, and that mismatch IS "floating" and "flying" -
        /// the feet were not driving him. Holding the gap makes his ground speed identical to
        /// the runner's, so the clip at 1.0x drives the motion exactly and there is nothing
        /// left to skate. There is also no relative motion to smooth, ease or lerp, which is
        /// what every previous version got wrong in a different way.
        ///
        /// THREE THINGS ARE LOAD-BEARING AND NONE OF THEM IS A TUNED NUMBER:
        ///
        ///  1. He is placed from the LIVE player every frame, never integrated. Their track
        ///     floats its origin roughly every 100m; a follower holding a world-space target
        ///     gets stranded, which is how the first chase died.
        ///  2. His feet are put on the road by BOUNDS, not by his pivot. The old code copied the
        ///     runner's transform y, which is only correct if the two prefabs happen to share a
        ///     pivot convention - they do not, and that is the second half of "floating". The
        ///     correction is measured ONCE per beat, after the animator has been forced to pose,
        ///     and then held: measuring it every frame chases the stride (the lowest point of a
        ///     running body rises and falls) and reintroduces jitter.
        ///  3. The camera pullback is not decoration. Projected through the scene's own camera,
        ///     a cop at this gap is at 1.30-1.56 of the frame half-extent WITHOUT it - off
        ///     screen. See GameRules.PatrolCameraPullback; locked by PatrolChaseGeometryTests.
        ///
        /// He cannot catch anybody: the gap is constant, so there is nothing to close.
        /// timesCaught stays 0 for every run ever logged (D7/L3).
        /// </summary>
        private void UpdatePatrolCameo(TrackManager track)
        {
            if (_patrol == null || track == null) return;
            var runner = track.characterController;
            if (runner == null) return;

            bool surging = _menaceSurging;

            if (!surging)
            {
                // Off the moment the beat ends. Hidden rather than parked off-screen: a hidden
                // renderer costs nothing, and there is no position to preserve between beats.
                if (_patrol.gameObject.activeSelf) _patrol.gameObject.SetActive(false);
                _wasSurging = false;
                return;
            }

            var playerPos = runner.transform.position;
            // The kid's LANE lives on the character, not on the controller's own transform:
            // characterController.transform.position.x is 0 for the whole run (measured), the
            // 1.5m lane offset is on characterCollider/character. Reading the controller would
            // park the cop on the centre line whatever lane the kid is in.
            var bodyT = runner.characterCollider != null
                ? runner.characterCollider.transform
                : runner.transform;

            if (!_wasSurging)
            {
                // Ground height captured once. Following the runner's live y would lift the cop
                // on every jump.
                _patrolGroundY = playerPos.y;
                _patrolGrounded = true;
                _patrolFootFix = 0f;
                _patrolFootFixed = false;

                // Placed BEFORE he is first drawn, so no frame ever shows him mid-slide.
                _patrol.position = new Vector3(bodyT.position.x, _patrolGroundY,
                    playerPos.z - SummaRace.Constants.GameRules.PatrolChaseGap);
                _patrol.rotation = Quaternion.identity;

                _patrol.gameObject.SetActive(true);
                if (_patrolAnim != null)
                {
                    // ---- THE COP USED TO GLIDE ALONG STANDING STILL FOR A FIFTH OF A SECOND ----
                    //
                    // Owner, 2026-08-22: "the patrol looks buggy, are you triggering the
                    // animation double?" - and the substance of that is exactly right.
                    //
                    // PatrolAnimator's DEFAULT STATE IS `Idle`, and `Run` is reachable only
                    // through a transition (0.1-0.2s) driven by the `Running` bool. Activating
                    // the GameObject resets the Animator to that default. So the sequence was:
                    // enable -> Animator resets to Idle -> we set the bool -> a 0.1-0.2s blend
                    // begins. For those frames a STANDING man slid down the road at 10-30 m/s
                    // and then popped into a run. On a 19-part rigid rig that reads as a glitch,
                    // and it happened on every single wrong-answer beat.
                    //
                    // Play() enters the state outright, so his first rendered frame is already
                    // mid-stride. The bool is still set, so the state machine agrees with us and
                    // nothing fights the transition afterwards.
                    _patrolAnim.SetBool("Running", true);
                    // Their Animator ships as CullUpdateTransforms and resolves visibility from
                    // the PREVIOUS frame, so an unposed first frame would be limbs at bind
                    // offsets rather than a slightly wrong pose.
                    _patrolAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    // 1x, because he holds a constant gap: his ground speed IS the runner's, so
                    // the run clip drives the motion exactly. The overtake needed 1.35 to cover
                    // for 15 m/s of sweep it was doing on top of the animation, and still lost.
                    _patrolAnim.speed = 1f;
                    // Random phase so two beats in a row do not start on the same footfall.
                    _patrolAnim.Play(PatrolRunState, 0, UnityEngine.Random.value);
                    // Evaluate now, so the bounds measured below describe a running cop and the
                    // first visible frame is already correct.
                    _patrolAnim.Update(0f);
                }
                _wasSurging = true;
            }

            // ---- FEET ON THE ROAD, whatever the prefab's pivot convention is ----
            // Measured once per beat and then held; see (2) in the summary for why not per frame.
            if (!_patrolFootFixed)
            {
                Bounds copB, kidB;
                if (BodyBounds(_patrol, ref _patrolRenderers, out copB)
                    && BodyBounds(bodyT, ref _kidRenderers, out kidB))
                {
                    _patrolFootFix = kidB.min.y - copB.min.y;
                    _patrolFootFixed = true;
                }
            }

            _patrol.position = new Vector3(
                bodyT.position.x,
                (_patrolGrounded ? _patrolGroundY : playerPos.y) + _patrolFootFix,
                playerPos.z - SummaRace.Constants.GameRules.PatrolChaseGap);
            // Straight down the road, exactly as the runner faces. Never yawed: on this rigid rig
            // a yaw swings the mesh 0.65m off its pivot (measured during the chase work), which is
            // how a safe offset stops being safe.
            _patrol.rotation = Quaternion.identity;
        }

        /// <summary>
        /// Slides the chase camera back and up while the patrol is on screen, and home again
        /// afterwards. This is the owner's "move the player camera focus a little back so that
        /// the patrol has space to see", and it is the enabling condition for the tail rather
        /// than a flourish - see GameRules.PatrolCameraPullback for the projection that says so.
        ///
        /// Applied on the camera's LOCAL position, because TrackManager parents Camera.main to
        /// the runner. The resting pose is captured by CountdownRoutine, which already has to
        /// know it in order to swoop back onto it after GO!.
        ///
        /// It writes NOTHING while the blend is at rest. That matters: the GO! swoop is still
        /// lerping this same transform for 0.6s after the run is released, and two writers on one
        /// transform is how the pre-race camera bugs of F42 happened.
        /// </summary>
        private void UpdateChaseCameraDolly()
        {
            if (!_camChaseCaptured) return;
            var cam = Camera.main;
            if (cam == null) return;

            float target = _menaceSurging ? 1f : 0f;
            float step = Time.deltaTime
                       / Mathf.Max(0.01f, SummaRace.Constants.GameRules.PatrolCameraBlendSeconds);
            _camDolly = Mathf.MoveTowards(_camDolly, target, step);

            if (_camDolly <= 0.0001f)
            {
                // Land exactly on the authored pose once, then stop writing entirely.
                if (_camDollyApplied)
                {
                    cam.transform.localPosition = _camChaseLocalPos;
                    _camDollyApplied = false;
                }
                return;
            }

            float e = Mathf.SmoothStep(0f, 1f, _camDolly);
            cam.transform.localPosition = _camChaseLocalPos
                + SummaRace.Constants.GameRules.PatrolCameraPullback * e;
            _camDollyApplied = true;
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

            // Owned by LateUpdate now, not by this method — see UpdateDangerVignette.
            bool surging = _menaceSurging;

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

        }

        /// <summary>
        /// The amber wrong-answer vignette. Driven by the menace surge and nothing else.
        ///
        /// This used to live at the tail of <see cref="UpdatePatrol"/>, which returns immediately
        /// on <c>_patrol == null</c> — and <c>_patrol</c> is ALWAYS null, because
        /// <see cref="SummaRace.Constants.GameRules.RacePatrolEnabled"/> is false and SpawnPatrol
        /// returns before creating him. So the vignette never appeared once, and neither did the
        /// _menaceTimer countdown that drives it. Two things followed from that:
        ///
        ///   * The comment on RacePatrolEnabled justifies cutting the cop with "the wrong-answer
        ///     beat is already carried by the amber vignette and the feedback line" — and half of
        ///     that was untrue the moment the cop went off. The only surviving wrong-answer signal
        ///     was the feedback pill and the 2.2s answer reveal.
        ///   * The "disable it when invisible" optimisation below was itself unreachable, so a
        ///     full-screen alpha-blended Image sat enabled at alpha 0 for the whole ~90s race —
        ///     a full screen of overdraw every frame, on the tile-based GPU of the 2GB floor
        ///     device that has to hold 30fps.
        ///
        /// Kept out of UpdatePatrol deliberately: this is learner feedback and must not depend on
        /// a chaser that is currently switched off and may stay off.
        /// </summary>
        private void UpdateDangerVignette(bool surging)
        {
            if (_vignette == null) return;
            var vc = _vignette.color;
            vc.a = Mathf.Lerp(vc.a, surging ? 0.35f : 0f, 6f * Time.deltaTime);
            _vignette.color = vc;
            // A full-screen alpha-blended Image still costs a full screen of overdraw at alpha 0,
            // and most frames of a clean run have no surge at all.
            bool visible = vc.a > 0.004f;
            if (_vignette.enabled != visible) _vignette.enabled = visible;
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
            var cardImg = MakeRacePanel(card);
            var crt = cardImg.rectTransform;
            crt.anchorMin = new Vector2(0.06f, 0.34f);
            crt.anchorMax = new Vector2(0.94f, 0.84f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;

            // Gold banner pill overlapping the card's top edge — the game's title language
            // (StorySelect banner, race HUD, old briefing all speak it).
            // The pill is wood too, not the kit's baked-yellow gold sprite. goldPillSprite is
            // left serialized and unused HERE rather than unwired, because the same field still
            // carries the gold language if this call is ever reverted.
            var titlePill = new GameObject("TitlePill");
            titlePill.transform.SetParent(card.transform, false);
            var tpImg = titlePill.AddComponent<UnityEngine.UI.Image>();
            tpImg.sprite = WoodPlaqueSprite();
            tpImg.type = UnityEngine.UI.Image.Type.Sliced;
            tpImg.color = Theme.WoodLight;
            var tpRt = tpImg.rectTransform;
            tpRt.anchorMin = new Vector2(0.09f, 0.915f);
            tpRt.anchorMax = new Vector2(0.91f, 1.045f);
            tpRt.offsetMin = Vector2.zero; tpRt.offsetMax = Vector2.zero;

            var title = MakeHudText(titlePill.transform, new Vector2(0.5f, 0.5f), Vector2.zero, 74f);
            title.text = SummaRace.Constants.GameText.RaceBriefingTitle;
            // Cream on wood, 11.9:1 - the inverse of the old dark-brown-on-yellow, and the same
            // pairing the tracker and the gate chip already use.
            title.color = new Color(1f, 0.96f, 0.88f);
            title.fontStyle = FontStyles.Bold;

            var body = MakeHudText(card.transform, new Vector2(0.5f, 0.63f), Vector2.zero, 54f);
            body.text = SummaRace.Constants.GameText.RaceBriefingBody(_story.title);
            // Only when the cameo is actually on, so the briefing can never promise a character
            // the kill-switch has removed.
            if (SummaRace.Constants.GameRules.RacePatrolCameoEnabled)
                body.text += System.Environment.NewLine + System.Environment.NewLine
                           + SummaRace.Constants.GameText.RaceBriefingPatrol;
            body.color = Theme.TextBrown;

            // ---- SIZED AS A FRACTION OF THE CARD, NOT AS A FIXED 760x380 BOX ----------------
            //
            // "The font is too small" (owner device playtest, 2026-08-21) and the screenshot
            // shows why: a nine-line body autosizing inside a 380px box, sitting in a card with
            // visible empty space above and below it. The copy had grown (it names the reading
            // panel, the three lanes and the patrol) while the box did not, so every extra line
            // came out of the point size.
            //
            // The old box could not simply be made taller, and the previous comment here is the
            // reason: it carried a four-row table proving that 380px at anchor 0.63 clears the
            // chip row and the title pill "at every aspect the canvas can take" - with just
            // 15.6px and 15.2px of margin at 4:3. Any fixed growth breaks the squarest tablet
            // while looking fine on the owner's phone, which is the exact shape of bug F56
            // spent a section on.
            //
            // So the box is anchored to the CARD instead, between its two neighbours' own
            // fractional edges. Then it cannot overlap at any aspect by construction rather than
            // by arithmetic, and it takes all the space that is actually there:
            //
            //   neighbour        its card fraction        this box
            //   chips row top    0.29 (0.20 +/- 65px)     bottom 0.31
            //   title pill base  0.915                    top    0.90
            //
            //   aspect   card H   old 380px box   new 0.59 box   9 lines at
            //   4:3        720        380            425          ~39pt  (was ~35)
            //   16:10      864        380            510          ~47pt
            //   9:16       960        380            566          54pt   (hits the ceiling)
            //   20:9      1200        380            708          54pt
            //
            // The floor rises with it: 32 -> 34, still low enough that the longest of the thirty
            // titles ("Baba Yaga, the Girl, and the Hedgehog") costs a little size rather than
            // the last line, which is the job the floor was given in the first place.
            var bodyRt = body.rectTransform;
            bodyRt.anchorMin = new Vector2(0.06f, 0.31f);
            bodyRt.anchorMax = new Vector2(0.94f, 0.90f);
            bodyRt.pivot = new Vector2(0.5f, 0.5f);
            bodyRt.offsetMin = Vector2.zero; bodyRt.offsetMax = Vector2.zero;
            body.enableAutoSizing = true;
            body.fontSizeMin = 34f;
            body.fontSizeMax = 54f;

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
                bubbleText.color = Theme.TextBrown;
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
            // Its own queued clip, on the same switch as the printed line above.
            if (SummaRace.Constants.GameRules.RacePatrolCameoEnabled &&
                SummaRace.Core.AudioManager.Instance != null)
                SummaRace.Core.AudioManager.Instance.PlayVoice(
                    SummaRace.Constants.AudioKeys.VoRaceBriefingPatrol, true);
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

        /// <summary>
        /// One of the five SWBST chips on the briefing card.
        ///
        /// THESE ARE THE "different color background swbst" THE OWNER OBJECTED TO. His note and
        /// its two "Too small" callouts are all anchored over the briefing screenshot, and at
        /// the bottom of that card sat five fully saturated blocks - blue, green, red, orange,
        /// purple - which is the single busiest thing on the learner's first frame of the race.
        ///
        /// They now match the in-race tracker exactly: one uniform dark wood plaque, the
        /// element's colour carried by an accent strip along its foot, the letter in white on
        /// top. That matters beyond taste - these chips exist so the run's gates are already
        /// familiar when the first one arrives, so a chip that does not look like a tracker slot
        /// is teaching the wrong thing. The palette is untouched (F18); it stops being the
        /// background, here and there, in the same way.
        /// </summary>
        private void MakeChip(RectTransform row, int index, string type)
        {
            var chip = new GameObject("Chip_" + index);
            chip.transform.SetParent(row, false);
            var img = chip.AddComponent<UnityEngine.UI.Image>();
            img.sprite = WoodPlaqueSprite();
            img.type = UnityEngine.UI.Image.Type.Sliced;
            // CREAM, not near-black. These were five dark plaques with a thin colour strip, and
            // on the device they read as five holes in the mission card — the over-correction
            // the owner flagged on 2026-08-22. Cream plaque + the element's ink letter is the
            // tracker's COLLECTED look, which the same screenshots show working, and it keeps
            // the palette doing real teaching work instead of being reduced to a hairline.
            img.color = Theme.Cream;
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(index * 0.2f + 0.02f, 0f);
            rt.anchorMax = new Vector2((index + 1) * 0.2f - 0.02f, 1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var accGo = new GameObject("Accent");
            accGo.transform.SetParent(chip.transform, false);
            var acc = accGo.AddComponent<UnityEngine.UI.Image>();
            acc.sprite = WoodPlaqueSprite();
            acc.type = UnityEngine.UI.Image.Type.Sliced;
            acc.color = SummaRace.Constants.SwbstPalette.ForIndex(index);
            acc.raycastTarget = false;
            var art = acc.rectTransform;
            art.anchorMin = new Vector2(0f, 0f); art.anchorMax = new Vector2(1f, 0f);
            art.pivot = new Vector2(0.5f, 0f);
            art.offsetMin = new Vector2(12f, 9f); art.offsetMax = new Vector2(-12f, 0f);
            art.sizeDelta = new Vector2(art.sizeDelta.x, 14f);

            var letter = MakeHudText(chip.transform, new Vector2(0.5f, 0.5f), Vector2.zero, 58f);
            letter.text = string.IsNullOrEmpty(type) ? "?" : type.Substring(0, 1);
            letter.color = SummaRace.Constants.SwbstPalette.InkForIndex(index);   // 5.2-7.4:1 on cream
            letter.fontStyle = FontStyles.Bold;
            letter.rectTransform.sizeDelta = new Vector2(150f, 140f);
            // Lifted clear of the accent strip AND of the word beneath it.
            letter.rectTransform.anchoredPosition = new Vector2(0f, 22f);

            // THE WORD ITSELF, under its initial. The briefing is the one place in the race with
            // no reading-time pressure - the world is held until the learner taps START - so it
            // is the cheapest place to teach the mapping the in-race tracker then uses as
            // shorthand. A chip showing only "S" is a mnemonic for something never stated.
            //
            // Autosized with NoWrap rather than clipped: SOMEBODY is the long one and it must
            // SHRINK to fit, never ellipsise into "SOMEB..." - the exact defect F47 had to fix
            // on the tracker, and there is no reason to reintroduce it here.
            if (!string.IsNullOrEmpty(type))
            {
                var word = MakeHudText(chip.transform, new Vector2(0.5f, 0f), new Vector2(0f, 32f), 20f);
                word.text = type;
                word.color = SummaRace.Constants.SwbstPalette.InkForIndex(index);
                word.fontStyle = FontStyles.Bold;
                word.enableAutoSizing = true;
                word.fontSizeMin = 10f;
                word.fontSizeMax = 20f;
                word.textWrappingMode = TextWrappingModes.NoWrap;
                word.rectTransform.sizeDelta = new Vector2(150f, 34f);
            }

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
            if (firstRelease)
            {
                RestartTheirMusic();
                // Final word on the world's light and fog. Their TrackManager.Begin() writes
                // RenderSettings.fogColor from the THEME, not from the world recipe, and the only
                // thing that undid it was the throttled re-assert inside `if (!_runReleased)` —
                // i.e. correctness depended on that poll landing after their write and before the
                // countdown ended. It currently does, but it is an undefended ordering dependency
                // on a path that has been reordered repeatedly, and the failure mode is every
                // world's fog silently collapsing to one of two theme colours: the bleaching that
                // had to be fixed once already. Re-asserting once here makes it not a race.
                if (_story != null)
                    SummaRace.Features.Race.RaceWorlds.Apply(_story.world, _story.difficulty);
            }
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
            // Handed to the patrol dolly, which slides back from exactly this pose during a
            // wrong-answer beat and returns to it. Captured here rather than re-read later
            // because after this point the camera is only ever a blend of two known poses.
            if (cam != null) { _camChaseLocalPos = gpPos; _camChaseCaptured = true; }

            // FIRST-RACE STEERING COACH. Built here rather than in BuildHud so it costs nothing
            // on the 29 races out of 30 that never show it, and shown over the countdown because
            // that is the only moment in the race with no reading to interrupt and no world
            // moving underneath - the learner can watch the hand and then use it two seconds
            // later on the real thing.
            BuildRaceCoach(hud);

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
            HideRaceCoach();
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

        /// <summary>
        /// The first-race steering coach: three ghosted lane zones and a hand that moves across
        /// them, with one line of text. Shown ONCE per device, over the 3-2-1.
        ///
        /// FOUR THINGS ARE DELIBERATE:
        ///
        ///  1. <b>It cannot swallow a tap.</b> Every graphic here is raycastTarget = false, and
        ///     the touch handler reads screen thirds directly rather than through the
        ///     EventSystem, so the coach is incapable of breaking the control it is teaching.
        ///     That matters more than it sounds: the pause chip has already had to be moved once
        ///     for overlapping a steering surface.
        ///  2. <b>It plays while the world is held.</b> The countdown is the only moment with
        ///     nothing to read and no road moving, so the coach costs zero reading seconds. It
        ///     is gone on GO!.
        ///  3. <b>It shows rather than tells.</b> The briefing already carries the sentence; a
        ///     nine-year-old learns a control from a hand that moves, not from a third line of
        ///     instructions read once under a START button.
        ///  4. <b>Once per device, not once per learner.</b> On a shared classroom tablet the
        ///     second child does not need it, and PlayerPrefs is written the moment it is shown
        ///     rather than when the race ends - so a run abandoned mid-countdown still counts as
        ///     "seen" and the coach can never become a thing that greets a child twice.
        ///
        /// Null-safe throughout: no hud, no coach, and the race is unchanged.
        /// </summary>
        private void BuildRaceCoach(Transform hud)
        {
            if (hud == null) return;
            if (PlayerPrefs.GetInt(SummaRace.Constants.PrefKeys.RaceCoachSeen, 0) != 0) return;
            PlayerPrefs.SetInt(SummaRace.Constants.PrefKeys.RaceCoachSeen, 1);
            PlayerPrefs.Save();   // F46(j): a toggle nobody saved is a toggle that did not happen

            var root = new GameObject("RaceCoach", typeof(RectTransform));
            root.transform.SetParent(hud, false);
            var rrt = (RectTransform)root.transform;
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;
            _coachRoot = root;

            // The three lane zones, drawn over the lower half where the road actually is, so the
            // learner associates "this third of the screen" with "that lane".
            const float bandBottom = 0.10f, bandTop = 0.52f;
            for (int i = 0; i < 3; i++)
            {
                var zone = new GameObject("Zone_" + i);
                zone.transform.SetParent(root.transform, false);
                var zimg = zone.AddComponent<UnityEngine.UI.Image>();
                zimg.sprite = WoodPlaqueSprite();
                zimg.type = UnityEngine.UI.Image.Type.Sliced;
                zimg.color = new Color(1f, 1f, 1f, 0.13f);
                zimg.raycastTarget = false;
                var zrt = zimg.rectTransform;
                zrt.anchorMin = new Vector2(i / 3f + 0.012f, bandBottom);
                zrt.anchorMax = new Vector2((i + 1) / 3f - 0.012f, bandTop);
                zrt.offsetMin = Vector2.zero; zrt.offsetMax = Vector2.zero;
            }

            // The hand. A generated soft disc rather than an icon: the kit has no hand sprite,
            // and a missing sprite here would leave the coach as three grey rectangles teaching
            // nothing. A pulsing marker that visibly VISITS each third carries the same meaning.
            var hand = new GameObject("Hand");
            hand.transform.SetParent(root.transform, false);
            var himg = hand.AddComponent<UnityEngine.UI.Image>();
            himg.sprite = MakeVignetteSprite();   // radial falloff = a soft glowing dot
            himg.color = new Color(1f, 0.92f, 0.55f, 0.95f);
            himg.raycastTarget = false;
            var hrt = himg.rectTransform;
            hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(0.5f, (bandBottom + bandTop) * 0.5f);
            hrt.sizeDelta = new Vector2(190f, 190f);

            var label = MakeHudText(root.transform, new Vector2(0.5f, bandTop + 0.055f),
                                    Vector2.zero, 44f);
            label.text = SummaRace.Constants.GameText.RaceCoachLine;
            label.color = new Color(1f, 0.97f, 0.90f);
            label.fontStyle = FontStyles.Bold;
            label.rectTransform.sizeDelta = new Vector2(900f, 120f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 28f;
            label.fontSizeMax = 44f;

            StartCoroutine(CoachHandRoutine(hrt));
        }

        /// <summary>Walks the coach hand centre -> left -> centre -> right -> centre, tapping as
        /// it lands. Unscaled time is not needed (the countdown runs at timeScale 1), but the
        /// loop is guarded on the root still existing so GO! can cut it at any point.</summary>
        private IEnumerator CoachHandRoutine(RectTransform hand)
        {
            float[] stops = { 0f, -0.30f, 0f, 0.30f, 0f };   // fraction of screen width
            int i = 0;
            while (_coachRoot != null && hand != null)
            {
                float from = stops[i % stops.Length];
                float to = stops[(i + 1) % stops.Length];
                i++;

                float t = 0f;
                const float travel = 0.34f;
                while (t < travel && _coachRoot != null && hand != null)
                {
                    t += Time.deltaTime;
                    float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / travel));
                    float x = Mathf.Lerp(from, to, e);
                    hand.anchorMin = hand.anchorMax = hand.pivot =
                        new Vector2(0.5f + x, hand.anchorMin.y);
                    yield return null;
                }
                if (_coachRoot == null || hand == null) yield break;

                // the "tap": a quick squash on arrival, so the gesture reads as a press
                Tween.PunchScale(hand.transform, Vector3.one * 0.35f, 0.22f);
                yield return new WaitForSeconds(0.20f);
            }
        }

        private void HideRaceCoach()
        {
            if (_coachRoot == null) return;
            Destroy(_coachRoot);
            _coachRoot = null;
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

            // The surge clock and the vignette are ticked HERE, before UpdatePatrol, because the
            // cop is optional and the learner's wrong-answer feedback is not. Paused/leaving
            // frames already returned above, so a surge does not burn down behind the pause menu.
            _menaceSurging = _menaceTimer > 0f;
            if (_menaceSurging) _menaceTimer -= Time.deltaTime;
            UpdateDangerVignette(_menaceSurging);

            // After TrackManager.Update has moved the runner, so the cop is placed against
            // THIS frame's player position rather than last frame's.
            if (_runReleased && track != null && !_finished)
            {
                if (SummaRace.Constants.GameRules.RacePatrolCameoEnabled) UpdatePatrolCameo(track);
                else UpdatePatrol(track);
                // After the cop is placed, so the frame the camera renders is the frame he is in.
                if (SummaRace.Constants.GameRules.RacePatrolCameoEnabled) UpdateChaseCameraDolly();
            }

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
