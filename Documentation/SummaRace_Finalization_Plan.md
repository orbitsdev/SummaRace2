# SummaRace — Finalization Plan

**The live plan.** Rewritten 2026-08-06 against `de63a3d`; **re-verified against HEAD `63e1be3`**
on `experiment/endless-override-2`. Superseded content has been deleted rather than appended to —
if it is not here, it is done or it no longer applies.

**Start with `SummaRace_Owner_Handover.md`.** That document carries the ordered critical path and
the decisions waiting on the owner. This one carries the *work list with effort*, so the two do
not restate each other.

---

## Status in one table

| Phase | State |
|---|---|
| P0 decontaminate | ✅ re-verified file by file — no ads/analytics/purchasing/GDK in `manifest.json`, every Unity service `m_Enabled: 0`, `Assets/Resources/` empty, zero `Application.OpenURL` reachable from a build scene, 12 build scenes with Boot at index 0, app icon is ours |
| P1 30 stories | ✅ 30/30 load through the real `StoryLoader`, 0 validation failures; race card-width tell closed (F44) and the same tell closed in the **Reader** (F47ⓖ, 112 distractors rewritten) |
| P2 reachable | ✅ StorySelect data-driven, SessionMap built and routed |
| P3 profiles / logging / gating | ✅ `SessionLogService`, `TeacherGate`, NameEntry + TeacherMenu, teacher-gated learner switching, **atomic profile writes** (`e5966be`), **participant codes** as the join key to the paper test (`9d839ba`), log schema **4** |
| P4 10 worlds | ✅ one per session, 30 applied / 30 distinct — **and since `63e1be3` a world also selects theme, zone family, sky dome and greenery**, which fixed all thirty races opening on the identical Industrial alley |
| P5 narration + art | ✅ 30/30 hero PNGs, 150/150 narration clips, 21/21 `AudioKeys` resolve. 27 hero images are blank placeholders — an appearance gap, not a wiring gap |
| P7 tests + build tooling | ✅ **new this session**: `Assets/_Game/Tests/` 45/45 green, `SummaRace ▸ Build Preflight`, `SummaRace ▸ Device Budget` |
| **P6 ship pass** | ⬜ **blocked on tooling — the only thing between here and a study build** |

---

## P6 — the ship pass, in order

The full step-by-step is in `SummaRace_Owner_Handover.md` §2 and
`SummaRace_Build_And_Release.md`. This is the effort view.

| # | Work | Effort | Blocking? |
|---|---|---|---|
| 1 | Install Android Build Support + OpenJDK + SDK/NDK on **6000.4.1f1**, Editor closed | 10–30 min download, unattended | **HARD BLOCKER** — `IsBuildTargetSupported(Android)` is false; no APK can be produced |
| 2 | Switch platform to Android (re-imports a 945MB `Assets/`) | 30–90 min, unattended — **do it the day before** | yes, follows 1 |
| 3 | Turn on **Build Addressables on Player Build** (or build content by hand, per build) | 5 min + a few min per build | **HARD BLOCKER** — verified: `m_BuildAddressablesWithPlayerBuild: 2` and `Library/com.unity.addressables/` has no `aa/` folder. The race's road, scenery and runner load via Addressables, so the first APK boots into an empty race |
| 4 | `SummaRace ▸ Build Preflight`, fix every ✖ | 15 min + unknown fixes | yes |
| 5 | `SummaRace ▸ Device Budget ▸ 4 · Apply ALL safe fixes` | 10 min | no, but recovers ~122MB of resident RAM on a 2GB device — see `SummaRace_Device_Budget.md` §2 |
| 6 | Build one APK, tag the commit, back up `debug.keystore` | 20–45 min (first IL2CPP build) | yes |
| 7 | Sideload + on-device smoke test, **Wi-Fi off, on a non-`s01` story** | 30–60 min | yes |
| 8 | Set + record the teacher PIN on every tablet, **and each learner's participant code** (`9d839ba`) | ~2 min per tablet, + ~1 min per learner | yes — the code is the join key to the paper pretest/posttest and cannot be back-filled onto rows already written. Runbook §1.2b |
| 9 | Prove the export path over USB (adb — `Android/data` is often hidden from MTP on Android 11+) | 15 min once | yes |
| 10 | Install the *same* APK on all 40 tablets | ~2 min per tablet | yes |

