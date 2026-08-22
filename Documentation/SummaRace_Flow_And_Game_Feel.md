# SummaRace — the flow, the runner, and the game-feel gap

**Written 2026-08-22**, from the full prototype set (`Desktop/summarace/canva_prototype/1..27.png`,
`web_prototype_screenshots/` ×29, all viewed), `promp.txt`, `August 21.docx`, and the build.

Companion documents: `SummaRace_Owner_Playtest_2026-08-21.md` (what shipped against the docx),
and `Desktop/summarace/SummaRace_Game_Definition_and_Evaluation.md` (the study-vs-build audit,
written 2026-08-21 — still accurate except that the patrol is no longer switched off).

---

## 1. The flow — and yes, your reading is correct

> *"choose mission, each number has 3 stories difficulties right, so 10, so total of 30 stories,
> I see"*

Exactly right, and verified from the shipped JSON rather than from the docs:

```
Pick a Mission  →  10 stops = 10 sessions = 10 classroom days (55 min each)
      each stop →  3 stories: EASY → AVERAGE → HARD
                   10 x 3    =  30 distinct stories
```

They are three **different stories**, not three difficulties of one passage — session 1 is
*The Playground* / *The Day the Crayons Quit* / *The Animal Assignment*. Difficulty is the
researcher's own authoring (each day's three passages get longer and more complex); the app adds
exactly one lever of its own, the reading runway per race gate (easy 1.25x, average 1.0x, hard
0.8x), and a cosmetic time-of-day shift.

Two locks, both there for the study rather than for pacing:

* **within a day** — Easy is always open, Average needs Easy done, Hard needs Average;
* **across days** — only the **teacher's PIN** opens the next mission. Learners must not
  self-advance, or every child gets a different dose and the comparison against the control group
  stops meaning anything. That is why a locked stop says *"Your teacher opens the next mission!"* —
  it is never the child's fault.

The whole app, end to end:

```
Boot → Name Entry (once per tablet) → Main Menu → Pick a Mission → Pick a Story
        ↓
   ┌─────────────────── ONE STORY = THE FOUR-STAGE LADDER ───────────────────┐
   │  1 READ     5 pages, text visible, narration optional, 1 question/page  │
   │  2 RACE     text GONE — collect the 5 SWBST parts, 3 lanes, 1 correct   │
   │  3 ARRANGE  put the 5 collected parts in S-W-B-S-T order                │
   │  4 SUMMARY  write ONE sentence from them                               │
   └────────────────────────────────────────────────────────────────────────┘
        ↓
   Results (stars + treasure + your own sentence) → back to the mission map
```

**The ladder is the design's spine.** Each stage removes one support: text visible → text gone →
order only → produce from nothing. That is what the study is measuring, and it is why the four
stages cannot be reordered, merged, or skipped.

What each stage writes to the data row:

| stage | logged | why it matters |
|---|---|---|
| Read | `readingFirstCorrect`, `readingSeconds` | comprehension **with** support |
| **Race** | **`raceFirstPickCorrect`** (= the star count), `racePicks` (which distractor) | comprehension **without** support — **the headline measure** |
| Arrange | `arrangeOrders`, `assisted` | sequence knowledge, and whether we helped |
| Summary | `summaryText` verbatim | the in-app rehearsal of the paper posttest |

---

## 2. The runner — this is the confusing part, and the confusion is legitimate

> *"and then the confusion part is runner?"*

Yes, and it is worth naming precisely, because it is not a bug and it is not you missing
something. Here it is in one line:

> **The race is not a runner game with a quiz bolted on. It is a recall test wearing a runner's
> clothes.**

The running does exactly three jobs:

1. **It takes the text away.** You cannot go back and re-read a page while you are moving. That
   *is* the support removal — Read is the supported rung, Race is the unsupported one. A
   still-screen quiz would let the child scroll back, and the two stages would measure the same
   thing.
2. **It forces a choice.** Three lanes, three answers, and the runner is always in one of them.
   There is no skip and no "I don't know" — so every gate produces a data point.
3. **It makes a test survivable thirty times.** A Grade-4 child will do this on ten separate days.
   A quiz they would dread; a run they will play.

And here is what the running deliberately does **not** do — which is the source of the odd feeling:

