# SummaRace — Data Dictionary

**For the researcher / statistician analysing the exported in-app performance data.**
No programming knowledge assumed. Everything below describes the files a tablet produces,
what each variable means, and the traps to avoid when you load them.

- Schema version described here: **5**
- Source of truth in code: `Assets/_Game/Scripts/Data/SaveModels.cs` (`SessionLog`) and
  `Assets/_Game/Scripts/Core/SessionLogService.cs` (what writes it, and when).
- If a field is ever added, the schema version is raised and this file is updated in the same
  change. **Fields are never renamed or removed** — analysis already written against a column
  must keep working.
- **What each version added**, so a mixed export can still be read: **1** the original core
  fields · **2** run context, the four phase clocks, per-element race detail (§3.3) · **3** the
  per-card race record and pause accounting (§3.3) · **4** `participantCode` on every row (§3.1)
  · **5** `arrangeOrders`, the order the learner actually built (§3.4).
- **An older tablet keeps writing its own schema.** If one device is never re-flashed its rows
  simply lack the newer keys. A missing key is **not recorded**, which is not the same as zero —
  see §5.4 and the `*_captured` columns the analysis toolkit writes.

---

## 1. What the app measures, and why these variables exist

The learner meets the **same five SWBST slots four times**, with the support taken away one
step at a time. That ladder is the design, so it is also the measurement:

| Phase | Support | What the data shows |
|---|---|---|
| **Read** | Story text on screen, optional read-aloud | `readingFirstCorrect` — can they identify the slot *with* the text |
| **Race** | Text gone, recall only | `raceFirstOutcome` / `raceFirstPickCorrect` — can they identify the same slot *without* it |
| **Arrange** | Parts given, order them | `arrangeAttempts`, `arrangeSolved`, `arrangeAssisted` — do they know the S-W-B-S-T sequence; `arrangeOrders` — **which** sequence they built |
| **Summary** | Produce one sentence | `summaryText` — the only free-response evidence, for rubric-style qualitative coding |

The **Read → Race contrast on the same five slots** is the core within-learner comparison the
app can offer. Everything else (time on task, attempts, wrong picks) describes how hard that
was.

> The app is the **intervention**, not the graded instrument. The paper pretest/posttest scored
> with the Summary Writing Rubric remains the study's outcome measure (GDD §8.1). These logs
> are process/engagement data and a manipulation check — evidence that a learner actually did
> the SWBST work, how much of it, and how it changed across the ten sessions.

---

## 2. The files you get

A teacher taps **Export** in the PIN-protected Teacher Menu. Two files are written side by
side, at the root of the app's private storage folder (the Teacher Menu prints the full path
on screen — copy it exactly):

| File | Content |
|---|---|
| `export_YYYYMMDD_HHMM.jsonl` | **The data.** Every learner on that tablet, one play-through per line. |
| `export_YYYYMMDD_HHMM_learners.json` | **The roster.** Maps `learnerId` → the child's name/alias. |

`.jsonl` = "JSON Lines": a plain text file where **each line is one complete JSON record**.
It is not a single JSON document — do not try to parse the whole file at once; read it line by
line (every stats package has a reader for this; see §7).

**The data file deliberately contains no names.** Rows are keyed by `learnerId`, a random id.
The roster is the only link back to a child, so it can be stored separately, and the analysis
file stays pseudonymised. Join on `learnerId`.

Roster shape:

```json
{ "learners": [
  { "learnerId": "3f2b9c1e-...", "participantCode": "P07", "displayName": "Maria R.",
    "unlockedSession": 4, "storiesCompleted": 9 }
] }
```

**`participantCode` is your join key to the paper booklets** — the same short id the teacher
copied off that child's pretest/posttest. Since schema 4 it is stamped on **every data row as
well**, so the join survives a roster file that was never pulled off the tablet. It is set only
from the PIN-gated teacher screen, and it is a pseudonym you chose, not the child's name. An
empty `participantCode` means nobody set one on that tablet — a finding about the install, and
worth chasing before the study ends rather than after.

