# F37 — Race Polish Pass (Cinematic + Collect Juice)

**Date:** 2026-07-25
**Branch:** `experiment/endless-override-2`
**Scene:** `Assets/Scenes/MainSummaRace.unity`
**Primary file:** `Assets/_Game/Scripts/Features/Race/Endless/EndlessRaceDirector.cs`

## Goal & Constraints

The endless race is feature-complete (F31–F36) but reads unpolished in three ways the
owner flagged: the kid never reacts to collecting, the start camera is dead-flat, and the
answer cards are lifeless flat rectangles. This pass is **pure polish** — it adds no new
mechanic and does not touch the SWBST collect loop, gate scheduling, danger model, or
`RaceResult`.

**Locked constraints (do not violate):**
- **No barriers/obstacles.** The only obstacle is a wrong answer (GDD; F35 removed
  `SpawnObstacle` for exactly this reason). This pass keeps it that way.
- **Never punish / friendly chaser (GDD D7).** The cop looms but never catches;
  `RaceResult.timesCaught` stays 0.
- **Legibility is non-negotiable.** The learner must always be able to read the SWBST
  answer text. No decoration may compete with or obscure the words.
- **No non-SWBST collectibles.** This is a thesis instrument; extra things to collect
  would be measurement noise. The only thing on the track to collect is the correct answer.
- **Trash Dash scripts stay untouched.** All work is in our director + small helper
  components + in-scene prefab wiring. Grey-box fallbacks stay intact.

## The Work Items

(Items 1–5 are the polish pass; item 6 is a bug fix surfaced during the scene audit. Two
cross-cutting design guards — card-bob vs. trigger honesty, and camera-swing vs. `CurveDip` —
are called out inline and repeated under Design Guards.)

### 1. Celebration hop on correct pick + disable dead controls

**Problem:** On a correct pick the cards fly up and sparkle, but the *character* keeps
running with no reaction — the flattest "unpolished" moment. Meanwhile jump/slide are bound
(WASD via `EndlessKeyboardInput`, plus their touch/arrow paths) but do nothing, since there
are no obstacles.

**Design:**
- In `CollectCorrect`, after the sparkle, call the kid's real jump so he hops as he grabs
  the card: `TrackManager.instance.characterController.Jump()`.
  - Verified available: `CharacterInputController.Jump()` is public
    (`Assets/Scripts/Characters/CharacterInputController.cs:316`), guarded by `if (!m_Jumping)`,
    sets the `Jumping` bool + `JumpSpeed` and performs a real arc. With no obstacles there is
    nothing to land badly on, so a hop reads purely as joy.
  - Only fire on a **first-hit** correct pick (`!wasRepresent`), bundled with the existing
    boost — collecting a re-presented gold card still celebrates via sparkle + praise but
    does not double up the hop. (Matches the existing boost-gating in `CollectCorrect`.)
- **Disable manual jump/slide input** so there are no dead buttons: lane-switching is the
  one true mechanic. `EndlessKeyboardInput` stops driving `Jump()`/`Slide()`; their
  touch-swipe up/down and arrow up/down paths are left to their controller but produce no
  gameplay effect (no obstacles to clear) — acceptable, since our added bindings are the
  ones that would read as "why is this here." Auto-hop remains director-owned.

**Verification:** Owner sees the kid hop each time a correct answer is collected; pressing
W/up mid-run no longer launches him for no reason.

### 2. Cinematic start camera (full swing)

**Problem:** The camera is a fixed chase rig; the 3-2-1 countdown freezes the world but
does nothing with the camera — a wasted cinematic window.

**Key technical fact:** TrackManager **parents `Camera.main` to the character** on begin
(`Assets/Scripts/Tracks/TrackManager.cs:216`, `SetParent(characterController.transform, true)`)
and restores it on cleanup (`:281-282`). By the time our countdown runs, the camera is
already a child of the character. Therefore the swing must be done in the character's local
space, or by temporarily unparenting for the swing and re-parenting to the exact gameplay
pose on GO!.

**Design (`AnimateStartCamera`, driven from `CountdownRoutine`):**
1. **Capture the gameplay pose first** — read `Camera.main.transform` local position/rotation
   relative to the character (its resting parented pose). This is the exact pose the swing
   must end on so their code takes over seamlessly.
2. **Hero shot** — during the briefing/countdown, move the camera to a front pose facing the
   kid at the start line (roughly in front + slightly above, looking back at him). The chase
   staging (cop behind — see item 4) is visible in this framing.
