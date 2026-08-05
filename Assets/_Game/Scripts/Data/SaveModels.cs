using System;
using System.Collections.Generic;

namespace SummaRace.Data
{
    /// <summary>App settings persisted to settings.json (TDD §6.2).</summary>
    [Serializable]
    public class AppSettings
    {
        public float musicVolume = 0.8f;
        public float sfxVolume = 1f;
        public float narrationVolume = 1f;
        public bool haptics = true;
        public bool narrationOn = true;
        public string teacherPinHash;   // never store the raw PIN

        /// <summary>
        /// Which learner is holding this tablet right now (a <see cref="LearnerProfile.id"/>).
        /// Device state, not part of any child's record, which is why it lives here and not in
        /// profiles.json — a post-study wipe deletes the profiles but keeps settings, so this
        /// can dangle and every reader must treat an unknown id as "fall back to the first".
        /// </summary>
        public string activeLearnerId;
    }

    [Serializable]
    public class LearnerProfile
    {
        public string id;               // guid
        public string displayName;      // name or alias
        public int avatarIndex;         // 0..3
        /// <summary>False until the learner sets their own name at Name Entry.</summary>
        public bool named;
        public int unlockedSession = 1; // teacher raises this
        public List<StoryProgress> progress = new List<StoryProgress>();
    }

    [Serializable]
    public class StoryProgress
    {
        public string storyId;
        public int bestStars;           // never decreases
        public bool completed;
    }

    /// <summary>
    /// One entry appended per play-through — the research data (TDD §12, GDD §8.2).
    /// <para>
    /// EVERY FIELD HERE IS THE STUDY. Nothing in this class may be renamed or removed once a
    /// build has been handed to a classroom: analysis written against a column cannot be
    /// re-run against data that no longer carries it, and a play-through is unrepeatable.
    /// Additions are safe — a row from an older build simply carries the type default, and
    /// <see cref="schemaVersion"/> tells the researcher which shape they are reading.
    /// </para>
    /// Documented for the statistician in <c>Documentation/SummaRace_Data_Dictionary.md</c>;
    /// change a field here and change it there in the same commit.
    /// </summary>
    [Serializable]
    public class SessionLog
    {
        /// <summary>Unique per play-through. The app snapshots a run to disk whenever it is
        /// backgrounded (a 2GB device may be killed while away), so one run can produce
        /// several rows: keep, per runId, the row with <c>isPartial:false</c> if there is
        /// one, else the last partial.</summary>
        public string runId;
        /// <summary>True for a mid-run snapshot, false for the row written when the run ended.</summary>
        public bool isPartial;
        public string learnerId;
        public string storyId;
        public string startedIso;
        public string finishedIso;
        public float totalSeconds;
        public List<int> readingFirstChoices = new List<int>();
        public List<bool> readingFirstCorrect = new List<bool>();
        public List<bool> raceFirstPickCorrect = new List<bool>();
        public int timesCaught;
        public int arrangeAttempts;
        /// <summary>The anti-frustration assist placed the remaining SWBST pieces for the
        /// learner instead of them completing the ordering unaided.</summary>
        public bool arrangeAssisted;
        public int nudgeCount;
        public string summaryText;      // verbatim
        public int starsEarned;
        public bool isReplay;

        // ------------------------------------------------------------------
        // Everything below is APPENDED, never reordered. Older exports simply
        // lack these keys (JsonUtility fills the type default on read).
        // ------------------------------------------------------------------

        /// <summary>Shape of this row. 1 = the original TDD §6.2 fields only; 2 = adds the
        /// run context, per-phase timing and per-element race detail below. Bump it whenever a
        /// field is added so a mid-study build change is visible in the data instead of being
        /// discovered during analysis.</summary>
        public int schemaVersion;

        /// <summary><c>Application.version</c> of the build that produced the row. Two builds
        /// in one study is a threat to internal validity; without this it is undetectable.</summary>
        public string appVersion;

