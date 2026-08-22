# SummaRace — per-scene UI audit

**2026-08-22.** Requested: *"each panel or scene, please review and analyse each detail — what can
be improved in terms of label, styles, animation, clarity, font. Don't limit yourself."*

Grounded in the scene files, the controllers, and the owner's device screenshots. Where something
is inferred rather than measured, it says so. **Nothing here has been seen running** — the Editor
cannot load this project's scripts (see the 2026-08-21 playtest doc §0b).

Severity: **A** = fix before the study · **B** = worth doing · **C** = polish / v2.

---

## 0. The systemic finding — one bug class, spread across five screens

This is the most valuable thing in the audit, and it is not visible on any single screen.

Counted across the ten authored scenes: **33 of 89 TMP labels have autosize OFF, and all 89 use
overflow mode `Overflow`.** `Overflow` means text that does not fit is *drawn outside its box*,
over whatever is next to it — it does not shrink and it does not clip.

```
  Summary      7 of 7 off      Arrange      8 of 19 off
  Results      4 of 5 off      TeacherMenu  3 of 8 off
  NameEntry    4 of 5 off      others       0-3 each
```

The concentration is the tell. Those are exactly the screens showing **runtime-variable content**:
a name a child typed, a story title, the child's own sentence, an export file path. A label
showing a fixed UI string can be laid out once and trusted. A label showing *content* cannot —
and this project has already shipped this exact bug three times: the race tracker printing a whole
sentence into a 118 px plaque (F46a), "SOMEBODY" ellipsised on every race (F47f), and the Arrange
pool pills, found in this pass.

**Done:** new `SummaRace.UI.LabelFit.Harden()` — one place, three properties (shrink, clip, wrap),
with a floor so "it fits" never becomes "it is unreadable". Applied to the ten Arrange labels and
to TeacherMenu's status line. **A** — the remaining sites should be swept once the Editor compiles
and someone can look at each screen.

---

## 1. Boot

| | |
|---|---|
| **works** | crown + SUMMA/RACE lockup, gold-gradient type with a dark depth copy, float + pop-in, real progress bar, spoken tagline |
| **B — the splash is 2.0 s of nothing to do** | it is the first thing a child sees ten times. Either shorten to ~1.2 s, or give the bar something honest to report (it currently sweeps rather than tracking real work) |
| **C — no `ButtonSquash`** | correct: there are no buttons here |
| **clarity** | the tagline *"Read! Race! Summarize!"* is the best three words in the app — it teaches the whole loop. Keep |

## 2. Main Menu

| | |
|---|---|
| **works** | Boot's twin, so the transition is seamless; TAP TO START with ring + pop; teacher corner discreet but not hidden |
| **B — "TAP TO START" is the only affordance and it is a label, not a button shape** | a nine-year-old on a shared tablet will find it, but a pulsing ring or a gentle bob on the words themselves would remove the half-second of doubt. `UIFloat` is already on the scene (4 uses) — one more costs nothing |
| **C — no exit** | correct and deliberate: `Application.Quit` appears nowhere and Android BACK is swallowed app-wide, so the app has no unconfirmed way out. Do not add one |

## 3. Name Entry

| | |
|---|---|
| **works** | 5 `ButtonSquash` (the best coverage in the app), four avatars distinguished by *shape and colour* — not colour alone, which was a deliberate fix; an empty name box is never an error |
| **A — 4 of 5 labels have autosize off, and one of them shows a name a child typed** | a long name draws outside its box. Covered by §0; sweep this scene first |
| **B — no `PanelIntro` on the avatar row** | the four avatars appear all at once. A 0.06 s stagger (the pattern the briefing chips already use) turns a static form into a choice |
| **B — this is the child's first impression and it has no music** | fixed once before (F50) — verify it survived |

## 4. Session Map ("Pick a Mission")

