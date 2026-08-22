# Scene-by-scene critique — 2026-08-22

Six agents, one per screen area, each producing a **WRONG** list (defects) and an **IMPROVE**
list (polish). Every contrast figure below was computed from the actual serialized colours and
sampled sprite pixels in gamma space (`m_ActiveColorSpace: 0`), not eyeballed.

**Fixed and committed the same day** — see `22115ca`, `b0f9292`:

| | |
|---|---|
| Summary | `m_OnFocusSelectAll` wiped the child's sentence on re-focus · rich text off on the input |
| Reader | narration read the passage aloud over the question · VOICE label 1.9:1 |
| NameEntry | "Pick your badge" 1.40:1 |
| Arrange | status band 1.39:1 (a defect introduced earlier the same day) |
| SessionMap | hint backing outlived its hint · backing alpha 0.55 → 0.75 |
| All | `SubtitleLine` shipped a duplicate empty pill on three screens |
| Race | patrol rebuilt to carry state across the run |

Everything below is **still open**. Ranked within each screen.

---

## Blocking-class (fix before the study, not necessarily before a test build)

1. **TeacherMenu — `DeleteData()` reports "All learner data deleted." without checking.**
   `SaveManager.DeleteAllData()` returns `void` and swallows failures. A locked file or a
   permission change after an OS update produces the identical cheerful line. This is the consent
   promise: a tablet handed back believed clean, with a named child's profile and ten sessions of
   logs still on it. Every neighbouring action was hardened for exactly this; the destructive one
   was missed. Make it return `bool`; make the status conditional.

2. **TeacherMenu — the 5-try / 30s lockout is defeated by two taps.** `_wrongAttempts` and
   `_retryAt` are instance fields on a scene-scoped controller and `ShowGate()` resets both on
   `Start()`. Back → menu → teacher corner → fresh scene → cooldown gone. It is the only thing
   between a nine-year-old and Delete-all-data. Persist `_retryAt`.

3. **TeacherMenu — `StorageIssueCount` / `LastStorageIssue` have zero readers.** Their docstring
   says they exist so the menu can say "this tablet has a storage problem" while it is still in
   hand. A tablet where `AppendLog` threw on every row for ten sessions reaches export, finds an
   empty folder, and is told "No logs to export yet." Two lines closes it.

4. **TeacherMenu — "Delete all data" is white on orange at 2.40:1**, and its armed state
   ("Tap again to confirm") is in the same pairing. The irreversible button is the unreadable one.
   Dark brown gives ~7.9:1.

5. **TeacherMenu — the export path is drawn on painted sky with no backing** (6.45:1 average but
   **3.10:1** against the cloud whites) and `LabelFit` truncates its *tail* — which is where the
   duplicate-participant-code warning is appended. The loudest warning in the app is the first
   thing lost.

6. **Results — one unguarded statement while the exit is hidden.** `IsSessionDone()` →
   `CurrentLearner?.progress.Find(...)`: the `?.` guards the learner, not `progress`. A null
   `progress` throws with the button already hidden and the coroutine not yet started — no stars,
   no exit, no timeout. The `try/finally` around the reveal is otherwise correct and cannot reach
   this line.

7. **Arrange — a throw inside `AssistRoutine` strands `_busy` forever.** `VerifyRoutine` yields on
   it and sets `handedOff` *after* the yield; the file's own comment states the rule this breaks
   ("Unity logs an inner coroutine's exception and never resumes the outer one"). Board dead, and
   Arrange has no exit.

8. **Arrange — the never-stuck ladder is gated on completing the board four times.** A child who
   never discovers tap-piece-then-tap-slot cannot reach the assist at all, and BACK is a message,
   not an exit. Today's slot-arming helps discoverability; it does not close the hole. A tap-count
   or elapsed-time fallback into the same assist would.

---

## Validity / data

9. **Reader — `readingSeconds` includes the loading overlay.** `GameManager.StartStory()` raises
   `StoryStarted` *before* `SceneLoader.Go(Reader)`, so the clock runs through the fade, the async
   load, the tip card and `Start()`. A device- and thermal-dependent 1–3s on every row of a
   headline variable, varying with Addressables warm-up. Re-base the phase at the first
   `ShowPage(0)`.

10. **Reader — the display slot is never logged.** The slot→index mapping is correct and verified,
    but the on-screen position is discarded, so nobody can audit the shuffle for uniformity or
    detect a residual "always tap the middle". One additive `readingFirstSlots` array closes it.
    (Also: CLAUDE.md calls this shuffle "seeded". It is not.)

11. **Summary — `PassesLightChecks` gates on a substring, so `nudgeCount` is not comparable across
    stories.** Nine Somebody lines contain a stopword longer than two characters ("**and**",
    "**the**"), so on those nine any sentence containing "and" passes the Somebody test outright.
    `nudgeCount` currently measures which story you drew as much as what you wrote.

12. **Summary — the 200-character limit is silent, and two of thirty stories exceed it.** Summed
    across the five `correct` strings: min 128, median 178, **max 244**. A child who conscientiously
    names all five parts on those stories cannot finish the sentence they were asked for, and the
    keyboard simply stops responding.

