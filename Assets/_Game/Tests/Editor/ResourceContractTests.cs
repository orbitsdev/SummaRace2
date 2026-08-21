namespace SummaRace.Tests.EditMode
{
    using System.Collections.Generic;
    using System.Reflection;
    using NUnit.Framework;
    using SummaRace.Constants;
    using UnityEngine;

    /// <summary>
    /// Every path the game hands to <c>Resources.Load</c> must resolve to a real asset.
    ///
    /// Nothing on screen reports these: <c>AudioManager.PlayNarration</c> treats a missing clip as
    /// a silent page (by design — art and audio swaps are meant to be free), a missing hero image
    /// falls back to a title-only card, and a missing sprite leaves a white box. So the whole
    /// content set can rot one file at a time and the only symptom is a quieter, plainer game. All
    /// counts here are derived from the content, never typed, so adding a story extends the check
    /// automatically.
    /// </summary>
    public class ResourceContractTests
    {
        /// <summary>The sprites the code loads by string name rather than by serialized reference —
        /// the ones a rename cannot break at compile time. Kept in sync by hand with
        /// <c>SceneLoader</c>, <c>ReaderController</c>, <c>SummaryController</c> and
        /// <c>EndlessRaceDirector</c>.</summary>
        private static readonly string[] CodeLoadedUiSprites =
        {
            "UI/bg_splash",     // SceneLoader overlay + race mission briefing backdrop
            "UI/panel_gold",    // loading tip card + briefing card
            "UI/bar_bg",        // loading progress track, Reader VOICE pill, Summary pill
            "UI/bar_fill",      // loading progress fill, Reader VOICE pill (armed)
            "UI/mslumi_wave",   // Ms. Lumi presenting the mission
            "UI/chip_tan",      // Story Select's AVERAGE difficulty chip
        };

        [Test]
        public void EveryHeroImageResolvesToASprite()
        {
            var failures = new List<string>();
            var ids = TestContent.Ids;
            var stories = TestContent.Stories;
            int counted = 0;

            for (int i = 0; i < ids.Length; i++)
            {
                var story = stories[i];
                if (story == null) continue;

                if (string.IsNullOrEmpty(story.heroImage))
                {
                    failures.Add(ids[i] + ": no heroImage path");
                    continue;
                }

                counted++;
                // StorySelect loads it as a Sprite, so a PNG imported as a plain Texture resolves
                // to null here exactly as it would on the card.
                if (Resources.Load<Sprite>(story.heroImage) == null)
                    failures.Add(ids[i] + ": '" + story.heroImage + "' does not resolve to a Sprite");
            }

            StoryContentTests.AssertNoFailures(failures, "stories whose hero art will not appear on the StorySelect card");
            Assert.AreEqual(TestContent.ExpectedStoryCount, counted, "Every story must name a hero image.");
        }

        [Test]
        public void EveryNarrationPathResolvesToAnAudioClip()
        {
            var failures = new List<string>();
            var ids = TestContent.Ids;
            var stories = TestContent.Stories;
            int counted = 0;

            for (int i = 0; i < ids.Length; i++)
            {
                var story = stories[i];
                if (story == null || story.pages == null) continue;

                for (int p = 0; p < story.pages.Length; p++)
                {
                    var page = story.pages[p];
                    string path = page == null ? null : page.narration;
                    if (string.IsNullOrEmpty(path))
                    {
                        // A blank path is a silent page, which is indistinguishable at runtime from
                        // a broken one — and narration is the accessibility support the study leans
                        // on, so it is a failure here rather than something to skip.
                        failures.Add(ids[i] + " page " + (p + 1) + ": no narration path");
                        continue;
                    }

                    counted++;
                    if (Resources.Load<AudioClip>(path) == null)
                        failures.Add(ids[i] + " page " + (p + 1) + ": '" + path + "' does not resolve to an AudioClip");
                }
            }

            StoryContentTests.AssertNoFailures(failures, "pages that would be read in silence");
            Assert.AreEqual(TestContent.ExpectedStoryCount * TestContent.PagesPerStory, counted,
                "Every page of every story must carry narration.");
        }

        [Test]
        public void EveryAudioKeyResolvesToAClipInResourcesAudio()
        {
            var failures = new List<string>();
            var keys = StringConstantsOf(typeof(AudioKeys));

            Assert.Greater(keys.Count, 0, "AudioKeys declares no clip names at all.");

            foreach (var key in keys)
            {
                // AudioManager.PlaySfx/PlayMusic build exactly this path.
                if (Resources.Load<AudioClip>("Audio/" + key.Value) == null)
                    failures.Add("AudioKeys." + key.Key + " = '" + key.Value + "' has no clip at Resources/Audio/" + key.Value);
            }

            StoryContentTests.AssertNoFailures(failures, "audio keys the game will play as silence");
        }

        [Test]
        public void EveryUiSpriteLoadedByNameResolves()
        {
            var failures = new List<string>();

            foreach (var path in CodeLoadedUiSprites)
                if (Resources.Load<Sprite>(path) == null)
                    failures.Add("'" + path + "' does not resolve to a Sprite");

            StoryContentTests.AssertNoFailures(failures,
                "UI sprites loaded by string name (a rename here fails silently at runtime, not at compile time)");
        }

        // ------------------------------------------------------------------------------------

        /// <summary>Every <c>public const string</c> on a constants class, by field name.</summary>
        internal static List<KeyValuePair<string, string>> StringConstantsOf(System.Type type)
        {
            var found = new List<KeyValuePair<string, string>>();
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            foreach (var field in fields)
            {
                if (!field.IsLiteral || field.IsInitOnly) continue;
                if (field.FieldType != typeof(string)) continue;

                var value = field.GetRawConstantValue() as string;
                if (string.IsNullOrEmpty(value)) continue;
                found.Add(new KeyValuePair<string, string>(field.Name, value));
            }

            return found;
        }
    }
}
