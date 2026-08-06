#if UNITY_ANDROID
namespace SummaRace.EditorTools
{
    using System.IO;
    using System.Xml;
    using UnityEditor.Android;
    using UnityEditor.Build;
    using UnityEngine;

    /// <summary>
    /// Turns Android's automatic cloud backup OFF in the generated manifest.
    ///
    /// WHY THIS EXISTS. `android:allowBackup` defaults to **true**, and Unity's generated
    /// manifest does not set it. On any tablet signed into a Google account, Android therefore
    /// copies the app's private data directory to Google Drive on its own schedule — which here
    /// means every learner profile (name, avatar, participant code, progress) and every
    /// `.jsonl` row of the study's process data.
    ///
    /// That is not a tidiness issue. GDD §11.4 names it directly — "Android auto-backup DISABLED
    /// in the manifest — learner data must never be copied to any cloud (privacy commitment in
    /// the thesis)" — and it is the same promise as the app being 100% offline: the consent given
    /// by forty families is that their child's data stays on the tablet and is erased afterwards.
    /// A backup would survive the post-study wipe entirely, on Google's servers, where nobody
    /// involved can delete it.
    ///
    /// It is invisible in every way the team can currently see. The Editor has no manifest, no
    /// APK has ever been built, and even on device the copy happens silently in the background.
    /// So it gets written into the build itself rather than onto a checklist.
    ///
    /// HOW. `IPostGenerateGradleAndroidProject` runs after Unity writes the Gradle project and
    /// before it is compiled, so the manifest exists and is still editable. The attribute is set
    /// on the **launcher** module's manifest, which is the application manifest the merger
    /// treats as authoritative — setting it only on `unityLibrary` invites a merge conflict with
    /// whatever a third-party library declares.
    ///
    /// FAILS THE BUILD if it cannot do its job. That is deliberate and is the whole point: an
    /// APK that silently ships with backup enabled is the bad outcome, and it would not be
    /// noticed until the data was already off the device. A failed build costs minutes and says
    /// exactly what to do by hand.
    /// </summary>
    public class AndroidBackupDisabler : IPostGenerateGradleAndroidProject
    {
        /// <summary>Late, so anything else that rewrites the manifest has already run.</summary>
        public int callbackOrder => 1000;

        private const string AndroidNs = "http://schemas.android.com/apk/res/android";

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // `path` is the unityLibrary module. The launcher module sits beside it and carries
            // the <application> tag that ends up in the APK.
            var root = Directory.GetParent(path);
            var launcher = root == null
                ? null
                : Path.Combine(root.FullName, "launcher", "src", "main", "AndroidManifest.xml");

            var manifest = launcher != null && File.Exists(launcher)
                ? launcher
                : Path.Combine(path, "src", "main", "AndroidManifest.xml");

            if (!File.Exists(manifest))
                throw new BuildFailedException(
                    "AndroidBackupDisabler: could not find a generated AndroidManifest.xml (looked " +
                    "for '" + manifest + "'). Android auto-backup would then stay ENABLED, which " +
                    "copies every learner profile and every study log row to Google Drive — the one " +
                    "thing the thesis promises will not happen (GDD 11.4). Either fix this script " +
                    "or add android:allowBackup=\"false\" to the manifest by hand before shipping.");

            try
            {
                var doc = new XmlDocument();
                doc.Load(manifest);

                var application = doc.SelectSingleNode("/manifest/application") as XmlElement;
                if (application == null)
                    throw new BuildFailedException(
                        "AndroidBackupDisabler: '" + manifest + "' has no <application> element, so " +
                        "auto-backup could not be disabled. See GDD 11.4 — learner data must never " +
                        "reach a cloud.");

                // false, not absent: absent means "use the platform default", which is true.
                application.SetAttribute("allowBackup", AndroidNs, "false");
                // Belt and braces for the older full-backup path; harmless where it is ignored.
                application.SetAttribute("fullBackupContent", AndroidNs, "false");

                doc.Save(manifest);

                Debug.Log("AndroidBackupDisabler: android:allowBackup=\"false\" written to " + manifest +
                          " — learner profiles and study logs will not be backed up to any cloud.");
            }
            catch (BuildFailedException)
            {
                throw;
            }
            catch (System.Exception e)
            {
                throw new BuildFailedException(
                    "AndroidBackupDisabler: failed to disable Android auto-backup in '" + manifest +
                    "'. The build is stopped on purpose — shipping with backup enabled would copy " +
                    "every learner profile and every study log to Google Drive, where it survives " +
                    "the post-study wipe and nobody involved can delete it (GDD 11.4). Add " +
                    "android:allowBackup=\"false\" to the <application> tag by hand if this script " +
                    "cannot be fixed quickly. Underlying error: " + e);
            }
        }
    }
}
#endif