        /// <summary>Stable, non-reversible token for the tablet (a truncated hash of the
        /// hardware id — never the id itself). Lets the researcher spot a learner who was moved
        /// to a spare device mid-session (GDD §9 recovery) and separate a device fault from a
        /// learner effect. Not personal data: it identifies hardware, not a child.</summary>
        public string deviceId;

        /// <summary>e.g. "samsung SM-T500". For QA of the 2GB device floor.</summary>
        public string deviceModel;

        /// <summary>Wall-clock local time the row itself was appended, for ordering the
        /// snapshots of one run. <see cref="startedIso"/>/<see cref="finishedIso"/> stay UTC.</summary>
        public string rowWrittenIso;

        /// <summary>1..10, copied from the story so the researcher never has to parse an id.</summary>
        public int session;

        /// <summary>"easy" | "average" | "hard" — the support level within the session.</summary>
        public string difficulty;

        /// <summary>Narration (text-to-speech read-aloud) state when the reading phase ended.
        /// Reading with the voice on is a different support condition from reading silently, and
        /// the toggle is learner-facing — so it is a covariate, not a setting.</summary>
        public bool narrationOn;

        /// <summary>How far the run got: "reading" | "race" | "arrange" | "summary" |
        /// "results" | "complete". On an abandoned row this is where the learner stopped.</summary>
        public string lastPhase;

        /// <summary>The page index each entry of <see cref="readingFirstChoices"/> belongs to,
        /// same order and length. The two answer lists are positional, and a story whose pages
        /// do not all carry a question would silently shift them; this makes each answer
        /// self-describing so the Reader can be compared to the Race element by element.</summary>
        public List<int> readingPageIndices = new List<int>();

        // --- time on task, seconds, one per phase of the support-removal ladder ---
        // Real elapsed time, so a phase left open while the tablet was backgrounded counts
        // that waiting. Treat long outliers as interruptions, not as effort.

        /// <summary>Story opened → last reading question answered (Reader, text visible).</summary>
        public float readingSeconds;
        /// <summary>Reading finished → finish line crossed. Includes the mission briefing and
        /// the 3-2-1 countdown; <see cref="raceRunSeconds"/> is the moving part alone.</summary>
        public float raceSeconds;
        /// <summary>Race finished → the ordering was solved or assisted.</summary>
        public float arrangeSeconds;
        /// <summary>Ordering done → the summary sentence was submitted.</summary>
        public float summarySeconds;

        /// <summary>Duration of the race itself, from the world starting to move to the finish
        /// line (GDD §8.2 "run duration"). Excludes the briefing and countdown.</summary>
        public float raceRunSeconds;

        /// <summary>Wrong cards picked per SWBST element, index 0=Somebody..4=Then (GDD §8.2
        /// "wrong picks count"). A second wrong pick at the same gate is a second entry here but
        /// never changes <see cref="raceFirstPickCorrect"/>, which is first-pick only.</summary>
        public List<int> raceWrongPicks = new List<int> { 0, 0, 0, 0, 0 };

        /// <summary>
        /// What actually happened at each element's first encounter, index 0..4:
        /// <c>"correct"</c>, <c>"wrong"</c> (a distractor was chosen — a comprehension error) or
        /// <c>"missed"</c> (no card was touched — the learner steered past the whole gate, a
        /// motor/attention event). <see cref="raceFirstPickCorrect"/> collapses the last two
        /// into <c>false</c>, so without this the star measure cannot separate "did not know"
        /// from "did not hit it". Empty string = the element was never reached (abandoned run).
        /// </summary>
        public List<string> raceFirstOutcome = new List<string> { "", "", "", "", "" };

        /// <summary>The learner put all five parts in S-W-B-S-T order themselves. With
        /// <see cref="arrangeAssisted"/> this is the assist ladder: solved unaided
        /// (true/false), helped to the end (false/true).</summary>
        public bool arrangeSolved;
    }
}
