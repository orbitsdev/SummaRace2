# SummaRace — Pre-ship Review

**Written:** 2026-08-06 at commit `46bbb90` · **Branch:** `experiment/endless-override-2` ·
**Two days before 40 children use this.**

> **Read §6's ranked list against `SummaRace_Owner_Handover.md` §2, not on its own.** That page is
> the maintained critical path; this one is a dated outside opinion, and three of its items moved
> within hours of it being written. Re-verified at HEAD `63e1be3`:
>
> | §6 item | Status now |
> |---|---|
> | 1 — build an APK on the actual tablet | **Still item 1, still unstarted.** Both blockers confirmed: no Android module in either installed Editor, and `m_BuildAddressablesWithPlayerBuild: 2` with no `aa/` folder for any platform. |
> | 9 — log the Arrange placements (*"schema 3 → 4"*) | **Recommendation taken; the numbering was already stale.** Schema 4 was spent on `participantCode` (`9d839ba`). Arrange orders land as **schema 5**, uncommitted in the working tree as this note is written. |
> | 10 — colour-only instructions + four contrast failures | **Half done.** The two colour-name strings were reworded in `06f1778`, one commit *before* this review was written — the "still open, verified today" line is stale. The **four contrast failures remain open** and the argument for them stands. |
> | 11 — render and frame settings | Unchanged, and now more relevant: the F48 pass this review warned about **landed as `63e1be3`**, at an author-estimated **+200 draw calls worst case, still unmeasured on any device**. |
>
> Also stale by construction: the line "5,058 lines across 13 markdown files" (now ~6,900 across 16,
> minus one deleted) and the "F48" label for the world pass, which `CLAUDE.md` records as **F54**.
> The substance of §5's complaint — *stop adding to the race and go build the APK* — is unaffected,
> and is still the single most useful sentence in this document.

This is an outside read of the whole product — content, teaching design, fun, fragility — not a
code review. Where I measured something I say so and give the file. Where I am giving an
opinion I say that too. I disagree with a few things recorded in `CLAUDE.md` and I argue them
rather than assert them.

The engineering here is unusually good. The logging schema, the never-a-dead-end work, the
validity passes in F44/F47 and the 45 tests are better than most shipped mobile games. None of
that is what worries me. What worries me is that **nobody has ever run this on a tablet**, and
that **two of the four teaching stages do not test what the design says they test**.

---

## 1. The headline: what this game actually teaches

### 1.1 There is no passage to summarize. Anywhere.

This is the most important thing in this document.

The researcher's source doc (`Documentation/STORIES FOR SESSION 1-10 ….docx`, extracted to
`stories_extracted.txt`) gives each story **three** things: a `Shortened Passage` (75–90 words
of connected prose), an `SWBST Analysis`, and a separate `Page 1..5 Text`. The pipeline emits
the **Page texts** into `pages[].text`. `StoryData.cs` has **no passage field at all**, and the
27 `Shortened Passage` blocks in the source doc reach the game nowhere.

Look at what the Page texts actually are. `s10_hard` in full, as the learner sees it:

> p1 Many animals have helped people during wars and emergencies through their hard work and loyalty.
> p2 Maria Dickin wanted to honor animals that showed courage while helping people.
> p3 Animals faced dangerous situations while carrying messages, guiding people, and helping during emergencies.
> p4 Pigeons delivered important messages, and guide dogs helped their owners reach safety during emergencies.
> p5 Because of their courage and loyalty, many animals received the Dickin Medal, a special award for service.

Those five sentences **are** Somebody / Wanted / But / So / Then. The doc's page text is a
paraphrase of the SWBST analysis, one slot per page, in order. Measured across all 30 stories:
mean total **74 words**, mean **14.8 words per page**, and **146 of 150 pages are under 30
words** (the 4 exceptions are all `s01_easy`).

So the learner never meets a text that needs reducing. They read a pre-made five-part summary,
one part at a time, labelled in order, and are then asked to recall its parts. **Summarizing is
the act of deciding what to throw away, and this game never asks a learner to throw anything
away.**

I want to be fair about this: it is not a defect anyone introduced, it is the shape of the
researcher's own content, and repeated exposure to 150 well-formed SWBST exemplars is a real
and defensible instructional design (worked examples plus retrieval practice). But it is a
*schema-exposure* trainer, not a *summarizing* trainer, and the paper posttest will ask
children to summarize an actual passage. That gap is the thesis's central untested assumption
and right now the app does nothing to bridge it.

**The cheap fix, if the researcher wants it:** add the doc's `Shortened Passage` as a page 0 in
the Reader — one screen, narrated, no question, "read the whole story first". The five pages
then become the guided breakdown of something the learner has actually read whole. Cost:
a `passage` field on `StoryData`, a pipeline re-run, one Reader page, ~27 TTS clips. Half a
day, and it changes no measure. **This is a researcher decision, not a developer one. Send the
email today.** If the answer is no, then the thesis write-up must describe what the app
practises accurately, or the results get over-claimed.