One export covers **one tablet**. With 40 learners on 40 tablets you will concatenate 40
`.jsonl` files and 40 rosters. Rows stay separable because every row carries its own
`learnerId`, and every learner id is unique per tablet.

---

## 3. The row: one play-through of one story

Every key below is present on every schema-5 row, even when empty (`""`, `0`, `false`, `[]`).
A row from an older build simply lacks the keys added after its version; the §3 tables say
which version brought each one in.

### 3.1 Identity and context

| Field | Type | Written | Meaning |
|---|---|---|---|
| `schemaVersion` | integer | story start | Shape of this row. `5` = as documented here. A row with `0` came from an older build (schema 1) and lacks everything in §3.4–3.6. Lower numbers lack whatever their version had not yet added — see the version list at the top. |
| `runId` | string (32 hex) | story start | Unique id for this play-through. **Several rows can share one `runId`** — see §5, deduplication. |
| `isPartial` | boolean | on write | `true` = a mid-run safety snapshot. `false` = the row written when the run ended. |
| `learnerId` | string (guid) | story start | The child. Join key to the roster. Never blank in exported data. Minted on the tablet, so it appears nowhere on paper — use `participantCode` to reach the booklets. |
| `participantCode` | string | story start | **Schema 4.** The researcher's own participant id for this child (e.g. `P07`), copied off their paper booklet by the teacher behind the PIN. **This is the join to the pretest/posttest scores**, and it is duplicated here from the roster on purpose: a lost or forgotten roster file would otherwise sever every row from the outcome measure. Normalised to upper case with spaces removed, and refused at entry if it duplicates another child on that tablet. Empty = the teacher never set one. |
| `storyId` | string | story start | e.g. `s07_average`. 30 possible values: `s01`–`s10` × `easy`/`average`/`hard`. |
| `session` | integer 1–10 | story start | Which of the ten teaching sessions this story belongs to. |
| `difficulty` | string | story start | `easy`, `average` or `hard` — the third within-session step. |
| `isReplay` | boolean | story start | `true` if this learner had **already completed** this story before this run. Per GDD §8.3, exclude replays from first-attempt analysis. |
| `appVersion` | string | story start | Build number. **All rows in the study should carry the same value** — if not, two different builds were in the field, which is a threat to internal validity. |
| `deviceId` | string (12 hex) | story start | Stable, one-way token for the tablet. Not a hardware id and not personal data; it exists only so you can see that a learner switched devices mid-study (e.g. after a device fault) or that two learners shared one tablet. |
| `deviceModel` | string | story start | e.g. `samsung SM-T295`. Useful when a performance complaint needs to be tied to a device class. |
| `startedIso` | string, **UTC** ISO-8601 | story start | When the story was opened. |
| `finishedIso` | string, **UTC** ISO-8601 | Results screen | When the run completed. **Empty string = the run never reached Results** (abandoned) — see §5. |
| `rowWrittenIso` | string, **UTC** ISO-8601 | on write | When this line hit the disk. Use it to order several snapshots of the same run. |
| `lastPhase` | string | continuously | How far the run got: `reading`, `race`, `arrange`, `summary`, `results`, `complete`. On an abandoned row, this is where the learner stopped. |
| `abandonReason` | string | on leaving | **Schema 3.** Why a run ended without finishing. `"race_left_by_learner"` = the learner chose to leave from the race's pause screen. **Empty on a completed run and also on a run that simply stopped** — a killed app, a flat battery, a tablet taken away — which stays recognisable by an empty `finishedIso`. This is the only thing that separates a deliberate exit from a dead device (§5.2). |

### 3.2 Reading phase (support present)

Three lists, **same length, same order** — element *k* of each describes the same answer.
Length is 5 for a completed reading phase (all 30 stories have 5 pages and every page carries
one question), shorter if the run was abandoned mid-reading.

