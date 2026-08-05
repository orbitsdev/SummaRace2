# SummaRace — Readability & Accessibility Audit

**Date:** 2026-08-05 · **Branch audited:** `experiment/endless-override-2` · **Method:** static
measurement of scene YAML, runtime UI-building controller code, TMP font assets, the 30 story
JSONs and the Trash Dash track/input scripts. **No play-mode run, no device build, no
screenshots** — every number below is derived from files, and where a number could not be
derived it is marked **[NOT MEASURED]** rather than guessed.

**Audience this is judged against:** 40 Grade-4 learners (≈9–10 years old) in the Philippines,
reading English as a second language, on shared Android tablets, in a classroom, over ten
sessions. Some are by the study's own design weaker readers. The app is a **thesis instrument**:
anything that makes the interface hard to *see* or hard to *operate* is measured as if it were
poor summarising, which is a threat to internal validity, not a polish item.

---

## Fix these before the study

Ruthlessly ordered. Items 1–3 change what the instrument measures. Items 4–6 are cheap and
remove real blockers. Everything else in this document can wait until after data collection.

| # | Fix | Why it cannot wait | Effort |
|---|---|---|---|
| **1** | **Give the learner time and size to read the three race answer cards** — HUD preview of the three options at full UI size as the gate approaches, and/or slow the run inside the gate approach. | Derived legible window is **0.4–0.8 s** to read **~19 words across three cards** and commit to a lane. A Grade-4 reader needs ~11 s just to decode that. `raceFirstPickCorrect` **is** the star count and the logged measure — at this window it is dominated by guessing, so the race cannot discriminate between a good and a poor summariser. (§4) | 0.5–1 day, code-only |
| **2** | **Fix the race control instruction, or add tap-to-change-lane.** `GameText.RaceBriefingBody` says *"Swipe or tap left and right to move!"*. `CharacterInputController.cs:203-251` implements **swipe only** on device. Tapping does nothing. | The one instruction a learner gets about how to play is half false. A child who taps and gets nothing concludes the game is broken, at the exact moment the measure starts. | 30 min (reword) or 2 h (add tap) |
| **3** | **Make a two-lane move reachable, or stop recording an unreached card as a wrong answer.** One swipe = `ChangeLane(±1)` only; the far lane needs two separate swipes (finger up, finger down) inside the same <1 s window. | A learner who reads correctly but is in the middle lane cannot physically reach the outer correct card in time. `HandleMissedActiveGate` records that as **first-pick incorrect**. (§4.4) | 2 h |
| **4** | **Rewrite `GameText.VerifyLabel` ("VERIFY ORDER") and `ArrangeIntroStatus`.** "Verify" is adult/technical vocabulary; the instruction line is abstract *and* renders at 20–28 pt. | Arrange is the one screen the story cannot leave until it is right. A learner who does not understand the button or the instruction stalls there in a 55-minute session. (§1, §2) | 15 min |
| **5** | **Remove the two colour-name instructions.** `ReaderWrongFeedback` = *"…the **green** one is the answer!"* and `ArrangeAlmost` = *"The **green** ones are locked in…"*. | ~8% of boys have a red–green deficiency; in 40 learners that is likely 1–2 children who are told to look for a colour they cannot name. Fix is a word change plus a ✓ glyph. (§5) | 30 min |
| **6** | **Raise the four contrast failures that sit on load-bearing text**, all one-line colour changes: FINISH card (**1.7:1**), Reader "Not quite" feedback (**2.99:1**), Summary reference SWBST type words WANTED/SO (**2.34 / 2.10:1**), StorySelect locked-card hint (**2.6:1** where the scrim has faded out). | These are the words that explain what went wrong or what to do next — the ones a struggling reader most needs. (§2.3) | 1 h |

Everything below is the evidence, plus the findings that are real but should not consume the
last two days.

---

## Measurement basis

State this up front so every number can be checked or overruled.

**Canvas.** All twelve scenes and both canvases the race builds at runtime
(`EndlessRaceDirector.BuildHud`, `BuildBriefing`) use `ScaleWithScreenSize`, reference
`1080×1920`, `ScreenMatchMode: MatchWidthOrHeight`, `match 0.5`. On any **16:10** tablet in
portrait the scale factor works out so that **≈1821 reference units span the full screen
height** (1200×1920 → ×1.0541 → 1821.5; 800×1280 → ×0.7027 → 1821.4). On a 16:9 device it is
exactly 1920. So "reference px" is a stable unit and the only thing that changes between
devices is how many millimetres one reference unit is worth.

**Millimetres per reference unit** (portrait, 16:10 panel, screen height / 1821):

| Device | Screen height | mm per ref unit |
|---|---|---|
| 10.1" 1920×1200 | 217.5 mm | 0.1194 |
| 8.0" 1280×800 | 172.4 mm | 0.0947 |
| 7.0" 1280×800 | 150.8 mm | 0.0828 |

**Type metrics, read from the actual TMP assets** (`Assets/Art/Fonts/TMP/*.asset`):
Nunito `CapLine 65 / PointSize 90` → **cap height = 0.722 em**; Fredoka-SemiBold `63 / 90` →
**0.700 em**. Nunito `LineHeight 122.76/90` = 1.364 em; Fredoka 1.21 em.

**Angular size** is the device-independent measure this audit judges by. At a 40 cm viewing
distance, `arcmin = mm × 8.594`. Combining:

| TMP fontSize (ref px, Nunito) | cap arcmin @10.1" | @8" | @7" |
|---|---|---|---|
| 66 | 48.9 | 38.8 | 33.9 |
| 52 | 38.5 | 30.6 | 26.7 |
| 44 | 32.6 | 25.9 | 22.6 |
| 40 | 29.6 | 23.5 | 20.6 |
| 34 | 25.2 | 20.0 | 17.5 |
| 32 | 23.7 | 18.8 | 16.4 |
| 28 | 20.7 | 16.5 | 14.4 |
| 26 | 19.3 | 15.3 | 13.4 |
| 20 | 14.8 | 11.8 | 10.3 |
| 16 | 11.9 | 9.4 | 8.2 |

**The bar used here.** ISO 9241-303 / ANSI-HFES 100 give a *preferred* character (cap) height of
**20–22 arcmin** and an absolute **minimum of 16 arcmin** for adults with normal vision under
good conditions. For 9–10-year-old developing readers, reading a second language, on a shared
tablet in classroom lighting, this audit treats **≥24 arcmin as the target and <16 arcmin as a
failure**. That means, in reference px:

* **10.1" tablet:** target ≥ 33 pt, floor 22 pt
* **8" tablet:** target ≥ 41 pt, floor 27 pt
* **7" tablet:** target ≥ 47 pt, floor 31 pt

