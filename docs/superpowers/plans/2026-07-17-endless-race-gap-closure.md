# Endless Race Gap Closure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the gaps between the working endless-race experiment (plan `2026-07-17-endless-swbst-collection.md`, complete, final-review-approved at cc87a52) and a learner-ready race: de-clutter the leftover Trash Dash menus/panels, satisfy the "never punish / never game-over" non-negotiable, add the missing wrong-pick consequence, and move learner strings into `GameText`.

**Architecture:** Unchanged from the first plan — ALL changes are additive in `SummaRace.Features.Race.Endless` (plus `GameText` constants). The one existing TrackManager guard remains the only Trash Dash edit, ever. New behavior uses their **public** APIs only: `CharacterInputController.currentLife/maxLife` (both public) for never-punish, `TrackManager.maxSpeed/minSpeed/speed` (public) for the slow-down, and runtime `SetActive(false)` on UI objects named by the audit for de-cluttering.

**Tech Stack:** Unity 6 / URP, MCP For Unity for edits + play verification, PrimeTween.

## Global Constraints

- **Branch `experiment/endless-override-2` still never ships** (ads/analytics/purchasing/gdk present; their Start.unity at build index 0). Decontamination remains a separate later plan — its running checklist lives in `.superpowers/sdd/progress.md` ("DECONTAMINATION LIST").
- **Zero new edits to any Trash Dash file** (`Assets/Scripts/**`, their scenes, prefabs, Addressables). The existing 2-line guard in `TrackManager.SpawnCoinAndPowerup` stays the only one. All hiding/behavior below is runtime work from our director.
- Original `Main.unity` must keep playing exactly as shipped (everything below is inside the director, which exists only in `MainSummaRace.unity`).
- Their classes are global-namespace; `GameManager` collides with ours: fully qualify all SummaRace types, no `using SummaRace.Core;` (established pattern in `EndlessRaceDirector.cs`).
- House rules: learner-facing strings in `GameText`, tuning numbers in `GameRules`, `_camelCase` privates, null-check SummaRace singletons (scene must play direct-in-editor).
- **New-file .meta gotcha:** .cs files created outside Unity get guid-only stub metas that refresh does NOT upgrade — write the canonical 11-line MonoImporter stanza yourself, keep the generated guid, trailing newline (recurred twice already; see memory).
- Unity Editor open with MCP; never `-batchmode`. Their `GameState` pauses on focus loss — play probes call `GameState.Resume()` defensively; verify via state probes, not console silence.
- Working tree has pre-existing dirty files — `git add` only named paths, never `-A`. Commit per task with `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.

## Gap inventory (what this plan closes, and what it explicitly does not)

**Closed here:**
1. **UI clutter** (owner: "menu, options and panels — many not necessary"): Loadout screen flash at start; pause button (its menu can quit to their Loadout); powerup zone; missions/shop/settings/leaderboard chrome; life hearts (meaningless after gap 2). Input: `.superpowers/sdd/ui-audit.md` (runtime handles per element).
2. **"Never punish" non-negotiable** (GDD D7): their 3-lives death → GameOver popup with premium-revive is a punishment + dead-end-adjacent flow. Fix: pin lives full every frame — obstacle hits still stumble + blink (friendly pressure) but death becomes unreachable, which also retires the premium-revive concern and makes the Run Again corruption path unreachable (its guard stays as belt-and-braces).
3. **No wrong-pick consequence** (owner's "slows" note; old race slowed you to 0.6× for `GameRules.SlowSeconds`): add a temporary speed clamp via their public `maxSpeed` — instant slow, gradual recovery from their own 0.2/s acceleration.
4. **Hard-coded learner strings** in the director → `GameText` constants.

**Explicitly NOT here (later plans, in rough order):**
- Mockup styling pass (kit-sprite HUD, briefing popup F17-style, teacher avatar, race music swap, patrol character visual) — after the owner blesses the endless base.
- Character replacement (owner: "once we finalize").
- Decontamination + build-settings reorder (mandatory pre-ship; list already recorded).
- Phase G (29 stories + SessionMap), old `Race.unity`/`RaceController` retirement decision.

## Reference (verified public APIs)

- `CharacterInputController` (`Assets/Scripts/Characters/CharacterInputController.cs`): `public int maxLife = 3` (line 23), `public int currentLife { get; set; }` (line 29). Their `CharacterCollider.OnTriggerEnter` decrements on obstacle hit, then plays stumble anim + 2s invincibility blink while life > 0 — pinning life full preserves exactly that friendly beat.
- `TrackManager`: `public float minSpeed = 5f / maxSpeed = 10f` (fields), `speed` (getter). Their `Update()` clamps `m_Speed = maxSpeed` instantly when above it, and re-accelerates at 0.2/s when below — so lowering `maxSpeed` slows instantly and restoring it recovers gradually. Scene reload restores serialized defaults.
- `GameState` public UI handles: `pauseButton`, `powerupZone`, `lifeRectTransform`, `coinText`, `premiumText`, `scoreText`, `distanceText`, `multiplierText`, `countdownText` (KEEP countdown).
- `GameRules` (ours): `SlowSeconds` already exists (old race used it). `GameText` (ours): add race strings (Task 3).
- The director's existing seams: `Update()` (restart detection + miss-grace), `HitWrong`, `HideTheirCurrencyHud()` (called ~1.25s after Start), `EnsureSingleAudioListener()`.

## File structure

- Modify: `Assets/_Game/Scripts/Features/Race/Endless/EndlessRaceDirector.cs` — all four gaps land here (it is the scene's conductor; ~460 lines now, acceptable; do not split without owner direction).
- Modify: `Assets/_Game/Scripts/Constants/GameText.cs` — five new constants.
- Input (read-only): `.superpowers/sdd/ui-audit.md` — the hide-list handles. If the audit file is missing or incomplete at execution time, the implementer re-derives handles from `GameState`/`LoadoutState` public fields + a scene hierarchy probe, and records them in the report.

---

### Task 1: De-clutter the Trash Dash chrome (hide-list + loadout-flash mask)

**Files:**
- Modify: `Assets/_Game/Scripts/Features/Race/Endless/EndlessRaceDirector.cs`

**Interfaces:**
- Consumes: `.superpowers/sdd/ui-audit.md` handles; existing `HideTheirCurrencyHud()`.
- Produces: `private void HideTheirChrome()` replacing/absorbing `HideTheirCurrencyHud()`; `private void MaskLoadoutFlash()` called from `Awake()`.

- [ ] **Step 1: Read the audit** (`.superpowers/sdd/ui-audit.md`). Build the final hide-list from its KEEP/HIDE table. Baseline expectation (adjust to what the audit actually found — its handles win over this list):
  - HIDE: pause button (`gs.pauseButton.gameObject`), powerup zone (`gs.powerupZone.gameObject`), life hearts (`gs.lifeRectTransform.gameObject` — meaningless after Task 2), the five currency/score texts (already hidden), any missions/shop/leaderboard/settings buttons the audit names on the Loadout or run HUD.
  - KEEP: `gs.countdownText` (3-2-1-GO), our `SummaRaceHud`, the death popup handles (they become unreachable in Task 2 — do not hide, so nothing breaks if a hit somehow lands).

- [ ] **Step 2: Implement the mask + chrome hiding.** Replace `HideTheirCurrencyHud()` with `HideTheirChrome()` (keep hiding the five texts, add the audit's handles, null-check every one), and add a loadout-flash mask called from `Awake()`:

```csharp
        /// <summary>The learner should never see the Trash Dash loadout screen —
        /// hide its visual roots on frame one; LoadoutState itself stays alive so
        /// StartGame() still works. Handles come from the UI audit.</summary>
        private void MaskLoadoutFlash()
        {
            var loadout = FindFirstObjectByType<LoadoutState>();
            if (loadout == null) return;
            // Use the audit's named visual roots. Pattern (adjust to audit):
            foreach (var canvas in loadout.GetComponentsInChildren<Canvas>(true))
                if (canvas.gameObject != loadout.gameObject)
                    canvas.enabled = false;
        }
