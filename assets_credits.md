# SummaRace — Asset Credits & Licences

**Date: 2026-08-06 · verified against commit `53d6e9d` (branch `experiment/endless-override-2`)**

*Audit basis: the sweep ran against `2b7a860`; HEAD advanced to `53d6e9d` during the pass. Re-checked
— the two intervening commits touched **12 `.cs` files and regenerated one narration clip**
(`vo_race_briefing.mp3`, same edge-tts recipe, §6). **No third-party asset was added, removed or
changed**, so every finding below holds at `53d6e9d`.*

Required by GDD §6.8: *"A credits tracker (`assets_credits.md` in the repo) logs every asset the
moment it enters the project: name, author, source URL, license, where used. CC0 preferred; CC-BY
allowed with attribution in the app."*

This file was built **from the repository contents**, not from the other documents. Every licence
claim below is backed by a file that exists in this repo and is quoted where it matters. Where no
such file exists, the entry says **UNVERIFIED** and names exactly what was looked at. Two of the
project's own claims turned out to be wrong and are corrected in place (§7).

**Scope note.** This tracker was written 40 sessions into the project, not "the moment each asset
entered". Everything here is reconstructed from files on disk, content hashes and `git log`. That
reconstruction is complete for what ships, but it cannot recover a source URL that was never
written down — which is why §3 has two entries that may not be resolvable at all.

---

## 1. Read this first — the owner's summary

### 1a. Three things to decide before the APK goes on 40 tablets

| # | Issue | Why it matters | §
|---|---|---|---|
| **1** | **This GitHub repo is public and contains ~700MB of Asset Store packs, Unity sample art and Mixamo FBX files.** | Verified today: `https://api.github.com/repos/orbitsdev/SummaRace2` returns `"private": false`. Asset Store licences permit shipping assets *inside a built application*; they generally do **not** permit publishing the source assets where anyone can download them. **This is live right now and is independent of the study.** | §2 |
| **2** | **Two audio files that ship in the app have no traceable source.** `music_menu.mp3` (6.5MB, plays on MainMenu / StorySelect / SessionMap) and `sfx_click.mp3` (the click on nearly every button). Neither matches any pack in this repo. | A thesis instrument cannot document a licence it does not have. Both are replaceable from Kenney (CC0, already on disk) in about ten minutes. | §3 |
| **3** | **The 160 narration clips were generated with `edge-tts`,** which drives Microsoft Edge's read-aloud endpoint. Narration is the accessibility support the study depends on — it is not decoration. | Microsoft's neural voices are licensed through Azure Speech, with terms about redistributing synthesized audio. `edge-tts` is an unofficial client for an endpoint intended for the browser's own read-aloud feature. **No licence grant for this use exists in this repo or, as far as this audit can tell, anywhere.** | §6 |

Items 1 and 3 are the two that a supervisor or committee could reasonably ask about. Item 2 is the
cheapest to fix and the easiest to defend once fixed.

### 1b. Which licences require attribution *inside the app*

**Verified: there is no credits screen in this game, and no code or string that could become one.**
Checked and confirmed — this is not assumed:

- `ProjectSettings/EditorBuildSettings.asset` holds **12 scenes**, none named Credits or About.
- `Assets/_Game/Scripts/Constants/GameText.cs` contains no credits, licence or attribution string.
- `grep -rniE "credit" Assets/_Game/Scripts/` returns only three unrelated code comments about
  scoring ("credited 1/k", "credited with the distractor").
- `Assets/Scripts/UI/LicenceDisplayer.cs` **is Trash Dash's** licence-acceptance popup, not ours.
  Its guid `acacd8c3bf2793f4a8da0727706d7f03` appears in **no built scene** — it is dead code.

Against that, here is what the shipped licences actually demand:

