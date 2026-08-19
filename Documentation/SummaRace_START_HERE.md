# SummaRace — START HERE

**One file. The goal, where the project actually is, and every asset still missing with its exact
target path.**

Written **2026-08-19** against HEAD `414c015` on branch `experiment/endless-override-2`.
Everything in §2 and §4 was **measured on disk and in the live Unity Editor today**, not copied
from another document.

> **How to use this file.** Read §1 (the goal) and §3 (the critical path) before doing anything.
> §4 is the asset worklist — that is the part you can start on right now, today, without Unity.
> §7 tells you which of the other eighteen documents to open when you need depth.

---

## 1. The goal — what "finished" means

SummaRace is **not a commercial game**. It is a **college thesis instrument**: 40 Grade-4 learners
use it across 10 sessions and are compared against a control group. That makes "done" a much
smaller and much sharper target than it feels like.

> ### Definition of done
> **One APK, installed on 40 tablets, that plays a full story with the Wi-Fi off and writes a
> data row that can be joined to that child's paper pretest/posttest.**

Three things must be true. Nothing else is allowed to block the study:

| # | Must be true | Status |
|---|---|---|
| 1 | It **runs on a real tablet** | ❌ never once tested — no APK has ever existed |
| 2 | It **records valid data** (`raceFirstPickCorrect` is the headline measure) | ✅ log schema 6, verified |
| 3 | It **joins to the paper test** (teacher-set participant code on every row) | ✅ built, needs setting at install |

Everything else — real hero art, the teacher's expressions, rain, sky gradients — **improves the
experience but does not block the study.** If time runs out, ship with placeholders.

### What the game is

A learner reads a short story, then races through it, then rebuilds it, then writes it — the same
five story parts each time, with the support removed one stage at a time:

```
Boot → MainMenu → SessionMap → StorySelect → Reader → Race → Arrange → Summary → Results
                                              │        │       │         │
                                    text VISIBLE   text GONE  sequence  produce
```

The five parts are the **SWBST** framework — **S**omebody · **W**anted · **B**ut · **S**o ·
**T**hen. 30 stories = 10 sessions × 3 difficulties. Every story is a JSON file; adding one costs
zero code.

### Non-negotiables (these are not preferences)

- **100% offline.** No networking, ads, analytics, or IAP. Enforced by an automated test, because
  this project has had ad packages return on their own twice.
- **Never punish the learner.** A wrong answer never blocks, never ends the run, never scolds.
- **Content is data, never code.** The 30 story JSONs are *generated artifacts* — edit
  `Tools/StoryPipeline/overrides.json` and re-run, never the 30 files by hand.
- **Learner data never leaves the tablet.** No raw PIN is ever stored; Android auto-backup is
  forced off in the manifest.

---

## 2. Where the project actually is — verified today

**The honest one-line summary:** *the game is feature-complete and plays end to end in the Editor;
the artefact you will hand to 40 children has never existed.*

### It is not "hugely missing" — eight of nine phases are done

| Phase | State |
|---|---|
| P0 decontaminate — offline, no ads/analytics/IAP, Boot at build index 0 | ✅ and guarded by a test |
| P1 30 stories | ✅ 30/30 load through the real `StoryLoader`, 0 validation failures |
| P2 all 30 reachable — StorySelect data-driven, SessionMap built | ✅ |
| P3 profiles · logging · teacher PIN · participant codes | ✅ log schema 6 |
| P4 ten race worlds — theme + zone + sky dome + greenery + light | ✅ 30 applied, 30 distinct |
| P5 narration + art wiring | ✅ 150 story clips + 10 instructional, all 31 audio keys backed |
| P7 tests + build tooling | ✅ 53/53 EditMode green, `SummaRace ▸ Build Preflight` |
| P8 device budget (RAM) | ✅ ~127MB of resident audio recovered |
| **P6 ship pass** | ⬜ **the only open phase — and it is blocked on a download, not on code** |

### Live Editor state (checked over MCP, 2026-08-19)

```
Unity 6000.4.1f1 · instance SummaRace2@ab987a51 · scene MainSummaRace · idle
Console: 13 entries, ZERO errors
  └─ 6 × deprecated FindObjectsSortMode overload (cosmetic)
  └─ 1 × "project uses Input Manager" — EXPECTED: activeInputHandler is
         deliberately 2 (Both) on this branch so Trash Dash's legacy touch
         path keeps working. Do not set it to 1 here.
```

