# F39 — Patrol Chase Redesign + Cop Weapon/Body Fix (Subway-Surfers feel)

**Date:** 2026-07-27
**Branch:** `experiment/endless-override-2`
**Scene:** `Assets/Scenes/MainSummaRace.unity`
**Primary files:** `Assets/_Game/Scripts/Features/Race/Endless/EndlessRaceDirector.cs`,
`Assets/_Game/Scripts/Constants/GameRules.cs`, `Assets/_Game/Prefabs/PatrolCop.prefab`

## Goal

The chaser must read like an endless runner (Subway Surfers): the cop is **always
visibly chasing close behind** the learner, never off-screen, never passed, and never
holding a weapon. Today it looks broken (detached rifle/doughnut floating behind the
running body) and abandoned (the player leaves it far behind and off-screen). This pass
rebuilds the chase so it is always-on and stable, disarms the cop, and corrects a
pacing regression found during the investigation.

## Locked constraints (GDD — do not violate)

- **Never punish / friendly chaser (D7).** The cop looms but **never catches**;
  `RaceResult.timesCaught` stays 0. "Always visible" does not mean "can catch."
- **No barriers/obstacles.** The only obstacle is a wrong answer (F35 removed
  `SpawnObstacle`). This pass adds none.
- **Legibility first.** The chaser and its motion must never obscure the SWBST answer
  cards or the "n/5" HUD.
- **Trash Dash scripts stay untouched.** All work is in our director + `GameRules` +
  in-prefab wiring. Grey-box fallbacks stay intact.

## Investigation findings (live, in Play mode — 2026-07-27)

Captured via MCP `execute_code` while the paused build ran:

```
PLAYER  pos z=16.87   speed=15.93   worldDist=115
CAMERA  pos z=11.87   (camera is 5.0m behind the player)
PATROL  pos z=7.25    (9.6m behind player, i.e. 4.6m BEHIND the camera -> off-screen)
danger  = 3.6 / 100
TrackManager  minSpeed=10  maxSpeed=30   (scene override; TD defaults are 5/10)
```

Cop accessory hierarchy (`PatrolCop(Clone)`), with live positions:

```
PatrolCop(Clone)                 root  z=7.25   [Animator]
  male_torso_cop  (Skinned)      renders on the skeleton
  rifle           (MeshRenderer) localPos (0.61, 1.00, -0.07)  <- STATIC child of root
  doughnut        (MeshRenderer) localPos (-0.61,1.00,-0.07)   <- STATIC child of root
  TSMGWorldJoint  (skeleton)     animated limbs land at z~8.4-8.7
  ...spine.../rightLeg.../holster_w_gun  (bone-parented; a holstered gun on the thigh)
```

Root causes, in plain terms:

1. **Detached accessories (the "split body / floating weapon").** `rifle` and `doughnut`
   are **static children of the root**, not skinned and not parented to a hand bone. The
   animated skeleton runs forward (~1.4m ahead of the root), so the rifle and doughnut
   are left hanging behind/beside the running body. `holster_w_gun` *is* bone-parented, so
   it rides the right thigh correctly — but it is still a visible firearm.

2. **Cop rests off-screen (the "player passes it").** The cop's trailing distance is read
   straight off the danger meter. Danger climbs slowly (`mission.dangerPerSecond = 1.5`)
   and every correct pick relieves it by 15, so in a normal run danger sits near 0. At
   danger 0 the cop parks at its "far" distance (`camBack + PatrolFarBehindCamera` = 5+3 =
   8m behind the player) — which is **behind the camera** (only 5m back) and therefore
   invisible. It only enters frame on a wrong answer. That is the opposite of a runner
   chase, where the pursuer is always on your heels.

3. **Run speed is intentionally fast and stays.** `maxSpeed = 30`, `minSpeed = 10`; the run is
   at ~16 m/s climbing to 30. **Owner decision (2026-07-27): the speed is good — do NOT change
   it.** The "I can't think" problem is to be solved purely by giving each item more *runway*
   (distance/time between gates), not by slowing the runner.

4. **Gate spacing is the lever (F38b needs re-checking against real speed).** F38b set the
   next-gate gap to `RaceSecondsPerGate(12) * track.maxSpeed`. At the real `maxSpeed = 30`
   that is **360m** between gates (~12s at top speed, more when slower). That is the right
   direction (longer road per item) but must be verified/clamped so the time-between-gates
   lands in the owner's 10-15s window across the whole speed range, and so a gate always has
   room to spawn and be seen before the runner reaches it.

5. **Latent recenter bug (F37 item 6).** The cop is placed by lerping its **world-Z** toward
   a player-derived target. Trash Dash floats the origin (~every 100m the world shifts back),
   so `playerPos.z` jumps ~100 in a frame while the cop crawls ~`PatrolFollowZ·dt`, leaving
   it stranded and off-screen for seconds. Not the main symptom today, but it will strand the
   redesigned chase too if we keep world-Z follow.

