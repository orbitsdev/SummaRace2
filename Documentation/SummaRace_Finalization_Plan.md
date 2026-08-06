# SummaRace — Finalization Plan

**The live plan.** Rewritten 2026-08-06, verified against HEAD `b399e03` on
`experiment/endless-override-2`. Superseded content has been deleted rather than appended to — if
it is not here, it is done or it no longer applies. Fifteen commits have landed since the previous
version's baseline (`63e1be3`) and this one was checked against the source, not against that page.

**Start with `SummaRace_Owner_Handover.md`.** That document carries the ordered critical path, the
decisions waiting on the owner, and the record of what the last fifteen commits closed. This one
carries the *work list with effort*, so the two do not restate each other.

---

## Status in one table

| Phase | State |
|---|---|
| P0 decontaminate | ✅ no ads/analytics/purchasing/GDK in `manifest.json`, every Unity service `m_Enabled: 0`, zero `Application.OpenURL` reachable from a build scene, 12 build scenes with Boot at index 0, app icon is ours |
| P1 30 stories | ✅ 30/30 load through the real `StoryLoader`, 0 validation failures. Card-width tell closed in both the race (F44) and the Reader (F47ⓖ). **The memoriser route is NOT closed — see C1** |
| P2 reachable | ✅ StorySelect data-driven, SessionMap built and routed |
| P3 profiles / logging / gating | ✅ `SessionLogService`, `TeacherGate`, NameEntry + TeacherMenu, teacher-gated switching, atomic profile writes, participant codes with export back-fill, log schema **5** (`arrangeOrders`) |
| P4 10 worlds | ✅ theme + zone mix + sky dome + greenery + light; 10–20 distinct segment prefabs per race (was 3–4) |
| P5 narration + art | ✅ 150/150 story clips, **plus 10 instructional clips** (Arrange, Summary, loading tips, race briefing), 30/30 hero PNGs resolve, 21/21 `AudioKeys`. 27 hero images are blank placeholders — appearance, not wiring |
| P7 tests + build tooling | ✅ `Assets/_Game/Tests/`, `SummaRace ▸ Build Preflight`, `SummaRace ▸ Device Budget`. ⚠️ **the "45/45 green" figure is stale** — two fixtures have been added since and one (`NarrationArrayTests.cs`) is untracked. Re-run it; do not quote a number |
| P8 device budget | ✅ **applied** (`c03c2cd` + `24d682e`) — ~127MB of resident RAM recovered; verified on disk, the stems are `loadType: 1 / preloadAudioData: 0` and `music_menu` is streaming |
| **P6 ship pass** | ⬜ **blocked on tooling — still the only thing between here and a study build** |

---

## P6 — the ship pass, in order

Step-by-step in `SummaRace_Owner_Handover.md` §2 and `SummaRace_Build_And_Release.md`. This is the
effort view.

| # | Work | Effort | Blocking? |
|---|---|---|---|
| 1 | Install Android Build Support + OpenJDK + SDK/NDK on **6000.4.1f1**, Editor closed | 10–30 min download, unattended | **HARD BLOCKER** — verified: `PlaybackEngines/` holds only `windowsstandalonesupport`, so no APK can be produced at all |
| 2 | Switch platform to Android (re-imports a 945MB `Assets/`) | 30–90 min, unattended — **do it the day before** | yes, follows 1 |
| 3 | Turn on **Build Addressables on Player Build** (or build content by hand, every build) | 5 min + a few min per build | **HARD BLOCKER, and the worst one.** Verified: `m_BuildAddressablesWithPlayerBuild: 2`, no `aa/` folder, no `StreamingAssets`. The race's road, scenery and runner all load via Addressables, and their `TrackManager` GameObject is only activated at the end of a `Begin()` that `yield break`s when the character load returns null — so with no content the race **never starts.** `a760ec0` made that escapable, not playable |
| 4 | `SummaRace ▸ Build Preflight`, fix every ✖ | 15 min + unknown fixes | yes |
| 5 | Build one APK, tag the commit, back up `debug.keystore` | 20–45 min (first IL2CPP build) | yes |
| 6 | Sideload + on-device smoke test, **Wi-Fi off, on a non-`s01` story** | 30–60 min | yes — and it is the only way to settle five things listed as never-measured in Handover §4a |
| 7 | Set + record the teacher PIN on every tablet, **and each learner's participant code** | ~2 min per tablet, + ~1 min per learner | yes. Export now back-fills the code onto rows written before it was set, so a late code is recoverable — set them at install anyway. Runbook §1.2b |
| 8 | Prove the export path over USB (adb — `Android/data` is often hidden from MTP on Android 11+) | 15 min once | yes |
| 9 | Install the *same* APK on all 40 tablets | ~2 min per tablet | yes |

