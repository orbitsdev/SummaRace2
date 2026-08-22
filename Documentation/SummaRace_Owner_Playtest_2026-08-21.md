# Owner device playtest — 21 August 2026

Source: `Desktop/summarace/August 21.docx` — six screenshots and nine notes.
This document is the investigation, the decisions, and what shipped against each note.

---

## 0. The headline the document buries

**Those are Android device screenshots.** Status bar, 4G, battery, 21:25–21:59. Every prior
playtest in this project was an Editor session, and both standing build blockers — Android Build
Support, and Addressables content for Android — were recorded as open in `CLAUDE.md`. They are
closed. The race ran on a phone with road, scenery, runner and theme all present, which is only
possible if the Addressables content build resolved; `Library/Bee/Android/Prj/IL2CPP/Il2CppBackup/`
on disk confirms an IL2CPP player build actually ran.

That is the biggest state change since the last handover and it is not written down anywhere else.
The screenshots below are therefore **evidence**, not mock-ups.

## 0b. ⚠️ Read this before opening the project

**The Unity Editor currently cannot compile this project, and it is nothing to do with these
changes.** Chased to root cause:

* `Library/PackageCache` has `com.unity.render-pipelines.universal@580a03820d50` but **no
  `com.unity.render-pipelines.core`** — that one resolved as `"source": "builtin"` in
  `packages-lock.json`, so the two halves of URP are coming from two different places.
* Consequently Unity compiles `Unity.RenderPipeline.Universal.ShaderLibrary` **with no reference
  to `Unity.RenderPipelines.Core.Runtime`** — `Editor.log` line 3511 onward is a wall of
  `CS0246: GenerateHLSL could not be found`, and the compiler invocation exits 1.
* That failure cascades. `Library/Bee/artifacts/1300b0aE.dag/` has `Assembly-CSharp.dll`
  (22:31) but **no `Assembly-CSharp-Editor.dll`**, and `Library/ScriptAssemblies/` has neither.
  So in the running Editor **every one of our MonoBehaviours is an unloaded script**: MCP cannot
  find `SessionMapController` in the scene that has one, `run_tests` finds 1 test instead of the
  suite's 45+, and `SummaRace ▸ Build Preflight` cannot be invoked at all.
* It predates this session. The last good `Assembly-CSharp.dll` is 22:31; the first edit here was
  at 23:44, and the same URP errors appear near the top of the log.

**Remedy (owner, Editor closed):** delete `Library/PackageCache` and reopen so Unity re-resolves
URP cleanly, or add `com.unity.render-pipelines.core@17.4.0` explicitly to `Packages/manifest.json`
to force one consistent source. Deleting `PackageCache` does not touch `Library/com.unity.
addressables` or the built `aa/` content.

**A second, separate collision is waiting behind it.** Once URP is fixed,
`Assembly-CSharp-Editor` still fails on:

```
Library\PackageCache\com.adjoint.editor@2be1a802c4db\Editor\Windows\Adjoint\AdjointToolbarButton.cs(156,35):
error CS0433: The type 'AssetPathUtility' exists in both 'Adjoint.Editor' and 'MCPForUnity.Editor'
```

`com.adjoint.editor` and `com.coplaydev.unity-mcp` are both in the manifest and both export that
type. The editor assembly holds the whole EditMode suite **and** Build Preflight, so until one of
those two packages is removed, the project has no test safety net and no preflight. Verified by
compiling the editor assembly twice offline: it fails with that file included and succeeds
(227,328 bytes, zero errors) with it excluded.

## 0c. How these changes were verified, and how they were not

**Verified.** Both assemblies were compiled offline with Unity's own Roslyn against its own
generated response files (`Library/Bee/artifacts/1300b0aE.dag/Assembly-CSharp*.rsp`), with missing
`.ref.dll` entries repointed at real assemblies and URP Core added by hand:

```
Assembly-CSharp        384,000 bytes   0 errors
Assembly-CSharp-Editor 227,328 bytes   0 errors   (minus the Adjoint/MCP collision above)
```

The patrol geometry was verified by re-implementing the test fixture's projection in Python and
running it against the **camera read out of `MainSummaRace.unity`** and the shipped constants —
all five assertions pass (numbers in §2.1).

**Not verified.** Nothing here has been seen running. The Editor cannot load our scripts (§0b),
so there is no Play-mode look and no portrait render of the new HUD. The patrol tail, the tracker,
the question line, the reveal panel, the confirm panel and the finish dance all still need an
owner playtest. Where a change carries a specific risk that only a playtest can settle, it is
named in this document rather than left implied.