13. **Race — `racePicks.represent` can only ever be `false`.** `EndlessOptionPickup.isRepresent` is
    never assigned true anywhere, so the Data Dictionary presents a field the instrument cannot
    vary. The whole re-present path is unreachable in five places (`MaxRepresentMisses`,
    `_previewIsRepresent`, `_selectorCentreOnly`, `_activeIsRepresent`, `PrepareGateOptions`'s
    branch).

14. **Race — OS backgrounding is invisible to the log.** `OnApplicationFocus`/`Pause` restore
    `timeScale` correctly but never raise `RacePauseChanged`, so `racePauseCount` misses every
    interruption while `runSeconds` silently excludes it.

15. **Race — the steering coach is consumed device-wide before it is shown.** `RaceCoachSeen` is a
    plain `PlayerPrefs` int, not per learner. On a shared tablet only the first child ever sees how
    to steer, and mis-steering logs as a wrong first pick.

16. **Race — `TriggerDepth`'s margin comment is wrong by 3×.** It claims 2.5 frames at 30fps; with
    `RaceMaxDeltaTime = 0.10` a frame is 3.0m at maxSpeed against a ~3.9m window — a **0.9m / 30%**
    margin, not 250%. `GameRules` states it correctly; the two comments contradict each other and
    the optimistic one sits on the constant someone would tune.

---

## Learner experience

17. **Nothing marks the end of anything.** No completion state for a session, and none for the
    whole study. After the last story of session 10 the button reads "NEXT MISSION" and routes to a
    map with nothing new. Ten sessions of work end on a label that is a lie.
18. **StorySelect — `EasyGlow` is scene-static, always on, never referenced by the controller**, and
    not even centred on the Easy card: its brightest band lands on the Easy/Average boundary.
19. **StorySelect — `MarkPlayable` marks every playable card**, so once Easy is finished two cards
    carry the ring and badge, and after all three, all three do. The "tap this one" signal dies
    exactly when it is needed. A cleared card also has no "done" state and still says PLAY.
20. **StorySelect — three gold stars mean first-pick accuracy here and stories-finished one tap
    earlier**, and only the map says which. Same sprite, same row, adjacent screens.
21. **StorySelect — card titles autosize down to 8pt.** The longest title is 37 chars
    (`s09_hard`), on the narrowest box. Same class as the tracker defect F46 fixed: a floor low
    enough that "it fits" and "it is readable" stop being the same statement.
22. **SessionMap — the glow marks the highest *unlocked* session, not the next *unfinished* one**,
    so incomplete sessions can look finished-with.
23. **SessionMap — the session-complete cheer writes into the label that explains the locks**, and
    wins the race with it, so on the most likely arrival the explanation is missing.
24. **SessionMap — there is no path.** Ten stops in a 2×5 grid; CLAUDE.md describes a route that
    was never drawn.
25. **MainMenu — `DecorCoins` / `DecorGems` are raycast targets that do nothing**, in the bottom
    band, above everything. The shiniest objects on screen answer a tap with silence.
26. **MainMenu — no Ms. Lumi, no welcome copy**, and the subtitle that would sit where she belongs
    is at 2.18–3.03:1.
27. **MainMenu — `_nameEntryOffered` is static and not reset on the failure path**, so the
    unnamed-learner safety net can disarm itself for the rest of the process.
28. **Boot — `EnsureSplashChrome()` can never run.** The component sits on a root GameObject, not
    under `SplashCanvas`, so `GetComponentInParent<Canvas>()` returns null and the method returns on
    its first line. The loading label and progress bar do not exist; the documented fix is inert.
29. **Boot — the tagline is 2.83 / 3.26 / 3.92:1**, and there is no dead-end guard if `Awake()`
    throws after `_initialized` latches.
30. **NameEntry — `avatarIndex` is written and read by nothing.** The only self-expression the
    instrument offers, and it evaporates. Also: no tap-outside to dismiss the keyboard, and avatar
    selection is signalled by a 1.35:1 brightness step alone.
31. **Reader — Ms. Lumi's correct-answer celebration is drawn behind the question card.** The one
    reaction `MsLumiReactor` was built for, on the only scene that has ever carried the component,
    is invisible: `Teacher` is sibling index 2, `QuestionPanel` is 4.
32. **Reader — `BackButtonGuard.Clear()` has zero call sites**, so the Reader's blocked message
    ("Keep reading — you're nearly there!") survives into the race's boot and briefing.
33. **Race — the tracker fills a slot identically whether the pick was right or wrong.** Defensible
    under D7, but it means the widget that teaches the framework carries no information about the
    run.
34. **Race — the question plaque draws over ~9% of the pause chip**, and neither the tracker nor
    the plaque is a tap blocker, so tapping either steers the runner.
35. **Race — a vertical flick produces a lane change from wherever the thumb happened to rest.**

---

## Notes for whoever picks this up

- **Two complete patrol implementations exist**; `UpdatePatrol` (~100 lines) is unreachable.
  Delete it rather than adding a third.
- **`Resources/UI/bar_bg` is NAVY.** `Image.color` multiplies. Tinting it cream produces
  near-black — that is exactly how the Arrange band shipped at 1.39:1 for half a day.
- **Trash Dash still writes `PlayerData.Save()` to disk every 300m** during a measured run — 5–7
  synchronous writes per race on the 2GB floor device — and still ticks score, multiplier and
  missions every frame. None of it is hidden by `EndlessRaceMode`.
- The portrait render that would confirm every layout item here is unavailable while the
  Adjoint/MCP `CS0433` collision stands.
