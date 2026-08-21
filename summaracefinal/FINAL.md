# SUMMARACE — FINAL (the closeout file)

**2026-08-21 · HEAD `8baa15a` · branch `experiment/endless-override-2` · score 85/100**

This is the ONE file to work from to END the project. It was produced by a 7-agent
full-angle audit (live Unity Editor + repo disk + content + UI spec + ship settings +
data pipeline + code review), all read-only, all evidence-based. Work top to bottom;
tick the boxes. When every box in §3 is ticked, the project is finished.
Companion detail lives in `promp/SummaRace_Remaining_Tasks.md` (the percent ledger),
`SUMMARACE_BLUEPRINT.md` (design) and `SUMMARACE_UI_SPEC.md` (visuals) — this file
supersedes them for *tracking the finish*, not for content.

---

## §1 · The verdict (7 audits, 2026-08-21)

| Audit | Result |
|---|---|
| ① Unity live (via MCP) | 7/8 PASS — GameRules, build list, hero imports, code-features all verified live; **tests could not run** (see F-2) |
| ② Repo disk state | 8/10 PASS — two known blockers confirmed; **manifest surprise** (see F-1) |
| ③ Story content validity | **8/8 PASS** — `flag.py` 0/150, `fit.py` 0 failures on all 6 surfaces, race tell 36.0%, Reader tell 29.3% (both better than documented), longest card 52 chars, 150/150 narration + 30/30 heroes resolve |
| ④ UI spec ↔ disk | Every spec element exists on disk except ONE — the optional Results finish-time line (decision D3). Six ⏳ "to-build" items are in fact already built (see F-4) |
| ⑤ Ship settings | **13/14 PASS** — portrait/IL2CPP/ARM64/API26/GLES3/input-Both/identity/Link.xml/services-off/12 scenes all correct; only adaptive-icon *assignment* open (files exist) |
| ⑥ Data pipeline | **10/10 PASS** — schema 6 code↔dictionary match 47/47 keys + 7/7 racePick keys; first-answer-only holds; codes stamped/refused/warned/backfilled; export/wipe/atomic/partial all correct. 3 doc nits found → **fixed 2026-08-21** |
| ⑦ Code review (recent features) | **CLEAN** — no bugs, no invariant violations (never-punish, measure-sacred, coroutine safety, null-safety, offline all verified) |

**Bottom line:** the game, content, and data pipeline are study-ready. What remains is
operational: one editor restart + test rerun, one playtest, one build day, one install day,
one email.

## §2 · Findings from this sweep

- [x] **F-1 · RESOLVED (verified live 2026-08-21 evening):** `com.adjoint.editor` is no
  longer in the working-tree manifest — the collision package is gone again.
- [x] **F-2 · RESOLVED (verified live 2026-08-21 evening):** the editor assembly is loaded
  (`Assembly-CSharp-Editor` in the domain, DLL in `ScriptAssemblies`, Preflight type + 11
  fixture types present) and the **full EditMode suite ran over MCP: 57 / 57 passed, 0
  failed, 4.3 s**. Editor tooling is fully back.
- [x] **F-3 · Three doc corrections** (found by audit ⑥, applied 2026-08-21): researcher
  email "point 7"→"point 6"; runbook no longer claims codes "cannot be back-filled"
  (export backfills them since F56); runbook §5.2 now lists the third Export outcome
  ("Export failed…" ≠ "no logs").
- [ ] **F-4 · `SUMMARACE_UI_SPEC.md` has 6 stale lines** (disk is ahead of the spec — update
  the spec, build nothing): ① Arrange 2-column pool is FINISHED (not "interrupted");
  ② coins-between-gates are deliberately absent in the endless race (EXP3 design);
  ③ reading window decision is closed at `RaceSecondsPerGate = 20`; ④ "superstar" praise
  deliberately reworded to process praise; ⑤ briefing patrol line deliberately softened;
  ⑥ ⏳ items 2–7 (gate timer, cameo, Reader hints, Summary ghost+tips, Results badge,
  chip tints) are all BUILT. Plus the 2 already-recorded ones (SessionMap grid; orange pill).
  ~15 min of spec editing, zero code.