| a normal runner has | SummaRace has | why |
|---|---|---|
| a score | nothing | a score implies losing points; D7 forbids punishment |
| a timer | nothing (only "next part in Ns", which counts an *arrival*) | a clock scores reading **speed**, which hits the slow readers the study exists to help |
| obstacles to dodge | none — `SpawnObstacle` is guarded off | the only obstacle is a wrong answer |
| death / game over | none — the patrol's `timesCaught` is 0 in every run ever logged | a re-run would let a child answer the same five gates twice, which destroys the first-pick measure |
| a fail state | none — every child finishes holding all 5 correct parts | Arrange needs all five; and the run must never end before the ladder does |

**So the honest design tension is this:** the runner has no stakes, because stakes would break the
instrument. What is left has to carry the feel *without* threat. Right now that job is done by
speed, the collect flight into the tracker, praise, the wrong-answer surge, stars and treasure —
and it is thinner than a commercial runner, on purpose.

The professional answer to "make it feel more like a game" is therefore **not** to add pressure
back. It is: **escalate the reward, never the threat.** See §4, Tier 1.

---

## 3. Prototype vs build — where the game feel actually differs

Read off the screenshots side by side, not from memory.

### 3a. Composition — and the build is closer than it looks

| | web/canva prototype | build |
|---|---|---|
| runner | small, bottom of frame | large, chase camera behind |
| option cards | large, **middle of the screen**, approaching from the horizon | small in the world, plus a **reading panel in the sky band** |
| reading surface | the whole screen | the sky panel (world cards are unreadable at speed — measured at 0.28–0.50 s of legible time) |

Worth saying plainly: **the sky reading panel is not a departure from your prototype — it is the
closest achievable thing to it.** In canva screen 15 the three cards sit in the upper-middle of the
frame at a readable size; that is exactly where the panel is. The 3D world cards were the departure.

### 3b. Art — this is the real gap, and it is the one you are feeling

| | prototype target (canva 15) | build today |
|---|---|---|
| setting | bright toy playground: slide, benches, picket fence, chunky trees | TrashDash suburb / city alley |
| track | **three coloured lane stripes** (blue / yellow / green) | grey street slabs |
| palette | high-saturation, soft-shaded, kid-toy | muted pastel, flat vertex colour |
| props | playground equipment | houses, warehouses, bins, wires |

That is the gap. It has nothing to do with validity and everything to do with whether a nine-year-old
thinks the game is *for* them. The cause is structural: the race is built on Trash Dash's theme
system, and **neither of its two themes contains a park, a field or a country lane** — so
`bright_park`, `golden_fields` and `autumn_lane` are all dressed streets.

### 3c. The wrong-answer beat

| prototype | build |
|---|---|
| the **whole screen** turns red (sky + road) | amber vignette at the screen edges |
| a tiny **police car** at the horizon | a full 3D cop, now a tail 2.5 m behind (see the playtest doc) |
| nudge line: *"That detail isn't the most important."* | same line, verbatim, already in `GameText.RaceWrongLines` |

The prototype's full-screen red is *stronger* than our edge vignette, and it costs nothing — worth
considering as a Tier-1 item. Note the prototype's patrol is a distant icon, which is why it never
looked broken there: it had no animation to get wrong.

### 3d. Everything else, checked

