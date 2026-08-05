using System;
using System.Collections.Generic;
using SummaRace.Data;
using UnityEngine;

namespace SummaRace.Core
{
    /// <summary>
    /// Builds one <see cref="SessionLog"/> per play-through out of EventBus traffic and appends
    /// it when the story completes (TDD §8.2, GDD §8.2). This is the study's in-app data, so
    /// features never write logs themselves — they raise events and this listens.
    /// <para>
    /// Two rules govern every change here. (1) A play-through is unrepeatable: anything not
    /// captured while 40 children are playing is gone, so prefer capturing a raw fact over a
    /// tidy derived one. (2) Logging must never be able to stop a learner — every handler
    /// tolerates a null log, out-of-range indices and a missing SaveManager, and the write
    /// itself cannot throw into gameplay.
    /// </para>
    /// The row schema is documented for the researcher in
    /// <c>Documentation/SummaRace_Data_Dictionary.md</c>.
    /// </summary>
    public class SessionLogService : MonoBehaviour
    {
        /// <summary>Bump whenever a field is added to <see cref="SessionLog"/>, and say what
        /// changed in the data dictionary. Stamped on every row so a build swapped in mid-study
        /// shows up in the data rather than in an argument about it.</summary>
        private const int SchemaVersion = 2;

        private const string PhaseReading = "reading";
        private const string PhaseRace = "race";
        private const string PhaseArrange = "arrange";
        private const string PhaseSummary = "summary";
        private const string PhaseResults = "results";
        private const string PhaseComplete = "complete";

        private const string OutcomeCorrect = "correct";
        private const string OutcomeWrong = "wrong";
        private const string OutcomeMissed = "missed";

        private SessionLog _log;
        private readonly HashSet<int> _pagesRecorded = new();
        private float _startedRealtime;
        private float _phaseStartedRealtime;   // start of the phase currently running
        private bool _dirtySinceWrite;         // something worth saving has happened since the last row