| Field | Type | Meaning |
|---|---|---|
| `readingPageIndices` | list of integers | Which page each answer belongs to. `0`-based, so `0..4`. |
| `readingFirstChoices` | list of integers | Which option they chose, **0-based index into the story JSON's own `options` array** — not the on-screen position. The three options are shuffled on screen for every learner; this field is already mapped back, so it means the same thing for everybody. |
| `readingFirstCorrect` | list of booleans | Was that first answer correct. |
| `narrationOn` | boolean | Whether the read-aloud voice was on when the reading phase ended. **This is a covariate, not a setting** — reading with audio support is a different condition from reading silently, and the learner can toggle it. |

Only the **first** answer to each page is recorded. The learner cannot re-answer a page, so
this is a belt-and-braces rule rather than a filter.

### 3.3 Race phase (support removed)

All five-element lists are in fixed **S-W-B-S-T order**:

| index | 0 | 1 | 2 | 3 | 4 |
|---|---|---|---|---|---|
| slot | Somebody | Wanted | But | So | Then |

| Field | Type | Meaning |
|---|---|---|
| `raceFirstPickCorrect` | list of 5 booleans | Was the learner's **first** encounter with each gate the correct card. **This is the star measure** (§4). Empty list if the race never finished. |
| `raceFirstOutcome` | list of 5 strings | What actually happened at that first encounter: `correct`, `wrong` (they chose a distractor — a comprehension error) or `missed` (they touched no card at all and steered past the gate — an attention/motor event). Empty string = the element was never reached. **Use this rather than `raceFirstPickCorrect` whenever "did not know" and "did not hit it" must be told apart** — the boolean collapses `wrong` and `missed` into the same `false`. |
| `raceWrongPicks` | list of 5 integers | How many wrong cards were picked at each element in total, including any after the first. A wrong pick makes the correct card come back, so this can exceed 1. |
| `raceRunSeconds` | float, seconds | Duration of the race itself, from the world starting to move to the finish line. Excludes the mission briefing and the 3-2-1 countdown. |
| `timesCaught` | integer | **Always `0`. This is by design, not a bug** (GDD decision D7): the patrol is a friendly chase that can never catch the learner, because the game must never punish. The field exists because the design document lists it; treat it as a constant and do not model it. |
| `racePicks` | list of objects | **Schema 3. Every card touched in the race, in the order it happened** — the item-level record. See below. |
| `racePauseCount` | integer | **Schema 3.** How many times the race was paused. Pausing is not a fail state and costs the learner nothing, but an interrupted run is not comparable to an uninterrupted one, and a teacher stepping in is exactly the classroom event that should be visible rather than inferred from an odd duration. |
| `racePausedSeconds` | float, seconds | **Schema 3.** Total real seconds spent paused. `raceSeconds` and `totalSeconds` are real elapsed time and therefore **include** this — subtract it for time actually on task. |

#### `racePicks` — which card, not just right or wrong

`raceWrongPicks` counts *how many* wrong cards were taken at each slot. `racePicks` says
**which** ones. The two distractors at every slot are authored to fail in different ways, so
"chose the retelling instead of the problem" and "chose a detail instead of the goal" are
different misconceptions, and only this can tell them apart.

One object per pick, including repeat picks at the same gate:

| Key | Type | Meaning |
|---|---|---|
| `element` | integer 0–4 | Which SWBST slot, in the S-W-B-S-T order above. |
| `option` | integer | Index into **the story JSON's own option list**: `0` = the correct answer, `1` = `distractors[0]`, `2` = `distractors[1]`. `-1` = the raiser could not say (an older build or the legacy race scene). |
| `text` | string | The card's exact words, stored beside the index so the row still reads on its own if the story files are ever regenerated and the indices point at different words. |
| `lane` | integer | `0` left, `1` centre, `2` right; `-1` unknown. Lanes are randomised per gate, so this is what lets position bias be **checked** rather than assumed away. |
| `correct` | boolean | Was this the right card. |
| `represent` | boolean | **`true` = the single gold card the app handed back** after a wrong pick or a missed gate — not a free choice among three. It is never a first encounter. **Exclude `represent: true` from any "what did they choose" analysis**, or the correct answer will appear to have been chosen far more often than it was. |
| `atSeconds` | float | Seconds since the play-through opened, for ordering picks against the phase clocks. |

