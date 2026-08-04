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
                learnerId = learner != null ? learner.id : string.Empty,
                storyId = evt.storyId,
                startedIso = DateTime.UtcNow.ToString("o"),
                isReplay = GameManager.Instance != null && GameManager.Instance.IsCompleted(evt.storyId),
            };
            _pagesRecorded.Clear();
            _startedRealtime = Time.realtimeSinceStartup;
        }

        /// <summary>Only the FIRST answer per page is the measure; later ones are practice.</summary>
        private void OnPageAnswered(PageAnswered evt)
        {
            if (_log == null || !_pagesRecorded.Add(evt.pageIndex)) return;
            _log.readingFirstChoices.Add(evt.chosenIndex);
            _log.readingFirstCorrect.Add(evt.correct);
        }

        private void OnRaceCompleted(RaceCompleted evt)
        {
            if (_log == null || evt.result == null) return;

            _log.raceFirstPickCorrect.Clear();
            for (int i = 0; i < evt.result.firstPickCorrect.Length; i++)
                _log.raceFirstPickCorrect.Add(evt.result.firstPickCorrect[i]);

            // Always 0 by design: the patrol never catches the learner (GDD D7).
            _log.timesCaught = evt.result.timesCaught;
        }

        private void OnArrangeVerified(ArrangeVerified evt)
        {
            if (_log != null) _log.arrangeAttempts = evt.attemptCount;
        }

        private void OnSummarySubmitted(SummarySubmitted evt)
        {
            if (_log == null) return;
            _log.summaryText = evt.text;      // verbatim, for the rubric
            _log.nudgeCount = evt.nudgeCount;
        }

        private void OnStoryCompleted(StoryCompleted evt)
        {
            if (_log == null) return;
            _log.starsEarned = evt.stars;
            Flush();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Flush();
        }

        private void OnApplicationQuit() => Flush();

        /// <summary>
        /// Writes the run in flight, if any, and clears it so it is never written twice.
        /// An abandoned run is recognisable by an empty <c>finishedIso</c>.
        /// </summary>
        private void Flush()
        {
            if (_log == null) return;

            if (string.IsNullOrEmpty(_log.finishedIso) && _log.starsEarned > 0)
                _log.finishedIso = DateTime.UtcNow.ToString("o");
            _log.totalSeconds = Time.realtimeSinceStartup - _startedRealtime;

            if (SaveManager.Instance != null) SaveManager.Instance.AppendLog(_log);
            _log = null;
        }
    }
}