**[NOT MEASURED]** Which tablet the study will actually use. The rows above bracket it. If the
device is 10.1", most of §2 becomes MEDIUM instead of HIGH; if it is 7–8", §2 is HIGH as
written. **This is the single cheapest thing the owner can resolve — knowing the panel size
retires or confirms a third of this document.**

**Colour space** is `m_ActiveColorSpace: 0` (**Gamma**), so `new Color(r,g,b)` values in code
are sRGB and WCAG contrast maths applies to them directly with no conversion.

**Safe area:** `androidRenderOutsideSafeArea: 0`, so notch/cutout overlap is *not* a risk. Good
— several elements anchor to `y = 1.0` and would otherwise be under the status bar.

---

## 1. Learner-facing strings

Source: `Assets/_Game/Scripts/Constants/GameText.cs` (all of it), plus strings composed in
`ReaderController`, `ArrangeController`, `SummaryController`, `EndlessRaceDirector`.

**Overall:** the tone work is genuinely good — process praise not ability praise, locked states
that blame the grown-ups, no scolding anywhere. The problems are **vocabulary level**,
**idiom**, and **one inconsistent noun**. Almost all of them are one-line edits in one file.

### 1.1 VALIDITY THREAT — the race instruction is factually wrong

**`GameText.RaceBriefingBody`** (`GameText.cs:278`):

> `Collect the 5 story parts of "{title}" in order.\n\nSwipe or tap left and right to move!`

`Assets/Scripts/Characters/CharacterInputController.cs:203-251` — the device branch reads
`Input.touchCount`, records `m_StartingTouch` on `TouchPhase.Began`, and calls `ChangeLane(±1)`
only when the drag exceeds 1% of screen width. **There is no tap handler.** A tap (press and
release with no movement) changes nothing.

Why it matters here: this is the *only* instruction the learner ever receives about how to
control the race. A 9-year-old who follows it literally taps the left of the screen, nothing
happens, and their model of the game is now "it is broken" — during the scored task.

**Fix (choose one):**
* Reword to `"Swipe left and right to move!"` — 30 seconds, zero risk; **or**
* add tap-to-lane in `EndlessRaceDirector` by calling the public `ChangeLane` on a
  screen-half tap (their script stays untouched, same as `EndlessKeyboardInput` already does
  for WASD). This is the better fix for the audience — a tap is a far easier gesture than a
  directional swipe for a child holding a tablet.

### 1.2 HIGH — "VERIFY ORDER" is adult vocabulary on a blocking button

**`GameText.VerifyLabel = "VERIFY ORDER"`**, rendered at 42 pt on `VerifyButton` in
`Arrange.unity`. "Verify" is not Grade-4 vocabulary in L1 English and is very unlikely to be
known by a Filipino Grade-4 ESL reader. Arrange is the **only** screen the story cannot pass
until the answer is right (`ArrangeController.VerifyRoutine`), so a learner who does not
understand this button has nowhere to go but the assist path four attempts later.

**Fix:** `"CHECK"` (fits comfortably; 42 pt in a 475×144 box). If a verb-object is wanted,
`"CHECK MY ORDER"`.

Related, same screen: **`UndoLabel = "UNDO"`** is computer jargon. `"TAKE BACK"` or an arrow
icon + `"BACK"` is clearer. LOW on its own — UNDO is optional, the learner can also tap a
filled slot to empty it.

### 1.3 HIGH — the Arrange instruction is abstract *and* the smallest text on the screen

**`GameText.ArrangeIntroStatus = "Tap a story part, then tap its place in the order."`**

The brief asks directly whether a 9-year-old can follow this. Three problems:

1. **"its place in the order"** — an abstract possessive over an abstract noun. Nothing on
   screen is labelled "the order"; the learner sees five numbered boxes and five cards.
2. **"a story part"** — the learner has just been shown "story parts" in the race, so the term
   is at least established, but it does not tell them *which* of the two rows to tap.
3. It renders in `StatusText` at **fontSize 28, autosize 20–30**, inside a
   `738 × 76 ref px` box (SpeechBubble `0.21–0.97 × 0.90–1.00`, StatusText `0.05–0.95 ×
   0.06–0.48`). At 28 pt the sentence wraps to two lines and just fits; the *hint* strings that
   share this box are longer (`ArrangeAlmost` is 60 chars) and drive the autosize down toward
   **20 pt = 10.3 arcmin on a 7" tablet**, which is below the acuity-comfort floor entirely.

**Fix:** two short concrete sentences, and give them room:
`"Tap a card below. Then tap the box where it goes."` — and raise the box (`SpeechBubble`
`0.90–1.00` → e.g. `0.88–1.00`) with `fontSizeMin` 20 → 30. Combine with the numbered slots
already on screen: `"Tap a card, then tap a box. Put the story in order, 1 to 5!"` if the space
allows.

### 1.4 HIGH — "mission" means two different things

| String | "mission" means |
|---|---|
| `SessionMapTitle = "Choose a Mission"` | one **session/day** |
| `SessionLockedHint = "Your teacher opens the next mission!"` | one **session/day** |
| `SessionCompleteCheer = "Mission complete! All three stories done!"` | one **session/day** |
| `NextMissionLabel = "NEXT MISSION"` (Results → map) | one **session/day** |
| **`RaceBriefingTitle = "Your Mission"`** | **one race** |

A learner who has learned "mission = the day's three stories" from the map is then shown "Your
Mission" over a single race. For an ESL reader building vocabulary from context this is exactly
the kind of collision that costs comprehension.

**Fix:** `RaceBriefingTitle` → `"Your Race"` (or `"Your Job"`). Keep *mission* for sessions only.

### 1.5 MEDIUM — idioms that will not transfer

`GameText.PraiseGeneric` / `PraiseByStars` / `PraiseByElement` are shown at moments of success,
so a missed idiom is low-cost — but four of sixteen are British or opaque:

| String | Problem | Suggested |
|---|---|---|
| `"Well spotted!"` | British idiom | `"You saw it!"` |
| `"Spot on!"` | British idiom | `"Exactly right!"` (already in the pool) |
| `"You're on a roll!"` | opaque idiom | `"Keep going!"` |
| `"Sharp eyes!"` | idiom, elliptical | `"Good looking!"` → better: `"You looked carefully!"` |
| `"You stayed with it — well done!"` (1★) | phrasal verb | `"You did not give up!"` |
| `"You know this story inside out!"` (3★) | idiom | `"You know this story so well!"` |
| `"That was strong reading!"` (2★) | odd collocation | `"You read that really well!"` |
| `"Perfect run — every part, first try!"` (3★) | no verb, elliptical | `"Perfect! You got every part on the first try!"` |

