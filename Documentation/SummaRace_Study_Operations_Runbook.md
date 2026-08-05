# SummaRace — Study Operations Runbook

**Purpose:** a step-by-step field guide for actually running the thesis study — installing
tablets, running sessions, unlocking sessions, exporting data, and recovering from problems —
written for someone under classroom time pressure. It does not re-argue design; it tells you
what to tap.

**Grounded in the code as of branch `experiment/endless-override-2`, commit `f044d39`**
(`Features/TeacherMenu/TeacherMenuController.cs`, `Core/TeacherGate.cs`, `Core/SaveManager.cs`,
`Core/Bootstrapper.cs`, `Core/GameManager.cs`, `Core/SessionLogService.cs`,
`Features/NameEntry/NameEntryController.cs`, `Features/SessionMap/SessionMapController.cs`,
`Constants/GameRules.cs`, `Constants/GameText.cs`, `Constants/PrefKeys.cs`, and the 30 files in
`Assets/_Game/Resources/Stories/`). Where the code could not answer a question, that is called
out explicitly rather than guessed — see the **"Verify before the study"** checklist in §7.

Other agents may still be touching the teacher-menu / learner-switching screens while this
document is written. If a step below stops matching what you see on a tablet, trust the tablet
and re-read the relevant `.cs` file before assuming the runbook is wrong.

---

## 0. The three facts that decide how the whole study runs

1. **Sessions are teacher-gated on purpose.** A learner can only ever play up to
   `unlockedSession`. Opening session *N+1* requires the teacher's PIN, on that specific
   tablet, every time. This is an internal-validity control (GDD §8.3): if a child could tap
   through all 10 sessions in one sitting, the study can no longer say the game was used
   "10 sessions over 10 days" — the comparison against the control group would be broken.
2. **The raw PIN is never stored — only a salted hash.** There is no "forgot PIN" recovery
   that gives the PIN back. The only way back in is a hidden gesture that **erases the whole
   tablet** (§6.1). This is a deliberate trade-off (a readable backdoor PIN could be found
   inside the APK), and it means **the PIN must be set correctly, by an adult, before any
   tablet reaches a learner** — see §1.
3. **Export lives behind the same PIN, and the recovery gesture wipes the logs too.**
   There is no cloud backup and no second copy anywhere. If the PIN is lost *and* you have to
   use recovery, every log on that tablet is gone, permanently, with no way to get it back.
   **Export before you ever need recovery, not after.**

---

## 1. Before the study: one-time setup per tablet

### 1.1 Get an APK onto the tablet

⚠️ **Verify-before-study item — no APK has ever been built from this project.**
`Documentation/SummaRace_Finalization_Plan.md` (P6) states Android Build Support is not
installed in the project's Unity Editor, so the two device numbers the study depends on — APK
size and frame rate on the target tablets — are estimates, not measurements, as of this
writing. Before day 1 you must:

1. In Unity Hub, add the **Android Build Support** module (with **OpenJDK** and **Android SDK
   & NDK Tools** sub-modules) to the Unity version this project uses.
2. Open the project, **File → Build Settings → Android → Switch Platform**.
3. Confirm the scene list starts with `Boot` at index 0 (CLAUDE.md records this as already
   correct — Boot creates the `[Core]` singletons everything else depends on; if some other
   scene is index 0, the app will not boot correctly).
4. **File → Build Settings → Build** to produce a debug-signed APK (Unity self-signs debug
   builds automatically — no keystore setup is required for sideloading). Do this once, then
   reuse the same APK file for every tablet — do not rebuild per tablet, so every learner runs
   identical content and code.
5. Copy the `.apk` to each tablet (USB cable + file manager, or `adb install path\to\app.apk`
   with the tablet in Developer Mode / USB debugging on) and install it. Android will warn
   about "unknown sources" for a sideloaded debug APK — allow the install anyway.
6. **Do a smoke test on the actual tablet hardware before day 1**: launch, get through the
   name screen, play one story end to end, confirm sound plays and the screen stays portrait.
   The 2GB/Android-8 floor device is explicitly called out in CLAUDE.md as unverified for
   frame rate — if a tablet chugs or the race is unplayably slow, that is a today problem, not
   a during-the-study problem.

