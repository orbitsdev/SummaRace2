using System;
using System.Collections.Generic;
using System.Text;
using SummaRace.Constants;

namespace SummaRace.Data
{
    /// <summary>
    /// Decides whether a learner's summary is a real English summary OF THIS STORY — fully
    /// offline, no model, no network (client feedback 2026-09-14: "must not accept random answers
    /// or letters; must check the summary matches the story and its main idea; grammar need not be
    /// perfect as long as the thought is right").
    ///
    /// HOW, in the order the checks run:
    /// <list type="number">
    /// <item><b>Enough words</b> — at least <see cref="GameRules.SummaryCheckMinWords"/>, with at
    /// least four different content words (so "Mateo Mateo Mateo…" is not a summary).</item>
    /// <item><b>English</b> — most words must be recognisable: the story's own words, a common-word
    /// list, or a one-letter misspelling of either. Grammar is never graded; this only refuses
    /// keyboard-mash and other languages.</item>
    /// <item><b>Somebody</b> — the story's main character is named (misspellings tolerated).</item>
    /// <item><b>Story parts</b> — at least <see cref="GameRules.SummaryCheckMinParts"/> of Wanted /
    /// But / So / Then are touched, IN STORY ORDER. A part is touched when the summary uses a
    /// content word from the researcher's own wording of that part: the SWBST element's
    /// <c>correct</c> line and that page's correct Reader answer. One word of the summary can only
    /// count for one part.</item>
    /// </list>
    ///
    /// Tuned against all 30 stories before it was written in C#: a plain S-W-B-S-T sentence built
    /// from each story's own answers passes 30/30; the same sentence checked against a DIFFERENT
    /// story passes 1/870; hand-written child-style summaries with misspellings and broken grammar
    /// passed 15/15 while off-topic ones ("Mateo went to the store with his dad…", "I like this
    /// story…") were refused. It is a gate, not a score, and it never tells the learner the answer —
    /// only which part to add. SummaryCheckerTests locks the corpus numbers.
    /// </summary>
    public static class SummaryChecker
    {
        public enum Verdict
        {
            Ok,
            TooShort,
            NotEnglish,
            MissingSomebody,
            MissingParts,
            OutOfOrder
        }

        public struct Result
        {
            public Verdict verdict;
            /// <summary>Element indices (1..4 = Wanted..Then) the summary did not touch.</summary>
            public List<int> missingParts;
            public int partsFound;
        }

        public const int PartCount = 4; // Wanted, But, So, Then

        public static Result Check(string text, StoryData story)
        {
            var result = new Result { verdict = Verdict.Ok, missingParts = new List<int>() };
            var words = Tokenize(text);

            if (words.Count < GameRules.SummaryCheckMinWords)
                return Fail(result, Verdict.TooShort);

            // Broken content can never be satisfied, so it passes (GDD D7, never a dead end).
            if (story == null || story.elements == null || story.elements.Length < 5 ||
                story.pages == null || story.pages.Length < 5)
                return result;

            // ---- English ----
            var vocab = StoryVocabulary(story);
            var vocabStems = new HashSet<string>();
            foreach (var v in vocab) vocabStems.Add(Stem(v));
            int recognised = 0;
            foreach (var w in words)
                if (IsRecognised(w, vocab, vocabStems)) recognised++;
            if (recognised < GameRules.SummaryCheckMinEnglishShare * words.Count)
                return Fail(result, Verdict.NotEnglish);

            var content = new List<string>();
            var distinct = new HashSet<string>();
            foreach (var w in words)
                if (!StopWords.Contains(w)) { content.Add(w); distinct.Add(w); }
            if (distinct.Count < 4) return Fail(result, Verdict.TooShort);

            // ---- Somebody ----
            var somebodyKeys = SomebodyKeywords(story.elements[0].correct);
            var used = new HashSet<int>();
            if (somebodyKeys.Count > 0)
            {
                for (int j = 0; j < content.Count; j++)
                    foreach (var k in somebodyKeys)
                        if (Fuzzy(content[j], k)) { used.Add(j); break; }
                if (used.Count == 0) return Fail(result, Verdict.MissingSomebody);
            }

            // ---- Parts, in order ----
            var candidates = new List<int>[PartCount];
            for (int p = 0; p < PartCount; p++)
            {
                int element = p + 1;
                var keys = PartKeywords(story, element);
                foreach (var s in somebodyKeys) keys.Remove(s);

                candidates[p] = new List<int>();
                for (int j = 0; j < content.Count && candidates[p].Count < MaxCandidatesPerPart; j++)
                {
                    if (used.Contains(j)) continue;
                    foreach (var k in keys)
                        if (Fuzzy(content[j], k)) { candidates[p].Add(j); break; }
                }
            }

            BestAssignment(candidates, out int found, out int inOrder, out int[] chosen);
            result.partsFound = found;
            for (int p = 0; p < PartCount; p++)
                if (chosen[p] < 0) result.missingParts.Add(p + 1);

            if (found < GameRules.SummaryCheckMinParts) return Fail(result, Verdict.MissingParts);
            // Out of order AND missing a part: ask for the missing part first — it is the more
            // useful thing to add, and "tell it in order" is confusing when a part is absent.
            if (inOrder < GameRules.SummaryCheckMinParts)
                return Fail(result, result.missingParts.Count > 0 ? Verdict.MissingParts : Verdict.OutOfOrder);
            return result;
        }

