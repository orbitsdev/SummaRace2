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
    }
}
