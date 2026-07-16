# SummaRace — Technical Design Document (TDD)

**The engineering bible. Every scene, script, class, and system needed to build SummaRace.**

This is the "how to build it in code" document. It sits between:
- **GDD** (*what the game is* — rules, decisions, content) and
- **Build Guide** (*what order to build in* — phases A–J).

Use all three together. When this TDD and the GDD ever disagree, the GDD wins on design; this doc wins on implementation detail.

**Reader assumption:** you know C# and app development but are newer to Unity. Unity-specific terms are explained inline the first time. A glossary is at the end (§16).

---

## Table of contents
1. MVP definition (scope control)
2. Architecture overview
3. Tech stack & project configuration
4. Naming conventions & coding standards
5. Folder & namespace structure
6. Data model (classes, JSON, save files)
7. Core systems (the singletons)
8. EventBus — full event catalog
9. Scene specifications (all 12 scenes)
10. Feature modules in detail
11. The Race system (deep dive)
12. Save system & research data capture
13. Error handling & edge cases
14. Testing & QA checklist
15. Definition of Done (per milestone)
16. Glossary for app developers

---

## 1. MVP definition (scope control)

**MVP = the smallest version that proves the whole game works.** For SummaRace, the MVP is **one story ("The Playground"), playable end to end, grey-box art, on a real phone.** That's the GDD's M1 "vertical slice." If that one story is fun and complete, everything else is repetition + content.

### 1.1 What's IN the MVP
- One playable story loaded from JSON (`s01_easy.json`).
- The full 6-phase loop: Reader → (questions) → Race → Arrange → Summary → Results.
- Core systems working: GameManager, SceneLoader, AudioManager (beeps ok), SaveManager, EventBus.
- 3-lane runner with lane switching, 5 checkpoints, correct/wrong feedback, DangerLevel, finish.
- Stars calculated and shown.
- Runs as an APK on an Android phone.

### 1.2 What's DEFERRED past MVP (do NOT build these first)
| Deferred | Add in phase |
|---|---|
| Remaining 29 stories | G |
| Session Map + 10-session unlock | G |
| Teacher PIN + gating | I |
| Logging + CSV export | I |
| Real characters, illustrations, narration, music | F/H |
| Settings screen full contents | F |
| Multiple learner profiles | I |
| Tutorial overlay | F |

### 1.3 The one rule of MVP
If a task doesn't help "The Playground" become playable end to end, it is **not** MVP — write it on the backlog and move on. Scope creep is the #1 killer of solo projects.

---

## 2. Architecture overview

### 2.1 The shape
SummaRace is a **scene-based, data-driven, event-communicating** Unity app.

```
                 ┌────────────────────────────────────────────┐
                 │         PERSISTENT (DontDestroyOnLoad)       │
                 │  GameManager  SceneLoader  AudioManager      │
                 │  SaveManager  EventBus(static)  Theme        │
                 └───────────────┬────────────────────────────┘
                                 │ current story id, settings, logs
   Scenes (loaded one at a time):│
   Boot → MainMenu → SessionMap → StorySelect → Reader →
   Race → Arrange → Summary → Results → (loop) ; TeacherMenu, Settings
                                 │
                 ┌───────────────┴────────────────┐
                 │   StoryLoader reads JSON  →  StoryData   │
                 │   feeds Reader, Race, Arrange, Summary   │
                 └──────────────────────────────────────────┘
```

### 2.2 Three architectural pillars
1. **Persistent singletons** hold state and services that must survive scene changes (like a global store + service layer in an app). Created once in the Boot scene.
2. **Scenes are screens.** Each scene is self-contained; it reads what it needs from GameManager and StoryLoader, does its job, then asks SceneLoader to move on.
3. **Data drives content.** No story facts live in code. `StoryData` (from JSON) is passed around; the same Reader/Race/Arrange/Summary code renders any of the 30 stories.

### 2.3 Communication rules
- Features never call each other directly. They **raise events** on the EventBus and **read shared state** from GameManager.
- Example: Race finishes → raises `RaceCompleted(results)` → GameManager stores results → SceneLoader loads Arrange → Arrange reads the collected pieces from GameManager.

---

## 3. Tech stack & project configuration

