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
    /// </summary>
    public class SessionLogService : MonoBehaviour
    {
        private SessionLog _log;
        private readonly HashSet<int> _pagesRecorded = new();
        private float _startedRealtime;
        private bool _dirtySinceWrite; // something worth saving has happened since the last row

        private void OnEnable()
        {
            EventBus.Subscribe<StoryStarted>(OnStoryStarted);
            EventBus.Subscribe<PageAnswered>(OnPageAnswered);
            EventBus.Subscribe<RaceCompleted>(OnRaceCompleted);
            EventBus.Subscribe<ArrangeVerified>(OnArrangeVerified);
            EventBus.Subscribe<SummarySubmitted>(OnSummarySubmitted);
            EventBus.Subscribe<StoryCompleted>(OnStoryCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<StoryStarted>(OnStoryStarted);
            EventBus.Unsubscribe<PageAnswered>(OnPageAnswered);
            EventBus.Unsubscribe<RaceCompleted>(OnRaceCompleted);
            EventBus.Unsubscribe<ArrangeVerified>(OnArrangeVerified);
            EventBus.Unsubscribe<SummarySubmitted>(OnSummarySubmitted);
            EventBus.Unsubscribe<StoryCompleted>(OnStoryCompleted);
        }

        private void OnStoryStarted(StoryStarted evt)
        {
            // A run abandoned midway still gets written, so a device failing mid-session costs
            // at most one screen of data (GDD §9).
            Flush();

            var learner = GameManager.Instance != null ? GameManager.Instance.CurrentLearner : null;
            _log = new SessionLog
            {
                runId = Guid.NewGuid().ToString("N"),
                learnerId = learner != null ? learner.id : string.Empty,
                storyId = evt.storyId,
                startedIso = DateTime.UtcNow.ToString("o"),
                isReplay = GameManager.Instance != null && GameManager.Instance.IsCompleted(evt.storyId),
            };
            _pagesRecorded.Clear();
            _startedRealtime = Time.realtimeSinceStartup;
            _dirtySinceWrite = true;
        }

        /// <summary>Only the FIRST answer per page is the measure; later ones are practice.</summary>
        private void OnPageAnswered(PageAnswered evt)
        {
            if (_log == null || !_pagesRecorded.Add(evt.pageIndex)) return;
            _log.readingFirstChoices.Add(evt.chosenIndex);
            _log.readingFirstCorrect.Add(evt.correct);
            _dirtySinceWrite = true;
        }

        private void OnRaceCompleted(RaceCompleted evt)
        {
            if (_log == null || evt.result == null) return;

            _log.raceFirstPickCorrect.Clear();
            for (int i = 0; i < evt.result.firstPickCorrect.Length; i++)
                _log.raceFirstPickCorrect.Add(evt.result.firstPickCorrect[i]);

            // Always 0 by design: the patrol never catches the learner (GDD D7).
            _log.timesCaught = evt.result.timesCaught;
            _dirtySinceWrite = true;
        }

        private void OnArrangeVerified(ArrangeVerified evt)
        {
            if (_log == null) return;
            _log.arrangeAttempts = evt.attemptCount;
            _dirtySinceWrite = true;
        }

        private void OnSummarySubmitted(SummarySubmitted evt)
        {
            if (_log == null) return;
            _log.summaryText = evt.text;      // verbatim, for the rubric
            _log.nudgeCount = evt.nudgeCount;
            _dirtySinceWrite = true;
        }

        private void OnStoryCompleted(StoryCompleted evt)
        {
            if (_log == null) return;
            _log.starsEarned = evt.stars;
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
            WriteRow(false);
            _log = null;
        }

        /// <summary>
        /// Appends one row for the run in flight. <paramref name="partial"/> marks a mid-run
        /// snapshot; the reconciliation rule lives on <see cref="SessionLog.runId"/>.
        /// An abandoned run is still recognisable by an empty <c>finishedIso</c>.
        /// </summary>
        private void WriteRow(bool partial)
        {
            if (_log == null) return;

            if (string.IsNullOrEmpty(_log.finishedIso) && _log.starsEarned > 0)
                _log.finishedIso = DateTime.UtcNow.ToString("o");
            _log.totalSeconds = Time.realtimeSinceStartup - _startedRealtime;
            _log.isPartial = partial;

            if (SaveManager.Instance != null) SaveManager.Instance.AppendLog(_log);
            _dirtySinceWrite = false;
        }
    }
}
