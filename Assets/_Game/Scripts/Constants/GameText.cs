namespace SummaRace.Constants
{
    /// <summary>All learner-facing strings — tone-editable in one place (GDD §7.4).</summary>
    public static class GameText
    {
        public const string TapToStart = "TAP TO START";

        /// <summary>Subtitle under the game title on the Boot splash (TMP rich text —
        /// one playful color per word; shown on Boot and MainMenu).</summary>
        public const string BootTagline =
            "<color=#E84855>Read!</color> <color=#1F8A3B>Race!</color> <color=#7B4FD8>Summarize!</color>";

        /// <summary>Name a profile starts with before Name Entry sets a real one.</summary>
        public const string DefaultLearnerName = "Runner";

        // Name entry
        public const string NameEntryTitle = "What's your name?";
        public const string NameEntryHint = "Type your name";
        public const string NameEntryPickAvatar = "Pick your runner";
        public const string NameEntryConfirm = "LET'S GO!";

        // Teacher menu (adults only — plain on purpose)
        public const string TeacherTitle = "Teacher";
        public const string TeacherEnterPin = "Enter PIN";
        /// <summary>Button under the PIN box — must not repeat the prompt above it.</summary>
        public const string TeacherSubmit = "OK";
        public const string TeacherWrongPin = "That PIN didn't match.";
        public const string TeacherPinTooShort = "Use at least 4 digits.";

        // First-time PIN setup. Two steps on purpose: the PIN box sits behind a button on the
        // main menu, so a learner could reach it, and whatever was typed used to become the
        // teacher's PIN on one tap. Typing it twice makes setting it a decision, and catches
        // the researcher's own typo — a typo is a lockout too.
        public const string TeacherSetPin = "Teacher setup:\nchoose a PIN (4+ digits)";
        public const string TeacherConfirmPin = "Type the same PIN again";
        /// <summary>Submit label on the first setup step — says there is a second one.</summary>
        public const string TeacherSubmitNext = "NEXT";
        public const string TeacherPinMismatch = "Those didn't match. Start again.";
        public const string TeacherPinSaved = "PIN saved. Write it in the study notes — it cannot be read back.";
        public const string TeacherSaveFailed = "Could not save the PIN on this device.";

        /// <summary>Shown while wrong PINs are being slowed down, e.g. "Too many tries. Wait 30s."</summary>
        public static string TeacherCooldown(int seconds) => $"Too many tries. Wait {seconds}s.";

        // Device reset — the way back in when the PIN is lost. Reached only by the hidden
        // gesture on the gate (see TeacherMenuController), so this copy is the first thing the
        // researcher sees about it: it has to name the cost before anything is erased.
        public const string TeacherRecoveryTitle = "Reset this tablet?";
        public const string TeacherRecoveryWarning =
            "This erases every learner profile, every star and every log on this tablet. " +
            "They cannot be exported afterwards. You can then set a new PIN.\n" +
            "Tap Back to leave without erasing.";
        public const string TeacherRecoveryErase = "ERASE TABLET";
        public const string TeacherRecoveryConfirm = "Tap again to erase";
        public const string TeacherRecoveryLastChance =
            "Last chance — this cannot be undone. Tap Back to leave without erasing.";
        public const string TeacherRecoveryDone = "Tablet reset. Set a new PIN.";
        /// <summary>The gate could not be cleared, so the wipe was not started. Says what did
        /// NOT happen: a researcher who reads "reset failed" and assumes the data went too would
        /// stop trying to export it.</summary>
        public const string TeacherRecoveryFailed =
            "Could not reset this tablet. Nothing was erased — try again.";

        // Switching learners. One tablet per learner is the study's intent, but a shared tablet
        // must never silently merge two children: stars, unlocks and every exported log row are
        // keyed to one profile, and a merge cannot be undone at analysis time. So the switch
        // exists, and it sits behind the PIN with session unlocking — learners must not be able
        // to change who they are any more than they can open the next session (GDD §8.3).
        public const string TeacherSwitchLearner = "Switch learner";
        public const string TeacherLearnerPickerTitle = "Who is playing?";
        public const string TeacherNewLearner = "+ New learner";
        public const string TeacherLearnerPickerClose = "Done";
        public const string TeacherLearnerPickerUnavailable = "Learner list unavailable on this screen.";
        public static string TeacherLearnerRow(string name, int session) =>
            $"{name}  ·  Session {session}";
        /// <summary>The learner already holding the tablet — says so in words rather than a
        /// tick, because a glyph outside the TMP atlas renders as an empty box.</summary>
        public static string TeacherLearnerRowActive(string name, int session) =>
            $"{name}  ·  Session {session}  (playing)";
        public static string TeacherActiveLearner(string name) => $"Now playing: {name}";

        /// <summary>Discreet line on the Main Menu. A run recorded against the wrong child is
        /// unrecoverable, so it is worth naming who the tablet thinks is playing.</summary>
        public static string PlayingAs(string name) => $"Playing as {name}";

        public const string TeacherUnlockNext = "Unlock next session";
        public const string TeacherExport = "Export logs";
        public const string TeacherDelete = "Delete all data";
        public const string TeacherDeleteConfirm = "Tap again to confirm";
        public const string TeacherDeleted = "All learner data deleted.";
        public const string TeacherNothingToExport = "No logs to export yet.";

        /// <summary>
        /// What the researcher reads off the screen and then goes looking for over USB, so it
        /// carries the whole path on its own line. It also names the second file: the export
        /// itself is pseudonymised (every row keys on a learnerId guid), and the roster written
        /// beside it is the ONLY mapping back to a child — pulling one file and not the other
        /// leaves 40 devices of unattributable rows, and the roster cannot be regenerated once
        /// the tablet is wiped.
        /// </summary>
        public static string TeacherExported(string path) =>
            $"Saved to:\n{path}\nAlso copy the _learners.json beside it — it is the only key from a log to a name.";
        public const string TeacherAllUnlocked = "All 10 sessions are already open.";
        public static string TeacherSessionOpened(int session) => $"Session {session} is now open.";

        // Session map
        public const string SessionMapTitle = "Choose a Mission";
        /// <summary>Sessions open one at a time via the teacher's PIN (GDD §8.3), so the
        /// locked state has to read as "not yet", never as the learner's fault.</summary>
        public const string SessionLockedHint = "Your teacher opens the next mission!";
        public const string SessionCompleteCheer = "Mission complete! All three stories done!";

        // Story select
        public const string StorySelectTitle = "Pick a Story";
        public const string DifficultyEasy = "EASY";
        public const string DifficultyAverage = "AVERAGE";
        public const string DifficultyHard = "HARD";
        public const string LockedLabel = "Locked";
        /// <summary>Story Select locks are by DIFFICULTY within the current session, not by
        /// session, so the hint must point at the story above rather than a later day.</summary>
        public const string LockedHint = "Finish the story above first!";
        /// <summary>A card whose story JSON is missing. The learner did nothing wrong, so it
        /// points at the grown-ups rather than at them (GDD D7).</summary>
        public const string StoryUnavailableHint = "This story isn't ready yet — ask your teacher!";
        /// <summary>One line for a locked card that has only a single text field to say it
        /// with (the EASY card has no lock label of its own).</summary>
        public static string LockedCardLine(string hint) => $"{LockedLabel} — {hint}";

        /// <summary>Ms. Lumi's cheer on the race mission briefing.</summary>
        public const string RaceBriefingLumi = "Ready, runner?";

        public const string SummaryHint =
            "Example: Somebody wanted ___, but ___, so ___, then ___.";

        public const string SummaryTitle = "Write your summary!";
        public const string SummaryPlaceholder = "Type your one-sentence summary here...";
        public const string SubmitLabel = "SUBMIT";

        /// <summary>Closes the on-screen keyboard, which on a portrait tablet covers SUBMIT and
        /// the nudge line. Named for what the learner is doing ("I've finished typing"), not for
        /// the device — "close keyboard" would ask a 9-year-old to think about the tablet, and
        /// it must never read as a second SUBMIT.</summary>
        public const string SummaryDoneTyping = "DONE TYPING";

        // Gentle nudges shown when a summary needs another try (GDD §4.5)
        public static readonly string[] SummaryNudges =
        {
            "Try writing a little more — use the story parts above!",
            "Almost! Can you say it in one sentence about the Somebody?",
        };

        // Praise lines by star count (index 1..3) — one picked at random per result
        public static readonly string[][] PraiseByStars =
        {
            new string[0],                          // unused
            new[]                                   // 1 star
            {
                "You finished the story!",
                "You made it to the end!",
                "You stayed with it — well done!",
            },
            new[]                                   // 2 stars
            {
                "Wow, you really know this story!",
                "You found most of the story parts!",
                "That was strong reading!",
            },
            new[]                                   // 3 stars
            {
                "Amazing! You found every story part!",
                "Perfect run — every part, first try!",
                "You know this story inside out!",
            },
        };

        // ---------- Ms. Lumi's praise (GDD §7.4 tone) ----------
        // She is the app's voice, so every "correct!" moment draws from here.
        // Deliberately PROCESS praise ("you read carefully") rather than ABILITY
        // praise ("you're so smart") — ability praise makes learners avoid harder
        // tasks, which is the opposite of what a 10-session study wants.
        // Kept short: these render inside a small feedback pill.
        public static readonly string[] PraiseGeneric =
        {
            "Nice one!",
            "That's it!",
            "You got it!",
            "Well spotted!",
            "Exactly right!",
            "Good thinking!",
            "You read that carefully!",
            "That's the one!",
            "Sharp eyes!",
            "You found it!",
            "Yes! Keep going!",
            "Great reading!",
            "You're on a roll!",
            "Spot on!",
            "You figured it out!",
            "Way to go!",
        };

        /// <summary>Praise that names the SWBST part just collected, indexed
        /// S=0 W=1 B=2 S=3 T=4. Reinforces the framework while it encourages.</summary>
        public static readonly string[][] PraiseByElement =
        {
            new[] { "You found the Somebody!", "That's who it's about!" },
            new[] { "That's what they wanted!", "You found the Wanted!" },
            new[] { "You spotted the problem!", "That's what got in the way!" },
            new[] { "That's what they did!", "You found the plan!" },
            new[] { "That's how it turned out!", "You found the ending!" },
        };

        /// <summary>Shown when every Arrange slot is right.</summary>
        public static readonly string[] ArrangePerfectPool =
        {
            "Perfect order!",
            "Every part in its place!",
            "You lined up the whole story!",
            "That's the story, start to finish!",
        };

        /// <summary>Small header above the tip card on the loading overlay.</summary>
        public const string LoadingLabel = "Loading...";

        // Results screen
        public const string MainIdeaHeader = "Main Idea";
        public const string NextMissionLabel = "NEXT MISSION";

        // Reader narration toggle
        public const string VoiceOn = "VOICE ON";
        public const string VoiceOff = "VOICE OFF";

        // Reader page flow
        public const string NextLabel = "NEXT";
        public const string NextPageLabel = "NEXT PAGE";
        public const string StartRaceLabel = "START RACE!";
        public const string ReaderWrongFeedback = "Not quite — the green one is the answer!";

        /// <summary>Reader's quiet corner exit. It is only offered before the learner's first
        /// answer (see ReaderController.RefreshSecondaryControls), so it is worded as a plain
        /// direction rather than as a warning — at that point nothing can be lost.</summary>
        public const string ReaderBackLabel = "BACK";

        /// <summary>Second step of that exit. A one-tap exit is not safe on a screen a
        /// 9-year-old taps freely, so BACK arms first and this asks for the confirming tap
        /// (the same "tap again" guard the teacher screen uses on its destructive actions).
        /// One short word on purpose: it has to fit the same small chip, because a control
        /// that changes size reads as a different button appearing under the finger.</summary>
        public const string ReaderBackConfirm = "LEAVE?";

        /// <summary>Replays the current page's narration once. Worded as the learner's own
        /// request rather than as a device control — VOICE beside it is the setting that
        /// sticks, this is a one-off, and before it existed the only way to hear a missed
        /// page again was to toggle VOICE off and on.</summary>
        public const string ReaderReplayLabel = "HEAR AGAIN";

        /// <summary>Progress line above the reading card, e.g. "Page 1 / 5".</summary>
        public static string PageProgress(int current, int total) => $"Page {current} / {total}";

        /// <summary>Same badge during the page's question — the learner is no longer
        /// on the page, so it would read as stale.</summary>
        public static string QuestionProgress(int current, int total) => $"Question {current} / {total}";

        /// <summary>Answer-option prefixes. The researcher's source doc writes every
        /// processing question's choices as "A. / B. / C.", so the Reader matches it.</summary>
        public static readonly string[] OptionLetters = { "A.", "B.", "C." };

        // Race briefing — the "get ready" beat between the Reader and the run.
        public const string RaceBriefingTitle = "Your Mission";
        public const string RaceStartLabel = "START!";

        /// <summary>Shown on the START button while the race world is still assembling —
        /// tapping through before then would reveal the runner-kit menus underneath.</summary>
        public const string RaceBriefingWait = "Getting ready...";

        /// <summary>Briefing body. Names the story so the learner knows the run is
        /// about what they just read.</summary>
        public static string RaceBriefingBody(string storyTitle) =>
            $"Collect the 5 story parts of\n\"{storyTitle}\" in order.\n\nSwipe or tap left and right to move!";

        /// <summary>3-2-1-GO! steps. Last entry is treated as the "go" beat.</summary>
        public static readonly string[] RaceCountdown = { "3", "2", "1", "GO!" };

        // Race feedback + banner
        public const string RaceWrongFeedback = "Not quite — the glowing one!";
        public const string RaceFinishBanner = "FINISH!";
        public const string RaceFinishCard = "FINISH";
        public const string RaceRunToFinish = "Run to the FINISH!";

        /// <summary>Race HUD banner, e.g. "Collect: SOMEBODY  1/5".</summary>
        public static string RaceCollectBanner(string elementType, int number, int total) =>
            $"Collect: {elementType}  {number}/{total}";

        // Arrange screen
        public const string ArrangeTitle = "Put the story parts in order!";
        public const string UndoLabel = "UNDO";
        public const string VerifyLabel = "VERIFY ORDER";
        public const string ArrangeIntroStatus = "Tap a story part, then tap its place in the order.";
        public const string ArrangeFillFirst = "Fill every slot first!";
        public const string ArrangeHintPrefix = "Hint: ";
        public const string ArrangeAlmost = "Almost! The green ones are locked in — try the others again.";

        /// <summary>Shown when the screen finishes the order for a learner who is stuck
        /// (GameRules.ArrangeMaxAttempts). Deliberately NOT drawn from the praise pools:
        /// this is not a correct answer, and congratulating a solve the learner did not
        /// make is the ability-praise trap the tone rules avoid. It says "together",
        /// names no mistake, and hands the story straight on to the next step.</summary>
        public const string ArrangeAssistIntro = "This one's tricky — let's put the rest in place together.";
        public const string ArrangeAssistDone = "There's the whole story! Now tell it in your own words.";

        /// <summary>Shown for the moment before Arrange bounces back to Story Select
        /// because no story could be loaded — a failure the learner never caused.</summary>
        public const string ArrangeNoStory = "Let's pick a story first!";

        /// <summary>SWBST definitions in S-W-B-S-T order (SceneLoader shows one at random
        /// on the loading overlay, GDD §11.5). The Arrange hint indexes this array BY
        /// ELEMENT INDEX, so the order is load-bearing — add or reorder entries and the
        /// hint starts explaining the wrong part.</summary>
        public static readonly string[] LoadingTips =
        {
            "SOMEBODY is who the story is about.",
            "WANTED tells what the character wished for.",
            "BUT is the problem that got in the way.",
            "SO is what the character did about it.",
            "THEN is how everything turned out.",
        };
    }
}
