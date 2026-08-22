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
- Commit the manifest fix (two commands, given in chat) + full Editor restart.
- Then: rebuild APK, device smoke test, participant codes + PIN at install, USB export proof.
