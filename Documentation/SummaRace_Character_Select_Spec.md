# Runner Select — design spec

**2026-08-22.** Owner: *"check this screenshot, the worst design I see — what, that avatar?
…for this selection of character we can make it 3D, specially the player… make it like a game
where you pick a champion, view the whole character and you can rotate… left is name, selection
of character right… when changing option the character like a little RPG game, with maybe grey or
dark background — or what is best? …should we make a new scene, focused camera, zoom, halt, boy
where it can rotate character?"*

He is right, and the screen is worse than it looks.

---

## 1. What is actually wrong — two things, and the second is the real one

**The visible problem.** "Pick your runner" sits above four tiles showing a **heart, a star, a
gem and a lightning bolt**. None of them is a runner. The screen asks a child to choose something
it never offers.

**The invisible problem, and it is the one that matters.** `LearnerProfile.avatarIndex` is
**written on this screen and read by nothing else in the entire game** — six references project-
wide, all of them inside Name Entry plus the field declaration. Whichever badge a child picks
never appears again: not on the map, not on Results, and above all not in the race, where they run
as Aj regardless.

So this is not a styling job. **The screen makes a promise on a child's first thirty seconds with
the game and then silently breaks it**, and no amount of 3D fixes that on its own. The fix is: the
character you pick is the character you run as.

**Shipped now as a stopgap:** the label reads *"Pick your badge"*, which is at least true of a
heart and a lightning bolt. That is the end of the lie, not the fix.

---

## 2. The roster problem — read this before designing anything

Every humanoid mesh in the project, measured:

| asset | what it is | usable? |
|---|---|---|
| `Art/Characters/Aj.fbx` (6.7 MB) | Boy01 — **the current race runner** | ✅ |
| `Art/Characters/Ch46_nonPBR.fbx` (28 MB) | the kid Aj replaced in F38 | ✅ but heavy |
| `Art/Characters/Ty.fbx` (5.1 MB) | the ZombieRun patrol body | ⚠️ not a kid |
| `Plugins/BitGem/…/cop.fbx` (244 KB) | the police officer | ❌ he is the chaser |
| `Bundles/Characters/Raccoon` | Trash Dash's animal | ⚠️ off-tone |
| `Plugins/StarterAssets/…/Armature.fbx` | grey mannequin | ❌ |

> **There is no girl character in this project.** The note says *"we have few avatars that exist,
> women kids boys"* — that is not what is on disk. What exists is **two boys**, one of whom is the
> patrol.

A champion-select built for two characters is a worse screen than four badges, because it
advertises a roster and then shows you it is empty.

**The cheap fix, and it is the owner's action not mine.** Aj and Ch46 are Mixamo characters on the
standard `mixamorig` skeleton — which is why F38's swap of one for the other cost nothing and
every clip retargeted free. **Any Mixamo character downloads into this project the same way and
inherits all six existing animations at no cost.** Four downloads (a girl, a second girl, a
different boy, one wildcard) gives a real roster for an afternoon's work and zero code.

**Recommended roster: 4–6.** Below four the grid looks broken; above six a child spends the
session choosing. Include at least two girls — 40 Grade-4 learners is roughly half girls, and a
picker with no girl in it is a worse first impression than no picker at all.

---

## 3. Layout — the split has to be the other way round

The note says *left is name, character selection right*. That is a **landscape** layout, and this
game is **portrait-locked** (and must stay so — every canvas, every measurement and the whole race
HUD is built against 1080×1920). Two columns in portrait gives each side ~500 units: too narrow
for a full-body character and too narrow for a name field with a keyboard over it.

The portrait form of the same idea — and the one every mobile game with a character picker
actually uses:

```
┌──────────────────────────────┐  ← dark stage, full width
│                              │
│         ╭──────────╮         │     THE STAGE  (top 52%)
│         │          │         │     • character full-body, filling ~80% of the stage height
│         │  runner  │         │     • drag left/right to spin
│         │          │         │     • idle animation running
│         ╰──────────╯         │     • soft ellipse shadow under the feet
│      ‹  ● ○ ○ ○ ○  ›         │     • dots = roster position
├──────────────────────────────┤
│   What's your name?          │     THE FORM   (bottom 48%)
│   ┌────────────────────┐     │
│   │ Type your name     │     │
│   └────────────────────┘     │
│   [👦][👧][👦][👧][🧒]        │     roster thumbnails, tap to switch
│                              │
│        ┌────────────┐        │
│        │  LET'S GO! │        │
│        └────────────┘        │
└──────────────────────────────┘
```

Why this ordering: the character is the reward, so it goes where the eye lands first; the name
field sits above the fold of the on-screen keyboard, which on this screen has already caused one
bug (F50 added DONE TYPING because the keyboard covered everything left to do).

---

## 4. "Grey or dark — what is best?"

**Dark, but not flat grey.** Flat grey reads as *unfinished* to a child — it is the colour of a
loading state. What makes a character pop in every game that does this well is three things
together, and the middle one is what flat grey is missing:

