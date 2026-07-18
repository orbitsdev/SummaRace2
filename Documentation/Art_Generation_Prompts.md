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

## LATER — Teacher avatar (Ms. Lumi)
Currently a TEMP crop from mockup 21. Used on Arrange + Summary speech bubbles.
```
Friendly female teacher character portrait, glossy 2D cartoon vector style, casual kids'
mobile game, warm and encouraging expression, waving or pointing gesture, simple bust
composition on transparent background. Saturated friendly colors. No text, no watermark.
```
**Size:** square with transparent background (PNG), ~512×512.