| Item | Value | Notes |
|---|---|---|
| Unity | 6000.x LTS | URP template |
| Render pipeline | URP 17.x | Mobile renderer, no realtime shadows |
| Language | C# | |
| Scripting backend | IL2CPP | required for ARM64 |
| Target arch | ARM64 | |
| Min Android API | 26 (Android 8.0) | old classroom devices |
| Orientation | Portrait, locked | Player Settings |
| Input | Input System package | touch + editor keyboard |
| UI | uGUI + TextMeshPro | |
| Camera | Cinemachine (Race only) | |
| Tween | PrimeTween | free, fast |
| JSON | JsonUtility (or Newtonsoft if nested needs) | |
| Save location | `Application.persistentDataPath` | JSON files |
| Networking | NONE | hard requirement |

### 3.1 Player Settings checklist
- Company Name + Product Name set (real values → package id `com.company.summarace`).
- Default Orientation = Portrait; auto-rotation OFF.
- Color Space = Linear.
- Minify/strip: enable for release only.
- `Application.targetFrameRate = 60` in Boot (drops to 30 automatically if needed).
- In `AndroidManifest`: `android:allowBackup="false"` (privacy — no cloud copy).

---

## 4. Naming conventions & coding standards

Consistency here is what keeps a solo project maintainable.

| Thing | Convention | Example |
|---|---|---|
| Scripts / classes | PascalCase | `StoryLoader`, `RaceController` |
| Scenes | PascalCase | `Reader`, `Race`, `SessionMap` |
| Methods | PascalCase | `LoadStory()`, `OnElementCollected()` |
| Public fields (Inspector) | camelCase | `playerSpeed`, `laneWidth` |
| Private fields | `_camelCase` | `_currentPage`, `_dangerLevel` |
| Constants | PascalCase static | `GameRules.MaxLanes` |
| Prefabs | PascalCase | `AnswerCard`, `TrackSegment` |
| Assets (art/audio) | snake_case = the key | `sfx_correct.ogg`, `s01_easy.png` |
| Events | PascalCase | `RaceCompleted`, `PageAnswered` |
| Namespaces | `SummaRace.<Layer>` | `SummaRace.Features.Race` |

### 4.1 Code standards
- One class per file, file name = class name.
- No magic strings/numbers — pull from `Constants/`.
- Every MonoBehaviour: fields at top, `Awake/OnEnable/Start` next, then logic, then event handlers.
- Comment the *why*, not the *what*.
- Prefer `[SerializeField] private` over `public` for Inspector fields.
- Guard against null on scene load (features must survive being opened directly for testing).

---

## 5. Folder & namespace structure

```
Assets/
  _Game/
    Scripts/
      Core/           namespace SummaRace.Core
        GameManager.cs
        SceneLoader.cs
        AudioManager.cs
        SaveManager.cs
        EventBus.cs
        Bootstrapper.cs
      Data/           namespace SummaRace.Data
        StoryData.cs   PageData.cs   QuestionData.cs   ElementData.cs
        MissionConfig.cs
        StoryLoader.cs
        LearnerProfile.cs   SessionLog.cs   AppSettings.cs
      Constants/      namespace SummaRace.Constants
        SceneNames.cs  AudioKeys.cs  PrefKeys.cs  GameRules.cs  GameText.cs
      Theme/          namespace SummaRace.Theme
        ThemeColors.cs (ScriptableObject)   TextStyles.cs
      Features/       namespace SummaRace.Features.<Name>
        Boot/  MainMenu/  SessionMap/  StorySelect/  Reader/
        Race/  Arrange/  Summary/  Results/  TeacherMenu/  Settings/
      UI/             namespace SummaRace.UI
        SafeAreaFitter.cs  ProgressBar.cs  StarRow.cs  SpeechBubble.cs
        AnswerCardView.cs  FancyButton.cs
    Scenes/           (one .unity per feature)
    Prefabs/          (grouped by feature)
    Resources/
      Stories/        s01_easy.json ... s10_hard.json
      Stories/Art/    s01_easy.png ...
      Stories/Narration/  s01_easy_p1.ogg ...
  Art/    (fonts, 2D UI, materials)
  Audio/  (music, sfx — named per AudioKeys)
  Plugins/ (third-party: PrimeTween, owned packs)
```

**Why `Resources/`?** Files in a `Resources` folder can be loaded by name at runtime (`Resources.Load("Stories/s01_easy")`) — perfect for data-driven content that ships inside the app and works offline.

