using SummaRace.Constants;
using SummaRace.Core;
using SummaRace.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SummaRace.Features.Summary
{
    /// <summary>
    /// Type ONE summary sentence with the arranged SWBST parts as reference
    /// (TDD §10.3). Checks are light and warm: at most 2 nudges, then the
    /// summary is always accepted — the app encourages, it never grades.
    /// </summary>
    public class SummaryController : MonoBehaviour
    {
        [SerializeField] private TMP_Text referenceText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private TMP_InputField summaryInput;
        [SerializeField] private Button submitButton;
        [SerializeField] private TMP_Text nudgeText;

        [Tooltip("Ms. Lumi's own speech bubble at the top of the screen. Its sprite and colour " +
                 "are COPIED onto the sentence-frame bubble so the two read as two balloons " +
                 "from the same character. Optional: with nothing wired the frame keeps a " +
                 "plain cream backing instead, which is still better than bare text.")]
        [SerializeField] private Image hintBubbleSource;

        [Header("Labels (set from GameText)")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text placeholderText;
        [SerializeField] private TMP_Text submitLabel;

        [Header("Keyboard")]
        // Closes the on-screen keyboard. Optional: with nothing wired the controller builds a
        // small kit pill for it at runtime (see EnsureDoneTypingChip).
        [SerializeField] private Button doneTypingButton;

                private static readonly Color ChipText = Theme.Paper;
        private static readonly Color ChipFallback = Theme.Alpha(Theme.Navy, 0.95f);

        // --- sentence-frame speech bubble (see BuildHintBubble) -------------------------
        private const string HintBubbleName = "HintBubble";
        /// <summary>Padding around the frame text, in reference pixels.</summary>
        private const float HintBubblePadX = 44f;
        private const float HintBubblePadY = 30f;
        /// <summary>Where the tail sits across the bubble's width. Kept left, under Ms. Lumi's
        /// own column (she anchors x 0.03-0.19), so it points at her and not at empty wall.</summary>
        private const float HintTailX = 0.12f;
        private const float HintTailSize = 34f;

        /// <summary>Padding around the nudge line's backing pill, in reference pixels. Wider than
        /// tall: the nudge band (y 0.265-0.32) is only 106 px, so vertical padding would eat the
        /// text's own room, while the band spans x 0.03-0.97 with margin to spare either side.</summary>
        private const float NudgePadX = 40f;
        private const float NudgePadY = 14f;

        // --- remaining-characters cue (see EnsureCharCounter) ---------------------------
        /// <summary>Remaining characters at which the counter appears. Deliberately late: a
        /// counter that is on screen from the first keystroke turns a writing task into a
        /// budgeting one, and 28 of the 30 stories cannot get near the cap at all. Local rather
        /// than in GameRules for the same reason the layout constants above are — it is a
        /// property of THIS screen's cue, not a rule of the game.</summary>
        private const int CharCounterShowAt = 40;

        /// <summary>Remaining characters at which the counter turns amber. Attention, never a
        /// punishment (Theme.AmberWarn exists for exactly this distinction).</summary>
        private const int CharCounterUrgentAt = 15;

        private StoryData _story;
        private int _nudgeCount;

        /// <summary>Navy pill drawn behind the nudge line, shown only while a nudge is up.</summary>
        private GameObject _nudgeBacking;

        private GameObject _charCounterRoot;
        private TMP_Text _charCounter;

        /// <summary>Latches the one sound the cap gets, so holding a key down at the limit does
        /// not machine-gun it.</summary>
        private bool _capAnnounced;

        /// <summary>Guards the hand-off. Two taps on SUBMIT used to raise SummarySubmitted
        /// twice and ask for the Results scene twice — the second load is swallowed by
        /// SceneLoader, but the study log took the duplicate.</summary>
        private bool _submitted;

        private bool _typing;          // input field focused = keyboard up on Android
        private float _hintRestRight;  // hint's own anchorMax.x, restored when typing ends

        private void Start()
        {
            _story = SummaRace.Core.GameManager.Instance != null ? SummaRace.Core.GameManager.Instance.CurrentStory : null;
                        if (_story == null) _story = StoryLoader.Load("s01_easy"); // editor-direct fallback
            if (_story == null)
            {
                // Without a story the reference list and the submit button would both be
                // dead, stranding the learner mid-loop — so hand them back to the cards
                // instead of returning into an empty screen (TDD §13).
                Debug.LogError("Summary: no story — returning to Story Select.");
                SceneLoader.Go(SceneNames.StorySelect);
                return;
            }

            // See ArrangeController: this screen had no music either. No-ops when the loop is
            // already running, so arriving from Arrange is free.
            // Mid-run, so there is no legal exit: the race is already logged and the
            // ladder is not finished. Android BACK says so warmly instead of doing
            // nothing (owner, 2026-08-22) - see Core/BackButtonGuard.
            SummaRace.Core.BackButtonGuard.RegisterBlocked(GameText.BackBlockedSummary);

            if (AudioManager.Instance != null) AudioManager.Instance.PlayMusic(AudioKeys.MusicMenu);

            // Ms. Lumi reacts here now (see MsLumiReactor.AttachBadge). Null-safe and pool-safe:
            // absent object or absent badge art simply leaves the screen as it was.
            SummaRace.UI.MsLumiReactor.AttachBadge();

            // Focus scrim over the backdrop art — the same block ArrangeController carries, and
            // the two screens must agree (owner device request, 2026-08-23). Inserted directly
            // above the Sky image so every card and control stays bright over a quieted room.
            var sky = GameObject.Find("Sky");
            if (sky != null && sky.transform.parent != null)
            {
                var scrim = new GameObject("FocusScrim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var srt = (RectTransform)scrim.transform;
                srt.SetParent(sky.transform.parent, false);
                srt.SetSiblingIndex(sky.transform.GetSiblingIndex() + 1);
                srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
                srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
                var simg = scrim.GetComponent<Image>();
                simg.color = Theme.Alpha(Theme.Ink, 0.30f);
                simg.raycastTarget = false;
            }

            if (referenceText != null && _story.elements != null)
            {
                // Ink, not the raw palette. This card is the kit's cream "Daily Reward
                // pannel" (0.971, 0.923, 0.829), and the five SWBST hues straight off the
                // palette read at 2.81 / 1.99 / 2.89 / 1.79 / 2.88:1 on it — WANTED and SO
                // are barely visible, and this list is what the learner writes their summary
                // FROM. InkForIndex keeps each element's own hue and lands them at
                // 7.13 / 5.61 / 7.31 / 5.18 / 7.25:1, all clear of WCAG AA's 4.5:1.
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < _story.elements.Length; i++)
                    sb.AppendLine($"{i + 1}. <color=#{SwbstPalette.InkHexForIndex(i)}><b>{_story.elements[i].type}</b></color>: {_story.elements[i].correct}");
                referenceText.text = sb.ToString();

                // Overflow guard, not a fix: measured across all 30 stories this list needs
                // 5-6 wrapped lines (218-262 px, worst case 306 px with the trailing break)
                // in a 389 px box, so nothing clips today. But autosizing was OFF with the
                // size pinned at 32, so the FIRST story whose correct answer grows past
                // ~56 characters would have spilled its last SWBST part off the card
                // silently. Max stays 32 (nothing renders differently today); the floor of
                // 26 is the smallest this audience should ever be asked to read.
                referenceText.fontSizeMax = referenceText.fontSize;
                referenceText.fontSizeMin = 26f;
                referenceText.enableAutoSizing = true;
            }

            if (titleText != null) titleText.text = GameText.SummaryTitle;
            if (placeholderText != null)
            {
                // The ghost is built from THIS story's own Somebody and Wanted, so the frame
                // the learner is copying is already about the story they just ran. It is a
                // placeholder and nothing else - TMP draws it only while the field is empty,
                // it cannot be submitted, and the first keystroke removes it (L6: the child
                // produces every word). It also breaks off at "but...", leaving the three
                // parts the summary is really judged on entirely to them.
                placeholderText.text = _story.elements != null && _story.elements.Length >= 2
                    ? GameText.SummaryGhost(_story.elements[0].correct, _story.elements[1].correct)
                    : GameText.SummaryPlaceholder;
            }
            if (submitLabel != null) submitLabel.text = GameText.SubmitLabel;
            if (hintText != null)
            {
                hintText.text = GameText.SummaryHint;
                BuildHintBubble();
            }
            if (nudgeText != null)
            {
                nudgeText.text = "";
                EnsureNudgeBacking();
            }
            if (summaryInput != null)
            {
                summaryInput.characterLimit = GameRules.SummaryMaxChars;
                // A nudge that stays on screen while the learner is already fixing the
                // sentence reads as "still wrong" and is the one thing here that could feel
                // like being told off. It clears the moment they start typing again.
                summaryInput.onValueChanged.AddListener(OnSummaryChanged);
            }
            if (submitButton != null) submitButton.onClick.AddListener(OnSubmit);

            // Title and sentence-frame read aloud. The frame ("Somebody wanted ___, but ___...")
            // is the instruction for HOW to write a summary, not any part of this story's
            // answer — the five SWBST parts on the reference card are deliberately NOT spoken,
            // because reading them back is the task. Queued behind the loading tip.
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayVoice(AudioKeys.VoSummaryTitle, true);
                AudioManager.Instance.PlayVoice(AudioKeys.VoSummaryHint, true);
            }

            EnsureTipsBlock();
            EnsureCharCounter();
            EnsureDoneTypingChip();
            // Once, with whatever is already in the box (nothing, in every real run) so the
            // counter starts in a state that matches the field rather than merely hidden.
            if (summaryInput != null) RefreshCharCounter(summaryInput.text);
            if (doneTypingButton != null)
            {
                // Through OnDoneTyping, not StopTyping directly: the tap needs its own click, and
                // StopTyping is also called from Accept(), which already plays its own sound.
                doneTypingButton.onClick.AddListener(OnDoneTyping);
                doneTypingButton.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Puts the sentence frame in a SPEECH BUBBLE with a tail pointing up at Ms. Lumi, so
        /// the instruction comes FROM the teacher instead of floating on the wallpaper.
        ///
        /// WHY. The title ("Write your summary!") was already inside her bubble at the top, but
        /// the part that actually tells a child what to do — "Somebody wanted ___, but ___, so
        /// ___, then ___" — sat detached in the middle of the screen as loose grey text on a
        /// painted room. So the ASK looked like teaching and the INSTRUCTION looked like
        /// decoration, which is exactly backwards.
        ///
        /// It is built here rather than moved into her top bubble on purpose: the frame is most
        /// useful directly above the box the learner types into, and relocating it to the top of
        /// the screen would have traded usability for theme. A second balloon gets both.
        ///
        /// The bubble is a SIBLING inserted at the hint's own index — uGUI draws a parent's
        /// graphic before its children, so a child would cover the words it is meant to sit
        /// behind. Idempotent, and null-safe at every step.
        /// </summary>
        private void BuildHintBubble()
        {
            var host = hintText.rectTransform;
            var parent = host.parent as RectTransform;
            if (parent == null) return;
            if (parent.Find(HintBubbleName) != null) return;   // already built

            var go = new GameObject(HintBubbleName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.SetSiblingIndex(host.GetSiblingIndex());

            var bubble = go.AddComponent<Image>();
            if (hintBubbleSource != null)
            {
                bubble.sprite = hintBubbleSource.sprite;
                bubble.type = hintBubbleSource.type;
                bubble.color = hintBubbleSource.color;
            }
            else
            {
                bubble.color = Theme.Cream;
            }
            bubble.raycastTarget = false;   // the box below it must stay tappable

            var rt = (RectTransform)go.transform;
            rt.anchorMin = host.anchorMin;
            rt.anchorMax = host.anchorMax;
            rt.pivot = host.pivot;
            rt.anchoredPosition = host.anchoredPosition;
            rt.sizeDelta = host.sizeDelta + new Vector2(HintBubblePadX, HintBubblePadY);

            // The tail: a plain square turned 45 degrees, half of it standing above the
            // bubble's top edge, near the left where Ms. Lumi is (she sits at x 0.03-0.19).
            // Deliberately not a drawn sprite — a rotated quad in the bubble's own colour reads
            // as a tail at this size and cannot go missing the way a new art file can.
            var tailGo = new GameObject("Tail", typeof(RectTransform));
            tailGo.transform.SetParent(go.transform, false);

            var tail = tailGo.AddComponent<Image>();
            tail.color = bubble.color;
            tail.raycastTarget = false;

            var trt = tail.rectTransform;
            trt.anchorMin = new Vector2(HintTailX, 1f);
            trt.anchorMax = new Vector2(HintTailX, 1f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(HintTailSize, HintTailSize);
            trt.anchoredPosition = new Vector2(0f, -HintTailSize * 0.32f);
            trt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            tailGo.transform.SetAsFirstSibling();   // behind the bubble's own rounded corner

            // Body copy on cream is warm brown everywhere else in this game (Theme.TextBrown,
            // 8.4:1). The hint was a cool grey chosen against the painted wall it used to sit
            // on, and that wall is no longer what is behind it.
            hintText.color = Theme.TextBrown;
        }

        private void OnSubmit()
        {
            if (_submitted) return;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);

            string text = summaryInput != null ? summaryInput.text.Trim() : "";

            // Light checks (GDD §4.5) — nudge at most twice, then accept.
            if (_nudgeCount < GameRules.SummaryMaxNudges && !PassesLightChecks(text))
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxNotQuite);
                SetNudge(GameText.SummaryNudges[NudgeIndexFor(text)]);
                _nudgeCount++;
                return;
            }

            Accept(text);
        }

        private void ClearNudge()
        {
            if (nudgeText != null && nudgeText.text.Length > 0) SetNudge("");
        }

        /// <summary>
        /// Writes the nudge and shows or hides its backing together, so the pill can never be
        /// left on screen empty and the words can never be left on screen bare.
        /// </summary>
        private void SetNudge(string message)
        {
            if (nudgeText != null) nudgeText.text = message;
            if (_nudgeBacking != null) _nudgeBacking.SetActive(!string.IsNullOrEmpty(message));
        }

        /// <summary>Every keystroke: clear a stale nudge, and keep the room-left cue honest.</summary>
        private void OnSummaryChanged(string value)
        {
            ClearNudge();
            RefreshCharCounter(value);
        }

        /// <summary>
        /// THE CAP WAS COMPLETELY SILENT. At GameRules.SummaryMaxChars the field simply stops
        /// accepting keys — no counter, no colour, no sound, nothing on screen changes — so to a
        /// nine-year-old the tablet has broken mid-sentence. It is not hypothetical. Measured
        /// across all 30 stories, the five SWBST `correct` lines concatenated with NO connecting
        /// words at all come to more than 200 characters in two of them (worst case s04_hard at
        /// 244) and to more than 180 in thirteen — so once a child adds "wanted", "but", "so",
        /// "then" and punctuation, a large part of the corpus can reach the cap while doing
        /// exactly what this screen asked.
        ///
        /// ⚠️ If GameRules.SummaryMaxChars is ever raised, ResultsController's summary card must
        /// be re-measured with it — its padding and font floor were sized against exactly 200.
        ///
        /// This is a CUE, not a limit and not a target. It says nothing about the sentence, only
        /// about the box: it stays hidden until the last stretch, warms to amber near the end,
        /// and at zero says the box is full rather than that the learner is wrong.
        /// </summary>
        private void RefreshCharCounter(string value)
        {
            if (_charCounterRoot == null || _charCounter == null) return;

            int used = value != null ? value.Length : 0;
            int remaining = Mathf.Max(0, GameRules.SummaryMaxChars - used);
            bool show = remaining <= CharCounterShowAt;

            if (_charCounterRoot.activeSelf != show) _charCounterRoot.SetActive(show);
            if (!show)
            {
                _capAnnounced = false;   // re-arms if they delete back below the cap and refill
                return;
            }

            _charCounter.text = GameText.SummaryCharsLeft(remaining);
            // Amber on the navy pill measures 7.3:1 — well past AA, and amber rather than red
            // because running out of room is not a mistake (D7).
            _charCounter.color = remaining <= CharCounterUrgentAt ? Theme.AmberWarn : ChipText;

            if (remaining == 0 && !_capAnnounced)
            {
                _capAnnounced = true;
                // Deliberately SfxPop, the neutral "something appeared" sound, and NOT
                // SfxNotQuite: the keys stopping is a fact about the box, and the nudge sound
                // would tell a child their sentence had been judged.
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxPop);
            }
        }

        /// <summary>
        /// Picks the nudge that matches what actually stopped the sentence, instead of walking
        /// the list in order: a learner who wrote three good lines but never named the Somebody
        /// used to be told "try writing a little more", which is advice for a different problem
        /// and cannot be acted on. The nudge count the study logs is unchanged.
        ///
        /// THERE ARE THREE WAYS TO FAIL AND THERE WERE ONLY TWO NUDGES. The clamp meant the
        /// third mode — wrote plenty, named the Somebody, but used three sentences — collapsed
        /// onto nudge 1, which told the child to "start with the Somebody" they had already
        /// named. Being corrected for something you did right is worse than no feedback at all,
        /// and it burned one of the only two nudges the screen is allowed.
        ///
        /// Ordered by what the learner can act on first: with fewer than SummaryMinWords there
        /// is nothing else worth judging; a missing protagonist is a gap in the CONTENT; and the
        /// sentence count is the last thing left once the content is there. The clamp stays so
        /// that a GameText.SummaryNudges array with only two entries degrades to the previous
        /// behaviour instead of throwing on a screen the learner cannot leave.
        /// </summary>
        private int NudgeIndexFor(string text)
        {
            int index;
            if (!HasEnoughWords(text)) index = 0;            // wrote too little
            else if (!MentionsSomebody(text)) index = 1;     // never said who it is about
            else index = 2;                                  // more than one sentence
            return Mathf.Clamp(index, 0, GameText.SummaryNudges.Length - 1);
        }

        /// <summary>
        /// The accept test, expressed as the same three predicates NudgeIndexFor asks about, so
        /// the two cannot disagree about why a sentence was held back. Never a score: it decides
        /// whether to nudge, and after GameRules.SummaryMaxNudges the answer stops being asked.
        /// </summary>
        private bool PassesLightChecks(string text)
        {
            return HasEnoughWords(text) && IsOneSentence(text) && MentionsSomebody(text);
        }

        /// <summary>At least a few words of effort.</summary>
        private static bool HasEnoughWords(string text)
        {
            return !string.IsNullOrWhiteSpace(text)
                && text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries).Length
                       >= GameRules.SummaryMinWords;
        }

        /// <summary>One sentence: no sentence break before the final punctuation.</summary>
        private static bool IsOneSentence(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;
            string body = text.TrimEnd('.', '!', '?', ' ');
            return body.IndexOfAny(new[] { '.', '!', '?' }) < 0;
        }

        /// <summary>
        /// Does the sentence name the story's Somebody?
        ///
        /// TWO DEFECTS, and both of them polluted a logged variable. The old test was
        /// <c>word.Length &gt; 2 &amp;&amp; lower.Contains(word)</c>, so:
        ///
        /// (1) SUBSTRING, not word. "the" from "The Giant" matched "there", "then", "other",
        ///     "together", "brother"; "and" matched "sand", "handed", "understand"; "her"
        ///     matched "there", "other", "where". Nine of the thirty Somebody lines contain one
        ///     of those four words, and on every one of them ANY sentence carrying the substring
        ///     passed this check. <c>nudgeCount</c> is exported per run, so part of what it was
        ///     measuring was WHICH STORY THE CHILD HAPPENED TO DRAW.
        /// (2) The length floor was 3 characters (<c>&gt; 2</c>), which is exactly what let those
        ///     four function words be candidates in the first place.
        ///
        /// Now: whole-word matching, and function words are not candidates. Punctuation does not
        /// break a match, so "Molly's" and "Molly," still name Molly.
        ///
        /// If nothing survives the filter — a Somebody made entirely of short function words —
        /// the check PASSES. Nudging a child twice for something no sentence of theirs could
        /// satisfy is the failure mode this is guarding, the same reasoning as the
        /// no-elements case below (GDD D7, never punish). Verified against all 30 stories:
        /// every one leaves at least one candidate word today, so this is a guard, not a path.
        /// </summary>
        private bool MentionsSomebody(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            // A story with no elements cannot supply a Somebody to look for, so this could only
            // ever fail. Broken content passes instead (GDD D7).
            if (_story == null || _story.elements == null || _story.elements.Length == 0) return true;
            var somebody = _story.elements[0] != null ? _story.elements[0].correct : null;
            if (string.IsNullOrWhiteSpace(somebody)) return true;

            string lower = text.ToLowerInvariant();
            bool anyCandidate = false;
            foreach (var raw in somebody.ToLowerInvariant().Split(' '))
            {
                string word = TrimToWord(raw);
                if (word.Length == 0 || IsSomebodyStopWord(word)) continue;
                anyCandidate = true;
                if (ContainsWholeWord(lower, word)) return true;
            }
            return !anyCandidate;
        }

        /// <summary>
        /// Function words that must never stand in for the Somebody. All four are three
        /// characters, so the length rule below already excludes them — they are named anyway
        /// because they are the ones that actually occur in the thirty Somebody lines, so a
        /// later edit that relaxes the length rule cannot quietly re-open the hole.
        /// </summary>
        private static readonly string[] SomebodyStopWords = { "and", "the", "his", "her" };

        private static bool IsSomebodyStopWord(string word)
        {
            if (word.Length <= 3) return true;
            foreach (var stop in SomebodyStopWords)
                if (word == stop) return true;
            return false;
        }

        /// <summary>Strips punctuation off both ends of a word — the Somebody lines carry commas
        /// ("Maggie, Travis, and Lucy") and the learner's typing carries everything.</summary>
        private static string TrimToWord(string raw)
        {
            int start = 0, end = raw.Length;
            while (start < end && !char.IsLetterOrDigit(raw[start])) start++;
            while (end > start && !char.IsLetterOrDigit(raw[end - 1])) end--;
            return raw.Substring(start, end - start);
        }

        /// <summary>
        /// <paramref name="needle"/> as a WHOLE word inside <paramref name="haystack"/> (both
        /// already lowercased). A letter or digit on either side breaks the match; punctuation
        /// does not, so "Molly's" and "(Molly)" both count. Scans past a failed boundary rather
        /// than giving up, so a later legitimate occurrence is still found.
        /// </summary>
        private static bool ContainsWholeWord(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle)) return false;

            int from = 0;
            while (from <= haystack.Length - needle.Length)
            {
                int at = haystack.IndexOf(needle, from, System.StringComparison.Ordinal);
                if (at < 0) return false;

                bool leftClear = at == 0 || !char.IsLetterOrDigit(haystack[at - 1]);
                int end = at + needle.Length;
                bool rightClear = end >= haystack.Length || !char.IsLetterOrDigit(haystack[end]);
                if (leftClear && rightClear) return true;

                from = at + 1;
            }
            return false;
        }

        private void Accept(string text)
        {
            _submitted = true;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxCorrect);
            if (SummaRace.Core.GameManager.Instance != null) SummaRace.Core.GameManager.Instance.LastSummaryText = text;

            // The Android keyboard belongs to the field, not to the scene: left open it rides
            // over the Results screen, where there is nothing to type and no way to dismiss it
            // except the system back gesture.
            StopTyping();

            EventBus.Raise(new SummarySubmitted { text = text, nudgeCount = _nudgeCount });
            SceneLoader.Go(SceneNames.Results);
        }

        // ---------- getting out from under the on-screen keyboard ----------

        /// <summary>
        /// On a portrait Android tablet the soft keyboard covers roughly the bottom 45% of the
        /// screen — which is exactly where SUBMIT (y 0.25-0.335) and the nudge line (0.355-0.41)
        /// live. A learner who does not know the keyboard's own "done" key can type a perfect
        /// summary and then see no way forward, on the one screen where forward is the only
        /// exit. So while the field is focused a DONE TYPING chip sits above it, well clear of
        /// the keyboard, and closing the keyboard restores the screen the learner already knows.
        ///
        /// Shown by focus rather than by TouchScreenKeyboard.visible so the behaviour also
        /// appears in an editor playtest, where no soft keyboard exists at all.
        /// </summary>
        private void Update()
        {
            if (_story == null) return; // Start already handed the learner back to Story Select
            bool typing = summaryInput != null && summaryInput.isFocused && !_submitted;
            if (typing == _typing) return;
            _typing = typing;

            if (doneTypingButton != null) doneTypingButton.gameObject.SetActive(typing);

            // The chip shares the hint's row, so the hint gives up its right end while the
            // chip is there and takes it back afterwards (it wraps to two lines and still
            // fits its band). Moving the hint aside beats hiding it: it is the sentence
            // frame the learner is writing from.
            if (hintText != null)
            {
                var rect = hintText.rectTransform;
                var max = rect.anchorMax;
                max.x = typing ? Mathf.Min(_hintRestRight, 0.63f) : _hintRestRight;
                rect.anchorMax = max;
            }
        }

        /// <summary>The learner tapping DONE TYPING — the only tap on this screen that answered
        /// with no sound of its own.</summary>
        private void OnDoneTyping()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);
            StopTyping();
        }

        /// <summary>
        /// Closes the keyboard. Note the chip may well never receive its click: pressing it
        /// takes selection off the input field, which deactivates it — and that IS the close.
        /// Whether the tap lands as a click or only as a focus change, the learner gets the
        /// same result, so nothing here needs to defend against the chip vanishing mid-tap.
        /// </summary>
        private void StopTyping()
        {
            if (summaryInput != null) summaryInput.DeactivateInputField();
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }

        /// <summary>
        /// <summary>
        /// The two tips under SUBMIT. They answer the only two questions a learner has in
        /// front of an empty box - how much do I write, and is one sentence really enough -
        /// and they are guidance, never a grade: the app does not score a summary (L6).
        ///
        /// Placed in the one empty band this screen has. Measured from the scene: SUBMIT
        /// bottoms out at y 0.13 and nothing else lives below it, so 0.02-0.11 is free and
        /// nothing has to move. It sits BELOW the input on purpose - the Android keyboard
        /// covers this band while typing, and these are pre-writing tips, so losing them at
        /// the moment the learner starts writing is correct rather than a defect. Anything
        /// they need mid-sentence (the frame, the reference list, DONE TYPING) is already
        /// above the keyboard line.
        ///
        /// On the navy pill for the same reason the other chips are: this screen's backdrop
        /// is a photographic sky, and Paper on Navy measures 12.9:1 whatever is behind it,
        /// where slate type straight on the sky depends on the pixel it lands over.
        /// </summary>
        private void EnsureTipsBlock()
        {
            var tips = GameText.SummaryTips;
            if (tips == null || tips.Length == 0) return;

            var root = ResolveSceneCanvas();
            if (root == null) return;

            var panelGo = new GameObject("TipsBlock", typeof(RectTransform));
            panelGo.transform.SetParent(root, false);
            var rect = (RectTransform)panelGo.transform;
            rect.anchorMin = new Vector2(0.05f, 0.020f);
            rect.anchorMax = new Vector2(0.95f, 0.110f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = panelGo.AddComponent<Image>();
            var pill = Resources.Load<Sprite>("UI/bar_bg"); // navy 9-sliced pill
            if (pill != null) { image.sprite = pill; image.type = Image.Type.Sliced; }
            else image.color = ChipFallback;
            image.raycastTarget = false;   // never intercepts a tap aimed at SUBMIT

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(panelGo.transform, false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            // Body face, not SUBMIT's Fredoka: these are two sentences to read, and the
            // reference list beside them is already set in the body face.
            if (hintText != null && hintText.font != null) label.font = hintText.font;
            label.text = string.Join(System.Environment.NewLine, tips);
            label.fontSize = 26f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 22f;       // the readability audit's acuity floor
            label.fontSizeMax = 26f;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.color = ChipText;
            label.raycastTarget = false;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(18f, 8f);
            labelRect.offsetMax = new Vector2(-18f, -8f);

            panelGo.AddComponent<SummaRace.UI.PanelIntro>();
        }

        /// <summary>
        /// A backing pill behind the nudge line.
        ///
        /// The nudge is the ONLY feedback this screen ever gives, and it was drawn straight onto
        /// the painted sky backdrop at about 2.15:1 — under WCAG's 4.5:1 floor for body text,
        /// and varying with whichever pixel of sky it happened to land over. So the one sentence
        /// a struggling learner is meant to act on was the least readable thing on the screen.
        ///
        /// Same construction as EnsureTipsBlock, for the same reason: Paper on the navy pill is
        /// 12.9:1 whatever the backdrop does. NOTE the sprite at Resources/UI/bar_bg is ITSELF
        /// navy and Image.color MULTIPLIES it — tinting it cream yields near-black. It is left
        /// at the default white so the navy shows through, which is the pairing already proven
        /// by the tips block and the DONE TYPING chip on this screen.
        ///
        /// A SIBLING inserted at the nudge's own index, never a child: uGUI draws a parent's
        /// graphic before its children, so a child would cover the words it is meant to sit
        /// behind (the same trap the hint bubble above documents). Hidden until there is
        /// something to say — see SetNudge.
        /// </summary>
        private void EnsureNudgeBacking()
        {
            if (_nudgeBacking != null) return;

            var host = nudgeText.rectTransform;
            var parent = host.parent as RectTransform;
            if (parent == null) return;

            _nudgeBacking = new GameObject("NudgeBacking", typeof(RectTransform));
            _nudgeBacking.transform.SetParent(parent, false);
            _nudgeBacking.transform.SetSiblingIndex(host.GetSiblingIndex());

            var image = _nudgeBacking.AddComponent<Image>();
            var pill = Resources.Load<Sprite>("UI/bar_bg");   // navy 9-sliced pill
            if (pill != null) { image.sprite = pill; image.type = Image.Type.Sliced; }
            else image.color = ChipFallback;                  // no sprite: flat navy, still legible
            image.raycastTarget = false;                      // never intercepts a tap aimed at SUBMIT

            var rt = (RectTransform)_nudgeBacking.transform;
            rt.anchorMin = host.anchorMin;
            rt.anchorMax = host.anchorMax;
            rt.pivot = host.pivot;
            rt.anchoredPosition = host.anchoredPosition;
            rt.sizeDelta = host.sizeDelta + new Vector2(NudgePadX, NudgePadY);

            // Cream on navy. The nudge inherited a colour chosen for the sky it used to sit on.
            nudgeText.color = ChipText;

            _nudgeBacking.SetActive(false);   // nothing to say yet
        }

        /// <summary>
        /// The room-left cue (see RefreshCharCounter for WHY it exists).
        ///
        /// WHERE, and why it is the only place it can go. Measured off Summary.unity, the
        /// portrait screen is full: SpeechBubble 0.885-0.962, ReferenceCard 0.66-0.88, the hint
        /// row 0.60-0.655, the input 0.33-0.585, the nudge 0.265-0.32, SUBMIT 0.13-0.225 and the
        /// tips block 0.02-0.11. The only gaps are a few reference pixels wide.
        ///
        /// It also has to be ABOVE the soft keyboard, which covers roughly the bottom 45% of a
        /// portrait tablet — a counter the learner cannot see WHILE TYPING is no counter at all,
        /// which rules out every band under y 0.45, including the input's own bottom edge. That
        /// leaves the hint row, so this takes its right-hand end and the DONE TYPING chip
        /// narrows to make room (the hint's typing clamp is 0.63, leaving a 16 px gutter, so the
        /// sentence frame's wrapping is untouched). Both are only ever on screen while typing,
        /// and neither can now sit on the other.
        ///
        /// Two lines inside one small pill: the number large, the word small under it. "40" on
        /// its own is a riddle, and there is no horizontal room for "40 characters left".
        /// </summary>
        private void EnsureCharCounter()
        {
            if (_charCounterRoot != null) return;

            var root = ResolveSceneCanvas();
            if (root == null) return;

            _charCounterRoot = new GameObject("CharCounter", typeof(RectTransform));
            _charCounterRoot.transform.SetParent(root, false);
            var rect = (RectTransform)_charCounterRoot.transform;
            rect.anchorMin = new Vector2(0.645f, 0.598f);
            rect.anchorMax = new Vector2(0.735f, 0.652f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();

            var image = _charCounterRoot.AddComponent<Image>();
            var pill = Resources.Load<Sprite>("UI/bar_bg");   // navy 9-sliced pill, as above
            if (pill != null) { image.sprite = pill; image.type = Image.Type.Sliced; }
            else image.color = ChipFallback;
            image.raycastTarget = false;   // it is a readout, not a control

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(_charCounterRoot.transform, false);
            _charCounter = labelGo.AddComponent<TextMeshProUGUI>();
            // SUBMIT's face (Fredoka) — this is a number to glance at, not prose to read.
            if (submitLabel != null && submitLabel.font != null) _charCounter.font = submitLabel.font;
            _charCounter.text = "";
            _charCounter.enableAutoSizing = true;
            // Floor at the readability audit's 22 pt acuity minimum for the 10.1" target; the
            // ceiling lets a one- or two-digit number fill the pill. Wrapping is ON because the
            // string is deliberately two lines.
            _charCounter.fontSizeMin = 22f;
            _charCounter.fontSizeMax = 34f;
            _charCounter.alignment = TextAlignmentOptions.Center;
            _charCounter.textWrappingMode = TextWrappingModes.Normal;
            _charCounter.color = ChipText;
            _charCounter.raycastTarget = false;

            var labelRect = _charCounter.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 4f);
            labelRect.offsetMax = new Vector2(-8f, -4f);

            _charCounterRoot.SetActive(false);   // silent until the cap is in sight
        }

        /// Builds the chip when the scene has no object for it. Serialized wiring wins if it
        /// ever gains one; built here so the escape hatch ships without a scene edit (same
        /// idiom as SceneLoader's overlay and the race briefing).
        /// </summary>
        private void EnsureDoneTypingChip()
        {
            if (hintText != null) _hintRestRight = hintText.rectTransform.anchorMax.x;
            if (doneTypingButton != null) return;

            var root = ResolveSceneCanvas();
            if (root == null) return;

            var chipGo = new GameObject("DoneTypingChip", typeof(RectTransform));
            chipGo.transform.SetParent(root, false);
            var rect = (RectTransform)chipGo.transform;
            // Level with the hint, right-hand end: above the input box and far above anything
            // the keyboard can reach. Left edge 0.66 -> 0.74 to seat the room-left counter
            // beside it (EnsureCharCounter owns 0.645-0.735); at 0.23 wide the pill still gives
            // "DONE TYPING" 228 px of inner width against roughly 150 px at the 22 pt floor, so
            // the NoWrap label has not lost any headroom.
            rect.anchorMin = new Vector2(0.74f, 0.598f);
            rect.anchorMax = new Vector2(0.97f, 0.652f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();

            var image = chipGo.AddComponent<Image>();
            var pill = Resources.Load<Sprite>("UI/bar_bg"); // navy 9-sliced pill, the HUD's own language
            if (pill != null) { image.sprite = pill; image.type = Image.Type.Sliced; }
            else image.color = ChipFallback;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(chipGo.transform, false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            // Borrow SUBMIT's face (Fredoka) rather than loading one — same scene, already loaded.
            if (submitLabel != null && submitLabel.font != null) label.font = submitLabel.font;
            label.text = GameText.SummaryDoneTyping;
            label.fontSize = 26f;
            label.enableAutoSizing = true;
            // Floor raised 16 -> 22 (2026-08-19). 22pt is the measured acuity floor for the
            // 10.1" target device in SummaRace_Readability_And_Accessibility_Audit.md 2.1;
            // at 16pt this chip was ~11.9 arcmin, below the 16' minimum the audit sets.
            // The audit's blanket advice is 26, which here would equal fontSizeMax and so
            // disable shrinking entirely — and these chips are NoWrap, so a longer string
            // would then spill outside the pill rather than shrink. 22 clears the floor and
            // keeps a little headroom, which is the safer trade while no one can run a
            // portrait render to catch an overflow.
            label.fontSizeMin = 22f;
            label.fontSizeMax = 26f;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.color = ChipText;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 6f);
            labelRect.offsetMax = new Vector2(-10f, -6f);

            chipGo.AddComponent<SummaRace.UI.ButtonSquash>();
            doneTypingButton = chipGo.AddComponent<Button>();
            doneTypingButton.targetGraphic = image;
        }

        /// <summary>
        /// The canvas this scene's own UI lives on. A blind FindAnyObjectByType would happily
        /// return SceneLoader's persistent FadeCanvas ([Core], DontDestroyOnLoad, alpha 0) and
        /// the chip would be built invisible on the loading overlay.
        /// </summary>
        private Transform ResolveSceneCanvas()
        {
            var canvas = submitButton != null ? submitButton.GetComponentInParent<Canvas>() : null;
            if (canvas == null && summaryInput != null) canvas = summaryInput.GetComponentInParent<Canvas>();
            if (canvas != null) return canvas.rootCanvas.transform;

            foreach (var candidate in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (candidate.gameObject.scene == gameObject.scene)
                    return candidate.rootCanvas.transform;

            Debug.LogWarning("Summary: no scene canvas found — DONE TYPING chip not built.");
            return null;
        }
    }
}
