# SummaRace — an outside critique

**Written 2026-08-06 · branch `experiment/endless-override-2` · HEAD `a4ed8bd`**
**Read `SummaRace_Owner_Handover.md` first. This document argues with it.**

I was asked for a senior developer's honest opinion of the game, not a QA pass. I read
`CLAUDE.md`, the Owner Handover, `SummaRace_Pro_Review.md`, `EndlessRaceDirector.cs` in full,
every file in `Core/`, every feature controller, `GameRules`/`GameText`, the validity test
fixtures, and the 30 story JSONs through a script. Where I measured something I say so and give
the number. Where I am giving an opinion I label it.

I have tried hard not to manufacture criticism. There is real craft here — §3.1 names it
specifically — and the previous review was largely right. But it missed the thing that matters
most, and so did the other twenty-odd passes that ran through this codebase today. That is §5.

---

## 0. The verdict in five sentences

The engineering is well above the standard of a student project and above the standard of most
shipped mobile games in exactly one respect: care about not harming the user. The game itself is
thin — one verb, used five times, thirty times over — and I do not believe a nine-year-old wants
session 7. The instrument does not measure what the documents say it measures: **I measured that
the race can be passed 84% of the time by remembering the Reader's answer and picking the card
that looks most like it, against 33% for guessing**, and the project's own validity tests guard
the one channel that does not matter while leaving that one unguarded. The code is good, in a
way that will be hard to maintain by anyone who is not an LLM. And the single largest risk in
this project is not the missing APK — it is that a thesis instrument's *content* has been
rewritten at scale, by machines, without the researcher reading it.

---

## 1. Is this a good game?

### 1.1 The loop is one good idea, repeated past the point where it is one

Per story: read 5 short pages → answer 5 questions → run a race that asks the same 5 things →
arrange the same 5 texts → write one sentence from those same 5 texts displayed on screen.
Three stories per session. Ten sessions.

Say that back plainly: **a learner meets the same five sentences four times inside seven
minutes, and does that thirty times.** The pages average 74 words in total (measured across all
30 JSONs; median 69, mean 14.8 words per page). So the content of one whole story is about the
length of this paragraph, and the game asks about it four times.

That is not automatically bad — worked examples plus spaced retrieval is real pedagogy, and I
will not pretend otherwise. But it is a *drill*, and it is presented as a game. The gap between
those two framings is where a child's engagement goes. My opinion, clearly labelled: **novelty
carries sessions 1–3; by session 5 the loop is homework with a runner attached.** I agree with
Pro Review's timing and I would go further — the repetition inside a *single* story is more
corrosive than the repetition across sessions, because a child can feel it immediately.

### 1.2 The race has one verb and eight seconds of nothing between uses

I verified the mechanics directly. `TrackManager.SpawnObstacle` (`Assets/Scripts/Tracks/TrackManager.cs:552`)
and `SpawnCoinAndPowerup` (`:582`) both early-out in our mode; `CharacterInputController.Jump`
(`:322`) and `.Slide` (`:356`) likewise. So between gates there is: a road, a chaser who is
deliberately invisible unless you got something wrong, and nothing else. Five lane taps per run.

`NextGateGap` derives the gap from `RaceSecondsPerGate` (12) × difficulty × current speed, so
per-gate *time* is roughly constant at ~15 / 12 / 9.6 s until `RaceMaxGateGap` (300 m) bites. The
option preview panel is up for the whole approach. So a confident reader decides in two seconds
and then waits eight.

Here is the part I find genuinely wrong-headed, and it is a design decision rather than a bug:
**the game is at its emptiest for the learner who is doing best.** The chaser only appears after
a mistake (`_menaceTimer`, set in `HitWrong`). A learner going 5/5 sees a bare corridor for
seventy-five seconds. The one dynamic element in the scene is a reward for failure.

I agree with Pro Review that centre-lane coin lines are the right patch and the wrong week. I
disagree with its framing that this is a misreading of the GDD. It is a *reasonable* reading of
"never punish the learner" that happened to also delete every reason to look at the screen
between gates. The fix is not coins specifically — it is that **something the learner controls
has to exist between gates**, or the race is a loading screen with a runner in it.

### 1.3 Where it genuinely delights

Not everything is thin, and I want to name what works before I go on.

- **The 3-2-1 with the cinematic dolly** (`CountdownRoutine` / `OrbitStartCamera`) is a real
  moment. It arcs from behind-left up into the chase pose on GO!. That is a AAA beat in a
  thesis project and it will land.
- **The collected word flying off the card into its SWBST plaque** (`FlyCollectedToSlot` +
  `RefreshTracker`) is the best single piece of design in the game. It teaches the framework,
  answers "what next?", and rewards the pick, in one gesture. This is the idea the whole race
  should have been built around.
- **The tone.** `GameText` has been written by someone who thought about a nine-year-old reading
  it under pressure — "Take a break!", "KEEP RUNNING", "Not quite — here is the answer!",
  "CHECK ORDER" instead of "VERIFY". The comments explaining *why* each string was changed are
  better reasoning than most localisation reviews I have seen.

