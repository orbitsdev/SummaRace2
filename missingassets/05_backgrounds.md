> # ✅ DONE 2026-08-19 — both generated, cropped and wired
>
> `bg_journey.png` -> SessionMap + StorySelect · `bg_reading.png` -> Reader + Arrange + Summary.
> NameEntry + TeacherMenu deliberately kept on `bg_storyselect.png`.
>
> **The one rule for any replacement: 941x1672 (ratio 0.563).** Every `Sky` Image has
> `preserveAspect` **off**, so it stretches to fill — these arrived 1024x1536 (0.667) and would
> have been squashed ~16% horizontally. Each was centre-cropped to 864x1536 then scaled to
> 941x1672 to match the three existing backgrounds exactly.
>
> Contrast measured on the middle band where panels sit: `bg_journey` passes AA for all three
> text tokens; `bg_reading` passes for TextBrownDeep (5.95) and Navy (5.91) but **TextBrown is
> 4.19 — large text only**. Body copy there sits on a card, so this is headroom, not a defect.

---

# Backgrounds — the real gap is that ONE image is on SEVEN screens

**You do not need to write prompts. I write them — that is what this folder is for.**
The prompts are below, ready to paste.

---

## What is actually there today (measured, not guessed)

The game has **three** backgrounds:

| File | Screens |
|---|---|
| `bg_splash.png` | Boot, the loading overlay, the race briefing |
| `bg_menu.png` | MainMenu |
| **`bg_storyselect.png`** | **Arrange, NameEntry, Reader, SessionMap, StorySelect, Summary, TeacherMenu — 7 screens** |

So Story Select is **not** missing a background. It has a good one — a portrait park with sky,
clouds, flowers and a treasure chest. Two things are true at once:

1. **On Story Select you barely see it.** The `LevelPanel` board spans x 0.045–0.955 and
   y 0.115–0.875 = **69% of the screen**, and the three cards fill 96% of that. Only a margin
   shows — the top strip, the bottom strip with the chest, and thin sides.
2. **Seven screens looking identical is what makes the game feel flat.** That is the thing worth
   fixing, and Story Select is the *worst* place to spend a new background because it is the most
   covered.

> Separate, and not an art problem: the `LevelPanel` board is a dark brown at 42% alpha that the
> cards cover completely, so it currently does nothing visible. Worth a look in Play mode before
> anyone paints around it.

---

## Recommended: 2 new backgrounds, not 1

Both **1080 × 1920 portrait**, PNG, opaque, **no text**, and — this matters — **keep the middle
50% quiet**, because every one of these screens puts a panel there.

Overwrite rule as always: new files, so Unity will make the `.meta`. Set **Texture Type = Sprite
(2D and UI)** after import.

### A — `bg_journey.png` → SessionMap + StorySelect (the "choose your adventure" screens)

Save to `Assets/_Game/Resources/UI/bg_journey.png`.

```
Vertical portrait children's storybook background, glossy 2D cartoon style, kids' mobile game,
bright candy colors, bold clean outlines, cheerful, soft warm morning light. A rolling green
adventure landscape seen from a gentle height: winding dirt path curving away between hills,
scattered round trees, small wooden signposts, wildflowers, distant blue mountains, big soft
white clouds in a bright blue sky. Rich detail along the TOP and BOTTOM edges, and the MIDDLE
of the image calm and uncluttered open grass so interface panels can sit over it. No characters,
no buildings in the centre. No text, no letters, no watermark.
```

### B — `bg_reading.png` → Reader + Arrange + Summary (the quiet working screens)

Save to `Assets/_Game/Resources/UI/bg_reading.png`.

```
Vertical portrait children's storybook background, glossy 2D cartoon style, kids' mobile game,
warm and calm, soft afternoon light. A cosy reading nook seen from across the room: a sunlit
window with gentle curtains, a bookshelf with colourful books, a potted plant, a soft rug, warm
wooden floor. Muted and low-contrast overall so text panels stay readable on top. Detail around
the TOP and BOTTOM edges, MIDDLE of the image soft, plain and uncluttered. No characters, no
desk in the centre. No text, no letters, no watermark.
```

Leave `NameEntry` and `TeacherMenu` on the current `bg_storyselect.png` — that keeps the park
image in the game and stops it being wasted.

---

## After you save them

Tell me and I will wire them up. It is a small scene change per screen (swap the `Sky` Image's
sprite), and I will check contrast on each one before and after — the reading screens especially,
since body text sits on them and the whole study depends on that text being legible.
