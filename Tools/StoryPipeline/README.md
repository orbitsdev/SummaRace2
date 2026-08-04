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
python flag.py             # quality gate: must report 0 flagged
```

`worksheet.py` prints an authoring worksheet for whatever `flag.py` flags.

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