        private static Result Fail(Result r, Verdict v) { r.verdict = v; return r; }

        /// <summary>
        /// Which of the five SWBST parts the text already touches (index 0 = Somebody .. 4 =
        /// Then), with none of Check's gates (length, English, order). Drives the Summary
        /// screen's live gems, so a learner SEES each part light up as they write it — the
        /// same matching as <see cref="Check"/>, so a lit gem always means the gate agrees.
        /// </summary>
        public static bool[] Detect(string text, StoryData story)
        {
            var found = new bool[5];
            if (story == null || story.elements == null || story.elements.Length < 5 ||
                story.pages == null || story.pages.Length < 5) return found;

            var content = new List<string>();
            foreach (var w in Tokenize(text)) if (!StopWords.Contains(w)) content.Add(w);
            if (content.Count == 0) return found;

            var somebodyKeys = SomebodyKeywords(story.elements[0].correct);
            var used = new HashSet<int>();
            for (int j = 0; j < content.Count; j++)
                foreach (var k in somebodyKeys)
                    if (Fuzzy(content[j], k)) { used.Add(j); break; }
            found[0] = used.Count > 0;

            var candidates = new List<int>[PartCount];
            for (int p = 0; p < PartCount; p++)
            {
                var keys = PartKeywords(story, p + 1);
                foreach (var s in somebodyKeys) keys.Remove(s);
                candidates[p] = new List<int>();
                for (int j = 0; j < content.Count && candidates[p].Count < MaxCandidatesPerPart; j++)
                {
                    if (used.Contains(j)) continue;
                    foreach (var k in keys)
                        if (Fuzzy(content[j], k)) { candidates[p].Add(j); break; }
                }
            }
            BestAssignment(candidates, out _, out _, out int[] chosen);
            for (int p = 0; p < PartCount; p++) found[p + 1] = chosen[p] >= 0;
            return found;
        }

        /// <summary>Bounds the assignment search (at most 7^4 combinations).</summary>
        private const int MaxCandidatesPerPart = 6;

        /// <summary>
        /// One summary word per part, no word reused: maximise parts found, then how many of
        /// them appear in story order (longest increasing run of positions).
        /// </summary>
        private static void BestAssignment(List<int>[] candidates, out int bestFound, out int bestOrder, out int[] bestChoice)
        {
            bestFound = 0; bestOrder = 0;
            bestChoice = new[] { -1, -1, -1, -1 };
            var choice = new[] { -1, -1, -1, -1 };
            int found = 0, order = 0;
            int[] bf = bestChoice;
            Search(0);
            bestFound = found; bestOrder = order; bestChoice = bf;

            void Search(int p)
            {
                if (p == PartCount)
                {
                    int n = 0;
                    for (int i = 0; i < PartCount; i++) if (choice[i] >= 0) n++;
                    int lis = LongestIncreasing(choice);
                    if (n > found || (n == found && lis > order))
                    {
                        found = n; order = lis; bf = (int[])choice.Clone();
                    }
                    return;
                }
                foreach (int j in candidates[p])
                {
                    if (Array.IndexOf(choice, j, 0, p) >= 0) continue;
                    choice[p] = j;
                    Search(p + 1);
                    if (found == PartCount && order == PartCount) return;
                }
                choice[p] = -1;
                Search(p + 1);
            }
        }