---

## 6. Data model

### 6.1 Runtime content classes (parsed from story JSON)

```csharp
[Serializable] public class StoryData {
    public string id;            // "s01_easy"
    public int session;          // 1..10
    public string difficulty;    // "easy" | "average" | "hard"
    public string title;
    public string heroImage;     // Resources path, no extension
    public string mainIdea;
    public PageData[] pages;     // 1..5
    public ElementData[] elements; // exactly 5, S-W-B-S-T order
    public MissionConfig mission;
}

[Serializable] public class PageData {
    public string text;
    public string narration;     // Resources path, optional
    public QuestionData question;
}

[Serializable] public class QuestionData {
    public string text;
    public string[] options;     // 3 options
    public int correctIndex;     // 0..2
}

[Serializable] public class ElementData {
    public string type;          // "SOMEBODY".. "THEN"
    public string correct;
    public string[] distractors; // 2
}

[Serializable] public class MissionConfig {
    public float playerSpeed;
    public float checkpointSpacing;
    public float startingDanger;
    public float dangerPerSecond;
}
```

### 6.2 Save / profile classes (written to persistentDataPath)

```csharp
[Serializable] public class AppSettings {
    public float musicVolume = .8f, sfxVolume = 1f, narrationVolume = 1f;
    public bool haptics = true, narrationOn = true;
    public string teacherPinHash;   // never store the raw PIN
}

[Serializable] public class LearnerProfile {
    public string id;               // guid
    public string displayName;      // name or alias
    public int avatarIndex;         // 0..3
    public int unlockedSession = 1; // teacher raises this
    public List<StoryProgress> progress = new();
}

[Serializable] public class StoryProgress {
    public string storyId;
    public int bestStars;           // never decreases
    public bool completed;
}

// One entry appended per play-through (research data — §12)
[Serializable] public class SessionLog {
    public string learnerId, storyId;
    public string startedIso, finishedIso;
    public float totalSeconds;
    public List<int> readingFirstChoices = new();      // per question
    public List<bool> readingFirstCorrect = new();
    public List<bool> raceFirstPickCorrect = new();    // 5 elements
    public int timesCaught, arrangeAttempts, nudgeCount;
    public string summaryText;      // verbatim
    public int starsEarned;
    public bool isReplay;
}
```

### 6.3 Files on disk
| File | Content |
|---|---|
| `settings.json` | one `AppSettings` |
| `profiles.json` | list of `LearnerProfile` |
| `logs/<learnerId>.jsonl` | one `SessionLog` per line (append-only) |
| exported: `Downloads/SummaRace/<name>.csv` + `.json` | researcher export |

---

## 7. Core systems (the singletons)

All live in the **Boot** scene, created by `Bootstrapper`, marked `DontDestroyOnLoad`. Each is a MonoBehaviour singleton with a static `Instance`.

### 7.1 Bootstrapper
- Runs first. Instantiates the singletons in order, loads `AppSettings`, then loads `MainMenu`.
- Guarantees no scene is entered before services exist (so you can still Play any scene directly in-editor by having each feature lazily create a fallback bootstrap if `Instance == null`).

### 7.2 GameManager
The app's brain and shared state.
```
State it holds:
  StoryData CurrentStory
  LearnerProfile CurrentLearner
  RaceResult LastRaceResult   (collected pieces, per-element correctness)
  ArrangeResult LastArrange
Public API:
  void StartStory(string storyId)      // loads JSON via StoryLoader, sets CurrentStory, goes to Reader
  void SetRaceResult(RaceResult r)
  void SetArrangeResult(...)
  int CalculateStars()                 // from LastRaceResult (GDD §4.2)
  void CompleteStory(int stars)        // updates profile progress, writes SessionLog
```

### 7.3 SceneLoader
- `Load(string sceneName)` with fade-out → load → fade-in, over a full-screen canvas that persists.
- Shows a random SWBST tip during load (GDD §11.5).
- Single method the whole app uses to change screens.

### 7.4 AudioManager
```
  void PlaySfx(string key)             // key from AudioKeys
  void PlayMusic(string key, bool loop)
  void StopMusic()
  void SetVolumes(AppSettings s)
Internal: preloads AudioClips from Audio/ by key; music on its own AudioSource, sfx on a pool.
```