### 1.4 Where it confuses

**The thing you read and the thing you steer into are in different places.** After F47 the three
options are legible only on a HUD board in the sky band (screen y 0.745–0.885); the cards on the
road are, by the project's own arithmetic in `GameRules.cs:91-116`, unreadable at any useful
distance. So the learner reads at the top of the screen, then maps left/centre/right onto three
white rectangles at the bottom, then steers. For an ESL nine-year-old under a twelve-second
clock, that mapping is a working-memory tax laid on top of the comprehension task, and the
briefing never mentions the panel exists (`GameText.RaceBriefingBody`).

The accidental good news: `EndlessTouchInput` maps taps to screen thirds, so **tapping the option
you want on the panel already works** — it is just not deliberate, and the panel's columns and
the screen thirds disagree by ~0.6% at the boundaries. Making the panel columns real buttons
would collapse read-and-choose into one act and remove the split. That is the cheapest real
improvement to the moment-to-moment experience in the whole game, and it is maybe 45 minutes.

### 1.5 Stars

`StarsThreeMin = 5`, `StarsTwoMin = 4`. 3/5 — comfortably above the 33% chance floor — shows the
identical screen as 0/5. In a game whose north star is "never punish the learner", the modal
learner will see one star, thirty times. `starsEarned` is a pure function of
`raceFirstPickCorrect`, which is logged separately, so the curve is purely motivational and free
to change. Pro Review says set `StarsTwoMin` to 3. I agree, and I would go further: make 5/5
three stars, 3–4 two stars, and 1–2 one star. The measure is untouched; the child stops being
told they failed for getting 60%.

---

## 2. Is it a good instrument?

Short answer: **no, not as documented — and the gap is measurable, not rhetorical.**

### 2.1 The support-removal ladder runs backwards

`CLAUDE.md` states the design as Reader (text visible, pre-teach) → Race (text gone, unsupported
recall) → Arrange (sequence) → Summary (produce). I checked all four rungs against the build.

| Stage | What the docs say | What the code does |
|---|---|---|
| **Reader** | text visible while answering | `ReaderController.cs:267` — `readingCard.SetActive(false)` the moment the question appears. **Text is gone.** |
| **Race** | unsupported recall | Same five slots, same order, 84% pickable from memory of the Reader's answer (§2.2). |
| **Arrange** | sequencing | `ArrangeController.cs:395` — slots are labelled `_story.elements[i].type` (SOMEBODY, WANTED, …) in the correct order, colour-coded. The five pieces are always the **correct** texts regardless of race performance (`EndlessRaceDirector.cs:1255`). It is a matching task with the answer key half-supplied. |
| **Summary** | produce | `SummaryController.cs:71` prints all five correct SWBST texts, numbered 1–5, in order, on the same screen as the input box. `GameText.SummaryHint` supplies the sentence frame: *"Somebody wanted ___, but ___, so ___, then ___."* `SummaryNudges[0]` literally instructs: *"use the story parts above!"* |

Read the right-hand column as a difficulty curve. **Stage 1 is the hardest and support goes up
from there.** By stage 4 the learner is filling a supplied template with five supplied strings.

Pro Review caught rungs 1 and 3. It called rung 4 "the richest datum in the log" and said "it
works". It does not work as a production measure. `summaryText` is the only free-text field in
the study, and the model answer is on screen while it is typed. Whatever rubric the thesis
applies to those sentences will be scoring transcription fidelity as much as summarising. That
is not a bug — `TDD §10.3` asks for the reference list — but the write-up cannot describe it as
production without qualification.

### 2.2 The measured hole: the race is 84% passable from memory

This is the finding. I ran it, then re-ran it myself independently of the agent that found it.

Strategy simulated: *understand nothing; remember the Reader's correct answer for slot i; at
race gate i, pick the card whose text is most similar to it.* Similarity = `difflib`
`SequenceMatcher` on normalised strings.

