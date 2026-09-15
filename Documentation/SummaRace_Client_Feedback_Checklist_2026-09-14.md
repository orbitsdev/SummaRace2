# Client feedback checklist — review of 2026-09-14

The researchers' review, point by point, with what was done and how it was checked.
Commits on `experiment/endless-override-2`: `eb8ece3`, `98e8654`, `48cb296`, `c6223ac`.

**Legend:** ✅ done and seen working in Play mode · 🟡 done, needs a device / human check · ⬜ needs a decision

Evidence screenshots (portrait, from Play mode) are in `Captures/client_feedback/` on the dev machine.

---

## Overall: "feels like a prototype/survey, not a game" · "understand and complete each part before moving on"

| | Item | Status | Where / how checked |
|---|---|---|---|
| 1 | Every activity is mastery-based: a learner cannot move on until the part is right | ✅ | Reader, Race, Arrange, Summary below |
| 2 | Nothing reveals the answer after a mistake | ✅ | Reader no longer highlights the right option; race answer card removed |
| 3 | Never a dead end (a stuck learner can always finish) | ✅ | Reader: tried option set aside, ends in ≤3 taps · Race: wrong card removed each return · Arrange: correct boxes lock · Summary: BACK still leaves |
| 4 | Study data still valid | ✅ | First attempt stays the measure (stars, `readingFirstCorrect`, `raceFirstPickCorrect`). Practice logged separately — schema 7 → **8** (`readingAttempts`, `readingRereads`, `summaryVerdicts`), documented in the Data Dictionary |

## Story / reading part

| | Client said | Status | What changed |
|---|---|---|---|
| 5 | Remove unnecessary text like "SO is what the character did about it." | ✅ | Loading screen no longer shows or speaks the SWBST definition tips. Also removed the italic line under each question that restated it ("Who is the story mainly about?" / "Who is this story about?") |
| 6 | Teacher/narration too fast; keep her longer per page | ✅ | QUESTION! waits until the page narration has finished (4 s minimum with voice off) — verified with the Boot flow on `s03_average`. Ms. Lumi's cheer 1.6 s → 3 s |
| 7 | Center "next page" and "question" texts | ✅ | The button was anchored off-centre (30–80% of the width); now centred |
| 8 | Consistent voice, less obviously AI | ✅ | Voice C chosen: all 150 pages + 11 instruction lines regenerated in `en-US-AvaMultilingualNeural` (rate −15%, slower than the original voice because the client said narration was too fast), one voice everywhere; verified 161/161 clips load in the Editor (`8fa172d`). A human recording remains the only way to remove the synthetic sound entirely |

## Processing questions

| | Client said | Status | What changed |
|---|---|---|---|
| 9 | If wrong, don't show the answer; prompt "Not quite, do you want to read the previous page again?" and allow review + retry | ✅ | "Not quite! Do you want to read the page again?" + **READ AGAIN** (back to the page, narration replays) → **TRY AGAIN** returns to the question. The tried answer is set aside. After two misses: "Almost there! Read the page again, or try the other answer." |
| 10 | Can't proceed until the question is understood | ✅ | NEXT PAGE only appears after the correct answer |

## Running game

| | Client said | Status | What changed |
|---|---|---|---|
| 11 | More like Subway Surfers, with a patrol chasing | ✅ | Patrol runs up behind the kid at GO!, drops back on a clean run, returns after a miss. Fixed during this pass: he was showing as a police hat cut off at the bottom edge — now always fully in frame when shown |
| 12 | Choices not static; they come out while running | ✅ 🟡 | Answer cards rise out of the road with a pop as the runner approaches, then bob. *Needs a look on the tablet at full speed* |
| 13 | A wrong item comes back until corrected | ✅ | The same part returns later **without the card already chosen**; the timer chip says "Try again in N s". Verified: SOMEBODY returned with "Bella" removed |
| 14 | Finish line should feel like the end of a mission, with achievement | ✅ | MISSION COMPLETE! stamp, fireworks + rising chimes, the whole story read back, a 5/5 badge with an encouraging first-try line, TAP TO CONTINUE (auto after 10 s). Badge layout fixed during this pass |

## SWBST (Arrange)

| | Client said | Status | What changed |
|---|---|---|---|
| 15 | Can't proceed until mastered; retry/review until correct | ✅ | The auto-solve after 4 tries is gone. From try 2 the hint names a box and asks that part's question, moving to a different wrong box each time. An all-wrong first try now says "Not yet! Try a different order…" (it used to say "Almost! the parts already in place are right" with nothing in place) |

## Final summary

