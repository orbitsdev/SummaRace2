# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

SummaRace2 is a **Unity 6 (6000.4.1f1)** project built on the **Universal Render Pipeline (URP 17.4.0)**. It is currently at the template stage: the only C# code is the URP template's `Assets/TutorialInfo` (`Readme`/`ReadmeEditor`), so most game systems are yet to be written.

## Working with the Unity Editor (MCP)

This project has **MCP For Unity** (`com.coplaydev.unity-mcp`) installed. When a Unity Editor instance is connected, prefer the `mcp__UnityMCP__*` tools over editing serialized assets by hand:

- **Read editor/project state via MCP resources**, then mutate via tools — check state before changing it.
- Use `manage_scene`, `manage_gameobject`, `manage_components`, `manage_prefabs`, `manage_material`, etc. for scene/object edits (raw `.unity`/`.asset`/`.prefab` YAML edits bypass Unity's serialization and break GUID/fileID references).
- Use `create_script`/`apply_text_edits`/`manage_script` for C#, then `read_console` to confirm compilation succeeded before continuing.
- Use `run_tests` for Edit/Play mode tests and `manage_editor` (play/pause/stop) for play-mode control.
- Do **not** run Unity CLI `-batchmode` builds/tests while the Editor is open on this project — it locks the project. Drive the open Editor through MCP instead.

If no Unity instance is connected, C# still compiles through the generated `Assembly-CSharp*.csproj`, but scene/asset changes require the Editor.

## Code layout & assemblies

There are **no `.asmdef` files**, so all scripts compile into the two default assemblies:

- Runtime scripts → `Assembly-CSharp` (any `.cs` under `Assets/` not in an `Editor/` folder).
- Editor-only scripts → `Assembly-CSharp-Editor` (any `.cs` inside a folder named `Editor/`, e.g. `Assets/TutorialInfo/Scripts/Editor/`).

If the codebase grows, introduce `.asmdef` files to split runtime/editor/tests before adding significant systems — this keeps compile times down and is required for isolated test assemblies.

## Rendering

URP is configured with **two quality tiers**, selected per platform in Project Settings → Quality / Graphics:

- `Assets/Settings/PC_RPAsset.asset` + `PC_Renderer.asset`
- `Assets/Settings/Mobile_RPAsset.asset` + `Mobile_Renderer.asset`

When adding renderer features, shaders, or post-processing, apply changes to **both** renderers (or deliberately decide a feature is PC- or mobile-only). Volume profiles live in `Assets/Settings/` (`DefaultVolumeProfile`, `SampleSceneProfile`).

## Input

Uses the **new Input System (1.19.0)**, not the legacy `Input` manager. Bindings are defined in `Assets/InputSystem_Actions.inputactions`. Add/modify controls there and reference the generated actions rather than polling `Input.GetKey`.

## Notable packages available

AI Navigation (NavMesh 2.0), Timeline, Visual Scripting, and the Multiplayer Center are installed and can be used without adding dependencies.