**Realistic total: one focused day**, most of it waiting on downloads and imports, assuming the
preflight and the smoke test do not surface something new. Steps 1–3 can all start today.

---

## Remaining work that is not P6

Ordered by value, with the effort honestly stated. None of it blocks a build.

| # | Work | Effort | Notes |
|---|---|---|---|
| R1 | **Owner full-loop playtest on a non-`s01` story** | 1 h | 27 of 30 stories have never been played once. All `PanelIntro` / `UIFloat` / `ButtonSquash` / PrimeTween motion runs only in Play mode and has been checked in Play **only for the race**. This is the single highest-value hour available. |
| R2 | **Four contrast one-liners still open.** The two "green" colour-name instructions are **done** (`06f1778`). What remains: Reader "Not quite" 2.99:1; Summary SWBST words 2.10–2.34:1 (`HexForIndex` → `DeepForIndex`); FINISH card 1.74:1; StorySelect locked-card hint 2.6:1 | 30 min | Verified still open in source at HEAD `63e1be3`. Exact colours in `SummaRace_Owner_Handover.md` §4c. ⚠️ A pass may be landing on these — check the files first. |
| R3 | **`RaceSecondsPerGate` 12 → 17** if the owner takes decision D1 | 5 min + one playtest | Meets a 70 wpm reader's budget. Gate spacing is clamped (110/300), so verify the change actually lands. |
| R4 | **`GameRules.TargetFrameRate` 60 → 30** | 2 min | Trash Dash's `MusicPlayer` already forces 30 once the race loads, so today the app targets 60 in the menus and burns thermal headroom the race then needs. |
| R5 | **Render-pipeline frame-rate levers**, if the race misses 30fps on hardware: cascades 4→1, shadowmap 2048→512, `m_IntermediateTextureMode` Always→Auto | 15 min each + a playtest each | All look-preserving in `MainSummaRace` (verified: nothing in the corridor can receive a shadow). `renderScale` 1.0→0.8 is the big lever and **is** visible — last resort only. **Never reassign `Mobile_RPAsset`**; it turns HDR back on. |
| R6 | **Real hero art ×27** | an evening | Blank gradients today. 27 ready-to-paste prompts + exact spec in `SummaRace_Asset_Shopping_List.md` §2. Overwrite the existing filenames; never delete the `.png.meta`. |
| R7 | **Drop `Race.unity` from Build Settings**, then delete `Ch46_nonPBR.fbx` | 15 min | 51.5MB of source assets are reachable only from it; 12–20MB estimated APK saving. Zero call sites. Only worth doing if the APK is near the cap — measure first. |
| R8 | **`SummaRace_Data_Dictionary.md` is TWO schemas behind** | 1 h | It documents schema **2** and states no distractor-level record exists. The build writes schema **4**: schema 3 added `racePicks` (option index, exact text, **lane**, timestamp), `racePauseCount`, `racePausedSeconds`, `abandonReason`; schema 4 added **`participantCode` to every row** — the join key to the paper pretest/posttest. The researcher analyses from that document and will otherwise not know either exists. ⚠️ A **schema 5** (`arrangeOrders`) is uncommitted in the working tree as this is written — check `SessionLogService.SchemaVersion` before updating the doc, and update it *once*, against the code. |
| R9 | **Strip Trash Dash's inactive objects** from `MainSummaRace` (GameOver, Loadout ×2, Highscore, store/leaderboard buttons) | half a day | The five buttons are already neutralised (`m_Calls: []`, non-interactable). GameOver and the Loadout children need a code guard first. **Their `UICamera/Game` chrome is load-bearing — `GameState.UpdateUI()` dereferences it every frame; do not delete it.** Post-study cleanup, not a shipping blocker. |
| R10 | **Narrate the ~10 instructional strings** (Arrange intro/title, Summary title/hint, the 5 loading tips, race briefing body) | 2 h | Today the 150 clips cover story pages only, so a learner who cannot read is stuck on an *interface* task, not a comprehension one. Leaves every scored item unnarrated, so the measure is untouched. Same `edge-tts en-PH-RosaNeural --rate=-10%` recipe. Post-study if time is short. |