| | |
|---|---|
| **works** | the strongest scene in the app: 11 `ButtonSquash`, 10 `UITwinkle`, 4 `UIFloat`, glow marking the current mission, all 12 labels autosize on |
| **done this pass — the green stops are now dark** | the green was the kit sprite's own paint, so it had to be swapped (to the kit's neutral grey) before it could be tinted; a multiply toward black over saturated green just gives dark green |
| **done this pass — the "Pick a Mission" banner is now dark with cream type** | 12.4:1 contrast, and it cannot produce dark-on-dark (see `TitleBannerSkin`) |
| **B — the stops are a 2-column ladder, not a path** | the design intent was "one route snaking bottom to top". Right now they read as a grid of buttons. A drawn dotted trail between consecutive stops would cost one sprite and turn the screen into a journey — which is what makes a child want the next one |
| **B — locked stops all look identical** | ten identical dark plates with padlocks. Showing *which one is next* more loudly (the glow already exists, make it unmistakable) answers "where am I?" at a glance |
| **C — mystery light pill under the hint** | visible in the device screenshot, directly under "Each mission has 3 stories". Likely baked art inside the kit's `Rectangle 356` panel sprite — the kit ships several sprites with showcase content drawn in. Worth one look |

## 5. Story Select

| | |
|---|---|
| **works** | 5 `PanelIntro`, hero art per story, three-star row, EASY card is the whole button (a big target), locked cards nudge rather than dead-end |
| **done this pass** | title banner darkened |
| **B — AVERAGE and HARD are dimmed grey cards with a big padlock** | correct rule, discouraging presentation. They are two thirds of the screen and they say "no" twice. Showing the story's *name* on a locked card ("Next: The Day the Crayons Quit") turns a wall into a promise, and it costs one label |
| **C — hero art is 3:2 via an `AspectRatioFitter` with `preserveAspect` off** | an uncropped 16:9 replacement squashes ~16 % horizontally. Not a bug today (all 30 are pre-cropped) but it is a trap for whoever replaces the art |

## 6. Reader

| | |
|---|---|
| **works** | the most carefully built screen: option fan-in stagger, punch on the correct answer, progress bar sweep, A./B./C. prefixes with hanging indent, HEAR AGAIN, per-page slot hint, back offered only before the first answer |
| **B — 3 of 9 labels autosize off** | the page text is content of varying length. Sweep (§0) |
| **B — the correct answer is revealed by colour + punch** | the punch carries it independently of colour, which was the deliberate fix. Still worth adding a non-colour mark (a left-edge bar on the correct row) so the moment does not rely on motion a distracted child may miss |
| **C — no page-turn transition** | pages swap instantly. A 0.15 s slide would make five pages feel like a book rather than five screens |

## 7. Race

Covered in depth in the 2026-08-21 playtest doc. This pass added: the SWBST question line, bigger
tracker letters on uniform dark plaques, wood panels replacing the gold, the patrol tail, the
leave confirmation, the finish dance, and the pacing fix below.

| | |
|---|---|
| **A — done: easy had 47 s of dead running in a 107 s race** | difficulty scaled the whole gate *gap* while the reading window stayed a flat 12 s, so every second "easy" bought was spent running past nothing — and it was backwards, the gentlest difficulty being the most boring. Difficulty now scales the **reading window**: easy reads 16.2 s per gate (the ~16.5 s a 70 wpm reader needs) and its dead time drops to 25 s. Same race length, strictly more reading |
| **A — done: first-race steering coach** | three ghosted lane zones and a hand that visits each one, over the 3-2-1 while the world is held. Once per device. Cannot swallow a tap (everything is `raycastTarget = false`) |
| **B — the top of the HUD is crowded** | tracker, then question, then option board, then the countdown chip: four stacked strips above a road the child also has to watch. Nothing is wrong individually; together they are most of the upper screen. Worth a portrait render once the Editor compiles |
| **B — a yellow sliver is visible above the tracker** in the device screenshot | something is drawing behind/above the tracker board. Not identified — needs a live look |
| **C — the world is a grey street; the target is a bright playground** | the real art gap. See the flow document, Tier 1: coloured lane stripes are the cheapest large step |

## 8. Arrange

