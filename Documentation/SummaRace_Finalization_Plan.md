# SummaRace — Finalization Plan

**Written:** 2026-08-04 · **Updated:** 2026-08-05 (post-F46 playtest pass) · **Branch:** `experiment/endless-override-2`

---

## STATUS — P0 through P5 are done; only P6 (ship pass) remains

Everything in §1's gap table below is closed. Read this block first; §1–§3 are kept as the
record of how the work was scoped, not as a to-do list.

| Phase | State |
|---|---|
| P0 decontaminate | ✅ **re-verified 2026-08-05 file by file** — `manifest.json` carries no ads/analytics/purchasing/GDK; `UnityConnectSettings.asset` has every service `m_Enabled: 0`; `Assets/Resources/` is empty (`BillingMode.json` gone); `UNITY_SOCIAL` is **not** an Android define (Android's list is just `UNITY_POST_PROCESSING_STACK_V2`); `Application.OpenURL` has zero references in `MainSummaRace.unity`; Build Settings is 12 scenes with Boot at index 0 and none of theirs; app icon is ours |
| P1 30 stories | ✅ 30/30 load through `StoryLoader`, 0 validation failures; distractor width tell closed in F44 |
| P2 reachable | ✅ StorySelect data-driven, SessionMap built |
| P3 profiles/logging/gating | ✅ `SessionLogService`, `TeacherGate`, NameEntry + TeacherMenu |
| P4 10 worlds | ✅ one per session, 30 applied / 30 distinct |
| P5 narration + art | ✅ **30/30** hero PNGs, **150/150** narration clips and **21/21** `AudioKeys` all resolve — nothing is missing at runtime. 27 of the 30 hero images are placeholders (quality gap, not a wiring gap) |
| **P6 ship pass** | ⬜ **blocked — see below** |

### P6 is blocked on tooling, not on code

**Android Build Support is not installed.** Unity is `6000.4.1f1` and its
`Editor/Data/PlaybackEngines` holds only `windowsstandalonesupport` — there is no
`AndroidPlayer`. **No APK has ever been produced**, so the two acceptance numbers that need
one — APK ≤ 300MB and 30fps in the race on the 2GB floor device — are estimates, not
measurements. Install the module via Unity Hub, then run P6.

**Second build blocker, now fixed but easy to reintroduce:** an unguarded `using UnityEditor;`
in a runtime script fails the *player* compile outright while the Editor compiles it happily.
Three have been found so far — `TrackManager.cs` (F44), `Plugins/ithappy/Animals_FREE/Scripts/CreatureMover.cs`
and `TextMesh Pro/Examples & Extras/Scripts/TMP_TextInfoDebugTool.cs` (both F46). Any new
third-party pack dropped into `Assets/` should be checked for one before the next build attempt.

### Still open, and each needs a human

1. **Owner full-loop playtest on a non-s01 story.** 27 of the 30 have never been played
   through the loop once. Editor-only verification cannot cover it — PrimeTween/PanelIntro
   /UIFloat motion only runs in Play mode, and F46 changed gate spacing, trigger depth, the
   start dolly and the tracker, none of which can be judged outside Play.
2. **One tablet per learner, or several learners per tablet?** Design call, and it decides
   how the study is run in the room. The data model already supports several profiles per
   device (`SaveManager` keeps one `.jsonl` per learner and `ExportLogs` merges them), and
   teacher-gated learner switching is being implemented — but the owner has to decide whether
   the protocol actually uses it, because it changes what the researcher hands out and how
   the PIN install rule reads.
3. **The race has no pause/back/quit.** The pause button is re-hidden every frame and the
   pause menu's Exit routes to their `QuitToLoadout`. Since the FINISH-gate fix (F43) the race
   always ends, so this is no longer a dead end — but a learner still cannot leave a race
   by choice, and there is no way to hand the tablet back mid-run. Design call.
4. **The patrol chase is not framable under the approved camera.** Measured in F46: the camera
   sits 5m behind the runner and 4m up, pitched ~15° with a 58.7° vertical FOV, so the bottom
   of the frustum is 44.4° below horizontal. A cop on the ground is only in frame while the gap
   is ≤ ~2.75m, and below ~2.2m he intersects the runner. The shipped 2.1m is a compromise: a
   sliver of head, or a blob growing out of the kid's back. Making the chaser read properly
   needs a **camera pull-back**, which is a look change to the whole race and therefore an
   owner call, not a tuning number. Doing nothing is defensible — he never catches (D7) and
   the danger vignette already carries the signal.
5. **Researcher content sign-off** (GDD D6) — the authored distractors and `s01_easy`'s
   AI-authored questions. Both are validity items, not bugs. See §5.
6. **Real hero art ×27.** All 30 resolve, so nothing is broken; 27 are 30–80KB generated
   fills. Specified in `SummaRace_Asset_Shopping_List.md`. Art swaps need no code change.
7. **`Race.unity` (legacy) is still build index 6** with nothing routing to it (~23MB).
   Cutting it would shrink the APK; worth deciding against a real build rather than guessing.
8. **Trash Dash's inactive objects** (GameOver / Loadout ×2 / Highscore / OpenLeaderboard /
   StoreButton / MissionsButton / Ad Button / Premium Button) are still in `MainSummaRace`.
   F46 neutralised the five buttons (onClick cleared, non-interactable) — they hung off
   `LevelLoader.LoadLevel(name)` and could have loaded a `shop` scene that is not in the build.
   Hidden is still not removed; GameOver and the Loadout children need a code guard first, and
   their `UICamera/Game` chrome must stay — `GameState.UpdateUI()` dereferences it every frame.