6. **Unidentified blue dome (owner screenshot 09:01).** A smooth solid-blue hemisphere sat
   on the road between cop and player. It is **not** in the live scene now, matches no
   powerup/coin/FX by name, and no blue-dominant renderer or road-side ParticleSystem was
   found. Treat as an open item (below) — likely transient; capture it before "fixing" it.

## Work items

### 1. Disarm the cop + fix the detached accessories

**Design:** in `SpawnPatrol`, after Instantiate, walk the hierarchy and:
- `SetActive(false)` on `rifle` and `holster_w_gun` — an armed cop chasing a Grade-4 kid is
  tonally wrong for a friendly-chaser study instrument (GDD D7 tone).
- `SetActive(false)` on `doughnut` as well **for now** — it is a static root child that
  floats detached from the running body, so it reads as a bug, not charm. (Alternative if
  charm is wanted later: re-parent it to a hand bone under `TSMGWorldJoint` so it rides the
  animation. Out of scope for this pass unless requested.)
- All lookups null-safe: a renamed/missing child is simply skipped (grey-box safe).

This is the same code path as the staging/gap work below (one `SpawnPatrol` edit).

**Verification:** the cop runs as one clean body — no rifle, no holstered gun, no floating
doughnut trailing behind it.

### 2. Subway-style "appear only on a bump" chase + reward loop (the core change)

**Owner's model (2026-07-27), confirmed — the cop is NOT always visible:**
- **Normal running** → the cop is **back / off-screen, hidden**. No chase is shown. (So the
  player being well ahead of the cop is *correct behaviour*, not the bug it looked like.)
- **Wrong pick ("bump")** → the cop **appears close behind the player for ~2s**, then recedes
  and disappears again. This is the one moment it is on screen.
- **Correct pick** → the player speeds up for ~3s (reward, pulls further ahead).

This matches how Subway Surfers actually reads: the guard only lunges into view when you hit
something, then falls back. It is close to what the game already does (danger/menace surge on
a wrong pick); the fixes are the floating weapon/detached body, recenter stability, decoupling
the surge from the slow baseline creep, and tuning the 3s/2s beats.

**Design (`UpdatePatrol` rewrite — gap-based, recenter-proof):**
- Placement each frame from the LIVE player: `cop.z = playerPos.z - gap`, `cop.x` lerped
  toward `playerPos.x`, `cop.y` locked to the captured flat ground height (F38b). Deriving
  from the current player position absorbs a floating-origin recenter with no stall/stranding
  (fixes item 5).
- **Two target gaps:**
  - `hiddenGap` = clearly **behind the camera** (`camBack + PatrolHiddenBehind`, ~3m behind
    the camera) → off screen. This is the resting state during normal play.
  - `surgeGap` = `PatrolSurgeGap` (~1.5-2m behind the player, comfortably **in front of the
    camera**) → close behind the kid, on screen. A visible gap always remains (never
    overlaps/passes; never catches, D7).
- **Target selection is driven by the bump, not by a timer creep:** while `_menaceTimer > 0`
  (set on a wrong pick) target `surgeGap`; otherwise target `hiddenGap`. Lerp `gap` toward the
  target at `PatrolGapFollow` — fast enough that the cop visibly rushes in on the bump and
  slides back out after ~2s.
- **Remove the baseline danger creep for the cop:** so it appears *only* on a bump, drop
  `mission.dangerPerSecond`'s influence on the patrol (either stop it driving the gap, or set
  the per-story value to 0). The danger meter can still exist for the amber vignette, but the
  cop's visibility keys off wrong picks only.
- Retire `PatrolCloseInFront` / `PatrolFarBehindCamera`; add `PatrolHiddenBehind`,
  `PatrolSurgeGap`, `PatrolGapFollow` in `GameRules`.

**Reward/consequence beats (retune, tie to the loop):**
- **Correct → boost ~3s:** keep `BoostSpeed` (m_Speed × 1.35, capped at maxSpeed) but make it
  a *sustained* 3s boost (timer re-asserts the elevated speed for the window) so the "getting
  faster" reward is clearly felt. Set `GameRules.BoostSeconds = 3`.
- **Wrong → slow + surge ~2s:** keep the existing slow (`SlowSeconds`) and set the menace
  window `GameRules.PatrolMenaceSeconds = 2` so the cop's close beat lasts ~2s, then recedes.

**Verification:** in a clean run the cop stays hidden; on a wrong pick it rushes into view
close behind the kid for ~2s then falls back off screen; a correct pick clearly speeds the kid
up (~3s). No stalls/teleports across a recenter. Reads like a Subway-Surfers bump.

### 3. Run speed stays — do not touch it