- [ ] **F-5 · CLAUDE.md validity numbers can be refreshed** (better, not contradicted):
  race widest-card tell 45.3% → **36.0%**, Reader longest-tell 31.0% → **29.3%**, longest
  card 53 → **52 chars**. Optional 5-min doc tidy.
- [ ] **F-6 · Adaptive icons: assign in Editor.** The two 432×432 layers exist
  (`Assets/_Game/Art/UI/app_icon_adaptive_bg.png` + `_fg.png`); all 18 Android
  adaptive/round/legacy `m_BuildTargetPlatformIcons` slots are empty. Player Settings ▸
  Android ▸ Icon ▸ Adaptive. 10 min, do it on build day (Task 7 window).
- ℹ️ **F-7 · Windows Addressables content exists** (built 2026-08-13) — proves the content
  pipeline works; changes nothing for Android. No action.
- ℹ️ **F-8 · Results finish-time is logged (`raceRunSeconds`) but not displayed** — the only
  spec element with zero code anywhere. That is decision **D3** (ruling: YES, ~30 min),
  not a defect. Slot into the Task 3 window if approved.

## §3 · THE FINISH LINE — do these in order, tick as you go

**Gate 0 — editor health ✅ CLOSED 2026-08-21**
- [x] Editor assembly loaded, Preflight present, manifest clean, and EditMode suite run
  live over MCP: **57 green / 0 red** (4.3 s). Nothing stands before Gate 1.

**Gate 1 — the playtest (Task 3, +5 → 90) — ✅ MECHANICALLY VERIFIED 2026-08-21 evening
(Claude drove the FULL loop live over MCP, portrait 720×1280, on `s05_easy` — a
never-played session-5 story); owner feel-pass still wanted**
- [x] Full loop completed with zero dead ends: Boot → MainMenu → map (session 5 glow) →
  StorySelect (chip tints live) → Reader (Q1 deliberately wrong: "Not quite — here is the
  answer!" · HEAR AGAIN fired · VOICE OFF→ON persisted to prefs · A./B./C. + 💡 slot hints
  rendered) → Race in the **overcast-industrial world with LIVE RAIN**: briefing + softened
  patrol line + chips + Lumi · single countdown · **"Next part in 7s→1s" chip counting
  honestly** · reading panel mirrored the road cards exactly · steered by lane · gates went
  wrong-right-wrong-right-wrong on purpose · **cameo captured on camera: cop sweeps the
  OPPOSITE shoulder, amber vignette, gold answer reveal, tracker fills, timesCaught 0** ·
  pause → KEEP RUNNING resumed at speed 11.3 (no reseed) → FINISH → Arrange (2-column
  yellow pool · "Almost! The parts already in place are right" · locked slots) → Summary
  (story-specific ghost · tips · verbatim echo) → Results (**1 star = exactly 2/5 correct;
  gems lit ONLY for W and SO — matching the run pick-for-pick**) → NEXT STORY → StorySelect
  with AVERAGE unlocked and best-stars 1 persisted. **Log row verified on disk: s05_easy,
  schemaVersion 6, starsEarned 1, timesCaught 0, isPartial false, summaryText verbatim.**
  Evidence: 20 screenshots + a 20-frame burst in the session scratchpad.
- [x] Hero art spot-checked in StorySelect — real art renders crisply post-resize.
- [ ] **Owner feel-pass (the human half that MCP cannot judge):** does the reading window
  feel right at real speed (D1/D2)? · sound/narration by ear · real touch input ·
  decide **D3** (finished-time on Results, ~30 min).
