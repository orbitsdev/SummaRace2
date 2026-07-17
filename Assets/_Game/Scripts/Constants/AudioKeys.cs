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
        public const string SfxPress = "sfx_press";           // button pointer-down tap
        public const string SfxPop = "sfx_pop";               // panel pop-in
        public const string SfxTransition = "sfx_transition"; // scene change chime
        public const string SfxCorrect = "sfx_correct";
        public const string SfxNotQuite = "sfx_not_quite";
        public const string SfxPageTurn = "sfx_page_turn";
        public const string SfxStar = "sfx_star";

        // Race
        public const string SfxCollect = "sfx_collect";
        public const string SfxCoin = "sfx_coin";             // coin-line pickup tick
        public const string SfxBoost = "sfx_boost";
        public const string SfxWhoosh = "sfx_whoosh";
        public const string SfxCaught = "sfx_caught";
        public const string SfxFootstepA = "sfx_footstep_a";
        public const string SfxFootstepB = "sfx_footstep_b";
        public const string SfxFootstepC = "sfx_footstep_c";

        // Arrange
        public const string SfxSlotLock = "sfx_slot_lock";
        public const string SfxSlotWiggle = "sfx_slot_wiggle";

        // Music
        public const string MusicMenu = "music_menu";
        public const string MusicRace = "music_race";
        public const string MusicVictory = "music_victory";
    }
}
