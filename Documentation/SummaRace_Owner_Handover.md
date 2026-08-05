# SummaRace — Owner Handover

**Written 2026-08-06 · branch `experiment/endless-override-2` · HEAD `de63a3d`**

**Read this first. It is the front door to every other document.** A lot landed in the last
day across many parallel passes; this page tells you what is true, what to do next, what only
you can decide, and what is still broken. Every claim here traces to a file or a commit that
was actually read — where two documents disagree, this page says which one is right and why.

---

## 1. What state is the game in right now

**The whole game plays, end to end, and almost none of it has been played by a human on a
tablet.** Boot → Main Menu → Session Map → Story Select → Reader → Race → Arrange → Summary →
Results is complete and wired: all 30 stories load through the real `StoryLoader`, all 150
narration clips and all 30 hero images resolve, learner profiles persist, every play-through
writes a research row, and the teacher PIN gates session unlocking and log export. That much is
genuinely *verified*, not believed: the project compiles under Unity's own Roslyn (commit
`dca9b27`), the first automated test suite in the project's history runs **45/45 green**
(`Assets/_Game/Tests/Editor/`, 7 fixtures), and a play-mode pass measured the things that could
only be derived before — the patrol no longer intersects the runner, the three answer options
render legibly, the SWBST tracker fits "SOMEBODY", and pause freezes and restores cleanly
(`de63a3d`). Against that: **no APK has ever been built from this project** (Android Build
Support is not installed, so APK size and the 30fps floor are arithmetic, not measurements),
**27 of the 30 stories have never been played through once by anybody**, and every animation in
the game — `PanelIntro`, `UIFloat`, `ButtonSquash`, PrimeTween — only runs in Play mode and has
been checked in Play only for the race. The honest summary is: *the logic is in good shape and
well tested; the artefact you will actually hand to 40 children has never existed.*

---

## 2. What you must do before the study, in order

Steps 1 and 2 are hard blockers. Nothing after them can happen until they are done, and step 2
is the one that will silently ship a broken app if you skip it.

### 1. Install Android Build Support — **close Unity first**

Unity Hub ▸ Installs ▸ **6000.4.1f1** (match exactly — a different patch re-imports and
re-serialises the whole project) ▸ gear ▸ Add modules ▸ tick **Android Build Support**,
**OpenJDK**, **Android SDK & NDK Tools**. ~2GB, 10–30 minutes.

Verified: `Editor/Data/PlaybackEngines/` contains only `windowsstandalonesupport`. Until this
is done `BuildPipeline.IsBuildTargetSupported(Android)` is false and **no APK can be produced
at all.**

Then **switch platform to Android once** (`File ▸ Build Profiles ▸ Android ▸ Switch Platform`).
The first switch re-imports every texture in a 945MB `Assets/` folder. **Do this the day
before, not on build day.**

### 2. Build Addressables content for Android — the silent one

Verified on disk: `AddressableAssetSettings.asset` has
`m_BuildAddressablesWithPlayerBuild: 2` (**DoNotBuildWithPlayer**) and
`Library/com.unity.addressables/` **has no `aa/` folder at all** — content has never been built
for any platform.

The race scene is Trash Dash's `MainSummaRace`, and its road segments, scenery, themes and the
runner prefab all load through `Addressables.InstantiateAsync`. In the Editor those resolve
straight off the Asset Database, so everything looks perfect. **In a player they come from
built bundles.** Ship as-is and a learner gets a Boot screen, a menu, a Reader — and then a
race with no road, no scenery and no runner.

Fix (pick one, and write it on the build checklist):
`Window ▸ Asset Management ▸ Addressables ▸ Settings` ▸ **Build Addressables on Player Build**
(recommended — every APK then carries current content with no human step), or build content by
hand from the Groups window with the platform already on Android, every single time.

### 3. Run `SummaRace ▸ Build Preflight`

`Assets/_Game/Editor/BuildPreflight.cs`. Read-only, never throws, prints to Console, a dialog
and the clipboard. It checks the Android module, the build-scene list and index 0, player
settings, the app icon, Addressables content, all 30 stories through the real loader, banned
packages, offline violations, and unguarded `using UnityEditor;` in player code.

