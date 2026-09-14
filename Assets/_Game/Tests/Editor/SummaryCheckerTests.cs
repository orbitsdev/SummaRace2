namespace SummaRace.Tests.EditMode
{
    using NUnit.Framework;
    using SummaRace.Data;

    /// <summary>
    /// Locks the offline summary gate (client feedback 2026-09-14) against the real content.
    /// The thresholds were tuned on all 30 stories in a prototype first; these tests are what keeps
    /// a later tweak to the word lists or GameRules from quietly turning the gate into a wall
    /// (good summaries refused — a child stuck on the last screen) or a sieve (random text accepted).
    /// </summary>
    public class SummaryCheckerTests
    {
        private static string ModelSummary(StoryData s)
        {
            var e = s.elements;
            return e[0].correct + " wanted " + e[1].correct.ToLowerInvariant() + " but " +
                   e[2].correct.ToLowerInvariant() + " so " + e[3].correct.ToLowerInvariant() +
                   " then " + e[4].correct.ToLowerInvariant() + ".";
        }

        private static StoryData Story(string id)
        {
            var s = StoryLoader.Load(id);
            Assert.IsNotNull(s, id + " failed to load");
            return s;
        }

        [Test]
        public void EveryStoryAcceptsASummaryBuiltFromItsOwnParts()
        {
            var failures = new System.Collections.Generic.List<string>();
            for (int i = 0; i < TestContent.Stories.Length; i++)
            {
                var s = TestContent.Stories[i];
                if (s == null) continue;
                var r = SummaryChecker.Check(ModelSummary(s), s);
                if (r.verdict != SummaryChecker.Verdict.Ok)
                    failures.Add(TestContent.Ids[i] + ": " + r.verdict);
            }
            Assert.IsEmpty(failures, "A plain S-W-B-S-T summary of the story was refused — a " +
                "learner doing exactly what the screen asks would be stuck:\n" + string.Join("\n", failures));
        }

        [Test]
        public void ASummaryOfOneStoryIsAlmostNeverAcceptedForAnother()
        {
            var stories = TestContent.Stories;
            int accepted = 0, pairs = 0;
            for (int a = 0; a < stories.Length; a++)
                for (int b = 0; b < stories.Length; b++)
                {
                    if (a == b || stories[a] == null || stories[b] == null) continue;
                    pairs++;
                    if (SummaryChecker.Check(ModelSummary(stories[a]), stories[b]).verdict == SummaryChecker.Verdict.Ok)
                        accepted++;
                }
            // Prototype measured 1 of 870. A handful is tolerable; dozens means the gate stopped
            // checking the story at all.
            Assert.LessOrEqual(accepted, 5, accepted + " of " + pairs + " cross-story summaries accepted.");
        }

        [TestCase("asdf jkl qwerty zxcv bnm poiu lkjh mnbv", SummaryChecker.Verdict.NotEnglish)]
        [TestCase("si mateo ganahan og iring pero ang iring tigulang ug masakiton mao nga", SummaryChecker.Verdict.NotEnglish)]
        [TestCase("Mateo Mateo Mateo Mateo Mateo Mateo Mateo Mateo", SummaryChecker.Verdict.TooShort)]
        [TestCase("Mateo wanted a cat", SummaryChecker.Verdict.TooShort)]
        [TestCase("I like this story because it is very nice and fun to read", SummaryChecker.Verdict.MissingSomebody)]
        [TestCase("Mateo went to the store with his dad and bought some food for dinner", SummaryChecker.Verdict.MissingParts)]
        public void RefusesRandomOrOffTopicText(string text, SummaryChecker.Verdict expected)
        {
            Assert.AreEqual(expected, SummaryChecker.Check(text, Story("s02_average")).verdict);
        }

        [TestCase("s02_average", "mateo want a pet but santiago is old and sick so he think carefuly then he take the cat home")]
        [TestCase("s02_average", "Mateo wants a cat. The cat was old and sick. He thought about what the cat needs. He chose the old cat.")]
        [TestCase("s01_easy", "molly want to play swing but bella dont want to go down so molly talk to bella how she feel then bella let her swing")]
        [TestCase("s07_easy", "hector is new in school he want friends but nobody know him so he play his ball tricks and the kids like him and become friends")]
        [TestCase("s09_hard", "marusia want to escape from the witch house but the gate was locked so she help find the flower and then they go home safe")]
        [TestCase("s10_average", "clara want to care the wounded soldiers in the civil war so she nurse them and she make the red cross")]
        public void AcceptsChildLikeSummariesWithImperfectGrammar(string id, string text)
        {
            var r = SummaryChecker.Check(text, Story(id));
            Assert.AreEqual(SummaryChecker.Verdict.Ok, r.verdict, "missing parts: " + string.Join(",", r.missingParts));
        }

        [Test]
        public void BrokenContentNeverTrapsTheLearner()
        {
            Assert.AreEqual(SummaryChecker.Verdict.Ok,
                SummaryChecker.Check("this is a long enough sentence about nothing much at all", null).verdict);
        }
    }
}