> **Truncation rule.** The list is capped at **64 picks**. A well-behaved run produces at most
> 35, so this cannot be reached in play — it exists so that no stuck gate can grow one JSON line
> without bound on a low-memory tablet. If the cap is ever hit, the **earliest picks are kept and
> the newest dropped**, because the early ones are the first encounters and those are the measure.
> A row with exactly 64 entries is the only way to see it happened, and it is worth a glance.

### 3.4 Arrange phase

| Field | Type | Meaning |
|---|---|---|
| `arrangeAttempts` | integer | How many times VERIFY ORDER was pressed. `1` = right first time. |
| `arrangeSolved` | boolean | The learner produced the correct S-W-B-S-T order themselves. |
| `arrangeAssisted` | boolean | After repeated failed attempts the app placed the remaining pieces **for** them (anti-frustration assist). |
| `arrangeOrders` | list of strings | **Schema 5. The order the learner actually built**, one entry per VERIFY press, in the order they were pressed. See below. |

Read the two booleans together — this is the assist ladder:

| `arrangeSolved` | `arrangeAssisted` | Interpretation |
|---|---|---|
| `true` | `false` | Ordered it unaided. |
| `false` | `true` | Was helped to the end. Do **not** count as a success. |
| `false` | `false` | Phase never completed (abandoned run). |

#### `arrangeOrders` — *which* wrong order, not just that it was wrong

Arrange is the **sequencing** rung of the ladder. The three fields above say the sequence was
wrong and how many tries it took; this says what the sequence *was*. Whether a cohort
systematically inverts **But** and **So**, or trails **Then** before **Somebody**, is a finding
about how Grade-4 learners hold story structure, and it cannot be recovered from a count.

Each entry is a **five-character string**. The **position is the slot**, the **character is the
part placed in it**:

```
"01234"   correct — every part in its own slot
"01324"   slot 2 (But) holds part 3 (So), slot 3 (So) holds part 2 (But)  →  a But/So swap
"01243"   So and Then traded places
```

| Reading it | How |
|---|---|
| Was the whole order right first time | `arrangeOrders[0] == "01234"` |
| Which part went into slot *k* | `int(arrangeOrders[0][k])` |
| How many slots were right | count the positions where character == position |
| A clean two-part swap of *a* and *b* | `order[a] == str(b) and order[b] == str(a)` |

> **⚠️ Use entry `0`. It is the only unconstrained one.**
> After the first VERIFY the app **locks every slot that was already right** and leaves it
> filled in, so the second and later boards are partly the app's own answer. Their diagonal is
> inflated and averaging across attempts would read as learning that did not happen. (Which
> slots were locked is still recoverable: after entry *k*, slot *i* is locked if any entry up to
> *k* had part *i* in slot *i*.)

Three more things to know before quoting it:

- **An assisted finish contributes no entry of its own.** When the app places the remaining
  pieces, that board is *not* appended — the last entry is still the last thing the child
  actually built. Otherwise every assisted run would look like five slots correct.
- **An empty list `[]` means the Arrange screen was never reached** (an abandoned run). **The
  key being absent entirely** means an older build that never recorded this. Those are different
  facts and neither is a zero.
- The list is capped at **12 entries**, on the same reasoning as `racePicks`: a real run
  produces at most 4. On overflow the **earliest are kept** — entry `0` is the measure.

### 3.5 Summary phase

| Field | Type | Meaning |
|---|---|---|
| `summaryText` | string | **The sentence the child typed, verbatim.** The only free-response data in the app; intended for qualitative coding against the same rubric constructs as the paper task. Never edited, never spell-corrected, may be empty if they submitted nothing. |
| `nudgeCount` | integer 0–2 | How many gentle prompts the app showed before accepting ("tell me a little more", "say it in ONE sentence", "who was it about?"). `0` = accepted straight away. After 2 nudges anything is accepted — the app never blocks. **The app does not grade the sentence and this is not a quality score**; it is a rough proxy for how far the first attempt was from the expected shape. |