### 7.5 SaveManager
```
  AppSettings LoadSettings() / SaveSettings(s)
  List<LearnerProfile> LoadProfiles() / SaveProfiles(list)
  void AppendLog(SessionLog log)       // append one line to logs/<id>.jsonl, flush now
  string ExportLearner(string learnerId) // writes csv+json to Downloads, returns path
All writes wrapped in try/catch; failures raise EventBus.SaveFailed with a reason.
```

### 7.6 StoryLoader (in Data, used like a service)
```
  StoryData Load(string storyId)       // Resources.Load<TextAsset> → JsonUtility.FromJson → Validate
  bool Validate(StoryData s)           // 5 elements, correctIndex range, pages 1..5; else graceful fail
```

---

## 8. EventBus — full event catalog

A tiny static pub/sub. Usage: `EventBus.Raise(new PageAnswered{...})` and `EventBus.Subscribe<PageAnswered>(handler)`.

| Event | Raised when | Payload |
|---|---|---|
| `AppReady` | Bootstrapper done | — |
| `StoryStarted` | GameManager.StartStory | storyId |
| `PageAnswered` | Reader question answered | pageIndex, chosenIndex, correct |
| `ReadingCompleted` | last page done | — |
| `ElementCollected` | Race pickup | elementIndex, wasCorrect |
| `PlayerCaught` | DangerLevel hit 100 | — |
| `RaceCompleted` | finish line | RaceResult |
| `ArrangeVerified` | verify pressed | correct(bool), attemptCount |
| `SummarySubmitted` | submit | text, nudgeCount |
| `StoryCompleted` | Results shown | storyId, stars |
| `SessionUnlocked` | Teacher PIN | sessionNumber |
| `SaveFailed` | any save error | reason |

Rule: features **only** talk through these events + GameManager state. This keeps every scene independently testable.

---

## 9. Scene specifications (all 12 scenes)

Each scene section lists: **purpose · UI elements · scripts · enters from / exits to.**

### 9.1 Boot
- **Purpose:** create singletons, load settings, route to MainMenu.
- **UI:** logo splash.
- **Scripts:** `Bootstrapper`.
- **Exit:** → MainMenu.

### 9.2 MainMenu
- **Purpose:** entry; start playing or open settings.
- **UI:** logo, "TAP TO START", Settings button, (later) credits.
- **Scripts:** `MainMenuController`.
- **Enter from:** Boot. **Exit to:** SessionMap (or StorySelect in MVP), Settings.

### 9.3 SessionMap *(post-MVP)*
- **Purpose:** show 10 sessions, unlock states, route into a session.
- **UI:** path with 10 stops, star badges, lock icons, Teacher button.
- **Scripts:** `SessionMapController`, `TeacherGate`.
- **Exit to:** StorySelect (chosen session), TeacherMenu.

### 9.4 StorySelect
- **Purpose:** pick Easy/Average/Hard within the current session.
- **UI:** 3 story cards (hero image, title, SELECT), lock states, back.
- **Scripts:** `StorySelectController`.
- **Behavior:** SELECT → `GameManager.StartStory(id)`.
- **Exit to:** Reader.

### 9.5 Reader *(reusable)*
- **Purpose:** show story pages + a question after each.
- **UI:** reading card (text), speaker/mute toggle, NEXT, progress bar, question panel (3 option buttons), feedback label.
- **Scripts:** `ReaderController`, `QuestionPanel`, uses `AudioManager`, `ProgressBar`.
- **Flow:** page → question → (feedback) → next page … → `ReadingCompleted` → Race.

### 9.6 Race *(reusable engine, unique data)* — see §11.
- **Exit to:** Arrange.

### 9.7 Arrange *(reusable)*
- **Purpose:** order the 5 collected pieces into S/W/B/S/T slots.
- **UI:** 5 labeled slots, shuffled piece pool, UNDO, VERIFY ORDER, Ms. Lumi bubble.
- **Scripts:** `ArrangeController`, `DraggablePiece`, `SlotView`.
- **Exit to:** Summary.

### 9.8 Summary *(reusable)*
- **Purpose:** type one summary sentence.
- **UI:** arranged SWBST reference list, example hint, input field (autocorrect off), SUBMIT.
- **Scripts:** `SummaryController` (+ light checks §4.5 of GDD).
- **Exit to:** Results.

