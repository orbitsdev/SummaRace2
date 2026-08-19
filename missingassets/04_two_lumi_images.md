> # ⛔ SUPERSEDED 2026-08-19 — do not generate these
>
> Both images were **derived from poses you already have** instead of generated, by
> `missingassets/derive_lumi.py`. Ms. Lumi is now one character on every screen. Nothing here
> needs doing. Kept only as the record of the specs, in case the art is ever redone by hand.

---

# The 2 images that make Ms. Lumi one person

**~20 minutes. Do these before the 27 hero images — they touch four screens.**

Right now she is three different women (Reader = new pool, race briefing = old flat-vector,
Arrange/Summary = a high-bun woman in a different style). Both offending screens already have
layout maths tuned to their current file's exact geometry, so **replacing the two files is safer
and faster than changing code.**

Generate both in the **same chat as your 22 poses**, so the face matches. Upload one of your new
poses as the reference image first.

---

## Image 1 — `mslumi_wave.png` (the race briefing)

**Overwrite:** `Assets\_Game\Resources\UI\mslumi_wave.png` — keep the filename, keep the `.png.meta`.

| | |
|---|---|
| Canvas | **2048 × 1024** exactly (2:1 landscape) |
| Format | PNG, **transparent** background |
| Figure | half-body, waving, **bottom-aligned** to the canvas floor |
| Placement | figure occupies the **middle ~44% of the width** (roughly x 510 → 1400) and ~95% of the height |

> ⚠️ **The placement rule is not cosmetic.** `EndlessRaceDirector` sizes her box assuming the
> figure sits in the middle 44% with transparent padding either side. A tight crop makes her
> render at the wrong size in the wrong corner. Generate at any size, then **pad to 2048×1024 in
> Photoshop** — do not ask the model for the padding.

```
Half-body cartoon illustration of a friendly Filipino woman primary-school teacher,
same character as the reference image: low soft bun, warm brown hair, mint-green
long-sleeve top, warm brown eyes, kind smile. She is waving hello with one hand
raised. Facing the viewer. Glossy 2D storybook style, bold clean outlines, soft warm
shading, bright cheerful colours, children's mobile game art. Full transparent
background. No text, no watermark, no background scenery.
```

---

## Image 2 — `teacher_temp.png` (Arrange + Summary badge)

**Overwrite:** `Assets\_Game\Art\UI\teacher_temp.png` — keep the filename, keep the `.png.meta`.

| | |
|---|---|
| Canvas | **512 × 512** square (the current file is 290×290; larger is fine, it is scaled) |
| Format | PNG, **transparent corners** |
| Composition | head-and-shoulders portrait inside a **white circle with a green ring border** |

> The file it replaces is a **fully opaque green square** (decoded: corner alpha 255), so on
> Arrange and Summary it currently draws a green patch over the real background. Making the
> corners transparent is strictly better and needs no code change — it is a plain `Image` with
> `preserveAspect`, no mask.

```
Circular badge portrait of a friendly Filipino woman primary-school teacher, same
character as the reference image: low soft bun, warm brown hair, mint-green top,
warm brown eyes, kind smile, facing slightly to one side. Head and shoulders only,
filling a white circle with a clean green ring border around it. Glossy 2D storybook
style, bold clean outlines, soft warm shading, children's mobile game art. Fully
transparent outside the circle. No text, no watermark.
```

---

## After both

1. Save over the two paths above (do **not** put these in `dropin/` — the installer only handles
   `Stories_Art/` and `Resources/UI/`, and `teacher_temp.png` lives outside both).
2. In Unity, run **`SummaRace ▸ Normalize Lumi Poses`** if you have not already.
3. Check Arrange and Summary in a **portrait** render — the Editor game view is landscape and will
   not show you what the learner sees.