```

Call `MaskLoadoutFlash();` at the end of `Awake()`. IMPORTANT: verify against the audit whether the loadout canvases are children of the LoadoutState object or siblings under another root — if siblings, disable the exact audit-named objects instead of the `GetComponentsInChildren` sweep. Never `SetActive(false)` the `LoadoutState` component's own GameObject (the state machine needs it).

- [ ] **Step 3: Verify in play** — enter play in `MainSummaRace.unity`: no loadout visuals at any point (screenshot within the first second), run starts normally, countdown still visible, no pause button / powerup zone / hearts during the run, our banner + feedback intact, zero console errors. Also open the ORIGINAL `Main.unity` and confirm its loadout still renders (our mask lives in the director only).

- [ ] **Step 4: Commit**

```bash
git add "Assets/_Game/Scripts/Features/Race/Endless/EndlessRaceDirector.cs"
git commit -m "EXP3-10: de-clutter Trash Dash chrome — loadout flash masked, pause/powerups/hearts hidden"
```

---

### Task 2: Never-punish — death becomes unreachable

**Files:**
- Modify: `Assets/_Game/Scripts/Features/Race/Endless/EndlessRaceDirector.cs`

**Interfaces:**
- Consumes: `TrackManager.instance.characterController` (public), `currentLife`/`maxLife` (public).
- Produces: life top-up inside the existing `Update()`.

- [ ] **Step 1: Implement.** In `Update()`, inside the existing `if (track == null || _finished) return;` block, immediately after the restart-detection block, add:

```csharp
            // Never punish (GDD D7): obstacle hits still stumble + blink (their friendly
            // 2s invincibility beat) but can never stack to a game over. Pinned every
            // frame; a hit drops life for at most one frame, far from the 0 that
            // triggers their GameOver state.
            var runner = track.characterController;
            if (runner != null && runner.currentLife < runner.maxLife)
                runner.currentLife = runner.maxLife;