`GameText.LoadingTips` are **teaching text**, not praise, so their wording matters more:

| Current | Problem | Suggested |
|---|---|---|
| `"WANTED tells what the character wished for."` | "wished for" ≠ *wanted*; "the character" is metalanguage | `"WANTED is what they wanted to do."` |
| `"SO is what the character did about it."` | metalanguage | `"SO is what they did about it."` |
| `"THEN is how everything turned out."` | phrasal verb ("turn out") is a known ESL difficulty | `"THEN is how the story ends."` |
| `"SOMEBODY is who the story is about."` | fine | keep |
| `"BUT is the problem that got in the way."` | phrasal, but consistent with `PraiseByElement` | keep, or `"BUT is the problem they had."` |

⚠️ **`LoadingTips` is indexed by element in `ArrangeController.VerifyRoutine` (line 276)** — do
not reorder it, only rewrite entries in place. `GameText.cs:315-318` already says this.

### 1.6 MEDIUM — Summary wording

| String | Problem | Suggested |
|---|---|---|
| `SummaryHint = "Example: Somebody wanted ___, but ___, so ___, then ___."` | "Example:" is teacher register; `___` is a worksheet convention a 9-year-old may not decode on a screen | `"Like this: Somebody wanted ___, but ___, so ___, then ___."` |
| `SummaryPlaceholder = "Type your one-sentence summary here..."` | "one-sentence summary" is a heavy compound noun | `"Write the whole story in one sentence."` |
| `SummaryNudges[1] = "Almost! Can you say it in one sentence about the Somebody?"` | "about the Somebody" is not natural English and inverts the framework term into an article-noun | `"Almost! Start with who the story is about."` |
| `SummaryNudges[0] = "Try writing a little more — use the story parts above!"` | vague quantity + em dash | `"Try writing a bit more. Use the story parts above!"` |

### 1.7 MEDIUM — `VOICE ON` / `VOICE OFF` is an ambiguous toggle

`ReaderController.RefreshVoiceButton` sets the label to the **current state**
(`NarrationEnabled ? VoiceOn : VoiceOff`) and dims the button image when off. A state-labelled
toggle is ambiguous even to adults ("does VOICE ON mean it *is* on, or *tap to turn it on*?").
Narration is the accessibility support the weakest readers depend on, so a learner who turns it
off by accident and cannot work out how to get it back loses the scaffold for the rest of the
session.

**Fix:** `"VOICE: ON"` / `"VOICE: OFF"` (colon makes it a state readout, not a command), or a
speaker icon with a slash. `HEAR AGAIN` beside it is already well worded.

### 1.8 LOW — em dashes and jargon

Twelve learner-facing strings contain an em dash (`—`): `ReaderWrongFeedback`, `RaceWrongFeedback`,
`ArrangeAlmost`, `ArrangeAssistIntro`, `SummaryNudges[0]`, `StoryUnavailableHint`,
`LockedCardLine`, `PraiseByStars[1][2]`, `PraiseByStars[3][1]`, plus teacher strings. An em dash
is an unfamiliar mark to a Grade-4 ESL reader and a clause break they must infer. A full stop is
always safer. (The glyph itself renders — see §6.2.)

`ArrangeFillFirst = "Fill every slot first!"` — **"slot"** is game jargon.
→ `"Put a card in every box first!"`

`RaceWrongFeedback = "Not quite — the glowing one!"` — sentence fragment with no verb, and
"glowing" is difficult vocabulary. → `"Not quite! Catch the shining card."`

### 1.9 Adult-facing strings

The whole teacher block (`TeacherTitle` … `TeacherExported`) is deliberately plain and is
appropriate for its reader. `TeacherRecoveryWarning` correctly names the cost before the wipe.
No changes recommended.

---

## 2. Type size and contrast

### 2.1 Effective type sizes, measured

All sizes below are read from scene YAML (`m_fontSize`, `m_fontSizeMin/Max`,
`m_enableAutoSizing`) or from the controller that builds the element at runtime. Arcmin is cap
height at 40 cm.

| Element | File | fontSize (autosize) | @10.1" | @7" | Verdict |
|---|---|---|---|---|---|
| Reader `PageText` (the story) | `Reader.unity` | 66 (40–66) | 48.9′ / 29.6′ | 33.9′ / 20.6′ | **OK**, and the floor of 40 protects it |
| Reader `QuestionText` | `Reader.unity` | 52 (34–52) | 38.5′ / 25.2′ | 26.7′ / 17.5′ | OK; floor is marginal on 7" |
| Reader option `Label` ×3 | `Reader.unity` | 44 (**26**–44) | 32.6′ / **19.3′** | 22.6′ / **13.4′** | **MEDIUM** — floor 26 is below the 16′ minimum on a 7–8" panel |
| Reader `FeedbackText` | `Reader.unity` | 40 (28–40) | 29.6′ / 20.7′ | 20.6′ / **14.4′** | MEDIUM |
| Reader `BackChip` / `ReplayChip` (built by `ReaderController.BuildChip`) | code | 30 / 28 (**min 16**) | 22.2′ / **11.9′** | 15.4′ / **8.2′** | **MEDIUM** — a 16 pt floor is at the acuity limit |
| Arrange `Slot` label | `Arrange.unity` | 34, **autosize off** | 25.2′ | 17.5′ | MEDIUM; see 2.2 for the overflow risk |
| Arrange `Piece` label | `Arrange.unity` | 32, **autosize off** | 23.7′ | 16.4′ | MEDIUM |
| **Arrange `StatusText`** (the instruction + hints) | `Arrange.unity` | 28 (**20**–30) | 20.7′ / **14.8′** | 14.4′ / **10.3′** | **HIGH** — the most instructional text on the screen is the smallest |
| Arrange `Title` | `Arrange.unity` | 34, autosize off | 25.2′ | 17.5′ | MEDIUM |
| Summary `ReferenceText` (the 5 SWBST parts) | `Summary.unity` | 32, **autosize off** | 23.7′ | 16.4′ | **MEDIUM/HIGH** — see 2.2 |
| Summary `HintText` | `Summary.unity` | 32, italic | 23.7′ | 16.4′ | MEDIUM (italic costs further legibility) |
| Summary `DoneTypingChip` (built by `SummaryController`) | code | 26 (min 16) | 19.3′ / 11.9′ | 13.4′ / 8.2′ | MEDIUM |
| StorySelect `CardTitle` | `StorySelect.unity` | 30 (**min 8**) | 22.2′ / 5.9′ | 15.4′ / 4.1′ | **MEDIUM** — a min of 8 is not a size, it is a disappearance |
| StorySelect `LockedLabel` / `HintLabel` | `StorySelect.unity` | 40 (min 10) / 28 (min 18) | — | — | MEDIUM |
| SessionMap `LockedHint` | `SessionMap.unity` | 38 (22–38) | 28.1′ | 19.6′ | OK |
| Race SWBST tracker slot labels | `EndlessRaceDirector.BuildTracker` | (24–50) | 17.8′–37.0′ | 12.3′–25.7′ | MEDIUM at the floor; the code comment already records raising this from 18 |
| Race feedback pill | `EndlessRaceDirector.BuildHud` | 56 (30–56) | 41.5′ | 28.8′ | OK |
| Race briefing body | `EndlessRaceDirector.BuildBriefing` | 42 | 31.1′ | 21.6′ | OK |
| Loading tip | `SceneLoader.cs:177` | 38 | 28.1′ | 19.6′ | OK |
| Results `MainIdeaText` | `Results.unity` | 34, autosize off | 25.2′ | 17.5′ | MEDIUM |
| Results treasure chip letter | `ResultsController.cs:158` | (min **10**) | — | — | LOW (decorative) |

