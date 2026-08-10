# SummaRace — Image Prompt Pack (Ms. Lumi variation set + hero-art workflow)

**Written 2026-08-10. Audience: whoever is sitting at ChatGPT / Gemini generating images today.**

> **Relationship to the other docs — read this line before anything else.**
> `SummaRace_Asset_Shopping_List.md` is still **the** list of *what* art is outstanding, and its
> **§2 already holds all 27 ready-to-paste hero-image prompts**. This file does **not** repeat them.
> This file adds the two things that document does not have:
> 1. **The full Ms. Lumi variation set** — 22 new prompts, because the game currently has
>    **3 Lumi images total** and she is visibly the same picture on every screen.
> 2. **The consistency wrapper** — the global-rules block, the anchor-frame workflow and the
>    post-processing spec that makes a set of 49 images look like one book instead of 49 sessions.
>
> Hero art: paste **STEP 1** from this file, then run the 27 prompts from Shopping List §2.

---

## 🚀 WHERE TO START (quick nav)

1. Read **[§0 — the honest ledger](#0--the-honest-ledger-what-actually-lands-in-the-game-today)**. Three of these images go
   straight into the game with no code change. The other 19 need a small wiring change first —
   know that before you spend the day.
2. Open a **fresh ChatGPT chat** with image generation on.
3. Paste **[STEP 1 — GLOBAL RULES](#step-1--paste-this-global-rules-block-first)** as your first message. Wait for
   *"Locks loaded."*
4. Generate **[STEP 2 — the anchor frame `L0`](#step-2--the-anchor-frame-generate-this-one-first-and-do-not-skip-it)** and do not move on until she is right.
   She becomes the reference image you upload for every later frame.
5. Work **[STEP 3 — Set A](#step-3--set-a--16-half-body-poses)** (16 half-body poses) → **[STEP 4 — Set B](#step-4--set-b--6-badge-portraits)** (6 badge portraits).
6. Do **[§5 — post-processing](#5--post-processing-transparency-and-the-2048--1024-padding)** (transparency + padding). This is the step that decides whether the
   files work in the game or render in the wrong place.
7. Hero art: **[STEP 5](#step-5--the-27-story-hero-images)**, then Shopping List §2.

**Realistic budget:** Lumi set ≈ 2½–3 h (22 images + post-processing). Hero art ≈ 3–4 h (27 images).
It is one long day. If you only get one done, **do the Lumi set** — it touches every screen.

---

## §0 — The honest ledger: what actually lands in the game today

Right now the whole app is carrying **three** pictures of the teacher, and **two of them are a
different woman in a different art style**:

| File on disk | Real size (measured) | Where the learner sees her | Style |
|---|---|---|---|
| `Assets\_Game\Art\UI\teacher_temp.png` | 290×290, opaque | **Arrange** + **Summary** (scene-wired Image) | Glossy storybook portrait, high bun, green ring badge — **matches the hero art** |
| `Assets\_Game\Resources\UI\mslumi_wave.png` | 2048×1024, alpha bbox `x 507→1406, y 48→1024` | **Reader** idle + **race mission briefing** | Flat vector, thin outlines, low ponytail, mint top — **a different person** |
| `Assets\_Game\Resources\UI\mslumi_cheer.png` | 2048×1024, alpha bbox `x 508→1594, y 48→1024` | **Reader**, on a correct answer only | Same flat-vector woman |

**Design decision made here (so the set has one target):** `teacher_temp.png` is the **canonical
Ms. Lumi**. It is in the same glossy storybook style as the `s01_*` hero art, which is the style
the other 27 hero images are being generated into. The flat-vector Lumi is the odd one out and
gets replaced. Everything below locks to the `teacher_temp` face.

### Drop-in vs. needs-wiring — read this before generating 22 images

| | Images | Lands in the game… |
|---|---|---|
| ✅ **Drop-in (3)** | `lumi_wave`, `lumi_cheer`, `lumi_badge_smile` | …**today**, by overwriting `mslumi_wave.png`, `mslumi_cheer.png` and `teacher_temp.png`. Zero code. Keep the filenames, never delete the `.png.meta`. |
| ⚙️ **Needs wiring (19)** | the other 13 poses + 5 badges | …**only after a one-off code change.** Nothing in the project loads `Resources/UI/Lumi/` yet — `MsLumiReactor` has exactly two serialized sprite slots (idle + cheer) and Arrange/Summary use one static scene-wired Image. |

**This is not a reason to generate fewer.** The wiring is small and is described in
**[§6](#6--the-wiring-that-makes-19-of-them-live-what-i-would-build)** — the filenames in this document are already the ones that change expects, so
nothing you generate today is wasted. But generate the **three drop-ins first**, so that even if
the day runs out, the app is visually consistent tonight.

**Why bother with 22 at all:** the whole point is that Lumi stops being one frozen picture. With
the wiring in place she is chosen from a **shuffle bag per emotional beat** (the same pattern
`Core/Praise.cs` already uses for her words) — so a learner doing ten sessions does not see the
same wave and the same cheer eighty times.

---

## STEP 1 — Paste this GLOBAL RULES block first

Paste the whole fenced block as the **first message** in a fresh chat. Every per-pose prompt below
is still self-contained, so if the model drifts you can re-paste this block as a corrective.

```
🎓 SummaRace — Ms. Lumi character art set
A children's educational reading game for 9–10 year olds. I need a set of images of ONE
recurring teacher character in ONE consistent style. Load these locks and confirm.

⚠️ SINGLE-IMAGE LOCK (read first, obey strictly):
Each prompt I send generates ONE single image showing ONE single pose.
- ONE frozen pose, NOT a sequence
- ONE Ms. Lumi, NOT a character sheet, NOT a turnaround, NOT a grid of poses
- NOT a before/after panel, NOT a side-by-side, NOT a comic strip
- If I need 22 images, I will prompt you 22 SEPARATE times.

👤 CHARACTER LOCK — "Ms. Lumi" (identical in every single image):
- A warm, encouraging Filipino primary-school teacher, mid-20s. Friendly, approachable,
  never stern, never a caricature.
- Warm medium-tan skin with a soft peach blush on the cheeks.
- Dark brown hair pulled up into a neat rounded HIGH BUN on top of her head, with a few soft
  loose strands framing her face and a slight side-swept fringe. Never loose hair, never a
  ponytail, never short hair.
- Thick soft dark eyebrows. Large warm brown almond eyes with long lashes. Small rounded nose.
- Soft round cheeks, small chin, wide friendly smile.
- Small pearl stud earrings. NO glasses. NO hat. NO necklace. NO lanyard, badge or ID card.
- WARDROBE (identical every time): a soft mint-green long-sleeve blouse with a small white
  rounded collar. Plain fabric — no logos, no patterns, no stripes, no buttons detail, no
  cardigan, no apron.
- She is the SAME AGE, SAME FACE, SAME HAIR and SAME OUTFIT in every image. Only her POSE and
  her FACIAL EXPRESSION change.

🎨 ART STYLE LOCK:
- Glossy 2D storybook cartoon — polished animated-feature look, like a modern children's
  mobile game.
- BOLD clean dark outlines, soft cel shading, gentle warm gradient lighting, bright candy
  colours, rounded friendly shapes.
- NOT flat minimal vector. NOT 3D render. NOT anime. NOT photorealistic. NOT pencil sketch.
- NOT a sticker with a white outline around her.

🟦 BACKGROUND LOCK — NON-NEGOTIABLE:
- FULLY TRANSPARENT background (alpha channel). If you cannot output transparency, use PURE
  FLAT #FFFFFF WHITE — solid white, no gradient, no texture, no vignette.
- NO scenery whatsoever: no classroom, no blackboard, no desk, no bookshelf, no window, no
  wall, no floor, no ground, no cast shadow under her.
- NO decorative circle, ring, frame, border, badge shape or background panel behind her
  (EXCEPT the badge-portrait set, where I will explicitly ask for the circle).
- ONLY Ms. Lumi and the single prop named in that pose. Nothing else in the frame.
- WHY: the game composites her over its own backgrounds. Anything baked behind her is unusable
  and has to be regenerated.

🧍 ANATOMY LOCK:
- Exactly ONE head, TWO arms, TWO hands, FIVE fingers per hand, natural proportions.
- No floating or disconnected limbs. No extra hand entering the frame. No duplicate Lumi.
- Hands must be clean and clearly drawn — hands are the most common failure, check them.

🚫 TEXT LOCK — ZERO TEXT ANYWHERE:
- No text, no letters, no numbers, no words, no captions, no title.
- No writing on any book, notepad, page, sign or object she holds — pages stay BLANK or carry
  a simple wordless drawing only.
- No speech bubble. No watermark. No signature. No logo. No UI element.

If you understand, reply exactly: "Locks loaded." and wait for my first pose.
```

---

## STEP 2 — The anchor frame: generate this one FIRST and do not skip it

Everything else hangs off this image. Get her right once and the other 21 are cheap; get her wrong
and you will regenerate all 22.

**Before you paste:** upload `Assets\_Game\Art\UI\teacher_temp.png` as **IMAGE-1**. Say
*"match this character's face, hair and skin tone — but redraw her half-body in the style and
outfit described below, with no circle or ring behind her."*

**Save as:** `lumi_wave.png` (this is also drop-in file #1 — see §5 for the padding step)

```
ONE single image. Character art for a children's educational reading game.

CHARACTER — "Ms. Lumi": a warm, encouraging Filipino primary-school teacher, mid-20s. Warm
medium-tan skin with a soft peach blush. Dark brown hair pulled up into a neat rounded HIGH BUN
with a few soft loose strands framing her face and a slight side-swept fringe. Thick soft dark
eyebrows, large warm brown almond eyes with long lashes, small rounded nose, soft round cheeks.
Small pearl stud earrings, no glasses. Wearing a soft mint-green long-sleeve blouse with a small
white rounded collar, plain fabric, no logos or patterns.

POSE: raising one hand in a friendly open-palm wave beside her head, the other hand relaxed at
her side; head tilted very slightly; bright welcoming open smile with clean white teeth; looking
straight at the viewer.

FRAMING: half body — from the top of her hair down to just below the waist, where she is cropped
by the bottom edge of the frame. Facing the viewer straight on, at eye level. Her whole figure
inside the frame with a small margin above her head and clear space at both sides; nothing cropped
at the top or the sides.

STYLE: glossy 2D storybook cartoon, polished animated-feature look, bold clean dark outlines, soft
cel shading, gentle warm lighting, bright candy colours, rounded friendly shapes. Not flat vector,
not 3D, not anime, not photorealistic.

BACKGROUND: fully transparent (alpha). If transparency is unavailable, pure flat #FFFFFF white.
No scenery, no floor, no shadow, no circle or frame behind her, nothing at all behind her.

Exactly one head, two arms, two hands, five clean fingers per hand. One single figure only.
No text, no letters, no numbers, no watermark, no logo, no speech bubble, no border.
```

**Acceptance check before you continue — all five must be true:**
- [ ] High bun, not a ponytail, not loose hair
- [ ] Mint-green blouse with a white collar
- [ ] Bold dark outlines + soft shading (not flat, not 3D)
- [ ] Nothing behind her — no circle, no shadow, no floor
- [ ] Five clean fingers on the waving hand

**Then: upload `lumi_wave.png` as IMAGE-1 for every remaining pose.** This is the single biggest
factor in the set looking like one character. If a later pose drifts, re-upload it and re-paste
the GLOBAL RULES block.

---

## STEP 3 — Set A — 16 half-body poses

**Every one of these uses the same header. Type this line, then paste the pose block:**

> *Same Ms. Lumi as the reference image — same face, same high bun, same mint-green blouse with
> white collar, same style, same half-body framing, same transparent background. Only the pose and
> expression change. ONE single image.*

**Shared spec for all 16** (repeated inside each prompt so they work standalone):

| Property | Value |
|---|---|
| Framing | Half body, cropped just below the waist by the bottom edge |
| Delivered file | `Assets\_Game\Resources\UI\Lumi\lumi_<pose>.png` |
| Final canvas | **2048 × 1024**, transparent — **see [§5](#5--post-processing-transparency-and-the-2048--1024-padding), do not skip it** |
| Background | Transparent (fallback: flat #FFFFFF) |
| Text | None, anywhere, ever |

The **Beat** column is the moment in the game the pose exists to serve — it is why each one is on
the list, and it is what the wiring in §6 keys off.

| # | File | Bucket | Beat in the game |
|---|---|---|---|
| A1 | `lumi_wave` | GREET | Reader idle · race briefing · **drop-in** |
| A2 | `lumi_present` | GREET | Race briefing, presenting the mission |
| A3 | `lumi_point_up` | GREET | Loading-screen tip card |
| A4 | `lumi_point_side` | GREET | Story Select / Session Map, inviting a choice |
| A5 | `lumi_read_book` | TEACH | Reader, narration playing |
| A6 | `lumi_listen` | TEACH | Reader "HEAR AGAIN" |
| A7 | `lumi_think` | TEACH | Arrange hint · question on screen |
| A8 | `lumi_write` | TEACH | Summary, inviting the learner to type |
| A9 | `lumi_cheer` | PRAISE | Reader correct answer · **drop-in** |
| A10 | `lumi_clap` | PRAISE | Results, stars appearing |
| A11 | `lumi_thumbsup` | PRAISE | Correct pick, race + reader |
| A12 | `lumi_proud` | PRAISE | Summary done · session complete |
| A13 | `lumi_trophy` | PRAISE | Results, treasure reveal |
| A14 | `lumi_go` | HYPE | Race 3-2-1-GO countdown |
| A15 | `lumi_encourage` | SUPPORT | Reader **wrong** answer (never-punish) |
| A16 | `lumi_gentle_no` | SUPPORT | Locked story / locked session — "not yet" |

---

### ✂️ Ready-to-paste — Set A

Each block is self-contained. Upload `lumi_wave.png` as IMAGE-1 first.

#### A1 — `lumi_wave` — friendly hello ✅ drop-in
> *This is the anchor frame from **STEP 2**. Already generated. Do not redo it.*

---

#### A2 — `lumi_present` — presenting the mission
```
ONE single image. Same Ms. Lumi as the reference: Filipino teacher, mid-20s, warm medium-tan
skin, dark brown hair in a neat rounded HIGH BUN with loose strands framing her face, large warm
brown eyes, pearl stud earrings, soft mint-green long-sleeve blouse with a small white rounded
collar.

POSE: one arm extended out to her side with the palm turned upward, presenting and introducing
something just off-frame; her other hand rests lightly at her waist; chin slightly lifted;
confident, enthusiastic encouraging smile with eyebrows raised; looking toward the viewer.

FRAMING: half body, cropped just below the waist by the bottom edge, facing the viewer at eye
level, whole figure inside the frame with a small margin above her head.

STYLE: glossy 2D storybook cartoon, bold clean dark outlines, soft cel shading, gentle warm
lighting, bright candy colours. Not flat vector, not 3D, not anime.

BACKGROUND: fully transparent, or pure flat #FFFFFF white. Nothing behind her — no scenery,
no floor, no shadow, no circle, no frame.

One head, two arms, two hands, five clean fingers each. No text, no letters, no watermark.
```

#### A3 — `lumi_point_up` — "here's a tip!"
```
ONE single image. Same Ms. Lumi as the reference: Filipino teacher, mid-20s, warm medium-tan
skin, dark brown hair in a neat rounded HIGH BUN with loose strands framing her face, large warm
brown eyes, pearl stud earrings, soft mint-green long-sleeve blouse with a small white rounded
collar.

POSE: the index finger of one hand raised straight up beside her head in a bright "here's a
tip!" gesture, her other hand resting on her hip; eyes wide and lively; cheerful knowing smile;
head tilted slightly to one side.

FRAMING: half body, cropped just below the waist by the bottom edge, facing the viewer at eye
level, whole figure inside the frame with a small margin above her raised hand.

STYLE: glossy 2D storybook cartoon, bold clean dark outlines, soft cel shading, gentle warm
lighting, bright candy colours. Not flat vector, not 3D, not anime.

BACKGROUND: fully transparent, or pure flat #FFFFFF white. Nothing behind her — no scenery,
no floor, no shadow, no circle, no frame.

One head, two arms, two hands, five clean fingers each. No text, no letters, no watermark.
```

#### A4 — `lumi_point_side` — inviting a choice
```
ONE single image. Same Ms. Lumi as the reference: Filipino teacher, mid-20s, warm medium-tan
skin, dark brown hair in a neat rounded HIGH BUN with loose strands framing her face, large warm
brown eyes, pearl stud earrings, soft mint-green long-sleeve blouse with a small white rounded
collar.

POSE: leaning slightly forward, pointing across to her left with one index finger and looking in
that direction with an interested, inviting expression; her other hand held softly against her
chest; warm half-smile with eyebrows raised in encouragement.

FRAMING: half body, cropped just below the waist by the bottom edge, at eye level, whole figure
including the pointing arm inside the frame with a small margin above her head.

STYLE: glossy 2D storybook cartoon, bold clean dark outlines, soft cel shading, gentle warm
lighting, bright candy colours. Not flat vector, not 3D, not anime.

BACKGROUND: fully transparent, or pure flat #FFFFFF white. Nothing behind her — no scenery,
no floor, no shadow, no circle, no frame.

One head, two arms, two hands, five clean fingers each. No text, no letters, no watermark.
```

#### A5 — `lumi_read_book` — reading aloud
```
ONE single image. Same Ms. Lumi as the reference: Filipino teacher, mid-20s, warm medium-tan
skin, dark brown hair in a neat rounded HIGH BUN with loose strands framing her face, large warm
brown eyes, pearl stud earrings, soft mint-green long-sleeve blouse with a small white rounded
collar.

POSE: holding an open picture book in both hands at chest height, tilted slightly toward the
viewer, looking down at the page with a soft delighted smile, as if reading a story aloud. The
book is a simple colourful hardcover children's book. ITS PAGES CARRY ONLY A SMALL SIMPLE
WORDLESS DRAWING — absolutely no letters, no words, no lines of writing anywhere on the pages.

FRAMING: half body, cropped just below the waist by the bottom edge, at eye level, whole figure
and the whole book inside the frame with a small margin above her head.

STYLE: glossy 2D storybook cartoon, bold clean dark outlines, soft cel shading, gentle warm
lighting, bright candy colours. Not flat vector, not 3D, not anime.

BACKGROUND: fully transparent, or pure flat #FFFFFF white. Nothing behind her — no scenery,
no floor, no shadow, no circle, no frame.

One head, two arms, two hands, five clean fingers each. No text, no letters, no watermark.
```

#### A6 — `lumi_listen` — "listen again"
```
ONE single image. Same Ms. Lumi as the reference: Filipino teacher, mid-20s, warm medium-tan
skin, dark brown hair in a neat rounded HIGH BUN with loose strands framing her face, large warm
brown eyes, pearl stud earrings, soft mint-green long-sleeve blouse with a small white rounded
collar.

POSE: one hand cupped playfully behind her ear as if listening closely, leaning in slightly
toward the viewer, her other hand relaxed at her side; eyes bright and curious; playful expectant
grin; head turned a little to one side.

FRAMING: half body, cropped just below the waist by the bottom edge, at eye level, whole figure
inside the frame with a small margin above her head.

STYLE: glossy 2D storybook cartoon, bold clean dark outlines, soft cel shading, gentle warm
lighting, bright candy colours. Not flat vector, not 3D, not anime.

BACKGROUND: fully transparent, or pure flat #FFFFFF white. Nothing behind her — no scenery,
no floor, no shadow, no circle, no frame.

One head, two arms, two hands, five clean fingers each. No text, no letters, no watermark.
```

#### A7 — `lumi_think` — thinking it through
```
ONE single image. Same Ms. Lumi as the reference: Filipino teacher, mid-20s, warm medium-tan
skin, dark brown hair in a neat rounded HIGH BUN with loose strands framing her face, large warm
brown eyes, pearl stud earrings, soft mint-green long-sleeve blouse with a small white rounded
collar.

POSE: the knuckle of one index finger resting lightly against her chin, her eyes looking upward
and to the side in thought, her other arm folded across her middle supporting the first elbow;
gently curious closed-lip smile; one eyebrow raised slightly. Thoughtful and warm, never
confused, never worried.

FRAMING: half body, cropped just below the waist by the bottom edge, at eye level, whole figure
inside the frame with a small margin above her head.

STYLE: glossy 2D storybook cartoon, bold clean dark outlines, soft cel shading, gentle warm
lighting, bright candy colours. Not flat vector, not 3D, not anime.

BACKGROUND: fully transparent, or pure flat #FFFFFF white. Nothing behind her — no scenery,
no floor, no shadow, no circle, no frame. NO thought bubble, NO question mark, NO light bulb.

One head, two arms, two hands, five clean fingers each. No text, no letters, no watermark.
```

#### A8 — `lumi_write` — "now you write it"
```
ONE single image. Same Ms. Lumi as the reference: Filipino teacher, mid-20s, warm medium-tan
skin, dark brown hair in a neat rounded HIGH BUN with loose strands framing her face, large warm
brown eyes, pearl stud earrings, soft mint-green long-sleeve blouse with a small white rounded
collar.

POSE: holding a small notepad in one hand and a chunky yellow pencil in the other, poised as if
about to write, but looking up at the viewer with an inviting, encouraging smile and raised
eyebrows. THE NOTEPAD PAGE IS COMPLETELY BLANK — no ruled lines, no letters, no words, no marks
of any kind.

FRAMING: half body, cropped just below the waist by the bottom edge, at eye level, whole figure
inside the frame with a small margin above her head.

STYLE: glossy 2D storybook cartoon, bold clean dark outlines, soft cel shading, gentle warm
lighting, bright candy colours. Not flat vector, not 3D, not anime.

BACKGROUND: fully transparent, or pure flat #FFFFFF white. Nothing behind her — no scenery,
no floor, no shadow, no circle, no frame.

One head, two arms, two hands, five clean fingers each. No text, no letters, no watermark.
```

#### A9 — `lumi_cheer` — celebrating ✅ drop-in
```
ONE single image. Same Ms. Lumi as the reference: Filipino teacher, mid-20s, warm medium-tan
skin, dark brown hair in a neat rounded HIGH BUN with loose strands framing her face, large warm
brown eyes, pearl stud earrings, soft mint-green long-sleeve blouse with a small white rounded
collar.

POSE: both arms thrown straight up above her head in celebration with her hands open; big joyful
open-mouthed smile showing clean white teeth; eyes bright and wide with delight; head tilted back
just slightly; a few strands of hair lifting with the movement.

FRAMING: half body, cropped just below the waist by the bottom edge, at eye level. BOTH RAISED
HANDS MUST BE FULLY INSIDE THE FRAME with clear space above them — do not crop her fingers at the
top edge.

STYLE: glossy 2D storybook cartoon, bold clean dark outlines, soft cel shading, gentle warm
lighting, bright candy colours. Not flat vector, not 3D, not anime.

BACKGROUND: fully transparent, or pure flat #FFFFFF white. Nothing behind her — no scenery,
no floor, no shadow, no confetti, no sparkles, no circle, no frame.

One head, two arms, two hands, five clean fingers each. No text, no letters, no watermark.
```

#### A10 — `lumi_clap` — applause
```
ONE single image. Same Ms. Lumi as the reference: Filipino teacher, mid-20s, warm medium-tan
skin, dark brown hair in a neat rounded HIGH BUN with loose strands framing her face, large warm
brown eyes, pearl stud earrings, soft mint-green long-sleeve blouse with a small white rounded
collar.

POSE: clapping her hands together in front of her chest mid-applause with her elbows out;
delighted open-mouthed smile; eyes squeezed happily shut into cheerful curves; shoulders raised a
little in excitement.

FRAMING: half body, cropped just below the waist by the bottom edge, at eye level, whole figure
inside the frame with a small margin above her head.

STYLE: glossy 2D storybook cartoon, bold clean dark outlines, soft cel shading, gentle warm
lighting, bright candy colours. Not flat vector, not 3D, not anime.

BACKGROUND: fully transparent, or pure flat #FFFFFF white. Nothing behind her — no scenery,
no floor, no shadow, no motion lines, no sparkles, no circle, no frame.

One head, two arms, two hands, five clean fingers each. No text, no letters, no watermark.
```

#### A11 — `lumi_thumbsup` — "you got it"
```
ONE single image. Same Ms. Lumi as the reference: Filipino teacher, mid-20s, warm medium-tan
skin, dark brown hair in a neat rounded HIGH BUN with loose strands framing her face, large warm
brown eyes, pearl stud earrings, soft mint-green long-sleeve blouse with a small white rounded
collar.

POSE: giving a strong clear thumbs-up with one hand held up near her shoulder, her other hand on
her hip; beaming wide open smile with visible white teeth; eyes crinkled happily; looking directly
at the viewer.

FRAMING: half body, cropped just below the waist by the bottom edge, at eye level, whole figure
inside the frame with a small margin above her head.

STYLE: glossy 2D storybook cartoon, bold clean dark outlines, soft cel shading, gentle warm
lighting, bright candy colours. Not flat vector, not 3D, not anime.

BACKGROUND: fully transparent, or pure flat #FFFFFF white. Nothing behind her — no scenery,
no floor, no shadow, no circle, no frame.

One head, two arms, two hands, five clean fingers each — the thumbs-up hand must be clearly and
correctly drawn. No text, no letters, no watermark.
```

#### A12 — `lumi_proud` — quietly proud of you
```
ONE single image. Same Ms. Lumi as the reference: Filipino teacher, mid-20s, warm medium-tan
skin, dark brown hair in a neat rounded HIGH BUN with loose strands framing her face, large warm
brown eyes, pearl stud earrings, soft mint-green long-sleeve blouse with a small white rounded
collar.

POSE: one hand pressed softly over her heart, her other hand relaxed at her side; chin lifted
slightly; a warm, proud, closed-lip smile; gentle affectionate eyes looking straight at the
viewer. Calm and heartfelt rather than excited.

FRAMING: half body, cropped just below the waist by the bottom edge, at eye level, whole figure
inside the frame with a small margin above her head.

STYLE: glossy 2D storybook cartoon, bold clean dark outlines, soft cel shading, gentle warm
lighting, bright candy colours. Not flat vector, not 3D, not anime.

BACKGROUND: fully transparent, or pure flat #FFFFFF white. Nothing behind her — no scenery,
no floor, no shadow, no hearts, no circle, no frame.

One head, two arms, two hands, five clean fingers each. No text, no letters, no watermark.
```

#### A13 — `lumi_trophy` — the treasure reveal
```
ONE single image. Same Ms. Lumi as the reference: Filipino teacher, mid-20s, warm medium-tan
skin, dark brown hair in a neat rounded HIGH BUN with loose strands framing her face, large warm
brown eyes, pearl stud earrings, soft mint-green long-sleeve blouse with a small white rounded
collar.

POSE: holding up a small shiny gold star trophy in one raised hand and admiring it with a
delighted smile, her other hand gesturing open toward the viewer as if offering it. THE TROPHY IS
A PLAIN GOLD STAR ON A SMALL ROUND BASE — completely smooth, with no engraving, no plaque, no
letters, no numbers on it anywhere.

FRAMING: half body, cropped just below the waist by the bottom edge, at eye level, whole figure
and the whole trophy inside the frame with a small margin above the trophy.

STYLE: glossy 2D storybook cartoon, bold clean dark outlines, soft cel shading, gentle warm
lighting, bright candy colours. Not flat vector, not 3D, not anime.

BACKGROUND: fully transparent, or pure flat #FFFFFF white. Nothing behind her — no scenery,
no floor, no shadow, no sparkles, no circle, no frame.

One head, two arms, two hands, five clean fingers each. No text, no letters, no watermark.
```

#### A14 — `lumi_go` — the countdown
```
ONE single image. Same Ms. Lumi as the reference: Filipino teacher, mid-20s, warm medium-tan
skin, dark brown hair in a neat rounded HIGH BUN with loose strands framing her face, large warm
brown eyes, pearl stud earrings, soft mint-green long-sleeve blouse with a small white rounded
collar.

POSE: leaning energetically forward toward the viewer, one fist pumped up beside her head, her
other arm bent at her side; mouth open in an excited encouraging cheer; eyebrows up; eyes wide and
full of energy; loose hair strands lifting with the motion. Full of momentum, like she is sending
a runner off at a starting line.

FRAMING: half body, cropped just below the waist by the bottom edge, at eye level, whole figure
inside the frame with a small margin above her raised fist.

STYLE: glossy 2D storybook cartoon, bold clean dark outlines, soft cel shading, gentle warm
lighting, bright candy colours. Not flat vector, not 3D, not anime.

BACKGROUND: fully transparent, or pure flat #FFFFFF white. Nothing behind her — no scenery,
no floor, no shadow, no motion lines, no speed streaks, no circle, no frame.

One head, two arms, two hands, five clean fingers each. No text, no letters, no watermark.
```

#### A15 — `lumi_encourage` — after a wrong answer
> ⚠️ **The most important pose in the set.** The game never punishes a wrong answer, so she must
> read as *warm and unbothered*. Any hint of disappointment, correction or sternness is a
> regenerate — a 9-year-old reads a face faster than a sentence.
```
ONE single image. Same Ms. Lumi as the reference: Filipino teacher, mid-20s, warm medium-tan
skin, dark brown hair in a neat rounded HIGH BUN with loose strands framing her face, large warm
brown eyes, pearl stud earrings, soft mint-green long-sleeve blouse with a small white rounded
collar.

POSE: both hands open with palms up and forward at chest height in a gentle, welcoming "that's
okay — let's look again together" gesture; shoulders relaxed and lowered; soft reassuring
closed-lip smile; kind, warm, patient eyes; head tilted gently to one side.

MOOD: completely warm and supportive. She is NOT disappointed, NOT correcting, NOT stern, NOT
sad, NOT shrugging, NOT confused. She looks like a kind teacher saying "no problem at all."

FRAMING: half body, cropped just below the waist by the bottom edge, at eye level, whole figure
inside the frame with a small margin above her head.

STYLE: glossy 2D storybook cartoon, bold clean dark outlines, soft cel shading, gentle warm
lighting, bright candy colours. Not flat vector, not 3D, not anime.

BACKGROUND: fully transparent, or pure flat #FFFFFF white. Nothing behind her — no scenery,
no floor, no shadow, no circle, no frame.

One head, two arms, two hands, five clean fingers each. No text, no letters, no watermark.
```

#### A16 — `lumi_gentle_no` — "not yet, but soon"
```
ONE single image. Same Ms. Lumi as the reference: Filipino teacher, mid-20s, warm medium-tan
skin, dark brown hair in a neat rounded HIGH BUN with loose strands framing her face, large warm
brown eyes, pearl stud earrings, soft mint-green long-sleeve blouse with a small white rounded
collar.

POSE: both hands held softly together in front of her chest, fingers loosely interlaced;
shoulders slightly raised; head tilted; a kind, patient, slightly apologetic smile with warm eyes
and gently raised eyebrows — the expression of "not yet, but very soon."

MOOD: kind and hopeful. NOT refusing, NOT scolding, NOT sad, NOT crossing her arms, NOT holding
up a stop hand.

FRAMING: half body, cropped just below the waist by the bottom edge, at eye level, whole figure
inside the frame with a small margin above her head.

STYLE: glossy 2D storybook cartoon, bold clean dark outlines, soft cel shading, gentle warm
lighting, bright candy colours. Not flat vector, not 3D, not anime.

BACKGROUND: fully transparent, or pure flat #FFFFFF white. Nothing behind her — no scenery,
no floor, no shadow, no lock icon, no circle, no frame.

One head, two arms, two hands, five clean fingers each. No text, no letters, no watermark.
```

---

## STEP 4 — Set B — 6 badge portraits

These replace `teacher_temp.png`, which appears on the **Arrange** and **Summary** screens as a
small round teacher badge next to a speech bubble.

**Different rules from Set A — read them:**

- **Head-and-shoulders**, not half body.
- **Square 1:1**, delivered at **512 × 512**.
- **The circular badge frame IS baked into the image** — a soft cream-white circle behind her with
  a light-green ring around it, and light-green filling the four corners of the square. This
  matches the existing `teacher_temp.png` exactly, so any of the six is a like-for-like swap with
  no compositing and no code change to the panel behind her.
- **Opaque** — no transparency needed for this set.

**Shared framing/frame block** (already inside each prompt below):

> *Square 1:1 head-and-shoulders portrait. She is centred inside a soft cream-white circle that
> nearly fills the square; a smooth light-green ring runs around the circle's edge; the four
> corners outside the circle are filled with a soft light-green gradient. The top of her hair bun
> touches near the top of the circle and her shoulders are cut off by the bottom of the circle.*

| # | File | Expression | Beat |
|---|---|---|---|
| B1 | `lumi_badge_smile` | Warm neutral smile, facing viewer | Arrange + Summary default · **drop-in over `teacher_temp.png`** |
| B2 | `lumi_badge_talk` | Mid-speech, mouth open, animated eyebrows | Instruction line / narration |
| B3 | `lumi_badge_think` | Finger near chin, eyes up, curious | Arrange hint |
| B4 | `lumi_badge_happy` | Eyes happily closed, big grin | Correct / perfect order |
| B5 | `lumi_badge_wink` | Playful wink, small closed smile | Praise variety |
| B6 | `lumi_badge_proud` | Soft proud smile, head slightly tilted | Summary submitted |

### ✂️ Ready-to-paste — Set B

**The base block. Generate B1 first**, then for B2–B6 just re-send it with the `EXPRESSION:` line
swapped (and upload B1 as an extra reference so the badge frame stays identical).

```
ONE single image. Square 1:1 badge portrait for a children's educational reading game.

CHARACTER — same Ms. Lumi as the reference image: a warm, encouraging Filipino primary-school
teacher, mid-20s. Warm medium-tan skin with a soft peach blush. Dark brown hair pulled up into a
neat rounded HIGH BUN with a few soft loose strands framing her face and a slight side-swept
fringe. Thick soft dark eyebrows, large warm brown almond eyes with long lashes, small rounded
nose, soft round cheeks. Small pearl stud earrings, no glasses. Soft mint-green blouse with a
small white rounded collar (only the collar and shoulders are visible).

EXPRESSION: a warm, welcoming, open smile with clean white teeth, looking straight at the viewer.

FRAMING: head-and-shoulders portrait, centred, filling most of the frame. She sits inside a soft
cream-white circle that nearly fills the square. A smooth light-green ring runs around the edge of
that circle. The four corners of the square, outside the circle, are filled with a soft light-green
gradient. The top of her hair bun sits near the top of the circle; her shoulders are cut off by the
bottom of the circle.

STYLE: glossy 2D storybook cartoon, polished animated-feature look, bold clean dark outlines, soft
cel shading, gentle warm lighting, bright candy colours. Not flat vector, not 3D, not anime, not
photorealistic.

One head, one face, one figure. No hands in frame unless the expression says otherwise.
No text, no letters, no numbers, no watermark, no logo, no badge icon, no star, no decoration
inside the circle other than Ms. Lumi herself.
```

**Swap this one line for each:**

| File | `EXPRESSION:` line |
|---|---|
| `lumi_badge_smile` | `a warm, welcoming, open smile with clean white teeth, looking straight at the viewer.` |
| `lumi_badge_talk` | `mid-sentence and animated — mouth open as if speaking warmly, eyebrows raised, eyes bright and engaged, looking straight at the viewer.` |
| `lumi_badge_think` | `thoughtful and curious — the knuckle of one index finger resting lightly against her chin (that hand is visible at the bottom of the circle), eyes looking up and to the side, gentle closed-lip smile, one eyebrow slightly raised.` |
| `lumi_badge_happy` | `beaming with delight — a big open grin and her eyes squeezed happily shut into cheerful upward curves, cheeks lifted, head tilted slightly.` |
| `lumi_badge_wink` | `playful and friendly — one eye closed in a clear wink, the other eye open and bright, a small closed-lip smile turned up at one side, head tilted slightly.` |
| `lumi_badge_proud` | `quietly proud and affectionate — a soft warm closed-lip smile, gentle half-lidded kind eyes, chin lifted a little and head tilted slightly, looking straight at the viewer.` |

---

## §5 — Post-processing: transparency, and the 2048 × 1024 padding

**Set B needs none of this** — save at 512×512 and you are done.

**Set A does, and this is the step that decides whether the files work.**

### 5a. Transparency

Ask for a transparent PNG first. If your tool returns a white background instead:

1. Generate on **pure flat #FFFFFF white** (the prompts already say this).
2. Key the white out in **Photopea** (free, browser, no install), Paint.NET or Krita — magic wand
   the white, `Select ▸ Modify ▸ Contract 1px`, delete, then check the edges.
3. ⚠️ **Check her white collar and her teeth afterwards.** A naive "remove white" eats both.
   Photopea's *Magic Wand with Contiguous ON* only removes the connected outer white and leaves
   them alone — use that, not "Select Color Range".

Save as **PNG-32 with alpha**. Not JPG. Not PNG-8.

### 5b. The 2048 × 1024 padding — do not skip, do not improvise

The game's race-briefing layout has the current file's padding **hard-coded into its anchor
maths** (there is a long comment explaining it in `EndlessRaceDirector.cs` around line 3029). Hand
in a tightly-cropped Lumi and she renders **thumbnail-sized in the wrong corner**.

Measured from the two files actually on disk:

| File | Canvas | Figure occupies |
|---|---|---|
| `mslumi_wave.png` | 2048 × 1024 | `x 507 → 1406` (899 px), `y 48 → 1024` (976 px) |
| `mslumi_cheer.png` | 2048 × 1024 | `x 508 → 1594` (1086 px), `y 48 → 1024` (976 px) |

Note the two are **not** the same width — the cheer pose's raised arms are simply wider. So the
rule is not "match 899 px". The rule that both files actually obey, and the one to follow:

> **On a 2048 × 1024 transparent canvas:**
> 1. Scale the figure so it is **976 px tall**.
> 2. **Bottom-align it** — her waist crop sits exactly on the bottom edge, `y = 1024`. Her head
>    top lands at `y ≈ 48`.
> 3. **Centre her head-and-torso centreline at `x ≈ 1000`.** Raised arms may extend past that;
>    that is fine and expected. ±60 px is invisible in game.
> 4. Everything else stays transparent.

**How to do it, easiest first:**

| Method | Notes |
|---|---|
| **Ask me to add a Unity menu item** | `SummaRace ▸ Art ▸ Pad Lumi Poses` — drop tight cut-outs into a folder, one click normalises all 16 to the spec above. ~20 lines, removes this whole step and any chance of getting it wrong. **Recommended — say the word.** |
| **Photopea, by hand** | `Image ▸ Canvas Size` → 2048 × 1024, anchor **bottom-centre**; then scale the layer so its height is 976 px; nudge to `x ≈ 1000`. ~1 min per file, 16 files. |
| **Ask the generator for the padded canvas directly** | **Don't.** Image models cannot place a figure at a specified pixel rect. It will look right and be wrong. |

---

## §6 — The wiring that makes 19 of them live (what I would build)

Not built yet. Written down here so the filenames you generate today already match it.

- **`Assets\_Game\Resources\UI\Lumi\`** — new folder, all 22 files, loaded with
  `Resources.LoadAll<Sprite>("UI/Lumi")` so adding a pose later is a file drop, not a code change
  (same principle as story JSON and narration).
- **`SummaRace.UI.LumiPortrait`** — one component replacing `MsLumiReactor`'s two hard-wired
  sprite slots. It takes a **default bucket** per screen and swaps pose on EventBus traffic:
  `PageAnswered.correct` → PRAISE · `PageAnswered` wrong → SUPPORT · countdown → HYPE, etc.
- **Shuffle-bag selection inside each bucket**, reusing the exact pattern in `Core/Praise.cs`:
  every pose in the bucket is used once before any repeats. This is what actually delivers
  *"stop showing the same teacher all app"* — five praise poses picked at random still repeat
  visibly; a shuffle bag does not.
- **Buckets:** `GREET` (A1–A4) · `TEACH` (A5–A8) · `PRAISE` (A9–A13) · `SUPPORT` (A15–A16) ·
  `HYPE` (A14) · `BADGE` (B1–B6).
- Arrange/Summary's scene-wired static `Image` gets the same component so the badge rotates too.

Effort: one script plus scene wiring on Reader / Arrange / Summary. Ask and it gets done in the
same pass as the art landing.

---

## STEP 5 — The 27 story hero images

**The prompts already exist and are ready to paste: `SummaRace_Asset_Shopping_List.md` §2.**
They are not copied here on purpose — two copies of 27 prompts drift apart, and that document is
the one the checklist in its §7 tracks against.

What this file adds is the wrapper that makes them consistent. Do this around them:

1. **Fresh chat, separate from the Lumi chat** (different style target, different framing).
2. **Upload `Assets\_Game\Resources\Stories\Art\s01_easy.png` as IMAGE-1 and leave it as the
   rolling reference for all 27.** The three `s01_*` images are real finished art in the house
   style and are **the** style anchor — the other 27 exist to match them. Say:
   *"Match this image's art style, outline weight, colour palette and lighting exactly for
   everything that follows."*
3. Paste this style-lock message once, before the first prompt:
   ```
   I am generating 27 wide landscape illustrations for a children's reading game, in ONE
   consistent style matching the reference image. For every one:
   - 3:2 LANDSCAPE, wide. Delivered at 1536 x 1024.
   - Glossy 2D children's storybook cartoon, bold clean outlines, bright candy colours, soft
     warm lighting — matching the reference image exactly.
   - Characters and action on the LEFT and CENTRE. The RIGHT third stays simple and
     uncluttered (open sky, plain wall, grass) because a title is drawn over it.
   - Full-bleed opaque illustration, edge to edge. No transparency, no border, no vignette.
   - Age-appropriate for 9-10 year olds: never scary, never violent, never grim. Diverse,
     Filipino-friendly children wherever children appear.
   - ZERO text: no title, no letters, no numbers, no signs, no watermark, no signature.
   - ONE single scene per image. Not a collage, not panels, not a storybook page layout.
   Reply "Ready" and I will send them one at a time.
   ```
4. Run §2's prompts **in order, in that one chat**, `s02_easy` → `s10_hard`.
5. **Save each straight over the placeholder**, same filename, in
   `Assets\_Game\Resources\Stories\Art\`. Never delete the `.png.meta`.
6. If one drifts — muted colours, thin outlines, a 3D look, a painterly look — **regenerate it
   before moving on**. Style drift compounds; by image 20 you will not be able to tell what the
   target was.

Two facts worth having in front of you while you work (both verified on disk):

- The 27 placeholders are **completely blank blue-to-green gradients** at 1024×640. There is no
  partial art to preserve. Generate at **1536 × 1024** to match the real `s01_*` files.
- File size does not matter. Unity re-compresses on import and already caps these at 1024 px for
  Android. A 3 MB PNG costs nothing in the APK.

---

## §7 — Where every file goes

| Set | Files | Destination | Size | Alpha |
|---|---|---|---|---|
| A (drop-in) | `lumi_wave` → save as **`mslumi_wave.png`** | `Assets\_Game\Resources\UI\` | 2048×1024 | ✔ |
| A (drop-in) | `lumi_cheer` → save as **`mslumi_cheer.png`** | `Assets\_Game\Resources\UI\` | 2048×1024 | ✔ |
| A (pending wiring) | the other 14, as `lumi_<pose>.png` | `Assets\_Game\Resources\UI\Lumi\` *(create it)* | 2048×1024 | ✔ |
| A (belt and braces) | copies of `lumi_wave` + `lumi_cheer` | `Assets\_Game\Resources\UI\Lumi\` too | 2048×1024 | ✔ |
| B (drop-in) | `lumi_badge_smile` → save as **`teacher_temp.png`** | `Assets\_Game\Art\UI\` | 512×512 | ✘ |
| B (pending wiring) | the other 5, as `lumi_badge_<n>.png` | `Assets\_Game\Resources\UI\Lumi\` | 512×512 | ✘ |
| Hero | `s02_easy.png` … `s10_hard.png` | `Assets\_Game\Resources\Stories\Art\` | 1536×1024 | ✘ |

**Install rule for all of them:** copy the file in with the **exact existing filename**, choose
*Replace*, **never touch the `.png.meta`** (it holds *Texture Type = Sprite* and Unity's asset ID —
keeping it is what makes this a zero-code swap). Then click into the Unity window; it reimports on
its own.

New files in the new `Lumi\` folder have no `.meta` yet — Unity creates one on import, defaulting
to *Texture*, **not Sprite**. Those need the importer set once (or the wiring pass in §6 handles
them, whichever comes first). Do not let that stop you generating them.

---

## §8 — Production order for today

Do it in this order so that whenever you stop, the game is in a better state than when you started.

| Block | What | Why this order |
|---|---|---|
| **1** | STEP 1 + STEP 2 — the anchor `lumi_wave` | Everything depends on her being right once |
| **2** | `lumi_cheer` (A9) + `lumi_badge_smile` (B1) | With block 1, these are the **3 drop-ins** — the app is visually consistent tonight even if you stop here |
| **3** | Pad + install those three (§5b, §7) | Bank the win before generating more |
| **4** | Set A, A2–A8 and A10–A16 | The variety pass |
| **5** | Set B, B2–B6 | Same chat as B1, badge frame stays identical |
| **6** | Pad all of Set A (§5b) | One batch is faster than one-by-one |
| **7** | Hero art, STEP 5 + Shopping List §2 | Fresh chat, `s01_easy` as anchor |

---

## §9 — Troubleshooting

| Symptom | Fix |
|---|---|
| **She has drifted into a different woman** | Re-upload `lumi_wave.png` as IMAGE-1 **and** re-paste the STEP 1 GLOBAL RULES block, then re-issue the pose. Do not try to fix it with "make her look more like before". |
| **Hair came out as a ponytail or loose** | The single most common drift. Add to the prompt: *"her hair is tied up in a rounded bun ON TOP of her head — absolutely no ponytail, no loose hair, no hair over her shoulders."* |
| **Flat vector look crept in** | Add: *"richly shaded with soft gradients and highlights, glossy polished storybook rendering — NOT flat, NOT minimal, NOT a simple icon."* |
| **Letters appeared on a book / notepad / trophy** | Regenerate with: *"the page/surface is completely blank — no writing, no letters, no symbols, no scribbles."* Text is the #1 reason to reject an image; the game draws its own text over her. |
| **A circle, shadow or background panel appeared behind her (Set A)** | Regenerate. Do not try to erase it — the edge blend will show. Add: *"absolutely nothing behind the character; the area around her is completely empty."* |
| **Hands are wrong (six fingers, fused, mangled)** | Regenerate, and add: *"hands clearly and correctly drawn, exactly five fingers on each hand, fingers separated."* Worst offenders here: A9 cheer, A10 clap, A11 thumbs-up. |
| **The model returns a character sheet / grid of poses** | Re-paste the SINGLE-IMAGE LOCK from STEP 1. It happens when a prompt lists more than one gesture. |
| **A prompt is refused** | Nothing in this set should trip a filter. If it does, drop any word that sounds evaluative ("correct", "wrong") and describe only the gesture and the face. Or switch to Gemini with the same prompt. |
| **She looks too old / too young** | Add: *"clearly a young adult in her mid-20s — not a teenager, not middle-aged."* |

---

## §10 — Credits log (do this as you go, it takes 10 seconds each)

`Documentation/Asset_Credits.md` does not exist yet — **create it with your first image.** A thesis
committee can ask how the art was produced, and reconstructing it afterwards is guesswork.

One line per image:

```
lumi_wave.png | AI-generated | ChatGPT (GPT image) | 2026-08-10 | prompt: SummaRace_Image_Prompt_Pack.md STEP 2
```

For the hero art, reference `SummaRace_Asset_Shopping_List.md §2, prompt #N` rather than pasting
the prompt again.

---

## §11 — Two things this file deliberately does not cover

- **The 4 learner avatars, the 5 SWBST pickup icons and `sfx_unlock`.** Shopping List §4 explains,
  per item, that dropping those files in does nothing without a developer, and why each is a
  *skip* for this study. That reasoning has not changed — don't spend generation time on them.
- **Anything that unblocks a build.** The two genuine blockers are still **Android Build Support**
  and **Addressables content built for Android**, and no image on this list moves either. See
  `SummaRace_Build_And_Release.md`.