**Fix every ✖ before building.** Read every ▲ and accept it deliberately.

### 4. Recommended, ~10 minutes: `SummaRace ▸ Device Budget ▸ 4 · Apply ALL safe fixes`

Verified still unapplied today: `Assets/Sounds/Stems/STEMSMainTrackMono.ogg.meta` is
`loadType: 0` (DecompressOnLoad) and `music_menu.mp3` likewise. That means **~156MB of raw PCM
is resident** on a 2GB device once the race has been entered, because Trash Dash's
`MusicPlayer` is `DontDestroyOnLoad` and plays all six 4-minute stems at once. The script
recovers ~122MB of that, is idempotent, look-preserving, and touches nothing but importer
settings. Details in `SummaRace_Device_Budget.md` §2. This is the highest-value change you can
make in ten minutes, and it is the difference between comfortable and being killed by Android's
low-memory reaper.

### 5. Build ONE APK

`File ▸ Build Profiles ▸ Android`. **Build App Bundle OFF** (an `.aab` cannot be sideloaded).
**Development Build OFF** for the study build. Output to a folder outside the repo. Name it
`SummaRace_v1.0_vc1_YYYYMMDD.apk`. First IL2CPP build takes 20–45 minutes.

Then: `git tag study-build-v1` so "what is on the tablets" is answerable forever, and **back up
`C:\Users\<you>\.android\debug.keystore` next to the APK**. The APK is debug-signed, the debug
keystore is per-machine, and a mid-study rebuild from a different PC will refuse to install over
the deployed app — the only way through is uninstall, which erases that tablet's profiles and
every unexported log. **Build every study APK on the same machine.**

### 6. Sideload to one tablet and smoke-test it

Full checklist in `SummaRace_Build_And_Release.md` §8. The four that matter most:

- The launcher icon is our crown/SUMMA·RACE!, **not a cat**. (The build shipped Trash Dash's
  icon through six playtests — nothing in the game window reveals it.)
- The app opens into **Boot**, not a runner game with coins and a shop.
- **Airplane mode + Wi-Fi off**, then play a whole story. 100% offline is non-negotiable.
- **The full loop on a story that is not `s01`.** 27 of the 30 have never been played once.
  This is the single most valuable hour you can spend before day 1.

### 7. Set the teacher PIN on every tablet, before any child touches it

Main Menu ▸ teacher corner ▸ *"Teacher setup: choose a PIN"* ▸ 4+ digits ▸ confirm.
**Write the PIN down per tablet, tied to an asset tag.** The raw PIN is never stored, only a
salted hash — it cannot be read back.

If a curious child reaches the tablet first, they can set the PIN, and you have lost teacher
control of that device. The only way back is the hidden recovery gesture, **which erases every
profile and every log on that tablet permanently.** Procedure in
`SummaRace_Study_Operations_Runbook.md` §6.1.

### 8. Pull one export over USB **before day 1**, not after

Teacher menu ▸ Export logs → writes `export_<stamp>.jsonl` plus
`export_<stamp>_learners.json` (the roster — without it the data is anonymous GUIDs) to
`Application.persistentDataPath`.

Do this once, on a test learner, and actually get the files onto a PC before the study starts.
On **Android 11+ the `Android/data` folder is frequently hidden from MTP**, so dragging it over
a USB cable may simply not show the folder. The reliable route is adb:

```
adb pull /storage/emulated/0/Android/data/com.orbitsdev.summarace/files/export_<stamp>.jsonl
```

**These logs are the entire in-app dataset.** There is no cloud backup, no second copy, and the
PIN recovery gesture destroys them. Prove the retrieval path works while it costs you nothing.

### 9. Install the *same single APK file* on all 40 tablets

Do not rebuild per tablet. Every learner must run byte-identical code and content or the study
is comparing two instruments.

---

## 3. Decisions waiting on you

Five. Each states the trade-off plainly and gives a recommendation.

### D1. Reading time for the slowest readers — `RaceSecondsPerGate` 12 → 17

