# SummaRace — Data Dictionary

**For the researcher / statistician analysing the exported in-app performance data.**
No programming knowledge assumed. Everything below describes the files a tablet produces,
what each variable means, and the traps to avoid when you load them.

- Schema version described here: **2**
- Source of truth in code: `Assets/_Game/Scripts/Data/SaveModels.cs` (`SessionLog`) and
  `Assets/_Game/Scripts/Core/SessionLogService.cs` (what writes it, and when).
- If a field is ever added, the schema version is raised and this file is updated in the same
  change. **Fields are never renamed or removed** — analysis already written against a column
  must keep working.

---

## 1. What the app measures, and why these variables exist

The learner meets the **same five SWBST slots four times**, with the support taken away one
step at a time. That ladder is the design, so it is also the measurement:

| Phase | Support | What the data shows |
|---|---|---|
| **Read** | Story text on screen, optional read-aloud | `readingFirstCorrect` — can they identify the slot *with* the text |
| **Race** | Text gone, recall only | `raceFirstOutcome` / `raceFirstPickCorrect` — can they identify the same slot *without* it |
| **Arrange** | Parts given, order them | `arrangeAttempts`, `arrangeSolved`, `arrangeAssisted` — do they know the S-W-B-S-T sequence |
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
  { "learnerId": "3f2b9c1e-...", "displayName": "Maria R.", "unlockedSession": 4, "storiesCompleted": 9 }
] }
```

One export covers **one tablet**. With 40 learners on 40 tablets you will concatenate 40
`.jsonl` files and 40 rosters. Rows stay separable because every row carries its own
`learnerId`, and every learner id is unique per tablet.

---

## 3. The row: one play-through of one story

Every key below is present on every schema-2 row, even when empty (`""`, `0`, `false`, `[]`).

### 3.1 Identity and context

| Field | Type | Written | Meaning |
|---|---|---|---|
| `schemaVersion` | integer | story start | Shape of this row. `2` = as documented here. A row with `0` came from an older build (schema 1) and lacks everything in §3.4–3.6. |
| `runId` | string (32 hex) | story start | Unique id for this play-through. **Several rows can share one `runId`** — see §5, deduplication. |
| `isPartial` | boolean | on write | `true` = a mid-run safety snapshot. `false` = the row written when the run ended. |
| `learnerId` | string (guid) | story start | The child. Join key to the roster. Never blank in exported data. |
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

### 3.4 Arrange phase

| Field | Type | Meaning |
|---|---|---|
| `arrangeAttempts` | integer | How many times VERIFY ORDER was pressed. `1` = right first time. |
| `arrangeSolved` | boolean | The learner produced the correct S-W-B-S-T order themselves. |
| `arrangeAssisted` | boolean | After repeated failed attempts the app placed the remaining pieces **for** them (anti-frustration assist). |

Read the two booleans together — this is the assist ladder:

| `arrangeSolved` | `arrangeAssisted` | Interpretation |
|---|---|---|
| `true` | `false` | Ordered it unaided. |
| `false` | `true` | Was helped to the end. Do **not** count as a success. |
| `false` | `false` | Phase never completed (abandoned run). |

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
  "schemaVersion": 2,
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
  "arrangeSolved": true
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
- `lastPhase` telling you where it stopped.

Abandoned rows are kept on purpose (a failing tablet then costs at most one screen of data),
but they are **not completed play-throughs**. Decide explicitly whether your analysis uses
them; for anything involving `starsEarned`, `summaryText` or phase durations, exclude them.

### 5.3 Separate first attempts from replays

`isReplay == true` means this learner had already completed this story. Exclude replays from
any first-attempt/learning analysis (GDD §8.3). Within a learner and story, order attempts by
`startedIso`.

### 5.4 Check the constants

Before analysing, confirm across the whole file: `timesCaught` is always `0`, `schemaVersion`
is always `2`, and `appVersion` has exactly one distinct value. Any surprise there is a data
provenance problem, not a finding.

---

## 6. Derived measures worth computing

| Measure | How |
|---|---|
| Reading accuracy | `sum(readingFirstCorrect) / 5` |
| Race accuracy (**= the star measure**) | `sum(raceFirstPickCorrect) / 5` |
| **Support-removal drop** | reading accuracy − race accuracy, per run |
| Per-slot difficulty | for each index 0–4, proportion `correct` across learners — shows which SWBST slot the cohort finds hardest |
| Comprehension error rate | proportion of `raceFirstOutcome == "wrong"` (excludes `missed`) |
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
df = (df.sort_values(["isPartial", "rowWrittenIso"])          # False sorts before True
        .groupby("runId", as_index=False).first())

roster = pd.json_normalize(pd.read_json("export_20260914_1030_learners.json")["learners"])
df = df.merge(roster[["learnerId", "displayName"]], on="learnerId", how="left")

completed = df[(df.finishedIso != "") & (~df.isReplay)]
completed["race_acc"]    = completed.raceFirstPickCorrect.apply(lambda v: sum(v) / 5)
completed["reading_acc"] = completed.readingFirstCorrect.apply(lambda v: sum(v) / 5)
completed["drop"]        = completed.reading_acc - completed.race_acc
```

**R**

```r
library(jsonlite); library(dplyr)
df <- stream_in(file("export_20260914_1030.jsonl"))
df <- df %>% arrange(isPartial, rowWrittenIso) %>% group_by(runId) %>% slice(1) %>% ungroup()
```

**Excel / SPSS:** convert first — `.jsonl` will not open usefully, and the list-valued columns
(`raceFirstPickCorrect`, `readingFirstCorrect`, `raceFirstOutcome`, …) must be expanded into
five columns each (`race_s`, `race_w`, `race_b`, `race_s2`, `race_t`) before import. Do that
in the Python step above and export a flat CSV.

---

## 8. Privacy and data handling

- Everything is written **on the device only**. There is no network code in the app; nothing is
  ever transmitted. Export is a deliberate teacher action behind a PIN.
- The data file carries **no child names** — only `learnerId`. Keep the roster file separate
  from the analysis file, and destroy the roster when the linkage is no longer needed.
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
5. **No item-level record of the race distractor chosen.** For a race gate you know *whether* the
   pick was wrong and how many wrong picks there were, not *which* distractor was taken. (The
   reading phase does record the chosen option index.)
6. **One row = one story, not one classroom session.** A session is three stories; aggregate by
   `session` yourself.
7. **No content-pack version in the row.** If story text or answer options are revised
   mid-study, the rows will not show it. `appVersion` will change only if a new build is
   installed — freeze content before the study starts.