**Pattern.** Nothing is catastrophically small on a 10.1" tablet. On a 7–8" tablet, the
**autosize floors** are the problem: `fontSizeMin` values of 8, 10, 16 and 20 appear on text
whose job is to explain something. Autosize floors exist to stop overflow; here they trade a
visible overflow for an invisible failure.

**Blanket recommendation (30 minutes, low risk):** raise every learner-facing `fontSizeMin`
to **26** (and to 30 for `Arrange.StatusText`). Where that then overflows, the box is too small
and should be found in a portrait render — which is much better than shipping 10 pt text.

### 2.2 Two elements can overflow their box silently

* **Summary `ReferenceText`** — `fontSize 32`, **autosize off**, TMP default overflow. The
  content is five rich-text lines built in `SummaryController.cs:63-66`
  (`1. <color><b>SOMEBODY</b></color>: <answer>`). Box is `914 × 389 ref px`. With a mean
  correct-answer length of **34.9 chars** (max **53**), each of the five lines wraps to two, so
  ten lines × ~44 px line height ≈ **436 px in a 389 px box** — an overflow of ~12% for an
  average story, worse for a long one. The last SWBST part (THEN) would be clipped or spill.
  **This is the reference the learner writes their summary from.**
  **Fix:** `enableAutoSizing = true`, min 26, max 32 — or grow `ReferenceCard` from
  `0.66–0.88` to `0.62–0.90`.
* **Arrange `Piece` / `Slot` labels** — `fontSize 32/34`, **autosize off**, in `931 × 119` and
  `931 × 125` boxes. A 53-char answer wraps to two lines at ~44 px each — fits. Marginal, but
  there is no headroom if content ever grows. **Fix:** turn autosize on with min 26.

### 2.3 Contrast (WCAG, computed on the real colours)

Every ratio below was computed from the actual colour values (scene YAML, controller code, or
the mean opaque pixel of the real sprite PNG). Backgrounds marked *(assumed)* could not be
measured statically.

#### Failures on load-bearing text — fix these

| Text | Where | Colours | Ratio | Note |
|---|---|---|---|---|
| **`FINISH` card label** | `EndlessRaceDirector.PlaceFinishGate` — white on `Rectangle 356.png` tinted `(1, .72, .15)` = `(.957,.649,.149)` | white on amber | **1.74–2.03:1** | The end-of-race target. Fix: text `(0.30,0.18,0.02)` deep brown → **8.9:1** |
| **`ReaderWrongFeedback`** | `ReaderController.FeedbackNotQuite (.85,.50,.15)` on white card | orange on white | **2.99:1** | The sentence that tells a learner what went wrong. Fix: `(0.62,0.32,0.02)` → **5.6:1** |
| **Summary reference: `WANTED`** | `SummaryController.cs:65`, `SwbstPalette.Wanted` on white | `(.30,.75,.40)` on white | **2.34:1** | Bold 32 pt |
| **Summary reference: `SO`** | same | `(.98,.62,.15)` on white | **2.10:1** | Bold 32 pt |
| **Summary reference: SOMEBODY / BUT / THEN** | same | | 3.30 / 3.39 / 3.39:1 | large-text only, and 32 pt is not "large" on a 7" panel |
| **StorySelect `HintLabel`** ("Finish the story above first!") | `StorySelect.unity` `(.82,.90,.93)` over hero art where `grad_scrim` alpha has fallen to 0.09–0.31 | | **2.6–4.8:1** | Worst at the top of its band. Placeholder hero art for 27 stories is of unknown brightness |
| **StorySelect `LockedLabel`** ("Locked") | `(.88,.95,.97)`, band `y 0.45–0.65`, but the scrim only reaches `y 0.55` | | **2.95:1 above the scrim** | The top ~30% of the word has no scrim behind it at all |

**Recommended fixes**, all one-liners:
* FINISH: dark text on the amber card.
* `FeedbackNotQuite` → `(0.62, 0.32, 0.02)`.
* Summary reference: use `SwbstPalette.DeepForIndex(i)` instead of `ForIndex(i)` in
  `SummaryController.cs:65`. That is already the project's own "dark variant for text on light
  backgrounds" helper and lifts WANTED to ~4.6:1 and SO to ~4.1:1. Better still, add a 0.65
  darken for this one site.
* StorySelect: extend `Scrim`/`Shade` `anchorMax.y` from `0.55` / `0.48` to `0.70`, which
  covers both the hint and the locked label.

#### Failures on decorative or low-stakes text — LOW, do not spend the two days here

| Text | Ratio | Why it is survivable |
|---|---|---|
| MainMenu `Subtitle` "Read! Race! Summarize!" white on sky | **1.58:1** | Decorative tagline. The coloured words inside it are 2.42 / 2.79 / 3.36:1 |
| MainMenu `Teacher` label, white @75% on sky | **1.40:1** | Deliberately discreet; adult-facing |
| NameEntry `AvatarPrompt` white on `bg_splash` | **1.60:1** *(assumed mid-band)* | Redundant with four visible avatars |
| SessionMap `LockedHint` white on `bg_storyselect` | 4.67:1 at the bottom band, **1.63:1** over the bright mid-band | Depends where the board sits. **[NOT MEASURED]** — needs a render |
| Summary `Placeholder` `(.6,.6,.6)` on white | **2.85:1** | Placeholder text; disappears on first keystroke. Fix anyway: `(0.42,0.42,0.44)` → 5.0:1 |
| Race countdown gold `(1,.83,.20)` over the world, no backing | **2.45:1** *(assumed mid-bright world)* | Rendered at 230–320 pt; size compensates |
| `RaceRunToFinish` banner, white over the world, no backing | **3.51:1** *(assumed)* | 64 pt |
| SWBST `"?"` on the wood plaque (`1,.96,.85 @50%`) | 3.12:1 | Deliberately faded to read as "empty" |
| Results praise cream on the victory panel | 3.55:1 *(assumed)* | 48 pt |

