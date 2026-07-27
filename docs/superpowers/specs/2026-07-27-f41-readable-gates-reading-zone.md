# F41 — Readable Answer Gates (reading-zone slowdown + legibility)

**Date:** 2026-07-27
**Branch:** `experiment/endless-override-2`
**Scene:** `Assets/Scenes/MainSummaRace.unity`
**Primary file:** `Assets/_Game/Scripts/Features/Race/Endless/EndlessRaceDirector.cs` (+ `GameRules.cs`)

## Problem (why this matters)

SummaRace is a **thesis measurement instrument**. At the current run speed the learner reaches
an answer gate before they can read the three options, so their lane pick becomes a reaction
guess instead of a comprehension choice — which invalidates what the study is trying to measure.
"The learner can read the 3 options and choose deliberately" is therefore a **hard requirement**;
runner energy is secondary to it.

Distance alone does not fix this (a card 100 m away is unreadable until it is close, and at high
speed "close" lasts ~1 s). The fix must act at the **decision moment**.

## Approach (owner-approved direction: B + F)

Keep the fast run **between** gates; make the **decision moment** readable. Two parts:

### Part B — Reading-zone slowdown
As the runner approaches the active answer gate, smoothly ease the run speed down to a calm
reading pace, hold it through the cards, then accelerate back to full after passing. The run
still *feels* fast on the open stretches; only the moment you must read and choose is calmed.

**Mechanism (no fighting their acceleration):**
- Each frame in `Update`, measure the runner's distance to the active gate
  (`_activeGateDistance - track.worldDistance`).
- When within `ReadingZoneAhead` metres *ahead* of the gate and until `ReadingZonePast` metres
  *past* it, lerp the runner's speed toward `ReadingZoneSpeed` by writing `m_Speed` via the
  existing `SpeedField` reflection (same handle already used for BoostSpeed) — a smooth ease-in.
- On leaving the zone we simply stop overriding; Trash Dash's own `k_Acceleration` eases the
  speed back up to `maxSpeed` naturally (smooth ease-out, no snap).
- Only applies to real answer/represent gates, never the FINISH (finish stays full speed).
- Grey-box safe; if `SpeedField` is null it no-ops (their code unchanged).

**New `GameRules`:**
- `ReadingZoneSpeed = 7f` — calm reading pace (m/s).
- `ReadingZoneAhead = 22f` — start easing down this far before the gate.
- `ReadingZonePast = 6f` — hold the calm speed until this far past the gate, then release.

*(Tunable in one place; these are starting values to dial in during playtest.)*

### Part F — Legibility of the cards
Even slowed, small angled cards are hard. Make them readable on approach:
- **Bigger cards + font:** answer card ~`1.55 × 0.85 → 2.0 × 1.1`, font max `2.4 → 3.2`; SWBST
  logic and lane offsets unchanged, just larger/clearer.
- **Face the camera:** billboard the card text/plate toward the camera on the horizontal plane
  as the runner approaches, so the words are read head-on instead of skewed down the road.
  (Locks once passed so it doesn't spin.)
- **Readable sooner:** ensure full opacity/scale by the time the gate enters the reading zone
  (no late fade-in).

## Explicitly NOT changing

- **No global speed change** (min/maxSpeed untouched) — only the transient reading-zone dip.
- No animation / controller / jump edits (reverted in F40c; staying reverted).
- No change to gate scheduling, danger/patrol, `RaceResult`, or the SWBST tracker.

## Interaction with prior work

The F40 gate runway (`RaceSecondsPerGate`) can stay; with the reading zone the exact spacing
matters less, so we may relax it later. No change here.

## Architecture

`EndlessRaceDirector.cs`
- `Update` — add the reading-zone speed override (distance-to-active-gate test).
- `PlaceAnswerGate` / `PlaceRepresentGate` / `BuildCard` — larger card + font; billboard the
  card toward the camera (a small `EndlessBillboard` helper or a rotation set in Update while the
  gate is ahead of the runner).

`GameRules.cs` — add `ReadingZoneSpeed`, `ReadingZoneAhead`, `ReadingZonePast`.

## Testing

Compile-clean + type-exists, then owner Play-mode pass:
1. Run feels fast between gates; smoothly slows as a gate nears, holds while the cards are
   readable, then speeds back up after passing (no snap).
2. The three options are large and face-on — clearly readable with time to choose the lane.
3. FINISH is unaffected (full speed).
4. No regressions to the tracker, cop, or collect flow.

## Open tuning (dial in during playtest, not blockers)

`ReadingZoneSpeed` (7), `ReadingZoneAhead` (22), `ReadingZonePast` (6), card/font sizes.
