# SUMMARACE — FINAL EXPECTED UI SPEC
### What the game LOOKS like, screen by screen, start → end — the visual checklist

**2026-08-21.** Use this to CHECK the build: every element below should look/behave as
written. Marks: **✅ = already in the build** · **⏳ = to build/change** (from the Blueprint
§9) — anything on screen that doesn't match a line here is a bug or an unapproved change.

> **2026-08-21: synced to disk after the seven-agent audit — see `summaracefinal/FINAL.md`.**

Reference canvas: **1080 × 1920 portrait** (positions given as fractions of that).
Palette shorthand: Gold `#FFD95A`-family · Cream `#F9EDCC` · Navy `#1C2B54` · Ink (near-black
navy) · TextBrown `#593F19` · SWBST = S blue · W green · B red · S orange · T purple.
Fonts: **Fredoka SemiBold** (titles/buttons) · **Nunito** (body).

---

## 0. APP ICON & LAUNCH
- **Icon:** our crown + SUMMA/RACE! lockup on sky (all densities). ⏳ adaptive-icon layers
  exist (`app_icon_adaptive_bg/_fg.png`) — assign in Player Settings before ship.
- Tapping it opens fullscreen portrait, status bar hidden, never rotates.

## 1. BOOT (splash — ~2 seconds)
**Environment:** bright painted playground-trail backdrop (`bg_splash`), soft radial light.
| Where (y) | Element |
|---|---|
| 0.545–0.795 | **Logo lockup**: kit crown above stacked **SUMMA / RACE!** — gold-gradient 3D-look letters with dark offset depth, ±2.5° playful tilt, 3 small star sparkles; gently floating (UIFloat) and popping in (PanelIntro) |
| 0.51–0.57 | Tagline: **"Read! Race! Summarize!"** — each word its own colour (red/green/purple) |
| 0.425–0.465 | "Loading…" (navy, small) |
| 0.365–0.395 | **Gold progress bar** in a navy pill trough (fills over the 2 s) |
**No buttons.** Auto-routes: first run → Name Entry, else → Main Menu.
**Audio:** pop of the lockup; no music yet.

## 2. NAME ENTRY (first run only; also after teacher "+ New learner")
**Environment:** same bright backdrop family; cheerful menu music **starts here**.
| Where | Element |
|---|---|
| top banner ~0.854+ | Title **"What's your name?"** |
| y 0.626–0.694 | Name input (placeholder **"Type your name"**) |
| y 0.712–0.766 right | ⏳(auto-appears while typing) navy **DONE TYPING** chip |
| ~0.50 | Prompt **"Pick your runner"** |
| y 0.373–0.467 | **4 avatar buttons** in a row — heart / star / gem / lightning, each a different shape AND colour; selected = full colour, others dimmed grey |
| y 0.181–0.259 | Big green **LET'S GO!** pill with dark ring |
**Rules to check:** empty name is fine (keeps "Runner"); tapping an avatar clicks + highlights;
keyboard never hides the way forward (chip). No back button — this screen completes forward.

## 3. MAIN MENU
**Environment:** Boot's twin — same backdrop, same floating logo lockup + tagline; coins/gem
decor corners; menu music looping.
| Where | Element |
|---|---|
| centre-low ~0.25 | **TAP TO START** — big orange pill, pop + float |
| y 0.152–0.203, x 0.26–0.74 | Navy pill **"Playing as <name>"** (hidden if no learner) |
| bottom corner | Small low-contrast **Teacher** button (deliberately quiet, not hidden) |
**Taps:** TAP TO START → Session Map · Teacher → Teacher Menu. No back (this is home).

## 4. SESSION MAP ("missions")
**Environment:** painted landscape board (bright sky + soft clouds drifting), gold banner
title at top, menu music.
| Element | Look |
|---|---|
| Title banner | Mission-map title + small subtitle explaining missions/stars |
| **10 stops** | Numbered circles in a grid (not a snake path). Per stop: number · 3 mini-stars underneath (gold = that story finished, dark = not) · **padlock** on locked stops (dimmed grey circle) · **glow ring** on the current (highest unlocked) stop |
| Bottom hint | On a dark backing pill: **"Your teacher opens the next mission!"** (only while something is locked) |
| Back | Bottom **Back** button → Main Menu |
**Behaviors to check:** locked stop tap = wiggle + lock punch + hint reappears (works muted);
finishing a session's 3rd story → arriving here plays a one-time celebration (stop punches,
3 stars pop one by one with coin ticks).

