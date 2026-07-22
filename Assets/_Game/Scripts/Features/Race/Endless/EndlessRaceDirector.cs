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

        // Warm gold the collect sparkle is retinted to, matching the story-treasure look.
        private static readonly Color StoryGold = new Color(1f, 0.85f, 0.45f);
        private UnityEngine.UI.Image _vignette; // amber screen-edge danger vignette (TDD §11.5)

        private const float FirstGateDistance = 80f; // clear of their starting safe segments
        private const float FinishGap = 30f;         // FINISH this far after the 5th gate
        private const float MissGrace = 5f;          // metres past a gate before it counts as missed
        private const float RepresentGap = 18f;      // metres ahead for a re-presented gold card
        private const int MaxRepresentMisses = 3;    // consecutive dodges before anti-frustration auto-resolve
        private const float CardY = 0.5f;

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

        // One active gate at a time.
        private Transform _activeGateRoot;
        private int _activeElement = -1;
        private bool _activeIsRepresent;
        private float _activeGateDistance;
        private int _representCount; // consecutive dodged re-presents of the current element

        private readonly bool[] _firstPickDone = new bool[5];
        private readonly bool[] _firstPickCorrect = new bool[5];

        private float _slowTimer;
        private float _savedMaxSpeed = -1f;

        // TDD §11.4 danger meter. It now drives the patrol: danger climbs on its own
        // (story mission.dangerPerSecond), jumps on a wrong pick and drops on a correct
        // one, and the patrol's distance is read straight off it. It still never ends
        // the run — the chaser is pressure the learner can SEE, never a fail state
        // (GDD D7, and RaceResult.timesCaught stays 0).
        private float _danger;
        private Transform _patrol;
        private Animator _patrolAnim;
        private float _menaceTimer;

        private TextMeshProUGUI _bannerText;
        private TextMeshProUGUI _feedbackText;
        private float _feedbackTimer;

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
            if (Instance == this) Instance = null;
            if (_subscribed && TrackManager.instance != null)
                TrackManager.instance.newSegmentCreated -= OnNewSegment;
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

            _pendingGateDistance = FirstGateDistance;
            _pendingElement = 0;
            _pendingIsRepresent = false;
            BuildHud();
            UpdateBanner();
            // Up before their Loadout/track boot so the learner never sees the spin-up:
            // the scrim doubles as the mask for it (the old MaskLoadoutFlash only hides
            // their canvas children, not the frames where the track is assembling).
            BuildBriefing();

            // Skip their FTUE/tutorial run.
            while (PlayerData.instance == null) yield return null;
            PlayerData.instance.tutorialDone = true;
            if (PlayerData.instance.ftueLevel < 2) PlayerData.instance.ftueLevel = 2;

            // Jump their Loadout menu straight into the run. Their TrackManager GameObject
            // stays inactive until GameState.Enter -> StartGame -> Begin() activates it,
            // so the instance wait MUST come after this call (waiting first deadlocks).
            yield return new WaitForSeconds(0.75f); // let Loadout.Enter settle
            var loadout = FindAnyObjectByType<LoadoutState>();
            if (loadout != null && loadout.isActiveAndEnabled) loadout.StartGame();

            // Safe to subscribe here: instance is set by Awake on activation, and the first
            // newSegmentCreated fires at least a frame later (Addressables instantiation).
            while (TrackManager.instance == null) yield return null;
            TrackManager.instance.newSegmentCreated += OnNewSegment;
            _subscribed = true;
            SpawnPatrol();
            // PC: WASD alongside the arrow keys their controller already binds.
            if (GetComponent<EndlessKeyboardInput>() == null)
                gameObject.AddComponent<EndlessKeyboardInput>();

            yield return new WaitForSeconds(0.5f);
            HideTheirChrome();
            EnsureSingleAudioListener();
            MarkBriefingReady(); // only now is it safe to take the scrim away
        }

        private void Update()
        {
            if (_feedbackTimer > 0f && _feedbackText != null)
            {
                _feedbackTimer -= Time.deltaTime;
                if (_feedbackTimer <= 0f) _feedbackText.text = "";
            }

            var track = TrackManager.instance;
            if (track == null || _finished) return;

            // Hold the world at the start line until the countdown finishes. Re-asserted
            // every frame rather than stopped once, because their StartGame -> WaitToStart
            // coroutine sets isMoving on its own schedule after we would have stopped it.
            if (!_runReleased)
            {
                if (track.isMoving) track.StopMove();
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

            // Baseline pressure: danger climbs on its own so the patrol is always
            // creeping in, and collecting correctly is what pushes it back.
            if (!_finished)
                _danger = Mathf.Clamp(_danger + _story.mission.dangerPerSecond * Time.deltaTime,
                    0f, SummaRace.Constants.GameRules.DangerMax);
            UpdatePatrol(track);

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
            if (_activeGateRoot != null && _activeElement < 5 &&
                track.worldDistance > _activeGateDistance + MissGrace)
            {
                HandleMissedActiveGate(track);
            }

            TryPlacePending();

            // Their Resume() unconditionally re-shows the pause button after a
            // focus-loss pause cycle — keep it hidden.
            if (_gameState != null && _gameState.pauseButton != null &&
                _gameState.pauseButton.gameObject.activeSelf)
                _gameState.pauseButton.gameObject.SetActive(false);
        }

        // ---------- gate spawning / scheduling ----------

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
            _pendingGateDistance = -1f;

            float local = placeDist - placeStart;
            if (element >= 5) PlaceFinishGate(placeSeg, local);
            else if (isRepresent) PlaceRepresentGate(placeSeg, local, element);
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
            var element = _story.elements[elementIndex];

            // Varied but deterministic correct-lane placement: 1,0,2,1,0.
            int correctLane = (elementIndex * 2 + 1) % 3;

            int d = 0;
            for (int lane = 0; lane < 3; lane++)
            {
                bool isCorrect = lane == correctLane;
                string text = isCorrect ? element.correct : element.distractors[d++];
                var card = BuildCard(root, new Vector3((lane - 1) * laneOffset, CardY, 0f),
                    new Vector2(cardWidth, 0.85f), text, Color.black, Color.white, 2.4f);

                var trigger = card.gameObject.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(cardWidth, 2.2f, 0.5f);
                // Card sits on the road now — lift the catch volume over the character's body.
                trigger.center = new Vector3(0f, 0.6f, 0f);

                var pickup = card.gameObject.AddComponent<EndlessOptionPickup>();
                pickup.elementIndex = elementIndex;
                pickup.isCorrect = isCorrect;
            }

            // SWBST pill above the middle card, palette-colored.
            BuildCard(root, new Vector3(0f, CardY + 1.55f, 0f), new Vector2(3.0f, 0.55f),
                element.type, Color.white,
                SummaRace.Constants.SwbstPalette.DeepForIndex(elementIndex), 2.6f);
        }

        /// <summary>TDD §11.4 re-presentation: ONE gold glowing card, center lane, standing in
        /// for the whole gate after a wrong pick or a miss. Collecting it resolves the element
        /// with the normal celebration but no boost and no first-pick change.</summary>
        private void PlaceRepresentGate(TrackSegment segment, float localDist, int elementIndex)
        {
            Vector3 pos; Quaternion rot;
            segment.GetPointAtInWorldUnit(localDist, out pos, out rot);

            var root = new GameObject("SwbstRepresent_" + elementIndex).transform;
            root.SetParent(segment.transform, true);
            root.SetPositionAndRotation(pos, rot);
            root.gameObject.AddComponent<EndlessCurveDip>();
            _activeGateRoot = root;

            float laneOffset = TrackManager.instance.laneOffset;
            float cardWidth = Mathf.Min(1.55f, laneOffset * 0.95f);
            var element = _story.elements[elementIndex];
            var goldColor = new Color(1f, 0.85f, 0.35f);

            var card = BuildCard(root, new Vector3(0f, CardY, 0f), new Vector2(cardWidth, 0.85f),
                element.correct, Color.black, goldColor, 2.4f);

            var trigger = card.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(cardWidth, 2.2f, 0.5f);
            trigger.center = new Vector3(0f, 0.6f, 0f);

            var pickup = card.gameObject.AddComponent<EndlessOptionPickup>();
            pickup.elementIndex = elementIndex;
            pickup.isCorrect = true;

            // SWBST pill above the gold card, same as a normal gate.
            BuildCard(root, new Vector3(0f, CardY + 1.55f, 0f), new Vector2(3.0f, 0.55f),
                element.type, Color.white,
                SummaRace.Constants.SwbstPalette.DeepForIndex(elementIndex), 2.6f);
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
            var card = BuildCard(root, new Vector3(0f, 1.6f, 0f), new Vector2(3.4f, 0.9f),
                SummaRace.Constants.GameText.RaceFinishCard, Color.white, new Color(1f, 0.72f, 0.15f), 3.2f);

            var trigger = card.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(laneOffset * 3f + 1f, 3.2f, 0.6f);
            trigger.center = new Vector3(0f, -0.4f, 0f);

            card.gameObject.AddComponent<EndlessOptionPickup>().isFinishGate = true;
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
            if (pickup.elementIndex != _activeElement) return; // stray hit on an inactive/destroyed gate

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

            SummaRace.Core.EventBus.Raise(new SummaRace.Core.ElementCollected
            {
                elementIndex = pickup.elementIndex,
                wasCorrect = pickup.isCorrect
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

            ShowFeedback(SummaRace.Core.Praise.ForRace(element), new Color(0.55f, 1f, 0.55f));

            // The collected card flies up and pops away; the rest of the gate goes now.
            var cardT = pickup.transform;
            var root = _activeGateRoot;
            // Keep the flying card parented to the segment: a floating-origin recenter
            // (~every 100m) would teleport a world-space orphan mid-celebration.
            cardT.SetParent(root != null ? root.parent : null, true);
            var col = pickup.GetComponent<Collider>();
            if (col != null) col.enabled = false;
            Tween.PositionY(cardT, cardT.position.y + 2.2f, 0.45f, Ease.OutQuad);
            Tween.Scale(cardT, Vector3.zero, 0.5f, Ease.InBack)
                .OnComplete(() => { if (cardT != null) Destroy(cardT.gameObject); });

            if (root != null) Destroy(root.gameObject);

            // TDD §11.4: a correct pick always relieves danger a little; the boost bundle
            // (sfx + speed) is reserved for a first-hit correct pick — collecting a
            // re-presented gold card still resolves the element, just without the extra reward.
            _danger = Mathf.Clamp(_danger - SummaRace.Constants.GameRules.DangerRelief,
                0f, SummaRace.Constants.GameRules.DangerMax);

            if (!wasRepresent && track != null)
            {
                if (SummaRace.Core.AudioManager.Instance != null)
                    SummaRace.Core.AudioManager.Instance.PlaySfx(SummaRace.Constants.AudioKeys.SfxBoost);
                BoostSpeed(track);
            }

            if (track != null) AdvanceToNext(track, element);
            else { _activeGateRoot = null; _activeElement = -1; _activeIsRepresent = false; }
        }

        private void HitWrong(EndlessOptionPickup pickup)
        {
            var track = TrackManager.instance;
            if (SummaRace.Core.AudioManager.Instance != null)
                SummaRace.Core.AudioManager.Instance.PlaySfx(SummaRace.Constants.AudioKeys.SfxNotQuite);

            ShowFeedback(SummaRace.Constants.GameText.RaceWrongFeedback, new Color(1f, 0.78f, 0.35f));

            _danger = Mathf.Clamp(_danger + SummaRace.Constants.GameRules.DangerOnWrong,
                0f, SummaRace.Constants.GameRules.DangerMax);
            // Surge the chaser into view for a beat — the visible half of "not quite".
            _menaceTimer = SummaRace.Constants.GameRules.PatrolMenaceSeconds;

            if (track != null)
            {
                if (_savedMaxSpeed < 0f) _savedMaxSpeed = track.maxSpeed;
                track.maxSpeed = Mathf.Max(track.minSpeed, track.speed * 0.6f);
                _slowTimer = SummaRace.Constants.GameRules.SlowSeconds;
            }

            int element = pickup.elementIndex;
            // The WHOLE gate (both distractors + the correct card) goes now — TDD §11.4:
            // the correct answer comes back on its own as a single glowing re-present.
            DestroyActiveGate();

            if (track != null) ScheduleRepresent(track, element);
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

            bool wasRepresent = _activeIsRepresent;
            DestroyActiveGate();

            if (wasRepresent)
            {
                _representCount++;
                if (_representCount >= MaxRepresentMisses)
                {
                    // Anti-frustration floor: the card is center-lane and glowing, so 3
                    // dodges means the learner is deliberately avoiding it. Not learner-facing.
                    Debug.Log("EndlessRaceDirector: element " + element +
                        " auto-resolved after " + MaxRepresentMisses + " dodged re-presents.");
                    AdvanceToNext(track, element);
                    return;
                }
            }

            ScheduleRepresent(track, element);
        }

        private void DestroyActiveGate()
        {
            if (_activeGateRoot != null) Destroy(_activeGateRoot.gameObject);
            _activeGateRoot = null;
        }

        /// <summary>Element fully resolved (correct first hit, re-present collected, or
        /// auto-resolved after 3 dodges) — clears the active slot and schedules the next
        /// answer gate, or FINISH once element 4 is done, through the same pending mechanism.</summary>
        private void AdvanceToNext(TrackManager track, int completedElement)
        {
            _representCount = 0;
            _activeGateRoot = null;
            _activeElement = -1;
            _activeIsRepresent = false;

            int next = completedElement + 1;
            if (next < 5)
            {
                _pendingElement = next;
                _pendingIsRepresent = false;
                _pendingGateDistance = track.worldDistance + Mathf.Max(25f, _story.mission.checkpointSpacing);
            }
            else
            {
                _pendingElement = 5;
                _pendingIsRepresent = false;
                _pendingGateDistance = track.worldDistance + FinishGap;
            }

            UpdateBanner();
            TryPlacePending();
        }

        private void ScheduleRepresent(TrackManager track, int element)
        {
            _pendingElement = element;
            _pendingIsRepresent = true;
            _pendingGateDistance = track.worldDistance + RepresentGap;
            UpdateBanner();
            TryPlacePending();
        }

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

            var track = TrackManager.instance;
            if (track != null) track.StopMove();

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

            // Amber danger vignette (TDD §11.5): darkest at the screen edges, clear in the
            // middle, alpha driven by danger in UpdatePatrol. Drawn behind the text.
            var vgo = new GameObject("DangerVignette");
            vgo.transform.SetParent(canvasGo.transform, false);
            _vignette = vgo.AddComponent<UnityEngine.UI.Image>();
            _vignette.sprite = MakeVignetteSprite();
            _vignette.raycastTarget = false;
            _vignette.color = new Color(1f, 0.42f, 0.05f, 0f); // amber, invisible until danger rises
            var vrt = _vignette.rectTransform;
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;

            _bannerText = MakeHudText(canvasGo.transform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), 64f);
            _feedbackText = MakeHudText(canvasGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), 56f);
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
            var runner = TrackManager.instance != null ? TrackManager.instance.characterController : null;
            if (runner == null) return;

            GameObject go;
            if (patrolPrefab != null)
            {
                go = Instantiate(patrolPrefab);
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

            // Start it far back and out of sight; UpdatePatrol eases it in from there.
            var p = runner.transform.position;
            _patrol.position = new Vector3(p.x, p.y, p.z - 30f);
            _patrol.rotation = Quaternion.identity; // faces down the road, same as the runner
            go.SetActive(_runReleased);
        }

        /// <summary>
        /// Distance is read off the danger meter. The visible band is derived from the
        /// LIVE camera rather than hard-coded: their camera sits a few metres behind the
        /// runner, so anything further back than that is off-screen. At max danger the
        /// patrol sits just in front of the camera and LOOMS; at zero danger it is well
        /// behind it and unseen. (The old park race hard-coded 1.8-12.8m for the same
        /// effect and had to be re-tuned when the camera moved — F12/F30.)
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
            }

            if (_menaceTimer > 0f) _menaceTimer -= Time.deltaTime;

            float t = _danger / SummaRace.Constants.GameRules.DangerMax;
            // A wrong pick makes it act near-max for a beat, so the learner actually SEES
            // the consequence instead of only feeling the 1.5s slow.
            if (_menaceTimer > 0f) t = Mathf.Max(t, SummaRace.Constants.GameRules.PatrolMenaceDanger);

            // Amber vignette intensifies with the same danger the patrol reads (TDD §11.5).
            if (_vignette != null)
            {
                var vc = _vignette.color;
                vc.a = Mathf.Lerp(vc.a, t * 0.35f, 6f * Time.deltaTime);
                _vignette.color = vc;
            }

            var playerPos = runner.transform.position;
            var cam = Camera.main;
            float camBack = cam != null
                ? Mathf.Max(1.5f, playerPos.z - cam.transform.position.z)
                : 3f; // sane default if the camera is mid-swap
            float near = Mathf.Max(1.2f, camBack - SummaRace.Constants.GameRules.PatrolCloseInFront);
            float far = camBack + SummaRace.Constants.GameRules.PatrolFarBehindCamera;

            float targetZ = playerPos.z - Mathf.Lerp(far, near, t);
            float nx = Mathf.Lerp(_patrol.position.x, playerPos.x,
                SummaRace.Constants.GameRules.PatrolFollowX * Time.deltaTime);
            float nz = Mathf.Lerp(_patrol.position.z, targetZ,
                SummaRace.Constants.GameRules.PatrolFollowZ * Time.deltaTime);
            _patrol.position = new Vector3(nx, playerPos.y, nz);
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
                // Clear of the START button, which is centred and 520 wide (x 0.26-0.74).
                trt.anchorMin = new Vector2(0.01f, 0.04f);
                trt.anchorMax = new Vector2(0.24f, 0.28f);
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
                bRt.anchorMin = new Vector2(0.03f, 0.29f);
                bRt.anchorMax = new Vector2(0.40f, 0.345f);
                bRt.offsetMin = Vector2.zero; bRt.offsetMax = Vector2.zero;
                var bubbleText = MakeHudText(bubble.transform, new Vector2(0.5f, 0.5f), Vector2.zero, 40f);
                bubbleText.text = SummaRace.Constants.GameText.RaceBriefingLumi;
                bubbleText.color = new Color(0.35f, 0.25f, 0.10f);
                bubbleText.fontStyle = FontStyles.Bold;
                bubbleText.rectTransform.sizeDelta = new Vector2(380f, 90f);
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
            if (Time.timeScale != 0f) return; // nothing was paused

            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (_runReleased && TrackManager.instance != null)
                TrackManager.instance.StartMove(false);
        }

        private void DismissBriefing()
        {
            if (_briefingDismissed || !_bootReady) return;
            _briefingDismissed = true;
            if (SummaRace.Core.AudioManager.Instance != null)
                SummaRace.Core.AudioManager.Instance.PlaySfx(SummaRace.Constants.AudioKeys.SfxClick);
            if (_briefingRoot != null) _briefingRoot.SetActive(false);
            StartCoroutine(CountdownRoutine());
        }

        /// <summary>3-2-1-GO! on the HUD banner, then the world is released.</summary>
        private IEnumerator CountdownRoutine()
        {
            var steps = SummaRace.Constants.GameText.RaceCountdown;
            var hud = transform.Find("SummaRaceHud");
            for (int i = 0; i < steps.Length; i++)
            {
                bool isGo = i == steps.Length - 1;
                if (_bannerText != null)
                {
                    _bannerText.text = steps[i];
                    Tween.PunchScale(_bannerText.transform, Vector3.one * 0.45f, 0.3f);
                }

                // Big center-screen number, runner-game style.
                if (hud != null)
                {
                    var big = MakeHudText(hud, new Vector2(0.5f, 0.55f), Vector2.zero, isGo ? 230f : 320f);
                    big.text = steps[i];
                    big.color = new Color(1f, 0.83f, 0.20f);
                    big.fontStyle = FontStyles.Bold;
                    big.rectTransform.sizeDelta = new Vector2(1000f, 420f);
                    big.transform.localScale = Vector3.one * 1.6f;
                    Tween.Scale(big.transform, Vector3.one, 0.25f, Ease.OutBack);
                    Destroy(big.gameObject, isGo ? 0.5f : 0.72f);
                }
                if (SummaRace.Core.AudioManager.Instance != null)
                    SummaRace.Core.AudioManager.Instance.PlaySfx(isGo
                        ? SummaRace.Constants.AudioKeys.SfxBoost
                        : SummaRace.Constants.AudioKeys.SfxPop);
                yield return new WaitForSeconds(isGo ? 0.45f : 0.7f);
            }

            _runReleased = true; // set before StartMove so Update() cannot re-stop it
            if (TrackManager.instance != null) TrackManager.instance.StartMove(false);
            UpdateBanner();
        }

        private TextMeshProUGUI MakeHudText(Transform parent, Vector2 anchor, Vector2 offset, float size)
        {
            var go = new GameObject("HudText");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (worldLabelFont != null) tmp.font = worldLabelFont;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            var rt = tmp.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(1000f, 120f);
            return tmp;
        }

        private void UpdateBanner()
        {
            if (_bannerText == null) return;
            int element = _activeGateRoot != null ? _activeElement : _pendingElement;
            _bannerText.text = element < 5
                ? SummaRace.Constants.GameText.RaceCollectBanner(_story.elements[element].type, element + 1, 5)
                : SummaRace.Constants.GameText.RaceRunToFinish;
        }

        private void ShowFeedback(string message, Color color)
        {
            if (_feedbackText == null) return;
            _feedbackText.text = message;
            _feedbackText.color = color;
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
