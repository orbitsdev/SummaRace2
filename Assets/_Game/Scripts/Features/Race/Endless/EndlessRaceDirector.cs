using System.Collections;
using PrimeTween;
using TMPro;
using UnityEngine;

namespace SummaRace.Features.Race.Endless
{
    /// <summary>
    /// SummaRace's SWBST collection layered onto the Trash Dash endless runner.
    /// Exists only in MainSummaRace.unity. While alive it sets EndlessRaceMode.Active
    /// (suppressing their coins/premium/powerups), skips their Loadout/FTUE, places
    /// 5 answer gates + a FINISH gate along the generated track, resolves picks,
    /// then builds a RaceResult and exits to Arrange. Their scripts are untouched
    /// beyond the one TrackManager guard.
    /// NOTE: their GameManager collides with ours by name — everything of ours is
    /// fully qualified (SummaRace.Core.*), do not add `using SummaRace.Core;`.
    /// </summary>
    public class EndlessRaceDirector : MonoBehaviour
    {
        public static EndlessRaceDirector Instance { get; private set; }

        [Header("Visuals (null = grey-box fallback)")]
        [SerializeField] private Sprite worldCardSprite;       // Hyper_Casual_UI rounded rect
        [SerializeField] private TMP_FontAsset worldLabelFont; // Fredoka-SemiBold SDF

        private const float FirstGateDistance = 80f; // clear of their starting safe segments
        private const float FinishGap = 30f;         // FINISH this far after the 5th gate
        private const float MissGrace = 5f;          // metres past a gate before it counts as missed
        private const float CardY = 0.5f;

        private SummaRace.Data.StoryData _story;
        private float _spawnedDistance;      // cumulative worldLength of spawned segments
        private float _nextTargetDistance;
        private int _gatesPlaced;            // 0..6 (index 5 = finish gate)
        private bool _subscribed;
        private bool _finished;
        private float _runStartTime = -1f;
        private float _lastWorldDistance;

        private int _currentElement;
        private readonly bool[] _firstPickDone = new bool[5];
        private readonly bool[] _firstPickCorrect = new bool[5];
        private readonly Transform[] _gateRoots = new Transform[6];
        private readonly float[] _gateDistances = new float[6];
        private readonly Transform[] _correctCards = new Transform[5];

        private TextMeshProUGUI _bannerText;
        private TextMeshProUGUI _feedbackText;
        private float _feedbackTimer;
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

            _nextTargetDistance = FirstGateDistance;
            BuildHud();
            UpdateBanner();

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

            yield return new WaitForSeconds(0.5f);
            HideTheirChrome();
            EnsureSingleAudioListener();
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

            // Their death popup's Run Again (or Loadout->RUN!) rebuilds the track from zero
            // and destroys every gate with its segments — reload the race so the learner
            // never runs a gate-less road (never a dead end).
            if (track.worldDistance < _lastWorldDistance - 1f)
            {
                _finished = true; // block double-triggering while the load happens
                if (SummaRace.Core.SceneLoader.Instance != null)
                    SummaRace.Core.SceneLoader.Instance.Load(SummaRace.Constants.SceneNames.RaceEndless);
                else
                    UnityEngine.SceneManagement.SceneManager.LoadScene(SummaRace.Constants.SceneNames.RaceEndless);
                return;
            }
            _lastWorldDistance = track.worldDistance;

            if (_runStartTime < 0f && track.isMoving) _runStartTime = Time.time;

            // A gate the player ran past without a correct pick resolves as missed.
            if (_currentElement < 5 && _currentElement < _gatesPlaced &&
                track.worldDistance > _gateDistances[_currentElement] + MissGrace)
            {
                if (!_firstPickDone[_currentElement])
                {
                    _firstPickDone[_currentElement] = true;
                    _firstPickCorrect[_currentElement] = false;
                }
                if (_correctCards[_currentElement] != null)
                    Tween.StopAll(_correctCards[_currentElement]);
                if (_gateRoots[_currentElement] != null)
                    Destroy(_gateRoots[_currentElement].gameObject);
                _currentElement++;
                UpdateBanner();
            }

            // Their Resume() unconditionally re-shows the pause button after a
            // focus-loss pause cycle — keep it hidden.
            if (_gameState != null && _gameState.pauseButton != null &&
                _gameState.pauseButton.gameObject.activeSelf)
                _gameState.pauseButton.gameObject.SetActive(false);
        }

        // ---------- gate spawning ----------

