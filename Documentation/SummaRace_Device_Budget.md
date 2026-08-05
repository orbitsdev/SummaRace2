# SummaRace — Device Budget (2GB Android 8 floor)

**Measured 2026-08-05 on branch `experiment/endless-override-2`, by reading the real files.**
Every number below comes from a file on disk; nothing is quoted from an earlier document.
No APK has ever been built (Android Build Support is not installed), so *build-time* figures
are estimates derived from source bytes and importer settings, and are labelled as such.
The RAM figures are not estimates in the same sense — they are the arithmetic consequence of
the import settings that are actually set.

Target: **2GB RAM, Android 8, 30fps floor during the race, APK ≤ 300MB, portrait, IL2CPP + ARM64, min API 26.**

---

## 0. Headline

| | |
|---|---|
| **Biggest real risk** | **178.2 MB of audio is set to DecompressOnLoad**, of which **156.5 MB of raw PCM is genuinely resident** once the race has been entered — not the ~36 MB CLAUDE.md records. On a 2GB device this alone can push the app into the low-memory killer. Fixing it is import settings only, no code and no art. |
| **Biggest free frame-rate win** | Main-light shadows render a **2048² atlas across 4 cascades every frame** into a race world where **nothing can receive a shadow** (verified: every corridor material is a single-pass unlit shader with no ShadowCaster pass, and `MainSummaRace.unity` references **zero** URP/Lit materials). |
| **Biggest free APK win** | Legacy `Race.unity` is build index 6 with **zero call sites**, and **51.5 MB of source assets are exclusive to it** (not the 23 MB previously recorded) — 29 MB of that is `Ch46_nonPBR.fbx`, the kid Aj replaced in F38. |
| **Confirmed non-win** | Deleting unused Addressables groups saves ~0. Re-measured: all `Night-*` groups combined are exclusive to **3 zone prefabs totalling 0.02 MB**. CLAUDE.md was right. |

---

## 1. Render pipeline — what actually runs

### 1.1 CLAUDE.md's claim, re-verified

| CLAUDE.md claim | Verdict | Evidence |
|---|---|---|
| Every quality level has `customRenderPipeline: {fileID: 0}` | ✅ **True** | `ProjectSettings/QualitySettings.asset` — all 6 levels (Fastest…Fantastic) |
| `GraphicsSettings.m_CustomRenderPipeline` → `Assets/RenderingPipeline.asset` | ✅ **True** | `GraphicsSettings.asset` line 43: guid `ccdbdba252991284fa9a2e2dc349537d` = `Assets/RenderingPipeline.asset.meta` |
| …with `Assets/UIRenderer.asset` as its renderer | ✅ **True** | `RenderingPipeline.asset` `m_RendererData` guid `45e4c9a2a5aaef0409ca1edd4a368fc6` = `UIRenderer.asset.meta` |
| `Mobile_RPAsset` / `PC_RPAsset` assigned to nothing | ✅ **True** | Their guids (`5e6cbd92…`, `4b83569d…`) appear in no quality level and not in GraphicsSettings |
| `renderScale` is 1.0 | ✅ **True** | `m_RenderScale: 1` |
| 2048 shadowmap, 4 cascades, 50 m | ✅ **True** | `m_MainLightShadowmapResolution: 2048`, `m_ShadowCascadeCount: 4`, `m_ShadowDistance: 50` |
| `m_IntermediateTextureMode: Always` on UIRenderer | ✅ **True** | `UIRenderer.asset` line 99 `m_IntermediateTextureMode: 1` (`Auto=0, Always=1`) |
| **F23:** "both RP tiers have HDR — check 2GB frame rate" | ❌ **Moot / misleading** | The **active** asset has `m_SupportsHDR: 0` and `m_MSAA: 1` (off). HDR=1 is true only of the two *inactive* tier assets. There is no HDR cost on the shipping path. |
| **F23:** Bloom post-FX in the race | ❌ **False for the shipping race** | `MainSummaRace.unity` has **0** cameras with `m_RenderPostProcessing: 1`, and `Assets/_Game/Art/RacePostFx.asset` has an empty component list (`components: - {fileID: 0}`). The Bloom volume exists only in the dead legacy `Race.unity` (1 camera with post on). |

