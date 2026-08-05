// ---------------------------------------------------------------------------------------------
// SummaRace EditMode regression suite.
//
// WHY THESE LIVE IN AN `Editor/` FOLDER AND NOT BEHIND `SummaRace.Tests.EditMode.asmdef`
// ---------------------------------------------------------------------------------------------
// The suite was asked for as an assembly-definition assembly referencing the test framework and
// `Assembly-CSharp`. Unity cannot do that: the predefined assemblies (`Assembly-CSharp`,
// `Assembly-CSharp-Editor`) automatically reference EVERY asmdef assembly, so an asmdef that
// referenced `Assembly-CSharp` back would be a cycle. Unity therefore refuses the reference, and
// a test assembly built that way cannot see one line of SummaRace code. CLAUDE.md's "Code layout
// & assemblies" note says the same thing from the other end: *introduce asmdefs before adding
// test assemblies*.
//
// Scripts in an `Editor/` folder compile into `Assembly-CSharp-Editor`, which:
//   * DOES reference `Assembly-CSharp` (so every SummaRace type below is directly usable),
//   * is editor-only and is never included in a player build — the stated reason the asmdef was
//     wanted in the first place,
//   * is picked up by the Test Runner as an EditMode test assembly, because it emits a reference
//     to `nunit.framework` (see UnityEditor.TestRunner's EditorLoadedTestAssemblyProvider, which
//     selects any loaded assembly referencing `nunit.framework` or `UnityEngine.TestRunner`, and
//     files it under EditMode when the assembly is flagged EditorOnly).
//
// THE SEAM THAT WOULD MAKE THE REQUESTED ASMDEF POSSIBLE (not done here — it touches production
// layout, which this pass is not allowed to do):
//   1. add `Assets/_Game/Scripts/SummaRace.Runtime.asmdef`
//        { "name": "SummaRace.Runtime", "references": ["Unity.InputSystem",
//          "Unity.RenderPipelines.Universal.Runtime", "Unity.TextMeshPro", "PrimeTween"] }
//      — note this also cuts SummaRace off from the Trash Dash scripts in `Assembly-CSharp`,
//      which `EndlessRaceDirector`/`EndlessKeyboardInput` currently call into, so those would
//      need an asmdef of their own (or a reference to it) in the same commit.
//   2. then move these files to `Assets/_Game/Tests/` under
//        { "name": "SummaRace.Tests.EditMode",
//          "references": ["SummaRace.Runtime", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
//          "includePlatforms": ["Editor"],
//          "precompiledReferences": ["nunit.framework.dll"],
//          "overrideReferences": true, "autoReferenced": false,
//          "defineConstraints": ["UNITY_INCLUDE_TESTS"] }
//
// HOUSE RULES FOR ANYTHING ADDED TO THIS SUITE
//   * No play mode, no coroutines, no `Awake`/`OnEnable` — Unity does not run them in edit mode
//     (CLAUDE.md gotcha 8), so singleton `Instance`s are null unless a test sets them itself and
//     puts them back afterwards.
//   * Never write to `persistentDataPath` and never mutate a project asset. `SaveManager.Instance`
//     is null in edit mode, which is what keeps `GameManager.CompleteStory` off the disk.
//   * Deterministic only. Anything touching `UnityEngine.Random` must seed it and restore
//     `Random.state` in a finally block.
//   * Do not raise `Debug.LogError` on a passing path — the test framework fails a test that logs
//     one, and the failure lands on whichever test happened to trigger it.
// ---------------------------------------------------------------------------------------------

namespace SummaRace.Tests.EditMode
{
    using System.Collections.Generic;
    using SummaRace.Constants;
    using SummaRace.Data;

    /// <summary>
    /// The whole content set, loaded once through the real runtime loader and shared by every
    /// fixture. Loading through <see cref="StoryLoader"/> (rather than reading the JSON) is the
    /// point: it is the pipe the game actually uses, so a validation rule that starts rejecting
    /// content shows up here.
    /// </summary>
    internal static class TestContent
    {
        /// <summary>S-W-B-S-T. The order is the framework being taught; it is not arbitrary.</summary>
        public static readonly string[] SwbstOrder = { "SOMEBODY", "WANTED", "BUT", "SO", "THEN" };

