# Story pipeline

Turns the researcher's Word document into the 30 story JSONs in
`Assets/_Game/Resources/Stories/`. Those JSONs are **generated artifacts** — if the
researcher revises content, re-run this rather than hand-editing 30 files.

Source doc:
`Documentation/STORIES FOR SESSION 1-10 – SHORTENED PASSAGE_SWBST ANALYSIS_…docx`

## Run order

```sh
# from a scratch dir holding the unzipped docx as stories_x/
python extract.py          # docx XML  -> runs.txt   (preserves run boundaries)
python parse.py            # runs.txt  -> parsed.json + anomaly report
python emit.py             # regression: reproduce the hand-checked s01 files
python emit.py --write     # write the 27 generated stories
python apply.py            # apply overrides.json (the editorial pass)
python flag.py             # content gate:  must report 0 flagged
python fit.py              # geometry gate: must report 0 failures
python qa.py               # corpus audit (not a gate -- read the numbers)
```

`extract.py`/`parse.py`/`emit.py` need `parsed.json`, which is a scratch artefact and is not
committed, so **only the last four are runnable straight from a clone**. `apply.py` rewrites
named fields of the JSONs already on disk and is content-idempotent: running it with
unmodified overrides produces a zero-line `git diff`.

`worksheet.py` prints an authoring worksheet for whatever `flag.py` flags.

## The three checkers

| Script | Asks |
|---|---|
| `flag.py` | Is this element set's WORDING a tell? (register, subject form, character budget) |
| `fit.py` | Will this string RENDER? Every string, in the exact rect it lands in at runtime, at the size TMP's auto-sizer would pick |
| `qa.py` | Can the corpus be beaten WITHOUT READING? Strategy scores against a permutation null, plus structure, asset resolution, Reader/race reuse, slot shape, grounding and typography |

`fit.py` and `qa.py` read every constant from the thing that owns it — `EndlessRaceDirector.cs`,
`MainSummaRace.unity`, the UI scenes (via `scenegeom.py`) and the TMP font assets and their
source TTFs (via `tmpfont.py`). Resize a race card or move a Reader anchor and the next run
picks it up instead of going quietly stale.

Two traps `tmpfont.py` exists to avoid, both of which make a naive measurement wrong rather
than merely imprecise:

* the TMP font assets are **dynamic** (`m_AtlasPopulationMode: 1`), so their baked character
  tables hold only glyphs some scene has already used — 69 of ASCII for Fredoka, 44 for
  Nunito, with no `J`, `Z`, `j`, `z`, `-` or `:`. Advances come from the source TTFs and are
  cross-checked against Unity's baked values (they agree to 0.0075 font units at pointSize 90).
* world-space TMP under a **perspective** camera carries TMP's ×0.1 `orthographicMultiplier`,
  so a race card's "font size 2.4" is 0.24 world units per em. Miss it and every race number
  is off by 10x.

`paths.py` resolves the project root (`$SUMMARACE_ROOT`, else the repo this file sits in).
Every script used to carry a hardcoded absolute path from the machine the pipeline was first
written on, so none of them could run where the project actually lives.

## Overriding a Reader question option

`apply.py` addresses options by index and **refuses to overwrite the option at
`correctIndex`** — one digit wrong there silently replaces the correct answer with a
distractor, and the result still validates (three distinct options, index in range); the only
symptom is an unanswerable item. If you really do mean to rewrite a correct option (the P7
card-shortening pass did, twice), add `"allowCorrect": true` beside `"options"`.

## Why it is shaped like this

**Word glues headings to body text**, both across `<w:r>` runs and inside one run, so
`extract.py` keeps run boundaries and `parse.py` splits again on known markers, then walks
the tokens with a small state machine. Content accumulates into whichever field is open,
which is what makes fragmented passages reassemble.

**Day numbers are verified twice** — from the document's own `DAY` headings and from ordinal
block position (blocks run strictly easy → average → hard, three per day). They must agree.

**Three files are never generated.** `s01_easy` has no 5-page treatment in the source doc,
so its JSON is hand-authored. `s01_average` / `s01_hard` are the hand-checked gold standard
for element wording and are what `emit.py` (no args) diffs against as a regression test.

## The mapping

| JSON field | Source |
|---|---|
| `pages[].text`, `pages[].question` | the doc's 5 pages and processing questions, verbatim (reproduces the gold files byte-for-byte) |
| `elements[].correct` | the doc's SWBST Analysis line, sentence-cased |
| `elements[].distractors` | the same page's two wrong options, **edited** — see below |
| `mission` | per-difficulty tuning: speed 5.5 / 6.0 / 6.5, danger 1.5 / 2.0 / 2.5 |
| `world` | one per session day, used by the race to pick its environment |

## Why distractors need `overrides.json`

Mechanically reusing the question's wrong options fails three ways, all of which matter for
a research instrument:

1. **Register tell** — a `To …` correct answer beside bare-verb distractors, or a named
   subject beside pronoun subjects, lets a learner pick the odd one out by *style* instead
   of comprehension.
2. **Length tell** — a long compound correct answer beside two short distractors. Also
   unreadable on a moving race card, so `correct` is trimmed above 12 words.
3. **Slot mismatch** — a page question is not always shaped like its SWBST slot (s04_easy
   asks "What animals did Omar enjoy watching?" for the *But* slot), so the derived
   distractors answer the question while `correct` answers the slot.

`overrides.json` carries the edits. Guiding principle: **keep the researcher's `correct`
line verbatim and bend the distractors to match it**; rewrite `correct` only to trim
over-long cards, never to change meaning. `apply.py` fails loudly if an override does not
match a real story/slot/page. `flag.py` is the gate and must report 0.

**The same three failure modes apply to the Reader's own question options**, and that was
missed for a long time: F44 rebalanced the race cards, which had been *derived from* the
Reader's wrong options, and left the source alone. The correct option was then strictly the
longest of the three in 124 of 150 Reader questions (mean +10.5 characters), so "tap the
longest" scored 81.3% against 33.3% for guessing — on data `SessionLogService` logs as
`readingFirstCorrect`. Rebalanced 2026-08-05 by extending 112 distractors (see
`Documentation/SummaRace_Content_QA_Report.md`). **If Reader content is ever revised, re-run
`qa.py` and check section B, not just `flag.py`.**
