namespace SummaRace.Tests.EditMode
{
    using System.Reflection;
    using NUnit.Framework;
    using SummaRace.Constants;
    using SummaRace.Data;
    using UnityEditor;
    using UnityEngine;
    using Core = SummaRace.Core;   // Trash Dash ships its own global `GameManager` (CLAUDE.md gotcha 4)

    /// <summary>
    /// Stars, saved progress and the unlock chain — the rules that decide what a learner sees next
    /// and what the researcher reads afterwards.
    ///
    /// Edit mode never runs Awake/OnEnable, so <c>GameManager.Instance</c> is null and the
    /// singletons do not exist (CLAUDE.md gotcha 8 — believing otherwise already produced one
    /// wrong result in P3a). This fixture therefore stands a GameManager up by hand on a hidden,
    /// never-saved object, borrows the private setters the Bootstrapper would normally drive, and
    /// puts <c>Instance</c> back exactly as it found it. <c>SaveManager.Instance</c> stays null
    /// throughout, which is what keeps <c>CompleteStory</c> off the disk: nothing here writes to
    /// persistentDataPath.
    /// </summary>
    public class ProgressionRuleTests
    {
        private GameObject _host;
        private Core.GameManager _manager;
        private Core.GameManager _previousInstance;

        [SetUp]
        public void SetUp()
        {
            Assert.IsNull(Core.SaveManager.Instance,
                "A live SaveManager would make these tests write real save files. Leave play mode and re-run.");

            _previousInstance = Core.GameManager.Instance;
            _host = EditorUtility.CreateGameObjectWithHideFlags(
                "~SummaRaceGameManagerProbe", HideFlags.HideAndDontSave, typeof(Core.GameManager));
            _manager = _host.GetComponent<Core.GameManager>();
            SetInstance(_manager);
        }

        [TearDown]
        public void TearDown()
        {
            SetInstance(_previousInstance);
            if (_host != null) Object.DestroyImmediate(_host);
            _host = null;
            _manager = null;
        }

        // --- stars ---------------------------------------------------------------------------

        [Test]
        public void StarThresholdsFollowTheGddLadder()
        {
            Assert.AreEqual(5, GameRules.StarsThreeMin, "3 stars is a perfect run of first picks (GDD S4.2).");
            // 3, not the GDD's 4 -- owner decision D4. At 4 the 2-star gate sat one step below a
            // perfect run, which collapsed 3/5 and 0/5 onto the same one-star screen.
            Assert.AreEqual(3, GameRules.StarsTwoMin, "2 stars is 3 or 4 of 5 first picks (owner decision D4).");
            Assert.Greater(GameRules.StarsThreeMin, GameRules.StarsTwoMin, "The ladder must go up.");
            Assert.AreEqual(TestContent.ElementsPerStory, GameRules.StarsThreeMin,
                "3 stars must mean every SWBST element was right first time — if the threshold and the number of "
                + "elements ever disagree, 3 stars becomes either unreachable or free.");
        }

        [Test]
        public void StarsCountFirstPicksAndAreNeverZero()
        {
            for (int correct = 0; correct <= TestContent.ElementsPerStory; correct++)
            {
                var result = new Core.RaceResult();
                for (int i = 0; i < correct; i++) result.firstPickCorrect[i] = true;
                _manager.SetRaceResult(result);

                int expected = correct >= GameRules.StarsThreeMin ? 3
                             : correct >= GameRules.StarsTwoMin ? 2
                             : 1;

                Assert.AreEqual(expected, _manager.CalculateStars(),
                    correct + " of 5 first picks correct should award " + expected + " star(s).");
            }
        }

        [Test]
        public void AMissingRaceResultStillAwardsOneStar()
        {
            // Never punish the learner (GDD D7): a run that lost its result must not read as zero.
            _manager.SetRaceResult(null);
            Assert.AreEqual(1, _manager.CalculateStars());
        }

        // --- saved progress ------------------------------------------------------------------

        [Test]
        public void BestStarsNeverDecreases()
        {
            var learner = NewLearner();
            SetInstanceProperty(_manager, "CurrentLearner", learner);

            string storyId = StoryIds.For(1, "easy");
            var story = StoryLoader.Load(storyId);
            Assert.IsNotNull(story, "s01_easy must load — every other fixture leans on it too.");
            SetInstanceProperty(_manager, "CurrentStory", story);

            _manager.CompleteStory(3);
            Assert.AreEqual(3, _manager.GetBestStars(storyId));
            Assert.IsTrue(_manager.IsCompleted(storyId), "A finished story must record as completed.");

            _manager.CompleteStory(1);
            Assert.AreEqual(3, _manager.GetBestStars(storyId),
                "A weaker replay overwrote the learner's best result. bestStars is a study measure and the "
                + "earlier run cannot be repeated.");

            _manager.CompleteStory(2);
            Assert.AreEqual(3, _manager.GetBestStars(storyId));
        }

        [Test]
        public void AnUnplayedStoryHasNoStarsAndIsNotComplete()
        {
            SetInstanceProperty(_manager, "CurrentLearner", NewLearner());

            string storyId = StoryIds.For(7, "hard");
            Assert.AreEqual(0, _manager.GetBestStars(storyId));
            Assert.IsFalse(_manager.IsCompleted(storyId));
            Assert.IsFalse(_manager.IsSessionComplete(7));
        }

        [Test]
        public void ASessionIsCompleteOnlyWhenAllThreeDifficultiesAre()
        {
            var learner = NewLearner();
            SetInstanceProperty(_manager, "CurrentLearner", learner);

            const int session = 3;
            for (int d = 0; d < StoryIds.Difficulties.Length; d++)
            {
                Assert.IsFalse(_manager.IsSessionComplete(session),
                    "Session " + session + " reported complete after " + d + " of 3 stories.");
                MarkCompleted(learner, StoryIds.For(session, d), 1);
            }

            Assert.IsTrue(_manager.IsSessionComplete(session));
        }