### ⛔ The one thing blocking everything

```
Editor/Data/PlaybackEngines/  →  WebGLSupport, windowsstandalonesupport
                                 ↑ no AndroidPlayer
Library/com.unity.addressables/  →  does not exist
Assets/StreamingAssets/          →  does not exist
```

**Android Build Support is not installed**, so no APK has ever been produced, so **Addressables
content has never been built for any platform.**

That second consequence is the dangerous one and is worth understanding exactly:

> The race scene loads its **road, its scenery and the runner itself** through
> `Addressables.InstantiateAsync`. In the Editor those resolve straight off the Asset Database —
> which is why every playtest has looked perfect. In an APK they come from bundles. With no
> bundles, `TrackManager.instance` stays **null forever** and **the race never starts at all** —
> it is not "an empty road", it is a race the learner cannot enter.
>
> This is now *escapable* (a timeout shows "this race is not ready — go back to Story Select") but
> **escapable is not playable.** `m_BuildAddressablesWithPlayerBuild` is set to `1`, so a player
> build now produces the content automatically — but **no build has ever run, so this has never
> been observed working.**

**Fix: install the module.** ~2GB, 10–30 minutes, unattended.

### Known gaps that are real but do not block

- **27 of 30 stories have never been played through by anybody.** Only `s01_*` has ever been run.
- **No portrait layout has ever been rendered on real hardware**, and nobody has confirmed which
  tablet model ships.
- **Frame rate on the 2GB floor device is arithmetic, not measurement.** If the race misses 30fps,
  the first and only lever to touch is `GameRules.RaceMaxSceneryPerSegment` 14 → 7.
- **APK size is estimated at ~190–230MB** against a 300MB cap. Never measured.

---

## 3. The critical path — do these in this order

| # | Step | Effort | Who |
|---|---|---|---|
| **1** | **Install Android Build Support + OpenJDK + Android SDK & NDK Tools.** Unity Hub ▸ Installs ▸ **6000.4.1f1** (match exactly) ▸ gear ▸ Add modules. **Close Unity first.** | 10–30 min, unattended | **owner — start this now** |
| **2** | **Switch platform to Android** (`File ▸ Build Profiles ▸ Android ▸ Switch Platform`). Re-imports a 945MB `Assets/`. | 30–90 min, unattended | owner — **do it the day before build day** |
| **3** | Run **`SummaRace ▸ Build Preflight`**. Fix every ✖. Read every ▲ and accept it deliberately. | 15 min + unknowns | either |
| **4** | **Build ONE APK.** Build App Bundle **OFF** (an `.aab` cannot be sideloaded). Development Build **OFF**. Output outside the repo. Name it `SummaRace_v1.0_vc1_YYYYMMDD.apk`. | 20–45 min first IL2CPP build | owner |
| **5** | `git tag study-build-v1` and **back up `C:\Users\<you>\.android\debug.keystore` next to the APK.** ⚠️ The APK is debug-signed and that keystore is per-machine — a mid-study rebuild from a different PC cannot install over the deployed app, and the only way through is an uninstall that **erases that tablet's profiles and every unexported log.** Build every study APK on the same machine. | 5 min | owner |
| **6** | **Sideload to one tablet and smoke-test.** In priority order: ① the race actually starts and finishes ② a full loop on a story that is **not** `s01` ③ Wi-Fi off + airplane mode, play a whole story ④ Android BACK does not close the app ⑤ tap-to-lane works. | 30–60 min | owner |
| **7** | Prove the **export path over USB** — `persistentDataPath` is `Android/data/<pkg>/files`, which Android 11+ usually hides from MTP. Use `adb`. **The logs are the entire dataset and there is no second chance to collect them.** | 15 min once | owner |
| **8** | Set the **teacher PIN** and **each learner's participant code** on every tablet at install. | ~2 min/tablet + 1 min/learner | owner |
| **9** | Install the **same** APK on all 40 tablets. | ~2 min each | owner |

**Realistic total: one focused day**, most of it waiting on downloads, assuming the preflight and
the smoke test surface nothing new.

**How you will know step 2 worked:** the race starts. If you see the "this race is not ready yet"
card with a button back to Story Select, the content did not ship.

---

## 4. 🎨 ASSETS STILL MISSING — the worklist

**This is the section to start on today.** None of it needs Unity open, and none of it blocks the
build. Everything below drops in **with no code change** — story JSONs already carry their
`heroImage` path, and audio loads by key from `Resources/Audio`.