### 1.2 The support-removal ladder has two broken rungs

`CLAUDE.md` states the design as: Reader (text visible) → Race (text gone, unsupported recall)
→ Arrange (sequence) → Summary (produce). I checked each rung against the build.

**Rung 1 — Reader. The text is NOT visible during the question.**
`ReaderController.cs:255` — `readingCard.SetActive(false)` the moment the question appears
("story gives way to its own question page", F32, prototype parity). So rung 1 is already
unsupported recall, over a 15-word span read two seconds earlier. The documented justification
for the whole ladder ("the Reader pre-teaches the five slots **with the text on screen**") is
not what the build does. This is defensible as UX — the prototype does it — but it should be a
deliberate decision, not an inherited one. Keeping a shrunken copy of the page beside the
question costs about an hour and it is the single support a struggling reader most needs.

**Rung 2 — Race. Verified: it is a recall test of the Reader's own answer key.**
The page questions run 1:1 with the SWBST slots (page 1 = Somebody … page 5 = Then), and the
gates run in the same order. The race's `correct` line is the Reader's correct answer, verbatim
or lightly reworded — `s10_average` has 2 of 5 byte-identical, `s05_easy` 1 of 5, the rest
paraphrases of the same fact. So the race asks: *recall the five answers you gave 60–120
seconds ago, cued by the same slot label, in the same order.*

That is retrieval practice, which is genuinely good pedagogy. But `raceFirstPickCorrect` — the
star count and the headline in-app measure — should be described in the thesis as **within-
session retrieval of SWBST elements**, not as summarizing ability. It will correlate with
working memory and attention at least as much as with the construct.

**Rung 3 — Arrange. Sequencing is not implemented.** The slots are labelled with the SWBST
words, numbered 1–5, in the correct order, colour-coded (`ArrangeController.cs:349,352`). The
five pieces are always the correct texts regardless of race performance
(`EndlessRaceDirector.cs:1208`). So the task is not "put the story in order", it is "match five
texts to five named labels" — the same mapping the race just ran, with two extra candidates, no
timer, no distractors, unlimited retries, and the answer sheet 20 seconds old.

It is also beatable by chance. Simulating the real mechanic (random permutation each verify,
correct slots lock, `ArrangeMaxAttempts = 4`): a learner who understands **nothing** solves it
unaided **47.8%** of the time, or **67.1%** if they use the free cue that the Somebody is the
shortest piece (it is the shortest of the five in 28/30 stories, ≤3 words in 24/30). So
`arrangeSolved` is roughly a coin flip on ignorance.

**Fix, and it is small:** leave the slots blank and numbered 1–5, and reveal the SWBST word as
the *reward* when a slot locks correctly. That is one line at `ArrangeController.cs:349` plus
the hint ladder. It turns matching back into sequencing and makes `arrangeSolved` mean
something. Owner's call whether to change an instrument two days out — but if you do it, do it
before the first learner plays and never mid-study.

**Rung 4 — Summary.** This is the only place a learner *produces* anything, and it is the
richest datum in the log. It works. Three problems, section 5.

### 1.3 The difficulty labels are wrong about a third of the time

Measured across all 30 JSONs (Flesch-Kincaid, vowel-group syllables; treat as a *relative*
signal — these are 57–102 word samples and FK wants 100+):

- **Word count is not a difficulty lever at all.** **0 of 10** sessions have easy < average <
  hard by length. Excluding `s01_easy`, all 29 stories sit in a 57–102 word band (mean 71).
- **FK grade increases easy→average→hard in only 6 of 10 sessions.** Session 3 is fully
  inverted: `s03_easy` FK **9.74** against `s03_hard` FK **4.59**. The "easy" story in session 3
  is the hardest text in that session by a wide margin.
- Across sessions there *is* a real progression, and it is the only one in the instrument:
  FK vs session **r = +0.74**, session 1 mean 5.47 → session 10 mean 10.40. That is defensible
  and worth stating in the thesis.
- **21 of 30 stories score above Grade 6.** `s04_hard` ("AgitAgueda") is **FK 14.64** — an
  unglossed 5-syllable foreign proper noun in a Grade-4 instrument. `s10_hard` is 13.05.

Three specific content items need the researcher's yes/no, today:

1. **`s05_hard` and `s06_hard` are the same story** — both "In Grandfather's Day", same
   characters (Sharr and Kaze), same beats, reworded. A learner meets it twice. (Already flagged
   as P3 in `SummaRace_Content_QA_Report.md`; it still needs a decision.)
