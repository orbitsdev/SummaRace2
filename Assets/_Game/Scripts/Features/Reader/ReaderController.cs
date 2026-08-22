using PrimeTween;
using SummaRace.Constants;
using SummaRace.Core;
using SummaRace.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SummaRace.Features.Reader
{
    /// <summary>
    /// Shows story pages one at a time with a question after each (TDD §10.1).
    /// Wrong answers never block — the correct option is highlighted and the
    /// learner moves on (GDD north star: learning is never punished).
    /// </summary>
    public class ReaderController : MonoBehaviour
    {
        [Header("Page")]
        [SerializeField] private GameObject readingCard; // hidden during the question so it becomes its own bright page (prototype screens 4→5)
        [SerializeField] private TMP_Text pageText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Image progressFill; // fills as the learner moves through the pages
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text nextButtonLabel;

        [Header("Narration")]
        [SerializeField] private Button voiceButton;
        [SerializeField] private TMP_Text voiceButtonLabel;
        // Replays the page the learner is on. Optional: with nothing wired the controller
        // builds a small kit pill for it at runtime, so the affordance ships without a
        // scene edit (same idiom as SceneLoader's overlay and the race briefing).
        [SerializeField] private Button replayButton;
        [SerializeField] private TMP_Text replayButtonLabel;

        [Header("Leaving the story")]
        // Also optional/self-building. When it is allowed is the interesting part — see
        // RefreshSecondaryControls.
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text backButtonLabel;

        [Header("Question")]
        [SerializeField] private GameObject questionPanel;
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private Button[] optionButtons = new Button[3];
        [SerializeField] private TMP_Text[] optionLabels = new TMP_Text[3];
        [SerializeField] private TMP_Text feedbackText;
        [Tooltip("Optional. The nudge under the question naming WHICH story part it asks "
               + "about. Self-built into the card's free band when the scene has none.")]
        [SerializeField] private TMP_Text hintText;

        [Header("Reading buddy")]
        // Ms. Lumi hides during the question so the learner focuses on the answers,
        // then pops back in cheering on a correct pick (see MsLumiReactor). CanvasGroup
        // (not SetActive) so her event listener stays alive while she's invisible.
        [SerializeField] private CanvasGroup teacherGroup;

        private const float OptionFanSeconds = 0.28f;  // per-option pop-in
        private const float OptionFanStagger = 0.06f;  // gap between options

        /// <summary>
        /// How long the SWBST nudge waits before it appears, and how long it takes to arrive.
        ///
        /// THE HINT IS A SCAFFOLD, SO IT MUST NOT ARRIVE WITH THE PROMPT. Drawn in the same frame
        /// as the question it reads as part of the item — a fourth line of the question rather
        /// than help offered after a try — so every learner is cued whether they needed it or
        /// not, and the page measures comprehension-with-a-cue uniformly instead of
        /// comprehension. The delay gives the reader the question on its own first; the nudge
        /// then fades in for whoever is still deciding.
        ///
        /// Long enough to read a question of this length, short enough that a stuck learner is
        /// not left alone. It never names the answer or the option letter, only which story part
        /// is being asked about, so arriving late costs nothing.
        /// </summary>
        private const float HintDelaySeconds = 1.7f;
        private const float HintFadeSeconds = 0.45f;

        /// <summary>How long BACK stays armed waiting for its confirming tap. Long enough to
        /// read the second label, short enough that a learner who wandered off does not leave
        /// the story with one later stray tap.</summary>
        private const float BackConfirmSeconds = 4f;

        /// <summary>
        /// The three answer pills, against the question card's CREAM interior
        /// (0.971, 0.923, 0.829 - the same value the feedback colours below are measured on).
        ///
        /// This was Theme.Paper (0.969, 0.969, 1.0), which measures <b>1.10:1</b> against that
        /// cream. A ratio that close is not a luminance edge at all - the only thing separating
        /// a tappable answer from the card it sits on was a HUE shift, cool lilac-white against
        /// warm cream, plus one faint sprite border. Hue-only separation is exactly the failure
        /// class F49 removed from this game's wording, and it fails the same learners: a
        /// red-green colour-blind child, and anyone reading at low classroom brightness where
        /// small hue differences wash out first. The prototype's rose pill measures ~1.7:1.
        ///
        /// Warm neutral at <b>1.25:1</b>, with the dark label still at 10.2:1 on it. It does not
        /// go further on purpose: <see cref="OptionCorrect"/> - the green the card turns when
        /// the right answer is revealed - is itself only <b>1.46:1</b> against the same cream, so
        /// the headroom above the resting pill is thin, and a resting state that crowds the
        /// correct state would blunt the one moment on this screen that has to be unmistakable.
        /// If a future pass wants a bolder pill, raise the green first and re-measure both.
        /// </summary>
        private static readonly Color OptionNormal = new Color(0.87f, 0.83f, 0.78f);
        private static readonly Color OptionCorrect = new Color(0.55f, 0.85f, 0.45f); // friendly green

        /// <summary>
        /// The pill the learner actually tapped, when it was not the answer. One shade deeper
        /// than <see cref="OptionNormal"/> and still the same warm neutral — never a red, never
        /// a second signal colour, because a wrong answer is never punished (D7). It is only the
        /// quieter half of the acknowledgement; the visible half is the settle in
        /// <see cref="OnAnswer"/>, because the disabled ColorTint MULTIPLIES this image colour
        /// and would flatten a colour-only cue along with everything else.
        /// </summary>
        private static readonly Color OptionChosen = new Color(0.78f, 0.74f, 0.69f);

        // Both feedback colours are read against the question card, which is the kit's
        // "Daily Reward pannel" — a CREAM interior (0.971, 0.923, 0.829 after the card_paper
        // grain, which sits at 0.9% alpha and changes nothing), not the white it looks like.
        // At the old values the sentence that explains a wrong answer ran at 2.54:1 and the
        // praise at 3.59:1, both under WCAG AA's 4.5:1 — on the one line a struggling reader
        // most needs, at low classroom brightness. Deepened, not re-hued: still a warm amber
        // and a friendly green, never a scolding red. Measured on that cream: 4.89:1 and
        // 5.57:1.
        private static readonly Color FeedbackCorrect = Theme.GreenDeep; // deep green
        private static readonly Color FeedbackNotQuite = new Color(0.62f, 0.32f, 0.02f); // deep warm amber, never harsh

        /// <summary>The hint line under the question. Slate rather than the body brown so
        /// it reads as a note ABOUT the question rather than as part of it - a learner must
        /// never mistake it for a fourth thing to answer. Measured 8.3:1 on the card's cream
        /// (see Theme's contrast table), so it is quieter without being faint.</summary>
        private static readonly Color HintInk = Theme.Slate;

        // Small-chip palette: the navy pill (Resources/UI/bar_bg) is the HUD's own language
        // (F16), the gold pill (bar_fill) is what "armed" looks like everywhere else.
        private static readonly Color ChipTextIdle = Theme.Paper;   // on navy
        private static readonly Color ChipTextArmed = new Color(0.30f, 0.20f, 0.05f);  // on gold
        private static readonly Color ChipFallbackIdle = Theme.Alpha(Theme.Navy, 0.95f);
        private static readonly Color ChipFallbackArmed = new Color(0.98f, 0.73f, 0.22f);

        private StoryData _story;
        private int _pageIndex;
        private bool _questionAnswered;
        /// <summary>Latches the one-way hand-off to the race, so a double tap on NEXT cannot
        /// raise ReadingCompleted twice and zero the logged readingSeconds. See Advance().</summary>
        private bool _readingHandedOff;

        /// <summary>True once the learner has answered any question in this play-through —
        /// i.e. once the run carries a measure. Never cleared.</summary>
        private bool _answerCommitted;

        private bool _backArmed;
        private float _backArmedAt;
        private Sprite _pillIdle;
        private Sprite _pillArmed;

        /// <summary>
        /// Which story option each on-screen slot shows: <c>_displayOrder[slot] = option index</c>.
        /// The source content is badly position-biased — B is the correct answer on 64% of the
        /// 150 questions and C on only 4% — so presenting options in file order lets a learner
        /// beat the instrument by always tapping B. Shuffling per page removes that, and mapping
        /// back through this array keeps the logged choice comparable across learners.
        /// </summary>
        private readonly int[] _displayOrder = new int[3];
        private bool _questionShown;

        /// <summary>True when the reading card carries its own <see cref="SummaRace.UI.PanelIntro"/>,
        /// which already animates every page turn. Resolved once in Start — see ShowPage for why
        /// it decides whether we add a page-turn punch at all.</summary>
        private bool _cardAnimatesItself;

        private void Start()
        {
            // Survive being opened directly in the editor (TDD §13).
            _story = SummaRace.Core.GameManager.Instance != null ? SummaRace.Core.GameManager.Instance.CurrentStory : null;
            if (_story == null) _story = StoryLoader.Load("s01_easy");
            if (_story == null)
            {
                // Nothing to read. Bailing out here would leave the scene with no wired
                // buttons at all — a literal dead end (TDD §13). Hand the learner back to
                // the story cards, which explain an unplayable story in their own words.
                Debug.LogError("Reader: no story available — returning to Story Select.");
                SceneLoader.Go(SceneNames.StorySelect);
                return;
            }

            Praise.ResetRun(); // new story — restart the praise cadence

            // Nothing plays under the voice. The narration is the accessibility support the
            // study depends on, and the menu loop started in MainMenu otherwise ran under
            // every page of every story. Story Select and the session map start it again on
            // the way back, so stories 2 and 3 are not entered in silence.
            if (AudioManager.Instance != null) AudioManager.Instance.StopMusic();

            // Resolved once: ShowPage needs to know whether anything ELSE is already driving the
            // reading card's localScale on a page turn, and GetComponent per page turn is waste.
            _cardAnimatesItself = readingCard != null &&
                                  readingCard.GetComponent<SummaRace.UI.PanelIntro>() != null;

            if (nextButton != null) nextButton.onClick.AddListener(OnNext);
            if (voiceButton != null) voiceButton.onClick.AddListener(ToggleNarration);
            // Navy, not the authored white. Measured on the kit's cyan pill this label ran at
            // 1.78-2.09:1 in its ON state - the least readable text on the screen, on the control
            // that owns the study's reading support, and the one PulseVoiceButtonOnce exists to
            // draw the eye to. Navy on that cyan measures about 7:1. Set here rather than in the
            // scene so it cannot drift back with a prefab or kit-sprite change.
            if (voiceButtonLabel != null) voiceButtonLabel.color = Theme.Navy;
            RefreshVoiceButton();
            PulseVoiceButtonOnce();

            EnsureSecondaryControls();
            if (replayButton != null) replayButton.onClick.AddListener(ReplayNarration);
            if (backButton != null) backButton.onClick.AddListener(OnBackTapped);

            // ---- QUESTION FOCUS SCRIM (owner, 2026-08-23: "background a little dark so the
            // question screen is focused") ---------------------------------------------------
            // A translucent ink layer as the panel's FIRST CHILD: it draws before the question
            // card (which stays bright on top) and only exists while the panel is active, so
            // reading pages keep their sunny room. The VOICE chip must survive the dim — it is
            // the study's narration control and sits BELOW the panel in the hierarchy — so it
            // is raised above the panel once here; on reading pages the panel is inactive and
            // the order change shows nothing.
            if (questionPanel != null)
            {
                var scrim = new GameObject("QuestionScrim", typeof(RectTransform));
                scrim.transform.SetParent(questionPanel.transform, false);
                scrim.transform.SetAsFirstSibling();
                var scrimImg = scrim.AddComponent<UnityEngine.UI.Image>();
                scrimImg.color = Theme.Alpha(Theme.Ink, 0.38f);
                scrimImg.raycastTarget = false;
                var scrimRt = scrimImg.rectTransform;
                scrimRt.anchorMin = Vector2.zero; scrimRt.anchorMax = Vector2.one;
                scrimRt.offsetMin = Vector2.zero; scrimRt.offsetMax = Vector2.zero;

                if (voiceButton != null &&
                    voiceButton.transform.parent == questionPanel.transform.parent)
                    voiceButton.transform.SetSiblingIndex(
                        questionPanel.transform.GetSiblingIndex() + 1);
            }
            for (int i = 0; i < optionButtons.Length; i++)
            {
                int index = i; // capture
                if (optionButtons[i] != null)
                    optionButtons[i].onClick.AddListener(() => OnAnswer(index));
            }

            ShowPage(0);
        }

        private void ShowPage(int index)
        {
            _pageIndex = index;
            _questionShown = false;
            _questionAnswered = false;

            var page = _story.pages[index];
            if (readingCard != null) readingCard.SetActive(true);
            if (pageText != null) pageText.text = page.text;

            // THE PAGE TURN IS ANIMATED BY PanelIntro, NOT BY US — and the comment that used to
            // sit here had it exactly backwards, so it is worth stating plainly.
            //
            // The old claim was "skipped on page 1, where PanelIntro is already playing". In fact
            // PanelIntro pops in on EVERY page: it fires from OnEnable, and ShowQuestion
            // deactivates this same card on every page so the question can be its own bright page
            // — so the SetActive(true) above re-enables it and re-triggers the pop-in each time
            // (page 1 gets it from the scene's own enable). The `index > 0` guard therefore fired
            // the punch on precisely the pages where PanelIntro was ALSO running, and this
            // block's StopAll then killed PanelIntro mid-tween: the reverse of the stated intent,
            // and why the page turn read inconsistently.
            //
            // PICKED: let PanelIntro own it. It is the larger and more legible motion (0.85 → 1
            // over 0.3s, OutBack) against a 2% punch, it already runs on every page, and one
            // tween per transform is the standing rule in this file — two driving one localScale
            // leave it wherever the last one wrote. The punch survives ONLY as the fallback for a
            // scene whose card has no PanelIntro, so a page turn is never a silent beat on a
            // screen where everything else moves.
            if (readingCard != null && !_cardAnimatesItself)
            {
                var cardT = readingCard.transform;
                Tween.StopAll(onTarget: cardT);
                cardT.localScale = Vector3.one;
                Tween.PunchScale(cardT, Vector3.one * 0.02f, 0.25f);
            }
            if (progressText != null)
                progressText.text = GameText.PageProgress(index + 1, _story.pages.Length);
            if (progressFill != null)
            {
                // Sweep to the new page rather than snapping — the bar is the learner's
                // sense of "how much story is left", so the movement is worth showing.
                float target = (index + 1) / (float)_story.pages.Length;
                Tween.StopAll(onTarget: progressFill);
                Tween.Custom(progressFill, progressFill.fillAmount, target, 0.4f,
                    (bar, v) => bar.fillAmount = v, Ease.OutQuad);
            }

            if (questionPanel != null) questionPanel.SetActive(false);
            // The hint's delayed fade belongs to the question that has just closed. A learner who
            // answers and taps NEXT inside HintDelaySeconds would otherwise leave it in flight
            // across the page turn, to land on the NEXT question's nudge and cut its delay short.
            if (hintText != null) Tween.StopAll(onTarget: hintText);
            if (teacherGroup != null) teacherGroup.alpha = 1f; // buddy is back for reading
            if (nextButton != null) nextButton.gameObject.SetActive(true);
            if (nextButtonLabel != null) nextButtonLabel.text = GameText.NextLabel;

            if (index > 0 && AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(AudioKeys.SfxPageTurn);

            RefreshSecondaryControls(readingPage: true);
            PlayPageNarration();
        }

        // ---------- narration (GDD: optional voice, learner-controlled) ----------

        private static bool NarrationEnabled
        {
            get => PlayerPrefs.GetInt(PrefKeys.NarrationOn, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(PrefKeys.NarrationOn, value ? 1 : 0);
                // Unity only writes prefs to disk on a clean quit, and a classroom tablet gets
                // swiped away or killed instead — the learner's voice choice would silently
                // come back reset. It is one int; flush it the moment they choose.
                PlayerPrefs.Save();
            }
        }

        private void PlayPageNarration()
        {
            if (AudioManager.Instance == null) return;
            if (NarrationEnabled) AudioManager.Instance.PlayNarration(_story.pages[_pageIndex].narration);
            else AudioManager.Instance.StopNarration();
        }

        private void ToggleNarration()
        {
            NarrationEnabled = !NarrationEnabled;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);
            RefreshVoiceButton();
            PlayPageNarration(); // re-read the current page when switched on
        }

        /// <summary>
        /// Reads the current page again, once. Deliberately does NOT touch
        /// <see cref="PrefKeys.NarrationOn"/>: a learner who missed the audio used to have to
        /// switch VOICE off and then on again to hear it (ToggleNarration re-reads the page as
        /// a side effect), which flipped their saved preference twice and only worked while the
        /// voice was already on. The toggle is the setting; this is a one-off request, so it
        /// plays even with VOICE OFF — the learner asked for it on this tap.
        /// </summary>
        private void ReplayNarration()
        {
            if (_story == null) return;

            // HEAR AGAIN is the one control in the game whose entire result is a sound, so on a
            // muted classroom tablet — which is most of them — it answered a tap with literally
            // nothing on screen and read as a broken button. The punch is not decoration: it is
            // the only proof the tap landed.
            // Punch the label, never the chip root: the root carries ButtonSquash, which drives
            // the same localScale from its own press tween, and two tweens on one transform
            // leave it wherever the last one wrote (same rule as Arrange and Story Select).
            if (replayButtonLabel != null)
                Tween.PunchScale(replayButtonLabel.transform, Vector3.one * 0.25f, 0.35f);

            if (AudioManager.Instance == null) return;
            AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);
            AudioManager.Instance.PlayNarration(_story.pages[_pageIndex].narration);
        }

        /// <summary>
        /// Draws the eye to the voice control once, on the first reading page of a session
        /// (GDD §11.1: "First reading page pulses the speaker icon once so learners discover
        /// narration"). Never implemented until now.
        ///
        /// Narration auto-plays, so this is not about starting it — it is about the learner
        /// knowing the control is theirs. It is the only way to hear a page AGAIN, and for a
        /// learner who cannot decode the passage that is the difference between the study
        /// measuring comprehension and measuring decoding. A child who never notices the button
        /// never gets a second listen, and nothing else on the screen mentions it.
        ///
        /// Once per app launch, not once per story: thirty pulses across ten sessions is a tic,
        /// and by story two the learner either knows or is not going to learn it this way.
        /// The tween goes on the LABEL — the button root carries ButtonSquash, and two tweens
        /// driving one localScale leave it wherever the last one wrote.
        /// </summary>
        private static bool _voicePulsed;

        /// <summary>
        /// "Once per app launch" only holds if the flag is cleared per launch — and a static
        /// bool is NOT. With Domain Reload disabled (Enter Play Mode Options, which this project
        /// benefits from and which is the default a fast iteration loop drifts to), statics
        /// survive stopping Play mode, so after the first Play of an Editor session the pulse
        /// never fired again — exactly when someone is trying to look at it, and exactly the
        /// class of bug that gets logged as "the GDD §11.1 pulse is missing" a second time.
        ///
        /// SubsystemRegistration is the earliest hook and runs before any scene loads, so the
        /// flag is false before Boot's first Reader can read it. Harmless on a real device,
        /// where the domain is fresh anyway.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetVoicePulseFlag() => _voicePulsed = false;

        private void PulseVoiceButtonOnce()
        {
            if (_voicePulsed || voiceButtonLabel == null) return;
            _voicePulsed = true;

            var target = voiceButtonLabel.transform;
            Tween.StopAll(target);
            target.localScale = Vector3.one;
            Tween.PunchScale(target, Vector3.one * 0.28f, 0.7f, startDelay: 0.9f);
        }

        private void RefreshVoiceButton()
        {
            if (voiceButtonLabel == null) return;
            voiceButtonLabel.text = NarrationEnabled ? GameText.VoiceOn : GameText.VoiceOff;
            if (voiceButton != null && voiceButton.image != null)
                voiceButton.image.color = NarrationEnabled ? Color.white : new Color(0.75f, 0.75f, 0.78f);
        }

        private void OnNext()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);

            var page = _story.pages[_pageIndex];

            // Page → question → next page.
            if (page.question != null && !_questionShown)
            {
                ShowQuestion(page.question);
                return;
            }

            Advance();
        }

        private void ShowQuestion(QuestionData question)
        {
            _questionShown = true;
            if (readingCard != null) readingCard.SetActive(false); // story gives way to its own question page

            // AND STOP READING IT ALOUD. The card is hidden here precisely so the question is
            // its own page - but narration kept playing the passage, so how much of the answer
            // a learner still had in their ear was a function of HOW FAST THEY TAPPED. Tap three
            // seconds into a twelve-second clip and nine seconds of the passage plays during the
            // item; wait, and none does. That is an uncontrolled per-item variance sitting
            // directly on readingFirstCorrect, and nothing logs it.
            //
            // It also could not be stopped by the learner: HEAR AGAIN is hidden during the
            // question, and the VOICE control is a persisted preference, not a stop button.
            if (SummaRace.Core.AudioManager.Instance != null)
                SummaRace.Core.AudioManager.Instance.StopNarration();
            if (progressText != null)
                progressText.text = GameText.QuestionProgress(_pageIndex + 1, _story.pages.Length);
            if (questionPanel != null) questionPanel.SetActive(true);
            if (teacherGroup != null) teacherGroup.alpha = 0f; // hide the buddy — focus on the answers
            if (nextButton != null) nextButton.gameObject.SetActive(false);
            if (feedbackText != null) feedbackText.text = "";
            RefreshSecondaryControls(readingPage: false);

            if (questionText != null) questionText.text = question.text;

            // The nudge names the SWBST part this page teaches. It is keyed by PAGE, which
            // is the same index the element carries, so page 3 asks about BUT and the hint
            // points at the problem. Blank (a story with a different page count) hides the
            // line rather than leaving an empty band under the question.
            if (hintText != null)
            {
                string hint = GameText.ReaderSlotHint(_pageIndex);
                bool hasHint = !string.IsNullOrEmpty(hint);
                hintText.text = hint;
                hintText.gameObject.SetActive(hasHint);

                // The hint arrives AFTER the question, never with it — see HintDelaySeconds for
                // why that distinction is about the measure and not about polish. It stays
                // active-but-transparent rather than being switched on by the tween, so nothing
                // depends on whether tweens run against a disabled GameObject; raycastTarget is
                // already off, so an invisible active line can never steal a tap from option A.
                Tween.StopAll(onTarget: hintText); // a fast page turn must not stack two fades
                if (hasHint)
                {
                    hintText.alpha = 0f;
                    Tween.Custom(hintText, 0f, 1f, HintFadeSeconds,
                        (t, v) => t.alpha = v, Ease.OutQuad, startDelay: HintDelaySeconds);
                }
                else hintText.alpha = 1f; // leave the field in a sane state for a wired scene
            }

            ShuffleDisplayOrder(question.options != null ? question.options.Length : 0);

            for (int i = 0; i < optionButtons.Length; i++)
            {
                if (optionButtons[i] == null) continue;

                // A slot with no option behind it is HIDDEN, never filled — see
                // ShuffleDisplayOrder for the duplicate-correct-answer defect that caused.
                // A scene wired with more buttons than _displayOrder has entries reads -1 too,
                // rather than throwing: an extra pill showing nothing beats a broken question.
                int optionIndex = i < _displayOrder.Length ? _displayOrder[i] : -1;
                if (optionIndex < 0 || question.options == null || optionIndex >= question.options.Length)
                {
                    optionButtons[i].gameObject.SetActive(false);
                    continue;
                }
                optionButtons[i].gameObject.SetActive(true);
                optionButtons[i].interactable = true;
                if (optionButtons[i].image != null) optionButtons[i].image.color = OptionNormal;

                string optionText = question.options[optionIndex];
                if (optionLabels != null && i < optionLabels.Length && optionLabels[i] != null)
                    optionLabels[i].text = i < GameText.OptionLetters.Length
                        // <indent> hangs the letter to the left so a wrapped second
                        // line starts under the text, not under the "C.".
                        ? GameText.OptionLetters[i] + "<indent=9%>" + optionText + "</indent>"
                        : optionText;

                // Fan the options in one after another so the page reads top-to-bottom
                // instead of arriving all at once. ButtonSquash cached scale 1 in Awake,
                // so returning to Vector3.one keeps press-squash correct.
                var option = optionButtons[i].transform;
                Tween.StopAll(onTarget: option);
                option.localScale = Vector3.one * 0.9f;
                Tween.Scale(option, Vector3.one, OptionFanSeconds, Ease.OutBack,
                    startDelay: OptionFanStagger * i);
            }
        }

        /// <summary>
        /// Fisher-Yates over the on-screen slots, reshuffled for every question.
        ///
        /// A slot with no option behind it gets <b>-1</b>, and every caller treats that as
        /// "hide this button". It used to get <b>0</b>, which DUPLICATED the first option into
        /// the surplus slot: a question with two options rendered the same answer twice, and
        /// whenever option 0 was the correct one the screen carried TWO correct answers — both
        /// of which map back through <c>_displayOrder[slot]</c> and log as correct. That is a
        /// scoring defect sitting directly on readingFirstCorrect, not a cosmetic one, and it
        /// fails silently: the row exports as perfectly valid.
        ///
        /// Latent today — all 150 questions carry exactly 3 options and StoryLoader validates it
        /// — but the measure must not depend on the content staying that way.
        /// </summary>
        private void ShuffleDisplayOrder(int optionCount)
        {
            // Clamp, not Min: a negative/absent options array would otherwise write past nothing
            // and leave stale indices from the previous question in every slot.
            int n = Mathf.Clamp(optionCount, 0, _displayOrder.Length);
            for (int i = 0; i < _displayOrder.Length; i++) _displayOrder[i] = i < n ? i : -1;

            for (int i = n - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (_displayOrder[i], _displayOrder[j]) = (_displayOrder[j], _displayOrder[i]);
            }
        }

        /// <param name="slot">Which button was tapped, NOT which option it holds.</param>
        private void OnAnswer(int slot)
        {
            if (_questionAnswered) return;

            var question = _story.pages[_pageIndex].question;
            if (question == null) return;

            // Map the tapped slot back to the story's own option index, so the logged choice
            // means the same thing for every learner regardless of how their page was shuffled.
            int chosenIndex = slot >= 0 && slot < _displayOrder.Length ? _displayOrder[slot] : -1;

            // An EMPTY slot holds no option and must never be logged as an answer. Its button is
            // hidden by ShowQuestion so this cannot fire normally — but the latches below are
            // one-way, and spending them on a non-answer would lock the learner out of the item
            // and write a chosenIndex of -1 into the study data. Checked BEFORE the latches.
            if (chosenIndex < 0) return;

            _questionAnswered = true;

            // From here the play-through is study data: SessionLogService records the FIRST
            // answer per page and this is one. The exit stays gone for the rest of the run.
            _answerCommitted = true;
            RefreshSecondaryControls(readingPage: false);

            bool correct = chosenIndex == question.correctIndex;

            // Always reveal the correct answer; never block (GDD §4.3).
            for (int i = 0; i < optionButtons.Length; i++)
            {
                if (optionButtons[i] == null) continue;
                // Hidden slots hold no option and take no part in the reveal (ShuffleDisplayOrder);
                // same out-of-range tolerance as ShowQuestion so the two can never disagree.
                int optionIndex = i < _displayOrder.Length ? _displayOrder[i] : -1;
                if (optionIndex < 0) continue;
                bool isCorrect = optionIndex == question.correctIndex;

                // Grey out the options that are NOT the answer, but leave the correct one
                // enabled. These buttons use ColorTint with a disabled colour of 0.784 grey at
                // alpha 0.502, and the tint MULTIPLIES the image colour — so disabling the
                // correct option rendered its green swatch at ~78% brightness and half opacity,
                // washing out the single most important teaching beat in the Reader. Nothing is
                // lost by leaving it enabled: re-entry is already blocked by _questionAnswered
                // above, so `interactable` was never what stopped a second tap — it was only
                // ever tinting. Greying the wrong ones and lighting up the right one is also
                // exactly the visual language this moment wants.
                optionButtons[i].interactable = isCorrect;

                if (isCorrect)
                {
                    if (optionButtons[i].image != null) optionButtons[i].image.color = OptionCorrect;
                    // The "you got it" beat — the flattest moment in the scene until now.
                    // Stop + reset first: these buttons carry ButtonSquash, whose release tween
                    // (0.18s back to scale 1) is still running on this same transform when the
                    // click handler fires. PunchScale samples the CURRENT scale as its rest
                    // value and returns to it, so it captured the 0.92 squash and left the
                    // correct answer parked at 92% for the rest of the question — the one beat
                    // that is supposed to grow. Same guard as the fan-in above, and the same
                    // reason StorySelect.PlayLockedNudge refuses to punch a card root.
                    var punched = optionButtons[i].transform;
                    Tween.StopAll(onTarget: punched);
                    punched.localScale = Vector3.one;
                    Tween.PunchScale(punched, Vector3.one * 0.12f, 0.45f);
                }
            }

            // ACKNOWLEDGE THE TAP THAT WAS ACTUALLY MADE.
            //
            // Until now a wrong pick looked identical to the two options the learner never
            // touched: the loop above greys all three with the same disabled tint and only the
            // correct one moves. So the screen answered "here is the answer" without ever
            // answering "here is what YOU chose" — and a learner who fat-fingered the wrong pill
            // could not tell a slip from a misunderstanding, on the one screen whose first
            // answers are the study's readingFirstCorrect.
            //
            // This is deliberately a SETTLE, not a rejection: it eases DOWN and stays there, like
            // a key pressed in. No red, no shake, no bounce-back — those read as "wrong of you"
            // and D7 forbids punishing a wrong answer. It is also deliberately quieter and
            // shorter than the correct answer's punch (0.94 over 0.22s against a 12% punch over
            // 0.45s), so the reveal stays the loudest thing on screen.
            //
            // Scale is the channel because the disabled ColorTint MULTIPLIES image colour, so a
            // colour-only cue would be flattened along with the other two; OptionChosen is only
            // the supporting half. Nothing here depends on which option was correct — it marks
            // the tapped slot, so it cannot leak the answer.
            if (!correct)
            {
                int chosenSlot = Mathf.Clamp(slot, 0, optionButtons.Length - 1);
                var chosen = optionButtons.Length > 0 ? optionButtons[chosenSlot] : null;
                if (chosen != null)
                {
                    if (chosen.image != null) chosen.image.color = OptionChosen;

                    // Same ButtonSquash guard as the correct-answer punch above: its release
                    // tween is still driving this localScale when the click handler fires, and
                    // two tweens on one transform leave it wherever the last one wrote.
                    var chosenT = chosen.transform;
                    Tween.StopAll(onTarget: chosenT);
                    chosenT.localScale = Vector3.one;
                    Tween.Scale(chosenT, Vector3.one * 0.94f, 0.22f, Ease.OutQuad);
                }
            }

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(correct ? AudioKeys.SfxCorrect : AudioKeys.SfxNotQuite);

            // GDD 11.4 asks for a tiny vibration on collect and on star pops. Only the star pops
            // were ever wired, so Haptics.Light — documented as "Collect / correct answer" — was
            // declared and called from nowhere. A classroom tablet is usually muted, which is
            // exactly when a non-audio confirmation carries the beat. Correct only: a wrong answer
            // is never punished, so it gets no buzz.
            if (correct) SummaRace.Core.Haptics.Play(SummaRace.Core.Haptics.Light);

            if (feedbackText != null)
            {
                feedbackText.text = correct ? Praise.Generic() : GameText.ReaderWrongFeedback;
                feedbackText.color = correct ? FeedbackCorrect : FeedbackNotQuite;

                // Pop it in, matching the race's feedback pill (F16) — the Reader was
                // the one scene where feedback just silently appeared.
                var fb = feedbackText.transform;
                Tween.StopAll(onTarget: fb);
                fb.localScale = Vector3.one * 0.7f;
                Tween.Scale(fb, Vector3.one, 0.35f, Ease.OutBack);
            }

            EventBus.Raise(new PageAnswered
            {
                pageIndex = _pageIndex,
                chosenIndex = chosenIndex,
                correct = correct
            });

            if (nextButton != null) nextButton.gameObject.SetActive(true);
            if (nextButtonLabel != null)
                nextButtonLabel.text = _pageIndex == _story.pages.Length - 1 ? GameText.StartRaceLabel : GameText.NextPageLabel;
        }

        private void Advance()
        {
            if (_pageIndex < _story.pages.Length - 1)
            {
                ShowPage(_pageIndex + 1);
                return;
            }

            // Hand-off happens exactly once. NEXT stays visible and interactable through this
            // branch (unlike ShowQuestion, which hides it), so two thumbs on a tablet — which is
            // how a 9-year-old actually holds one — dispatch OnNext twice in quick succession.
            // SceneLoader's guard blocks the second scene LOAD but not the second EVENT, and by
            // then SessionLogService has already run ClosePhase() and reset its start time, so
            // the second ReadingCompleted overwrites readingSeconds with ~0. The row still
            // exports as perfectly valid, so a study variable silently reads zero and nobody
            // finds out until analysis. Hide the button and latch, so neither can happen.
            if (_readingHandedOff) return;
            _readingHandedOff = true;
            if (nextButton != null) nextButton.gameObject.SetActive(false);

            EventBus.Raise(new ReadingCompleted());
            SceneLoader.Go(SceneNames.RaceEndless); // experiment: Trash Dash base race
        }

        // ---------- leaving safely, and hearing the page again ----------

        private void Update()
        {
            // The armed BACK forgets itself. Unscaled so a paused/slowed frame cannot leave
            // the confirm sitting there indefinitely.
            if (_backArmed && Time.unscaledTime - _backArmedAt > BackConfirmSeconds) DisarmBack();
        }

        /// <summary>
        /// Decides whether the learner may leave, and whether the replay chip is useful.
        ///
        /// THE RULE: BACK exists only while a page is being READ and only until the learner's
        /// first answer. Two separate reasons, both about the study rather than about taste:
        ///
        /// 1. Once one question has been answered the play-through carries a measure —
        ///    SessionLogService records the FIRST answer per page and nothing later replaces
        ///    it — so leaving after that point would either discard recorded data or file a
        ///    half-run against the learner. Before it, the run holds nothing: SessionLogService
        ///    .HasData is false, so the row is dropped rather than written as an abandonment.
        ///    A learner who tapped the wrong story card therefore costs the study exactly zero.
        ///    In practice this window is page 1 before its question is answered, because the
        ///    NEXT button is hidden until a question is answered, so no later page is reachable
        ///    without committing one.
        /// 2. It is never offered DURING a question, even an unanswered one. An exit sitting
        ///    next to an item a learner is unsure about invites bailing out of hard questions,
        ///    which would bias the reading measure by exactly the learners it matters most for.
        ///
        /// Everything downstream (race, Arrange, Summary, Results) stays one-way on purpose:
        /// by then the run is data. The teacher's escape hatch there is still the app switcher.
        /// </summary>
        private void RefreshSecondaryControls(bool readingPage)
        {
            bool mayLeave = readingPage && !_answerCommitted;
            if (!mayLeave) DisarmBack();
            if (backButton != null) backButton.gameObject.SetActive(mayLeave);

            // ANDROID BACK FOLLOWS THE SAME RULE, DECIDED ON THE SAME LINE.
            //
            // The Reader's exit is offered only before the learner's first answer, because after
            // that the run is study data. Registering BACK anywhere else would re-open exactly
            // the data-loss path `mayLeave` exists to close - and the two would drift apart the
            // first time either was edited. Sharing this one decision means BACK is legal
            // precisely when the chip is on screen, permanently.
            Core.BackButtonGuard.RegisterExitOrBlock(
                mayLeave, GameText.BackLeaveToStories, GameText.BackBlockedReader,
                () => SceneLoader.Go(SceneNames.StorySelect));

            // A page with no narration path would give a chip that plays silence — worse than
            // no chip. AudioManager already treats a missing clip as a silent page, so this is
            // only about not offering a control that cannot do anything.
            bool hasNarration = _story != null &&
                                !string.IsNullOrEmpty(_story.pages[_pageIndex].narration);
            if (replayButton != null) replayButton.gameObject.SetActive(readingPage && hasNarration);
        }

        /// <summary>
        /// Two taps to leave. The first only arms it: a 9-year-old tapping around the corner of
        /// the screen must not be able to drop out of the story, and the same "tap again"
        /// pattern already guards the teacher screen's destructive actions.
        /// </summary>
        private void OnBackTapped()
        {
            if (!_backArmed) { ArmBack(); return; }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopNarration(); // the voice never follows them out
                AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);
            }

            // Back to the cards they came from, so re-picking the right difficulty is one tap.
            // Nothing is raised on the way out: the in-flight log is left empty on purpose and
            // the next StoryStarted drops it (SessionLogService.HasData).
            SceneLoader.Go(SceneNames.StorySelect);
        }

        private void ArmBack()
        {
            _backArmed = true;
            _backArmedAt = Time.unscaledTime;
            if (backButtonLabel != null)
            {
                backButtonLabel.text = GameText.ReaderBackConfirm;
                // Both armed looks are light (gold pill, or an amber flat fallback), so the
                // label goes dark either way — a light-on-gold label is the one combination
                // that would make the confirming step the hardest thing on screen to read.
                backButtonLabel.color = ChipTextArmed;
            }
            ApplyChipStyle(backButton, armed: true);
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxSlotWiggle);
        }

        private void DisarmBack()
        {
            if (!_backArmed) return;
            _backArmed = false;
            if (backButtonLabel != null)
            {
                backButtonLabel.text = GameText.ReaderBackLabel;
                backButtonLabel.color = ChipTextIdle;
            }
            ApplyChipStyle(backButton, armed: false);
        }

        /// <summary>Idle = navy pill, armed = gold pill. Falls back to flat colours when the
        /// generated pill sprites are missing, so the state is never invisible.</summary>
        private void ApplyChipStyle(Button chip, bool armed)
        {
            if (chip == null || chip.image == null) return;

            var sprite = armed ? _pillArmed : _pillIdle;
            if (sprite != null)
            {
                chip.image.sprite = sprite;
                chip.image.color = Color.white;
            }
            else
            {
                chip.image.color = armed ? ChipFallbackArmed : ChipFallbackIdle;
            }
        }

        /// <summary>
        /// Builds the two small chips when the scene carries no objects for them. Serialized
        /// references win whenever they exist, so dressing these properly later is a scene
        /// edit and no code change. Built here because the Reader had no exit and no replay at
        /// all, and a control that only appears once someone remembers to wire it is the same
        /// as no control on study day.
        /// </summary>
        private void EnsureSecondaryControls()
        {
            _pillIdle = Resources.Load<Sprite>("UI/bar_bg");    // navy 9-sliced pill
            _pillArmed = Resources.Load<Sprite>("UI/bar_fill");  // gold 9-sliced pill

            if (backButton != null && backButtonLabel == null)
                backButtonLabel = backButton.GetComponentInChildren<TMP_Text>(true);
            if (replayButton != null && replayButtonLabel == null)
                replayButtonLabel = replayButton.GetComponentInChildren<TMP_Text>(true);

            var root = ResolveSceneCanvas();
            if (root != null)
            {
                if (backButton == null)
                    // Bottom-left corner: clear of NEXT (x 0.30-0.80) and below Ms. Lumi
                    // (y 0.11 up), so it reads as a quiet corner control rather than a choice.
                    backButton = BuildChip(root, "BackChip",
                        new Vector2(0.035f, 0.040f), new Vector2(0.235f, 0.105f),
                        GameText.ReaderBackLabel, 30f, out backButtonLabel);

                if (replayButton == null)
                    // Directly under VOICE, in the band between the top HUD row (y 0.945) and
                    // the reading card (y 0.90) — it belongs with the audio controls.
                    replayButton = BuildChip(root, "ReplayChip",
                        new Vector2(0.700f, 0.902f), new Vector2(0.970f, 0.945f),
                        GameText.ReaderReplayLabel, 28f, out replayButtonLabel);
            }

            EnsureHintLine();

            if (backButtonLabel != null) backButtonLabel.text = GameText.ReaderBackLabel;
            if (replayButtonLabel != null) replayButtonLabel.text = GameText.ReaderReplayLabel;
            ApplyChipStyle(backButton, armed: false);
            ApplyChipStyle(replayButton, armed: false);
        }

        /// <summary>
        /// Builds the question's hint line when the scene carries no object for it. It goes
        /// inside the question CARD, not on the canvas, so it travels with the card and is
        /// hidden by the same SetActive that hides the question - a hint that outlived its
        /// question would sit over the story page naming a part the learner is not being
        /// asked about.
        ///
        /// The band is measured, not guessed: within QCard the question text ends at y 0.80
        /// and option A begins at 0.72, so 0.73-0.79 is the one gap on this card that is
        /// already empty. Nothing moves; if a scene later wires its own hintText, that
        /// reference wins and none of this runs.
        /// </summary>
        private void EnsureHintLine()
        {
            if (hintText != null || questionText == null) return;

            var card = questionText.transform.parent;
            if (card == null) return;

            var go = new GameObject("HintText", typeof(RectTransform));
            go.transform.SetParent(card, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.06f, 0.73f);
            rect.anchorMax = new Vector2(0.94f, 0.79f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var label = go.AddComponent<TextMeshProUGUI>();
            // Borrow the question's own face so the card keeps one typeface.
            if (questionText.font != null) label.font = questionText.font;
            label.fontStyle = FontStyles.Italic;   // the second channel: it is not the question
            label.enableAutoSizing = true;
            label.fontSizeMin = 22f;               // the readability audit's acuity floor
            label.fontSizeMax = 30f;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.color = HintInk;
            label.raycastTarget = false;           // never steals a tap meant for option A
            label.text = string.Empty;
            go.SetActive(false);                   // ShowQuestion turns it on with real text

            hintText = label;
        }

        /// <summary>One small pill button, Fredoka-labelled like every other button in the kit.</summary>
        private Button BuildChip(Transform root, string name, Vector2 anchorMin, Vector2 anchorMax,
                                 string text, float fontSize, out TMP_Text label)
        {
            var chipGo = new GameObject(name, typeof(RectTransform));
            chipGo.transform.SetParent(root, false);
            var rect = (RectTransform)chipGo.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling(); // the question page is a full-rect sibling — stay tappable above it

            var image = chipGo.AddComponent<Image>();
            if (_pillIdle != null)
            {
                image.sprite = _pillIdle;
                image.type = Image.Type.Sliced;
            }
            else image.color = ChipFallbackIdle;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(chipGo.transform, false);
            label = labelGo.AddComponent<TextMeshProUGUI>();
            // Borrow the NEXT button's font rather than loading one: it is the kit's heading
            // face (Fredoka) and it is already in memory in this scene.
            if (nextButtonLabel != null && nextButtonLabel.font != null) label.font = nextButtonLabel.font;
            label.text = text;
            label.fontSize = fontSize;
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
            label.fontSizeMax = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.color = ChipTextIdle;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 6f);
            labelRect.offsetMax = new Vector2(-10f, -6f);

            chipGo.AddComponent<SummaRace.UI.ButtonSquash>();
            var button = chipGo.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        /// <summary>
        /// The canvas this scene's own UI lives on. A blind FindAnyObjectByType would happily
        /// return SceneLoader's persistent FadeCanvas ([Core], DontDestroyOnLoad, alpha 0) and
        /// the chips would be built invisible on the loading overlay.
        /// </summary>
        private Transform ResolveSceneCanvas()
        {
            var canvas = nextButton != null ? nextButton.GetComponentInParent<Canvas>() : null;
            if (canvas == null && voiceButton != null) canvas = voiceButton.GetComponentInParent<Canvas>();
            if (canvas != null) return canvas.rootCanvas.transform;

            foreach (var candidate in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (candidate.gameObject.scene == gameObject.scene)
                    return candidate.rootCanvas.transform;

            Debug.LogWarning("Reader: no scene canvas found — back and replay chips not built.");
            return null;
        }
    }
}