#### Passing, for the record

The core reading surfaces are in good shape and should **not** be touched:
Reader page text **14.9:1**, Reader options **13.3:1** (and **8.7:1** when the correct one turns
green), Arrange piece/slot labels **6.4–9.5:1**, Arrange status **8.7:1**, Summary input text
**17.3:1**, race answer cards black-on-white **~19:1**, race gold re-present card **12.9:1**,
race feedback pill **9.0–11.1:1**, race briefing title **7.4:1** and START **5.3:1** (white on
`empty_buttons/green.png`, mean `(.192,.485,.152)`), StorySelect banner **11.2:1**, Results
main idea **14.9:1**.

Two borderline ones worth knowing about:
* **Arrange empty slots**, `SwbstPalette.DeepForIndex` on `PastelForIndex`: **3.30–4.44:1**
  — large-text only across all five, weakest on SO (orange, 3.30:1). The labels are bold 34 pt,
  so this is acceptable; raising the Deep lerp from 0.30 to 0.45 would clear AA on all five for
  free.
* **Race tracker collected slots**, white word on an SWBST-tinted wood plaque: **4.4–7.0:1**,
  weakest on SO (orange, 4.41:1). Fine.

---

## 3. Tap targets

The Android guideline is **48 dp**. Converting: on a 7" 1280×800 (hdpi, density 1.5), one
reference unit is `0.7027/1.5 = 0.468 dp`, so **48 dp ≈ 103 reference px**. On a 10.1"
1920×1200 (density 1.5), one reference unit is `1.0541/1.5 = 0.703 dp`, so **48 dp ≈ 68
reference px**. The 7" figure (103 ref px) is the conservative bar used below.

| Element | Size (ref px) | dp @7" | dp @10.1" | Verdict |
|---|---|---|---|---|
| Reader `OptionA/B/C` | 817 × 181 | 85 | 127 | **PASS** |
| Reader `NextButton` | 540 × 173 | 81 | 122 | PASS |
| Reader `VoiceButton` | 292 × 77 | **36** | 54 | **MEDIUM** — under 48 dp on a 7" panel, and it is the narration control |
| Reader `ReplayChip` (built at runtime) | 292 × 83 | **39** | 58 | MEDIUM |
| Reader `BackChip` (built at runtime) | 216 × 125 | 59 | 88 | PASS |
| Arrange `Slot_0..4` | 950 × 125 | 59 | 88 | PASS |
| Arrange `Piece_0..4` | 950 × 119 | 56 | 84 | PASS |
| **Gap between adjacent Arrange pieces** | **19 ref px** | **9 dp** | 13 dp | **MEDIUM** — five full-width targets stacked 9 dp apart; an edge tap lands on the neighbour |
| Gap between adjacent Arrange slots | 19 ref px | 9 dp | 13 dp | MEDIUM, same |
| Arrange `Verify` / `Undo` | 475 × 144 / 410 × 144 | 67 | 101 | PASS |
| Summary `SubmitButton` | 540 × 163 | 76 | 115 | PASS |
| Summary `SummaryInput` | 972 × 317 | 148 | 223 | PASS |
| Summary `DoneTypingChip` | 335 × 104 | 49 | 73 | PASS (just) |
| StorySelect `EasyCard` | 943 × 467 | 219 | 328 | PASS |
| StorySelect `Average/HardCard` | 943 × 467 | 219 | 328 | PASS |
| StorySelect `BackButton` | 248 × 77 | **36** | 54 | **MEDIUM** |
| SessionMap `Stop_0..9` | 224 × 156 | 73 | 110 | PASS |
| SessionMap `BackButton` | 208 × 80 | **37** | 56 | MEDIUM |
| NameEntry `Avatar_0..3` | 180 × 180 (gap 30) | 84 | 127 | PASS |
| NameEntry `ConfirmButton` | 620 × 150 | 70 | 105 | PASS |
| MainMenu `StartButton` | 712 × 230 | 108 | 162 | PASS |
| MainMenu `TeacherButton` | 190 × 74 | **35** | 52 | LOW — adult-facing, and being hard to hit is partly the point |
| TeacherMenu `SubmitButton` (OK) | 420 × 104 | 49 | 73 | PASS (just) |
| TeacherMenu action buttons | 660 × 130 | 61 | 91 | PASS |
| TeacherMenu `BackButton` | 208 × 80 | **37** | 56 | MEDIUM (adult) |
| Race — lane change | **swipe, whole screen** | n/a | n/a | see §4.4 |
| Race — pause / exit | **none** | — | — | see §6.4 |

**Findings:**

* **MEDIUM — the four 74–80 px header/footer chips.** `VoiceButton`, `ReplayChip`,
  `BackButton` (StorySelect, SessionMap, TeacherMenu) all land at **35–39 dp** on a 7" panel.
  The Reader's `VoiceButton` is the one that matters for this audience — it is the narration
  control, which the weakest readers depend on. **Fix:** raise `VoiceButton`'s anchor from
  `y 0.955–0.995` to `0.945–0.998` (→ 102 ref px, 48 dp) and give the three Back buttons
  `sizeDelta.y` 80 → 104.
* **MEDIUM — the 19 px gutter between Arrange rows.** The targets themselves are fine; the
  gutters are not. Ten full-width targets stacked with 9 dp between them means a slightly high
  or low tap places the wrong card, or empties a slot the learner meant to fill. Mitigated by
  UNDO and by tap-to-empty, so not a dead end. **Fix (cheap):** shrink each row by ~8 ref px
  (`0.062` → `0.058` of the canvas height) to double the gutter, or keep the visual size and
  inset the `Button`'s raycast area.
* **PASS everywhere else.** Notably the race has no small tap targets at all, because it has
  no taps.

---

## 4. Time pressure in the race — the core of the instrument

This is the finding that matters most. The full derivation is given so it can be checked.

### 4.1 The measured inputs

