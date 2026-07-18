# SummaRace — AI Image Generation Prompts

Central list of image prompts for the game. Owner generates via **ChatGPT / Gemini**, saves the
PNG, and Claude wires it into Unity (importer → Sprite Single, correct Resources path).

## How we work
1. Copy a prompt below into ChatGPT/Gemini. Ask for **portrait 1080×1920** (backgrounds) or the size noted.
2. Generate 3–4 variants, pick the one matching the acceptance notes.
3. Save to `Downloads` (any name) and tell Claude the path + which slot it fills.
4. Claude imports it, fixes the importer (these packs often import as broken `Multiple`-mode sprites → must be `Single`), and swaps it in.

## Shared style DNA (paste this line into EVERY prompt so images match)
> glossy 2D cartoon vector style, casual kids' mobile game, saturated candy colors (sky blue, emerald green, warm gold), soft shiny highlights, chunky rounded shapes, clean bold edges, cheerful and friendly — no text, no letters, no watermark.

## ⚠️ GDD D6 rule — before generating all 30 story images
The 30 story hero images must have a style the **researcher approves first** (GDD decision D6).
So: lock the style with ONE great "The Playground" hero, get owner/researcher sign-off, THEN batch the other 29.

---

## ✅ DONE
| Slot | File | Status |
|---|---|---|
| Boot / loading splash | `Assets/_Game/Resources/UI/bg_splash.png` | done (park + trail + chest + books) |
| MainMenu background | `Assets/_Game/Resources/UI/bg_menu.png` | done (tree-framed park + ground band + chest) |

---

## 🎯 NEXT: Story hero image — "The Playground" (s01_easy)
**Slot:** `Assets/_Game/Resources/Stories/Art/s01_easy.png` (replaces the current TEMP white crop)
**Size:** square, ~1024×1024 (it sits in a card, roughly square)
**This one sets the style for all 30 — judge it carefully.**

```
Square children's storybook illustration, glossy 2D cartoon vector style, casual kids'
mobile game art, saturated candy colors, soft shiny highlights, chunky rounded shapes,
clean bold edges, cheerful and friendly. Scene: a bright sunny school PLAYGROUND — a
happy young child near colorful playground equipment (swings and a slide), green grass,
a few flowers, blue sky with puffy clouds. Warm, welcoming, storybook mood. Centered
simple composition that reads clearly at small size. No text, no letters, no watermark.
```
**Acceptance:** reads clearly when small, one clear child + playground, not too busy, matches the menu/splash art family.

---

## 🎯 Reader / activity background (cozy classroom)
**Slot:** `Assets/_Game/Resources/UI/bg_classroom.png`
**Size:** portrait 1080×1920
**Used by:** Reader (and optionally Arrange/Summary). Ms. Lumi + the story card sit in the
center, so keep the MIDDLE calm/empty; detail on the upper walls + bottom floor only.

```
Mobile game background, portrait 9:16, glossy 2D cartoon vector style, casual kids'
mobile game, saturated warm candy colors, soft shiny highlights, chunky rounded shapes,
clean bold edges, cheerful and cozy. A bright cheerful CLASSROOM / story-time corner:
soft pastel wall in the upper two thirds with a sunny window showing blue sky and a
tree outside, a colorful alphabet or picture banner high on the wall, a shelf of
rounded colorful books in a corner, a warm wooden floor across the bottom with a soft
round rug. The whole CENTER of the image stays calm and uncluttered — a story card and
a teacher character will sit there. No people, no text, no letters, no watermark.
High resolution, portrait 1080x1920, crisp clean edges.
```
**Acceptance:** cozy classroom, calm empty center for the card + teacher, warm and inviting,
same art family as the menu/splash.

## 🎯 StorySelect background
**Slot:** `Assets/_Game/Resources/UI/bg_storyselect.png`
**Size:** portrait 1080×1920
**Note:** a large UI board covers the CENTER of the screen, so keep the middle simple —
detail lives at the top strip and the bottom/side edges only.

```
Mobile game level-select screen background, portrait 9:16, glossy 2D cartoon vector style,
casual kids' mobile game, saturated candy colors (sky blue, emerald green, warm gold), soft
shiny highlights, chunky rounded shapes, clean bold edges, cheerful and friendly. A bright
sunny park: blue sky with a few puffy clouds across the TOP, soft rolling green hills and a
row of round leafy trees along the far horizon, a grassy foreground at the very bottom with
small flowers, bushes and a tiny golden treasure chest tucked in a bottom corner. The whole
CENTER of the image is a calm, gently uniform grassy park with no big objects — it will be
covered by a UI board, so keep it clean and uncluttered there. No text, no letters, no
characters, no watermark. High resolution, portrait 1080x1920, crisp clean edges.
```
**Acceptance:** top has sky+clouds, bottom has a grassy edge with small props, and the MIDDLE
is calm/empty (a board sits there). Same art family as the menu/splash.

---

## LATER (Phase G) — remaining 29 story hero images
Generate only after the researcher approves the "Playground" style. Each uses the same
square storybook prompt above with the scene swapped for that story. Fill this table as stories are authored:

| id | title | scene one-liner for the prompt |
|---|---|---|
| s01_easy | The Playground | (done above) |
| … | … | … |

## LATER (Phase G) — SessionMap background
The winding-path level map (10 numbered stops). Separate scene, currently empty.
```
Mobile game level-map background, portrait 9:16, [STYLE DNA]. A winding tan dirt path
snaking from the bottom of the screen up into sunny green park hills in the distance,
with open flat spots along the path where numbered level buttons will sit. Trees, bushes,
flowers, a small river with a little bridge, a treasure chest near the end of the path.
Leave the path spots uncluttered for UI level nodes. No text, no numbers, no characters.
```

## ✅ DONE — Teacher avatar (Ms. Lumi)
**Already generated — do NOT re-generate.** 3 poses exist in `Assets/Art/MsLumi/`:
- `mslumi_1.png` — waving (cut to transparent → `Resources/UI/mslumi_wave.png`, used in Reader)
- `mslumi_2.png` — both hands up, cheering (for correct-answer reactions)
- `mslumi_3.png` — third pose
Plus `Assets/Art/Avatars/avatar_1..5.png`. Old TEMP crop `teacher_temp.png` is superseded.
**To add MORE poses** (pointing, thinking, thumbs-up), generate stills in the SAME style on a
plain WHITE background (Claude cuts white→transparent via edge flood-fill). Do NOT use video —
sprite poses only (lightweight + offline). Match the existing Ms. Lumi look:
```
Friendly female teacher, tan skin, brown hair in a low bun, sage-green long-sleeve shirt,
flat 2D cartoon vector style with bold black outlines, warm encouraging smile, [POSE:
e.g. pointing to the side / thinking with finger on chin / thumbs up], simple waist-up
composition on a plain solid WHITE background. No text, no watermark.
```