3. **On GO!** — PrimeTween-orbit the camera from the hero pose back to the captured gameplay
   pose over ~0.6–0.9s (ease out), so it settles precisely as the world releases. The tween
   writes local pose relative to the character (or restores the parent + local pose at the
   end) so no fight with their parenting.

**Verification:** Owner confirms the camera starts in front of the kid during the countdown
and smoothly swings behind him on GO!, ending in the normal chase view with no snap/jump.
Confirmed: nothing else drives the camera during the frozen window (only TrackManager's
parenting, which we account for).

### 3. Floating glowing cards

**Problem:** Answer cards are flat white kit-sprite rectangles with text — no life. The gold
re-present card has no extra draw despite being the "come get me" beat.

**Design:**
- **Bob:** add a small `EndlessCardBob` component to each card (gentle local-Y sine, small
  amplitude, per-card phase offset so a gate's three cards don't bob in lockstep). Pattern
  mirrors the old `CoinPickup` ripple-bob. The bob is cosmetic and must not move the trigger
  collider enough to affect the honest catch volume (bob the visual child, not the collider,
  or keep amplitude tiny).
- **Glow ring:** spawn a soft glow pad under each card using the Hovl healing/magic pad FX
  (the same family the old `RaceController.optionPadFxPrefab` used), gold-tinted via the
  existing `TintFx`/`StoryGold` approach. New serialized field `cardGlowFxPrefab`
  (null = no ring, grey-box safe), wired in-scene to the Hovl pad prefab. Rings are parented
  to the gate/card so they recycle and destroy with it (same lifetime rule as the cards; a
  wrong pick that destroys the gate must not orphan a ring — see `OptionPickup.padFx`
  precedent from F31).
- **Gold re-present pulse:** the single gold re-present card gets a stronger/brighter pulsing
  ring (larger scale or a punch-scale loop) so it is unmistakably the one to collect.
- **Legibility guard:** rings sit *under* the card and behind the text; text color/size is
  unchanged. No ring may wash out the words.

**Verification:** Owner sees cards gently bob with a glow ring beneath each; the gold
re-present card pulses more strongly; all text stays crisp; no rings left floating after a
gate is collected/missed.

### 4. Cop reads friendly + always chases from behind

**Problem A (tone):** `PatrolCop.prefab` (BitGem cop) carries a **rifle** (`rifle`) and a
**holstered gun** (`holster_w_gun`) — an armed cop chasing a child is tonally wrong for a
Grade-4 "friendly chaser" study instrument.

**Problem B (staging):** The owner observed the cop can appear at/too near the player's
position at the start; it should clearly chase from *behind* for realism.

**Design:**
- **Hide the weapons:** in `SpawnPatrol`, after Instantiate, find and `SetActive(false)` the
  `rifle` and `holster_w_gun` child GameObjects (verified present in the prefab hierarchy).
  Keep the `doughnut` for charm. Null-safe — a renamed/missing child is simply skipped.
- **Stage behind at the start:** the cop is placed a clear fixed distance behind the kid
  (~6–8 m back) at spawn so the cinematic hero shot (item 2) frames **kid in front, cop
  chasing behind** — realistic race staging, never overlapping the player.

The staging + gap guarantee is delivered by the item-6 gap refactor (below), not a separate
patch — they are the same code path.

**Verification:** Owner confirms the cop has no rifle/gun, sits clearly behind the kid during
the countdown/hero shot, and during the run looms close but never overlaps or passes the kid.

### 5. Collect FX consistency (no functional change)

The existing gold sparkle on a correct pick (`SpawnCollectSparkle`, F36) stays. Ensure the
new glow rings share the same `StoryGold` tint so the correct-pick moment reads as one warm
gold language (sparkle + ring + hop).

### 6. Patrol chase correctness — fix the "sometimes stops" bug (gap-based follow)

**Bug (owner-reported, root-caused):** `UpdatePatrol` positions the cop by lerping its
**world-Z** toward a target derived from the player's world position. Trash Dash uses a
**floating-origin recenter** (~every 100 m the world + character shift back toward origin),
so `playerPos.z` jumps ~100 in one frame; the target jumps with it, but the cop only moves
`PatrolFollowZ·dt` (~5%) per frame — so it is suddenly ~100 m out of place and crawls back
over several seconds, off-screen. That is the "sometimes it stops / disappears." The existing
code comment claims recenter-proofness because it "recomputes from the runner each frame,"
but it **lerps** rather than snapping, so the claim is false.

**Fix — follow by GAP, not by world-Z:**
- Maintain a smoothed scalar `gap` = distance the cop trails behind the player.
- Each frame, the danger meter drives a **target gap** (high danger → small gap → looms;
  low danger → large gap → recedes off-screen), clamped to `[minGap, maxGap]`.