### 3.6 Outcome and timing

| Field | Type | Meaning |
|---|---|---|
| `starsEarned` | integer 1–3 | The reward shown to the learner. Computed **only** from first-pick race accuracy: 5 of 5 → 3 stars, 4 of 5 → 2 stars, 3 or fewer → 1 star. Minimum is always 1 — there is no zero. `0` in the data means the run never reached Results. |
| `totalSeconds` | float | Whole play-through, story opened → row written. |
| `readingSeconds` | float | Story opened → last reading question answered. |
| `raceSeconds` | float | Reading finished → finish line. Includes briefing + countdown; `raceRunSeconds` is the moving part alone. |
| `arrangeSeconds` | float | Race finished → order solved or assisted. |
| `summarySeconds` | float | Ordering done → sentence submitted. |

**Timing caveat — read before using any duration.** All durations are real elapsed time. If a
learner puts the tablet down, is called away, or the app is backgrounded, that waiting is
inside the number. Treat long outliers as interruptions, not as effort: winsorise, or filter
on a sensible ceiling, before averaging. The four phase durations do not have to sum exactly
to `totalSeconds` (the Results screen and any tail after the last event fall outside them).

---

## 4. A worked example row

One completed run. In the real file this is a **single line**; it is wrapped here for reading.

```json
{
  "runId": "9c1f4b7a2e6d4a1f8b0c3d5e7f912345",
  "isPartial": false,
  "learnerId": "3f2b9c1e-77a4-4f0e-9d21-6b8c0a5e4d13",
  "participantCode": "P07",
  "storyId": "s03_average",
  "startedIso": "2026-09-14T02:11:07.4180000Z",
  "finishedIso": "2026-09-14T02:19:52.9030000Z",
  "totalSeconds": 525.4,
  "readingFirstChoices": [1, 0, 2, 0, 1],
  "readingFirstCorrect": [true, true, false, true, true],
  "raceFirstPickCorrect": [true, true, false, false, true],
  "timesCaught": 0,
  "arrangeAttempts": 2,
  "arrangeAssisted": false,
  "nudgeCount": 1,
  "summaryText": "Ana wanted to join the parade but she lost her slippers so her lola made new ones and she danced.",
  "starsEarned": 1,
  "isReplay": false,
  "schemaVersion": 5,
  "appVersion": "1.0.0",
  "deviceId": "4a7c19e0b3d2",
  "deviceModel": "samsung SM-T295",
  "rowWrittenIso": "2026-09-14T02:19:53.0110000Z",
  "session": 3,
  "difficulty": "average",
  "narrationOn": true,
  "lastPhase": "complete",
  "readingPageIndices": [0, 1, 2, 3, 4],
  "readingSeconds": 214.8,
  "raceSeconds": 168.2,
  "arrangeSeconds": 96.1,
  "summarySeconds": 44.0,
  "raceRunSeconds": 121.6,
  "raceWrongPicks": [0, 0, 1, 0, 0],
  "raceFirstOutcome": ["correct", "correct", "wrong", "missed", "correct"],
  "arrangeSolved": true,
  "racePicks": [
    {"element":0,"option":0,"text":"Ana, a girl from the barangay","lane":2,"correct":true,"represent":false,"atSeconds":131.4},
    {"element":1,"option":0,"text":"To dance in the fiesta parade","lane":0,"correct":true,"represent":false,"atSeconds":146.9},
    {"element":2,"option":2,"text":"The parade started very early","lane":1,"correct":false,"represent":false,"atSeconds":161.2},
    {"element":2,"option":0,"text":"Her only slippers were lost","lane":1,"correct":true,"represent":true,"atSeconds":166.0},
    {"element":4,"option":0,"text":"She danced at the front","lane":1,"correct":true,"represent":false,"atSeconds":205.7}
  ],
  "racePauseCount": 0,
  "racePausedSeconds": 0.0,
  "abandonReason": "",
  "arrangeOrders": ["01324", "01234"]
}
```

