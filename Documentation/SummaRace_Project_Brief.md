# SummaRace — Project Brief (START HERE)

**One-page front door to the whole project. Read this first, then dive into the detailed docs.**

---

## In one sentence

SummaRace is an **offline Android reading game** that teaches **Grade 4 learners to summarize stories** — they read a short story, race to collect its story parts, put them in order, and write a one-sentence summary.

---

## Why this project exists (the goal)

It's the tool for a college thesis: *"SummaRace: A Gamified App to Improve Grade 4 Learners' Skills in Summarizing Story Events."* About 80% of the target learners couldn't summarize — they copied sentences instead of finding the main idea. SummaRace teaches summarizing through the **SWBST framework** (Somebody · Wanted · But · So · Then) in a fun, game-like way.

The study puts 40 learners on the app for 10 sessions and compares them to 40 who get traditional teaching, to measure if the game helps. **The app is the experiment; the paper pretest/posttest measures the result.**

---

## What the project is (the game in 6 steps)

One story = one full round. The learner:

1. **Reads** a short story, one page at a time (with optional voice narration).
2. **Answers** a simple question after each page.
3. **Races** down a path collecting the correct story parts (Somebody → Wanted → But → So → Then) while a friendly patrol chases — right answers speed you up, wrong ones slow you down.
4. **Arranges** the collected parts into the correct order.
5. **Writes** one summary sentence using those parts.
6. **Earns stars** and moves to the next story.

This repeats across **30 stories** (10 sessions × Easy/Average/Hard). Everything is **offline**.

---

## The core logic (how it actually works)

- **Content is data, not code.** Each of the 30 stories is a **JSON file** (its pages, questions, the 5 SWBST parts, the main idea). One set of game screens reads any story's JSON and plays it. Add a story = add a JSON file, *not* new code.
- **One reusable engine, many stories.** The Reader, Race, Arrange, Summary, and Results screens are built **once** and reused for all 30 stories. Only the JSON content changes; the race feels different because each story's collectible parts differ.
- **Learning is never punished.** Wrong answers still let the learner continue and always show the correct answer. The "chase" creates excitement but can never make you fail — it's a game of tag, not a test.
- **The app encourages; it doesn't grade.** In-game checks are gentle nudges only. The researchers grade summaries on paper with a rubric — that's the real measurement.
- **It records progress for the researchers.** The app logs each learner's in-app performance locally and exports it (PIN-protected) — no internet, ever.

---

## How it's built (the tech, briefly)

- **Engine:** Unity 6 (URP), C#, Android (ARM64), 100% offline — no ads, no accounts, no networking.
- **Shape:** persistent core systems (GameManager, SceneLoader, AudioManager, SaveManager, EventBus) + one scene per screen + a StoryLoader that turns JSON into gameplay.
- **Assets:** all free (Kenney, Mixamo, Quaternius, Google Fonts, Pixabay), sourced against a bill-of-materials.
- **Fresh Unity project** built solo, from scratch. (An earlier demo exists only as reference.)

---

## The plan of attack (how to start)

**Build the smallest complete thing first, then scale.**

1. **MVP = one story ("The Playground") playable start-to-finish**, using grey-box art (capsules and beeps). Prove the whole loop works.
2. **Polish that one story** until an 8–10 year old wants to replay it.
3. **Pour in the other 29** (just JSON content) + real art and sound.
4. **Add the research features** (session unlock PIN, logging, export).
5. **Validate → pilot → deploy** on classroom devices.

Golden rule: *playable ugly before pretty; one story before thirty.*

---

## The document set (where to find detail)

| Document | Answers | Read when |
|---|---|---|
| **Project Brief** (this file) | What is this, why, and the big picture? | First — orientation |
| **Final GDD** | *What* the game is: every rule, decision, content, art/audio, assets | Designing / deciding |
| **Build Guide** | *What order* to build in, solo workflow (phases A–J) | Planning your work |
| **Technical Design Document (TDD)** | *How* to build each piece in code: scenes, scripts, classes, the Race system, MVP | Writing code |

Plus the thesis materials (manuscript, 10 intervention plans, 30 stories, tests, validation tools) and the asset requirements list.

**If you're picking this up to build it:** read this brief → skim the GDD §2 (the walkthrough) → open the Build Guide "what to do next" → keep the TDD beside you while coding.

---

*Read the brief. Build the slice. Make it delightful. Then it's only content.*