```

- [ ] **Step 2: Verify in play** — run WITHOUT their invincibility cheat, deliberately eat 4+ obstacle hits across the run (more than their 3 lives): each hit stumbles + blinks, run continues, no death popup ever, probe `currentLife == maxLife` after each hit settles. Then finish the race normally (their cheat allowed for the remainder) → Arrange loads.

- [ ] **Step 3: Confirm original untouched** — the top-up only runs where the director lives; state (not code) so nothing to check in their files, but play `Main.unity` briefly: death still works there.

- [ ] **Step 4: Commit**

```bash
git add "Assets/_Game/Scripts/Features/Race/Endless/EndlessRaceDirector.cs"
git commit -m "EXP3-11: never-punish — lives pinned, obstacle hits stumble but can never game-over"
```

---

### Task 3: Wrong-pick slow-down (their-API speed clamp)

**Files:**
- Modify: `Assets/_Game/Scripts/Features/Race/Endless/EndlessRaceDirector.cs`

**Interfaces:**
- Consumes: `TrackManager.maxSpeed/minSpeed/speed` (public), `SummaRace.Constants.GameRules.SlowSeconds` (exists).
- Produces: fields `_slowTimer`, `_savedMaxSpeed`; slow application in `HitWrong`, restore in `Update()`.

- [ ] **Step 1: Implement.** Add fields with the other privates:

```csharp
        private float _slowTimer;
        private float _savedMaxSpeed = -1f;
```

In `HitWrong(EndlessOptionPickup pickup)`, after the feedback call, add:

```csharp
            // Old-race parity: a wrong pick slows the runner briefly (their Update
            // clamps speed to maxSpeed instantly; restoring lets their 0.2/s
            // acceleration recover it gradually).
            var track = TrackManager.instance;
            if (track != null)
            {
                if (_savedMaxSpeed < 0f) _savedMaxSpeed = track.maxSpeed;
                track.maxSpeed = Mathf.Max(track.minSpeed, track.speed * 0.6f);
                _slowTimer = SummaRace.Constants.GameRules.SlowSeconds;
            }
