namespace SummaRace.Constants
{
    /// <summary>
    /// Audio clip keys. Each key = a file in Resources/Audio with the same name.
    /// Swap a sound by replacing the file — never by changing code.
    /// </summary>
    public static class AudioKeys
    {
        // UI / feedback
        public const string SfxClick = "sfx_click";
        public const string SfxCorrect = "sfx_correct";
        public const string SfxNotQuite = "sfx_not_quite";
        public const string SfxPageTurn = "sfx_page_turn";
        public const string SfxStar = "sfx_star";

        // Race
        public const string SfxCollect = "sfx_collect";
        public const string SfxBoost = "sfx_boost";
        public const string SfxWhoosh = "sfx_whoosh";
        public const string SfxCaught = "sfx_caught";

        // Arrange
        public const string SfxSlotLock = "sfx_slot_lock";
        public const string SfxSlotWiggle = "sfx_slot_wiggle";

        // Music
        public const string MusicMenu = "music_menu";
        public const string MusicVictory = "music_victory";
    }
}
