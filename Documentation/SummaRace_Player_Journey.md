# SummaRace — The Player Journey, End to End

**Written 2026-08-20 · branch `experiment/endless-override-2` · verified against HEAD `bbbc72d`**

What a learner actually does, in order, from tapping the app icon to leaving a finished story —
and the evidence for each claim. Nothing here is quoted from the design documents. Every step was
read out of the code that ships, and every number was measured. Where the build and the GDD
disagree, **this file describes the build**.

Read alongside `SummaRace_Owner_Handover.md` (state and critical path). This file answers one
question only: *what is it like to play?*

---

## 0. The one-line version

> A learner reads a five-page story with the words on screen, then runs a race that asks the same
> five questions with the words **gone**, then puts the five pieces in order, then writes the
> summary in their own words — and is never once blocked, failed, or told they lost.

That ladder is the whole design. Each step removes a support. It is also the measurement: the gap
between how a learner does *with* the text and *without* it is what the thesis reports.

---

## 1. Opening the app — Boot

**~2 seconds.** No input. The learner watches.

A crown, the stacked **SUMMA / RACE!** logo, a tagline, a "Loading..." label and a gold progress
bar that fills over the beat.

Behind the splash, the app builds its four permanent services — `GameManager`, `AudioManager`,
`SaveManager`, `SceneLoader` — on a `[Core]` object that survives every later scene change.

> **Justification.** `Bootstrapper.Start()` holds for `GameRules.SplashSeconds` (**2f**,
> `GameRules.cs:413`) while driving `splashFill.fillAmount`. The singletons are created in
> `Bootstrapper.Awake()` before the beat starts, so the bar is honestly showing *elapsed time*,
> not fake progress — the comment in `Start()` says exactly that.
>
> **Corrected 2026-08-20.** The bar and label were wired to `{fileID: 0}` in `Boot.unity` and
> only null-guarded in code, so **neither had ever appeared** since F15. `EnsureSplashChrome()`
> now builds both at runtime when the scene does not supply them, following the same pattern as
> `ReaderController.EnsureSecondaryControls`. Verified present in `Assembly-CSharp` by reflection.

**Where it goes next** is decided once, by the save file:

| Condition | Destination |
|---|---|
| Fresh device — learner has never been named | **Name Entry** |
| Every launch after that | **Main Menu** |

> `Bootstrapper.cs:164` — `SceneLoader.Go(needsName ? SceneNames.NameEntry : SceneNames.MainMenu, false)`,
> where `needsName` reads `LearnerProfile.named`.

---

## 2. First run only — Name Entry

**Once per tablet.** The learner types a name and taps one of four avatars.

The four avatars differ in **both shape and colour** — heart, star, gem, lightning — because an
earlier pair of near-identical gold stars was indistinguishable in a portrait render.

An empty name box is **never an error**. A "DONE TYPING" chip closes the Android keyboard.

> **Justification.** `NameEntryController.cs:76` wires the four avatar buttons, `:80` the confirm.
> `EnsureDoneTypingChip()` (`:82`) builds the keyboard-dismiss chip at runtime — the field is
> `{fileID: 0}` in the scene, so this is the only reason that control exists on device.
> Exit is `SceneLoader.Go(SceneNames.MainMenu)` at `:134`.

> **Study note.** The learner types a *name*. The **participant code** — the join key to the paper
> pretest/posttest — is a separate value set by the teacher behind the PIN, never by the child.
> See `SummaRace_Study_Operations_Runbook.md` §1.2b.

---

## 3. Main Menu

One large **TAP TO START**, and a deliberately low-contrast teacher corner.

> `MainMenuController.cs:86` → `OnStartTapped` → **Session Map** (`:177`).
> `:89` → the teacher corner → **Teacher Menu** (`:93`). The corner is *discreet, not hidden* —
> everything behind it is PIN-gated anyway.

---

## 4. Session Map — choosing the day

Ten numbered stops snaking bottom-to-top, so the ten sessions read as **one route**, not a grid.