**One scene was saved while our scripts were unloaded** (`SessionMap.unity`, for the stop-sprite
swap). Checked afterwards: the diff is exactly ten `m_Sprite` guid changes, `MonoBehaviour:` count
122 before and after, `m_Script:` count unchanged — nothing was stripped. `MainSummaRace.unity`
was deliberately **not** opened in the Editor for that reason; its one change was made directly in
the serialized data (§2.8).

---

## 0d. Where the three callouts actually point — and a scoping error it caught

The document's floating text boxes were read out of the XML rather than eyeballed, because a
first pass guessed wrong about them and fixed partly the wrong screens.

`word/document.xml` puts **both "Too small" boxes and the "yellow border" note in the same
paragraph as `rId5`** — image 1, the *briefing* screen — and their anchor offsets place them
against it:

| callout | anchor | maps to, on that screenshot | points at |
|---|---|---|---|
| image 1 itself | x 0.17in, y 0.65in, 2.48 x 5.64in | the frame | — |
| "Too small" | x 3.31in, y 1.29in (beside the image) | 11–39% down | the title pill and the body text |
| "Too small" | x 2.20in, y 1.95in (over the image) | 23–35% down | the body text |
| "yellow border → gray or brown" | same paragraph | the card frame | the gold border + yellow title pill |

So notes 1 and 2 are about the **briefing**, not the race HUD. The first pass fixed the briefing's
*font* but then applied the colour notes to the in-race tracker and the answer-reveal panel —
changes that stand on their own merit (the tracker really is illegible in image 2, and the reveal
really is a slab of yellow), but they were not what these three boxes were pointing at. The
briefing's five coloured chips and its gold furniture are now done as well; see §2.2 and §2.5.

## 1. The two questions, answered

### #7 — "each mission easy/medium/hard, that is different stories?" Yes.

Not three difficulties of one passage. Session 1, read from the shipped JSON:

```
easy    | The Playground           | SOMEBODY = "Molly"
average | The Day the Crayons Quit | SOMEBODY = "Duncan"
hard    | The Animal Assignment    | SOMEBODY = "Jayden"
```

10 sessions x 3 = **30 distinct stories**, each with its own five pages, its own five SWBST
elements, its own hero art and its own narration. Difficulty additionally drives gate spacing
(easy gets the longest runway between parts, hard the shortest — 18.0 s flat at hard). Within a
session the three share a world recipe but shift time of day, so a session feels like one place
across its three runs.

### #4 — the prototype's clock cannot ship, and the reason is the thesis, not taste

The prototype (screens 4 and 5) shows `⏳ 89s` and `x1 MULTIPLIER`: a global race clock, a score
multiplier, and — as the note reads it — fail-and-retry if the clock wins. Three independent
reasons that stays out:

1. **It punishes.** "Never punish the learner" is a project non-negotiable and D7 is locked. A
   time-out fail state is the one thing the race has never had.
2. **It scores reading speed, and it pressures exactly the children the study is about.** The
   measured budget is ~11.6 s of decoding per gate at 100 wpm and ~16.5 s at the 70 wpm a
   struggling Grade-4 reader manages. A clock turns a comprehension measure into a decoding-speed
   measure.
3. **Retry corrupts the instrument.** `raceFirstPickCorrect` *is* the star count and the headline
   logged measure. A learner who retries the same story answers the same five items twice; the
   second attempt is memory, not comprehension, and nothing in the export could separate them.

**But the underlying complaint was right and is fixed** — see §2.4. "Next part in 29s" really did
read as a race clock counting down to nothing.

---

## 2. What changed

### 2.1 The patrol — rebuilt to the owner's own spec (#3)

> "the police patrol appear but look buggy floating and flying to player… subway usually if
> obstacle was hit it appear running at the back of player… same movement and animation of player
> but put it at the back of player, same x-y-z of where the player position, and then move the
> player camera focus a little back so that the patrol has space to see"

**Why it looked wrong.** The cameo shipped the day before swept the cop from 4 m behind the runner
to 26 m ahead over 2 s. That is 30 m of travel in 2 s = **15 m/s on top of the runner's own
10–30 m/s**, played against a run clip at 1.35x. Motion and animation disagreed by roughly a
factor of two, which is exactly what "floating" and "flying" describe — the feet were not driving
him — and most of that 30 m was spent as a dot far up the road. No value of the sweep constants
fixes that, because the mismatch *is* the sweep. A second cause sat underneath it: his ground
height was copied from the runner's transform `y`, which is only correct if the two prefabs share
a pivot convention. They do not.