| Quantity | Value | Source |
|---|---|---|
| Camera | local `(0, 4, −3)`, pitch **14.947°**, **vertical FOV 58.7°** (`m_FOVAxisMode: 0`) | `Assets/Scenes/MainSummaRace.unity:4578, 4604-4610` |
| Runner position ahead of camera | **≈5–6 m** | `GameRules.cs:20-27` (F34/F39 in-play measurements; the two comments disagree between 5 m and 6 m, so 5.7 m is used) |
| Run speed | `minSpeed 10` → `maxSpeed 30`, acceleration `k_Acceleration = 0.2 m/s²` | `MainSummaRace.unity:15480-15483`, `TrackManager.cs:129` |
| Lane offset | **1.5 m** | `MainSummaRace.unity:15483` |
| Answer card | **1.425 m × 0.85 m** (`min(1.55, laneOffset × 0.95)`) | `EndlessRaceDirector.cs:496, 518` |
| Card text box | 1.275 × 0.73 m, TMP autosize **max 2.4** | `EndlessRaceDirector.BuildCard` (`cs:720-723`) |
| Card font | Fredoka-SemiBold SDF, cap **0.700 em**, line height **1.21 em** | `Fredoka-SemiBold SDF.asset` |
| Gate spacing | `clamp(12 s × difficulty × speed, max(checkpointSpacing, 110), 300)` m | `GameRules.cs:72-88`, `EndlessRaceDirector.NextGateGap` |
| Text to read per gate | mean **19.3 words / 102 chars** across three options (max 32 words / 152 chars) | computed over all 30 story JSONs |
| Longest single card | **53 chars**; mean correct **34.9** | same |

### 4.2 How large the card text actually is on screen

TMP world-space text scales at `0.1 world units per point` (the `m_isOrthographic ? 1 : 0.1f`
factor in `TMP_Text`), so at the maximum permitted `fontSize 2.4` one em is **0.24 m** and a
line box is **0.29 m**. The 0.73 m-tall text box therefore holds **2.5 lines at full size**.

An average 35-character answer needs, at font size *F*:
`chars/line = 1.275 / (0.52 × 0.1 F)` and `lines = 0.73 / (0.121 F)` → capacity `≈ 148 / F²`.
Requiring ~40 characters of capacity (35 chars plus wrapping slack) gives **F ≈ 1.92**, so:

* **cap height ≈ 0.134 m** for an average answer,
* **cap height ≈ 0.168 m** for a short one that renders at the 2.4 cap.

At distance *d* from the camera the visible frame is `2 d tan(29.35°) = 1.1246 d` metres tall,
and the screen subtends **1821 arcmin** (10.1") or **1284 arcmin** (7") at 40 cm. Therefore:

`cap arcmin = screenArcmin × capHeight / (1.1246 × d)`

| | 10.1" tablet | 7" tablet |
|---|---|---|
| average answer (cap 0.134 m) | **217 / d** arcmin | **153 / d** arcmin |
| short answer (cap 0.168 m) | 272 / d | 192 / d |

### 4.3 The legible window, in seconds

Solving for the distance at which the card reaches the **20 arcmin preferred** and **16 arcmin
minimum** thresholds, and subtracting the 5.7 m camera-to-runner gap to get how far the card is
in front of the *learner's character*:

| | 20′ (preferred) | 16′ (bare minimum) |
|---|---|---|
| **10.1"**, average answer | legible from **d = 10.9 m** → **5.2 m ahead of the runner** | d = 13.6 m → **7.9 m ahead** |
| **7"**, average answer | d = 7.7 m → **2.0 m ahead** | d = 9.6 m → **3.9 m ahead** |
| **10.1"**, short answer | d = 13.6 m → 7.9 m ahead | d = 17.0 m → 11.3 m ahead |

Converting to time, using the speed the run is actually at when each gate arrives (start 10 m/s,
+0.2 m/s², gates roughly every 11–15 s ⇒ ≈11.6 m/s at gate 1 rising to ≈22 m/s at gate 5):

| | Gate 1 (≈11.6 m/s) | Gate 3 (≈16.5 m/s) | Gate 5 (≈22 m/s) |
|---|---|---|---|
| **10.1"**, 20′ threshold | **0.45 s** | 0.32 s | 0.24 s |
| **10.1"**, 16′ threshold | **0.68 s** | 0.48 s | 0.36 s |
| **7"**, 16′ threshold | **0.34 s** | 0.24 s | 0.18 s |

Even the most generous reading of these numbers — the largest tablet, the shortest possible
answer, the bare acuity minimum, the first and slowest gate — gives **under one second**.

### 4.4 What the learner has to do inside that window

1. **Read three options**, mean **19.3 words / 102 characters**. Grade-4 oral reading fluency
   norms sit around 100–125 wpm for on-level readers and 60–80 wpm for the struggling readers
   this study deliberately includes. At 100 wpm, 19.3 words is **≈11.6 s of decoding alone**,
   before any comprehension or comparison. At 70 wpm it is **≈16.5 s**.
2. **Decide** which matches the SWBST slot the tracker is showing.
3. **Execute a swipe.** `CharacterInputController` gives **one `ChangeLane(±1)` per swipe**, so
   moving two lanes requires **two separate swipes** (finger up, finger down). Two deliberate
   flicks by a 9-year-old is realistically 0.4–0.6 s on its own, plus 2 × 0.107 s of lane travel
   (`laneChangeSpeed 14`, `laneOffset 1.5`).
4. The swipe axis is decided from a displacement of only **1% of screen width** (≈8–12 device
   px), and `|Δy| > |Δx|` is routed to **Jump/Slide**, not a lane change. A hurried diagonal
   flick therefore makes the character jump — and a jump is not a lane change, so the learner
   collides with whichever card is in the lane they were already in.

### 4.5 Why this is a VALIDITY THREAT, not a difficulty setting

All three lanes always carry a card (`PlaceAnswerGate` builds one per lane), so **every gate is
a forced choice**. There is no "no answer". `HandleMissedActiveGate` records a run-past as
first-pick **incorrect**, and `EndlessRaceDirector.OnPickupHit` records the first card touched
as the first pick. `FinishRoutine` writes those five booleans into `RaceResult.firstPickCorrect`,
which is both the star count (`GameRules.StarsThreeMin/TwoMin`) and the logged research
variable.

If the reading window is 0.2–0.7 s against an 11–16 s reading requirement, the learner cannot
in general read in time, so their pick approaches a **1-in-3 guess**. The expected score
converges on 33% for a strong summariser and 33% for a weak one. **A measure that cannot
separate the two groups cannot detect the treatment effect the thesis exists to detect** — and
it will look like a floor effect in the data, which is very hard to interpret after the fact.

Note that F44 already removed the card-width cue precisely because it let learners score
without reading. That fix was right. But it removed the only channel that was making the race
scoreable — it did not make the text readable. The two findings are the same finding seen twice.

### 4.6 Recommended fixes, in order of value per hour