### 1.2 Set the teacher PIN — do this before the tablet reaches any child

**This is the single most important thing in this document.**

On first launch, the app has no PIN at all. `TeacherGate.HasPin()` is false, so the Teacher
screen's gate defaults to **"Teacher setup: choose a PIN (4+ digits)"** — anyone who finds the
small teacher button on the Main Menu can set the PIN, including a curious child before you
ever hand out the tablet. If a **learner** sets the PIN, you have lost teacher control of that
tablet: only whoever knows that PIN can unlock the next session or export the logs, and you
have no way to find out what they typed.

**Steps, per tablet, before it is handed to any learner:**

1. Launch the app. It boots to Name Entry the very first time (§2), or straight to the Main
   Menu afterwards.
2. On the Main Menu, tap the small teacher-corner button (deliberately low-contrast — it is
   discreet, not hidden, because everything behind it is PIN-gated).
3. You will see **"Teacher setup: choose a PIN (4+ digits)"**. Type a PIN of **4 or more
   digits** (shorter is rejected with "Use at least 4 digits."), tap **NEXT**.
4. You will see **"Type the same PIN again"**. Type the identical PIN, tap **OK**.
   - If the two entries don't match you get "Those didn't match. Start again." and go back to
     step 3 — nothing is saved yet, so this is safe to retry.
5. On success you land on the Teacher actions screen and see **"PIN saved. Write it in the
   study notes — it cannot be read back."** — take that literally. **Write the PIN down now**,
   per tablet, in your study notes. There is no way to display it again; the app only stores a
   one-way hash.
6. Tap **Back** to return to the Main Menu. The tablet is now gated: the Main Menu's teacher
   button will ask for that PIN every time from now on.

**Why this can't be fixed after the fact:** once a PIN exists, entering the teacher screen
always asks "Enter PIN" first — there is no way to change or clear a PIN except the destructive
recovery gesture in §6.1, which also deletes every learner's progress and logs on that tablet.
Setting the PIN correctly, once, before a child touches the device, avoids that entirely.

### 1.3 Decide: one tablet per learner, or a shared tablet?

**Verify-before-study item** — `SummaRace_Finalization_Plan.md` lists this as an open design
call for you, the owner, to make; the code supports either answer.

**What the code actually does, either way:**

- Every learner is a separate **profile** on the tablet (`LearnerProfile`, identified by a
  GUID, holding their own name, avatar, `unlockedSession`, and per-story stars). `GameManager`
  keeps a `CurrentLearner` — whoever is "active" right now — and every screen (stars, unlocks,
  logs) reads and writes against that one profile only.
- Every learner's log is its own file: `logs/<learnerId>.jsonl` on that tablet. Nothing merges
  across learners at write time.
- **`GameText.cs` states the intended design directly**: *"One tablet per learner is the
  study's intent, but a shared tablet must never silently merge two children: stars, unlocks
  and every exported log row are keyed to one profile, and a merge cannot be undone at analysis
  time."* In other words: the code was built assuming one-tablet-per-learner is the plan, and
  the shared-tablet path exists as a safety net, not the preferred design.

**If you do have one tablet per learner** (recommended, and the assumed default): nothing
further to do per session beyond the ordinary flow in §3. The tablet always has exactly one
profile, and it is always that learner's.

**If a tablet must be shared by more than one learner**, the switch exists and is
teacher-gated (learners cannot switch themselves — GDD §8.3, same reason they can't
self-advance sessions). Exact tap path, from the Main Menu:

1. Tap the teacher-corner button, enter the PIN.
2. On the Teacher actions screen, tap **"Switch learner"**.
   - If nothing happens and you see "Learner list unavailable on this screen," the picker
     could not be built on that device — treat as a **Verify-before-study** item; report the
     scene state to the developer rather than trying to work around it in front of a class.
3. A **"Who is playing?"** list appears: **"+ New learner"** at the top, then one row per
   existing profile on this tablet, formatted `Name · Session N`, with `(playing)` appended to
   whoever is currently active (that row is greyed out — you cannot re-select the child already
   active).