**What the code said, and why that changed.** `SpawnPatrol` carried a long comment explaining that
a behind-the-runner chaser is geometrically impossible and ended "flip this to true only alongside
a camera change" — because the camera was off-limits. **The owner has now asked for the camera
change in writing**, so it was taken.

**What it is now.** A tail:

* he runs **in the runner's own lane**, a constant `PatrolChaseGap` behind him, recomputed from
  the live player every frame (their track floats its origin ~every 100 m; a follower holding a
  world-space target gets stranded — that is how the first chase died);
* the gap is **held**, so his ground speed is the runner's, so the run clip at **1.0x** drives the
  motion exactly and there is nothing left to skate;
* his feet are placed by **renderer bounds**, not by his pivot — measured once per beat after the
  animator has been forced to pose, then held (measuring per frame chases the stride, because the
  lowest point of a running body rises and falls, and that is jitter);
* the chase camera **dollies back and up** for the beat and returns, which is the "space to see";
* the beat is 2 s → **3 s**, because a tail holds station rather than crossing the frame, and ~0.9 s
  of the old 2 s is the camera easing out and back.

**The gap is 2.5 m, and it is smaller than intuition says.** The camera already sits ~6 m behind
the runner, so "5 m behind the kid" is 1 m in front of the *lens*. He has to run **between** the
camera and the kid. Solved by projecting his eight bounding corners through the scene's own camera
(fov 58.7, local (0, 4, −3), pitch 14.947°), where 1.0 is the edge of frame:

| | centre lane | outer lanes |
|---|---|---|
| cop at gap 4.5, no pullback | 2.877 | — |
| cop at gap 2.5, **no pullback** | 1.304 | 1.555 |
| cop at gap 2.5, **with pullback (0, +0.65, −2.9)** | 0.804 | **0.858** |
| the runner himself, with that pullback | 0.456 | 0.640 |

So the camera move is not a preference; it is the enabling condition. Overlap is impossible by a
wide margin: 2.5 m against the 1.34 m the two bodies need including the 0.79 m the cop's rendered
mass swings off his pivot as the 19-part rigid rig animates. He holds a constant gap, so there is
no closing rate — `timesCaught` stays 0 for every run ever logged (D7/L3).

`PatrolCameoGeometryTests` was rewritten to lock all of this, including a test that asserts the
pull-back is **required** rather than cosmetic, so a future pass that deletes it as decoration
learns what it costs. All five assertions were run offline against the real scene camera and pass.

**Playtest risk to watch:** nothing else in the project writes `Camera.main.transform.localPosition`
during a run, but the dolly and Trash Dash's own camera handling have never been on screen
together. If the framing jitters during a wrong-answer beat, that is where to look.

### 2.2 The SWBST tracker — legible, and one colour language instead of five backgrounds (#1)

Two complaints, one structural cause.

> "The font is too small and I don't like the different color background swbst"

The board is 880 units wide on a 1080 reference because the pause chip needs the rest of that row
to reach Android's 48 dp minimum, which leaves a **140-unit label box**. "SOMEBODY" needs **128 of
those at the 24 pt floor**. The word fit only by being small — on every gate of all thirty races.
No font change could fix that; only removing the word could.

So the plaque now carries its **letter at 40–72 pt** instead of a shrunken word, and the word moved
somewhere it fits: the new question line (§2.3), in a full sentence at sentence size. That is
strictly more than the plaque was saying.

And the five saturated colour blocks are gone. All three states share **one uniform dark wood
plaque**; the element's colour is a slim **accent strip** along its foot:

| state | plaque | strip | label |
|---|---|---|---|
| upcoming | dark wood | dimmed | faded "?" |
| current | dark wood, lifted 12 %, pulses | full colour | white letter |
| collected | **cream, filled** | full colour | letter in the element's ink |

Fill-versus-dark is what separates collected from not, so it does not depend on colour vision
(F49). The palette itself is untouched — it is a teaching device (F18) that also carries the
Arrange slots and the Summary list, and deleting it here would break the one thing tying those
screens together. It simply stops being the background. Collected letters use
`SwbstPalette.InkForIndex`, the variant measured against a cream card (5.18–7.31:1), not `Deep`,
which drops WANTED and SO under AA on exactly that background.

