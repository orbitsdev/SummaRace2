# SummaRace — Missing Assets working folder

**Created 2026-08-19. This is the folder to sit in while generating art.**
Everything that is still outstanding is here; nothing else in the repo needs to be open.

---

## The honest state, measured on disk today

| Item | Status |
|---|---|
| Audio — 31 sound effects + 150 narration clips | ✅ **complete, commission nothing** |
| Ms. Lumi — 23 poses, all screens | ✅ **done 2026-08-19.** Poses normalized; the 3 legacy images rebuilt from those poses so she is one character everywhere. No new art needed — see §"Ms. Lumi" below |
| **27 story hero images** | ✅ **DONE 2026-08-19.** All 27 generated, verified subject-by-subject, cropped to 3:2 and installed. 30/30 story cards now show real art |
| `map_path.png`, `sparkle_soft.png` | ❌ two small sprites, low value |
| Backgrounds | ✅ **DONE 2026-08-19.** `bg_journey` (SessionMap + StorySelect) and `bg_reading` (Reader + Arrange + Summary) generated and wired; 7 screens sharing one image is now 3 images across 7 |
| Race track segment art | ⚠️ see §"Race track" below — **do not commission** |

**Nothing here blocks the build.** Every path already resolves to *something*. This folder is
"make it look finished", not "make it work". The two things that actually block shipping are the
Android module and an Addressables content build — see `Documentation/SummaRace_Owner_Handover.md` §2.

---

## Work in this order

### 1. ~~The 27 hero images~~ — DONE 2026-08-19
All 27 generated, installed and verified. `HERO_PROMPTS.md` is kept as the record of what was
asked for. **Two things were caught on the way in, worth knowing if art is ever redone:**

- **#19 and #20 came back swapped** — the skateboard was generated where the icebox belonged.
  Filenames were assigned from generation ORDER, so this would have put the wrong picture on two
  story cards silently. Every one of the 27 was checked against its story before install; only
  that pair was wrong.
- **They arrived 16:9 (1672x941), and the card forces 3:2.** The `Hero` Image has an
  **AspectRatioFitter, mode EnvelopeParent, ratio 1.5** and `preserveAspect` is **off**, so a 16:9
  source is squashed ~16% horizontally — faces go narrow. Each was cropped to 1412x941 from the
  right, which the composition allows because the prompts deliberately keep the right side empty
  for the title (25 of 27 had 280-570px of clear margin; the two "tight" ones were checked by eye).
  **Any replacement art must be 3:2, or cropped to it before install.**

- Size **1536 × 1024** (3:2 landscape), PNG, opaque, **no text in the image**
- Characters on the **left and centre** — the card's title sits on the right
- Do all 27 in **one chat session, in order**, so the style does not drift
- ⚠️ `s05_hard` and `s06_hard` are the same story (*In Grandfather's Day*). They need **two
  different pictures**, not one file used twice.

Save each one into **`dropin/Stories_Art/`** using the exact story id as the filename
(`s02_easy.png`, `s02_average.png`, …). Then run the installer below.

### 2. The two small sprites — 10 minutes, optional
See `02_whats_actually_missing.md` §3. Save into `dropin/UI/`.

### 3. Ms. Lumi — already done
`03_ms_lumi_DONE_reference.md` is kept only as the style reference for her look, and because its
global-rules block is what you should paste at the top of the hero-image chat too. **Its §0
"drop-in vs needs-wiring" table is stale** — the wiring exists now and all 22 poses are live.

---

## Installing what you generate

Drop files into `dropin/` mirroring their destination, then from the repo root:

```powershell
powershell -ExecutionPolicy Bypass -File missingassets/install.ps1
```

It copies each file over the existing one in `Assets/`, **never touches the `.png.meta`**, and
refuses to install a file whose name does not already exist in the project (a typo'd filename is
the one failure mode that silently produces a card with no picture).

### The two rules that matter more than the art
1. **Overwrite the existing filename.** The `.png.meta` beside it holds the GUID and already says
   *Texture Type = Sprite*. Delete it and the image will not draw.
2. **Never add a new filename.** The story JSON asks for `s02_easy.png` by name.

---

## Race track — read before commissioning any 3D art

The race world is Trash Dash's segment system: **3 zone families** (Industrial 3 prefabs,
Suburbs 4, Urban 14 = 21 street pieces) in **2 themes** (Day, NightTime).

This is a **variety** limit, not a missing-asset limit, and it has been attacked three times in
code already (F48 place, F49 family mix, F57 per-difficulty variation). What is left is that
three of the ten worlds — `bright_park`, `golden_fields`, `autumn_lane` — are **dressed streets
pretending to be a park, a field and a country lane**, because neither theme contains any of
those. Fixing that properly means new 3D segment prefabs, which is Addressables + RAM + a device
frame-rate risk on a 2GB tablet, two of which have never been measured.

**Recommendation: do not buy or generate 3D track art for this study.** The cheap honest fix is
renaming those three worlds to what the art actually is. See the conversation summary or ask.


---

## Ms. Lumi — closed 2026-08-19

She was **three different women** depending on the screen. She is now one, and **no new art was
generated** — all three legacy files were rebuilt from poses already in `Resources/UI/Lumi/`.

| Screen | Before | Now |
|---|---|---|
| Reader | the 23-pose pool | unchanged ✅ |
| Race briefing | old flat-vector Lumi | rebuilt from `idle_wave` ✅ |
| Arrange + Summary | a high-bun woman, different style | rebuilt from `idle_smile` ✅ |