**How to read it.** This learner answered 4 of 5 reading questions right *with the text in
front of them* and got 3 of 5 race gates right *from memory* — the support-removal drop the
study is interested in. Slot 2 (**But**) is the interesting one: wrong in the Reader **and**
wrong in the Race, with one wrong pick — a genuine misunderstanding of the story's problem.
Slot 3 (**So**) reads `false` in `raceFirstPickCorrect` but `missed` in `raceFirstOutcome`
with **zero** wrong picks: they never chose a wrong answer, they simply drove past the gate.
Counting that as a comprehension failure would understate this child. They then ordered the
five parts unaided on the second attempt and wrote a sentence that carries all five SWBST
slots after one nudge — much stronger performance than `starsEarned: 1` suggests, because
stars come from race accuracy alone.

**What the two new lists add.** `racePicks` names the wrong idea: at **But** this child chose
*"The parade started very early"* — a detail from the story rather than the problem — and the
following entry has `represent: true`, which is the app handing back the gold card and must not
be counted as a choice. Note there is **no free pick at all for element 3 (So)**: that is what a
`missed` gate looks like in this list, and it is why the outcome field says `missed` rather than
`wrong`. `arrangeOrders` shows the same **But/So confusion again on a different screen** — the
first board `"01324"` put the So part in the But slot and the But part in the So slot, and the
second board (with those two now the only unlocked slots) got it right. One child agreeing with
themselves across two phases is far stronger evidence than either screen alone.

---

## 5. Cleaning rules — do these first

### 5.1 Deduplicate: keep one row per `runId`

The app writes a safety snapshot whenever it is backgrounded (a low-memory tablet can be
killed while the app is away), so **one play-through can appear as several lines**.

> **Rule:** group by `runId`; keep the row with `isPartial == false` if one exists,
> otherwise keep the row with the latest `rowWrittenIso`.

Never sum or average across rows sharing a `runId` — you would count one child twice.

### 5.2 Identify abandoned runs

A run that never reached the Results screen has:

- `finishedIso == ""` **and** `starsEarned == 0`, and
- `lastPhase` telling you where it stopped, and
- `abandonReason` telling you **why**, when the app knows.

**`abandonReason` is the only way to separate a deliberate exit from a dead device.**
`"race_left_by_learner"` means the child chose to leave from the race's pause screen — a
disengagement event you can count. **Empty** means the run simply stopped: the app was killed,
the battery died, the tablet was taken away, the session ended. Do not read an empty reason as
"finished normally" — a completed run is identified by `finishedIso`, not by this field.

Abandoned rows are kept on purpose (a failing tablet then costs at most one screen of data),
but they are **not completed play-throughs**. Decide explicitly whether your analysis uses
them; for anything involving `starsEarned`, `summaryText` or phase durations, exclude them.
On an abandoned row `raceFirstOutcome` only reaches as far as the learner got, and
`arrangeOrders` is `[]` if they never got to Arrange.

### 5.3 Separate first attempts from replays

`isReplay == true` means this learner had already completed this story. Exclude replays from
any first-attempt/learning analysis (GDD §8.3). Within a learner and story, order attempts by
`startedIso`.

### 5.4 Check the constants

Before analysing, confirm across the whole file: `timesCaught` is always `0`, `schemaVersion`
is always `5`, and `appVersion` has exactly one distinct value. Any surprise there is a data
provenance problem, not a finding.

**More than one `schemaVersion` means a tablet was never re-flashed.** Its rows are perfectly
readable, they simply lack the keys added after that version. Handle it by reporting **n
separately for every measure that has missing rows** — never by filling the gaps with `0`. A
build that could not count pauses did not observe "0 pauses", and averaging that invented zero
into a mean puts a number in the thesis that nothing measured. The analysis toolkit in
`Tools/Analysis/` writes a `*_captured` column beside each such measure and counts the affected
rows in its data-quality report; do the same if you load the files yourself.

---

## 6. Derived measures worth computing