### Ⓐ The overwrite rule — read this before generating anything

> **Keep the exact filename and never delete the `.png.meta` / `.ogg.meta` beside it.**
> The `.meta` holds the asset's GUID and its import settings. Delete it and every reference in
> every scene silently breaks. **Overwrite the image file in place, leave the meta alone.**

Two traps this project has already hit:

- **Some packs import as `Texture`, not `Sprite`.** A UI image will silently refuse to draw. Set
  the importer to Sprite before wiring anything new.
- **Kit sprites can be baked showcase art.** `Hyper_Casual_UI/Sprites/GameUI/Level.png` has
  "Level 23" and "Score 232,00" *drawn into the picture*. **Look at any kit sprite before using it.**

### Ⓑ Priority 1 — visibly improves the study build

#### 1. Story hero art — **27 of 30 missing** ⭐ biggest visual gap

Measured today: `s01_easy/average/hard` are real illustrations at **1.75–1.97 MB**. The other
**27 are blank generated gradients at 29–80 KB** — they resolve, they just show no story identity.

| | |
|---|---|
| **Target path** | `Assets/_Game/Resources/Stories/Art/<story_id>.png` |
| **Filenames** | `s02_easy` … `s10_hard` (27 files; `s01_*` are done — **do not overwrite them**) |
| **Spec** | ≤1024px, PNG, imported as Sprite |
| **Prompts** | **`SummaRace_Asset_Shopping_List.md` §2** — 27 ready-to-paste prompts, one per story. This is the source of truth. |

Full list of the 27 to produce:

```
s02_easy  s02_average  s02_hard        s03_easy  s03_average  s03_hard
s04_easy  s04_average  s04_hard        s05_easy  s05_average  s05_hard
s06_easy  s06_average  s06_hard        s07_easy  s07_average  s07_hard
s08_easy  s08_average  s08_hard        s09_easy  s09_average  s09_hard
s10_easy  s10_average  s10_hard
```

#### 2. Ms. Lumi, the teacher — **3 images exist and two of them are a different woman**

Measured today, and this is worse than "needs polish":

| File | Size | Canvas | Problem |
|---|---|---|---|
| `Resources/UI/mslumi_wave.png` | 718 KB | 2048×1024 | flat-vector woman, **ponytail** |
| `Resources/UI/mslumi_cheer.png` | 769 KB | 2048×1024 | same woman, only other pose |
| `Art/UI/teacher_temp.png` | — | 290×290 | glossy storybook portrait, **high bun — a different person** |

A learner sees **two different teachers** across ten sessions, and each of them only ever does one
thing. `teacher_temp` is the canonical look (it matches the hero-art house style the 27 new images
target).

| | |
|---|---|
| **Prompts** | **`SummaRace_Image_Prompt_Pack.md`** — 22 images (16 half-body poses + 6 badge portraits), each tied to a beat that already exists in the game |
| **Padding rule** | On a **2048 × 1024 transparent** canvas: scale the figure to **976 px tall**, bottom-align at y = 1024, centre the head-and-torso line at **x ≈ 1000**. ±60 px is invisible in game. |
| **Why the padding matters** | `EndlessRaceDirector` has that padding **hard-coded in its anchor maths**. A tight crop renders thumbnail-sized in the wrong corner. |

⚠️ **Only 3 of the 22 are drop-in today** — `MsLumiReactor` has exactly two serialized sprite
slots and Arrange/Summary use one static scene-wired Image. Generate these three first, because
they land with zero wiring:

```
lumi_wave        → overwrite Resources/UI/mslumi_wave.png
lumi_cheer       → overwrite Resources/UI/mslumi_cheer.png
lumi_badge_smile → overwrite Art/UI/teacher_temp.png
```

The other 19 need a small code change first (a `LumiPortrait` component reading a `Resources/UI/Lumi/`
folder with a shuffle-bag picker). **Ask for that before generating all 22**, or 19 images will sit
on disk doing nothing.

#### 3. Learner avatars — **4 missing**

Name Entry currently draws four kit shapes (heart / star / gem / lightning). They work — they were
deliberately given different *shape* **and** *colour* after two near-identical gold stars failed a
portrait render — but they are placeholder icons, not characters.

| | |
|---|---|
| **Target path** | `Assets/_Game/Resources/UI/avatar_0.png` … `avatar_3.png` |
| **Spec** | 256×256, PNG, transparent, Sprite |
| **Rule** | The four must differ by **shape as well as colour** — a colour-blind learner has to tell them apart, and they are how a child identifies their own profile. |