        private void OnNewSegment(TrackSegment segment)
        {
            float segStart = _spawnedDistance;
            float segEnd = segStart + segment.worldLength;
            _spawnedDistance = segEnd;

            while (_gatesPlaced < 6 &&
                   _nextTargetDistance >= segStart && _nextTargetDistance < segEnd)
            {
                float local = _nextTargetDistance - segStart;
                if (_gatesPlaced < 5) PlaceAnswerGate(segment, local, _gatesPlaced);
                else PlaceFinishGate(segment, local);

                _gateDistances[_gatesPlaced] = _nextTargetDistance;
                _gatesPlaced++;
                _nextTargetDistance += _gatesPlaced < 5
                    ? Mathf.Max(25f, _story.mission.checkpointSpacing)
                    : FinishGap;
            }
        }

        private void PlaceAnswerGate(TrackSegment segment, float localDist, int elementIndex)
        {
            Vector3 pos; Quaternion rot;
            segment.GetPointAtInWorldUnit(localDist, out pos, out rot);

            var root = new GameObject("SwbstGate_" + elementIndex).transform;
            root.SetParent(segment.transform, true); // dies with the segment on recycle
            root.SetPositionAndRotation(pos, rot);
            root.gameObject.AddComponent<EndlessCurveDip>();
            _gateRoots[elementIndex] = root;

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
                if (isCorrect) _correctCards[elementIndex] = card;
            }

            // SWBST pill above the middle card, palette-colored.
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
            _gateRoots[5] = root;

            float laneOffset = TrackManager.instance.laneOffset;
            var card = BuildCard(root, new Vector3(0f, 1.6f, 0f), new Vector2(3.4f, 0.9f),
                "FINISH", Color.white, new Color(1f, 0.72f, 0.15f), 3.2f);

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
            if (pickup.elementIndex != _currentElement) return; // stray hit on a future gate

            // Only the FIRST pick at each gate counts for stars (GDD §4.2).
            if (!_firstPickDone[_currentElement])
            {
                _firstPickDone[_currentElement] = true;
                _firstPickCorrect[_currentElement] = pickup.isCorrect;
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
            if (SummaRace.Core.AudioManager.Instance != null)
                SummaRace.Core.AudioManager.Instance.PlaySfx(SummaRace.Constants.AudioKeys.SfxCollect);

            ShowFeedback("You got it!", new Color(0.55f, 1f, 0.55f));

            // The collected card flies up and pops away; the rest of the gate goes now.
            var cardT = pickup.transform;
            int index = pickup.elementIndex;
            var root = _gateRoots[index];
            // Keep the flying card parented to the segment: a floating-origin recenter
            // (~every 100m) would teleport a world-space orphan mid-celebration.
            cardT.SetParent(root != null ? root.parent : null, true);
            var col = pickup.GetComponent<Collider>();
            if (col != null) col.enabled = false;
            Tween.PositionY(cardT, cardT.position.y + 2.2f, 0.45f, Ease.OutQuad);
            Tween.Scale(cardT, Vector3.zero, 0.5f, Ease.InBack)
                .OnComplete(() => { if (cardT != null) Destroy(cardT.gameObject); });

            if (root != null) Destroy(root.gameObject);
            _gateRoots[index] = null;

            _currentElement++;
            UpdateBanner();
        }

        private void HitWrong(EndlessOptionPickup pickup)
        {
            if (SummaRace.Core.AudioManager.Instance != null)
                SummaRace.Core.AudioManager.Instance.PlaySfx(SummaRace.Constants.AudioKeys.SfxNotQuite);

            ShowFeedback("Not quite — the glowing one!", new Color(1f, 0.78f, 0.35f));

            var correct = _correctCards[pickup.elementIndex];
            Destroy(pickup.gameObject);

            // Teaching moment: the correct card glows gold as it passes.
            if (correct != null)
            {
                correct.localScale = Vector3.one * 1.3f;
                var sr = correct.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) sr.color = new Color(1f, 0.85f, 0.35f);
                Tween.PunchScale(correct, Vector3.one * 0.18f, 0.4f);
            }
            // The miss-grace check in Update() advances the gate a few metres later.
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
                timesCaught = 0, // no patrol in the endless experiment
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
            if (_bannerText != null) _bannerText.text = "FINISH!";

            yield return new WaitForSeconds(2.2f); // victory beat

            if (SummaRace.Core.SceneLoader.Instance != null)
                SummaRace.Core.SceneLoader.Instance.Load(SummaRace.Constants.SceneNames.Arrange);
            else // editor-direct play fallback (TDD §13)
                UnityEngine.SceneManagement.SceneManager.LoadScene(SummaRace.Constants.SceneNames.Arrange);
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

            _bannerText = MakeHudText(canvasGo.transform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), 64f);
            _feedbackText = MakeHudText(canvasGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), 56f);
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
            _bannerText.text = _currentElement < 5
                ? "Collect: " + _story.elements[_currentElement].type + "  " + (_currentElement + 1) + "/5"
                : "Run to the FINISH!";
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