        private void OnEnable()
        {
            EventBus.Subscribe<StoryStarted>(OnStoryStarted);
            EventBus.Subscribe<PageAnswered>(OnPageAnswered);
            EventBus.Subscribe<ReadingCompleted>(OnReadingCompleted);
            EventBus.Subscribe<ElementCollected>(OnElementCollected);
            EventBus.Subscribe<RaceCompleted>(OnRaceCompleted);
            EventBus.Subscribe<ArrangeVerified>(OnArrangeVerified);
            EventBus.Subscribe<SummarySubmitted>(OnSummarySubmitted);
            EventBus.Subscribe<StoryCompleted>(OnStoryCompleted);
            EventBus.Subscribe<LearnerChanged>(OnLearnerChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<StoryStarted>(OnStoryStarted);
            EventBus.Unsubscribe<PageAnswered>(OnPageAnswered);
            EventBus.Unsubscribe<ReadingCompleted>(OnReadingCompleted);
            EventBus.Unsubscribe<ElementCollected>(OnElementCollected);
            EventBus.Unsubscribe<RaceCompleted>(OnRaceCompleted);
            EventBus.Unsubscribe<ArrangeVerified>(OnArrangeVerified);
            EventBus.Unsubscribe<SummarySubmitted>(OnSummarySubmitted);
            EventBus.Unsubscribe<StoryCompleted>(OnStoryCompleted);
            EventBus.Unsubscribe<LearnerChanged>(OnLearnerChanged);
        }

        /// <summary>
        /// The tablet changed hands. Close the run in flight instead of letting it drift into
        /// the next child's session: the row keeps the learnerId stamped at StoryStarted, so it
        /// is written to the previous learner's file and is correct either way — but leaving it
        /// open would let the NEXT StoryStarted's Flush write it long after the fact, with a
        /// totalSeconds that includes however long the tablet sat in the teacher's hand.
        /// </summary>
        private void OnLearnerChanged(LearnerChanged evt) => Flush();

        private void OnStoryStarted(StoryStarted evt)
        {
            // A run abandoned midway still gets written, so a device failing mid-session costs
            // at most one screen of data (GDD §9).
            Flush();

            var learner = GameManager.Instance != null ? GameManager.Instance.CurrentLearner : null;
            var story = GameManager.Instance != null ? GameManager.Instance.CurrentStory : null;

            _log = new SessionLog
            {
                schemaVersion = SchemaVersion,
                runId = Guid.NewGuid().ToString("N"),
                learnerId = learner != null ? learner.id : string.Empty,
                storyId = evt.storyId,
                startedIso = DateTime.UtcNow.ToString("o"),
                isReplay = GameManager.Instance != null && GameManager.Instance.IsCompleted(evt.storyId),

                // Run context. Session and difficulty are readable out of the story id, but a
                // researcher parsing 1,200 ids in a spreadsheet is a chance to get it wrong for
                // free — CurrentStory is already set by the time this event is raised.
                appVersion = Application.version,
                deviceId = DeviceToken,
                deviceModel = SystemInfo.deviceModel,
                session = story != null ? story.session : 0,
                difficulty = story != null ? story.difficulty : string.Empty,
                narrationOn = NarrationOn,
                lastPhase = PhaseReading,
            };

            _pagesRecorded.Clear();
            _startedRealtime = Time.realtimeSinceStartup;
            _phaseStartedRealtime = _startedRealtime;
            _dirtySinceWrite = true;
        }

        /// <summary>Only the FIRST answer per page is the measure; later ones are practice.</summary>
        private void OnPageAnswered(PageAnswered evt)
        {
            if (_log == null || !_pagesRecorded.Add(evt.pageIndex)) return;
            // Kept index-aligned with the two answer lists — see readingPageIndices.
            _log.readingPageIndices.Add(evt.pageIndex);
            _log.readingFirstChoices.Add(evt.chosenIndex);
            _log.readingFirstCorrect.Add(evt.correct);
            _dirtySinceWrite = true;
        }

        /// <summary>Last page done. Closes the supported half of the ladder — everything after
        /// this asks for the same five SWBST slots with the story text gone.</summary>
        private void OnReadingCompleted(ReadingCompleted evt)
        {
            if (_log == null) return;
            _log.readingSeconds = ClosePhase();
            // Sampled here rather than at StoryStarted: the toggle is learner-facing and lives
            // in the Reader, so this is the state they actually read under.
            _log.narrationOn = NarrationOn;
            _log.lastPhase = PhaseRace;
            _dirtySinceWrite = true;
        }

        /// <summary>
        /// Every card touched in the race, right or wrong, at any gate. Two things come out of
        /// this stream that <see cref="RaceCompleted"/> alone cannot give:
        /// the wrong-pick count GDD §8.2 asks for, and — because a gate the learner steers past
        /// entirely raises nothing at all — whether a failed element was a wrong answer or a
        /// missed gate. Both collapse into a single <c>false</c> in raceFirstPickCorrect.
        /// </summary>
        private void OnElementCollected(ElementCollected evt)
        {
            if (_log == null) return;
            int i = evt.elementIndex;
            if (i < 0 || i >= _log.raceWrongPicks.Count || i >= _log.raceFirstOutcome.Count) return;

            if (!evt.wasCorrect) _log.raceWrongPicks[i]++;

            // Provisional; RaceCompleted has the last word (see ResolveFirstOutcomes).
            if (string.IsNullOrEmpty(_log.raceFirstOutcome[i]))
                _log.raceFirstOutcome[i] = evt.wasCorrect ? OutcomeCorrect : OutcomeWrong;

            _dirtySinceWrite = true;
        }

        private void OnRaceCompleted(RaceCompleted evt)
        {
            if (_log == null || evt.result == null) return;

            _log.raceFirstPickCorrect.Clear();
            for (int i = 0; i < evt.result.firstPickCorrect.Length; i++)
                _log.raceFirstPickCorrect.Add(evt.result.firstPickCorrect[i]);

            _log.raceRunSeconds = evt.result.runSeconds;

            // Always 0 by design: the patrol never catches the learner (GDD D7).
            _log.timesCaught = evt.result.timesCaught;

            ResolveFirstOutcomes();

            _log.raceSeconds = ClosePhase();
            _log.lastPhase = PhaseArrange;
            _dirtySinceWrite = true;
        }

        /// <summary>
        /// Turns the provisional pick stream into the per-element verdict. The race sets
        /// firstPickCorrect=true only when the learner's FIRST interaction with that gate was
        /// the correct card, so:
        /// true → correct; false with a wrong card seen first → wrong; false with nothing seen,
        /// or with a correct card seen first (that was the re-presented answer, collected after
        /// the original gate went by) → missed.
        /// </summary>
        private void ResolveFirstOutcomes()
        {
            for (int i = 0; i < _log.raceFirstOutcome.Count; i++)
            {
                bool first = i < _log.raceFirstPickCorrect.Count && _log.raceFirstPickCorrect[i];
                if (first) _log.raceFirstOutcome[i] = OutcomeCorrect;
                else if (_log.raceFirstOutcome[i] != OutcomeWrong) _log.raceFirstOutcome[i] = OutcomeMissed;
            }
        }

        private void OnArrangeVerified(ArrangeVerified evt)
        {
            if (_log == null) return;
            _log.arrangeAttempts = evt.attemptCount;
            // Sticky: a later raise must never clear the fact that the learner was helped.
            if (evt.assisted) _log.arrangeAssisted = true;
            if (evt.correct) _log.arrangeSolved = true;

            // VERIFY is pressed on every failed attempt too, so only the raise that ENDS the
            // phase closes the clock.
            if (evt.correct || evt.assisted)
            {
                _log.arrangeSeconds = ClosePhase();
                _log.lastPhase = PhaseSummary;
            }
            _dirtySinceWrite = true;
        }

        private void OnSummarySubmitted(SummarySubmitted evt)
        {
            if (_log == null) return;
            _log.summaryText = evt.text;      // verbatim, for the rubric
            _log.nudgeCount = evt.nudgeCount;
            _log.summarySeconds = ClosePhase();
            _log.lastPhase = PhaseResults;
            _dirtySinceWrite = true;
        }

        private void OnStoryCompleted(StoryCompleted evt)
        {
            if (_log == null) return;
            _log.starsEarned = evt.stars;
            _log.lastPhase = PhaseComplete;
            Flush();
        }

        /// <summary>
        /// Android backgrounds the app for a notification, the home button or a screen lock,
        /// and a 2GB device may be killed while it is away — so the run has to reach disk here.
        /// But the learner usually comes back and finishes, so the live log must SURVIVE:
        /// this used to call Flush(), which nulled it, and since every handler early-returns
        /// on a null log, one notification silently discarded every page, race pick, arrange
        /// attempt and summary for the rest of that story, and no completed row was ever
        /// written. Snapshot instead, and keep going.
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            if (paused && _dirtySinceWrite) WriteRow(true);
        }