### 9.9 Results *(reusable)*
- **Purpose:** stars + praise + main-idea reveal.
- **UI:** star row (animated), title, praise text, main idea card, NEXT MISSION.
- **Scripts:** `ResultsController`, `StarRow`.
- **Exit to:** StorySelect (or SessionMap if session complete).

### 9.10 TeacherMenu *(post-MVP)*
- **Purpose:** PIN-gated teacher tools.
- **UI:** unlock next session, switch/create profile, export logs, delete data, last-flush status.
- **Scripts:** `TeacherMenuController`, `TeacherGate`.

### 9.11 Settings
- **Purpose:** volumes, haptics, credits, about.
- **UI:** 3 sliders, master mute, haptics toggle, credits list, version label.
- **Scripts:** `SettingsController`.

### 9.12 (Optional) Tutorial overlay
- Not a scene — an overlay prefab shown over Reader/Race on first run (GDD §11.1).

---

## 10. Feature modules in detail

### 10.1 Reader
```
State: int _pageIndex; StoryData story = GameManager.CurrentStory;
Show page: set card text = story.pages[i].text; narration button plays story.pages[i].narration.
NEXT (page has question): reveal QuestionPanel with 3 options.
Answer: if index == correctIndex → AudioManager.PlaySfx(sfx_correct); mark green.
        else → PlaySfx(sfx_not_quite); highlight correct option; (never block).
        Raise PageAnswered(i, chosen, correct); enable NEXT.
Advance: i++ until pages end → Raise ReadingCompleted → SceneLoader.Load(Race).
Log: append first choice + correctness to the in-progress SessionLog.
```

### 10.2 Arrange
```
On enter: pieces = GameManager.LastRaceResult.collectedPieces (5, correct text) shuffled.
Place: tap piece then slot (or drag). UNDO returns last placement.
VERIFY: for each slot, correct if piece.type matches slot.type in S-W-B-S-T order.
        correct slots lock green (sfx_slot_lock); wrong wiggle amber (sfx_slot_wiggle), return to pool.
        attemptCount++. After 3 misses on same piece → show Ms. Lumi hint.
All correct → Raise ArrangeVerified(true, attempts) → SceneLoader.Load(Summary).
```

### 10.3 Summary
```
Show arranged list (S/W/B/S/T text) as read-only reference + example hint.
Input field: multiline off; autocorrect/prediction disabled; max 200 chars.
SUBMIT → run light checks (not empty/≥5 words; ≤1 sentence; mentions Somebody or ≥2 SWBST keywords).
  fail (≤2 times) → show gentle nudge, stay.
  else accept → store summaryText → Raise SummarySubmitted → SceneLoader.Load(Results).
```

### 10.4 Results
```
stars = GameManager.CalculateStars();
Animate StarRow (pop each with rising pitch sfx_star).
Show title, praise line (GameText by star count), then reveal mainIdea card.
GameManager.CompleteStory(stars) → updates profile + writes SessionLog (flush).
NEXT MISSION → if session's 3 stories done → SessionMap (celebrate) else StorySelect.
```

### 10.5 SessionMap + TeacherGate *(post-MVP)*
```
Render stops 1..10 from CurrentLearner.unlockedSession + progress stars.
Current session playable; future locked.
Teacher button → PIN prompt (TeacherGate) → TeacherMenu.
Unlock next: unlockedSession++ (persisted) → Raise SessionUnlocked.
```

---

## 11. The Race system (deep dive)

The only real-time, per-frame system. Build it in the sub-steps from Build Guide Phase E4. Components:

### 11.1 Objects
| Object | Role |
|---|---|
| `RaceController` | orchestrates the run, checkpoints, danger, finish |
| `PlayerRunner` | auto-forward motion, lane switching, speed state |
| `PatrolChaser` | positioned behind player based on DangerLevel (visual only) |
| `LaneManager` | maps lane index (0,1,2) → world X |
| `Checkpoint` | a gate holding 3 `OptionPickup`s for one SWBST element |
| `OptionPickup` | a collectible with text + isCorrect flag |
| `TrackSegment` | scrolling ground (object-pooled) |
| `DangerMeter` | 0–100 model + UI vignette |
| `RaceHUD` | "Collect: WANTED" banner, timer, danger visual |