#### 4. SWBST pickup icons — **5 missing**

| | |
|---|---|
| **Target path** | `Assets/_Game/Resources/UI/swbst_s.png`, `swbst_w.png`, `swbst_b.png`, `swbst_s2.png`, `swbst_t.png` |
| **Spec** | 256×256, PNG, transparent |
| **Colours** | Use `Constants/SwbstPalette.cs` — S=blue · W=green · B=red · S=orange · T=purple |
| **Reference** | Mockup 17 |

### Ⓒ Priority 2 — nice to have, genuinely optional

| Asset | Target path | Spec | Note |
|---|---|---|---|
| `sfx_unlock` | `Resources/Audio/sfx_unlock.ogg` | short, mono | Specified but never produced. `TeacherMenuController` currently substitutes `sfx_star`. |
| Rain streak | `Art/FX/rain_streak.png` | 64×256, PNG, alpha | The **only** art the weather system needs — every other mood is lighting + fog values. |
| Rain loop | `Resources/Audio/sfx_rain.ogg` | seamless loop | |
| Sky gradients ×10 | `Resources/UI/sky_<world>.png` | 512×512 | Only if the per-world shader tint proves insufficient. It currently does not. |
| Session-map path art | `Resources/UI/map_path.png` | 9-sliceable | Map currently uses kit pills on a plain board. |

### Ⓓ Two audio files that should be **replaced**, not added

Both work fine. Both have **no traceable source**, which matters for a thesis document:

| File | Size | Issue |
|---|---|---|
| `Resources/Audio/music_menu.mp3` | 6.5 MB | 320 kbps stereo LAME encode — a desktop-rip bitrate. Matches nothing among 514 hashed pack files, carries no ID3 tag. Also has an **audibly gapped loop**. |
| `Resources/Audio/sfx_click.mp3` | 11 KB | Same — untraceable. |

Both are roughly **ten-minute swaps to CC0 Kenney audio already on disk** in
`Assets/Audio/Kenney/`. Worth doing before the thesis is written, not after.

### Ⓔ ✅ Do NOT generate these — already owned and wired

Check here before producing anything:

| Category | Already in the project |
|---|---|
| UI buttons / panels / icons | `Hyper_Casual_UI` |
| Coins, gems, chests | `Gems and gold` |
| Trophies, 2D icons | Layer Lab |
| Nature props, rocks, trees | Supercyan · SimpleNaturePack · SimplePoly |
| City buildings, palms | ithappy Cartoon City |
| Magic / portal / burst FX | Hovl Studio |
| Impact FX | CFXR |
| Characters + run/jump/stumble clips | Mixamo (Aj is the runner) |
| Cop model | BitGem |
| **Story narration ×150** | ✅ complete — `Resources/Stories/Narration/` |
| **Instructional narration ×10** | ✅ complete — `vo_*` in `Resources/Audio/` |
| **All 21 sfx + music keys** | ✅ complete |

### Ⓕ Note on generating these

**No image-generation provider is configured in MCP** — `generate_image` needs a fal.ai or
OpenRouter key. So the options are: supply the art yourself, add a key, or ship the placeholders.
Shipping the placeholders is a legitimate choice; it costs story identity on the Story Select
cards and nothing else.

---

## 5. Decisions only you can make

None of these block the build. All have a recommendation.

| # | Decision | Recommendation |
|---|---|---|
| 1 | **Freeze content and email the researcher.** ~130 machine-edited strings across three validity passes, plus `s01_easy`'s AI-authored questions, none read by a researcher. Bundle the `s05_hard`/`s06_hard` duplicate and the `s03_easy` mislabel into the same email. | **Send today** — the answers arrive on her clock, not yours |
| 2 | `RacePreviewLeadSeconds` 12 → 17, for the slowest readers | ⚠️ **Time one race first.** 17 makes the gate floor 23s, which collapses average (20s) and hard (18s) onto the same cadence and makes difficulty inert on two of three |
| 3 | One tablet per learner vs shared | one per learner |
| 4 | `GameRules.StarsTwoMin` 4 → 3 | yes — at 4, a 3/5 and a 0/5 show the identical screen. Display only, no logged measure moves |
| 5 | A passive run that collects all five gates | accept — it scores at chance, and `racePicks[].lane` detects it afterwards |
| 6 | The 27 placeholder hero images | only after the build works |
| 7 | `s05_hard` / `s06_hard` are the same story | leave the content, record it as a limitation |
| 8 | **Trash Dash art/music licence** before distributing to 40 devices | almost certainly fine for a thesis instrument — confirm deliberately |
| 9 | ⚠️ **The GitHub repo is PUBLIC** and carries ~700MB of Asset Store content in extractable form | **one repository setting closes all of it** — unrelated to the study, live right now |