| | |
|---|---|
| **works** | correct slots lock and can never be lost, UNDO, automatic hint ladder, never-stuck assist after 4 failed verifies, every attempt logged |
| **A — done: 8 of 19 labels had autosize off** | the pool pills are a 2-column grid (identical to the web prototype), so a 53-character part — which the content pipeline allows — was three lines in a two-line pill, with the third drawn over its neighbour. All ten piece and slot labels hardened |
| **B — pieces vanish when placed** | `SetActive(false)`. A child who mis-taps sees a piece disappear. A shrink-and-fade into the slot (0.15 s) makes the same event legible |
| **B — the pool has no empty state** | when all five are placed the area is blank. One line ("Now check your order!") pointing at CHECK ORDER would close it |

## 9. Summary

| | |
|---|---|
| **works** | sentence frame built from the child's own collected parts, SWBST reference list, DONE TYPING chip so the keyboard never traps them, at most two nudges then always accepted |
| **A — 7 of 7 labels have autosize off**, the worst ratio in the app, on the screen showing the reference list (story content) *and* the child's own typing | sweep first, with Results |
| **B — only 1 `ButtonSquash` on the whole scene** | the least tactile screen in the app, and it is the one asking for the most effort |
| **B — no visible progress toward "one sentence"** | the child types into a void and finds out at SUBMIT. A quiet live cue — the frame's blanks filling in as their words match, or a simple word count — turns two nudges into continuous feedback. Must stay a *cue*, never a grade, and never auto-fill |
| **C — the prototype's 14 s red timer is correctly absent** | a countdown on a writing task measures panic. Do not add it |

## 10. Results

| | |
|---|---|
| **works** | star reveal, treasure gems matching the exact first-pick mix, the child's own sentence echoed back (that card autosizes — it was built in code), race time |
| **A — 4 of 5 scene labels autosize off**, including the story title | the title crossing the trophy has been fixed once already (F56h) — the underlying sizing was not. Sweep |
| **B — the stars land and stop** | this is the payoff screen of a 5-minute loop and it has 1 `ButtonSquash` and 1 `UIFloat`. It should be the loudest, most animated screen in the app and it is currently among the quietest |
| **B — nothing says what improved** | "3 stars" means little on its own. "You got 4 of 5 first tries — last time it was 3" is the single strongest motivator available, and every number needed is already in the log |

## 11. Teacher Menu

| | |
|---|---|
| **works** | PIN never stored raw, export writes a companion roster, two-tap wipe, participant-code capture, 30 s lockout after 5 wrong PINs, hold-to-reset recovery |
| **A — done: the status line prints the export file path** and was authored with autosize off and `Overflow` | on the one action that retrieves the entire study dataset, an unreadable path is a data-loss bug wearing a layout bug's clothes. Hardened with an 18 pt floor |
| **done this pass** | title banner darkened |
| **B — 0 `PanelIntro`, the only scene with no panel animation** | fine for a teacher tool; mentioned only so it is a decision rather than an oversight |
| **C — "Export logs" keeps its yellow** | deliberate: it is an action, not a title, and yellow is doing real work there — it is the button the researcher must not miss |

---

## 12. Cross-cutting

1. **Dark furniture, bright content.** The direction this pass took, on the owner's note: panels,
   banners and plaques go dark and calm; saturated colour is spent only where it *means* something
   — the SWBST palette, hero art, the world. Nine surfaces moved (three race cards, the race
   reveal, the tracker, the briefing chips, five title banners, ten session stops). What is left
   bright on purpose: the SWBST palette, the green CTAs, the Boot/Main Menu logo lockup, and
   TeacherMenu's Export.
2. **Two fonts, used consistently** — Fredoka for headings and buttons, Nunito for body. No change
   needed; this is one of the more disciplined parts of the project.
3. **Animation coverage is lopsided.** Session Map has 11 squashes and 10 twinkles; Summary and
   Results have 1 each — and those two are the *effort* screen and the *payoff* screen. If there
   is budget for animation anywhere, it is those two, in that order.
4. **Everything above needs a portrait render.** The Editor's landscape Game view has hidden every
   layout bug this project has ever had; the fix each time was rendering the canvas through an
   offscreen camera at 720×1280. That is blocked until the URP package split is repaired.