- **Findings from the drive:** ⓐ **FIXED** — Reader's Ms. Lumi rendered ~2.1× too big and
  covered the story text (`Teacher` localScale 2.11 authored for the pre-F58 sprite; now
  1.0, re-rendered clean). ⓑ **OPEN, cosmetic** — StorySelect: a long story title runs
  under the PLAY badge ("The Crowded House: A Folktale"); needs a title-width clamp or
  badge-aware wrap. ⓒ Editor-only: Trash Dash's focus-pause freezes the race when the
  Unity window loses focus mid-drive — irrelevant on a tablet (app always focused).

**Gate 2 — build day (Tasks 5–8, one focused day, +4 → 94)**
- [ ] Install Android Build Support (+OpenJDK+SDK/NDK) for **6000.4.1f1**, Editor closed.
- [ ] Switch platform to Android (30–90 min reimport — start it and walk away).
- [ ] `SummaRace ▸ Build Preflight` → fix every ✖ · assign adaptive icons (F-6) ·
  resolve F-1 consciously (commit it or revert it — do not build with a dirty manifest).
- [ ] Build APK #1 (App Bundle OFF, Development OFF) → `git tag study-build-v1` →
  **back up `%USERPROFILE%\.android\debug.keystore` next to the APK** · same machine forever.

**Gate 3 — device proof (Tasks 9–10, +4 → 98)**
- [ ] Smoke test one tablet, the 7 checks in order (race starts · full non-s01 loop ·
  airplane mode · BACK inert · tap-to-lane · ~30 fps · narration + VOICE persists).
  If fps misses: `RaceMaxSceneryPerSegment` 14 → 7, rebuild.
- [ ] Prove the data path: play a story → PIN → Export → `adb pull
  /sdcard/Android/data/com.orbitsdev.summarace/files/` → open the `.jsonl`: participantCode
  present · schemaVersion 6 · raceFirstPickCorrect matches the stars · readingSeconds > 0.
  **If adb pull fails on this tablet model: STOP — public-export-folder code change first.**

**Gate 4 — deployment (Task 11, +1.5 → 99.5)**
- [ ] 40 tablets: install same APK → set same PIN → set each learner's participant code
  (from their paper booklet) → child names + avatar → unlock session 1 → label tablet with
  the code. One tablet per learner, same tablet all 10 sessions. Export after EVERY
  session day.

**Gate 5 — the email (Task 12, +0.5 → 100)**
- [ ] Send `promp/Researcher_Email_Draft.md` (typo already fixed). Her replies gate the
  content freeze, not the score.

**Cleanup along the way (no score weight)**
- [ ] F-4 UI-spec sync (~15 min, Claude can do on "go")
- [ ] F-5 CLAUDE.md number refresh (~5 min, Claude)

## §4 · Decisions still open (owner veto; zero score weight)

Full table: Remaining-Tasks §⚪. In one line each — D1 reading window (test in Gate 1;
lean 17s paired with SecondsPerGate~29 **only if** the playtest feels tight — note the
constants already give 17.1–23.2 s/gate) · D2 timer-chip visibility (keep 12) · **D3
finished-time on Results (YES — the one unbuilt spec item, F-8)** · D4 coach line (NO) ·
D5 hint button (NO) · D6 one tablet/learner (locked into Gate 4) · D7 accept passive runs ·
D8 skip change-PIN · **D9 keep TeacherMenu + PIN (asked & answered 2026-08-21: it carries
session gating, participant codes, and export — post-study it can go)**.

## §5 · After the study (parked — spend zero pre-study time)

Repo → private · legacy `Race.unity` + `Ch46_nonPBR.fbx` removal · 8 unused packages ·
Trash Dash inactive objects · CC0 audio swaps · girl runner · park-style track art ·
missions finish-to-unlock (the D9 swap).

---
*Everything not listed here was verified done by the 2026-08-21 seven-agent audit and is
committed on `experiment/endless-override-2`.*