**Where it stands.** `GameRules.RaceSecondsPerGate = 12f` (verified in source today). Before
this session an answer card gave **0.28–0.50 seconds** of legible time against ~102 characters
per gate; the fix (`dca9b27`) moved the three options onto a screen-space preview panel in the
sky band and lengthened gate 1, giving **9.0–13.7 seconds** per gate. That meets a 100 wpm
reader's ~11.6s budget. It does **not** meet the ~16.5s a 70 wpm reader needs — and this study
deliberately includes struggling readers.

**Trade-off.** Raising it to 17 grows every gate gap by ~42%, so the moving part of a race grows
by roughly the same fraction — order of +25 seconds per race, ~+75 seconds per session of three
stories. The cost is classroom minutes. The benefit is that the race stops being partly a
reading-speed test for the very learners the thesis is about.

**Recommendation: change it to 17.** `raceFirstPickCorrect` *is* the star count and *is* the
headline logged measure; if your weakest readers converge on a 1-in-3 guess, the instrument
cannot separate them from your strongest, and that shows up as an uninterpretable floor effect
after the data is collected. 75 seconds a session is a cheap insurance premium. It is a
one-constant change — but **playtest one race after changing it**, because gate spacing is
clamped (`RaceMinGateGap` 110 / `RaceMaxGateGap` 300) and the clamp can swallow part of the
increase.

### D2. One tablet per learner, or shared tablets

**Where it stands.** The data model supports both: one `LearnerProfile` per child, one
`logs/<learnerId>.jsonl` per child, and teacher-gated learner switching now exists (Main Menu ▸
teacher corner ▸ PIN ▸ "Switch learner"). `GameText.cs` states the design intent directly:
one tablet per learner, with the shared path as a safety net.

**Trade-off.** Shared tablets need a per-child switch before every hand-off, and a missed switch
silently attributes one child's run to another — unfixable after the fact. One-per-learner costs
40 devices and 40 PINs to set and write down.

**Recommendation: one tablet per learner if you have 40 devices.** If you do not, use the shared
path but make the Main Menu's **"Playing as \<name\>"** line a mandatory spoken check before
every hand-off, and never change the policy mid-study.

### D3. Is a passive run — never steering, collecting all five gates — acceptable?

**Where it stands.** All three lanes always carry a card, so every gate is a forced choice and a
learner who never touches the screen still collects five cards. Which lane holds the correct
card is **shuffled per gate** (`PlaceAnswerGate`, verified), so a passive run scores at chance:
~1.67 of 5, the same as guessing.

**Trade-off.** It is not a validity threat — a do-nothing learner is not flattered, and the
alternative (punishing a missed gate) violates D7 "never punish the learner". The residual risk
is that you cannot tell a passive run from an unlucky one just by looking at the stars.

**Recommendation: accept it, no code change — and check for it in analysis.** Schema 3 logs
`racePicks[].lane` (0/1/2) for every card touched, precisely so position bias can be checked
rather than assumed. A learner whose five picks are all `lane: 1` never steered. Add that as a
one-line filter when you clean the data.

### D4. The 27 placeholder hero images

**Where it stands.** All 30 resolve, so nothing is broken. `s01_easy/average/hard.png` are real
1536×1024 illustrations and are the style reference. The other 27 are **blank blue-to-green
gradients** — not weak art, literally nothing drawn — at 1024×640.

**Trade-off.** Pure appearance. Learners in sessions 2–10 see a title on an empty card. It costs
you an evening with an image generator; `SummaRace_Asset_Shopping_List.md` §2 has 27
ready-to-paste prompts, the exact spec, and the overwrite rule (same filename, keep the `.meta`).

**Recommendation: do it if and only if steps 1–9 of §2 are complete and the smoke test passed.**
It is the biggest visual win available and the least important thing on this page. A thesis is
not marked on hero art; a study that could not be built is fatal.

### D5. `s05_hard` and `s06_hard` are the same story

**Where it stands.** Both are titled "In Grandfather's Day", both star Sharr, Kaze and
Grandfather, both turn on grandfather describing pre-modern technology. This is **not** a
pipeline bug — the researcher's own source doc carries that title under DAY 5 and again under
DAY 6, with different SWBST treatments and different passages (`difflib` ratio < 0.45).

**Trade-off.** A learner who reaches HARD in both sessions meets the same story twice, a week
apart — a repeated-exposure confound. Replacing one means authoring a new story and re-running
the pipeline, two days out.

