# F40 — SWBST Inventory Tracker + Collect-to-Slot + Race Juice

**Date:** 2026-07-27
**Branch:** `experiment/endless-override-2`
**Scene:** `Assets/Scenes/MainSummaRace.unity`
**Primary file:** `Assets/_Game/Scripts/Features/Race/Endless/EndlessRaceDirector.cs`

## Why

Owner playtest review of the endless race (screenshots 2026-07-27 11:27):
- The learner can't see **what to collect next** or **what they've already collected** — the
  current "Collect: SOMEBODY 1/5" is transient text and the type only lives on an in-world pill.
- The three answer cards read as **flat, boring white rectangles**.
- **Collecting has little payoff** — no sense of "I put that piece in place."
- The **character doesn't react** to right/wrong, so the run feels lifeless.

These are polish + scaffolding, not a mechanic change. The SWBST collect loop, gate scheduling,
danger model, and `RaceResult` are untouched.

## Constraints (GDD)

- **Legibility is non-negotiable** — the tracker and cards must never crowd the road or the
  answer text.
- **Never punish (D7)** — reactions to a wrong pick are friendly (a stumble, not a fail).
- **Content stays data** — slot labels/types come from the loaded `StoryData`, never hard-coded.
- **Grey-box safe** — every new visual degrades gracefully when its sprite/prefab is unwired.

## The work items

### 1. Persistent SWBST inventory tracker (top strip) — the headline feature

A fixed HUD strip of **5 slots** across the top, one per SWBST element, that teaches the
framework while it plays and always answers "what's next / what do I have."

- Order S · W · B · S · T, each slot tinted in `SwbstPalette` (blue / green / red / orange /
  purple), with the element letter always visible.
- **State per slot:**
  - *Upcoming* — dim / faint, letter only.
  - *Current target* — bright + a gentle pulse (this replaces the transient "Collect: X 1/5"
    banner and the in-world pill as the "what to collect now" signal).
  - *Collected* — filled with the collected **word** (e.g. "Bella") on the element color, with
    a small check/lock.
- **Position: top, horizontal.** Chosen over bottom/side because the answer cards sit in the
  lower-middle and the runner at the bottom — top is the only clear band, and it matches where
  the old "1/5" text already lived. (Owner may override to bottom/side.)
- Built in code on the existing HUD canvas (`BuildHud`), TMP labels + `SwbstPalette`; sprite
  slots use `worldCardSprite` when present, tinted quads as the grey-box fallback.

### 2. Collect → center pop → fly into the slot

Ties the collect moment to the tracker so progress feels tangible (the owner's "show it in the
center then put it in the inventory slot" idea, à la the shop reference).

- On a **correct** pick: the collected word/card **scales up briefly in screen center**
  (celebration), then **tweens up into its SWBST slot** (PrimeTween, ease), and the slot
  **locks** with a click SFX + a small sparkle.
- The existing world-card fly-up (F36) is replaced/retargeted to fly to the slot position.
- On a re-presented gold card the slot still fills, just without the extra fanfare.

### 3. Answer cards: from flat rectangles to game pickups

Keep the text crisp; make the cards feel alive (much of this overlaps F37 item 3):
- **3D pill** look — soft drop shadow + subtle vertical gradient so they read as pressable.
- **Gentle bob** (per-card phase offset) + a **soft glow ring** underneath
  (`cardGlowFxPrefab`, gold-tinted; null-safe).
- **A / B / C badge** chip on each card (uses `GameText.OptionLetters`, already defined for the
  Reader) so they read as pick options.
- **Collect pop** — the chosen card squashes/punches before flying to its slot (item 2).
- Legibility guard: glow sits under the card, text size/color unchanged.

### 4. Character reacts (use the Mixamo clips we already have)

Clips available on the kid rig: `Idle, Running, RunningJump, JoggingStumble, RunToRolling,
HipHopDancing, FlyingBackDeath`.
- **Correct** → celebration **hop** (`RunningJump` / `Jump` trigger) as they grab the card
  (F37 item 1) + the ~3s speed burst (F39, done).
- **Wrong** → **JoggingStumble** trip (friendly, D7) alongside the slow + cop surge (F39, done).
- **Finish** → **HipHopDancing** on the victory beat before Arrange.
- All via the kid's existing Animator params; null-safe for grey-box.

### 5. Retire / shrink the in-world SWBST pill

With the top tracker owning "what's next," the large in-world blue pill above the cards is
redundant. **Drop it** (or shrink to small floating type over the gate) to de-clutter the road.
The three answer cards remain in-world; only the type pill changes.

### 6. Blue dome — RESOLVED (non-issue)

It is Trash Dash's editor **gizmo**: `TrackSegment.OnDrawGizmos` (`#if UNITY_EDITOR`) draws
`Gizmos.color = Color.blue; Gizmos.DrawSphere(pos, 0.5f)` at each obstacle-spawn position
(`TrackSegment.cs:113-119`). It renders **only in the Scene view**, never in the Game view or
a build — players never see it. No code change (their script stays untouched); hide it in-editor
via the Scene toolbar's Gizmos toggle if desired.

## Architecture (additive, localized)

**`EndlessRaceDirector.cs`**
- `BuildHud` — add the 5-slot tracker row (`_slot[5]` refs); helper to set slot state.
- `UpdateBanner` / a new `RefreshTracker` — drive current/collected/upcoming states from
  `_firstPickDone` + `_activeElement`.
- `CollectCorrect` — retarget the fly-up to the slot; trigger center-pop + slot lock; fire the
  hop.
- `HitWrong` — fire the stumble.
- `PlaceAnswerGate` / `BuildCard` — 3D/shadow/bob/glow + A/B/C badge; drop the type pill (or
  shrink); remove the blue dome.
- `FinishRoutine` — fire the dance.

**`GameText.cs`** — any new learner strings (reuse `OptionLetters`; tracker labels come from
`StoryData.elements[i].type`).

**Untouched:** collect loop, gate scheduling, danger/patrol model (F39), `RaceResult`,
Arrange/Summary/Results.

## Open decisions (owner)

- **Tracker position:** top strip (recommended) vs. bottom vs. side.
- **In-world type pill:** drop entirely vs. shrink to small floating label.
- **A/B/C badges on cards:** yes/no (they help "pick one" reading but add a little clutter).

## Testing strategy

Compile-clean + type-exists, then owner Play-mode pass:
1. Top tracker shows 5 SWBST slots; current pulses; collected fill with the word in-order.
2. A correct pick pops in center then flies into its slot and locks (SFX + sparkle).
3. Cards read as 3D glowing pickups with A/B/C, text still crisp.
4. Kid hops on correct, stumbles on wrong, dances at the finish.
5. No in-world type pill clutter; no blue dome.
6. Grey-box (unwired sprites/prefabs) still runs error-free.

## Out of scope

Reader/Arrange/Summary/Results changes; HUD-wide redesign beyond the tracker; new audio beyond
existing keys; world/track dressing.
