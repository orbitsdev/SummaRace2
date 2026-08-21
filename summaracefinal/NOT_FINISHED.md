# SUMMARACE — WHAT IS NOT FINISHED YET (nothing else)

**2026-08-21 evening · verified LIVE via Unity MCP (tests 57/57 green) · score 85/100**

This file lists ONLY the unfinished work. Everything not on this page was verified done
today by the seven-agent audit + live MCP checks (see `FINAL.md` for the evidence).
Each item says WHO does it, HOW, and HOW WE KNOW it's done. Cross the items off here
and in `FINAL.md` §3 as they close.

---

## 1 · The playtest — Task 3 (+5 → 90) — **the only thing Claude + you can do TODAY**
**Who:** you play in the Editor (Claude drives/screenshots over MCP alongside).
**How:** Play from Boot, pick a **session-5+ story** (never played before) and run the
whole loop: Reader (HEAR AGAIN · VOICE off/on · BACK before first answer) → Race
(briefing · one countdown · "Next part in Ns" chip · panel-tap steering · one deliberate
wrong pick to see surge + cameo + reveal · pause RESUME · pause LEAVE on a second run) →
Arrange (miss 3× to see the hint, 4× the assist) → Summary (ghost text, DONE TYPING) →
Results (stars/gems match the run) → NEXT.
**Done when:** the loop completes with no dead end and no visual break vs
`SUMMARACE_UI_SPEC.md`; while playing, decide **D1/D2** (does the reading window feel
right?) and **D3** (want "Your race: 1:42" on Results? ~30 min to add — the ONLY spec
element that exists nowhere in code).

## 2 · Android Build Support — Task 5 (+1 → 91)
**Who:** you. **How:** close Unity → Unity Hub → Installs → **6000.4.1f1** → Add modules →
Android Build Support + OpenJDK + SDK/NDK (~2 GB).
**Done when:** `...\6000.4.1f1\Editor\Data\PlaybackEngines\AndroidPlayer\` exists.
*(Verified today it does NOT — this is still the hard blocker for any APK.)*

## 3 · Platform switch — Task 6 (+0.5 → 91.5)
**Who:** you, the day before build day. **How:** File ▸ Build Profiles ▸ Android ▸ Switch
Platform; the 30–90 min "hang" is the reimport — walk away.
**Done when:** editor title bar / Build Profiles shows Android as active target.

## 4 · Preflight clean + adaptive icons — Task 7 (+0.5 → 92)
**Who:** both. **How:** `SummaRace ▸ Build Preflight` → fix every ✖ (the two Android rows
should clear after items 2–3; the Addressables row stays red until the first build — that
is correct). While there: **assign the adaptive-icon layers** (files already exist:
`Assets/_Game/Art/UI/app_icon_adaptive_bg.png` + `_fg.png`) in Player Settings ▸ Android ▸
Icon ▸ Adaptive — verified today all 18 slots are still empty.
**Done when:** Preflight shows 0 ✖ apart from the documented pre-first-build Addressables row.

## 5 · APK #1 — Task 8 (+2 → 94)
**Who:** you. **How:** Build Profiles: App Bundle OFF, Development OFF → Build outside the
repo → `SummaRace_v1.0_vc1_YYYYMMDD.apk`. Immediately after: `git tag study-build-v1` and
**copy `%USERPROFILE%\.android\debug.keystore` next to the APK** (debug signing is
per-machine — every future study APK must come from THIS machine).
**Done when:** the .apk file exists and is ≤300 MB.

## 6 · One-tablet smoke test — Task 9 (+2 → 96)
**Who:** you. **How:** enable USB debugging on the tablet → `adb install -r <apk>` → the
7 checks in order: ① race actually starts (stuck "Getting ready…" = Addressables didn't
ship → rebuild) ② full non-s01 loop ③ airplane mode whole story ④ Android BACK inert
⑤ tap-to-lane works ⑥ ~30 fps (if not: `RaceMaxSceneryPerSegment` 14→7, rebuild)
⑦ narration plays, VOICE survives app kill.
**Done when:** all 7 pass on a real device.

## 7 · Export proof — Task 10 (+2 → 98) — **non-negotiable before Day 1**
**Who:** you. **How:** play one story on the tablet → PIN → Export → on PC:
`adb pull /sdcard/Android/data/com.orbitsdev.summarace/files/` → open the export .jsonl:
`participantCode` present · `schemaVersion: 6` · `raceFirstPickCorrect` matches the stars
you saw · `readingSeconds > 0`.
**Done when:** the pulled file passes those four checks. **If adb pull fails on this
tablet model, STOP — a public-export-folder code change comes before anything else.**

## 8 · 40-tablet install + ritual — Task 11 (+1.5 → 99.5)
**Who:** you (+ teacher). **How per tablet (~3 min):** install same APK → same PIN →
that learner's participant code from their paper booklet → child types name + avatar →
unlock session 1 → label the tablet with the code. One tablet per learner, the same
tablet all 10 sessions, export after every session day.
**Done when:** 40 tablets labeled and handed over.

## 9 · Send the researcher email — Task 12 (+0.5 → **100**)
**Who:** you. **How:** open `promp/Researcher_Email_Draft.md` (typo already fixed), edit
the greeting, send. Her replies gate the content freeze, not the score.
**Done when:** sent.

---

## Small non-scored leftovers (any "go" and Claude does them)
- **UI-spec sync** — 6 stale lines where the build is ahead of the spec (~15 min).
- **CLAUDE.md validity numbers** — refresh 45.3%→36.0%, 31.0%→29.3%, 53→52 chars (~5 min).
- **D3 finished-time on Results** — if you approve it during the playtest (~30 min).

## NOT tasks (do not spend time)
Owner decisions D1–D9 (recorded with rulings in `promp/SummaRace_Remaining_Tasks.md` §⚪) ·
everything in the post-study park list (`FINAL.md` §5).

---
**How this project finishes, in one sentence:** playtest one unseen story (item 1), then
one build day (items 2–5), one device day (items 6–7), one install day (item 8), one
email (item 9) — and the score is 100 by construction.