**Recommendation: leave the content alone and record it as a known limitation in the thesis.**
It affects only learners who complete HARD in both sessions 5 and 6, it is identifiable in the
data by `storyId`, and it came from the source material rather than from the build. If the
researcher would rather avoid it entirely, the cheap operational fix is to not unlock session 6
HARD — no code change.

---

## 4. Known-broken and unverified — nothing hidden

### 4a. Never measured, at all

| # | Thing | Why it is unmeasured |
|---|---|---|
| 1 | **APK size** vs the 300MB cap | No APK has ever been built. The ~160–230MB figure is arithmetic from source bytes. |
| 2 | **30fps in the race** on the 2GB / Android 8 floor device | Same. Never observed on hardware. |
| 3 | **27 of 30 stories, end to end** | Never played once, by anyone, in any mode. |
| 4 | **Every tween and pop-in outside the race** | `PanelIntro`, `UIFloat`, `ButtonSquash`, PrimeTween only run in Play mode. F36–F47 changed many of them. |
| 5 | **Which tablet the study uses** | The readability audit's type-size verdicts swing on 7" vs 10.1". Knowing the panel size retires or confirms a third of that document. Cheapest unknown to resolve. |
| 6 | **End-to-end session timing** | The 15–25 min/session planning figure in the runbook is an estimate. Do one timed dry run. |

### 4b. Known limitations that will still be there on day 1

- **The patrol is still clipped at the frame's bottom/outer edge.** The overlap bug is fixed and
  measured (`Bounds.Intersects` false, 2.28m lateral separation, verified in Play). But full
  containment needs a camera pull-back, which would change the look of the whole race and is
  deliberately off-limits this close to the study. He never catches (D7) and the vignette carries
  the signal, so doing nothing is defensible.
- **The render pipeline is not the one any older document assumes.** Every quality level has
  `customRenderPipeline: {fileID: 0}` and `GraphicsSettings` points at **Trash Dash's**
  `RenderingPipeline.asset`. `Mobile_RPAsset` / `PC_RPAsset` are assigned to nothing, so every
  tuning decision recorded against them is dead. Verified today: `m_RenderScale: 1`,
  `m_MainLightShadowmapResolution: 2048`, `m_ShadowCascadeCount: 4` — rendering a full shadow
  atlas across four cascades into a race world where **nothing can receive a shadow** (the whole
  corridor is single-pass unlit). Free frame rate is sitting there, but it is a pipeline-asset
  edit that changes rendering project-wide and needs a playtest, not a config tidy. **Do not
  reassign `Mobile_RPAsset`** — it turns HDR back on, which is a regression on a tiler.
- **~156MB of resident PCM audio on a 2GB target**, unapplied as of today (verified: the stems
  and `music_menu` are still `loadType: 0`). See §2 step 4.
- **`GameRules.TargetFrameRate` is 60** while Trash Dash's `MusicPlayer.Awake` sets 30 when the
  race first loads. The app therefore targets 60fps until the first race and 30 for the rest of
  the session, burning thermal headroom in the menus that the race then needs. One-constant fix,
  not applied.
- **Tap-to-move and race pause are verified only partially.** Pause was asserted in Play mode
  (timeScale 0, audio paused, resume restores speed without reseeding `minSpeed`). Tap-to-lane
  was verified by code path and compile, **not by a finger on a tablet** — and touch is exactly
  the path that a desktop test cannot exercise. Put it at the top of the smoke test.
- **`Race.unity` (legacy) is still build index 6** with zero call sites, carrying 51.5MB of
  source assets nothing else uses. Harmless, but it is free APK weight.
- **Trash Dash's inactive objects are still inside `MainSummaRace`** (GameOver, Loadout,
  Highscore, leaderboard and store buttons). The five buttons have had their click handlers
  emptied and been made non-interactable. Hidden is not removed. **Their `UICamera/Game` chrome
  is load-bearing — `GameState.UpdateUI()` dereferences it every frame. Do not delete it.**
- **`Assets/MobileDependencyResolver/` is back on disk.** Editor-only, resolves nothing (there is
  no `*Dependencies.xml` in the project), injects nothing into Gradle. It self-resurrects after a
  domain reload; delete it with the Editor closed if you want it gone. The preflight will keep
  reporting it.

