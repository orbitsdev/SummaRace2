# Where we are, and what to test

**2026-08-22, 08:06.** Tick as you go. Everything in §2 is written and compiles; **none of it has
been seen running.**

---

## 0. CAN I TEST NOW? — not yet. One line stands in the way.

| | |
|---|---|
| Unity compiles our game code | ✅ `Assembly-CSharp.dll`, 394,752 bytes, 08:06 |
| Unity compiles the editor code | ❌ `Assembly-CSharp-Editor.dll` is **not produced** |
| Our scripts load in the Editor | ❌ still unloaded — Unity will not finish a domain reload while any assembly errors |
| Play mode / portrait renders / EditMode tests / Build Preflight | ❌ all blocked by the above |

### The one edit — `Packages/manifest.json`, delete line 4

```json
"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main",
```

Line 3 already ends in a comma, so the JSON stays valid. `git checkout Packages/manifest.json`
undoes it instantly.

**Why that one:** `Adjoint.Editor.dll` contains **572 `MCPForUnity` references**, including the
colliding `MCPForUnity.Editor.Helpers.AssetPathUtility`. Adjoint ships MCP-for-Unity bundled
inside it — you have the same tool installed twice, and two assemblies exporting one type name is
the entire error:

```
AdjointToolbarButton.cs(156,35): error CS0433:
'AssetPathUtility' exists in both 'Adjoint.Editor' and 'MCPForUnity.Editor'
```

Confirmed against Unity's own unmodified response file with **zero missing references**: that is
the *only* error in the project.

**Already fixed and no longer a blocker:** the URP package split. `com.unity.render-pipelines.core`
is now extracted and URP compiles. That one is done — ignore older notes saying otherwise.

### After you delete the line

Unity recompiles (~1 min). Then **I** run, in order:

1. EditMode suite — including the five patrol-geometry assertions so far only checked in Python
2. Portrait renders at 720×1280 of every changed screen
3. Play mode through a full race
4. `SummaRace ▸ Build Preflight`

---

## 1. Two things only you can do (they need a tablet)

- [ ] **Drag `Art/Characters/Animations/HipHopDancing2`** onto the race director's
      **Finish Dance Clip Alt** slot — one drag, gives the finish a second dance. Safe to skip.
- [ ] **Device smoke test.** Two things are *impossible* to verify in the Editor:
      Android BACK (`Application.wantsToQuit` never fires on Play-mode exit) and real touch
      steering.

---

## 2. What to look at, screen by screen

### Race — briefing
- [ ] Body text is bigger and fills the card (was 9 lines squashed into a fixed box)
- [ ] Card is **dark wood with a cream title**, not gold-bordered with a yellow pill
- [ ] The five S/W/B/S/T chips are **dark plaques with a colour strip**, not five solid colour blocks
- [ ] Ms. Lumi and her bubble are not clipped by START

### Race — countdown
- [ ] **First race on a fresh device only:** three ghosted lane zones + a hand moving
      left → centre → right, with *"Tap a side of the screen to move there."*
- [ ] It disappears on GO!
- [ ] It never blocks a tap (everything on it is non-interactive by construction)

### Race — running
- [ ] **The question line** — *"Who is this story about?"* etc., above the three options
- [ ] Tracker letters are **large** on uniform dark plaques; the colour is a strip at the foot
- [ ] Collected slot = **cream filled**; current = dark + pulse; upcoming = "?"
- [ ] Chip reads *"Next part coming up"* far out, switches to *"Next part in Ns"* inside 10s
- [ ] **Pacing** — easy should feel much less empty. Measured: dead running 47s → 25s, reading
      12s → 16.2s per gate, same race length
- [ ] Wrong pick: reveal panel is **dark wood with cream text**, not a yellow slab

### Race — the patrol (the big one)
- [ ] Appears **behind you, in your lane**, on a wrong answer
- [ ] **Feet on the road** — not floating, not sinking
- [ ] Runs at your speed — feet drive the motion, no skating
- [ ] Camera eases back while he's there, and returns after
- [ ] He never catches you

### Race — pause / leave
- [ ] Pause chip opens the pause screen
- [ ] LEAVE RACE opens a **real panel**: *"Leave this race?"* with **GO BACK** big and
      **YES, LEAVE** small (it used to be a label swap with no cancel)
- [ ] GO BACK returns you to the pause screen

