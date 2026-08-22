# SummaRace — Code-Verified Game Summary (Start to Finish)

*Updated 2026-08-22. Sources: (1) all 12 build scenes opened live in the Unity Editor and their
hierarchies dumped via MCP; (2) source-code audits of the actual C# — entry/navigation, Reader,
race (all 5,156 lines of `EndlessRaceDirector` plus its helpers), and post-race/persistence
traced file-by-file with line evidence. **Docs and comments were NOT trusted** — everything
below is what the code does today, and where a comment contradicts the code, the code is
reported; (3) a **full live Play-mode run** driven through MCP on 2026-08-22 — Boot → MainMenu
→ SessionMap → StorySelect → Reader (`s05_average`, a never-before-playtested story) → the
race → Arrange → Summary → Results, with portrait screenshots at every screen and the
resulting `.jsonl` log row inspected. See §7 for what the live run confirmed and corrected.*

SummaRace is an offline Android reading game (portrait, Unity 6000.4.1f1, URP) teaching Grade-4
learners to summarize stories with **SWBST** (Somebody · Wanted · But · So · Then). It is a
thesis instrument: 40 learners × 10 sessions vs a control group. 100% offline; wrong answers
never block; all 30 stories are JSON.

---

## 1 · The player's journey (verified routing, build index in brackets)

```
Boot [0] ──(fresh device: learner.named == false)──▶ NameEntry [9] ──▶ MainMenu [3]
   └─(returning)──▶ MainMenu [3] ──▶ TeacherMenu [11] (corner button; PIN inside)
                        │
                        ▼  TAP TO START
                  SessionMap [10] ── stop n ──▶ StorySelect [4] ── card ──▶
                        ▲                                                  │
                        │                                    GameManager.StartStory(id)
                        │                                                  ▼
                        │                                             Reader [5]
                        │                                                  │ last answer
                        │                                                  ▼
                        │                                    MainSummaRace [1] (endless race)
                        │                                                  │ FINISH
                        │                                                  ▼
                        │                                             Arrange [2]
                        │                                                  │ solved or assisted
                        │                                                  ▼
                        │                                             Summary [7]
                        │                                                  │ submit
                        │                                                  ▼
   └────(session done)── Results [8] ──(session not done)──▶ StorySelect
```

- Reader's last answer routes to `SceneNames.RaceEndless` = the string `"MainSummaRace"` —
  the legacy `Race.unity` [6] is in the build with **zero inbound routes** (dead).
- Results routes: session complete → SessionMap (with a one-shot session cheer); otherwise →
  StorySelect ("NEXT STORY"). Session 10 fully done → title "…all missions cleared", button
  "SEE MY JOURNEY", still → SessionMap.
- Every content scene falls back to `StoryLoader.Load("s01_easy")` when played directly in
  the editor; if even that fails it bails to StorySelect.

Learning design (support-removal ladder): **Read** = question with text visible → **Race** =
same 5 SWBST slots with text gone → **Arrange** = sequence the 5 parts → **Summary** = produce
your own summary.

---

## 2 · Scene by scene (code truth)

### Boot [0] — `Bootstrapper.cs`
Builds splash chrome first (loading label + progress bar are code-built if unwired), then —
behind a static `_initialized` latch — creates **`[Core]`** (DontDestroyOnLoad) with components
in this order: AudioListener, GameManager, AudioManager, SaveManager, SceneLoader,
SessionLogService, BackButtonGuard. Then `LoadSettings → SetVolumes`, `InitProfiles()`,
raises `AppReady`. Target frame rate 30. Splash bar is **elapsed-time over
`SplashSeconds` (2s)**, not real load progress. If core construction throws: a code-built
failure card with TRY AGAIN (reloads Boot directly). Routing: `learner != null && !learner.named`
→ NameEntry, else MainMenu (quiet fade, no tips). `InitProfiles` creates a first profile on a
fresh device (`named=false`), restores the last active learner via `settings.activeLearnerId`
on later launches.