| | Score |
|---|---|
| **This strategy, all 150 slots** | **126 / 150 = 84.0%** (agent's independent run: 83.3%) |
| Chance | 33.3% |
| Misaligned control (match slot *i* against race element *i+2*) | 38.7% |
| Reader-correct **byte-identical** to race-correct | **31 / 150** |
| Word-set Jaccard = 1.0 | 32 / 150 |
| Per-story: 5/5 | **13 of 30 stories** |
| Per-story: 4/5 or better | 24 of 30 stories |

The misaligned control is the important line. 38.7% versus 84.0% means this is not a generic
artefact of short similar strings — it is **slot-specific text carried from the Reader into the
race**. A pure memoriser earns 2 or 3 stars in 24 of the 30 stories.

Verbatim examples, from the files:

- `s09_easy` Somebody — Reader correct `Cora` / race correct `Cora`
- `s10_easy` Somebody — `Ocean waves` / `Ocean waves`
- `s10_average` Wanted — `To help people in need` / `To help people in need`
- `s07_average` Then — `Slice and Dice became Chefs, Pierre the Frog Keeper`, byte-identical
- `s01_easy` — the hand-checked gold standard — Somebody `Molly`/`Molly`; But `Bella would not
  give her a turn` / `Bella would not get off the swing`

Now the part that makes this a *systems* finding rather than a content finding.
`Assets/_Game/Tests/Editor/DistractorReuseTests.cs` opens with this class comment:

> *"If a gate's two distractors are the very strings the learner just saw rejected on that page,
> the gate can be cleared from memory of the Reader screen rather than from comprehension of the
> story — and `raceFirstPickCorrect` is the study's measure."*

The mechanism is identified exactly. It is then guarded on the **distractors** only, with three
tests, including an absolute one (`NoRaceDistractorIsAnAnswerTheReaderTaughtAsCORRECT`). Nothing
anywhere guards the **correct** card against the string the learner just saw *accepted* — which
is the same exploit through the door that is wide open. Guarding the foils while the answer is a
copy is guarding the wrong side of the door.

**Consequence for the thesis:** `raceFirstPickCorrect` *is* the star count and *is* the headline
logged measure. As it stands it is a measure of within-session verbatim retention over a 60–120
second delay, not of summarising. It will correlate with working memory and attention at least
as much as with the construct, and a learning gain across sessions will be partly a gain in
holding five short strings in mind.

**What I would do about it, today, at near-zero cost:** nothing to the code. Add one line to the
thesis's limitations, and — if the researcher has an hour — paraphrase the race's `correct` line
away from the Reader's wording in the ~50 slots where similarity is highest. That is an
`overrides.json` edit and a pipeline re-run. But see §5 before touching content again.

### 2.3 The Reader is exploitable too, by lexical matching

Strategy: *pick the option that shares the most words with the page you just read.* Expected
score with random tie-break: **81.3%** (115/150 outright wins, 14 ties). Mean overlap margin,
correct minus mean distractor: **+0.365**.

Caveat I want to state fairly, because the agent that measured it did not: the reading card is
hidden during the question, so this is word-matching against *remembered* text, not visible text.
But the pages average 14.8 words. A child does not need comprehension to hold one sentence for
four seconds and pick the echo.

### 2.4 What has genuinely been fixed, and deserves saying

I re-derived these rather than trusting the docs.

- **The card-width tell is really gone.** Race: correct is strictly the longest option in
  **45.3%** of sets, mean margin **+1.37 chars**. `CLAUDE.md`'s claim verifies exactly.
- **The Reader was fixed too, and is cleaner than the race**: correct is strictly longest in
  **29.3%** — *below* the 33.3% null. "Tap the longest" now scores at or under chance in both.
- **Position bias is neutralised at runtime.** The JSONs are badly biased (`correctIndex`
  distribution across 150 pages: {0: 48, 1: 96, 2: 6} — B is correct 64% of the time), but
  `ReaderController.ShuffleDisplayOrder` and `EndlessRaceDirector.PlaceAnswerGate`'s Fisher-Yates
  both reshuffle per item, and `AnswerPositionTests` guards it.

That is three real validity threats found and closed. The work was good. It just did not look at
the largest channel.

### 2.5 `arrangeSolved` is close to a coin flip

I simulated the real mechanic (random permutation each verify, correct slots lock,
`ArrangeMaxAttempts = 4`), 200k trials: **47.7% solve rate on pure chance.** That matches Pro
Review's 47.8%. With the free cue that the Somebody piece is the shortest, it goes higher. So
`arrangeSolved` carries very little information, and the assist path means the phase always
completes anyway.

The good news is that `arrangeOrders` (schema 5) now logs the *actual board per attempt*, which
turns this from a near-useless boolean into a per-slot SWBST confusion matrix. That was the right
call and it is the most valuable new field in the log. I would not change the mechanic two days
out; I would just stop reporting `arrangeSolved` as a measure and report the confusion matrix
instead.

### 2.6 A quieter measurement problem nobody has named

Every phase clock in `SessionLogService` is `Time.realtimeSinceStartup`. Only the **race** pause
is netted out (`racePausedSeconds`). So `readingSeconds`, `arrangeSeconds` and `summarySeconds`
silently include any interval where the tablet was backgrounded, locked, or sitting in a
teacher's hand. In a classroom of forty children that is not an edge case, and time-on-task is
one of the few process variables a summarising study can actually use. Cheap fix: record
app-background intervals in `OnApplicationPause` and subtract, or just log them alongside so the
researcher can filter. Half an hour.

---

## 3. Is the code good?

Mostly yes, and in unusual ways. Then some things I would refuse.

### 3.1 What is genuinely well made

I am naming these once each and moving on, but they are real.

- **`SessionLogService` is the best file in the project.** The distinction between provisional
  and resolved first-outcomes (`ResolveFirstOutcomes`); the rule that a re-present may never fill
  a first outcome; `runId` + `isPartial` so a backgrounded run costs a snapshot rather than the
  rest of the story; `HasData` so a double-tapped story card does not write a fake abandonment;
  the reasoning about why `OnRunAbandoned` deliberately does *not* run `ResolveFirstOutcomes`.
  That is measurement discipline, and it is better than most published instruments manage.
- **The monotonic `_gateSerial`.** The bug it fixes is genuinely subtle: a wrong pick calls
  `HitWrong → ScheduleRepresent → TryPlacePending` *synchronously*, re-arming `_activeElement`
  to the same index, so the dead gate's other card passed an index check moments later. A latch
  has the identical hole. A never-reused id is the correct fix and the comment explains exactly
  why the obvious fixes fail.
- **Geometry derived from live data, not tuned by feel.** `TriggerWidth = 0.85` is chosen so that
  `0.425 + 0.288 = 0.713 < 0.75` — i.e. it is *geometrically impossible* to be inside two lanes'
  triggers at once. `UpdatePatrol` reads the cop's rendered bounds every frame because his
  animated mass is up to 0.79 m off his transform. `LateralTarget` expresses his position as a
  fraction of the view half-width read off the live camera, so a camera retune carries him. This
  is how you make a thing that survives three camera changes.
- **`SceneLoader.Go`'s null fallback**, and the `LoadSceneAsync == null` guard that stops a
  missing build-list entry from freezing the app behind an opaque overlay. Both are exactly the
  right shape.
- **Every controller's editor-direct fallback routes to Story Select rather than returning.**
  Consistently applied. TDD §13 actually holds.
- **Atomic profile writes** with `File.Replace` and a `.bak`.

### 3.2 `EndlessRaceDirector.cs` at 2,880 lines

Is the size a problem, or a reasonable consequence of driving someone else's runner? **Both, and
the split is not where you would guess.**

The *adapter* part is justified. Trash Dash's `TrackManager` was not designed to be driven, the
track floats its origin every ~100 m, gates must be parented to segments that recycle, and their
`WaitToStart` coroutine fights you on its own timer. Any correct integration is thick. I would
not shrink that.

What is not justified is that the same class also:

- builds **four canvases** from scratch (HUD, briefing, pause, and the option preview board),
- **generates two textures procedurally** at runtime (`WoodPlaqueSprite`, `MakeVignetteSprite`),
- runs the **countdown camera choreography**,
- owns the **patrol AI**,
- owns **pause/leave/focus/abandon** semantics,
- and hides **their chrome** by walking their hierarchy by name.

That is six separable responsibilities with no seams, and `new GameObject(` appears **37 times**
in it. Every remaining fix in the last two days lands in this one file. I would not refactor it
now — Pro Review is right about that — but I will say plainly what I would have refused at
review time: **the HUD, the briefing and the pause overlay should have been three prefabs and
three small components.** They are code-built for a defensible reason (their scene stays free of
our prefabs) and an indefensible consequence (nobody can see the race's UI without running it,
on a project where nobody can run a build).

The comment density is 791 comment lines to 1,803 code lines — **44%**, much of it 10–20 line
essays. My honest read: in *this* project the comments have earned their place, because they are
the only defence against an agent re-deriving a wrong conclusion for the fourth time, and several
of them explicitly say "do not put this back". The cost is that the file is hostile to skimming,
which is precisely what a two-days-out hotfix needs. If I owned this I would leave every comment
alone and add a 40-line table of contents at the top.

### 3.3 The seams with Trash Dash

I counted them myself. `CLAUDE.md` says "one guarded line" in one place and "three" in another.
Neither is true at HEAD. The real figure is **six guard lines in three of their files, plus two
unrelated one-line fixes in two more — four files modified in total** (`git diff --stat
e4de43e HEAD -- Assets/Scripts/` = 4 files, +25/−4):

- `CharacterInputController.cs:322` and `:356` — Jump and Slide disabled
- `TrackManager.cs:552` and `:582` — obstacles and coins/powerups disabled
- `GameState.cs:285` and `:290` — `Pause(!EndlessRaceMode.Active)` on app-pause/focus-loss
- (non-guard) `LoadoutState.cs:98` — a null check on `MusicPlayer.instance`
- (non-guard) `TrackManager.cs` — `using UnityEditor;` deleted, the player-build blocker

Plus two reflection hooks into their private fields (`TrackManager.m_Speed`,
`CharacterInputController.m_CurrentLane`) and `HideTheirChrome()`, which reaches into their
hierarchy by *child name* (`gs.pauseMenu.Find("Exit")`) and by *parent-of-parent* traversal
(`gs.scoreText.transform.parent.parent`).

The whole seam rests on one mutable static: `EndlessRaceMode.Active`, a 13-line class with a
bare `public static bool`, written only in `EndlessRaceDirector.Awake` and `OnDestroy`. It has no
`[RuntimeInitializeOnLoadMethod]` reset and no scene scoping, so any path that skips `OnDestroy`
leaves it true and silently strips their game from `Main.unity`. In the editor with domain
reload disabled that is a normal Tuesday. One attribute fixes it.

Worth recording for the write-up: **no `Assets/_Game` asset is addressable.** All 16 Addressables
groups are theirs and every entry points into `Assets/Bundles`, `Models`, `Materials`, `Prefabs`.
`CLAUDE.md`'s phrase "the Default-Zones group lost the SummaRace zones" is a misnomer — those are
Trash Dash's three zone folders, and what `ffaec91` repaired was their entry GUIDs. The SummaRace
layer (`RaceWorlds.cs`) is a pure index mapping onto their content.

The guards themselves are fine — a static bool checked in six places is the least invasive
integration available. The two things I would refuse to merge as written:

1. **The reflection is scattered.** Two `static readonly FieldInfo` fields in two different
   classes, each with a silent fallback if the field is renamed. There should be one
   `TrashDashBridge` with the contract in one place and a *loud* failure, because the silent
   fallback in `EndlessTouchInput.MoveToLane` degrades to a geometric estimate that is wrong
   mid-lane-change — and nothing would ever tell you.
2. **`HideTheirChrome` is name-and-shape coupled to a scene nobody controls.** It is
   audit-verified today. It is one Unity re-serialisation away from silently leaving a coin
   counter on screen during a study session, and there is no assertion that would catch it.

Offline compliance I verified independently and it **holds**: `Packages/manifest.json` is clean
of ads/analytics/purchasing/GDK, and every analytics/ads call site in their code sits inside
`#if UNITY_ANALYTICS` / `#if UNITY_ADS`, which are now undefined. Good.

### 3.4 Three defects I would flag at review

- **`BackButtonGuard` almost certainly does not do what it says.** The class comment asserts:
  *"Reading the control marks it handled for this frame; there is no other consumer, so simply not
  acting on it is what keeps the app open."* There is no such consumption semantics in the Input
  System — `wasPressedThisFrame` is a query, not a claim. Unity's documented mechanism for
  refusing a quit is `Application.wantsToQuit` returning false, which this project does not use.
  And `ProjectSettings.asset:81` has `androidPredictiveBackSupport: 1`, which routes back
  navigation through the system's `OnBackInvokedCallback` on Android 13+ — with the exit animated
  before your code sees anything. **Stated as opinion, because I cannot test it without a device:
  I think this is a no-op that reads as a fix.** It is thirty seconds to settle on a tablet and it
  is exactly the class of confident-but-wrong conclusion this project's own notes warn about.
- **`ReadWithRecovery` does not recover from the failure it was written for.** It falls back to
  `.bak` only when `File.ReadAllText` *throws*. A file that is readable but truncated returns
  fine, `JsonUtility.FromJson` then throws inside `LoadProfiles`, which catches it and returns an
  **empty list** — the learner silently reboots as a brand-new child, and `.bak` is never
  consulted. That path is reachable: `TryWrite`'s catch block falls back to a plain
  `File.WriteAllText`, which is truncate-then-stream. Move the parse inside the recovery, or have
  `LoadProfiles` retry against `.bak` on a parse failure. Ten minutes.
- **`GameManager.LastArrangeAttempts` and `LastSummaryText` are written and read by nothing.**
  Grepped: the only reader is the writer. Two of four stages produce no acknowledgement anywhere
  in the game. From the child's seat that says the last two screens do not count — and Summary is
  the most expensive screen in the loop. Showing the learner their own sentence back on Results
  with "you wrote this" is half an hour and is the cheapest motivational win available.

### 3.5 "Delete all data" does not delete all the data

This is the second-strongest finding in the review and it is a fifteen-minute fix.

Trash Dash keeps its own save: `Application.persistentDataPath + "/save.bin"`, a `BinaryWriter`
blob created on first entry to the race scene (`PlayerData.cs:216, 226, 255, 392`). It is written
during play — `TrackManager.cs:460` calls `PlayerData.instance.Save()` on every 300 m rank-up,
and `GameState`, `GameOverState` and `LoadoutState` each call it too. It holds rank, coins,
premium currency, consumables, the mission list and **highscores** (six `HighscoreUI` components
and a `Leaderboard` are live in `MainSummaRace.unity`).

`SaveManager.DeleteAllData()` (`SaveManager.cs:208-235`) deletes `profiles.json` + `.bak` +
`.tmp`, `settings.json.bak` + `.tmp`, and the `logs/` directory. **It never touches `save.bin`,
and grepping `Assets/_Game/` for `save.bin` returns zero hits — our codebase has no awareness
that the file exists.** Their only delete path is `PlayerData.cs:472`, a
`[MenuItem("Trash Dash Debug/Clear Save")]` inside `#if UNITY_EDITOR`, unreachable at runtime.

So after the teacher taps the two-tap wipe behind the PIN — the action `TeacherGate.ResetDevice`
and the runbook both describe as erasing the tablet, and which is the consent promise — a file
of per-device behavioural residue from that child's ten sessions remains in
`persistentDataPath`. It carries no name and no learner id, so it is pseudonymous rather than
identifying, and I would not call it a privacy breach. I *would* call it a promise the code does
not keep, on the one action whose entire purpose is keeping it.

**Fix:** one line in `DeleteAllData` deleting `PathFor("save.bin")`, inside the existing
try/catch. Add it to the post-study wipe and to `BuildPreflight`'s checklist.

### 3.6 Offline-ness is true today and is not enforced

I verified the manifest is clean and every ads/analytics/IAP call site sits inside
`#if UNITY_ADS` / `#if UNITY_ANALYTICS` / `#if UNITY_PURCHASING`, none of which are defined. No
`UnityWebRequest`, `System.Net`, `Social.` or `internetReachability` anywhere in either tree. The
one unguarded `Application.OpenURL` (`Assets/Scripts/OpenURL.cs:9`) compiles into the player but
its GUID appears only in `Main.unity`, `Start.unity` and an old `_Recovery` scene — none of which
are in Build Settings.

The point is what that rests on: **two packages being absent and one script not being in the
shipped scene.** Nothing asserts either. Re-add `com.unity.ads` for any reason and live
`Advertisement.Show` calls re-arm inside `GameState`, which *is* in the race scene. There is also
a reachable-by-one-wire path — `MissionUI.cs:36` instantiates `AddMissionButton.prefab` through
Addressables and `:43` dereferences its `AdsForMission` component — held closed only by
`CallOpen()` being unwired.

Related, and worth a line for a study that must ship byte-identical code to 40 tablets:
`manifest.json:3` pins `com.coplaydev.unity-mcp` to a **git URL at `#main`**. That is a
non-reproducible dependency in the build graph. It is editor tooling and should not reach a
player, but nothing in the project states that, and a floating `#main` is the wrong thing to have
in a thesis instrument's manifest. Pin it to a tag, or drop it before the study build.

### 3.7 Documentation

7,055 lines of markdown in `Documentation/`, against ~12,800 lines of our C#. Since commit
`a988992` the docs grew by 5,810 lines and the code by 5,151. **More than half of this project's
recent output, by volume, is prose about the project.** `SummaRace_Study_Operations_Runbook.md`
is genuinely needed — a teacher will hold it. Most of the rest is a development record that has
already begun to contradict itself, which is why the Owner Handover needs a §4d listing which
document is right.

I will say the blunt version once: the honest cure is not another reconciliation section. It is
to delete the superseded documents. `SummaRace_Pro_Review.md` and this file should both be
archived the day the APK exists.

---

## 4. Two things the previous review got wrong, and one it got right that I want to reinforce

**Where I disagree with `SummaRace_Pro_Review.md`:**

1. **§1.2 rung 4: "Summary — it works."** It does not, as a production measure. The five correct
   answers are on screen while the learner types (§2.1). The review's own §6.7 worries about
   `SummaryMaxChars` truncation while missing that the answer key is visible.
2. **§1.1: "there is no passage to summarize, anywhere" is the most important thing in the
   document.** It is a real observation and worth the researcher's attention, but it is *second*.
   The most important thing is that the race — the measure the whole study rests on — is passable
   from memory (§2.2), and that was measurable from the same 30 JSONs the review already parsed.
3. **§3.1 on the Android back gesture** was correct when written and has since been "fixed" by a
   guard I do not believe works (§3.4). The review's item was closed on the strength of a commit
   message, not a device. That is worth noting as a *pattern*: three of the review's eleven items
   were closed within hours by passes that could not test their own fix.

**Where it is exactly right and I want to reinforce it:** *"Stop adding to the race and go build
the APK."* Since that sentence was written, four more commits have landed on race visuals, world
dressing and audio. `RaceSecondsPerGate` is still 12, `StarsTwoMin` is still 4,
`TargetFrameRate` is still 60, `SummaryMaxChars` is still 200, and no APK exists.

---

## 5. The biggest risk nobody has named

**The content of a thesis instrument has been rewritten at scale by language models, two days
before use, and nobody with the standing to approve it has read the result.**

Counting only what the project's own commit messages and `CLAUDE.md` record:

- **120 of 150 element sets rewritten** in F44 (distractors), plus **7 `correct` lines changed**
  for being factually wrong about their own story.
- **112 Reader distractors rewritten** in F47ⓖ.
- **6 page questions rewritten** toward their SWBST slot (`4a78905`).
- **`s01_easy` — the story every one of the 40 learners plays first — is entirely AI-authored**:
  its page split, its five questions and its distractors. It is also a 162-word outlier in a
  corpus averaging 74, the only story with dialogue, and the only one that is actually a story.
- The 30 JSONs are **generated artefacts**, so none of this is visible as a diff a researcher
  could review. It lives in `Tools/StoryPipeline/overrides.json`.

Now the evidence that this is not a theoretical concern. Commit `4a78905`, from today:

> *"Two of the seven were regressions we introduced. The P7 pass shortened race cards to fit their
> width and changed meaning doing it: `s04_easy` BUT: the researcher's 'they had many animals to
> explore' became 'They had too much to see in only one day' — a different claim about the story.
> `s10_easy` SO: dropped the word 'earthquakes', the single thing page 4 is about."*

An automated pass silently changed what two of the researcher's stories *mean*, in the service of
fitting text onto a card. It was found only because a *different* agent later happened to audit
page-to-slot alignment. Nothing in the pipeline detects it; `flag.py` reported 0 of 150 both
before and after. There is no reason to believe those two were the only ones — they are the only
two that a subsequent audit happened to look for.

**Why this is the biggest risk, and why twenty-five audits missed it.** Every pass today was
scoped to *engineering* validity: is the card readable, is the width tell gone, does the log
record the right field. Content was treated as an input — as data the pipeline transforms — and
transformations of data were held to a *mechanical* standard (`flag.py` 0/150, tests green)
rather than an *editorial* one. But in a reading-comprehension instrument the content **is** the
instrument. A story whose "But" no longer names the obstacle is a broken item no test can see,
and `flag.py` passing it is not evidence of anything except that `flag.py` does not measure
meaning.

There is a second-order effect that I think is the most interesting thing in this repository.
`CLAUDE.md` carries a ⚠️ correction telling future agents *not* to conclude the Reader's
questions contaminate the race, because an earlier pass concluded that and was wrong. **That
guardrail was correct about the specific claim it addressed** (the questions being SWBST-shaped
is the researcher's own design) **and it appears to have prevented anyone from testing the
adjacent claim that is true**: that the race's *answer text* is carried over from the Reader's
answer text, 31 times verbatim, worth 84% against a 33% floor. A note written to stop one wrong
conclusion stopped the right one from being found. If you take one process lesson from this
review, take that one: guardrails should forbid *unverified assertions*, never *whole questions*.

**What I would actually do about it, and it is not "rewrite the content again":**

1. **Freeze the content now.** No further pipeline runs before the study. Every additional
   automated edit is a fresh chance to break a story's meaning with no detector.
2. **Send the researcher a diff, today.** `git log -p -- Assets/_Game/Resources/Stories/` against
   the original Phase-G generation gives every changed string. Ask for a yes on the ~130 changed
   items — or, realistically, on the ~20 where meaning could plausibly have moved (`correct`
   lines and rewritten questions). That is a one-hour read for the researcher and it converts an
   integrity problem into a documented editorial decision.
3. **Write the AI-authorship into the thesis method section.** "Distractors were authored/edited
   with LLM assistance and reviewed by the researcher" is a normal, defensible sentence in 2026.
   "The stories came from the source document" would not be true, and that is the version that
   currently exists in `SummaRace_Content_QA_Report.md`.

None of that costs engineering time. All of it is unrecoverable if skipped.

---

## 6. Two days, this owner's constraints — what I would actually do

Ranked so that stopping after any item leaves you better off than before it. Effort is honest,
including the parts that are waiting rather than working.

### 1. Build one APK and run one non-`s01` story on the real tablet — 3–5 h (mostly unattended)

Unchanged from Pro Review and the Owner Handover, and it is still item 1 because **nothing else
on this list is verifiable without it.** Install Android Build Support; turn on **Build
Addressables on Player Build** (verified: `m_BuildAddressablesWithPlayerBuild: 2` and no `aa/`
folder for any platform — ship as-is and the race has no road, no scenery and no runner); run
`Build Preflight`; build; sideload; play one whole story with Wi-Fi off.

While you are there, settle three things a device answers in ninety seconds and nothing else can:
does the BACK gesture close the app (§3.4); does the soft keyboard cover the Summary input; does
a tap on the option panel actually change lane.

### 2. Run `Device Budget ▸ Apply ALL safe fixes` — 30 min

~156 MB of raw PCM resident on a 2 GB device, because Trash Dash's `MusicPlayer` is
`DontDestroyOnLoad` and plays six 4-minute stems at once. Idempotent, look-preserving, importer
settings only. Highest value per minute in the whole plan.

### 3. Freeze the content and email the researcher — 30 min of your time — §5

Four questions, none of which need a developer: (a) sign off the ~130 AI-edited strings, or at
least the `correct` lines and rewritten questions; (b) `s05_hard` = `s06_hard` duplicate;
(c) `s03_easy` mislabelled; (d) the race-answer carryover in §2.2 — is that acceptable, is it a
limitation, or does she want the ~50 highest-similarity `correct` lines paraphrased? **Send this
before anything else on this list, because the answers gate everything and they arrive on her
clock, not yours.**

### 4. Make "delete all data" actually delete all the data — 15 min — §3.5

One line in `SaveManager.DeleteAllData` removing `save.bin`, Trash Dash's own
`persistentDataPath` blob, which the teacher's wipe currently leaves behind along with the
learner's rank, coins and highscores from all ten sessions. This is the only item on this list
that is about a promise made to a parent rather than to a developer, which is why it outranks
tuning.

### 5. `RaceSecondsPerGate` 12 → 17, and `StarsTwoMin` 4 → 3 — 15 min + one playtest

Two constants. The first gives a 70 wpm reader the ~16.5 s they need instead of the ~11.6 s a
100 wpm reader needs — and this study exists for the slower readers. The second stops the modal
learner seeing one star thirty times. Neither touches a logged measure. Verify the first actually
lands: `RaceMaxGateGap` (300 m) clamps it at higher speeds.

Also free while you are in that file: `TargetFrameRate` 60 → 30, since their `MusicPlayer`
already forces 30 the first time the race loads and the two currently disagree.

### 6. Tell the learner where to read, and make the option panel tappable — 45 min

One sentence in `GameText.RaceBriefingBody` naming the panel at the top of the screen (today it
says only *"Tap or swipe left and right to move!"* — it never mentions the only readable copy of
the three answers, and it does not mention the middle lane). Then make the three panel columns
real buttons that call `ChangeLane`, so reading and choosing become one act instead of two.

This is my one addition to Pro Review's list that it did not have, and I rank it here because it
is the only change on this page that improves both the *experience* and the *measure* at once.

### 7. The four contrast one-liners — **already done; do not spend time on them**

The Owner Handover §4c and the Finalization Plan R2 both list these as open. Verified in source
at HEAD, **all four have landed**: FINISH card is deep brown on gold
(`EndlessRaceDirector.cs:786`, `(0.32, 0.19, 0.02)`); the Reader "Not quite" line is
`(0.62, 0.32, 0.02)` (`ReaderController.cs:74`); the Summary reference list uses
`InkHexForIndex` (`SummaryController.cs:71`); StorySelect dims the locked hero and uses
`LockedHintInk` at a measured 6.09:1 (`StorySelectController.cs:66-71`).

I am listing this as an item only to take it off your list — and as an example of why §3.7 is a
real problem. Two "current" documents agree on work that was finished before they were written.

### 8. Log the background intervals for the non-race phases — 30 min — §2.6

`readingSeconds`, `arrangeSeconds` and `summarySeconds` currently include tablet-locked time.
Unrecoverable after the fact.

### 9. Show the learner their own summary on Results — 30 min

`GameManager.LastSummaryText` is already written and read by nobody. Two of four stages currently
produce no acknowledgement at all.

### What I would deliberately NOT do

- **Any change to the race's visuals, worlds, dressing, or the patrol cop.** Seven passes on a
  chaser that never catches and feeds no measure. Freeze him. If he reads badly on the device,
  delete him — the vignette carries the signal.
- **Any refactor of `EndlessRaceDirector.cs`.** It works and every fix lands in it. After the
  study.
- **Re-running the story pipeline.** See §5. Content edits are now the highest-risk operation
  available and the lowest-detectability.
- **Changing the Arrange mechanic** (revealing SWBST labels as rewards rather than showing them).
  It is the right change and it is small, but it alters the instrument, and `arrangeOrders` now
  captures the data either way. Do it after the study or not at all.
- **Real hero art ×27.** All 30 resolve. No thesis is marked on a hero image.
- **More documentation.** There are 7,055 lines. Archive half of it instead.

---

## 7. What I measured versus what I am asserting

**Measured, by me, from the files:** the 84.0% memory-strategy score and the 31 byte-identical
Reader/race correct pairs (script over all 30 JSONs, re-derived independently of the agent that
first found it); the 47.7% Arrange chance-solve (200k-trial simulation of the real lock-and-retry
mechanic); the 45.3% / +1.37 race width figure and the 29.3% Reader figure; the
`correctIndex` {48, 96, 6} distribution; the six guarded lines in Trash Dash's code and the four
files they live in; the `#if UNITY_ANALYTICS` / `#if UNITY_ADS` guards and the clean
`manifest.json`; the 44% comment ratio and 37 `new GameObject(` calls in `EndlessRaceDirector`;
the 7,055 markdown lines and the 5,810-vs-5,151 doc-to-code churn since `a988992`;
`androidPredictiveBackSupport: 1`; that `LastArrangeAttempts` and `LastSummaryText` have no
readers; that `TrackManager.m_TrackSeed` defaults to −1 so lane shuffles are genuinely
non-deterministic per run.

**Read in source and reported, not independently re-run:** every claim about what
`SessionLogService`, `ArrangeController`, `SummaryController`, `ReaderController`, `SaveManager`
and `GameManager` do — I read those files in full.

**Taken from other documents, spot-checked only:** the 156 MB resident PCM figure and the
Addressables build trap (both from `SummaRace_Device_Budget.md` / `SummaRace_Build_And_Release.md`,
and both re-confirmed against `ProjectSettings`/`AddressableAssetSettings` values quoted there);
the reading-speed budgets in `GameRules.cs:91-116`.

**Opinion, clearly labelled:** that the game stops being fun around session 5; that
`BackButtonGuard` is a no-op; that the HUD/briefing/pause should have been prefabs; that the
Summary reference list invalidates it as a production measure; that content should be frozen. All
five are judgement calls. Argue with any of them.

**Not verified, and it matters:** everything about the device. No APK exists. I have not seen a
single frame of this game running, and neither has any child.
