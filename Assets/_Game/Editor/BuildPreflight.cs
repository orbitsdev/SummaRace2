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

            Run("Editor & platform", CheckEditorAndPlatform);
            Run("Build Settings scenes", CheckBuildScenes);
            Run("SceneNames coverage", CheckSceneNamesCoverage);
            Run("Player settings (Android)", CheckPlayerSettings);
            Run("App icon", CheckIcons);
            Run("Signing", CheckSigning);
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
            var handler = ReadProjectSettingValue("activeInputHandler");
            if (handler == "2") Pass("Input handling = Both (required)",
                "Trash Dash's CharacterInputController uses legacy Input.touchCount for the race swipe; " +
                "our UI uses the new Input System. Both must stay enabled.");
            else if (handler == "0") Warn("Input handling = Old only",
                "The new Input System drives our UI (InputSystemUIInputModule) and EndlessKeyboardInput.",
                "Project Settings ▸ Player ▸ Active Input Handling = Both.");
            else if (handler == "1") Fail("Input handling = New only — race touch controls will throw",
                "CharacterInputController calls Input.touchCount / Input.GetKeyDown; those throw " +
                "InvalidOperationException when only the new system is active. Swipe steering breaks on device.",
                "Project Settings ▸ Player ▸ Active Input Handling = Both.");
            else Info("Input handling: could not read activeInputHandler");
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
                     "Worth ~23MB of APK if size becomes the binding constraint.");
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
            // unbuilt Addressables content means an empty road and no runner.
            var addressableCallers = SafeCount(() =>
                Directory.EnumerateFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories)
                         .Count(f => File.ReadAllText(f).Contains("Addressables.")));
            Info("Addressables is on the shipping path",
                 addressableCallers + " script(s) under Assets/Scripts call Addressables (track segments, themes, " +
                 "zones, the character prefab). In the Editor these resolve off the Asset Database, so a missing " +
                 "content build is invisible until the APK runs.");

            var settings = File.ReadAllText(settingsPath);
            var m = Regex.Match(settings, @"m_BuildAddressablesWithPlayerBuild:\s*(\d+)");
            if (m.Success)
            {
                // AddressableAssetSettings.PlayerBuildOption: 0 PreferencesValue, 1 BuildWithPlayer, 2 DoNotBuildWithPlayer
                var v = m.Groups[1].Value;
                if (v == "1")
                    Pass("Addressables content is built with the player", "BuildAddressablesWithPlayerBuild = BuildWithPlayer");
                else if (v == "2")
                    Fail("Addressables content will NOT be built with the player",
                         "BuildAddressablesWithPlayerBuild = DoNotBuildWithPlayer.",
                         "Either flip it (Window ▸ Asset Management ▸ Addressables ▸ Settings ▸ Build Addressables on " +
                         "Player Build = Build Addressables content on Player Build) or run " +
                         "Window ▸ Asset Management ▸ Addressables ▸ Groups ▸ Build ▸ New Build ▸ Default Build Script " +
                         "by hand BEFORE every APK build. Forgetting this ships a race with no track.");
                else
                    Warn("Addressables build-with-player follows the global Preferences value",
                         "BuildAddressablesWithPlayerBuild = PreferencesValue — the setting lives in Unity Preferences, " +
                         "not in the repo, so it is per-machine and invisible in review.",
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
                Fail("No Addressables content has ever been built for Android",
                     "Expected " + aaAndroid + " — it does not exist.",
                     "Window ▸ Asset Management ▸ Addressables ▸ Groups ▸ Build ▸ New Build ▸ Default Build Script, " +
                     "with the platform already switched to Android (content is per-platform).");
            }

            var groups = SafeCount(() =>
                Directory.EnumerateFiles("Assets/AddressableAssetsData/AssetGroups", "*.asset", SearchOption.TopDirectoryOnly).Count());
            Info(groups + " Addressables groups", "Unused groups cost ~nothing in the APK — their meshes and " +
                 "textures are shared with what does ship, measured previously. Deleting them is not a size fix.");
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

            // --- networking calls reachable from a shipping scene
            var builtScenePaths = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)
                                                            .Where(File.Exists).ToList();
            var sceneText = new Dictionary<string, string>();
            foreach (var p in builtScenePaths)
            {
                try { sceneText[p] = File.ReadAllText(p); } catch { }
            }

            var netPattern = new Regex(@"Application\.OpenURL|UnityWebRequest|System\.Net|new\s+WWW\s*\(|HttpClient|TcpClient|Socket\s*\(");
            var offenders = new List<string>();
            var reachable = new List<string>();

            foreach (var cs in EnumerateRuntimeScripts())
            {
                string body;
                try { body = File.ReadAllText(cs); } catch { continue; }
                if (!netPattern.IsMatch(body)) continue;

                offenders.Add(cs);

                var guid = AssetDatabase.AssetPathToGUID(cs.Replace('\\', '/'));
                if (string.IsNullOrEmpty(guid)) continue;
                foreach (var kv in sceneText)
                    if (kv.Value.Contains(guid))
                        reachable.Add(Path.GetFileName(cs) + "  in  " + kv.Key);
            }

            if (reachable.Count == 0)
                Pass("No networking call is reachable from a scene in the build list",
                     offenders.Count == 0
                        ? "No runtime script contains one at all."
                        : offenders.Count + " runtime script(s) contain one but no build scene references them:\n" +
                          string.Join("\n", offenders));
            else
                Fail("A networking call is reachable from a shipping scene", string.Join("\n", reachable),
                     "100% offline is a non-negotiable. Remove the component (both OpenURL objects were deleted once " +
                     "before and this is how they would be caught coming back).");
        }

        // ------------------------------------------------------------------ 11. UnityEditor leakage

        private static void CheckUnityEditorLeakage()
        {
            // An unguarded `using UnityEditor;` in code that compiles into Assembly-CSharp makes the
            // PLAYER build fail while the Editor compiles it happily — so it is invisible until a
            // build is attempted, and this project has hit it three times (TrackManager.cs,
            // CreatureMover.cs, TMP_TextInfoDebugTool.cs). It cost more than the missing module.
            var usingRx = new Regex(@"^\s*using\s+(static\s+)?([A-Za-z_]\w*\s*=\s*)?UnityEditor(\.|;|\s)");
            var qualifiedRx = new Regex(@"(?<![\w.])UnityEditor(Internal)?\s*\.");

            var unguardedUsings = new List<string>();
            var unguardedQualified = new List<string>();
            int scanned = 0;

            foreach (var cs in EnumerateRuntimeScripts())
            {
                string[] lines;
                try { lines = File.ReadAllLines(cs); } catch { continue; }
                scanned++;

                var stack = new List<bool>();   // one entry per open #if, true when UNITY_EDITOR-guarded
                for (int i = 0; i < lines.Length; i++)
                {
                    var raw = lines[i];
                    var line = raw.TrimStart();

                    if (line.StartsWith("#if"))
                    {
                        stack.Add(ImpliesEditor(line.Substring(3)));
                        continue;
                    }
                    if (line.StartsWith("#elif"))
                    {
                        if (stack.Count > 0) stack[stack.Count - 1] = ImpliesEditor(line.Substring(5));
                        continue;
                    }
                    if (line.StartsWith("#else"))
                    {
                        // The else-branch of a UNITY_EDITOR block is the player branch: not guarded.
                        if (stack.Count > 0) stack[stack.Count - 1] = false;
                        continue;
                    }
                    if (line.StartsWith("#endif"))
                    {
                        if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                        continue;
                    }

                    if (stack.Any(g => g)) continue;                       // inside a UNITY_EDITOR region
                    if (line.StartsWith("//") || line.StartsWith("*") || line.StartsWith("/*")) continue;

                    if (usingRx.IsMatch(raw)) unguardedUsings.Add($"{cs}:{i + 1}  {line.Trim()}");
                    else if (qualifiedRx.IsMatch(raw) && !line.Contains("nameof(")) unguardedQualified.Add($"{cs}:{i + 1}  {line.Trim()}");
                }
            }

            if (unguardedUsings.Count == 0)
                Pass($"No unguarded 'using UnityEditor' in runtime-compiled code ({scanned} scripts scanned)",
                     "Scripts inside an Editor/ folder, and scripts governed by an Editor-only .asmdef, are correctly excluded.");
            else
                Fail($"{unguardedUsings.Count} unguarded 'using UnityEditor' in runtime code — the player build cannot compile",
                     string.Join("\n", unguardedUsings),
                     "Wrap the import in #if UNITY_EDITOR … #endif (and every use of it), or delete it if unused, " +
                     "or move the file into an Editor/ folder. The Editor compiles these fine, so nothing else will warn you.");

            if (unguardedQualified.Count > 0)
                Warn($"{unguardedQualified.Count} unguarded fully-qualified UnityEditor reference(s)",
                     string.Join("\n", unguardedQualified.Take(25)) + (unguardedQualified.Count > 25 ? "\n…" : ""),
                     "Same failure mode as the using-directive if it is real code; check for false positives in " +
                     "strings and block comments.");
        }

        private static bool ImpliesEditor(string condition)
        {
            if (string.IsNullOrEmpty(condition)) return false;
            if (Regex.IsMatch(condition, @"!\s*UNITY_EDITOR")) return false;
            return condition.Contains("UNITY_EDITOR");
        }

        // ------------------------------------------------------------------ shared helpers

        /// <summary>
        /// Every .cs under Assets/ that compiles into a PLAYER assembly: excludes Editor/ folders
        /// and anything governed by the nearest enclosing .asmdef whose includePlatforms is Editor.
        /// </summary>
        private static List<string> _runtimeScripts;

        private static List<string> EnumerateRuntimeScripts()
        {
            if (_runtimeScripts != null) return _runtimeScripts;

            var asmdefCache = new Dictionary<string, bool?>();   // directory → is editor-only (null = no asmdef here)
            var result = new List<string>();

            List<string> files;
            try { files = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories).ToList(); }
            catch { _runtimeScripts = result; return result; }

            foreach (var raw in files)
            {
                var path = raw.Replace('\\', '/');

                var asm = NearestAsmdefIsEditorOnly(path, asmdefCache);
                if (asm.HasValue)
                {
                    if (asm.Value) continue;                     // editor-only assembly → not in the player
                }
                else if (path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;                                    // no asmdef → Unity's Editor-folder rule applies
                }

                result.Add(path);
            }

            _runtimeScripts = result;
            return result;
        }

        private static bool? NearestAsmdefIsEditorOnly(string filePath, Dictionary<string, bool?> cache)
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
                            cached = stub != null && stub.includePlatforms != null &&
                                     stub.includePlatforms.Length > 0 &&
                                     stub.includePlatforms.All(p => p == "Editor");
                        }
                    }
                    catch { cached = null; }
                    cache[key] = cached;
                }

                if (cached.HasValue) return cached;
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
        }
#pragma warning restore 0649

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
    }
}