### 4c. Readability fixes that were recommended and are **still open** — verified in source today

The audit's top four pre-study items are done (option preview, tap-to-move, far-lane
reachability, "CHECK ORDER"). Items 5 and 6 are not. Each is a one-line change:

| Item | Current state, verified | Fix |
|---|---|---|
| Two strings instruct by **colour name** | `GameText.ReaderWrongFeedback` = *"Not quite — the green one is the answer!"*; `GameText.ArrangeAlmost` = *"Almost! The green ones are right…"* | ~8% of boys have a red–green deficiency; in 40 learners that is 1–2 children told to find a colour they cannot name. Reword to *"This one is the answer!"* / *"The ✓ ones are right."* |
| Reader "Not quite" feedback contrast | `ReaderController.FeedbackNotQuite` = `(0.85, 0.50, 0.15)` on white = **2.99:1** | `(0.62, 0.32, 0.02)` → 5.6:1. This is the sentence that tells a learner what went wrong. |
| Summary reference SWBST words | `SummaryController` uses `SwbstPalette.HexForIndex` — WANTED **2.34:1**, SO **2.10:1** on white | Use `DeepForIndex`. It is the project's own dark-on-light helper and already exists. |
| FINISH card label | `Color.white` on `(1, 0.72, 0.15)` amber = **1.74:1** | Deep brown `(0.30, 0.18, 0.02)` → 8.9:1 |

Half an hour of work, all in `GameText.cs`, `ReaderController.cs`, `SummaryController.cs` and
one line of `EndlessRaceDirector.PlaceFinishGate`. Worth doing before the build.

### 4d. Where the documents disagree — and which one is right

This project has a recorded history of confident, wrong summaries, so these were checked against
source rather than reconciled on the page.

| Disagreement | Which is right | Evidence |
|---|---|---|
| **`SummaRace_Data_Dictionary.md` documents schema **2** and states "No item-level record of the race distractor chosen"** | **The code is right; the Data Dictionary is one commit stale.** `SessionLogService.cs:27` = `SchemaVersion = 3`. Schema 3 adds `racePicks` (element, option index, exact card text, **lane**, correct, represent, atSeconds), `racePauseCount`, `racePausedSeconds`, `abandonReason` — none of which are in the dictionary. **This is the most important contradiction on this page**, because the researcher analyses from that document and will otherwise not know the misconception-level data exists. |
| `SummaRace_Study_Operations_Runbook.md` §7.5 and the old Finalization Plan: *"the race has no in-run pause/back/quit"* | **Stale — the race has a pause chip and a two-tap leave** (`dca9b27`, verified in Play in `de63a3d`). Leaving routes through the partial-run mechanism and flushes immediately. The runbook is explicitly grounded at commit `f044d39`, one commit earlier. |
| `SummaRace_Content_QA_Report.md` §5 P1: *"`SOMEBODY` is truncated in the race tracker — open"* | **Closed.** Plaques widened 138→160 in `dca9b27`; "SOMEBODY now fits its plaque" asserted in Play in `de63a3d`. |
| `SummaRace_Build_And_Release.md` §9 quotes *"Race.unity ~23MB"*, *"~36MB of Vorbis DecompressOnLoad"*, and offers *"Bloom in `_Game/Art/RacePostFx.asset`"* as a frame-rate lever | **`SummaRace_Device_Budget.md` is right on all three** — it read the files, Build & Release quoted the older CLAUDE.md figures. Measured: **51.5MB** exclusive to `Race.unity` (12–20MB APK), **178.2MB** DecompressOnLoad / **156.5MB** genuinely resident, and `RacePostFx.asset` has an **empty component list** with **zero** post-processing cameras in `MainSummaRace` — the Bloom lever does not exist on the shipping path. |
| `SummaRace_Readability_And_Accessibility_Audit.md` "Fix these before the study" items 1–3 | **Done since the audit was written** (screen-space option preview, tap-to-any-lane, far lane now reachable in one tap). Items 5 and 6 are **not** — see §4c above, verified in source today. |
| `CLAUDE.md`'s own NEXT list item ③ still says the race has no pause | Internally inconsistent with its own F47 row ⓓ, which records the pause as closing that item. **F47 is right.** |
| Study Ops §1.1 says debug signing needs no setup | True but incomplete. **`SummaRace_Build_And_Release.md` §5 is the fuller account**: the debug keystore is per-machine, and a mid-study rebuild elsewhere forces an uninstall that destroys that tablet's unexported logs. |

