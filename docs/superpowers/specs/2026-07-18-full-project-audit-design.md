# Full-Project Audit — Design Spec

**Date:** 2026-07-18 · **Branch:** `experiment/endless-override-2` · **Status:** approved approach, pending owner review of this spec

## Goal

A complete professional audit of SummaRace — every game scene, its objects, prefabs, and driving scripts — to find flaws, wrong labels, unnecessary logic/objects, gaps, and anything not aligned with the project goal (Grade-4 SWBST thesis instrument, offline, never-punish). Output: one findings document + a prioritized fix plan.

The audit itself is **read-only**. Nothing is changed during the audit; all changes come from the plan it produces. Exception: none.

## Order of work (owner's instruction: start at the very beginning)

Scenes are audited in **play order**, race last:

1. `Boot.unity`
2. `MainMenu.unity` (home)
3. `StorySelect.unity`
4. `Reader.unity`
5. Race — **both** candidates, side by side:
   - `_Game/Scenes/Race.unity` (original, F4–F31 polish)
   - `Assets/Scenes/MainSummaRace.unity` (EXP3, Trash Dash engine)
6. `Arrange.unity`
7. `Summary.unity`
8. `Results.unity`
9. Placeholder scenes: `Settings`, `NameEntry`, `SessionMap`, `TeacherMenu` (verify they are honest never-dead-end stubs, nothing more)
10. Project level: EditorBuildSettings scene list, `Packages/manifest.json` (ads/IAP contamination), Addressables groups, `Assets/_Recovery/`, orphaned root assets (`Assets/Scenes/Main|Shop|Start.unity`), `_Game/Prefabs/`, `_Game/Scripts/` dead-code sweep

## Method

Live Unity Editor via MCP (approach A) with playtest checks folded in (approach C):

- Open each scene, enumerate the full hierarchy (including inactive objects), inspect component wiring and serialized fields.
- Screenshot each screen (portrait game view) as evidence.
- Cross-check against: GDD locked decisions D1–D18, TDD (scenes/events/race spec), `GameText`/`GameRules`/`SceneNames` constants, Mockups 1–27, SWBST palette.
- Static pass on the scripts each scene uses: hard-coded strings/numbers, cross-feature calls bypassing EventBus, unused fields/members.
- Full-loop play-through at the end to catch flow/UX issues.

## Judging rubric — every finding scored from three angles

- **QA:** broken/missing wiring, dead ends, wrong/mislabeled text, objects that do nothing, console errors/warnings.
- **Developer:** unnecessary logic, duplication, violations of project architecture rules (EventBus-only communication, Constants-only strings/numbers, JSON-only content, singletons pattern).
- **Designer:** alignment with GDD D1–D18 and mockups, SWBST color language, never-punish rule, Grade-4 readability (font size, wording, contrast), portrait layout.

## Finding format

Each finding: **ID · Scene/asset path (or file:line) · Classification (Remove / Fix / Change / Missing) · Severity (Blocker / Should-fix / Polish) · Evidence · Recommendation.**

- **Blocker** = breaks the study loop, violates a non-negotiable (offline, never-punish, content-as-data), or is shipping contamination (ads/IAP).
- **Should-fix** = wrong label, misaligned behavior, dead object, architecture violation.
- **Polish** = cosmetic/quality gap below mockup standard.

## Race verdict

After auditing both races, the findings doc ends with a **keep/kill recommendation**: which race ships, what porting/decontamination work the choice implies (EXP3 branch carries ads/IAP packages and can never ship as-is), and what the losing race's removal entails.

## Deliverables

1. `docs/superpowers/audits/2026-07-18-full-project-audit.md` — the findings document (per-scene sections in play order, screenshots referenced, race verdict, summary tables).
2. An implementation plan (via superpowers:writing-plans) ordered: Blockers → Should-fix → Polish, with study-blocking items first.

## Out of scope

- Third-party packs under `Assets/Plugins/` (audited only where the game references them).
- Story content review (`s01_easy.json` correctness is the researcher's call — flagged, not judged).
- Documentation rewrites.
- Any fixes/changes during the audit itself.
