# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

SummaRace is an **offline Android reading game** (Unity 6 / URP 17.4.0, portrait, new Input System) teaching Grade-4 learners to summarize stories via the **SWBST framework** (Somebody·Wanted·But·So·Then). It is a **college thesis instrument** — 40 learners will use it across 10 sessions and be compared to a control group.

**Read the docs in `Documentation/` before making design decisions:**
- `SummaRace_Project_Brief.md` — orientation (read first)
- `SummaRace_Final_GDD.docx` — design bible; all decisions D1–D18 are LOCKED
- `SummaRace_Technical_Design_Document.md` — implementation spec (scenes, scripts, events, race system)
- `SummaRace_Build_Guide.md` — build order (phases A–J)
- `SummaRace_Asset_Requirements_List.md` — asset bill of materials
- `Documentation/Mockups/1..27.png` — validated prototype screens (visual target). Live demo: https://tdrx44.csb.app/

**Non-negotiables:** 100% offline (no networking/ads/analytics), never punish the learner (wrong answers never block; "caught" is friendly, never game-over), content is JSON data never code, all 30 stories flow through one reusable engine.

## Build state (updated 2026-07-16)

| Phase | Status |
|---|---|
| B skeleton, C core systems, D content pipeline | ✅ done |
| **E vertical slice (M1)** — full loop Boot→MainMenu→StorySelect→Reader→Race→Arrange→Summary→Results, grey-box, sounds wired | ✅ **code-complete**; full-loop playtest by owner pending |
| **F1 typography** — all UI on TextMeshPro; TMP assets in `Art/Fonts/TMP/` (Fredoka-SemiBold = headings/buttons, Nunito-Regular = body + TMP default, Nunito-Bold = feedback); Summary uses TMP_InputField; Results stars are Hyper_Casual_UI sprite Images | ✅ done (verify in playtest) |
| **F2 UI skin** — Hyper_Casual_UI kit applied: 9-sliced pill buttons + Rectangle 356 panels everywhere, sky/curved-hill backdrops (generated `_Game/Art/UI/hill_arc.png`), race banner on toggled BannerBG pill | ✅ done (verify in playtest) |
| **F3 juice + bordered panels** — major cards on the kit's bordered popup sprites (gold `Daily Reward pannel`, teal `Frame 1583`); `SummaRace.UI.ButtonSquash` on every button, `PanelIntro` pop-in on cards/popups; PrimeTween PunchScale on stars + race feedback | ✅ done (verify in playtest) |
| **F4 race reskin** — `_Game/Prefabs/PlayerCharacter` (Ch46 kid) + `PatrolCharacter` (Ty, ZombieRun) spawn via `RaceController.playerModelPrefab/patrolModelPrefab` (grey-box fallback if unwired); AnimatorControllers in `_Game/Animation/` (Running bool, Stumble/Dance triggers); grass track w/ blue-yellow-green lanes | ✅ done (verify in playtest) |
| **F4b world dressing** — Supercyan forest props (URP-converted) line the race track via `RaceController.sceneryPrefabs`/`BuildScenery`, deterministic scatter, 2x scale | ✅ done (verify in playtest) |
| **F5 hero + world labels** — StorySelect easy card shows hero image + title from story JSON (missing art → title-only fallback); race world text is 3D TMP (Fredoka) on dark backing quads | ✅ done (verify in playtest) |
| **Phase F complete** → owner full-loop playtest = the "M1 polished" gate. Then Phase G (29 stories + SessionMap — use Hyper_Casual_UI `Level screen.png` as SessionMap template) | ⬜ next |
| G 29 more stories + SessionMap · H asset pass · I research features (PIN/logging/export) · J device builds | ⬜ later |

Post-MVP scenes NameEntry/SessionMap/TeacherMenu/Settings exist as named scenes but are empty/placeholder by design. SampleScene deleted (GDD D18 done).

**Known flags:**
- `s01_easy.json` page-split/questions/distractors were AI-authored (source doc lacked them for Day 1) — **needs researcher review** before study build (GDD D6).
- `Resources/Stories/Art/s01_easy.png` is a **TEMP hero image** cropped from Mockups/3.png — replace when the researcher locks the 30-image style (Phase G). No image-gen provider keys are configured in MCP (`generate_image` needs fal.ai/OpenRouter key if wanted).
- ~~BitGem `cop.fbx` external-material warnings~~ **fixed** (InPrefab import + URP-converted `cop_blue.mat`); note cop is a **Generic** rig — it cannot play the Mixamo Humanoid clips, so the patrol uses Ty instead. `Aj.fbx` renders invisible (mesh offset?) — parked, not used.
- Narration plan: pre-generate clips with **edge-tts** (`en-PH-RosaNeural`) into `Resources/Stories/Narration/` named `s01_easy_p1` etc. Runtime TTS is forbidden (offline rule). Not yet generated.
- Race world-space labels (pickups, gates, FINISH) still use legacy 3D `TextMesh` — restyle during the race reskin (F4), not part of the TMP UI migration.
- Design references inside Hyper_Casual_UI `Sprites/GameUI/`: `Victory Pannel.png` (Results screen, applied), `Level screen.png` (numbered circle grid = template for Phase G SessionMap), `Main Menu.png` (glossy icon pills). The pack's demo scenes are baked showcase sprites, not prefabs; several kit sprites ship with broken import settings (not Sprite type / Multiple mode with no rects) — fix importer before use.

