# Full-Project Audit Execution Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce `docs/superpowers/audits/2026-07-18-full-project-audit.md` — a complete findings document over every SummaRace scene/object/prefab/script plus a race keep/kill verdict — per the spec `docs/superpowers/specs/2026-07-18-full-project-audit-design.md`.

**Architecture:** Read-only inspection of the live Unity Editor via MCP (`mcp__UnityMCP__*` tools), one scene per task in play order, each task appending a findings section to the audit doc and committing it. Reusable inspection snippets are written once (Task 1) and executed via `execute_code` in every scene task.

**Tech Stack:** Unity 6 / URP, MCP For Unity (`manage_scene`, `execute_code`, `read_console`), git, PowerShell.

## Global Constraints

- **Read-only audit:** no scene, prefab, or script mutations. The screenshot recipe temporarily dirties open scenes — ALWAYS restore by reopening the scene from disk afterwards (`EditorSceneManager.OpenScene(path)` without saving).
- Never run Unity CLI `-batchmode` while the Editor is open.
- Finding format (from spec): `ID · Path (scene path or file:line) · Remove/Fix/Change/Missing · Blocker/Should-fix/Polish · Evidence · Recommendation`. IDs are `AUD-<scene>-<n>` (e.g. `AUD-BOOT-1`).
- Judge every finding as QA + Developer + Designer per the spec rubric (dead ends / architecture violations / GDD-mockup alignment, never-punish, Grade-4 readability).
- Blocker = breaks study loop, violates a non-negotiable (offline, never-punish, content-as-data), or ships contamination (ads/IAP).
- Cross-reference sources: `Documentation/SummaRace_Technical_Design_Document.md`, `Documentation/Mockups/*.png`, `_Game/Scripts/Constants/GameText.cs`, `GameRules.cs`, `SceneNames.cs`, `AudioKeys.cs`, `Constants/SwbstPalette.cs`.
- Screenshots go to `docs/superpowers/audits/shots/<scene>.png` (540×960).
- Commit after every task: `git add docs/superpowers/audits && git commit -m "Audit: <scene> findings"`.
- Unity MCP gotchas that apply here: `read_console` may drop entries while the editor is unfocused (verify via direct state probes); overlay canvases can't be captured by `manage_camera` (use the Task 1 recipe); portrait view via `UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1080, 1920, "Portrait 1080x1920")`.

---

### Task 1: Audit scaffolding + reusable inspection snippets

**Files:**
- Create: `docs/superpowers/audits/2026-07-18-full-project-audit.md` (skeleton)
- Create: `docs/superpowers/audits/snippets/hierarchy_dump.cs`
- Create: `docs/superpowers/audits/snippets/screenshot_overlay.cs`
- Create: `docs/superpowers/audits/shots/` (directory)

**Interfaces:**
- Produces: the audit doc skeleton every later task appends to; two `execute_code` snippet files every scene task runs verbatim (with `___SCENE___` / `___OUT___` placeholders substituted per scene).

- [ ] **Step 1: Verify the Unity MCP connection and pin the instance**

Read MCP resource `mcpforunity://instances`; if more than one instance, call `set_active_instance` with the SummaRace2 entry. Then read `mcpforunity://editor/state` and confirm `data.advice.ready_for_tools` is true and the project path ends in `SummaRace2`.

- [ ] **Step 2: Set portrait game view**

Via `execute_code`:

```csharp
UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1080, 1920, "Portrait 1080x1920");
```

- [ ] **Step 3: Write the audit doc skeleton**

Create `docs/superpowers/audits/2026-07-18-full-project-audit.md`:

```markdown
# SummaRace Full-Project Audit — 2026-07-18

Branch: experiment/endless-override-2 · Spec: ../specs/2026-07-18-full-project-audit-design.md
Finding format: ID · Path · Remove/Fix/Change/Missing · Blocker/Should-fix/Polish · Evidence · Recommendation

## 1. Boot
## 2. MainMenu
## 3. StorySelect
## 4. Reader
## 5. Race (original, _Game/Scenes/Race.unity)
## 6. Race (EXP3, Assets/Scenes/MainSummaRace.unity)
## 7. Arrange
## 8. Summary
## 9. Results
## 10. Placeholder scenes (Settings, NameEntry, SessionMap, TeacherMenu)
## 11. Project level
## 12. Full-loop playtest notes
## 13. Race verdict
## 14. Summary tables
```