### 11.2 Lane switching (Input System)
```
lanes at X = -laneWidth, 0, +laneWidth.
swipe left/right (or A/D, arrows) → currentLane clamp 0..2 → tween player X (PrimeTween) 0.15s.
```

### 11.3 Movement & scrolling
```
Two options (pick one):
 (a) Player moves forward in world; camera follows (Cinemachine); track pooled ahead.
 (b) Player fixed at Z; track + objects scroll toward player (classic runner).
Recommend (b) for infinite-feel and simple pooling.
Speed = mission.playerSpeed × speedMultiplier (boost/slow).
```

### 11.4 Checkpoints & collection
```
Spawn checkpoints spaced by mission.checkpointSpacing, in S-W-B-S-T order.
Each checkpoint spawns 3 OptionPickups across the 3 lanes:
  one = element.correct (isCorrect=true), two = element.distractors (false), shuffled into lanes.
On trigger-enter with an OptionPickup:
  if correct → speedMultiplier boost 2s, sfx_boost, sparkle VFX, danger -= 15,
               record raceFirstPickCorrect[i]=true (only first pickup of this checkpoint counts),
               pass checkpoint.
  if wrong  → speedMultiplier slow 1.5s, sfx_not_quite, danger += 10,
               the correct pickup starts glowing; player MUST still collect it to advance
               (so they always leave holding the 5 correct pieces).
  Raise ElementCollected(i, wasCorrect).
```

### 11.5 DangerLevel (the "fake chase")
```
Every frame: danger += mission.dangerPerSecond × dt.
danger modifies PatrolChaser distance & screen-edge amber vignette intensity.
danger ≥ 100 → PlayerCaught: patrol touches shoulder, friendly overlay 1.5s,
              danger = 50, timesCaught++, run continues. NEVER a fail state.
danger clamped 0..100.
```

### 11.6 Finish & result
```
After 5th checkpoint passed → spawn FINISH gate → on cross:
  build RaceResult { collectedPieces[5] (the correct text of each element in order),
                     firstPickCorrect[5], timesCaught, runSeconds }
  GameManager.SetRaceResult(result); Raise RaceCompleted; SceneLoader.Load(Arrange).
```

### 11.7 Grey-box first
Player = capsule. Patrol = red cube. Pickups = cubes with a TextMeshPro label. Track = a long quad with a scrolling material. Get all §11.4–11.6 working like this before any art. This *is* the game — everything else is paint.

---

## 12. Save system & research data capture

### 12.1 What gets logged (one SessionLog per play-through)
Reading first choices + correctness · race first-pick correctness (5) · times caught · arrange attempts · summary text verbatim · nudge count · stars · replay flag · timestamps + total time. (Matches GDD §8.2.)

### 12.2 When it writes
- A `SessionLog` is built up in memory across the phases (Reader → Race → Arrange → Summary).
- On Results, `GameManager.CompleteStory` finalizes it and `SaveManager.AppendLog` writes one line to `logs/<learnerId>.jsonl` and flushes.
- Also flush on `OnApplicationPause(true)` and scene change so nothing is lost if the app is backgrounded mid-story.

### 12.3 Export (TeacherMenu)
- `ExportLearner(id)` reads the `.jsonl`, produces a flat CSV (one row per story attempt, columns for every logged field) + a JSON copy, writes to `Downloads/SummaRace/`, and opens the Android share sheet.
- Never automatic — only a deliberate teacher action (privacy).

### 12.4 Data lifecycle
- Alias allowed instead of real name. Teacher Menu → "Delete data" wipes profiles + logs after the study (GDD §8.4).

---

## 13. Error handling & edge cases

| Case | Handling |
|---|---|
| Story JSON missing/corrupt | `StoryLoader.Validate` fails → friendly card "This story is taking a nap!" → back to StorySelect; log error. Never crash. |
| Narration file missing | hide speaker icon for that page; continue silently. |
| Hero image missing | show placeholder card with title. |
| App backgrounded mid-race | auto-pause + flush log. |
| App killed mid-story | on relaunch resume at current phase (persist phase + collected pieces). |
| Save/export fails | catch → EventBus.SaveFailed(reason) → teacher-facing message; data intact on device. |
| Opened a feature scene directly (editor test) | each controller null-checks GameManager.CurrentStory and loads a debug story if none. |
| Summary blank / gibberish | light nudges ≤2, then accept (never block). |
| Verify order wrong repeatedly | unlimited retries; hint after 3 same-piece misses. |

