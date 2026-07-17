# Endless Race Gap Closure Implementation Plan (v2 — docs-grounded)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring the endless-race experiment (complete + reviewed at cc87a52) up to the documents: TDD §11 (race deep dive), the validated mockups (15/16: low grounded card row, yellow Collect pill, hourglass timer), and the GDD non-negotiables (never punish, never a dead end). v1 of this plan was written before re-reading the docs; v2 supersedes it after reviewing TDD §11.1–11.7, §13, mockups 15/16, their `WorldCurver.cs` + `CurvedCode.cginc`, and the owner's playtest screenshot (cards floating in the sky at distance; game over on obstacle death).

**Architecture:** unchanged — ALL work is additive in `SummaRace.Features.Race.Endless` (+`GameText`); the 2-line TrackManager guard remains the only Trash Dash edit ever. New mechanisms use their public APIs (`currentLife/maxLife`, `maxSpeed/minSpeed/speed`, `TrackManager.segments`, global shader float `_CurveStrength`) plus ONE cached reflection write (`m_Speed`, for the TDD-mandated boost — runtime state only, no code edits).

**Tech Stack:** Unity 6 / URP, MCP For Unity, PrimeTween.

## Global Constraints

- Branch `experiment/endless-override-2` never ships (decontamination list lives in `.superpowers/sdd/progress.md`).
- Zero new edits to any Trash Dash file; original `Main.unity` must keep playing as shipped.
- Their classes are global-namespace; `GameManager` collides: fully qualify SummaRace types, no `using SummaRace.Core;`.
- Learner strings in `GameText`, tuning numbers in `GameRules` (reuse existing: `BoostSeconds=2`, `SlowSeconds=1.5`, `DangerOnWrong`, `DangerRelief`, `DangerMax`, `DangerAfterCaught` — all already used by the old `RaceController`).
- New-file .meta gotcha: guid-only stubs appear for .cs files created outside Unity — write the canonical MonoImporter stanza manually, keep the guid, trailing newline.
- MCP editor rules as before (no `-batchmode`; `GameState.Resume()` in probes; state probes over console silence; owner may be at the machine).
- Pre-existing dirty tree: `git add` named paths only; every commit ends `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.

## Doc findings this plan implements

| Source | Requirement | Current state |
|---|---|---|
| Owner screenshot + F31 lesson | Gate cards must sit ON the curved road at every distance (their `CurvedCode.cginc` drops clip-Y by `_CurveStrength·d²`, scene value 0.005 → ~18 m visual drop at 80 m; our unbent cards float in the sky) | ❌ floating |
| GDD D7 / TDD §13 | Never punish, never game-over, never dead end | ❌ their 3-lives death popup reachable |
| TDD §11.4 | Correct pick → speed boost + `sfx_boost` + danger −15 | ❌ no boost, no danger |
| TDD §11.4 | Wrong pick → slow + `sfx_not_quite` + danger +10, **correct card glows and MUST still be collected to advance** | ❌ wrong/miss auto-advances after 5 m |
| TDD §11.5 | DangerLevel model + patrol chaser (visual only) + friendly caught beat; `timesCaught` is a **research log field** (§12.1) | ❌ absent; `timesCaught` hard-coded 0 |
| TDD §11.1 / mockups 15–16 | RaceHUD = "Collect:" banner (yellow pill, dark text) + timer chip + danger visual; cards read as a tidy dark row low over the track | ❌ plain white text banner; white cards |
| Owner feedback | De-clutter Trash Dash menus/panels, mask loadout flash | ✅ done (EXP3-10/10b) |

**Explicitly NOT here (later):** per-option card icons (mockup 17 — needs data-model addition, researcher input), character/patrol final art beyond the existing PatrolCop prefab, race music swap, briefing popup, decontamination, Phase G.

## Reference (verified)

- Their curve: `Assets/Shaders/CurvedCode.cginc:38` → `o.vertex.y -= _CurveStrength * dist * dist * _ProjectionParams.x`; scene `WorldCurver.curveStrength = 0.005` (updates the global every frame). World-unit dip at view-depth d: `strength · d² · tan(vFOV/2)` (same derivation as our proven `CurveDip.cs`, F31).
- `TrackManager` (public): `segments` (List<TrackSegment>, live ahead-of-player), `worldDistance`, `speed`, `minSpeed`, `maxSpeed`, `laneOffset`, `characterController`; protected `m_Speed` (reflection target for boost only).
- `CharacterInputController` (public): `currentLife`, `maxLife`, `ChangeLane(int)`, `characterCollider`.
- `StoryData.mission`: `playerSpeed`, `checkpointSpacing`, `startingDanger`, `dangerPerSecond`.
- Ours: `_Game/Prefabs/PatrolCop.prefab` (Humanoid cop + PatrolAnimator, Running bool — F27b), `GameRules` constants above, `AudioKeys.SfxBoost/SfxNotQuite/SfxCaught/SfxCollect/SfxStar`.
- Director today (`EndlessRaceDirector.cs`, ~500 lines after EXP3-10b): distance-scheduled gates pre-placed via `OnNewSegment`, miss-grace auto-advance, restart detection, life of its own HUD canvas. Task 3 rewires the scheduling — read the file fully before editing.

## File structure

- Create: `Assets/_Game/Scripts/Features/Race/Endless/EndlessCurveDip.cs` — one job: glue a transform to the shader curve.
- Modify: `Assets/_Game/Scripts/Features/Race/Endless/EndlessRaceDirector.cs` — scheduling rework, consequences, danger/patrol, HUD restyle. It will reach ~700 lines; acceptable for the scene conductor, mirrors `RaceController` precedent. Do not split without owner direction.
- Modify: `Assets/_Game/Scripts/Constants/GameText.cs` — race strings.
- Modify: `Assets/Scenes/MainSummaRace.unity` — wire the `patrolPrefab` serialized field (MCP SerializedObject, PatrolCop prefab guid).

---

### Task 1: ✅ COMPLETE — chrome de-clutter + loadout mask (EXP3-10 `1b83671`, EXP3-10b pause-button guard)

Recorded here for continuity; do not redo. Remaining G-numbering starts at Task 2.

---

### Task 2: Curve-glue the gates (cards stand on the bent road at every distance)

**Files:**
- Create: `Assets/_Game/Scripts/Features/Race/Endless/EndlessCurveDip.cs`
- Modify: `EndlessRaceDirector.cs` (attach to gate/finish roots)

**Interfaces:**
- Produces: `EndlessCurveDip` MonoBehaviour; director adds it to every gate root, finish root, and (Task 3) re-present cards.

- [ ] **Step 1: Write `EndlessCurveDip.cs`:**

```csharp
using UnityEngine;