2. **`s03_easy` is mislabelled** (FK 9.74 as "easy", harder than its own session's "hard").
3. **`s01_easy` is an outlier on every axis** — 165 words against a corpus mean of 74, the only
   story with dialogue, the only real narrative, FK 2.65, and AI-authored. It is also the first
   thing every one of the 40 learners plays. Session 1 then drops 165 → 74 words at
   easy→average, so the game reads as getting *easier* exactly where it should step up.

Ironically, the AI-authored gold-standard story is the only one in the corpus that is actually
a story.

---

## 2. Is it fun? Honest answer: for about three sessions.

### 2.1 The race has one verb, used five times

I traced what a learner can actually do during a run:

- `TrackManager.SpawnObstacle` returns early in our mode (`TrackManager.cs:552`) — **no obstacles**.
- `SpawnCoinAndPowerup` yields immediately (`:582`) — **no coins, no powerups**.
- `CharacterInputController.Jump` and `.Slide` return early (`:322`, `:356`) — **no jump, no slide**.

So between gates: nothing happens. The only input is a lane tap, used five times per run.

And the option panel (F47) is up for the *whole* gap, so a confident learner decides in the
first second or two and then waits. Per-gate gap time, derived from `GameRules`
(`RaceSecondsPerGate 12`, difficulty ×1.25/1.0/0.8, clamped [110, 300] m):

Verified from the scene, not from the docs: `minSpeed 10`, `maxSpeed 30`,
`k_Acceleration 0.2 m/s²` (`MainSummaRace.unity:15480`, `TrackManager.cs:129`). Because the gap
is `12 × difficulty × current speed`, the *time* per gate is constant until the 300 m cap bites:

| | gap per gate | reading needed | dead air for a fluent reader |
|---|---|---|---|
| easy | **~15 s** (≈13 s at the last gate, capped) | ~11.6 s @100 wpm, ~16.5 s @70 wpm | ~8–10 s |
| average | **12 s** | ~11.6 s | ~5–7 s |
| hard | **9.6 s** | ~11.6 s | ~3–5 s |

Whole race: ~75 s easy, ~61 s average, ~51 s hard, plus briefing and 3-2-1. So roughly **8–10
seconds of empty corridor per gate, five times a run, thirty runs**. F46 explicitly fixed this
once ("the learner ran down an empty corridor between gates wondering if the game had stopped")
and F47's legibility work necessarily re-lengthened it. Those two forces are in direct conflict
and the current numbers are a compromise no child has playtested.

The friendly cop only appears **after a mistake** (F39, "appear only on a bump"), so a learner
doing well never sees the one dynamic element in the scene. The better they get, the emptier it
gets.

**My prediction, stated as opinion:** novelty carries sessions 1–3. By session 5 the race is a
corridor children wait in. Because the race is where the measure lives, disengagement will
degrade the measure over exactly the sessions where a learning gain should be appearing, and it
will read in the data as a plateau — which is very hard to interpret afterwards.

### 2.2 The biggest fun asset in the build is switched off

You imported a complete, shipped endless runner — obstacles, coin lines, powerups, a whole
economy of moment-to-moment decisions — and turned all of it off. The kids get the graphics and
none of the game.

I think the reason is a misreading of the GDD. F35 records: *"GDD says the only obstacle is a
wrong answer — no barriers/bins to crash into."* That rule is about **punishment**: nothing may
make a learner fail. A coin you can pick up is not an obstacle and cannot punish anybody. The
legacy race already had `BuildCoinLines` (F19) for exactly this reason and the endless path
dropped it.

**Cheapest fix that restores a loop:** centre-lane coin lines in the first ~60% of each gate
gap, ending well before the cards appear so they never compete with the reading. That is a
guard removal plus a spacing rule, ~2 h with a playtest. I would not do it in the next two days
— it is the **first thing to patch between session 1 and session 2**, once you have actually
watched children play.

### 2.3 The star curve puts most learners on the floor

`GameRules.cs:154` — 5/5 = 3★, 4/5 = 2★, **everything else = 1★**. So 3/5 (60%, well clear of
the 33% chance floor) shows the same screen as 0/5. Now that F44 and F47 have removed the
width tell and the position tell, real scores will cluster at 2–4, meaning most learners see
**one star, thirty times**, in a game whose north star is "never punish the learner" (D7).

`starsEarned` is a pure function of `raceFirstPickCorrect`, which is logged separately, so the
star curve is purely motivational and safe to change. **`StarsTwoMin` 4 → 3** is a one-line
change that moves the modal learner off the floor. I would do it.

### 2.4 Two of four stages are unscored, and children work that out fast

Stars, praise and the treasure gems on Results are all a readout of the same five race
booleans. `GameManager.LastArrangeAttempts` and `.LastSummaryText` are written and **read by
nothing**. A learner who orders perfectly and writes a lovely summary after a poor race sees
exactly the same screen as one who did neither.

That may be intentional — the race *is* the measure — but from the child's seat it says the
last two screens don't count, and Summary is the most expensive screen in the loop
(1.5–4 minutes of tablet typing). Giving Results a single acknowledgement of the summary — even
just showing their own sentence back with "you wrote this" — would cost half an hour and is the
cheapest motivational win in the game.

---

## 3. Where it is fragile

A lot of classroom-failure work has already been done and it shows. Verified as **already
handled**, so nobody re-does it:

- **Save writes are atomic.** `SaveManager.cs:241-256` writes to a temp file and uses
  `File.Replace` with a `.bak`, falling back to a direct write only if that throws. A power cut
  mid-write does not corrupt the profile. Reads are wrapped and hand back an empty list rather
  than throwing.
- **Backgrounding snapshots the run.** `SessionLog.runId` / `isPartial` (F44ⓒ) means an Android
  low-memory kill costs at most the tail of one run, and partial rows are recognisable.
- **Scene loads are re-entry guarded.** `SceneLoader.cs:53` — a child mashing a button cannot
  double-load. F46ⓕ fixed the case where the guard stuck true forever.
- **The race can be left.** F47ⓓ: pause chip, two-tap leave, `timeScale`/`AudioListener.pause`
  restored on every exit path including focus loss and `OnDestroy`.
- **Trash Dash's exits are neutralised.** No `Application.OpenURL` in the shipping scene, the
  five leftover buttons have empty `onClick` and `interactable: 0`, and `LicenceDisplayer` —
  the only `Application.Quit()` in the project — is in **no built scene** (verified by
  searching every scene asset).

What still worries me, in order:

### 3.1 The Android back gesture ejects the child from the app, and it lives where the swipe input lives

Nothing in `Assets/_Game/Scripts` or `Assets/Scripts` reads `Keyboard.escapeKey`,
`KeyCode.Escape`, `Application.wantsToQuit` or `Input.backButtonLeavesApp` — zero hits. And
`ProjectSettings/ProjectSettings.asset:81` has **`androidPredictiveBackSupport: 1`**.

With no handler, back finishes the activity — the app closes. On a gesture-nav tablet the back
gesture is an **edge swipe**, which is exactly the gesture the race asks a child to make; on
three-button nav it is a permanent on-screen target at the bottom of a portrait game a
nine-year-old is tapping. Either way it will happen, repeatedly, all week.

The data cost is small — a graceful activity finish should still fire `OnApplicationQuit`, so
`SessionLogService` flushes the partial row (*inferred*: I have not confirmed Unity 6 fires it
under predictive back specifically — thirty seconds on the device settles it). The classroom
cost is large: the child is out of the app, the run is gone, and a teacher relaunches and
re-navigates, forty times over.

**Fix:** swallow it in `[Core]` — poll `Keyboard.current.escapeKey.wasPressedThisFrame` and
either route it to the current scene's own back action or ignore it. Consider
`androidPredictiveBackSupport: 0` so Escape is still delivered to your code at all. This is the
cheapest high-impact fix in the whole document.

### 3.2 A double-tap on Reader NEXT silently zeroes `readingSeconds`

Verified on the current file. `ReaderController.OnNext` (`:235`) has **no re-entry guard**, and
on the last page `Advance()` (`:375`) raises `ReadingCompleted` and then calls
`SceneLoader.Go` — without ever deactivating `nextButton`. Compare `ShowQuestion` at `:260`,
which does exactly that.

A second dispatch in the same frame raises `ReadingCompleted` twice.
`SessionLogService.cs:149` does `_log.readingSeconds = ClosePhase()`, and `ClosePhase()`
(`:374-380`) has already reset `_phaseStartedRealtime` — so **the run's reading time is
overwritten with ≈0.000**. `SceneLoader.cs:53` blocks the second *scene load* but not the
second *event*, and the raycast block lives inside the nested fade coroutine, so the screen is
still live on the frame of the first tap. Two thumbs on a tablet is the normal way a nine-year-
old holds one.

The row still writes and still exports as valid, so this is invisible until analysis. One line
fixes it. Same class, lower stakes: `SummaryController.cs:102` burns the nudge ladder in under
200 ms under mashing (the accept path at `:160` is guarded correctly), and
`ReaderController.OnBackTapped` has a 4 s arm *maximum* with no arm *minimum*, so two fast taps
drop the child out of the story. `ArrangeController` is the one screen that got this right
(`_busy` set as the first statement inside its coroutine) — copy that pattern.

### 3.3 The soft keyboard on the Summary screen

Nothing in the project reads `TouchScreenKeyboard`, keyboard height or `Screen.safeArea`. Unity
does not resize the canvas. The mitigation is a code-built DONE TYPING chip anchored at a
**guessed** y 0.598, while the input field's own bottom edge is at 0.42 — inside the band a
tall keyboard (prediction bar + gesture bar) will cover. If the child cannot see what they are
typing, the study's only qualitative datum is compromised. **Check on the real tablet, not in
the editor.**

### 3.4 A learner switch mid-run attributes one child's data to another

**The code side of this is correct** — `GameManager.SetActiveLearner` clears the story, race
result, arrange and summary state and raises `LearnerChanged`; `SessionLogService` flushes the
in-flight run on that event; and the row's `learnerId` was stamped at `StoryStarted`, so a
mid-run switch files the partial against the *previous* learner, which is right. No guard gap.

The risk is entirely human. The runbook (`§3.3`) already names this as the one step that, if
skipped, silently attributes a session to the wrong child and cannot be undone, and the Main
Menu's "Playing as \<name\>" line is the only backstop — a *reading* task for a teacher moving
fast between children. If you share tablets, this is your highest-probability data error, higher
than anything technical in this document. **My recommendation: one tablet per learner, as
`GameText.cs` already states is the intent, and do not use the switch at all during the study.**
That removes the failure mode instead of mitigating it.

### 3.5 Audio device changes silently kill the narration

Nothing subscribes to `AudioSettings.OnAudioConfigurationChanged` anywhere in the project, and
narration plays on a persistent `_voiceSource` (`AudioManager.cs:52-58`). When a Bluetooth
headset pairs, or headphones go in or out, Unity resets the audio system and stops playing
sources; nothing restarts them, so the child gets a silent page. Recoverable — the Reader has a
replay chip — but across 40 tablets with headphones, expect it. Also
`muteOtherAudioSources: 0` (`ProjectSettings.asset:86`), so a notification chime mixes *over*
the narration, and Unity does not implement Android's `ACTION_AUDIO_BECOMING_NOISY` — yanking
headphones dumps the narration onto the tablet speaker at full volume, in a quiet classroom.

### 3.6 The `mission` block in every story JSON is dead on the shipping race

Verified: `SceneNames.Race` has **zero references** anywhere in `_Game/Scripts`, so the legacy
`RaceController` never runs — and it is the only consumer of `GameRules.LaneWidth`,
`LaneSwitchSeconds`, `BoostSeconds`, `DangerOnWrong`, `DangerRelief`, `DangerAfterCaught` and
`RaceAccelPerSecond/MaxBonus`. On the endless path the director reads exactly one field from
the story's mission block — `checkpointSpacing`, which is **45 in all 30 stories** and is always
beaten by the 110 m floor. `playerSpeed`, `startingDanger` and `dangerPerSecond` are inert.

Not a bug — but it means a whole block of `GameRules` and a whole block of every story JSON
read as live tuning and govern nothing. If someone tunes them during the study expecting an
effect, they will get none and will not know why. Worth one comment line.

### 3.7 Things a classroom does that I could not verify from files

Listed so they land on the device smoke test rather than being discovered in front of children:

- A child resting a palm on the screen during a race (`EndlessTouchInput` has no palm rejection;
  a press-and-lift under 2% drag is a lane change wherever it lands).
- Headphones unplugged / a notification stealing audio focus mid-narration.
- A tablet at 5% battery entering Android's power-save mode (frame rate throttle on a build
  already targeting 30 fps).
