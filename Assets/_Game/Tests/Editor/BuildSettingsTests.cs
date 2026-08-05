namespace SummaRace.Tests.EditMode
{
    using System.Collections.Generic;
    using System.IO;
    using NUnit.Framework;
    using SummaRace.Constants;
    using UnityEditor;

    /// <summary>
    /// Build Settings against <see cref="SceneNames"/>.
    ///
    /// A scene the code can name but the build list does not contain is a guaranteed runtime dead
    /// end: <c>SceneLoader.Go</c> / <c>SceneManager.LoadScene</c> simply fail, the learner is left
    /// on a screen whose button "does nothing", and nothing about it is visible in the editor until
    /// someone walks that exact path. This project has been bitten twice — the Endless Runner
    /// import dropped every SummaRace scene in favour of the sample's own list (Arrange first,
    /// which blocked FINISH -> Arrange), and Boot lost build index 0 to one of their scenes, which
    /// would have shipped an APK that launches into Trash Dash with no <c>[Core]</c> singletons.
    /// </summary>
    public class BuildSettingsTests
    {
        [Test]
        public void BootIsBuildIndexZeroAndEnabled()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.Greater(scenes.Length, 0, "Build Settings is empty — no APK could start.");

            string first = Path.GetFileNameWithoutExtension(scenes[0].path);
            Assert.AreEqual(SceneNames.Boot, first,
                "Build index 0 is '" + first + "'. Only Boot creates the [Core] singletons "
                + "(GameManager/AudioManager/SaveManager/SceneLoader); anything else at index 0 launches a game "
                + "with no state, no save and no learner.");
            Assert.IsTrue(scenes[0].enabled, "Boot is in the build list but disabled.");
        }

        [Test]
        public void EverySceneTheCodeCanLoadIsInBuildSettingsAndEnabled()
        {
            var listed = new Dictionary<string, bool>();   // scene name -> enabled
            foreach (var scene in EditorBuildSettings.scenes)
            {
                string name = Path.GetFileNameWithoutExtension(scene.path);
                if (!listed.ContainsKey(name)) listed[name] = scene.enabled;
                else listed[name] = listed[name] || scene.enabled;
            }

            var failures = new List<string>();
            foreach (var constant in ResourceContractTests.StringConstantsOf(typeof(SceneNames)))
            {
                bool enabled;
                if (!listed.TryGetValue(constant.Value, out enabled))
                    failures.Add("SceneNames." + constant.Key + " = '" + constant.Value + "' is not in Build Settings");
                else if (!enabled)
                    failures.Add("SceneNames." + constant.Key + " = '" + constant.Value + "' is in Build Settings but disabled");
            }

            StoryContentTests.AssertNoFailures(failures, "scenes the code routes to that a build cannot load");
        }

        [Test]
        public void EveryBuildSettingsEntryPointsAtASceneThatExists()
        {
            var failures = new List<string>();

            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (string.IsNullOrEmpty(scene.path)) { failures.Add("an entry with no path"); continue; }
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path) == null)
                    failures.Add("'" + scene.path + "' has no scene asset (a moved or deleted scene still listed)");
            }

            StoryContentTests.AssertNoFailures(failures, "dangling Build Settings entries");
        }

        [Test]
        public void NoTwoBuildSettingsScenesShareAName()
        {
            // SceneManager.LoadScene(name) is ambiguous when two paths end in the same file name,
            // and every scene transition in the game is by name via SceneNames.
            var seen = new Dictionary<string, string>();
            var failures = new List<string>();

            foreach (var scene in EditorBuildSettings.scenes)
            {
                string name = Path.GetFileNameWithoutExtension(scene.path);
                string first;
                if (seen.TryGetValue(name, out first)) failures.Add("'" + name + "': " + first + " and " + scene.path);
                else seen[name] = scene.path;
            }

            StoryContentTests.AssertNoFailures(failures, "duplicate scene names in Build Settings");
        }

        [Test]
        public void BuildSettingsContainsOnlyScenesSummaRaceNames()
        {
            // Decontamination guard (P0). The Trash Dash sample's Start/Main/Shop scenes were in
            // this list once; Shop is still reachable from leftover buttons in MainSummaRace, and
            // the only thing stopping it loading is that it is NOT listed here. Anything in the
            // build list that SceneNames does not name is either dead weight in the APK or a route
            // out of the study instrument.
            var known = new HashSet<string>();
            foreach (var constant in ResourceContractTests.StringConstantsOf(typeof(SceneNames)))
                known.Add(constant.Value);

            var failures = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled) continue;
                string name = Path.GetFileNameWithoutExtension(scene.path);
                if (!known.Contains(name))
                    failures.Add("'" + scene.path + "' is in the build but no SceneNames constant names it");
            }

            StoryContentTests.AssertNoFailures(failures, "scenes in the build that the game never routes to");
        }
    }
}