namespace SummaRace.Features.Race.Endless
{
    /// <summary>
    /// Glues our uncurved gate visuals (kit sprites + TMP) to the Trash Dash curved
    /// world: their CurvedCode.cginc drops clip-space Y by _CurveStrength·d², so at
    /// 80 m a straight-line card floats ~18 m above the visually-bent road. Reproduce
    /// the same dip in world units each frame (dip→0 at pickup range, so triggers
    /// stay honest — F31 lesson).
    /// </summary>
    public class EndlessCurveDip : MonoBehaviour
    {
        private static readonly int CurveStrengthId = Shader.PropertyToID("_CurveStrength");

        private float _baseLocalY;
        private Transform _cam;
        private float _frustumScale; // tan(vertical FOV / 2)

        private void Start()
        {
            _baseLocalY = transform.localPosition.y;
            var cam = Camera.main;
            _cam = cam != null ? cam.transform : null;
            _frustumScale = cam != null
                ? Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad)
                : 0.6f;
        }

        private void LateUpdate()
        {
            if (_cam == null) return;
            float d = Mathf.Max(0f, transform.position.z - _cam.position.z);
            float dip = Shader.GetGlobalFloat(CurveStrengthId) * d * d * _frustumScale;
            var p = transform.localPosition;
            p.y = _baseLocalY - dip;
            transform.localPosition = p;
        }
    }
}
```

(Write the canonical MonoImporter .meta per the gotcha.)

- [ ] **Step 2: Attach in the director** — in `PlaceAnswerGate` and `PlaceFinishGate`, after `root.SetPositionAndRotation(...)`, add `root.gameObject.AddComponent<EndlessCurveDip>();`.

- [ ] **Step 3: Verify in play** — screenshots at gate distance ~60–80 m (cards must hug the road exactly like their barrier obstacles do — compare against an obstacle in the same frame) AND at ≤10 m (cards standing on the road, unchanged pickup). If the far cards dip BELOW the road instead of onto it, the sign convention is inverted — flip `-dip` to `+dip`, re-verify, and record which sign won. Probe a correct pick still registers.

- [ ] **Step 4: Commit** — `git add` the two script files + new .meta; message `EXP3-15: gates glued to the world curve — cards sit on the road at every distance`.

---

### Task 3: Never-punish life pinning (death unreachable)

**Files:** Modify `EndlessRaceDirector.cs`.

- [ ] **Step 1:** In `Update()`, immediately after the restart-detection block:

```csharp
            // Never punish (GDD D7): obstacle hits still stumble + blink (their
            // friendly 2s invincibility beat) but can never stack to a game over.
            var runner = track.characterController;
            if (runner != null && runner.currentLife < runner.maxLife)
                runner.currentLife = runner.maxLife;
