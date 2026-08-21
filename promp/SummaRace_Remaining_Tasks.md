# SUMMARACE — REMAINING TASKS, UPDATES & FIXES
### Every open item, in execution order, with exact steps, owner, time, and how to verify

**2026-08-21 (rev 4, HEAD `7bb6fec`+). CURRENT SCORE: 85/100 — Tasks 1, 2 and 4 are DONE.**
Everything NOT listed here is done, committed, and pushed (branch
`experiment/endless-override-2`). Companion docs: `summaracefinal/SUMMARACE_BLUEPRINT.md`
(design/logic — incl. §15 acceptance argument) · `SUMMARACE_UI_SPEC.md` (visuals) ·
`promp/Researcher_Email_Draft.md` (Task 12, ready to send).

**Completion snapshot: 80/100 — and this list is the exact remaining 20.** Each task carries
its credit below; complete them all and the project is 100 by construction. The ⚪ decisions
and ⚫ post-study items carry **zero weight** — 100% never depends on optional work.

### The percent ledger (80 → 100)

| Task | Credit | Running total |
|---|---|---|
| ✅ 1 — package collision removed, Editor tooling back — **DONE** (`7bb6fec`; `Assembly-CSharp-Editor.dll` compiled 14:32) | +2 | **82 ← we are here** |
| ✅ 2 — **DONE**: suite ran for the FIRST TIME in project history — **57 green / 0 red** (56 SummaRace fixtures + the Addressables stub), owner-witnessed | +2 | 84 |
| ▶ 3 — full-loop portrait playtest clean (non-s01) — **NEXT** | +5 | 90 (after T3) |
| ✅ 4 — **DONE** (`e645bee`): all 30 heroes resized 1020×680 (÷4, exact 3:2) — compression unblocked; sources 57.2→30.8 MB; spot-check ASTC format during Task 3 | +1 | **85 ← we are here** (T3 pending) |
| 5 — Android Build Support installed | +1 | 91 |
| 6 — platform switched to Android | +0.5 | 91.5 |
| 7 — Preflight: every ✖ fixed | +0.5 | 92 |
| 8 — APK #1 built, tagged, keystore backed up | +2 | 94 |
| 9 — one-tablet smoke test passed (all 7 checks) | +2 | 96 |
| 10 — `adb pull` export proven, row verified | +2 | 98 |
| 11 — 40 tablets installed + full ritual | +1.5 | 99.5 |
| 12 — researcher email sent (her replies gate content freeze, not the %) | +0.5 | **100** |

*(Weights follow risk retired, not hours: Task 3 retires the most unknowns, Tasks 9–10
retire the on-device unknowns, Task 12 is credit for sending — the reply is her clock.)*

**Legend:** 🔴 blocker · 🟠 do immediately after · 🟡 ship pipeline · 🔵 other people's clocks ·
⚪ decision · ⚫ optional/post-study

---

## ✅ TASK 1 — DONE 2026-08-21 (`7bb6fec`) — package collision removed
`com.adjoint.editor` deleted from the manifest; `Assembly-CSharp-Editor.dll` compiled for the
first time ever (14:32). Preflight menu + test fixtures + Play mode are unblocked. Steps kept
below only as the record of what was done.

<details><summary>original task text</summary>

**Problem:** `com.adjoint.editor` (Adjoint toolbar, line 3 of `Packages/manifest.json`) and
`com.coplaydev.unity-mcp` (the MCP bridge) both ship a type named `AssetPathUtility` →
`CS0433` → `Assembly-CSharp-Editor` never compiles → **all ~56 test fixtures unloadable,
`SummaRace ▸ Build Preflight` menu absent, Play mode blocked.** The runtime game code is
unaffected and compiles clean.

**Exact steps:**
1. Close Unity.
2. Open `Packages/manifest.json`, delete the line:
   `"com.adjoint.editor": "https://github.com/adjointdev/adjoint-beta-dist.git",`
3. Reopen Unity, let it resolve + recompile (~1–2 min).
4. **Verify:** menu **SummaRace ▸ Build Preflight** exists · Test Runner shows the SummaRace
   fixtures · Play mode enters.

**Owner:** you (or say "go" and Claude does the edit — Unity must be closed/reopened by you).
**Time:** 2 min + reopen. **Risk:** none to the game — the package is an editor toolbar only;
if you use the Adjoint toolbar daily, the alternative is updating one of the two packages to
a version without the clash.

---

</details>

## 🟠 TASK 2 — Run the full test suite and record the real number ← **YOU ARE HERE**