- Two children handing the tablet over mid-run — the pause exists, but nothing tells a teacher
  it does.
- The first launch after install, on a device with no profile, in front of a class.

---

## 4. What is missing that this actually needs

Distinguishing "the study fails without this" from "this would be nicer".

**The study fails without these:**

1. **An APK that runs on the study tablet.** None has ever been built. See §6.1 — and note the
   Addressables trap, which produces an app that boots, plays the menus and the Reader, and
   then drops the child into a race with **no road, no scenery and no runner**.
2. **Audio import settings.** `SummaRace_Device_Budget.md` measures **178 MB set to
   DecompressOnLoad, ~156 MB resident as raw PCM** once the race is entered. On a 2 GB device
   that is a plausible low-memory kill mid-run.
3. **Reading time the target learner can actually use.** §6.3.

**Genuinely nice but not study-critical:**

- Real hero art ×27. Placeholders resolve; nothing fails.
- Any further work on the patrol cop (see §5).
- More documentation. There are already 5,058 lines of it.

**Missing and cheap and I would still add it:**

- **The learner is never told where to read.** `GameText.RaceBriefingBody` says *"Collect the 5
  story parts of "X" in order. Tap or swipe left and right to move!"* — it never mentions the
  option panel at the top of the screen, which after F47 is **the only readable copy of the
  three answers**. The world cards on the road are, by the project's own derivation in
  `GameRules.cs:91-116`, physically unreadable. If a child spends the run squinting at the road
  cards, the entire F47 fix goes unused. One sentence in the briefing. 10 minutes.