---

Guiding principle, per owner: **the build wins over the documents.** The GDD is intent;
where the game has deliberately diverged for playability (most of all the endless-runner
race), the build is the source of truth. Docs are consulted for *design questions the code
has not already answered*.

Second principle, because the horizon is days: **every phase ends with a playable,
committed game.** No phase leaves the loop broken. If a day disappears, we stop at a
phase boundary and still have something better than we started with.

---

## 1. Verified state (checked in-repo and against the live Editor, 2026-08-04)

**What is genuinely done:** the whole loop, polished — Boot → MainMenu → StorySelect →
Reader → Race (endless) → Arrange → Summary → Results. (At the time of writing, git log ran
to F42 and CLAUDE.md's table stopped at F35; both have since been brought current — the
table now runs to F46.)

**What is genuinely missing** (each verified, not assumed):

| # | Gap | Evidence |
|---|---|---|
| 1 | 27 of 30 story JSONs | only `s01_easy/average/hard` exist in `Resources/Stories/` |
| 2 | 300 SWBST distractors | GDD §5.3 already flags this; confirmed by regression (below) |
| 3 | StorySelect reaches one story | `StorySelectController` hardcodes `"s01_easy"`; average/hard are decorative locks |
| 4 | No learner profile at all | `GameManager.CurrentLearner` is declared and read but **never assigned** |
| 5 | No progress persistence | `SaveManager.LoadProfiles/SaveProfiles/AppendLog` exist with **zero callers** |
| 6 | 4 scenes are empty | `NameEntry`, `SessionMap`, `TeacherMenu`, `Settings` = 0 GameObjects |
| 7 | Branch cannot ship | `manifest.json` has `com.unity.ads` 4.16.4, `analytics` 3.8.2, `purchasing` 5.4.1, `microsoft.gdk`; Build Settings still lists their `Start`/`Main`/`Shop` |
| 8 | No per-story environment | one world for all 30 stories |
| 9 | Narration for 1 of 30 | 5 mp3s, `s01_easy` only |
| 10 | 29 hero images | `s01_*` only (and those are temp crops) |

Consequence of #4+#5 together: stars on StorySelect are always empty, replay tracking does
nothing, and no research data is written. The plumbing is built; nothing is connected to it.

### The story content is not missing — it is already written

The researcher's doc (`STORIES FOR SESSION 1-10 …docx`) contains **all 10 days × 3
difficulties**. 29 of the 30 have the full treatment: shortened passage, SWBST analysis,
5 pages, 5 processing questions **with A/B/C options and ✅ on the correct one**, main idea.
Day 1 EASY is the only partial one (passage + main idea), which is why its JSON is
hand-authored — that stays as-is.

So the story work is **conversion, not authoring**. A parser now reads the doc cleanly
(27/30 blocks perfect; the 3 exceptions are understood, see §5). Day numbering is verified
two independent ways (document `DAY` headings and ordinal block position agree on all 30).

**The one part that is real authoring:** a regression test — regenerating the two
hand-checked files `s01_average` / `s01_hard` from the doc and diffing — showed `pages`
reproduce **byte-for-byte**, and element `correct` lines come straight from the doc's SWBST
Analysis line (sentence-cased): 4–5 of 5 matched exactly. But **8 of 20 element fields
differed**, all in distractors, and not merely cosmetically:

