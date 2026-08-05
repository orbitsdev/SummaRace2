# SummaRace page ↔ SWBST slot alignment audit

Audit date **2026-08-06**, branch `experiment/endless-override-2`.
Scope: all **150 page↔slot pairs** across the 30 stories, graded by hand against each page's
own text, plus the researcher's source document
(`Documentation/STORIES FOR SESSION 1-10 … .docx`, extracted to `stories_extracted.txt`).

---

## Why this matters

The instrument is a **support-removal ladder**. The Reader pre-teaches SWBST slot *k* with the
story text on screen; the Race asks slot *k* again with the text gone; `SessionLogService`
records `readingFirstCorrect` and `raceFirstPickCorrect` per page. The thesis analysis wants a
**per-slot contrast** — "how did this learner do on *But* with support versus without support".

**Nothing in the data or the code enforces the pairing.** There is no field linking a page
question to a slot. `StoryData` holds `pages[]` and `elements[]` as two independent arrays;
`ReaderController` walks pages by index and `EndlessRaceDirector` walks elements by index, and
the only thing that makes page 3 "the *But* page" is the convention that both arrays are
five long and in the same order. That convention is what this audit tests.

---

## Headline

| Verdict | Count | Share |
|---|---|---|
| **ALIGNED** — the question really does pre-teach that slot | **135 / 150** | 90.0% |
| **WEAK** — related to the slot but a different proposition | 8 / 150 | 5.3% |
| **MISALIGNED** — asks about something else entirely | **7 / 150** | 4.7% |

**The per-slot contrast can be reported**, with two qualifications:

1. It is sound for **143 of 150 items** as they stood before this pass. The 7 misaligned items
   were concentrated in **5 stories**, and 6 of those 7 are now repaired (see §3).
2. **Alignment is a convention, not a constraint.** No test, loader or gate would notice if a
   future content revision broke it again. §6 recommends the one check that would.

### Worst offenders

| Story | Misaligned | Weak | Note |
|---|---|---|---|
| `s10_easy` "Why Does the Ocean Have Waves?" | **2** (p2 WANTED, p4 SO) | 1 (p5 THEN) | **3 of 5 slots off.** Expository text forced into a narrative frame — `Somebody` is "Ocean waves", so `Wanted` has to be what waves *do*, which no page question asks about |
| `s04_easy` "A Visit to the Zoo" | **2** (p3 BUT, p4 SO) | 0 | Pages 3 and 4 ask about *which animals* and *what the sister did* — pure detail recall against an obstacle slot and an action slot |
| `s03_hard` "The Selfish Giant" | **1** (p2 WANTED) | 1 (p3 BUT) | The one case that **cannot be fixed from the page text** — see sign-off S1 |
| `s08_average` "How to Skateboard" | **1** (p2 WANTED) | 0 | Also expository |
| `s10_hard` "We Also Serve" | **1** (p2 WANTED) | 0 | Page 2 is about Maria Dickin; the SWBST subject is the animals |

Both expository passages (`s08_average`, `s10_easy`) appear here, and both were already noted
as a source-selection issue in `SummaRace_Content_QA_Report.md` §4/P4. The misalignment is a
**consequence** of forcing SWBST onto text that has no protagonist: the `Somebody`/`Wanted`
slots become abstractions ("Ocean waves" / "to move energy across the water") that the page
questions, written as ordinary comprehension checks, never touch.

### Where the misalignment comes from

Checked mechanically: **145 of the 150 page questions are verbatim from the researcher's
source document.** The only five that are not are `s01_easy`'s, which are AI-authored (the
source doc gives Day 1 EASY a passage and a main idea but no 5-page treatment) and already
carry a sign-off flag in CLAUDE.md.

So there is **almost no JSON drift in the questions** — the pipeline reproduced the source
faithfully. Two of the seven misalignments, however, were **our own regressions in the SWBST
line**, introduced by the P7 card-shortening pass, which trimmed for width and changed
meaning:

| Story / slot | Researcher's line | What P7 shipped | Damage |
|---|---|---|---|
| `s04_easy` BUT | "they had many animals to explore" | "They had too much to see in only one day" | Severed the only link page 3 ("giraffes and monkeys") had to the slot |
| `s10_easy` SO | "waves form through wind, storms, **earthquakes**, and gravity" (56 chars, over the 53-char card budget) | "Waves and tides form from wind, storms, and gravity" | Deleted **earthquakes** — the single word page 4 is entirely about — and imported "tides", which is page 5 |

Both are restored below. The remaining five are misaligned **in the researcher's source
document itself** and are listed as sign-off items.

---

## 1. Method

For each of the 150 pairs: read the page text, the page question, the question's correct
answer, and the SWBST `correct` line at the same index.

* **ALIGNED** — the question's correct answer is *the same proposition* as the slot line,
  allowing wording, tense and the `To …` infinitive framing (`"A kitten"` ↔ `"A pet to bring
  home"`, `"Using a metal tray to melt the ice"` ↔ `"They used what Lucy learned in science"`).
  The operational test: *would a learner who answered this question correctly be able to pick
  the right race card for this slot on the strength of that?*
* **WEAK** — same topic, adjacent proposition. Usually the question teaches a *consequence*,
  a *realisation* or a *detail* where the slot holds an *action*, or vice versa.
