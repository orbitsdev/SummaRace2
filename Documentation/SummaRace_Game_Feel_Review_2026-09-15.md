# SummaRace — Client feedback breakdown and end-to-end game-feel review (2026-09-15)

Every page was captured in portrait from the real flow (Boot → Main Menu → Name Entry →
Session Map → Story Select → Reader → Race → Arrange → Summary → Results → Teacher) and
critiqued one at a time against (1) what the client asked for and (2) what "game feel" is made
of. What failed was fixed, re-captured and re-checked. Evidence: `Captures/e2e/`, `Captures/e2e2/`.

---

## 1. The client's statement, broken down

> "Present naman po ang main elements, pero ang main concern namo is murag prototype/survey pa
> gihapon ang feel instead of an actual game."

**Reading:** the content and flow are there; what is missing is the *experience*. It looks and
behaves like a questionnaire. Because this is a **game-based intervention**, feeling like a game
is part of the treatment, not decoration.

The whole note reduces to **two goals**, and every bullet serves one of them:

| Goal | In their words |
|---|---|
| **G1 — Mastery gates** | "for the learner to understand and complete each part before moving to the next one" |
| **G2 — Game, not survey** | "for the whole app to feel like an actual game rather than a survey or questionnaire" |

| Section | Client said (translated) | What it really asks | Goal |
|---|---|---|---|
| Reading | Remove unnecessary text like "SO is what the character did about it." | Less instructional/survey text on screen | G2 |
| Reading | Teacher/narration is a bit fast; make her stay longer per page | Slower narration; Ms. Lumi present longer, not flashing by | G2 |
| Reading | Center the "next page" and "question" texts | Clean, polished layout | G2 |
| Reading | Consistent, less obviously AI voice | One voice everywhere; as natural as possible | G2 |
| Questions | If wrong, don't show the answer; ask "Not quite, read the page again?" and let them retry | No answer reveal; offer review; retry | G1 |
| Questions | Can't proceed until the question is mastered | Gate on a correct answer | G1 |
| Race | More like Subway Surfers, with a patrol/enemy chasing | A real runner with a chaser | G2 |
| Race | Items shouldn't be static — they should appear while running, and you choose/collect | Items that come out as you run | G2 |
| Race | A wrong item must come back until corrected | Mastery loop inside the race | G1 |
| Race | Finish line should feel like the end of a mission, with achievement/feedback | A real "mission complete" moment | G2 |
| SWBST | Can't proceed until the order is mastered; retry/review until correct | Gate Arrange on a correct order | G1 |
| Summary | Must not accept random answers/letters; check it matches the story and main idea | Real validation | G1 |
| Summary | English; shows understanding; grammar need not be perfect | Judge meaning, not grammar | G1 |

---

## 2. What "game feel" is made of (the factors checked on every page)

| Factor | What it means for a Grade-4 learner |
|---|---|
| **Feedback / juice** | Every tap answers back: motion, sparkle, sound |
| **Reward** | Success earns something visible that adds up |
| **Progress / goal** | You always see where you are and what is next |
| **Character** | A living buddy who reacts to you |
| **Animation / motion** | Things move; the screen is never a still page |
| **Transitions** | Moving between screens feels like moving through a game |
| **Colour language** | One job per colour, kept everywhere |
| **Button style** | Chunky, pressable, the same style for the same job |
| **Fonts** | Bold rounded game type for UI; readable type for story text |
| **Sound** | Music bed, success chimes, soft "try again", pops |
| **Backgrounds / textures / icons / images** | Illustrated worlds, real icons, not flat panels |
| **Text load** | As little UI reading as possible; the story is the reading |

Colour roles now in force (`Theme.cs`): **green** = go · **navy** = secondary and HUD ·
**wood** = boards/banners · **cream + gold** = reading · **yellow** = question ·
**SWBST colours** = the five story parts only.

---

## 3. Page-by-page critique

✅ good · 🔧 was failing, fixed in this pass · 🟡 acceptable, could be better

### Boot / splash
✅ Illustrated world, logo lockup, loading bar. Nothing to change.

### Main Menu
- Colour/button: 🔧 TAP TO START was the only **orange** button in the game → green.
- Reward: 🔧 no sign of anything earned → **saved coin wallet** top-right.
- 🟡 "Teacher" corner chip is deliberately quiet (adult control).

### Name Entry (first screen a child sees, once)
- ✅ Wood title, green LET'S GO!, Ms. Lumi.
- 🟡 **Images:** the two runner choices are small tiles. Enlarging them needs a layout change; left for the art pass.

