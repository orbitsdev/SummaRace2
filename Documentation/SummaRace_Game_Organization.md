# SummaRace — Official Game Organization

*Updated 2026-08-23. Every number and path below was read from the running game or its data,
not from memory. Companion documents: `SummaRace_Answer_Key.md` (all 30 stories and their
answers), `SummaRace_How_To_Play.md` (player + teacher walkthrough), `summarice.md` (the game
definition and the reasoning behind each locked decision).*

---

## 1 · How the content is organized

```
SummaRace
└── 10 MISSIONS  (= the ten intervention sessions, one per classroom day)
    └── 3 STORIES each  (easy → average → hard)
        └── 5 PAGES per story   → 5 reading questions (one per page)
        └── 5 SWBST ELEMENTS     → 5 race gates, 5 arrange slots, 1 written summary

10 × 3 = 30 stories · 150 reading questions · 150 SWBST elements · 150 narration clips
```

**Mission = session = classroom day.** A mission is opened by the teacher's PIN, never by the
child. Within an open mission the three stories unlock in order: easy is always available,
average opens when easy is finished, hard when average is finished.

**Story ids** are `sNN_<difficulty>` — `s07_hard` is Mission 7, hard. This convention lives in
one place in the code (`Data/StoryIds`), so no screen can disagree about which story is which.

## 2 · What a learner does in one story (the four stages)

| # | Stage | What the learner does | Support | What is recorded |
|---|---|---|---|---|
| 1 | **Read** | 5 pages, narrated; one question per page | Full text visible | First answer per page |
| 2 | **Race** | Collect the 5 SWBST parts at 5 gates | Text gone — recall | **First pick per gate** (headline measure) + which distractor |
| 3 | **Arrange** | Put the 5 parts in S-W-B-S-T order | Hints fade in; assist after 4 tries | Attempts, order submitted, whether assisted |
| 4 | **Summary** | Write ONE sentence in their own words | Reference list only | The sentence verbatim, nudges used |

This is the **support-removal ladder**: each stage takes away one scaffold. The contrast
between stage 1 (text visible) and stage 2 (text gone) on the *same five slots* is the design's
core, and it is why page *n* and element *n* must stay aligned.

Then **Results**: 1–3 stars from the race, the story's main idea, the child's own sentence,
and five SWBST treasure gems showing which parts were right first time.

## 3 · The ten missions

| Mission | Easy | Average | Hard |
|---|---|---|---|
| 1 | The Playground | The Day the Crayons Quit | The Animal Assignment |
| 2–10 | *(see `SummaRace_Answer_Key.md` for every title, main idea and answer)* | | |

Each mission also has its own **race world** (light, weather, buildings) so the ten days feel
like a journey: morning suburbs → bright park → sunset town → blue hour → overcast industrial
→ golden fields → night city → misty morning → autumn lane → starlit finale. Within a mission,
the three difficulties shift the time of day, so all 30 races differ while a day still feels
like one place.

## 4 · Difficulty — what actually changes

| | Easy | Average | Hard |
|---|---|---|---|
| Story text | The researcher's own three variants per day (length, vocabulary) | | |
| Reading time per race gate | ~16 s | ~12 s | ~12 s |
| Everything else | identical | | |

Difficulty does **not** change scoring, patrol behaviour, or the number of gates. The study
prescribes "three levels of increasing difficulty" without defining the mechanism; the content
carries most of it, gate reading-time carries the rest.

## 5 · Screens, in order

```
Boot → (first run only) Name Entry → Main Menu → Pick a Mission → Pick a Story
     → Read → Race → Arrange → Write Summary → Results → back to stories/missions
```

Plus **Teacher** (PIN-gated, reachable from Main Menu): unlock next session, participant codes,
switch learner, music, export logs, erase tablet.

## 6 · What the app records

One row per play-through, stored **only on that tablet**, exported by the teacher as `.jsonl`.
Schema version 7. Key fields:

- Identity: `participantCode` (the join key to the paper booklet), `learnerId`, `storyId`,
  `session`, `difficulty`, `isReplay`
- Reading: `readingFirstChoices`, `readingFirstCorrect`, `readingPageIndices`
- Race: `raceFirstPickCorrect` (**the headline measure**), `raceFirstOutcome`
  (correct/wrong/missed), `racePicks` (which distractor, which lane, when), `raceWrongPicks`,
  `raceSteerCount` (0 on a finished run = the child never steered), `timesCaught` (always 0 by
  design), `raceRunSeconds`, pause counts
- Arrange: `arrangeAttempts`, `arrangeOrders`, `arrangeAssisted`, `arrangeSolved`
- Summary: `summaryText` (verbatim), `nudgeCount`
- Outcome + timing: `starsEarned`, per-stage seconds, backgrounded seconds, `abandonReason`

Full field-by-field definitions: `Documentation/SummaRace_Data_Dictionary.md`.

## 7 · Rules the game will not break

| Rule | Why |
|---|---|
| **No game over, no fail, no retry** | The study licenses only "correct speeds up, wrong slows down"; a retry would destroy the first-pick measure. Locked decision D7. |
| **Max 3 stars** | Locked decision D8. |
| **No timers** | The thesis never operationalizes one; a countdown pressures exactly the struggling readers the study recruited. |
| **No score or multiplier** | Nothing in the study to multiply; the SWBST tracker already shows progress *and* teaches the framework. |
| **No summary auto-fill** | Writing the sentence IS the treatment. |
| **Sessions open only by teacher PIN** | Uniform exposure — the study's internal-validity control. |
| **100% offline** | No network, ads, analytics or purchases anywhere in the build. |
| **The patrol never catches** | It is a mood beat; `timesCaught` is structurally always 0. |

## 8 · Where things live

| What | Where |
|---|---|
| Story content (30 JSON files) | `Assets/_Game/Resources/Stories/` — **generated**; edit via `Tools/StoryPipeline/`, never by hand |
| Narration (150 clips) | `Assets/_Game/Resources/Stories/Narration/` |
| Story art (30 images) | `Assets/_Game/Resources/Stories/Art/` |
| Game rules (every tuning number) | `Assets/_Game/Scripts/Constants/GameRules.cs` |
| Learner-facing text | `Assets/_Game/Scripts/Constants/GameText.cs` |
| Colours + contrast table | `Assets/_Game/Scripts/Constants/Theme.cs` |
| SWBST teaching colours | `Assets/_Game/Scripts/Constants/SwbstPalette.cs` |
| Saved data on device | `profiles.json`, `settings.json`, `logs/<learnerId>.jsonl` |

## 9 · Runners

The child picks a runner — boy or girl — on the Name Entry screen. The choice is stored on the
learner profile (`runnerIndex`) and can be changed later; it affects only who is on screen, and
nothing about the task or the data.

---

*This document describes the shipped organization. For the reasoning behind each decision —
including what was deliberately NOT built and why — see `summarice.md`.*