## 5. STORY SELECT
**Environment:** bright sky + drifting soft clouds, gold banner title, kit board holding
three cards; menu music.
Each of the **3 story cards** (full width of the board, stacked):
| State | Look |
|---|---|
| **Open** | Hero illustration (real art, 3:2 crop) · difficulty chip top-left (**EASY** green / **AVERAGE** tan / **HARD** red-orange — ✅ tinted per prototype) · story title · row of 3 stars (gold = best earned; dark silhouettes otherwise) · **breathing gold ring** around the whole card · **PLAY** badge (gold pill, dark-brown word) bottom-right — the whole card is the button |
| **Locked** | Art dimmed to dark slate · **padlock icon** centre · **"Locked"** + hint line ("finish the story above" — or "this story isn't ready yet" if content failed) · tap = wiggle + lock punch, never navigates |
| Back | → Session Map |
**Rule to check:** exactly one card is ever "open-new"; earlier ones stay replayable; stars
persist per story.

## 6. READER
**Environment:** softly blurred painted playground room (`bg_reading` family), **no music**
(narration owns the audio), page-turn sfx between pages.
**Top HUD row (~y 0.945+):** progress badge **"Page n / 5"** (→ "Question n / 5" during
questions) · thin **pages progress bar** sweeping as you advance · **VOICE ON/OFF** toggle
(speaker icon, white = on, grey = off) · ⏳(under it, y 0.902–0.945) **HEAR AGAIN** navy chip
(only on pages that have narration).
**Reading page:**
- Large rounded **story card** (cream/gradient) centre screen with the page text (Nunito,
  black, autosized), text revealing with a gentle fade.
- **Ms. Lumi** sits bottom-left with a white speech bubble (pose varies per page — wave,
  smile, point…), visible ONLY on reading pages.
- **NEXT** big cyan pill bottom centre (x 0.30–0.80).
- **BACK** navy chip bottom-left (x 0.035–0.235, y 0.040–0.105) — **exists ONLY before the
  first answered question**; first tap arms it (turns gold, label changes to confirm), second
  tap within 4 s leaves to Story Select.
**Question page (after NEXT):**
- Story card hides — the question is its own bright page.
- Gold **question bar** on top ("Who is the story mainly about?").
- ✅ small 💡 hint line under it (per-slot wording).
- **3 option buttons** stacked, cream pills, **"A. / B. / C."** prefixes with hanging indent,
  wrapped to 2 lines when long, fanning in one after another.
- Ms. Lumi hidden; BACK hidden.
- On answer: tapped wrong options grey out, the **correct one turns friendly green + punches**;
  feedback line pops in — praise (green) or **"Not quite — here is the answer!"** (warm amber,
  never red); correct/incorrect sfx; NEXT returns (label **"NEXT PAGE"**, on the last page
  **"START RACE"**).
**Check:** options are position-shuffled every page; a second tap on options does nothing;
double-tap on the final START RACE cannot fire twice.

## 7. RACE — the big one
### 7a. Mission briefing (world hidden behind this)
**Environment:** full-screen `bg_splash` backdrop (no grey scrim, no engine menus visible —
ever).
| Element | Look |
|---|---|
| Gold title pill overlapping a big cream **mission card** (x 0.06–0.94, y 0.34–0.84) | briefing title + body naming the story ("…**<Story Title>**…") + instructions: read the 3 answers at the top, tap the answer / tap left-middle-right, collect the 5 story parts in order |
| Inside card bottom | **5 SWBST chips** (S blue · W green · B red · S orange · T purple, white letters) popping in one by one |
| Bottom-left | **Ms. Lumi** (half-body, ~x 0.03–0.25) with white bubble **"Ready, runner?"** |
| ✅ near Lumi | patrol framing line — implemented deliberately softer than a "PATROL IS COMING!"-style beat: **"If you miss a part, the patrol races past. It never catches you!"** (`GameText.RaceBriefingPatrol`) |
| Bottom centre (y ~0.18) | Big glossy **green START pill** with dark-green ring — disabled ("Getting ready…") until the world is built, then **"START!"** + punch |
**Voice:** briefing instruction read aloud. **Failure look (UC-8):** same screen, body text
changes to "this race isn't ready", button becomes **Back to Story Select**.

