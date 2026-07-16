# SummaRace — Solo Developer Build Guide

**How to build SummaRace from an empty Unity project, step by step, alone.**
Companion to the *SummaRace Final GDD* (the "what"). This guide is the "how" and the "in what order."

> **Read this first if you're new to game dev:** the golden rule is **make it playable ugly, then make it pretty.** Do NOT collect all art/sound first. Build the whole game with grey boxes and beep sounds so it *works end to end*, then swap in real assets. This one habit prevents 90% of solo-dev burnout and dead ends.

---

## 0. The mental model (how a game project differs from web/mobile app work)

You've done web and mobile apps, so here's the translation:

| You know (web/mobile) | Game equivalent |
|---|---|
| Components / widgets | Prefabs (reusable GameObjects) |
| App state / store | Singletons + ScriptableObjects + save files |
| Routes / pages | Scenes |
| API returns JSON → render | StoryLoader reads JSON → drives Reader/Race/etc. |
| CSS theme file | ThemeColors + TextStyles |
| `onClick` handlers | MonoBehaviour methods + UnityEvents |
| Hot reload | Play button (enter Play Mode) |

The big new idea: a game has a **loop** (runs every frame, `Update()`), not just event-driven renders. Only the Race really uses per-frame logic; the rest (menus, reading, arranging) is basically app-style UI you already understand.

**Your build philosophy (write it on a sticky note):**
1. Playable ugly before pretty.
2. One story perfect before thirty.
3. Data-driven — content is JSON, never hard-coded.
4. Commit small, commit often (Git from minute one).

---

## The order of work (the whole journey on one page)

```
PHASE A  Understand + Plan        (you mostly did this — the GDD)
PHASE B  Project setup            (empty project → clean skeleton)
PHASE C  Core systems            (the plumbing every scene needs)
PHASE D  Content pipeline        (StoryLoader + 1 story JSON)  ← most important
PHASE E  Vertical slice          (ONE story playable, grey-box, end to end)
PHASE F  Polish the slice        (make that one story delightful)
PHASE G  Scale to 30             (content, not code)
PHASE H  Assets pass             (swap grey boxes for real art/sound)
PHASE I  Research features       (PIN, logging, export)
PHASE J  Build, test, deploy     (APK on real devices)
```

Your instinct (understand → list needs → assets → architecture → menu → play → read → race → summarize) is right — I've only reordered two things:
- **Assets come partly later.** Find/make just enough to build; do the big asset hunt during Phase H when you know exactly what fits.
- **Architecture comes before scenes** (you had this right), and **one full story before all thirty.**

---

## PHASE A — Understand & plan ✅ (mostly done)

You already have the GDD. Before coding, make sure you can answer these from memory:
- What are the 6 phases of one story round? (Read → Question → Race → Arrange → Summarize → Result)
- What's reusable vs unique? **Reusable:** menu, story-select, reader, arrange, summary, results. **Unique per story:** only the JSON content and the race's collected items — the race *engine* is reused, the *data* differs.
- What measures success? Stars from first-pick race accuracy; the paper test measures learning, not the app.

**Deliverable:** none — just confidence you understand the game. ✔

---

## PHASE B — Project setup (half a day)

Goal: an empty project becomes a clean, version-controlled skeleton that boots to a grey menu.

### B1. Create the project
- Unity Hub → New Project → **Universal 2D/3D (URP)** template, Unity 6 LTS (6000.x).
- Name: `SummaRace`. This is the fresh project (the old demo is reference only).