A locked stop is a friendly "not yet" — it plays a nudge and stays put. It never scolds, because a
locked session is **never the learner's fault**: sessions are opened by the teacher's PIN.

> `SessionMapController.cs:155` wires an open stop to `SelectSession(number)`; `:160` wires a locked
> one to `PlayLockedNudge`. Exits: **Story Select** (`:205`), **Main Menu** via BACK (`:77`).
> `GameRules.SessionCount` = **10** (`GameRules.cs:391`).

---

## 5. Story Select — choosing the difficulty

Three cards for the chosen session: **EASY**, **AVERAGE**, **HARD**.

- EASY is **always open**
- Each later difficulty waits on the one before it
- Every card shows the story's hero art, title, and a 3-star row of the learner's best result
- A locked card nudges and explains; it never dead-ends

> **Justification.** `StorySelectController.cs:207` wires an unlocked card to `SelectStory(id)`,
> `:212` a locked one to the nudge. Exits: **Reader** (`:249`), **Session Map** (`:122`).
> The unlock ladder is covered by the test
> `ProgressionRuleTests.EasyIsAlwaysOpenAndEachDifficultyWaitsOnTheOneBefore` (passing).
> All 30 hero images resolve — `ResourceContractTests.EveryHeroImageResolvesToASprite` (passing).

---

## 6. Read — the story, with the words on screen

**Five pages.** This is the scaffolded rung of the ladder: everything is visible and nothing is
being tested for the first time.

Per page the learner gets:

1. **The passage**, on a card
2. **Narration** — a recorded voice reads it aloud (Filipino-English, `en-PH-RosaNeural`, slowed 10%)
3. **A progress bar** that sweeps — not snaps — to show how much story is left
4. **NEXT**, which reveals **one question** about that page
5. **Three options**, labelled `A. / B. / C.`, in a **seeded shuffle** so the answer is not always in the same place

Answer it and the story card hides so the question owns the screen. Get it right — punch-scale and
praise. **Get it wrong and nothing bad happens:** the correct option is highlighted *and*
punch-scaled, and NEXT continues. The learner is never held back.

Two small controls: **VOICE** (narration on/off, persisted) and **HEAR AGAIN** (replay this page).
A two-tap **BACK** exists, but only *before* the first answer — after that the run is study data.

> **Justification.** `ShowPage()` (`:150`) sets text, progress and resets `_questionAnswered`.
> `OnNext()` (`:277`) is explicitly `page → question → next page`. `OnAnswer()` (`:349`) latches
> `_questionAnswered` and sets `_answerCommitted`, which removes the exit (`:356-357`).
> `EnsureSecondaryControls()` (`:137`) builds the replay and BACK chips at runtime —
> both fields are `{fileID: 0}` in `Reader.unity`, so this is why they exist on device.
> Narration: all **150** clips resolve (`ResourceContractTests.EveryNarrationPathResolvesToAnAudioClip`).
> Shuffle fairness: `AnswerPositionTests.TheReaderPutsEveryOptionInEveryOnScreenSlotAboutEquallyOften`.
>
> **Measured validity.** "Always tap the longest option" scores **29.3%** across all 150 questions —
> *below* the 33.3% chance floor. The learner cannot pass by option length.

**Exit:** the last page routes to the race (`:457`).

---

## 7. Race — the same five questions, with the words gone

This is the unsupported rung, and the **headline measure** of the study.

### 7a. The briefing

Before anything moves: a gold card names the story and shows the five SWBST slots as coloured
chips, with Ms. Lumi presenting. **START!** is deliberately non-interactable for the first moment —
it reads "Getting ready…" until the underlying track has finished booting.

Then a giant gold **3 · 2 · 1 · GO!** on a low tracking camera that arcs up into the chase view.

> `EndlessRaceDirector.BuildBriefing()`; `CountdownRoutine()`; `MarkBriefingReady()` gates the
> button. `Update()` re-asserts `TrackManager.StopMove()` **every frame** until GO! — stopping once
> loses, because the underlying game turns movement back on later.