4. **To bring in a learner who has never used this tablet**: tap **"+ New learner"**. This
   creates a blank profile and sends you to the Name Entry screen (§2) to name it and pick an
   avatar; confirming there returns you to the Main Menu with the new learner now active.
5. **To switch to a learner who has used this tablet before**: tap their row. Status text
   confirms **"Now playing: <name>"**, and you're returned to the actions screen.
6. Tap **Back** twice to reach the Main Menu. **Before starting the session, check the Main
   Menu subtitle line — it reads "Playing as <name>".** This is your last check that the
   correct child is about to play; a run recorded against the wrong child cannot be fixed after
   the fact, only after export by cross-referencing the roster file (§5) and it still cannot be
   un-mixed once merged in a paper record.

**Either way, one rule doesn't change:** whichever policy you pick, treat it as fixed for the
whole study. Switching your policy mid-study (e.g. starting shared, moving to one-per-child
later) makes the exported data harder to interpret, not easier.

---

## 2. First launch on a tablet: Name Entry

The very first time the app boots on a device (`Bootstrapper`, checking `learner.named`), it
routes to **Name Entry** instead of the Main Menu. This also happens right after you create a
new learner via "+ New learner" in §1.3.

What the learner (or you, on their behalf) does:

1. **"What's your name?"** — type a name in the box (placeholder text: "Type your name"). This
   is what will identify their exported log rows in the roster file (§5) — the log rows
   themselves are pseudonymous, keyed by an internal id, not by this name.
   - Leaving it blank is not an error: confirming with an empty box assigns a default name
     rather than blocking the learner.
2. **"Pick your runner"** — tap one of 4 avatar buttons (heart / star / gem / lightning shapes,
   distinguishable by shape and colour). The selected one highlights white; the rest are dimmed.
3. Tap **LET'S GO!** — this saves the name+avatar to the active profile and returns to the Main
   Menu.

This screen only appears again if you deliberately create a new learner (§1.3) or if a
tablet's save data is wiped (§6.1).

---

## 3. Running a session — what the learner does, end to end

One **session** (in the study's 1–10 numbering) = three stories, **Easy → Average → Hard, in
that order** (`StoryIds.Difficulties`). Average unlocks only after Easy is completed; Hard only
after Average. This progression is automatic and needs no teacher action — only moving to the
*next session number* needs the PIN (§4).

### 3.1 The loop, screen by screen

1. **Main Menu** — learner taps the big **TAP TO START** button (or you've routed them there
   already). This opens the **Session Map**.
2. **Session Map ("Choose a Mission")** — ten stops in a snaking path. Playable stops (up to
   `unlockedSession`) are tappable; anything beyond is shown locked with a lock icon, and
   tapping a locked stop does **not** scold or dead-end — it just gives a small nudge sound and
   the hint **"Your teacher opens the next mission!"**. The current session's stop is glowing.
   Learner taps their current, unlocked stop.
3. **Story Select** — three difficulty cards (Easy / Average / Hard) for that session. Easy is
   always open; the others show a lock + hint until the one before is finished. Learner taps
   Easy first.
4. **Reader** — the story's 5 pages, one at a time, each with narration (unless VOICE has been
   turned off) and a SWBST question after the text. Wrong answers never block progress — the
   correct answer is shown and the learner moves on. This is the "pre-teach" pass: the same
   five questions come back in the Race with the story text no longer visible.