- **The instruction omits the middle lane.** "Tap or swipe left and right" — a learner in the
  left lane is not told how to reach the centre. `EndlessTouchInput` maps taps to screen thirds,
  so the middle works; the words don't say so.
- **The tap zones and the option panel don't quite line up.** Taps use exact screen thirds
  (`EndlessTouchInput.cs:85`); the panel's columns span 0.043–0.339 / 0.352–0.648 /
  0.661–0.957 (`BuildOptionPreview`). Tapping the outer ~0.6% of an outer column selects the
  centre lane. Children *will* tap the option they want — it is the obvious affordance and it
  currently works by accident. Make it exact, or make the panel columns real buttons.

**Missing and I would leave it:** a tutorial. The briefing plus a teacher in the room is enough,
and building one two days out is how you break a working loop.

---

## 5. Over-built, and where effort went that did not serve the study

I am going to be blunt here because it is the part nobody says out loud.

**The patrol cop is the single biggest sink of effort in this project's history, for zero
study value.** He appears in F12, F17, F34, F38b, F39, F46ⓔ and F47ⓑ — seven passes.
`GameRules.cs` carries **forty lines of comments** about why he cannot be framed. He never
catches (D7), he contributes nothing to any measure, and `SummaRace_Finalization_Plan.md` §4
still concludes he is "a sliver of head, or a blob growing out of the kid's back" and that
fixing it needs a camera change that is off-limits. The danger vignette already carries the
signal on its own.