| Licence | Packs / assets | Attribution obligation | Satisfied today? |
|---|---|---|---|
| **CC0 1.0** | All Kenney audio | **None.** *"Credit … would be nice but is not mandatory."* | ✅ n/a |
| **SIL OFL 1.1** | Fredoka, Nunito, Baloo 2, Anton, Bangers, Oswald, Unity, Liberation Sans | **Yes — but not in the UI.** OFL §2: *"Original or Modified Versions of the Font Software may be bundled, redistributed and/or sold with any software, **provided that each copy contains the above copyright notice and this license**."* A copy of the notice must travel with the APK. | ❌ **No** |
| **Apache License 2.0** | Luckiest Guy (Trash Dash's font), Roboto-Bold (TMP examples) | **Yes.** §4(a) requires giving recipients a copy of the Licence. | ❌ **No** |
| **Unity Asset Store EULA** | Most packs (§4) | **No attribution required.** Kevin Iglesias states it outright: *"Attribution not required."* | ✅ n/a |
| **Unity Companion License** | StarterAssets (does not ship) | n/a — not shipped | ✅ n/a |
| **PrimeTween custom** | PrimeTween | None for binary distribution | ✅ n/a |
| **MIT** | Kino Bloom (does not ship — JMO demo assets) | Would require notice if shipped | ✅ n/a |

**So: the only unmet in-app obligation is fonts** — OFL and Apache 2.0, both of which want the
notice distributed *with the software*, not displayed on screen.

### 1c. The minimum fix

Neither of these needs a credits screen, and neither is a code change of any size:

1. **Ship this file.** Copy `assets_credits.md` (or a fonts-only extract) to
   `Assets/_Game/Resources/credits.txt`. Anything under a `Resources/` folder ships unconditionally
   inside the APK, which satisfies "each copy contains the notice" without any UI work at all.
   *Cost: one file copy.*
2. **Delete `Assets/TextMesh Pro/Examples & Extras/`.** It ships **2.7MB via its own `Resources/`
   folder**, is used by nothing, and is the sole reason Anton, Bangers, Oswald, Roboto-Bold, Unity
   and Electronic Highway Sign are in the APK at all. Deleting it removes **five fonts' worth of
   obligation and one UNVERIFIED entry** and makes the build smaller.
   *Cost: one folder delete. Do it before the icon/atlas rebuild, not after.*

After those two steps the shipped font set is Fredoka, Nunito, Baloo 2 and Luckiest Guy, and this
file covers all four.

---

## 2. The distribution question — the largest single finding

**The repository is public.** Verified 2026-08-06 against the GitHub API:

```
GET https://api.github.com/repos/orbitsdev/SummaRace2  ->  200
"private": false      "visibility": "public"      "fork": false
```

Nothing in `.gitignore` excludes any asset pack — `git ls-files` tracks all of them. The repo
therefore publishes, in extractable original form:

- ~15 Unity Asset Store packs (§4, §5), including paid ones;
- the whole of Unity's Trash Dash / Endless Runner sample art, models, materials and music (§6);
- Mixamo character and animation FBX files (§8);
- `Assets/Plugins/PrimeTween/internal/com.kyrylokuzyk.primetween.tgz`.

Two of those are worth stating precisely, because their own licence text in this repo speaks to it:

> **PrimeTween** — `package/license.md` inside the tarball: *"It's **not allowed**: — Distribute
> PrimeTween's source code and tarball (.tgz) archive inside derivative products."* The `.tgz` is
> committed at `Assets/Plugins/PrimeTween/internal/com.kyrylokuzyk.primetween.tgz`. Using PrimeTween
> in the APK is explicitly allowed; publishing its tarball is the part that is not.

> **Mixamo** — Adobe permits use of Mixamo characters and animations in projects, but not
> redistribution of the assets themselves as standalone files. `Aj.fbx`, `Ty.fbx`,
> `Ch46_nonPBR.fbx` and twelve animation FBXs are committed as standalone files (§8).

**This is a repo-hosting question, not a study question.** Installing the compiled APK on 40 tablets
is the use these licences are written for and is not in doubt. Publishing the raw assets is a
separate act, and it is happening now.

**Options, cheapest first:** flip the repo to **private** (one setting, no history rewrite, resolves
every item above at once) · or strip the packs and re-add them via Package Manager. The exact terms
are in the Unity Asset Store EULA at `https://unity.com/legal/as-terms`, which is **not** in this
repo — the two packs that cite it (JMO Assets, Supercyan) link to it rather than reproducing it, so
the operative clause has **not** been read as part of this audit and the owner should read it before
acting. What is established here is the fact pattern, not the legal conclusion.

---

## 3. Audio — every shipped file traced by content hash

`Assets/Audio/Kenney/` is the **raw source library**: 479 `.ogg` files, **0 external GUID references
project-wide**, and not under a `Resources/` folder — so **the library itself does not ship**. What
ships is the 20 files copied into `Assets/_Game/Resources/Audio/`. Each was matched back to its
source by MD5, not by filename.

**Kenney Vleugels — Kenney.nl — Creative Commons Zero (CC0 1.0)** · `http://creativecommons.org/publicdomain/zero/1.0/`
Licence text: `Assets/Audio/Kenney/<pack>/License.txt`, one per pack, all six verified to say CC0:
> *"License (Creative Commons Zero, CC0) … You may use these assets in personal and commercial
> projects. Credit (Kenney or www.kenney.nl) would be nice but is not mandatory."*

| Shipped file | Source pack | Source file | Used for |
|---|---|---|---|
| `sfx_boost.ogg` | digital-audio | `powerUp2.ogg` | race speed boost |
| `sfx_not_quite.ogg` | digital-audio | `lowDown.ogg` | wrong answer |
| `sfx_slot_wiggle.ogg` | digital-audio | `lowRandom.ogg` | Arrange, wrong slot |
| `sfx_transition.ogg` | digital-audio | `highUp.ogg` | scene transitions |
| `sfx_caught.ogg` | impact-sounds | `impactSoft_medium_000.ogg` | patrol menace beat |
| `sfx_footstep_a/b/c.ogg` | impact-sounds | `footstep_grass_000/001/002.ogg` | runner footsteps |
| `sfx_correct.ogg` | interface-sounds | `confirmation_002.ogg` | correct answer |
| `sfx_pop.ogg` | interface-sounds | `pluck_001.ogg` | panel pop-in |
| `sfx_press.ogg` | interface-sounds | `drop_004.ogg` | button press |
| `sfx_star.ogg` | interface-sounds | `glass_002.ogg` | Results stars, unlock |
| `sfx_coin.ogg` · `sfx_collect.ogg` | rpg-audio | `handleCoins.ogg` | coin pickup, collect (**byte-identical to each other** — same file shipped twice, ~25KB each) |
| `sfx_page_turn.ogg` | rpg-audio | `bookFlip2.ogg` | Reader page turn |
| `sfx_slot_lock.ogg` | rpg-audio | `metalLatch.ogg` | Arrange slot locks |
| `sfx_whoosh.ogg` | rpg-audio | `cloth2.ogg` | card fly-up |
| `music_victory.ogg` | music-jingles | `Pizzicato jingles/jingles_PIZZI07.ogg` | Results sting |

All 15 rows verified byte-identical to the named source (`music_victory.ogg` = `jingles_PIZZI07.ogg`,
MD5 `c1295c7f…`, 19,137 bytes each). `kenney_ui-audio` is on disk but **contributed nothing** to the
shipped set.

### The two that could not be traced

| File | Evidence gathered | Status |
|---|---|---|
| **`music_menu.mp3`** (6,456,423 bytes) | MD5 `700816b7…` matches **nothing** in `Assets/Audio/Kenney/` (479 files) or `Assets/Sounds/` (35 files). No ID3v1 or ID3v2 tag — the only embedded string is the encoder, `LAME3.98`. 320kbps **stereo**, a desktop-rip bitrate rather than a game-asset one. Added in `b1e8b27` "Phase C: core systems + Boot/MainMenu playable" with no source recorded in the commit. `SummaRace_Audio_Audit.md` line 302 independently marks it **"(not Kenney)"**. | **UNVERIFIED — owner must confirm before distribution.** Ships; plays on MainMenu, StorySelect and SessionMap. |
| **`sfx_click.mp3`** (11,702 bytes) | MD5 `7f90c47f…` matches nothing on disk. No ID3 tag. Added in `f9aa714`, whose message reads *"Audio: sfx_click -> owner's mouse-click.mp3 (replaces Kenney click)"* — so it is owner-supplied, but the commit records no origin for the owner's file. | **UNVERIFIED — owner must confirm before distribution.** Ships; the click on nearly every button. |

**Recommended fix (10 minutes, no code change).** `AudioManager` loads by filename, so replacing the
file is the whole job. `sfx_click` → any Kenney `interface-sounds` click, already on disk and CC0.
`music_menu` has no CC0 loop on disk (Kenney ships jingles ≤3s, not loops) — either source one, or
reuse `music_race.ogg`, which is already licence-documented (§6). `SummaRace_Audio_Audit.md` D8
separately wants `music_menu` re-encoded anyway (gapped loop, 28MB resident), so both jobs are one job.

---

## 4. Unity Trash Dash / Endless Runner sample — the largest borrowed body of content

**Author: Unity Technologies.** In-repo licence statement — `Assets/EndlessRunner_Third-PartyNotice.txt`,
quoted in full:

> *"This asset is governed by the **Asset Store EULA**; however, the following components are
> governed by the licenses indicated below:*
> *A. Luckiest Guy Font — Apache License, Version 2.0, January 2004, http://www.apache.org/licenses/"*

An identical copy (MD5 `53a30ba1…`) sits at `Assets/Plugins/TrashDash Art/EndlessRunner_Third-PartyNotice.txt`.

⚠️ **Correction to the project's working assumption.** It has been described internally as **Unity
Companion Licence**. **The only licence statement in this repo says Asset Store EULA.** The
distinction is real: the Endless Runner sample was distributed both on the Asset Store and on GitHub,
under different terms. **The notice that shipped with the copy actually in this repo says Asset Store
EULA**, so that is what is recorded here. For comparison, `Assets/Plugins/StarterAssets/license.txt`
*does* say Unity Companion License — so the project contains both, and they are not interchangeable.
If the Companion Licence is the one wanted (it is the more permissive of the two for redistribution),
the owner must establish that from the download source, not from this repo. **Status of that specific
question: UNVERIFIED.**

⚠️ **`Assets/Plugins/TrashDash Art/` contains no art.** It holds exactly one file — the notice above.
CLAUDE.md's F23 row describes it as the source of clouds and road materials; that is wrong. The Trash
Dash content actually lives at the `Assets/` root, **without the notice alongside it**:

| Path | What it is | Ships via |
|---|---|---|
| `Assets/Bundles/Themes/` | Day + NightTime themes, all zone segment prefabs | Addressables (groups `Themes`, `Default-Zones`, `Night-Zones`, `Day/Night-Theme-Obstacles`, `Default/Night-Sky`) |
| `Assets/Bundles/Characters/Cat/character.prefab` | the runner prefab (cat meshes stripped, our Mixamo kid inside) | Addressables (`Characters`) |
| `Assets/Models/`, `Assets/Textures/`, `Assets/Materials/` | road segments, buildings, props, clouds | Addressables (incl. 109-entry `Duplicate Asset Isolation`) |
| `Assets/Shaders/` | `CurvedUnlitCloud.shader` and siblings | Addressables |
| `Assets/Animation/`, `Assets/Prefabs/`, `Assets/Tutorial/`, `Assets/UI/` | their animator graphs, prefabs, UI chrome | scene + Addressables |
| `Assets/Sounds/` | music stems, SFX, mixers | scene |
| `Assets/Font/LuckiestGuy.ttf` + `FontLicense.txt` | their display font | Addressables |
| `Assets/Scripts/` | their runtime code (`TrackManager`, `GameState`, …) | compiled into the player |

**`music_race.ogg` — stated plainly.** `Assets/_Game/Resources/Audio/music_race.ogg` is
**byte-identical** to `Assets/Sounds/Stems/STEMSMainTrackMono.ogg` — MD5 `ab3867fa20ede410335dcc707675cc16`,
7,277,424 bytes each. It is Unity's Trash Dash main music track, copied under our own filename, and
**both copies ship** (6.9MB of pure duplication). Terms are the Asset Store EULA notice above. It
plays for the whole race and is stopped at FINISH so the victory sting owns Results.

**Luckiest Guy font — Apache License 2.0.** Full licence text at `Assets/Font/FontLicense.txt`
(11,560 bytes, verified Apache 2.0). Ships via Addressables. Apache §4(a) requires that recipients
get a copy of the Licence — see §1c.

---

## 5. Asset Store packs that ship in the APK

Reachability was established by **full transitive GUID closure** — a `guid → path` map built from all
5,291 `.meta` files, then walked out of every text-serialized asset (`.unity`, `.prefab`, `.asset`,
`.mat`, `.controller`, `.anim`, …) plus every asset's `.meta` (to catch FBX material remaps), seeded
from the 12 build scenes, all Addressables groups, and every `Resources/` folder. No pack was sampled.

| Pack | Author / vendor | Source URL in repo | Licence **as stated by a file in this repo** | Where used |
|---|---|---|---|---|
| **Hyper_Casual_UI** | *not stated* | — | **Sprites: UNVERIFIED.** Only licence file is `Fonts/license.txt` (Baloo 2, OFL 1.1). Grepped every file including binary PNGs for `copyright\|http\|www.\|Licen\|EULA` — the *only* hits in the pack are the two URLs inside that font licence. | **All 12 scenes** — pill buttons, panels, icons, lock icons |
| ↳ *Baloo 2 font* | The Baloo 2 Project Authors | `https://github.com/EkType/Baloo2` | **SIL OFL 1.1** — *"Copyright 2019 The Baloo 2 Project Authors"* | kit sprites carry baked labels; TMP uses Fredoka/Nunito |
| **Hovl Studio** — Magic effects pack | *not stated* | — | **UNVERIFIED.** Only file is a 3-line `Demo scene/Readme.txt` about Bloom. Zero `.cs`, zero `.shader`, no copyright string anywhere; vendor known from folder name only. | `MainSummaRace` — answer-card glow pads, collect burst, finish portal |
| **BitGem** — Smashy Craft Series | BitGem | — | **UNVERIFIED.** No licence file of any kind (8 assets total). Vendor confirmed only from an FBX build path: `…/BITGEM_products/_smashy_craft_series/city/police_man/models/cop.fbx` | `MainSummaRace` — the patrol cop |
| **Kevin Iglesias** — Human Basic Motions 2.4 FREE | Kevin Iglesias | `https://www.keviniglesias.com/#license` | **Stated.** `Human Basic Motions 2.4 FREE.pdf`: *"License: Standard Asset Store EULA \| Fab Standard License · Royalty-free and allowed for commercial use. · Resale not allowed. · **Attribution not required**."* | Addressables — animation clips in `KidCharacterAnimation.controller` |
| **Layer Lab** — 2D Icons / Casual Icon Pack | Layer Lab | — | **UNVERIFIED.** 46 PNGs, no licence file, and the PNGs carry **no metadata at all** (binary-grepped for `Creator\|Author\|dc:creator\|copyright` — zero hits). Vendor from folder name only. | Results (trophy), NameEntry |
| **"Gems and gold"** — real name *Coins, Crystals, Diamonds vector icons for IAP* | unnamed; `pirate.parrot.software@gmail.com` | `http://u3d.as/1LSz` (in `Readme.pdf`) | **UNVERIFIED.** Readme gives store URL + contact but **states no licence terms**. | MainMenu (2 sprites), Results (`Chest Coins.png`) — **3 PNGs of 367 assets** |
| **PrimeTween** | Kyrylo Kuzyk | `https://github.com/KyryloKuzyk/PrimeTween` | **Stated**, in `package/license.md` inside the tgz: *"**allowed**: Use PrimeTween … in free and commercial products distributed in binary format"*; *"**not allowed**: Distribute PrimeTween's source code and tarball (.tgz) archive inside derivative products."* Copyright Kyrylo Kuzyk 2023. v1.4.8. | Code-only — 10 live controllers (StorySelect, SessionMap, Reader, Results, Arrange, ButtonSquash, MsLumiReactor…) |
| **TextMesh Pro** (Examples & Extras) | Unity Technologies | — | Unity package terms; bundled fonts individually licensed below | **Ships unconditionally** via its own `Resources/` — see §1c step 2 |

### Fonts that ship

| Font | Author | Source URL | Licence (file in repo) | Where |
|---|---|---|---|---|
| **Fredoka** | The Fredoka Project Authors | `https://github.com/hafontia/Fredoka-One` | **SIL OFL 1.1** — `Assets/Art/Fonts/Fredoka/OFL.txt`, *"Copyright 2016"* | headings, buttons, logo lockup |
| **Nunito** | The Nunito Project Authors | `https://github.com/googlefonts/nunito` | **SIL OFL 1.1** — `Assets/Art/Fonts/Nunito/OFL.txt`, *"Copyright 2014"* | body text, TMP default |
| **Baloo 2** | The Baloo 2 Project Authors | `https://github.com/EkType/Baloo2` | **SIL OFL 1.1** — `Assets/Plugins/Hyper_Casual_UI/Fonts/license.txt` | kit sprite baked labels |
| **Luckiest Guy** | *(via Unity Trash Dash)* | — | **Apache 2.0** — `Assets/Font/FontLicense.txt` | Trash Dash UI, Addressables |
| Anton · Bangers · Oswald-Bold · Unity | various | — | **SIL OFL 1.1** — `Anton OFL.txt`, `Bangers - OFL.txt`, `Oswald-Bold - OFL.txt`, `Unity - OFL.txt` | TMP examples only — **delete, §1c** |
| Roboto-Bold | Google | `https://www.apache.org/licenses/LICENSE-2.0` | **Apache 2.0** — `Roboto-Bold - License.txt` (+ `- AFL.txt`) | TMP examples only — **delete, §1c** |
| Liberation Sans | — | — | TMP default fallback, OFL | TMP core `Resources/` |
| **Electronic Highway Sign** | *not stated* | — | **UNVERIFIED.** `Electronic Highway Sign.TTF` sits in `TextMesh Pro/Examples & Extras/Fonts/` and is the **only font there with no accompanying licence file**. | TMP examples only — **removed by §1c step 2** |

---

## 6. Generated content — ours, and the tools that made it

| Asset | How made | Licence position |
|---|---|---|
| **150 narration clips** `Resources/Stories/Narration/*.mp3` (30 stories × 5 pages) + **10 `vo_*.mp3`** UI clips | Microsoft **edge-tts**, voice **`en-PH-RosaNeural`**, `--rate=-10%`. Recipe recorded in `CLAUDE.md` and `SummaRace_Readability_And_Accessibility_Audit.md:659`. | **UNVERIFIED — owner must confirm before distribution.** `en-PH-RosaNeural` is a **Microsoft Azure neural voice**. `edge-tts` is an unofficial Python client for the endpoint behind Edge's read-aloud feature; it is not the Azure Speech service and carries no licence grant. Azure's own terms govern redistribution of synthesized speech. **No grant covering embedding 160 clips of this voice in a distributed APK exists in this repo.** Runtime TTS is already forbidden by project rule, which does not help here — these are pre-rendered files that ship. |
| **27 placeholder hero images** `Resources/Stories/Art/s02…s10_*.png` | Generated in `af0da9c` (P8). 30–80KB blank gradients. | Ours. No third-party content. Due to be replaced with real art (`SummaRace_Asset_Shopping_List.md`). |
| **3 real hero images** `s01_easy/average/hard.png` (~1.8MB each) | `6eb8b8b` (F5) — cropped from `Documentation/Mockups/`. | **UNVERIFIED.** The mockups are prototype screens; nothing in this repo records what produced their artwork. |
| **Ms. Lumi** `Resources/UI/mslumi_wave.png`, `mslumi_cheer.png` (ship) · `Art/MsLumi/*`, `Art/UI/teacher_temp.png` (do **not** ship) | `teacher_temp.png` is a **crop from mockup 21** (F7). `SummaRace_Asset_Shopping_List.md` §3 specifies AI generation for the replacements. | **UNVERIFIED.** Same mockup-provenance gap as above. |
| **Owner-generated backgrounds** `Resources/UI/bg_splash.png`, `bg_menu.png` | Commit messages say **"(owner-generated)"** — `e97fadb`, `9e8cc3d`. Per project memory the owner runs image prompts through Gemini / ChatGPT. | **UNVERIFIED — which tool, and under whose terms.** Most major image tools grant the user output rights, but the free tiers differ and none of this is recorded. Owner can resolve from memory in a minute. |
| **`bg_logo_radial.png`** `Resources/UI/` (ships) | Derived from the **20 Logos PSD** pack in `bfc0cd0` (F27c). | Inherits that pack's position — **UNVERIFIED**, see §7. The pack folder is otherwise unreferenced, but this derived file ships. |
| **Code-generated textures** `Art/UI/hill_arc.png`, `cloud_soft.png`, `trail_dirt.png`, `v_gradient*.png`, `bar_bg/bar_fill`, `grad_scrim.png` | Generated procedurally in-project. | Ours. No issue. |
| **`app_icon.png`** | Rendered from our own Boot logo lockup in `9ce91c8`. Confirmed live: `ProjectSettings` `m_Icon` guid `101643a65c7e7cd4fa261b323f27ac0e` = `Assets/_Game/Art/UI/app_icon.png`. Trash Dash's cat icon is no longer referenced. | Ours (Fredoka under OFL). |
| **30 story JSONs** `Resources/Stories/*.json` | Converted by `Tools/StoryPipeline/` from the researcher's `Documentation/STORIES FOR SESSION 1-10 ….docx`. | **The researcher's intellectual property**, used with her involvement as the study instrument. Not a third-party asset licence question, but it is the most important content in the app and belongs in this record. |

---

## 7. Present in the repo but **not** in the shipping APK

These still sit in a **public** repo (§2), so they are listed. None ships.

### 7a. Reachable only through the dead `Race.unity` (build index 6, zero call sites)

`Race.unity` is still **in the build list**, so Unity pulls its whole dependency closure into the
APK even though nothing routes there. **Dropping it from Build Settings removes ~230MB of source
assets** — by far the largest reduction available, and it deletes six licence entries at once.

| Pack | Licence evidence | Size |
|---|---|---|
| **ithappy** — Cartoon City FREE + Animals FREE | **UNVERIFIED.** No licence file. Only vendor strings are two Discord invites in render-pipeline instructions. `.cs` files have no copyright headers. | 132MB |
| **JMO Assets** — Cartoon FX Remaster FREE | **Stated, and the best-documented pack here.** *"© 2012-2025 - Jean Moreno"*; *"The full license can be found here: https://unity.com/legal/as-terms (Appendix 1, End User License Agreement)"*. R 1.5.1. Bundles Kino Bloom — **MIT, © 2015-2017 Keijiro Takahashi**, `https://github.com/keijiro/KinoBloom`. | 38MB |
| **Supercyan Free Forest Sample** v2.0.1 | **Stated.** *"These files are distributed under the standard Unity - Asset Store Terms of Service and EULA license: https://unity3d.com/legal/as_terms"* · *"© 2024 Virtual Frontiers Oy"* · `www.supercyanassets.com` | 23MB |
| **SimplePoly City - Low Poly Assets** | **UNVERIFIED.** No licence file; no author/URL string anywhere. Vendor from folder name only. | 8MB |
| **SimpleNaturePack** | **UNVERIFIED.** No licence file; zero author/URL strings. FBX metadata scrubbed to `foobar.fbx`. | 2MB |
| **FirstGearGames** — Smooth Camera Shaker v2.12 | **UNVERIFIED** (vendor yes, terms no). `Documentation.pdf`: `firstgeargames@gmail.com`, `http://firstgeargames.com`. No licence terms stated. | 2MB |
| `Art/Characters/Ch46_nonPBR.fbx` | Mixamo — see §8. Replaced by Aj in F38. | 28MB |

### 7b. Zero references anywhere in the project

| Pack | Licence evidence | Size |
|---|---|---|
| **StarterAssets** | **Stated.** `license.txt`: *"This package is licensed under the Unity Companion License. For full license terms, please see: https://unity3d.com/legal/licenses/Unity_Companion_License"* · Unity Technologies · `http://u3d.as/2z1q` | 86MB |
| **20 Logos PSD** | **UNVERIFIED for the pack.** `Read me.pdf`: `http://u3d.as/1XRD`, `pirate.parrot.software@gmail.com` — no licence terms. Bundled fonts *are* individually licensed: 4× OFL 1.1 (incl. *"Copyright (c) 2011, Pablo Impallari … Reserved Font Name Kaushan Script"*), Fontfabric FFF EULA 2.0, Fontsonline FFC, and **Typodermic** — whose `read-this.html` states *"free for commercial use"* but lists **"Not allowed: … app (embedded)"**. ⚠️ **None of these fonts may be embedded in the APK.** Not currently at risk: F28 recreated the logo lockup in Fredoka precisely because the pack's PNGs have baked names. Only `bg_logo_radial.png` (an image, no glyphs) is derived from this pack. | 37MB |
| **Trees Package Lite** | **UNVERIFIED, and the readme is for a different product.** `ReadMe.txt` describes *"Cozy Kitchen Interior Props & Modular Parts, Publisher: Hiroba Games"* and tells you to open `Assets/Cozy Kitchen/Prefabs`, a path that does not exist. The folder holds **59 tree/rock/bush meshes and zero kitchen props**; the readme's "94 meshes / 6 textures" does not match the actual 59 meshes / 1 texture. It states no licence terms even for its own product. **Do not attribute these trees to Hiroba Games.** | 4MB |
| **Game GUI Vol1** | **UNVERIFIED.** `Readme.pdf` is three lines ending *"THANK YOU FOR PURCHASING"* — no author, no URL, no licence. Authorship only from Illustrator metadata: `%%For: (Rizwan Ashraf)`. | 10MB |
| **Simple Vehicle Pack** | **UNVERIFIED.** No licence file, no `.txt`/`.cs` at all. Only evidence is a Russian-language build path in the FBXs referencing `Fix_SimpleTown`. | 7MB |
| **Free Stylized URP Shaders** | **UNVERIFIED** (vendor yes, terms no). `Readme -Document.pdf`: `STYLIZEDFX.COM`, `stylizedfx@gmail.com`. No licence text. | 5MB |
| **Calcatz** — Weather Elemental VFX | **UNVERIFIED.** No licence file; `.cs` files have no copyright headers; vendor from folder name only. | 4MB |
| **SceneSwift Free** | **UNVERIFIED.** `SceneSwift_Free_Documentation.txt` ends *"Copyright (c) 2026. All rights reserved."* — **naming no holder**. Contact `nakranipiyush01@gmail.com`. No permission grant of any kind. Editor-only tooling. | 1MB |
| `Art/Avatars/`, `Art/MsLumi/` | 34MB, zero references. The shipping Lumi art is a different file under `_Game/Resources/UI/`. | 34MB |
| `Assets/Audio/Kenney/` | The 479-file source library. CC0 — no obligation either way. | 13MB |

**Empty / stub directories** (no content, no licence question): `Assets/ithappy/` is empty; `Assets/SceneSwift/` holds only an auto-generated settings asset; `Assets/Plugins/TrashDash Art/` holds only the notice text.

### 7c. Editor-only tooling

| Item | Author | Licence |
|---|---|---|
| **External Dependency Manager for Unity** (`Assets/MobileDependencyResolver/`) | Google Inc. | **Apache 2.0** — `Editor/LICENSE`, *"Copyright (C) 2014 Google Inc."*; `README.md` → `https://openupm.com/packages/com.google.external-dependency-manager/` |
| **MCP For Unity** | CoplayDev | `https://github.com/CoplayDev/unity-mcp` (from `Packages/manifest.json`) |

⚠️ `MobileDependencyResolver` is **still tracked in git** (`git ls-files` confirms), despite CLAUDE.md
F19 recording it as deleted. It is Editor-only and does not enter the APK, so this is a repo-hygiene
and decontamination note rather than a distribution issue — but the F19 claim is inaccurate.

---

## 8. Characters and animations — Mixamo (Adobe)

**Verified from the FBX binaries themselves, not from filenames.** Every file below contains the
embedded strings `Mixamo`, `mixamo.com` and the `mixamorig:` bone prefix:

| File | Ships? | Role |
|---|---|---|
| `Art/Characters/Aj.fbx` | **Yes** — Addressables, via `Bundles/Characters/Cat/character.prefab` | the race runner (F38) |
| `Art/Characters/Ty.fbx` | legacy race only | old patrol character |
| `Art/Characters/Ch46_nonPBR.fbx` | dead `Race.unity` only (28MB) | the kid Aj replaced |
| `Art/Characters/Animations/*.fbx` (12) | 6 ship via `KidCharacterAnimation.controller` / `PatrolAnimator.controller` | Idle, Running, RunningJump, RunToRolling, JoggingStumble, FlyingBackDeath, PoliceRun, ZombieRun, HipHopDancing ×2, FastRun |

**Licence — stated plainly, because it is commonly misread.** Mixamo content is provided by **Adobe**
and is free to use, including commercially, with an Adobe account. Characters and animations may be
**incorporated into projects and distributed as part of a compiled application** — which is exactly
what the APK does, so **the study distribution is fine**. What Adobe does *not* permit is
redistributing the assets **as standalone files**, which is what a public repo does (§2).

**No Adobe/Mixamo licence file exists in this repo** — the terms above are Mixamo's published terms,
not a document on disk. Recorded as **UNVERIFIED against in-repo evidence**; the owner should
confirm from `https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html` before distribution.
Additionally, ~46MB of `Art/Characters/Textures/` (Ch46 and Boy maps) have **zero references** — no
material remap in `Ch46_nonPBR.fbx.meta` — and are strippable independently of any other decision.

---

## 9. Unity engine and packages

`Packages/manifest.json`, 56 entries, all governed by Unity's package terms and the Unity Editor
licence. Notable: `com.unity.render-pipelines.universal` 17.4.0, `com.unity.addressables` 2.9.1,
`com.unity.inputsystem` 1.19.0, `com.unity.postprocessing` 3.5.4, `com.unity.test-framework` 1.6.0,
plus `com.kyrylokuzyk.primetween` (§5) and `com.coplaydev.unity-mcp` (§7c).

**Verified clean:** no `com.unity.ads`, `com.unity.analytics`, `com.unity.purchasing` or
`com.unity.microsoft.gdk`. This matters for the offline non-negotiable as well as for licensing.

---

## 10. UNVERIFIED register — 21 entries

**Ships in the APK (9)** — these are the ones that matter for distribution:

| # | Entry | What was looked at |
|---|---|---|
| 1 | `music_menu.mp3` | 514 audio files hashed; no match. No ID3 tag; encoder `LAME3.98` only. Commit `b1e8b27` records no source. |
| 2 | `sfx_click.mp3` | Same hash sweep; no match. Commit `f9aa714` says "owner's mouse-click.mp3", no origin. |
| 3 | 160 edge-tts narration clips | `en-PH-RosaNeural` is an Azure neural voice; `edge-tts` is an unofficial client. No grant in repo. |
| 4 | Hyper_Casual_UI **sprites** | Every file incl. binary PNGs grepped; only hits are two URLs in the Baloo 2 font licence. |
| 5 | Hovl Studio | Only file is a 3-line Bloom readme. No `.cs`, no `.shader`, no copyright string. |
| 6 | BitGem cop | All 8 assets + metas grepped. Vendor only from an FBX build path. |
| 7 | Layer Lab | 46 PNGs; no licence file; PNGs carry no metadata at all. |
| 8 | "Gems and gold" (3 shipped PNGs) | `Readme.pdf`/`.docx` give store URL + email, **no terms**. |
| 9 | Mixamo characters + animations | FBX binaries confirm Mixamo/Adobe; **no Adobe licence file in repo**. |

**Also unverified, but does not ship (12):** Electronic Highway Sign font *(removed by §1c step 2)* ·
ithappy · SimplePoly City · SimpleNaturePack · FirstGearGames · 20 Logos PSD *(pack-level; `bg_logo_radial.png` derived from it does ship)* ·
Trees Package Lite · Game GUI Vol1 · Simple Vehicle Pack · Free Stylized URP Shaders · Calcatz · SceneSwift.

**Plus three provenance gaps in our own generated art** (§6): the mockup-derived `s01_*` hero images,
Ms. Lumi, and the owner-generated backgrounds.

**Verified and clean:** all Kenney audio (CC0, six licence files) · Fredoka, Nunito, Baloo 2, Anton,
Bangers, Oswald, Roboto, Unity (OFL 1.1 / Apache 2.0, licence file each) · Luckiest Guy (Apache 2.0) ·
Kevin Iglesias (EULA stated, attribution explicitly not required) · JMO Assets (EULA + MIT for Kino
Bloom) · Supercyan (EULA + © Virtual Frontiers Oy) · StarterAssets (Unity Companion) · PrimeTween
(custom licence, terms quoted) · Google EDM4U (Apache 2.0).

---

## 11. How to keep this file true

The GDD asks for a log kept *"the moment"* an asset enters. Practically:

1. **Add a row here in the same commit that adds the asset** — name, author, source URL, licence, where used.
2. **Save the licence file next to the asset.** The five packs with no attribution evidence at all
   (BitGem, Layer Lab, SimpleNaturePack, SimplePoly City, Simple Vehicle Pack) became unverifiable
   because their store page was never recorded — nothing can recover it from the files.
3. **Prefer CC0.** Kenney was the least trouble in this entire audit, by a wide margin.
4. **Re-run the trace when the build list changes.** Dropping `Race.unity` (§7a) deletes six entries;
   `SummaRace ▸ Build Preflight` is the place to notice.