### 7b. The run

The learner's character runs forward automatically. They only steer.

**Five gates.** Each gate is one SWBST slot in order — **S**omebody, **W**anted, **B**ut, **S**o,
**T**hen. Every gate presents **three lanes, three answers**, and — critically — the same three
options are *also* shown as readable UI text in the sky band above the road.

Controls: **tap a third of the screen** to go straight to that lane (one tap to any lane), swipe,
or WASD on a keyboard.

A persistent five-slot **tracker** across the top shows which slots are filled and which is next.
A correct answer's word **flies out of the card into its slot**.

| | |
|---|---|
| Race length | **1685–2201 m** |
| Race duration | **89–107 s** |
| Reading time per gate | **17.1–23.2 s** |
| Gates | 5 + finish |
| Lanes | 3 |

> **Justification.** Simulated against the real track (`minSpeed` 10, `maxSpeed` 30,
> `k_Acceleration` 0.2) with `RaceSecondsPerGate` **20**, `RaceFirstGateDistance` **200**,
> `RaceMaxGateGap` **800**. The reading window meets the ~16.5 s a 70 wpm struggling reader needs —
> owner decision **D2 is closed**, and it closed by fixing the `secondsFloor` integration
> (`d = v·T + ½·a·T²`) rather than by changing a constant.

### 7c. What happens when you collect a WRONG item

Asked directly, so recorded precisely. In order:

1. A "not quite" sound
2. **The correct answer is shown, in gold, for a beat** — it teaches, it does not scold
3. **The patrol (a cop) surges into view for 2 seconds**, then drops back out of frame
4. The run slows to 60% speed for 1.5 s
5. That gate disappears; the next is already coming

**The patrol appears *only* on a wrong collect.** He is not something you outrun, and he is not
connected to collecting. **He never catches anyone** — there is no fail state.

> **Justification.** `HitWrong()` (`EndlessRaceDirector.cs:1155`) in order: `SfxNotQuite` →
> `ShowFeedback(_story.elements[...].correct, Theme.StoryGold)` →
> `_menaceTimer = GameRules.PatrolMenaceSeconds` (**2f**) →
> `track.maxSpeed = Mathf.Max(track.minSpeed + 1f, track.speed * 0.6f)` for
> `GameRules.SlowSeconds` (**1.5f**) → `DestroyActiveGate()` → `ShowAnswerReveal(element)`.
> `timesCaught` is written as a literal `0` at `:1572` — GDD **D7**.

**The only cost of a wrong answer is a star.** Getting every gate wrong still finishes the race and
still hands the learner all five pieces for the next screen.

> **Measured validity.** "Ignore the words and tap the widest card" scores **36.0%** against a
> 33.3% chance floor, mean margin **+1.0 characters**. Before this was fixed the same strategy
> scored **84.7%** — the race was winnable without reading, which would have made the instrument
> unable to tell a strong summariser from a fast tapper. Guarded by
> `RaceCardValidityTests` (7 tests, passing).

A **pause chip** sits in the top-right; leaving is a deliberate two-tap.

**Exit:** FINISH → **Arrange** (`:1591`).

---

## 8. Arrange — put the five pieces in order

Five slots labelled with the SWBST types, five word-pieces below. Tap a piece, tap a slot.

- A correct placement **locks** and stops responding
- A tap on a locked slot **nudges** — it never swallows the tap silently
- **UNDO** is available

**The learner cannot get stuck.** After `ArrangeMaxAttempts` (**4**) the game solves it *for* them
and moves on.

> `OnPieceTapped` (`:149`), `OnSlotTapped` (`:160`), `NudgeLockedSlot` (`:170`).
> The give-up path: `if (_attempts >= GameRules.ArrangeMaxAttempts)` → `AssistRoutine()` (`:335`),
> which exits to Summary. `ArrangeMaxAttempts` = **4** (`GameRules.cs:383`) — set so the hint
> arrives *before* the assist does.

**Exit:** **Summary** (`:315` on success, `:426` after assist).

---

## 9. Summary — write it in your own words