        private static int LongestIncreasing(int[] choice)
        {
            var seq = new List<int>();
            foreach (int c in choice) if (c >= 0) seq.Add(c);
            if (seq.Count == 0) return 0;
            var lis = new int[seq.Count];
            int best = 0;
            for (int a = 0; a < seq.Count; a++)
            {
                lis[a] = 1;
                for (int b = 0; b < a; b++)
                    if (seq[b] < seq[a] && lis[b] + 1 > lis[a]) lis[a] = lis[b] + 1;
                if (lis[a] > best) best = lis[a];
            }
            return best;
        }

        // ---------- words ----------

        public static List<string> Tokenize(string text)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(text)) return list;
            string lower = text.ToLowerInvariant().Replace("'s", "").Replace("’s", "");
            var sb = new StringBuilder();
            foreach (char c in lower)
            {
                if (c >= 'a' && c <= 'z') sb.Append(c);
                else if (sb.Length > 0) { list.Add(sb.ToString()); sb.Length = 0; }
            }
            if (sb.Length > 0) list.Add(sb.ToString());
            return list;
        }

        private static List<string> Keywords(string text)
        {
            var list = new List<string>();
            foreach (var w in Tokenize(text))
                if (w.Length >= 3 && !StopWords.Contains(w)) list.Add(w);
            return list;
        }

        private static List<string> SomebodyKeywords(string somebody)
        {
            var all = Keywords(somebody);
            var longer = all.FindAll(w => w.Length > 3);
            return longer.Count > 0 ? longer : all;
        }

        private static HashSet<string> PartKeywords(StoryData story, int element)
        {
            var keys = new HashSet<string>(Keywords(story.elements[element].correct));
            var q = story.pages[element].question;
            if (q != null && q.options != null && q.correctIndex >= 0 && q.correctIndex < q.options.Length)
                foreach (var k in Keywords(q.options[q.correctIndex])) keys.Add(k);
            return keys;
        }

        private static HashSet<string> StoryVocabulary(StoryData story)
        {
            var sb = new StringBuilder();
            sb.Append(story.title).Append(' ').Append(story.mainIdea).Append(' ');
            foreach (var p in story.pages)
            {
                if (p == null) continue;
                sb.Append(p.text).Append(' ');
                if (p.question != null)
                {
                    sb.Append(p.question.text).Append(' ');
                    if (p.question.options != null)
                        foreach (var o in p.question.options) sb.Append(o).Append(' ');
                }
            }
            foreach (var e in story.elements)
            {
                if (e == null) continue;
                sb.Append(e.correct).Append(' ');
                if (e.distractors != null) foreach (var d in e.distractors) sb.Append(d).Append(' ');
            }
            var vocab = new HashSet<string>(Tokenize(sb.ToString()));
            vocab.UnionWith(CommonWords);
            vocab.UnionWith(StopWords);
            return vocab;
        }

        private static bool IsRecognised(string w, HashSet<string> vocab, HashSet<string> vocabStems)
        {
            if (vocab.Contains(w) || vocabStems.Contains(Stem(w))) return true;
            if (w.Length < 4) return false;
            foreach (var v in vocab)
                if (Math.Abs(v.Length - w.Length) <= 1 && EditDistance(w, v, 1) <= 1) return true;
            return false;
        }

        /// <summary>Same word, same stem, a shared stem of 4+ letters, or a small misspelling.</summary>
        public static bool Fuzzy(string w, string k)
        {
            if (w == k) return true;
            string sw = Stem(w), sk = Stem(k);
            if (sw == sk) return true;
            if (sw.Length >= 4 && sk.Length >= 4 && (sw.StartsWith(sk, StringComparison.Ordinal) || sk.StartsWith(sw, StringComparison.Ordinal))) return true;
            if (k.Length >= 5 && EditDistance(w, k, 1) <= 1) return true;
            if (k.Length >= 8 && EditDistance(w, k, 2) <= 2) return true;
            return false;
        }

        public static string Stem(string w)
        {
            if (Irregular.TryGetValue(w, out var baseForm)) w = baseForm;
            foreach (var suffix in Suffixes)
            {
                if (w.Length - suffix.Length >= 3 && w.EndsWith(suffix, StringComparison.Ordinal))
                {
                    w = w.Substring(0, w.Length - suffix.Length);
                    break;
                }
            }
            if (w.Length > 3 && w[w.Length - 1] == w[w.Length - 2] && w[w.Length - 1] != 'l' && w[w.Length - 1] != 's')
                w = w.Substring(0, w.Length - 1); // stopped -> stopp -> stop
            if (w.Length > 3 && w[w.Length - 1] == 'i') w = w.Substring(0, w.Length - 1) + "y"; // tried -> tri -> try
            return w;
        }

        private static readonly string[] Suffixes = { "ingly", "edly", "ing", "ed", "es", "ly", "s" };

        /// <summary>Levenshtein with an early exit once every cell in a row exceeds <paramref name="max"/>.</summary>
        private static int EditDistance(string a, string b, int max)
        {
            if (Math.Abs(a.Length - b.Length) > max) return max + 1;
            var prev = new int[b.Length + 1];
            var cur = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) prev[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                cur[0] = i;
                int rowBest = cur[0];
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    cur[j] = Math.Min(Math.Min(prev[j] + 1, cur[j - 1] + 1), prev[j - 1] + cost);
                    if (cur[j] < rowBest) rowBest = cur[j];
                }
                if (rowBest > max) return max + 1;
                var t = prev; prev = cur; cur = t;
            }
            return prev[b.Length];
        }

        // ---------- word lists ----------

        private static readonly HashSet<string> StopWords = new HashSet<string>((
            "a an the and or but so then to of in on at for with by from up out as is was were be been are am " +
            "it its he she they them his her their him i me my we our you your this that there these those " +
            "not no yes do did does done have has had will would can could should into over after before when " +
            "while because very just also all some any one who what how why where which about off again too than").Split(' '));

        /// <summary>Everyday English a Grade-4 learner may use that a given story never does.</summary>
        private static readonly HashSet<string> CommonWords = new HashSet<string>((
            "about after again all almost also always am an and animal animals any are around as ask asked at away back " +
            "bad be because become been before began best better big bird book boy bring brought but by call called came " +
            "can care cared careful carefully cat child children city class come could day decide decided did different " +
            "do does dog dont done door down each end ended even every everyone eye face family far father feel feeling " +
            "feelings felt find finally first food for found friend friends from fun game gave get girl give go goes going " +
            "good got great grow had happy has have he help helped her here him his home house how i if in inside instead " +
            "into is it its just keep kept kind kitten knew know last learn learned left let like liked little live long " +
            "look looked looking lost lot love made make many mom mother more morning most much must my name need needed " +
            "never new next nice night no not nothing now of off old on once one only open or other our out over own part " +
            "people pet pick picked place plan play played problem put ran read ready real really right run said same saw " +
            "say school see seemed she should show sister small so solve solved some something soon started stay still stop " +
            "stopped story sure take talk tell than thank that the their them then there they thing things think thought " +
            "time to together told too took tree tried try turn two up us use used very wait walk want wanted was water way " +
            "we well went were what when where which while who why will wish with without woman work world would write " +
            "wrote year yes you young your sad angry scared teacher mad glad win won brave afraid hurt sick safe free " +
            "cant didnt wasnt isnt wont because dad brother parents grandma grandpa baby man men women kids kid").Split(' '));

        private static readonly Dictionary<string, string> Irregular = new Dictionary<string, string>
        {
            {"took","take"},{"taken","take"},{"found","find"},{"went","go"},{"gone","go"},{"gave","give"},
            {"given","give"},{"made","make"},{"told","tell"},{"said","say"},{"thought","think"},{"got","get"},
            {"ran","run"},{"saw","see"},{"seen","see"},{"came","come"},{"left","leave"},{"kept","keep"},
            {"felt","feel"},{"knew","know"},{"known","know"},{"wrote","write"},{"written","write"},
            {"brought","bring"},{"bought","buy"},{"caught","catch"},{"taught","teach"},{"ate","eat"},
            {"fell","fall"},{"flew","fly"},{"grew","grow"},{"won","win"},{"lost","lose"},{"sat","sit"},
            {"stood","stand"},{"woke","wake"},{"children","child"},{"mice","mouse"},{"people","person"},
            {"men","man"},{"women","woman"},{"feet","foot"},{"teeth","tooth"},{"better","good"},{"best","good"}
        };
    }
}
