# SummaRace — turning the tablet exports into thesis tables

**This folder is for after the study, when you have a pile of files off 40 tablets
and need tables for your results chapter.**

You do not need to know Python. You need to be able to copy files into a folder and
type one line. That is the whole job.

There is **nothing to install**. No pandas, no pip, no internet. If the lab machine
has Python 3, this works.

---

## 0. Do this FIRST — today, not after the study

Rehearse the whole thing on fake data. It takes two minutes and it is the only way
to find out that a variable you need is missing **while there is still time to add
it**. Once 40 children have played, the data is what it is forever.

Open a terminal in this folder and run:

```
python make_test_data.py --out fake_exports
python summarace_analyze.py --in fake_exports --out fake_analysis
```

Then open `fake_analysis/` and look at every CSV. Ask yourself, for each table in
your results chapter: **is the number I need in one of these files?** If it is not,
say so now — the app can still be changed.

The fake data deliberately contains the messes you will really get (a tablet
exported twice, a test profile, a child who missed two sessions, abandoned runs), so
you also get to see what the data-quality report looks like when it has something to
complain about.

---

## 1. What to copy off the tablets

On each tablet: **Teacher Menu → Export**. The screen prints a folder path. In that
folder are two files with today's date:

| File | What it is |
|---|---|
| `export_YYYYMMDD_HHMM.jsonl` | **the data** — every play-through on that tablet |
| `export_YYYYMMDD_HHMM_learners.json` | **the names** — which learnerId is which child |

**Copy BOTH.** The `.jsonl` has no names in it at all; without the `_learners.json`
you cannot tell whose data is whose.

> ### ⚠️ Export before the study ends, not after
> The Teacher Menu wipe deletes every log on that tablet permanently, and export
> lives behind the same PIN — after a wipe the logs are gone. Export at the halfway
> point as well, as a backup. Exporting twice is completely safe: the analyser
> notices the overlap and counts nothing twice. (See §6.)

### Where to put them

Make one folder and put **all 80 files** in it (40 data + 40 rosters). Do not rename
them — the tablet name keeps them apart. If two tablets produce a file with the same
name, add the tablet number: `export_20261019_1540_tablet07.jsonl`.

```
C:\thesis\exports\
    export_20261019_1540_tablet01.jsonl
    export_20261019_1540_tablet01_learners.json
    export_20261019_1540_tablet02.jsonl
    ...
```

**Keep a second copy of that folder somewhere else before you do anything.** The
analyser never writes to it, but you will be handling irreplaceable data on a
deadline, and a stray drag-and-drop is how it gets lost.

---

## 2. The one command

```
python summarace_analyze.py --in C:\thesis\exports --out C:\thesis\analysis
```

That is it. `--in` is where you put the exports. `--out` is a **new, different**
folder for the tables. (The script refuses to write into your export folder.)

Add `--stories` if you also want the story-alignment audit (see §5):

```
python summarace_analyze.py --in C:\thesis\exports --out C:\thesis\analysis --stories ..\..\Assets\_Game\Resources\Stories
```

Everything the script does is printed on screen **and** saved to
`analysis/00_report.txt`. Paste that into your methods appendix — it is a complete
record of what was read, what was excluded, and why.

### If it stops with a big red STOPPED message

Good. It found a line it could not read and it will not throw a play-through away
without telling you. Open `analysis/00_bad_rows.csv`; it names the file, the line
number and the reason.

The usual cause is the **last line of one file** being half-written, because a tablet
was switched off mid-save. That costs you one play-through and nothing else. Once you
have looked and you accept it:

```
python summarace_analyze.py --in C:\thesis\exports --out C:\thesis\analysis --skip-bad-rows
```

---

## 3. The files you get, and which one answers which question

| File | Your question | What it is |
|---|---|---|
| `01_runs.csv` | *"give me the raw table"* | **One row per play-through**, cleaned and de-duplicated. This is the file you open in Excel or import into SPSS. Everything else is built from it. |
| `02_learner_session.csv` | *"how did each child do in each session?"* | One row per learner × session (a session is 3 stories). |
| `03_swbst_contrast.csv` | **THE research question** | Reader (text visible) vs Race (text gone), **per SWBST element**. See §4. |
| `04_growth_by_session.csv` | *"did they improve from session 1 to 10?"* | One row per session, learner-level means. Your growth table and your line graph. |
| `05_learner_summary.csv` | *"one row per child"* | 40 rows. Use this for any test that treats the **child** as the unit (which is most of them). |
| `06_race_picks.csv` | *"what exactly did they tap?"* | One row per card touched in the race. Raw; usually you want `10_...` instead. |
| `07_summaries.csv` | *"the sentences the children wrote"* | Every summary sentence, verbatim, with **empty `rubric_score` and `coder_notes` columns for you to fill in.** This is the file you hand to your rubric coders. |
| `08_data_quality.csv` | *"is anything wrong with my data?"* | Every problem found, with learner ids. **Read this before you analyse anything.** |
| `09_story_alignment_audit.csv` | *"can I compare Reader and Race slot by slot?"* | All 150 page-question/SWBST-slot pairs side by side. See §5. |
| `10_distractor_frequency.csv` | *"which wrong idea did they have?"* | **Which specific wrong card** learners chose at each slot. The qualitative half of your results. See §4.3. |
| `00_report.txt` | *"what did the script actually do?"* | The full printed report. Methods appendix material. |
| `00_bad_rows.csv` | only if something failed | Lines that could not be read, and why. |

