# SummaRace — Owner Handover

**Rewritten 2026-08-06 · branch `experiment/endless-override-2` · verified against HEAD `b5a7003`**

**Read this first. It is the front door to every other document.** Eighteen commits have landed
since this page was last true (`63e1be3`), several of them owner-driven steers, and this rewrite
was checked against the source at HEAD rather than reconciled against the previous version. Where
a claim could not be verified, it says so.

⚠️ **This branch moved four times while this page was being written** (`b399e03` → `902e078` →
`d4f2544` → `e56643a` → `b5a7003`). Run `git log --oneline b5a7003..HEAD` before trusting any specific claim
below; if that prints nothing, this page is current.

---

## 1. What state is the game in right now

**The whole game plays end to end in the Editor, and it cannot be completed on a tablet at all
until step 2 of §2 is done.** That sentence is the whole handover.

Boot → Main Menu → Session Map → Story Select → Reader → Race → Arrange → Summary → Results is
complete and wired: all 30 stories load through the real `StoryLoader`, all 150 narration clips
and all 30 hero images resolve, learner profiles persist atomically, every play-through writes a
research row (log schema **5**), and the teacher PIN gates session unlocking and log export.

**But the race scene loads its road, its scenery and the runner itself through Addressables, and
Addressables content has never been built for any platform.** In the Editor those resolve off the
Asset Database, which is why every playtest has looked perfect. In an APK they come from bundles.
Commit `a760ec0` traced the chain: their `TrackManager` GameObject is activated at the *end* of
`Begin()`, which `yield break`s early when the Addressables character load returns null — so
`TrackManager` never appears, and the briefing used to wait for it forever with no working control
on screen and Android BACK swallowed app-wide.

**Two things changed this, and neither is a substitute for building an APK.** First, that trap is
bounded and escapable: after a timeout the briefing becomes a working "this race is not ready — go
back to Story Select" screen with the real cause logged. **Escapable is not playable.** Second,
`e56643a` set `m_BuildAddressablesWithPlayerBuild` to **1 (BuildWithPlayer)**, verified at HEAD, so
"build an APK" and "build the content that APK needs" are no longer two separate actions with
nothing connecting them. There is no longer an obvious way to produce a plausible-looking APK with
no content in it — **but no build has ever run, so this has never been observed working.** The race
starting on a tablet is still the first thing to confirm and it is still unconfirmed.

Against that: **no APK has ever been built** (Android Build Support is not installed, so APK size
and the 30fps floor remain arithmetic), **27 of the 30 stories have never been played through by
anybody**, and every tween outside the race runs only in Play mode.

The honest summary: *the logic is in good shape, the last two days closed a large number of real
defects, and the artefact you will hand to 40 children has still never existed.*

---

## 2. What you must do before the study, in order

Steps 1 and 2 are hard blockers. Step 2 is the one that silently ships a broken app.

### 1. Install Android Build Support — **close Unity first**

Unity Hub ▸ Installs ▸ **6000.4.1f1** (match exactly) ▸ gear ▸ Add modules ▸ tick **Android Build
Support**, **OpenJDK**, **Android SDK & NDK Tools**. ~2GB, 10–30 minutes.

Verified at HEAD: `ProjectVersion.txt` is `6000.4.1f1` and
`Editor/Data/PlaybackEngines/` contains only `windowsstandalonesupport`. Until this is done no APK
can be produced at all.

Then **switch platform to Android once** (`File ▸ Build Profiles ▸ Android ▸ Switch Platform`).
The first switch re-imports every texture in a 945MB `Assets/`. **Do this the day before, not on
build day.**

### 2. Confirm Addressables content actually builds — the one that decides whether the study happens

**Half of this is already done.** Verified on disk at HEAD: `AddressableAssetSettings.asset` line 61
is now `m_BuildAddressablesWithPlayerBuild: 1` (**BuildWithPlayer**, set in `e56643a`), so the
player build produces the content automatically. Do not change it back — with `2` it was possible to
build an APK that installs, boots, plays Boot, the menu, the map, Story Select and the whole Reader,
and then hits a race that never starts, with no symptom until a learner is holding the tablet.

**The other half has never happened.** `Library/com.unity.addressables/` still contains only
`AddressablesBuildTEP.json` (a play-mode entry, not a content build), there is no `aa/` folder for
any platform, and no `Assets/StreamingAssets`. The preflight's second Addressables check correctly
stays red until a build actually runs.

Also reported by the preflight in `d4f2544`, and **not independently verified here**: 13 of 15
bundled Addressables groups are empty on disk, including all six the race needs. That commit filed
it as a WARN rather than a blocker on the reasoning that `OnEnable` very likely re-seeds them.
**Treat it as a thing to look at on the first build**, not as a settled non-issue.

**How you will know it worked:** the race starts. If you see the "this race is not ready yet" card
with a button back to Story Select, content did not ship.

### 3. Run `SummaRace ▸ Build Preflight`

`Assets/_Game/Editor/BuildPreflight.cs`. Read-only, never throws, prints to Console, a dialog and
the clipboard. It checks the Android module, the build-scene list and index 0, player settings,
the app icon, Addressables content, all 30 stories through the real loader, banned packages,
offline violations, and unguarded `using UnityEditor;` in player code.

**Fix every ✖ before building.** Read every ▲ and accept it deliberately.

### 4. Build ONE APK

`File ▸ Build Profiles ▸ Android`. **Build App Bundle OFF** (an `.aab` cannot be sideloaded).
**Development Build OFF** for the study build. Output to a folder outside the repo. Name it
`SummaRace_v1.0_vc1_YYYYMMDD.apk`. First IL2CPP build takes 20–45 minutes.

Then `git tag study-build-v1`, and **back up `C:\Users\<you>\.android\debug.keystore` next to the
APK**. The APK is debug-signed, that keystore is per-machine, and a mid-study rebuild from a
different PC will refuse to install over the deployed app — the only way through is uninstall,
which erases that tablet's profiles and every unexported log. **Build every study APK on the same
machine.**