### Race — finish
- [ ] The runner **dances** (random start point, so two finishes don't look identical)
- [ ] **Three staggered firework bursts**
- [ ] A card reads your five collected parts back, **one at a time**, in S‑W‑B‑S‑T order
- [ ] The beat lasts ~3.2s and nothing is cut off

### Android BACK — device only
- [ ] Session Map / Story Select / Teacher Menu → *"Go back to …?"* with **STAY HERE** big
- [ ] Reader **before** the first answer → asks; **after** → *"Keep reading — you're nearly there!"*
- [ ] Arrange / Summary / Name Entry → blocked, warmly
- [ ] Race → opens the pause screen (not a second dialog)
- [ ] **A second BACK press closes the dialog** (means "no")
- [ ] BACK never closes the app, anywhere

### Other screens
- [ ] **Session Map** — the ten stops are **dark slate**, not green; stars still readable
- [ ] Title banners on Session Map / Story Select / Results / Name Entry / Teacher Menu are
      **dark with cream text**, not yellow
- [ ] **Name Entry** now says *"Pick your badge"* (stopgap — see §4)
- [ ] **Arrange** — long story parts shrink to fit instead of spilling over the neighbouring pill
- [ ] **Teacher Menu** — the export path fits on screen
- [ ] **Race praise** — *"That belongs in the summary."* appears on correct picks

---

## 3. Answers you asked for (no testing needed)

- [x] **10 missions × 3 stories = 30**, and they are three *different* stories per mission
- [x] ~~**The prototype's timer is a survival time-bank, not a reading deadline** — proven from two
      of your own screenshots 11s apart where it went **65s → 84s**. Correct picks *add* time,
      scaled by the multiplier. It stays out because the currency is time, so a slow reader bleeds
      out for reading slowly~~
      ⚠️ **WRONG, corrected 2026-08-22 — do not quote this.** The two frames are `080113` (65s)
      and `080124` (84s), and **`080116` sits between them: a second race briefing screen.** That
      is a restart, not time being added. Re-derived across all 29 screenshots using the real
      timestamps in their filenames: within a run the clock only ever **falls**, and it falls
      *through* collections — `c.png → 080056` is 5.4 real seconds against 89s → 83s while the
      question changes and the multiplier goes x1 → x2. It is a plain global race clock. The
      multiplier is separately `1 + parts collected`, with no score anywhere to multiply.
      The conclusion "it stays out" is unchanged and still right, but the reason above is not one
      of the reasons: the real ones are that a clock **punishes** (D7 is locked), that it
      **scores reading speed** rather than comprehension, and that the retry it implies would let
      a child answer the same five gates twice and destroy `raceFirstPickCorrect`.
- [x] **Why the patrol floated** — two causes: motion/animation disagreed by ~2× (30m sweep in 2s
      = 15 m/s on top of your speed), and his height came from the *kid's pivot* rather than the
      ground, on a rig whose rendered mass swings 0.79m off its own transform. Fixed by holding a
      constant gap and placing his feet from renderer bounds

---

## 4. Deliberately not built — decisions waiting on you

| | |
|---|---|
| **3D character select** | Specced in `SummaRace_Character_Select_Spec.md`. It is a *lighting* feature; built blind the likely result is a black rectangle nobody sees until it is in front of a child. ~1 hour once the Editor reloads |
| **No girl character exists** | Only two boys are in the project, one of them the patrol. Mixamo fixes it in an afternoon, zero code |
| **Avatar → race runner** | `avatarIndex` is written and read by nothing. Do **not** wire it before the study: that prefab is on the critical path of the measured stage and fails Addressables-shaped — perfect in the Editor, dead on the tablet |
| **Coloured lane stripes** | The biggest cheap step toward the playground look. ~40 lines |
| **Full-screen red on a wrong answer** | The prototype's version is stronger than our edge vignette and cheaper |
| **The patrol dances at the finish too** | Funny, on-brand (he never catches you, so he joins the party). Slightly muddies "cop = wrong answer" |

---

## 5. Still open from before all this

- [ ] Full-loop playtest on a **non-s01 story** — 27 of 30 have never been run end to end
- [ ] Prove the **USB export path** on a real tablet before day 1 — the logs are the whole dataset
- [ ] Set a **teacher PIN** and a **participant code per learner** during install
- [ ] Race **draw calls unmeasured on device** — halve `RaceMaxSceneryPerSegment` (14) if it
      misses 30fps

---

## 6. Repo state

23 files changed, ~1,800 lines. Six documents. **Nothing committed** — say the word.

`Assembly-CSharp` 394,752 bytes, 0 errors. `Assembly-CSharp-Editor` 227,328 bytes, 0 errors when
the Adjoint collision is excluded.
