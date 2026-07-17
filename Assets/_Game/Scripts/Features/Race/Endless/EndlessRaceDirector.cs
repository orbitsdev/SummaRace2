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
        private const float CardY = 1.15f;

        private SummaRace.Data.StoryData _story;
        private float _spawnedDistance;      // cumulative worldLength of spawned segments
        private float _nextTargetDistance;
        private int _gatesPlaced;            // 0..6 (index 5 = finish gate)
        private bool _subscribed;
        private bool _finished;
        private float _runStartTime = -1f;

        private int _currentElement;
        private readonly bool[] _firstPickDone = new bool[5];
        private readonly bool[] _firstPickCorrect = new bool[5];
        private readonly Transform[] _gateRoots = new Transform[6];
        private readonly float[] _gateDistances = new float[6];
        private readonly Transform[] _correctCards = new Transform[5];

        private TextMeshProUGUI _bannerText;
        private TextMeshProUGUI _feedbackText;
        private float _feedbackTimer;

        private void Awake()
        {
            Instance = this;
            EndlessRaceMode.Active = true;
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

            while (TrackManager.instance == null) yield return null;
            TrackManager.instance.newSegmentCreated += OnNewSegment;
            _subscribed = true;

            // Jump their Loadout menu straight into the run.
            yield return new WaitForSeconds(0.75f); // let Loadout.Enter settle
            var loadout = FindFirstObjectByType<LoadoutState>();
            if (loadout != null && loadout.isActiveAndEnabled) loadout.StartGame();

            yield return new WaitForSeconds(0.5f);
            HideTheirCurrencyHud();
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
                if (_gateRoots[_currentElement] != null)
                    Destroy(_gateRoots[_currentElement].gameObject);
                _currentElement++;
                UpdateBanner();
            }
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
            cardT.SetParent(null, true);
            var col = pickup.GetComponent<Collider>();
            if (col != null) col.enabled = false;
            Tween.PositionY(cardT, cardT.position.y + 2.2f, 0.45f, Ease.OutQuad);
            Tween.Scale(cardT, Vector3.zero, 0.5f, Ease.InBack)
                .OnComplete(() => { if (cardT != null) Destroy(cardT.gameObject); });

            if (_gateRoots[index] != null) Destroy(_gateRoots[index].gameObject);
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

        /// <summary>Their coin/score counters mean nothing here — hide just those texts.</summary>
        private void HideTheirCurrencyHud()
        {
            var gs = FindFirstObjectByType<GameState>();
            if (gs == null) return;
            if (gs.coinText != null) gs.coinText.gameObject.SetActive(false);
            if (gs.premiumText != null) gs.premiumText.gameObject.SetActive(false);
            if (gs.scoreText != null) gs.scoreText.gameObject.SetActive(false);
            if (gs.distanceText != null) gs.distanceText.gameObject.SetActive(false);
            if (gs.multiplierText != null) gs.multiplierText.gameObject.SetActive(false);
        }
    }
}