### B2. Git first, before anything else
- Add a Unity `.gitignore` (GitHub's Unity template). Commit the empty project as commit #1.
- This is your undo button for the whole game. Commit at the end of every step below.

### B3. Project settings
- Player Settings → **Orientation: Portrait, locked.**
- Company Name + Product Name (real values, for the APK id).
- Min API level 26 (Android 8.0). Scripting backend IL2CPP, ARM64.
- Color space Linear (URP default).

### B4. Import the essentials only (not art yet)
- **TextMeshPro** (Window → TMP → Import Essentials).
- **PrimeTween** (Asset Store / GitHub — free).
- **Fredoka** + **Nunito** fonts → make TMP font assets. (These two you get NOW, because they affect every layout.)
- Unity **Input System** package.

### B5. Create the folder structure (from GDD §7.2)
Make these empty folders now so everything has a home from day one:
```
Assets/_Game/
  Scripts/{Core, Data, Constants, Theme, Features, UI}
  Scenes/
  Prefabs/
  Resources/Stories/{Art, Narration}
Assets/Art/   Assets/Audio/   Assets/Plugins/
```

**Milestone check (= GDD M0):** project opens, is on Git, portrait-locked, fonts + PrimeTween in, folders exist. Commit: "M0 skeleton."

---

## PHASE C — Core systems (2–4 days)

These are the "plumbing" singletons every scene relies on. Build them empty-but-working; you'll flesh out methods as features need them. Build in this order:

1. **Constants** (`SceneNames`, `AudioKeys`, `PrefKeys`, `GameRules`) — just static strings/numbers. Do this first so nothing is ever typed as a magic string.
2. **EventBus** — a simple static publish/subscribe so features never call each other directly (like an event emitter you'd use on the web).
3. **GameManager** — the app's brain; holds "which story are we playing," persists across scenes (`DontDestroyOnLoad`).
4. **SceneLoader** — fade-out/in scene transitions (+ your loading tips later).
5. **AudioManager** — `Play(AudioKeys.sfx_correct)`, music channels, volume settings. Feed it placeholder beeps for now.
6. **SaveManager** — reads/writes JSON to `Application.persistentDataPath` (settings, learner profile, progress). This is your localStorage equivalent.
7. **ThemeColors / TextStyles** — the palette from GDD §6.2 as a ScriptableObject so every UI pulls one source.

**Test:** a throwaway scene with buttons that call each manager (play a sound, save a value, load a scene). When those work, your foundation is solid. Commit: "Core systems."

---

## PHASE D — Content pipeline (2–3 days) ← THE most important phase

This is the heart of the whole architecture. Get this right and 30 stories cost almost no extra code.

### D1. Define the data classes (GDD §5.3)
Plain C# classes matching the JSON schema: `StoryData`, `PageData`, `QuestionData`, `ElementData`, plus `mission` tuning. Mark them `[Serializable]`.

### D2. Write ONE real story JSON
- Hand-author `s01_easy.json` = **"The Playground"** (it's your pretest story and the fully-mockup'd one, so you can match visuals exactly).
- Put it in `Resources/Stories/`. Fill real pages, questions (mark the correct index), the 5 SWBST elements with **2 distractors each** (write these now for this one story).

### D3. Build **StoryLoader**
- `StoryLoader.Load("s01_easy")` → returns a `StoryData`. Uses `JsonUtility` or Newtonsoft.
- Validate on load (right number of elements, correctIndex in range) and fail *gracefully* (GDD §11.6).

**Test:** a script that loads the JSON and `Debug.Log`s the title, page 1 text, and the 5 elements. When you see your story print to console, the pipeline works — this is the biggest milestone in the project. Commit: "StoryLoader + first story."

---

## PHASE E — Vertical slice: ONE story, grey-box, end to end (1–2 weeks)

Now build the actual game — but only enough to play "The Playground" start to finish, using **grey boxes and beeps**. Build the scenes in play-order (your instinct was right):

### E1. MainMenu scene
Title, TAP TO START, Settings button. Wire TAP TO START → SessionMap (or straight to StorySelect for now).

### E2. StorySelect scene
Show the 3 cards (Easy/Average/Hard). For the slice, only Easy is real. SELECT → tell GameManager the story id → load Reader.

### E3. Reader scene (reusable for all 30)
- Show `pages[i].text` on a card; NEXT advances.
- After each page, show the `question` as 3 buttons; correct → chime + NEXT; wrong → highlight correct, gentle sound, NEXT (never block).
- Progress bar "n/5". Speaker button (wire narration later — fine to be silent now).
- When pages end → load Race.

### E4. Race scene (the one truly game-y part)
This is the only per-frame, physics-y scene. Build it in sub-steps:
1. A capsule "player" auto-running forward on a flat plane; swipe/arrows to switch between 3 lanes.
2. A "patrol" cube behind, distance = DangerLevel (rises over time, cosmetic).
3. Spawn 5 checkpoints in order; each floats 3 labeled cubes (1 correct + 2 distractors from JSON) across lanes.
4. Hit correct → boost + `EventBus` "element collected"; hit wrong → slow + correct one glows, still must be collected.
5. After 5th → FINISH → load Arrange.
Keep it UGLY. Capsule + cubes + text labels. It just needs to *work*.

### E5. Arrange scene (reusable)
- Show the 5 collected pieces shuffled in a pool; 5 labeled slots (S/W/B/S/T).
- Tap piece → tap slot to place; UNDO; VERIFY ORDER → correct locks green, wrong wiggles amber, retry unlimited.
- When correct → load Summary.

### E6. Summary scene (reusable)
- Show the arranged S/W/B/S/T list + example hint.
- One text input (autocorrect off — GDD §11.3). SUBMIT → light checks (§4.5) → accept → load Results.

### E7. Results scene (reusable)
- Stars (from first-pick race accuracy), story title, praise, reveal Main Idea. NEXT MISSION → back to StorySelect.

**Milestone check (= GDD M1):** you can play "The Playground" from menu to results without touching the keyboard-of-god. It's ugly. It's a game. **This is the single most important moment of the project — celebrate it.** Commit: "M1 vertical slice playable."

---

## PHASE F — Polish the slice (3–5 days)

Make that ONE story feel great before multiplying it. Now the fun part:
- Drop in the real **UI kit** (Cartoon UI), palette colors, Fredoka/Nunito everywhere.
- Add **core SFX** from Kenney (the 5 packs from GDD §12.13) — rename to your `AudioKeys` names.
- Add **PrimeTween** juice: button squash, card slides, star pops, speed-boost FOV kick, danger vignette.
- Add one **real player character** (Mixamo) + patrol, and the grassy track reskin — just enough that the Race looks like the mockups.
- Add the hero image for The Playground.

**The bar:** would an 8–10 year old *want to play it again?* If yes, the template is proven. Everything after this is repetition + content. Commit: "M1 polished — the look is locked."

---

## PHASE G — Scale to 30 stories (content, mostly not code)

Because it's data-driven, this is authoring, not engineering:
1. Convert the content doc → 30 JSON files (script the boring parts).
2. Write the **300 race distractors** (10 per story) — batch with AI help, then you review each (GDD §5.2, D6).
3. Add the **Session Map** scene (10 stops, unlock states) and per-session Story Select.
4. Generate **30 hero images** in one locked AI style; drop into Resources.
5. Spot-check every story loads and completes (a story that crashes = bad JSON, not bad code).

**Milestone check (= GDD M2 content side):** all 30 playable. Commit per batch of stories.

---

## PHASE H — Full assets pass (ongoing, parallel-friendly)

Now do the big asset hunt, because you know exactly what you need (GDD §12 is your shopping list). Swap remaining grey boxes:
- Environment props (fences, benches, playground), skybox polish, particles.
- Narration: generate TTS for all pages; drop files named to match JSON paths (zero code change).
- Music tracks; victory jingles; remaining icons/badges.
- **Log every asset** in `assets_credits.md` with its license the moment you add it (GDD §6.8).

Rule: because names are the contract, swapping an asset = overwriting a file. Never blocks code.

---

## PHASE I — Research features (3–5 days)

The features that make it a *study* tool (GDD §8):
- **TeacherGate**: PIN prompt; unlocks next session; opens Teacher Menu.
- **SessionLogService**: append every event (per §8.2) to the learner's log; flush on scene change + app pause.
- **Export**: Teacher Menu → write JSON + CSV per learner to Downloads; share sheet.
- **LearnerProfile**: name/alias + avatar at first launch; profile picker if devices are shared.

**Milestone check (= GDD M2 complete):** validation build ready — 30 stories + PIN + logging + export, stable APK for the validators.

---

## PHASE J — Build, test, deploy (ongoing → final)

- Build an **APK early** (end of Phase E if possible) and put it on a real cheap Android phone. The editor lies about performance and touch feel — test on device often.
- Device-floor QA: 2GB RAM Android 8 phone, 30fps in the race, ≤300MB APK, one session ≤25% battery (GDD §7.5, §11.4).
- Pilot build (M3) → fixes → content-complete 1.0.0 (M4) → sign APK → install on all classroom devices per checklist (M5).

---

## The realistic solo timeline (rough, part-time)

| Phase | What | Rough time |
|---|---|---|
| B | Setup | 0.5 day |
| C | Core systems | 2–4 days |
| D | Content pipeline | 2–3 days |
| E | Vertical slice (grey-box) | 1–2 weeks |
| F | Polish the slice | 3–5 days |
| G | Scale to 30 | 1–2 weeks (mostly content) |
| H | Assets pass | ongoing / parallel |
| I | Research features | 3–5 days |
| J | Build + pilot + deploy | ongoing → 1 week final |

Don't fixate on dates — fixate on the **milestone order**. Each phase unlocks the next.

---

## Solo-dev survival rules

1. **Commit to Git after every step.** Your only safety net.
2. **Grey-box first.** A capsule that plays beats a beautiful thing that doesn't.
3. **One story before thirty.** Prove the template, then pour content.
4. **Test on a real phone weekly.** The editor is not the phone.
5. **Don't gold-plate.** Polish over scope means *finish* the small set, don't expand it.
6. **When stuck, shrink the task.** "Build the Race" is scary; "make a capsule move forward" is not. Always find the smallest next capsule.
7. **Name things their final names now** (assets + scripts) so future-you isn't renaming.
8. **The GDD is the boss.** If code and GDD disagree, fix the one that's wrong on purpose — don't drift silently.

---

## What to do literally next (your first 3 sessions at the computer)

1. **Session 1:** Phase B end to end — new URP project, Git, portrait lock, import TMP + PrimeTween + fonts, create folders. Commit "M0 skeleton."
2. **Session 2:** Phase C — Constants + EventBus + GameManager + SceneLoader. Test scene that changes scenes with a fade. Commit.
3. **Session 3:** Phase D — data classes + `s01_easy.json` (The Playground) + StoryLoader that prints the story to console. When it prints, you've built the spine of the whole game.

After that, you're into the vertical slice — and from there, SummaRace is just repetition and polish.

*Build the slice. Make it delightful. Then it's only content.*
