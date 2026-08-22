namespace SummaRace.Constants
{
    /// <summary>Keys for save files and PlayerPrefs — one source of truth.</summary>
    public static class PrefKeys
    {
        public const string SettingsFile = "settings.json";
        public const string ProfilesFile = "profiles.json";
        public const string LogsFolder = "logs";

        /// <summary>PlayerPrefs int: 1 = read stories aloud (default), 0 = voice off.</summary>
        public const string NarrationOn = "narration_on";

        /// <summary>Set once the learner has seen the race's steering coach, so it appears on
        /// their FIRST race only. Device-wide rather than per-learner on purpose: a shared
        /// classroom tablet should not replay the tutorial for every child who picks it up, and
        /// the coach costs nothing to a child who already knows (it plays during the countdown,
        /// while the world is held still).</summary>
        public const string RaceCoachSeen = "race_coach_seen";
    }
}