**Recommendation: freeze him. No more passes, ever.** If he still reads badly on the real
device, delete him — the vignette is sufficient and D7 means the chase was never allowed to
matter anyway.

**The Trash Dash import.** Sunk cost, not actionable now, but it is the honest source of most
of the remaining risk and it should be said once: the project now carries a 2,807-line director
that drives someone else's game through reflection into private fields (`m_Speed`,
`m_CurrentLane`), three guarded lines inside their code, 120 deleted objects and more still
hidden, an unrelated render pipeline that is the actual shipping one, 178 MB of their audio at
DecompressOnLoad, and a licence question that is still open. Meanwhile `RaceController.cs`
(1,165 lines) still exists, still works, is self-contained, and already had coins, scenery and
worlds. **My opinion: the legacy race would have been the lower-risk ship.** Don't act on that
now — it is far too late — but it explains where the two-days-out risk is concentrated, and it
is the reason §6.1 is ranked first.

**An eleventh race-visuals pass is landing while I write this.** `git status` during this review
showed `GameRules.cs`, `EndlessRaceDirector.cs` and `RaceWorlds.cs` modified and a new
`EndlessWorldDressing.cs` added — an F48 world-dressing pass scattering roadside trees and
grass, with its own new constants and, by its own comment, a budget of "**~120 extra draw
calls on the 2GB floor device**".

I want to be careful here: it is good work and the reasoning in its comments is sound. But it
is adding per-frame cost to a race whose frame rate **has never been measured on any device**,
on a project that **cannot currently produce an APK at all**, forty-eight hours before forty
children use it. That is the pattern this section is about. Ten worlds already differ by light
and fog; nobody in the study will fail because the verge was bare. **Stop adding to the race
and go build the APK.** If the frame budget turns out to be fine, add the trees on Wednesday.

**Documentation.** 5,058 lines across 13 markdown files, several written in the last 24 hours.
`SummaRace_Study_Operations_Runbook.md` is genuinely needed — a teacher will use it. The rest is
developer record. Stop writing documents; that time is an APK.

**`EndlessRaceDirector.cs` at 2,807 lines** is not over-built exactly, but it is a
single-point-of-failure for every remaining fix in the last two days. Do not refactor it now.
Just be aware that every change on the list below lands in one file.

**Not over-built, and worth saying:** the logging schema (`SaveModels.cs`) is excellent —
`runId`/`isPartial`, per-phase timing, `raceFirstOutcome` separating "wrong" from "missed",
`racePicks` capturing which distractor was chosen, `deviceId`, `appVersion`, `schemaVersion`.
That is better than most published instruments manage. The 45 tests are aimed at the right
things (they guard the *validity* properties, not the code). Neither is where the effort was
wasted.

---

## 6. The ranked list

Two days. Roughly 16 working hours plus your own time. Ordered so that if you stop after any
item, what you have is better than what you had.

### 1. Build an APK and put it on the actual study tablet — **3–5 h**
Install Android Build Support (Unity Hub), set Addressables → **Build Addressables content on
Player Build**, build, install, play one full story. `SummaRace_Build_And_Release.md` §1–§4 has
the exact steps and is accurate.

