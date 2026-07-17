# Endless Runner Base + SWBST Collection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Adopt the already-imported Endless Runner (Trash Dash) as the race base: keep their run/track-generation/character/obstacles untouched, work in a *copy* of their `Main.unity`, suppress their coin/premium/powerup collection behind a runtime flag, and layer SummaRace's 5-gate SWBST answer collection on top — finite run, finish → Arrange.

**Architecture:** All new code is additive, in `SummaRace.Features.Race.Endless`. Exactly ONE guarded edit touches their code (a 1-line early-out in `TrackManager.SpawnCoinAndPowerup`). A new `EndlessRaceDirector` MonoBehaviour lives only in the scene copy `MainSummaRace.unity`; it flips the flag, skips their Loadout/FTUE, subscribes to `TrackManager.newSegmentCreated` to place 5 SWBST answer gates + a FINISH gate along the generated track, resolves picks via trigger pickups, builds a `RaceResult`, and exits to Arrange through `SceneLoader`. Their original `Main.unity` and the whole package stay pristine and playable.

**Tech Stack:** Unity 6 / URP, Addressables (theirs), TextMeshPro, PrimeTween, MCP For Unity for scene edits + play-mode verification.

## Global Constraints

- **Branch `experiment/endless-override-2` NEVER ships** — it contains com.unity.ads/analytics/purchasing (banned by GDD). Decontamination is a separate, later plan. Do not remove those packages in this plan; do not build APKs from this branch.
- **Do not modify any Trash Dash script except the single `TrackManager.cs` guard defined in Task 2.** No edits to `Main.unity`, `Start.unity`, `Shop.unity`, their prefabs, or their Addressables groups.
- **Original `Main.unity` must keep playing exactly as before** (coins, powerups, loadout, game over) — the flag defaults to `false` and is only set by the director, which exists only in the copy.
- Their classes are in the **global namespace** and `GameManager` collides with ours: always use `global::GameManager` for theirs and fully qualify ours (`SummaRace.Core.GameManager`) — do NOT add `using SummaRace.Core;` in the new scripts (see commit e4de43e note).
- House rules apply to new code: PascalCase types, `_camelCase` privates, `[SerializeField] private`, no hard-coded scene names/strings/numbers where a Constants class exists, null-check all SummaRace singletons so the scene plays directly in-editor (TDD §13).
- Unity Editor is open with MCP: use `mcp__UnityMCP__*` tools; never run `-batchmode` CLI; after adding a `.cs`, refresh + verify the type exists in `Assembly-CSharp` before scene work (known incremental-compiler gotcha).
- No test assemblies exist in this project (no asmdefs by design) — "tests" in this plan are **play-mode verifications through MCP** with concrete expected console/scene outcomes. Do each verification before its commit.
- Commit after every task (small commits, per owner's standing instructions).

## Design decisions (locked with owner, 2026-07-17)

1. Endless Runner is **already imported** on this branch (commit e4de43e, 663 files at Assets root). No re-import. It doesn't appear in Package Manager → My Assets because it came from the local disk copy, not the Asset Store.
2. Run is **finite: 5 gates then finish** → Arrange. Obstacles/lives/game-over stay as-is for now (owner accepted; "never punish" reconciliation happens at finalization).
3. Their coins/premium/powerups are **suppressed, not deleted** (one guarded early-out). Their character, themes, tutorial data, shop, missions all stay untouched.
4. Wrong pick = feedback + gold-highlight the correct card as it passes; the gate auto-resolves a few metres later. First pick per gate is what counts for stars (GDD §4.2), same as the old race.
5. A gate passed with no pick counts as first-pick-wrong (simple, never blocks the learner).
6. Patrol/danger meter from the old race is **not** ported in this plan — their obstacles are the pressure. `RaceResult.timesCaught = 0`.

## Reference: the code you are integrating with

**Their side (global namespace, `Assets/Scripts/`):**
- `TrackManager` (`Tracks/TrackManager.cs`) — singleton `TrackManager.instance`. Key members: `laneOffset` (float), `worldDistance` (float, metres travelled), `isMoving`, `StopMove()`, `characterController`, event `System.Action<TrackSegment> newSegmentCreated` (fires each time a segment spawns ahead), `SpawnCoinAndPowerup(TrackSegment)` (coins + premium + powerups all spawn here and ONLY here — line ~576).
- `TrackSegment` — `worldLength` (float), `GetPointAtInWorldUnit(float dist, out Vector3 pos, out Quaternion rot)` (world-space point `dist` metres into the segment), `transform` (parent gates here so segment cleanup destroys them).
- `CharacterCollider` (`Characters/CharacterCollider.cs`) — the box collider that runs into things; our pickups detect it by component. Coins layer is 8, obstacles 9, powerups 10 — our gates stay on **Default** (layer 8 would enter their coin branch and NRE on the missing `Coin` component).
- `LoadoutState` — `public void StartGame()` switches state machine to "Game" (this is how we skip their menu).
- `GameState` — public UI fields we hide at runtime: `coinText`, `premiumText`, `scoreText`, `distanceText`, `multiplierText` (all `UnityEngine.UI.Text`).
- `PlayerData.instance` — `tutorialDone` (bool), `ftueLevel` (int); set `tutorialDone = true`, `ftueLevel = 2` to skip FTUE/tutorial runs.
- `MusicPlayer.instance` — DontDestroyOnLoad; must be silenced when we leave for Arrange or their music plays over our scenes.

**Our side (`Assets/_Game/Scripts/`):**
- `SummaRace.Data.StoryLoader.Load("s01_easy")` → `StoryData` with `elements[5]` (`type` "SOMEBODY".."THEN", `correct`, `distractors[2]`) and `mission.checkpointSpacing` (s01 = 45).
- `SummaRace.Core.GameManager.Instance` — `CurrentStory`, `SetRaceResult(RaceResult)`.
- `SummaRace.Core.EventBus.Raise(...)` — payloads `ElementCollected { elementIndex, wasCorrect }`, `RaceCompleted { result }` (in `Core/GameEvents.cs`).
- `SummaRace.Core.RaceResult` — `collectedPieces[5]`, `firstPickCorrect[5]`, `timesCaught`, `runSeconds`.
- `SummaRace.Core.AudioManager.Instance.PlaySfx(...)` + `SummaRace.Constants.AudioKeys` (`SfxCollect`, `SfxNotQuite`, `SfxStar`).
- `SummaRace.Core.SceneLoader.Instance.Load(...)` + `SummaRace.Constants.SceneNames`.
- `SummaRace.Constants.SwbstPalette.DeepForIndex(int)` → gate pill colors.
- PrimeTween: `using PrimeTween;` then `Tween.PositionY / Tween.Scale / Tween.PunchScale` (see old `RaceController.CollectCorrect` for the exact fly-up idiom, `RaceController.cs:279-288`).

## File structure

- Create: `Assets/_Game/Scripts/Features/Race/Endless/EndlessRaceMode.cs` — static flag, nothing else.
- Create: `Assets/_Game/Scripts/Features/Race/Endless/EndlessOptionPickup.cs` — dumb trigger component on each answer card.
- Create: `Assets/_Game/Scripts/Features/Race/Endless/EndlessRaceDirector.cs` — everything else (mode flag lifecycle, loadout skip, gate spawning, pick resolution, HUD, finish). One scene-specific conductor, same "controller owns the feature" shape as `RaceController`.
- Modify: `Assets/Scripts/Tracks/TrackManager.cs` (~line 576) — one guarded early-out.
- Modify: `Assets/_Game/Scripts/Constants/SceneNames.cs` — add `RaceEndless`.
- Modify: `Assets/_Game/Scripts/Features/Reader/ReaderController.cs:197` — route to the new scene.
- Scene: `Assets/Scenes/MainSummaRace.unity` — copy of their `Main.unity` + one added GameObject.

---

### Task 1: Scene copy + SceneNames constant

**Files:**
- Create: `Assets/Scenes/MainSummaRace.unity` (asset copy, via MCP)
- Modify: `Assets/_Game/Scripts/Constants/SceneNames.cs`

**Interfaces:**
- Produces: scene `"MainSummaRace"` in Build Settings; constant `SceneNames.RaceEndless = "MainSummaRace"` used by Tasks 4–6.

- [ ] **Step 1: Copy the scene and register it in Build Settings** (MCP `execute_code`):

```csharp
AssetDatabase.CopyAsset("Assets/Scenes/Main.unity", "Assets/Scenes/MainSummaRace.unity");
var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/MainSummaRace.unity", true));
EditorBuildSettings.scenes = scenes.ToArray();
AssetDatabase.SaveAssets();
```

- [ ] **Step 2: Add the constant** — in `SceneNames.cs`, after `public const string Race = "Race";` add:

```csharp
        public const string RaceEndless = "MainSummaRace"; // experiment: Trash Dash base + SWBST gates
```

- [ ] **Step 3: Verify** — refresh Unity (MCP `refresh_unity` with compile), then open `Assets/Scenes/MainSummaRace.unity` (MCP `manage_scene`), enter play mode: the copy must behave exactly like the original Main (loadout screen appears, Run starts a normal Trash Dash game with coins). Exit play mode. Check console for new errors (`read_console`) — none expected beyond their usual warnings.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scenes/MainSummaRace.unity Assets/Scenes/MainSummaRace.unity.meta Assets/_Game/Scripts/Constants/SceneNames.cs ProjectSettings/EditorBuildSettings.asset
git commit -m "EXP3-1: scene copy MainSummaRace + RaceEndless scene constant"
```

---

### Task 2: EndlessRaceMode flag + the single TrackManager guard

**Files:**
- Create: `Assets/_Game/Scripts/Features/Race/Endless/EndlessRaceMode.cs`
- Modify: `Assets/Scripts/Tracks/TrackManager.cs` (method `SpawnCoinAndPowerup`, ~line 576)

**Interfaces:**
- Produces: `SummaRace.Features.Race.Endless.EndlessRaceMode.Active` (static bool, default false). Task 3's director sets/clears it.

- [ ] **Step 1: Write the flag class** — `EndlessRaceMode.cs`:

```csharp
namespace SummaRace.Features.Race.Endless
{
    /// <summary>
    /// Set true by EndlessRaceDirector (MainSummaRace.unity only) while that scene runs.
    /// TrackManager checks it to suppress coins/premium/powerups — the SWBST answer
    /// gates become the only collectibles. Default false: the original Main.unity
    /// and the whole Trash Dash package behave exactly as shipped.
    /// </summary>
    public static class EndlessRaceMode
    {
        public static bool Active;
    }
}
```

- [ ] **Step 2: Add the guard** — in `TrackManager.cs`, `SpawnCoinAndPowerup`. Old:

```csharp
    public IEnumerator SpawnCoinAndPowerup(TrackSegment segment)
    {
        if (!m_IsTutorial)
        {
```

New:

```csharp
    public IEnumerator SpawnCoinAndPowerup(TrackSegment segment)
    {
        // SummaRace: in SWBST-collection mode the answer gates are the only collectibles.
        if (SummaRace.Features.Race.Endless.EndlessRaceMode.Active) yield break;
        if (!m_IsTutorial)
        {
```

This is the ONLY edit to their code in the whole plan. It kills coins, premium fishbones, and powerups in one place (they all spawn inside this method and nowhere else).

- [ ] **Step 3: Verify compile + no behavior change** — MCP refresh with compile; confirm `SummaRace.Features.Race.Endless.EndlessRaceMode` exists in `Assembly-CSharp` (MCP `execute_code`: `System.Type.GetType("SummaRace.Features.Race.Endless.EndlessRaceMode, Assembly-CSharp") != null` must print true; if the type never appears despite a clean file, apply the CLAUDE.md stale-tracking fix: delete the .cs+.meta, refresh, re-add). Then play the ORIGINAL `Main.unity`: coins must still spawn (flag is false).

- [ ] **Step 4: Commit**

```bash
git add Assets/_Game/Scripts/Features/Race/Endless/ Assets/Scripts/Tracks/TrackManager.cs
git commit -m "EXP3-2: EndlessRaceMode flag + lone TrackManager collectible guard"
```

---

### Task 3: EndlessOptionPickup + EndlessRaceDirector

**Files:**
- Create: `Assets/_Game/Scripts/Features/Race/Endless/EndlessOptionPickup.cs`
- Create: `Assets/_Game/Scripts/Features/Race/Endless/EndlessRaceDirector.cs`

**Interfaces:**
- Consumes: `EndlessRaceMode.Active` (Task 2); their `TrackManager`/`TrackSegment`/`LoadoutState`/`GameState`/`PlayerData`/`CharacterCollider`/`MusicPlayer` APIs listed in the Reference section.
- Produces: `EndlessRaceDirector.Instance` (static), `public void OnPickupHit(EndlessOptionPickup pickup)` — called by the pickup component. Serialized fields `worldCardSprite` (Sprite), `worldLabelFont` (TMP_FontAsset) wired in Task 4.

- [ ] **Step 1: Write `EndlessOptionPickup.cs`**

```csharp
using UnityEngine;

namespace SummaRace.Features.Race.Endless
{
    /// <summary>
    /// Trigger on one answer card of a SWBST gate (or the FINISH card).
    /// Detects the Trash Dash character by its CharacterCollider component —
    /// no tag/layer assumptions about their prefabs.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class EndlessOptionPickup : MonoBehaviour
    {
        public int elementIndex;
        public bool isCorrect;
        public bool isFinishGate;

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<CharacterCollider>() == null) return;
            if (EndlessRaceDirector.Instance != null)
                EndlessRaceDirector.Instance.OnPickupHit(this);
        }
    }
}
```

- [ ] **Step 2: Write `EndlessRaceDirector.cs`**

```csharp
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
            var loadout = Object.FindFirstObjectByType<LoadoutState>();
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
            var gs = Object.FindFirstObjectByType<GameState>();
            if (gs == null) return;
            if (gs.coinText != null) gs.coinText.gameObject.SetActive(false);
            if (gs.premiumText != null) gs.premiumText.gameObject.SetActive(false);
            if (gs.scoreText != null) gs.scoreText.gameObject.SetActive(false);
            if (gs.distanceText != null) gs.distanceText.gameObject.SetActive(false);
            if (gs.multiplierText != null) gs.multiplierText.gameObject.SetActive(false);
        }
    }
}
```

- [ ] **Step 3: Compile-verify** — MCP refresh with compile; verify BOTH types exist in `Assembly-CSharp` (same `System.Type.GetType` check as Task 2 for `SummaRace.Features.Race.Endless.EndlessRaceDirector` and `...EndlessOptionPickup`). Console must be error-free.

- [ ] **Step 4: Commit**

```bash
git add Assets/_Game/Scripts/Features/Race/Endless/
git commit -m "EXP3-3: EndlessRaceDirector + EndlessOptionPickup (SWBST gates on Trash Dash track)"
```

---

### Task 4: Wire the director into the scene copy

**Files:**
- Modify: `Assets/Scenes/MainSummaRace.unity` (via MCP only — never hand-edit scene YAML)

**Interfaces:**
- Consumes: `EndlessRaceDirector` (Task 3), scene copy (Task 1).
- Produces: a playable MainSummaRace scene that skips Loadout and shows SWBST gates.

- [ ] **Step 1: Add the director GameObject** — open `MainSummaRace.unity` (MCP `manage_scene`), create root GameObject `EndlessRaceDirector`, add component `SummaRace.Features.Race.Endless.EndlessRaceDirector` (MCP `manage_gameobject`/`manage_components`).

- [ ] **Step 2: Wire the two visual fields** via MCP `execute_code` with `SerializedObject` (the established pattern):

```csharp
var director = UnityEngine.Object.FindFirstObjectByType<SummaRace.Features.Race.Endless.EndlessRaceDirector>();
var so = new SerializedObject(director);
// Same card sprite the old race used (RaceController.worldCardSprite in Race.unity,
// GUID e96be3c2e6d907741939a389219ea24f resolved to this asset):
var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Plugins/Hyper_Casual_UI/Sprites/Panel_Sprites/Rectangle 356.png");
var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Art/Fonts/TMP/Fredoka-SemiBold SDF.asset");
so.FindProperty("worldCardSprite").objectReferenceValue = sprite;
so.FindProperty("worldLabelFont").objectReferenceValue = font;
so.ApplyModifiedProperties();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
```

(Grey-box fallback exists, so a missing sprite is not a blocker — but wire it.)

- [ ] **Step 3: Play-mode verification (the big one)** — enter play mode in `MainSummaRace.unity` and confirm, via MCP screenshot + `read_console` + `find_gameobjects`:
  1. Loadout screen auto-skips (~1s) and the run starts.
  2. NO coins/premium/powerups spawn anywhere (the guard works).
  3. `SwbstGate_0` exists ahead; 3 white cards + colored pill visible and readable.
  4. Running into a card logs no errors and the gate resolves (banner advances to `2/5`).
  5. **Trigger check:** if `OnPickupHit` never fires (physics-matrix surprise), fall back per design: give the card trigger a kinematic `Rigidbody` (`isKinematic = true`) in `BuildCard`'s pickup branch and retest before touching anything else.
  6. Their obstacles still hit/kill normally (untouched behavior).
  7. Original `Main.unity` still plays with coins (flag cleanly resets — check after exiting the copy).

- [ ] **Step 4: Commit**

```bash
git add Assets/Scenes/MainSummaRace.unity
git commit -m "EXP3-4: director wired into MainSummaRace — gates live on the Trash Dash track"
```

---

### Task 5: Full gate-flow verification (correct / wrong / miss / finish)

**Files:** none new — this task is pure play-mode verification and any fixes it forces.

**Interfaces:**
- Consumes: everything above.
- Produces: a verified in-scene loop ending at Arrange with a correct `RaceResult`.

- [ ] **Step 1: Correct pick** — collect a correct card: fly-up pop, "You got it!", `SfxCollect` (if Core is absent in direct play, silence is fine), banner advances, gate destroyed.

- [ ] **Step 2: Wrong pick** — hit a distractor: "Not quite" feedback, wrong card gone, correct card gold + enlarged, gate auto-resolves ~5m later, banner advances.

- [ ] **Step 3: Miss** — pass one gate touching nothing (weave between cards if possible; otherwise temporarily narrow `trigger.size.x` in a play-mode-only test): gate resolves as missed, banner advances, no stall.

- [ ] **Step 4: Finish** — after gate 5, FINISH card appears ~30m later; hitting it stops the track, waits 2.2s, loads Arrange. In direct-editor play, Arrange must come up on its s01 fallback with the race result reflected (`GameManager` null-checks make this work; if Core singletons are absent the `SceneManager.LoadScene` fallback fires).

- [ ] **Step 5: Death mid-run** — deliberately die to obstacles before gate 5: their GameOver popup appears (accepted for now). Note any weirdness in the commit message; do NOT fix their flow in this plan.

- [ ] **Step 6: Commit** (only if fixes were needed; otherwise fold into Task 6's commit)

```bash
git add -A
git commit -m "EXP3-5: gate flow verified — correct/wrong/miss/finish all resolve"
```

---

### Task 6: Route the game loop through the new race + document

**Files:**
- Modify: `Assets/_Game/Scripts/Features/Reader/ReaderController.cs:197`
- Modify: `CLAUDE.md` (build-state table)

**Interfaces:**
- Consumes: `SceneNames.RaceEndless` (Task 1), verified scene (Task 5).

- [ ] **Step 1: Reroute the Reader** — line 197, old:

```csharp
                SceneLoader.Instance.Load(SceneNames.Race);
