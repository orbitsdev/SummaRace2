# SummaRace Full-Project Audit — 2026-07-18

Branch: experiment/endless-override-2 · Spec: ../specs/2026-07-18-full-project-audit-design.md
Finding format: ID · Path · Remove/Fix/Change/Missing · Blocker/Should-fix/Polish · Evidence · Recommendation
Mode: scene-by-scene with fixes applied inline (owner's instruction); evidence in `shots/`.

## 1. Boot — ✅ clean after 1 fix
- AUD-BOOT-1 · Boot.unity `SplashCanvas/Hill` · Remove · Polish · Inactive hill_arc image left from pre-F27 backdrop · **FIXED — deleted** (commit 61b0a82).
- Wiring verified: `Bootstrapper.taglineText/loadingText/splashFill` all connected; strings from `GameText`, timing from `GameRules`, destination via `SceneNames`. Screenshot `shots/boot.png` matches F28 lockup design.

## 2. MainMenu — ✅ clean after 2 fixes
- AUD-MENU-1 · MainMenu.unity `Canvas/Subtitle` · Fix · Should-fix · Baked text "Read, Run, and Summarize!" drifted from `GameText.BootTagline` ("Read! Race! Summarize!") · **FIXED** — controller now sets it from GameText (commit 10c533e).
- AUD-MENU-2 · MainMenuController.cs · Fix · Should-fix · `GameText.TapToStart` existed but nothing used it; start label baked · **FIXED** — wired via new `startLabel` field.
- StartButton → StorySelect confirmed. Screenshot `shots/mainmenu.png`.

## 3. StorySelect — ✅ clean, no changes
- All 18 serialized references wired; all learner strings GameText-driven; locked cards give gentle wiggle sfx (never-punish); hero-art fallback correct. Empty labels in the edit-mode screenshot are runtime-filled by design. Screenshot `shots/storyselect.png`.

## 4. Reader — ✅ clean after 2 fixes
- AUD-READ-1 · ReaderController.cs · Fix · Should-fix · 6 hard-coded learner strings ("NEXT", "NEXT PAGE", "START RACE!", "Great job!", "Not quite — the green one is the answer!", page-progress format) · **FIXED** — moved to GameText (commit d5b38ef).
- AUD-READ-2 · Reader.unity `Canvas/Hill` · Remove · Polish · Dead inactive hill · **FIXED — deleted**.
- AUD-READ-3 · Reader.unity `ReadingCard` · Change · Polish · Card leaves a lot of empty backdrop before NEXT; taller card = more room for Grade-4 text · **flagged, owner call** (visual design).
- Confirmed: NEXT on last page routes to `SceneNames.RaceEndless` (EXP3 race) on this branch. Never-punish question flow correct; narration toggle persists via `PrefKeys.NarrationOn`.

## 5. Race (original, _Game/Scenes/Race.unity) — audited, deliberately untouched
- AUD-RACE-1 · Race.unity briefing/caught texts · Fix · Should-fix · Baked learner strings not in GameText ("PATROL IS COMING!", instructions, "Tag! The patrol caught up…", "Start Mission") · **NOT fixed — scene is pending the race verdict; don't polish a possibly-dead scene.**
- AUD-RACE-2 · RaceController.cs:222 · Fix · Polish · 2 deprecation warnings (`FindObjectsSortMode` obsolete) · fix only if scene survives the verdict.
- Health: 38 objects, no missing scripts, wiring intact, world fully code-built (F31 state). Evidence `shots/race_orig_*`.

## 6. Race (EXP3, Assets/Scenes/MainSummaRace.unity) — ✅ live race, 1 fix, contamination inventoried
- AUD-EXP3-1 · EndlessRaceDirector.cs · Fix · Should-fix · 6 hard-coded learner strings ("You got it!", "Not quite — the glowing one!", "FINISH!", "FINISH", banner formats) · **FIXED** — moved to GameText (commit e89bfb1).
- AUD-EXP3-2 · scene-wide · Remove(-on-ship) · **Blocker for shipping** · Trash Dash chrome physically present: Store/Mission/Leaderboard buttons, char/theme/accessory selectors, currency+premium HUD, tutorial FTUE, **DeathPopup with Ad + Premium buttons**. All masked/neutralized at runtime by the director (verified in code: `MaskLoadoutFlash`, `HideTheirChrome`, lives pinned so death unreachable) — but the objects, scripts, and the ads/analytics/purchasing packages they drag in can never ship. This is the documented branch-level ship-blocker.
- AUD-EXP3-3 · EndlessRaceDirector `_danger` field · Missing · Should-fix · Danger bookkeeping exists with zero consequence and no UI ("Task 5 activates") — either finish the danger meter or delete the bookkeeping.
- Director logic verified: TDD §11.4 sequential gates + re-present + anti-frustration auto-resolve; never-punish enforced; music silenced at FINISH; RaceResult → Arrange. Fields wired (kit sprite + Fredoka).

## 7. Arrange — ✅ clean after 2 fixes
- AUD-ARR-1 · ArrangeController.cs + scene · Fix · Should-fix · 8 learner strings hard-coded/baked (5 status + title/UNDO/VERIFY ORDER) · **FIXED** — GameText + wired label fields (commit e74acd8).
- AUD-ARR-2 · Arrange.unity `Canvas/Hill` · Remove · Polish · **FIXED — deleted**.
- Logic verified: unlimited retries, greens lock, hint after 3 misses, → Summary. TeacherAvatar still TEMP mockup crop (known flag, Phase G art).

## 8. Summary — ✅ clean after 2 fixes
- AUD-SUM-1 · SummaryController.cs + scene · Fix · Should-fix · Title/placeholder/SUBMIT baked, not in GameText · **FIXED** — GameText + wired (commit 107456f).
- AUD-SUM-2 · Summary.unity `Canvas/Hill` · Remove · Polish · **FIXED — deleted**.
- Logic verified: max-2 nudges then always accepts (never grades); reference list SWBST-colored; → Results.

## 9. Results — ✅ clean after 1 fix
- AUD-RES-1 · ResultsController.cs + scene · Fix · Should-fix · "Main Idea" + "NEXT MISSION" baked · **FIXED** — GameText + wired (commit ff3df94).
- Logic verified: stars from first-pick accuracy (min 1), treasure gems match race result, NEXT MISSION → StorySelect (no dead end). Hill here is ACTIVE by design (Results kept the hill look).

## 10. Placeholder scenes — ✅ honest stubs
- Settings, NameEntry, SessionMap, TeacherMenu are completely empty (zero objects) AND unreachable — no code loads their `SceneNames` constants. No dead end possible. `PlaceholderScreen.cs` is currently unreferenced but kept deliberately (GDD §11.6 helper for Phase G stubs).
- AUD-STUB-1 · Settings scene · Missing · Should-fix(Phase G) · GDD expects a Settings surface eventually (voice/volume); currently nothing — schedule with Phase G, don't stub now since it's unreachable.

## 11. Project level
- AUD-PROJ-1 · Packages/manifest.json · Remove(-on-ship) · **Blocker for shipping** · `com.unity.ads` 4.16.4, `com.unity.analytics` 3.8.2, `com.unity.purchasing` 5.4.1 — required by the Trash Dash sample on this branch; must never reach main/ship.
- AUD-PROJ-2 · Assets/Resources/BillingMode.json + IAPProductCatalog.json + Assets/MobileDependencyResolver/ · Remove(-on-ship) · Blocker for shipping · self-resurrected (F19 warning confirmed); pointless to delete while purchasing package is installed — part of the same decontamination.
- AUD-PROJ-3 · Assets/_Recovery/ · Remove · Should-fix · Two Unity crash-recovery snapshots (Boot + MainSummaRace copies, verified redundant) · **FIXED — deleted** (second time this folder resurrected; watch it).
- AUD-PROJ-4 · EditorBuildSettings · OK for this branch · all 12 core scenes present with correct GUIDs + the 4 Trash Dash/EXP3 scenes.
- AUD-PROJ-5 · story-id fallback `"s01_easy"` literal in 7 controllers · Change · Polish · consistent TDD §13 pattern but would be cleaner as one `GameRules.DefaultStoryId` constant.
- Prefabs (PlayerCharacter/PatrolCharacter/PatrolCop) all belong to the original race — their fate follows the race verdict.

## 12. Full-loop playtest notes
Today's changes are string/label-level + dead-object deletions: compile clean after every step, zero null serialized refs in every touched scene, edit-mode screenshots verified. The full runtime loop was live-verified by the owner during EXP3 (per CLAUDE.md). **Owner should re-run the loop once (Boot→Results) to sign off the label changes** — expected visible differences: MainMenu subtitle now "Read! Race! Summarize!", everything else identical.

## 13. Race verdict — recommendation: EXP3 is the race, with a decontamination port as the price
Evidence: the owner's last ~20 commits are all EXP3; Reader already routes to it; the director passes TDD §11.4 (sequential gates, boost/slow, re-present, never-punish) on top of a professionally-tuned runner core the original race approximates but doesn't match (their track recycling, curve shader, input feel). The original race (F31) is fully SummaRace-themed and contamination-free but is a second-best runner and now a second codebase to maintain.

**Recommended path:**
1. Keep EXP3 as the gameplay direction; this branch stays the working branch.
2. Before any device/study build: **decontamination port** — bring the minimal Trash Dash runtime (TrackManager, CharacterInputController/Collider, TrackSegment + themes, curve shader) into a clean branch from main, stripping store/missions/leaderboard/ads/IAP/analytics scripts and the 3 packages. The runtime-masking in EndlessRaceDirector proves which pieces are actually needed.
3. Original `Race.unity` + RaceController stack stays as fallback until the port passes a 2GB-device test, then delete it (with PlayerCharacter/PatrolCharacter/PatrolCop prefabs and CoinPickup/OptionPickup/CurveDip/PlayerRunner/RaceChaseCamera).
4. Never build/ship from this branch as-is (AUD-EXP3-2, AUD-PROJ-1).

## 14. Summary tables

| Scene | Findings | Fixed today | Remaining |
|---|---|---|---|
| Boot | 1 | 1 | 0 |
| MainMenu | 2 | 2 | 0 |
| StorySelect | 0 | — | 0 |
| Reader | 3 | 2 | 1 polish (card size, owner call) |
| Race (orig) | 2 | 0 (pending verdict) | 2 |
| Race (EXP3) | 3 | 1 | 2 (ship-blocker inventory + danger meter) |
| Arrange | 2 | 2 | 0 |
| Summary | 2 | 2 | 0 |
| Results | 1 | 1 | 0 |
| Placeholders | 1 | 0 (Phase G) | 1 |
| Project | 5 | 1 | 4 (3 = ship decontamination, 1 polish) |

**Blockers (ship-path only, not today's loop):** AUD-EXP3-2, AUD-PROJ-1, AUD-PROJ-2 — all one item really: *the decontamination port before the study build*.
**Next actions in order:** ① owner sign-off playtest of today's label changes ② race-verdict decision (recommendation above) ③ decontamination port plan ④ danger-meter finish-or-delete ⑤ Phase G items (Settings, teacher art, hero art, 29 stories).