        [Test]
        public void SelectedSessionIsClampedToTheTenSessions()
        {
            _manager.SelectedSession = 0;
            Assert.AreEqual(1, _manager.SelectedSession, "Session 0 has no stories; StorySelect would build empty cards.");

            _manager.SelectedSession = -4;
            Assert.AreEqual(1, _manager.SelectedSession);

            _manager.SelectedSession = GameRules.SessionCount + 7;
            Assert.AreEqual(GameRules.SessionCount, _manager.SelectedSession);

            _manager.SelectedSession = 6;
            Assert.AreEqual(6, _manager.SelectedSession);
        }

        // --- unlock chain --------------------------------------------------------------------

        [Test]
        public void EasyIsAlwaysOpenAndEachDifficultyWaitsOnTheOneBefore()
        {
            var learner = NewLearner();
            SetInstanceProperty(_manager, "CurrentLearner", learner);

            const int session = 2;

            CollectionAssert.AreEqual(new[] { true, false, false }, UnlockChain(session),
                "With nothing finished, only EASY may be playable (GDD S3.1).");

            MarkCompleted(learner, StoryIds.For(session, 0), 2);
            CollectionAssert.AreEqual(new[] { true, true, false }, UnlockChain(session),
                "Finishing EASY must open AVERAGE and only AVERAGE.");

            MarkCompleted(learner, StoryIds.For(session, 1), 2);
            CollectionAssert.AreEqual(new[] { true, true, true }, UnlockChain(session),
                "Finishing AVERAGE must open HARD.");
        }

        [Test]
        public void StartingAStoryWithoutFinishingItUnlocksNothing()
        {
            var learner = NewLearner();
            SetInstanceProperty(_manager, "CurrentLearner", learner);

            // completed:false with stars on the record — an abandoned run, which must not advance
            // the ladder or a learner could skip straight to HARD by quitting twice.
            learner.progress.Add(new StoryProgress { storyId = StoryIds.For(4, 0), bestStars = 3, completed = false });

            CollectionAssert.AreEqual(new[] { true, false, false }, UnlockChain(4));
        }

        [Test]
        public void WithNoLearnerProfileEveryDifficultyStaysReachable()
        {
            // Playing a scene directly in the editor never creates a profile (TDD S13). If this
            // inverted, every story past EASY would be locked for anyone testing content.
            SetInstanceProperty(_manager, "CurrentLearner", null);
            CollectionAssert.AreEqual(new[] { true, true, true }, UnlockChain(1));
        }

        [Test]
        public void TheTeacherUnlockIsClampedToTheTenSessions()
        {
            var learner = NewLearner();
            SetInstanceProperty(_manager, "CurrentLearner", learner);

            learner.unlockedSession = 0;
            Assert.AreEqual(1, UnlockedSession(), "Session 1 must always be open or the study cannot start.");

            learner.unlockedSession = GameRules.SessionCount + 12;
            Assert.AreEqual(GameRules.SessionCount, UnlockedSession(),
                "A corrupted save must not offer a session that has no stories.");

            learner.unlockedSession = 4;
            Assert.AreEqual(4, UnlockedSession());

            SetInstanceProperty(_manager, "CurrentLearner", null);
            Assert.AreEqual(GameRules.SessionCount, UnlockedSession(),
                "With no profile every stop stays reachable so all 30 stories can be tested.");
        }

        // ------------------------------------------------------------------------------------

        /// <summary>
        /// The chain StorySelect builds in Start(): EASY is always open, and each later difficulty
        /// waits on the one before it. The per-story test is the controller's own
        /// <c>IsCleared</c>, invoked here rather than reimplemented.
        /// </summary>
        private static bool[] UnlockChain(int session)
        {
            var isCleared = typeof(SummaRace.Features.StorySelect.StorySelectController)
                .GetMethod("IsCleared", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(isCleared, "StorySelectController.IsCleared is gone; the unlock rule moved or vanished.");

            var playable = new bool[StoryIds.Difficulties.Length];
            bool unlocked = true;

            for (int d = 0; d < playable.Length; d++)
            {
                playable[d] = unlocked;
                unlocked = unlocked && (bool)isCleared.Invoke(null, new object[] { StoryIds.For(session, d) });
            }

            return playable;
        }

        private static int UnlockedSession()
        {
            var method = typeof(SummaRace.Features.SessionMap.SessionMapController)
                .GetMethod("UnlockedSession", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "SessionMapController.UnlockedSession is gone; session gating is the study's "
                                     + "internal-validity control (GDD S8.3).");
            return (int)method.Invoke(null, null);
        }

        private static LearnerProfile NewLearner() => new LearnerProfile
        {
            id = "test-learner",
            displayName = "Test",
            named = true,
        };

        private static void MarkCompleted(LearnerProfile learner, string storyId, int stars)
        {
            learner.progress.Add(new StoryProgress { storyId = storyId, bestStars = stars, completed = true });
        }

        private static void SetInstance(Core.GameManager value)
        {
            var property = typeof(Core.GameManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(property, "GameManager.Instance is gone.");
            var setter = property.GetSetMethod(true);
            Assert.IsNotNull(setter, "GameManager.Instance has no setter to borrow.");
            setter.Invoke(null, new object[] { value });
        }

        private static void SetInstanceProperty(object target, string name, object value)
        {
            var property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(property, "GameManager." + name + " is gone.");
            var setter = property.GetSetMethod(true);
            Assert.IsNotNull(setter, "GameManager." + name + " has no setter to borrow.");
            setter.Invoke(target, new object[] { value });
        }
    }
}
