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

        private StoryData _story;
        private int _nudgeCount;

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
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMusic(AudioKeys.MusicMenu);

            // Ms. Lumi reacts here now (see MsLumiReactor.AttachBadge). Null-safe and pool-safe:
            // absent object or absent badge art simply leaves the screen as it was.
            SummaRace.UI.MsLumiReactor.AttachBadge();

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
            if (placeholderText != null) placeholderText.text = GameText.SummaryPlaceholder;
            if (submitLabel != null) submitLabel.text = GameText.SubmitLabel;
            if (hintText != null)
            {
                hintText.text = GameText.SummaryHint;
                BuildHintBubble();
            }
            if (nudgeText != null) nudgeText.text = "";
            if (summaryInput != null)
            {
                summaryInput.characterLimit = GameRules.SummaryMaxChars;
                // A nudge that stays on screen while the learner is already fixing the
                // sentence reads as "still wrong" and is the one thing here that could feel
                // like being told off. It clears the moment they start typing again.
                summaryInput.onValueChanged.AddListener(_ => ClearNudge());
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

            EnsureDoneTypingChip();
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
                if (nudgeText != null)
                    nudgeText.text = GameText.SummaryNudges[NudgeIndexFor(text)];
                _nudgeCount++;
                return;
            }

            Accept(text);
        }

        private void ClearNudge()
        {
            if (nudgeText != null && nudgeText.text.Length > 0) nudgeText.text = "";
        }

        /// <summary>
        /// Picks the nudge that matches what actually stopped the sentence, instead of walking
        /// the list in order: a learner who wrote three good lines but never named the Somebody
        /// used to be told "try writing a little more", which is advice for a different problem
        /// and cannot be acted on. The nudge count the study logs is unchanged.
        /// </summary>
        private int NudgeIndexFor(string text)
        {
            bool tooShort = string.IsNullOrWhiteSpace(text) ||
                            text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries).Length
                                < GameRules.SummaryMinWords;
            return Mathf.Clamp(tooShort ? 0 : 1, 0, GameText.SummaryNudges.Length - 1);
        }

        private bool PassesLightChecks(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            // At least a few words of effort.
            if (text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries).Length < GameRules.SummaryMinWords)
                return false;

            // One sentence: no sentence breaks before the final punctuation.
            string body = text.TrimEnd('.', '!', '?', ' ');
            if (body.IndexOfAny(new[] { '.', '!', '?' }) >= 0) return false;

            // A story with no elements cannot supply a Somebody to look for, so the check
            // below could only ever fail — the learner would be nudged twice for something no
            // sentence of theirs could fix. Broken content passes instead (GDD D7).
            if (_story.elements == null || _story.elements.Length == 0) return true;

            // Mentions the Somebody (any word of it, e.g. "Molly").
            string lower = text.ToLowerInvariant();
            foreach (var word in _story.elements[0].correct.ToLowerInvariant().Split(' '))
                if (word.Length > 2 && lower.Contains(word)) return true;

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
            // the keyboard can reach.
            rect.anchorMin = new Vector2(0.66f, 0.598f);
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
