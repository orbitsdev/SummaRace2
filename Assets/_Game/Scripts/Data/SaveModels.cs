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

    /// <summary>One entry appended per play-through — the research data (TDD §12).</summary>
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
    }
}