* **Arrange piece layout** — your note said the choices should wrap like the prototype instead of
  being single-line. Checked in the scene: the five pool pills are **already a 2-column grid** at
  x 0.06–0.485 / 0.515–0.94 over three rows, which is the prototype's layout exactly. What was
  genuinely wrong was the label sizing: autosize off + overflow mode Overflow, so a 53-character
  piece (the content pipeline's cap) would have drawn its third line *outside* the pill and over
  its neighbour. **Fixed this pass** — autosize with a 22 pt floor and clip instead of spill, on
  all ten labels (five pieces, five slots) from one place.
* **"That belongs in the summary!"** — the best sentence in either prototype, because it is the
  only feedback that tells the child *why* they are collecting. It was not in the build.
  **Added this pass** as a race-only praise pool (`GameText.PraiseRaceCollect`), so it cannot leak
  into Arrange, where it would be false, or Results, where it is meaningless.
* **Tagline** — the prototype's *"Read, collect key events, and turn them into one great summary"*
  is already in the build as *"Read! Race! Summarize!"*.
* **`Collect: SOMEBODY` banner** (canva) — the build now says it two ways at once: the tracker
  plaque pulses on the live slot, and the new question line spells it out in a sentence
  (*"Who is this story about?"*).
* **Emoji icons on the option cards** (both prototypes) — deliberately not adopted, and this one is
  load-bearing. An icon is a surface cue: a child can pick the house picture without reading a
  word. Two separate audits measured surface cues in this game at **84.7 %** correct against 33 %
  for guessing, and removing them took two passes. Icons would hand it straight back.
* **Summary timer** (prototype shows a red **14 s**) — not adopted. A countdown on a writing task
  measures panic. The build has no timer there and should not.

---

## 4. What I would actually do, in order

Constraints that shape this: the study is imminent, the Editor cannot compile right now (URP
package split — see the playtest doc §0b), and therefore **nothing can be playtested today**.

### Tier 0 — before anything else

1. **Fix the URP compile.** Nothing below can be verified until the EditMode suite and Build
   Preflight run again. This is the single highest-value action available.
2. **Device playtest of the last two passes** — patrol tail, tracker, question line, wood panels,
   confirm panel, finish dance. Six changes, none of them ever seen running.
3. **Then the study pipeline**: Preflight → APK → on-device smoke test → USB export rehearsal.

### Tier 1 — best game feel per unit of risk (after Tier 0, each needs a playtest)

1. **Coloured lane stripes on the road.** ~40 lines, additive geometry, no art commission. Three
   thin quads per road segment in the canva palette (blue / yellow / green), laid by the existing
   per-segment hook in `EndlessWorldDressing`. This is the **single biggest step** from "grey city
   alley" toward screenshot 15, and it also does real work: it makes the three lanes legible as
   *choices* rather than as road.
2. **Full-screen wrong-answer wash**, replacing the edge vignette — the prototype's red screen. One
   colour and one alpha curve; strictly cheaper than what is there now, and it reads at arm's length
   on a classroom tablet where an edge vignette does not.
3. **Escalate the reward as the tracker fills.** The race is the same interaction five times with no
   rising curve — that flatness is what the prototype's multiplier was clumsily trying to solve. Do
   it with spectacle instead of pressure: each collected part adds a music layer, brightens the
   tracker board, and makes the collect burst bigger, so part 5 lands like a finale. **No timing, no
   scoring, no threat** — those are what the multiplier got wrong.
4. **Park-ify the three park worlds**: greenery density up hard, skyline off, warmer grade. Cheap
   because the greenery system already exists; it will not reach screenshot 15, but it will stop
   `bright_park` looking like an industrial estate.

### Tier 2 — a real v2, not now

* **A genuine park theme** (commissioned art: playground equipment, picket fence, toy trees). The
  only way to actually hit the canva target. Do not start this before the study.
* **Prototype-style camera** — pulled back so the world cards themselves become readable and the
  sky panel can be retired. Attractive, but it means re-deriving the F47 legibility numbers, the
  patrol geometry and the start dolly together. That is a week with playtests, not a tweak.

### Tier 3 — do not do, and the reason on the record

| | why not |
|---|---|
| global race timer | scores reading speed; punishes exactly the strugglers the study is about |
| score multiplier | implies a score, which implies losing points (D7) |
| game over / retry | a re-run lets a child answer the same five gates twice, destroying `raceFirstPickCorrect` |
| auto-fill the summary | the child stops producing; every logged sentence becomes the app's |
| emoji icons on race cards | reinstates the 84.7 % surface-cue exploit |

---

## 5. The one design note I would push hardest

Everything above is tuning. This is the structural one.

**The race's five gates are the same beat five times, and the game never acknowledges that the
child is building something.** They collect SOMEBODY, then WANTED, then BUT — and each one feels
identical to the last. Then Arrange asks them to order five parts they were never shown as a set,
and Summary asks them to write a sentence from parts that never behaved like a sentence.

The tracker at the top of the race is already the fix, half-built: it is the only thing on screen
that shows the summary assembling. Push it much harder —

* when a part lands, **write it into the tracker as a growing sentence**, not just a letter:
  `Molly → wanted a turn on the swing → but…`
* on the finish, **read the assembled five back** as one line before Arrange loads;
* in Arrange, open with **that same line, scrambled** — so the child sees the connection between
  what they collected and what they are ordering.

That costs no validity (it is their own collected answers, after the picks are logged), needs no
art, and it is the difference between four disconnected mini-games and one act of building a
summary. If there is time for exactly one more feature before the study, it is this.