- [ ] **Step 4: Write `hierarchy_dump.cs`** (run per scene via `execute_code` after opening the scene; prints full hierarchy incl. inactive objects, components, and missing-script markers)

```csharp
// C#6-safe (no local functions): iterative depth-first walk, inactive objects included.
var sb = new System.Text.StringBuilder();
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
sb.AppendLine("SCENE: " + scene.path);
var stack = new System.Collections.Generic.Stack<System.Collections.Generic.KeyValuePair<UnityEngine.Transform, int>>();
var roots = scene.GetRootGameObjects();
for (int r = roots.Length - 1; r >= 0; r--)
    stack.Push(new System.Collections.Generic.KeyValuePair<UnityEngine.Transform, int>(roots[r].transform, 0));
while (stack.Count > 0)
{
    var pair = stack.Pop();
    var t = pair.Key; int depth = pair.Value;
    var go = t.gameObject;
    var comps = go.GetComponents<UnityEngine.Component>();
    var names = new System.Collections.Generic.List<string>();
    foreach (var c in comps) names.Add(c == null ? "MISSING_SCRIPT" : c.GetType().Name);
    string pad = new string(' ', depth * 2);
    sb.AppendLine(pad + (go.activeInHierarchy ? "" : "[INACTIVE] ") + go.name + "  <" + string.Join(",", names.ToArray()) + ">");
    // surface learner-visible text so wrong labels are caught in the dump
    var tmp = go.GetComponent<TMPro.TMP_Text>();
    if (tmp != null) sb.AppendLine(pad + "  TEXT=\"" + tmp.text.Replace("\n", "\\n") + "\" font=" + (tmp.font != null ? tmp.font.name : "NULL"));
    var legacy = go.GetComponent<UnityEngine.TextMesh>();
    if (legacy != null) sb.AppendLine(pad + "  LEGACYTEXT=\"" + legacy.text + "\"");
    for (int i = t.childCount - 1; i >= 0; i--)
        stack.Push(new System.Collections.Generic.KeyValuePair<UnityEngine.Transform, int>(t.GetChild(i), depth + 1));
}
System.IO.File.WriteAllText(@"___OUT___", sb.ToString());
return "wrote " + sb.Length + " chars";
```