---

## 6. The known limitation to report in the thesis

**The race is ~59% passable by remembering the Reader's answer** rather than by comprehending —
down from 84% originally, against a 34% misaligned control.

**Do not try to close this further.** Measured across all 150 page↔element pairs: **25 remain
byte-identical, and all 25 are the `SOMEBODY` slot.** Every one is a character's name — Duncan,
Molly, Chef Pierre, Marusia. You cannot reword a name, and *remembering who the story was about
is the recall being tested*, not a leak. An automated pass told to "reduce carryover" would rename
the researcher's characters.

**Excluding names, the race scores 49.2% against a 33.3% chance floor.** That is the honest number
to report. Whether 49.2% is acceptable is the researcher's call, not the build's.

---

## 7. The other documents — open these when you need depth

Do not read them all. This file plus the right one is enough.

| I need… | Open |
|---|---|
| The ordered critical path in full detail | `SummaRace_Owner_Handover.md` |
| The remaining work with effort estimates | `SummaRace_Finalization_Plan.md` |
| Step-by-step toolchain → APK → sideload | `SummaRace_Build_And_Release.md` |
| **27 hero-image prompts** | `SummaRace_Asset_Shopping_List.md` §2 |
| **22 Ms. Lumi prompts** | `SummaRace_Image_Prompt_Pack.md` |
| The classroom side — PIN, codes, sessions, export, recovery | `SummaRace_Study_Operations_Runbook.md` |
| Every field in the exported `.jsonl` | `SummaRace_Data_Dictionary.md` |
| RAM / APK weight / render pipeline — **the measured authority** | `SummaRace_Device_Budget.md` |
| Type size, contrast, tap targets | `SummaRace_Readability_And_Accessibility_Audit.md` |
| Every interactive moment's sound | `SummaRace_Audio_Audit.md` |
| Content measured against the geometry it renders into | `SummaRace_Content_QA_Report.md` · `SummaRace_Story_Alignment_Audit.md` |
| An outside pre-ship read | `SummaRace_Pro_Review.md` |
| Ten world recipes, weather, collectibles | `AssetGeneration/README.md` |
| Design intent (all decisions D1–D18 LOCKED) | `SummaRace_Final_GDD.docx` |
| Every asset's author, source and licence | `assets_credits.md` |

**Where documents disagree, the build wins.** The GDD is intent; the game has deliberately
diverged for playability — the biggest divergence being that the race is now the Trash Dash
endless runner (`MainSummaRace`), not the GDD's park/trail race.

---

## 8. Verify any claim in this file yourself

```bash
# Is this file still current?
git log --oneline 414c015..HEAD          # prints nothing = current

# Android module installed yet?
ls "/c/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/"
#   AndroidPlayer present = step 1 done

# Has Addressables content ever been built?
ls Library/com.unity.addressables/aa/     # exists = step 2 done

# Which hero images are still placeholders?
ls -lS Assets/_Game/Resources/Stories/Art/*.png
#   ~1.8MB = real art · 29-80KB = placeholder

# Everything else
#   Unity ▸ SummaRace ▸ Build Preflight
```

---

## 9. Two housekeeping notes from the git log

- Your last two commits (`5f40298` "EE", `414c015` "dpences") are **`.meta` files only** — nothing
  is broken. `5f40298` actually fixed a real problem: two `.cs` files had been committed without
  their `.meta`, which gives them a fresh GUID on every clone.
- `414c015` committed `.meta` files under **`Assets/MobileDependencyResolver/`** — Google's EDM4U.
  That is the component which has **twice** pulled `com.unity.ads` / `analytics` / `purchasing`
  back into `manifest.json` on this project. `manifest.json` is clean right now and
  `OfflineComplianceTests` guards it, so nothing is wrong today. It is simply the one folder worth
  watching, because Unity defines `UNITY_ADS` automatically the moment the matching package returns.

---

*Next action: start the Android Build Support download (§3 step 1). It runs unattended — begin the
hero art in §4Ⓑ1 while it downloads.*
