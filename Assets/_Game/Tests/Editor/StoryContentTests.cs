namespace SummaRace.Tests.EditMode
{
    using System.Collections.Generic;
    using System.Text;
    using NUnit.Framework;
    using SummaRace.Constants;
    using SummaRace.Data;
    using SummaRace.Features.Race;
    using UnityEngine;

    /// <summary>
    /// Content integrity for all 30 stories, checked through the real runtime loader.
    ///
    /// The 30 JSONs are GENERATED artifacts (Tools/StoryPipeline). A re-run that drops a field,
    /// renames an element type or loses a page is invisible until a learner reaches that story —
    /// and 27 of the 30 have never been played through the loop. This fixture is the net.
    /// </summary>
    public class StoryContentTests
    {
        [Test]
        public void ThirtyStoryIdsAreDerivedFromStoryIdsAndAreAllDistinct()
        {
            Assert.AreEqual(10, GameRules.SessionCount, "GDD S3.1 fixes the study at 10 sessions.");
            Assert.AreEqual(3, StoryIds.Difficulties.Length, "GDD S3.1 fixes 3 difficulties per session.");
            Assert.AreEqual(30, TestContent.ExpectedStoryCount,
                "10 sessions x 3 difficulties must be the 30 stories the study runs on.");

            var ids = TestContent.Ids;
            Assert.AreEqual(30, ids.Length);
            var seen = new HashSet<string>(ids);
            Assert.AreEqual(30, seen.Count, "Two sessions resolved to the same story id.");

            foreach (var id in ids)
                StringAssert.IsMatch("^s[0-9][0-9]_(easy|average|hard)$", id,
                    "StoryIds broke the sNN_<difficulty> convention: " + id);
        }

        [Test]
        public void EveryStoryLoadsThroughStoryLoaderWithNoValidationFailure()
        {
            var failures = new List<string>();
            var ids = TestContent.Ids;
            var stories = TestContent.Stories;

            for (int i = 0; i < ids.Length; i++)
            {
                if (stories[i] == null) { failures.Add(ids[i] + ": StoryLoader.Load returned null"); continue; }
                if (!StoryLoader.Validate(stories[i])) failures.Add(ids[i] + ": StoryLoader.Validate rejected it");
            }

            AssertNoFailures(failures, "stories that do not load");
        }

        [Test]
        public void ResourcesStoriesFolderHoldsExactlyTheThirtyExpectedFiles()
        {
            var assets = Resources.LoadAll<TextAsset>("Stories");
            var found = new HashSet<string>();
            foreach (var asset in assets) found.Add(asset.name);

            var expected = new HashSet<string>(TestContent.Ids);

            var extra = new List<string>(found);
            extra.RemoveAll(expected.Contains);
            var missing = new List<string>(expected);
            missing.RemoveAll(found.Contains);

            Assert.AreEqual(0, missing.Count, "Story JSONs the game asks for but cannot find: " + string.Join(", ", missing));
            Assert.AreEqual(0, extra.Count,
                "Story JSONs nothing routes to (dead content ships in the APK and confuses a re-run of the pipeline): "
                + string.Join(", ", extra));
            Assert.AreEqual(TestContent.ExpectedStoryCount, found.Count);
        }

        [Test]
        public void EveryStoryDeclaresTheSessionAndDifficultyItsIdPromises()
        {
            var failures = new List<string>();
            var ids = TestContent.Ids;
            var stories = TestContent.Stories;

            for (int i = 0; i < ids.Length; i++)
            {
                var story = stories[i];
                if (story == null) continue;
                if (story.id != ids[i]) failures.Add(ids[i] + ": declares id '" + story.id + "'");
                if (story.session != TestContent.Sessions[i])
                    failures.Add(ids[i] + ": declares session " + story.session + ", file says " + TestContent.Sessions[i]);
                if (story.difficulty != TestContent.DifficultyOf[i])
                    failures.Add(ids[i] + ": declares difficulty '" + story.difficulty + "'");
                if (string.IsNullOrEmpty(story.title)) failures.Add(ids[i] + ": no title (StorySelect card would be blank)");
            }

            AssertNoFailures(failures, "stories whose own fields contradict their id");
        }

        [Test]
        public void EveryStoryHasFivePages()
        {
            var failures = new List<string>();
            var ids = TestContent.Ids;
            var stories = TestContent.Stories;

            for (int i = 0; i < ids.Length; i++)
            {
                var story = stories[i];
                if (story == null) continue;
                if (story.pages == null || story.pages.Length != TestContent.PagesPerStory)
                {
                    failures.Add(ids[i] + ": " + (story.pages == null ? "no pages" : story.pages.Length + " pages"));
                    continue;
                }
                for (int p = 0; p < story.pages.Length; p++)
                    if (story.pages[p] == null || string.IsNullOrEmpty(story.pages[p].text))
                        failures.Add(ids[i] + " page " + (p + 1) + ": empty page text");
            }

            AssertNoFailures(failures, "stories without 5 readable pages");
        }

        [Test]
        public void EveryStoryHasFiveElementsInSwbstOrder()
        {
            var failures = new List<string>();
            var ids = TestContent.Ids;
            var stories = TestContent.Stories;

            for (int i = 0; i < ids.Length; i++)
            {
                var story = stories[i];
                if (story == null) continue;
                if (story.elements == null || story.elements.Length != TestContent.ElementsPerStory)
                {
                    failures.Add(ids[i] + ": " + (story.elements == null ? "no elements" : story.elements.Length + " elements"));
                    continue;
                }
                for (int e = 0; e < TestContent.SwbstOrder.Length; e++)
                {
                    var element = story.elements[e];
                    if (element == null) { failures.Add(ids[i] + " element " + e + ": null"); continue; }
                    if (element.type != TestContent.SwbstOrder[e])
                        failures.Add(ids[i] + " element " + e + ": type '" + element.type + "', expected '"
                                     + TestContent.SwbstOrder[e] + "'");
                }
            }

            AssertNoFailures(failures,
                "stories whose SWBST elements are missing or out of S-W-B-S-T order (Arrange scores against this order)");
        }

        [Test]
        public void EveryPageQuestionHasThreeDistinctOptionsAndAnInRangeCorrectIndex()
        {
            var failures = new List<string>();
            var ids = TestContent.Ids;
            var stories = TestContent.Stories;

            int questions = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                var story = stories[i];
                if (story == null || story.pages == null) continue;

                for (int p = 0; p < story.pages.Length; p++)
                {
                    var page = story.pages[p];
                    var question = page == null ? null : page.question;
                    string where = ids[i] + " page " + (p + 1);

                    if (question == null) { failures.Add(where + ": no question (the Reader pre-teaches one slot per page)"); continue; }
                    questions++;

                    if (string.IsNullOrEmpty(question.text)) failures.Add(where + ": empty question text");

                    if (question.options == null || question.options.Length != TestContent.OptionsPerQuestion)
                    {
                        failures.Add(where + ": " + (question.options == null ? "no options" : question.options.Length + " options"));
                        continue;
                    }

                    var distinct = new HashSet<string>();
                    for (int o = 0; o < question.options.Length; o++)
                    {
                        if (string.IsNullOrEmpty(question.options[o])) { failures.Add(where + ": option " + o + " is empty"); continue; }
                        if (!distinct.Add(question.options[o].Trim().ToLowerInvariant()))
                            failures.Add(where + ": duplicated option '" + question.options[o] + "' (two taps would both be right)");
                    }

                    if (question.correctIndex < 0 || question.correctIndex >= question.options.Length)
                        failures.Add(where + ": correctIndex " + question.correctIndex + " out of range");
                }
            }

            AssertNoFailures(failures, "reader questions that cannot be answered");
            Assert.AreEqual(TestContent.ExpectedStoryCount * TestContent.PagesPerStory, questions,
                "Every one of the 30 stories must carry a question on all 5 pages — the reading measure is per page.");
        }

        [Test]
        public void EveryElementHasTwoDistractorsDistinctFromEachOtherAndFromTheCorrectAnswer()
        {
            var failures = new List<string>();

            foreach (var element in TestContent.Elements())
            {
                if (element.Element.distractors.Length != TestContent.DistractorsPerElement)
                {
                    failures.Add(element.Where + ": " + element.Element.distractors.Length + " distractors");
                    continue;
                }

                var cards = element.Cards();
                var distinct = new HashSet<string>();
                foreach (var card in cards)
                {
                    if (string.IsNullOrEmpty(card)) { failures.Add(element.Where + ": an empty race card"); continue; }
                    if (!distinct.Add(card.Trim().ToLowerInvariant()))
                        failures.Add(element.Where + ": two identical race cards ('" + card + "')");
                }
            }

            AssertNoFailures(failures, "race gates with a broken set of three cards");
        }

        [Test]
        public void EveryStoryCarriesAMissionConfigTheRaceCanRun()
        {
            var failures = new List<string>();
            var ids = TestContent.Ids;
            var stories = TestContent.Stories;

            for (int i = 0; i < ids.Length; i++)
            {
                var story = stories[i];
                if (story == null) continue;
                var mission = story.mission;
                if (mission == null) { failures.Add(ids[i] + ": no mission block"); continue; }

                if (mission.checkpointSpacing <= 0f)
                    failures.Add(ids[i] + ": checkpointSpacing " + mission.checkpointSpacing + " (gates would stack on the start line)");
                if (mission.playerSpeed <= 0f)
                    failures.Add(ids[i] + ": playerSpeed " + mission.playerSpeed);
                if (mission.dangerPerSecond < 0f)
                    failures.Add(ids[i] + ": dangerPerSecond " + mission.dangerPerSecond + " (danger must never run backwards)");
                if (mission.startingDanger < 0f || mission.startingDanger > GameRules.DangerMax)
                    failures.Add(ids[i] + ": startingDanger " + mission.startingDanger + " outside 0.." + GameRules.DangerMax);
            }

            AssertNoFailures(failures, "stories the race cannot be configured from");
        }

        [Test]
        public void EveryStoryNamesAWorldRaceWorldsActuallyKnows()
        {
            // RaceWorlds.For falls back to "bright_park" for an unknown id, silently. A typo in a
            // world name therefore costs a session its look with nothing on screen to say so.
            var failures = new List<string>();
            var fallback = RaceWorlds.For("__definitely_not_a_world__");
            var ids = TestContent.Ids;
            var stories = TestContent.Stories;

            for (int i = 0; i < ids.Length; i++)
            {
                var story = stories[i];
                if (story == null) continue;

                if (string.IsNullOrEmpty(story.world)) { failures.Add(ids[i] + ": no world"); continue; }
                if (story.world == "bright_park") continue;   // legitimately the same recipe as the fallback

                if (SameRecipe(RaceWorlds.For(story.world), fallback))
                    failures.Add(ids[i] + ": world '" + story.world + "' is not in the RaceWorlds table (falls back to bright_park)");
            }

            AssertNoFailures(failures, "stories naming a world that does not exist");
        }

        [Test]
        public void TheTenWorldsAreOnePerSession()
        {
            var perSession = new Dictionary<int, string>();
            var failures = new List<string>();
            var ids = TestContent.Ids;
            var stories = TestContent.Stories;

            for (int i = 0; i < ids.Length; i++)
            {
                var story = stories[i];
                if (story == null || string.IsNullOrEmpty(story.world)) continue;
                int session = TestContent.Sessions[i];

                string already;
                if (!perSession.TryGetValue(session, out already)) perSession[session] = story.world;
                else if (already != story.world)
                    failures.Add("session " + session + ": " + ids[i] + " is '" + story.world + "', its siblings are '"
                                 + already + "' (a session must feel like one place)");
            }

            AssertNoFailures(failures, "sessions whose three stories disagree about the world");

            Assert.AreEqual(GameRules.SessionCount, perSession.Count, "Every session must name a world.");
            var distinct = new HashSet<string>(perSession.Values);
            Assert.AreEqual(GameRules.SessionCount, distinct.Count,
                "The ten sessions must use ten different worlds so the study reads as a journey; found "
                + distinct.Count + " distinct: " + string.Join(", ", new List<string>(distinct)));
        }

        // ------------------------------------------------------------------------------------

        private static bool SameRecipe(RaceWorlds.World a, RaceWorlds.World b)
        {
            return a.sun == b.sun
                   && Mathf.Approximately(a.sunIntensity, b.sunIntensity)
                   && a.ambient == b.ambient
                   && a.fog == b.fog
                   && Mathf.Approximately(a.fogStart, b.fogStart)
                   && Mathf.Approximately(a.fogEnd, b.fogEnd)
                   && a.sky == b.sky;
        }

        internal static void AssertNoFailures(List<string> failures, string what)
        {
            if (failures.Count == 0) return;

            var message = new StringBuilder();
            message.Append(failures.Count).Append(' ').Append(what).Append(':');
            foreach (var failure in failures) message.Append("\n  ").Append(failure);
            Assert.Fail(message.ToString());
        }
    }
}