        public static readonly string[] Difficulties = StoryIds.Difficulties;

        /// <summary>10 sessions x 3 difficulties = 30 (GDD S3.1), derived, never typed.</summary>
        public static int ExpectedStoryCount => GameRules.SessionCount * StoryIds.Difficulties.Length;

        public const int PagesPerStory = 5;
        public const int ElementsPerStory = 5;
        public const int OptionsPerQuestion = 3;
        public const int DistractorsPerElement = 2;

        private static string[] _ids;
        private static StoryData[] _stories;
        private static int[] _sessions;
        private static string[] _difficulties;

        public static string[] Ids { get { Ensure(); return _ids; } }
        public static StoryData[] Stories { get { Ensure(); return _stories; } }
        /// <summary>The session each entry of <see cref="Ids"/> was asked for, 1..10.</summary>
        public static int[] Sessions { get { Ensure(); return _sessions; } }
        /// <summary>The difficulty each entry of <see cref="Ids"/> was asked for.</summary>
        public static string[] DifficultyOf { get { Ensure(); return _difficulties; } }

        private static void Ensure()
        {
            if (_ids != null) return;

            var ids = new List<string>();
            var stories = new List<StoryData>();
            var sessions = new List<int>();
            var difficulties = new List<string>();

            for (int session = 1; session <= GameRules.SessionCount; session++)
            {
                for (int d = 0; d < StoryIds.Difficulties.Length; d++)
                {
                    // Every id is resolved through StoryIds, never string-built here: if the
                    // convention ever moves, these tests move with the game instead of quietly
                    // testing a naming scheme nothing else uses any more.
                    string id = StoryIds.For(session, d);
                    ids.Add(id);
                    sessions.Add(session);
                    difficulties.Add(StoryIds.Difficulties[d]);
                    stories.Add(StoryLoader.Load(id));
                }
            }

            _ids = ids.ToArray();
            _stories = stories.ToArray();
            _sessions = sessions.ToArray();
            _difficulties = difficulties.ToArray();
        }

        /// <summary>
        /// Every element of every story, already null-guarded. Skips a story that failed to load
        /// so the "content is broken" failure is reported once, by the load test, instead of as
        /// a null-reference in ten different fixtures.
        /// </summary>
        public static IEnumerable<ElementRef> Elements()
        {
            var stories = Stories;
            var ids = Ids;
            for (int s = 0; s < stories.Length; s++)
            {
                var story = stories[s];
                if (story == null || story.elements == null) continue;
                for (int e = 0; e < story.elements.Length; e++)
                {
                    var element = story.elements[e];
                    if (element == null || element.correct == null || element.distractors == null) continue;
                    bool bad = false;
                    for (int d = 0; d < element.distractors.Length; d++)
                        if (element.distractors[d] == null) bad = true;
                    if (bad) continue;
                    yield return new ElementRef(ids[s], story, e, element);
                }
            }
        }

        internal readonly struct ElementRef
        {
            public readonly string StoryId;
            public readonly StoryData Story;
            public readonly int Index;
            public readonly ElementData Element;

            public ElementRef(string storyId, StoryData story, int index, ElementData element)
            {
                StoryId = storyId;
                Story = story;
                Index = index;
                Element = element;
            }

            public string Where => StoryId + " [" + Element.type + "]";

            /// <summary>The three strings that become the three race cards at this gate.</summary>
            public string[] Cards()
            {
                var cards = new string[1 + Element.distractors.Length];
                cards[0] = Element.correct;
                for (int i = 0; i < Element.distractors.Length; i++) cards[i + 1] = Element.distractors[i];
                return cards;
            }

            /// <summary>The Reader question that pre-teaches this same SWBST slot, or null.</summary>
            public QuestionData ReaderQuestion()
            {
                if (Story.pages == null || Index >= Story.pages.Length) return null;
                var page = Story.pages[Index];
                return page == null ? null : page.question;
            }
        }
    }
}