All CSVs open by double-click in Excel, including names with accents.

---

## 4. The measures that matter

### 4.1 The support-removal contrast — this is the point of the whole app

The learner meets the **same five SWBST slots twice**, with the support taken away:

| Phase | Support | Column |
|---|---|---|
| **Read** | story text on screen | `read_acc` — can they find the slot *with* the text |
| **Race** | text gone, memory only | `race_acc` — can they find it *without* it |

`03_swbst_contrast.csv` gives you, for each of the five slots:

| Column | Meaning |
|---|---|
| `read_acc` | proportion correct **with** the text on screen |
| `race_acc` | proportion correct **without** it (a missed gate counts as a failure) |
| `race_acc_excl_missed` | ... counting only gates where the child actually chose a card |
| `drop_read_minus_race` | `read_acc − race_acc` — **the support-removal cost** |
| `race_wrong_rate` | chose a distractor — a **comprehension** error |
| `race_missed_rate` | touched no card at all — an **attention/motor** event, not a comprehension error |
| `paired_t`, `paired_p`, `paired_cohen_dz` | a paired comparison, as a manipulation check |

The `grouping` column splits the same table by session and by difficulty, so you can
say "the drop shrank from session 1 to session 10" — which is the learning claim.

> **`race_acc` vs `race_acc_excl_missed`.** A child who drove past a gate without
> touching anything did not tell you they don't know the answer — they told you they
> missed the gate. `race_acc` counts that as a failure; `race_acc_excl_missed` leaves
> it out. **Report both.** If the report says missed gates are above 15%, the race is
> partly measuring dexterity and you must say so in your limitations.

> **Statistics warning.** The paired test in this file uses one pair per *run*, and
> each child contributes many runs, so it is a **descriptive manipulation check, not
> your inferential test**. For a proper test use `05_learner_summary.csv`, which has
> one row per child, and run your paired t-test / Wilcoxon there on `read_acc_mean`
> vs `race_acc_mean`.

### 4.2 The other measures

| What | Where |
|---|---|
| Stars (1–3) | `starsEarned` in `01_runs.csv`; `stars_mean` in the summaries |
| Arrange assisted rate | `arrange_unaided` (1 = ordered it themselves), `arrangeAssisted` (1 = the app placed the pieces for them) |
| Summary length | `summary_words`, `summary_chars` |
| Time on task per phase | `readingSeconds`, `raceSeconds`, `arrangeSeconds`, `summarySeconds` (and `totalSeconds`) |

### 4.3 Which wrong idea did they have? — `10_distractor_frequency.csv`

Knowing a child was wrong at **But** is a number. Knowing they thought the But was
*"the swing chain snapped in half"* is a finding. The two wrong cards at each slot
are written to fail in different ways, so this table is where the misconceptions are.

Rows with `grouping = story` name the exact card:

| Column | Meaning |
|---|---|
| `card_text` | the actual words on the card they chose |
| `option_kind` | `correct`, `distractor_1` or `distractor_2` |
| `n_first_picks` | how many children took it on their **first** try at that gate |
| `pct_of_first_picks_at_slot` | what share of first picks at that slot it took |
| `n_learners` | how many different children |

Rows with `grouping = slot_overall` add `n_early_1to5` / `n_late_6to10`, so you can
show a wrong idea fading across the study.

**Sort by `n_first_picks` and read the top ten cards.** A distractor that pulled in
many children is either a real misconception worth discussing, or a badly written
option worth reporting as a limitation. Only reading the card tells you which — the
numbers cannot.

Re-presented gold cards are excluded: after a wrong pick the app hands the child the
correct answer, and taking it is not a choice.

### 4.4 A validity check the script runs for you

The three answer cards are placed in randomly shuffled lanes, so if the race is
really measuring comprehension, children's first picks should land in the left,
centre and right lanes about equally (≈33% each). The data-quality report checks
this and tells you the split. If one lane takes more than 45%, some picks are
steering habit rather than an answer, and you must report it — it inflates or
deflates race accuracy for reasons that have nothing to do with summarising.

