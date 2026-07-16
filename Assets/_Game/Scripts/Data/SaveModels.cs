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
        public int nudgeCount;
        public string summaryText;      // verbatim
        public int starsEarned;
        public bool isReplay;
    }
}
