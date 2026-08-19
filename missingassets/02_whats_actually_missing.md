# SummaRace — Missing Assets

Verified 2026-08-19 on disk, against the code that loads each path, and against a portrait render
of every screen. **Nothing here blocks the build** — every path already resolves to something.

## Rules
1. Overwrite the existing filename. **Never delete the `.png.meta`** beside it — it holds the GUID.
2. New files must import as **Sprite (2D and UI)**, not Texture, or a UI image silently will not draw.
3. **Audio is complete.** Commission none. All 31 AudioKeys resolve, plus 150 narration clips.

## 1. Hero art x27 — biggest visible gap

Folder `Assets\_Game\Resources\Stories\Art\` · **1536 x 1024** (3:2) · PNG RGB · import as Sprite.
Loaded by `StorySelectController.cs:137` via each story JSON heroImage field.

**Which files:** every combination of `s02..s10` with `_easy`, `_average`, `_hard` — 27 in total.
Leave the three `s01_*` alone, they are the real art. Sanity check by size: about 1.8 MB is real,
29-80 KB is a placeholder to replace.

**Style:** warm glossy storybook, children as the subject, must read at thumbnail size on a card.
Match `s01_easy.png` and `Art\UI\teacher_temp.png`.

**Subjects and 27 ready-to-paste prompts:** `SummaRace_Asset_Shopping_List.md` section 2. Ignore its
size note of 1024px — use 1536 x 1024.

WARNING: `s05_hard` and `s06_hard` are the same story (In Grandfather's Day, Days 5 and 6 of the
source document). They need **two different illustrations**, not one shared file.

## 2. Ms. Lumi — there are currently TWO different teachers

| File | Size | What it is |
|---|---|---|
| `Art\MsLumi\mslumi_1..3.png` | 2816 x 1536 | source art, ponytail woman, not loadable at runtime |
| `Resources\UI\mslumi_wave.png` | 2048 x 1024 | shipped — Reader idle + race briefing |
| `Resources\UI\mslumi_cheer.png` | 2048 x 1024 | shipped — Reader cheer |
| `Art\UI\teacher_temp.png` | 290 x 290 | shipped — Arrange + Summary badge, **high bun, a different person** |

`teacher_temp.png` is the canonical look. Redraw the half-body poses as that character.

**Many expressions are now supported.** Drop PNGs into `Assets\_Game\Resources\UI\Lumi\` named
`idle_*.png` and `cheer_*.png`. Count does not matter; no code or scene change is needed. Selection
is a shuffle bag, so every pose plays once before any repeat. Empty folder falls back to the two
serialized sprites, so nothing breaks.

**Padding rule for 2048 x 1024 poses — not cosmetic.** Figure **976 px tall**, bottom-aligned at
y = 1024, head-and-torso centred at **x = 1000** (plus or minus 60 px is invisible). A tight crop
renders her thumbnail-sized in the wrong corner.

Note: the expression pool drives the **Reader only**. Arrange, Summary and the race briefing still
use single static images.

## 3. Two small, useful

- `Resources\UI\map_path.png` — about 64 x 256, 9-sliceable or dotted trail, alpha. The ten mission
  stops were a snaking route with no path drawn, so numbers read as scrambled; they are in plain
  reading order now as a workaround. This art brings the journey layout back.
- `Resources\UI\sparkle_soft.png` — 128 x 128, alpha. Story Select's EASY card decorates with
  `star golden.png`, the same sprite as the score stars, so it shows six gold stars: three
  meaningful, three not.

## 4. Do NOT make these — verified to have no consumer

- **Learner avatars x4** — already pack-solved. `NameEntryController.cs:22` drives four kit sprites
  (heart, star, gem, lightning), tint-only selection, differing by shape AND colour deliberately.
- **SWBST pickup icons x5** — that design no longer ships. The race uses coloured cards and tracker
  plaques from `SwbstPalette.cs`. Zero code references.
- **Rain / weather art** — there is no weather system. No `Art\FX\` folder, no script mentions it.
- **Any audio** — complete. sfx_unlock already substitutes sfx_star at
  `TeacherMenuController.cs:579`. For anything new use `Assets\Audio\Kenney\`.

## 5. Checking your work

Sort `Assets\_Game\Resources\Stories\Art\` by size: about 1.8 MB real, 29-80 KB placeholder.

Unity menu SummaRace then Capture Portrait Screen renders the open scene to `Captures\<scene>.png`
at true 1080x1920. Existing renders: Reader, StorySelect, Arrange, Summary, Results, SessionMap.
The two different Ms. Lumis are obvious side by side in `Captures\Reader.png` versus
`Captures\Arrange.png`.