**Why:** every quoted count ("45/45", "53/53") is stale; the suite has NEVER run with the
newest fixtures (incl. `PatrolCameoGeometryTests`).
**Steps:** after Task 1 → Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All.
**Verify:** all green; write the count into the blueprint §11 phase 5. Investigate ANY red
before proceeding — these fixtures guard the study's validity (offline compliance, card
tells, cameo geometry, export backfill…).
**Owner:** Claude. **Time:** 5 min.

## 🟠 TASK 3 — Full-loop PORTRAIT playtest on a NON-s01 story

**Why:** nine recent changes have **never been rendered or played**: race part-timer chip
("Next part in 8s"), patrol cameo sweep, Reader hint lines, Story-Select chip tints at
runtime, Arrange dashed frame + yellow pool + Lumi bubble line, Summary ghost placeholder +
tips block, Results Lumi badge + reworded praise. Also 27 of 30 stories have never been
played end-to-end at all.
**Steps:** Play from Boot → pick a session-5+ story (different world, night/mist variety) →
Read all 5 pages (test HEAR AGAIN, VOICE off/on, BACK before first answer) → Race (verify:
briefing voiced · ONE countdown · timer chip counts honestly · panel taps steer · wrong pick
= amber surge + cameo sweep + answer reveal + rotation line · tracker fills · pause →
RESUME and pause → LEAVE both behave) → Arrange (hint after 3 misses · assist after 4 ·
locked-slot nudge) → Summary (ghost text is ghost-only · nudges ≤2 then accept · DONE
TYPING) → Results (star count matches the run · gems match per-element · own sentence shown)
→ NEXT STORY routing.
**Verify against:** `SUMMARACE_UI_SPEC.md` screen by screen; screenshot anything off.
**Owner:** you playing (Claude can drive + screenshot over MCP). **Time:** 30–60 min.

## 🟠 TASK 4 — Hero-art import fix (~50 MB off the APK)

**Problem:** the 30 hero PNGs are 1024×683 — 683 is not divisible by 4, so Android texture
compression silently fails and each ships **uncompressed RGB24 (~2 MB each, ~59 MB total)**.
**Steps:** select all 30 in `Assets/_Game/Resources/Stories/Art/` → Inspector: Max Size 1024,
**Non-Power of 2: ToNearest** → Android platform override ON, format **ASTC** → Apply.
**Verify:** any one asset's Inspector footer shows a compressed format (ASTC/ETC2) and
~0.3–0.7 MB, not RGB24/2 MB. Then spot-check 2–3 cards in Story Select for visual quality.
**Owner:** Claude (import settings via editor). **Time:** ~1 h incl. reimport.

---

## 🟡 THE SHIP PIPELINE (one focused day, in this exact order)