### Session Map
- Progress: 🔧 the current mission did not stand out (its gold glow was a faint haze on the dark board) → **pulsing green GO! badge** on the current mission.
- Reward: 🔧 wallet chip.
- 🟡 Tiles keep the "2/3" counter (owner's decision 2026-08-23, stars meant two things).

### Story Select
- 🔧 Wood frames on a wood board; three unrelated chip pills (green EASY clashed with "go", red HARD read as danger); dark empty stars; thin titles → **level cards outlined in difficulty colour** (EASY blue, MEDIUM yellow, HARD purple), one chip style, bold titles, soft empty stars. GREEN PLAY, NAVY REPLAY.
- Text: 🔧 "Stars = your race score" caption removed.

### Reader — story page
- Character: 🔧 Ms. Lumi was a still image → sways, changes pose with a bounce.
- Pace: 🔧 QUESTION! waits until the narration ends; narration regenerated slower (voice C, −15%).
- Layout: 🔧 NEXT/QUESTION button centred.
- HUD: 🔧 page badge (green) and voice toggle (cyan) → navy chips; coin counter.
- 🟡 Story text stays calm Nunito on cream on purpose — it is the reading.

### Reader — question
- G1: 🔧 wrong answer no longer reveals the right one: "Not quite! Do you want to read the page again?" + READ AGAIN; tried option set aside; can't continue until correct.
- Feel: 🔧 pastel pills → **yellow question banner + chunky white answer cards with coloured A/B/C badges**; correct turns green and **pays coins**.
- Character: 🔧 Ms. Lumi now **stays** on the question and reacts — cheer + sparkles + "Great reading!", or thinking pose + "Look again!" (never sad), with a pop sound.
- Text: 🔧 the italic line that repeated the question removed.

### Loading between parts
- Progress: 🔧 the SWBST definition tips (the client's "unnecessary text") removed → **mission path READ → RACE → ORDER → WRITE** with stars on finished parts and the next part pulsing. This makes G1 visible as a game journey.

### Race — briefing
- Text: 🔧 four paragraphs → three short lines; spoken lines re-recorded.
- Colour: 🔧 Ms. Lumi's bubble was pink → white.

### Race — running
- G2: 🔧 answer cards **pop up out of the road** as you approach, then bob.
- G2: 🔧 **patrol** runs up at GO!, drops back on a clean run, returns after a miss; always fully in frame when shown (was a cut-off hat).
- G1: 🔧 a wrong or missed part **comes back** without the tried card until collected; timer chip says "Try again in N s".
- Colour: 🔧 the answer panel cards were pink → white outlined, the same as every other answer card.

### Race — finish
- G2: 🔧 **MISSION COMPLETE!** stamp, fireworks, rising chimes, the story read back, 5/5 badge.
- Colour: 🔧 TAP TO CONTINUE was a near-black plaque → **green** (it is the go action).

### Arrange (SWBST)
- G1: 🔧 no auto-solve; retry until correct; correct boxes lock; hints move between wrong boxes; honest "Not yet!" when nothing is right.
- Feel: 🔧 pastel → **full-colour SWBST boxes**, white story-part cards, selected card lifts in yellow, locked box turns green and **pays coins**; navy UNDO.

### Summary
- G1: 🔧 checked offline against the story (English, names the Somebody, at least three parts in order); feedback names the missing part; grammar not graded.
- G1: 🔧 the card listed all five **answers** and the placeholder started the answer → card shows each part's **question**; neutral placeholder. The learner's words must come from understanding.
- Feel: 🔧 **five SWBST gems light up live** as each part is written; tips block removed; coin counter.

### Results
- Feel: 🔧 dark teal panel + black empty stars (read as a verdict) → **wood board** like the map, soft empty stars, chunky SWBST gems, **coins counted into the wallet**.

### Teacher (adults)
- 🔧 lilac kit panel → white card. Otherwise deliberately plain.

---

## 4. Factor summary after this pass

| Factor | Before | After |
|---|---|---|
| Feedback / juice | Colour change + text line | Sparkles, flying coins, punches, Lumi reactions, pops |
| Reward | None | Coins per success; saved wallet |
| Progress / goal | Screen after screen | Mission path; GO! badge; SWBST tracker and gems |
| Character | Still picture | Moves, reacts, speaks, encourages |
| Animation | Sparse | Idle motion everywhere; pop-ins; pulses |
| Transitions | Fade + definition text | Fade + mission path |
| Colour language | Green/orange/teal/cyan/yellow/blue, no rules | Six roles, documented |
| Button style | Kit pills of mixed styles | Chunky outlined cards; one style per job |
| Fonts | Regular body type on UI | Bold rounded Fredoka for UI; Nunito kept for story text |
| Sound | Present | Present + coin chimes, bubble pops |
| Backgrounds / images | Good illustrated art; 30 real hero images | Unchanged (already strong) |
| Text load | Tips, captions, long briefing | Removed or shortened |

## 5. Still open (honest)

- **Tablet playtest** — colours at classroom brightness, touch sizes, performance.
- **Voice** — one consistent voice, slower; still synthetic (only a human recording removes that).
- **Name Entry runner tiles** are small (art/layout pass).
- **Researcher sign-off** — retry-until-correct overrides locked GDD rules; the first attempt is still the study measure.
