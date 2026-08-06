namespace SummaRace.Core
{
    // Event payload structs — the full catalog lives in TDD §8.

    public struct AppReady { }

    public struct StoryStarted { public string storyId; }

    public struct PageAnswered
    {
        public int pageIndex;
        public int chosenIndex;
        public bool correct;
    }

    public struct ReadingCompleted { }

    /// <summary>
    /// One answer card touched in the race — raised for every pick, right or wrong, at any gate
    /// (including a re-presented correct card). This is study data, not just juice:
    /// SessionLogService counts wrong picks per SWBST element from it (GDD §8.2), and because a
    /// gate the learner steers past raises nothing at all, its absence is what separates a
    /// wrong ANSWER from a missed GATE. Keep raising it on every pick.
    /// </summary>
    public struct ElementCollected
    {
        public int elementIndex;
        public bool wasCorrect;

        // ---- appended, never reordered (see SessionLog's additive rule) ----

        /// <summary>
        /// WHICH card was taken, as an index into the story JSON's own option list for this
        /// element: 0 = <c>correct</c>, 1 = <c>distractors[0]</c>, 2 = <c>distractors[1]</c>.
        /// -1 when the raiser cannot say.
        /// <para>
        /// This is the qualitative half of the measure. <see cref="wasCorrect"/> says only THAT
        /// the learner was wrong at, say, the "But" slot; this says which wrong idea they held —
        /// and since two distractors are authored to fail in different ways, that is the
        /// difference between "a misconception is visible in the data" and "it is not". A
        /// play-through cannot be repeated, so it has to be captured while it happens.
        /// </para>
        /// A raiser that does not know this must leave <see cref="chosenText"/> empty; that is
        /// how the log distinguishes "index 0, the correct one" from "field never filled in".
        /// </summary>
        public int chosenOptionIndex;

        /// <summary>Exact text on the card that was taken. Stored verbatim beside the index so
        /// the row still reads on its own after the content pipeline regenerates a story
        /// (Tools/StoryPipeline) — the index alone would then point at different words.</summary>
        public string chosenText;

        /// <summary>Which lane the card was in: 0 left, 1 centre, 2 right; -1 when unknown.
        /// Lane is randomised per gate, so this is what lets the researcher check afterwards
        /// that position carried no information — the F44 finding was exactly a surface cue
        /// (card width) that the log could not have detected on its own.</summary>
        public int lane;

        /// <summary>True when this was the single gold RE-PRESENTED card rather than a free
        /// choice among three. A re-present can only follow a wrong pick or a missed gate, so
        /// it is never the learner's first encounter with that element and must never be read
        /// as one.</summary>
        public bool wasRepresent;
    }

    /// <summary>
    /// The race was paused or resumed by the learner (or a teacher stepping in). Not a fail
    /// state — but the phase clocks run on real time, so a two-minute intervention would
    /// otherwise land in the data as two minutes of reading effort.
    /// </summary>
    public struct RacePauseChanged { public bool paused; }

    /// <summary>
    /// The play-through ended without finishing — today only by the learner choosing to leave
    /// the race. The log row for an abandoned run already exists (empty <c>finishedIso</c>);
    /// this is what makes it say WHY, so a deliberate exit is never confused with a device
    /// that died mid-story. <c>reason</c> is a short stable token, not a sentence.
    /// </summary>
    public struct RunAbandoned { public string reason; }

    public struct PlayerCaught { }

    public struct RaceCompleted { public RaceResult result; }

    public struct ArrangeVerified
    {
        public bool correct;
        public int attemptCount;
        /// <summary>True when the remaining pieces were placed FOR the learner by the
        /// anti-frustration assist rather than solved. The researcher must be able to tell
        /// those apart directly; inferring it from the attempt count would silently break
        /// the moment the threshold is retuned.</summary>
        public bool assisted;

        // ---- appended, never reordered (see SessionLog's additive rule) ----

        /// <summary>
        /// THE ORDER THE LEARNER ACTUALLY PRODUCED on this verify: length 5, indexed by SLOT
        /// (0 = the Somebody slot .. 4 = the Then slot), each entry the ELEMENT index of the
        /// piece sitting in that slot. A correct board is therefore 0,1,2,3,4.
        /// <para>
        /// Arrange is the sequencing rung of the support-removal ladder, and until this existed
        /// the log said only THAT the order was wrong, never WHICH wrong order — so the classic
        /// SWBST finding ("learners swap But and So") was unrecoverable once the study ran. A
        /// play-through cannot be repeated, so the placement is captured as it happens.
        /// </para>
        /// Snapshotted BEFORE verification runs: verifying returns the misplaced pieces to the
        /// pool, so by the time the coroutine raises this the board no longer holds what the
        /// learner submitted. <c>null</c> when the raiser cannot say — which is deliberately the
        /// case for the assist's own raise, because the assist placed those pieces, not the child.
        /// </summary>
        public int[] placement;
    }

    public struct SummarySubmitted
    {
        public string text;
        public int nudgeCount;
    }

    public struct StoryCompleted
    {
        public string storyId;
        public int stars;
    }

    public struct SessionUnlocked { public int sessionNumber; }

    /// <summary>
    /// The tablet was handed to a different learner (teacher-gated, GDD §8.3). Raised AFTER
    /// GameManager.CurrentLearner has moved, so listeners read the learner who is playing now.
    /// A log row already in flight keeps the id it was opened with — see SessionLogService.
    /// </summary>
    public struct LearnerChanged { public string learnerId; }

    public struct SaveFailed { public string reason; }

    /// <summary>
    /// Every profile and every log on this tablet has just been erased — the post-study wipe, or
    /// the PIN-recovery reset. Listeners must DISCARD anything they were about to write, not
    /// flush it.
    /// <para>
    /// That distinction is the whole reason this event exists. The wipe is followed immediately
    /// by <c>GameManager.InitProfiles</c>, and if a play-through was still in flight the next
    /// row written would call <c>Directory.CreateDirectory</c> and recreate the logs folder —
    /// putting one row of an erased child's data back on a tablet the researcher has been told
    /// is clean. The erase is the consent promise; it has to be the last word.
    /// </para>
    /// </summary>
    public struct AllDataErased { }

    /// <summary>Result of one race run (built at the finish line, TDD §11.6).</summary>
    [System.Serializable]
    public class RaceResult
    {
        public string[] collectedPieces = new string[5]; // correct text of each element in order
        public bool[] firstPickCorrect = new bool[5];
        public int timesCaught;
        public float runSeconds;

        public int CountFirstPickCorrect()
        {
            int n = 0;
            foreach (var ok in firstPickCorrect) if (ok) n++;
            return n;
        }
    }
}