**And the five briefing chips got the same treatment**, which is what the note was actually
pointing at (§0d). At the foot of the mission card sat five fully saturated blocks — blue, green,
red, orange, purple — the busiest thing on the learner's first frame of the race. They are now
identical in language to the tracker slots: dark wood plaque, colour on an accent strip, white
letter. That matters beyond taste: those chips exist so the run's gates are already familiar when
the first one arrives, so a chip that does not look like a tracker slot is teaching the wrong
thing. One shared look, two screens.

**Left alone deliberately:** the in-world lane-selector halo still uses the element's full colour.
It is a functional highlight on the road showing which lane the runner is pointed at, not a panel
background, and it is the one place where the colour is doing a job nothing else can do.

### 2.3 The race now asks the question (#5)

> "the prototype has question in race, who is somebody? please check this as well"

Right, and we had no equivalent: the learner saw a pulsing "B" and three sentences and had to
remember what B stood for **while steering**. That is a working-memory tax on top of the
comprehension task the race exists to measure, and it falls hardest on exactly the readers the
study is about.

The slot questions already existed as `GameText.ReaderSlotHints` ("Who is this story about?",
"What is the problem stopping them?", …) and are already shown in the Reader. They now appear in
the race too, on their own plaque between the tracker and the three options.

**This is safe for validity, and it is checkable rather than asserted.** The line is identical for
all three options and identical across all thirty stories, so it cannot be used to choose an option
without reading one — which is the exact property F44 and F47 spent two passes restoring after the
card-width tell. It names the *slot*, never the answer, and the page text is still gone, so the
support-removal ladder is unchanged. It is kept outside the option board rather than inside it, so
the three columns stay a set of three equal things.

The question is bound to its gate at **arm** time, not read at reveal time: `_pendingElement` and
`_activeElement` swap over as a gate goes live, and the panel is armed once and shown later, so
reading them late would ask the wrong question for one class of gate.

**Cost, stated plainly.** The option board gave up 0.024 of canvas height to make room. Its three
tap columns go from 58 dp to **48 dp** at the squarest supported aspect (4:3) — exactly the Android
minimum, comfortably above it at every phone aspect. The road thirds remain a full-screen
alternative for steering, so the columns were always a convenience rather than the only control.

### 2.4 The gate chip stops looking like a race clock (#4)

Both of the owner's observations were right and they pull opposite ways. The day before, at a 12 s
window, the chip hid for the first half of every gap and read as "no timer at all", so it was made
always-on and bigger. Always-on then meant a number counting down from 29 — and a two-digit number
ticking down is a race clock, which this game does not have.

Both are now true at once. The chip is **always on**, but outside the last 10 seconds it shows the
same plaque with **no number**: *"Next part coming up"*. Inside 10 seconds it counts to the
arrival. Nothing fails at zero; the part simply arrives and the normal pick/miss rules apply.

`GameRules.RaceGateTimerCountdownSeconds` (10) is the lever, and the reasoning is written at the
constant so the next person does not have to rediscover the tension between the two notes.

### 2.5 The race's panels go to wood (#2)

> "instead of yellow border you can make it gray or brown like back button gray or some our brown,
> more like game feel rather than colour yellow… if you can improve this scene or panel please
> improve"

**The briefing card itself.** This is what the callout points at (§0d): a gold-bordered
`Resources UI/panel_gold` card with a baked-yellow kit pill over its top edge. Tinting that sprite
brown was not available — one `Image` cannot darken the border without darkening the cream interior
with it, and the body text on that card is `Theme.TextBrown`, which on a browned interior stops
being readable. So the frame and the face became two graphics: `MakeRacePanel` builds a dark wood
frame (the same wood as the tracker board, the feedback pill, the pause chip and the gate chip)
around a cream inner face, inset 18 px. The title pill is wood too, with cream type at **11.9:1**
— the inverse of the old dark-brown-on-yellow.

**All three race cards, not just that one.** The pause card and the new leave confirmation were on
the same gold panel, and a learner meets all three inside one sitting, so one of them staying gold
would read as a different game. They share the helper, which is also the single place to revert
this if the call is ever revisited. `goldPillSprite` stays serialized and wired rather than being
removed, for the same reason.

**The answer reveal**, separately: after a wrong pick the race showed the correct answer full width
for 2.2 s painted `Theme.Gold` — the loudest thing on the race screen, reading as an alert rather
than as furniture. Now `Theme.TextBrownDeep` with cream type, which keeps the only job the colour
actually had: being plainly distinct from the three white option columns.

