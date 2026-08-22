# Fix Execution Plan — 2026-08-22

Source of truth: `Desktop/summarace/summarice.md` §5 fix list + `project/GAME_SUMMARY.md` §5 defects.
Branch: `experiment/endless-override-2`. Editor is OPEN with MCP — agents edit files on disk only;
compile/play verification happens centrally after all agents land.

## Constraints for every task
- Modify existing files only — NO new .cs files (stub .meta trap).
- Follow house style: `Theme` tokens for colors, `GameText` for learner strings, `GameRules` for
  numbers, null-check every scene reference, match surrounding comment density.
- Do NOT touch `Packages/manifest.json`, do NOT commit, do NOT use Unity MCP tools.
- EditMode tests cannot run until the owner's Editor restart (CS0433) — verification is by
  compile + live play, done centrally.

## Tasks (parallel, no file overlaps)

| # | Task | Files | Agent |
|---|---|---|---|
| A | SceneLoader fade overlay: add GraphicRaycaster + CanvasScaler so blocksRaycasts works; tip/label sizes become reference-scaled | `Core/SceneLoader.cs` | 1 |
| B | BackButtonGuard leak: clear stale registration on scene change; MainMenu registers its own blocked rule | `Core/BackButtonGuard.cs`, `Features/MainMenu/MainMenuController.cs` | 2 |
| C | Badge picker wiring: learner's avatarIndex shown in MainMenu "Playing as X" pill + TeacherMenu learner rows | `Features/MainMenu/MainMenuController.cs` (agent 2 owns), `Features/TeacherMenu/TeacherMenuController.cs`, read `NameEntryController` for sprite source | 2 |
| D | StorySelect PLAY badge dark-on-dark → readable per Theme | `Features/StorySelect/StorySelectController.cs` | 3 |
| E | Weather falls, not flies: rotate/redirect the camera-parented emitter downward per kind | `Features/Race/Endless/EndlessWorldDressing.cs` | 4 |
| F | Lumi cheer restores hidden alpha (question-state safe) | `UI/MsLumiReactor.cs` | 5 |
| G | Data Dictionary regenerated against schema 6 | `Documentation/SummaRace_Data_Dictionary.md`, read `Core/SessionLogService.cs` + `Data/SaveModels.cs` | 6 |
| H | Researcher email draft: 4 thesis-wording mismatches + pretest-story confirm | new `Documentation/Researcher_Email_Draft.md` (md file — safe) | 6 |

## Central verification (after agents land)
1. Unity refresh + compile, console must be 0 errors.
2. Live Play spot-checks: overlay blocks taps · MainMenu BACK shows its own rule · badge visible
   in pill · PLAY badge readable · rain falls in `overcast_industrial` · Lumi stays hidden
   mid-question after a correct answer.
3. Commit per verified milestone (never the manifest).

## Owner-only (P0, gating the APK)
- ~~Commit the manifest fix~~ ALREADY COMMITTED (79f4cc2). **Only the full Editor restart remains.**
- Then: rebuild APK, device smoke test, participant codes + PIN at install, USB export proof.

---

# PHASE 2 (added 2026-08-22 evening) — readability sweep, exit consistency, finish line

## 2a · UI readability sweep (owner's priority: "labels not readable")
After the Editor restart (portrait render + play mode both needed):
1. Drive every screen in Play mode at 1080x1920 and capture: Boot, NameEntry, MainMenu,
   SessionMap, StorySelect (all 3 card states), Reader (page + question + wrong + correct),
   Race (briefing, countdown, gate + preview, wrong beat, finish card), Arrange (empty,
   held-piece, wrong, solved), Summary (empty, typing, nudge), Results (full reveal),
   TeacherMenu (every gate step + actions + learner picker).
2. For each capture: list every text element that fails easy-read at arm's length —
   suspects from evidence so far: loading-tip text (now reference-scaled — recheck),
   "Stars = your race score" pill, locked-card hint lines, race feedback pill over bright
   sky, TeacherMenu status line length. Fix strictly via Theme pairings (measured ratios
   in Theme.cs), never raw colors. The owner's device screenshots trump editor renders.
3. Re-render after fixes; before/after strip for the owner.

## 2b · Exit + navigation consistency
- Results: register BackButtonGuard exit matching the NEXT button's destination
  (SessionMap when session done, StorySelect otherwise) instead of the generic blocked card.
- Verify (not change): Reader leave-window, race pause/leave, Arrange/Summary forward-only
  with their friendly blocked lines — these are design, re-confirm the copy reads warmly.

## 2c · PIN — documentation, not code
No default PIN exists by design (hash-only storage). Testing instructions are in chat +
Study_Operations_Runbook. No code change.

## 2d · Settings
Stays cut (F43). VOICE lives in Reader, music behind the PIN. Post-study wishlist only.

## Finish line (what "done" means) — status 2026-08-22 23:15
1. ✅ Six-fix verification in Play mode DONE: overlay raycaster/scaler probed live; MainMenu
   BACK card on screen; badge in learner pill on screen; PLAY badge gold-on-navy on screen;
   rain avgVel (0, -23.5, +5) probed + vertical streaks on screen; Lumi alpha 1.00 during
   cheer → 0.00 after, probed. Race pause→leave exit path re-verified (timeScale/audio clean).
2. ✅ 2b done (Results BACK registered — commit 163eb4e; runtime check pending next full loop).
   2a readability: fresh captures of MainMenu/StorySelect/Race clean; TeacherMenu badge row +
   Results BACK are the two owner-eyeball leftovers.
3. ✅ EditMode tests: 59/59 PASSED in 2.7s (first run since the CS0433 collision).
4. ⬜ OWNER: run SummaRace ▸ Build Preflight from the menu (must not be triggered via MCP —
   it ends in a modal dialog that kills the bridge).
5. ⬜ OWNER: Build APK (Addressables build with player, Android) → install on the Infinix →
   full-loop smoke test on device.
6. ⬜ OWNER: play 2-3 stories on device; send Documentation/Researcher_Email_Draft.md.