**Done in this pass:**

1. **`SummaRace ▸ Normalize Lumi Poses` was run.** All **23** poses now carry
   `alignment: 9` (Custom), a measured head-centre pivot and `pixelsPerUnit` = head width
   (99–146 px). Before this every meta was at import defaults, so her head jumped and her size
   swung ~±50% between poses. Verified on disk against an independent re-implementation of the
   measuring algorithm — every pivot and ppu agreed.
2. **The three legacy images were rebuilt** by `derive_lumi.py`:
   - `Resources/UI/mslumi_wave.png` — 2048×1024 → **512×256**, from `idle_wave`
   - `Resources/UI/mslumi_cheer.png` — 2048×1024 → **512×256**, from `cheer_hooray`
   - `Art/UI/teacher_temp.png` — 290×290 opaque → **512×512 with transparent corners**, from
     `idle_smile`, in a white disc with a `SwbstPalette.Wanted` green ring

   No `.png.meta` was touched (md5-verified before and after).

### Also added: a badge pool, so she reacts on Arrange and Summary

Those two screens showed her as a **static** circular portrait. `MsLumiReactor` has carried
`reactToArrangeVerified` / `reactToSummarySubmitted` switches since it was written, but only
`Reader.unity` ever had the component, so both switches were dead code.

- **14 disc-cropped badges** derived from the poses into `Resources/UI/Lumi/`, in three new pools:
  `badge_*` (9 resting), `badgecheer_*` (3 celebration), `badgemood_*` (2, never drawn at random).
  Pools come from the filename prefix, so this needed **no change** to `LumiExpressions`.
- `MsLumiReactor.AttachBadge()` called from `ArrangeController` / `SummaryController`.

**9 of the 23 poses were deliberately left out of the badge pool:**

| Excluded | Why |
|---|---|
| `lumi_sad`, `lumi_stern`, `lumi_worried`, `lumi_arms_crossed` | **D7 — never punish the learner.** A disappointed teacher is the one thing you must never show a struggling nine-year-old in a study about reading confidence. These four should stay unused game-wide. |
| `cheer_hooray`, `idle_wave`, `lumi_shrug` | raised arms — the silhouette is wider than tall and crops badly in a circle |
| `idle_profile`, `cheer_proud` | the torso-extension row is hair or hands, not shirt, and tiled into vertical streaks. A flatness check now rejects this automatically rather than shipping the smear |

`headPixels` stays 0 on the badge reactor, which is what makes it safe: `ApplyPose` then swaps the
sprite and returns **without touching the RectTransform**, so each scene's own framing is preserved.

⚠️ **Compiles clean, has never run.** Verified with Unity's own Roslyn against the project response
file (0 `error CS`; `AttachBadge`, `PoolBadgeIdle`, `PoolBadgeCheer` all present in the built
assembly). That proves compilation only — nothing about how it looks or whether the event fires.

### Two things worth knowing if you ever redo this

**The half-bodies are fitted to the original's alpha ENVELOPE, not to a matching head size.**
Head-matching was tried first: it put her silhouette at 49% of the canvas where the briefing's
tuned anchors assume 44%, which on screen **overlapped the START button**. The envelope is what
the layout actually depends on. `derive_lumi.py` hardcodes the measured originals
(`ENV_WAVE`, `ENV_CHEER`) rather than reading the file it overwrites — reading it would make each
re-run drift.

**Canvas dropped from 2048 to 512 on purpose.** The poses are 162–345 px, so a 2048 canvas meant a
4× upscale of content that renders at ~547 px on screen — blur with no added detail. At 512 the
scale factors are 0.93 / 0.95 / 1.72, i.e. essentially native. The aspect stays 2:1 so
`preserveAspect` behaves identically.

⚠️ **Still unverified:** this has not been seen in Play mode. Check Arrange, Summary and the race
briefing in a **portrait** render — the Editor game view is landscape and will not show you what
the learner sees.

## Never produced — but each already has a working substitute

None of these is a blank or a broken path. Listed so nobody generates them twice.

| Item | Status |
|---|---|
| 4 learner avatars `Resources/UI/avatar_0..3.png` | Name Entry uses kit heart / star / gem / lightning icons instead. Fine. |
| `sfx_unlock` | TeacherMenu substitutes `sfx_star`. Teacher-facing only, no learner hears it. |
| 5 SWBST pickup icons | **Obsolete** — the design moved to the F40 tracker plaques. Do not make these. |
| Rain streak texture + rain loop | Weather was never built. Skip. |
| `map_path.png`, `sparkle_soft.png` | Genuinely absent, low value — see `CHECKLIST.md`. |

## Verified complete — commission nothing

- **Audio: 31 / 31.** Every `AudioKeys` constant maps to a file in `Resources/Audio/`, zero
  missing, zero orphaned files. (CLAUDE.md still says "21/21" — that is stale.)
- **Narration: 150 / 150**, as `.mp3` in `Resources/Stories/Narration/`.
- **Hero image paths: 30 / 30 resolve** — 3 are real art, 27 are blank gradients.

## Dead art, not a placeholder

`Art/UI/bg_playground.png` (158 KB) is referenced by **no scene and no prefab** — its GUID appears
nowhere. CLAUDE.md's F13 row says it backs Reader / Arrange / Summary; those screens actually use
`bg_storyselect.png`. Nothing to generate; the doc is just out of date.