**The briefing's type**, the other half of the note. It was small for a structural reason,
not a timid number: the copy had grown to nine lines (it names the reading panel, the three lanes
and the patrol) inside a fixed 380 px box, in a card with visible empty space above and below.
The old box could not simply be made taller — the comment there carried a four-row table proving
380 px clears the chip row and the title pill at every aspect, with **15.6 px** of margin at 4:3.
So the box is now anchored to the **card's own fractions** (0.31–0.90) between its two neighbours,
which cannot overlap at any aspect by construction rather than by arithmetic:

| aspect | card height | old box | new box | 9 lines at |
|---|---|---|---|---|
| 4:3 | 720 | 380 | 425 | ~39 pt (was ~35) |
| 16:10 | 864 | 380 | 510 | ~47 pt |
| 9:16 | 960 | 380 | 566 | 54 pt (ceiling) |
| 20:9 | 1200 | 380 | 708 | 54 pt |

**Deliberately not changed:** the gold banner and title lockup on **Boot, Main Menu, Story Select
and Results**. That gold is the app's brand language on those screens and the note is anchored to a
race panel; swapping it there is a larger call, offered rather than assumed. Say the word and it
becomes slate or wood everywhere.

**Ms. Lumi was checked and left alone.** In the screenshot she looks cropped at the hip in the
bottom-left corner, which looks like a placement bug. Measured instead: `mslumi_wave.png` is
512x256 and her alpha envelope runs x 24.2%–69.1%, y 19.1%–**100%** — she is a half-body asset whose
artwork ends at the image's bottom edge, and she still occupies the middle ~45% of the width that
the F46 clearance derivation assumes. So the framing is correct and the crop is the art. Moving her
means re-deriving the START-button clearance that has already been fixed once for exactly this, and
doing that blind with no Play mode available (§0c) is how that bug came back the first time.

### 2.6 Session stops go dark (#6)

> "I don't like the design colour green square with number, please choose black or dark colours"

The green was not a tint we chose — it is the kit sprite's own paint
(`empty_buttons/green.png`), shown untinted when a session is playable. Darkening it in code was
therefore not available: a multiply toward black over saturated green gives dark *green*, which is
the same complaint one shade down. The ten stops now use the kit's neutral `GREY.png` — same
9-slice border (24/20/24/20), so nothing about the layout moves — tinted dark slate when playable
and darker when locked. Contrast against the white number: **7.3:1** playable, **12.7:1** locked.

Unearned stars were `Theme.Slate`, which on the new dark plate is very nearly the plate itself, so
they were lifted to a muted steel — "how many of the three you finished" is the only progress this
screen shows.

### 2.7 Leaving a race gets a real confirmation (#8)

> "when I click exit button, the back button of the exit panel or confirmation not appear, and make
> sure when leave game make sure it has confirmation"

He is right on both counts. Leaving was a **label swap**: the first tap on the leave chip changed
its text from `LEAVE RACE` to `LEAVE?` and armed a second tap. A word changing inside a small chip
is not a confirmation — nothing on screen says a question was asked — and there was no control
meaning "no", only the unrelated-looking KEEP RUNNING behind it. A nine-year-old who taps once and
hesitates cannot read what state they are in.

It is now a panel with a question and two named ways out: **GO BACK** (large, green, the same CTA
treatment as START) and **YES, LEAVE** (small, quiet). Leaving stays deliberately the small option
— the race never ends by itself and never punishes (D7) — and it is still two taps, so one stray
press still cannot end a run.

The 4-second auto-disarm was **removed** along with it, and its reason went with it: it existed
because the armed state was invisible after a glance and the next stray tap on that same corner
would end the run. There is no stray tap to guard against now, and a panel that closes itself
while a child is still reading the question is worse than one that waits.
`GameRules.RaceLeaveConfirmSeconds` is left in place, marked as no longer read.

**"make sure when leave game make sure it has confirmation" — every other exit was audited.**
All 30 `SceneLoader.Go` call sites, and a search for `Application.Quit`:

* **`Application.Quit` appears nowhere in the project**, and Android BACK is swallowed app-wide by
  `Core/BackButtonGuard` (F51), so "leaving the game" cannot mean quitting the app. There is no
  unconfirmed way out of SummaRace.
* **The Reader's back** is the only other exit where a learner could lose work, and it is already
  guarded twice over: it is *hidden entirely* once the first question has been answered
  (`mayLeave`), because after that the run is study data, and before that it is two-tap. Left as
  it is on purpose — a modal there would put a dialog on a screen where leaving costs nothing.