### TASK 5 — Install Android Build Support  ← *the only reason no APK exists*
Close Unity → Unity Hub → Installs → **6000.4.1f1** (exactly) → ⚙ Add modules → tick
**Android Build Support + OpenJDK + Android SDK & NDK Tools** → install (~2 GB).
CLI alternative:
`"C:\Program Files\Unity Hub\Unity Hub.exe" -- --headless install-modules --version 6000.4.1f1 --module android --childModules`
**Verify:** `C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\PlaybackEngines\AndroidPlayer\`
exists and is non-empty. **Owner:** you. **Time:** 10–30 min unattended.

### TASK 6 — Switch platform to Android *(the day BEFORE build day)*
File ▸ Build Profiles ▸ Android ▸ **Switch Platform**. Re-imports 945 MB of assets for
Android compression — looks like a hang for 30–90 min; it isn't. Start it and walk away.
**Owner:** you. 

### TASK 7 — Build Preflight, fix every ✖
**SummaRace ▸ Build Preflight** (read-only, never breaks anything). Expect the two Android
rows to clear after Tasks 5–6; the **Addressables row stays red until the first player
build** — that is correct, not a failure. Ignorable WARNs: Google EDM (editor-only);
adaptive-icon slots — the layers exist (`Assets/_Game/Art/UI/app_icon_adaptive_bg.png` +
`_fg.png`, 432×432): assign them in Player Settings ▸ Android ▸ Icons ▸ Adaptive if you
have 10 minutes. **Owner:** both. **Time:** 15 min + findings.

### TASK 8 — Build APK #1
File ▸ Build Profiles: **Build App Bundle = OFF** (an .aab cannot be sideloaded),
Development Build = OFF → **Build** → output OUTSIDE the repo → name
`SummaRace_v1.0_vc1_YYYYMMDD.apk`. Addressables content builds with the player
automatically. Likely first-build snags: Gradle/JDK paths → Edit ▸ Preferences ▸ External
Tools → tick every "…installed with Unity" box.
**Immediately after success:** `git tag study-build-v1` · **copy
`C:\Users\Owner\.android\debug.keystore` next to the APK.** ⚠ The APK is debug-signed and
that keystore is per-machine: an APK built on any other PC will NOT install over the
deployed one, and the forced uninstall erases that tablet's learners and unexported logs.
**Every study APK from this machine, forever.**
**Owner:** you. **Time:** 20–45 min (first IL2CPP build).

### TASK 9 — Smoke test ONE tablet
Tablet: Settings ▸ About ▸ tap Build Number ×7 → Developer options → **USB debugging ON** →
`adb install -r SummaRace_v1.0_vc1_YYYYMMDD.apk`. Test in this exact order:
1. **The race starts.** Stuck on "Getting ready…" → Addressables content did not ship (the
   #1 predicted failure; the game offers a way back, but the BUILD is wrong — rebuild).
2. Full loop on a **non-s01** story (session 5+).
3. **Wi-Fi off + airplane mode**, whole story.
4. **Android BACK does nothing** (never quits mid-story).
5. **Tap-to-lane** works (screen thirds AND panel columns).
6. Race holds ~**30 fps**. If not: `GameRules.RaceMaxSceneryPerSegment` 14 → 7, rebuild —
   the documented first (and only race-specific) lever.
7. Narration plays; VOICE choice survives killing the app.
**Owner:** you. **Time:** 30–60 min.

### TASK 10 — Prove the data path (NON-NEGOTIABLE before Day 1)
Play one full story on the tablet → teacher corner → PIN → **Export** (full path shown on
screen) → on the PC:
`adb pull /sdcard/Android/data/com.orbitsdev.summarace/files/`
(`Android/data` is invisible to normal USB browsing on Android 11+ — adb is the way).
**Verify inside the export_*.jsonl:** `participantCode` present · `schemaVersion: 6` ·
`raceFirstPickCorrect` matches the stars you saw · `readingSeconds` > 0.
**If adb pull fails on this tablet model: STOP — a code change (public export folder) comes
before anything else.** The logs are the entire dataset; there is no second chance.
**Owner:** you. **Time:** 15 min.

### TASK 11 — Install all 40 tablets (+ the ritual, per tablet ~3 min)
`adb install -r` the SAME apk file → leave USB debugging ON → launch once → teacher corner →
**set the PIN** (same PIN on all 40; record it once in the study notes) → **set that
learner's participant code** (copied from their paper booklet; duplicates are refused) →
child types their name + picks an avatar → **Unlock session 1** → hand over.
**During the study:** export after EVERY session day · learner switching only via the PIN
menu · lost PIN = the 6-s hold recovery (ERASES the tablet — export first) · post-study:
Delete all data per tablet (keeps the PIN; removes every child's data incl. old exports —
the consent promise).

---

## 🔵 TASK 12 — The researcher email — **DRAFTED, ready to send**
Full text sits in **`promp/Researcher_Email_Draft.md`** (seven points, each with a
recommendation and a paste-back reply template). Edit greeting → send → +0.5 lands.
The seven points, for reference:

One email, seven points (evidence packs live in `Documentation/`):
1. **Content sign-off + freeze request** — ~130 machine-edited strings across the validity
   passes (`SummaRace_Content_QA_Report.md`, `SummaRace_Story_Alignment_Audit.md`).
2. `s01_easy`'s five questions were app-authored following her own SWBST pattern (her doc
   gives Day-1 Easy only a passage) — formality sign-off.
3. **s05_hard & s06_hard are both titled "In Grandfather's Day"** with contradicting
   SOMEBODY answers (Grandfather vs Sharr and Kaze) — keep / revise / record as limitation
   (recommend: record).
4. The reported **s03_easy** difficulty-label question.
5. **Three Chapter-3 wording fixes** so the thesis matches the app:
   - p.28 "a patrol actively attempts to catch the player" → "the patrol pressures the
     runner after wrong choices; it never catches."
   - p.29 "no explicit SWBST guide… is displayed" at summary → the reference list IS
     displayed (in her own prototypes too) — correct the sentence or ask us to hide the list.
   - p.29 "the system automatically evaluates the correctness, coherence, and completeness"
     → "guided checks and reflective prompts; sentences recorded verbatim for rubric scoring."
6. Confirm the **pretest story ("Playground") doubling as s01_easy** is intentional.
7. The limitation number to cite: race 59.3% passable from Reader-memory (34.0% misaligned
   control); **excluding character names 49.2% vs 33.3% chance** — irreducible (all 25
   residuals are SOMEBODY names).
**Owner:** you send; Claude can draft the full email text on request. **Time:** 30 min.

---

## ⚪ OPEN DECISIONS — with Claude's RULINGS (owner holds the veto; zero ledger weight)

| # | Decision | Claude's ruling | Grounding |
|---|---|---|---|
| D1 | Reading window **12 s vs 17 s** | **Test first, lean 17** — read one race slowly in Task 3; if tight, take 17 **paired with** `RaceSecondsPerGate`→~29 (17 alone collapses difficulty on 2 of 3 levels) | the study *selects* low-mastery readers (p.25) — its own population argues for time |
| D2 | Timer chip visibility (`RaceGateTimerVisibleSeconds` 12/5/0) | **Keep 12** — it aids pacing; drop to 5 only if Task 3 shows clock-fixation | honest version of the prototype's clock |
| D3 | Finished time on Results ("Your race: 1:42!") | **YES — do it** (~30 min, slot into the Task-3 window) | prototype's racing-clock spirit at zero validity cost |
| D4 | Reader persistent coach line | **NO for the study** — clutter vs the story text; post-study | Reader already gained better equivalents (narration, hints, tips) |
| D5 | NEED HINT? button on Arrange | **NO** — a button gives confident kids help and shy kids none = **uneven scaffolding across learners**; the auto-hint reaches all 40 equally | uniform support is a validity property |
| D6 | Tablets per learner | **One per learner** | identity mixups are unrecoverable after export |
| D7 | Passive-run stance | **Accept + report** — detectable in `racePicks[].lane`; a countermeasure would punish non-steering | D7/never-punish |
| D8 | "Change PIN" button | **Skip for the study** — set once, same PIN ×40, recorded once; post-study convenience | less gate surface = fewer study-day failure modes |

## ⚫ EXPLICITLY POST-STUDY (do not spend pre-study time here)

Park/playground-style track art matching the prototype look · a second (girl) runner tied to
the avatar picker · CC0 replacements for the two untraceable audio files (`music_menu.mp3`,
`sfx_click.mp3`) · delete legacy `Race.unity` from Build Settings + `Ch46_nonPBR.fbx`
(≈12–20 MB APK, only if size is tight — measure first) · remove the 8 unused packages ·
strip Trash Dash's inactive scene objects · make the GitHub repo private (it currently
exposes paid asset-store content) · two tiny UI-spec text syncs (Session Map is a grid;
TAP TO START is an orange pill).

## ✅ SANITY: what is already DONE (do not redo)

All 11 screens built & portrait-render-checked · race core (gates/tracker/panel/steering/
pause/worlds/weather) incl. part-timer + patrol cameo · Arrange side-by-side yellow pool +
dashed frame + hint/assist ladder · Summary ghost + tips + checks · Results full reveal +
own-sentence card + Lumi · all 30 stories + 160 voice clips + 30 real hero images ·
logging schema 6 + participant codes + atomic saves + export/wipe · teacher gate complete ·
device-RAM budget applied · offline enforced by test · all finalization docs
(`summaracefinal/`) · everything committed & pushed.

**Also settled (owner audits, answered & recorded — don't reopen):**
- **The PIN is in NO prototype** — correct, and deliberate: it's a beyond-prototype addition
  required by the study itself (p.29 uniform exposure, p.33 data handling). The web demo even
  shows "DAY 2 🔒" with no mechanism to open it — the PIN is that missing key. Recorded as
  its own row in the blueprint's parity matrix (§15.1), ready for the panel question.
- **Timers:** prototypes had 3 countdowns (race/arrange/summary); the build ships the honest
  race part-timer only, arrange/summary untimed, durations logged invisibly (§9.0 policy).
- **Two lock systems:** Easy→Average→Hard unlocks by finishing (normal game); missions unlock
  by PIN (research dose control). Post-study, one condition swap makes missions
  finish-to-unlock too.
- **Arrange pool** now matches the prototype: side-by-side wrapped pills (2+2+1), yellow vs
  the slots — layout verified by portrait render.
- **Scratch scenes:** only ONE leftover actually ships — legacy `Race.unity` (build index 6,
  zero call sites, ~12–20 MB of APK via its 51.5 MB dependency closure; drop only if the
  measured APK nears 300 MB). Trash Dash's own Main/Start/Shop, the _Recovery autosaves and
  ~75 plugin/TMP demo scenes are NOT in Build Settings and cost the APK nothing — leave them
  until post-study.

---

### The short version (updated)
**✅ Done:** Task 1 (package collision removed, `7bb6fec`) · Task 12 draft
(`promp/Researcher_Email_Draft.md` — sending it lands the credit).
**Now:** Task 2 (Test Runner ▸ EditMode ▸ Run All — record the number) → Task 3 (full-loop
portrait playtest, non-s01; includes D1/D2 observation and, if approved, D3's 30-min
finished-time addition) → Task 4 (hero-art compression).
**Build day (one focused day):** 5 → 6 → 7 → 8 → 9 → 10 — ends with the APK on a real
device and the export proven.
**Install day:** 11.
**Score: 82/100.** Nothing else stands between this project and 40 tablets.