```

In `Update()`, after the never-punish block (Task 2), add:

```csharp
            if (_slowTimer > 0f)
            {
                _slowTimer -= Time.deltaTime;
                if (_slowTimer <= 0f && _savedMaxSpeed > 0f)
                {
                    track.maxSpeed = _savedMaxSpeed;
                    _savedMaxSpeed = -1f;
                }
            }
```

(Scene-reload restart resets their serialized `maxSpeed` automatically, so a reload mid-slow is safe; `FinishRoutine` stops the track so no restore is needed there.)

- [ ] **Step 2: Verify in play** — steer into a distractor (probe `characterController.ChangeLane(±1)`), then probe `TrackManager.instance.speed` immediately (~0.6× the pre-hit value) and again after `SlowSeconds`+2s (climbing back). A second wrong pick during a slow refreshes the timer without corrupting `_savedMaxSpeed` (probe it holds the ORIGINAL value).

- [ ] **Step 3: Commit**

```bash
git add "Assets/_Game/Scripts/Features/Race/Endless/EndlessRaceDirector.cs"
git commit -m "EXP3-12: wrong pick slows the runner (public maxSpeed clamp, gradual recovery)"
```

---

### Task 4: Learner strings → GameText

**Files:**
- Modify: `Assets/_Game/Scripts/Constants/GameText.cs`
- Modify: `Assets/_Game/Scripts/Features/Race/Endless/EndlessRaceDirector.cs`

**Interfaces:**
- Produces: five `GameText` constants consumed by the director.

- [ ] **Step 1: Add constants** to `GameText.cs` (match the file's existing style/placement):

```csharp
        public const string RaceCollectPrefix = "Collect: ";
        public const string RaceRunToFinish = "Run to the FINISH!";
        public const string RaceFinishBanner = "FINISH!";
        public const string RaceCorrectFeedback = "You got it!";
        public const string RaceWrongFeedback = "Not quite — the glowing one!";
```

- [ ] **Step 2: Replace the literals** in `EndlessRaceDirector.cs` — `UpdateBanner()` (`"Collect: "` → `SummaRace.Constants.GameText.RaceCollectPrefix`, `"Run to the FINISH!"`), `FinishRoutine` (`"FINISH!"`), `CollectCorrect` (`"You got it!"`), `HitWrong` (`"Not quite — the glowing one!"`). The world-card strings `"FINISH"`/`"START"`-style and element types come from story data / are gate geometry — leave them.

- [ ] **Step 3: Compile + spot-check in play** (banner + one feedback shows the same text as before). Note in the report: `RaceController.cs` has the same literals (pre-existing precedent) — NOT migrated here, logged for the old-race retirement decision.

- [ ] **Step 4: Commit**

```bash
git add "Assets/_Game/Scripts/Constants/GameText.cs" "Assets/_Game/Scripts/Features/Race/Endless/EndlessRaceDirector.cs"
git commit -m "EXP3-13: race learner strings moved to GameText"
```

---

### Task 5: Full-loop regression + docs + push

**Files:**
- Modify: `CLAUDE.md` (extend the EXP3 build-state row)

- [ ] **Step 1: Full-loop regression from Boot** — Boot → MainMenu → StorySelect → Reader → race (NO cheats this time: eat obstacle hits, mix correct/wrong picks, confirm slow-down + stumbles + no death popup + no chrome) → FINISH → Arrange → Summary → Results; stars match picks; audio alive in Arrange; zero errors from our code.
- [ ] **Step 2: Original-game check** — play `Main.unity`: loadout renders, coins spawn, death works (all our behavior is director-gated).
- [ ] **Step 3: CLAUDE.md** — extend the EXP3 row: chrome hidden + loadout masked, never-punish via life pinning (their death flow unreachable), wrong-pick slow, strings in GameText; note the deferred styling/patrol/music/decontamination items.
- [ ] **Step 4: Commit + push**

```bash
git add CLAUDE.md
git commit -m "EXP3-14: gap-closure verified end to end; docs updated"
git push
```

- [ ] **Step 5: Owner playtest gate** — hand off. The owner should specifically: die-tackle obstacles on purpose (feel the stumble, confirm no popup), make a wrong pick on a live frame (closes the one never-seen visual), and judge the de-cluttered screen against the mockups' spirit.
