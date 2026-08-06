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

        // ---------- Instructional narration (vo_*) ----------
        // Read aloud in the SAME voice as the 150 story pages (edge-tts
        // en-PH-RosaNeural, --rate=-10%), through the SAME AudioSource, and behind the
        // SAME VOICE toggle — see AudioManager.PlayVoice.
        //
        // These narrate INTERFACE instructions only. Nothing a learner is scored on is
        // spoken: no story page, no answer option, no SWBST element, no praise line. A
        // learner who cannot read the Arrange instruction was failing an INTERFACE task
        // rather than a comprehension one, which is the opposite of what the instrument
        // measures; every scored item stays unnarrated, so the measure itself is untouched.
        //
        // A missing clip is silence, never an error (AudioManager.GetClip warns once and
        // caches the miss), so the wiring is safe ahead of the audio.

        /// <summary>"Put the story parts in order!" (GameText.ArrangeTitle)</summary>
        public const string VoArrangeTitle = "vo_arrange_title";
        /// <summary>"Tap a story part, then tap where it goes." (GameText.ArrangeIntroStatus)</summary>
        public const string VoArrangeHow = "vo_arrange_how";

        /// <summary>"Write your summary!" (GameText.SummaryTitle)</summary>
        public const string VoSummaryTitle = "vo_summary_title";
        /// <summary>GameText.SummaryHint. The written "___" is a visual blank, so it is
        /// spoken as "blank" — what a screen reader does, and what the learner fills in.</summary>
        public const string VoSummaryHint = "vo_summary_hint";

        /// <summary>The race briefing's instruction (GameText.RaceBriefingBody) WITHOUT the
        /// story title: the title is story content and differs every run, so it stays on
        /// screen only. Says "Collect the 5 story parts in order. Read the 3 answers at the
        /// top. Tap the answer you want. Or tap the left, middle, or right side of the
        /// screen." Regenerate with the project's standard voice whenever
        /// GameText.RaceBriefingBody changes — an instruction spoken aloud that disagrees with
        /// the one on screen is worse for an emerging reader than no audio at all:
        ///   python -m edge_tts --voice en-PH-RosaNeural --rate=-10% -f &lt;text&gt; --write-media vo_race_briefing.mp3
        /// generated OUTSIDE Assets/ and then moved over the existing file, so the .meta
        /// (guid + loadType 1 / Vorbis / mono / no preload / load in background) survives.</summary>
        public const string VoRaceBriefing = "vo_race_briefing";

        // The five SWBST definitions on the loading overlay (GameText.LoadingTips).
        public const string VoTipSomebody = "vo_tip_somebody";
        public const string VoTipWanted = "vo_tip_wanted";
        public const string VoTipBut = "vo_tip_but";
        public const string VoTipSo = "vo_tip_so";
        public const string VoTipThen = "vo_tip_then";

        /// <summary>Voice key for GameText.LoadingTips[i]. Indexed in the SAME S-W-B-S-T
        /// order as that array — the two are read together by SceneLoader, so a reorder of
        /// one without the other makes the overlay say a different part than it shows.
        /// Callers must bounds-check against this array's own Length, not LoadingTips'.</summary>
        public static readonly string[] VoLoadingTips =
        {
            VoTipSomebody, VoTipWanted, VoTipBut, VoTipSo, VoTipThen,
        };
    }
}