| | Client said | Status | What changed |
|---|---|---|---|
| 16 | Don't accept random answers or letters | ✅ | Keyboard mash and non-English (e.g. Cebuano) are refused: "Let's write it in English words, like the story." |
| 17 | Check it matches the story and main idea; grammar need not be perfect | ✅ 🟡 | Offline check: names the Somebody + at least 3 of Wanted/But/So/Then, in story order, using the story's own ideas. Spelling/grammar never graded. Tested on all 30 stories (own summary 30/30 accepted; another story's summary accepted 1/870; child-style broken grammar accepted; off-topic refused). Feedback names the missing part ("Now add the SO and THEN parts"), never the answer. *Should be tried with real Grade-4 writing* |

---

## Things noticed while checking (not in the client's list)

| | Finding | Status |
|---|---|---|
| A | The patrol hat cut off at the bottom of the race screen | ✅ fixed |
| B | Timer chip said "Next part" over a returning part | ✅ fixed |
| C | Finish badge text overflowing / overlapping | ✅ fixed |
| D | Race briefing didn't say a missed part comes back (written + spoken) | ✅ fixed, clip re-recorded |
| E | Arrange "Almost!" message untrue when nothing was right; hint never changed | ✅ fixed |
| F | The Summary screen showed all five answers (and the text-box placeholder started the answer), so a learner could copy them in and pass | ✅ fixed 2026-09-15: the card now lists the five parts with the question each one answers; neutral placeholder |
| G | The check uses the five SWBST parts, not the story's `mainIdea` sentence | ⬜ decision: SWBST coverage is the framework being taught; adding a main-idea requirement is possible but would refuse more real summaries |
| H | A passive race (never steering) can now be long — one test run took ~4.5 km because every missed part returns | 🟡 expected with mastery; watch on device |
| I | The race still has only one visible "obstacle" type (the answer gate). Coins/jump/slide obstacles are switched off by earlier design ("the only obstacle is a wrong answer") | ⬜ decision if the researchers want more "game" |
| J | Changing D7 ("wrong answers never block") and the Arrange/Summary "always accept" rules are **locked GDD decisions** — GDD §10.2 asks for researcher + developer sign-off | ⬜ get written OK from the researchers |

## Game feel (owner follow-up 2026-09-15: "still feels like a survey")

Diagnosed by capturing every screen in portrait and comparing with the mockups: the learning
screens (Reader, Arrange, Summary, Results) used pastel, thin-bordered, flat "worksheet" styling;
a correct answer earned nothing; nothing showed the four-part mission; Ms. Lumi was a still
picture; and colour had no rules (green, orange, teal, cyan, yellow and blue buttons).

| | Change | Status |
|---|---|---|
| K | Shared game skin: chunky outlined cards with drop shadow, bold rounded type, badges (`UI/GameSkin`) | ✅ |
| L | Reward loop: coins burst from correct answers into a counter; first tries pay more than retries (`UI/CoinHud`). Never part of stars or study data | ✅ |
| M | Saved coin wallet on the learner profile, shown top-right on Main Menu, Session Map, Story Select; Results counts the story's coins into it | ✅ |
| N | Ms. Lumi alive: constant gentle sway, pose changes with a bounce, cheer (spin + sparkles + "Great reading!") on success, thinking pose + "Look again!" when stuck — never sad (D7). Stays on the question screen | ✅ |
| O | Mission path READ → RACE → ORDER → WRITE on the loading card between parts: finished parts get a star, the next part pulses (the client's goal made visible) | ✅ |
| P | Summary live gems: S-W-B-S-T light up as each part is written (same matching as SUBMIT) | ✅ |
| Q | Colour roles, one job per colour on every screen: green = go, navy = secondary/HUD, wood = boards, cream+gold = reading, yellow = question, SWBST colours = story parts only (documented in `Theme.cs`) | ✅ |
| R | Results: wood board like the map, soft empty stars instead of black ones | ✅ |
| S | Check on the tablet: colours at classroom brightness, Ms. Lumi's bubble not covering text on small screens | 🟡 |

## Double-check pass (2026-09-15) — against the goal

| | Finding | Action |
|---|---|---|
| T | Summary tips block under SUBMIT repeated what the live gems show | Removed; coin counter in its place |
| U | Race briefing was four paragraphs | Shortened to three lines; spoken lines re-recorded |
| V | "Stars = your race score" caption on every story card | Removed |
| W | Ms. Lumi overlapped READ AGAIN on the question screen | Moved clear of the button |
| X | Voice C read faster than the original voice, against "too fast" | Regenerated at −15% (avg page 5.8s → 6.5s) |

## Before study day

- [x] Pick the narration voice (item 8) — voice C
- [ ] Decide F, G, I, J
- [ ] Play one full story on the tablet: card pop-in at speed, patrol framing, finish screen on a 4:3 tablet
- [ ] Build a new APK (the target is now Android) and re-run `SummaRace ▸ Build Preflight`