**If skipped:** you ship a build nobody has run on the target hardware. And with
`m_BuildAddressablesWithPlayerBuild: 2` and no Android content ever built, the most likely
outcome is **a race with no world at all** — the app boots fine and then the race is empty. Also
untested: Vulkan on Android-8 budget silicon (it is first in the API list), IL2CPP stripping,
and the 30 fps floor. Nothing else on this list matters if the app does not run.

### 2. Fix the audio import settings — **30 min**
`SummaRace_Device_Budget.md` §5 priorities 1–2; the Editor script is already written (§6).
Stems → CompressedInMemory, music → Streaming, 22 short SFX → DecompressOnLoad.

**If skipped:** ~156 MB of raw PCM resident on a 2 GB device. The likely symptom is the app
being killed while backgrounded, or dying mid-race — which costs a run of data and strands a
child in front of a class.

### 3. Swallow the Android back gesture, and guard Reader NEXT — **45 min, both**
`[Core]` eats `escapeKey` (and set `androidPredictiveBackSupport: 0`); `Advance()` gets
`nextButton.SetActive(false)` or a `_leaving` flag before it raises `ReadingCompleted`.

**If skipped:** children are ejected from the app by an edge swipe all week (§3.1), and
`readingSeconds` — a per-phase study variable — silently reads zero for any learner who taps
NEXT with two thumbs (§3.2). Both are one-line fixes with no design consequences, which is why
they sit this high: nothing else on this list is this cheap per unit of harm avoided.

### 4. Email the researcher, today — **20 min of your time**
Four yes/no questions: (a) `s05_hard` = `s06_hard` duplicate; (b) `s03_easy` mislabelled;
(c) `s01_easy` outlier/AI-authored sign-off; (d) **do we add the Shortened Passage as page 0**
(§1.1). You need the answers back before you can act on (d), so it has to go first.

**If skipped:** the published instrument ships with wrong difficulty labels, a duplicated story,
and a claim about teaching summarizing that the app does not support.

### 5. Race reading time — **15 min + one playtest**
`RaceSecondsPerGate` 12 → **17** (easy 21 s / average 17 s / hard 13.6 s), and
`RepresentSeconds` 3.2 → **6** with `RepresentMinGap` 45 → **80**.

**If skipped:** the hard race gives 9.6 s against the ~16.5 s a 70 wpm reader needs for the
~102 characters at a gate — so the weakest readers, the ones this study exists for, converge on
a 1-in-3 guess on the headline measure. And the re-present after a wrong answer — the single
most important teaching moment in the whole race — currently gives **3.2 seconds** to read the
correct answer and steer into it. That is the shortest window in the game attached to the most
important content in it.

*(Note the tension with §2.1: longer gaps mean more empty corridor. Take the reading time now;
fix the corridor between session 1 and session 2 with coin lines.)*

### 6. Tell the learner where to read, and fix the tap map — **45 min**
Briefing names the top panel; instruction names the middle lane; tap thirds aligned to the
panel columns, or panel columns made into real buttons.

**If skipped:** the F47 legibility fix — the most important change of the last week — may
simply not be noticed by nine-year-olds who are looking at the road.

### 7. Summary hardening — **45 min + a device check**
`SummaryMaxChars` 200 → **350** (the model answer built from a story's own five parts exceeds
200 chars in **18 of 30** stories, and `TMP_InputField.characterLimit` swallows keystrokes
silently). Fix the Somebody check to word-boundary matching with a stoplist. **On the tablet,
confirm the input field is not under the soft keyboard** — the DONE TYPING chip is anchored at
a guessed 0.598 and the field's own bottom edge is at 0.42, inside the likely keyboard band.

**If skipped:** the study's only qualitative datum gets silently truncated in most stories, on a
screen where the child cannot see why their typing stopped appearing.

### 8. A timed, stopwatch full-loop run on a non-`s01` session — **1 h**
All three difficulties of one later session, on the tablet, start to finish, wall clock.

**If skipped:** 27 of 30 stories have never been played once, every tween across F36–F47 is
unverified (PrimeTween/PanelIntro only run in Play mode), and your classroom time budget is a
guess. My derived estimate is 7–13 min per story → **21–39 min per session** — the top of that
range plus a slow typist does not fit comfortably in a period.

### 9. Log the Arrange placements — **45 min**
`_slotContent[]` and `_missCount[]` already exist in memory (`ArrangeController.cs:54,209`) and
are thrown away at the end of the scene. Appending a per-element miss array to `SessionLog`
(schema 3 → 4, purely additive) turns a scalar attempt count into an **SWBST confusion
matrix** — "this learner reliably swaps But and So" is the single most useful thing a
summarizing thesis could report, and right now the screen computes it and discards it.