| Measure | How |
|---|---|
| Reading accuracy | `sum(readingFirstCorrect) / 5` |
| Race accuracy (**= the star measure**) | `sum(raceFirstPickCorrect) / 5` |
| **Support-removal drop** | reading accuracy − race accuracy, per run |
| Per-slot difficulty | for each index 0–4, proportion `correct` across learners — shows which SWBST slot the cohort finds hardest |
| Comprehension error rate | proportion of `raceFirstOutcome == "wrong"` (excludes `missed`) |
| Which wrong idea, per slot | count `racePicks` entries by (`element`, `option`), **excluding `represent: true`** and keeping only each element's first free pick |
| Sequencing accuracy | proportion of runs whose `arrangeOrders[0] == "01234"` |
| **Per-slot sequencing confusion** | for each slot *k* and part *e*, how often `arrangeOrders[0][k] == str(e)`. The 5×5 matrix; the diagonal is correct, the off-diagonal cells are the systematic confusions |
| Clean two-part swaps | for each pair *(a,b)*, how often `order[a]==str(b)` **and** `order[b]==str(a)` on the first attempt — a transposition is stronger evidence than one wrong slot, because guessing rarely produces a tidy swap |
| Handling-noise rate | proportion of `raceFirstOutcome == "missed"` — if this is high, the race is testing dexterity, not comprehension, and the star measure is contaminated |
| Growth over the study | any of the above, by `session` (1–10), first attempts only |
| Difficulty gradient | any of the above, by `difficulty`, holding `session` constant |

**Aligning the Reader with the Race (important assumption).** The five reading questions
pre-teach the five SWBST slots in order, so reading page `i` corresponds to race element `i`
(`readingPageIndices` gives the page for each answer). This is a **content convention** in the
story files, not a field the app enforces — the researchers should confirm it holds for the
stories they intend to compare slot by slot before reporting a per-slot Reader→Race contrast.
The whole-story contrast (4/5 vs 3/5 above) does not depend on the assumption.

---

## 7. Loading the export

**Python / pandas**

```python
import pandas as pd

df = pd.read_json("export_20260914_1030.jsonl", lines=True)

# 5.1 deduplicate: one row per play-through
# .head(1), NOT .first(): pandas' groupby.first() takes the first NON-NULL value in each
# column INDEPENDENTLY, so it can assemble a row out of two different snapshots of the same
# run -- a play-through that never existed. .head(1) keeps one real row intact.
df = (df.sort_values(["isPartial", "rowWrittenIso"],
                     ascending=[True, False])   # final row first, then newest snapshot
        .groupby("runId", sort=False)
        .head(1))

roster = pd.json_normalize(pd.read_json("export_20260914_1030_learners.json")["learners"])
df = df.merge(roster[["learnerId", "participantCode", "displayName"]],
              on="learnerId", how="left", suffixes=("", "_roster"))
# The row's own participantCode wins -- it was written when the run happened. Fall back to
# the roster only for rows from a build older than schema 4.
df["participantCode"] = df["participantCode"].replace("", pd.NA).fillna(
    df["participantCode_roster"])

completed = df[(df.finishedIso != "") & (~df.isReplay)]
completed["race_acc"]    = completed.raceFirstPickCorrect.apply(lambda v: sum(v) / 5)
completed["reading_acc"] = completed.readingFirstCorrect.apply(lambda v: sum(v) / 5)
completed["drop"]        = completed.reading_acc - completed.race_acc

# The Arrange order the learner built (schema 5). NaN = the build never recorded it;
# an empty list = recorded, but the Arrange screen was never reached. Do not conflate them.
has_order  = completed.arrangeOrders.apply(lambda v: isinstance(v, list) and len(v) > 0)
first_ord  = completed.loc[has_order, "arrangeOrders"].str[0]
completed.loc[has_order, "arrange_first_exact"] = (first_ord == "01234").astype(int)
# the classic But/So swap, on the first (unconstrained) attempt only
completed.loc[has_order, "arrange_but_so_swap"] = (
    first_ord.str[2].eq("3") & first_ord.str[3].eq("2")).astype(int)
```

