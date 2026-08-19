using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace SummaRace.EditorTools
{
    /// <summary>
    /// Answers one question that no amount of reading the .asset files can settle.
    ///
    /// 13 of the 15 bundled Addressables groups have an EMPTY m_BuildPath, m_LoadPath and
    /// provider class on disk -- inherited from the Endless Runner import. Build Preflight
    /// reports it as a WARN rather than a failure, on the reasoning that
    /// BundledAssetGroupSchema.OnEnable very likely re-seeds them to the Local profile
    /// variables (which is presumably why the sample project ever built).
    ///
    /// "Very likely" is not good enough for the six groups the race loads its road, scenery
    /// and runner from: if the paths really are empty at build time, the content build
    /// produces bundles nothing can find, TrackManager.instance stays null, and the race
    /// never starts on the tablet -- the exact failure the whole ship pass is trying to avoid,
    /// discovered on build day instead of now.
    ///
    /// This reads the LIVE, post-OnEnable values through the same API the content build uses,
    /// so what it prints is what the build will see. Read-only: it changes nothing.
    /// </summary>
    public static class AddressablesGroupAudit
    {
        /// <summary>The groups the endless race cannot start without.</summary>
        private static readonly string[] RaceCritical =
        {
            "Themes", "Default-Zones", "Night-Zones", "Default-Sky", "Night-Sky",
            "Duplicate Asset Isolation",
        };

        [MenuItem("SummaRace/Audit Addressables Groups", false, 1)]
        public static void Audit()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[AddressablesGroupAudit] No AddressableAssetSettings found. " +
                               "Window > Asset Management > Addressables > Groups, then re-run.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== Addressables group audit (live values, as the content build sees them) ===");
            sb.AppendLine("Profile: " + settings.profileSettings.GetProfileName(settings.activeProfileId));
            sb.AppendLine();

            int bundled = 0, emptyPaths = 0, emptyProvider = 0, notIncluded = 0, criticalBroken = 0;

            foreach (var group in settings.groups)
            {
                if (group == null) continue;

                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null)
                {
                    // Built In Data / PlayerDataGroup have no bundle schema by design.
                    sb.AppendLine("  ·  " + group.Name + "  (no bundled schema -- expected for built-in data)");
                    continue;
                }

                bundled++;

                // GetValue resolves the profile variable to the string the build actually uses.
                // An empty result here is a real defect; an empty m_Id on disk with a resolved
                // value here means Unity re-seeded it and the WARN is benign.
                string build = schema.BuildPath != null ? schema.BuildPath.GetValue(settings) : null;
                string load = schema.LoadPath != null ? schema.LoadPath.GetValue(settings) : null;
                string provider = schema.BundledAssetProviderType.Value != null
                    ? schema.BundledAssetProviderType.Value.Name
                    : null;

                bool pathsBad = string.IsNullOrEmpty(build) || string.IsNullOrEmpty(load);
                bool providerBad = string.IsNullOrEmpty(provider);
                bool critical = System.Array.IndexOf(RaceCritical, group.Name) >= 0;

                if (pathsBad) emptyPaths++;
                if (providerBad) emptyProvider++;
                if (!schema.IncludeInBuild) notIncluded++;
                if (critical && (pathsBad || providerBad || !schema.IncludeInBuild)) criticalBroken++;

                string glyph = pathsBad || providerBad || !schema.IncludeInBuild ? "  X " : "  ok";
                sb.AppendLine(glyph + " " + group.Name + (critical ? "   [RACE-CRITICAL]" : ""));
                sb.AppendLine("        entries        : " + group.entries.Count);
                sb.AppendLine("        includeInBuild : " + schema.IncludeInBuild);
                sb.AppendLine("        buildPath      : " + Show(build));
                sb.AppendLine("        loadPath       : " + Show(load));
                sb.AppendLine("        provider       : " + Show(provider));
            }

            sb.AppendLine();
            sb.AppendLine("--- summary ---");
            sb.AppendLine("bundled groups            : " + bundled);
            sb.AppendLine("with an empty build/load  : " + emptyPaths);
            sb.AppendLine("with no provider type     : " + emptyProvider);
            sb.AppendLine("excluded from the build   : " + notIncluded);
            sb.AppendLine("RACE-CRITICAL groups bad  : " + criticalBroken + " of " + RaceCritical.Length);
            sb.AppendLine();

            if (criticalBroken > 0)
            {
                sb.AppendLine("VERDICT: a content build would NOT give the race what it needs. Open");
                sb.AppendLine("Window > Asset Management > Addressables > Groups and set each broken");
                sb.AppendLine("group's Build & Load Paths to Local, BEFORE build day.");
                Debug.LogError(sb.ToString());
            }
            else if (emptyPaths + emptyProvider + notIncluded > 0)
            {
                sb.AppendLine("VERDICT: every race-critical group resolves. The remaining gaps are in");
                sb.AppendLine("groups the race does not load, so they cannot stop it starting.");
                Debug.LogWarning(sb.ToString());
            }
            else
            {
                sb.AppendLine("VERDICT: all groups resolve. Unity re-seeded the empty on-disk values on");
                sb.AppendLine("load, so the preflight WARN is benign -- the build will find its content.");
                Debug.Log(sb.ToString());
            }
        }

        private static string Show(string value)
        {
            return string.IsNullOrEmpty(value) ? "<EMPTY>" : value;
        }
    }
}
