// BuildPreflight.cs — read-only, never mutates the project.
//
// Everything that can go wrong on the way to an APK, checked before the APK exists.
// The study has one shot at 40 tablets; this is the checklist that runs in seconds
// instead of a 20-minute build that fails on something a grep would have caught.
//
// Menu: SummaRace ▸ Build Preflight
//
// Design rules for anyone extending this file:
//   * READ-ONLY. No PlayerSettings setters, no AssetDatabase writes, no scene opens.
//   * NEVER THROW. Every check runs inside a try/catch; a broken check reports itself
//     as ERROR and the rest of the report still prints.
//   * NO ANDROID-MODULE TYPES. This tool has to run on a machine where Android Build
//     Support is *not* installed — that is its main job — so it must not reference
//     UnityEditor.Android.*. Settings that only live in the module's serialized data
//     (platform icons) are read out of ProjectSettings/ProjectSettings.asset as text.
//   * NO PACKAGE ASSEMBLY REFERENCES. Addressables settings are read from their asset
//     file as text for the same reason: a missing package must not break compilation.
//   * RE-DERIVE, DON'T HARDCODE. Story/clip/scene counts come from the constants and
//     the loader the game itself uses, so the report cannot drift from the game.
//   * "PLAYER CODE" IS NOT THE SAME AS "Assets/". A package whose Runtime asmdef has an
//     empty includePlatforms compiles into the APK exactly like our own scripts — this
//     project has one (com.coplaydev.unity-mcp, a git-URL package). Every source scan
//     therefore covers Assets/ AND the runtime assemblies of packages that are not
//     Unity-registry packages. See EnumerateRuntimeScripts.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using SrConst = SummaRace.Constants;
using SrData = SummaRace.Data;

namespace SummaRace.EditorTools
{
    public static class BuildPreflight
    {
        // ------------------------------------------------------------------ result model

        private enum Level { Pass, Info, Warn, Fail, Error }

        private sealed class Result
        {
            public Level Level;
            public string Section;
            public string Title;
            public string Detail;
            public string Remedy;
        }

        private static readonly List<Result> Results = new List<Result>();
        private static string _section = "";

        private static void Section(string name) { _section = name; }

        private static void Add(Level level, string title, string detail = null, string remedy = null)
        {
            Results.Add(new Result { Level = level, Section = _section, Title = title, Detail = detail, Remedy = remedy });
        }

        private static void Pass(string t, string d = null) => Add(Level.Pass, t, d);
        private static void Info(string t, string d = null) => Add(Level.Info, t, d);
        private static void Warn(string t, string d = null, string r = null) => Add(Level.Warn, t, d, r);
        private static void Fail(string t, string d = null, string r = null) => Add(Level.Fail, t, d, r);

        /// <summary>Runs one check and turns any exception into an ERROR row instead of aborting the report.</summary>
        private static void Run(string sectionName, Action check)
        {
            Section(sectionName);
            try { check(); }
            catch (Exception e)
            {
                Add(Level.Error, "check crashed", $"{e.GetType().Name}: {e.Message}",
                    "This is a bug in BuildPreflight, not necessarily in the project. Fix the check, then re-run.");
            }
        }

        // ------------------------------------------------------------------ entry point

        [MenuItem("SummaRace/Build Preflight", false, 0)]
        public static void RunPreflight()
        {
            Results.Clear();
            _projectSettingsText = null;   // ProjectSettings edits do not trigger a domain reload
            _runtimeScripts = null;
            _runtimeScriptSources = null;
            _androidDefines = null;
            SelfDeclaredCache.Clear();     // source files change between runs without a domain reload

            Run("Editor & platform", CheckEditorAndPlatform);
            Run("Build Settings scenes", CheckBuildScenes);
            Run("SceneNames coverage", CheckSceneNamesCoverage);
            Run("Player settings (Android)", CheckPlayerSettings);
            Run("App icon", CheckIcons);
            Run("Signing", CheckSigning);
            Run("IL2CPP stripping & shader keywords", CheckStrippingAndKeywords);
            Run("Addressables", CheckAddressables);
            Run("Content: stories", CheckStories);
            Run("Content: audio keys", CheckAudioKeys);
            Run("Offline integrity", CheckOfflineIntegrity);
            Run("Runtime code / UnityEditor leakage", CheckUnityEditorLeakage);

            var report = BuildReport();
            var fails = Results.Count(r => r.Level == Level.Fail);
            var errors = Results.Count(r => r.Level == Level.Error);
            var warns = Results.Count(r => r.Level == Level.Warn);

            if (fails + errors > 0) Debug.LogError(report);
            else if (warns > 0) Debug.LogWarning(report);
            else Debug.Log(report);

            try { EditorGUIUtility.systemCopyBuffer = report; } catch { /* clipboard is a nicety */ }

            var verdict = fails + errors > 0
                ? $"BLOCKED — {fails} failure(s)" + (errors > 0 ? $", {errors} check error(s)" : "")
                : warns > 0 ? $"OK with {warns} warning(s)" : "ALL CLEAR";

            EditorUtility.DisplayDialog(
                "SummaRace build preflight",
                verdict + "\n\n" + Summarise() +
                "\n\nThe full report is in the Console and on the clipboard.",
                "OK");
        }

        private static string Summarise()
        {
            var sb = new StringBuilder();
            foreach (var r in Results.Where(r => r.Level == Level.Fail || r.Level == Level.Error))
                sb.AppendLine("✖ " + r.Section + ": " + r.Title);
            foreach (var r in Results.Where(r => r.Level == Level.Warn))
                sb.AppendLine("▲ " + r.Section + ": " + r.Title);
            if (sb.Length == 0) sb.AppendLine("Every check passed.");
            return sb.ToString().TrimEnd();
        }

        private static string BuildReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══ SummaRace build preflight ═══  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            sb.AppendLine("(read-only: this tool changed nothing)");
            sb.AppendLine();

            string current = null;
            foreach (var r in Results)
            {
                if (r.Section != current)
                {
                    current = r.Section;
                    sb.AppendLine();
                    sb.AppendLine("── " + current + " ──");
                }

                sb.AppendLine(Glyph(r.Level) + " " + r.Title);
                if (!string.IsNullOrEmpty(r.Detail))
                    foreach (var line in r.Detail.Split('\n'))
                        sb.AppendLine("      " + line);
                if (!string.IsNullOrEmpty(r.Remedy))
                    sb.AppendLine("      → " + r.Remedy.Replace("\n", "\n        "));
            }

            sb.AppendLine();
            sb.AppendLine("─────────────────────────────────");
            sb.AppendLine($"FAIL {Results.Count(r => r.Level == Level.Fail)}   " +
                          $"ERROR {Results.Count(r => r.Level == Level.Error)}   " +
                          $"WARN {Results.Count(r => r.Level == Level.Warn)}   " +
                          $"PASS {Results.Count(r => r.Level == Level.Pass)}");
            return sb.ToString();
        }

        private static string Glyph(Level l)
        {
            switch (l)
            {
                case Level.Pass: return "  ✔";
                case Level.Info: return "  ·";
                case Level.Warn: return "  ▲";
                case Level.Fail: return "  ✖";
                default: return "  !!";
            }
        }

        // ------------------------------------------------------------------ 1. editor & platform

        private static void CheckEditorAndPlatform()
        {
            Info("Unity " + Application.unityVersion, "Active build target: " + EditorUserBuildSettings.activeBuildTarget);

            var supported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);
            if (supported)
                Pass("Android Build Support is installed", "BuildPipeline.IsBuildTargetSupported(Android) = true");
            else
                Fail("Android Build Support is NOT installed — no APK can be produced",
                     "BuildPipeline.IsBuildTargetSupported(Android) = false.\n" +
                     "Editor/Data/PlaybackEngines has no AndroidPlayer for this editor version.",
                     "Unity Hub ▸ Installs ▸ the gear on " + Application.unityVersion + " ▸ Add modules ▸ tick\n" +
                     "  • Android Build Support\n" +
                     "  • ├ OpenJDK\n" +
                     "  • └ Android SDK & NDK Tools\n" +
                     "Then restart the Editor and re-run this preflight.");

            if (supported && EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                Warn("Active build target is not Android",
                     "Currently " + EditorUserBuildSettings.activeBuildTarget + ".",
                     "File ▸ Build Profiles (or Build Settings) ▸ Android ▸ Switch Platform. " +
                     "The first switch re-imports every texture and takes a long while — do it before the study day, not on it.");

            // Trash Dash's CharacterInputController reads legacy Input.touchCount for the swipe
            // controls. With activeInputHandler = "Input System Package (New)" that call throws at
            // runtime and touch steering dies on the tablet, which no editor test would reveal.
            //
            // NOTE ON HOW THIS IS READ. activeInputHandler comes out of the SAVED
            // ProjectSettings/ProjectSettings.asset file, not from a live API — so an Inspector
            // change that has not been File ▸ Save Project'd is invisible here and this line will
            // confidently report the OLD value. That matters more for this setting than for any
            // other in the report: a wrong value costs nothing in the Editor and on a desktop test
            // (mouse and keyboard keep working), and only shows up as a learner on a tablet
            // swiping and swiping while the runner refuses to change lane.
            const string savedFileCaveat =
                "Read from the SAVED ProjectSettings/ProjectSettings.asset. If you changed Active Input " +
                "Handling in the Inspector just now, File ▸ Save Project and re-run — until you do, this " +
                "line reports the previous value.";

            var handler = ReadProjectSettingValue("activeInputHandler");
            if (handler == "2") Pass("Input handling = Both (required)",
                "Trash Dash's CharacterInputController uses legacy Input.touchCount for the race swipe; " +
                "our UI uses the new Input System. Both must stay enabled.\n" + savedFileCaveat);
            else if (handler == "0") Warn("Input handling = Old only",
                "The new Input System drives our UI (InputSystemUIInputModule) and EndlessKeyboardInput.\n" + savedFileCaveat,
                "Project Settings ▸ Player ▸ Active Input Handling = Both, then File ▸ Save Project.");
            else if (handler == "1") Fail("Input handling = New only — race touch controls will throw",
                "CharacterInputController calls Input.touchCount / Input.GetKeyDown; those throw " +
                "InvalidOperationException when only the new system is active. On the tablet the learner " +
                "taps and swipes and the runner never changes lane — and nothing about a desktop or Editor " +
                "test reveals it.\n" + savedFileCaveat,
                "Project Settings ▸ Player ▸ Active Input Handling = Both, then File ▸ Save Project.");
            else Warn("Input handling: could not read activeInputHandler",
                "The key was not found in ProjectSettings/ProjectSettings.asset.\n" + savedFileCaveat,
                "Check Project Settings ▸ Player ▸ Active Input Handling = Both by eye before building. " +
                "This branch must be Both; it is the setting that silently kills touch steering on device.");
        }

        // ------------------------------------------------------------------ 2. build scenes