### NameEntry [9] — `NameEntryController.cs`
Name field + 4 avatar buttons (colour + gold halo + 1.16× scale on the selected one) +
code-built DONE TYPING chip and keyboard-dismiss catcher. BACK is blocked ("Let's get you set
up first!"). Confirm (one-shot): empty name → **"Runner"** (`GameText.DefaultLearnerName`),
`named = true`, persist, → MainMenu. **Participant code is never touched here** — it's set
only behind the teacher PIN; TeacherMenu's "+ New learner" flow sends a freshly-coded learner
*into* NameEntry.

### MainMenu [3] — `MainMenuController.cs`
Menu music, "Playing as X" learner pill (code-built if unwired), decor raycasts silenced.
START → SessionMap. Teacher corner → TeacherMenu (**no PIN here** — the gate is inside
TeacherMenu). Safety net: if the active learner is somehow unnamed, MainMenu re-offers
NameEntry (max 2 attempts via a static counter with a 2s grace refund; exhausting it marks the
learner pill amber and leaves the menu playable).

### SessionMap [10] — `SessionMapController.cs`
10 scene-wired stops (not generated). `unlocked = clamp(learner.unlockedSession, 1, 10)` —
**no learner ⇒ all 10 open** (editor convenience). The current stop (first open session with
< 3 completions) glows; each stop shows 3 stars from per-difficulty completion. Locked tap =
wiggle SFX + lock punch + "Your teacher opens the next mission!" — never navigation. Playable
tap sets `GameManager.SelectedSession` → StorySelect. One-shot session-complete cheer via
`ConsumeJustCompletedSession()`. BACK: confirm-dialog exit to MainMenu.

### StorySelect [4] — `StorySelectController.cs`
Builds 3 cards for the selected session from `StoryIds.For(session, difficulty)`, loading each
JSON up-front. Chain rule: Easy always open; each later difficulty needs the previous
**cleared**; a *missing* JSON deliberately does **not** block the chain (opens the gate) but its
own card is Locked + "This story isn't ready yet — ask your teacher!". `next` card gets a gold
play-ring + PLAY badge; cleared cards get a quiet REPLAY badge; stars from
`GetBestStars` (best race accuracy). Tap → `GameManager.StartStory(id)`: sets `CurrentStory`,
clears `LastRaceResult`, raises **`StoryStarted`**, → Reader (load failure → back to
StorySelect). No learner ⇒ everything replayable (editor mode).

### Reader [5] — `ReaderController.cs` (975 lines)
- **Page flow**: story card with typewriter reveal (`StoryTextReveal`, 0.25–0.9s), "Page N/M"
  badge + tweened progress fill, page-turn SFX from page 2. NEXT is labelled "QUESTION!" →
  shows the question (card hides, Ms. Lumi's group alpha → 0, badge flips to "Question N/M");
  after answering, NEXT = "NEXT PAGE" or "START RACE!" on page 5. A page with a null question
  (allowed by the validator) skips straight to the next page.
- **Questions**: 3 options with fixed slot letters `A./B./C.` and a **Fisher–Yates shuffle
  that is unseeded and unlogged** (the on-screen position of the correct answer is
  unrecoverable from the data; only the story-space `chosenIndex` is logged). SWBST slot hint
  fades in after 1.7s. Answer: correct = green + punch + praise from the shuffle-bag
  (never repeats back-to-back); wrong = the tapped option dims and sinks slightly, correct one
  highlights, feedback "Not quite — here is the answer!" — no red, no retry, never blocks.
  Only the **first** answer per page becomes data. Haptics only on correct.
- **Narration**: auto-plays per page when VOICE is ON (default ON, `PlayerPrefs` persisted
  immediately); stops when a question opens and on every scene load. Toggling ON re-reads the
  page. **HEAR AGAIN ignores the VOICE toggle** (plays even when OFF) and is shown whenever
  the page has a narration *path* — a missing file yields a silent button (warning log only).
  Volume from `AppSettings.narrationVolume` via `SetVolumes` (Bootstrapper + TeacherMenu).
- **Exit**: BACK chip + Android BACK share one rule — `readingPage && !_answerCommitted`:
  leaving is possible only on a reading page before the **first committed answer anywhere in
  the run**; after that the run is study data and both are blocked ("Keep reading - you're
  nearly there!"). On-screen BACK is two-tap ("LEAVE?", 4s auto-disarm) → StorySelect,
  raising nothing (the empty log row is discarded — costs the study nothing).
- **Events raised**: `PageAnswered{pageIndex, chosenIndex, correct}` and `ReadingCompleted`
  (latched against double-tap so `readingSeconds` can't be zeroed). Last answer →
  `SceneLoader.Go("MainSummaRace")`.

### MainSummaRace [1] — the race — `EndlessRaceDirector.cs` (5,156 lines) + helpers
Trash Dash's runner scene taken over by our director (`EndlessRaceMode.Active` flips their
guarded code paths; their Loadout UI is masked in `Awake`).

- **Boot**: story from `CurrentStory` (fallback `s01_easy`; null → StorySelect). Bounded 8s
  waits (`BootWaitSeconds`) on `PlayerData` and `TrackManager` — on timeout the briefing's
  START becomes **"GO BACK"** → StorySelect (escapable, not playable). A detected track
  rebuild (worldDistance jumping backwards) reloads the race scene. `Time.maximumDeltaTime`
  clamped to 0.10 for the run, restored in `OnDestroy`.
- **Briefing**: code-built canvas — wood mission card, story title, 5 SWBST chips (cream
  plaque + colour strip + letter + word, staggered pop-in), Ms. Lumi + bubble, START disabled
  ("Getting ready…") until boot completes. Their music stems are already playing under it.
- **Countdown**: 3-2-1-GO as giant centered gold text; camera orbits from behind-left into
  the chase pose over 2.1s, then a 0.6s swoop on GO. A **first-race steering coach** (3 ghost
  lane zones + a hand walking the lanes) shows once ever (`PrefKeys.RaceCoachSeen`, written
  on show). GO restarts their music stems (first release only).
- **Gates**: 5 answer gates then FINISH; exactly one active gate (monotonic `gateId` makes
  double-hits impossible). Placement clamps to already-spawned track spans. **Live pacing**:
  gap = reading window (`RacePreviewLeadSeconds` 12 × 1.35/1.0/1.0 easy/avg/hard) + quiet
  stretch (`RaceQuietRunSeconds` 6 × a story-seeded ±20% rhythm hash), integrated against the
  track's real acceleration; floors `checkpointSpacing`/`RaceMinGateGap` 110, cap
  `RaceMaxGateGap` 800; first gate at `RaceFirstGateDistance` 200m.
- **The reading surface** is the screen-space **option preview** in the sky band: 3 columns
  filled from the same shuffled array as the world cards, opened when
  `SecondsToCover(gate) ≤ reading window` (same integration used to reserve the distance, so
  reserved and delivered time agree). **The columns are tappable** — tapping one steers the
  runner to that lane (it never records a pick; only driving through a card does). A gold
  arrival glow pulses behind the board; a gate-arrival chip counts down the last 10s (its
  visibility window constant is 999 ⇒ effectively always on).
- **Picks**: correct → sparkle + word flies to its tracker slot + speed boost ×1.35 (capped
  at maxSpeed); wrong → gentle slow (×0.6 for `SlowSeconds` 1.5, min-speed-safe), 3s patrol
  menace, and a **2.2s answer reveal** — the preview's centre column repaints as the correct
  answer full-width. **There is no re-present any more** (that whole mechanism was deleted;
  `wasRepresent` in the log is now permanently false). A run-past gate (5m grace) closes the
  first pick as a miss, raises no `ElementCollected`, and shows the same answer reveal. A 2s
  stranded-run watchdog advances a stuck race and can re-schedule FINISH.
- **Tracker**: 5 wooden slots, four states — found (cream + ink letter), collected-but-wrong
  (dark wood + cream letter), current (light wood, 1.12×, pulsing), future (faded "?").
- **Patrol cop** (cameo model): a 0–1 "step" that starts at **0.35**, +0.30 per wrong/miss,
  −0.18 per correct, gap lerped 24m→2.5m by step, hidden only below step 0.02.
  ⚠️ Consequence: **he is visible from GO! on a clean run** (~16.5m back) and only disappears
  after two correct picks — the code comments claim the opposite ("hidden until a wrong
  pick"). Placement uses live renderer bounds (foot-level match, lane from the collider's
  real x); he has no collider and `timesCaught` is hard-written 0 — structurally can never
  catch. The camera pull-back + amber vignette ride only the 3s menace timer, not his
  visibility.
- **Worlds**: `RaceWorlds.Apply` sets fog/ambient/sun/sky + a runtime post-processing grade
  (night floor −0.35 EV); `EndlessWorldDressing` (`[DefaultExecutionOrder(-2000)]`) forces
  theme + zone before their first Update, holds it every 5s, mixes primary/accent zone blocks
  (story-seeded), swaps + tints the sky dome, scatters the theme's own trees/grass (cap 14 per
  segment, blocked-area tested), and builds Shuriken weather per world table (rain/motes/snow
  on 6 of 10 worlds). ⚠️ Difficulty variation (`Vary`) affects **geometry/greenery only** —
  `Apply` deliberately reads the unvaried recipe, so lighting never changes with difficulty.
- **Input**: three coexisting paths — their swipe (1% drag), our tap (≤2% drag AND ≤0.45s;
  tap a third of the screen → runner steers to that lane, up to two lane steps), and keyboard
  **A/D only** (arrows left to their script; W/S removed). Tap blockers: pause chip, tracker,
  question plaque, + the 3 preview columns while tappable. Their Jump/Slide are guarded off in
  our mode.
- **Pause/leave**: pause chip → timeScale 0 + `AudioListener.pause` + their pause-menu
  suppressed; leave is a two-button confirm panel (NO left / YES right, no auto-disarm
  timer); leaving raises `RunAbandoned{"race_left_by_learner"}` (flushes the log
  immediately) → StorySelect. Android BACK = `RegisterAction(OpenPause)`. Focus loss is
  handled on both their side and ours; every exit path (resume, leave, focus, `OnDestroy`)
  restores `timeScale`/audio.
- **FINISH**: stop track + music stems, random finish dance (alt clip deliberately unwired),
  fireworks ×3, a 5-row finish card of the correct answers, `RaceResult` built
  (`collectedPieces` = the correct texts unconditionally; `firstPickCorrect` = the real
  picks), raise `RaceCompleted`, 3.2s beat → **Arrange**.
- **Log events**: `ElementCollected{elementIndex, wasCorrect, chosenOptionIndex, chosenText,
  lane, wasRepresent(false)}`, `RacePauseChanged`, `RunAbandoned`, `RaceCompleted`.

### Arrange [2] — `ArrangeController.cs`
Pieces = the 5 collected texts (`LastRaceResult.collectedPieces`, falling back to
`elements[i].correct` — same text either way), pool order shuffled. Tap piece → **every**
empty slot arms (brighten + 1.04× — deliberately never just the correct one); tap slot to
place; tapping a filled slot returns the piece; UNDO pops the last still-valid placement;
locked slots nudge and never respond. VERIFY (empty slots → "fill first", no attempt cost):
correct slots lock green one-by-one; wrong ones wiggle amber and return to the pool. All
correct → praise, `SetArrangeResult(attempts)`, `ArrangeVerified{correct:true, attemptCount,
placement}`, → Summary (then stays `_busy` so a second VERIFY can't inflate the count). Wrong
→ `ArrangeVerified{correct:false,…}`; hint after 3 misses of one element or 3 attempts
("Hint: <that element's tip>"). **After 4 attempts (`ArrangeMaxAttempts`) the game assists**:
fills and locks the remaining slots itself, raises `ArrangeVerified{assisted:true, placement:
null}`, → Summary. BACK blocked.

### Summary [7] — `SummaryController.cs`
Reference card lists **all five SWBST parts with their correct answers** (numbered,
colour-inked). Ghost placeholder is built from this story's Somebody + Wanted. Input capped
at **320 chars** (`SummaryMaxChars`) with a live countdown from ≤40 remaining. Submit: up to
**2 nudges** for (≥5 words / mentions the Somebody / one sentence) — targeted nudge text per
failure — then the **third submit accepts anything, including empty**. Accept: store
`LastSummaryText`, raise `SummarySubmitted{text, nudgeCount}`, → Results. BACK blocked.

### Results [8] — `ResultsController.cs`
Stars = `CalculateStars()` from race first-picks: **≥5 → 3★, ≥3 → 2★, else 1★** (null race
result → 1★ — display only). `CompleteStory(stars)` persists progress (`bestStars` never
decreases) and raises `StoryCompleted` **before** the reveal. Reveal (skippable via an
invisible full-screen catcher; unscaled time): victory sting → stars pop with rising pitch →
treasure chest + 5 SWBST letter-gems (lit per race first-pick; **no race result = all dim**)
→ praise by stars → "Your race: m:ss" → Lumi celebrates → main-idea panel → "You wrote: …"
echo card (skipped when the summary is empty). Exit button appears only after the reveal
(`finally` block, so it cannot be stranded). No BackButtonGuard registration (falls back to
the generic blocked card).

### TeacherMenu [11] — `TeacherMenuController.cs`
One input field runs a 5-state gate: EnterPin / CreatePin / ConfirmPin / Recovery /
ParticipantCode.
- **PIN**: min 4 chars; wrong ×5 → 30s cooldown (**static** — survives scene reloads, resets
  on app restart). First-run create+confirm; note **setting the first PIN does not grant
  Delete** — the wipe button is only active after a real PIN *entry* (`_pinVerified`).
- **Recovery**: empty box + hold OK 6s → "Reset this tablet?" → second tap →
  `TeacherGate.ResetDevice()` = clear PIN first, then `DeleteAllData` + `InitProfiles` →
  back to PIN setup.
- **Actions** (post-PIN; if the active learner has no participant code you are diverted to
  the code step first): participant code, switch learner, music toggle, unlock next session,
  export, delete-all (two-tap; disarmed by any other action).
- **Participant code**: 2–12 alphanumerics; outcomes Invalid / Duplicate (names the clash) /
  SaveFailed (rolls back) / Saved. "+ New learner" = code **before** NameEntry.
- **Export**: `ExportLogs` concatenates every learner's `.jsonl` into
  `export_yyyyMMdd_HHmm.jsonl` + a learner roster file; **backfills empty participantCode**
  onto old rows (targeted string replace, preserving unknown fields); path is shown pinned
  on-screen; "nothing to export" vs "failed" are distinct messages; missing/duplicate-code
  warnings are appended.
- **Delete-all**: raises `AllDataErased` FIRST (so the in-flight log row is discarded, not
  flushed), removes profiles + logs + `save.bin` + all `export_*` files; **keeps
  `settings.json`** so the tablet stays PIN-locked.

### Race [6] (legacy) — dead
`Race.unity` + `RaceController` still in the build; nothing routes into `SceneNames.Race`.
~51.5MB exclusive dependency closure. Candidate for removal after the first measured APK.

---

## 3 · The data layer (this is the thesis instrument)

### SessionLogService — **SchemaVersion 6** *(the Data Dictionary doc says 4 — doc is stale)*
One row per play-through, built purely from EventBus traffic. Row fields: `runId, isPartial,
learnerId, participantCode, storyId, startedIso, finishedIso, totalSeconds,
readingPageIndices, readingFirstChoices, readingFirstCorrect, readingSeconds,
raceFirstPickCorrect, raceFirstOutcome[5] (correct|wrong|missed), raceWrongPicks[5],
racePicks[] (element/option/text/lane/correct/represent/atSeconds, cap 64), raceSeconds,
raceRunSeconds, racePauseCount, racePausedSeconds, timesCaught (always 0 by design, D7),
arrangeAttempts, arrangeAssisted, arrangeSolved, arrangeOrders[] (placement digests, cap 12),
arrangeSeconds, nudgeCount, summaryText, summarySeconds, starsEarned, isReplay, session,
difficulty, narrationOn, lastPhase, abandonReason, backgrounded* (total + per-phase),
schemaVersion, appVersion, deviceId (SHA-256-derived, 12 hex), deviceModel, rowWrittenIso`.

Rules that protect the measures:
- **First answer per page only** (`_pagesRecorded` dedupe); race first-outcomes never filled
  by re-presented cards; `arrangeAttempts` protected from inflation by Arrange staying busy
  after handoff.
- `StoryStarted` flushes any previous row then opens a new one (fresh `runId`); `isReplay`
  marks re-plays of a completed story.
- Backgrounding writes a **partial snapshot** and keeps the live row; abandons flush
  immediately with `abandonReason` (`race_left_by_learner` / `learner_switched`); an
  abandoned/partial run is recognisable by empty `finishedIso`.
- A row with no data at all (left before any answer) is silently discarded — by design.
- `AllDataErased` **discards** the in-flight row (wipe never resurrects data).

### SaveManager
- **Atomic profile writes**: tmp → `File.Replace` (keeps `.bak`) → reads fall back to `.bak`;
  last-resort plain write if the atomic path throws (still counted as a storage issue).
- `AppendLog` refuses rows with empty `learnerId` (keeps editor traffic out of the dataset).
- Storage failures are surfaced via static `StorageIssueCount`/`LastStorageIssue`, which the
  teacher screen appends to its status lines. (The `SaveFailed` event exists but has **no
  subscriber** — the statics are the real channel.)

### TeacherGate
Salted SHA-256 (`"SummaRace:teacher:" + pin`), never a raw PIN; `SetPin` verifies by
re-reading from disk; `UnlockNextSession` rolls back on a failed write; `ResetDevice` clears
the PIN before wiping (vs the teacher Delete which keeps it).

---

## 4 · Where the CODE contradicts the DOCS (found this audit)

1. **Log schema is 6, not the 4 CLAUDE.md (F52) records** — CLAUDE.md is behind.
   (`SummaRace_Data_Dictionary.md` was already at 6; it carried two subtler staleness bugs —
   a wrong stars mapping and pre-F55 re-present semantics — both fixed 2026-08-22.)
2. **Summary cap is 320 chars**, but Results' echo-card geometry (and its own comment) was
   measured for the old 200 cap — mitigated only by ellipsis truncation.
3. Reader comment claims BACK exists "only on page 1"; in code the window is *any reading page
   before the first committed answer*, and a story with question-less pages would widen it.
4. `Bootstrapper`'s header says it "routes to MainMenu" — it branches to NameEntry; several
   other comments in Bootstrapper/StorySelect/GameText are historical narrative or stale
   (e.g. `GameText` points at a Reader method `OnOptionChosen` that doesn't exist).
5. `BackButtonGuard.Clear()`'s doc says "called by scene teardown" — no external caller exists.
6. **The patrol cop's own comments say "hidden until a wrong pick / hidden for a clean run"
   — the code disagrees**: his step starts at 0.35 (visible threshold 0.02), so he is on
   screen from GO! at ~16.5m and only vanishes after two correct picks. The `PatrolStartStep`
   constant's own doc describes the real behaviour; the two director comments don't.
7. **The re-present mechanism described throughout CLAUDE.md (F43/F46/F47) no longer exists**
   — it was deleted and replaced by a 2.2s on-panel answer reveal; `wasRepresent` in the log
   is permanently false. The docs' re-present narrative is history, not behaviour.
8. CLAUDE.md's race-pacing constants note is confirmed: `RaceSecondsPerGate` (20) and
   `GateTimeEasy/Average/Hard` have zero readers; the live levers are
   `RacePreviewLeadSeconds`/`RaceQuietRunSeconds`/`RaceFirstGateDistance`.
9. `RaceWorlds` has a `Vary(difficulty)` on the same struct `Apply` reads — but `Apply` uses
   the unvaried recipe, so the documented "easy warmer / hard cooler" lighting shift happens
   only via `ApplyTimeOfDay`, and `Vary`'s lighting fields are dead weight on that path.

## 5 · Defects & oddities found (candidates for the final update)

**Real defects:**
1. **SceneLoader's fade overlay cannot block input** — its code-built canvas has no
   `GraphicRaycaster`, so `blocksRaycasts = true` is inert; taps during a fade/load hit the
   scene underneath. (It also lacks a `CanvasScaler`, so tip text is raw-pixel-sized.)
2. **BackButtonGuard registrations leak across scenes** — nothing ever calls `Clear()`, and
   MainMenu/Boot register nothing, so BACK on MainMenu replays the previous screen's rule
   (blocked card after NameEntry; a "leave?" that reloads MainMenu after TeacherMenu).
3. **Ms. Lumi's mid-question alpha bug is real but visually benign** — measured live: alpha
   0.00 during the question, 1.00 the instant a correct answer lands (the cheer overrides
   the hide). BUT the QuestionPanel is a later sibling and draws over her, so nothing wrong
   appears on screen. Low priority; fix is one line (restore alpha after the cheer) if the
   panel layout ever changes.
4. **SceneLoader's missing-scene branch drops the pending queue**, and if the missing scene is
   MainMenu itself the branch does nothing at all (silent stall).
5. **Stars vs gems disagree on a null race result** (1★ vs all-dim gems) — acknowledged in
   comments; editor-only today, but it's the honest-display question to settle.
6. **Reader's option shuffle is unseeded and unlogged** — the on-screen slot a learner tapped
   is unrecoverable; only the story-space choice index is in the data. If position-bias
   analysis matters to the researcher, this needs a logged seed or slot index.
7. **HEAR AGAIN can be a silent button** when a page's narration path names a missing file
   (visibility checks the path string, not the clip).
8. **Third Summary submit accepts an empty string** — `summaryText` logs as `""`; decide if
   an empty accept is intended.
9. **NameEntry ignores the save result** — a disk failure at naming is silent (contrast
   `SetParticipantCode`, which rolls back and reports).
10. ~~The patrol is visible on every clean run from GO!~~ **CORRECTED BY LIVE RUN**: at rest
    step (0.29–0.35) his gap is ~16–22m behind the *player*, which is **behind the camera —
    measured live: active=true, gap 21.6m, in-frustum FALSE**. On a clean run he is spawned
    and animating but off-screen; he only enters the frame during a wrong-pick surge. The
    code comments ("hidden until a wrong pick") are right about what the *player sees*, wrong
    about the object's active state. No fix needed; docs should say "off-screen", not
    "inactive".
11. **Race weather emits mostly forward, not downward** — confirmed live (352 particles,
    speed 24): the emitter inherits only the camera's ~15° downward pitch, so rain travels
    ~15° below horizontal down the corridor rather than falling. Visible as faint forward
    streaks in the live frame. Needs a real downward rotation (or gravity) to read as rain.
12. **Which lane holds the correct answer is story-deterministic, not random** — the gate
    shuffle uses `UnityEngine.Random`, which `EndlessWorldDressing` re-seeds from the story
    id. Still uniform per gate, but **identical for every learner on a given story**; a
    validity consideration nothing documents (a learner shown the answer by a classmate on
    the same story sees the same lanes).
13. **A run-past gate raises no `ElementCollected`**, so `racePicks` has no row for missed
    gates — the miss appears only via `raceFirstOutcome` = "missed" after `RaceCompleted`.
    An abandoned run before FINISH therefore has *no trace at all* of gates the learner ran
    past (only unfilled outcomes). Worth confirming that's acceptable for analysis.

**Dead code / write-only state:**
- `GameRules` orphans confirmed by grep: `RaceSecondsPerGate`, `GateTimeEasy/Average/Hard`,
  `RaceLeaveConfirmSeconds`, `RacePatrolEnabled` (test-only), the seven `Patrol*` screen
  constants, and `DangerOnWrong/Relief/Max/AfterCaught` + `BoostSeconds`/`MaxLanes`/
  `LaneWidth`/`LaneSwitchSeconds`/`RaceAccel*` (legacy-race-only readers);
  `RaceGateTimerVisibleSeconds = 999` disables its own gating.
- Race: `EndlessOptionPickup.isRepresent` never set true (logged as permanent false);
  `Represent*` constants survive only to pad `RescheduleFinish`; `goldPillSprite` serialized
  but unused; `finishDanceClipAlt` deliberately unwired; `RaceWorlds.Forget()` zero callers.
- `learner.avatarIndex` is write-only (the chosen runner icon affects nothing else);
  `GameManager.LastArrangeAttempts` write-only (log gets attempts via the event);
  `SaveFailed` event has no subscriber; `SceneNames.Race` has no inbound route.
- Reader page-turn punch is unreachable (card carries `PanelIntro`); progress bar hits 100%
  before the last question is answered.

**By-design (do not "fix"):**
- No-learner fallbacks unlock everything (editor-direct play support).
- Wipe keeps `settings.json` (PIN survives) while recovery-erase clears the PIN first.
- `timesCaught` always 0 (D7); empty name → "Runner"; empty log rows discarded.

---

## 6 · State before the final update

- ✅ Full loop code-complete; an APK has run on a real Android phone (2026-08-21).
- ⚠️ **Player builds blocked** by the Adjoint/MCP `CS0433` duplicate-DLL collision — the
  `Packages/manifest.json` fix exists on disk but is **uncommitted**, and a full Editor
  restart is required (a recompile is not enough).
- ⚠️ APK ≤ 300MB and 30fps on the 2GB floor device are still **estimates, not measurements**.
- ⬜ Owner items: prove USB export on a real tablet before day 1, set PIN + participant codes
  at install (the current test learner has an empty `participantCode` — the export will warn),
  regenerate the Data Dictionary against schema 6, decide the §5 defect priorities (weather
  direction and the input-blocking overlay are the top two; the patrol and Lumi items were
  downgraded by the live run).

---

## 7 · Live Play-mode verification (2026-08-22, driven via MCP in the Editor)

One complete run of a **never-before-playtested story** (`s05_average`, "Owen and Mzee",
Mission 5), with portrait 1080×1920 screenshots at every screen. What it proved:

**Flow, all live**: Boot → MainMenu ("Playing as Maria") → SessionMap (1–5 open, 6–10
locked) → StorySelect (EASY cleared/REPLAY, MEDIUM next/gold ring, HARD locked) → Reader
(5 pages, 5 questions) → race briefing → 3-2-1-GO → 5 gates → FINISH → Arrange → Summary →
Results → NEXT STORY. Zero errors in the console for the whole run.

**Reader**: page/question badges, progress bar, SWBST hint line, A./B./C. options, VOICE +
HEAR AGAIN chips, BACK chip all render correctly. Wrong answer verified: correct option
turns bright green at full opacity (F51 fix holds), tapped option sinks and dims, feedback
"Not quite — here is the answer!", no red, no blocking. Lumi alpha bug measured (see §5.3) —
visually benign.

**Race** (world `overcast_industrial`, rain active): briefing named the story with 5 SWBST
chips and Lumi; START gated until boot ready; tracker + question plaque + pastel SOMEBODY
tag + 3-column option preview + "Next part coming up" chip all on screen exactly as coded.
Run finished in 83.5s at 30 m/s. **Patrol measured**: active the whole run but out of
frustum at rest (gap 21.6m = behind the camera) — visible only on the wrong-pick surge.
**Weather measured**: 352 particles at speed 24 pitched only ~15° down (the camera's own
pitch) — the forward-rain defect is real on screen.

**Arrange**: Lumi badge + bubble, five pastel slots with their SWBST words kept visible,
tap-piece-tap-slot worked, solved in 1 attempt → Summary.

**Summary**: reference card with the five parts, story-specific ghost text, submit accepted
a 146-char one-sentence summary on the first try → Results.

**Results**: "Mission Cleared!", **2/3 stars — exactly right** for 3 correct first picks,
race time "1:24" (= the measured 83.5s), Main Idea card, verbatim "You wrote:" echo, and
the five SWBST gems lit in **precisely the first-pick pattern S✓ W✗ B✓ S✓ T✗**.

**The data row** (read from the learner's `.jsonl` after the run): `schemaVersion 6`,
`readingFirstCorrect [F,T,T,T,T]` (the one deliberate wrong answer), `raceFirstPickCorrect
[T,F,T,T,F]`, `racePicks` carrying the exact distractor text + lane for both wrong gates,
`arrangeAttempts 1` + order `"01234"`, the summary verbatim, `starsEarned 2`,
`timesCaught 0`, all four phase clocks filled, `finishedIso` set, `isPartial false`.
**The instrument records exactly what happened on screen.**