### 5. Sideload to one tablet and smoke-test it

Full checklist in `SummaRace_Build_And_Release.md` §8. What matters most, in order:

- **The race actually starts and finishes.** If it does not, go back to step 2.
- **The full loop on a story that is not `s01`.** 27 of the 30 have never been played once.
- **Airplane mode + Wi-Fi off**, then play a whole story. 100% offline is non-negotiable.
- **Android BACK does not close the app.** `Core/BackButtonGuard` was inert until `2380de5` — it
  read `escapeKey.wasPressedThisFrame` and asserted that "marked it handled", which is not a thing.
  It now uses `Application.wantsToQuit`, the documented hook. **`wantsToQuit` is never raised on
  Play-mode exit, so this has only ever been verified by reading it.** Test it on the tablet.
- **A tap on the screen changes lane** — touch is the one path a desktop test cannot exercise.
- The launcher icon is our crown/SUMMA·RACE!, not a cat.
- **The soft keyboard on Summary and Name Entry can be dismissed** (both now carry a DONE TYPING
  chip; Name Entry's was added in `c343244` and has never run on a device with a real keyboard).

### 6. Set the teacher PIN **and each learner's participant code**, before any child touches it

**6a — the PIN.** Main Menu ▸ teacher corner ▸ *"Teacher setup: choose a PIN"* ▸ 4+ digits ▸
confirm. **Write the PIN down per tablet, tied to an asset tag.** Only a salted hash is stored; it
cannot be read back. If a curious child sets the PIN first you have lost teacher control of that
device, and the only way back is the recovery gesture, which erases every profile and every log on
it. Procedure: `SummaRace_Study_Operations_Runbook.md` §6.1.

**6b — the participant code.** Each learner carries a **participant code — your own id from that
child's paper booklet** — set by *you* behind the PIN, never typed by the child. It is the join key
between the app's logs and the paper pretest/posttest, and it is stamped on **every log row** as
well as the export roster. Before it existed the only link was `displayName`, a name a nine-year-old
typed.

Asked for at: passing the PIN with a codeless active learner (diverts to the prompt); **"+ New
learner"** (asks *before* Name Entry); duplicates refused while you still hold both booklets.
Format 2–12 letters or numbers, e.g. `P07`. Export warns on missing or duplicated codes.

Since `a760ec0` the export **back-fills** the code onto rows written before it was set, by targeted
string replacement rather than re-serialisation. So a code set late is recoverable — but set them
at install anyway.

### 7. Pull one export over USB **before day 1**, not after

Teacher menu ▸ Export logs → writes `export_<stamp>.jsonl` plus `export_<stamp>_learners.json`
(the roster) to `Application.persistentDataPath`. **Read the status line after tapping.** Since
`a760ec0` a failed write says so instead of reporting "No logs to export yet" — which is what it
used to say when the disk was full or permission was denied, on the one control that retrieves the
entire dataset.

On **Android 11+ the `Android/data` folder is frequently hidden from MTP**, so a USB drag may
simply not show it. The reliable route is adb:

```
adb pull /storage/emulated/0/Android/data/com.orbitsdev.summarace/files/export_<stamp>.jsonl
```

**These logs are the entire in-app dataset.** No cloud backup, no second copy, and the PIN recovery
gesture destroys them. Prove the retrieval path while it costs you nothing.

### 8. Install the *same single APK file* on all 40 tablets

Do not rebuild per tablet. Every learner must run byte-identical code and content or the study is
comparing two instruments.

---

## 3. Decisions waiting on you

Seven. D1 goes out today because its answer arrives on someone else's clock.

### D1. Freeze the content and email the researcher — send this first

**Where it stands.** Four questions, none of which need a developer, and all of them gate work:

1. **~130 strings of this thesis instrument were rewritten by machines and no researcher has read
   them.** F44 rewrote 120 element sets, F47ⓖ extended 112 Reader distractors, F53 rewrote 6 page
   questions, and `s01_easy`'s questions were AI-authored outright. Each pass had a good reason
   (see §4c) and two of them are documented as having silently changed what a story *means* before
   being caught. This is `SummaRace_Critique.md` §5 and it is the largest risk in the project that
   is not the missing APK.
2. **The race can still be passed from memory.** Measured: **84.0%** passable by remembering the
   Reader's correct answer for a slot and picking the most similar race card, against a 34.0%
   misaligned control. A content pass in `8cba732` brought it to **59.3%**, with **25 of 150** race
   `correct` lines still byte-identical to the Reader's. That work is explicitly recorded as
   unfinished. `raceFirstPickCorrect` is the star count and the headline logged measure, so at 59%
   a memoriser still beats a reader. **Verified at HEAD: no story JSON has changed since `8cba732`,
   so 59.3% is the live figure.** The researcher's call is whether that is acceptable, a stated
   limitation, or worth paraphrasing the ~50 highest-similarity `correct` lines.
3. `s05_hard` and `s06_hard` are the same story (D7 below).
4. `s03_easy` is reported mislabelled in `SummaRace_Critique.md`. **I did not verify this claim.**

**Recommendation: send all four in one email today.** Nothing on this page is more time-sensitive,
because you are not the one who answers it.

### D2. Reading time for the slowest readers — `RacePreviewLeadSeconds` 12 → 17

**This decision changed shape and the lever is no longer the one earlier documents name.** Verified
in `GameRules` at HEAD: `RaceSecondsPerGate` is **20**, not 12, and it now sets gate *spacing*, not
reading time. The F55 pass (`8cba732`) decoupled the two: the option preview is armed when a gate is
*scheduled* and revealed on a distance derived from the live run speed, so the reading window is a
**guaranteed `RacePreviewLeadSeconds` = 12 seconds** rather than the old 9.0–13.7s range that
depended on where their track happened to spawn a segment. `NextGateGap` enforces
`(lead + RaceQuietRunSeconds 6) × speed` as a hard floor, so no difficulty and no speed can take
the window away.

12s meets a 100 wpm reader's ~11.6s budget for the ~102 characters a gate carries. It does not meet
the ~16.5s a 70 wpm reader needs, and this study exists for the slower readers.

**Trade-off, and a trap.** Raising the lead to 17 raises the floor to 23s per gate. `bySpeed` is
`20 × difficulty × speed`, i.e. **25s easy / 20s average / 18s hard** — so at a 23s floor *average
and hard collapse onto the same cadence* and difficulty goes inert on two of three, which is exactly
the defect F46ⓑ had to fix. Keeping them separate needs `RaceSecondsPerGate` raised past ~29 as
well, which lengthens the race again.

Cost either way: +5s per gate × 5 gates = **+25s per race, ~+75s per session of three stories.**

For scale, the race is already much longer than any earlier document records. My arithmetic from the
constants at HEAD (`RaceFirstGateDistance` 200m; minSpeed 10, maxSpeed 30, accel 0.2 m/s² read from
`TrackManager` and `MainSummaRace.unity`): roughly **1,700m and ~95 seconds of running** on average
difficulty, against the ~840m recorded in `CLAUDE.md`. **That is arithmetic, not a measurement** —
time one race in the smoke test before spending classroom minutes on this decision.

**Recommendation: play one race first, then decide.** The 12s guarantee is a much better position
than the one the old documents describe, and the length has already roughly doubled without anyone
timing it. If you do raise it, raise `RaceSecondsPerGate` with it and playtest.

### D3. One tablet per learner, or shared tablets

**Where it stands.** Both are supported: one `LearnerProfile` and one `logs/<learnerId>.jsonl` per
child, with teacher-gated learner switching (Main Menu ▸ teacher corner ▸ PIN ▸ "Switch learner").
Since `0752fb3` a learner handover flushes the run in flight with its own `abandonReason` token
rather than an empty one, so a shared tablet no longer manufactures a steady stream of rows that
look byte-identical to a dead battery.

**Trade-off.** Shared tablets need a switch before every hand-off, and a missed switch attributes
one child's run to another, unfixably. One-per-learner costs 40 devices and 40 PINs.

**Recommendation: one tablet per learner if you have 40 devices.** Otherwise use the shared path,
make the Main Menu's **"Playing as \<name\>"** chip a mandatory spoken check before every hand-off,
and never change the policy mid-study.

### D4. `GameRules.StarsTwoMin` 4 → 3

**Where it stands.** Verified at HEAD: `StarsTwoMin = 4`, `StarsThreeMin = 5`. So 3★ needs 5/5 first
picks, 2★ needs 4/5, and **everything at 3/5 or below shows the same one star** — a learner who got
three of five right sees exactly what a learner who got none right sees, thirty times.

**Trade-off.** It changes nothing that is logged: `raceFirstPickCorrect` is the measure and stars are
display. The argument against is that it makes the star count a weaker signal to the teacher.

**Recommendation: change it to 3.** One constant, no measure touched. Raised by
`SummaRace_Critique.md` §6.5.

### D5. Is a passive run — never steering, collecting all five gates — acceptable?

**Where it stands.** All three lanes always carry a card, so every gate is a forced choice and a
learner who never touches the screen still collects five. Which lane holds the correct card is
shuffled per gate, so a passive run scores at chance (~1.67 of 5).

**Recommendation: accept it, no code change — and filter for it in analysis.** Schema 5 logs
`racePicks[].lane` for every card touched. A learner whose five picks are all `lane: 1` never
steered. One line in your cleaning script.

### D6. The 27 placeholder hero images

**Where it stands.** All 30 resolve, so nothing is broken. Verified on disk: `s01_*.png` are the
real illustrations at ~1.8MB each; `s02`–`s10` are the 42–47KB generated fills — **blank
blue-to-green gradients, not weak art**.

**Recommendation: do it only after §2 steps 1–8 are complete and the smoke test passed.** It is the
biggest visual win available and the least important thing on this page. A thesis is not marked on
hero art; a study that could not be built is fatal. 27 ready-to-paste prompts and the overwrite rule
are in `SummaRace_Asset_Shopping_List.md` §2.

### D7. `s05_hard` and `s06_hard` are the same story

**Where it stands.** Both titled "In Grandfather's Day", both starring Sharr, Kaze and Grandfather.
**Not a pipeline bug** — the researcher's source doc carries that title under DAY 5 and again under
DAY 6, with different SWBST treatments and different passages.

**Recommendation: leave the content alone and record it as a known limitation.** It affects only
learners who complete HARD in both sessions 5 and 6, is identifiable by `storyId`, and came from the
source material. The cheap operational fix, if the researcher would rather avoid it, is to not
unlock session 6 HARD — no code change. Fold this into the D1 email.

---

## 4. Known-broken and unverified — nothing hidden

### 4a. Never measured, at all

| # | Thing | Why it is unmeasured |
|---|---|---|
| 1 | **Whether the race runs at all in a player** | Addressables content has never been built for any platform. The setting that makes a player build produce it is now on, and has never been exercised. §2 step 2. |
| 2 | **APK size** vs the 300MB cap | No APK has ever been built. |
| 3 | **30fps in the race** on the 2GB / Android 8 floor device | Same. Never observed on hardware. |
| 4 | **27 of 30 stories, end to end** | Never played once, by anyone, in any mode. |
| 5 | **Android BACK on a device** | `Application.wantsToQuit` is not raised on Play-mode exit, so `BackButtonGuard` has only ever been read, never run. The previous implementation was inert for the same reason nobody could test it. |
| 6 | **Touch: tap-to-lane, and both soft-keyboard dismiss chips** | Code path and compile only. A desktop test cannot exercise any of it. |
| 7 | **Every tween and pop-in outside the race** | `PanelIntro`, `UIFloat`, `ButtonSquash`, PrimeTween run only in Play mode. |
| 8 | **Which tablet the study uses** | The readability audit's verdicts swing on 7" vs 10.1", and `b399e03`'s aspect-ratio fix was derived for 4:3 / 16:10 / 9:16 / 20:9 without knowing which one ships. Cheapest unknown to resolve. |
| 9 | **End-to-end session timing** | The runbook's 15–25 min/session is an estimate and the race got materially longer in `8cba732` (D2). Do one timed dry run. |
| 10 | **Draw-call cost of the race world** | `63e1be3` + `c03c2cd` added greenery, a second theme and a mixed-family road. Author's estimate: up to **+200 draw calls worst case**, unmeasured on any device. If the race misses 30fps, **halve `GameRules.RaceMaxSceneryPerSegment` (14) first.** |
| 11 | **Test suite count at HEAD** | The "45/45 green" figure predates two new fixtures (`ExportBackfillTests`, `NarrationArrayTests`). I cannot run Unity's test runner from here. **Re-run it; do not quote 45/45.** |
| 12 | **Whether the 13 empty Addressables groups matter** | Reported by the preflight (`d4f2544`), filed as WARN on the reasoning that Unity re-seeds them on `OnEnable`. Not independently verified. |

### 4b. Known limitations that will still be there on day 1

- **The patrol cop is cut.** `GameRules.RacePatrolEnabled = false` (`c36d819`, owner-driven). Three
  placements each traded one defect for another, and the cause is the approved chase camera — 5m
  back, 4m up, ~15° — under which a ground-level chaser is either inside the runner or below the
  frame. Nothing is lost: it never caught anyone (`timesCaught` stays 0 by design, GDD D7), it
  carried no rule, and the wrong-answer beat is carried by the amber vignette and the feedback line.
  The tuning constants and the whole `UpdatePatrol` path are kept so reviving it alongside a camera
  change does not start from nothing.
- **A wrong answer no longer stages a pickup.** The re-present is gone (`8cba732`, owner-driven): a
  wrong pick or a missed gate now shows the correct answer plainly for **2.2s** on the panel the
  learner already reads from, then the run moves on. `ScheduleRepresent` was deleted outright in
  `76612eb` rather than left unreferenced, because calling it would have logged a collection against
  a question whose first pick was already closed.
- **The render pipeline is not the one any older document assumes.** Every quality level has
  `customRenderPipeline: {fileID: 0}` and `GraphicsSettings` points at **Trash Dash's**
  `RenderingPipeline.asset`. `Mobile_RPAsset` / `PC_RPAsset` are assigned to nothing. `renderScale`
  is 1.0, main-light shadows are on with a 2048 map and 4 cascades — a full shadow atlas rendered
  into a corridor where nothing can receive a shadow. Free frame rate is sitting there, but it is a
  project-wide rendering change that needs a playtest. **Do not reassign `Mobile_RPAsset`** — it
  turns HDR back on, a regression on a tiler.
- **`Race.unity` (legacy) is still build index 6** with zero call sites, carrying 51.5MB of source
  assets nothing else uses. Harmless, but free APK weight.
- **Trash Dash's inactive objects are still inside `MainSummaRace`** (GameOver, Loadout, Highscore,
  leaderboard and store buttons). The five buttons have empty click handlers and are
  non-interactable. Hidden is not removed. **Their `UICamera/Game` chrome is load-bearing —
  `GameState.UpdateUI()` dereferences it every frame. Do not delete it.**