**Realistic total: one focused day**, most of it waiting on downloads and imports, assuming the
preflight and the smoke test surface nothing new. Steps 1–3 can all start today.

*(The previous version's step 5, `Device Budget ▸ Apply ALL safe fixes`, is done — see P8 above.)*

---

## Content work — the one that is not tooling

| # | Work | Effort | Notes |
|---|---|---|---|
| **C1** | **Finish narrowing the memoriser route** | half a day of editorial + a pipeline re-run | The race was measured **84.0%** passable by remembering the Reader's correct answer for a slot and picking the most similar race card, against a **34.0%** misaligned control. `8cba732` got it to **59.3%**, with **25 of 150** race `correct` lines still byte-identical to the Reader's, and states plainly that it is not finished. **Verified: no story JSON has changed since that commit, so 59.3% is live.** `raceFirstPickCorrect` **is** the star count and the headline logged measure, so at 59% a memoriser still beats a reader. Content edits belong in `Tools/StoryPipeline/overrides.json` + a re-run, never in the 30 files. **Gated on the researcher (Handover D1)** — paraphrasing a `correct` line changes her content |
| **C2** | **Researcher content sign-off** | 30 min of the owner's time; then her clock | ~130 machine-edited strings across F44 / F47ⓖ / F53 plus `s01_easy`'s AI-authored questions, none read by a researcher. `SummaRace_Critique.md` §5 argues this is the project's largest non-APK risk. Evidence pack: `SummaRace_Content_QA_Report.md` + `SummaRace_Story_Alignment_Audit.md`. Bundle C1(4), `s05_hard`/`s06_hard`, and the reported `s03_easy` mislabel into the same email. **Do not "fix" `s01_easy`'s questions to be comprehension-shaped** — that conclusion was reached once and was wrong |

---

## Remaining work that is not P6 or content

Ordered by value, effort honestly stated. None of it blocks a build.

| # | Work | Effort | Notes |
|---|---|---|---|
| R1 | **Owner full-loop playtest on a non-`s01` story, on the tablet** | 1 h | 27 of 30 stories have never been played once. All `PanelIntro` / `UIFloat` / `ButtonSquash` / PrimeTween motion runs only in Play mode. Folded into P6 step 6 because on-device is where it is worth an hour |
| R2 | **Tell the learner where to read, and make the option panel tappable** | 45 min | `GameText.RaceBriefingBody` is verified at HEAD to read only *"Tap or swipe left and right to move!"* — it never names the board at the top of the screen that carries the only readable copy of the three answers, and never mentions the middle lane. Then make the three panel columns real buttons calling `ChangeLane`, so reading and choosing become one act. `SummaRace_Critique.md` §6.6 ranks this as the one change that improves the experience **and** the measure at once |
| R3 | **`GameRules.StarsTwoMin` 4 → 3** | 2 min | Verified 4 at HEAD, so 3/5 and 0/5 show the identical screen. Display only; no logged measure moves. Owner decision D4 |
| R4 | **`RacePreviewLeadSeconds` 12 → 17** if the owner takes decision D2 | 5 min + one playtest | **Not `RaceSecondsPerGate`** — that is 20 now and sets gate *spacing*. The reading window is a guaranteed 12s, floored into the gap as `(lead + RaceQuietRunSeconds 6) × speed`. ⚠️ Raising it to 17 makes the floor 23s, which **collapses average (20s) and hard (18s) onto the same cadence** and makes difficulty inert on two of three — the F46ⓑ defect returning. Keeping them separate needs `RaceSecondsPerGate` past ~29 too. Time one race first |
| R5 | **Log the background intervals for the non-race phases** | 30 min | `readingSeconds`, `arrangeSeconds` and `summarySeconds` include tablet-locked time. Unrecoverable after the fact, so it is worth more now than later. `SummaRace_Critique.md` §2.6 |
| R6 | **Show the learner their own summary on Results** | 30 min | `GameManager.LastSummaryText` is verified written and read by nobody. Two of four stages currently end without showing the learner what they produced |
| R7 | **Render-pipeline frame-rate levers**, if the race misses 30fps on hardware | 15 min each + a playtest each | In cost order: `RaceMaxSceneryPerSegment` 14→7 (the documented first lever, and the only one that is not a project-wide rendering change), then cascades 4→1, shadowmap 2048→512, `m_IntermediateTextureMode` Always→Auto. All look-preserving in `MainSummaRace` — nothing in the corridor can receive a shadow. `renderScale` 1.0→0.8 is the big lever and **is** visible: last resort. **Never reassign `Mobile_RPAsset`** — it turns HDR back on. **There is no Bloom to turn off** — `RacePostFx.asset` holds a single null component and no camera in `MainSummaRace` has post-processing on |
| R8 | **Real hero art ×27** | an evening | Verified: `s01_*` are ~1.8MB illustrations, `s02`–`s10` are 42–47KB blank gradients. 27 ready-to-paste prompts + exact spec in `SummaRace_Asset_Shopping_List.md` §2. Overwrite the filenames; never delete the `.png.meta` |
| R9 | **Drop `Race.unity` from Build Settings**, then delete `Ch46_nonPBR.fbx` | 15 min | 51.5MB of source assets are reachable only from it (the scene file is 119KB); 12–20MB estimated APK saving. Zero call sites. Only worth doing if the APK is near the 300MB cap — **measure first, since no APK has ever been built** |
| R10 | **Strip Trash Dash's inactive objects** from `MainSummaRace` | half a day | GameOver, Loadout ×2, Highscore, store/leaderboard buttons. The five buttons are already neutralised (`m_Calls: []`, non-interactable). GameOver and the Loadout children need a code guard first. **Their `UICamera/Game` chrome is load-bearing — `GameState.UpdateUI()` dereferences it every frame; do not delete it.** Post-study cleanup |
| R11 | **Commit the working tree** | 5 min | `Assets/_Game/Editor/BuildPreflight.cs` has ~440 lines of uncommitted changes and `Assets/_Game/Tests/Editor/NarrationArrayTests.cs` is untracked. Also `ExportBackfillTests.cs` is tracked with no `.meta` — Unity will generate one on next import, which will show up as an unexplained diff |

---

## Needs a human decision

Fully stated, with recommendations, in **`SummaRace_Owner_Handover.md` §3**. Listed here so the plan
is complete:

1. **Freeze the content and email the researcher** — the memoriser route, ~130 machine-edited
   strings, the `s05_hard`/`s06_hard` duplicate, the reported `s03_easy` mislabel *(send today; the
   answers gate C1 and C2 and arrive on her clock)*
2. `RacePreviewLeadSeconds` 12 → 17 — reading time for the slowest readers *(time one race first)*
3. One tablet per learner vs shared *(recommend: one per learner)*
4. `StarsTwoMin` 4 → 3 *(recommend: yes)*
5. A passive run that collects all five gates *(recommend: accept — it scores at chance, and
   `racePicks[].lane` detects it after the fact)*
6. The 27 placeholder hero images *(recommend: only after the build works)*
7. `s05_hard` / `s06_hard` are the same story *(recommend: leave the content, record it as a
   limitation)*

Plus one that belongs to neither the build nor the researcher:

8. **Trash Dash art/music licence** (Unity Companion Licence) — almost certainly fine for a thesis
   instrument, but worth a deliberate confirmation before distributing to 40 devices.

---

## Risks

| Risk | Where it stands |
|---|---|
| **The first APK ships without Addressables content** | The single highest-consequence risk in the project. The race does not merely look wrong — it never starts. Preflight fails on it; do not override that ✖ |
| **The build has never been attempted on the target platform** | Everything about APK size, frame rate and IL2CPP behaviour is inference from disk. Three unguarded `using UnityEditor;` in runtime code have been found and fixed, each of which alone would have failed the *player* compile while the Editor stayed silent. `Link.xml` now preserves the crypto assemblies, which was the same class of defect one layer down: `SHA256.Create()` resolves by reflection and stripping was free to remove it, taking the PIN, session unlock and **Export** with it |
| **The instrument can be passed without comprehension** | Found three times at the same magnitude — race card width (F44), Reader option length (F47ⓖ), and now the Reader→race answer carryover, at 59.3% against a 34.0% control. The first two are guarded **in the build** by test fixtures; the third is not, and it is the one that is still live. C1 |
| **Race defects that silently produce wrong study data** | Eight found and fixed to date: a tunnelled gate logged as WRONG, a watchdog re-present logged as CORRECT, `raceFirstOutcome` filled by re-present collections, a backgrounding that discarded the rest of a run, four saves that reported success without checking, an export that reported a failed write as "no logs", a learner handover indistinguishable from a dead battery, and a `HasData` check that would discard a run carrying only `racePicks`/`arrangeOrders`. Anything that writes `raceFirstPickCorrect` deserves the same scrutiny — it **is** the star count and the headline measure |
| **RAM on the 2GB floor** | **Largely closed.** The pool was 220.6MB, not the 178.2MB previously recorded — there was a seventh DecompressOnLoad stem nobody had found — and ~127MB has been recovered and verified on disk. What remains unmeasured is the render side: `renderScale` 1.0, a 2048 shadow atlas and 4 cascades into a world that cannot receive a shadow. R7 |
| **Editor-only verification** | The test suite and a Roslyn compile prove a great deal and prove nothing about motion, touch, or hardware. Specifically unverifiable here: Android BACK (`Application.wantsToQuit` is never raised on Play-mode exit), tap-to-lane, and both soft-keyboard dismiss chips |
| **A mid-study rebuild from a second machine** | The APK is debug-signed and the debug keystore is per-machine. Installing over a deployed build with a different key forces an uninstall, which destroys that tablet's profiles and every unexported log. Build every study APK on one machine; back up the keystore with the APK |
| **Losing the data** | Logs live only on the tablet, export is behind the PIN, and the PIN recovery gesture wipes them. Export regularly during the study, not once at the end, and prove the USB path before day 1. Profile writes are atomic, the wipe now removes `.bak`/`.tmp` siblings and Trash Dash's `save.bin`, and it can no longer recreate an erased learner's log folder |
| **The logs not joining to the paper test** | Closed *if the codes are actually set*. It now rests on a teacher-set participant code stamped on every row, `Normalize` refuses an over-long code instead of silently truncating two booklet codes into one participant, and the export back-fills rows written before the code was set. Residual risk is entirely operational |
| **Layout on the actual study device** | `b399e03` moved the tracker and pause chip off pixel anchors after measuring that at **4:3** the reading board covered 34px of the tracker and 50px of the pause chip — the only way out of a run. At 9:16 the layout is byte-for-byte what was playtested, so this can only improve other aspects. **But nobody knows which tablet ships**, and no portrait layout has ever been rendered on hardware |
| **Frame rate after the world passes** | `63e1be3` + `c03c2cd` added greenery, a second theme and a mixed-family road, at an author-estimated **+200 draw calls worst case, unmeasured on any device**. RAM is unchanged. Halve `RaceMaxSceneryPerSegment` (14) before touching anything else in R7 |