* Every remaining exit (Session Map, Story Select, Teacher Menu, Name Entry backs; Arrange, Summary
  and Results moving forward) either loses nothing or is the "never a dead end" rescue path, which
  must not stop to ask a question.

### 2.8 The finish dances (#9)

> "if game finish at least add animation, instead of pausing all character movement, make sure the
> player dance, we have dance asset there if needed"

`FinishRoutine` carried the comment *"no dance — idle is enough"*, which was itself an owner steer
(F42d) and is now steered back. He is right that a runner who simply stops is the weakest moment in
the loop — it is the one place the game says "well done" and it said it with a character standing
still.

`Art/Characters/Animations/HipHopDancing.fbx` was already in the project, is a Mixamo humanoid clip
(`loopTime: 1`), and retargets onto Aj for free — both are `mixamorig` skeletons.

**How, and why not a new Animator state.** `KidCharacterAnimation` is a 1:1 clone of Trash Dash's
own graph, kept structurally identical because *their* code drives its parameters. Adding a Dance
state means editing that graph — the one asset in the race where diverging from their expectations
shows up as a character stuck in a pose. So the clip is swapped into the state the finish already
plays. `"Start"` is their idle and it is an **orphan with no outbound transitions** (measured in
F40e), so once we `Play()` it nothing can move the character off it — the safest possible host for
a one-shot celebration. An `AnimatorOverrideController` re-points that one state and touches
nothing else, and it is applied only at the finish, so the pre-race idle hold is unaffected.

The victory beat is 2.2 s → **3.2 s**, because a celebration cut off mid-move reads as a bug rather
than as a reward.

The clip is a **serialized reference**, wired in `MainSummaRace.unity`. Not a name lookup: an asset
reached only by name is not a build dependency, so it resolves in the Editor and returns null on
the tablet — the failure this project has already been bitten by twice (F54's SkyTint,
`RaceController`'s three surviving `Shader.Find` calls), and by construction invisible until the
APK is on a device. Null is the designed fallback: the finish then holds idle exactly as before.

Because the Editor cannot load our scripts (§0b), opening `MainSummaRace.unity` to wire this in the
Inspector would have risked losing the director's serialized fields on save. The reference was
written directly into the scene's serialized data instead — two lines, verified as the only change
to that file.

---

## 3. Deliberately not done

| | |
|---|---|
| Global race clock + multiplier + fail/retry | §1. Punishes, scores reading speed, and a retry corrupts the study's headline measure. Owner's call if he wants it anyway — it is not a small change. |
| Repainting the gold brand lockups on Boot / Main Menu / Story Select / Results | The note is anchored to a race panel, and the race's three cards are done. Those four are the app's brand. Offered, not assumed. |
| Re-framing Ms. Lumi on the briefing | Verified as a half-body asset, not a placement bug (§2.5); moving her means re-deriving a clearance that has already regressed once, with no Play mode to check it. |
| A permanent camera pull-back | The surge-only dolly gives the patrol its room without re-framing all thirty races. Say the word and it becomes permanent. |
| Fixing the URP package split or the Adjoint/MCP collision | §0b. Both are environment state on the owner's machine; one of them requires choosing which editor plugin to keep. |

**One note I answered one way and could have read another.** *"the prototype has question in race,
who is somebody? I think the popup is based on selection, please check this as well, what is the
real main points"* — read as "the prototype's question popup changes with the SWBST slot being
collected", which is what §2.3 implements. If "the real main points" instead meant the story's
`mainIdea` field (which today only the Summary screen uses), say so and it is a separate, small
piece of work.

**One loose end worth naming.** `GameText.RaceBriefingPatrol` now reads *"If you miss a part, the
patrol runs up behind you. It never catches you!"* — accurate for the tail. The recorded clip
`VoRaceBriefingPatrol` still says *"races past"*, which was cut against the overtake. One phrase of
spoken narration is out of date; the re-cut command is written at the constant.

---

## 4. Still open from before this pass

Unchanged and unaffected:

* **owner full-loop playtest on a non-s01 story** — 27 of 30 have never been run end to end;
* **prove the USB export path on a real tablet before day 1** — the logs are the entire dataset and
  `persistentDataPath` is usually hidden from MTP on Android 11+;
* **set a teacher PIN and a participant code per learner during install** — the code is the join key
  to the paper pretest/posttest and a run logged without one cannot be joined afterwards;
* **draw calls in the race are still unmeasured on device** — `GameRules.RaceMaxSceneryPerSegment`
  (14) is the first number to halve if the race misses 30 fps.