- Lerp `gap` toward the target gap; place the cop at
  `playerPosition − forward·gap` (with lateral X lerping toward the player's X as today).
- Because the position is derived from the **live** player position every frame, a recenter
  is absorbed automatically — no jump, no lag, no stall.
- `minGap` is the hard floor that guarantees the cop never overlaps or passes the player
  (satisfies item 4's "always behind"); the spawn/staging value is just the initial `gap`.
- Replaces the `PatrolCloseInFront`/`PatrolFarBehindCamera` camera-derived band with explicit
  `minGap`/`maxGap` `GameRules` constants (clearer, and camera-independent so the item-2
  camera swing can't perturb it).

**Verification:** Owner runs well past a recenter (100 m+, multiple gates) and confirms the
cop never stalls, teleports, or vanishes; it stays smoothly behind and looms/recedes with
danger; at closest approach there is always a visible gap.

## Architecture

All changes are additive and localized:

**`EndlessRaceDirector.cs`:**
- `CollectCorrect` — add the celebration `Jump()` call (first-hit only).
- `CountdownRoutine` / new `AnimateStartCamera` — the cinematic swing (local-space aware;
  see Design Guard F re: `CurveDip`).
- `PlaceAnswerGate` / `PlaceRepresentGate` — attach `EndlessCardBob` + spawn `cardGlowFxPrefab`
  under each card; stronger pulse variant for the re-present.
- `SpawnPatrol` — hide `rifle`/`holster_w_gun`; seed the initial `gap`.
- `UpdatePatrol` — **rewrite to gap-based follow** (item 6): smoothed `gap` clamped
  `[minGap, maxGap]`, cop placed at `playerPosition − forward·gap`. Removes the world-Z lerp
  and the camera-derived band.

**`EndlessKeyboardInput.cs`:**
- Remove the `wKey → Jump()` and `sKey → Slide()` bindings (item 1 — no dead controls);
  keep A/D lane switching.

**New small components (namespace `SummaRace.Features.Race.Endless`):**
- `EndlessCardBob` — cosmetic local-Y bob for a card **visual child** (never the trigger;
  Design Guard E).

**Constants (`GameRules`):**
- Add `PatrolMinGap` / `PatrolMaxGap` (and start-staging gap); retire the now-unused
  `PatrolCloseInFront` / `PatrolFarBehindCamera` if nothing else references them.
- New serialized field on the director: `cardGlowFxPrefab` (Hovl pad), wired in-scene.

**Untouched:** all Trash Dash scripts, the SWBST gate logic, scheduling, danger accrual,
`RaceResult`, Arrange/Summary/Results, HUD, race music, world dressing.

## Design Guards

- **Guard E — card bob vs. trigger honesty:** `EndlessCardBob` animates only the card's
  visual child; the `BoxCollider` trigger stays put so the catch volume remains honest
  (F31 lesson: gameplay triggers must not drift with cosmetics).
- **Guard F — camera swing vs. `EndlessCurveDip`:** `CurveDip` reads `Camera.main` every
  `LateUpdate` to compute each gate's dip, so moving the camera during the item-2 swing could
  distort a visible card. Gates are not placed during the frozen briefing (first gate at
  80 m needs the world to move), so the risk is low; still, the swing must be verified to not
  distort any card, and if one is ever visible during the swing, freeze `CurveDip` (or drive
  it from the resting gameplay pose) for the swing's duration.

## Out of Scope (deferred flags)

Barriers/obstacles; bonus/non-SWBST collectibles; HUD redesign; race music loop; world/track
dressing; teacher art; Arrange/Summary/Results changes. These remain on the NEXT list.

## Testing Strategy

Editor cannot reliably run the frozen-world countdown + their parented camera in edit mode,
so verification is: **compile-clean + type-exists check** (each new type appears in
`Assembly-CSharp`) for correctness, then an **owner full-loop playtest** with a per-item
visible pass/fail:

1. Kid hops on every correct pick; manual W/S does nothing mid-run.
2. Camera opens in front of the kid, swings behind on GO!, settles with no snap; no visible
   card is distorted during the swing (Guard F).
3. Cards bob with glow rings; gold re-present pulses; text crisp; no orphan rings; triggers
   still catch honestly despite the bob (Guard E).
4. Cop has no weapons, stages behind at the start, never overlaps during the run.
5. **Cop never stalls/vanishes across a floating-origin recenter** (item 6) — run 100 m+ past
   several gates and confirm smooth chase throughout; this is the primary regression check
   and is not observable in a short run.

Grey-box fallback (null `cardGlowFxPrefab`, grey-box patrol capsule) must still run without
error.