        private void OnApplicationQuit() => Flush();

        /// <summary>Writes the run in flight, if any, and clears it so it is never written twice.</summary>
        private void Flush()
        {
            if (_log == null) return;
            // Double-tapping a story card raises StoryStarted twice, and the first run has not
            // recorded anything yet. Writing it would put a row in the study data that reads
            // exactly like a genuinely abandoned run — and those always carry something, since
            // the learner has to have answered, picked or finished to get anywhere. So an empty
            // run is dropped; an abandoned one still writes, as before.
            if (HasData(_log)) WriteRow(false);
            _log = null;
        }

        /// <summary>Anything the researcher could analyse: a page answered, a race pick, or stars.</summary>
        private static bool HasData(SessionLog log) =>
            log.readingFirstChoices.Count > 0
            || log.raceFirstPickCorrect.Count > 0
            || log.starsEarned > 0;

        /// <summary>Seconds spent in the phase that just ended, and start the clock on the next.</summary>
        private float ClosePhase()
        {
            float now = Time.realtimeSinceStartup;
            float elapsed = now - _phaseStartedRealtime;
            _phaseStartedRealtime = now;
            return elapsed < 0f ? 0f : elapsed;
        }

        /// <summary>
        /// Appends one row for the run in flight. <paramref name="partial"/> marks a mid-run
        /// snapshot; the reconciliation rule lives on <see cref="SessionLog.runId"/>.
        /// An abandoned run is still recognisable by an empty <c>finishedIso</c>.
        /// </summary>
        private void WriteRow(bool partial)
        {
            if (_log == null) return;

            try
            {
                if (string.IsNullOrEmpty(_log.finishedIso) && _log.starsEarned > 0)
                    _log.finishedIso = DateTime.UtcNow.ToString("o");
                _log.totalSeconds = Time.realtimeSinceStartup - _startedRealtime;
                _log.isPartial = partial;
                _log.rowWrittenIso = DateTime.UtcNow.ToString("o");

                if (SaveManager.Instance != null) SaveManager.Instance.AppendLog(_log);
            }
            catch (Exception e)
            {
                // Losing a row is bad; freezing a child mid-story in front of a class is worse.
                Debug.LogWarning("SessionLogService: could not write the run row (" + e.Message + ").");
            }

            _dirtySinceWrite = false;
        }

        // ---------- run context ----------

        /// <summary>Narration (read-aloud) state, from the same PlayerPrefs key the Reader's
        /// VOICE toggle writes. Read rather than cached: the learner can change it mid-story.</summary>
        private static bool NarrationOn =>
            PlayerPrefs.GetInt(SummaRace.Constants.PrefKeys.NarrationOn, 1) == 1;

        private static string _deviceToken;

        /// <summary>
        /// A short, stable, one-way token for this tablet. The hardware id is hashed and never
        /// stored: the study's privacy commitment is that nothing identifying leaves the device,
        /// and a raw device id in an exported file is exactly that. Twelve hex characters is far
        /// more than enough to keep 40 tablets apart.
        /// </summary>
        private static string DeviceToken
        {
            get
            {
                if (!string.IsNullOrEmpty(_deviceToken)) return _deviceToken;
                try
                {
                    string raw = SystemInfo.deviceUniqueIdentifier;
                    if (string.IsNullOrEmpty(raw) || raw == SystemInfo.unsupportedIdentifier)
                    {
                        _deviceToken = "unknown";
                        return _deviceToken;
                    }

                    using (var sha = System.Security.Cryptography.SHA256.Create())
                    {
                        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
                        var text = new System.Text.StringBuilder(12);
                        for (int i = 0; i < 6; i++) text.Append(bytes[i].ToString("x2"));
                        _deviceToken = text.ToString();
                    }
                }
                catch (Exception)
                {
                    _deviceToken = "unknown";
                }
                return _deviceToken;
            }
        }
    }
}
