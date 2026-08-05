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

        [Header("Labels (set from GameText)")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text placeholderText;
        [SerializeField] private TMP_Text submitLabel;

        [Header("Keyboard")]
        // Closes the on-screen keyboard. Optional: with nothing wired the controller builds a
        // small kit pill for it at runtime (see EnsureDoneTypingChip).
        [SerializeField] private Button doneTypingButton;

        private static readonly Color ChipText = new Color(0.97f, 0.97f, 1.00f);
        private static readonly Color ChipFallback = new Color(0.11f, 0.17f, 0.33f, 0.95f);

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

            if (referenceText != null && _story.elements != null)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < _story.elements.Length; i++)
                    sb.AppendLine($"{i + 1}. <color=#{SwbstPalette.HexForIndex(i)}><b>{_story.elements[i].type}</b></color>: {_story.elements[i].correct}");
                referenceText.text = sb.ToString();
            }

            if (titleText != null) titleText.text = GameText.SummaryTitle;
            if (placeholderText != null) placeholderText.text = GameText.SummaryPlaceholder;
            if (submitLabel != null) submitLabel.text = GameText.SubmitLabel;
            if (hintText != null) hintText.text = GameText.SummaryHint;
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

            EnsureDoneTypingChip();
            if (doneTypingButton != null)
            {
                doneTypingButton.onClick.AddListener(StopTyping);
                doneTypingButton.gameObject.SetActive(false);
            }
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
            label.fontSizeMin = 16f;
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
