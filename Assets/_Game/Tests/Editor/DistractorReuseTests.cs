namespace SummaRace.Tests.EditMode
{
    using System.Collections.Generic;
    using NUnit.Framework;

    /// <summary>
    /// The support-removal ladder only works if the Race asks for RECALL. The Reader pre-teaches
    /// the same five SWBST slots with the text on screen; the Race asks them again with the text
    /// gone. If a gate's two distractors are the very strings the learner just saw rejected on
    /// that page, the gate can be cleared from memory of the Reader screen rather than from
    /// comprehension of the story — and <c>raceFirstPickCorrect</c> is the study's measure.
    ///
    /// F44 measured ~50% verbatim reuse and rewrote the distractors. Today it sits at 17.7% of
    /// distractors, 47 of the 53 hits being SOMEBODY cards: the plausible wrong characters in a
    /// five-page story really are the same two or three names, so zero is neither reachable nor
    /// desirable there. The non-character slots are held to a much tighter bound.
    /// </summary>
    public class DistractorReuseTests
    {
        /// <summary>Share of ALL distractors that may repeat a Reader wrong option verbatim.</summary>
        private const float MaxOverallReuseRate = 0.30f;

        /// <summary>Share of NON-SOMEBODY distractors that may. Measured today: 2.5%.</summary>
        private const float MaxNonCharacterReuseRate = 0.08f;

        private const string CharacterSlot = "SOMEBODY";

        [Test]
        public void DistractorsRarelyRepeatTheReadersOwnWrongOptionsVerbatim()
        {
            int total = 0;
            int reused = 0;
            var examples = new List<string>();

            foreach (var element in TestContent.Elements())
            {
                var question = element.ReaderQuestion();
                var readerWrong = WrongOptions(question);

                foreach (var distractor in element.Element.distractors)
                {
                    total++;
                    if (!readerWrong.Contains(Key(distractor))) continue;
                    reused++;
                    if (examples.Count < 8) examples.Add(element.Where + ": '" + distractor + "'");
                }
            }

            Assert.Greater(total, 0, "No distractors were loaded at all.");

            float rate = reused / (float)total;
            Assert.LessOrEqual(rate, MaxOverallReuseRate,
                reused + " of " + total + " race distractors (" + (rate * 100f).ToString("0.0")
                + "%) are verbatim copies of the wrong options the learner just saw in the Reader, so the race is "
                + "passable from memory of the Reader instead of from the story. Examples:\n  "
                + string.Join("\n  ", examples.ToArray()));
        }

        [Test]
        public void NonCharacterDistractorsAreWrittenFreshRatherThanCopiedFromTheReader()
        {
            int total = 0;
            int reused = 0;
            var examples = new List<string>();

            foreach (var element in TestContent.Elements())
            {
                if (element.Element.type == CharacterSlot) continue;   // names come from one small cast

                var readerWrong = WrongOptions(element.ReaderQuestion());
                foreach (var distractor in element.Element.distractors)
                {
                    total++;
                    if (!readerWrong.Contains(Key(distractor))) continue;
                    reused++;
                    if (examples.Count < 8) examples.Add(element.Where + ": '" + distractor + "'");
                }
            }

            Assert.Greater(total, 0, "No non-character distractors were loaded at all.");

            float rate = reused / (float)total;
            Assert.LessOrEqual(rate, MaxNonCharacterReuseRate,
                reused + " of " + total + " WANTED/BUT/SO/THEN distractors (" + (rate * 100f).ToString("0.0")
                + "%) are copied verbatim from the Reader's wrong options. Examples:\n  "
                + string.Join("\n  ", examples.ToArray()));
        }

        [Test]
        public void NoRaceDistractorIsAnAnswerTheReaderTaughtAsCORRECT()
        {
            // This one is absolute. A card that the Reader marked right and the Race marks wrong
            // punishes the learner for having learned, which the game must never do (GDD D7), and
            // it corrupts the measure in the direction that looks like a comprehension failure.
            var failures = new List<string>();

            foreach (var element in TestContent.Elements())
            {
                var taught = CorrectOptionsAnywhereInStory(element.Story);
                foreach (var distractor in element.Element.distractors)
                    if (taught.Contains(Key(distractor)))
                        failures.Add(element.Where + ": distractor '" + distractor
                                     + "' is a correct answer the Reader taught in this same story");
            }

            StoryContentTests.AssertNoFailures(failures, "race distractors that contradict the Reader");
        }

        // ------------------------------------------------------------------------------------

        private static HashSet<string> WrongOptions(SummaRace.Data.QuestionData question)
        {
            var wrong = new HashSet<string>();
            if (question == null || question.options == null) return wrong;

            for (int i = 0; i < question.options.Length; i++)
            {
                if (i == question.correctIndex) continue;
                if (string.IsNullOrEmpty(question.options[i])) continue;
                wrong.Add(Key(question.options[i]));
            }
            return wrong;
        }

        private static HashSet<string> CorrectOptionsAnywhereInStory(SummaRace.Data.StoryData story)
        {
            var correct = new HashSet<string>();
            if (story == null || story.pages == null) return correct;

            foreach (var page in story.pages)
            {
                var question = page == null ? null : page.question;
                if (question == null || question.options == null) continue;
                if (question.correctIndex < 0 || question.correctIndex >= question.options.Length) continue;

                string option = question.options[question.correctIndex];
                if (!string.IsNullOrEmpty(option)) correct.Add(Key(option));
            }
            return correct;
        }

        private static string Key(string value) => value.Trim().ToLowerInvariant();
    }
}