(Substitute `___OUT___` with an absolute path like `C:\Users\Owner\Documents\GitHub\SummaRace2\docs\superpowers\audits\shots\<scene>_hierarchy.txt`; read the file afterwards with the Read tool — don't return huge dumps through MCP.)

- [ ] **Step 5: Write `screenshot_overlay.cs`** (per-scene UI capture; from the proven recipe)

```csharp
// Capture Screen Space Overlay canvases in edit mode:
// 1) temp camera far away, 2) switch overlay canvases to ScreenSpaceCamera,
// 3) render to 540x960 RT, 4) save PNG, 5) caller MUST reopen scene from disk to discard dirt.
var camGo = new UnityEngine.GameObject("___AuditCam");
var cam = camGo.AddComponent<UnityEngine.Camera>();
camGo.transform.position = new UnityEngine.Vector3(9000, 9000, 9000);
cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
cam.backgroundColor = UnityEngine.Color.black;
var canvases = UnityEngine.Object.FindObjectsByType<UnityEngine.Canvas>(UnityEngine.FindObjectsInactive.Exclude, UnityEngine.FindObjectsSortMode.None);
foreach (var cv in canvases)
{
    if (cv.renderMode == UnityEngine.RenderMode.ScreenSpaceOverlay)
    { cv.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera; cv.worldCamera = cam; cv.planeDistance = 1f; }
}
var rt = new UnityEngine.RenderTexture(540, 960, 24);
cam.targetTexture = rt;
cam.Render();
UnityEngine.RenderTexture.active = rt;
var tex = new UnityEngine.Texture2D(540, 960, UnityEngine.TextureFormat.RGB24, false);
tex.ReadPixels(new UnityEngine.Rect(0, 0, 540, 960), 0, 0);
tex.Apply();
System.IO.File.WriteAllBytes(@"___OUT___", tex.EncodeToPNG());
UnityEngine.RenderTexture.active = null;
cam.targetTexture = null;
UnityEngine.Object.DestroyImmediate(camGo);
UnityEngine.Object.DestroyImmediate(rt);
UnityEngine.Object.DestroyImmediate(tex);
return "saved ___OUT___";
```

For 3D-world scenes (both races) also capture through the real scene camera with `manage_camera` (it can see world objects; only overlay UI needs the recipe).

- [ ] **Step 6: Commit**

```powershell
git add docs/superpowers/audits; git commit -m "Audit: scaffolding + inspection snippets"
```

---

### Task 2: Boot scene audit

**Files:**
- Modify: `docs/superpowers/audits/2026-07-18-full-project-audit.md` (section 1)
- Read: `Assets/_Game/Scenes/Boot.unity` (via editor), `_Game/Scripts/Core/Bootstrapper.cs`, `Constants/GameText.cs`, `Constants/GameRules.cs`

**Interfaces:**
- Consumes: Task 1 snippets (substitute `___OUT___` → `...\shots\boot_hierarchy.txt` / `...\shots\boot.png`).
- Produces: findings `AUD-BOOT-*` in section 1.

- [ ] **Step 1: Open the scene** — `manage_scene` open `Assets/_Game/Scenes/Boot.unity`.
- [ ] **Step 2: Dump hierarchy** — run `hierarchy_dump.cs`; Read the output file.
- [ ] **Step 3: Screenshot** — run `screenshot_overlay.cs` → `shots/boot.png`; then reopen Boot from disk (restore). View the PNG.
- [ ] **Step 4: Wiring check** — via `execute_code`, inspect `Bootstrapper` serialized fields (`SerializedObject`) for null references (tagline TMP, splashFill, logo lockup parts). Confirm tagline/loading text come from `GameText`, splash time from `GameRules.SplashSeconds`, next scene from `SceneNames`.
- [ ] **Step 5: Judge & record** — check against CLAUDE.md F14/F15/F27/F28 (bg_logo_radial backdrop, LogoLockup SUMMA/RACE!, crown, progress bar, no leftover treasure decor), wrong/misspelled labels, inactive junk objects, missing scripts. Write findings to section 1 with the required format; explicitly note "no findings" categories too.
- [ ] **Step 6: Commit** — `git add docs/superpowers/audits; git commit -m "Audit: Boot findings"`.

---

### Task 3: MainMenu scene audit

**Files:**
- Modify: audit doc section 2
- Read: `Assets/_Game/Scenes/MainMenu.unity` (via editor), `_Game/Scripts/UI/` menu controller scripts

**Interfaces:**
- Consumes: Task 1 snippets (`___OUT___` → `shots/mainmenu_hierarchy.txt` / `shots/mainmenu.png`).
- Produces: findings `AUD-MENU-*`.

- [ ] **Step 1: Open** `Assets/_Game/Scenes/MainMenu.unity`.
- [ ] **Step 2: Dump hierarchy**; Read output.
- [ ] **Step 3: Screenshot**; restore scene; view PNG.
- [ ] **Step 4: Wiring check** — TAP TO START target scene (must be `SceneNames.StorySelect` per TDD flow), button sounds via `AudioKeys`, `GameManager.Instance` null-check fallback, ButtonSquash/PanelIntro presence. Verify F24/F28 lockup matches Boot's (owner flagged "home and menu" specifically — compare the two lockups for drift).
- [ ] **Step 5: Judge & record** — labels vs `GameText`, dead/inactive objects, mockup alignment (Mockups 1–2), Settings/voice buttons state.
- [ ] **Step 6: Commit** `"Audit: MainMenu findings"`.

---

### Task 4: StorySelect scene audit

**Files:**
- Modify: audit doc section 3
- Read: `Assets/_Game/Scenes/StorySelect.unity`, StorySelect controller script, `GameText.cs` (StorySelectTitle/Difficulty*/Locked*)

**Interfaces:**
- Consumes: Task 1 snippets (`shots/storyselect_hierarchy.txt` / `shots/storyselect.png`).
- Produces: findings `AUD-SEL-*`.

- [ ] **Step 1: Open** `Assets/_Game/Scenes/StorySelect.unity`.
- [ ] **Step 2: Dump hierarchy**; Read output.
- [ ] **Step 3: Screenshot**; restore; view.
- [ ] **Step 4: Wiring check** — EASY card → Reader flow, hero image fallback path, `GameManager.GetBestStars` star row, locked AVERAGE/HARD cards (chip + lock + hint text from `GameText`), Back button target.
- [ ] **Step 5: Judge & record** — F22 redesign intact, labels, dead objects, Mockup 3 alignment, Grade-4 readability of locked hints.
- [ ] **Step 6: Commit** `"Audit: StorySelect findings"`.

---

### Task 5: Reader scene audit

**Files:**
- Modify: audit doc section 4
- Read: `Assets/_Game/Scenes/Reader.unity`, Reader controller, `StoryLoader.cs`, `Resources/Stories/s01_easy.json`

**Interfaces:**
- Consumes: Task 1 snippets (`shots/reader_hierarchy.txt` / `shots/reader.png`).
- Produces: findings `AUD-READ-*`.

- [ ] **Step 1: Open** `Assets/_Game/Scenes/Reader.unity`.
- [ ] **Step 2: Dump hierarchy**; Read output.
- [ ] **Step 3: Screenshot**; restore; view.
- [ ] **Step 4: Wiring check** — page navigation (no dead end on last page), narration autoplay + VOICE ON/OFF persistence (`PrefKeys.NarrationOn`), **which race scene the "start race" button loads** (`SceneNames.RaceEndless` on this branch — record it; this is race-verdict evidence), `StoryLoader.Load("s01_easy")` fallback, bg_playground backdrop.
- [ ] **Step 5: Judge & record** — body font (Nunito) + size for Grade 4, labels vs `GameText`, teacher avatar TEMP art flag, Mockup 4–6 alignment.
- [ ] **Step 6: Commit** `"Audit: Reader findings"`.

---

### Task 6: Original Race scene audit (`_Game/Scenes/Race.unity`)

**Files:**
- Modify: audit doc section 5
- Read: `Assets/_Game/Scenes/Race.unity`, `_Game/Scripts/` `RaceController.cs`, `PlayerRunner.cs`, `CoinPickup.cs`, `OptionPickup.cs`, `CurveDip.cs`, `RaceChaseCamera.cs`

**Interfaces:**
- Consumes: Task 1 snippets (`shots/race_orig_hierarchy.txt` / `shots/race_orig_ui.png`).
- Produces: findings `AUD-RACE-*` + inputs to the Task 13 verdict.

- [ ] **Step 1: Open** `Assets/_Game/Scenes/Race.unity`.
- [ ] **Step 2: Dump hierarchy**; Read output.
- [ ] **Step 3: Screenshots** — overlay recipe for HUD → `shots/race_orig_ui.png`; plus `manage_camera` world shot from the scene camera → `shots/race_orig_world.png`; restore scene.
- [ ] **Step 4: Wiring check** — `RaceController` serialized arrays (playerModelPrefab, patrolModelPrefab, sceneryPrefabs, roadSegmentPrefabs, cloudPrefabs, FX prefabs) for null/empty slots; gate count & checkpointSpacing vs `GameRules`; never-punish path (caught → friendly, no game-over); FINISH → `SceneNames.Arrange`; legacy `TextMesh` usage (known flag); mode (road vs park) actually configured.
- [ ] **Step 5: Judge & record** — size/complexity of RaceController (F4→F31 accretion — dead code, park-mode leftovers, unused fields), SWBST palette on gates, TDD §11.4 collection rules, 2GB-device risk items (bloom, FX count).
- [ ] **Step 6: Commit** `"Audit: original Race findings"`.

---

### Task 7: EXP3 Race scene audit (`Assets/Scenes/MainSummaRace.unity`)

**Files:**
- Modify: audit doc section 6
- Read: `Assets/Scenes/MainSummaRace.unity`, `EndlessRaceDirector.cs` (locate via Grep), the one guarded line in `TrackManager.cs`

**Interfaces:**
- Consumes: Task 1 snippets (`shots/race_exp3_hierarchy.txt` / `shots/race_exp3_ui.png`).
- Produces: findings `AUD-EXP3-*` + inputs to the Task 13 verdict.

- [ ] **Step 1: Open** `Assets/Scenes/MainSummaRace.unity`.
- [ ] **Step 2: Dump hierarchy**; Read output.
- [ ] **Step 3: Screenshots** — overlay + world as in Task 6 → `shots/race_exp3_*.png`; restore.
- [ ] **Step 4: Wiring check** — `EndlessRaceDirector` fields; 5-gate sequence per TDD §11.4 (sequential, boost/slow, glowing correct card must be collected); lives pinned/never-punish (EXP3-16); FINISH → Arrange; Trash Dash leftovers active in-scene (shop hooks, missions, ads UI, currency HUD — anything not needed by the SWBST race); music handoff at FINISH.
- [ ] **Step 5: Judge & record** — contamination inventory: what this scene drags in (packages: ads/analytics/IAP; scripts; Addressables) = the port/decontamination cost if EXP3 wins the verdict. Also list what the SummaRace theming still lacks vs the original race (branding, SWBST palette, teacher-facing polish).
- [ ] **Step 6: Commit** `"Audit: EXP3 race findings"`.

---

### Task 8: Arrange scene audit

**Files:**
- Modify: audit doc section 7
- Read: `Assets/_Game/Scenes/Arrange.unity`, Arrange controller, `Constants/SwbstPalette.cs`

**Interfaces:**
- Consumes: Task 1 snippets (`shots/arrange_hierarchy.txt` / `shots/arrange.png`).
- Produces: findings `AUD-ARR-*`.

- [ ] **Step 1: Open** `Assets/_Game/Scenes/Arrange.unity`.
- [ ] **Step 2: Dump hierarchy**; Read output.
- [ ] **Step 3: Screenshot**; restore; view.
- [ ] **Step 4: Wiring check** — slot fill/lock states, SWBST pastel/deep colors (F18), wrong-order handling is never-punish, continue → Summary, teacher avatar TEMP crop flag, works when entered directly (fallback story).
- [ ] **Step 5: Judge & record** — labels, dead objects, Mockup 21 alignment.
- [ ] **Step 6: Commit** `"Audit: Arrange findings"`.

---

### Task 9: Summary scene audit

**Files:**
- Modify: audit doc section 8
- Read: `Assets/_Game/Scenes/Summary.unity`, Summary controller

**Interfaces:**
- Consumes: Task 1 snippets (`shots/summary_hierarchy.txt` / `shots/summary.png`).
- Produces: findings `AUD-SUM-*`.

- [ ] **Step 1: Open** `Assets/_Game/Scenes/Summary.unity`.
- [ ] **Step 2: Dump hierarchy**; Read output.
- [ ] **Step 3: Screenshot**; restore; view.
- [ ] **Step 4: Wiring check** — TMP_InputField behavior (mobile keyboard implications), SWBST reference list colors, submit → Results with no validation dead end (empty input must not block — never-punish), reference list matches story JSON.
- [ ] **Step 5: Judge & record** — labels, readability, Mockup 22–24 alignment.
- [ ] **Step 6: Commit** `"Audit: Summary findings"`.

---

### Task 10: Results scene audit

**Files:**
- Modify: audit doc section 9
- Read: `Assets/_Game/Scenes/Results.unity`, `ResultsController.cs`

**Interfaces:**
- Consumes: Task 1 snippets (`shots/results_hierarchy.txt` / `shots/results.png`).
- Produces: findings `AUD-RES-*`.

- [ ] **Step 1: Open** `Assets/_Game/Scenes/Results.unity`.
- [ ] **Step 2: Dump hierarchy**; Read output.
- [ ] **Step 3: Screenshot**; restore; view.
- [ ] **Step 4: Wiring check** — star logic + `GameManager.GetBestStars` persistence, `RevealTreasure()` SWBST chips (F20), victory sting vs race music handoff, next-destination buttons (replay / story select — no dead end), editor-direct fallback.
- [ ] **Step 5: Judge & record** — labels, trophy/star art states, Mockup 25–27 alignment.
- [ ] **Step 6: Commit** `"Audit: Results findings"`.

---

### Task 11: Placeholder scenes audit (Settings, NameEntry, SessionMap, TeacherMenu)

**Files:**
- Modify: audit doc section 10
- Read: the four `.unity` files via editor

**Interfaces:**
- Consumes: Task 1 snippets (`shots/<name>_hierarchy.txt`; screenshot only if a scene has visible UI).
- Produces: findings `AUD-STUB-*`.

- [ ] **Step 1–4: For each scene** (open → dump → screenshot if non-empty): verify it is an honest stub — if anything in the flow links TO it, it must show something and offer a way back (never a dead end); no half-built junk objects; note owner's flag that Settings/home need attention: record exactly what Settings currently contains vs. what the GDD requires of it (this becomes a **Missing** finding, not silent).
- [ ] **Step 5: Record findings** for all four in section 10.
- [ ] **Step 6: Commit** `"Audit: placeholder scenes findings"`.

---

### Task 12: Project-level audit

**Files:**
- Modify: audit doc section 11
- Read: `ProjectSettings/EditorBuildSettings.asset`, `Packages/manifest.json`, `Assets/AddressableAssetsData/AssetGroups/*.asset`, `Assets/_Recovery/`, `Assets/Scenes/`, `Assets/_Game/Prefabs/`, `Assets/_Game/Scripts/` (full sweep)

**Interfaces:**
- Consumes: nothing scene-specific (static pass, no editor needed except prefab checks).
- Produces: findings `AUD-PROJ-*`.

- [ ] **Step 1: Build settings** — Read `EditorBuildSettings.asset`; verify all 12 core scenes present with correct GUIDs (compare each scene's `.meta`), note EXP3 scene entries.
- [ ] **Step 2: Manifest** — Read `Packages/manifest.json`; list every non-shippable package (com.unity.ads / analytics / purchasing / anything networked). On this branch they're expected — the finding records exactly what must not reach main.
- [ ] **Step 3: Cruft sweep** — `Assets/_Recovery/0.unity` + untracked `0 (1).unity` (open briefly to confirm nothing unique lives there before recommending Remove), `Assets/Scenes/Main|Shop|Start.unity` (Trash Dash originals), stray root-level assets (Glob `Assets/*` top level).
- [ ] **Step 4: Prefab pass** — Glob `Assets/_Game/Prefabs/**`; for each prefab confirm something references it (Grep scenes/scripts for its name); orphans → Remove candidates.
- [ ] **Step 5: Script sweep** — Glob `Assets/_Game/Scripts/**/*.cs`; Grep each type name across `Assets/_Game` + both race scenes; unreferenced types → Remove candidates. Also Grep for hard-coded scene-name strings (`"Boot"|"MainMenu"|...` outside `SceneNames.cs`), learner-facing string literals outside `GameText.cs`, and magic numbers outside `GameRules.cs` in controllers.
- [ ] **Step 6: Record + commit** `"Audit: project-level findings"`.

---

### Task 13: Full-loop playtest + race verdict + summary tables

**Files:**
- Modify: audit doc sections 12–14

**Interfaces:**
- Consumes: all prior findings.
- Produces: the race keep/kill recommendation; final tables (counts by severity/classification, ordered blocker list).

- [ ] **Step 1: Full-loop check** — attempt MCP play mode (Boot → … → Results) with state probes; if the bridge stalls (Run In Background off), record what was verified statically and mark the loop check "owner playtest required" — do not fake it.
- [ ] **Step 2: Race verdict** — write section 13 comparing Tasks 6 vs 7 findings: gameplay quality, TDD §11 compliance, contamination/port cost, polish debt; end with one recommendation and its migration outline (what moves to main, what gets deleted).
- [ ] **Step 3: Summary tables** — section 14: findings count by scene × severity; the ordered Blocker list; top-10 Should-fix.
- [ ] **Step 4: Spec-coverage self-check** — every spec scope item (1–10 in the spec's "Order of work") has a non-empty section or an explicit "no findings".
- [ ] **Step 5: Commit** `"Audit: verdict + summary — audit complete"` and push.

---

### Task 14: Fix-plan handoff

- [ ] **Step 1:** Invoke `superpowers:writing-plans` with the completed audit doc as the spec, producing `docs/superpowers/plans/2026-07-<dd>-audit-fixes.md` ordered Blockers → Should-fix → Polish (study-blocking first). This is a separate plan authored AFTER findings exist — do not pre-write fixes now.
