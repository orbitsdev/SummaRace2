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

        [Header("World visuals (fallback = grey-box primitives)")]
        [SerializeField] private GameObject playerModelPrefab;
        [SerializeField] private GameObject patrolModelPrefab;
        [SerializeField] private GameObject[] sceneryPrefabs;
        [SerializeField] private GameObject fencePrefab;
        [SerializeField] private Material highlightMaterial;
        [SerializeField] private TMP_FontAsset worldLabelFont;

        [Header("Cartoon FX (optional)")]
        [SerializeField] private GameObject collectFxPrefab;
        [SerializeField] private GameObject wrongFxPrefab;
        [SerializeField] private GameObject caughtFxPrefab;
        [SerializeField] private GameObject finishFxPrefab;
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

        /// <summary>Current boost/slow factor — read by the chase camera for its FOV kick.</summary>
        public float SpeedMultiplier => _speedMultiplier;
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
            SetRunning(true);
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

            // Run cycle keeps pace with the boost/slow so feet don't slide.
            if (_playerAnim != null) _playerAnim.speed = _speedMultiplier;

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
            SpawnFx(collectFxPrefab, pickup.transform.position);

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
            SpawnFx(wrongFxPrefab, pickup.transform.position);

            Destroy(pickup.gameObject);

            // Highlight the correct pickup: it glows until collected (TDD §11.4).
            if (_correctPickup != null)
            {
                _correctPickup.localScale = Vector3.one * 1.6f;
                var rend = _correctPickup.GetComponent<Renderer>();
                if (rend != null)
                {
                    if (highlightMaterial != null) rend.material = highlightMaterial;
                    else rend.material.color = new Color(1f, 0.9f, 0.2f);
                }
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
            if (_playerAnim != null) _playerAnim.SetTrigger("Stumble");
            if (_player != null) SpawnFx(caughtFxPrefab, _player.transform.position + Vector3.up * 1.6f);
            EventBus.Raise(new PlayerCaught());

            yield return new WaitForSeconds(1.5f);

            if (caughtPanel != null) caughtPanel.SetActive(false);
            _state = State.Running;
        }

        private void Finish()
        {
            _state = State.Finished;
            _player.InputEnabled = false;
            SetRunning(false);
            if (_playerAnim != null) _playerAnim.SetTrigger("Dance"); // victory dance!
            if (_player != null) SpawnFx(finishFxPrefab, _player.transform.position + new Vector3(0f, 2.5f, 4f));

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

            // Grass ground with three colored running lanes (mockup 17 style).
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(_world, false);
            float length = 40f + _checkpoints_Length() * _story.mission.checkpointSpacing + 60f;
            ground.transform.localScale = new Vector3(GameRules.LaneWidth * 3f + 6f, 0.5f, length);
            ground.transform.localPosition = new Vector3(0f, -0.25f, length * 0.5f - 20f);
            ground.GetComponent<Renderer>().material.color = new Color(0.43f, 0.73f, 0.29f); // grass
            Destroy(ground.GetComponent<Collider>());

            var laneColors = new[]
            {
                new Color(0.29f, 0.45f, 0.84f), // blue
                new Color(0.95f, 0.76f, 0.11f), // yellow
                new Color(0.34f, 0.73f, 0.30f)  // green
            };
            for (int lane = 0; lane < 3; lane++)
            {
                var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                strip.name = "Lane_" + lane;
                strip.transform.SetParent(_world, false);
                strip.transform.localScale = new Vector3(GameRules.LaneWidth * 0.9f, 0.1f, length);
                strip.transform.localPosition = new Vector3((lane - 1) * GameRules.LaneWidth, 0.03f, length * 0.5f - 20f);
                strip.GetComponent<Renderer>().material.color = laneColors[lane];
                Destroy(strip.GetComponent<Collider>());
            }

            BuildScenery(length);

            // Player at the origin: physics root + character model (or grey-box capsule).
            var playerGo = new GameObject("Player");
            playerGo.tag = "Player";
            playerGo.transform.position = Vector3.zero;
            var col = playerGo.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1f, 0f);
            col.height = 2f;
            col.radius = 0.4f;
            var rb = playerGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            _playerAnim = SpawnCharacter(playerGo.transform, playerModelPrefab, new Color(0.95f, 0.5f, 0.15f));
            _player = playerGo.AddComponent<PlayerRunner>();
            _player.InputEnabled = false;

            var chaseCam = Camera.main != null ? Camera.main.GetComponent<RaceChaseCamera>() : null;
            if (chaseCam != null) chaseCam.SetTarget(playerGo.transform);

            // Patrol behind (visual pressure only).
            var patrolGo = new GameObject("Patrol");
            _patrolAnim = SpawnCharacter(patrolGo.transform, patrolModelPrefab, new Color(0.25f, 0.35f, 0.85f));
            _patrol = patrolGo.transform;

            // 5 checkpoints in S-W-B-S-T order.
            _checkpoints = new Transform[5];
            for (int i = 0; i < 5; i++)
                _checkpoints[i] = BuildCheckpoint(i);

            // Finish gate past the last checkpoint.
            BuildFinishGate(20f + 5 * _story.mission.checkpointSpacing + 15f);
        }

        private int _checkpoints_Length() => 5;

        private Animator _playerAnim;
        private Animator _patrolAnim;

        /// <summary>Fences, trees, props and clouds along the track; deterministic so every run of a story looks the same.</summary>
        private void BuildScenery(float length)
        {
            float edge = GameRules.LaneWidth * 1.5f + 2f;

            // Wooden fence lines hugging the track (mockup 17).
            if (fencePrefab != null)
            {
                for (float z = -12f; z < length - 22f; z += 1.8f)
                for (int s = 0; s < 2; s++)
                {
                    float side = s == 0 ? -1f : 1f;
                    var f = Instantiate(fencePrefab, _world);
                    f.transform.localPosition = new Vector3(side * (edge - 1.1f), 0f, z);
                    f.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // fence mesh runs along X
                }
            }

            // Trees / props / friendly animals beyond the fence.
            if (sceneryPrefabs != null && sceneryPrefabs.Length > 0)
            {
                for (float z = -10f; z < length - 25f; z += 7f)
                {
                    for (int s = 0; s < 2; s++)
                    {
                        float side = s == 0 ? -1f : 1f;
                        int pick = Mathf.Abs((int)(z * 7.31f + side * 3f)) % sceneryPrefabs.Length;
                        var prefab = sceneryPrefabs[pick];
                        if (prefab == null) continue;

                        var prop = Instantiate(prefab, _world);
                        float lateral = edge + Mathf.PingPong(z * 0.53f, 2.5f);
                        prop.transform.localPosition = new Vector3(side * lateral, 0f, z + (side > 0f ? 3f : 0f));
                        prop.transform.localRotation = Quaternion.Euler(0f, (z * 41f) % 360f, 0f);
                        prop.transform.localScale = Vector3.one * 2f; // pack props are small; 2x reads like the mockup
                    }
                }
            }

            BuildClouds(length);
        }

        /// <summary>Puffy cartoon clouds: squashed white unlit spheres drifting past with the world.</summary>
        private void BuildClouds(float length)
        {
            var cloudMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.white };
            for (float z = 5f; z < length + 40f; z += 22f)
            {
                float side = ((int)(z / 22f) % 2 == 0) ? -1f : 1f;
                var cloud = new GameObject("Cloud").transform;
                cloud.SetParent(_world, false);
                cloud.localPosition = new Vector3(side * (8f + (z * 0.37f) % 9f), 11f + (z * 0.61f) % 5f, z);

                int puffs = 3;
                for (int i = 0; i < puffs; i++)
                {
                    var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(puff.GetComponent<Collider>());
                    puff.transform.SetParent(cloud, false);
                    float t = i - (puffs - 1) * 0.5f;
                    puff.transform.localPosition = new Vector3(t * 1.6f, Mathf.Abs(t) * -0.4f, 0f);
                    float s = 2.4f - Mathf.Abs(t) * 0.7f;
                    puff.transform.localScale = new Vector3(s, s * 0.62f, s * 0.8f);
                    puff.GetComponent<Renderer>().sharedMaterial = cloudMat;
                }
            }
        }

        /// <summary>Instantiates the rigged character, or a grey-box capsule when no prefab is wired.</summary>
        private Animator SpawnCharacter(Transform parent, GameObject modelPrefab, Color fallbackColor)
        {
            if (modelPrefab != null)
            {
                var model = Instantiate(modelPrefab, parent);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                return model.GetComponentInChildren<Animator>();
            }

            var prim = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            prim.transform.SetParent(parent, false);
            prim.transform.localPosition = new Vector3(0f, 1f, 0f);
            prim.GetComponent<Renderer>().material.color = fallbackColor;
            Destroy(prim.GetComponent<Collider>());
            return null;
        }

        private void SpawnFx(GameObject prefab, Vector3 position)
        {
            if (prefab == null) return;
            var fx = Instantiate(prefab, position, Quaternion.identity);
            Destroy(fx, 4f); // CFXR effects self-stop; this just tidies the scene
        }

        private void SetRunning(bool running)
        {
            if (_playerAnim != null) _playerAnim.SetBool("Running", running);
            if (_patrolAnim != null) _patrolAnim.SetBool("Running", running);
        }

        /// <summary>3D TMP label on a dark backing card (race world text, mockup 17 style).</summary>
        private void BuildWorldLabel(Transform parent, Vector3 localPos, Vector3 localScale,
            Vector2 rect, string text, Color textColor, float maxFontSize)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;

            var tmp = go.AddComponent<TextMeshPro>();
            if (worldLabelFont != null) tmp.font = worldLabelFont;
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = textColor;
            tmp.rectTransform.sizeDelta = rect;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.5f;
            tmp.fontSizeMax = maxFontSize;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "LabelBack";
            Destroy(quad.GetComponent<Collider>()); // must never catch the player's trigger
            quad.transform.SetParent(go.transform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            quad.transform.localScale = new Vector3(rect.x + 0.2f, rect.y + 0.2f, 1f);
            var rend = quad.GetComponent<Renderer>();
            rend.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            rend.material.color = new Color(0.15f, 0.18f, 0.28f, 1f);
        }

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

                cube.AddComponent<PickupBob>();
                var pickup = cube.AddComponent<OptionPickup>();
                pickup.elementIndex = elementIndex;
                pickup.isCorrect = correct[lane];
                if (correct[lane] && elementIndex == 0) _correctPickup = cube.transform;
                if (correct[lane]) TagAsCorrectOf(elementIndex, cube.transform);

                // Floating world-space label: white text on a dark card (mockup 17).
                BuildWorldLabel(cube.transform, new Vector3(0f, 1.15f, 0f), Vector3.one,
                    new Vector2(1.5f, 0.9f), texts[lane], Color.white, 2.5f);
            }

            // SWBST banner above the middle of the gate.
            BuildWorldLabel(root, new Vector3(0f, 4.2f, 0f), Vector3.one,
                new Vector2(5f, 1.1f), element.type, new Color(1f, 0.85f, 0.3f), 4f);

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

            // Label must undo the gate's non-uniform scale to stay square.
            var inverse = new Vector3(1f / (GameRules.LaneWidth * 3f), 1f / 3f, 1f);
            BuildWorldLabel(gate.transform, new Vector3(0f, 1.2f, 0f), inverse,
                new Vector2(6f, 1.5f), "FINISH", Color.white, 5f);
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

    }
}