The final rung: no options, no pieces. A text box and the learner's own sentence.

Light checks nudge at most **twice** — and the nudge is chosen to match *what actually stopped the
sentence*, not walked in list order. After two nudges the game **accepts whatever is there**,
including nothing.

> `OnSubmit()` (`:224`): `if (_nudgeCount < GameRules.SummaryMaxNudges && !PassesLightChecks(text))`
> → nudge and return; otherwise `Accept(text)` (`:241`). `SummaryMaxNudges` = **2**
> (`GameRules.cs:388`). `NudgeIndexFor()` picks by failure type — a learner who wrote three good
> lines but never named the Somebody used to be told "try writing a little more", advice for a
> different problem that cannot be acted on.

**Exit:** **Results** (`:300`).

---

## 10. Results — stars and treasure

The reveal, in order: stars pop in, then a treasure chest opens five SWBST-coloured letter chips —
full colour for a slot answered right first time, dimmed for one that was missed.

| First-pick correct (of 5) | Stars |
|---|---|
| 5 | ★★★ |
| 3–4 | ★★ |
| 0–2 | ★ |

**Nobody ever scores zero stars.**

> `GameManager.CalculateStars()` (`GameManager.cs:98`): `if (LastRaceResult == null) return 1;` then
> `StarsThreeMin` (**5**) and `StarsTwoMin` (**3**). Guarded by
> `ProgressionRuleTests.StarsCountFirstPicksAndAreNeverZero` and `StarThresholdsFollowTheGddLadder`.
> `ResultsController.cs:95` reads it; `:112` calls `CompleteStory(stars)` which persists progress.
>
> The star count **is** `raceFirstPickCorrect` — the study's headline measure. What the child
> sees as a reward and what the researcher analyses are the same number.

**Exit** (`:336`): back to **Story Select** for the next difficulty, or — if this was the third
story of the session — to the **Session Map** with a celebration.

---

## 11. The loop, as a whole

```
Boot ──► Name Entry (first run only)
  │           │
  └──────► Main Menu ──► Session Map ──► Story Select
                │              ▲              │
                │              │              ▼
          Teacher Menu         │           Reader
           (PIN-gated)         │              │
                               │              ▼
                               │           RACE ──► Arrange ──► Summary ──► Results
                               └──────────────────────────────────────────────┘
```

**Total for one story: roughly 6–9 minutes.** A session is three stories. The study is ten sessions.

---

## 12. Justification — why I am confident this is what ships

| Claim | How it was checked |
|---|---|
| Every step above | Read from the controller that runs it, cited by `file:line` |
| Nothing dead-ends | All 33 `SceneLoader.Go` call sites mapped; **every scene has at least one exit** |
| Every button works | All listeners enumerated; scene wiring parsed from `.unity` YAML; every unwired field confirmed to have a runtime `Ensure*` builder |
| Content is real | **53/53** EditMode tests pass (3.7 s) — 30 stories through the real loader, 150 narration clips, 30 hero images, 31 audio keys |
| Never punishes | Verified per phase: Reader reveals, Race cannot fail, Arrange auto-assists at 4, Summary accepts anything after 2 nudges |
| Measure is sound | Heuristics re-measured on the live JSON: race widest-card **36.0%**, Reader longest-option **29.3%**, both at or below the 33.3% chance floor |

### What this document does NOT prove

**No part of this has ever run on an Android tablet.** No APK has ever been built. Everything above
is verified in the Unity Editor, where Addressables resolve straight off the Asset Database.

Two hard blockers stand, both confirmed by `SummaRace ▸ Build Preflight` on 2026-08-20
(**2 FAIL, 2 WARN**):

1. **Android Build Support is not installed** — no APK can be produced
2. **Addressables have never been built for Android** — content exists only for Windows
   (`Library/com.unity.addressables/aa/Windows`). Without `aa/Android`, `TrackManager.instance`
   stays null forever and **the race never starts at all**

Until one APK runs on one real tablet, this file describes a game that is complete **in the Editor**.