5. **Race (endless runner)** — a short mission briefing (names the story, shows the 5 SWBST
   chips) → **START** → 3-2-1-GO countdown → the runner auto-runs down a track, and the learner
   swipes/taps or uses arrow keys/WASD to change lanes and collect the card matching each SWBST
   element as it comes up, while a friendly police-car "chaser" visually closes in after a wrong
   pick (it can never actually catch the learner — GDD D7, always 0 catches by design). The
   race always ends at a FINISH gate now (an earlier gap where a missed finish trapped the
   learner has been fixed; if it still happens, that's a "Verify" item, see §7).
6. **Arrange** — drag/tap the 5 collected SWBST pieces into S-W-B-S-T order. Getting a slot
   wrong just wiggles it back to the pool — retries never run out; after repeated misses a hint
   appears, and after enough failed attempts the app finishes the order with the learner rather
   than stalling them.
7. **Summary** — learner types one summary sentence with the arranged SWBST parts shown as a
   reference. Checks are light (minimum 5 words); at most 2 gentle nudges, then it is always
   accepted — the app encourages, it never grades. This typed sentence is what your paper rubric
   ultimately looks at (in-app logs are supporting data, not the primary measure per GDD §8.1).
8. **Results** — stars (3 = 5/5 first-try correct in the race, 2 = 4/5, 1 = 3-or-fewer, always
   at least 1 for finishing), praise text, the story's main idea revealed, and a "treasure"
   reveal of the 5 SWBST letters earned. Tapping through returns to the **Session Map**, which
   shows the next difficulty now unlocked (or, on the third story of a session, plays a short
   "session complete" cheer on that session's stop).
9. Repeat steps 3–8 for **Average**, then **Hard**, to finish the session.

### 3.2 How long this takes

**Verify-before-study item — no full end-to-end timing has been measured on real hardware.**
`SummaRace_Finalization_Plan.md` explicitly lists "owner full-loop playtest on a non-s01
story" as still outstanding — 27 of the 30 stories have never been played through even once,
and several UI animations only run in Play mode, so timing has not been confirmed for most
content. As a rough planning number only: a single story (Reader + Race + Arrange + Summary +
Results) is unlikely to take more than 5–8 minutes once a learner is comfortable with the
controls, putting one full session (all three difficulties) at roughly 15–25 minutes — but
**do at least one timed dry run yourself, on the real tablet, on a story other than `s01`,
before day 1**, and adjust your classroom time budget to what you actually measure.

### 3.3 What the teacher does between learners

- **On a shared tablet**: switch the active learner via the Teacher screen (§1.3) before
  handing it to the next child, and double-check the Main Menu's "Playing as <name>" line
  before they tap START. This is the one step that, if skipped, silently attributes one
  child's play session to another and cannot be undone later.
- **On one-tablet-per-learner**: nothing — each tablet already belongs to one child, the app
  remembers who they are between launches (`activeLearnerId` persists in `settings.json`), and
  nothing needs to be reset between sessions.
- In both cases, you do **not** need to touch the Teacher PIN screen at all during a normal
  session — only when opening the *next numbered session* (§4) or exporting/at the end of the
  study (§5).

---

## 4. Opening the next session (once per tablet, per session)

Sessions are deliberately teacher-gated (GDD §8.3) so a learner cannot self-advance through the
study's 10-session schedule. `TeacherGate.UnlockNextSession()` only ever advances the **active
learner's** `unlockedSession` by exactly one step, and only when you ask it to.

**Exact tap path**, done once per tablet at the end of each classroom session (after the
learner has actually finished that session's three stories, so their stars reflect real work):

1. Return to the **Main Menu**.
2. On a shared tablet, first confirm the correct learner is active (§1.3/§3.3) — unlocking
   advances whoever is currently active, not a specific named child.
3. Tap the teacher-corner button, enter the PIN, tap **OK**.
4. On the Teacher actions screen, tap **"Unlock next session"**.
   - If the learner has already finished all 10, you'll see **"All 10 sessions are already
     open."** and nothing changes.
   - Otherwise you'll see **"Session N is now open."** — N is now the number the learner can
     play next time they reach the Session Map.
5. Tap **Back** to return to the Main Menu.

**This is per learner, per tablet, once per session.** If you have 40 tablets (or fewer tablets
shared across 40 learners), you must repeat this tap path once for **every** learner after
**every** session they complete — there is no bulk "unlock everyone" action. Budget a few
minutes at the end of each classroom period for this if you're running many tablets.

---

## 5. Getting the data out

### 5.1 What gets logged, and by whom

`SessionLogService` builds one `SessionLog` row per story play-through purely from in-app
events (Reader answers — first attempt only, race first-pick-correct per SWBST element, arrange
attempts/whether the learner needed the assist, the typed summary text verbatim, nudge count,
stars, total seconds). It is appended as one line of JSON to
`logs/<learnerId>.jsonl` on that tablet the moment the story completes — or, per §6.3, even if
the run is abandoned partway through.

### 5.2 Exact tap path to export

1. Main Menu → teacher-corner button → enter PIN → **OK**.
2. On the Teacher actions screen, tap **"Export logs"**.
3. The status line then shows either:
   - **"No logs to export yet"** (nobody has finished a story on this tablet yet), or
   - **the full on-device file path** of the export — read it directly off the screen, because
     you'll need it in the next step.

### 5.3 What the export actually produces, and where it lands

`SaveManager.ExportLogs()` combines **every learner's** `.jsonl` on that tablet into one
timestamped file, written to the root of the app's private storage
(`Application.persistentDataPath`, typically something like
`/storage/emulated/0/Android/data/<package id>/files/` or the equivalent internal path on that
Android version):

- `export_YYYYMMDD_HHmm.jsonl` — every learner's log rows, one JSON object per line. Rows are
  **pseudonymised**: each row carries a `learnerId` (an internal GUID), never a name.
- `export_YYYYMMDD_HHmm_learners.json` — the **companion roster**, written alongside it in the
  same step: one entry per learner on that tablet with `learnerId`, `displayName`,
  `unlockedSession`, and `storiesCompleted`. **You need this file to map the pseudonymous log
  rows back to a child's name.** Without it, the `.jsonl` alone is 40 devices' worth of
  unlabeled GUIDs.

**Both files land in the same folder, at the same time, from the one "Export logs" tap.**
Retrieve them over USB (connect the tablet, enable file transfer / MTP, browse to the path
shown on screen — you may need a file manager app with "show hidden/app-private files" access,
or `adb pull`, depending on the Android version and whether the path is on external or internal
storage).

### 5.4 The golden rule: EXPORT BEFORE THE STUDY ENDS

**Export lives behind the same PIN as everything else.** If a PIN is ever lost on a tablet
(§6.1), the *only* way back in is the recovery gesture — and that gesture **deletes every log
on that tablet as part of erasing it**. There is no other copy, no cloud sync, no way to
recover logs after that point.

Practical implications:
- **Export regularly during the study**, not just once at the very end — a research day where
  something goes wrong (lost PIN, dead tablet, dropped tablet) should only ever cost you the
  data since your last export, never the whole study.
- **Export from every tablet before you consider the study "done."** A tablet you meant to
  export "later" and then handed back, wiped, repurposed, or lost has no recovery path.
- Copy the exported files off-device (USB, or however you retrieve them) immediately after
  exporting — the files sit on the tablet's own storage until you copy them out, so exporting
  alone does not get them off the device.

---

## 6. When things go wrong

### 6.1 Lost PIN (or a learner set/changed it)

There is no "forgot PIN" flow that gives the PIN back — a salted hash cannot be reversed. The
only path back in is a **hidden hold gesture**, deliberately not advertised on screen (a visible
"forgot PIN?" button is exactly what a curious child would tap):

1. Main Menu → teacher-corner button. You'll land on **"Enter PIN"** (this only works from that
   exact step — it will not arm from the PIN-setup screens).
2. **Leave the PIN box completely empty.**
3. **Press and hold the OK button for 6 seconds.** (Both conditions are required —
   `BeginHold()` only arms the hold when the step is "Enter PIN" *and* the box is empty; typing
   anything, even one digit, disables it.)
4. On release, a warning appears: **"Reset this tablet? This erases every learner profile,
   every star and every log on this tablet. They cannot be exported afterwards. You can then
   set a new PIN."** — Back cancels here with nothing erased.
5. If you mean it, tap **ERASE TABLET**. You'll see **"Last chance — this cannot be undone."**
   — Back still cancels at this point.
6. Tap it again to confirm. The tablet wipes: **every profile, every star, every unlocked
   session, and every log on that tablet is gone, permanently** — and returns to first-run PIN
   setup (§1.2), as if brand new.

**Cost, stated plainly:** this is not a way to "reset the PIN" cheaply — it costs all progress
and all research data collected on that specific tablet up to that moment. Re-read §5.4 before
you ever need this. After using it, **immediately do §1.2 again** (set the new PIN, write it
down) before the tablet goes anywhere near a learner.

There is also a soft brake on guessing: **5 wrong PIN attempts in a row impose a 30-second
wait** before another attempt is accepted (the wait counts down and clears itself — it is
friction against a child trying "1111"/"1234", not a lockout you need to escalate). The hold
gesture in step 3 above still works during that wait.

### 6.2 A learner taps the wrong story

- **Wrong difficulty card, before playing**: no consequence — tapping a locked card (one whose
  prerequisite isn't finished) just gives a friendly nudge sound and does not open anything.
  Tapping the correct unlocked card is the only way forward.
- **Realizes mid-story they're in the wrong place**: there is currently **no in-story way to
  back out** of the Reader/Race/Arrange/Summary sequence — this is a known open item (see §7).
  The practical options are: let them finish it (a wrong-but-completed run against the wrong
  story is retrievable later — the log records which `storyId` was played, so it can be
  identified and excluded during analysis if needed) or force-quit the app (§6.4) if the class
  cannot wait.
- **Replays a story they already finished**: allowed, and recorded — `SessionLog.isReplay` is
  set to true for that run, and `CompleteStory` never *reduces* a previously earned star count
  (only raises it), so a replay cannot erase a learner's prior result. A replay is still logged
  as a new row for the researcher to filter as they see fit.

### 6.3 A tablet dies / loses power / crashes mid-run

`SessionLogService` is built to lose as little as possible:

- **App is backgrounded** (Android sends it to background for a notification, the home button,
  a screen lock, low memory) but the tablet stays powered: the in-flight run is **snapshotted
  to disk immediately** (`OnApplicationPause`), marked `isPartial: true`, but **kept alive in
  memory** so that if the learner comes back and finishes, the completed run still gets written
  normally. You do not lose progress just because the screen locked.
- **App is force-quit or the tablet loses power outright**: whatever was snapshotted at the
  last pause survives; anything since the last pause (or since the story started, if it never
  paused) is lost. In the worst case — a run that never backgrounded at all before the power
  cut — you lose that one story's data, and nothing else: a prior finished story's row was
  already written to disk when it completed, and the *next* story the learner starts writes an
  entirely new row.
- **A run that never produced any data at all** (e.g. the story loaded but the learner never
  answered a page, made a race pick, or earned a star) is **not** written — an empty row would
  be indistinguishable from a genuinely abandoned run and would just add noise.
- **A genuinely abandoned run that did produce some data** (they answered a page or two, then
  the tablet was taken away or the app was switched to a different story) is still written, and
  is recognisable in the exported data by an **empty `finishedIso`** field — that is the signal
  a researcher should use to identify and separately handle incomplete attempts.

**What to actually do in the room**: if a tablet dies mid-story, don't panic about the data —
restart the tablet, relaunch the app (it should resume showing the correct learner and their
correct `unlockedSession`), and either let them redo that one story or move on; at most one
story's data for that one learner is affected, never more.

### 6.4 App is frozen or has to be force-quit

Force-quitting Android apps (via the recent-apps switcher, or a full device restart) triggers
the same "backgrounded" or "quit" handling as §6.3 — whatever had already been snapshotted or
completed is safe; only the delta since the last snapshot is lost. After relaunch, confirm on
the Main Menu that **"Playing as <name>"** still shows the expected learner before letting them
continue (this should not change on its own, but it's a one-second check worth making a habit).

### 6.5 A learner finishes early, or you run out of classroom time

- **Finishes a full session (all 3 difficulties) early**: nothing more to do until you (the
  teacher) unlock the next session (§4) at the appropriate scheduled time — the app will not
  let them skip ahead even if they try, by design.
- **Runs out of time partway through a story**: there's no in-app "save and exit" for a story
  in progress (see §7's open item about no pause/back/quit in the race specifically). If you
  must stop, force-quitting is safe per §6.3 — you'll lose at most the current story's data, and
  the learner can redo that difficulty next time (StorySelect always shows it, whether or not a
  prior attempt was abandoned).
- **Runs out of time between stories** (e.g. just finished Easy, no time for Average): this is
  clean — Easy's completion is already saved, and Average will simply be waiting, unlocked, the
  next time they get the tablet. No teacher action is needed for this (progressing between
  difficulties within a session is automatic, unlike between numbered sessions).

---

## 7. Checklists

### 7.1 Pre-flight checklist — run on EVERY tablet before day 1

- [ ] APK installed and launches to the Main Menu (via Name Entry, the first time).
- [ ] Sound plays (narration + music + effects) — check the physical volume too.
- [ ] Screen stays portrait-locked throughout the full loop.
- [ ] Play at least one full story end to end on **this specific tablet** (not just once in
      the Editor) — Reader → Race → Arrange → Summary → Results — and confirm it does not
      freeze, especially the Race (this is the one screen flagged as unverified for frame rate
      on low-end hardware).
- [ ] Teacher PIN is **set** (§1.2) and **written down in your study notes**, tied to that
      tablet's identifier (e.g. an asset tag or a piece of tape with a number on it).
- [ ] If tablets are shared, confirm the "Switch learner" flow works on this tablet (§1.3) —
      tap through creating one throwaway test learner and switching back, then decide whether
      to leave that test profile or wipe the tablet clean before day 1 (a wipe here is fine,
      since no real study data exists yet — see §6.1, but note it also clears the PIN, so
      re-set it after).
- [ ] Confirm the Session Map shows only Session 1 unlocked for a fresh learner (i.e., nobody
      starts the study with unearned sessions already open).
- [ ] Battery charged; a charging plan for multi-day use is in place.
- [ ] Note the tablet's Android version/model somewhere — useful if a device-specific bug shows
      up only on some tablets.

### 7.2 Per-session checklist (each classroom period)

- [ ] Confirm the correct learner is active before handing over the tablet (§1.3/§3.3) —
      check the Main Menu's "Playing as <name>" line.
- [ ] Confirm the Session Map's glowing/current stop matches the session you intend them to
      play today.
- [ ] Let the learner play Easy → Average → Hard through to Results each time (§3.1).
- [ ] After the learner finishes all three difficulties, unlock the next session for them
      (§4) — do this before the tablet is put away, so you don't have to remember which
      learners still need unlocking later.
- [ ] Periodically (at least every few sessions, not just at the very end) export logs (§5)
      and copy them off-device. **Do not wait until the last day.**
- [ ] If anything went wrong this session (§6), make a note of which learner/tablet/story was
      affected — the exported data can usually tell you what survived, but a contemporaneous
      note is faster to reconcile than reverse-engineering it from `isPartial`/`finishedIso`
      flags later.

---

## Appendix: "Verify before the study" — everything this document could not confirm from code alone

These are called out inline above; collected here for a final pre-study check:

1. **No APK has ever been built from this project** (Android Build Support not installed per
   `SummaRace_Finalization_Plan.md` P6). APK size and race frame-rate on the 2GB-RAM/Android-8
   floor device are unmeasured. Build one and test on real hardware before day 1 (§1.1).
2. **One-tablet-per-learner vs. shared** is an explicit open design decision for the owner
   (`SummaRace_Finalization_Plan.md` §"Still open"). The code and this runbook support either
   choice (§1.3); pick one before day 1 and do not change it mid-study.
3. **End-to-end session timing has not been measured on real hardware or with real content**
   — 27 of 30 stories have never been played through once, per the project's own build-state
   table. Do a timed dry run yourself before relying on any duration estimate (§3.2).
4. **The learner-switching screen (§1.3) may still be in flux** — the task that produced this
   document was written while another pass was actively touching the teacher-menu/learner
   screens. Re-check the exact button labels and flow on the actual build you ship, not just
   against this document, if it was generated significantly before day 1.
5. **The race has no in-run pause/back/quit** (confirmed current limitation, not a bug to
   "fix" here) — factor this into how you brief teachers about interrupting a session (§6.5).
6. **Content sign-off**: `s01_easy`'s questions are AI-authored (by design, per CLAUDE.md's
   known flags) and the other 29 stories' distractors have had an editorial pass but are still
   flagged in the Finalization Plan as wanting a researcher sign-off pass (GDD D6). This is a
   content-validity question for the researcher, not an operational one, but it should be
   resolved before data collected under it is treated as final.