---

## Needs a human decision

Fully stated, with recommendations, in **`SummaRace_Owner_Handover.md` §3**. Listed here so the
plan is complete:

1. `RaceSecondsPerGate` 12 → 17 — reading time for the slowest readers *(recommend: yes)*
2. One tablet per learner vs shared *(recommend: one per learner; the shared path exists and is PIN-gated)*
3. A passive run that collects all five gates *(recommend: accept — it scores at chance, and `racePicks[].lane` detects it after the fact)*
4. The 27 placeholder hero images *(recommend: only after the build works)*
5. `s05_hard` / `s06_hard` are the same story *(recommend: leave the content, record it as a limitation)*

Plus two that belong to the researcher, not to the build:

6. **Content sign-off (GDD D6)** — the authored distractors, and `s01_easy`'s AI-authored
   questions. `SummaRace_Content_QA_Report.md` is the evidence pack: structure 30/30,
   `flag.py` 0 of 150, `fit.py` 0 failures, 0 of 150 `correct` lines contradict their story,
   zero spelling/typography hits. **Do not "fix" `s01_easy`'s questions to be
   comprehension-shaped** — that conclusion was reached once before and was wrong; the
   researcher's own processing questions are SWBST-shaped by design.
7. **Trash Dash art/music licence** (Unity Companion Licence) — almost certainly fine for a
   thesis instrument, but worth a deliberate confirmation before distributing to 40 devices.

---

## Risks

| Risk | Where it stands |
|---|---|
| **The build has never been attempted on the target platform** | Everything about APK size, frame rate and IL2CPP behaviour is inference from disk. Three unguarded `using UnityEditor;` in runtime code have been found and fixed — each one alone would have failed the *player* compile while the Editor stayed silent. Check any new third-party pack for a fourth. |
| **RAM, not polygons, on the 2GB floor** | ~156MB of raw PCM is resident once the race is entered, and `MusicPlayer` is `DontDestroyOnLoad` so it stays for the session. The Device Budget script fixes it in ten minutes and has **not been run** (verified today: the stems are still `loadType: 0`). |
| **Race defects that silently produce wrong study data** | Five have been found and fixed: a tunnelled gate logged as WRONG, a watchdog re-present logged as CORRECT, `raceFirstOutcome` filled by re-present collections, a backgrounding that discarded the rest of a run, and `raceFirstPickCorrect` unable to tell "answered wrong" from "ran past". Anything that writes that field deserves the same scrutiny — it **is** the star count and the headline measure. |
| **A cue that lets the race be scored without reading** | Found twice (race cards F44, Reader options F47ⓖ) at the same magnitude. Now guarded **in the build** by `RaceCardValidityTests` rather than by a script someone must remember to run, and the fixture asserts the symmetric heuristic too so the tell cannot simply flip direction. |
| **Editor-only verification** | 45/45 tests and a Roslyn compile prove a great deal and prove nothing about motion, touch, or hardware. R1 and the on-device smoke test are the only cures. |
| **A mid-study rebuild from a second machine** | The APK is debug-signed and the debug keystore is per-machine. Installing over a deployed build with a different key forces an uninstall, which destroys that tablet's profiles and every unexported log. Build every study APK on one machine; back up the keystore with the APK. |
| **Losing the data** | Logs live only on the tablet, export is behind the PIN, and the PIN recovery gesture wipes them. Export regularly during the study, not once at the end, and prove the USB retrieval path before day 1. Profile *writes* are no longer a risk — they became atomic in `e5966be`, and before that a crash mid-write silently rebooted a learner as a brand-new child. |
| **The logs not joining to the paper test** | Closed by `9d839ba`, *if the codes are actually set*. The join used to rest on a name a nine-year-old typed; it now rests on a teacher-set participant code stamped on every row. The residual risk is entirely operational: a tablet set up without codes records everything and joins to nothing. Export warns; set them at install. |
| **Frame rate after the world pass** | `63e1be3` added roadside greenery and a second theme, at an author-estimated **+200 draw calls worst case, unmeasured on any device**. RAM is unchanged. If the race misses 30fps, halve `GameRules.RaceMaxSceneryPerSegment` (14) before touching anything in R5. |