Golden rule: **never a crash, never a blank screen, never a dead end.**

---

## 14. Testing & QA checklist

### 14.1 Per-feature smoke tests (do while building)
- [ ] Each singleton works from a throwaway test scene.
- [ ] StoryLoader prints The Playground to console.
- [ ] Reader: correct + wrong answers both advance; progress bar fills.
- [ ] Race: lane switch, collect correct (boost), collect wrong (slow + must still collect), caught resets not fails, finish loads Arrange.
- [ ] Arrange: verify, amber wiggle, unlimited retry, hint after 3.
- [ ] Summary: nudges then accept; text saved.
- [ ] Results: stars match race accuracy; main idea shows.

### 14.2 Full-loop test
- [ ] Menu → play The Playground → Results, no keyboard/editor intervention.

### 14.3 Device test (weekly, on real phone)
- [ ] APK installs & launches offline (airplane mode).
- [ ] 30fps in the race on a 2GB Android 8 device.
- [ ] Touch swipe feels right; text readable at arm's length.
- [ ] One full story ≤ target battery; APK ≤ 300MB.

### 14.4 Content test (Phase G)
- [ ] All 30 JSONs load & complete without error.
- [ ] Distractors are plausible-but-wrong (researcher review).

### 14.5 Research test (Phase I)
- [ ] Teacher PIN gates sessions; export produces valid CSV; logs flush on pause.

---

## 15. Definition of Done (per milestone)

| Milestone | Done when |
|---|---|
| **M0** setup | New URP project on Git, portrait locked, fonts + PrimeTween in, folders + namespaces created, boots to grey MainMenu. |
| **M1** MVP slice | The Playground playable menu→results, grey-box, core SFX, logging in memory; passes §14.2. **The project's proof of life.** |
| **M1-polish** | Real UI/palette/fonts, Kenney SFX, PrimeTween juice, one real character + track, hero image; passes the "play again?" test. |
| **M2** validation build | 30 JSONs load + distractors approved, SessionMap + PIN + export, summer world assembled, stable APK for validators. |
| **M3** pilot | Validator fixes in; pilot run; notes gathered. |
| **M4** study build 1.0.0 | 30 hero images + full narration, pilot fixes, content frozen, device-floor QA pass. |
| **M5** deploy | Signed APK on all devices per checklist; researcher dry-run + export OK; GO. |

---

## 16. Glossary for app developers

| Unity term | What it is (app-dev analogy) |
|---|---|
| GameObject | a node in the scene tree (like a DOM element) |
| Component | a script/behavior attached to a GameObject (like a mixin) |
| MonoBehaviour | base class for components; has lifecycle hooks |
| Prefab | a saved reusable GameObject template (like a component you instantiate) |
| Scene | a screen/level; loaded one at a time (like a route) |
| ScriptableObject | a data asset that lives in the project (like a config/JSON asset with types) |
| Inspector | the panel where you set public fields per object (like props in a visual editor) |
| Serialize | expose a field to the Inspector / save it (`[SerializeField]`) |
| `Awake/Start` | init hooks (Awake = on load, Start = before first frame) |
| `Update()` | runs every frame (~60×/sec) — the game loop |
| `Instantiate/Destroy` | create/remove objects at runtime |
| Coroutine | a function that can pause across frames (`yield`) — like async steps |
| `DontDestroyOnLoad` | keep an object alive across scene changes (a global singleton) |
| URP | Universal Render Pipeline — the rendering/graphics settings |
| Cinemachine | smart camera system |
| Object pooling | reuse objects instead of create/destroy (perf) |
| `Resources.Load` | load an asset by path at runtime |
| Tween (PrimeTween) | animate a value over time (like CSS transitions in code) |

---

## How to use this document

1. Keep it open beside Unity while building.
2. Build in the Build Guide's phase order; use this doc for the *details* of each piece.
3. When you add something new, update the relevant section (events, scenes, data) so the doc never drifts from the code.
4. If in doubt about design → GDD. About order → Build Guide. About implementation → here.

*Build the slice. Make it delightful. Then it's only content.*
