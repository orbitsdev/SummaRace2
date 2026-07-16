# SummaRace — Asset & Requirements List (Bill of Materials)

> **Purpose:** One list of *everything* the game needs (art, audio, fonts, VFX), where to get it **free**, and what we **already own** so we never download twice.
> **Budget rule:** ₱0 — everything must be free for commercial/academic use (CC0 preferred). **Always confirm each asset's license before shipping** — this is a thesis, so credit and licensing matter.
> **Theme locked:** Summer / green / playground (matches final mockups & stories). ❄️ Snow assets are **not** needed.
> **Status:** Planning — nothing downloaded yet.

**Legend:** 🔴 Must-have (game won't work without it) · 🟡 Important · 🟢 Nice-to-have
**Own?:** ✅ already in project · 🔶 partially (need to pick/adapt) · ❌ need to get

---

## Rule 0 — Shop our own shelf first

We already imported these packs in the demo. **Check here before downloading anything new:**

| Owned pack | Use it for |
|---|---|
| RPG Tiny Hero Duo | Player character candidate |
| Easy Chara | Character rigging/framework |
| RPG Monster Buddies | Enemy/patrol candidate (may be too "monster" — see §1) |
| Kenney Nature Kit | Trees, rocks, park props |
| SimpleNaturePack, Trees Package Lite, Palmov Island | Track scenery (great for green park) |
| Customizable Skybox, Skyden_Games | Blue sky / clouds |
| Cartoon UI, 2D Progress Bar Toolkit, Buttons | Menu & HUD UI kit |
| Pandazole Ultimate Pack, 300Mind, HONETi | Extra models |
| BOXOPHOBIC | Shader / LOD utilities |
| ~~Realistic Hail Set~~ | ❌ Not needed (we're summer, not snow) |

**Custom assets we already made:** logo, splash background, progress-bar gradient+stripes (2D); story music, mission music, UI click (audio); AnswerCard, TrackSegment, Checkpoint (prefabs).

---

## 1. 3D Characters & Animations

| # | Item | Priority | Own? | Free source | Notes |
|---|---|---|---|---|---|
| 1.1 | **Player** — running schoolkid w/ backpack | 🔴 | 🔶 | **Mixamo**, **Quaternius** (Ultimate Animated Character), owned RPG Tiny Hero | Mockup = girl, ponytail, orange backpack. Needs: run, idle, jump/dodge, "caught/sad" anims |
| 1.2 | **Patrol** — the friendly chaser | 🔴 | 🔶 | **Mixamo** (guard/police-ish rig), **Quaternius** | Keep it **friendly, never scary** (north-star rule). Owned Monster Buddies may read too scary |
| 1.3 | **Ms. Lumi** — guide/teacher character | 🟡 | ❌ | 2D portrait (see §3, AI-consistent) | Appears in TeacherWelcome + guide speech bubbles. Likely **2D illustration**, not 3D |
| 1.4 | **4 avatars** (NameEntry pick) | 🟡 | 🔶 | 2D character icons — AI set or Kenney characters | Must feel like "me" to a Grade 4 kid; diverse Filipino-friendly faces |
| 1.5 | **Character animations** (run/idle/jump/collect/caught) | 🔴 | ❌ | **Mixamo** (free, huge library) | Mixamo auto-rigs + animates any humanoid; retarget to chosen model |

> **Tip:** Pick **one** character source/style and stick to it so player + patrol + avatars look like one world. Mixamo + Quaternius mix well (both stylized low-poly friendly).

---

## 2. 3D Environment & Track (the run)

| # | Item | Priority | Own? | Free source | Notes |
|---|---|---|---|---|---|
| 2.1 | Track/path segments | 🔴 | ✅ | TrackSegment prefab exists | Reskin to grassy path (mockup: blue/green/yellow lanes) |
| 2.2 | Trees, bushes, flowers | 🟡 | ✅ | Kenney Nature Kit, Palmov Island, Quaternius | Already own plenty |
| 2.3 | Fences, benches, park props | 🟡 | 🔶 | Kenney, **Poly Pizza** | Mockups show fences + benches lining the track |
| 2.4 | Playground bg (swings/slide) | 🟢 | ❌ | Poly Pizza, Kenney | Background dressing, low priority |
| 2.5 | Skybox (blue sky + clouds) | 🟡 | ✅ | Customizable Skybox (owned) | Bright, cheerful daytime |
| 2.6 | Patrol vehicle (optional) | 🟢 | ❌ | Poly Pizza, Kenney | Mockup shows a patrol car; optional flavor |

---

## 3. 2D Story Illustrations ⚠️ (the biggest job)

| # | Item | Priority | Own? | Free source | Notes |
|---|---|---|---|---|---|
| 3.1 | **Story page art** — up to 5 pages × 30 stories | 🔴 | ❌ | **AI image generation** (consistent style) | This is the #1 art risk. 30 stories need consistent, culturally-appropriate cartoon art |
| 3.2 | Level-select story cards | 🟡 | 🔶 | Reuse story art thumbnails | Mockup shows Easy/Average/Hard cards w/ art |
| 3.3 | Ms. Lumi portrait + expressions | 🟡 | ❌ | Same AI style as 3.1 | Reuse across all guide moments |

> **⚠️ Decision needed — story illustrations.** 30 stories is a lot of art. Realistic free options:
> 1. **AI-generated in one locked style** (fastest, consistent — the mockups already look this way). Recommended.
> 2. Start with **1 illustration per story** (title scene) instead of 5 pages each → cuts art by 80%, still looks great.
> 3. Text-only reader for pilot, add art later. (Weakest for young readers.)
> **My rec:** AI style + 1 hero image per story for v1; expand to per-page later if time allows.

---

## 4. UI Kit & Icons

| # | Item | Priority | Own? | Free source | Notes |
|---|---|---|---|---|---|
| 4.1 | Buttons, panels, popups | 🔴 | ✅ | Cartoon UI, Buttons (owned) | Reskin to green palette |
| 4.2 | Progress bar ("1/5 pages") | 🔴 | ✅ | 2D Progress Bar Toolkit (owned) | |
| 4.3 | Answer cards (A/B/C) | 🔴 | ✅ | AnswerCard prefab (owned) | Green=correct, tan=option (per mockup) |
| 4.4 | Star rating (1–3 stars) | 🔴 | 🔶 | Kenney UI Pack, owned UI | Victory + results screens |
| 4.5 | Icons: speaker/mute, settings, back, undo, verify | 🟡 | ❌ | **Kenney UI/Game Icons** (CC0) | Mockups show speaker+mute top-right |
| 4.6 | SWBST element icons | 🟡 | ❌ | Kenney icons or AI | Small icons for Somebody/Wanted/But/So/Then pieces |
| 4.7 | Badges / celebration graphics | 🟢 | ❌ | Kenney | GameComplete screen |

---

## 5. Fonts

| # | Item | Priority | Own? | Free source | Notes |
|---|---|---|---|---|---|
| 5.1 | Kid-friendly rounded display font | 🔴 | ❌ | **Google Fonts**: Fredoka, Baloo 2, Nunito, Quicksand | Must be **highly legible** — learners have low reading mastery |
| 5.2 | Readable body font (story text) | 🔴 | ❌ | **Google Fonts**: Nunito / Open Sans | Bigger size, high contrast for Grade 4 |

> All Google Fonts are free for commercial use. Import into Unity via TextMeshPro font assets.

---

## 6. Music

| # | Item | Priority | Own? | Free source | Notes |
|---|---|---|---|---|---|
| 6.1 | Menu / UI loop | 🟡 | ❌ | **Pixabay Music**, **Incompetech (Kevin MacLeod)**, **FreePD** | Light, cheerful |
| 6.2 | Story-reading loop (calm) | 🟡 | ✅ | owned story music | |
| 6.3 | Mission/race loop (energetic) | 🟡 | ✅ | owned mission music | |
| 6.4 | Victory / celebration jingle | 🟢 | ❌ | Pixabay, FreePD | Short sting for stars/victory |

---

## 7. Sound Effects (SFX)

| # | Item | Priority | Own? | Free source | Notes |
|---|---|---|---|---|---|
| 7.1 | UI tap/click | 🔴 | ✅ | owned + **Kenney Interface Sounds** | (your link — CC0) |
| 7.2 | Correct answer chime | 🔴 | ❌ | **Kenney Interface/UI Sounds** | Warm, positive |
| 7.3 | "Not quite" soft sound | 🔴 | ❌ | Kenney | **Never harsh** — gentle, not a buzzer |
| 7.4 | Collect item pop | 🔴 | ❌ | Kenney, **Freesound** | On SWBST pickup |
| 7.5 | Lane-switch whoosh | 🟡 | ❌ | Freesound, **Mixkit** | |
| 7.6 | Star earned | 🟡 | ❌ | Kenney | |
| 7.7 | Patrol-approaching / caught | 🟡 | ❌ | Freesound | Tense but **friendly**, cartoonish |
| 7.8 | Page turn | 🟢 | ❌ | Kenney | Story reader |
| 7.9 | Summary success | 🟡 | ❌ | Kenney | On submit |

---

## 8. Narration (produced, not downloaded)

| # | Item | Priority | Own? | Source | Notes |
|---|---|---|---|---|---|
| 8.1 | Story page voice-over | 🟡 | ❌ | **TTS for pilot**, human voice for final | Thesis says narration is **optional/toggleable** — de-risked. Strongly recommended for low readers |
| 8.2 | Question/option read-aloud | 🟢 | ❌ | Same as 8.1 | Helps struggling readers |

> **Volume note:** up to 30 stories × 5 pages ≈ 150 clips. For the **pilot**, generate with free TTS; record a human voice only if the final version needs it. AudioManager already supports narration playback.

---

## 9. VFX / Particles

| # | Item | Priority | Own? | Free source | Notes |
|---|---|---|---|---|---|
| 9.1 | Collect sparkle | 🟡 | 🔶 | Unity built-in particles, Kenney | On correct pickup |
| 9.2 | Star burst | 🟡 | 🔶 | Unity particles | Victory |
| 9.3 | Run dust trail | 🟢 | 🔶 | Unity particles | Under player feet |

> Keep VFX simple and hand-made in Unity's Shuriken — no paid packs needed. (Owned BOXOPHOBIC helps with shaders.)

---

## Free Source Master List (verify license per asset)

| Source | Best for | License | Link |
|---|---|---|---|
| **Mixamo** | Rigged 3D humanoids + animations | Free (Adobe acct) | mixamo.com |
| **Quaternius** | Low-poly 3D characters, nature, animations | CC0 | quaternius.com |
| **Kenney** | UI, icons, **interface sounds**, some 3D, particles | CC0 | kenney.nl |
| **Poly Pizza** | Free low-poly 3D models (props, vehicles) | CC0 / CC-BY | poly.pizza |
| **Freesound** | SFX library | CC0 / CC-BY (varies) | freesound.org |
| **Mixkit** | SFX + music | Free license | mixkit.co |
| **Pixabay** | Music + SFX + images | Free | pixabay.com |
| **Incompetech** | Background music (Kevin MacLeod) | CC-BY | incompetech.com |
| **FreePD** | Public-domain music | CC0 | freepd.com |
| **Google Fonts** | Fonts | Open (OFL) | fonts.google.com |
| **OpenGameArt** | Mixed 2D/3D/audio | Varies — check each | opengameart.org |
| **itch.io (free assets)** | Mixed game assets | Varies — check each | itch.io |

> ⚠️ **CC-BY** (like Incompetech, some Poly Pizza/Freesound) requires **crediting the author** — keep a running credits list from day one. **CC0** requires nothing but is still polite to credit.

---

## Two things to decide before we source anything

1. **Story illustrations (§3):** AI-consistent style? 1 hero image per story or full 5 pages? → biggest scope lever.
2. **Character set (§1):** assemble from owned packs, or go Mixamo/Quaternius for a cleaner unified look? → decides the whole visual feel.

## Suggested order of work

1. Lock the two decisions above.
2. **Build the "credits & licenses" tracker** now (a simple sheet) — log every asset as we grab it.
3. Source the **vertical-slice set only first**: 1 player, 1 patrol, track/skybox (owned), UI (owned), 1 font, core SFX (Kenney), 1 story's art. Enough to finish "The Playground" end-to-end.
4. Once the slice looks right, **batch-source** the rest against this list.

---
*This list is a living document — tick items, add links, and note the license beside each asset as we collect them.*