```

- [ ] **Step 2: Verify** — no cheats, eat 4+ obstacle hits: stumble+blink each time, no death popup ever, `currentLife == maxLife` after each hit settles; `Main.unity` death still works (behavior is director-gated state, but confirm).
- [ ] **Step 3: Commit** — `EXP3-16: never-punish — lives pinned, obstacle hits stumble but never game-over`.

---

### Task 4: TDD §11.4 faithful collection — sequential gates, boost/slow, must-still-collect

This is the core rework. Replace the "pre-placed by fixed distance + miss-grace auto-advance" model with **sequential scheduling + re-presentation**, so a wrong pick or a missed gate re-offers the glowing correct card until the learner physically collects it — "they always leave holding the 5 correct pieces" (TDD §11.4), with zero dead ends.

**Files:** Modify `EndlessRaceDirector.cs`.

**New model:**
- Maintain `private readonly List<(TrackSegment seg, float start, float end)> _spans = new();` — appended in `OnNewSegment` (existing `_spawnedDistance` bookkeeping), entries with destroyed segments pruned lazily (`seg == null`).
- `private float _pendingGateDistance = -1f; private int _pendingElement; private bool _pendingIsRepresent;` — one pending placement at a time. `TryPlacePending()` runs in both `OnNewSegment` and `Update()`: find the span covering `_pendingGateDistance`, place there (`GetPointAtInWorldUnit(dist - span.start, ...)`), clear pending. If the target distance is already behind the farthest span, clamp to the farthest span's end minus 2 m (never unplaceable).
- Gate 0: schedule at `FirstGateDistance` (80). On **resolve** of gate i (correct card collected — first-hit or re-present): if i < 4 schedule gate i+1 at `track.worldDistance + Spacing`; if i == 4 schedule FINISH at `+FinishGap` (re-use `PlaceFinishGate` through the same pending mechanism, `_pendingElement = 5`).
- **Correct first hit:** existing celebration + destroy gate + advance, PLUS TDD consequences: `sfx_boost` (`AudioKeys.SfxBoost`), danger −`GameRules.DangerRelief` (Task 5 owns `_danger`; guard with `#region` order — Task 4 introduces the field at 0-effect if Task 5 not yet merged, see note below), and a **speed boost** via one cached reflection write:

```csharp
        private static readonly System.Reflection.FieldInfo SpeedField =
            typeof(TrackManager).GetField("m_Speed",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        private void BoostSpeed(TrackManager track)
        {
            // TDD §11.4: correct pick = boost. Their protected m_Speed self-clamps to
            // maxSpeed in Update, so an over-write is safe; reflection is runtime-only
            // state — their code stays untouched.
            if (SpeedField != null)
                SpeedField.SetValue(track, Mathf.Min(track.maxSpeed, track.speed * 1.35f));
        }
```

- **Wrong first hit:** record `firstPickCorrect[i] = false` (unchanged), `sfx_not_quite`, slow (below), danger +`GameRules.DangerOnWrong`, destroy the WHOLE gate root, and schedule a **re-present**: `_pendingIsRepresent = true`, `_pendingGateDistance = track.worldDistance + RepresentGap` (const 18f). A re-present is ONE gold glowing card, center lane, `CardY`, same element, `isCorrect = true`, with `EndlessCurveDip`, plus the SWBST pill above it; collecting it resolves the gate (no first-pick change, normal celebration minus boost).
- **Missed gate/re-present** (passed by `MissGrace` without any hit): record first-pick false if unrecorded, destroy the root, schedule another re-present the same way. Track `_representCount`; after 3 consecutive un-collected re-presents of the same element, auto-resolve and move on (anti-frustration floor — the card is center-lane and glowing, so 3 misses means the learner is deliberately dodging; log it in the report).
- **Slow on wrong** (public-API clamp, from v1):