## Architecture (implemented — follow these patterns)

- **Singletons** (`_Game/Scripts/Core/`): `Bootstrapper` (Boot scene) creates `[Core]` GameObject with `GameManager`, `AudioManager`, `SaveManager`, `SceneLoader` (DontDestroyOnLoad). `EventBus` is static pub/sub; event payloads in `Core/GameEvents.cs` (catalog in TDD §8).
- **Features never call each other** — raise EventBus events + read `GameManager` state. Scene flow only via `SceneLoader.Instance.Load(SceneNames.X)`.
- **Constants only** (`_Game/Scripts/Constants/`): `SceneNames`, `AudioKeys`, `PrefKeys`, `GameRules` (all tuning numbers), `GameText` (all learner-facing strings). Never hard-code a scene name, audio key, gameplay number, or UI string.
- **Content pipeline** (`_Game/Scripts/Data/`): `StoryLoader.Load("s01_easy")` reads `Resources/Stories/<id>.json` → validated `StoryData`. Add a story = add a JSON, zero code.
- **Audio**: clips live in `Assets/_Game/Resources/Audio/` named exactly per `AudioKeys` (e.g. `sfx_correct.ogg`); `AudioManager.Instance.PlaySfx(AudioKeys.SfxCorrect)`. Swap a sound = replace the file. Raw Kenney packs in `Assets/Audio/Kenney/` are the source library.
- **Every scene controller** null-checks `GameManager.Instance`/`CurrentStory` and falls back to `StoryLoader.Load("s01_easy")` so any scene can be played directly in-editor (TDD §13).
- **Naming**: PascalCase scripts/scenes/methods, `_camelCase` privates, `[SerializeField] private` over public, snake_case asset files, namespaces `SummaRace.<Layer>` matching folders.
- **Third-party packs go in `Assets/Plugins/`** (13 packs there now). Design reference for UI skin: `Plugins/Hyper_Casual_UI/Scenes/Demo_Game_UI`. Fonts: `Assets/Art/Fonts/` Fredoka (headings) + Nunito (body) — make TMP assets in Phase F. Characters + Mixamo clips (all Humanoid): `Assets/Art/Characters/`.

## Workflow rules (owner's standing instructions)

- **Professional quality at every stage**: grey-box = placeholder *visuals* only; logic must always be correct, complete, sound-wired, dead-end-free. Placeholder screens allowed only as temporary "never a dead end" stubs.
- **Commit + push after every verified milestone** (small commits). Owner playtests in the Editor before a milestone counts as done.
- Grey-box first, one story before thirty, GDD wins design arguments, docs win over improvisation.

## Working with the Unity Editor (MCP)

This project has **MCP For Unity** (`com.coplaydev.unity-mcp`) installed. When a Unity Editor instance is connected, prefer the `mcp__UnityMCP__*` tools over editing serialized assets by hand:

- **Read editor/project state via MCP resources**, then mutate via tools — check state before changing it.
- Scenes/objects: `manage_scene`, `manage_gameobject`, etc. — raw `.unity`/`.prefab` YAML edits break GUID/fileID references. Complex scene builds work well via `execute_code` (C#6-ish subset; use `UnityEngine.Object.DestroyImmediate`, wire `[SerializeField]` fields via `SerializedObject`).
- C#: write files, then refresh with compile and verify the type exists in `Assembly-CSharp` before building scenes that reference it.
- Do **not** run Unity CLI `-batchmode` while the Editor is open — it locks the project.
- **Gotchas learned:** (1) If a new `.cs` validates clean but its type never appears in `Assembly-CSharp` (compile time ~1ms, file missing from CompilationPipeline sources), Unity's incremental compiler has stale tracking → delete the `.cs`+`.meta`, refresh, re-add the file fresh. (2) Play mode pauses without Editor focus (Run In Background off) — the MCP bridge stalls; have the owner click Unity/press Play. (3) Asset-store imports land in `Assets/` root — move to `Plugins/` via `AssetDatabase.MoveAsset` (or move file+`.meta` together on disk), never a bare file move.

## Code layout & assemblies

No `.asmdef` files: runtime → `Assembly-CSharp`, editor-only (`Editor/` folders) → `Assembly-CSharp-Editor`. Introduce asmdefs before adding test assemblies or if compile times grow.

## Rendering

Two URP tiers (Project Settings → Quality/Graphics): `Assets/Settings/PC_RPAsset` + `PC_Renderer`, `Mobile_RPAsset` + `Mobile_Renderer`. Apply renderer/shader/post changes to **both** (or deliberately tier-gate). Device floor: 2GB Android 8, 30fps in race, APK ≤ 300MB. Android: IL2CPP, ARM64, min API 26, portrait locked.

## Input

New Input System only (`Assets/InputSystem_Actions.inputactions`). UI needs `InputSystemUIInputModule` on the EventSystem. Race lane-switching polls `Keyboard.current`/`Touchscreen.current` (see `PlayerRunner`).