> The whole of the above — deduplication, the roster join, the Arrange confusion matrix and a
> data-quality report — is already written for you in `Tools/Analysis/summarace_analyze.py`,
> which needs no pandas and no installation at all. Its README is written for a non-programmer.
> Use this section only if you would rather work in a notebook.

**R**

```r
library(jsonlite); library(dplyr)
df <- stream_in(file("export_20260914_1030.jsonl"))
df <- df %>% arrange(isPartial, rowWrittenIso) %>% group_by(runId) %>% slice(1) %>% ungroup()
```

**Excel / SPSS:** convert first — `.jsonl` will not open usefully, and the list-valued columns
(`raceFirstPickCorrect`, `readingFirstCorrect`, `raceFirstOutcome`, …) must be expanded into
five columns each (`race_s`, `race_w`, `race_b`, `race_s2`, `race_t`) before import.
`racePicks` is a list of objects and `arrangeOrders` a list of strings, so neither flattens into
one row at all: `racePicks` needs its own long table (one row per pick) and `arrangeOrders`
wants its first entry pulled out into one column plus five per-slot columns. Do that
in the Python step above and export a flat CSV.

---

## 8. Privacy and data handling

- Everything is written **on the device only**. There is no network code in the app; nothing is
  ever transmitted. Export is a deliberate teacher action behind a PIN.
- The data file carries **no child names** — only `learnerId` and `participantCode`. Neither is a
  name: `learnerId` is a random id minted on the tablet, and `participantCode` is the
  researcher's own booklet pseudonym. The roster is the only file that maps either to a child —
  keep it separate from the analysis file, and destroy it when the linkage is no longer needed.
- `deviceId` identifies a tablet, not a person, and is a one-way hash — the hardware id itself
  is never stored.
- `summaryText` is free text written by a child and **may contain a name or other identifying
  detail**. Treat it as identifiable until it has been screened.
- **Export before the study ends.** The Teacher Menu wipe deletes every profile and every log on
  that tablet permanently, and the export function lives behind the same PIN — logs cannot be
  retrieved after a wipe.

---

## 9. Known limitations (state these, do not work around them)

1. **`timesCaught` is always 0** — the patrol never catches the learner (GDD D7). It is a
   constant, not a measure.
2. **Durations include interruptions** (§3.6). No idle detection.
3. **`nudgeCount` is not a summary quality score.** The app never grades the sentence; the paper
   rubric does.
4. **`starsEarned` is a function of `raceFirstPickCorrect` alone** — it carries no information
   the race lists do not already carry, and it ignores the reading, arrange and summary phases.
   Do not use it as an overall performance score.
5. **The `represent` trap in `racePicks`.** After a wrong pick or a missed gate the app hands the
   learner the correct card back. That pick is logged like any other but carries
   `represent: true`, and counting it as a choice would make the correct answer look far more
   popular than it was. Filter it out of every "what did they choose" analysis. (This is the one
   real footgun in the item-level data; the item-level record itself has existed since schema 3.)
6. **One row = one story, not one classroom session.** A session is three stories; aggregate by
   `session` yourself.
7. **No content-pack version in the row.** If story text or answer options are revised
   mid-study, the rows will not show it. `appVersion` will change only if a new build is
   installed — freeze content before the study starts.
8. **`arrangeOrders` is only unconstrained at entry `0`.** From the second VERIFY on, slots
   already correct are locked and pre-filled by the app, so later boards are partly the app's
   answer. All the per-slot confusion measures in §6 use the first attempt alone.
9. **A missing key is not a zero.** An older tablet's rows simply lack the newer fields (§5.4).
   Report n separately for any measure with missing rows; never fill the gap with `0` or `false`.
10. **`participantCode` is only as good as the install.** It is typed by an adult from the paper
   booklet, and an empty one means nobody set it on that tablet. Check for blanks and for two
   children sharing a code **before** the study ends — afterwards the linkage cannot be
   reconstructed, because the data file carries no names by design.