```csharp
        private float _slowTimer;
        private float _savedMaxSpeed = -1f;
        // in HitWrong:
            if (_savedMaxSpeed < 0f) _savedMaxSpeed = track.maxSpeed;
            track.maxSpeed = Mathf.Max(track.minSpeed, track.speed * 0.6f);
            _slowTimer = SummaRace.Constants.GameRules.SlowSeconds;
        // in Update:
            if (_slowTimer > 0f)
            {
                _slowTimer -= Time.deltaTime;
                if (_slowTimer <= 0f && _savedMaxSpeed > 0f)
                { track.maxSpeed = _savedMaxSpeed; _savedMaxSpeed = -1f; }
            }
```

- The old `_gateDistances`/`_gatesPlaced` fixed-schedule fields and the auto-advance miss-grace branch are REPLACED by this model — delete what the new model obsoletes (keep restart detection, life pinning, `_lastWorldDistance`). `_gateRoots` shrinks to a single `_activeGateRoot` + `_activeElement` since only one gate exists at a time.

Note on Task 5 coupling: introduce `private float _danger;` and the +/− adjustments in THIS task but with no behavior attached (no UI, no caught check) — Task 5 activates it. This keeps each task compiling and testable alone.

- [ ] **Step 1:** Implement the model above. Read the whole director first; keep restart detection, life pinning, curve dip, HUD, finish flow intact.
- [ ] **Step 2: Verify in play** (probes: `ChangeLane`, `CheatInvincible`, reflection reads):
  - Correct-only run: 5 gates appear one at a time, spacing ≈ `checkpointSpacing`, boost measurable (`track.speed` jumps ~1.35× then decays to normal accel curve), FINISH → Arrange, stars 5/5 path intact.
  - Wrong pick at gate 2: slow measurable, gate replaced by ONE gold center card ~18 m ahead, collecting it advances to gate 3; `firstPickCorrect[1] == false`.
  - Dodge the re-present twice, collect the third: still advances; dodge three: auto-resolves.
  - Restart mid-pending (die → Run Again → reload): fresh director, gate 0 back at 80 m.
- [ ] **Step 3: Commit** — `EXP3-17: TDD §11.4 collection — sequential gates, boost/slow, glowing correct card must be collected`.

---

### Task 5: TDD §11.5 danger, patrol chaser, friendly caught beat, timer

**Files:** Modify `EndlessRaceDirector.cs`; modify `Assets/Scenes/MainSummaRace.unity` (wire `patrolPrefab`).

- [ ] **Step 1: Danger model** — activate the `_danger` field: `_danger = _story.mission.startingDanger` at run start; `_danger += _story.mission.dangerPerSecond * Time.deltaTime` each running frame; clamp 0..`GameRules.DangerMax`; at max → `StartCoroutine(CaughtRoutine())`:

```csharp
        private int _timesCaught;
        private bool _caughtBusy;

        private System.Collections.IEnumerator CaughtRoutine()
        {
            if (_caughtBusy) yield break;
            _caughtBusy = true;
            _timesCaught++;
            _danger = SummaRace.Constants.GameRules.DangerAfterCaught;
            if (SummaRace.Core.AudioManager.Instance != null)
                SummaRace.Core.AudioManager.Instance.PlaySfx(SummaRace.Constants.AudioKeys.SfxCaught);
            ShowFeedback(SummaRace.Constants.GameText.RaceCaughtFeedback, new Color(1f, 0.6f, 0.4f));
            // Patrol lunges to the runner's shoulder for the friendly tag, then falls back.
            yield return new WaitForSeconds(1.5f);
            _caughtBusy = false;
        }
```

The run NEVER stops or fails (TDD §11.5). `FinishRoutine` sets `result.timesCaught = _timesCaught` (replacing the hard-coded 0) — this restores the §12.1 research field.

