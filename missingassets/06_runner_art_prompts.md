# Runner art — prompts to generate (Name Entry character cards)

**Open this file while generating.** 4 required images + 2 optional. Paste each prompt as-is.

The game already works without these (it uses 3D renders of the runners). Dropping these files in
makes the Name Entry screen look like a real character-select screen, and matches the painted style
of the 30 story pictures.

---

## Before you paste anything

| | |
|---|---|
| **Tool** | ChatGPT (image) or Gemini — use **one chat for all 4**, in order, so the style matches |
| **Size** | **640 × 840** portrait (ask for "portrait 3:4, tall") |
| **Background** | **Transparent PNG.** If the tool can't, ask for a **plain flat pure-green (#00FF00) background** and remove it in Photoshop (Select ▸ Color Range ▸ Delete) |
| **Framing** | Full body, head to shoes, feet near the bottom edge, small margin all round |
| **Text in image** | **NONE** — no names, letters, logos or watermark (names are added by the game) |
| **Save as** | the exact filename in each heading, into `missingassets/dropin/Runners/` |

⚠️ **They must look like the runners in the race.** The race uses 3D models that can't be changed by
an image, so each prompt describes that runner's exact outfit. A picture of a different-looking
child would promise the learner a character they will not get.

---

## Style DNA (already inside every prompt — don't change it)

> Bright 2D children's-book illustration, soft cel shading, clean dark outlines, warm saturated
> colours, friendly rounded proportions (slightly large head, big expressive eyes), Filipino
> Grade-4 child, same painted look as a cheerful mobile game for kids. No text.

---

## 1. `runner_0.png` — Jay, standing (idle)

```
Bright 2D children's-book illustration, soft cel shading, clean dark outlines, warm saturated colours, friendly rounded proportions with a slightly large head and big expressive eyes. A cheerful 9-year-old Filipino boy named Jay, full body, standing relaxed and confident, facing three-quarters toward the viewer, small friendly smile. Outfit exactly: red-orange baseball cap worn BACKWARDS, black round glasses, short dark hair, grey zip-up hoodie over an orange t-shirt, a dark backpack with straps over both shoulders, dark grey jeans slightly worn at the knees, grey-and-white sneakers. Tall portrait 3:4, 640x840, full body with feet near the bottom, small margin. Transparent background (or plain flat pure green #00FF00). No text, no logo, no watermark.
```

## 2. `runner_0_cheer.png` — Jay, cheering

```
Same boy, same art style and exact same outfit as the previous image (red-orange backwards cap, black round glasses, grey hoodie over orange t-shirt, dark backpack, dark grey jeans, grey-and-white sneakers). Full body, jumping for joy: both feet slightly off the ground, one fist raised high, big open happy smile, eyes bright. Tall portrait 3:4, 640x840, whole body inside the frame. Transparent background (or plain flat pure green #00FF00). No text.
```

## 3. `runner_1.png` — Mia, standing (idle)

```
Same 2D children's-book art style as the boy above (soft cel shading, clean dark outlines, warm saturated colours, slightly large head, big expressive eyes). A cheerful 9-year-old Filipino girl named Mia, full body, standing relaxed and confident, facing three-quarters toward the viewer, friendly smile, warm natural skin tone. Outfit exactly: a magenta-pink bike helmet, short lavender-purple hair with a small side tuft, a white cropped t-shirt with pink sleeves and a round pink bear-face print on the chest, loose lavender cargo pants rolled up at the ankles with a small yellow patch on one leg, pink socks, pastel yellow-and-pink sneakers. Tall portrait 3:4, 640x840, full body with feet near the bottom, small margin. Transparent background (or plain flat pure green #00FF00). No text, no logo, no watermark.
```

## 4. `runner_1_cheer.png` — Mia, cheering

```
Same girl, same art style and exact same outfit as the previous image (magenta-pink helmet, short lavender hair, white crop t-shirt with pink sleeves and pink bear-face print, lavender rolled-up cargo pants with yellow patch, pink socks, yellow-and-pink sneakers). Full body, jumping for joy: both feet slightly off the ground, both arms up in a victory pose, big open happy smile. Tall portrait 3:4, 640x840, whole body inside the frame. Transparent background (or plain flat pure green #00FF00). No text.
```

---

## Optional — nice to have

### 5. `runner_stage.png` — the backdrop inside each card

```
Bright 2D children's-book illustration background, soft cel shading, warm saturated colours. A small sunny outdoor "stage" for a character select screen: soft blue sky with two small fluffy clouds at the top, a gentle green grassy hill at the bottom with a round flat patch of light grass in the centre where a character would stand, a few tiny flowers. Nothing in the middle — empty space for a character. Tall portrait 3:4, 640x840. No characters, no text.
```

### 6. `runner_shadow.png` — only if the stage above doesn't include a ground patch

Skip unless asked; the game already draws a soft shadow.

---

## When you're done

1. Check each picture against the checklist:
   - [ ] same art style as the story pictures
   - [ ] outfit matches the 3D runner in the race (cap/glasses/hoodie for Jay; helmet/lavender hair/bear shirt for Mia)
   - [ ] transparent (or green removed), nothing cut off, no text
2. Send the files to Claude, or copy them into `Assets/_Game/Resources/UI/Runners/` **over** the
   existing `runner_0.png` / `runner_1.png` (keep the `.png.meta` files). New files (`_cheer`,
   `runner_stage`) need their importer set to **Sprite (2D and UI)**.
3. What happens in the game:
   - `runner_0.png`, `runner_1.png` → the character on each card
   - `runner_0_cheer.png`, `runner_1_cheer.png` → shown for a moment when that runner is tapped (already wired; the game uses it automatically when the file exists)
   - `runner_stage.png` → needs one small code change to use (tell Claude)

**Names:** the cards say **Jay** and **Mia**. To change them, edit `GameText.RunnerNames` in
`Assets/_Game/Scripts/Constants/GameText.cs` (and the names in the prompts above).
