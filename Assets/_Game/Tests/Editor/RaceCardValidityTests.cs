namespace SummaRace.Tests.EditMode
{
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using NUnit.Framework;

    /// <summary>
    /// THE MOST IMPORTANT FIXTURE IN THE SUITE. It guards internal validity, and the defect it
    /// guards against has already happened once.
    ///
    /// The race is the study's measure: <c>raceFirstPickCorrect</c> is both the star count and the
    /// logged outcome. Lane position is randomised, so the last thing that could tell a learner
    /// which of the three cards is correct without reading is the SHAPE OF THE CARD. In F44 it was
    /// measured that <c>correct</c> was the strictly widest of the three cards in 121 of 150 sets
    /// (mean +10.8 characters); "always tap the widest card" scored 84.7% of gates against 33% for
    /// guessing, and was perfect in 7 stories. The instrument could not separate comprehension
    /// from card-width perception at all.
    ///
    /// The pipeline's own gate (Tools/StoryPipeline/flag.py) reported "0 flagged" throughout,
    /// because it counted WORDS with a +/-5 tolerance and exempted s01 as GOLD — and s01 was the
    /// worst offender in the corpus, i.e. the session every learner plays first was the one never
    /// measured. The rules below are the repaired flag.py rules, re-based on characters with no
    /// exemptions, so the gate now lives in the build as well as in a Python script nobody has to
    /// remember to run.
    ///
    /// A failure here is never "loosen the threshold". It means an edit to
    /// Tools/StoryPipeline/overrides.json re-introduced a tell, and the distractors for that
    /// element need an editorial pass.
    /// </summary>
    public class RaceCardValidityTests
    {
        // --- flag.py, character-based, GOLD empty -----------------------------------------
        /// <summary>How much longer (characters) `correct` may be than its widest distractor.</summary>
        private const int LongestMarginChars = 6;
        /// <summary>How far `correct` may sit from the distractor mean.</summary>
        private const int MeanSpreadChars = 10;
        /// <summary>Race cards are 1.55 x 0.85 world units with autosize capped at 2.4 (F30). The
        /// only widths ever playtested are s01's, whose longest option is 53 characters.</summary>
        private const int MaxCardChars = 53;
        private const int MaxCardWords = 12;

        /// <summary>
        /// Ceiling on how well a learner can do by ignoring the words entirely and always taking
        /// the widest (or the narrowest) card. Chance at a three-card gate is 33.3%. Measured
        /// today: widest 50.8%, narrowest 17.8%. The ceiling is deliberately far below the 84.7%
        /// that made the race unusable, and far enough above today's figure that ordinary
        /// editorial churn does not flake the build.
        /// </summary>
        private const float MaxWidthHeuristicScore = 0.58f;

        /// <summary>Share of sets in which `correct` is strictly the widest card.</summary>
        private const float MaxStrictlyWidestRate = 0.55f;

        /// <summary>Mean of (correct length - widest distractor length), in characters.</summary>
        private const float MaxAbsMeanMarginChars = 3f;

        private static readonly HashSet<string> SubjectPronouns =
            new HashSet<string> { "he", "she", "they", "it" };

        [Test]
        public void CorrectIsNeverMoreThanSixCharactersWiderThanItsWidestDistractor()
        {
            var failures = new List<string>();

            foreach (var element in TestContent.Elements())
            {
                int correct = element.Element.correct.Length;
                int widest = Longest(element.Element.distractors);
                if (correct > widest + LongestMarginChars)
                    failures.Add(element.Where + ": correct is " + correct + " chars, widest distractor " + widest
                                 + " (+" + (correct - widest) + ") — '" + element.Element.correct + "'");
            }

            StoryContentTests.AssertNoFailures(failures,
                "gates where the correct card is visibly the widest — the learner can score without reading");
        }

        [Test]
        public void CorrectIsNeverMoreThanSixCharactersNarrowerThanItsNarrowestDistractor()
        {
            // The tell is symmetric: a consistently SHORT correct answer is just as free a cue as
            // a consistently long one.
            var failures = new List<string>();

            foreach (var element in TestContent.Elements())
            {
                int correct = element.Element.correct.Length;
                int narrowest = Shortest(element.Element.distractors);
                if (correct < narrowest - LongestMarginChars)
                    failures.Add(element.Where + ": correct is " + correct + " chars, narrowest distractor " + narrowest
                                 + " (" + (correct - narrowest) + ") — '" + element.Element.correct + "'");
            }

            StoryContentTests.AssertNoFailures(failures, "gates where the correct card is visibly the narrowest");
        }

        [Test]
        public void CorrectStaysWithinTenCharactersOfTheDistractorMean()
        {
            var failures = new List<string>();

            foreach (var element in TestContent.Elements())
            {
                int correct = element.Element.correct.Length;
                float mean = 0f;
                foreach (var distractor in element.Element.distractors) mean += distractor.Length;
                mean /= element.Element.distractors.Length;

                if (Abs(correct - mean) > MeanSpreadChars)
                    failures.Add(element.Where + ": correct " + correct + " chars vs distractor mean "
                                 + mean.ToString("0.0"));
            }

            StoryContentTests.AssertNoFailures(failures, "gates where the correct card's width sits apart from the pair");
        }

        [Test]
        public void IgnoringTheWordsAndTappingTheWidestCardScoresNoBetterThanChancePlusMargin()
        {
            float widest = HeuristicScore(true);
            float narrowest = HeuristicScore(false);

            Assert.LessOrEqual(widest, MaxWidthHeuristicScore,
                "'Always tap the widest card' scores " + Pct(widest) + " of race gates (chance is 33.3%). "
                + "raceFirstPickCorrect is the study's comprehension measure — a learner who never reads must not "
                + "beat chance by this much. Rewrite the distractors, do not raise this threshold.");

            Assert.LessOrEqual(narrowest, MaxWidthHeuristicScore,
                "'Always tap the narrowest card' scores " + Pct(narrowest) + " of race gates (chance is 33.3%). "
                + "The width tell has simply flipped direction.");
        }

        [Test]
        public void CorrectIsTheWidestCardAtNoMoreThanChanceAndTheMeanMarginStaysNearZero()
        {
            int sets = 0;
            int strictlyWidest = 0;
            float totalMargin = 0f;

            foreach (var element in TestContent.Elements())
            {
                sets++;
                int correct = element.Element.correct.Length;
                int widest = Longest(element.Element.distractors);
                if (correct > widest) strictlyWidest++;
                totalMargin += correct - widest;
            }

            Assert.Greater(sets, 0, "No element sets were loaded at all.");

            float rate = strictlyWidest / (float)sets;
            Assert.LessOrEqual(rate, MaxStrictlyWidestRate,
                "correct is strictly the widest card in " + strictlyWidest + " of " + sets + " sets (" + Pct(rate)
                + "). This was 121/150 (80.7%) before F44 and made the race winnable without reading.");

            float meanMargin = totalMargin / sets;
            Assert.LessOrEqual(Abs(meanMargin), MaxAbsMeanMarginChars,
                "The correct card runs " + meanMargin.ToString("0.0")
                + " characters wider than the widest distractor on average (was +10.8 before F44).");
        }

        [Test]
        public void NoRaceCardIsTooWideOrTooWordyToReadAtSpeed()
        {
            var failures = new List<string>();

            foreach (var element in TestContent.Elements())
            {
                foreach (var card in element.Cards())
                {
                    if (card.Length > MaxCardChars)
                        failures.Add(element.Where + ": " + card.Length + " chars (budget " + MaxCardChars + ") — '" + card + "'");

                    int words = card.Split(' ').Length;
                    if (words > MaxCardWords)
                        failures.Add(element.Where + ": " + words + " words (budget " + MaxCardWords + ") — '" + card + "'");
                }
            }

            StoryContentTests.AssertNoFailures(failures,
                "race cards that will not fit the 1.55 x 0.85 card at a readable font size");
        }

        [Test]
        public void DistractorsShareTheCorrectAnswersGrammaticalShape()
        {
            // Register tells: if the correct WANTED card reads "To escape the room" and both
            // distractors are bare noun phrases, the infinitive alone gives it away — and the
            // same for a pronoun subject sitting beside two named ones.
            var failures = new List<string>();

            foreach (var element in TestContent.Elements())
            {
                string correct = element.Element.correct;
                var distractors = element.Element.distractors;

                bool correctInfinitive = StartsWithTo(correct);
                bool allInfinitive = true;
                foreach (var distractor in distractors) allInfinitive &= StartsWithTo(distractor);
                if (correctInfinitive != allInfinitive)
                    failures.Add(element.Where + ": infinitive mismatch — correct '" + correct + "' vs '"
                                 + string.Join("' | '", distractors) + "'");

                bool correctPronoun = SubjectPronouns.Contains(FirstWord(correct));
                foreach (var distractor in distractors)
                {
                    if (SubjectPronouns.Contains(FirstWord(distractor)) != correctPronoun)
                    {
                        failures.Add(element.Where + ": subject mismatch — correct '" + correct + "' vs '" + distractor + "'");
                        break;
                    }
                }
            }

            StoryContentTests.AssertNoFailures(failures,
                "gates where the correct card is identifiable by register rather than by comprehension");
        }

        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Score of a learner who reads nothing and always takes the widest (or narrowest) card,
        /// splitting ties at random. Chance is 1/3.
        /// </summary>
        private static float HeuristicScore(bool widest)
        {
            float score = 0f;
            int sets = 0;

            foreach (var element in TestContent.Elements())
            {
                sets++;
                var cards = element.Cards();
                int target = cards[0].Length;
                for (int i = 1; i < cards.Length; i++)
                    target = widest
                        ? (cards[i].Length > target ? cards[i].Length : target)
                        : (cards[i].Length < target ? cards[i].Length : target);

                int tied = 0;
                foreach (var card in cards) if (card.Length == target) tied++;

                if (cards[0].Length == target) score += 1f / tied;
            }

            return sets == 0 ? 0f : score / sets;
        }

        private static int Longest(string[] values)
        {
            int longest = 0;
            foreach (var value in values) if (value.Length > longest) longest = value.Length;
            return longest;
        }

        private static int Shortest(string[] values)
        {
            int shortest = int.MaxValue;
            foreach (var value in values) if (value.Length < shortest) shortest = value.Length;
            return shortest == int.MaxValue ? 0 : shortest;
        }

        private static bool StartsWithTo(string value) =>
            value.Length >= 3 && value.Substring(0, 3).ToLowerInvariant() == "to ";

        private static string FirstWord(string value)
        {
            var match = Regex.Match(value, "[A-Za-z']+");
            return match.Success ? match.Value.ToLowerInvariant() : string.Empty;
        }

        private static string Pct(float value) => (value * 100f).ToString("0.0") + "%";

        private static float Abs(float value) => value < 0f ? -value : value;
    }
}
