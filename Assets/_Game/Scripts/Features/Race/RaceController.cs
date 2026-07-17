using System.Collections;
using FirstGearGames.SmoothCameraShaker;
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
        [SerializeField] private TMP_Text progressText;    // "1/5" pill on the banner
        [SerializeField] private GameObject feedbackGroup; // pill behind the feedback text
        [SerializeField] private TMP_Text feedbackText;

        [Header("World visuals (fallback = grey-box primitives)")]
        [SerializeField] private GameObject playerModelPrefab;
        [SerializeField] private GameObject patrolModelPrefab;
        [SerializeField] private GameObject[] sceneryPrefabs;
        [SerializeField] private GameObject[] tracksideObstaclePrefabs;
        [SerializeField] private GameObject[] skylinePrefabs;
        [SerializeField] private GameObject fencePrefab;
        [SerializeField] private Sprite worldCardSprite;
        [SerializeField] private TMP_FontAsset worldLabelFont;

        [Header("Story sparkle FX (Hovl, retinted gold at spawn)")]
        [SerializeField] private GameObject optionPadFxPrefab;    // golden glow ring under each answer card
        [SerializeField] private GameObject collectMagicFxPrefab; // gold star burst on a correct pick
        [SerializeField] private GameObject finishPortalPrefab;   // golden story-gate waiting at the finish line

        [Header("Cartoon FX (optional)")]
        [SerializeField] private GameObject collectFxPrefab;
        [SerializeField] private GameObject wrongFxPrefab;
        [SerializeField] private GameObject caughtFxPrefab;
        [SerializeField] private GameObject finishFxPrefab;
        [SerializeField] private GameObject boostTrailPrefab;
        [SerializeField] private ShakeData caughtShake;
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
        private float _menaceTimer; // wrong pick → patrol surges into view for a beat
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
            if (dangerFill != null) dangerFill.parent.gameObject.SetActive(false); // meter appears with the countdown
            if (startMissionButton != null)
                startMissionButton.onClick.AddListener(StartMission);
        }

        private void StartMission()
        {
            if (_state != State.Briefing) return;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);
            if (briefingPanel != null) briefingPanel.SetActive(false);
            StartCoroutine(CountdownRoutine());
        }

        /// <summary>3-2-1-GO! before the run — every runner game needs one.</summary>
        private IEnumerator CountdownRoutine()
        {
            if (bannerGroup != null) bannerGroup.SetActive(true);
            if (dangerFill != null) dangerFill.parent.gameObject.SetActive(true);
            string[] steps = { "3", "2", "1", "GO!" };
            foreach (var step in steps)
            {
                if (bannerText != null)
                {
                    bannerText.text = step;
                    Tween.PunchScale(bannerText.transform, Vector3.one * 0.45f, 0.3f);
                }
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySfx(step == "GO!" ? AudioKeys.SfxBoost : AudioKeys.SfxPop);
                yield return new WaitForSeconds(step == "GO!" ? 0.45f : 0.7f);
            }

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

            // The run gently accelerates over time (Trash Dash pacing) — the finish feels fast.
            float accel = 1f + Mathf.Min(_runSeconds * GameRules.RaceAccelPerSecond, GameRules.RaceAccelMaxBonus);

            // Run cycle keeps pace with the boost/slow so feet don't slide.
            if (_playerAnim != null) _playerAnim.speed = _speedMultiplier * accel;

            // Wind trail only while boosted.
            bool boosting = _speedMultiplier > 1.05f;
            if (_boostTrail != null && _boostTrail.activeSelf != boosting) _boostTrail.SetActive(boosting);

            // Grass footsteps in time with the run cycle.
            _footstepTimer -= dt * _speedMultiplier * accel;
            if (_footstepTimer <= 0f)
            {
                _footstepTimer = 0.34f;
                if (AudioManager.Instance != null)
                {
                    _footstepIndex = (_footstepIndex + 1) % 3;
                    var key = _footstepIndex == 0 ? AudioKeys.SfxFootstepA
                        : _footstepIndex == 1 ? AudioKeys.SfxFootstepB : AudioKeys.SfxFootstepC;
                    AudioManager.Instance.PlaySfx(key);
                }
            }

            // Scroll the world toward the player.
            float speed = _story.mission.playerSpeed * _speedMultiplier * accel;
            _world.position += Vector3.back * speed * dt;

            // Danger climbs over time (TDD §11.5).
            _danger = Mathf.Clamp(_danger + _story.mission.dangerPerSecond * dt, 0f, GameRules.DangerMax);
            UpdateDangerVisuals();
            if (_danger >= GameRules.DangerMax) StartCoroutine(CaughtRoutine());

            // The correct piece must always be collected — recycle a missed checkpoint,
            // but never on top of the next gate (that read as a looping bug).
            if (_currentElement < _checkpoints.Length)
            {
                var cp = _checkpoints[_currentElement];
                if (cp != null && cp.position.z < -3f)
                {
                    float newZ = cp.position.z + 25f;
                    if (_currentElement + 1 < _checkpoints.Length && _checkpoints[_currentElement + 1] != null)
                        newZ = Mathf.Min(newZ, _checkpoints[_currentElement + 1].position.z - 12f);
                    newZ = Mathf.Max(newZ, 14f); // always respawn comfortably ahead of the player
                    cp.position = new Vector3(cp.position.x, cp.position.y, newZ);
                }
            }

            if (_feedbackTimer > 0f)
            {
                _feedbackTimer -= dt;
                if (_feedbackTimer <= 0f)
                {
                    if (feedbackText != null) feedbackText.text = "";
                    if (feedbackGroup != null) feedbackGroup.SetActive(false);
                }
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
            ShowFeedback("You got it! SPEED BOOST!", new Color(0.55f, 1f, 0.55f));
            SpawnFx(collectFxPrefab, pickup.transform.position);
            TintFx(SpawnFx(collectMagicFxPrefab, pickup.transform.position), StoryGold);
            if (_playerAnim != null) _playerAnim.SetTrigger("Jump"); // celebratory hop

            // The collected card celebrates: flies up and pops away.
            var cardT = pickup.transform;
            cardT.SetParent(null, true);
            var col = pickup.GetComponent<Collider>();
            if (col != null) col.enabled = false;
            var bob = pickup.GetComponent<PickupBob>();
            if (bob != null) Destroy(bob);
            Tween.PositionY(cardT, cardT.position.y + 2.2f, 0.45f, Ease.OutQuad);
            Tween.Scale(cardT, Vector3.zero, 0.5f, Ease.InBack)
                .OnComplete(() => { if (cardT != null) Destroy(cardT.gameObject); });

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
            _menaceTimer = 1.6f; // the patrol visibly closes in — the mistake has a face
            ShowFeedback("Not quite — grab the glowing one!", new Color(1f, 0.78f, 0.35f));
            SpawnFx(wrongFxPrefab, pickup.transform.position);

            Destroy(pickup.gameObject);

            // Highlight the correct pickup: golden card + gentle grow until collected (TDD §11.4).
            if (_correctPickup != null)
            {
                _correctPickup.localScale = new Vector3(2.3f, 2.3f, 0.5f);
                var sr = _correctPickup.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) sr.color = new Color(1f, 0.85f, 0.35f);
                Tween.PunchScale(_correctPickup, Vector3.one * 0.18f, 0.4f);
            }
        }

        private IEnumerator CaughtRoutine()
        {
            // Friendly tag, never a fail state (GDD D7).
            _state = State.Caught;
            _timesCaught++;
            _danger = GameRules.DangerAfterCaught;
            if (_playerAnim != null) _playerAnim.speed = 1f;
            // The patrol actually lunges in to make the tag.
            if (_patrol != null && _player != null)
                Tween.Position(_patrol, _player.transform.position + new Vector3(0f, 0f, -1.3f), 0.25f, Ease.OutQuad);
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxCaught);
            if (caughtPanel != null) caughtPanel.SetActive(true);
            if (_playerAnim != null) _playerAnim.SetTrigger("Stumble");
            if (_player != null) SpawnFx(caughtFxPrefab, _player.transform.position + Vector3.up * 1.6f);
            if (caughtShake != null) CameraShakerHandler.Shake(caughtShake);
            EventBus.Raise(new PlayerCaught());

            yield return new WaitForSeconds(1.5f);

            if (caughtPanel != null) caughtPanel.SetActive(false);
            _state = State.Running;
        }

        private void Finish() => StartCoroutine(FinishRoutine());

        private IEnumerator FinishRoutine()
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
            if (progressText != null) progressText.text = "5/5";

            // Victory beat: let the dance and fireworks land before leaving.
            yield return new WaitForSeconds(2.2f);
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

            // Natural dirt trail across all three lanes (adventure-park look):
            // sandy bed, packed-earth edges, dashed cream lane guides.
            var trail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trail.name = "Trail";
            trail.transform.SetParent(_world, false);
            trail.transform.localScale = new Vector3(GameRules.LaneWidth * 3f - 0.2f, 0.1f, length);
            trail.transform.localPosition = new Vector3(0f, 0.03f, length * 0.5f - 20f);
            var trailMat = trail.GetComponent<Renderer>().material;
            trailMat.color = new Color(0.76f, 0.60f, 0.38f);
            trailMat.SetFloat("_Smoothness", 0f); // matte dirt — no specular wash-out
            Destroy(trail.GetComponent<Collider>());

            Material edgeMat = null;
            for (int s = 0; s < 2; s++)
            {
                var trailEdge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                trailEdge.name = "TrailEdge";
                trailEdge.transform.SetParent(_world, false);
                trailEdge.transform.localScale = new Vector3(0.45f, 0.12f, length);
                trailEdge.transform.localPosition = new Vector3(
                    (s == 0 ? -1f : 1f) * (GameRules.LaneWidth * 1.5f + 0.1f), 0.04f, length * 0.5f - 20f);
                var er = trailEdge.GetComponent<Renderer>();
                if (edgeMat == null) { edgeMat = er.material; edgeMat.color = new Color(0.52f, 0.39f, 0.24f); edgeMat.SetFloat("_Smoothness", 0f); }
                else er.sharedMaterial = edgeMat;
                Destroy(trailEdge.GetComponent<Collider>());
            }

            Material guideMat = null;
            for (int d = 0; d < 2; d++)
            for (float z = -16f; z < length - 22f; z += 6f)
            {
                var dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dash.name = "LaneGuide";
                dash.transform.SetParent(_world, false);
                dash.transform.localScale = new Vector3(0.12f, 0.02f, 1.0f);
                dash.transform.localPosition = new Vector3(
                    (d == 0 ? -1f : 1f) * GameRules.LaneWidth * 0.5f, 0.09f, z);
                var dr = dash.GetComponent<Renderer>();
                if (guideMat == null) { guideMat = dr.material; guideMat.color = new Color(0.87f, 0.79f, 0.60f); guideMat.SetFloat("_Smoothness", 0f); }
                else dr.sharedMaterial = guideMat;
                Destroy(dash.GetComponent<Collider>());
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

            if (boostTrailPrefab != null)
            {
                _boostTrail = Instantiate(boostTrailPrefab, playerGo.transform);
                _boostTrail.transform.localPosition = new Vector3(0f, 1f, -0.4f);
                _boostTrail.SetActive(false);
            }

            // Patrol behind (visual pressure only).
            var patrolGo = new GameObject("Patrol");
            _patrolAnim = SpawnCharacter(patrolGo.transform, patrolModelPrefab, new Color(0.25f, 0.35f, 0.85f));
            _patrol = patrolGo.transform;

            // 5 checkpoints in S-W-B-S-T order.
            _checkpoints = new Transform[5];
            for (int i = 0; i < 5; i++)
                _checkpoints[i] = BuildCheckpoint(i);

            // Finish gate past the last checkpoint.
            float finishZ = 20f + 5 * _story.mission.checkpointSpacing + 15f;
            BuildFinishGate(finishZ);

            BuildCoinLines(finishZ);
        }

        /// <summary>
        /// Center-lane coin trails between the answer gates (Trash Dash micro-reward).
        /// They also guide the eye back to the middle, where all 3 upcoming cards are visible.
        /// </summary>
        private void BuildCoinLines(float finishZ)
        {
            var coinMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            coinMat.color = new Color(1f, 0.82f, 0.20f);
            coinMat.SetFloat("_Smoothness", 0.45f);

            float spacing = _story.mission.checkpointSpacing;
            for (int gap = 0; gap <= 5; gap++)
            {
                float from = gap == 0 ? 6f : 20f + (gap - 1) * spacing + 7f;
                float to = gap == 5 ? finishZ - 8f : 20f + gap * spacing - 9f;
                for (float z = from; z <= to; z += 2.6f)
                {
                    var coin = new GameObject("Coin");
                    coin.transform.SetParent(_world, false);
                    coin.transform.localPosition = new Vector3(0f, 1.15f, z);

                    var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(disc.GetComponent<Collider>());
                    disc.transform.SetParent(coin.transform, false);
                    disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // face the runner
                    disc.transform.localScale = new Vector3(0.5f, 0.045f, 0.5f);
                    disc.GetComponent<Renderer>().sharedMaterial = coinMat;

                    var trigger = coin.AddComponent<SphereCollider>();
                    trigger.isTrigger = true;
                    trigger.radius = 0.55f;
                    coin.AddComponent<CoinPickup>();
                }
            }
        }

        private int _checkpoints_Length() => 5;

        private Animator _playerAnim;
        private Animator _patrolAnim;
        private GameObject _boostTrail;
        private float _footstepTimer;
        private int _footstepIndex;

        /// <summary>Called by PlayerRunner so the character leans into lane changes.</summary>
        public void OnLaneSwitched(int direction)
        {
            if (_playerAnim != null)
                _playerAnim.SetTrigger(direction < 0 ? "LeanLeft" : "LeanRight");
        }

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

            // Rocks, stumps and mushrooms on the trail shoulder (between trail edge
            // and fence) — adventure dressing only: colliders stripped, lanes stay
            // clear, and nothing spawns near a checkpoint's answer cards.
            if (tracksideObstaclePrefabs != null && tracksideObstaclePrefabs.Length > 0)
            {
                float spacing = _story.mission.checkpointSpacing;
                for (float z = 4f; z < length - 30f; z += 9f)
                {
                    bool nearCheckpoint = false;
                    for (int i = 0; i < 5; i++)
                        if (Mathf.Abs(z - (20f + i * spacing)) < 6f) { nearCheckpoint = true; break; }
                    if (nearCheckpoint) continue;

                    float side = ((int)(z * 0.731f) % 2 == 0) ? -1f : 1f;
                    int pick = Mathf.Abs((int)(z * 5.13f)) % tracksideObstaclePrefabs.Length;
                    var obstacle = tracksideObstaclePrefabs[pick];
                    if (obstacle == null) continue;

                    var prop = Instantiate(obstacle, _world);
                    prop.transform.localPosition = new Vector3(side * (edge - 1.35f), 0f, z);
                    prop.transform.localRotation = Quaternion.Euler(0f, (z * 77f) % 360f, 0f);
                    prop.transform.localScale = Vector3.one * 2.2f;
                    foreach (var c in prop.GetComponentsInChildren<Collider>()) Destroy(c);
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

            BuildSkyline(length);
            BuildClouds(length);
        }

        /// <summary>Distant cartoon-city skyline beyond the park — big blocks fading into the fog.</summary>
        private void BuildSkyline(float length)
        {
            if (skylinePrefabs == null || skylinePrefabs.Length == 0) return;

            for (float z = 10f; z < length + 60f; z += 30f)
            {
                for (int s = 0; s < 2; s++)
                {
                    float side = s == 0 ? -1f : 1f;
                    int pick = Mathf.Abs((int)(z * 3.17f + s * 7f)) % skylinePrefabs.Length;
                    var prefab = skylinePrefabs[pick];
                    if (prefab == null) continue;

                    var block = Instantiate(prefab, _world);
                    float lateral = 34f + (z * 0.83f) % 10f; // pushed back: skyline reads as distance, not neighbors
                    block.transform.localPosition = new Vector3(side * lateral, 0f, z);
                    block.transform.localRotation = Quaternion.Euler(0f, side > 0f ? 180f : 0f, 0f);
                }
            }
        }

        /// <summary>Puffy cartoon clouds: squashed white unlit spheres drifting past with the world.</summary>
        private void BuildClouds(float length)
        {
            // High, wide, chunky clouds — sky dressing, never window-cluttering blobs.
            var cloudMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(1f, 1f, 1f, 1f) };
            for (float z = 5f; z < length + 40f; z += 26f)
            {
                float side = ((int)(z / 26f) % 2 == 0) ? -1f : 1f;
                var cloud = new GameObject("Cloud").transform;
                cloud.SetParent(_world, false);
                cloud.localPosition = new Vector3(side * (13f + (z * 0.37f) % 12f), 15f + (z * 0.61f) % 6f, z);

                int puffs = 5;
                for (int i = 0; i < puffs; i++)
                {
                    var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(puff.GetComponent<Collider>());
                    puff.transform.SetParent(cloud, false);
                    float t = i - (puffs - 1) * 0.5f;
                    puff.transform.localPosition = new Vector3(t * 1.7f, Mathf.Abs(t) * -0.55f, ((i % 2) - 0.5f) * 0.9f);
                    float s = 3.1f - Mathf.Abs(t) * 0.75f;
                    puff.transform.localScale = new Vector3(s, s * 0.6f, s * 0.85f);
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

        private GameObject SpawnFx(GameObject prefab, Vector3 position)
        {
            if (prefab == null) return null;
            var fx = Instantiate(prefab, position, Quaternion.identity);
            Destroy(fx, 4f); // CFXR effects self-stop; this just tidies the scene
            return fx;
        }

        // ---------- story-sparkle tinting (theme: "stories are treasure") ----------

        /// <summary>Warm gold shared by every Hovl FX — sparkle, never sorcery.</summary>
        private static readonly Color StoryGold = new Color(1f, 0.85f, 0.45f);

        /// <summary>Retints every particle system in an FX instance toward the given color, keeping alphas.</summary>
        private static void TintFx(GameObject fx, Color tint)
        {
            if (fx == null) return;
            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.startColor = Tinted(main.startColor, tint);

                var col = ps.colorOverLifetime;
                if (col.enabled) col.color = Tinted(col.color, tint);
            }
        }

        private static ParticleSystem.MinMaxGradient Tinted(ParticleSystem.MinMaxGradient g, Color tint)
        {
            switch (g.mode)
            {
                case ParticleSystemGradientMode.Color:
                    g.color = WithAlpha(tint, g.color.a);
                    break;
                case ParticleSystemGradientMode.TwoColors:
                    g.colorMin = WithAlpha(tint, g.colorMin.a);
                    g.colorMax = WithAlpha(tint, g.colorMax.a);
                    break;
                case ParticleSystemGradientMode.Gradient:
                case ParticleSystemGradientMode.RandomColor:
                    g.gradient = Tinted(g.gradient, tint);
                    break;
                case ParticleSystemGradientMode.TwoGradients:
                    g.gradientMin = Tinted(g.gradientMin, tint);
                    g.gradientMax = Tinted(g.gradientMax, tint);
                    break;
            }
            return g;
        }

        private static Gradient Tinted(Gradient source, Color tint)
        {
            if (source == null) return null;
            var colorKeys = source.colorKeys;
            for (int i = 0; i < colorKeys.Length; i++)
                colorKeys[i].color = tint;
            var gradient = new Gradient();
            gradient.SetKeys(colorKeys, source.alphaKeys);
            return gradient;
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        private void SetRunning(bool running)
        {
            if (_playerAnim != null) _playerAnim.SetBool("Running", running);
            if (_patrolAnim != null) _patrolAnim.SetBool("Running", running);
        }

        /// <summary>3D TMP label on a dark backing card (race world text, mockup 17 style).</summary>
        /// <summary>Rounded kit-sprite card with text — clean, small, kid-friendly (mockup 17).</summary>
        private void BuildWorldCard(Transform parent, Vector3 localPos, Vector3 localScale,
            Vector2 size, string text, Color textColor, Color cardColor, float maxFontSize)
        {
            var go = new GameObject("Card");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = worldCardSprite;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.color = cardColor;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.02f); // toward the camera
            var tmp = textGo.AddComponent<TextMeshPro>();
            if (worldLabelFont != null) tmp.font = worldLabelFont;
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = textColor;
            tmp.rectTransform.sizeDelta = new Vector2(size.x - 0.15f, size.y - 0.12f);
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.2f;
            tmp.fontSizeMax = maxFontSize;
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
                // Invisible trigger volume; the rounded answer card IS the visible pickup.
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Option_" + lane;
                cube.transform.SetParent(root, false);
                cube.transform.localPosition = new Vector3((lane - 1) * GameRules.LaneWidth, 1.35f, 0f);
                cube.transform.localScale = new Vector3(1.9f, 1.9f, 0.5f);
                cube.GetComponent<Renderer>().enabled = false;
                cube.GetComponent<Collider>().isTrigger = true;

                cube.AddComponent<PickupBob>();
                var pickup = cube.AddComponent<OptionPickup>();
                pickup.elementIndex = elementIndex;
                pickup.isCorrect = correct[lane];
                if (correct[lane] && elementIndex == 0) _correctPickup = cube.transform;
                if (correct[lane]) TagAsCorrectOf(elementIndex, cube.transform);

                BuildWorldCard(cube.transform, Vector3.zero, Vector3.one,
                    new Vector2(1.2f, 0.66f), texts[lane], new Color(0.20f, 0.24f, 0.32f), Color.white, 2f);

                // Soft golden glow ring on the trail under each card — "this story piece is precious".
                if (optionPadFxPrefab != null)
                {
                    var pad = Instantiate(optionPadFxPrefab, root);
                    pad.transform.localPosition = new Vector3((lane - 1) * GameRules.LaneWidth, 0.12f, 0f);
                    pad.transform.localScale = Vector3.one * 0.45f; // Healing circle is wide; keep rings lane-sized
                    TintFx(pad, StoryGold);
                }
            }

            // SWBST pill floating above the gate, in the element's signature color.
            BuildWorldCard(root, new Vector3(0f, 3.6f, 0f), Vector3.one,
                new Vector2(2.6f, 0.62f), element.type, Color.white, SwbstPalette.ForIndex(elementIndex), 3f);

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

            // Card must undo the gate's non-uniform scale to stay square.
            var inverse = new Vector3(1f / (GameRules.LaneWidth * 3f), 1f / 3f, 1f);
            BuildWorldCard(gate.transform, new Vector3(0f, 1.2f, 0f), inverse,
                new Vector2(5f, 1.3f), "FINISH", new Color(0.42f, 0.28f, 0.05f), new Color(1f, 0.83f, 0.25f), 4.5f);

            // A golden story-gate waiting at the finish — the treasure payoff in view all run.
            if (finishPortalPrefab != null)
            {
                var portal = Instantiate(finishPortalPrefab, _world);
                portal.transform.localPosition = new Vector3(0f, 2.1f, z + 3f);
                portal.transform.localScale = Vector3.one * 2.6f;
                TintFx(portal, StoryGold);
            }
        }

        // ---------- HUD ----------

        private void ShowBanner()
        {
            // Track the glowing-correct reference for the current gate.
            if (_currentElement < 5) _correctPickup = _correctOf[_currentElement];

            if (bannerGroup != null) bannerGroup.SetActive(true);
            if (progressText != null)
                progressText.text = Mathf.Min(_currentElement + 1, 5) + "/5";
            if (bannerText == null) return;
            bannerText.text = _currentElement < 5
                ? "Collect: " + _story.elements[_currentElement].type
                : "Run to the FINISH!";
        }

        private void ShowFeedback(string message, Color color)
        {
            if (feedbackText == null) return;
            if (feedbackGroup != null)
            {
                feedbackGroup.SetActive(true);
                Tween.PunchScale(feedbackGroup.transform, Vector3.one * 0.25f, 0.35f);
            }
            feedbackText.text = message;
            feedbackText.color = color;
            Tween.PunchScale(feedbackText.transform, Vector3.one * 0.3f, 0.35f);
            _feedbackTimer = 1.4f;
        }

        private void UpdateDangerVisuals()
        {
            float t = _danger / GameRules.DangerMax;

            // Patrol creeps closer as danger rises — smoothed so it feels like a chaser, not a mirror.
            // Camera sits 5.4m back, so under ~5m the patrol LOOMS INTO VIEW behind the player:
            // invisible while safe, visibly closing in once danger passes ~2/3 (subway-runner pressure).
            if (_patrol != null)
            {
                if (_menaceTimer > 0f) _menaceTimer -= Time.deltaTime;
                // During a menace beat the patrol acts like danger is near-max, so the
                // learner actually SEES the chaser close in after a wrong pick.
                float tChase = _menaceTimer > 0f ? Mathf.Max(t, 0.85f) : t;
                var playerPos = _player.transform.position;
                float targetZ = playerPos.z - (1.8f + (1f - tChase) * 8.2f);
                float nx = Mathf.Lerp(_patrol.position.x, playerPos.x, 5f * Time.deltaTime);
                float nz = Mathf.Lerp(_patrol.position.z, targetZ, 3f * Time.deltaTime);
                _patrol.position = new Vector3(nx, 0f, nz);
            }

            if (dangerFill != null)
                dangerFill.anchorMax = new Vector2(Mathf.Lerp(0.02f, 1f, t), 1f);

            if (vignette != null)
                vignette.color = new Color(1f, 0.55f, 0.1f, t * 0.28f);
        }

    }
}
