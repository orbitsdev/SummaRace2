#if UNITY_EDITOR
// -----------------------------------------------------------------------------
// DeviceBudgetTools — safe, idempotent import-setting fixes for the 2GB Android 8
// floor device. Lives in an Editor folder (Assembly-CSharp-Editor) AND is wrapped
// in #if UNITY_EDITOR, so it can never reach a player build.
//
// Full analysis, with the measurements these rules are derived from:
//     Documentation/SummaRace_Device_Budget.md
//
// WHAT THIS DOES (all look-preserving):
//   * audio load types      — the six MusicPlayer stems and the long music tracks
//                             are DecompressOnLoad today, which costs ~178 MB of
//                             raw PCM. Fixing that saves ~124 MB of resident RAM.
//   * audio load types      — 22 of Trash Dash's one-shot SFX are Streaming, which
//                             is the wrong shape for a <1s clip.
//   * texture compression   — two reachable textures import Uncompressed (12.0 MB
//                             of RGBA32 between them and the app icon).
//   * Read/Write flags      — 0 offenders today; the rule keeps it that way.
//
// WHAT THIS DELIBERATELY DOES NOT DO (owner decisions — see the doc, §1.4 / §5):
//   * touch Assets/RenderingPipeline.asset or Assets/UIRenderer.asset
//     (shadow cascades, shadowmap size, IntermediateTextureMode, renderScale)
//   * touch Build Settings (dropping the dead legacy Race.unity)
//   * delete ANY file (music_race.ogg duplicate, Ch46_nonPBR.fbx, TMP Examples,
//     Assets/_Recovery)
//   * change ProjectSettings (targetFrameRate, Swappy, graphics API, accelerometer)
//   * modify any scene or prefab
//
// It is idempotent: it reads the current value first and only writes + reimports
// when something actually differs. A second run reports "0 changed".
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SummaRace.EditorTools
{
    public static class DeviceBudgetTools
    {
        // ---------------------------------------------------------------------
        // Scope
        // ---------------------------------------------------------------------

        /// <summary>
        /// Every audio asset that can reach the shipping build lives under one of
        /// these. Deliberately excludes Assets/Audio/Kenney (505 unreachable source
        /// files) so a run does not churn the asset database for nothing.
        /// </summary>
        private static readonly string[] AudioRoots =
        {
            "Assets/_Game/Resources/Audio",
            "Assets/_Game/Resources/Stories/Narration",
            "Assets/Sounds",
        };

        /// <summary>The six stems MusicPlayer.RestartAllStems() plays simultaneously.</summary>
        private const string StemFolder = "Assets/Sounds/Stems/";

        /// <summary>Never recompress: this is ProjectSettings.m_Icon's source image.</summary>
        private static readonly string[] TextureSkipList =
        {
            "Assets/_Game/Art/UI/app_icon.png",
        };

        /// <summary>Clips at or under this length are one-shots, not music.</summary>
        private const float ShortClipSeconds = 10f;

        /// <summary>Clips longer than this are long-form music.</summary>
        private const float MusicClipSeconds = 20f;

        /// <summary>Opt-in only. Vorbis quality for long music (1.0 = 100%, the current value).</summary>
        private const float ReducedMusicQuality = 0.5f;

        // ---------------------------------------------------------------------
        // Menu
        // ---------------------------------------------------------------------

        [MenuItem("SummaRace/Device Budget/1 · Audit (report only, changes nothing)", false, 100)]
        public static void Audit()
        {
            var log = new Report("AUDIT (no changes made)");
            ProcessAudio(log, dryRun: true);
            ProcessTextures(log, dryRun: true);
            ProcessMeshes(log, dryRun: true);
            ReportExtras(log);
            log.Flush(reimported: 0);
        }

        [MenuItem("SummaRace/Device Budget/2 · Apply safe AUDIO import settings", false, 200)]
        public static void ApplyAudio()
        {
            if (!Confirm("audio import settings",
                    "Re-imports (and re-encodes) the clips it changes — roughly 30 files, a minute or two.\n\n" +
                    "Saves an estimated ~124 MB of resident RAM on the floor device.\n" +
                    "No audio is deleted and no clip's encoding quality is altered."))
                return;

            var log = new Report("APPLY — audio import settings");
            int n = ProcessAudio(log, dryRun: false);
            Finish(log, n);
        }

        [MenuItem("SummaRace/Device Budget/3 · Apply safe TEXTURE + MESH flags", false, 300)]
        public static void ApplyTexturesAndMeshes()
        {
            if (!Confirm("texture + mesh import flags",
                    "Compresses textures that currently import Uncompressed (ASTC 4x4, near-lossless)\n" +
                    "and clears any Read/Write Enabled flags.\n\n" +
                    "The app icon source is deliberately skipped."))
                return;

            var log = new Report("APPLY — texture + mesh flags");
            int n = ProcessTextures(log, dryRun: false);
            n += ProcessMeshes(log, dryRun: false);
            Finish(log, n);
        }

        [MenuItem("SummaRace/Device Budget/4 · Apply ALL safe fixes", false, 400)]
        public static void ApplyAll()
        {
            if (!Confirm("all safe fixes",
                    "Runs items 2 and 3 together.\n\n" +
                    "Look-preserving only. Nothing is deleted, no scene is touched, and the render\n" +
                    "pipeline assets are not modified (those are manual owner decisions — see\n" +
                    "Documentation/SummaRace_Device_Budget.md)."))
                return;

            var log = new Report("APPLY — all safe fixes");
            int n = ProcessAudio(log, dryRun: false);
            n += ProcessTextures(log, dryRun: false);
            n += ProcessMeshes(log, dryRun: false);
            ReportExtras(log);
            Finish(log, n);
        }

        [MenuItem("SummaRace/Device Budget/5 · OPTIONAL — lower music Vorbis quality to 50% (AUDIBLE)", false, 500)]
        public static void ApplyMusicQuality()
        {
            if (!EditorUtility.DisplayDialog(
                    "SummaRace — lower music encoding quality",
                    "This is NOT a look-preserving change: it re-encodes the long music clips at\n" +
                    "50% Vorbis quality instead of 100%.\n\n" +
                    "Estimated APK saving: ~15 MB.\n" +
                    "In practice it is inaudible — MusicPlayer.maxVolume is 0.1, so the stems play\n" +
                    "at 10% volume under gameplay — but it IS a quality reduction and it is\n" +
                    "irreversible without re-importing the source files.\n\n" +
                    "Proceed?",
                    "Lower quality", "Cancel"))
                return;

            var log = new Report("APPLY — music Vorbis quality → " +
                                 ReducedMusicQuality.ToString("0.##", CultureInfo.InvariantCulture));
            int changed = 0;

            foreach (string path in FindAssets("t:AudioClip", AudioRoots))
            {
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) continue;

                float length = ClipLength(path);
                if (length <= MusicClipSeconds) continue;

                var s = importer.defaultSampleSettings;
                if (Mathf.Approximately(s.quality, ReducedMusicQuality)) continue;

                log.Change(path, "quality " + s.quality.ToString("0.##", CultureInfo.InvariantCulture) +
                                 " → " + ReducedMusicQuality.ToString("0.##", CultureInfo.InvariantCulture));
                s.quality = ReducedMusicQuality;
                importer.defaultSampleSettings = s;
                ApplyAndroidOverride(importer, s);
                importer.SaveAndReimport();
                changed++;
            }

            Finish(log, changed);
        }

        // ---------------------------------------------------------------------
        // Audio
        // ---------------------------------------------------------------------

        private static int ProcessAudio(Report log, bool dryRun)
        {
            log.Section("AUDIO — load types");

            int changed = 0;
            long pcmBefore = 0, pcmAfter = 0;
            int stems = 0, music = 0, sfx = 0, narration = 0, skipped = 0;

            foreach (string path in FindAssets("t:AudioClip", AudioRoots))
            {
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (importer == null || clip == null) continue;

                float length = clip.length;
                int channels = importer.forceToMono ? 1 : Mathf.Max(1, clip.channels);
                long pcm = (long)(length * clip.frequency * channels * 2);

                AudioClipLoadType wantLoad;
                bool wantPreload, wantBackground;
                string reason;

                bool isStem = path.StartsWith(StemFolder, StringComparison.OrdinalIgnoreCase);
                bool isNarration = path.IndexOf("/Stories/Narration/", StringComparison.OrdinalIgnoreCase) >= 0;

                if (isStem)
                {
                    // Six of these play at once and must stay sample-synced. Streaming
                    // six concurrent Vorbis reads off eMMC risks underrun and drift, so
                    // CompressedInMemory is the right shape: ~31 MB compressed in RAM
                    // instead of 127 MB of PCM, decoded on the fly.
                    wantLoad = AudioClipLoadType.CompressedInMemory;
                    wantPreload = false; wantBackground = true;
                    reason = "MusicPlayer stem"; stems++;
                }
                else if (isNarration)
                {
                    // Already correct for all 150 (P5). The rule makes it self-healing.
                    wantLoad = AudioClipLoadType.CompressedInMemory;
                    wantPreload = false; wantBackground = true;
                    reason = "narration"; narration++;
                }
                else if (length > MusicClipSeconds)
                {
                    // Single-source long music: one instance, no sync requirement.
                    wantLoad = AudioClipLoadType.Streaming;
                    wantPreload = false; wantBackground = true;
                    reason = "long music"; music++;
                }
                else if (length <= ShortClipSeconds)
                {
                    // One-shots: tiny PCM, zero latency, no streaming decoder.
                    wantLoad = AudioClipLoadType.DecompressOnLoad;
                    wantPreload = true; wantBackground = false;
                    reason = "short SFX"; sfx++;
                }
                else
                {
                    // 10-20s: ambiguous. Leave whatever the author chose.
                    skipped++;
                    continue;
                }

                var s = importer.defaultSampleSettings;
                bool loadDiff = s.loadType != wantLoad;
                // preloadAudioData lives on the sample settings, not the importer. The importer
                // property is obsolete-as-ERROR (CS0619), so reading it does not merely warn —
                // it fails the whole Assembly-CSharp-Editor compile, taking BuildPreflight and
                // the EditMode test suite down with this file.
                bool preDiff = s.preloadAudioData != wantPreload;
                bool bgDiff = importer.loadInBackground != wantBackground;

                pcmBefore += s.loadType == AudioClipLoadType.DecompressOnLoad ? pcm : 0;
                pcmAfter += wantLoad == AudioClipLoadType.DecompressOnLoad ? pcm : 0;

                if (!loadDiff && !preDiff && !bgDiff) continue;

                var parts = new List<string>();
                if (loadDiff) parts.Add(s.loadType + " → " + wantLoad);
                if (preDiff) parts.Add("preload " + s.preloadAudioData + " → " + wantPreload);
                if (bgDiff) parts.Add("loadInBackground " + importer.loadInBackground + " → " + wantBackground);

                log.Change(path, string.Format(CultureInfo.InvariantCulture,
                    "[{0}, {1:0.0}s, {2:0.00} MB PCM] {3}",
                    reason, length, pcm / 1e6, string.Join("; ", parts.ToArray())));

                if (!dryRun)
                {
                    // Set both on the struct BEFORE writing it back, so the Android override
                    // below carries the preload flag too rather than inheriting the old value.
                    s.loadType = wantLoad;
                    s.preloadAudioData = wantPreload;
                    importer.defaultSampleSettings = s;
                    ApplyAndroidOverride(importer, s);
                    importer.loadInBackground = wantBackground;
                    importer.SaveAndReimport();
                }
                changed++;
            }

            log.Note(string.Format(CultureInfo.InvariantCulture,
                "scanned: {0} stems, {1} long music, {2} short SFX, {3} narration, {4} left alone (10-20s)",
                stems, music, sfx, narration, skipped));
            log.Note(string.Format(CultureInfo.InvariantCulture,
                "resident PCM from DecompressOnLoad: {0:0.0} MB → {1:0.0} MB  (saving {2:0.0} MB)",
                pcmBefore / 1e6, pcmAfter / 1e6, (pcmBefore - pcmAfter) / 1e6));
            return changed;
        }

        /// <summary>
        /// If the clip carries an explicit Android override it wins over the default,
        /// so mirror the settings into it rather than leaving a stale override behind.
        /// </summary>
        private static void ApplyAndroidOverride(AudioImporter importer, AudioImporterSampleSettings s)
        {
            try
            {
                if (importer.ContainsSampleSettingsOverride("Android"))
                    importer.SetOverrideSampleSettings("Android", s);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DeviceBudget] could not mirror the Android override on " +
                                 importer.assetPath + ": " + e.Message);
            }
        }

        // ---------------------------------------------------------------------
        // Textures
        // ---------------------------------------------------------------------

        private static int ProcessTextures(Report log, bool dryRun)
        {
            log.Section("TEXTURES — compression + Read/Write");

            int changed = 0, scanned = 0, skipped = 0;
            int mipSprites = 0;
            long vramBefore = 0, vramAfter = 0;
            var mipReport = new List<string>();

            foreach (string path in FindAssets("t:Texture2D", new[] { "Assets" }))
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                scanned++;

                bool skip = TextureSkipList.Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));

                // -- report-only: sprites carrying mipmaps -----------------------
                if (importer.textureType == TextureImporterType.Sprite && importer.mipmapEnabled)
                {
                    mipSprites++;
                    if (mipReport.Count < 20) mipReport.Add(path);
                }

                var changes = new List<string>();

                // -- Read/Write Enabled doubles memory (a CPU copy is kept) -------
                if (importer.isReadable)
                {
                    changes.Add("isReadable true → false");
                    if (!dryRun) importer.isReadable = false;
                }

                // -- Uncompressed import ------------------------------------------
                if (importer.textureCompression == TextureImporterCompression.Uncompressed)
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    long raw = tex != null ? (long)tex.width * tex.height * 4 : 0;

                    if (skip)
                    {
                        skipped++;
                        log.Note(string.Format(CultureInfo.InvariantCulture,
                            "SKIPPED (app icon source, deliberate): {0}  [{1:0.00} MB RGBA32]", path, raw / 1e6));
                    }
                    else
                    {
                        vramBefore += raw;
                        vramAfter += raw / 4; // ASTC 4x4 / CompressedHQ is 8 bpp vs 32 bpp
                        changes.Add(string.Format(CultureInfo.InvariantCulture,
                            "compression Uncompressed → CompressedHQ  [{0:0.00} MB → ~{1:0.00} MB]",
                            raw / 1e6, raw / 4e6));
                        if (!dryRun)
                            importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    }
                }

                // -- a stale Android override can silently win --------------------
                var android = importer.GetPlatformTextureSettings("Android");
                if (android != null && android.overridden &&
                    android.textureCompression == TextureImporterCompression.Uncompressed && !skip)
                {
                    changes.Add("Android override Uncompressed → CompressedHQ");
                    if (!dryRun)
                    {
                        android.textureCompression = TextureImporterCompression.CompressedHQ;
                        importer.SetPlatformTextureSettings(android);
                    }
                }

                if (changes.Count == 0) continue;

                log.Change(path, string.Join("; ", changes.ToArray()));
                if (!dryRun) importer.SaveAndReimport();
                changed++;
            }

            log.Note("scanned " + scanned + " textures, skipped " + skipped + " by explicit skip list");
            if (vramBefore > 0)
                log.Note(string.Format(CultureInfo.InvariantCulture,
                    "uncompressed texture memory: {0:0.00} MB → ~{1:0.00} MB  (saving ~{2:0.00} MB)",
                    vramBefore / 1e6, vramAfter / 1e6, (vramBefore - vramAfter) / 1e6));

            if (mipSprites > 0)
            {
                log.Note("REPORT ONLY — " + mipSprites + " sprite(s) have mipmaps enabled. Most are " +
                         "world decals (Assets/Textures/Graffiti) where mipmaps are WANTED; disabling them " +
                         "on a 3D surface causes shimmering. Not changed automatically — review by hand:");
                foreach (string p in mipReport) log.Note("    " + p);
            }

            return changed;
        }

        // ---------------------------------------------------------------------
        // Meshes
        // ---------------------------------------------------------------------

        private static int ProcessMeshes(Report log, bool dryRun)
        {
            log.Section("MESHES — Read/Write");

            int changed = 0, scanned = 0;
            foreach (string path in FindAssets("t:Model", new[] { "Assets" }))
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                scanned++;
                if (!importer.isReadable) continue;

                log.Change(path, "ModelImporter.isReadable true → false (a readable mesh keeps a full CPU copy)");
                if (!dryRun)
                {
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
                changed++;
            }

            log.Note("scanned " + scanned + " models");
            return changed;
        }

        // ---------------------------------------------------------------------
        // Extras (report only)
        // ---------------------------------------------------------------------

        private static void ReportExtras(Report log)
        {
            log.Section("MANUAL STEPS — NOT performed by this tool");
            log.Note("These need an owner decision and a playtest. Detail + measurements:");
            log.Note("    Documentation/SummaRace_Device_Budget.md");
            log.Note("");
            log.Note("  RENDER (Assets/RenderingPipeline.asset — the ACTIVE pipeline, not Mobile_RPAsset):");
            log.Note("    • m_ShadowCascadeCount 4 → 1                (removes 3 of 4 shadow passes)");
            log.Note("    • m_MainLightShadowmapResolution 2048 → 512 (-94% shadow texels)");
            log.Note("    • m_MainLightShadowsSupported 1 → 0        (nothing in MainSummaRace can");
            log.Note("      receive a shadow: every corridor material is a single-pass unlit shader");
            log.Note("      with no ShadowCaster, and the scene references zero URP/Lit materials)");
            log.Note("    • Assets/UIRenderer.asset m_IntermediateTextureMode Always → Auto");
            log.Note("    • m_RenderScale 1.0 → 0.8 is the biggest lever but VISIBLY SOFTER — last resort");
            log.Note("    • do NOT reassign Mobile_RPAsset: the F26-F42 look was built against THEIR");
            log.Note("      pipeline, and Mobile_RPAsset turns HDR back on (a regression on a tiler)");
            log.Note("");
            log.Note("  PAYLOAD:");
            log.Note("    • drop Assets/_Game/Scenes/Race.unity from Build Settings (zero call sites;");
            log.Note("      51.5 MB of source assets are exclusive to it, 29 MB of that Ch46_nonPBR.fbx)");
            log.Note("    • delete Assets/_Game/Resources/Audio/music_race.ogg — byte-identical to");
            log.Note("      Assets/Sounds/Stems/STEMSMainTrackMono.ogg and on a dead path (7.3 MB)");
            log.Note("    • delete Assets/TextMesh Pro/Examples & Extras (2.3 MB ships via its own");
            log.Note("      Resources folder; also removes an unguarded 'using UnityEditor;')");
            log.Note("    • delete Assets/_Recovery (4 stale autosave scenes, still on disk)");
            log.Note("");
            log.Note("  PLAYER / CODE:");
            log.Note("    • GameRules.TargetFrameRate 60 → 30 (MusicPlayer.Awake already pins 30 once");
            log.Note("      the race loads, so the app currently targets 60 until the first race)");
            log.Note("    • Player → Android → Optimized Frame Pacing (Swappy) is OFF; turn it on");
            log.Note("    • accelerometerFrequency 60 → 0 (portrait-locked, no tilt input)");
            log.Note("    • graphics APIs are Vulkan-then-GLES3; test both on the study device");
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private static IEnumerable<string> FindAssets(string filter, string[] roots)
        {
            var valid = roots.Where(AssetDatabase.IsValidFolder).ToArray();
            if (valid.Length == 0) yield break;

            foreach (string guid in AssetDatabase.FindAssets(filter, valid))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path)) yield return path;
            }
        }

        private static float ClipLength(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            return clip != null ? clip.length : 0f;
        }

        private static bool Confirm(string what, string body)
        {
            return EditorUtility.DisplayDialog(
                "SummaRace — apply " + what,
                body + "\n\nThis is idempotent: running it again will report 0 changes.\n" +
                "Nothing is deleted and no scene, prefab or render-pipeline asset is touched.",
                "Apply", "Cancel");
        }

        private static void Finish(Report log, int changed)
        {
            if (changed > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            log.Flush(changed);
        }

        /// <summary>Accumulates one console message so the run reads as a single report.</summary>
        private sealed class Report
        {
            private readonly StringBuilder _sb = new StringBuilder();
            private int _changes;

            public Report(string title)
            {
                _sb.AppendLine("========================================================");
                _sb.AppendLine("  SummaRace Device Budget — " + title);
                _sb.AppendLine("  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                _sb.AppendLine("========================================================");
            }

            public void Section(string name)
            {
                _sb.AppendLine();
                _sb.AppendLine("--- " + name + " " + new string('-', Math.Max(0, 50 - name.Length)));
            }

            public void Change(string path, string detail)
            {
                _changes++;
                _sb.AppendLine("  CHANGE  " + path);
                _sb.AppendLine("          " + detail);
            }

            public void Note(string text)
            {
                _sb.AppendLine("  " + text);
            }

            public void Flush(int reimported)
            {
                _sb.AppendLine();
                _sb.AppendLine("--------------------------------------------------------");
                _sb.AppendLine("  " + _changes + " asset(s) needed a change; " + reimported + " re-imported.");
                if (_changes == 0)
                    _sb.AppendLine("  Nothing to do — the project already matches the safe profile.");
                _sb.AppendLine("========================================================");
                Debug.Log(_sb.ToString());
            }
        }
    }
}
#endif