---

## 5. One thing you must check before making a per-slot claim

The per-slot Reader→Race comparison rests on an assumption the app **does not
enforce**: that the Reader's question on page 1 is about *Somebody*, page 2 about
*Wanted*, and so on. That is a convention in the story files, not a rule in the code.

Run with `--stories` and open `09_story_alignment_audit.csv`. Each row puts a page
question next to the SWBST slot it is supposed to match, with an empty
`aligned_yes_no` column. Go through it with your adviser and mark each one.

**It does not hold everywhere.** For example in `s04_easy` the page-3 question is
*"What animals did Omar enjoy watching?"* (answer: giraffes and monkeys) while SWBST
slot 3 is **But** = *"They had too much to see in only one day"*. Those are different
questions, so for that story a slot-3 Reader-vs-Race comparison compares nothing.
Drop the misaligned stories from the per-slot analysis, or report the per-slot
contrast only for the stories that pass.

**The whole-story contrast (`read_acc` vs `race_acc` averaged over all five slots)
does not depend on this** and is safe to report for everything.

---

## 6. What the script does to your data, and what it never does

### Never
- It **never changes, moves or deletes anything in the export folder.** Files are
  opened read-only, and it refuses to run if `--out` is the same folder as `--in`.
- It **never silently drops a row.** A line it cannot read stops the whole run.
- It **never fills a missing value with 0.** See §7.

### Always — deduplication
The app writes a **safety snapshot** whenever it is backgrounded (a notification, the
home button), so one play-through can appear as several lines. And if you export the
same tablet twice, the second file repeats everything in the first.

Both are handled by the same rule, from the app's own source:

> group by `runId`; keep the row with `isPartial = false` if there is one, otherwise
> the row with the latest `rowWrittenIso`.

The report tells you how many lines were folded away. **Exporting twice is safe.**

### Always — which runs the analysis tables use
`01_runs.csv` contains **everything**. The other tables use only runs that were:

- **completed** — reached the Results screen. An abandoned run has an empty
  `finishedIso`; it is kept in `01_runs.csv` but excluded from the analysis.
- **first attempts** — `isReplay = 0`. A replay of a story the child already
  finished is practice, not a measurement.
- **within the time cap** — 30 minutes by default. Durations include interruptions.

Change any of that with `--include-replays`, `--include-time-outliers`,
`--time-cap 2400`. Or ignore all of it and filter `01_runs.csv` yourself — the
`completed`, `isReplay` and `time_outlier` columns are right there.

---

## 7. Traps — read this before you quote any number

1. **A blank cell means "not recorded", NOT zero.** If a tablet was never updated it
   keeps writing an older row shape, and older rows lack the newer fields. Those
   cells are left **empty**, never filled with 0, because averaging a 0 that was
   never measured would put an invented number in your thesis. The `*_captured`
   columns in `01_runs.csv` (`picks_captured`, `pause_captured`,
   `race_outcome_captured`, `phase_clocks_captured`, `read_pages_captured`) say
   exactly which rows carry which measure, and the data-quality report counts them.
   **If a measure has blanks, report its n separately.**

2. **`timesCaught` is always 0 and always will be.** The patrol is designed never to
   catch the learner — the game must never punish. It is a constant, not a measure.
   Do not put it in a model.

3. **`starsEarned` is not a performance score.** It is computed from race first-picks
   alone (5/5 = 3★, 4/5 = 2★, otherwise 1★) and ignores reading, arranging and the
   summary entirely. It carries no information the race columns do not already have.
   It is a reward for the child.

4. **Durations include interruptions.** There is no idle detection. If a child put
   the tablet down for ten minutes, that is inside `totalSeconds`. `time_outlier`
   flags the long ones; `racePausedSeconds` is only the part they deliberately
   paused. Treat long values as interruptions, not as effort.

5. **`nudgeCount` is not a summary quality score.** The app never grades the
   sentence — your paper rubric does. It just counts how many gentle prompts appeared.

6. **A "missed" gate is not a wrong answer.** `raceFirstOutcome` separates `correct`
   / `wrong` / `missed`; the older `raceFirstPickCorrect` collapses the last two into
   the same `false`. Always use the outcome columns. (If you see the outcome
   `notcorrect`, that row came from a build too old to tell the difference.)

7. **One row is one story, not one classroom session.** A session is three stories.
   `02_learner_session.csv` has already aggregated it for you.

8. **These logs are process data and a manipulation check.** Your outcome measure is
   the paper pretest/posttest scored with the Summary Writing Rubric. These tables
   show that the children actually did the SWBST work, how much, and how it changed
   — they do not replace the rubric.

---

## 8. Privacy

- Nothing ever leaves the tablet by itself. There is no networking in the app.
  Export is a deliberate teacher action behind a PIN.