        private static void CheckBuildScenes()
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes == null || scenes.Length == 0)
            {
                Fail("Build Settings has no scenes", null, "File ▸ Build Profiles ▸ Scene List.");
                return;
            }

            var sb = new StringBuilder();
            for (int i = 0; i < scenes.Length; i++)
                sb.AppendLine($"[{i,2}] {(scenes[i].enabled ? "on " : "OFF")} {scenes[i].path}");
            Info($"{scenes.Length} scenes in the list", sb.ToString().TrimEnd());

            // index 0 decides what the APK launches into. This project once shipped a list whose
            // index 0 was Trash Dash's own scene — the app would have opened into their game with
            // none of our [Core] singletons alive.
            var first = scenes[0];
            var firstName = Path.GetFileNameWithoutExtension(first.path);
            if (firstName == SrConst.SceneNames.Boot && first.enabled)
                Pass("Boot is build index 0 and enabled", first.path);
            else
                Fail("Build index 0 is not an enabled Boot scene — the APK will launch into the wrong scene",
                     $"index 0 = '{firstName}' (enabled: {first.enabled})",
                     "Boot creates the [Core] singletons (GameManager/AudioManager/SaveManager/SceneLoader). " +
                     "Drag Boot.unity to the top of the scene list.");

            var missing = new List<string>();
            var disabled = new List<string>();
            var seen = new Dictionary<string, int>();
            var duplicates = new List<string>();

            for (int i = 0; i < scenes.Length; i++)
            {
                var s = scenes[i];
                if (!s.enabled) disabled.Add($"[{i}] {s.path}");
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(s.path) == null) missing.Add($"[{i}] {s.path}");

                var key = Path.GetFileNameWithoutExtension(s.path);
                if (seen.ContainsKey(key)) duplicates.Add($"{key} at [{seen[key]}] and [{i}]");
                else seen[key] = i;
            }

            if (missing.Count == 0) Pass("Every listed scene path resolves to a real scene asset");
            else Fail($"{missing.Count} listed scene path(s) do not exist", string.Join("\n", missing),
                      "A dangling entry silently shifts every later build index. Remove or repoint it.");

            if (disabled.Count == 0) Pass("No scene in the list is disabled");
            else Warn($"{disabled.Count} scene(s) are in the list but disabled", string.Join("\n", disabled),
                      "A disabled scene is not in the APK; loading it by name at runtime is a hard dead end.");

            if (duplicates.Count > 0)
                Fail("Duplicate scene names in the build list", string.Join("\n", duplicates),
                     "SceneManager.LoadScene(name) resolves the lowest index — the second copy is unreachable.");

            // Trash Dash's own scenes must never re-enter the list (P0 removed them once already).
            var theirs = new[] { "Main", "Start", "Shop", "Loadout", "Game" };
            var intruders = scenes
                .Where(s => theirs.Contains(Path.GetFileNameWithoutExtension(s.path)) &&
                            !s.path.StartsWith("Assets/_Game/", StringComparison.OrdinalIgnoreCase))
                .Select(s => s.path).ToList();
            if (intruders.Count == 0) Pass("No Trash Dash sample scene is in the build list");
            else Fail("Trash Dash sample scene(s) are in the build list", string.Join("\n", intruders),
                      "Remove them. They carry the shop/leaderboard/ads chrome and none of our [Core] bootstrap.");

            // Dead weight worth knowing about before measuring APK size.
            const string legacyRace = "Assets/_Game/Scenes/Race.unity";
            if (scenes.Any(s => s.enabled && s.path == legacyRace))
                Info("Legacy Race.unity is still in the build list",
                     "Nothing routes to it (the shipping race is " + SrConst.SceneNames.RaceEndless + "), " +
                     "but SceneNames.Race still names it, so removing it needs the constant removed too. " +
                     // ~23MB was an early estimate and is wrong. Documentation/SummaRace_Device_Budget.md
                     // measured the exclusive asset closure at 51.5MB of source (12-20MB once Android
                     // texture/audio compression is applied), 29MB of it Ch46_nonPBR.fbx -- the runner
                     // Aj replaced in F38. Dropping the scene also retires six otherwise-dead AudioKeys
                     // and a 6.9MB music track that is byte-identical to one already shipping.
                     "Worth ~51.5MB of source assets (roughly 12-20MB of APK) if size becomes the " +
                     "binding constraint -- see Documentation/SummaRace_Device_Budget.md for the measurement.");
        }

        // ------------------------------------------------------------------ 3. SceneNames coverage

        private static void CheckSceneNamesCoverage()
        {
            // A scene the code can ask for but the build does not contain is a guaranteed
            // runtime dead end: SceneManager.LoadScene throws and the learner is stranded.
            var constants = typeof(SrConst.SceneNames)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
                .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue());

            var built = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => Path.GetFileNameWithoutExtension(s.path))
                .ToList();

            var absent = constants.Where(kv => !built.Contains(kv.Value)).ToList();
            if (absent.Count == 0)
                Pass($"All {constants.Count} SceneNames constants are enabled build scenes",
                     string.Join(", ", constants.Select(kv => kv.Key + "=" + kv.Value)));
            else
                Fail($"{absent.Count} SceneNames constant(s) are not in the build",
                     string.Join("\n", absent.Select(kv => $"SceneNames.{kv.Key} = \"{kv.Value}\"")),
                     "Either add the scene to Build Settings or delete the constant. " +
                     "Code can load any of these by name — a missing one is a dead end in the player only.");

            var unnamed = built.Where(b => !constants.Values.Contains(b)).ToList();
            if (unnamed.Count > 0)
                Warn($"{unnamed.Count} build scene(s) are not named in SceneNames",
                     string.Join(", ", unnamed),
                     "Nothing can route to them through the normal path — they are payload with no entry point.");
        }

        // ------------------------------------------------------------------ 4. player settings

        private static void CheckPlayerSettings()
        {
            var android = NamedBuildTarget.Android;

            // --- identity
            var company = PlayerSettings.companyName;
            var product = PlayerSettings.productName;
            var appId = PlayerSettings.GetApplicationIdentifier(android);

            if (string.IsNullOrWhiteSpace(company) || company == "DefaultCompany")
                Fail("Company name is unset or still 'DefaultCompany'", "companyName = '" + company + "'");
            else Pass("Company name: " + company);

            if (string.IsNullOrWhiteSpace(product))
                Fail("Product name is empty", null, "This is the launcher label under the icon.");
            else if (product.IndexOf("trash", StringComparison.OrdinalIgnoreCase) >= 0)
                Fail("Product name still reads as the Trash Dash sample", "productName = '" + product + "'");
            else Pass("Product name: " + product);

            var badId = string.IsNullOrWhiteSpace(appId)
                        || appId.StartsWith("com.Company", StringComparison.OrdinalIgnoreCase)
                        || appId.StartsWith("com.DefaultCompany", StringComparison.OrdinalIgnoreCase)
                        || appId.StartsWith("com.unity", StringComparison.OrdinalIgnoreCase)
                        || appId.EndsWith(".trashdash", StringComparison.OrdinalIgnoreCase);
            if (badId)
                Fail("Android application id is a placeholder or a third-party id", "applicationIdentifier = '" + appId + "'",
                     "Project Settings ▸ Player ▸ Android ▸ Other Settings ▸ Package Name. " +
                     "The id is permanent per install: changing it after the tablets are deployed makes the new APK a " +
                     "second, separate app and orphans every learner's saved progress and logs.");
            else Pass("Android application id: " + appId);

            Info("Version " + PlayerSettings.bundleVersion + "  (versionCode " + PlayerSettings.Android.bundleVersionCode + ")",
                 "Every re-install over an existing tablet build needs a versionCode strictly higher than the one on the device.");
            if (PlayerSettings.Android.bundleVersionCode <= 0)
                Fail("Android bundleVersionCode must be ≥ 1");
            else if (PlayerSettings.Android.bundleVersionCode > 100)
                Warn("bundleVersionCode is unusually high (" + PlayerSettings.Android.bundleVersionCode + ")",
                     "This project inherited Trash Dash's release counter (350) once before.",
                     "Only a concern if it was not deliberate — a high code cannot be lowered on a device without uninstalling.");

            // --- orientation. UIOrientation.AutoRotation is the only value that consults the
            //     allowedAutorotateTo* flags; anything else is a hard lock.
            if (PlayerSettings.defaultInterfaceOrientation == UIOrientation.Portrait)
                Pass("Orientation locked to Portrait", "defaultInterfaceOrientation = Portrait");
            else if (PlayerSettings.defaultInterfaceOrientation == UIOrientation.AutoRotation)
                Fail("Orientation is AutoRotation — the game is designed portrait-only",
                     "Every UI layout in this project was authored and portrait-render-tested at 1080×1920.",
                     "Project Settings ▸ Player ▸ Resolution and Presentation ▸ Default Orientation = Portrait.");
            else
                Fail("Orientation is " + PlayerSettings.defaultInterfaceOrientation + ", not Portrait");

            var resizeable = ReadProjectSettingValue("androidResizeableActivity");
            if (resizeable == "1")
                Warn("androidResizeableActivity is on",
                     "On tablets this permits split-screen / freeform windows, where the portrait layouts are not tested.",
                     "Project Settings ▸ Player ▸ Android ▸ Resolution and Presentation ▸ Resizable Window (off), " +
                     "or accept it and add a line to the tablet setup sheet.");

            // --- Android BACK. Core/BackButtonGuard cancels every quit through
            //     Application.wantsToQuit. Predictive back opts the activity into the platform's
            //     OnBackInvokedCallback path, where the system commits the gesture itself — the
            //     managed hook is no longer asked, so the guard is bypassed and BACK quits again.
            var predictiveBack = ReadProjectSettingValue("androidPredictiveBackSupport");
            if (predictiveBack == "0")
                Pass("Android predictive back is off (required)",
                     "Core/BackButtonGuard refuses every quit via Application.wantsToQuit, which is what stops a " +
                     "learner's edge swipe ending the story. Predictive back would route BACK through the platform " +
                     "instead and the guard would never be consulted.");
            else if (predictiveBack == "1")
                Fail("Android predictive back is ON — BackButtonGuard is bypassed and BACK quits the app",
                     "androidPredictiveBackSupport = 1. On gesture navigation BACK is an EDGE SWIPE — the same " +
                     "motion the race trains — so on the tablet a learner mid-story swipes, watches the app peel " +
                     "away, and lands on the launcher. That run is then filed as abandoned, and there is no way " +
                     "to recover it. On three-button navigation it is a permanent target under a portrait game.",
                     "Project Settings ▸ Player ▸ Android ▸ Other Settings ▸ Predictive Back Support = off, " +
                     "then File ▸ Save Project. Verify on the tablet in the first smoke test — " +
                     "Application.wantsToQuit is never raised in the Editor, so this cannot be tested in Play mode.");
            else
                Info("Android predictive back: could not read androidPredictiveBackSupport",
                     "Expected 0. Check Project Settings ▸ Player ▸ Android ▸ Other Settings ▸ Predictive Back Support.");

            // --- graphics API. The floor device is a 2GB Android 8 tablet, where Vulkan drivers are
            //     the least reliable thing on the device, and the race depends on two custom shaders
            //     (SummaRace/SkyTint and CurvedVertexColor). A driver-specific shader failure looks
            //     like a black or untextured world and cannot be reproduced on the build machine.
            var gfxBlock = ExtractTargetBlock(
                ExtractBlock(ReadProjectSettingsText(), "m_BuildTargetGraphicsAPIs:", "m_BuildTargetVRSettings:"),
                "AndroidPlayer");
            if (gfxBlock == null)
            {
                Info("Android graphics APIs: no explicit list in ProjectSettings",
                     "Unity will choose automatically, which on current versions puts Vulkan first. " +
                     "Project Settings ▸ Player ▸ Android ▸ Other Settings ▸ Auto Graphics API (off) ▸ OpenGLES3.");
            }
            else
            {
                var apis = DecodeGraphicsApis(gfxBlock);
                var auto = Regex.Match(gfxBlock, @"m_Automatic:\s*(\d)");
                var isAuto = auto.Success && auto.Groups[1].Value == "1";
                var list = apis.Count == 0 ? "(none listed)" : string.Join(", ", apis);

                if (isAuto)
                    Warn("Android graphics API is set to Automatic",
                         "Unity picks the list at build time and currently prefers Vulkan. Resolved list in the file: " + list,
                         "Project Settings ▸ Player ▸ Android ▸ Other Settings ▸ untick Auto Graphics API and leave " +
                         "OpenGLES3 alone in the list.");
                else if (apis.Contains("Vulkan"))
                    Warn("Vulkan is in the Android graphics API list",
                         "Graphics APIs: " + list + ".\n" +
                         "The study floor device is a 2GB Android 8 tablet. Vulkan drivers on that class of hardware " +
                         "are the usual source of shader-specific breakage, and the race leans on two custom shaders " +
                         "(SummaRace/SkyTint, CurvedVertexColor). On the tablet that shows up as a black sky, an " +
                         "untextured road, or a straight crash into the launcher on entering the race — none of which " +
                         "reproduces on the build machine.",
                         "Project Settings ▸ Player ▸ Android ▸ Other Settings ▸ Graphics APIs: remove Vulkan and " +
                         "leave OpenGLES3. If you keep Vulkan, the race must be smoke-tested on the actual tablet " +
                         "model before the study, not on a newer phone.");
                else if (apis.Count == 1 && apis[0] == "OpenGLES3")
                    Pass("Android graphics API: OpenGLES3 only", "Explicit list (Auto Graphics API off) — the safe " +
                         "choice for a 2GB Android 8 floor device with two custom shaders in the race.");
                else
                    Info("Android graphics APIs: " + list, "Explicit list (Auto Graphics API off).");
            }

            // --- scripting / architecture
            var backend = PlayerSettings.GetScriptingBackend(android);
            if (backend == ScriptingImplementation.IL2CPP) Pass("Scripting backend: IL2CPP");
            else Fail("Scripting backend is " + backend + ", not IL2CPP",
                      "Mono cannot produce a 64-bit Android player.",
                      "Project Settings ▸ Player ▸ Android ▸ Other Settings ▸ Scripting Backend = IL2CPP.");

            var arch = PlayerSettings.Android.targetArchitectures;
            var hasArm64 = (arch & AndroidArchitecture.ARM64) != 0;
            var hasArmv7 = (arch & AndroidArchitecture.ARMv7) != 0;
            if (hasArm64 && !hasArmv7) Pass("Target architecture: ARM64 only", "arch flags = " + arch);
            else if (hasArm64) Warn("Both ARM64 and ARMv7 are selected", "arch flags = " + arch,
                                    "Two native slices roughly double the IL2CPP payload. Drop ARMv7 unless a study tablet is genuinely 32-bit.");
            else Fail("ARM64 is not selected", "arch flags = " + arch,
                      "Project Settings ▸ Player ▸ Android ▸ Other Settings ▸ Target Architectures ▸ ARM64.");

            // --- SDK levels
            var min = (int)PlayerSettings.Android.minSdkVersion;
            if (min >= 26) Pass("Minimum API level " + min + " (Android " + ApiName(min) + ")");
            else Fail("Minimum API level is " + min + ", below the required 26",
                      null, "Project Settings ▸ Player ▸ Android ▸ Other Settings ▸ Minimum API Level = 26.");

            var target = (int)PlayerSettings.Android.targetSdkVersion;
            if (target == 0)
                Info("Target API level: Automatic (highest installed)",
                     "Fine for sideloaded study tablets — nothing here is being submitted to Play, so the Play\n" +
                     "target-API policy does not apply. The caveat is reproducibility: 'Automatic' resolves against\n" +
                     "whatever SDK platform the build machine has, so two machines can produce different APKs.\n" +
                     "Pin it to a concrete level if the build has to be reproducible later.");
            else if (target < min)
                Fail("Target API level (" + target + ") is below the minimum (" + min + ")");
            else
                Info("Target API level: " + target);

            // --- size / stripping
            Info("Engine code stripping: " + (PlayerSettings.stripEngineCode ? "on" : "OFF") +
                 ", managed stripping: " + PlayerSettings.GetManagedStrippingLevel(android),
                 "Both feed the ≤300MB APK cap. Turning stripping off is the usual fix for a missing-type crash, " +
                 "but measure the size again afterwards.");
            if (!PlayerSettings.stripEngineCode)
                Warn("Engine code stripping is off", null, "Costs tens of MB on the 300MB budget.");

            Info("Target frame rate set by Bootstrapper: " + SrConst.GameRules.TargetFrameRate,
                 "The acceptance floor is 30fps in the race on a 2GB device. Note Trash Dash's MusicPlayer " +
                 "pins targetFrameRate to 30 once the race scene is live.");
        }

        private static string ApiName(int api)
        {
            switch (api)
            {
                case 26: return "8.0 Oreo";
                case 27: return "8.1";
                case 28: return "9 Pie";
                case 29: return "10";
                case 30: return "11";
                case 31: return "12";
                case 33: return "13";
                case 34: return "14";
                default: return "API " + api;
            }
        }

        /// <summary>
        /// ProjectSettings stores the per-platform graphics API list as one hex blob: four bytes
        /// per entry, little-endian, each a UnityEngine.Rendering.GraphicsDeviceType. Decoded here
        /// rather than via PlayerSettings.GetGraphicsAPIs so the check keeps working with the
        /// Android module absent, and so the names come from the enum instead of a hardcoded table.
        /// </summary>
        private static List<string> DecodeGraphicsApis(string androidBlock)
        {
            var names = new List<string>();
            var m = Regex.Match(androidBlock ?? "", @"m_APIs:\s*([0-9a-fA-F]+)");
            if (!m.Success) return names;

            var hex = m.Groups[1].Value;
            for (int i = 0; i + 8 <= hex.Length; i += 8)
            {
                try
                {
                    int value = 0;
                    for (int b = 0; b < 4; b++)                       // little-endian
                        value |= Convert.ToInt32(hex.Substring(i + b * 2, 2), 16) << (b * 8);
                    var name = Enum.GetName(typeof(UnityEngine.Rendering.GraphicsDeviceType), value);
                    names.Add(name ?? ("api#" + value));
                }
                catch { names.Add("<unreadable>"); }
            }
            return names;
        }

        // ------------------------------------------------------------------ 6b. stripping & keywords

        /// <summary>
        /// Two settings that behave perfectly in the Editor and only bite in a stripped IL2CPP
        /// player: managed-code stripping removing a type that is only ever reached by reflection,
        /// and shader-variant stripping removing a fog mode that is only ever set from code.
        /// </summary>
        private static void CheckStrippingAndKeywords()
        {
            // --- link.xml: the crypto assemblies behind the teacher PIN.
            const string linkPath = "Assets/Link.xml";
            var needed = new[] { "System.Security.Cryptography.Algorithms", "System.Security.Cryptography.Primitives" };
            const string whyCrypto =
                "TeacherGate.Hash uses SHA256.Create(), which resolves its implementation through " +
                "CryptoConfig BY REFLECTION — so managed stripping cannot see the type is needed and is " +
                "free to remove it. Nothing in the Editor strips, so this is invisible until the APK runs.\n" +
                "On the tablet the teacher taps a PIN-gated control and NOTHING HAPPENS: the throw " +
                "inside the UI callback is swallowed and only logged. That is the PIN at install, the " +
                "session unlock, and EXPORT — the single control that retrieves the whole dataset, with " +
                "no second chance to collect it after the study.";

            if (!File.Exists(linkPath))
            {
                Fail("Assets/Link.xml is missing — IL2CPP stripping can delete the PIN's crypto",
                     whyCrypto,
                     "Restore Assets/Link.xml with <assembly fullname=\"System.Security.Cryptography.Algorithms\" " +
                     "preserve=\"all\" /> and the same for …Cryptography.Primitives. " +
                     "Verify by setting a PIN and running Export on the tablet, not in the Editor.");
            }
            else
            {
                var link = SafeRead(linkPath) ?? "";
                var absent = needed.Where(n => !Regex.IsMatch(link, "fullname\\s*=\\s*\"" + Regex.Escape(n) + "\"")).ToList();
                if (absent.Count == 0)
                    Pass("Link.xml preserves the crypto assemblies the teacher PIN depends on",
                         string.Join(", ", needed));
                else
                    Fail($"Link.xml no longer preserves {absent.Count} crypto assembly/assemblies",
                         "Missing: " + string.Join(", ", absent) + "\n" + whyCrypto,
                         "Add <assembly fullname=\"…\" preserve=\"all\" /> back to Assets/Link.xml for each.");
            }

            // --- fog variant stripping. Custom + KeepLinear is CORRECT here precisely because
            //     RaceWorlds sets the mode from code: Unity's Automatic mode only keeps what it can
            //     see used by scenes and materials, and a code-only assignment is invisible to it.
            const string gfxPath = "ProjectSettings/GraphicsSettings.asset";
            var gfx = SafeRead(gfxPath);
            if (gfx == null)
            {
                Warn("Could not read " + gfxPath, null, "Fog stripping check skipped.");
            }
            else
            {
                var strippingMode = Regex.Match(gfx, @"m_FogStripping:\s*(\d)");
                var kept = new HashSet<string>();
                foreach (var mode in new[] { "Linear", "Exp", "Exp2" })
                    if (Regex.IsMatch(gfx, @"m_FogKeep" + mode + @":\s*1")) kept.Add(mode);

                // Which fog modes does our code actually ask for at runtime?
                var used = new SortedSet<string>();
                var usedWhere = new List<string>();
                foreach (var cs in EnumerateRuntimeScripts())
                {
                    var body = SafeRead(cs);
                    if (body == null) continue;
                    foreach (Match hit in Regex.Matches(body, @"(?<![\w.])FogMode\.(\w+)"))
                    {
                        used.Add(hit.Groups[1].Value);
                        usedWhere.Add(Path.GetFileName(cs) + " → FogMode." + hit.Groups[1].Value);
                    }
                }

                var isCustom = strippingMode.Success && strippingMode.Groups[1].Value == "1";
                var detail = "m_FogStripping = " + (isCustom ? "Custom" : "Automatic") +
                             ", kept variants: " + (kept.Count == 0 ? "(none)" : string.Join(", ", kept)) +
                             "\nfog modes set from code: " + (used.Count == 0 ? "(none)" : string.Join(", ", used)) +
                             (usedWhere.Count > 0 ? "\n" + string.Join("\n", usedWhere.Distinct()) : "");

                if (!isCustom)
                {
                    Info("Fog shader stripping is Automatic", detail +
                         "\nAutomatic keeps the modes Unity can see used by scenes and materials. RaceWorlds sets " +
                         "the mode from C# at runtime, which Unity cannot see — so this is only safe while the " +
                         "scene's own fog mode happens to match.");
                }
                else
                {
                    var unkept = used.Where(u => !kept.Contains(u)).ToList();
                    if (unkept.Count == 0)
                        Pass("Every fog mode the race sets from code survives shader stripping", detail);
                    else
                        Fail("A fog mode is set at runtime but its shader variant is stripped out of the player",
                             detail + "\nStripped but used: " + string.Join(", ", unkept),
                             "In the Editor the race looks exactly right. On the tablet that world renders with NO " +
                             "FOG at all — the corridor's far end stops fading and the world reads as flat, bright " +
                             "and wrong, only in the worlds using that mode.\n" +
                             "Fix either side: Project Settings ▸ Graphics ▸ Shader Stripping ▸ Fog Modes — tick " +
                             string.Join(" and ", unkept) + " — or change RaceWorlds back to a kept mode.");
                }
            }
        }

        // ------------------------------------------------------------------ 5. icons

        private static void CheckIcons()
        {
            // Read from the saved ProjectSettings file rather than the icon API: the modern Android
            // icon API lives in the Android module's editor extension, and this tool has to run
            // when that module is absent.
            var text = ReadProjectSettingsText();
            if (text == null)
            {
                Warn("Could not read ProjectSettings/ProjectSettings.asset", null, "Icon check skipped.");
                return;
            }

            Info("Icons are read from the saved ProjectSettings file",
                 "If you just changed them in the Inspector, File ▸ Save Project first.");

            // Legacy per-density set (m_BuildTargetIcons ▸ Android).
            var legacy = ExtractBlock(text, "m_BuildTargetIcons:", "m_BuildTargetPlatformIcons:");
            var androidLegacy = ExtractTargetBlock(legacy, "Android");
            var guids = Regex.Matches(androidLegacy ?? "", @"guid:\s*([a-f0-9]{32})")
                             .Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            var slots = Regex.Matches(androidLegacy ?? "", @"m_Icon:").Count;

            if (slots == 0)
            {
                Fail("No Android icon densities are configured",
                     "m_BuildTargetIcons has no Android entry.",
                     "Project Settings ▸ Player ▸ Android ▸ Icon.");
            }
            else if (guids.Count < slots)
            {
                Fail($"{slots - guids.Count} of {slots} Android icon densities are empty",
                     null, "An empty density falls back to the default Unity logo on some launchers.");
            }
            else
            {
                var paths = guids.Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList();
                var foreign = paths.Where(p => string.IsNullOrEmpty(p) ||
                                               !p.StartsWith("Assets/_Game/", StringComparison.OrdinalIgnoreCase)).ToList();
                if (foreign.Count > 0)
                    Fail("The app icon is not one of ours",
                         "Assigned: " + string.Join(", ", paths.Select(p => string.IsNullOrEmpty(p) ? "<missing asset>" : p)),
                         "This build shipped Trash Dash's cat (Assets/UI/StoreIcon.png) through six playtests — " +
                         "nothing in the game window reveals the launcher icon. Expected Assets/_Game/Art/UI/app_icon.png.");
                else
                    Pass($"All {slots} Android icon densities point at our own art", string.Join(", ", paths));
            }

            // Adaptive / round / legacy platform icons (m_BuildTargetPlatformIcons ▸ Android).
            var platform = ExtractBlock(text, "m_BuildTargetPlatformIcons:", "m_BuildTargetBatching:");
            var androidPlatform = ExtractTargetBlock(platform, "Android");
            if (androidPlatform != null)
            {
                var totalSlots = Regex.Matches(androidPlatform, @"m_Textures:").Count;
                var emptySlots = Regex.Matches(androidPlatform, @"m_Textures:\s*\[\]").Count;
                if (totalSlots > 0 && emptySlots == totalSlots)
                    Warn("All Android adaptive/round icon slots are empty (" + totalSlots + " slots)",
                         "Minimum API is 26, where launchers prefer an adaptive icon. With every slot empty Unity " +
                         "falls back to the legacy square, so the icon still appears — it just will not be masked or " +
                         "animated like every other icon on the tablet.",
                         "Optional polish, not a blocker: Project Settings ▸ Player ▸ Android ▸ Icon ▸ Adaptive, " +
                         "supply a 432×432 background + foreground. Verify on the tablet either way.");
                else if (totalSlots > 0)
                    Pass($"Android platform icons: {totalSlots - emptySlots} of {totalSlots} slots filled");
            }

            if (PlayerSettings.SplashScreen.show)
                Info("Unity splash screen is enabled",
                     "Personal-licence builds cannot disable it. It plays before Boot — expected, not a bug.");
        }

        // ------------------------------------------------------------------ 6. signing

        private static void CheckSigning()
        {
            if (PlayerSettings.Android.useCustomKeystore)
            {
                var ks = PlayerSettings.Android.keystoreName;
                var alias = PlayerSettings.Android.keyaliasName;
                if (string.IsNullOrEmpty(ks) || !File.Exists(ks))
                    Fail("A custom keystore is selected but the file is missing", "keystoreName = '" + ks + "'",
                         "The build will fail at the signing step. Point at the .keystore or turn custom signing off.");
                else
                    Pass("Custom keystore: " + Path.GetFileName(ks) + " (alias '" + alias + "')",
                         "Keep the keystore and its passwords backed up somewhere other than this repo — " +
                         "an update signed by a different key cannot install over an existing one.");
            }
            else
            {
                Info("No custom keystore — the APK will be debug-signed",
                     "Correct for this study. A debug-signed APK sideloads onto the 40 tablets exactly like any\n" +
                     "other APK; the signature only matters for (a) the Play Store, which will not accept it, and\n" +
                     "(b) updating in place, which requires every later APK to be signed with the same key.\n" +
                     "Unity's debug keystore is per-machine, so BUILD EVERY STUDY APK ON THE SAME MACHINE, or a\n" +
                     "mid-study update will refuse to install over the deployed build and will need an uninstall\n" +
                     "(which erases learner progress and every unexported log on that tablet).");
            }
        }

        // ------------------------------------------------------------------ 7. addressables

        private static void CheckAddressables()
        {
            const string settingsPath = "Assets/AddressableAssetsData/AddressableAssetSettings.asset";
            if (!File.Exists(settingsPath))
            {
                Info("No Addressables settings asset — nothing to check.");
                return;
            }

            // The shipping race scene is Trash Dash's, and its track, themes, zones and the runner
            // prefab all come through Addressables.InstantiateAsync / LoadAssetsAsync. In the Editor
            // this works off the Asset Database, which hides the problem completely; in a player,
            // unbuilt Addressables content does not mean "a race that looks wrong" — it means the
            // race never begins. See NoContentConsequence below for the traced chain.
            var callerFiles = EnumerateRuntimeScripts()
                .Where(f => (SafeRead(f) ?? "").Contains("Addressables."))
                .Select(f => f.Replace('\\', '/'))
                .ToList();
            Info("Addressables is on the shipping path",
                 callerFiles.Count + " player-compiled script(s) call Addressables (track segments, themes, zones, " +
                 "sky domes, the runner prefab). In the Editor these resolve off the Asset Database, so a missing " +
                 "content build is invisible until the APK runs.\n" +
                 string.Join("\n", callerFiles.Take(20)) + (callerFiles.Count > 20 ? "\n…" : ""));

            var settings = File.ReadAllText(settingsPath);
            var m = Regex.Match(settings, @"m_BuildAddressablesWithPlayerBuild:\s*(\d+)");
            if (m.Success)
            {
                // AddressableAssetSettings.PlayerBuildOption: 0 PreferencesValue, 1 BuildWithPlayer, 2 DoNotBuildWithPlayer
                var v = m.Groups[1].Value;
                if (v == "1")
                    Pass("Addressables content is built with the player", "BuildAddressablesWithPlayerBuild = BuildWithPlayer");
                else if (v == "2")
                    Fail("HARD STOP — Addressables content will NOT be built with the player, and the race never starts",
                         "BuildAddressablesWithPlayerBuild = DoNotBuildWithPlayer.\n" + NoContentConsequence,
                         BuildContentRemedy);
                else
                    Warn("Addressables build-with-player follows the global Preferences value",
                         "BuildAddressablesWithPlayerBuild = PreferencesValue — the setting lives in Unity Preferences, " +
                         "not in the repo, so it is per-machine and invisible in review. A machine whose preference is " +
                         "off produces an APK with no content and no warning.\n" + NoContentConsequence,
                         "Set it explicitly on the settings asset, or always run a manual content build first.");
            }

            // Has content ever been built for Android? Default LocalBuildPath is
            // [Addressables.BuildPath]/[BuildTarget] = Library/com.unity.addressables/aa/Android.
            var aaAndroid = Path.Combine("Library", "com.unity.addressables", "aa", "Android");
            var altAndroid = Path.Combine("Library", "com.unity.addressables", "aa", "Android".ToLowerInvariant());
            var built = Directory.Exists(aaAndroid) || Directory.Exists(altAndroid);
            if (built)
            {
                var dir = Directory.Exists(aaAndroid) ? aaAndroid : altAndroid;
                var bundles = SafeCount(() => Directory.EnumerateFiles(dir, "*.bundle", SearchOption.AllDirectories).Count());
                Pass("Addressables content exists for Android (" + bundles + " bundle(s))",
                     dir + "\nLast written " + Directory.GetLastWriteTime(dir).ToString("yyyy-MM-dd HH:mm") +
                     " — rebuild it whenever an addressable asset changes.");
            }
            else
            {
                Fail("HARD STOP — no Addressables content has ever been built for Android; the race never starts",
                     "Expected " + aaAndroid + " — it does not exist.\n" + NoContentConsequence,
                     BuildContentRemedy);
            }

            var groups = SafeCount(() =>
                Directory.EnumerateFiles("Assets/AddressableAssetsData/AssetGroups", "*.asset", SearchOption.TopDirectoryOnly).Count());
            Info(groups + " Addressables groups", "Unused groups cost ~nothing in the APK — their meshes and " +
                 "textures are shared with what does ship, measured previously. Deleting them is not a size fix.");

            CheckAddressableGroupPaths();
        }

        /// <summary>What an APK without built content actually does, traced through their code.</summary>
        private const string NoContentConsequence =
            "WHAT THE LEARNER SEES: not a race missing its scenery — NO RACE AT ALL. The screen sits on the\n" +
            "mission briefing / an empty scene and never moves, with no error the child or teacher can see.\n" +
            "The chain, traced in Assets/Scripts/Tracks/TrackManager.cs:\n" +
            "  :194  Addressables.InstantiateAsync(<runner prefab>) resolves to nothing in a player with no bundles\n" +
            "  :197  op.Result is null →\n" +
            "  :201  yield break — the whole Begin coroutine abandons here\n" +
            "  :228  gameObject.SetActive(true) is therefore NEVER reached\n" +
            "  :131  TrackManager.Awake (which sets s_Instance) only runs when that object activates\n" +
            "  ⇒ TrackManager.instance stays null forever, so nothing that drives the run can start.\n" +
            "In the Editor every one of those calls resolves straight off the Asset Database, so the race is\n" +
            "flawless right up until it is an APK. This is the single most expensive way to lose a study day.";

        private const string BuildContentRemedy =
            "Switch the platform to Android FIRST (content is per-platform), then either\n" +
            "  Window ▸ Asset Management ▸ Addressables ▸ Settings ▸ Build Addressables on Player Build =\n" +
            "    'Build Addressables content on Player Build'  (do this once and stop thinking about it), or\n" +
            "  Window ▸ Asset Management ▸ Addressables ▸ Groups ▸ Build ▸ New Build ▸ Default Build Script\n" +
            "    by hand BEFORE every APK build.\n" +
            "Then re-run this preflight and confirm this row turns green before building.";

        /// <summary>Groups the race cannot run without — theme, zone family, sky dome (F54).</summary>
        private static readonly string[] RaceCriticalGroups =
        {
            "Themes", "Default-Zones", "Night-Zones", "Default-Sky", "Night-Sky", "Duplicate Asset Isolation",
        };

        /// <summary>
        /// Each group's BundledAssetGroupSchema carries the build/load path variables and the
        /// provider types that tell the runtime where its bundles are. 12+ of these were left empty
        /// on disk by the Endless Runner import (e4de43e). BundledAssetGroupSchema.OnEnable very
        /// probably re-seeds them from the active profile the moment the Addressables window opens —
        /// but "very probably" is not a thing anyone should discover at 11pm before a study.
        /// Read as text: the Addressables package must not be a compile dependency of this tool.
        /// </summary>
        private static void CheckAddressableGroupPaths()
        {
            const string groupDir = "Assets/AddressableAssetsData/AssetGroups";
            var groupFiles = SafeList(() =>
                Directory.EnumerateFiles(groupDir, "*.asset", SearchOption.TopDirectoryOnly).ToList());

            if (groupFiles.Count == 0)
            {
                Info("No Addressables group assets found under " + groupDir);
                return;
            }

            var empty = new List<string>();
            var ok = new List<string>();
            int bundled = 0;

            foreach (var gf in groupFiles)
            {
                var text = SafeRead(gf);
                if (text == null) continue;

                var name = Regex.Match(text, @"^\s*m_GroupName:\s*(.*)$", RegexOptions.Multiline);
                var groupName = name.Success ? name.Groups[1].Value.Trim() : Path.GetFileNameWithoutExtension(gf);

                var schema = FindBundledSchemaText(text);
                if (schema == null) continue;                       // e.g. Built In Data (player-data group)
                bundled++;

                var buildPath = YamlChildValue(schema, "m_BuildPath", "m_Id");
                var loadPath = YamlChildValue(schema, "m_LoadPath", "m_Id");
                var provider = YamlChildValue(schema, "m_AssetBundleProviderType", "m_ClassName");

                if (string.IsNullOrEmpty(buildPath) || string.IsNullOrEmpty(loadPath) || string.IsNullOrEmpty(provider))
                    empty.Add(groupName +
                              "  (build:" + (string.IsNullOrEmpty(buildPath) ? "EMPTY" : "set") +
                              " load:" + (string.IsNullOrEmpty(loadPath) ? "EMPTY" : "set") +
                              " provider:" + (string.IsNullOrEmpty(provider) ? "EMPTY" : "set") + ")");
                else
                    ok.Add(groupName);
            }

            if (empty.Count == 0)
            {
                Pass($"All {bundled} bundled Addressables group(s) have build/load paths and a provider",
                     string.Join(", ", ok));
                return;
            }

            var critical = RaceCriticalGroups
                .Where(c => empty.Any(e => e.StartsWith(c + "  ", StringComparison.Ordinal)))
                .ToList();

            Warn($"{empty.Count} of {bundled} Addressables groups have EMPTY build/load path and provider on disk",
                 string.Join("\n", empty) +
                 (critical.Count > 0
                    ? "\nRace-critical groups affected: " + string.Join(", ", critical) +
                      "\nThose carry the theme, the zone families and the sky domes the race loads (F54)."
                    : "") +
                 "\nThese were emptied by the Endless Runner import (e4de43e). BundledAssetGroupSchema.OnEnable " +
                 "most likely re-seeds them from the active profile as soon as the Addressables window opens, " +
                 "which is why content builds have worked — but nothing in the repo proves it, and if it does NOT " +
                 "happen the content build silently produces bundles the player cannot locate.\n" +
                 "WHAT THAT LOOKS LIKE ON THE TABLET: identical to having built no content at all — the race " +
                 "never starts (see the Addressables content rows above for the traced chain).",
                 "Do this once, now, not on build day: Window ▸ Asset Management ▸ Addressables ▸ Groups, click " +
                 "each listed group and confirm Build & Load Paths read 'Local' in the Inspector, then File ▸ Save " +
                 "Project and re-run this preflight. This row should turn green — if it does not, the paths are " +
                 "genuinely unset and must be picked by hand before any APK is built.");
        }

        /// <summary>
        /// Finds the BundledAssetGroupSchema text for a group: first by the sibling-file naming
        /// convention (&lt;groupGuid&gt;_BundledAssetGroupSchema.asset), then by resolving the guids
        /// listed in the group's m_SchemaSet. Returns null when the group has no bundled schema.
        /// </summary>
        private static string FindBundledSchemaText(string groupText)
        {
            var guid = Regex.Match(groupText, @"^\s*m_GUID:\s*(\S+)\s*$", RegexOptions.Multiline);
            if (guid.Success)
            {
                var byConvention = "Assets/AddressableAssetsData/AssetGroups/Schemas/" +
                                   guid.Groups[1].Value + "_BundledAssetGroupSchema.asset";
                var text = SafeRead(byConvention);
                if (text != null && text.Contains("m_BuildPath:")) return text;
            }

            var set = ExtractBlock(groupText, "m_SchemaSet:", "\n  m_") ?? groupText;
            foreach (Match m in Regex.Matches(set, @"guid:\s*([a-f0-9]{32})"))
            {
                string path;
                try { path = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value); } catch { continue; }
                if (string.IsNullOrEmpty(path)) continue;
                var text = SafeRead(path);
                if (text != null && text.Contains("m_BuildPath:")) return text;
            }
            return null;
        }

        /// <summary>
        /// Reads `parent:` on its own line and returns the value of `child:` on one of the next few
        /// lines. A multi-line regex cannot do this safely here: when the value is empty, `\s*`
        /// happily eats the newline and swallows the following key as the value.
        /// </summary>
        private static string YamlChildValue(string yaml, string parent, string child)
        {
            var lines = yaml.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim() != parent + ":") continue;
                for (int j = i + 1; j < Math.Min(i + 4, lines.Length); j++)
                {
                    var t = lines[j].Trim();
                    if (t.StartsWith(child + ":", StringComparison.Ordinal))
                        return t.Substring(child.Length + 1).Trim();
                }
                return "";
            }
            return "";
        }

        // ------------------------------------------------------------------ 8. stories

        private static void CheckStories()
        {
            var expected = new List<string>();
            for (int s = 1; s <= SrConst.GameRules.SessionCount; s++)
                foreach (var d in SrData.StoryIds.Difficulties)
                    expected.Add(SrData.StoryIds.For(s, d));

            var badLoad = new List<string>();
            var badHero = new List<string>();
            var badNarration = new List<string>();
            var badWorld = new List<string>();
            int narrationChecked = 0;
            int longestCard = 0;
            string longestCardText = "";

            // StoryLoader logs its own errors; keep the console readable and report them here instead.
            var previousFilter = Debug.unityLogger.filterLogType;
            try
            {
                Debug.unityLogger.filterLogType = LogType.Exception;

                foreach (var id in expected)
                {
                    var story = SrData.StoryLoader.Load(id);
                    if (story == null) { badLoad.Add(id); continue; }

                    if (string.IsNullOrEmpty(story.heroImage) || Resources.Load<Sprite>(story.heroImage) == null)
                        badHero.Add(id + " → '" + story.heroImage + "'");

                    if (string.IsNullOrEmpty(story.world)) badWorld.Add(id);

                    if (story.pages != null)
                    {
                        foreach (var page in story.pages)
                        {
                            if (page == null || string.IsNullOrEmpty(page.narration)) continue;
                            narrationChecked++;
                            if (Resources.Load<AudioClip>(page.narration) == null)
                                badNarration.Add(id + " → '" + page.narration + "'");
                        }
                    }

                    if (story.elements != null)
                    {
                        foreach (var el in story.elements)
                        {
                            if (el == null) continue;
                            foreach (var card in new[] { el.correct }.Concat(el.distractors ?? new string[0]))
                            {
                                if (string.IsNullOrEmpty(card)) continue;
                                if (card.Length > longestCard) { longestCard = card.Length; longestCardText = id + ": " + card; }
                            }
                        }
                    }
                }
            }
            finally { Debug.unityLogger.filterLogType = previousFilter; }

            if (badLoad.Count == 0)
                Pass($"All {expected.Count} stories load and validate through StoryLoader",
                     $"sessions 1..{SrConst.GameRules.SessionCount} × {SrData.StoryIds.Difficulties.Length} difficulties, " +
                     "re-derived from GameRules.SessionCount and StoryIds — not hardcoded.");
            else
                Fail($"{badLoad.Count} of {expected.Count} stories fail to load or validate",
                     string.Join(", ", badLoad),
                     "Re-run this preflight with the console filter off to see StoryLoader's own message, " +
                     "then fix content in Tools/StoryPipeline/overrides.json and re-run the pipeline — never by hand-editing the 30 JSONs.");

            if (badHero.Count == 0) Pass("Every story's heroImage resolves to a Sprite in Resources");
            else Fail($"{badHero.Count} heroImage path(s) do not resolve", string.Join("\n", badHero),
                      "StorySelect hides the image and falls back to title-only, so this degrades rather than crashes — " +
                      "but the card looks broken.");

            if (badNarration.Count == 0)
                Pass($"All {narrationChecked} narration paths resolve to AudioClips");
            else
                Fail($"{badNarration.Count} of {narrationChecked} narration path(s) do not resolve",
                     string.Join("\n", badNarration.Take(20)) + (badNarration.Count > 20 ? "\n…" : ""),
                     "AudioManager treats a missing clip as a silent page, so the game runs — but narration is the " +
                     "accessibility support the study depends on, and a silent page is invisible in testing.");

            if (badWorld.Count == 0) Pass("Every story names a race world");
            else Warn($"{badWorld.Count} story/stories have no 'world' field", string.Join(", ", badWorld),
                      "RaceWorlds falls back, so all races would share one look. s01_* are excluded from the " +
                      "pipeline and have to be edited by hand — that is how session 1 lost its world once before.");

            // Orphan JSONs — content that ships but nothing can reach.
            var onDisk = SafeList(() => Directory.EnumerateFiles("Assets/_Game/Resources/Stories", "*.json", SearchOption.TopDirectoryOnly)
                                                 .Select(Path.GetFileNameWithoutExtension).ToList());
            var orphans = onDisk.Where(f => !expected.Contains(f)).ToList();
            if (orphans.Count > 0)
                Warn($"{orphans.Count} story JSON(s) exist that no session/difficulty maps to",
                     string.Join(", ", orphans), "They ship inside Resources and nothing can open them.");

            Info("Longest race-answer string: " + longestCard + " chars",
                 longestCardText + "\nThe playtested baseline is 53. Longer strings shrink or clip on a moving card.");
        }

        // ------------------------------------------------------------------ 9. audio keys

        private static void CheckAudioKeys()
        {
            var keys = typeof(SrConst.AudioKeys)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => new { f.Name, Value = (string)f.GetRawConstantValue() })
                .ToList();

            var missing = keys.Where(k => Resources.Load<AudioClip>("Audio/" + k.Value) == null).ToList();
            if (missing.Count == 0)
                Pass($"All {keys.Count} AudioKeys constants resolve to a clip in Resources/Audio");
            else
                Fail($"{missing.Count} of {keys.Count} AudioKeys have no clip",
                     string.Join("\n", missing.Select(k => $"AudioKeys.{k.Name} = \"{k.Value}\"")),
                     "AudioManager plays nothing and logs nothing for a missing key — silent, not broken, " +
                     "which is why these go unnoticed in playtests.");
        }

        // ------------------------------------------------------------------ 10. offline integrity

        private static readonly string[] BannedPackages =
        {
            "com.unity.ads",
            "com.unity.analytics",
            "com.unity.purchasing",
            "com.unity.microsoft.gdk",
            "com.unity.microsoft.gdk.tools",
            "com.unity.services.analytics",
        };

        /// <summary>Not banned outright, but nothing offline needs them — worth a look if they appear.</summary>
        private static readonly string[] SuspectPackages =
        {
            "com.unity.services.core",
            "com.unity.services.authentication",
            "com.unity.remote-config",
        };

        private static void CheckOfflineIntegrity()
        {
            // --- packages. These have re-entered the manifest twice (once via the Endless Runner
            //     import, once via its revert), and the purchasing package auto-initialises at
            //     runtime even when its scripts are compiled out.
            const string manifestPath = "Packages/manifest.json";
            if (File.Exists(manifestPath))
            {
                var manifest = File.ReadAllText(manifestPath);
                var found = BannedPackages.Where(p => Regex.IsMatch(manifest, "\"" + Regex.Escape(p) + "\"\\s*:")).ToList();
                if (found.Count == 0)
                    Pass("Packages/manifest.json carries no ads / analytics / purchasing / GDK package");
                else
                    Fail("Banned package(s) are back in the manifest", string.Join(", ", found),
                         "The study build must be 100% offline (GDD non-negotiable). Remove them from " +
                         "Packages/manifest.json — note com.unity.purchasing initialises itself at runtime even with " +
                         "its scripts compiled out, so #if-ing the code is not enough.");

                var suspect = SuspectPackages.Where(p => Regex.IsMatch(manifest, "\"" + Regex.Escape(p) + "\"\\s*:")).ToList();
                if (suspect.Count > 0)
                    Warn("Unity Services package(s) present that an offline build has no use for",
                         string.Join(", ", suspect),
                         "Not banned by name, but they exist to talk to Unity's backend. Confirm nothing pulled them in.");
            }
            else Warn("Packages/manifest.json not found");

            // --- Unity services toggles
            const string connectPath = "ProjectSettings/UnityConnectSettings.asset";
            if (File.Exists(connectPath))
            {
                var connect = File.ReadAllText(connectPath);
                var enabled = Regex.Matches(connect, @"m_Enabled:\s*1").Count;
                if (enabled == 0) Pass("Every Unity service is disabled (ads, analytics, purchasing, crash/perf reporting)");
                else Fail(enabled + " Unity service(s) are enabled in UnityConnectSettings", null,
                          "Project Settings ▸ Services — turn all of them off.");
            }

            // --- billing artefact that has resurrected before
            if (File.Exists("Assets/Resources/BillingMode.json"))
                Fail("Assets/Resources/BillingMode.json is back", null,
                     "An in-app-purchasing leftover. Delete it (it has self-resurrected after a domain reload before — " +
                     "if it keeps returning, delete it with the Editor closed).");
            else Pass("No BillingMode.json in Resources");

            // --- External Dependency Manager. Editor-only, so it never enters the APK, but it
            //     patches the Gradle build and can pull Play Services in if any *Dependencies.xml
            //     appears anywhere in the project.
            if (Directory.Exists("Assets/MobileDependencyResolver") ||
                Directory.Exists("Assets/ExternalDependencyManager"))
            {
                var xml = SafeList(() => Directory.EnumerateFiles("Assets", "*Dependencies.xml", SearchOption.AllDirectories).ToList());
                if (xml.Count == 0)
                    Warn("Google External Dependency Manager is present in Assets/",
                         "Editor-only (everything sits under an Editor/ folder), and there is no *Dependencies.xml " +
                         "anywhere, so it currently resolves nothing and injects nothing into the Gradle build.",
                         "Harmless as-is, but it self-resurrects after a domain reload, so it will keep showing up in " +
                         "git status. Delete it with the Editor closed if you want it gone for good.");
                else
                    Fail("External Dependency Manager has dependency manifests to resolve",
                         string.Join("\n", xml),
                         "These pull Google Play Services / Firebase AARs into the Android Gradle build — " +
                         "network code in a build that must be 100% offline. Delete them.");
            }
            else Pass("No Google External Dependency Manager in Assets/");

            CheckNetworkingReachability();
        }

        /// <summary>
        /// Anything that could talk to a network or a vendor backend. Ads / analytics / purchasing
        /// APIs are in here as well as raw sockets: com.unity.ads and com.unity.analytics have both
        /// re-entered this project once already, and the Trash Dash code that calls them is still on
        /// disk and still wired into the shipping scene — it is compiled out by a #if today and one
        /// package install away from being compiled back in.
        /// </summary>
        private static readonly Regex NetPattern = new Regex(
            @"Application\.OpenURL|UnityWebRequest|UnityEngine\.Networking|System\.Net|new\s+WWW\s*\(|" +
            @"HttpClient|TcpClient|Socket\s*\(|" +
            @"(?<![\w.])Analytics\s*\.|(?<![\w.])AnalyticsEvent\s*\.|(?<![\w.])Advertisement\s*\.|" +
            @"(?<![\w.])Social\s*\.|(?<![\w.])Purchasing(?![\w])|UnityEngine\.Purchasing|(?<![\w.])Firebase(?![\w])");

        private static void CheckNetworkingReachability()
        {
            var sceneText = new Dictionary<string, string>();
            foreach (var p in EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).Where(File.Exists))
            {
                var t = SafeRead(p);
                if (t != null) sceneText[p] = t;
            }

            // file → the guard that compiles its hits out (null = compiled in)
            var liveHits = new List<string>();
            var inertHits = new List<string>();
            var liveFiles = new List<string>();
            var inertFiles = new List<string>();

            foreach (var cs in EnumerateRuntimeScripts())
            {
                var body = SafeRead(cs);
                if (body == null || !NetPattern.IsMatch(body)) continue;

                bool anyLive = false;
                var guards = new SortedSet<string>();

                foreach (var line in ScanCode(cs))
                {
                    var hit = NetPattern.Match(line.Code);
                    if (!hit.Success) continue;

                    if (line.InactiveGuard == null)
                    {
                        anyLive = true;
                        liveHits.Add(cs + ":" + line.Number + "  " + hit.Value.Trim());
                    }
                    else guards.Add(line.InactiveGuard);
                }

                if (anyLive) liveFiles.Add(cs);
                else if (guards.Count > 0) inertFiles.Add(cs);

                if (guards.Count > 0)
                    inertHits.Add(cs + "  — behind #if " + string.Join(" / #if ", guards));
            }

            // Reachability: a script matters if a build scene references it, or if a PREFAB does —
            // a prefab is where a component actually lives, and the old check looked only at scenes,
            // so an ads button sitting on a prefab that a shipping scene instantiates was invisible.
            var reachable = new List<string>();
            var interesting = liveFiles.Concat(inertFiles).Distinct().ToList();
            var guidToScript = new Dictionary<string, string>();
            foreach (var cs in interesting)
            {
                string guid;
                try { guid = AssetDatabase.AssetPathToGUID(cs.Replace('\\', '/')); } catch { continue; }
                if (!string.IsNullOrEmpty(guid) && !guidToScript.ContainsKey(guid)) guidToScript[guid] = cs;
            }

            var liveSet = new HashSet<string>(liveFiles);
            foreach (var kv in guidToScript)
                foreach (var scene in sceneText)
                    if (scene.Value.Contains(kv.Key))
                        reachable.Add((liveSet.Contains(kv.Value) ? "LIVE " : "inert") + "  " +
                                      Path.GetFileName(kv.Value) + "  on a GameObject in  " + scene.Key);

            if (guidToScript.Count > 0)
            {
                var prefabs = SafeList(() => Directory.EnumerateFiles("Assets", "*.prefab", SearchOption.AllDirectories).ToList());
                foreach (var prefab in prefabs)
                {
                    var text = SafeRead(prefab);
                    if (text == null) continue;

                    foreach (var kv in guidToScript)
                    {
                        if (!text.Contains(kv.Key)) continue;

                        var prefabPath = prefab.Replace('\\', '/');
                        string prefabGuid;
                        try { prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath); } catch { prefabGuid = null; }

                        var host = string.IsNullOrEmpty(prefabGuid)
                            ? null
                            : sceneText.FirstOrDefault(s => s.Value.Contains(prefabGuid)).Key;

                        reachable.Add((liveSet.Contains(kv.Value) ? "LIVE " : "inert") + "  " +
                                      Path.GetFileName(kv.Value) + "  on prefab  " + prefabPath +
                                      (host != null
                                        ? "  ← used by  " + host
                                        : "  (no build scene references this prefab directly — it can still be reached " +
                                          "through Addressables or Resources)"));
                    }
                }
            }

            // --- verdicts
            if (liveHits.Count == 0)
                Pass("No compiled-in networking, ads, analytics or purchasing call exists in player code",
                     inertHits.Count == 0
                        ? "Nothing matched at all."
                        : "Nothing is compiled in. " + inertHits.Count + " file(s) still CONTAIN such calls but " +
                          "they are behind a preprocessor guard that is not defined for Android — see the next row.");
            else
                Fail("A compiled-in networking / ads / analytics call exists in player code",
                     string.Join("\n", liveHits.Take(30)) + (liveHits.Count > 30 ? "\n…" : ""),
                     "100% offline is a GDD non-negotiable and a condition of the study's consent. " +
                     "Delete the call or the script. (Both Application.OpenURL objects were removed once already; " +
                     "this is the check that catches them coming back.)");

            if (inertHits.Count > 0)
                Info(inertHits.Count + " file(s) contain ads/analytics/purchasing calls that are PRESENT BUT COMPILED OUT",
                     string.Join("\n", inertHits.Take(30)) + (inertHits.Count > 30 ? "\n…" : "") +
                     "\nAndroid scripting define symbols: " +
                     (AndroidDefineSymbols().Count == 0 ? "(none)" : string.Join(", ", AndroidDefineSymbols())) +
                     "\nThis is Trash Dash's shop / leaderboard / rewarded-ad code. It ships as dead source, costs " +
                     "nothing in the APK, and reaches no network today. The reason it is reported rather than " +
                     "ignored: UNITY_ADS / UNITY_ANALYTICS / UNITY_PURCHASING are defined AUTOMATICALLY by " +
                     "Unity the moment the matching package is installed. Nobody has to edit these files to arm " +
                     "them — re-adding one package to Packages/manifest.json is enough, and that has already " +
                     "happened twice in this repo. Removing the code outright is the permanent fix.");

            if (reachable.Count == 0)
                Pass("No such script is referenced by a build scene or by any prefab");
            else if (reachable.All(r => r.StartsWith("inert", StringComparison.Ordinal)))
                Info(reachable.Count + " reference(s) to compiled-out ads/analytics code from shipping content",
                     string.Join("\n", reachable.Take(30)) + (reachable.Count > 30 ? "\n…" : "") +
                     "\nHarmless while the guards are off — listed so that if a banned package ever returns you can " +
                     "see immediately which shipping objects would come alive.");
            else
                Fail("A LIVE networking / ads / analytics script is on shipping content",
                     string.Join("\n", reachable.Where(r => r.StartsWith("LIVE", StringComparison.Ordinal))),
                     "Delete the component from that object/prefab, or delete the script. " +
                     "An offline build that reaches a network breaks the study's consent terms, not just the GDD.");
        }

        // ------------------------------------------------------------------ 11. UnityEditor leakage

        private static readonly Regex UsingEditorRx =
            new Regex(@"^\s*using\s+(static\s+)?([A-Za-z_]\w*\s*=\s*)?UnityEditor(\.|;|\s)");
        private static readonly Regex QualifiedEditorRx =
            new Regex(@"(?<![\w.])UnityEditor(Internal)?\s*\.");

        /// <summary>
        /// Editor-only types used WITHOUT the UnityEditor prefix. A file whose `using UnityEditor;`
        /// is correctly wrapped in #if UNITY_EDITOR but whose *use* of AssetDatabase is not still
        /// fails the player compile, and neither of the two patterns above can see it.
        /// Split three ways so each can be matched in the shape it is actually written in.
        /// </summary>
        private static readonly string[] EditorStaticTypes =
        {
            "AssetDatabase", "AssetImporter", "AssetPreview", "BuildPipeline", "EditorApplication",
            "EditorBuildSettings", "EditorGUI", "EditorGUILayout", "EditorGUIUtility", "EditorJsonUtility",
            "EditorPrefs", "EditorSceneManager", "EditorStyles", "EditorUserBuildSettings", "EditorUtility",
            "GameObjectUtility", "HandleUtility", "Handles", "MonoImporter", "PlayerSettings",
            "PrefabUtility", "SceneView", "Selection", "Undo", "Unwrapping",
        };
        private static readonly string[] EditorDeclaredTypes =
        {
            "SerializedObject", "SerializedProperty", "EditorWindow", "PropertyDrawer", "ScriptableWizard",
            "GenericMenu", "TextureImporter", "ModelImporter", "AudioImporter", "AssetPostprocessor",
        };
        private static readonly string[] EditorAttributes =
        {
            "MenuItem", "CustomEditor", "CustomPropertyDrawer", "InitializeOnLoad", "InitializeOnLoadMethod",
            "DidReloadScripts", "PostProcessBuild", "PostProcessScene", "OnOpenAsset", "CanEditMultipleObjects",
        };

        private static readonly Regex UnqualifiedStaticRx =
            new Regex(@"(?<![\w.])(" + string.Join("|", EditorStaticTypes) + @")\s*\.");
        private static readonly Regex UnqualifiedDeclRx =
            new Regex(@"(?<![\w.])(" + string.Join("|", EditorDeclaredTypes) + @")(?![\w])");
        private static readonly Regex UnqualifiedAttrRx =
            new Regex(@"\[\s*(?:\w+\s*:\s*)?(" + string.Join("|", EditorAttributes) + @")(?![\w])");

        private static void CheckUnityEditorLeakage()
        {
            // An unguarded reference to UnityEditor in code that compiles into a PLAYER assembly
            // makes the APK build fail while the Editor compiles it happily — so it is invisible
            // until a build is attempted, and this project has hit it three times (TrackManager.cs,
            // CreatureMover.cs, TMP_TextInfoDebugTool.cs). It cost more than the missing module.
            var unguardedUsings = new List<string>();
            var unguardedQualified = new List<string>();
            var unguardedUnqualified = new List<string>();
            var filesWithBadUsing = new HashSet<string>();
            int scanned = 0;

            foreach (var cs in EnumerateRuntimeScripts())
            {
                var lines = ScanCode(cs);
                if (lines.Count == 0 && SafeRead(cs) == null) continue;
                scanned++;

                var self = SelfDeclaredNames(cs);

                foreach (var line in lines)
                {
                    if (line.EditorGuarded) continue;                  // inside a UNITY_EDITOR-only region

                    if (UsingEditorRx.IsMatch(line.Code))
                    {
                        unguardedUsings.Add($"{cs}:{line.Number}  {line.Code.Trim()}");
                        filesWithBadUsing.Add(cs);
                        continue;
                    }

                    if (QualifiedEditorRx.IsMatch(line.Code) && !line.Code.Contains("nameof("))
                    {
                        unguardedQualified.Add($"{cs}:{line.Number}  {line.Code.Trim()}");
                        continue;
                    }

                    var token = FirstUnqualifiedEditorType(line.Code, self);
                    if (token != null)
                        unguardedUnqualified.Add($"{cs}:{line.Number}  [{token}]  {line.Code.Trim()}");
                }
            }

            // A file whose `using UnityEditor;` is already reported would otherwise list every
            // AssetDatabase call in it as a second finding; one fix closes them all.
            unguardedUnqualified = unguardedUnqualified
                .Where(h => !filesWithBadUsing.Any(f => h.StartsWith(f + ":", StringComparison.Ordinal))).ToList();

            var scannedNote =
                "Covers Assets/ AND the runtime assemblies of non-registry packages — a git or local package " +
                "whose Runtime asmdef has an empty includePlatforms compiles into the APK exactly like our own " +
                "code (this project has one: com.coplaydev.unity-mcp). Scripts in an Editor/ folder, scripts " +
                "under an Editor-only .asmdef, and test assemblies are correctly excluded.\n" +
                PackageScanNote();

            if (unguardedUsings.Count == 0)
                Pass($"No unguarded 'using UnityEditor' in player-compiled code ({scanned} scripts scanned)", scannedNote);
            else
                Fail($"{unguardedUsings.Count} unguarded 'using UnityEditor' in player code — the APK cannot compile at all",
                     string.Join("\n", unguardedUsings),
                     "You will not get a broken game, you will get no game: the build stops with a compile error " +
                     "and no APK is produced. Wrap the import in #if UNITY_EDITOR … #endif (and every use of it), " +
                     "delete it if unused, or move the file into an Editor/ folder. The Editor compiles these " +
                     "happily, so nothing else will warn you.");

            if (unguardedUnqualified.Count == 0)
                Pass($"No unguarded unqualified editor type (AssetDatabase, Handles, MenuItem, …) in player code",
                     "This is the leak the two checks above cannot see: a file whose `using UnityEditor;` IS " +
                     "correctly guarded, but which then uses AssetDatabase or [MenuItem] outside the guard. " +
                     "Same hard build failure, no warning anywhere else.");
            else
                Fail($"{unguardedUnqualified.Count} unguarded editor-only type use(s) in player code — the APK cannot compile",
                     string.Join("\n", unguardedUnqualified.Take(25)) + (unguardedUnqualified.Count > 25 ? "\n…" : ""),
                     "Same outcome as an unguarded using: the build stops and no APK is produced. Move the code " +
                     "inside #if UNITY_EDITOR … #endif, or into an Editor/ folder.\n" +
                     "FALSE POSITIVE CHECK: this matches by NAME, because there is no compiler here. If the line " +
                     "is really your own type or field that happens to share the name, ignore the row — comments, " +
                     "strings and types declared in the same file are already excluded.");

            if (unguardedQualified.Count > 0)
                Warn($"{unguardedQualified.Count} unguarded fully-qualified UnityEditor reference(s)",
                     string.Join("\n", unguardedQualified.Take(25)) + (unguardedQualified.Count > 25 ? "\n…" : ""),
                     "Same hard build failure as the using-directive if it is real code. Comments and string " +
                     "literals are already excluded, so treat each row as real until you have looked at it.");
        }

        /// <summary>
        /// Returns the first editor-only type name used unqualified on a line, or null. Names the
        /// file declares itself are excluded — the common false positive is a project type that
        /// happens to be called Selection or Undo.
        /// </summary>
        private static string FirstUnqualifiedEditorType(string code, HashSet<string> selfDeclared)
        {
            foreach (var rx in new[] { UnqualifiedStaticRx, UnqualifiedDeclRx, UnqualifiedAttrRx })
            {
                var m = rx.Match(code);
                if (m.Success && !selfDeclared.Contains(m.Groups[1].Value)) return m.Groups[1].Value;
            }
            return null;
        }

        private static readonly Dictionary<string, HashSet<string>> SelfDeclaredCache =
            new Dictionary<string, HashSet<string>>();

        private static HashSet<string> SelfDeclaredNames(string path)
        {
            if (SelfDeclaredCache.TryGetValue(path, out var cached)) return cached;

            var set = new HashSet<string>();
            var body = SafeRead(path);
            if (body != null)
                foreach (Match m in Regex.Matches(body, @"\b(?:class|struct|interface|enum)\s+([A-Za-z_]\w*)"))
                    set.Add(m.Groups[1].Value);

            SelfDeclaredCache[path] = set;
            return set;
        }

        private static bool ImpliesEditor(string condition)
        {
            if (string.IsNullOrEmpty(condition)) return false;
            if (Regex.IsMatch(condition, @"!\s*UNITY_EDITOR")) return false;
            return condition.Contains("UNITY_EDITOR");
        }

        /// <summary>True when the condition is exactly "!UNITY_EDITOR", so its #else IS editor-only.</summary>
        private static bool NegationImpliesEditor(string condition)
        {
            return condition != null && Regex.IsMatch(condition, @"^\s*\(*\s*!\s*UNITY_EDITOR\s*\)*\s*$");
        }

        // ------------------------------------------------------------------ shared helpers

        /// <summary>
        /// Every .cs that compiles into a PLAYER assembly. Two roots, not one:
        ///
        ///   Assets/ — minus Editor/ folders and anything under an .asmdef that is Editor-only or
        ///             a test assembly.
        ///   Packages — the runtime assemblies of every package that is NOT a Unity registry or
        ///             built-in package, i.e. the ones a human dropped into this project. A package
        ///             whose Runtime asmdef has an empty includePlatforms compiles into the APK
        ///             exactly like our own scripts, and this project has one: com.coplaydev.unity-mcp
        ///             (git URL) alongside com.kyrylokuzyk.primetween (local tarball). Scanning only
        ///             Assets/ left both of them, and any future one, completely unchecked.
        ///
        /// Unity's own registry packages are deliberately skipped: they are not where a
        /// hand-written mistake lands, and reading every .cs in URP would dominate the runtime.
        /// </summary>
        private static List<string> _runtimeScripts;
        private static List<string> _runtimeScriptSources;   // human-readable roots, for the report

        private static List<string> EnumerateRuntimeScripts()
        {
            if (_runtimeScripts != null) return _runtimeScripts;

            var asmdefCache = new Dictionary<string, bool?>();   // directory → excluded from player (null = no asmdef here)
            var result = new List<string>();
            var sources = new List<string>();

            CollectPlayerScripts("Assets", "Assets", asmdefCache, result);
            sources.Add("Assets/");

            // Packages live outside the project folder on disk (Library/PackageCache, or anywhere
            // for a local package), so PackageInfo is the only reliable way to find their real path.
            UnityEditor.PackageManager.PackageInfo[] packages = null;
            try { packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages(); }
            catch { /* package manager unavailable: Assets/ alone is still a useful scan */ }

            if (packages != null)
            {
                foreach (var pkg in packages)
                {
                    if (pkg == null) continue;
                    if (pkg.source == UnityEditor.PackageManager.PackageSource.BuiltIn) continue;
                    if (pkg.source == UnityEditor.PackageManager.PackageSource.Registry) continue;

                    var root = pkg.resolvedPath;
                    if (string.IsNullOrEmpty(root) || !SafeDirExists(root)) continue;

                    var before = result.Count;
                    CollectPlayerScripts(root, root, asmdefCache, result);
                    sources.Add(pkg.name + " (" + pkg.source + ") — " + (result.Count - before) + " player script(s)");
                }
            }

            _runtimeScripts = result;
            _runtimeScriptSources = sources;
            return result;
        }

        private static string PackageScanNote()
        {
            EnumerateRuntimeScripts();
            return _runtimeScriptSources == null || _runtimeScriptSources.Count == 0
                ? "Scanned roots: Assets/"
                : "Scanned roots: " + string.Join("  ·  ", _runtimeScriptSources);
        }

        private static void CollectPlayerScripts(string root, string stopAt, Dictionary<string, bool?> cache, List<string> into)
        {
            List<string> files;
            try { files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories).ToList(); }
            catch { return; }

            foreach (var raw in files)
            {
                var path = raw.Replace('\\', '/');

                // Unity ignores any folder ending in '~' entirely (Samples~, Documentation~).
                if (path.Contains("~/")) continue;

                var asm = NearestAsmdefExcludesPlayer(path, stopAt.Replace('\\', '/'), cache);
                if (asm.HasValue)
                {
                    if (asm.Value) continue;                     // editor-only or test assembly → not in the player
                }
                else if (path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;                                    // no asmdef → Unity's Editor-folder rule applies
                }

                into.Add(path);
            }
        }

        /// <summary>
        /// Walks up to the nearest .asmdef and reports whether it keeps its code OUT of a player
        /// build — either because includePlatforms is Editor-only, or because it is constrained on
        /// UNITY_INCLUDE_TESTS (a test assembly, which a player build never contains).
        /// Returns null when no .asmdef governs the file.
        /// </summary>
        private static bool? NearestAsmdefExcludesPlayer(string filePath, string stopAt, Dictionary<string, bool?> cache)
        {
            var dir = Path.GetDirectoryName(filePath);
            while (!string.IsNullOrEmpty(dir))
            {
                var key = dir.Replace('\\', '/');
                if (!cache.TryGetValue(key, out var cached))
                {
                    cached = null;
                    try
                    {
                        var asmdef = Directory.EnumerateFiles(key, "*.asmdef", SearchOption.TopDirectoryOnly).FirstOrDefault();
                        if (asmdef != null)
                        {
                            var stub = JsonUtility.FromJson<AsmdefStub>(File.ReadAllText(asmdef));
                            var editorOnly = stub != null && stub.includePlatforms != null &&
                                             stub.includePlatforms.Length > 0 &&
                                             stub.includePlatforms.All(p => p == "Editor");
                            var testOnly = stub != null && stub.defineConstraints != null &&
                                           stub.defineConstraints.Any(d => d != null && d.Contains("UNITY_INCLUDE_TESTS"));
                            cached = editorOnly || testOnly;
                        }
                    }
                    catch { cached = null; }
                    cache[key] = cached;
                }

                if (cached.HasValue) return cached;
                if (string.Equals(key.TrimEnd('/'), stopAt.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)) break;
                if (key.TrimEnd('/').EndsWith("Assets", StringComparison.OrdinalIgnoreCase)) break;
                dir = Path.GetDirectoryName(key);
            }
            return null;
        }