- register normalization — `"Throw away the crayons"` → `"To throw the crayons away"` to
  match an infinitive `correct`; pronouns resolved (`"They were broken"` → `"The crayons
  were broken"`); subject matched to the correct line (`"Jayden chose…"` → `"He chose…"`).
- **semantic replacement** — on page 4 the doc's question is not always a clean *So*
  question, so mechanically-derived distractors are sometimes wrong for the slot
  (`"The crayons wanted new boxes"` offered as a *So*). The hand-checked file replaced
  them with plausible *So* alternatives.

A style tell (correct answer phrased differently from its distractors) is a validity threat
in a thesis instrument, so distractors get an editorial pass rather than mechanical output.

---

## 2. Decisions taken

| Decision | Choice |
|---|---|
| Which race ships | Decontaminate this branch, keep `MainSummaRace` and all F36–F42 polish |
| Environment variety | One world per session day (10), data-driven from a `world` field in story JSON |
| Narration | `en-PH-RosaNeural --rate=-10%` for all 30, batch-generated |
| Hero art | Title-only fallback for now (already supported); art if time remains |
| Deliverable | **Study-ready, with minimal teacher tools** — owner's call was "what's best" |

On the last one: the thesis is the point, and a demo build that cannot run the study wastes
the days. But note from GDD §8.1 that the *measurement instrument* is the paper
pretest/posttest with the Summary Writing Rubric — in-app logs are supporting data, not the
primary measure. So logging must exist and be exportable, but it does not need a polished
UI. Session PIN gating does matter (§8.3, internal validity: learners must not self-advance),
and it is cheap. Hence: real profiles, real logging, real gating; minimal teacher screens.

---

## 3. Phases

Each phase is independently committable and ends playable.

### P0 — Decontaminate (half day) · do this first
Everything downstream is built and tested on top of the build config, so fix the config
before adding to it — and do it while there is still time to recover if the race breaks.

- Remove `com.unity.ads`, `com.unity.analytics`, `com.unity.purchasing`,
  `com.unity.modules.unityanalytics`, `com.unity.microsoft.gdk(.tools)` from
  `Packages/manifest.json`. Their script paths are already compiled out via undefined
  `UNITY_PURCHASING`/`UNITY_ANALYTICS`, but the purchasing package still auto-initializes
  at runtime (source of the `IStoreService.Connect` console errors).
- Drop `Start`/`Main`/`Shop` from Build Settings; SummaRace scenes keep their indices.
- Strip from `MainSummaRace`: Loadout/Shop/GameOver/leaderboard objects, `LevelLoader` on
  StoreButton, `DataDeleteConfirmation`, and the `OpenURL` to unity.com (breaks offline).
- Re-audit for networking; confirm `UNITY_SOCIAL` is not defined for Android.

**Verify:** clean compile, zero console errors at race start, full loop still plays, no
`Connect` errors. **Gate:** owner plays one race. Commit.

### P1 — 30 stories (1 day) · the largest item, and the explicit ask
- Emit 29 JSONs from the parser (day 1 easy keeps its existing file).
- Author the 145 element sets: `correct` from the doc's SWBST line (sentence-cased),
  distractors edited for register and slot-correctness against the `s01_*` gold standard.
  Work from a compact worksheet (title + SWBST lines + question options), not full stories.
- Validation script mirroring `StoryLoader.Validate`, plus: 3 distinct options, no `✅`
  residue, correct index in range, distractors ≠ correct, element count 5, slot order S-W-B-S-T.
- Load all 30 through `StoryLoader` in the Editor and assert zero errors.

**Verify:** validator green on 30/30; spot-play 3 stories across difficulties. Commit.

### P2 — Make the content reachable (half day)
Currently the 30 files would be unreachable. Keep the existing F22 select-level design
(it is good) and make it data-driven rather than rebuilding it to the GDD's older spec.

- `StorySelectController`: load the current session's three stories, real per-card state
  (playable / locked / completed-with-stars), hero-image fallback, unlock in difficulty order.
- `SessionMap`: build the 10 stops (kit `Level screen.png` template), lock/current/complete
  states, route into StorySelect.

**Verify:** navigate to every session and story; locks behave; no dead ends. Commit.

### P3 — Profiles, persistence, gating (half day)
This closes gaps #4 and #5 — the plumbing exists, so this is wiring, not building.

- `NameEntry`: minimal name/alias + avatar, creates a `LearnerProfile`, sets
  `GameManager.CurrentLearner`, saves via `SaveManager`.
