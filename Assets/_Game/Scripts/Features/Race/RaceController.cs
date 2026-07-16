using System.Collections;
using PrimeTween;
using SummaRace.Constants;
using SummaRace.Core;
using SummaRace.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SummaRace.Features.Race
{
    /// <summary>
    /// Orchestrates the run (TDD §11): briefing → scroll world → 5 SWBST
    /// checkpoints → danger/patrol pressure → finish → RaceResult.
    /// Grey-box: capsule player, cube patrol, cube pickups with 3D labels.
    /// The chase can never fail the learner — "caught" is a friendly reset.
    /// </summary>
    public class RaceController : MonoBehaviour
    {
        public static RaceController Instance { get; private set; }

        [Header("HUD")]
        [SerializeField] private GameObject bannerGroup;
        [SerializeField] private TMP_Text bannerText;
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField] private RectTransform dangerFill;
        [SerializeField] private Image vignette;
        [SerializeField] private GameObject briefingPanel;
        [SerializeField] private Button startMissionButton;
        [SerializeField] private GameObject caughtPanel;

        private enum State { Briefing, Running, Caught, Finished }

        private State _state = State.Briefing;
        private StoryData _story;
        private Transform _world;          // everything that scrolls
        private PlayerRunner _player;
        private Transform _patrol;
        private Transform[] _checkpoints;  // roots, index = element
        private Transform _correctPickup;  // glowing target after a wrong pick

        private int _currentElement;
        private float _danger;
        private float _speedMultiplier = 1f;
        private float _speedEffectTimer;
        private float _feedbackTimer;
        private float _runSeconds;
        private int _timesCaught;
        private readonly bool[] _firstPickCorrect = new bool[5];
        private readonly bool[] _firstPickDone = new bool[5];

        private void Awake() => Instance = this;

        private void Start()
        {
            _story = GameManager.Instance != null ? GameManager.Instance.CurrentStory : null;
            if (_story == null) _story = StoryLoader.Load("s01_easy"); // editor-direct fallback
            if (_story == null) { Debug.LogError("Race: no story."); return; }

            _danger = _story.mission.startingDanger;
            BuildWorld();

            if (briefingPanel != null) briefingPanel.SetActive(true);
            if (caughtPanel != null) caughtPanel.SetActive(false);
            if (bannerGroup != null) bannerGroup.SetActive(false); // shown when the run starts
            if (startMissionButton != null)
                startMissionButton.onClick.AddListener(StartMission);
        }

        private void StartMission()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);
            if (briefingPanel != null) briefingPanel.SetActive(false);
            _state = State.Running;
            _player.InputEnabled = true;
            ShowBanner();
        }

        private void Update()
        {
            if (_state != State.Running) return;

            float dt = Time.deltaTime;
            _runSeconds += dt;

            // Speed boost/slow decays back to normal.
            if (_speedEffectTimer > 0f)
            {
                _speedEffectTimer -= dt;
                if (_speedEffectTimer <= 0f) _speedMultiplier = 1f;
            }

            // Scroll the world toward the player.
            float speed = _story.mission.playerSpeed * _speedMultiplier;
            _world.position += Vector3.back * speed * dt;

            // Danger climbs over time (TDD §11.5).
            _danger = Mathf.Clamp(_danger + _story.mission.dangerPerSecond * dt, 0f, GameRules.DangerMax);
            UpdateDangerVisuals();
            if (_danger >= GameRules.DangerMax) StartCoroutine(CaughtRoutine());

            // The correct piece must always be collected — recycle a missed checkpoint.
            if (_currentElement < _checkpoints.Length)
            {
                var cp = _checkpoints[_currentElement];
                if (cp != null && cp.position.z < -3f)
                    cp.position += Vector3.forward * 25f;
            }

            if (_feedbackTimer > 0f)
            {
                _feedbackTimer -= dt;
                if (_feedbackTimer <= 0f && feedbackText != null) feedbackText.text = "";
            }
        }

        public void OnPickupHit(OptionPickup pickup)
        {
            if (_state != State.Running) return;

            if (pickup.isFinishGate) { Finish(); return; }
            if (pickup.elementIndex != _currentElement) return; // stray hit on a future gate

            // Only the FIRST pick at each checkpoint counts for stars (GDD §4.2).
            if (!_firstPickDone[_currentElement])
            {
                _firstPickDone[_currentElement] = true;
                _firstPickCorrect[_currentElement] = pickup.isCorrect;
            }

            if (pickup.isCorrect) CollectCorrect(pickup);
            else HitWrong(pickup);

            EventBus.Raise(new ElementCollected
            {
                elementIndex = _currentElement,
                wasCorrect = pickup.isCorrect
            });
        }

        private void CollectCorrect(OptionPickup pickup)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySfx(AudioKeys.SfxCollect);
                AudioManager.Instance.PlaySfx(AudioKeys.SfxBoost);
            }

            _speedMultiplier = 1.5f;
            _speedEffectTimer = GameRules.BoostSeconds;
            _danger = Mathf.Max(0f, _danger - GameRules.DangerRelief);
            ShowFeedback("You got it! SPEED BOOST!", new Color(0.2f, 0.6f, 0.2f));

            Destroy(_checkpoints[_currentElement].gameObject);
            _checkpoints[_currentElement] = null;
            _correctPickup = null;

            _currentElement++;
            ShowBanner();
        }

        private void HitWrong(OptionPickup pickup)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxNotQuite);

            _speedMultiplier = 0.6f;
            _speedEffectTimer = GameRules.SlowSeconds;
            _danger = Mathf.Min(GameRules.DangerMax, _danger + GameRules.DangerOnWrong);
            ShowFeedback("Not quite — grab the glowing one!", new Color(0.75f, 0.45f, 0.05f));

            Destroy(pickup.gameObject);

            // Highlight the correct pickup: it glows until collected (TDD §11.4).
            if (_correctPickup != null)
            {
                _correctPickup.localScale = Vector3.one * 1.6f;
                var rend = _correctPickup.GetComponent<Renderer>();
                if (rend != null) rend.material.color = new Color(1f, 0.9f, 0.2f);
            }
        }

        private IEnumerator CaughtRoutine()
        {
            // Friendly tag, never a fail state (GDD D7).
            _state = State.Caught;
            _timesCaught++;
            _danger = GameRules.DangerAfterCaught;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxCaught);
            if (caughtPanel != null) caughtPanel.SetActive(true);
            EventBus.Raise(new PlayerCaught());

            yield return new WaitForSeconds(1.5f);

            if (caughtPanel != null) caughtPanel.SetActive(false);
            _state = State.Running;
        }

        private void Finish()
        {
            _state = State.Finished;
            _player.InputEnabled = false;

            var result = new RaceResult
            {
                timesCaught = _timesCaught,
                runSeconds = _runSeconds
            };
            for (int i = 0; i < 5; i++)
            {
                result.collectedPieces[i] = _story.elements[i].correct;
                result.firstPickCorrect[i] = _firstPickCorrect[i];
            }

            if (GameManager.Instance != null) GameManager.Instance.SetRaceResult(result);
            EventBus.Raise(new RaceCompleted { result = result });

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxStar);
            if (bannerText != null) bannerText.text = "FINISH!";
            if (SceneLoader.Instance != null) SceneLoader.Instance.Load(SceneNames.Arrange);
        }

        // ---------- world building (grey-box, TDD §11.7) ----------

        private void BuildWorld()
        {
            _world = new GameObject("World").transform;

            // Ground: one long green strip (object pooling comes with polish).
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(_world, false);
            float length = 40f + _checkpoints_Length() * _story.mission.checkpointSpacing + 60f;
            ground.transform.localScale = new Vector3(GameRules.LaneWidth * 3f + 2f, 0.5f, length);
            ground.transform.localPosition = new Vector3(0f, -0.25f, length * 0.5f - 20f);
            ground.GetComponent<Renderer>().material.color = new Color(0.45f, 0.75f, 0.35f);
            Destroy(ground.GetComponent<Collider>());

            // Player capsule at the origin.
            var playerGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerGo.name = "Player";
            playerGo.tag = "Player";
            playerGo.transform.position = new Vector3(0f, 1f, 0f);
            playerGo.GetComponent<Renderer>().material.color = new Color(0.95f, 0.5f, 0.15f);
            var rb = playerGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            _player = playerGo.AddComponent<PlayerRunner>();
            _player.InputEnabled = false;

            // Patrol cube behind (visual pressure only).
            var patrolGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            patrolGo.name = "Patrol";
            patrolGo.transform.localScale = new Vector3(2f, 2f, 2f);
            patrolGo.GetComponent<Renderer>().material.color = new Color(0.25f, 0.35f, 0.85f);
            Destroy(patrolGo.GetComponent<Collider>());
            _patrol = patrolGo.transform;

            // 5 checkpoints in S-W-B-S-T order.
            _checkpoints = new Transform[5];
            for (int i = 0; i < 5; i++)
                _checkpoints[i] = BuildCheckpoint(i);

            // Finish gate past the last checkpoint.
            BuildFinishGate(20f + 5 * _story.mission.checkpointSpacing + 15f);
        }

        private int _checkpoints_Length() => 5;

        private Transform BuildCheckpoint(int elementIndex)
        {
            var element = _story.elements[elementIndex];
            var root = new GameObject("Checkpoint_" + element.type).transform;
            root.SetParent(_world, false);
            root.localPosition = new Vector3(0f, 0f, 20f + elementIndex * _story.mission.checkpointSpacing);

            // One correct + two distractors, shuffled across the 3 lanes.
            string[] texts = { element.correct, element.distractors[0], element.distractors[1] };
            bool[] correct = { true, false, false };
            for (int i = 2; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (texts[i], texts[j]) = (texts[j], texts[i]);
                (correct[i], correct[j]) = (correct[j], correct[i]);
            }

            for (int lane = 0; lane < 3; lane++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Option_" + lane;
                cube.transform.SetParent(root, false);
                cube.transform.localPosition = new Vector3((lane - 1) * GameRules.LaneWidth, 1f, 0f);
                cube.transform.localScale = new Vector3(1.6f, 1.6f, 0.6f);
                cube.GetComponent<Renderer>().material.color = new Color(0.96f, 0.87f, 0.70f);
                cube.GetComponent<Collider>().isTrigger = true;

                var pickup = cube.AddComponent<OptionPickup>();
                pickup.elementIndex = elementIndex;
                pickup.isCorrect = correct[lane];
                if (correct[lane] && elementIndex == 0) _correctPickup = cube.transform;
                if (correct[lane]) TagAsCorrectOf(elementIndex, cube.transform);

                // Floating world-space label.
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(cube.transform, false);
                labelGo.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                labelGo.transform.localScale = Vector3.one * 0.08f;
                var mesh = labelGo.AddComponent<TextMesh>();
                mesh.text = Wrap(texts[lane], 16);
                mesh.fontSize = 60;
                mesh.characterSize = 0.5f;
                mesh.anchor = TextAnchor.LowerCenter;
                mesh.alignment = TextAlignment.Center;
                mesh.color = Color.black;
            }

            // SWBST banner above the middle of the gate.
            var typeGo = new GameObject("TypeLabel");
            typeGo.transform.SetParent(root, false);
            typeGo.transform.localPosition = new Vector3(0f, 4.2f, 0f);
            var typeMesh = typeGo.AddComponent<TextMesh>();
            typeMesh.text = element.type;
            typeMesh.fontSize = 80;
            typeMesh.characterSize = 0.35f;
            typeMesh.anchor = TextAnchor.MiddleCenter;
            typeMesh.alignment = TextAlignment.Center;
            typeMesh.color = new Color(0.13f, 0.3f, 0.55f);

            return root;
        }

        private readonly Transform[] _correctOf = new Transform[5];

        private void TagAsCorrectOf(int elementIndex, Transform t) => _correctOf[elementIndex] = t;

        private void BuildFinishGate(float z)
        {
            var gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gate.name = "FinishGate";
            gate.transform.SetParent(_world, false);
            gate.transform.localPosition = new Vector3(0f, 1.5f, z);
            gate.transform.localScale = new Vector3(GameRules.LaneWidth * 3f, 3f, 0.5f);
            var rend = gate.GetComponent<Renderer>();
            rend.material.color = new Color(1f, 0.85f, 0.2f, 0.6f);
            gate.GetComponent<Collider>().isTrigger = true;
            var pickup = gate.AddComponent<OptionPickup>();
            pickup.isFinishGate = true;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(gate.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var mesh = labelGo.AddComponent<TextMesh>();
            mesh.text = "FINISH";
            mesh.fontSize = 90;
            mesh.characterSize = 0.12f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = new Color(0.5f, 0.3f, 0.05f);
        }

        // ---------- HUD ----------

        private void ShowBanner()
        {
            // Track the glowing-correct reference for the current gate.
            if (_currentElement < 5) _correctPickup = _correctOf[_currentElement];

            if (bannerGroup != null) bannerGroup.SetActive(true);
            if (bannerText == null) return;
            bannerText.text = _currentElement < 5
                ? "Collect: " + _story.elements[_currentElement].type
                : "Run to the FINISH!";
        }

        private void ShowFeedback(string message, Color color)
        {
            if (feedbackText == null) return;
            feedbackText.text = message;
            feedbackText.color = color;
            Tween.PunchScale(feedbackText.transform, Vector3.one * 0.3f, 0.35f);
            _feedbackTimer = 1.4f;
        }

        private void UpdateDangerVisuals()
        {
            float t = _danger / GameRules.DangerMax;

            // Patrol creeps closer as danger rises.
            if (_patrol != null)
            {
                var playerPos = _player.transform.position;
                _patrol.position = new Vector3(playerPos.x, 1f, playerPos.z - (3f + (1f - t) * 12f));
            }

            if (dangerFill != null)
                dangerFill.anchorMax = new Vector2(Mathf.Lerp(0.02f, 1f, t), 1f);

            if (vignette != null)
                vignette.color = new Color(1f, 0.55f, 0.1f, t * 0.28f);
        }

        private static string Wrap(string text, int lineLength)
        {
            var words = text.Split(' ');
            var sb = new System.Text.StringBuilder();
            int lineLen = 0;
            foreach (var word in words)
            {
                if (lineLen + word.Length > lineLength && lineLen > 0)
                {
                    sb.Append('\n');
                    lineLen = 0;
                }
                else if (lineLen > 0)
                {
                    sb.Append(' ');
                    lineLen++;
                }
                sb.Append(word);
                lineLen += word.Length;
            }
            return sb.ToString();
        }
    }
}