- **`Assets/MobileDependencyResolver/` is still on disk** (verified). Editor-only, resolves nothing,
  injects nothing into Gradle. It self-resurrects after a domain reload; delete it with the Editor
  closed if you want it gone. The preflight will keep reporting it.
- **`RaceBriefingBody` still never mentions the option panel.** Verified at HEAD, it reads *"Tap or
  swipe left and right to move!"* — it does not tell the learner that the only readable copy of the
  three answers is the board at the top of the screen, and it does not mention the middle lane. The
  panel columns are also not tappable. `SummaRace_Critique.md` §6.6 argues this is the single change
  that improves both the experience and the measure; it is not done.
- **`readingSeconds` / `arrangeSeconds` / `summarySeconds` include tablet-locked time.**
  Unrecoverable after the fact. `SummaRace_Critique.md` §2.6.
- **`GameManager.LastSummaryText` is written and read by nobody** — Results never shows the learner
  the sentence they wrote. Verified at HEAD.

### 4c. What landed in the last fifteen commits — so you do not redo it

Every row verified in source at HEAD. **If another document tells you one of these is open, that
document is stale.**

| Landed | What it was |
|---|---|
| **All six contrast failures are closed** (`f260977`) | And the audit's own basis was wrong: the Reader question card and Summary reference card are the kit's *cream* `Daily Reward pannel` (0.977, 0.929, 0.835), not the white every ratio was computed against, so every published number was ~15% optimistic. Re-derived against the real background: Reader "Not quite" 2.54→**4.89:1**, Reader praise 3.59→**5.57:1**, Summary SWBST words 1.79–2.89→**5.18–7.31:1**, StorySelect "Locked" 1.72→**6.86:1**, StorySelect hint 1.85→**6.09:1**, Arrange slot labels 3.30–4.44→**4.88–6.16:1**. FINISH card 1.74→**6.77:1** (`c03c2cd`). Two "silent overflow" bugs the audit reported turned out **not to exist** when re-derived with real TTF advances. |
| **Device Budget was run** (`c03c2cd` + `24d682e`) | ~**127MB** of resident RAM recovered on the 2GB floor. The earlier audit had found six DecompressOnLoad stems and put the pool at 178.2MB; there was a **seventh** (`STEMSSpeed2Choir`, stereo, 42.3MB), so the real figure was **220.6MB**. Verified on disk today: `STEMSMainTrackMono.ogg.meta` is `loadType: 1` with `preloadAudioData: 0`, `music_menu.mp3.meta` is `loadType: 2` (streaming). Note `24d682e` exists because the first commit **missed `Assets/Sounds`, where the stems live** — the fix was documented, measured and reported, and not in the build. |
| **`TargetFrameRate` 60 → 30** (`0752fb3`) | Verified. It was already a half-measure — their `MusicPlayer` forces 30 when the race loads and never puts it back, so the app ran at two rates depending on the scene. |
| **Android graphics APIs: Vulkan-first → OpenGLES3 only** (`321dc23`) | Verified: `AndroidPlayer m_APIs: 0b000000` (= 11, OpenGLES3), `m_Automatic: 0`. Nothing has ever rendered on Vulkan here — every hour of playtesting was desktop D3D11 — the floor device is where Vulkan drivers are least reliable, and the race leans on two custom shaders. Also halves shader-variant compilation against a 300MB cap. |
| **`Assets/Link.xml` now preserves the crypto assemblies** (`321dc23`) | `SHA256.Create()` resolves through `CryptoConfig` by **reflection**, and IL2CPP managed stripping was free to remove it. `TeacherGate.Hash` sits behind setting the PIN, unlocking a session and **exporting the logs**; a throw inside a UI callback is swallowed, so the researcher would tap Export and see nothing happen. Plus a caught-and-logged fallback and a `SetPin` guard (a null hash used to be *stored*, and null == null passes the read-back check, so the screen reported a PIN set on a tablet that had none). |
| **`androidPredictiveBackSupport` 1 → 0** (`321dc23`) | Verified. It fought `BackButtonGuard`: either the system played its full closing animation and snapped back mid-story on the same edge swipe the race trains, or the gesture finished the activity without routing through the hook at all. |
| **`SceneLoader` was silently dropping every rescue raised from a scene's `Start()`** (`a760ec0`) | F46ⓕ's fix had been **inert**, and its comment was wrong about Unity's player loop: scene integration and the new scene's `Start()` both run in `EarlyUpdate`, and a coroutine resumed by `yield return null` continues later the same frame in Update — so clearing the flag anywhere inside that coroutine cannot beat a `Start()`. What it dropped: the four "never a dead end" rescues in Reader/Arrange/Summary/Results, each of which returns immediately afterwards, past the code that wires its buttons. Requests are now remembered rather than dropped. |
| **MainMenu returned before wiring a single button** (`a760ec0`) | On the assumption its Name Entry redirect always takes. Combined with the above it left a fully drawn menu where TAP TO START and the teacher corner were both inert and there was no music — reachable from the post-study wipe and from the documented PIN recovery, both of which mint a blank profile and return there. |
| **Four saves reported success without checking** (`a760ec0`) | `TryWrite` swallows a total failure into a `SaveFailed` event that has **eight raise sites and not one subscriber**. Affected: the participant code (in-memory value now rolls back on failure), "session N is now open" vs "all sessions already open" (same return value — a teacher told the wrong one hands over a tablet still on the previous session), stars, and name/avatar. **And Export reported a failed write — disk full, permission denied — as "No logs to export yet."** |
| **Export back-fills the participant code** (`a760ec0`) | Onto rows written before it was set, by targeted string replacement, never by re-serialising: an older row carries fields this build's `SessionLog` may not declare and a `JsonUtility` round trip would drop them silently. That coupling is now asserted by a test. |
| **The post-study wipe could put an erased child's data back** (`c343244`) | `DeleteAllData` is followed immediately by `InitProfiles`, so a play-through still in flight would write its next row afterwards — and `AppendLog` calls `Directory.CreateDirectory`, recreating the logs folder for a learner just erased, on a tablet the researcher had been told was clean. New `AllDataErased` event raised **before** anything is removed; `SessionLogService` drops the run. |
| **The wipe also left Trash Dash's own `save.bin` behind** (`2380de5`) | Their `PlayerData` writes `persistentDataPath/save.bin` every 300m, carrying that child's rank, coins, highscores and mission state. Pseudonymous, so not a disclosure — but the erase *is* the consent promise. Now deleted by name. |
| **Pause chip 29dp → 48dp** (`c343244`) | It is the only way out of a run and was a 29dp target 8px from the screen edge, i.e. inside Android's ~20dp back-gesture strip. 144×144 = 48dp exactly at xxhdpi. Board 880 + chip 144 + a 60px gesture margin = 1084 in a 1080-wide reference, so a chip that is simultaneously 48dp, clear of the strip and clear of the tracker **does not exist** — the tracker (non-interactive) slid left and the gesture strip is the constraint traded away, since `BackButtonGuard` already swallows BACK. **9.3dp of residual margin.** |
| **The HUD banner was drawn under the option-preview board** (`c343244`) | Entirely inside it, and the board draws over it — so a learner who got the last element wrong lost the "Run to the FINISH!" instruction behind a wooden board for 2.2s, on the one gate where that instruction is the whole point. Now anchored to the board's underside via a shared constant. |
| **Name Entry had no way to dismiss the keyboard** (`c343244`) | The *first* screen a learner ever sees put the soft keyboard over all four runners **and** "LET'S GO!". Summary's DONE TYPING chip was ported rather than reinvented, same wording, so a child learns the gesture once. |
| **The Results title overran the trophy in 22 of 30 stories** (`321dc23`) | Measured with the project's own font metrics. Boxed clear with autosize; all 30 fit, closest approach 20px. |
| **The tracker and the reading band collided on tablet aspects** (`b399e03`) | ⚠️ **Recorded one commit earlier as "cosmetic, deliberately unfixed at ~1.3px on 16:10". That was wrong and the number was for the wrong device.** The tracker and pause chip were pinned in **pixels** while the F47ⓐ reading band is anchored in **fractions**; with `matchWidthOrHeight = 0` the canvas is always 1080 wide but its *height* tracks the aspect. Measured: 9:16 clear, **16:10 −1px**, **4:3 −34px** (the bottom ~22% of every tracker plaque, clipping the five words through their descenders) **and −50px of the pause chip**. Cheap Android 8 tablets are commonly 4:3 or 16:10. Both now anchored by their bottom edge to a fraction; at 9:16 the layout is byte-for-byte what was playtested. |
| **The briefing's unbounded waits** (`a760ec0`, `76612eb`) | See §1. Also: the escape screen reached its state by `yield break`ing out of `Start()` *before* the line that hides the runner kit's own HUD, so the learner read "This race is not ready yet" under Trash Dash's coins, gems, score, distance, multiplier, life hearts and pause button. Hidden first now. |
| **`EventBus.Raise` did not guard its subscribers** (`321dc23`) | Raise sites are gameplay code, usually inside coroutines, so a throwing handler killed the **caller's** coroutine at the raise point. The listener most likely to throw is `SessionLogService`, whose whole job is recording data it must never be able to stop a learner to collect. |
| **`ArrangeController.VerifyRoutine` could strand `_busy = true`** (`321dc23`) | On the one screen a story cannot get past, leaving VERIFY, UNDO, every slot and every piece dead with no exit. |
| **`ParticipantCodes.Normalize` truncated instead of refusing** (`321dc23`) | It capped at `MaxLength`, so `IsAcceptable`'s `> MaxLength` test could never fire: a 14-character code was silently cut to its first 12 and accepted. Two booklet codes sharing a 12-character prefix would have collapsed into one participant — precisely what the field exists to prevent — with the teacher told it saved. Also `InitProfiles` gained a null-element guard; it runs inside `Bootstrapper.Awake`, so a corrupted `profiles.json` took out `[Core]` before any screen loaded. |
| **Arrange now logs the order the learner built** (`34b8c7a`, schema 4→**5**) | `arrangeOrders`: one five-character string per VERIFY press, position = slot, character = element. `"01234"` is correct, `"01324"` is the But/So swap. The sequencing rung recorded attempts, solved and assisted — never the *sequence* — so "children systematically swap But and So" was unavailable and unrecoverable once the study ran. The assist deliberately logs nothing, so the game's answer is never recorded as the child's. |
| **Instructional narration now exists** (`0752fb3`) | 10 clips for interface strings — Arrange title + how-to, Summary title + hint, the 5 loading tips, the race briefing — verified wired through `AudioManager.PlayVoice` in `ArrangeController`, `SummaryController`, `SceneLoader` and `EndlessRaceDirector`. The 150 story-page clips already existed. Every scored item stays unnarrated, so the measure is untouched. |
| **`fit.py` reads the tracker geometry out of the source** (`0752fb3`) | It had two transcribed literals under a header promising "every geometry number below is READ, not assumed", and the tracker one had drifted: F47 widened the plaque 138→160 and `fit.py` went on reporting that failure against the old 138 for every run since. **A stale FAIL is worse than no check.** Now parsed from `EndlessRaceDirector.cs`, scoped to the method body, raising rather than falling back. Two traps found doing it, each producing a confident wrong number rather than an error. |
| **`HasData` did not know about `racePicks` or `arrangeOrders`** (`0752fb3`) | A run carrying only those would have been discarded as "empty" by the check whose own comment promises abandoned runs always carry something. |
| **A learner handover was byte-identical to a dead battery** (`0752fb3`) | Empty `abandonReason`. On a shared tablet that happens every time a child finishes, so the data would have carried a steady stream of false dropouts in exactly the configuration where someone might report a dropout rate. It has its own token now. |
| **The race world stopped being four buildings** (`c03c2cd`) | Five of the ten worlds drew from the same **four** Suburbs prefabs across ~55 placements. Worlds now draw a weighted **mix** of families in blocks measured in metres of laid track: **10–20 distinct prefabs per race, mean ~16.** Zero new prefabs, live segment count untouched. |
| **Addressables content is now coupled to the player build** (`e56643a`) | `m_BuildAddressablesWithPlayerBuild` 2 → **1**. Slower builds, but there is no longer a way to produce a plausible-looking APK with no content in it. §2 step 2. |
| **The preflight had eight blind spots** (`d4f2544`) | Each closed and exercised offline against the real repo with 32 mutation assertions. It matched only `using UnityEditor` and fully-qualified `UnityEditor.`, so an **unqualified `AssetDatabase.` or `[MenuItem]`** in a file whose using *is* guarded was invisible — and that fails the player compile while the Editor stays silent, which has already bitten this project three times (45 editor names now, 0 hits today). It walked `Assets/` only, never `Packages/` — this project has a git-URL package whose runtime asmdef compiles into the player (scope 222 → 260 scripts). `#else` was always treated as unguarded. The offline check omitted `Analytics.`, `Advertisement.`, `Purchasing`, `Social.`, `UnityEngine.Networking` and `Firebase`, so it reported PASS while the shipping scene reached files full of them — right only by accident, since those sit behind defines Unity adds **automatically** the moment the matching package returns, which has happened twice here. It searched scene YAML only, so a script on a **prefab** used by a scene was invisible (found `AdsForMission` on `AddMissionButton.prefab`, referenced by `MainSummaRace`). Four checks added that did not exist: predictive-back vs `BackButtonGuard`, Android graphics APIs decoded from the hex blob, `Link.xml` crypto preservation, and fog stripping. |
| **`Assets/Scripts/OpenURL.cs` deleted** (`d4f2544`) | Verified gone. Referenced only by their `Main.unity`/`Start.unity`, neither in Build Settings — but it **compiled into the player** and was the last live `Application.OpenURL` there, guaranteeing a permanent FAIL on the offline check. A check that is always red is a check nobody reads. Zero live networking calls in player code now; the remaining hits are all in `includePlatforms: ["Editor"]` assemblies. |
| **Offline compliance is a test now, not a checklist item** (`b5a7003`) | `OfflineComplianceTests` guards the *source* of the regression rather than its symptom. Trash Dash's shop, leaderboard and rewarded-ad code is still in the project at **49 call sites**, inert only because `UNITY_ADS` / `UNITY_ANALYTICS` / `UNITY_PURCHASING` are undefined — and **nobody has to touch one of those files to arm them**, because Unity defines those symbols automatically when the matching package is present. Not hypothetical: the full Endless Runner import silently added all three to the manifest, and Google's `BillingMode.json` re-created itself after being deleted once. Neither was noticeable by playing the game. Verified: 0 banned packages, Android defines are `UNITY_POST_PROCESSING_STACK_V2` only, no `BillingMode.json`, every `m_Enabled` in `UnityConnectSettings` is 0. |
| **`AudioKeys.VoLoadingTips` was unguarded** (`902e078`) | `ResourceContractTests` proves every audio key resolves by reflecting over `const string` fields, so the ten new instructional constants were covered for free — but `VoLoadingTips` is a `static readonly string[]`, invisible to reflection, and it is the one place where an **index carries meaning** (entry *i* is the spoken form of `GameText.LoadingTips[i]`). A mismatch does not throw: `SceneLoader` bounds-checks, so it degrades to silence or to the wrong SWBST definition read aloud over the right one on screen — and the learner who cannot read the tip is the one person unable to notice. Verified alongside: **31/31** `AudioKeys` constants, 30/30 hero images, 150/150 story clips, and the ten new `.meta` files are CompressedInMemory / no preload. |
| **The Main Menu's "Playing as \<name\>" was navy on dark brown, half cut by the ground edge** (`a4ed8bd`) | Placed on the stated assumption that `bg_playground` is a bright sky backdrop. It is not. Found by playing the game from Boot rather than reading the code. It now carries its own chip and stops depending on what is behind it. |

