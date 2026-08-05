namespace SummaRace.Tests.EditMode
{
    using System.Reflection;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Position must carry no information about which option is correct.
    ///
    /// The audit found two separate versions of this defect: the race laid the correct card out in
    /// a fixed 1,0,2,1,0 lane pattern for every story and every run, and the Reader inherited the
    /// researcher's own content bias — the correct option is authored at index 1 in 96 of the 150
    /// questions (48 at index 0, 6 at index 2). Both let a learner score without reading, and both
    /// feed logged research data.
    ///
    /// The content bias is deliberately NOT asserted away here: the researcher's JSON is theirs and
    /// must stay untouched. What is asserted is that the DISPLAY re-randomises it every question,
    /// so the authored position never reaches the screen.
    ///
    /// NEEDS A SEAM (not testable here): the race's lane assignment is Fisher-Yates written inline
    /// inside <c>EndlessRaceDirector.PlaceGate</c> and <c>RaceController.BuildCheckpoint</c>, both
    /// of which need a live TrackManager/scene. Extracting it to a pure static helper — e.g.
    /// <c>SummaRace.Data.OptionShuffle.Shuffle(string[] texts, bool[] correct)</c> called by both —
    /// would let the same uniformity check below cover the race path too.
    /// </summary>
    public class AnswerPositionTests
    {
        private const int Draws = 30000;
        private const int Slots = 3;

        /// <summary>+/- 3% of the expected cell count. n = 30000 makes one standard deviation
        /// about 82 draws, so this is a ~3.7 sigma band: wide enough never to flake, narrow
        /// enough that a removed or lopsided shuffle (30000 / 0 / 0) fails on sight.</summary>
        private const int Tolerance = 300;   // 3% of 30000/3

        [Test]
        public void TheReaderStillShufflesItsOptionsIntoOnScreenSlots()
        {
            var readerType = typeof(SummaRace.Features.Reader.ReaderController);

            var shuffle = readerType.GetMethod("ShuffleDisplayOrder",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(shuffle,
                "ReaderController.ShuffleDisplayOrder is gone. Without it the Reader shows the "
                + "researcher's authored option order, and the correct answer is at index 1 in 96 of 150 "
                + "questions — a learner can score by position alone.");

            var order = readerType.GetField("_displayOrder", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(order, "ReaderController._displayOrder is gone; the slot-to-option mapping no longer exists.");
        }

        [Test]
        public void TheReaderPutsEveryOptionInEveryOnScreenSlotAboutEquallyOften()
        {
            var readerType = typeof(SummaRace.Features.Reader.ReaderController);
            var shuffle = readerType.GetMethod("ShuffleDisplayOrder", BindingFlags.Instance | BindingFlags.NonPublic);
            var orderField = readerType.GetField("_displayOrder", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(shuffle);
            Assert.NotNull(orderField);

            // A hidden, never-saved object: edit mode does not run Awake/Start, so the component is
            // inert and only the pure method below ever executes on it.
            var probe = EditorUtility.CreateGameObjectWithHideFlags(
                "~SummaRaceReaderShuffleProbe", HideFlags.HideAndDontSave, readerType);

            var counts = new int[Slots, Slots];   // [option index, on-screen slot]
            var arguments = new object[] { Slots };
            var randomState = Random.state;

            try
            {
                var reader = probe.GetComponent(readerType);
                Assert.NotNull(reader);

                Random.InitState(20260805);       // deterministic: the suite must not flake

                for (int draw = 0; draw < Draws; draw++)
                {
                    shuffle.Invoke(reader, arguments);
                    var displayOrder = (int[])orderField.GetValue(reader);

                    Assert.AreEqual(Slots, displayOrder.Length);

                    int seen = 0;
                    for (int slot = 0; slot < Slots; slot++)
                    {
                        int option = displayOrder[slot];
                        Assert.IsTrue(option >= 0 && option < Slots,
                            "Slot " + slot + " maps to option " + option + ", which does not exist.");
                        seen |= 1 << option;
                        counts[option, slot]++;
                    }

                    Assert.AreEqual(0x7, seen,
                        "The display order is not a permutation — an option was shown twice or dropped, "
                        + "so a learner could see the same answer in two slots.");
                }
            }
            finally
            {
                Random.state = randomState;
                Object.DestroyImmediate(probe);
            }

            int expected = Draws / Slots;
            for (int option = 0; option < Slots; option++)
            {
                for (int slot = 0; slot < Slots; slot++)
                {
                    int actual = counts[option, slot];
                    Assert.AreEqual(expected, actual, Tolerance,
                        "Authored option " + option + " landed in on-screen slot " + slot + " " + actual
                        + " times out of " + Draws + " (expected about " + expected
                        + "). The Reader's option positions carry information about the answer.");
                }
            }
        }

        [Test]
        public void EveryAuthoredCorrectIndexIsInsideItsOwnOptionList()
        {
            // The shuffle maps a tapped slot back through _displayOrder to the authored index, so
            // an out-of-range correctIndex would silently mark a right answer wrong in the log.
            var failures = new System.Collections.Generic.List<string>();

            var ids = TestContent.Ids;
            var stories = TestContent.Stories;
            for (int i = 0; i < ids.Length; i++)
            {
                var story = stories[i];
                if (story == null || story.pages == null) continue;

                for (int p = 0; p < story.pages.Length; p++)
                {
                    var question = story.pages[p] == null ? null : story.pages[p].question;
                    if (question == null || question.options == null) continue;
                    if (question.correctIndex < 0 || question.correctIndex >= question.options.Length)
                        failures.Add(ids[i] + " page " + (p + 1) + ": correctIndex " + question.correctIndex
                                     + " with " + question.options.Length + " options");
                }
            }

            StoryContentTests.AssertNoFailures(failures, "questions whose correct answer is unreachable");
        }
    }
}
