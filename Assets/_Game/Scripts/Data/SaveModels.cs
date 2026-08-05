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

        // ------------------------------------------------------------------
        // APPENDED — never reorder or rename anything above. A profile written
        // by an older build simply has no key for this and keeps the "" below
        // (JsonUtility constructs the object first, then overwrites only the
        // keys the JSON actually carries), so old saves load unchanged.
        // ------------------------------------------------------------------

        /// <summary>
        /// THE JOIN KEY FOR THE WHOLE STUDY. The researcher's own participant id — the same
        /// short code written on that child's paper pretest/posttest booklet, e.g. "P07".
        /// <para>
        /// The outcome measure is a paper rubric and the app's logs are the process data; the
        /// results chapter exists only if the two can be joined. <see cref="id"/> is a guid
        /// minted on the tablet and appears nowhere on paper, and <see cref="displayName"/> is
        /// typed by a nine-year-old — misspelled, abbreviated, or "Maria R." on two booklets —
        /// so neither can carry that join on its own. This can, because an adult sets it from
        /// the booklet in front of them.
        /// </para>
        /// Set only from the PIN-gated teacher screen, never by the learner. Normalised and
        /// checked for uniqueness through <see cref="ParticipantCodes"/> — a duplicate code is
        /// the exact failure this field exists to prevent, so it is refused at entry rather
        /// than discovered during analysis. Empty means "not set yet", which the teacher
        /// screen treats as an unfinished install.
        /// </summary>
        public string participantCode = "";
    }

    /// <summary>
    /// Rules for <see cref="LearnerProfile.participantCode"/>, in one place because three
    /// layers touch it: the teacher screen validates what was typed, GameManager enforces
    /// uniqueness across the tablet, and SaveManager writes it into the export roster. They
    /// must agree on what "P07", "p07 " and "P07" are, or the join fails on whitespace.
    /// <para>Not in GameRules: that file is gameplay tuning, and none of this is.</para>
    /// </summary>
    public static class ParticipantCodes
    {
        /// <summary>Long enough for "P01".."P40" plus a site/class prefix, short enough to stay
        /// legible on a picker row and on a booklet cover.</summary>
        public const int MaxLength = 12;

        /// <summary>Two, so one stray keystroke cannot become a participant.</summary>
        public const int MinLength = 2;

        /// <summary>
        /// The one canonical form: trimmed, spaces removed, upper-cased, length-capped.
        /// "p07", " P07" and "P 07" are one participant; without this they are three, and the
        /// duplicate check below would wave all three through.
        /// </summary>
        public static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            var text = new System.Text.StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length && text.Length < MaxLength; i++)
            {
                char c = raw[i];
                if (char.IsWhiteSpace(c)) continue;
                text.Append(char.ToUpperInvariant(c));
            }
            return text.ToString();
        }

        /// <summary>Letters and digits only, <see cref="MinLength"/>..<see cref="MaxLength"/>.
        /// Deliberately no punctuation: the code is retyped into a spreadsheet by hand, and a
        /// hyphen or a stray period is a join that silently misses.</summary>
        public static bool IsAcceptable(string raw)
        {
            string code = Normalize(raw);
            if (code.Length < MinLength || code.Length > MaxLength) return false;
            for (int i = 0; i < code.Length; i++)
                if (!char.IsLetterOrDigit(code[i])) return false;
            return true;
        }

        /// <summary>True when this profile carries a usable code. Treats null, "" and
        /// whitespace alike so a hand-edited or older save reads the same as a fresh one.</summary>
        public static bool IsSet(LearnerProfile learner) =>
            learner != null && Normalize(learner.participantCode).Length > 0;

        /// <summary>The stored code in canonical form, or "" when there is none.</summary>
        public static string Of(LearnerProfile learner) =>
            learner == null ? string.Empty : Normalize(learner.participantCode);
    }

    [Serializable]
    public class StoryProgress
    {
        public string storyId;
        public int bestStars;           // never decreases
        public bool completed;
    }

    /// <summary>
    /// One card touched in the race — the qualitative half of the race measure.
    /// <para>
    /// The star count only records whether the learner was right at each SWBST slot. This
    /// records WHICH idea they took when they were wrong, which is the half a summarising
    /// study is actually interested in: the two distractors at a slot are authored to fail in
    /// different ways, so "chose the retelling instead of the problem" and "chose a detail
    /// instead of the goal" are different misconceptions and only this can tell them apart.
    /// A play-through cannot be repeated, so it is captured as it happens.
    /// </para>
    /// One entry per pick, in the order they happened, including repeat picks at the same gate.
    /// </summary>
    [Serializable]
    public class RacePick
    {
        /// <summary>SWBST slot, 0 = Somebody .. 4 = Then.</summary>
        public int element;

        /// <summary>Index into the story JSON's own option list for that element:
        /// 0 = <c>correct</c>, 1 = <c>distractors[0]</c>, 2 = <c>distractors[1]</c>.
        /// -1 when the raiser could not say (an older build, or the legacy race scene).</summary>
        public int option;

        /// <summary>The card's exact text, stored beside the index so the row still reads on
        /// its own after Tools/StoryPipeline regenerates a story and the indices point at
        /// different words.</summary>
        public string text;

        /// <summary>0 left, 1 centre, 2 right; -1 unknown. Lane is randomised per gate, so this
        /// is what lets position bias be checked after the fact rather than assumed away.</summary>
        public int lane;

        public bool correct;

        /// <summary>This was the single gold RE-PRESENTED card, not a free choice among three.
        /// A re-present only ever follows a wrong pick or a missed gate, so it is never a first
        /// encounter and must be excluded from any "what did they choose" analysis.</summary>
        public bool represent;

        /// <summary>Seconds since the play-through opened (real time), for ordering picks
        /// against the phase clocks.</summary>
        public float atSeconds;
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
        /// <summary>Schema 4. The researcher's participant id for this learner (e.g. "P07"),
        /// copied off their paper booklet and set by the teacher behind the PIN. Duplicated here
        /// from the companion roster deliberately: the roster is a separate file that has to be
        /// pulled off the tablet too, and without it learnerId is a guid that appears nowhere on
        /// paper — so a lost roster would sever every row from the pretest/posttest score it has
        /// to be joined to. Empty means the teacher never set one on that tablet, which is a
        /// finding about the install, not a formatting detail.</summary>
        public string participantCode;
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

        /// <summary>UTC time the row itself was appended (<c>DateTime.UtcNow</c>, same clock as
        /// <see cref="startedIso"/>/<see cref="finishedIso"/>), for ordering the snapshots of one
        /// run. All three are UTC so rows from tablets in different time zones — or one tablet
        /// whose clock crossed a DST boundary mid-study — still sort against each other.</summary>
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

        // ---------------- schema 3 ----------------

        /// <summary>Every card touched in the race, in order — see <see cref="RacePick"/>.
        /// <see cref="raceWrongPicks"/> counts these per element; this says which card each
        /// one was. Capped defensively (see SessionLogService) so a pathological run can
        /// never grow an unbounded row.</summary>
        public List<RacePick> racePicks = new List<RacePick>();

        /// <summary>How many times the race was paused during this run. The pause is not a
        /// fail state and costs the learner nothing, but a run that was interrupted is not
        /// comparable to one that was not — a teacher stepping in is exactly the kind of
        /// classroom event that has to be visible in the data rather than inferred from an
        /// odd duration.</summary>
        public int racePauseCount;

        /// <summary>Total real seconds spent paused. <see cref="raceSeconds"/> and
        /// <see cref="totalSeconds"/> are real elapsed time and therefore INCLUDE this;
        /// subtract it to get time actually on task.</summary>
        public float racePausedSeconds;

        /// <summary>Why a run ended without finishing. Empty on a completed run and on a run
        /// that simply stopped (a killed app, a flat battery), which stays recognisable by an
        /// empty <see cref="finishedIso"/>. <c>"race_left_by_learner"</c> = they chose to
        /// leave from the race's pause screen. On such a row <see cref="raceFirstOutcome"/> is
        /// only as far as the run got, and elements that were missed and then re-presented but
        /// never re-taken stay empty — <see cref="racePicks"/> is the full account.</summary>
        public string abandonReason;

        // ---------------- schema 5 ----------------

        /// <summary>
        /// THE ORDER THE LEARNER PRODUCED at Arrange, one entry per VERIFY press, in the order
        /// they were pressed. Each entry is a five-character string indexed by SLOT, whose
        /// character is the ELEMENT placed in that slot: <c>"01234"</c> is correct, and
        /// <c>"01324"</c> is the classic But/So swap (slot 2 = But holds element 3 = So, slot 3
        /// = So holds element 2 = But).
        /// <para>
        /// <see cref="arrangeAttempts"/>, <see cref="arrangeSolved"/> and
        /// <see cref="arrangeAssisted"/> say only THAT the sequence was wrong and how often. A
        /// summarising study wants to know WHICH sequence: whether a cohort systematically
        /// inverts But and So, or trails Then before Somebody, is a finding about how Grade-4
        /// learners hold story structure, and it is not recoverable from a count. Arrange is the
        /// sequencing rung of the support-removal ladder and this is the only record of what it
        /// measured.
        /// </para>
        /// <para>
        /// READ ENTRY 0 AS THE MEASURE. It is the board the learner built with nothing given
        /// away. From the second verify on, every slot that was already right is locked green
        /// and pre-filled, so later entries are constrained by the app's own feedback and their
        /// diagonal is inflated. (Which slots were locked is recoverable: after entry k, slot i
        /// is locked iff some entry up to k had element i in slot i.)
        /// </para>
        /// An assisted finish contributes NO entry for the assist itself — the app placed those
        /// pieces, so the last entry here is still the learner's last real attempt. An empty
        /// list on a row that carries the field means the Arrange phase was never reached (an
        /// abandoned run); the field ABSENT means an older build that never recorded it, which
        /// is a different thing and must not be read as zero. Capped (see SessionLogService) so
        /// a stuck screen can never grow one row without bound.
        /// </summary>
        public List<string> arrangeOrders = new List<string>();
    }
}