**If skipped:** unrecoverable. A play-through cannot be repeated.

### 10. Colour-only instructions and the four contrast failures — **1 h**
Still open, verified today: `GameText.cs:261` *"Not quite — the green one is the answer!"* and
`:362` *"The green ones are right"*. Add a ✓ glyph and name the position, not the colour. Plus
the four contrast failures in `SummaRace_Readability_And_Accessibility_Audit.md` §2.3 — the
Reader's "Not quite" line at 2.99:1 is the worst, and it is the text that explains what went
wrong.

**If skipped:** in 40 Grade-4 children, statistically 1–2 boys with a red-green deficiency are
told to look for a colour they cannot pick out, in the exact moment they got something wrong.

### 11. Render and frame settings — **30 min, after item 1 tells you if you need them**
`RenderingPipeline.asset`: shadow cascades 4 → 1, shadowmap 2048 → 512 (nothing in the race can
receive a shadow — every corridor material is single-pass unlit, verified in
`SummaRace_Device_Budget.md` §1.3). `GameRules.TargetFrameRate` 60 → 30 (their `MusicPlayer`
already sets 30 the first time the race loads, so the app currently targets 60 in the menus and
30 afterwards — they disagree). Enable Optimized Frame Pacing. Do **not** reassign
`Mobile_RPAsset` and do **not** touch `renderScale` unless the device says you must.

**If skipped:** you burn a 2048² four-cascade shadow pass every frame for nothing, and a 30 fps
target with Swappy off judders on a 60 Hz panel.

### Explicitly not on this list, and why

- **The patrol cop.** Frozen. Seven passes is enough. (§5)
- **Real hero art ×27.** All 30 resolve. No thesis fails on a hero image.
- **Arrange slot relabelling** (§1.2). It is the right change and it is small, but it alters the
  instrument. If you do it, do it before the first learner plays and never mid-study. If you
  cannot decide, item 8 (logging) captures the data either way.
- **Coin lines / corridor filling** (§2.2). The right fix, wrong week. Patch it after you have
  watched session 1.
- **More documentation.** There is enough.
- **Any refactor of `EndlessRaceDirector.cs`.** Not now.

---

## 7. What I measured vs what I am asserting

Because this project's own notes record several confident conclusions that later turned out
wrong, here is the line.

**Measured, from files I read:** all content statistics in §1.1 and §1.3 (30 JSONs parsed);
the Reader/Race answer-key overlap in §1.2 (compared per element for `s10_average` and
`s05_easy`); the Arrange chance-solve rates (200k-trial simulation of the real lock-and-retry
mechanic); the `SummaryMaxChars` truncation count (model sentence built from each story's own
five parts); the gate timings in §2.1 (`GameRules` against the scene's real `minSpeed`/
`maxSpeed`/`k_Acceleration`); every "returns early" claim about obstacles, coins, jump and
slide (read in `TrackManager.cs` and `CharacterInputController.cs`); the star formula; the
atomic-save and re-entry guards; the absence of any Escape/back handling; `SceneNames.Race`
having zero call sites; and `LicenceDisplayer` not being in a built scene.

**Taken from the other agents' documents, spot-checked but not independently re-derived:** the
178 MB DecompressOnLoad audio figure and the shadow finding (`SummaRace_Device_Budget.md`), the
Addressables build trap (`SummaRace_Build_And_Release.md`), and the contrast ratios
(`SummaRace_Readability_And_Accessibility_Audit.md`). I did verify that the two colour-name
strings that audit flagged are still in `GameText.cs` unchanged.

**Opinion, clearly labelled as such:** that the game gets boring around session 5; that the
legacy race would have been the lower-risk ship; that the patrol cop should be frozen; that
one-tablet-per-learner should be mandated; that `StarsTwoMin` should be 3. Argue with any of
these — they are judgement calls, not findings.

**A caveat on line numbers.** Another pass was editing `GameRules.cs`,
`EndlessRaceDirector.cs`, `RaceWorlds.cs` and `ReaderController.cs` while I was reading them —
`ReaderController.cs`'s question-hide moved from line 243 to 255 mid-review. Citations here were
re-checked against the working tree at the end, but treat any line number as approximate and
grep for the quoted code. I re-confirmed at the end that `RaceSecondsPerGate` (12),
`StarsTwoMin` (4), `SummaryMaxChars` (200) and `RepresentSeconds` (3.2) are all still at the
values this review recommends changing.

**Not verified, and it matters:** everything about the real device. No APK exists. Frame rate,
memory headroom, keyboard occlusion, the back button, Vulkan, tap accuracy on a real
touchscreen, and whether a child can actually read the option panel at arm's length are all
unmeasured. That is why item 1 is item 1.