### 1.2 The active pipeline, as measured

`Assets/RenderingPipeline.asset` (Trash Dash's) + `Assets/UIRenderer.asset`:

| Setting | Value | Cost on a tiler at 1080×1920 / 30fps |
|---|---|---|
| `m_RenderScale` | **1.0** | 2,073,600 fragments/frame. At 0.8 it would be 1,327,104 → **−36.0 % fragment work**. |
| `m_MainLightShadowsSupported` | **1** | see below |
| `m_MainLightShadowmapResolution` | **2048** | 4,194,304 depth texels written per frame = **126 M texels/s** at 30fps |
| `m_ShadowCascadeCount` | **4** | **4 separate shadow-caster culls + draw submissions per frame**, each into a 1024² slice of the atlas |
| `m_ShadowDistance` | **50** | every caster within 50 m is submitted 4× |
| `m_SoftShadowsSupported` | 0 | good — hard shadows only |
| `m_SupportsHDR` | **0** | good — 32-bit colour target, no FP16 bandwidth |
| `m_MSAA` | **1** (= disabled) | good |
| `m_RequireDepthTexture` / `m_RequireOpaqueTexture` | 0 / 0 | good — no `_CameraDepthTexture` or `_CameraOpaqueTexture` copies |
| `m_AdditionalLightsPerObjectLimit` | 0 | good |
| `m_UseSRPBatcher` | 1 | good |
| `m_IntermediateTextureMode` (UIRenderer) | **1 = Always** | Forces an off-screen colour target + a final blit: **8.29 MB written + 8.29 MB read per frame = ~497 MB/s of extra bandwidth at 30fps.** `ProjectSettings` already sets `androidBlitType: 1` (Never), so URP is overriding the player setting. |
| `UIPass` renderer feature | RenderObjects, layer 5 (UI), `AfterRenderingOpaques`, `overrideCamera: 1`, FOV 60 | This is Trash Dash's 3D-UI trick and is why there is only **1 Camera** in `MainSummaRace`. It may itself require an intermediate — `Auto` lets URP decide rather than forcing it. |

Quality settings that URP still honours: **Android default quality level = index 1 ("Fast")**, `vSyncCount: 0` — correct; `Application.targetFrameRate` governs the cap. (`m_PerPlatformDefaultQuality: Android: 1`.)

### 1.3 The shadow finding (this is the important one)

Main-light shadows are on, but **there is nothing in the race that can receive them**:

* Reachable materials by shader — `Unlit/CurvedUnlit` ×18, `Unlit/CurvedUnlitAlpha` ×13, `Unlit/UnlitBlinking` ×3, `Unlit/CurvedRotation` ×2, `Unlit/CurvedUnlitCloud` ×1, `Unlit/VertexColor` ×1. That is **38 of 97** reachable materials, and it is the whole visible corridor (roads, houses, walls, clouds).
* Each of those shader files (`Assets/Shaders/*.shader`) has **exactly one `Pass`**, no `LightMode` tag, no `Fallback`, no `UsePass`. Verified by reading them. → they **cannot cast and cannot receive** shadows, and are not in any depth pass.
* Our own `Assets/_Game/Art/Shaders/CurvedVertexColor.shader` is the same shape: one `CurvedUnlit` pass, no ShadowCaster.
* `MainSummaRace.unity` contains **0 references to the URP/Lit shader guid** (`933532a4fcc9baf4fa0491de14d08ed7`). The only lit things in the race are instantiated at runtime — Aj (materials embedded in `Assets/Art/Characters/Aj.fbx`) and the cop (`cop_blue.mat`, URP/Lit) — and they stand on unlit ground.
* The scene has exactly **1 Light** (Directional, `m_Shadows.m_Type: 2` = Soft requested, downgraded to hard because `m_SoftShadowsSupported: 0`).

So the engine renders a 2048² shadow atlas across 4 cascades every frame, and the result is sampled by no visible surface. **This is the cheapest frame rate in the project — but it is a pipeline-asset edit and must be playtested, not assumed.**

### 1.4 Recommended render changes, smallest first

| # | Change | Est. gain | Changes the look? | Risk |
|---|---|---|---|---|
| R1 | `RenderingPipeline.asset`: `m_ShadowCascadeCount` **4 → 1** | Removes 3 of 4 shadow-caster culls/draws per frame | **No** — shadows still render, just one cascade over 50 m | Low. Shadows on the kid/cop get coarser; nothing visible receives them anyway. |
| R2 | `RenderingPipeline.asset`: `m_MainLightShadowmapResolution` **2048 → 512** | 4.19 M → 0.26 M depth texels/frame (**−94 %**) | **No** (see 1.3) | Low |
| R3 | `UIRenderer.asset`: `m_IntermediateTextureMode` **1 (Always) → 0 (Auto)** | Up to **~497 MB/s** of bandwidth if URP can then go direct-to-backbuffer | **No** | Low–medium. If the `UIPass` feature forces an intermediate anyway, this is a no-op rather than a regression. |
| R4 | `RenderingPipeline.asset`: `m_MainLightShadowsSupported` **1 → 0** | Removes the shadow pass entirely, plus the `_MAIN_LIGHT_SHADOWS*` shader variants | **No in `MainSummaRace`** (verified). **Yes in the dead legacy `Race.unity`**, whose ground *is* URP/Lit. | Low if `Race.unity` is being dropped anyway (see §3). |
| R5 | `RenderingPipeline.asset`: `m_RenderScale` **1.0 → 0.8** | **−36 % fragment work** — by far the largest single frame-rate lever | **YES — visibly softer.** | Medium. Do this only if R1–R4 miss the 30fps floor on the real device. |
| R6 | **Do NOT** swap to `Mobile_RPAsset` | — | **YES** | The whole F26–F42 race look was built against *their* pipeline (curved/unlit materials). `Mobile_RPAsset` also turns **HDR back on** (`m_SupportsHDR: 1`), which is a *regression* on a tiler. Reassigning it is a redesign, not a config tidy. |

**None of R1–R6 are in the Editor script.** They are pipeline-asset edits that alter rendering for the whole project and need an owner decision plus a playtest. Apply them by hand in Project Settings → Graphics / the asset inspector.

---

## 2. Audio RAM — the real pressure

### 2.1 CLAUDE.md's claim, re-verified

> "~36MB of Vorbis music is set **DecompressOnLoad** and expands to raw PCM."

**Directionally right, quantitatively wrong by ~5×.** Measured across all 706 audio files under `Assets/` (importer settings parsed from each `.meta`; duration from the Ogg granule position / WAV `data` chunk / MP3 frame header; PCM = duration × rate × channels × 2 bytes):

| Load type | Reachable clips | On disk | **Raw PCM once loaded** |
|---|---:|---:|---:|
| **DecompressOnLoad** | 27 | 44.93 MB | **178.22 MB** |
| CompressedInMemory | 150 | 6.29 MB | 38.47 MB (only the 6.29 MB compressed is resident) |
| Streaming | 24 | 5.92 MB | 12.46 MB (only small ring buffers are resident) |

("Reachable" = transitive closure from the 12 scenes in `EditorBuildSettings.asset` + every `Resources/` folder + all Addressables group entries. The closure totals **994 assets / 236.9 MB**, which corroborates CLAUDE.md's 238 MB figure.)

### 2.2 The worst offenders

| Clip | Length | ch | Disk | **PCM resident** | Load type | Why it is resident |
|---|---:|---:|---:|---:|---|---|
| `Assets/Sounds/Stems/STEMSMainTrackMono.ogg` | 240.0 s | 1 | 7.28 MB | **21.17 MB** | DecompressOnLoad, `preloadAudioData: 1` | on `Assets/Prefabs/MusicPlayer.prefab`, which is `DontDestroyOnLoad` |
| `…/STEMSSpeed1BongosMono.ogg` | 240.0 s | 1 | 2.68 MB | **21.17 MB** | ″ | ″ |
| `…/STEMSSpeed1HatsMono.ogg` | 240.0 s | 1 | 5.11 MB | **21.17 MB** | ″ | ″ |
| `…/STEMSSpeed2ChoirMono.ogg` | 240.0 s | 1 | 4.96 MB | **21.17 MB** | ″ | ″ |
| `…/STEMSSpeed3StringsMono.ogg` | 240.0 s | 1 | 5.22 MB | **21.17 MB** | ″ | ″ |
| `…/STEMSSpeed4SynthMono.ogg` | 240.0 s | 1 | 5.75 MB | **21.17 MB** | ″ | ″ |
| **6 stems subtotal** | | | **30.99 MB** | **127.01 MB** | | |
| `Assets/_Game/Resources/Audio/music_menu.mp3` | 161.4 s | **2** | 6.46 MB | **28.47 MB** | DecompressOnLoad | `AudioManager.PlayMusic(AudioKeys.MusicMenu)` from MainMenu / StorySelect / SessionMap |
| `Assets/_Game/Resources/Audio/music_race.ogg` | 240.0 s | 1 | 7.28 MB | **21.17 MB** | DecompressOnLoad | **dead** — only `RaceController` (legacy `Race.unity`) plays it |
| 19 short SFX in `_Game/Resources/Audio/` | ≤1.3 s | | 0.20 MB | 1.57 MB | DecompressOnLoad | **correct as-is** |

`MusicPlayer.RestartAllStems()` plays **all six stems simultaneously** and cross-fades their volumes with speed, so all six are genuinely resident at once — this is not a worst case, it is the normal case. Because `MusicPlayer` is `DontDestroyOnLoad`, that 127 MB stays allocated for the rest of the session after the first race.

**Resident audio on the shipping path today ≈ 127.0 (stems) + 28.5 (menu music) + 1.6 (our SFX, PCM) + 6.3 (narration, compressed) ≈ 163 MB**, of which 156.5 MB is raw PCM that need not be.

### 2.3 Narration — CLAUDE.md's claim held

✅ **Confirmed.** All **150** clips in `Assets/_Game/Resources/Stories/Narration/` are `CompressedInMemory` + `Vorbis` + `forceToMono: 1` + `preloadAudioData: 0` + `loadInBackground: 1`, **6.29 MB on disk**. If they were DecompressOnLoad they would be 38.47 MB of PCM. The P5 decision was correct and has not drifted.

### 2.4 A second class of mistake: Streaming used for one-shot SFX

22 of Trash Dash's `Assets/Sounds/SFX/*.ogg` are **Streaming** — including `ButtonPresses.ogg` (0.2 s) and `FishboneCollection.ogg` (0.04 s) — and all 24 Streaming clips carry `preloadAudioData: 1`, which is a contradictory pair. A streaming decoder + file handle per one-shot is the wrong shape on a cheap eMMC device and adds latency to every button press. `MenuTheme.ogg` (66.8 s) and `DeathLoop.ogg` (46.5 s) are correctly Streaming and should stay that way.

### 2.5 Correct settings by category

| Category | Correct setting | Rationale |
|---|---|---|
| Single-source long music (`music_menu`, `music_race`, `Sounds/Music/*`) | **Streaming**, `preloadAudioData: false`, `loadInBackground: true` | one instance, no sync requirement → RAM cost drops to a ring buffer |
| The 6 `MusicPlayer` stems | **CompressedInMemory**, `preloadAudioData: false`, `loadInBackground: true` | **not Streaming** — six concurrent streams on eMMC risk buffer underrun and drift, and they must stay sample-synced. CompressedInMemory keeps ~31 MB compressed in RAM and decodes on the fly. |
| Narration (150 clips) | **CompressedInMemory** + `forceToMono` (already correct) | one plays at a time, 6.29 MB total |
| Short SFX ≤ 10 s | **DecompressOnLoad**, `preloadAudioData: true` | tiny PCM, zero latency, no decoder |

**Applying these saves 96.0 MB (stems: 127.0 → 31.0 compressed) + 28.3 MB (menu music: 28.5 → ~0.2) = 124.3 MB**, against a cost of **+2.5 MB** from the 22 one-shot SFX moving Streaming → DecompressOnLoad (their combined PCM is 12.46 − 5.89 − 4.10 = 2.47 MB). **Net ≈ 122 MB of resident RAM recovered.** This is the single most valuable change in this document and it is **entirely look-preserving** — it is implemented by the Editor script in §6.

---

## 3. APK payload

**Estimated, not measured** — no APK exists. Source bytes over-state APK impact (FBX → serialized mesh, PNG → ASTC), so a range is given.

### 3.1 Legacy `Race.unity`

* `SceneNames.Race` has **zero references** in any `.cs` file, and there is no `LoadScene("Race")` anywhere. Confirmed dead.
* It sits at **build index 6** of 12 in `EditorBuildSettings.asset`.
* **146 assets totalling 51.47 MB of source bytes are reachable ONLY from it** (closure with vs. without the scene). CLAUDE.md's "23 MB" is understated by ~2×.

| Exclusive to `Race.unity` | Source MB |
|---|---:|
| `Assets/Art/Characters/Ch46_nonPBR.fbx` (the pre-F38 kid, replaced by Aj) | **28.98** |
| Supercyan forest tree diffuse ×2 (2048²) | 3.37 |
| Supercyan skybox ×6 (1500²) | 6.70 |
| Kevin Iglesias human model + 3 run clips | 2.27 |
| ithappy animals (Dog/Kitty/Chicken) + texture | 3.12 |
| Hovl magic circle / healing circle / portal prefabs | 1.84 |
| CFXR prefabs + `cfxr font AmazGoDaBold.png` | ~1.5 |
| ithappy Cartoon City buildings + texture | ~1.2 |
| everything else (109 assets) | ~2.5 |

**Estimated APK saving from dropping it: 12–20 MB.** Dropping the scene from Build Settings is the safe first step; deleting `Ch46_nonPBR.fbx` afterwards is where most of the win actually lands.

### 3.2 Duplicate audio

| | |
|---|---|
| `Assets/Sounds/Stems/STEMSMainTrackMono.ogg` | sha256 `5330441614f15b8a9feedf84dae8007f166389a2b14a5ac81db65d2fd670e006` |
| `Assets/_Game/Resources/Audio/music_race.ogg` | sha256 `5330441614f15b8a9feedf84dae8007f166389a2b14a5ac81db65d2fd670e006` |

✅ **CLAUDE.md's byte-identical claim is confirmed.** Both ship (`music_race.ogg` is in a `Resources/` folder, so it ships unconditionally regardless of Build Settings). **7.28 MB duplicated.**

**Also found, not in CLAUDE.md:** `Assets/_Game/Resources/Audio/music_menu.mp3` is byte-identical to `Assets/Audio/audio_hero_Story-Time_SIPML_Q-0216.mp3` (both sha256 `6b98deb1f1b21057633c2b8da742715f8b647ef481bac3b0dfb1aa6d1d72c377`). The source copy is **unreachable**, so it costs nothing in the APK — but it should be deleted so nobody edits the wrong one.

### 3.3 Uncompressed textures

✅ **CLAUDE.md said "three textures import uncompressed (~9MB incl. `UISpritesheet.png` at 6MB)".** Exactly three reachable textures have `textureCompression: 0` (Uncompressed); the `UISpritesheet` figure is exact; the total is **12.00 MB**, not 9.

| Texture | Dimensions | Disk | RGBA32 in build/RAM | ASTC 4×4 (CompressedHQ) |
|---|---|---:|---:|---:|
| `Assets/UI/UISpritesheet.png` (Sprite) | 2048×779 | 0.83 MB | **6.38 MB** | ~1.60 MB |
| `Assets/_Game/Art/UI/app_icon.png` | 1024×1024 | 1.10 MB | **4.19 MB** | ~1.05 MB |
| `Assets/Plugins/JMO Assets/…/cfxr font AmazGoDaBold.png` (Sprite) | 4949×72 | 0.13 MB | **1.43 MB** | ~0.36 MB |

`app_icon.png` is the app icon source (its guid `101643a65c7e7cd4fa261b323f27ac0e` is `ProjectSettings.m_Icon` for all 6 densities) — **the Editor script deliberately skips it**; compressing an icon source is not worth the risk on a thesis instrument. The other two are compressed by the script: **saving ~5.85 MB.**

Other texture findings:
* **0 reachable textures have Read/Write enabled**, and **0 of 196 reachable models** have `m_IsReadable: 1`. Nothing to fix; the rules exist in the script so this stays true after future imports.
* 14 reachable sprites have mipmaps on, all but one under `Assets/Textures/Graffiti/` (world decals on 3D walls, where mipmaps are *wanted*). The script reports these but does not change them.
* 43 reachable textures still carry **legacy `serializedVersion: 2` importer metas from 2016** (Trash Dash), 15.90 MB of it in `Assets/Textures/*.tif`, with `buildTargetSettings: []` — no platform overrides at all. Unity upgrades them on import to Automatic/Compressed, so they land on ASTC; no action needed, but worth knowing they have never been tuned.

### 3.4 TextMesh Pro Examples & Extras

`Assets/TextMesh Pro/Examples & Extras` = **6.2 MB**, of which **2.3 MB is in its own `Resources/` folder** and therefore ships unconditionally. (CLAUDE.md said 2.7 MB — close.) This is also where F46 found the `TMP_TextInfoDebugTool.cs` unguarded `using UnityEditor;` build blocker: deleting the folder removes both problems.

### 3.5 Music encoding quality (optional, audible in principle)

All 8 long music clips import at **Vorbis `quality: 1` (= 100 %)**. Unity re-encodes at build time, so the *built* clips are near-maximum-bitrate Vorbis — roughly **30 MB of music in the APK**. `MusicPlayer.maxVolume` is **0.1**, i.e. the stems play at 10 % volume under gameplay. Dropping quality to 0.5 would roughly halve that (**~15 MB**) and is inaudible in practice, but it *is* an audio-quality change and so is a **separate, explicitly opt-in menu item** in the Editor script, not part of "apply all safe fixes".

### 3.6 Confirmed non-win: Addressables groups

CLAUDE.md: *"Deleting unused Addressables groups saves ~0 — their meshes/textures are shared, measured."* ✅ **Re-verified independently.** Exclusive footprint per group (closure with vs. without):

| Group | Entries | Exclusive assets | Exclusive MB |
|---|---:|---:|---:|
| Night-Sky / Night-Theme-Obstacles / Day-Theme-Obstacles / Powerups / Coin / Tutorial / UI / Duplicate Asset Isolation (109 entries!) | — | **0** | **0.00** |
| Night-Zones | 4 | 3 | 0.02 |
| Default-Zones | 4 | 3 | 0.02 |
| Particles | 6 | 8 | 0.40 |
| Themes | 3 | 106 | 7.09 ← where the shared art actually lives |
| Characters | 10 | 29 | 16.42 ← includes the live Aj character; not a saving |

All `Night-*` groups combined: **3 assets, 0.02 MB.**

### 3.7 APK total

| Item | Est. APK saving | Look change? |
|---|---:|---|
| Drop `Race.unity` from Build Settings (+ delete `Ch46_nonPBR.fbx`) | **12–20 MB** | No (dead scene) |
| Delete `_Game/Resources/Audio/music_race.ogg` (byte-identical duplicate, dead path) | **7.3 MB** | No |
| Compress `UISpritesheet.png` + `cfxr font` to ASTC 4×4 | **~5.9 MB** | No (near-lossless) |
| Delete `TextMesh Pro/Examples & Extras` | **2.3 MB** (+ removes a build blocker) | No |
| **Safe subtotal** | **≈ 28–36 MB** | |
| *Optional:* music Vorbis quality 1.0 → 0.5 | *≈ 15 MB* | *audible in principle; inaudible at `maxVolume 0.1`* |

Against CLAUDE.md's ~190–230 MB estimate, that lands the build comfortably at **~160–200 MB against the 300 MB cap** — but the cap is not the binding constraint. **RAM is** (§2).

---

## 4. Other things that will bite on a 2GB device

| # | Finding | Evidence | Action |
|---|---|---|---|
| O1 | **`Application.targetFrameRate` is set twice and they disagree.** `Bootstrapper.Awake` sets `GameRules.TargetFrameRate` = **60** once (guarded by `_initialized`); `MusicPlayer.Awake` sets **30** when `MainSummaRace` first loads. Both objects are `DontDestroyOnLoad` and both `Awake` once. Net effect: the app targets **60 fps until the first race and 30 fps for the rest of the session.** | `Assets/_Game/Scripts/Constants/GameRules.cs:123`, `Assets/_Game/Scripts/Core/Bootstrapper.cs:37`, `Assets/Scripts/Sounds/MusicPlayer.cs:32` | Set `GameRules.TargetFrameRate = 30`. Chasing 60 in the menus burns thermal headroom that the race then needs. **Code change — manual.** |
| O2 | **Optimized Frame Pacing (Swappy) is OFF.** With `vSyncCount: 0` and a 30 fps target on a 60 Hz panel, Unity's sleep-based limiter judders — a 30 fps *average* that reads as stutter. | `ProjectSettings.asset:71 androidUseSwappy: 0` | Enable **Player → Android → Optimized Frame Pacing**. Costs nothing visually; the standard fix for a locked 30. **Manual.** |
| O3 | **Vulkan is the first graphics API on Android.** `m_APIs: 150000000b000000` → `0x15` = Vulkan, then `0x0b` = OpenGLES3. Android-8-era Vulkan drivers on budget SoCs are a classic source of black screens and crashes. | `ProjectSettings.asset:486-488` | Test both on the actual study device; if Vulkan misbehaves, ship GLES3-only. **Manual, needs the device.** |
| O4 | **Accelerometer polled at 60 Hz** in a portrait-locked, non-tilt game. | `ProjectSettings.asset:14 accelerometerFrequency: 60` | Set to 0. Small but free. **Manual.** |
| O5 | Android quality level is **1 ("Fast")** with `vSyncCount: 0`, `shadows: 0`, `textureQuality: 0`, `lodBias: 0.4`. Under URP most of these are ignored (the URP asset wins) — **but `vSyncCount` is honoured**, and it is correct. | `QualitySettings.asset` `m_PerPlatformDefaultQuality: Android: 1` | No action. |
| O6 | `Assets/_Recovery/` is still on disk — 4 scenes including a 902 KB `0 (2).unity` — despite F33 recording it as deleted. It is gitignored and not in Build Settings, so **no APK cost**, but it is imported by the Editor and shows up in dependency searches. | `Assets/_Recovery/`, `.gitignore:74` | Delete again. **Manual.** |
| O7 | Correct and worth keeping: IL2CPP (`scriptingBackend Android: 1`), **ARM64 only** (`AndroidTargetArchitectures: 2`), `stripEngineCode: 1`, `.NET Standard 2.1` (`apiCompatibilityLevelPerPlatform Android: 6`), `minSdk 26`, portrait (`defaultScreenOrientation: 0`), `androidBlitType: 1` (Never), `mobileMTRendering: 1`. | `ProjectSettings.asset` | No action. |
| O8 | `managedStrippingLevel Android: 1` (Low). Medium/High would shave a few MB of IL2CPP output but risks stripping reflection-reached types (`StoryLoader` JSON, Addressables). | `ProjectSettings.asset:881` | Leave at Low unless the APK is genuinely over budget. |
| O9 | Scene geometry is not the problem: **1 Camera, 1 Light, 1 MeshRenderer, 0 SkinnedMeshRenderers, 1 ParticleSystem, 3 Canvases** in the saved `MainSummaRace` — everything else is spawned by their `TrackManager` at runtime. | parsed from `Assets/Scenes/MainSummaRace.unity` | No action. |

---

## 5. Prioritised remediation table

| Pri | Change | Est. saving | Changes the look? | Risk | How |
|---|---|---|---|---|---|
| **1** | Stems → CompressedInMemory; `music_menu`/`music_race`/`Sounds/Music` → Streaming | **−124.3 MB RAM** | **No** | Low | **Editor script** (§6) |
| **2** | 22 short Trash Dash SFX: Streaming → DecompressOnLoad | +2.5 MB RAM, −22 streaming decoders, lower input latency | **No** | Low | **Editor script** |
| **3** | `m_ShadowCascadeCount` 4 → 1 and `m_MainLightShadowmapResolution` 2048 → 512 | 3 of 4 shadow passes gone; −94 % shadow texels | **No** (verified §1.3) | Low | **Manual** — `Assets/RenderingPipeline.asset` |
| **4** | Drop `Race.unity` from Build Settings; then delete `Ch46_nonPBR.fbx` | **12–20 MB APK** | **No** (zero call sites) | Low | **Manual** — Build Settings + delete |
| **5** | `m_IntermediateTextureMode` Always → Auto | up to ~497 MB/s bandwidth | **No** | Low–med (may be a no-op) | **Manual** — `Assets/UIRenderer.asset` |
| **6** | Compress `UISpritesheet.png` + `cfxr font` (ASTC 4×4) | **~5.9 MB** | Near-lossless; check the coin/life icons in a playtest | Low | **Editor script** |
| **7** | Delete `_Game/Resources/Audio/music_race.ogg` (byte-identical duplicate, dead path) | **7.3 MB APK + 21 MB RAM if ever loaded** | **No** | Low — but do #4 first | **Manual** |
| **8** | `GameRules.TargetFrameRate` 60 → 30 | thermal headroom for the race | **No** (menus at 30) | Low | **Manual** — code |
| **9** | Enable Optimized Frame Pacing (Swappy) | smoothness at a locked 30 | **No** | Low | **Manual** — Player Settings |
| **10** | Delete `TextMesh Pro/Examples & Extras` | **2.3 MB** + removes an `using UnityEditor;` build blocker | **No** | Low | **Manual** |
| **11** | `m_MainLightShadowsSupported` 1 → 0 | whole shadow pass + variants gone | **No in `MainSummaRace`**; yes in the dead legacy race | Low after #4 | **Manual** |
| **12** | Accelerometer 60 → 0; `_Recovery/` deleted | trivial | No | Low | **Manual** |
| *13* | *Music Vorbis quality 1.0 → 0.5* | *≈ 15 MB APK* | *audible in principle; plays at `maxVolume 0.1`* | *Med* | **Editor script, separate opt-in menu item** |
| *14* | *`m_RenderScale` 1.0 → 0.8* | *−36 % fragment work* | ***YES — visibly softer*** | *Med* | **Manual, last resort** — only if 30 fps is still missed |
| ❌ | *Reassign `Mobile_RPAsset`* | — | ***YES*** — and it turns HDR back on | High | **Do not.** |
| ❌ | *Delete unused Addressables groups* | **0.02 MB** | No | — | Not worth doing. CLAUDE.md was right. |

---

## 6. The Editor script

`Assets/_Game/Editor/DeviceBudgetTools.cs` — menu **`SummaRace/Device Budget/`**. Editor-folder only (`Assembly-CSharp-Editor`), additionally wrapped in `#if UNITY_EDITOR`, so it can never ship.

| Menu item | What it does |
|---|---|
| `1 · Audit (report only)` | Reports everything in this document with live numbers. Changes nothing. |
| `2 · Apply safe audio import settings` | Priorities 1 + 2 above |
| `3 · Apply safe texture + mesh flags` | Priority 6, plus Read/Write and `isReadable` hygiene |
| `4 · Apply ALL safe fixes` | 2 + 3 |
| `5 · OPTIONAL — lower music Vorbis quality to 0.5 (AUDIBLE)` | Priority 13, deliberately separate |

Design constraints it honours:

* **Idempotent** — it reads the current importer value and only writes + reimports when it differs. Running it twice reports "0 changed".
* **Logs every change** — one line per asset, `old → new`, plus a summary and a "manual steps not performed" reminder.
* **Never touches** the render pipeline assets, Build Settings, scenes, prefabs, `ProjectSettings`, or any file (nothing is deleted).
* **Scoped** — audio changes are confined to `Assets/_Game/Resources/Audio`, `Assets/_Game/Resources/Stories/Narration` and `Assets/Sounds` (the entire shipping set), so the 505-file unreachable Kenney source library in `Assets/Audio/Kenney` is never churned.
* **Explicit skip list** — `Assets/_Game/Art/UI/app_icon.png` is never recompressed.

**Run it when the Editor is free** — it triggers reimports of the clips it changes (~30 assets), which takes a minute or two and re-encodes the music.

---

## 7. What is still unmeasured

Android Build Support is **not installed** (`ProjectSettings/ProjectVersion.txt` = `6000.4.1f1`; `Editor/Data/PlaybackEngines` has only `windowsstandalonesupport`). Until the module is added and an APK is produced:

* the APK size figures above are **estimates from source bytes**, not measurements;
* the 30 fps floor has **never been observed on hardware**;
* the RAM figures in §2 are arithmetic from real importer settings and are the most trustworthy numbers in this document, but the actual peak RSS is unknown.

The order of work should be: **install the module → apply the safe fixes (script) → build → measure → only then consider R5 (`renderScale`) or #14.**
