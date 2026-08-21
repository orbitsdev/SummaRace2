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

        /// <summary>Closes the on-screen keyboard on the FIRST screen a learner ever sees, where
        /// the keyboard covers the four runners AND "LET'S GO!" — i.e. everything left to do.
        /// Same words as <see cref="SummaryDoneTyping"/> on purpose: it is the same gesture in
        /// the same place, and a child should only ever have to learn it once.</summary>
        public const string NameEntryDoneTyping = "DONE TYPING";

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

        /// <summary>Picker row. The participant code leads, because on a shared tablet it is the
        /// only part of the row that means the same thing on the tablet and on the paper booklet
        /// — and a row that shows no code is an install that was never finished.</summary>
        public static string TeacherLearnerRow(string code, string name, int session) =>
            $"{(string.IsNullOrEmpty(code) ? TeacherParticipantNone : code)}  ·  {name}  ·  Session {session}";
        /// <summary>The learner already holding the tablet — says so in words rather than a
        /// tick, because a glyph outside the TMP atlas renders as an empty box.</summary>
        public static string TeacherLearnerRowActive(string code, string name, int session) =>
            $"{TeacherLearnerRow(code, name, session)}  (playing)";

        // Participant code — the researcher's join key between this tablet and the paper
        // pretest/posttest booklets. Teacher-facing only: it is set behind the PIN and never
        // shown to or typed by a learner, because a nine-year-old's spelling of their own name
        // is exactly what this exists to stop the study depending on.
        public const string TeacherParticipantAction = "Participant code";
        /// <summary>Stands in for an unset code wherever one would be printed, so "missing"
        /// never renders as a blank the reader can mistake for a layout gap.</summary>
        public const string TeacherParticipantNone = "(no code)";
        public static string TeacherParticipantActionLabel(string code) =>
            string.IsNullOrEmpty(code)
                ? $"{TeacherParticipantAction}: {TeacherParticipantNone}"
                : $"{TeacherParticipantAction}: {code}";
        /// <summary>The prompt over the entry box. Names the booklet, because copying the code
        /// off the child's own paper is the whole procedure — inventing one on the tablet
        /// recreates the problem one step later.</summary>
        public const string TeacherParticipantPrompt =
            "Participant code for this learner\nCopy it from their test booklet (e.g. P07)";
        public const string TeacherParticipantSubmit = "SAVE CODE";
        public const string TeacherParticipantInvalid = "Use 2-12 letters or numbers, like P07.";
        public static string TeacherParticipantDuplicate(string code, string name) =>
            $"{code} already belongs to {name} on this tablet. Give each learner their own code.";
        public static string TeacherParticipantSaved(string code) =>
            $"Participant code saved: {code}. It is written beside this learner in every export.";
        /// <summary>Shown when a learner reaches the teacher screen without a code. Says what it
        /// costs rather than "required", because the cost is the whole reason it is asked for.</summary>
        public const string TeacherParticipantMissing =
            "Set this learner's participant code before they play — it is the only link from " +
            "their play data to their paper pretest.";
        public static string TeacherParticipantMissingCount(int count) =>
            count == 1
                ? "1 learner on this tablet still has no participant code."
                : $"{count} learners on this tablet still have no participant code.";
        /// <summary>Loud on purpose: a shared code makes two children the same child in the
        /// exported data, and nothing downstream can separate them again.</summary>
        public static string TeacherParticipantDuplicateWarning(string code) =>
            $"WARNING: {code} is on two learners here. The export cannot tell them apart.";
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
        /// Distinct from <see cref="TeacherNothingToExport"/> on purpose. Both used to be shown
        /// for the same null return, so a write that FAILED — disk full, permission denied, the
        /// file locked — told the researcher the tablet simply had no data on it. The logs are
        /// the whole dataset and there is no second chance to collect them, so this outcome has
        /// to be unmistakable and has to say that the data is still there.
        /// </summary>
        public const string TeacherExportFailed =
            "Export failed. The logs are still on this tablet — check free space and try again.";

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
        /// <summary>Same verb as StorySelectTitle ("Pick a Story") on purpose: the map and the
        /// cards are the same action one level apart, and two verbs for it is a word a
        /// second-language reader has to learn for nothing.</summary>
        public const string SessionMapTitle = "Pick a Mission";
        /// <summary>Sessions open one at a time via the teacher's PIN (GDD §8.3), so the
        /// locked state has to read as "not yet", never as the learner's fault.</summary>
        public const string SessionLockedHint = "Your teacher opens the next mission!";
                public const string SessionCompleteCheer = "Mission complete! All three stories done!";
        /// <summary>
        /// What a "mission" IS, and what its three stars count. Both were unexplained anywhere
        /// on screen, and the stars are the sharper problem: this screen's three stars mean
        /// "stories finished" while Story Select's three stars — same sprite, same row, one tap
        /// away — mean "how well you did". A teacher reading two stars here as "did okay" would
        /// be wrong. Say which one this is, on the screen it is on.
        /// </summary>
        public const string SessionMapSubtitle =
            "Each mission has 3 stories. Stars show how many you finished.";

        // Story select
        public const string StorySelectTitle = "Pick a Story";
        /// <summary>Names the rule the cards already follow but never stated: three stories per
        /// mission, opening in order. Without it a learner sees two locked cards and no reason.</summary>
        public const string StorySelectSubtitle = "Finish a story to open the next one.";
        /// <summary>On the one card that can actually be tapped. The locked cards say "Locked"
        /// and carry a padlock; nothing said the opposite, so the open card was distinguished
        /// only by NOT being dimmed — a difference a child has to notice rather than read.</summary>
        public const string PlayBadge = "PLAY";
        public const string DifficultyEasy = "EASY";
        /// <summary>Label only — the story id stays "sNN_average" (StoryIds), and the
        /// researcher's own documents say AVERAGE. On the chip a 9-year-old reads, "MEDIUM" is
        /// the word they already know: "average" is a maths/measurement word in Grade 4, not a
        /// difficulty word. Revert this one value if the study materials must match verbatim.</summary>
        public const string DifficultyAverage = "MEDIUM";
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

        /// <summary>Ms. Lumi's cheer on the race briefing.</summary>
        public const string RaceBriefingLumi = "Ready, runner?";

        public const string SummaryHint =
            "Example: Somebody wanted ___, but ___, so ___, then ___.";

        public const string SummaryTitle = "Write your summary!";
        /// <summary>Ghost text inside the box. "One-sentence summary" is a compound the title
        /// above already carries ("summary") — the box only has to say how much to write.</summary>
        public const string SummaryPlaceholder = "Write one sentence here...";
        public const string SubmitLabel = "SUBMIT";

        /// <summary>Closes the on-screen keyboard, which on a portrait tablet covers SUBMIT and
        /// the nudge line. Named for what the learner is doing ("I've finished typing"), not for
        /// the device — "close keyboard" would ask a 9-year-old to think about the tablet, and
        /// it must never read as a second SUBMIT.</summary>
        public const string SummaryDoneTyping = "DONE TYPING";


        /// <summary>The two tips beside the Summary box (from the web prototype). Between
        /// them they answer the only two questions a 9-year-old has in front of an empty box
        /// - how much do I write, and is one sentence really allowed - and they define a good
        /// summary without grading the one being written. The app never scores a summary
        /// (L6); the paper rubric is the outcome measure.</summary>
        public static readonly string[] SummaryTips =
        {
            "A good summary is short but complete.",
            "One sentence is enough if it includes all the key events.",
        };

        /// <summary>
        /// Story-specific GHOST text for the summary box, built from that story's own
        /// SOMEBODY and WANTED. An empty box plus an abstract frame ("Somebody wanted ___")
        /// asks the learner to instantiate the frame before they can begin; showing the frame
        /// already started ON THIS STORY removes that step without writing any of their
        /// sentence for them.
        ///
        /// It is a placeholder and nothing more - never insertable, never pre-filled, and
        /// gone the moment a key is pressed (L6: the child produces every word, and
        /// summaryText is stored verbatim as the record). It also stops one part short on
        /// purpose, breaking off at "but...", so the three elements the summary is really
        /// judged on are still entirely the learner's.
        ///
        /// Verified against all 30 stories: 24 WANTED lines start "To ...", 5 are bare noun
        /// phrases ("A pet to bring home"), and s01_easy alone starts "She wanted ...", which
        /// is why the leading pronoun-plus-wanted is stripped - without that strip the very
        /// first story every learner plays would read "Molly wanted she wanted to swing".
        /// </summary>
        public static string SummaryGhost(string somebody, string wanted)
        {
            if (string.IsNullOrWhiteSpace(somebody) || string.IsNullOrWhiteSpace(wanted))
                return SummaryPlaceholder;

            string who = somebody.Trim();
            string want = StripLeadingWanted(wanted.Trim(), who);
            if (string.IsNullOrEmpty(want)) return SummaryPlaceholder;

            return $"{who} wanted {LowerFirst(want)}, but...";
        }

        /// <summary>Removes a leading "She wanted " / "Marusia wanted " so the ghost does not
        /// say "wanted" twice. Matched case-insensitively against the four pronouns the
        /// stories use and against this story's own SOMEBODY.</summary>
        private static string StripLeadingWanted(string wanted, string somebody)
        {
            string[] leads = { "he", "she", "they", "it", somebody };
            foreach (var lead in leads)
            {
                if (string.IsNullOrEmpty(lead)) continue;
                string prefix = lead + " wanted ";
                if (wanted.Length > prefix.Length &&
                    wanted.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                    return wanted.Substring(prefix.Length).TrimStart();
            }
            return wanted;
        }

        /// <summary>Lowercases the FIRST letter only, so a WANTED line that was written as a
        /// sentence reads as the middle of one - while a proper noun further along
        /// ("...wanted Baba Yaga to leave") keeps its capital.</summary>
        private static string LowerFirst(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

        // Gentle nudges shown when a summary needs another try (GDD §4.5)
        public static readonly string[] SummaryNudges =
        {
            "Write a little more — use the story parts above!",
            "Almost! Try one sentence. Start with the Somebody.",
        };

        // Praise lines by star count (index 1..3) — one picked at random per result
        public static readonly string[][] PraiseByStars =
        {
            new string[0],                          // unused
            new[]                                   // 1 star
            {
                "You finished the story!",
                "You made it to the end!",
                "You kept going — well done!",
            },
            new[]                                   // 2 stars
            {
                "Wow, you remembered a lot!",
                "You found most of the story parts!",
                "That was careful reading!",
            },
            new[]                                   // 3 stars
            {
                "Amazing! You found every story part!",
                // The web prototype celebrates here with "you're a summarizing
                // superstar!". Kept the moment, changed the grammar: that line praises
                // the CHILD ("you are a ..."), and every pool in this file is
                // deliberately process praise for the reason given above it - ability
                // praise makes learners avoid harder tasks, across ten sessions. This
                // names the thing they just did instead, at the same volume.
                "You summarized the whole story!",
                "Perfect run — every part, first try!",
                "You read every page carefully!",
            },
        };

        // ---------- Ms. Lumi's praise (GDD §7.4 tone) ----------
        // She is the app's voice, so every "correct!" moment draws from here.
        // Deliberately PROCESS praise ("you read carefully") rather than ABILITY
        // praise ("you're so smart") — ability praise makes learners avoid harder
        // tasks, which is the opposite of what a 10-session study wants.
        // Kept short: these render inside a small feedback pill.
        // Also kept IDIOM-FREE — the readers are Filipino ESL learners, and "sharp eyes",
        // "spot on", "on a roll" and "way to go" are exactly the phrases a second-language
        // reader has to stop and decode, at the one moment the game wants them running.
        public static readonly string[] PraiseGeneric =
        {
            "Nice one!",
            "That's it!",
            "You got it!",
            "You picked the right one!",
            // From the web prototype's per-pick feedback — the one praise line that also
            // TEACHES: it ties the pick to the summary the child will write two stages later.
            "That belongs in your summary!",
            "Exactly right!",
            "Good thinking!",
            "You read that carefully!",
            "That's the one!",
            "You looked closely!",
            "You found it!",
            "Yes! Keep going!",
            "Great reading!",
            "Nice work!",
            "Just right!",
            "You figured it out!",
            "Well done!",
        };

        /// <summary>Praise that names the SWBST part just collected, indexed
        /// S=0 W=1 B=2 S=3 T=4. Reinforces the framework while it encourages.</summary>
        public static readonly string[][] PraiseByElement =
        {
            // Wording matches LoadingTips, which is where these five parts are TAUGHT: praise
            // that names a part differently from its definition ("the plan" for SO, "got in the
            // way" for BUT) makes the learner hold two labels for one idea.
            new[] { "You found the Somebody!", "That's who it's about!" },
            new[] { "That's what they wanted!", "You found the Wanted!" },
            new[] { "You found the problem!", "That's what stopped them!" },
            new[] { "That's what they did!", "You found what they did!" },
            new[] { "That's how the story ended!", "You found the ending!" },
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

        /// <summary>
        /// The learner's own summary, shown back to them on Results. <c>{0}</c> is their
        /// sentence, verbatim and unedited — it is quoted rather than restated so it plainly
        /// belongs to them and not to the app.
        /// <para>
        /// Deliberately a plain caption and nothing else. Summary is the one rung of the ladder
        /// that produces something, and until now the learner never saw what they produced; but
        /// the app does not grade a summary (GDD D7, and the paper rubric is the actual outcome
        /// measure), so this must not praise it, score it, correct it, or set it beside the
        /// reference answer. "You wrote" is the whole claim being made.
        /// </para>
        /// The caption is on the SAME line as the sentence on purpose: Results has exactly one
        /// unoccupied band, 108 reference px tall, and spending a whole line on a header would
        /// cost the sentence itself about a third of its type size.
        /// </summary>
        public const string ResultsYourSummary = "You wrote: “{0}”";

        /// <summary>D3 (owner approved 2026-08-21): the finished race time on Results — the
        /// prototype's racing-clock spirit at zero validity cost, because it appears AFTER the
        /// run ends and never during it (L1: no clock on the learner while reading). Plain
        /// fact, not a score: no "fast", no comparison, no target to beat — a slower reader's
        /// time is as celebrated as anyone's (D7).</summary>
        public static string ResultsRaceTime(int totalSeconds)
        {
            if (totalSeconds < 0) totalSeconds = 0;
            return "Your race: " + (totalSeconds / 60) + ":" + (totalSeconds % 60).ToString("00") + "!";
        }

        /// <summary>Continue button AFTER the third story of a session, when it leaves for the
        /// Session Map. "Mission" means a session everywhere else in the game (SessionMapTitle,
        /// SessionLockedHint), so this wording is only true on that branch.</summary>
        public const string NextMissionLabel = "NEXT MISSION";

        /// <summary>Continue button mid-session, when it returns to the three story cards of the
        /// SAME session. Calling that "next mission" taught the wrong word for where the learner
        /// was going — the mission has not changed, only the story.</summary>
        public const string ResultsNextStoryLabel = "NEXT STORY";

        // Reader narration toggle
        public const string VoiceOn = "VOICE ON";
        public const string VoiceOff = "VOICE OFF";

        // Reader page flow
        /// <summary>The button under a story PAGE. It used to say "NEXT", which promises the
        /// next page and delivers a question — and not a harmless one: ReaderController
        /// .ShowQuestion hides the reading card, so the page the question is about is GONE
        /// while the learner answers, and that first answer is study data
        /// (SessionLogService records only the first answer per page). A learner who thinks
        /// NEXT means "next page" reads casually, taps, and is measured on a text they can no
        /// longer see. Naming the destination is the whole fix, and it is the same thing this
        /// button already does in its other two states below — this was the odd one out.
        ///
        /// It cannot touch the measure it protects: it is identical on every page of every
        /// story, it is gone from the screen before any option exists, and it says nothing
        /// about which option is right. No extra reading either — one word for one word, and
        /// "Question" is the word the progress badge shows one tap later
        /// (QuestionProgress), so the button teaches the badge and the badge confirms the
        /// button. Fits: 248 px at 48pt Fredoka in the 540 px NextButton, narrower than the
        /// "NEXT PAGE" that already ships there.</summary>
        public const string NextLabel = "QUESTION!";
        public const string NextPageLabel = "NEXT PAGE";
        public const string StartRaceLabel = "START RACE!";
        /// <summary>Says nothing about colour. "The green one is the answer" made colour the ONLY
        /// channel carrying the answer, which tells a red-green colour-blind learner nothing at
        /// all — ~8% of boys, so 1-2 children in a 40-learner study, silently excluded from the
        /// one sentence that teaches after a wrong pick. ReaderController highlights the correct
        /// option AND punch-scales it (see OnOptionChosen), so the answer is already carried by
        /// motion as well as colour; this line just stops naming the channel a learner may not
        /// have. Deliberately NOT a tick glyph — there is no tick in the UI to point at.</summary>
        public const string ReaderWrongFeedback = "Not quite — here is the answer!";

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


        /// <summary>
        /// The hint line under each Reader question - a nudge toward WHICH SWBST slot the
        /// question is asking about, indexed BY PAGE (page i teaches element i; that pairing
        /// is what SummaRace_Story_Alignment_Audit.md measured, so this index is load-bearing
        /// in exactly the same way LoadingTips' is).
        ///
        /// Safe to show because the Reader is the SUPPORTED rung of the ladder: the page text
        /// is on screen, the framework is being pre-taught, and the race is where support is
        /// removed. It names the SLOT, never the answer - it is identical for all three
        /// options and for all 30 stories, so it cannot be used to pick an option without
        /// reading, which is the exploit F44/F47 spent two passes closing in the race.
        ///
        /// Phrased as a QUESTION rather than as LoadingTips' definitions on purpose: the
        /// definition already appears on the loading overlay and as the Arrange hint, and a
        /// learner who reads "BUT is the problem the character had" directly above a question
        /// about the problem is handed the sentence frame instead of prompted to think.
        ///
        /// No emoji glyph in the string. The web prototype draws a lamp here, but Fredoka and
        /// Nunito SDF carry no emoji, so a literal one would render as a missing-glyph box on
        /// the tablet - the Reader styles this line instead (see ReaderController).
        /// </summary>
        public static readonly string[] ReaderSlotHints =
        {
            "Who is this story about?",
            "What does the character want?",
            "What is the problem stopping them?",
            "What did the character do about it?",
            "How does the story end?",
        };

        /// <summary>Safe accessor - a story whose page count ever drifts from five must not
        /// throw on the screen that teaches. Returns empty, and the line hides itself.</summary>
        public static string ReaderSlotHint(int pageIndex) =>
            pageIndex >= 0 && pageIndex < ReaderSlotHints.Length ? ReaderSlotHints[pageIndex] : "";

        // Race briefing — the "get ready" beat between the Reader and the run.
        /// <summary>Was "Your Mission", which collided with the game's other meaning of mission
        /// (a whole session of three stories — SessionMapTitle, SessionLockedHint). One race is
        /// not a mission, so the briefing names what it actually is.</summary>
        public const string RaceBriefingTitle = "Your Race";
        public const string RaceStartLabel = "START!";

        /// <summary>Shown on the START button while the race world is still assembling —
        /// tapping through before then would reveal the runner-kit menus underneath.</summary>
        public const string RaceBriefingWait = "Getting ready...";

        /// <summary>
        /// The race world never finished building, so the briefing turns into a way out
        /// (EndlessRaceDirector.ShowBriefingEscape). Two rules the wording has to keep: it is
        /// never the child's fault, and it must not pretend the race is about to start. It says
        /// what will happen next instead of what went wrong, because "what went wrong" is a
        /// content-build problem no nine-year-old can act on — the diagnosis goes to the log.
        /// </summary>
        public const string RaceBootFailedBody =
            "This race is not ready yet. Let's go back and pick a story.";

        public const string RaceBootFailedButton = "GO BACK";

        /// <summary>
        /// Briefing body. Names the story so the learner knows the run is about what they just read.
        ///
        /// IT MUST NAME THE READING PANEL, because after F47(a) that panel is the ONLY legible copy
        /// of the three answers — the world cards give 0.28–0.50s of legible time and were never
        /// readable (the derivation is on EndlessRaceDirector.BuildOptionPreview). This line is the
        /// only instruction the learner ever receives about the race, and it used to say nothing
        /// about the panel at all, so a learner who never looked up was reading unreadable cards and
        /// guessing — which raceFirstPickCorrect, the study's headline measure, records as failure
        /// to comprehend rather than as a UI they were never told about.
        ///
        /// It also has to say THREE. "left and right" describes two choices over a three-lane road,
        /// and the middle lane is a third of every gate. Tap-to-lane has always selected it (the
        /// centre third of the screen); nothing had ever said so.
        ///
        /// Tap is named FIRST because it is the input a struggling learner should reach for: one tap
        /// to any lane (a swipe is one lane per swipe, so the far lane needed two inside a sub-second
        /// window), less dexterity, and it cannot be misread as a Jump the way a hurried diagonal
        /// flick can. Tapping the ANSWER is named before tapping the road because the panel columns
        /// are now real tap targets (EndlessRaceDirector.OnPreviewColumnTapped), which collapses
        /// "read at the top, then map it onto a lane at the bottom, then steer" into one act.
        ///
        /// Swipe is deliberately no longer named: it still works (their CharacterInputController
        /// path is untouched), but naming three controls in a briefing a nine-year-old reads once is
        /// worse than naming the one that is easiest and always sufficient.
        ///
        /// Kept short and idiom-free: it is also spoken aloud (AudioKeys.VoRaceBriefing), and the
        /// clip carries the same words minus the story title.
        /// </summary>
        public static string RaceBriefingBody(string storyTitle) =>
            $"Collect the 5 story parts of\n\"{storyTitle}\" in order."
            + "\n\nRead the 3 answers at the top.\nTap the answer you want."
            + "\n\nOr tap the left, middle, or right side of the screen.";

        /// <summary>
        /// The patrol sentence, appended to the briefing ONLY when the cameo is switched on
        /// (GameRules.RacePatrolCameoEnabled). Kept out of RaceBriefingBody so the two cannot
        /// drift: turn the cameo off and the briefing stops promising a character that will
        /// never appear, with no second edit and no re-recording, because this line has its own
        /// clip (AudioKeys.VoRaceBriefingPatrol) queued after the main one.
        ///
        /// NOT the prototype's "PATROL IS COMING!". That shouts, and worse, it is not true of
        /// what the game does: nothing comes for the learner. The cop appears AHEAD on the
        /// shoulder for under two seconds and is left behind - he has no rule, ends nothing, and
        /// timesCaught is 0 for every run ever logged (D7/L3). A briefing that threatens a catch
        /// which cannot happen teaches a nine-year-old to fear a beat that is only decoration,
        /// which is the opposite of never-punish. So it names the beat and closes the door on
        /// the fear in the same breath.
        /// </summary>
        public const string RaceBriefingPatrol =
            "If you miss a part, the patrol races past. It never catches you!";

        /// <summary>3-2-1-GO! steps. Last entry is treated as the "go" beat.</summary>
        public static readonly string[] RaceCountdown = { "3", "2", "1", "GO!" };

        // Race feedback + banner
        //
        // RaceWrongFeedback ("Not quite — get the glowing card!") was here and is deleted. It
        // told the learner to go and collect a card that no longer exists: the re-present gate it
        // described was removed when the wrong-answer beat became a 2.2s reveal of the correct
        // answer. An instruction to do something impossible is worse than no instruction — a
        // child who cannot find the glowing card concludes they missed it, on the beat that
        // exists to reassure them. The director now shows the answer itself, which needs no
        // constant because it is story content.
        // Race pause. A learner could not leave a race at all before this — in a 55-minute
        // classroom session the likeliest real failure is a child tapping the wrong story, or the
        // clock running out, and force-quitting was the only way out (which files the run as
        // abandoned). Pausing is never framed as failure or as a penalty: "Take a break!" and
        // "KEEP RUNNING" say the run is still theirs and still waiting.
        public const string RacePauseTitle = "Take a break!";
        public const string RacePauseBody = "Your story is waiting for you.";
        public const string RaceResumeLabel = "KEEP RUNNING";
        public const string RaceLeaveLabel = "LEAVE RACE";
        /// <summary>Second tap of the leave confirm — deliberately two taps so a 9-year-old
        /// cannot lose a run to one stray press.</summary>
        public const string RaceLeaveConfirm = "LEAVE?";

        public const string RaceFinishBanner = "FINISH!";
        public const string RaceFinishCard = "FINISH";
        public const string RaceRunToFinish = "Run to the FINISH!";

        /// <summary>
        /// The gate-arrival countdown chip, e.g. "Next part in 8s".
        ///
        /// "Next PART", not "next gate": a gate is the designer's word for this thing and the
        /// learner is never taught it, while "story parts" is the vocabulary Arrange and Summary
        /// already use for the same five items - and the thing arriving IS the next SWBST part.
        /// So the chip reinforces the framework instead of naming a mechanism.
        ///
        /// Counts the REAL seconds until the cards reach the runner (integrated against the
        /// acceleration, so it does not lie). At zero the part simply arrives - there is no fail
        /// state attached to it, and no global race clock anywhere (L1).
        /// </summary>
        public static string RaceGateTimer(int seconds) =>
            $"Next part in {(seconds < 0 ? 0 : seconds)}s";


        /// <summary>
        /// The framing line in the race feedback pill on a wrong pick, one beat BEFORE the
        /// answer itself appears on the reading panel (EndlessRaceDirector.HitWrong then
        /// ShowAnswerReveal).
        ///
        /// The pill used to show the correct answer and the panel then showed it again, so
        /// the moment said one thing twice and said nothing about why the pick was wrong.
        /// These lines do the teaching that repetition was wasting: the distractors are
        /// usually TRUE of the story - what makes them wrong is that they are not the part
        /// being collected. Naming that is the whole point of the framework.
        ///
        /// Warm, never scolding (D7): no "wrong", no "no", nothing that reads as a buzzer.
        /// The answer follows immediately, so the learner is never left holding only a
        /// correction.
        /// </summary>
        public static readonly string[] RaceWrongLines =
        {
            "That detail isn't the most important.",
            "Not quite - here's the part we need!",
            "Close! That's not the part we're collecting.",
            "That happened, but it isn't this part.",
        };

        /// <summary>Race HUD banner, e.g. "Collect: SOMEBODY  1/5".</summary>
        public static string RaceCollectBanner(string elementType, int number, int total) =>
            $"Collect: {elementType}  {number}/{total}";

        // Arrange screen
        /// <summary>The screen's plain title. No longer drawn - ArrangeLumiIntro took the
        /// bubble - but kept because it is the shortest correct statement of what this
        /// screen asks, and the two nearby strings (ArrangeIntroStatus, ArrangeLumiIntro)
        /// are both worded to agree with it. Delete it and the next edit to either one has
        /// nothing to agree with.</summary>
        public const string ArrangeTitle = "Put the story parts in order!";


        /// <summary>Ms. Lumi's line as Arrange opens. It names what just happened before it
        /// asks for anything, which is the whole job of this beat: the learner arrives here
        /// straight off a run, and the screen otherwise begins with an instruction and no
        /// acknowledgement that they just finished the race.
        ///
        /// "S.W.B.S.T" is spelled with stops because the five slots on screen are five
        /// separate words - the learner is matching letters to labels, not reading an acronym
        /// they have never heard said aloud. "Put the story parts in order" rather than the
        /// prototype's "organize the story elements": "organize" and "element" are the
        /// designer's words for this screen, and ArrangeTitle/ArrangeIntroStatus already
        /// teach the learner's ("story parts"). One vocabulary per screen.</summary>
        public const string ArrangeLumiIntro =
            "Great running! Now put the story parts in S.W.B.S.T order.";
        public const string UndoLabel = "UNDO";
        /// <summary>"Verify" is a designer's word; the learner is checking their work.</summary>
        public const string VerifyLabel = "CHECK ORDER";
        /// <summary>The whole instruction has to survive one reading under time pressure, so it
        /// is two short taps in the order they happen. "Its place in the order" asked a
        /// 9-year-old to hold an abstract noun phrase; the empty slot they are aiming at is
        /// right there on screen, wearing its SWBST word.</summary>
        public const string ArrangeIntroStatus = "Tap a story part, then tap where it goes.";
        /// <summary>"Slot" is a word this screen never teaches and nothing else in the game
        /// uses — said in the vocabulary the learner already has ("story parts") instead.</summary>
        public const string ArrangeFillFirst = "Put all 5 parts in first!";
        public const string ArrangeHintPrefix = "Hint: ";
        /// <summary>"Locked in" meant CORRECT here while "Locked" everywhere else in the game
        /// (cards, sessions) means "you cannot have this yet" — one word, two opposite feelings,
        /// on the screen a stuck learner reads most carefully. And "the GREEN ones" made colour
        /// the only channel: a red-green colour-blind learner (~1-2 children in this study) could
        /// not tell which parts were already right, on the one screen where being stuck is
        /// possible. A correct slot is also physically locked — it stops responding to taps — so
        /// "already in place" names something every learner can perceive.</summary>
        public const string ArrangeAlmost = "Almost! The parts already in place are right — try the others again.";

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
            // These five lines are the game's DEFINITIONS of the framework — they are also the
            // Arrange hint. Phrasal verbs were doing the defining ("got in the way", "turned
            // out"), which is the hardest kind of English for a second-language reader, so a
            // learner who did not know the phrase learned nothing from the tip.
            "SOMEBODY is who the story is about.",
            "WANTED is what the character wanted.",
            "BUT is the problem the character had.",
            "SO is what the character did about it.",
            "THEN is how the story ended.",
        };
    }
}