```

New:

```csharp
                SceneLoader.Instance.Load(SceneNames.RaceEndless); // experiment: Trash Dash base race
```

- [ ] **Step 2: Full-loop verification** — play from `Boot.unity`: Boot → MainMenu → StorySelect → Reader (read s01 pages) → **MainSummaRace run** → 5 gates → FINISH → Arrange → Summary → Results. Confirm Results stars match the first-pick outcomes you produced during the run, and no Trash Dash music bleeds into Arrange.

- [ ] **Step 3: Update CLAUDE.md** — add a build-state row summarizing this milestone, e.g.:

```markdown
| **EXP3 (branch experiment/endless-override-2) — Trash Dash IS the race**: their game untouched (one guarded line in TrackManager); `MainSummaRace.unity` copy + `EndlessRaceDirector` = SWBST gates replace coins/powerups, finite 5-gate run → Arrange; Reader routes to `SceneNames.RaceEndless`; original Main.unity still plays their full game. Branch never ships (ads/IAP present). | ✅ done (verify in playtest) |
```

- [ ] **Step 4: Commit + push**

```bash
git add Assets/_Game/Scripts/Features/Reader/ReaderController.cs CLAUDE.md
git commit -m "EXP3-6: game loop routes through the endless-runner race; docs updated"
git push -u origin experiment/endless-override-2
```

---

## Explicitly out of scope (later plans)

- Replacing their character with ours ("once we finalize" — owner).
- Removing ads/analytics/IAP packages and other decontamination (mandatory before any build).
- Patrol/danger meter port, "never punish" reconciliation of their lives/game-over.
- Their HUD reskin beyond hiding the currency counters; briefing popup; narration hooks.
- Deleting the now-superseded park/road race (`Race.unity` + `RaceController`) — keep until the owner declares the experiment the winner.