1. **Show the three options as screen-space UI during the approach** (best fix, code-only,
   ~0.5 day). When a gate is placed, populate a HUD row of three panels — left / centre / right,
   in lane order — at full canvas size (44 pt+, the same treatment as the Reader's options),
   and let the world cards become plain coloured lane markers. The learner then reads at
   Reader legibility and only has to *steer*. This preserves the "unsupported recall" design
   (the story text is still gone) while removing the acuity confound entirely. It also fixes
   §4.4's two-lane problem indirectly, because reading can now start 8–10 s before the gate.
2. **Slow the run through the gate approach** (~2 h). Clamp `TrackManager.maxSpeed` to
   `minSpeed + 1` for the last N metres before `_activeGateDistance` and release after. Halving
   the speed doubles every number in §4.3 — still not enough on its own, but it compounds well
   with (1).
3. **Make the far lane reachable** (~2 h). Either allow a swipe magnitude to move two lanes, or
   widen the lane-change window by lengthening `MissGrace`. Cheapest variant: on a swipe, if the
   learner is more than one lane from any card they have not yet passed, allow a second
   `ChangeLane` in the same gesture.
4. **Do not shorten the card text as the primary fix.** It is the obvious lever and it is the
   wrong one: F44 rewrote 120 element sets specifically to equalise option lengths (45.3% /
   +1.4 chars margin), and re-shortening risks re-introducing the length tell that made the race
   passable without reading. Shorten only if (1) is not built.
5. **[NOT MEASURED] Verify on the real device.** Everything in §4.2–4.3 is a derivation from
   file values. One 30-second capture of a real gate at a real distance on the real tablet would
   confirm or overturn it. That should be the first thing done in the next play session.

---

## 5. Colour as the only channel

Around 8% of boys have a red–green colour vision deficiency. In 40 Grade-4 learners that is
**likely 1–2 children**, and this design leans on colour heavily (`SwbstPalette`:
S=blue, W=green, B=red, S=orange, T=purple).

**The good news, checked and confirmed:** the SWBST framework itself is *never* colour-only.
Every place the palette appears carries a text or shape backup:

| Site | Backup |
|---|---|
| Race briefing chips (`MakeChip`) | the element's **first letter** on the chip |
| Race SWBST tracker (`RefreshTracker`) | `"?"` empty / **letter** current / **full word** collected, plus a scale bump and a pulse on the current slot |
| Arrange empty slots (`RefreshUI`) | the **element type word** in bold |
| Summary reference (`SummaryController.cs:65`) | the **numeral** `1.`–`5.` and the **type word** |
| Race lane selector (`BuildLaneSelector`) | it is a **frame behind one card** — position and shape, not hue; and it is the *same* colour whichever card it sits behind, so it also cannot leak correctness |
| StorySelect / SessionMap stars | earned = white tint, unearned = `(0.20,0.28,0.32)` dark — a **lightness** difference, not a hue one |

**The failures:**

### 5.1 HIGH — two strings instruct by colour name

* `GameText.ReaderWrongFeedback` = **"Not quite — the green one is the answer!"**
* `GameText.ArrangeAlmost` = **"Almost! The green ones are locked in — try the others again."**

For a deuteranope, `OptionCorrect (0.55,0.85,0.45)` and `SlotLocked (0.55,0.85,0.45)` are
distinguishable from their neighbours by *lightness*, so the affordance survives — but the
**word "green" does not**, and a child told to find "the green one" who cannot name green has
been handed a task they cannot perform. This is also just poor practice for the sighted
majority: the sentence describes the answer rather than pointing at it.

**Fix:** `"Not quite. This one is the answer!"` and `"Almost! The ✓ ones are right. Try the
others again."`, plus a ✓ glyph or a small tick sprite on the correct/locked element. Both
strings live in `GameText.cs`; the glyph is a few lines in `ReaderController.OnAnswer` and
`ArrangeController.VerifyRoutine`.

### 5.2 MEDIUM — Arrange verify feedback is green vs orange

`SlotLocked (0.55,0.85,0.45)` and `SlotWrong (0.95,0.65,0.3)` are the classic
deuteranope confusion pair (both read as a similar yellow-ochre). Backups exist and are
strong — different sounds (`SfxSlotLock` vs `SfxSlotWiggle`), and the wrong piece **physically
returns to the pool** while the right one stays — so this is not a blocker.
**Fix if time allows:** a ✓ on locked and a ↺ on wrong.

### 5.3 MEDIUM — the palette's own W/B/S triad

`WANTED (0.30,0.75,0.40)` vs `BUT (0.93,0.35,0.35)` vs `SO (0.98,0.62,0.15)` are three of the
five, and are exactly the hues a red–green deficient learner conflates. Because every site
carries text, this costs *speed of recognition*, not correctness. **Fix if time allows:** widen
the lightness spread — e.g. darken BUT and lighten WANTED — so the five are separable by
lightness alone. This would also fix several of the §2.3 contrast borderlines in one change.

### 5.4 LOW — Results treasure chips

`ResultsController.cs:145,160`: earned = `SwbstPalette.ForIndex(i)` + white letter;
missed = `Lerp(colour, grey, 0.65)` + white at **60% alpha**. Earned vs missed is signalled by
saturation and text alpha. It is a summary flourish shown after the stars, not information the
learner acts on. Leave it.

### 5.5 LOW — StorySelect locked cards

`CardLocked (0.62,0.66,0.70)` desaturates and darkens the card. This is a lightness change, and
it is backed by a lock **icon** and the word `"Locked"`. Fine as designed.

---

## 6. Everything else that would block a struggling reader

### 6.1 HIGH — narration covers the story pages and nothing else

`AudioManager.PlayNarration` has exactly **two callers**, both in `ReaderController`
(`cs:188` auto-play, `cs:212` HEAR AGAIN), both passing `_story.pages[_pageIndex].narration`.
The 150 generated clips are page text only.

So a learner who cannot read receives audio support for the **story**, and none for:

* the Reader's **question** and its three **options** (the item being scored),
* the three **race answer cards**,
* the five **Arrange piece texts**,
* the **Summary reference list**,
* every **instruction and button label** in the game.

Some of this is correct by design — a reading comprehension instrument should not read the test
items aloud. But the *instructions* should not be part of the test. A learner who cannot read
`"Tap a story part, then tap its place in the order."` is stuck on an interface task, not a
comprehension task.

**Fix (cheap, high value):** narrate the **instructional strings only** — `ArrangeIntroStatus`,
`ArrangeTitle`, `SummaryTitle`, `SummaryHint`, the five `LoadingTips`, and `RaceBriefingBody`.
That is ~10 clips, generated with the same `edge-tts en-PH-RosaNeural --rate=-10%` recipe
already documented in CLAUDE.md, triggered on screen entry and repeatable from a
`HEAR AGAIN` chip. It leaves every scored item unnarrated, so the measure is untouched.

### 6.2 Resolved — no missing-glyph risk

Checked because CLAUDE.md records a previous "empty box" incident and because Filipino learner
names commonly contain **ñ** (Peña, Muñoz, Niño), typed by the child into `NameEntry`.

All three TMP assets have **`m_AtlasPopulationMode: 1` (Dynamic)** — glyphs are rasterised from
the source font at runtime, so `ñ`, `—`, `·` and `'` will all render even though the current
static tables hold only 20–69 glyphs. **No action needed.** (Residual, LOW: a dynamic atlas that
fills up stops adding glyphs silently; the atlases are 1024² and the content is small, so this
is not a practical risk.)

### 6.3 MEDIUM — text over busy photographic backdrops with no scrim

`Sky` in Reader/Arrange/Summary/StorySelect/SessionMap is a rendered-and-blurred 3D scene
(`bg_playground.png` per CLAUDE.md F13/F15; the file is referenced as `UI/bg_storyselect.png`
in SessionMap and StorySelect — measured mean over bands: `(.248,.515,.130)` bottom,
`(.645,.864,.073)` middle, `(.167,.685,.990)` top, with local pixel values spanning the full
0–1 range). Any white text placed straight onto it has a contrast that varies from ~4.7:1 to
~1.6:1 **depending on where it lands**, which is not a number that can be certified statically.

Affected: SessionMap `LockedHint` (white 38 pt), NameEntry `AvatarPrompt` (white 42 pt),
MainMenu `Subtitle`. The main reading surfaces are all on opaque cards and are unaffected.

**Fix:** put the three loose white labels on the same 9-sliced pill the rest of the UI uses
(`Resources/UI/bar_bg`), or add a `grad_scrim` behind them. ~30 min.
**[NOT MEASURED]** — needs an offscreen portrait render to confirm which bands the labels
actually land on. CLAUDE.md's MCP gotcha #5 describes exactly the recipe.

### 6.4 MEDIUM — the race cannot be left, and there is no pause

Already on the project's own NEXT list, restated here because it is an accessibility issue as
well as a design one. `HideTheirChrome` hides the pause button and `Update` re-hides it every
frame; the pause menu's Exit is hidden because it dead-ends into the Trash Dash loadout. A
learner who is overwhelmed, distressed, or simply needs to stop has **no in-game way out** of a
~60-second run. In a classroom with a supervising researcher this is survivable, but it is a
poor fit for "never punish the learner".

**Minimum fix for the study:** a small pause pill that stops the world and offers "Keep going"
only (no quit) — the child gets control of the clock without breaking the run's data.

### 6.5 LOW–MEDIUM — motion behind and around text

Every card and panel is animated (`PanelIntro`, `UIFloat`, `ButtonSquash`, PrimeTween punch
scales), the Reader fans its options in with a 0.06 s stagger, and the race HUD pulses the
current tracker slot every gate. None of this obscures text for long, but for a learner with
attention difficulties the cumulative effect is a screen where something is always moving.
**[NOT MEASURED]** — none of these tweens run outside Play mode, so their real duration and
overlap are unverified. Worth one deliberate look during the owner's playtest, specifically:
does the tracker pulse or the collect-token flight ever cross the answer cards during the
reading window (§4)? `FlyCollectedToSlot` sweeps a 600×180 px pill across the middle of the
screen for ~0.9 s, and the code comment at `EndlessRaceDirector.cs:1363-1368` already records
that an earlier version of this "swept across the middle of the screen for most of a second
right when the learner needs to see the road".

### 6.6 LOW — font choice

**Nunito** (body, x-height 0.50 em, cap 0.722 em) is a rounded humanist sans with a generous
x-height and open apertures — a good choice for developing readers, and the right one to have
made. **Fredoka SemiBold** (headings, buttons) is a heavy geometric rounded face; at the sizes
used (36–185 pt) it is fine.

Two small notes:
* Nunito's **single-storey `a` is not used** (Nunito has a double-storey `a`), which is correct
  for this age — no change needed.
* **All-caps button labels** (`NEXT PAGE`, `START RACE!`, `VERIFY ORDER`, `LET'S GO!`,
  `NEXT MISSION`, `TAP TO START`, `DONE TYPING`) remove word-shape cues, which measurably slows
  ESL readers. The labels are short and repetitive enough that this is a minor cost, and the
  visual identity depends on them. **No change recommended** — flagged only so it is a decision
  rather than an oversight.

### 6.7 LOW — Reader answer options can drop to 26 pt

`Reader.unity` option labels are `44 (26–44)` with wrapping. F32 deliberately chose wrapping
over shrinking, which was right. But 26 pt is still reachable, and on a 7" panel that is
**13.4 arcmin**. The option boxes are `817 × 181 ref px` and the longest option in the content
is 58 chars (`s01_hard`), which at 44 pt wraps to two lines and fits. **Fix:** raise
`fontSizeMin` from 26 to 32 and let a genuinely oversized option overflow visibly rather than
shrink invisibly.

---

## What could not be measured from files

Stated explicitly so nothing here reads as more certain than it is.

1. **The actual tablet.** Screen size drives §2 and §4 more than anything else. Resolving it
   retires or confirms a third of this document.
2. **On-device legibility of the race cards.** §4.2–4.3 is a derivation from TMP's world-space
   scaling factor, the font asset's own cap-height metric, and camera geometry. It has not been
   confirmed against a rendered frame.
3. **Whether the answer-card sprite and its TMP text are affected by scene fog.** `RaceWorlds`
   sets `fogStart` between **18 m** (`misty_morning`) and **90 m** (`bright_park`). Card sprites
   use the default sprite shader (no fog); TMP's distance-field shader does support fog. Since
   the decision distance derived in §4.3 is under 14 m, fog is almost certainly **not** the
   binding constraint — but the interaction was not confirmed.
4. **Where loose white labels actually land** on the photographic backdrops (§6.3).
5. **All tween timings and overlaps** — PrimeTween, `PanelIntro`, `UIFloat` and `ButtonSquash`
   only run in Play mode (§6.5).
6. **Background luminance assumptions** used for four contrast rows, marked *(assumed)* in
   §2.3: the race world behind the countdown and finish banner, and the Results victory panel.
7. **The exact hero-art brightness** behind StorySelect's locked-card labels — 27 of the 30
   hero images are generated placeholders (CLAUDE.md), so the §2.3 StorySelect numbers use the
   real `s01_hard.png` and will differ once the final art lands. The scrim fix in §2.3 makes the
   result independent of the art, which is why it is the recommended fix.