* **MISALIGNED** — a different proposition with a different subject or predicate. Answering
  the question correctly gives the learner nothing for the slot.

Grading was done by hand rather than by keyword. As a cross-check against my own bias, all 150
pairs were also ranked by content-word Jaccard overlap between the question's answer and the
slot line, and every zero-overlap pair was re-read.

**Overlap is a search tool, not a verdict, and the numbers say why.** There are 20 zero-overlap
pairs. Twelve of them are perfectly aligned — `"A kitten"` ↔ `"A pet to bring home"`,
`"Storm surges"` ↔ `"Storms and earthquakes can create dangerous waves"`,
`"Cooking stew with his mother"` ↔ `"Grandfather shared memories from his childhood"` — so a
lexical rule would have raised twelve false alarms. And it misses in the other direction too:
two of the seven misaligned pairs (`s08_average` p2, `s03_hard` p2) share content words with
their slot while meaning something different, and two of the *highest*-overlap pairs in the
whole corpus (`s01_average` p4, `s03_hard` p3) are among the weak ones. What the scan does
guarantee is that no candidate went unexamined.

---

## 2. Totals

```
ALIGNED     135 / 150   (90.0%)
WEAK          8 / 150   ( 5.3%)
MISALIGNED    7 / 150   ( 4.7%)
```

By slot (misaligned + weak):

| Slot | Misaligned | Weak |
|---|---|---|
| SOMEBODY | 0 | 1 |
| WANTED | **4** | 1 |
| BUT | 1 | 3 |
| SO | 2 | 3 |
| THEN | 0 | 1 |

**`Wanted` is the weak slot of the framework**, and for a consistent reason: a page question
naturally asks *what happened on this page*, while `Wanted` is a story-level goal that a
single page rarely restates. All four misaligned `Wanted` items sit on page 2, where the
passage has moved on to the first event but the slot still points at the protagonist's aim.
`Somebody` and `Then` never misalign, because "who is this about" and "what happened at the
end" are the two questions that cannot drift.

---

## 3. What changed

All content edits went through `Tools/StoryPipeline/overrides.json` + `apply.py`. **No story
JSON was hand-edited.** Diff against `HEAD`: **5 files, 33 changed lines, 0 unintended
changes** — no page text, narration path, hero image, `world`, `mission` or `correctIndex`
differs, and no SWBST `correct` line was bent to match a question.

`apply.py` gained a `questions[].text` field (it could previously repair a question's wrong
options but not the question itself). Every use of that field is a sign-off item below.

### Category A — restore the researcher's own line (our regression, no sign-off needed)

| Story / slot | New value | Rationale |
|---|---|---|
| `s04_easy` BUT `correct` | "They had many animals to explore" | Restores the source doc verbatim (sentence-cased). Distractors rebalanced to hold `flag.py`'s ±6 margin: "They lost their zoo map at the gate" / "They saw only the lions that day" |
| `s10_easy` SO `correct` | "Wind, storms, earthquakes, and gravity form waves" | Re-trim of the source line that keeps **all four** forces inside the 53-char card budget (49 chars, 7 words) instead of dropping earthquakes. Distractors reshaped to match: "Rain, clouds, snow, and moonlight form waves" / "Boats, birds, and fishing nets form waves" |

### Category B — rewrite the page QUESTION toward its slot (**researcher sign-off required**)

The SWBST line is never touched: it is the researcher's framework and the summary target. The
question is the thing bent toward the slot. Each rewrite stays faithful to text the learner has
already read.

| # | Story / page → slot | Was (researcher's own question / answer) | Now | Faithful to |
|---|---|---|---|---|
| B2 | `s04_easy` p3 → BUT | "What animals did Omar enjoy watching?" → "Giraffes and monkeys" | "What did Omar and his family find at the zoo?" → "Many animals to explore" (vs "One empty animal cage" / "A playground with swings") | p3 shows the *next* set of animals after p2's lions; "many to explore" is what pages 1–3 demonstrate |
| B3 | `s04_easy` p4 → SO | "What activity did Omar's sister enjoy?" → "Feeding the ducks" | "How did Omar's family see so much of the zoo?" → "They visited one exhibit after another" (vs "They stayed near the front gate" / "They watched only the lions rest") | p4 adds the duck pond to p2's lions and p3's giraffes/monkeys — the family moving exhibit to exhibit |
| B4 | `s08_average` p2 → WANTED | "What does a beginner need before skateboarding?" → "A skateboard and protective gear" | "Why does a beginner need a board and safety gear?" → "To learn how to skateboard safely" (vs "To ride a motorcycle on the highway" / "To surf on the big ocean waves") | p2 lists what is needed; the rewrite asks what it is needed *for*, which is p1's stated goal |
| B5 | `s10_easy` p2 → WANTED | "What is the most common cause of waves?" → "Wind" | "What do the waves made by the wind carry?" → "Energy across the water" (vs "Fish from the deep sea" / "Boats from the harbor") | p2 (wind makes waves) + p1 (waves carry energy through the water) |
| B6 | `s10_easy` p4 → SO | "What can create a tsunami?" → "Underwater disturbances like earthquakes" | "Besides wind and storms, what else can make waves?" → "Earthquakes and volcanic eruptions" (vs "Only the wind blowing across the surface" / "Thick storm clouds high above the water") | p4 verbatim (earthquakes, landslides, volcanic eruptions), framed as *adding to the list* — which is what the SO slot is |
| B7 | `s10_hard` p2 → WANTED | "What did Maria Dickin want?" → "To honor brave animals" | "Why did Maria Dickin want to honor the animals?" → "They helped and protected people" (vs "They performed tricks for crowds" / "They were kept as pets in homes") | p2 verbatim ("wanted to honor animals that showed courage while helping people"), redirected onto the animals' motive, which is the SWBST subject |