### 4d. Where the documents disagree — and which one is right

Checked against source at HEAD rather than reconciled on the page.

| Disagreement | Which is right | Evidence |
|---|---|---|
| **`SummaRace_Readability_And_Accessibility_Audit.md` item 6, `SummaRace_Pro_Review.md` §6 item 10, and the previous versions of this page and the Finalization Plan all say "four contrast failures still open"** | **All four are closed, and there were six.** Landed in `f260977`, verified in source at HEAD: `ReaderController.cs:74` `(0.62, 0.32, 0.02)`; `SummaryController.cs:71` `InkHexForIndex`; `EndlessRaceDirector.cs:921` deep brown `(0.32, 0.19, 0.02)` on the gold FINISH card; `StorySelectController.cs:71` `LockedHintInk`. **Do not spend time on these.** The line numbers in those documents no longer resolve, which is how they came to read stale locations as open items. |
| **`SummaRace_Build_And_Release.md` §11, `SummaRace_Device_Budget.md`, `SummaRace_Pro_Review.md`, `SummaRace_Critique.md` §6.2, `SummaRace_Audio_Audit.md` and the previous Finalization Plan all say the Device Budget script "has not been run" and that ~156MB of PCM is resident** | **It was run, in `c03c2cd` + `24d682e`.** Verified on disk: `Assets/Sounds/Stems/STEMSMainTrackMono.ogg.meta` is `loadType: 1`, `preloadAudioData: 0`, `loadInBackground: 1`; `music_menu.mp3.meta` is `loadType: 2` (streaming). ~127MB recovered, and the pool was **220.6MB**, not 178.2MB — there was a seventh stem nobody had found. `SummaRace_Device_Budget.md` is now a record of what the problem was, not of what is outstanding. |
| **Every document that says `GameRules.TargetFrameRate` is 60** (Device Budget item 8, Build & Release, Pro Review, Critique, previous Plan R4) | **It is 30** (`0752fb3`), verified in `GameRules.cs`. |
| **`SummaRace_Data_Dictionary.md` "documents schema 2 / is two schemas behind"** (previous versions of this page and the Plan) | **Stale in the opposite direction — the dictionary is current.** Its header states schema **5**, verified field by field against `04a4040`, all 41 keys documented, and it was updated again in `0752fb3` and `c343244`. `SessionLogService.SchemaVersion` = **5**. It also fixed a pandas snippet whose `groupby.first()` could assemble a row that never existed. **Nothing to do here.** |
| **`SummaRace_Critique.md` §6.4 — "make delete all data actually delete all the data"** | **Done, in `2380de5`**, one commit after the Critique was written. `SaveManager` deletes `save.bin` by name. `c343244` then closed the *other* half nobody had noticed: the wipe could recreate the logs folder for an erased learner. |
| **`SummaRace_Critique.md` §6.5 and §2.2 — `RaceSecondsPerGate` 12 → 17** | **The lever moved.** `RaceSecondsPerGate` is **20** and now sets gate spacing; reading time is `RacePreviewLeadSeconds` (12s, *guaranteed*, floored into the gap). See D2 — the change is still available but has a different name and a different trap. |
| **`SummaRace_Critique.md` §2.2 — the race is 84% passable from memory** | **Right when written, and improved but not closed.** `8cba732` brought it to **59.3%** with 25 of 150 race `correct` lines still byte-identical to the Reader's, and records the work as unfinished. Verified: no story JSON has changed since. **This is D1 and it is the most important open item in the project that is not the APK.** |
| **`SummaRace_Critique.md` §3.4 — does BACK close the app?** | **Genuinely unknown, and the Critique is right to ask.** F51's guard was inert (it read an input control and asserted that consumed the event); `2380de5` replaced it with `Application.wantsToQuit`. That hook is never raised on Play-mode exit, so **this has only ever been read, never run.** §2 step 5. |
| **`CLAUDE.md`'s F12/F34/F39/F47ⓑ patrol rows, and every tuning number in `GameRules`' patrol block** | **The patrol is cut** (`c36d819`, `RacePatrolEnabled = false`). The constants and `UpdatePatrol` are kept deliberately, for a revival alongside a camera change. Do not tune them. |
| **Anything describing the wrong-answer re-present, or `GameText.RaceWrongFeedback`** | **Both gone** (`8cba732`, `76612eb`). A wrong pick shows the correct answer for 2.2s on the option panel. `RaceWrongFeedback` said *"get the glowing card!"* — a card that no longer exists, i.e. an instruction to do something impossible, on the beat whose whole job is to reassure. |
| **`SummaRace_Pro_Review.md` §6 item 9, "log Arrange placements as schema 3 → 4"** | **Done as schema 5** (`34b8c7a`). Schema 4 was spent on `participantCode`. |
| **`SummaRace_Study_Operations_Runbook.md` and older text saying the race has no pause/quit** | **Stale.** Pause chip top-right, full-screen overlay with KEEP RUNNING and a two-tap LEAVE RACE (auto-disarming after `RaceLeaveConfirmSeconds` 4s), routing to Story Select through the partial-run mechanism. The chip is now 48dp (`c343244`). Resume uses `StartMove(false)`, so a pause never reseeds `minSpeed` and is never a penalty. |
| **`SummaRace_Build_And_Release.md` §9's older figures** (`Race.unity` ~23MB, ~36MB of Vorbis, Bloom as a frame-rate lever) | **`SummaRace_Device_Budget.md` is right on all three; corrected in place.** `Race.unity`'s exclusive dependency closure is **51.5MB** (the scene *file* is 119KB), and **`RacePostFx.asset`'s `components:` holds a single null entry with zero post-processing cameras in `MainSummaRace` — the Bloom lever does not exist on the shipping path.** Do not offer it. |
| **Anything claiming a saved profile can be lost to a crash** | **Closed in `e5966be`.** Atomic writes (temp → `File.Replace`, `.bak` kept, reads fall back). |
| **`CLAUDE.md` generally** | **It stops at F54 (`63e1be3`).** Everything in §4c above is newer than it. It is the engineering log, not a plan. |