**Owner decision (2026-07-27):** the run speed is good and must not change. No edits to
`TrackManager.minSpeed` / `maxSpeed` / `k_Acceleration`. The runner keeps its current arcade
feel. All pacing work happens in gate spacing (item 4) only.

### 4. Longer runway per item (the pacing fix)

The complaint is that the road between items is too short — the next card appears with too
little distance to read and reach it. Fix by spacing gates far enough ahead that, **at the
current fast speed**, the learner gets 10-15s of runway per item:
- Keep `NextGateGap = RaceSecondsPerGate * track.maxSpeed` (speed unchanged, so this uses the
  real `maxSpeed = 30` → 360m; ~12s at top speed, more when slower — generous early-run
  thinking time, which suits a learning instrument).
- **Clamp** it so it can never collapse short (or blow out absurdly long):
  `gap = Mathf.Clamp(RaceSecondsPerGate * maxSpeed, RaceMinGateGap, RaceMaxGateGap)` with new
  `GameRules.RaceMinGateGap` (~150m) and `RaceMaxGateGap` (~400m). This guarantees every item
  always has enough road to appear and be collectible.
- Confirm the gate-placement path (`TryPlacePending`) reliably places a gate this far ahead:
  it already waits for track segments to spawn before placing (so a far gate simply appears a
  little later as the road extends) — verify it never clamps a gate to the segment edge and
  pops it in right in front of the runner at these distances.
- Tune `RaceSecondsPerGate` in playtest until the felt time-between-items is 10-15s; it is a
  single constant.

### 5. Identify the blue dome (open item — diagnose before fixing)

Before changing anything, capture it: add a temporary on-spawn `Debug.Log` net (or watch the
hierarchy while reproducing the exact moment from the 09:01 screenshot) to name the object,
its path, and its material/shader. Candidates to rule out: a leaked Trash Dash powerup/bonus
bubble (their spawn is guarded but audit the guard), a mis-tinted Hovl pad/portal FX, or a
stray primitive. Only then decide remove vs. re-tint. **Do not delete blind.**

## Key design decision (RESOLVED 2026-07-27)

**Cop appears only on a bump — not always visible.** The owner confirmed the Subway-Surfers
read: the cop is hidden/off-screen during normal running and only rushes into view when the
learner makes a mistake (wrong pick), for ~2s, then recedes. The player being ahead of the cop
in normal play is correct, not a bug. This keeps the instrument calm during good play and
reserves the visible pressure for the moment it teaches something (a wrong answer). Still
"never catches" (D7).

## Architecture (all additive/localized)

**`EndlessRaceDirector.cs`**
- `SpawnPatrol` — hide `rifle` / `holster_w_gun` / `doughnut`; seed initial `gap` at
  `hiddenGap`.
- `UpdatePatrol` — rewrite to gap-based follow keyed on `_menaceTimer` (hidden ↔ surge);
  remove world-Z lerp + camera-band; decouple from baseline danger creep.
- `CollectCorrect` — make the boost a sustained ~3s window (timer).
- `HitWrong` — set `_menaceTimer` = 2s (the surge) alongside the existing slow.
- `NextGateGap` — add the `RaceMinGateGap`/`RaceMaxGateGap` clamp (item 4).

**`GameRules.cs`**
- Add `PatrolHiddenBehind`, `PatrolSurgeGap`, `PatrolGapFollow`, `RaceMinGateGap`,
  `RaceMaxGateGap`.
- Retune `PatrolMenaceSeconds` → 2, `BoostSeconds` → 3.
- Retire `PatrolCloseInFront` / `PatrolFarBehindCamera` if nothing else references them.

**`MainSummaRace.unity` (TrackManager)** — **no speed edits** (owner: speed is good).

**Untouched:** run speed, all Trash Dash scripts, SWBST gate/scheduling logic, `RaceResult`,
Arrange/Summary/Results, HUD. (The danger meter stays for the vignette but no longer drives the
cop's visibility — the bump does.)

## Testing strategy

Editor edit-mode can't run the parented camera + frozen countdown reliably, so: **compile-clean
+ type-exists** for correctness, then an **owner Play-mode pass** with per-item pass/fail:
1. Cop has no rifle/holstered gun/floating doughnut; runs as one clean body.
2. Cop stays hidden during a clean run; on a wrong pick it rushes into view close behind for
   ~2s then recedes off screen; never overlaps/passes; never catches.
3. Cop never stalls/teleports across a floating-origin recenter (run 100m+, multiple gates).
4. Speed unchanged; each item now has enough runway — real time between gates is ~10-15s and
   the next card always appears with room to read and reach it.
5. Blue dome identified and resolved (item 5).
6. Grey-box fallback (null `patrolPrefab`, capsule stand-in) still runs error-free.

## Out of scope (deferred)

F37's celebration hop, cinematic start camera, and glowing cards; HUD redesign; race music;
world dressing; teacher art. Those remain on their own lists.