### Category C — hand-authored `s01_easy` (not the researcher's content, but **called out**)

`s01_*` are excluded from `emit.py` generation as the hand-checked gold standard, so this is
the one file where an override edits a file the pipeline does not regenerate. `apply.py`
rewrites files on disk by name, so the change is still reproducible from `overrides.json` — it
does **not** need re-doing by hand — but it is recorded here because the CLAUDE.md rule says
any `s01_*` change must be called out.

| Story / page | Was | Now | Why |
|---|---|---|---|
| `s01_easy` p1 → SOMEBODY | "Who is the story about?" → "Molly and her friend Bella" (vs "The teacher" / "A boy named Duncan") | "Who is the story mainly about?" → "Molly" (vs "The teacher" / "Duncan") | The slot is `Molly`; the Reader was teaching a wider referent than the race then asks for. Already noted in `SummaRace_Content_QA_Report.md` §4/P4. Third option shortened so the correct one is not conspicuously the shortest |

---

## 4. Items needing the researcher's sign-off

**S1 — `s03_hard` "The Selfish Giant" p2 → WANTED. NOT FIXED; needs a decision.**

* Slot: `Wanted – to keep the garden only for himself` (the Giant's want).
* Page 2 text, verbatim: *"The garden had green grass, flowers, fruit trees, and singing birds."*
* Question: *"What did the children enjoy?"* → *"Playing in the beautiful garden"*.

The page carries **no statement of the Giant's want at all** — it is a description of the
garden, and the Giant's possessiveness only appears on page 4. No question faithful to page 2
can pre-teach this slot, so I did not invent one. Two options for the researcher:

* **(a)** move a clause onto page 2 — e.g. *"The garden had green grass, flowers, fruit trees,
  and singing birds. The Giant wanted it all to himself."* — and then ask *"What did the Giant
  want?"*; or
* **(b)** accept that `Wanted` is taught on page 4 rather than page 2 for this story, and
  record it as a known exception in the analysis.

Recommendation: **(a)**. It is one clause, it matches the shortened passage's own framing, and
`s03_hard` is the only story where the ladder has a genuine hole.

**S2 — the five source-document rewrites (B2–B7 above, six items across five pages).**

These change questions the researcher wrote. Each was rewritten toward the slot and stays
inside text the learner has read, but the researcher should read the "Was → Now" column and
confirm. The most debatable is **B3** (`s04_easy` p4), which asks about the family's route
through the zoo while page 4 talks only about the sister and the ducks; it is grounded across
pages 2–4 rather than on page 4 alone.

**S3 — `s03_hard` BUT is itself not an obstacle.** `But – the children loved playing there` is
correct from the Giant's point of view but sits beside two obstacle-shaped distractors, so a
learner applying "But = the problem" can reason *past* the right answer. Already recorded as a
P4 note in the content QA report; repeated here because it is also why `s03_hard` p3 grades
WEAK. Keep verbatim; worth a line in the session-3 script.

**S4 — the two expository passages.** `s08_average` ("How to Skateboard") and `s10_easy` ("Why
Does the Ocean Have Waves?") have no protagonist, so `Somebody`/`Wanted` become abstractions
and are where 3 of the 7 misalignments landed. Not fixable by editing questions — it is a
source-selection question. If either is replaced with a narrative passage before the study,
re-run this audit for that story.

**S5 — `s01_easy` remains an open sign-off item** for its AI-authored page split, questions and
distractors, unchanged by this pass except for C1 above. See CLAUDE.md's known flag.

---

## 5. Weak items left as authored (no change made)

Each of these still pre-teaches its slot well enough for a learner to pick the right race card;
the mismatch is one of precision, not of subject. They are listed so the researcher can decide,
not because they block the analysis.

| Item | Verdict | Why it is not exact | Action |
|---|---|---|---|
| `s01_average` p4 SO | WEAK | Question teaches what Duncan *learned* (the crayons had different complaints); the slot is what he *did* (listened to them) | left as authored |
| `s02_hard` p4 SO | WEAK | Question teaches Shawn's *realisation*; the slot is his *action* (moved ditch, later helped). The page does state the move | left as authored |
| `s03_average` p3 BUT | WEAK | Answer ("His lunchbox was missing") is page 1's fact and is really the *Wanted*'s premise; the slot is "he could not find it **by himself**" | left as authored |
| `s03_hard` p3 BUT | WEAK | Question teaches the Giant's anger; the slot is "the children loved playing there" (see S3) | left as authored |
| `s04_average` p2 WANTED | WEAK | Answer is one specific wish; the slot is the general want ("good luck and for her wishes to come true"). Also flagged as weakly grounded in the content QA report §3.6 | left as authored |
| `s09_hard` p4 SO | WEAK | Answer names working with the hedgehog to *find* the flower; the slot is *telling* Baba Yaga where it grew. Both are in page 4's single sentence | left as authored |
| `s10_easy` p5 THEN | WEAK | Answer names the forces; the slot is the ocean's constant motion. Adjacent clauses of the same page-5 sentence | left as authored |
| `s01_easy` p1 SOMEBODY | WEAK | Answer named two people; the slot is Molly alone | **fixed — C1** |

---

## 6. Gates re-run after the change

Everything below was run against the tree as it stands now.

| Gate | Before | After |
|---|---|---|
| `flag.py` (register / subject / length tells, card budget) | 0 of 150 flagged | **0 of 150 flagged** |
| `fit.py` race answer card | 0 / 450 fail | **0 / 450** |
| `fit.py` Reader option | 0 / 450 fail | **0 / 450** |
| `fit.py` Reader question (7 texts rewritten, some longer) | 0 / 150 fail | **0 / 150** |
| `fit.py` all other surfaces | 0 fail | **0 fail** (the pre-existing `SOMEBODY` tracker overflow is a code issue, unchanged — QA report §5/P1) |
| `qa.py` structure | 30 / 30 pass | **30 / 30 pass** |
| `qa.py` "tap the widest card" | 50.8% (null 33.3%) | **50.8%** |
| `qa.py` `correct` strictly the widest | 45.3% | **45.3%** |
| `qa.py` mean character margin | −0.1 | **−0.1** |
| `qa.py` margins breaching ±6 | 4 | **4** (the same four, all on the harmless short side) |
| `qa.py` "tap the longest **Reader** option" | 32.0% | **31.0%** |
| Race distractors reused verbatim from the Reader | 16.7% overall / 2.5% non-character | **16.7% / 1.3%** |
| Race distractors that are a Reader-taught correct answer | 0 | **0** |

The EditMode fixture's own thresholds were re-checked item by item against the new content
(`RaceCardValidityTests`: ±6 longest/narrowest, ±10 from the distractor mean, ≤58% width
heuristic, ≤55% strictly-widest, |mean margin| ≤ 3, ≤53 chars, ≤12 words, infinitive and
subject shape; `DistractorReuseTests`: ≤30% overall, ≤8% non-character, 0 contradictions;
`StoryContentTests`/`AnswerPositionTests`: structure and `correctIndex` range). **All hold, and
non-character reuse improved from 2.5% to 1.3%.** The suite itself was not executed here — the
Unity Editor is held by another agent this session — so the owner should run it once before the
next milestone.

### The check that does not exist

No gate tests **alignment**. `flag.py`, `fit.py` and `qa.py` all measure *within* an element
set or *within* a question; nothing compares a page question to its slot, which is why seven
items sat misaligned through F44, the P7 trim, the content QA pass and 45 EditMode tests.
A cheap approximation worth adding to `qa.py`: for each pair, compute content-word overlap
between the question's correct answer and the slot's `correct`, and print the bottom 20 for a
human to read. It cannot decide anything — in this corpus 12 of the 20 zero-overlap pairs are
perfectly fine, and two misaligned pairs are *not* zero-overlap — but it would put the
candidates in front of whoever next revises content, instead of requiring someone to think of
looking.

---

## 7. The 150 rows

Values shown are the corpus **as audited** (`HEAD`, before this pass's edits). Rows repaired in
§3 are the ones marked MISALIGNED, plus `s01_easy` p1.

| # | Story | Pg | Slot | Page question | Question's correct answer | SWBST element `correct` | Verdict |
|---|---|---|---|---|---|---|---|
| 1 | `s01_easy` | 1 | SOMEBODY | Who is the story about? | Molly and her friend Bella | Molly | WEAK |
| 2 | `s01_easy` | 2 | WANTED | What did Molly want? | A turn on the swing | She wanted a turn on the swing | aligned |
| 3 | `s01_easy` | 3 | BUT | What was Molly's problem? | Bella would not give her a turn | Bella would not get off the swing | aligned |
| 4 | `s01_easy` | 4 | SO | What did Molly decide to do? | Use an "I message" to share her feelings | Molly used an "I message" to tell Bella how she felt | aligned |
| 5 | `s01_easy` | 5 | THEN | How did the story end? | Bella got off and Molly had her turn | Bella got off and Molly happily took her turn | aligned |
| 6 | `s01_average` | 1 | SOMEBODY | Who is the main character in the story? | Duncan | Duncan | aligned |
| 7 | `s01_average` | 2 | WANTED | What did Duncan want to do? | Color with his crayons | To color with his crayons | aligned |
| 8 | `s01_average` | 3 | BUT | What problem did the crayons have? | They were unhappy with their jobs | The crayons quit because they were unhappy | aligned |
| 9 | `s01_average` | 4 | SO | What did Duncan learn from the letters? | The crayons had different complaints | Duncan listened to their complaints | WEAK |
| 10 | `s01_average` | 5 | THEN | What happened at the end? | Duncan tried to help the crayons so they could color again | He tried to make them happy so they could color again | aligned |
| 11 | `s01_hard` | 1 | SOMEBODY | Who is the main character in the story? | Jayden | Jayden | aligned |
| 12 | `s01_hard` | 2 | WANTED | What did Jayden need to do? | Pick an animal for his assignment | To choose an animal for his assignment | aligned |
| 13 | `s01_hard` | 3 | BUT | What problem did Jayden face? | He could not decide which animal to choose | Every animal he researched had disadvantages | aligned |
| 14 | `s01_hard` | 4 | SO | What did Jayden do to solve his problem? | He kept researching and thinking | He kept researching and thinking carefully | aligned |
| 15 | `s01_hard` | 5 | THEN | What happened in the end? | Jayden chose to be a human | He chose to stay human | aligned |
| 16 | `s02_easy` | 1 | SOMEBODY | Who are the main characters? | Maggie, Travis, and Lucy | Maggie, Travis, and Lucy | aligned |
| 17 | `s02_easy` | 2 | WANTED | What did the friends want to do? | Escape the room | To escape the room | aligned |
| 18 | `s02_easy` | 3 | BUT | What problem did the friends face? | The door was locked and the key was stuck in ice | The key was trapped inside ice | aligned |
| 19 | `s02_easy` | 4 | SO | What did Lucy suggest? | Using a metal tray to melt the ice | They used what Lucy learned in science | aligned |
| 20 | `s02_easy` | 5 | THEN | What happened at the end? | They escaped the room | The ice melted and they escaped | aligned |
| 21 | `s02_average` | 1 | SOMEBODY | Who is the main character? | Mateo | Mateo | aligned |
| 22 | `s02_average` | 2 | WANTED | What did Mateo want? | A kitten | A pet to bring home | aligned |
| 23 | `s02_average` | 3 | BUT | What problem did Mateo face? | Santiago needed extra care | Santiago was old and needed extra care | aligned |
| 24 | `s02_average` | 4 | SO | What did Mateo do? | He thought about Santiago's needs | Mateo spent time with him and thought carefully | aligned |
| 25 | `s02_average` | 5 | THEN | What happened in the end? | Mateo chose Santiago | He chose Santiago as his friend | aligned |
| 26 | `s02_hard` | 1 | SOMEBODY | Who is the main character in the story? | Shawn | Shawn | aligned |
| 27 | `s02_hard` | 2 | WANTED | What did Shawn want? | To get the food and water first | To get all the food and water first | aligned |
| 28 | `s02_hard` | 3 | BUT | What problem did Shawn cause? | He made the other snails hungry by being selfish | His selfishness left his friends hungry and upset | aligned |
| 29 | `s02_hard` | 4 | SO | What did Shawn realize? | His actions had hurt his friends | He moved to a new ditch and later helped his friends | WEAK |
| 30 | `s02_hard` | 5 | THEN | What happened in the end? | Shawn helped his friends and changed his behavior | He used his speed to help others instead of himself | aligned |
| 31 | `s03_easy` | 1 | SOMEBODY | Who is one of the main characters? | Emma | Emma and Josh | aligned |
| 32 | `s03_easy` | 2 | WANTED | What did Emma and Josh want? | To eat at their favorite restaurant | To eat at their favorite restaurant | aligned |
| 33 | `s03_easy` | 3 | BUT | What problem did Emma and Josh face? | They had to try a new restaurant they were unsure about | Their father wanted to try a new restaurant | aligned |
| 34 | `s03_easy` | 4 | SO | What did Emma and Josh do? | They listened to their parents and agreed to try it | They agreed to give the new restaurant a chance | aligned |
| 35 | `s03_easy` | 5 | THEN | What happened in the end? | They decided to try the new restaurant | They became willing to try something new | aligned |
| 36 | `s03_average` | 1 | SOMEBODY | Who is the main character? | Timmy | Timmy | aligned |
| 37 | `s03_average` | 2 | WANTED | What did Timmy want? | To find his missing lunchbox | To find his missing lunchbox | aligned |
| 38 | `s03_average` | 3 | BUT | What problem did Timmy face? | His lunchbox was missing | He could not find it by himself | WEAK |
| 39 | `s03_average` | 4 | SO | What did Timmy and his friends do? | They searched for clues and looked outside | His friends helped him search | aligned |
| 40 | `s03_average` | 5 | THEN | What happened in the end? | Timmy found his lunchbox with his friends' help | They found the lunchbox near the swings | aligned |
| 41 | `s03_hard` | 1 | SOMEBODY | Who is one of the main characters? | The Giant | The Giant | aligned |
| 42 | `s03_hard` | 2 | WANTED | What did the children enjoy? | Playing in the beautiful garden | To keep the garden only for himself | **MISALIGNED** |
| 43 | `s03_hard` | 3 | BUT | What problem began in the story? | The Giant became angry when he saw the children | The children loved playing there | WEAK |
| 44 | `s03_hard` | 4 | SO | What did the Giant do? | He chased the children away and built a wall | He chased them away and built a wall | aligned |
| 45 | `s03_hard` | 5 | THEN | What happened in the end? | The children could no longer play in the garden | The children lost their favorite place to play | aligned |
| 46 | `s04_easy` | 1 | SOMEBODY | Who is the main character? | Omar | Omar and his family | aligned |
| 47 | `s04_easy` | 2 | WANTED | What did Omar's family want to do? | Visit the zoo and see animals | To enjoy a day at the zoo | aligned |
| 48 | `s04_easy` | 3 | BUT | What animals did Omar enjoy watching? | Giraffes and monkeys | They had too much to see in only one day | **MISALIGNED** |
| 49 | `s04_easy` | 4 | SO | What activity did Omar's sister enjoy? | Feeding the ducks | They visited different animal exhibits | **MISALIGNED** |
| 50 | `s04_easy` | 5 | THEN | How did Omar feel at the end? | Excited and happy | They had a fun day and made happy memories | aligned |
| 51 | `s04_average` | 1 | SOMEBODY | Who is the main character in the story? | Rylee | Rylee | aligned |
| 52 | `s04_average` | 2 | WANTED | What did Rylee want? | Her classmates to like the necklace | Good luck and for her wishes to come true | WEAK |
| 53 | `s04_average` | 3 | BUT | What problem did Rylee notice? | Her wishes seemed to cause unexpected problems | Her wishes seemed to have unexpected consequences | aligned |
| 54 | `s04_average` | 4 | SO | What did Rylee do to solve her problem? | She asked the museum for information about it | She visited a museum to learn about the necklace | aligned |
| 55 | `s04_average` | 5 | THEN | What happened in the end? | Rylee decided she no longer wanted the necklace | She decided to get rid of the necklace | aligned |
| 56 | `s04_hard` | 1 | SOMEBODY | Who are the main subjects in the story? | Festival visitors | People who visit AgitAgueda and other festivals | aligned |
| 57 | `s04_hard` | 2 | WANTED | What did the visitors want? | To enjoy Portugal's festivals and culture | To enjoy music, art, food, and Portuguese culture | aligned |
| 58 | `s04_hard` | 3 | BUT | What challenge did the visitors face? | They had many festivals and attractions to explore. | There are too many festivals to visit in one trip | aligned |
| 59 | `s04_hard` | 4 | SO | How did the visitors experience Portugal's culture? | By attending festivals and visiting famous attractions | They visited AgitAgueda and its famous landmarks | aligned |
| 60 | `s04_hard` | 5 | THEN | What happened in the end? | The visitors experienced Portugal's festivals and culture. | They discovered Portugal's rich festival traditions | aligned |
| 61 | `s05_easy` | 1 | SOMEBODY | Who are the main characters in the story? | The Rubin family | The Rubin family | aligned |
| 62 | `s05_easy` | 2 | WANTED | What did the Rubin family want? | More space in their home | A less crowded house | aligned |
| 63 | `s05_easy` | 3 | BUT | What problem did the family face? | Their house felt too crowded | Their house felt too small and cramped | aligned |
| 64 | `s05_easy` | 4 | SO | What did the family do to solve the problem? | They followed Reb Solman's advice | They followed Reb Solman's unusual advice | aligned |
| 65 | `s05_easy` | 5 | THEN | What happened in the end? | The house felt spacious and the family was grateful | They realized their house was not crowded after all | aligned |
| 66 | `s05_average` | 1 | SOMEBODY | Who is the main character in the story? | Owen | Owen | aligned |
| 67 | `s05_average` | 2 | WANTED | What did Owen want? | Comfort and friendship | Comfort and friendship | aligned |
| 68 | `s05_average` | 3 | BUT | What problem did Owen face? | Mzee did not want to be his friend at first | He was alone and Mzee avoided him | aligned |
| 69 | `s05_average` | 4 | SO | What did Owen do? | He kept trying to be near Mzee | Owen kept staying close to Mzee | aligned |
| 70 | `s05_average` | 5 | THEN | What happened in the end? | Owen and Mzee became close friends | They became close friends | aligned |
| 71 | `s05_hard` | 1 | SOMEBODY | Who is sharing stories about the past? | Grandfather | Grandfather | aligned |
| 72 | `s05_hard` | 2 | WANTED | What did Grandfather want to do? | Share memories about the past | To share memories of life in the past | aligned |
| 73 | `s05_hard` | 3 | BUT | What challenge did the grandchildren have? | They found it hard to imagine life in the past | His grandchildren found it hard to imagine | aligned |
| 74 | `s05_hard` | 4 | SO | How did Grandfather help them understand? | He shared stories and memories | He showed them pictures and told stories | aligned |
| 75 | `s05_hard` | 5 | THEN | What happened in the end? | The grandchildren appreciated learning about the past | They appreciated how different life used to be | aligned |
| 76 | `s06_easy` | 1 | SOMEBODY | Who are the main characters in the story? | A father and his son | A father and his son | aligned |
| 77 | `s06_easy` | 2 | WANTED | What did the father and son want? | To find safety after the flood | To find safety and rebuild their lives | aligned |
| 78 | `s06_easy` | 3 | BUT | What problem did they face? | Many things seemed to go wrong for them | Many things kept going wrong for them | aligned |
| 79 | `s06_easy` | 4 | SO | What did the father do when problems happened? | He stayed hopeful and kept going | They continued moving forward and stayed hopeful | aligned |
| 80 | `s06_easy` | 5 | THEN | What happened in the end? | They found treasure and realized their luck was good | They learned their bad luck led to good things | aligned |
| 81 | `s06_average` | 1 | SOMEBODY | Who is the main character in the story? | Amelia | Amelia | aligned |
| 82 | `s06_average` | 2 | WANTED | What did Amelia want? | To perform her solo successfully | To perform her tap dance solo successfully | aligned |
| 83 | `s06_average` | 3 | BUT | What problem did Amelia face? | She was nervous about making mistakes | She was nervous about making mistakes on stage | aligned |
| 84 | `s06_average` | 4 | SO | What did Amelia do to solve the problem? | She practiced and continued dancing confidently | She practiced everywhere and stayed confident | aligned |
| 85 | `s06_average` | 5 | THEN | What happened in the end? | Amelia performed successfully and felt proud of herself | She finished the recital and felt proud | aligned |
| 86 | `s06_hard` | 1 | SOMEBODY | Who are the main characters learning about the past? | Sharr and Kaze | Sharr and Kaze | aligned |
| 87 | `s06_hard` | 2 | WANTED | What did Sharr and Kaze want to learn? | What life was like in the past | To learn about life in Grandfather's day | aligned |
| 88 | `s06_hard` | 3 | BUT | What was different about life long ago? | People typed on keyboards and drove cars | Life in the past was nothing like their world | aligned |
| 89 | `s06_hard` | 4 | SO | What memory did Grandfather share? | Cooking stew with his mother | Grandfather shared memories from his childhood | aligned |
| 90 | `s06_hard` | 5 | THEN | What happened in the end? | The children learned to appreciate life in the past | The children appreciated how people lived long ago | aligned |
| 91 | `s07_easy` | 1 | SOMEBODY | Who is the main character in the story? | Hector | Hector | aligned |
| 92 | `s07_easy` | 2 | WANTED | What did Hector want? | To make friends | To make friends at his new school | aligned |
| 93 | `s07_easy` | 3 | BUT | What problem did Hector face? | He did not know anyone at school | He was new and did not know anyone | aligned |
| 94 | `s07_easy` | 4 | SO | What did Hector do to help solve his problem? | He showed his ball skills | He showed his ball tricks during recess | aligned |
| 95 | `s07_easy` | 5 | THEN | What happened in the end? | Hector made new friends through his talent | His classmates admired him and became his friends | aligned |
| 96 | `s07_average` | 1 | SOMEBODY | Who is the main character in the story? | Chef Pierre | Chef Pierre | aligned |
| 97 | `s07_average` | 2 | WANTED | What did Chef Pierre want? | To become the Queen's Chef Royale | To become the Queen's Chef Royale with frog legs | aligned |
| 98 | `s07_average` | 3 | BUT | What problem did Chef Pierre face? | The frog kept escaping | He and his assistants could not catch the frog | aligned |
| 99 | `s07_average` | 4 | SO | What did Chef Pierre do to solve the problem? | He tried to catch the frog himself | Chef Pierre tried to catch the frog himself | aligned |
| 100 | `s07_average` | 5 | THEN | What happened in the end? | Slice and Dice became Chefs, Pierre the Frog Keeper | Slice and Dice became Chefs, Pierre the Frog Keeper | aligned |
| 101 | `s07_hard` | 1 | SOMEBODY | Who is the main character in the story? | Stanley Marks | Stanley Marks | aligned |
| 102 | `s07_hard` | 2 | WANTED | What did Stanley want? | To succeed at his job and support his family | To do well at his job and support his family | aligned |
| 103 | `s07_hard` | 3 | BUT | What problem did Stanley face? | The work was difficult and exhausting. | The work was hard, repetitive, and tiring | aligned |
| 104 | `s07_hard` | 4 | SO | How did Stanley respond to the challenge? | He learned his job and worked hard. | He learned his task and worked hard all day | aligned |
| 105 | `s07_hard` | 5 | THEN | What happened in the end? | Stanley continued working to provide for his family. | He stayed for his family's better future | aligned |
| 106 | `s08_easy` | 1 | SOMEBODY | Who is the main character in the story? | Doris | Doris and her family | aligned |
| 107 | `s08_easy` | 2 | WANTED | What did the family want? | To keep their food cold and fresh | To keep their food fresh and cold | aligned |
| 108 | `s08_easy` | 3 | BUT | What problem did the family face? | Their icebox could not keep food cold enough | The icebox could not keep ice in the hot summer | aligned |
| 109 | `s08_easy` | 4 | SO | What did the family do to solve the problem? | Purchased a new refrigerator | Mama bought a new electric refrigerator | aligned |
| 110 | `s08_easy` | 5 | THEN | What happened in the end? | The food stayed fresh, but Doris felt sorry for Charlie | Food stayed fresh; Doris felt sorry for Charlie | aligned |
| 111 | `s08_average` | 1 | SOMEBODY | Who is the passage about? | A beginner skateboarder | A beginner skateboarder | aligned |
| 112 | `s08_average` | 2 | WANTED | What does a beginner need before skateboarding? | A skateboard and protective gear | To learn how to skateboard | **MISALIGNED** |
| 113 | `s08_average` | 3 | BUT | What challenge does the beginner face? | Learning balance and control | Skateboarding requires balance and practice | aligned |
| 114 | `s08_average` | 4 | SO | What does the beginner do to improve? | Practices basic skateboarding skills | The beginner learned the basic moves step by step | aligned |
| 115 | `s08_average` | 5 | THEN | What happens in the end? | The beginner becomes ready for more advanced tricks | They became ready to learn more advanced tricks | aligned |
| 116 | `s08_hard` | 1 | SOMEBODY | Who is the main character in the story? | Anansi | Anansi | aligned |
| 117 | `s08_hard` | 2 | WANTED | What did Anansi want? | To eat food from all his friends | To eat food from all of his friends without cooking | aligned |
| 118 | `s08_hard` | 3 | BUT | What problem did Anansi face? | All the cook pots were ready at once | All of the cook pots became ready at the same time | aligned |
| 119 | `s08_hard` | 4 | SO | What happened when the friends pulled the webs? | His legs were pulled in different directions | Every web attached to his legs was pulled at once | aligned |
| 120 | `s08_hard` | 5 | THEN | What happened in the end? | Anansi learned a lesson about greed | His legs stretched out, and he learned about greed | aligned |
| 121 | `s09_easy` | 1 | SOMEBODY | Who is the main character? | Cora | Cora | aligned |
| 122 | `s09_easy` | 2 | WANTED | What did Cora want? | A pool to stay cool | A pool to stay cool during the summer | aligned |
| 123 | `s09_easy` | 3 | BUT | What problem did Cora face? | She did not know which company to choose | She needed to decide which company to buy from | aligned |
| 124 | `s09_easy` | 4 | SO | What did Cora do to solve her problem? | She researched and compared companies | She researched different pool companies and reviews | aligned |
| 125 | `s09_easy` | 5 | THEN | What happened in the end? | Cora bought a pool and enjoyed it | She bought a pool and enjoyed using it | aligned |
| 126 | `s09_average` | 1 | SOMEBODY | Who is the main character in the story? | Asma | Asma | aligned |
| 127 | `s09_average` | 2 | WANTED | What did Asma want? | To complete her first Ramadan fast | To complete her first Ramadan fast | aligned |
| 128 | `s09_average` | 3 | BUT | What problem did Asma face? | She became hungry while fasting | She became hungry and could not eat until sunset | aligned |
| 129 | `s09_average` | 4 | SO | What did Asma do to solve the problem? | She continued fasting and followed the Ramadan practices | She kept fasting and followed Ramadan traditions | aligned |
| 130 | `s09_average` | 5 | THEN | What happened in the end? | Asma celebrated Eid al-Fitr and felt proud of herself | She celebrated Eid al-Fitr, proud of her first fast | aligned |
| 131 | `s09_hard` | 1 | SOMEBODY | Who is the main character in the story? | Marusia | Marusia | aligned |
| 132 | `s09_hard` | 2 | WANTED | What did Marusia want? | To escape and return home | To escape Baba Yaga and return home | aligned |
| 133 | `s09_hard` | 3 | BUT | What problem did Marusia face? | She was trapped by Baba Yaga. | Baba Yaga captured her and magically locked the gate | aligned |
| 134 | `s09_hard` | 4 | SO | How did Marusia solve the problem? | She worked with the hedgehog to find the magical flower. | She told Baba Yaga where the magic flower grew | WEAK |
| 135 | `s09_hard` | 5 | THEN | What happened in the end? | Marusia and Dmitri were freed and safely returned home. | Marusia and Dmitri were set free and returned home | aligned |
| 136 | `s10_easy` | 1 | SOMEBODY | What is the topic of the passage? | Ocean waves | Ocean waves | aligned |
| 137 | `s10_easy` | 2 | WANTED | What is the most common cause of waves? | Wind | To move energy across the water | **MISALIGNED** |
| 138 | `s10_easy` | 3 | BUT | What problem can severe weather cause? | Storm surges | Storms and earthquakes can create dangerous waves | aligned |
| 139 | `s10_easy` | 4 | SO | What can create a tsunami? | Underwater disturbances like earthquakes | Waves and tides form from wind, storms, and gravity | **MISALIGNED** |
| 140 | `s10_easy` | 5 | THEN | What happens in the end of the explanation? | Different forces work together to create waves and tides | The ocean remains in constant motion | WEAK |
| 141 | `s10_average` | 1 | SOMEBODY | Who is the main character in the story? | Clara Barton | Clara Barton | aligned |
| 142 | `s10_average` | 2 | WANTED | What did Clara Barton want? | To help people in need | To help people in need | aligned |
| 143 | `s10_average` | 3 | BUT | What problem did Clara face? | Soldiers needed help during the war | Many soldiers and families suffered in the Civil War | aligned |
| 144 | `s10_average` | 4 | SO | What did Clara do to help solve the problem? | She gathered supplies and cared for wounded soldiers | She gathered supplies and cared for soldiers | aligned |
| 145 | `s10_average` | 5 | THEN | What happened in the end? | Clara founded the American Red Cross and became a hero | She founded the American Red Cross | aligned |
| 146 | `s10_hard` | 1 | SOMEBODY | Who are the main subjects of the story? | Brave service animals | Brave animals that served people | aligned |
| 147 | `s10_hard` | 2 | WANTED | What did Maria Dickin want? | To honor brave animals | To help and protect others | **MISALIGNED** |
| 148 | `s10_hard` | 3 | BUT | What challenge did the animals face? | They worked in dangerous situations | They faced danger during wars and emergencies | aligned |
| 149 | `s10_hard` | 4 | SO | How did the animals help people? | By bravely carrying out important tasks | They worked loyally and courageously to save lives | aligned |
| 150 | `s10_hard` | 5 | THEN | What happened in the end? | The animals were honored with the Dickin Medal | They were honored with the Dickin Medal | aligned |