#pragma warning disable 0649   // populated by JsonUtility, never assigned in code
        [Serializable]
        private class AsmdefStub
        {
            public string[] includePlatforms;
            public string[] defineConstraints;
        }
#pragma warning restore 0649

        // ------------------------------------------------------------------ source scanning

        /// <summary>
        /// One source line, with comments and string literals blanked out and with the state of the
        /// preprocessor stack around it resolved. Shared by the UnityEditor-leakage check and the
        /// offline-integrity check so both agree on what "compiled into the player" means.
        /// </summary>
        private struct ScannedLine
        {
            public int Number;
            /// <summary>The line with comments and string/char literals replaced by spaces.</summary>
            public string Code;
            /// <summary>True when the line only ever compiles in the Editor.</summary>
            public bool EditorGuarded;
            /// <summary>The #if condition that keeps this line out of the Android player, or null.</summary>
            public string InactiveGuard;
        }

        private sealed class PpFrame
        {
            public readonly List<string> Conditions = new List<string>();
            public string Current;
            public bool EditorGuarded;
        }

        private static List<ScannedLine> ScanCode(string path)
        {
            var result = new List<ScannedLine>();
            var body = SafeRead(path);
            if (body == null) return result;

            var lines = body.Replace("\r\n", "\n").Split('\n');
            var stack = new List<PpFrame>();
            var inBlockComment = false;

            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();

                if (trimmed.StartsWith("#if", StringComparison.Ordinal) &&
                    !trimmed.StartsWith("#ifdef", StringComparison.Ordinal))
                {
                    var c = StripDirectiveComment(trimmed.Substring(3));
                    var frame = new PpFrame { Current = c, EditorGuarded = ImpliesEditor(c) };
                    frame.Conditions.Add(c);
                    stack.Add(frame);
                    continue;
                }
                if (trimmed.StartsWith("#elif", StringComparison.Ordinal))
                {
                    if (stack.Count > 0)
                    {
                        var frame = stack[stack.Count - 1];
                        // This branch also implies every earlier condition was false, so a preceding
                        // "!UNITY_EDITOR" makes it editor-only just as an explicit UNITY_EDITOR would.
                        var earlier = frame.Conditions.Any(NegationImpliesEditor);
                        var c = StripDirectiveComment(trimmed.Substring(5));
                        frame.Conditions.Add(c);
                        frame.Current = c;
                        frame.EditorGuarded = ImpliesEditor(c) || earlier;
                    }
                    continue;
                }
                if (trimmed.StartsWith("#else", StringComparison.Ordinal))
                {
                    if (stack.Count > 0)
                    {
                        // The else-branch is NOT(every condition above it). For "#if UNITY_EDITOR"
                        // that is the player branch (unguarded); for "#if !UNITY_EDITOR" it is the
                        // EDITOR branch, and treating it as unguarded — which this used to do —
                        // reports a perfectly correct file as a build blocker.
                        var frame = stack[stack.Count - 1];
                        frame.EditorGuarded = frame.Conditions.Any(NegationImpliesEditor);
                        frame.Current = "!(" + string.Join(" || ", frame.Conditions.ToArray()) + ")";
                    }
                    continue;
                }
                if (trimmed.StartsWith("#endif", StringComparison.Ordinal))
                {
                    if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                    continue;
                }
                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                    continue;                                     // #region, #pragma, #define …

                string masked;
                masked = MaskCode(lines[i], ref inBlockComment);

                // Report the OUTERMOST guard that excludes the line, not the innermost: with
                // "#if UNITY_ADS { #if UNITY_ANALYTICS … }" the answer to "what would arm this
                // code again" is the ads package, and naming the inner guard would send the
                // reader after the wrong one.
                string inactive = null;
                for (int f = 0; f < stack.Count; f++)
                {
                    if (!IsCompiledOutOfAndroidPlayer(stack[f].Current)) continue;
                    inactive = stack[f].Current;
                    break;
                }

                result.Add(new ScannedLine
                {
                    Number = i + 1,
                    Code = masked,
                    EditorGuarded = stack.Any(f => f.EditorGuarded),
                    InactiveGuard = inactive,
                });
            }

            return result;
        }

        private static string StripDirectiveComment(string condition)
        {
            if (condition == null) return "";
            var slash = condition.IndexOf("//", StringComparison.Ordinal);
            if (slash >= 0) condition = condition.Substring(0, slash);
            condition = Regex.Replace(condition, @"/\*.*?\*/", " ");
            return condition.Trim();
        }

        /// <summary>
        /// Symbols Unity defines automatically when the matching service package is installed. They
        /// never appear in scriptingDefineSymbols, so their absence there is exactly what says the
        /// code behind them is compiled out today — and their return is exactly what would arm it.
        /// </summary>
        private static readonly string[] ServiceSymbols =
        {
            "UNITY_ADS", "UNITY_ANALYTICS", "UNITY_PURCHASING", "UNITY_IAP", "UNITY_SOCIAL",
            "UNITY_ANALYTICS_EVENT_LOGS", "ENABLE_CLOUD_SERVICES", "ENABLE_CLOUD_SERVICES_ADS",
            "ENABLE_CLOUD_SERVICES_ANALYTICS", "ENABLE_CLOUD_SERVICES_PURCHASING",
        };

        /// <summary>
        /// Deliberately conservative: only says "compiled out" for a bare symbol it is sure about
        /// (UNITY_EDITOR, or a service symbol that is not in the Android define list). Anything
        /// else — an expression, an unknown symbol, a platform symbol — is treated as compiled in,
        /// so an unrecognised guard produces a loud finding rather than a quiet pass.
        /// </summary>
        private static bool IsCompiledOutOfAndroidPlayer(string condition)
        {
            if (string.IsNullOrEmpty(condition)) return false;
            if (!Regex.IsMatch(condition, @"^\(*\s*[A-Za-z_]\w*\s*\)*$")) return false;

            var symbol = Regex.Match(condition, @"[A-Za-z_]\w*").Value;
            if (symbol == "UNITY_EDITOR") return true;
            return ServiceSymbols.Contains(symbol) && !AndroidDefineSymbols().Contains(symbol);
        }

        private static HashSet<string> _androidDefines;

        private static HashSet<string> AndroidDefineSymbols()
        {
            if (_androidDefines != null) return _androidDefines;

            _androidDefines = new HashSet<string>();
            try
            {
                var raw = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android) ?? "";
                foreach (var s in raw.Split(';', ','))
                    if (!string.IsNullOrEmpty(s.Trim())) _androidDefines.Add(s.Trim());
            }
            catch { /* leave empty: every service symbol then reads as "not defined", which is the safe default */ }

            return _androidDefines;
        }

        /// <summary>
        /// Blanks out comments and string/char literals so a pattern cannot match prose. The old
        /// leakage check only skipped whole lines that *started* with a comment marker, which is
        /// why its report had to carry a "check for false positives in strings" caveat.
        /// </summary>
        private static string MaskCode(string line, ref bool inBlockComment)
        {
            if (string.IsNullOrEmpty(line)) return line ?? "";

            var sb = new StringBuilder(line.Length);
            int i = 0;
            while (i < line.Length)
            {
                if (inBlockComment)
                {
                    var end = line.IndexOf("*/", i, StringComparison.Ordinal);
                    if (end < 0) { sb.Append(' ', line.Length - i); i = line.Length; }
                    else { sb.Append(' ', end + 2 - i); i = end + 2; inBlockComment = false; }
                    continue;
                }

                var c = line[i];
                if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
                {
                    sb.Append(' ', line.Length - i);
                    break;
                }
                if (c == '/' && i + 1 < line.Length && line[i + 1] == '*')
                {
                    inBlockComment = true; sb.Append("  "); i += 2; continue;
                }
                if (c == '@' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    int j = i + 2;
                    while (j < line.Length)
                    {
                        if (line[j] == '"' && j + 1 < line.Length && line[j + 1] == '"') { j += 2; continue; }
                        if (line[j] == '"') break;
                        j++;
                    }
                    var stop = Math.Min(j + 1, line.Length);
                    sb.Append(' ', stop - i); i = stop; continue;
                }
                if (c == '"' || c == '\'')
                {
                    int j = i + 1;
                    while (j < line.Length)
                    {
                        if (line[j] == '\\') { j += 2; continue; }
                        if (line[j] == c) break;
                        j++;
                    }
                    var stop = Math.Min(j + 1, line.Length);
                    sb.Append(' ', stop - i); i = stop; continue;
                }

                sb.Append(c); i++;
            }

            return sb.ToString();
        }

        private static string _projectSettingsText;

        private static string ReadProjectSettingsText()
        {
            if (_projectSettingsText != null) return _projectSettingsText;
            try { _projectSettingsText = File.ReadAllText("ProjectSettings/ProjectSettings.asset"); }
            catch { _projectSettingsText = null; }
            return _projectSettingsText;
        }

        private static string ReadProjectSettingValue(string key)
        {
            var text = ReadProjectSettingsText();
            if (text == null) return null;
            var m = Regex.Match(text, @"^\s*" + Regex.Escape(key) + @":\s*(\S+)\s*$", RegexOptions.Multiline);
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string ExtractBlock(string text, string startKey, string endKey)
        {
            if (text == null) return null;
            var start = text.IndexOf(startKey, StringComparison.Ordinal);
            if (start < 0) return null;
            var end = text.IndexOf(endKey, start, StringComparison.Ordinal);
            return end < 0 ? text.Substring(start) : text.Substring(start, end - start);
        }

        /// <summary>Pulls the "- m_BuildTarget: Android" sub-block out of an icon block.</summary>
        private static string ExtractTargetBlock(string block, string target)
        {
            if (block == null) return null;
            var marker = "m_BuildTarget: " + target;
            var start = block.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return null;
            var next = block.IndexOf("- m_BuildTarget:", start + marker.Length, StringComparison.Ordinal);
            return next < 0 ? block.Substring(start) : block.Substring(start, next - start);
        }

        private static int SafeCount(Func<int> f) { try { return f(); } catch { return 0; } }

        private static List<T> SafeList<T>(Func<List<T>> f) { try { return f() ?? new List<T>(); } catch { return new List<T>(); } }

        /// <summary>File.ReadAllText that returns null instead of throwing on anything.</summary>
        private static string SafeRead(string path)
        {
            try { return File.ReadAllText(path); } catch { return null; }
        }

        private static bool SafeDirExists(string path)
        {
            try { return Directory.Exists(path); } catch { return false; }
        }
    }
}