- Load the profile on boot; persist progress in `CompleteStory`; stars now real.
- `SessionLog` written per play-through via the existing `AppendLog` (one `.jsonl` per learner).
- Teacher PIN gate on session unlock (`teacherPinHash` already in `AppSettings`).
- `TeacherMenu`: unlock next session, export logs, delete data. Deliberately plain.

**Verify:** create a learner, play a story, relaunch — stars and unlock survive; a log line
appears on device. Commit.

### P4 — 10 worlds (half day) · the second explicit ask
Cheap because Trash Dash's `ThemeData` already models exactly this: `zones[]` (Urban /
Suburbs / Industrial prop sets), `cloudPrefabs`, `skyMesh`, `fogColor`.

- Add `world` to `StoryData`; `EndlessRaceDirector` applies the named world at race start.
- 10 variants = zone mix × sky/fog/light colour × cloud set, from art already in the project:
  `morning_suburbs`, `bright_park`, `sunset_town`, `blue_hour_suburbs`,
  `overcast_industrial`, `golden_fields`, `night_city`, `misty_morning`, `autumn_lane`,
  `starlit_finale`. Day-theme art plus the existing `NightTime` set covers all ten.

**Verify:** race each of the 10 and screenshot; each reads as a different place. Commit.

### P5 — Narration + art pass (half day, mostly unattended)
- Batch 145 clips: `python -m edge_tts --voice en-PH-RosaNeural --rate=-10% -f <file>
  --write-media <id>_pN.mp3` (page text via temp file — embedded quotes break inline
  `--text` in PS 5.1). Delete `/NarrationSamples` after.
- Confirm title-only hero fallback looks right on all 30 cards.

**Verify:** narration plays on a sampled page from each session. Commit.

### P6 — Ship pass
Full loop on a real device, 30fps in race on the 2GB floor device, APK ≤ 300MB, portrait
lock, IL2CPP/ARM64/API 26. Owner playtest of one complete session (all 3 difficulties).

---

## 4. Cut-line, if a day disappears

**Spent — none of these was needed.** All 150 narration clips and all 10 worlds shipped, and
the teacher export UI exists. Kept as the record of what was considered droppable.

Drop from the bottom, in this order:

1. Narration for average/hard (keep easy — the youngest support need)
2. Worlds beyond 3 (easy/average/hard tinting) instead of 10
3. Teacher export UI — leave the raw `.jsonl` on device for manual pull
4. SessionMap polish — a plain working list beats a pretty broken map

**Never cut:** all 30 stories reachable · decontamination · never-punish behaviour ·
no dead ends · progress that survives a relaunch.

---

## 5. Needs a human decision (does not block the plan)

1. **Day 5 HARD and Day 6 HARD are both "In Grandfather's Day"** — genuinely different
   treatments in the doc (different SWBST subject: *Grandfather* vs *Sharr and Kaze*,
   different questions), not a parse error. Researcher should confirm this is intended
   before the study build.
2. **Day 2 AVERAGE / HARD have no passage paragraph** in the doc — harmless for the game
   (`StoryData` has no passage field; pages carry the text), but flagging it.
3. **Content sign-off (GDD D6)** — the 300 authored distractors should get a researcher pass.
4. **Trash Dash art/music licence** — Unity Companion Licence. Almost certainly fine for a
   thesis instrument, but worth a deliberate confirmation before distributing to devices.
5. **`s01_easy` questions remain AI-authored** (the doc gives Day 1 EASY no page treatment).
   They follow the researcher's own pattern faithfully. Do **not** rewrite them
   comprehension-shaped — that conclusion was reached once before and was wrong.

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| P0 breaks the race | First phase, own commit, owner gate before anything builds on it |
| Distractor quality is the validity-critical item | Gold-standard register model + programmatic validation + researcher sign-off |
| Bloom + 10 worlds on a 2GB device | Frame-rate check in P6; worlds are data, so any one can be flattened |
| Editor-only verification | Motion/tweens only run in Play mode; owner playtest gates each phase |
| **The build has never been attempted on the target platform** | Everything about size, frame rate and IL2CPP behaviour is inference from disk. Install the Android module *early* in the remaining time, not last — F44 and F46 each found a compile-time blocker the Editor could not see |
| Race defects that silently produce wrong study data | F46 found two (a tunnelled gate logged as WRONG, a watchdog re-present logged as CORRECT). Anything that writes `raceFirstPickCorrect` deserves the same scrutiny — it *is* the star count and the logged measure |