---

## 5. The documents, and when to read each one

| Document | What it is for | When you read it |
|---|---|---|
| **`SummaRace_Owner_Handover.md`** (this) | The front door. State, critical path, your decisions, what is broken. | Now, and any time you lose the thread. |
| **`SummaRace_Finalization_Plan.md`** | The live plan: what is done, what remains, realistic effort. | Right after this page. It is the only doc that is meant to change as work lands. |
| **`SummaRace_Build_And_Release.md`** | Toolchain install → preflight → Addressables → APK → sideload → smoke test. Has a printable build-day checklist. | On build day. §5 (signing) before you build the *first* APK, not after. |
| **`SummaRace_Study_Operations_Runbook.md`** | The classroom side: PIN setup, running a session, unlocking, exporting, recovering. Written for someone under time pressure. | Before day 1, and hand a copy to whoever supervises sessions. |
| **`SummaRace_Data_Dictionary.md`** | Every field in the exported `.jsonl`, what it means, cleaning rules, pandas/R loaders. **Currently documents schema 2; the build writes schema 3** (see §4d). | Give it to whoever analyses the data — with §4d's correction attached. |
| **`SummaRace_Device_Budget.md`** | Measured RAM / APK / render-pipeline reality on the 2GB floor, with a prioritised remediation table and an Editor script. | Before the build (step 4 of §2), and again if the race misses 30fps. |
| **`SummaRace_Content_QA_Report.md`** | All 30 stories measured against the geometry they render into, plus the corpus validity audit (can a gate be passed without reading?). | If anyone questions the content, or before a researcher sign-off conversation. |
| **`SummaRace_Readability_And_Accessibility_Audit.md`** | Type size in arcmin, WCAG contrast on real colours, tap targets in dp, colour-only channels, and the race time-pressure derivation. | Its top items are triaged in §4c above. Read in full only if you have a spare hour. |
| **`SummaRace_Asset_Shopping_List.md`** | The art still outstanding, with real paths, exact specs, and 27 ready-to-paste image prompts. | Only after the build works (decision D4). |
| `SummaRace_Final_GDD.docx` | The design bible. Decisions D1–D18 are locked. Intent, not implementation. | For design arguments the code has not already answered. |
| `SummaRace_Technical_Design_Document.md` | Original implementation spec (scenes, scripts, events). | Reference; the build has deliberately diverged from it. |
| `SummaRace_Project_Brief.md` | Orientation, one read. | If someone new joins. |
| `SummaRace_Build_Guide.md`, `SummaRace_Asset_Requirements_List.md` | Historical phase plan and original bill of materials. | Superseded by the Finalization Plan and the Shopping List. |
| `CLAUDE.md` | The engineering log — every pass F1…F47 with the reasoning. Not a plan. | When you need to know *why* something is the way it is. |

Two further documents (`SummaRace_Audio_Audit.md`, `SummaRace_Pro_Review.md`) were in progress
when this page was written and are not on disk yet. If they appear, read them the same way as
the audits above — a dated snapshot, triaged against §4c and §4d, not a to-do list.

**Rule of thumb:** this page and the Finalization Plan are the only two that claim to be current.
Everything else is a snapshot with a date on it, and §4d lists the places where a snapshot has
since gone stale.

---

## 6. If you only have one hour

1. Install the Android module (it downloads while you do everything else).
2. Turn on **Build Addressables on Player Build**.
3. Run `Build Preflight`, fix every ✖.
4. Run `Device Budget ▸ Apply ALL safe fixes`.
5. Build, sideload to one tablet, and play **one non-`s01` story end to end with Wi-Fi off**.

That sequence converts the largest block of unknowns in this document into facts. Everything
else on this page can wait until you know the app installs, runs, and finishes a story on real
hardware.