- The `.jsonl` files carry **no names**, only `learnerId`. The roster is the only
  link back to a child. **Keep the roster files separate from anything you share,
  and destroy them once you no longer need the linkage.**
- `deviceId` identifies a *tablet*, not a person, and is a one-way hash.
- **`summaryText` is free text written by a child and may contain a real name.**
  Treat `07_summaries.csv` as identifiable until a human has read it.

---

## 9. Worked example

You have finished the study. 40 tablets are on your desk.

**Step 1** — Export from each tablet, copy both files per tablet into
`C:\thesis\exports`. Duplicate that folder to a USB stick and put it in a drawer.

**Step 2** — Run:

```
python summarace_analyze.py --in C:\thesis\exports --out C:\thesis\analysis --stories ..\..\Assets\_Game\Resources\Stories
```

**Step 3** — Read the printed report top to bottom. It looks like this:

```
  log lines in      : 1513
  play-throughs out : 1226
  duplicate/snapshot lines folded away: 287

  ANALYSED (completed, first attempts only, within the time cap) : 1149

  slot        read_acc  race_acc  race_excl_missed   drop   wrong  missed
  SOMEBODY     0.7581    0.5735            0.6205  0.1845     403      87
  WANTED       0.7598    0.6110            0.6673  0.1488     350      97
  BUT          0.7607    0.5857            0.6361  0.1749     385      91
  SO           0.7563    0.6092            0.6667  0.1471     350      99
  THEN         0.7502    0.5814            0.6278  0.1688     396      85

  Handling noise: 8.0% of race gates were MISSED (no card touched).
```

Read that as: with the text on screen children found the right SWBST part about 76%
of the time; from memory, about 59%. The gap of roughly 17 points is the cost of
removing the support, and it is similar across all five slots. 8% missed is
acceptable — below the 15% line where the race would be measuring dexterity.

**Step 4** — Open `08_data_quality.csv` and deal with anything marked **SERIOUS** or
**CHECK**. In the example run: one profile named "test" that tapped through five
stories in under a minute on a Windows machine — that is you, testing. Note its
`learnerId` and delete those rows from `01_runs.csv` before analysing.

**Step 5** — Open `04_growth_by_session.csv`. Plot `race_acc_mean` against session.
That is your growth figure.

**Step 6** — Open `05_learner_summary.csv` (40 rows, one per child). Run your paired
test on `read_acc_mean` vs `race_acc_mean`, and look at `race_acc_gain` (last three
sessions minus first three) per child.

**Step 7** — Send `07_summaries.csv` to your rubric coders. They fill in
`rubric_score` and `coder_notes`. Nothing else in the file needs to change.

**Step 8** — Open `10_distractor_frequency.csv`, sort by `n_first_picks`, read the
top ten `card_text` values. Those are your qualitative findings — the specific wrong
ideas Grade-4 learners hold about story structure.

---

## 10. Command reference

```
python summarace_analyze.py --in <exports> --out <tables> [options]

  --stories <path>          also write the story alignment audit (09)
  --skip-bad-rows           continue past unreadable lines (they are still listed)
  --include-replays         include repeat plays of a story
  --include-time-outliers   include runs above the time cap
  --time-cap 1800           seconds; above this a run is treated as interrupted
  --min-plausible-seconds 90  a completed run faster than this is flagged as testing
  --study-start 2026-09-01  flag runs before this date (pilot / developer data)
  --study-end   2026-11-30  flag runs after this date
```

```
python make_test_data.py --out <folder> [options]

  --learners 40 --tablets 10   size of the fake cohort
  --corrupt                    add a half-written final line, to rehearse the failure
  --pristine                   skip the deliberate messes and write clean data
  --seed 20260806              change for a different fake cohort
```

---

## 11. For whoever maintains this

`summarace_analyze.py` is written against **`SessionLog` schema version 3** —
`Assets/_Game/Scripts/Data/SaveModels.cs` (the shape) and
`Assets/_Game/Scripts/Core/SessionLogService.cs` (what writes it, and when). Those
two files are the ground truth; `Documentation/SummaRace_Data_Dictionary.md` is the
researcher-facing description of them.

**If a field is ever added to `SessionLog`:** bump `SchemaVersion` in
`SessionLogService.cs`, add the field name to `SCHEMA3_FIELDS` (or a new
`SCHEMA4_FIELDS`) and `SCRIPT_SCHEMA` here, and update the data dictionary — in the
same commit. Fields are only ever appended, never renamed or removed, because
analysis already written against a column has to keep working and a play-through
cannot be repeated.

The script reads field **presence**, not `schemaVersion`, to decide whether a measure
was captured — presence is the fact, the version number is the label — and reports
the version separately so a mid-study build swap is visible in the data rather than
discovered during analysis.