---

## 5. The documents, and when to read each one

| Document | What it is for | When you read it |
|---|---|---|
| **`SummaRace_Owner_Handover.md`** (this) | The front door. State, critical path, your decisions, what is broken. | Now, and any time you lose the thread. |
| **`SummaRace_Finalization_Plan.md`** | The live plan: what remains, with realistic effort. | Right after this page. The only other doc that is meant to change as work lands. |
| **`SummaRace_Critique.md`** | An outside senior-developer read that **argues with this page**. Its §5 — a thesis instrument's content rewritten at scale by machines without researcher review — is the most important thing in it and is decision D1. Its §2.2 measurement of the memoriser route is the second. | Once, in full, when you have an hour. **Its §6.2 (Device Budget), §6.4 (delete-all-data) and §6.7 are already closed** — see §4d. |
| **`SummaRace_Build_And_Release.md`** | Toolchain install → preflight → Addressables → APK → sideload → smoke test. Printable build-day checklist. | On build day. Its §5 (signing) before your *first* APK. Its Device Budget checklist item is already done. |
| **`SummaRace_Study_Operations_Runbook.md`** | The classroom side: PIN, participant codes, running a session, unlocking, exporting, recovering. | Before day 1, and hand a copy to whoever supervises sessions. |
| **`SummaRace_Data_Dictionary.md`** | Every field in the exported `.jsonl`, cleaning rules, pandas/R loaders. **Current at schema 5.** | Give it to whoever analyses the data. No correction needed. |
| **`SummaRace_Device_Budget.md`** | The measured RAM / APK / render-pipeline reality on the 2GB floor. **Read it as a record of what was fixed** — its audio remediation has been applied (§4d). Its render-pipeline findings are still live. | If the race misses 30fps. |
| **`SummaRace_Content_QA_Report.md`** | All 30 stories measured against the geometry they render into, plus the corpus validity audit. | Before the researcher sign-off conversation (D1). |
| **`SummaRace_Readability_And_Accessibility_Audit.md`** | Type size in arcmin, WCAG contrast, tap targets, colour-only channels, the race time-pressure derivation. **Its item 6 is closed and its contrast basis was ~15% optimistic** (§4c). | Only if you have a spare hour. |
| **`SummaRace_Story_Alignment_Audit.md`** | Whether page *n* teaches SWBST slot *n* across all 150 pairs. 135 aligned / 8 weak / 7 misaligned. | Alongside the Content QA report, for D1. |
| **`SummaRace_Audio_Audit.md`** | Every interactive moment judged against a **muted classroom tablet**. Its "races 2 and 3 are silent" defect was fixed in `63e1be3`; its resident-audio figures are superseded (§4d). | If a sound is wrong. |
| **`SummaRace_Asset_Shopping_List.md`** | The art still outstanding, with real paths, exact specs, 27 ready-to-paste prompts. | Only after the build works (D6). |
| **`SummaRace_Pro_Review.md`** | An earlier outside pre-ship read. **Superseded by `SummaRace_Critique.md` and by §2 of this page**; three of its items are closed (§4d). | Skip unless you want the history. |
| `SummaRace_Final_GDD.docx` | The design bible. D1–D18 locked. Intent, not implementation. | For design arguments the code has not already answered. |
| `SummaRace_Technical_Design_Document.md` | Original implementation spec. | Reference; the build has deliberately diverged. |
| `SummaRace_Project_Brief.md` | Orientation, one read. | If someone new joins. |
| `SummaRace_Build_Guide.md`, `SummaRace_Asset_Requirements_List.md` | Historical phase plan and original bill of materials. | Superseded. |
| `CLAUDE.md` | The engineering log, F1…F54. **It stops at `63e1be3`; §4c above is all newer.** Rows F1–F31 describe the legacy `Race.unity`, which no longer ships. | When you need to know *why* something is the way it is. |

**Rule of thumb:** this page and the Finalization Plan are the only two that claim to be current.
Everything else is a snapshot with a date on it, and §4d lists where a snapshot has gone stale.

---

## 6. If you only have one hour

1. **Send the researcher email (D1).** Five minutes of typing, and the answers arrive on her clock.
2. Install the Android module — it downloads while you do everything else.
3. Run `Build Preflight`, fix every ✖. (Build Addressables on Player Build is already on.)
4. Build, sideload to one tablet, and play **one non-`s01` story end to end with Wi-Fi off**.

Step 4 answers, in ninety seconds each, four things nothing else can: does the race start at all,
does Android BACK close the app, does a tap change lane, and can the soft keyboard be dismissed.
Everything else on this page can wait until you know the app installs, runs, and finishes a story
on real hardware.
