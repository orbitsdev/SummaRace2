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
        // 4: adds participantCode to every row, so the export joins to the paper pretest/posttest
        // without depending on the companion roster file also being retrieved.
        // 5: adds arrangeOrders — the sequence the learner actually built at Arrange, per verify,
        // so a wrong order is recoverable as an order and not only as a count of attempts.
        // 6: adds the backgrounded clocks — how much of each phase duration was spent with the
        // app not on screen at all, so "a struggling reader" and "a lunch break" stop being the
        // same number. Counted, never subtracted: the existing durations keep their meaning.
        private const int SchemaVersion = 6;

        /// <summary>Hard ceiling on <see cref="SessionLog.racePicks"/>. A well-behaved run
        /// produces at most 5 gates x (1 first pick + MaxRepresentMisses re-presents) = 35
        /// entries, so this cannot be reached in play — it exists so that no future change to
        /// the re-present ladder, and no stuck gate, can grow one JSON line without bound on a
        /// 2GB device. Overflow drops the newest picks and keeps the earliest, because the
        /// early ones are the first encounters and those are the measure.</summary>
        private const int MaxRacePicks = 64;

        /// <summary>Hard ceiling on <see cref="SessionLog.arrangeOrders"/>, on the same reasoning
        /// as <see cref="MaxRacePicks"/>. GameRules.ArrangeMaxAttempts is 4, so a real run
        /// produces at most 4 entries; twelve is headroom for that threshold being retuned
        /// upward without anyone remembering this line. Overflow drops the NEWEST and keeps the
        /// earliest, because entry 0 — the board built before the app locked anything green — is
        /// the measure, and a late attempt on a mostly-locked board is nearly information-free.</summary>
        private const int MaxArrangeOrders = 12;

        /// <summary>Somebody, Wanted, But, So, Then. Both the number of slots and the number of
        /// pieces, which is why one constant serves as the length of an arrangeOrders entry.</summary>
        private const int SwbstSlots = 5;

        private const string AbandonLearnerLeftRace = "race_left_by_learner";

        /// <summary>The tablet was handed to a different child while a run was open. Its own
        /// token, because otherwise this row is byte-identical to a dead battery: both are a
        /// partial with an empty <c>abandonReason</c>. On a shared tablet the handover is
        /// SYSTEMATIC — it happens every time a learner finishes — so leaving it unnamed would
        /// manufacture a steady stream of false dropouts in exactly the configuration where
        /// dropout rate is a number someone might report.</summary>
        private const string AbandonLearnerSwitched = "learner_switched";

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
        private float _pauseStartedRealtime = -1f; // realtime the race was paused, -1 = not paused

        /// <summary>Realtime the app was last backgrounded; -1 = it is on screen. Distinct from
        /// <see cref="_pauseStartedRealtime"/>, which is the learner deliberately tapping the
        /// race's own pause chip — a different event, in one phase only, and already logged.</summary>
        private float _backgroundedAtRealtime = -1f;

        /// <summary>Backgrounded seconds accrued INSIDE the phase currently running, banked into
        /// that phase's own field by <see cref="ClosePhase"/> and then reset.</summary>
        private float _phaseBackgrounded;

        private void OnEnable()
        {
            EventBus.Subscribe<StoryStarted>(OnStoryStarted);
            EventBus.Subscribe<PageAnswered>(OnPageAnswered);
            EventBus.Subscribe<ReadingCompleted>(OnReadingCompleted);
            EventBus.Subscribe<ElementCollected>(OnElementCollected);
            EventBus.Subscribe<RacePauseChanged>(OnRacePauseChanged);
            EventBus.Subscribe<RunAbandoned>(OnRunAbandoned);
            EventBus.Subscribe<RaceCompleted>(OnRaceCompleted);
            EventBus.Subscribe<ArrangeVerified>(OnArrangeVerified);
            EventBus.Subscribe<SummarySubmitted>(OnSummarySubmitted);
            EventBus.Subscribe<StoryCompleted>(OnStoryCompleted);
            EventBus.Subscribe<LearnerChanged>(OnLearnerChanged);
            EventBus.Subscribe<AllDataErased>(OnAllDataErased);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<StoryStarted>(OnStoryStarted);
            EventBus.Unsubscribe<PageAnswered>(OnPageAnswered);
            EventBus.Unsubscribe<ReadingCompleted>(OnReadingCompleted);
            EventBus.Unsubscribe<ElementCollected>(OnElementCollected);
            EventBus.Unsubscribe<RacePauseChanged>(OnRacePauseChanged);
            EventBus.Unsubscribe<RunAbandoned>(OnRunAbandoned);
            EventBus.Unsubscribe<RaceCompleted>(OnRaceCompleted);
            EventBus.Unsubscribe<ArrangeVerified>(OnArrangeVerified);
            EventBus.Unsubscribe<SummarySubmitted>(OnSummarySubmitted);
            EventBus.Unsubscribe<StoryCompleted>(OnStoryCompleted);
            EventBus.Unsubscribe<LearnerChanged>(OnLearnerChanged);
            EventBus.Unsubscribe<AllDataErased>(OnAllDataErased);
        }

        /// <summary>
        /// The tablet has just been erased. Drop the run in flight WITHOUT writing it — the one
        /// place in this class that throws data away on purpose.
        ///
        /// <see cref="Flush"/> would be exactly wrong here: it appends, and AppendLog creates the
        /// logs directory if it is missing, so flushing after a wipe recreates that folder and
        /// puts a row of the erased child's play back on a tablet the researcher believes is
        /// clean. The wipe is the consent promise made to those families, so it has to win over
        /// this class's usual rule that a play-through is never lost.
        /// </summary>
        private void OnAllDataErased(AllDataErased evt)
        {
            _log = null;
            _pagesRecorded.Clear();
            _dirtySinceWrite = false;
            _pauseStartedRealtime = -1f;
            _phaseBackgrounded = 0f;
        }

        /// <summary>
        /// The tablet changed hands. Close the run in flight instead of letting it drift into
        /// the next child's session: the row keeps the learnerId stamped at StoryStarted, so it
        /// is written to the previous learner's file and is correct either way — but leaving it
        /// open would let the NEXT StoryStarted's Flush write it long after the fact, with a
        /// totalSeconds that includes however long the tablet sat in the teacher's hand.
        /// </summary>
        private void OnLearnerChanged(LearnerChanged evt)
        {
            // Name it before flushing, so the row says WHY it is partial. Only when nothing
            // else already has: if the learner had left the race a moment earlier, that is the
            // truer reason and OnRunAbandoned has already flushed anyway.
            if (_log != null && string.IsNullOrEmpty(_log.abandonReason))
                _log.abandonReason = AbandonLearnerSwitched;
            Flush();
        }

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

                // The researcher's own participant id, copied off this child's paper booklet, is
                // ALSO stamped on every row — not only into the companion roster. The roster is a
                // separate file the teacher has to remember to pull off the tablet; if only the
                // .jsonl is retrieved, learnerId is a guid that appears nowhere on paper and the
                // process data cannot be joined to that child's pretest/posttest score, which is
                // what makes these logs usable in the results chapter at all. Copying it here
                // makes the export self-joining. It is still pseudonymous: a participant code is
                // the researcher's own pseudonym, not the child's name.
                participantCode = ParticipantCodes.Of(learner),
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
            _pauseStartedRealtime = -1f;
            _phaseBackgrounded = 0f;
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
            _log.readingSeconds = ClosePhase(out _log.readingBackgroundedSeconds);
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

            // WHICH card, not just right/wrong. Wrapped because this is the one handler that
            // allocates, and a logging fault must never surface as a stall in front of a class.
            try
            {
                if (_log.racePicks != null && _log.racePicks.Count < MaxRacePicks)
                {
                    // A raiser that cannot say which option it was leaves chosenText empty —
                    // that is the only way to tell "index 0, the correct card" apart from a
                    // struct field nobody filled in (0 is a meaningful value here).
                    bool detailed = !string.IsNullOrEmpty(evt.chosenText);
                    _log.racePicks.Add(new SummaRace.Data.RacePick
                    {
                        element = i,
                        option = detailed ? evt.chosenOptionIndex : -1,
                        text = evt.chosenText,
                        lane = detailed ? evt.lane : -1,
                        correct = evt.wasCorrect,
                        represent = evt.wasRepresent,
                        atSeconds = Time.realtimeSinceStartup - _startedRealtime,
                    });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("SessionLogService: could not record a race pick (" + e.Message + ").");
            }

            // Provisional; RaceCompleted has the last word (see ResolveFirstOutcomes).
            // A RE-PRESENT is never a first encounter — it only exists because the learner
            // already picked wrong or ran the gate past — so it must not fill this in. It used
            // to: a missed gate raised nothing, then the gold re-present was collected and wrote
            // "correct", so a gate the learner never even chose at read as a correct answer on
            // any row RaceCompleted did not later overwrite (i.e. every abandoned run).
            if (!evt.wasRepresent && string.IsNullOrEmpty(_log.raceFirstOutcome[i]))
                _log.raceFirstOutcome[i] = evt.wasCorrect ? OutcomeCorrect : OutcomeWrong;

            _dirtySinceWrite = true;
        }

        /// <summary>
        /// The race was paused or resumed. Every phase clock here is REAL time, so a teacher
        /// stepping in for two minutes would otherwise be indistinguishable from two minutes of
        /// effort. Counted rather than subtracted at source: the raw durations stay raw, and the
        /// researcher decides whether to net the pause out.
        /// </summary>
        private void OnRacePauseChanged(RacePauseChanged evt)
        {
            if (_log == null) return;
            if (evt.paused)
            {
                if (_pauseStartedRealtime >= 0f) return; // already paused — never double-count
                _pauseStartedRealtime = Time.realtimeSinceStartup;
                _log.racePauseCount++;
            }
            else
            {
                if (_pauseStartedRealtime < 0f) return;  // resume without a pause — ignore
                float held = Time.realtimeSinceStartup - _pauseStartedRealtime;
                if (held > 0f) _log.racePausedSeconds += held;
                _pauseStartedRealtime = -1f;
            }
            _dirtySinceWrite = true;
        }

        /// <summary>
        /// The learner left mid-run. The abandoned row already existed — it is written by the
        /// next StoryStarted, or at quit, and is recognisable by an empty finishedIso — so this
        /// does not invent a second mechanism: it names the reason and writes the row NOW,
        /// because "the next StoryStarted" may be a different child on a tablet that was handed
        /// over, or may never come at all.
        /// <para>
        /// ResolveFirstOutcomes is deliberately NOT run: it reads raceFirstPickCorrect, which
        /// only RaceCompleted fills, so on a partial row it would rewrite every element as
        /// "missed" including the ones actually answered. The provisional stream is already
        /// honest for everything the learner reached, and racePicks is the full account.
        /// </para>
        /// </summary>
        private void OnRunAbandoned(RunAbandoned evt)
        {
            if (_log == null) return;
            _log.abandonReason = string.IsNullOrEmpty(evt.reason)
                ? AbandonLearnerLeftRace : evt.reason;
            // Close an open pause so its seconds are not silently lost with the run.
            if (_pauseStartedRealtime >= 0f) OnRacePauseChanged(new RacePauseChanged { paused = false });
            _dirtySinceWrite = true;
            Flush();
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

            _log.raceSeconds = ClosePhase(out _log.raceBackgroundedSeconds);
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

            // WHICH order, not just that it was wrong — see SessionLog.arrangeOrders. Wrapped
            // and bounded for the same reason racePicks is: the learner is standing in front of
            // this screen, and a logging fault must cost a row, never the run.
            try
            {
                string order = EncodePlacement(evt.placement);
                if (order != null && _log.arrangeOrders != null
                    && _log.arrangeOrders.Count < MaxArrangeOrders)
                    _log.arrangeOrders.Add(order);
            }
            catch (Exception e)
            {
                Debug.LogWarning("SessionLogService: could not record the arranged order ("
                                 + e.Message + ").");
            }

            // Sticky: a later raise must never clear the fact that the learner was helped.
            if (evt.assisted) _log.arrangeAssisted = true;
            if (evt.correct) _log.arrangeSolved = true;

            // VERIFY is pressed on every failed attempt too, so only the raise that ENDS the
            // phase closes the clock.
            if (evt.correct || evt.assisted)
            {
                _log.arrangeSeconds = ClosePhase(out _log.arrangeBackgroundedSeconds);
                _log.lastPhase = PhaseSummary;
            }
            _dirtySinceWrite = true;
        }

        /// <summary>
        /// One submitted board → one five-character row entry: position = slot, character =
        /// the element placed there, so <c>"01234"</c> is correct and <c>"01324"</c> is a
        /// But/So swap. A string rather than a nested list because JsonUtility cannot serialise
        /// a list of lists at all, and because one short token per attempt stays readable in
        /// the raw .jsonl and drops straight into a CSV cell.
        /// <para>
        /// Returns null for anything that is not a full board — a raiser that cannot say
        /// (the assist), or a wrong-length array from some future caller. Null is not appended,
        /// so a malformed attempt leaves a gap rather than a row of lies. An out-of-range slot
        /// becomes '?' instead of discarding the whole attempt: the other four slots are still
        /// evidence.
        /// </para>
        /// </summary>
        private static string EncodePlacement(int[] placement)
        {
            if (placement == null || placement.Length != SwbstSlots) return null;

            var slots = new char[SwbstSlots];
            for (int i = 0; i < SwbstSlots; i++)
            {
                int element = placement[i];
                slots[i] = element >= 0 && element < SwbstSlots
                    ? (char)('0' + element)
                    : '?';
            }
            return new string(slots);
        }

        private void OnSummarySubmitted(SummarySubmitted evt)
        {
            if (_log == null) return;
            _log.summaryText = evt.text;      // verbatim, for the rubric
            _log.nudgeCount = evt.nudgeCount;
            _log.summarySeconds = ClosePhase(out _log.summaryBackgroundedSeconds);
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
        /// <para>
        /// It is also where the backgrounded clock starts and stops (schema 6). Every phase
        /// duration here is real elapsed time, which keeps running while the tablet is face
        /// down in a bag — so a child called out of the room and a child labouring over the
        /// passage produced the same readingSeconds, and nothing in the row could tell them
        /// apart afterwards. The interval is only ever COUNTED, never netted out of the phase
        /// clock: the existing fields keep meaning exactly what they have always meant, which
        /// is the same call already made for racePausedSeconds.
        /// </para>
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                // Marked before the write, so a row snapshotted here is honest about carrying
                // only the intervals that have already CLOSED — this one has no duration yet.
                if (_backgroundedAtRealtime < 0f) _backgroundedAtRealtime = Time.realtimeSinceStartup;
                if (_dirtySinceWrite) WriteRow(true);
            }
            else
            {
                CloseBackgroundInterval();
            }
        }

        /// <summary>
        /// Banks the away time since the app was backgrounded, into the run and into whichever
        /// phase was open at the time. Safe to call when it was never backgrounded, and safe to
        /// call with no run in flight — the interval is dropped rather than attributed to a run
        /// it does not belong to.
        /// <para>
        /// The interval still OPEN when a tablet is killed in the background is necessarily lost:
        /// there is no code running to close it. That costs nothing, because the last row written
        /// is the snapshot taken at the moment of backgrounding and it has no idle after it.
        /// </para>
        /// </summary>
        private void CloseBackgroundInterval()
        {
            if (_backgroundedAtRealtime < 0f) return;
            float away = Time.realtimeSinceStartup - _backgroundedAtRealtime;
            _backgroundedAtRealtime = -1f;

            if (_log == null || away <= 0f) return;
            _log.backgroundedSeconds += away;
            _log.backgroundedCount++;
            _phaseBackgrounded += away;
            _dirtySinceWrite = true;
        }

        private void OnApplicationQuit() => Flush();

        /// <summary>Writes the run in flight, if any, and clears it so it is never written twice.</summary>
        private void Flush()
        {
            if (_log == null) return;
            // A run that ends while still paused (the app is quit from the pause screen) would
            // otherwise drop that whole interval, and it is exactly the interval a researcher
            // most wants to see.
            if (_pauseStartedRealtime >= 0f) OnRacePauseChanged(new RacePauseChanged { paused = false });
            // Same reasoning for a run that ends while the app is in the background: Android
            // sends OnApplicationPause(true) and then OnApplicationQuit, so without this the
            // away time sits inside totalSeconds with nothing naming it.
            CloseBackgroundInterval();
            // Double-tapping a story card raises StoryStarted twice, and the first run has not
            // recorded anything yet. Writing it would put a row in the study data that reads
            // exactly like a genuinely abandoned run — and those always carry something, since
            // the learner has to have answered, picked or finished to get anywhere. So an empty
            // run is dropped; an abandoned one still writes, as before.
            if (HasData(_log)) WriteRow(false);
            _log = null;
        }

        /// <summary>
        /// Anything the researcher could analyse: a page answered, a race pick, an arrange
        /// attempt, or stars.
        ///
        /// This is the one place allowed to DISCARD a play-through, so it has to be a superset
        /// of everything a run can record. It was not: it gated on reading answers, race
        /// first-picks and stars only, while the log also carries <c>racePicks</c> (schema 3 —
        /// which distractor was chosen) and <c>arrangeOrders</c> (schema 5 — the order the
        /// learner actually built). A run holding only those would have been thrown away as
        /// "empty" by the very check whose comment promises abandoned runs always carry
        /// something.
        ///
        /// That is probably unreachable today, because the race is only entered through the
        /// Reader and the Reader records first — but "probably unreachable" is not a property
        /// worth resting irreplaceable data on, and nothing enforces the ordering. Widen the
        /// check instead: the cost of keeping a thin row is a row the analyst filters out; the
        /// cost of dropping a real one is a child's session that no longer exists.
        /// </summary>
        private static bool HasData(SessionLog log) =>
            log.readingFirstChoices.Count > 0
            || log.raceFirstPickCorrect.Count > 0
            || log.racePicks.Count > 0
            || log.arrangeOrders.Count > 0
            || log.starsEarned > 0;

        /// <summary>
        /// Seconds spent in the phase that just ended, and start the clock on the next.
        /// <paramref name="backgrounded"/> is how much of that same span the app was not on
        /// screen for — always a subset of the return value, so the researcher's subtraction
        /// (<c>readingSeconds - readingBackgroundedSeconds</c>) can never go negative.
        /// <para>
        /// Both numbers come off <see cref="Time.realtimeSinceStartup"/>, deliberately. Whatever
        /// that clock does while the device is asleep, it does to BOTH, so the difference is
        /// exact even on a platform where it is not strict wall time.
        /// </para>
        /// </summary>
        private float ClosePhase(out float backgrounded)
        {
            float now = Time.realtimeSinceStartup;
            float elapsed = now - _phaseStartedRealtime;
            _phaseStartedRealtime = now;
            if (elapsed < 0f) elapsed = 0f;

            backgrounded = _phaseBackgrounded < 0f ? 0f : _phaseBackgrounded;
            if (backgrounded > elapsed) backgrounded = elapsed;
            _phaseBackgrounded = 0f;
            return elapsed;
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