- [ ] **Step 2: Patrol chaser (visual only)** — add `[SerializeField] private GameObject patrolPrefab;`. At run start instantiate it (fallback: red capsule primitive, grey-box per TDD §11.7) parented to `track.characterController.transform` at local `(0, 0, -patrolDistance)`; each frame `patrolDistance = Mathf.Lerp(9f, 2.2f, _danger / GameRules.DangerMax)` (smoothed with `Mathf.MoveTowards`, ~3 m/s); during `CaughtRoutine` tween it to −1.2 briefly (PrimeTween). Set its Animator `Running` bool true if an Animator exists. Wire `patrolPrefab` in the scene via MCP SerializedObject → `Assets/_Game/Prefabs/PatrolCop.prefab`.
- [ ] **Step 3: Danger vignette + timer chip** — on the existing HUD canvas: a full-screen amber `UnityEngine.UI.Image` (raycastTarget false), `color.a = (_danger / GameRules.DangerMax) * 0.30f`; a timer chip TMP top-left under the banner showing elapsed `((int)(Time.time - _runStartTime)) + "s"` (mockups 15/16 show an hourglass chip; text-only is fine now, icon in the styling pass).
- [ ] **Step 4: Verify in play** — danger climbs (probe `_danger`), wrong pick bumps it, correct pick relieves it, patrol visibly closes as danger rises, caught beat at max (feedback + patrol lunge + `_timesCaught` increments + run continues), Results path receives real `timesCaught`. Patrol never blocks picks (it is collider-free — strip colliders on instantiation).
- [ ] **Step 5: Commit** — `EXP3-18: danger + patrol chaser + friendly caught (TDD §11.5); timesCaught research field restored`.

---

### Task 6: Mockup HUD + card look, learner strings → GameText

**Files:** Modify `EndlessRaceDirector.cs`, `Assets/_Game/Scripts/Constants/GameText.cs`.

- [ ] **Step 1: GameText constants** (match file style):

```csharp
        public const string RaceCollectPrefix = "Collect: ";
        public const string RaceRunToFinish = "Run to the FINISH!";
        public const string RaceFinishBanner = "FINISH!";
        public const string RaceCorrectFeedback = "You got it!";
        public const string RaceWrongFeedback = "Not quite — grab the glowing one!";
        public const string RaceCaughtFeedback = "Almost caught! Keep going!";
```

Replace every director literal with these.

- [ ] **Step 2: Banner as mockup pill** — banner TMP gets a parent `UnityEngine.UI.Image` using the kit sprite already wired to `worldCardSprite` (9-sliced), tinted mockup-yellow `new Color(1f, 0.86f, 0.10f)`, dark navy bold text `new Color(0.10f, 0.12f, 0.22f)`, anchored top-LEFT like mockups 15/16 (anchor (0,1), offset ~(40,−100), padding via sizeDelta). Timer chip (Task 5) styled the same, smaller, beneath it.
- [ ] **Step 3: Cards to mockup look** — in the gate/re-present card builds: `cardColor` → dark navy `new Color(0.13f, 0.16f, 0.25f)`, `textColor` → white; keep the gold highlight/gold re-present card as-is (it must contrast). SWBST pill keeps its palette color.
- [ ] **Step 4: Verify** — screenshot vs mockup 15 side-by-side in the report (banner top-left yellow pill, dark card row low over the road); play one gate to confirm readability at speed; strings render identically from GameText.
- [ ] **Step 5: Commit** — `EXP3-19: mockup HUD (yellow Collect pill, timer chip) + navy card row; strings in GameText`.

---

### Task 7: Full-loop regression, docs, push

- [ ] **Step 1: No-cheat full loop from Boot** — Boot → … → Reader → race: eat obstacle hits (stumble only), one wrong pick (slow + re-present + collect the glowing card), let danger reach caught once, finish → Arrange → Summary → Results; stars match first-pick truth; `timesCaught ≥ 1` visible in the flow/log; audio alive in Arrange; zero errors from our code.
- [ ] **Step 2: Original-game check** — `Main.unity` unchanged (loadout, coins, death).
- [ ] **Step 3: CLAUDE.md** — rewrite the EXP3 row to describe the docs-faithful state (curve-glued gates, sequential must-collect gates, danger/patrol/caught, mockup HUD, chrome hidden, never-punish) + deferred list.
- [ ] **Step 4: Commit + push** — `EXP3-20: docs-faithful endless race verified end to end` (CLAUDE.md in this commit) then `git push`.
- [ ] **Step 5: Owner playtest gate** — specifically: watch a far gate hug the road, make a wrong pick and collect the glowing card, get caught once (feel that it's friendly), judge HUD vs mockup 15.