### 7b. Countdown
Briefing drops → giant **gold 3 … 2 … 1 … GO!** centre screen (320pt, pop each beat, tick
sfx, boost sfx on GO) while the camera sweeps from low-behind up into the chase position.
Runner stands idle until GO, then breaks into a run. **Check: exactly ONE countdown (no
small white second countdown anywhere).**

### 7c. The run (the world)
**Camera:** behind and above the runner (chase view), road curving away with a gentle
horizon bend.
**The runner:** our kid character (Aj) running centre-lane, leaning into lane changes,
jump animation on correct collects.
**Environment (varies per mission — see §12 table):** 3-lane street/suburb/industrial
corridor built from the runner-kit's art: houses/walls/warehouses lining both sides,
roadside **trees & grass clumps** per world recipe, sky dome tinted per world (morning blue /
sunset gold / night navy…), linear fog with the world's colour, optional **weather**
(rain streaks / snow / floating motes) drifting with the camera.
**HUD, top → bottom:**
| Where (y) | Element |
|---|---|
| ~0.896–0.96, slid left | **SWBST tracker**: wooden board with **5 beveled plaques** — collected = full element colour with the WORD (SOMEBODY…) in white; current = brighter + pulsing + big letter; upcoming = natural wood + faded "?" |
| same row, right gutter | **Pause chip** — small dark wooden square, two cream pause bars (48 dp) |
| 0.745–0.885 | **Reading panel**: dark-wood board holding **3 white cards in lane order** with the 3 options (black Fredoka, autosized 26–44, wraps, ellipsis-guarded). Appears ONLY when the gate is ≤ ~12 s away, with a **double gold border pulse** + pop + the tracker slot pulsing its colour. **Each card is tappable → steers to that lane** |
| ✅ beside/under panel | **"⏳ Gate in 12s…"** countdown chip (real gate arrival) |
| just under panel | Banner text — only on the final stretch: **"Run to the FINISH!"** |
| middle | **Feedback pill** (dark wood): praise on correct ("That belongs in your summary!" …), pops in, fades ~1.4 s |
| screen edges | **Amber vignette** — invisible normally; glows in for ~2 s after a wrong pick |
| ✅ during that surge | **Patrol cameo** — cop/patrol sweeps across the frame edge, never near the runner |
**On the road:** each gate = **3 white rounded cards** side by side (one per lane) with the
same 3 options as the panel; a **coloured halo frame** (current element's colour) sits behind
whichever card the runner is lined up with and slides with lane changes; **no coins between
gates** — deliberately absent in the shipping endless race (EXP3: gates replaced Trash Dash's
coins/powerups; coin lines belong to the dead legacy `Race.unity` only).
**Moments to check:**
- Correct: collect sfx + tiny vibration + **gold sparkle burst** + the word lifts off and
  **flies into its tracker slot** (punch) + brief speed boost + praise line.
- Wrong: "not quite" sfx + slow-down ~1.5 s + amber surge (+ patrol sweep) + the panel
  becomes **one full-width GOLD card showing the correct answer** for 2.2 s (a statement,
  not a question — not tappable), then next gate.
- Missed gate: same reveal treatment, no extra scold.
- **FINISH:** one wide gold card spanning all lanes, deep-brown **FINISH** text; crossing it
  = star sfx, banner **"FINISH!"**, runner stops to idle, music stops, 2.2 s beat → Arrange.
**Pause overlay:** dark dim + gold card **"Take a rest?"-style title** + big green **RESUME**
+ small wood **LEAVE RACE** chip (first tap arms it gold "Tap again to leave", 4 s), whole
world + audio frozen behind it.

## 8. ARRANGE
**Environment:** warm painted cozy-room backdrop; menu music returns; Ms. Lumi **badge**
(round portrait, top-left) with the title.
| Where (y) | Element |
|---|---|
| top | Badge + title **"Put the story parts in order!"** (⏳ + Lumi bubble "Great running! Now organize the story elements…" per prototype) |
| 0.52–0.89 | **5 slot rows** (x 0.06–0.94): empty = pastel of that element's colour with the bold element name in its ink colour (SOMEBODY / WANTED / BUT / SO / THEN); filled = cream with the placed text; **locked-correct = green** |
| 0.13–0.48 | **The pool** — ✅ **2-column side-by-side pills (Piece_0..4 anchored 2-column, rows 2+2+1), warm-yellow like the prototype, wrapped text** — so pool ≠ slots at a glance |
| bottom | **UNDO** (grey pill, left) · **VERIFY ORDER** (green pill, right) · status line above them |
**Behaviors to check:** tap piece (turns blue-selected) → tap slot places it; tap filled slot
returns it; VERIFY: greens lock with lock-click one by one, wrongs wiggle **amber** (never
red) and hop back to the pool; status shows praise / "Almost!" / a **hint naming the
element's meaning** after repeated misses; after 4 tries the app **finishes the board with
the learner** (pieces glide in one by one, friendly line) and continues. Nothing here has a
timer.

## 9. SUMMARY
**Environment:** same cozy-room family backdrop; menu music; Ms. Lumi badge top with her
bubble title **"Write your summary!"**; her second **speech-bubble card** (with the little
tail pointing up at her) holds the sentence frame: *"Somebody wanted ___, but ___, so ___,
then ___."*
| Element | Look |
|---|---|
| Reference card (cream) | The 5 parts as a numbered list, each **element name bold in its ink colour**, then the text: "1. SOMEBODY: Molly …" |
| ✅ tips block | two 💡 lines ("A good summary is short but complete." / "One sentence is enough if it includes all key events.") |
| Input box | large, rounded; ✅ placeholder is story-specific ghost text ("Molly wanted a turn on the swing, but…") — ghost only, never inserted |
| **DONE TYPING** chip | appears right of the hint row while the keyboard is up (hint slides left to make room) |
| **SUBMIT** | big pill bottom |
| Nudge line | above submit; warm wording, clears when typing resumes; max 2, then accept |
No back button (one-way); no timer.

## 10. RESULTS
**Environment:** celebration on the same backdrop; menu music + victory sting; **Victory
panel** kit frame; ✅ Ms. Lumi badge celebrating.
Sequence the child SEES (order matters):
1. Story title at top (must not cross the trophy art).
2. **3 star sockets** — earned stars flip from dark silhouette to gold with punch + sfx +
   little vibration, one by one.
3. **Treasure chest** + **5 SWBST letter-gems** popping in L→R (full colour = that element's
   first pick was right; greyed = missed) with coin ticks.
4. Praise line (pool by stars; wording is **process praise by design** — "You summarized the
   whole story!" style, deliberately reworded from the prototype's "superstar" line).
5. **Main Idea card** — the story's main idea sentence.
6. **"You wrote:"** card — the child's own sentence, verbatim, plain warm styling, **no
   grade, no comparison** (absent if they wrote nothing).
7. ⏳ optional: **"Your race: 1:42!"** finished-time line (decision D3, pending).
8. Bottom button: **NEXT STORY** (mid-session) / **NEXT MISSION** (after 3rd) — appears only
   after the reveal, always appears even if something failed.

## 11. TEACHER MENU (adults; deliberately plain)
| State | On screen |
|---|---|
| Gate | Title **"Teacher"** · one input (dots for PIN) · prompt above it (**"Enter PIN"** / setup: "Teacher setup: choose a PIN (4+ digits)" → "Type the same PIN again") · **OK / NEXT / SAVE CODE** button · status line (wrong PIN, cooldown "Too many tries. Wait 30s.") · **Back** on the canvas at all times |
| Participant code step | prompt "Participant code for this learner — copy it from their test booklet (e.g. P07)"; input switches to visible alphanumerics |
| Actions panel | Column of kit pills: **Participant code: P07 / (no code)** · **Switch learner** · **Unlock next session** · **Export logs** · **Delete all data** (arms to "Tap again…" and disarms if you do anything else) |
| Learner picker | Board overlay "Who is playing?" — scrollable rows "**P07 · Maria · Session 3** (playing)" + **+ New learner** + **Done** |
| Recovery | Reached ONLY by holding OK 6 s on an empty box: red-serious copy "Reset this tablet? This erases every learner profile…", **ERASE TABLET** → "Tap again to erase" |
**Sound rule to check:** every refusal (wrong PIN, nothing to export, mismatch) makes the
*nudge* sound, never the success click.

## 12. THE TEN RACE ENVIRONMENTS (what each mission's run looks like)
| M | World | The look |
|---|---|---|
| 1 | morning_suburbs | fresh low gold sun, pale blue sky, houses + garden walls, a few trees, light haze |
| 2 | bright_park | the sunniest: clear blue sky, greenest verge (most trees/grass), open warehouse-yard stretches reading as parkland |
| 3 | sunset_town | warm orange-gold light, long shadows-feel, town walls/blocks, sky burning amber |
| 4 | blue_hour_suburbs | dusk: day houses under the **night sky dome**, cool blue light, lit-window urban stretches |
| 5 | overcast_industrial | flat grey light, close fog, warehouses & sheds, almost bare verge — moody |
| 6 | golden_fields | golden haze, industrial-yard openness dressed with heavy grass — reads as fields |
| 7 | night_city | true night: dark urban blocks, near-black blue sky, rain streaks, glowing feel |
| 8 | misty_morning | pale milk fog close to the camera, soft white sun, town shapes fading out, floating motes |
| 9 | autumn_lane | russet warm light, the leafiest suburbs (most trees), amber fog, drifting motes |
| 10 | starlit_finale | celebration night: deep navy starlit dome, suburbs under snow-fall sparkle |
Within a mission: Easy = earlier/warmer/greener · Hard = later/cooler/barer. Same story =
same street for every learner, always.

## 13. GLOBAL LOOK & FEEL RULES (check everywhere)
- Loading between screens: bright backdrop + **gold tip card** (a SWBST definition) + gold
  progress bar; the tip is SPOKEN only entering Reader/Race/Arrange/Summary.
- Every button: squash on press; pop-in on appear; press/pop/transition sfx; labels Fredoka.
- Feedback colours: green = correct · warm amber = try again · **red is never used for
  feedback** · every state also carried by a word or motion, never colour alone.
- Ms. Lumi never frowns. Praise never repeats twice in a row.
- Android BACK does nothing, anywhere. The keyboard is always dismissible. No screen ever
  dead-ends.
- Type floors: ~24 pt min on 1080-wide reference everywhere a child must read.

## 14. OPEN ⏳ ITEMS IN THIS SPEC (the to-build list, in one place)
1. ✅ Arrange pool → 2-column yellow wrapped pills *(finished on disk — Piece_0..4 anchored
2-column, rows 2+2+1, warm-yellow)*
2. ✅ Race "⏳ Gate in Ns" chip · 3. ✅ Patrol cameo sweep + briefing framing · 4. ✅ Reader 💡
hint lines · 5. ✅ Summary ghost placeholder + tips block · 6. ✅ Results Lumi badge +
process-praise line · 7. ✅ Story Select AVERAGE/HARD chip tints *(2–7 all verified live
2026-08-21)* · 8. ⏳ Adaptive icon assignment (Player Settings) · 9. ⏳ optional Results
finish-time line (decision D3) · 10. ~~(decision) reading window 12→17 s~~ **closed**:
`GameRules.RaceSecondsPerGate` = 20; gates deliver 17.1–23.2 s of reading.