1. **A deep, slightly warm dark** rather than neutral grey — around `#1B1520`, a very dark
   plum-brown. It sits in the same family as the app's new dark furniture (`Theme.TextBrownDeep`),
   so the stage looks like part of this game rather than a borrowed screen.
2. **A radial gradient, brightest directly behind the character's head**, falling to near-black at
   the edges. This is the whole trick: it separates the silhouette from the background without a
   single extra light, and it points the eye at the face.
3. **A warm rim light from behind-right** plus a cool fill from front-left. The rim is what makes
   a low-poly character read as three-dimensional; without it a dark background just eats the
   silhouette.

Plus a **soft ellipse shadow** under the feet — cheap, and without it the character floats, which
is the exact complaint already raised about the patrol.

Do **not** put the world behind them. A playground backdrop competes with the character and makes
the two look pasted together; the dark stage is what says *this one is yours*.

---

## 5. Interaction

| | |
|---|---|
| **rotate** | horizontal drag spins the character; 1 screen-width ≈ 360°. Release → eases back to a ¾ front pose over ~0.6 s, so a child who spins it and looks away never leaves it facing backwards |
| **switch** | tap a thumbnail, or the ‹ › arrows. The outgoing character exits with a small hop-and-fade, the incoming one lands with a squash — **never a hard cut**, because the swap is the moment the screen is selling |
| **feedback** | each switch plays that character's idle from frame 0 and fires `SfxPop`. A wave or a jump on selection is better still if a clip exists — `HipHopDancing` already does, and it is exactly the "little RPG game" energy the note describes |
| **first open** | the character bounces in and does one idle loop before anything is interactive, so a child sees it is alive |

---

## 6. Build sequencing — and one hard warning

**Step 1 — the roster, as data.** `Data/RunnerRoster`: id, display name, prefab path, thumbnail.
One source of truth read by both the picker and the race, so the two can never disagree about what
character 3 is. Zero risk; do this first.

**Step 2 — the stage, inside the existing Name Entry scene.** A dedicated camera renders the
character to a RenderTexture shown in a `RawImage`, with its own light parented to that camera and
the character on its own layer. Self-contained: nothing about scene lighting or the main camera can
change how it looks. **Hard rule: if the roster is empty or a prefab fails to load, the stage
deletes itself and the existing badge tiles remain.** The screen must never be able to end up worse
than it is today.

**Step 3 — make the choice reach the race.** This is the half that carries risk, and it should be
a separate change with its own playtest. The runner lives in
`Bundles/Characters/Cat/character.prefab` — Trash Dash's character prefab with our kid's meshes
swapped in — loaded through their character database via Addressables. Changing it means swapping
the model child and re-pointing `animator.avatar` at spawn.

> ⚠️ **Do not do step 3 before the study.** That prefab is on the critical path of the measured
> stage. If it breaks, the race does not start — and the failure mode is Addressables-shaped,
> which means it works perfectly in the Editor and fails only on the tablet. That is precisely the
> class of bug that produced the "no road, no runner" blocker earlier in this project.

**Why none of this was built today.** The Editor cannot compile this project (URP package split —
2026-08-21 playtest doc §0b), so there is no Play mode and no portrait render. A character stage
is a *lighting* feature: built blind, the likeliest outcome is a black rectangle, and it would not
be discovered until it was on a tablet in front of a child. Everything else in the last three
passes was verifiable by compiling; this genuinely is not. It is specced to the point where it is
about an hour once the Editor is back.

---

## 7. What shipped today instead

| | |
|---|---|
| **"Pick your runner" → "Pick your badge"** | ends the promise the screen could not keep, until §2–6 land |
| **Android BACK now confirms** | see below — the other half of the note |
| **Name Entry BACK** | registers *blocked*: it is the first screen on a fresh device and there is nothing behind it, so BACK answers "Let's get you set up first!" rather than doing nothing |

### Android BACK, since it is the same note

BACK used to be swallowed silently — safe (it must never quit the app) but it reads as a broken
button and invites repeated pressing. It now always answers, and **what it does comes from the
screen, never from the guard**:

| screen | BACK does |
|---|---|
| Session Map, Story Select, Teacher Menu | asks *"Go back to …?"* — **STAY HERE** (big) / LEAVE (small) |
| Reader | asks the same, **but only while the on-screen exit chip is showing** — both read the same `mayLeave` line, so they can never drift apart. After the first answer the run is study data and BACK says *"Keep reading — you're nearly there!"* |
| Arrange, Summary | blocked, warmly — the run is mid-flight |
| Name Entry | blocked — nothing is behind it |
| **Race** | **opens the pause screen**, which is already the race's own confirmation. Stacking a second dialog over it would be two questions for one press, and the second could skip the first one's rules |
| anything unwired | falls back to the blocked form, so a forgotten screen is quiet-but-honest rather than silent |

Two details worth keeping: a **second BACK press means "no"** and closes the dialog (a child
pressing BACK repeatedly is not confirming anything), and the quit refusal via
`Application.wantsToQuit` is untouched — the app still cannot be closed by BACK in any state.
