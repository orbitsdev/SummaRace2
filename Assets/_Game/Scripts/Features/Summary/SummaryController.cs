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

        private StoryData _story;
        private int _nudgeCount;

        private void Start()
        {
            _story = SummaRace.Core.GameManager.Instance != null ? SummaRace.Core.GameManager.Instance.CurrentStory : null;
            if (_story == null) _story = StoryLoader.Load("s01_easy"); // editor-direct fallback
            if (_story == null) { Debug.LogError("Summary: no story."); return; }

            if (referenceText != null)
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
            if (summaryInput != null) summaryInput.characterLimit = GameRules.SummaryMaxChars;
            if (submitButton != null) submitButton.onClick.AddListener(OnSubmit);
        }

        private void OnSubmit()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);

            string text = summaryInput != null ? summaryInput.text.Trim() : "";

            // Light checks (GDD §4.5) — nudge at most twice, then accept.
            if (_nudgeCount < GameRules.SummaryMaxNudges && !PassesLightChecks(text))
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxNotQuite);
                if (nudgeText != null)
                    nudgeText.text = GameText.SummaryNudges[Mathf.Min(_nudgeCount, GameText.SummaryNudges.Length - 1)];
                _nudgeCount++;
                return;
            }

            Accept(text);
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

            // Mentions the Somebody (any word of it, e.g. "Molly").
            string lower = text.ToLowerInvariant();
            foreach (var word in _story.elements[0].correct.ToLowerInvariant().Split(' '))
                if (word.Length > 2 && lower.Contains(word)) return true;

            return false;
        }

        private void Accept(string text)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxCorrect);
            if (SummaRace.Core.GameManager.Instance != null) SummaRace.Core.GameManager.Instance.LastSummaryText = text;

            EventBus.Raise(new SummarySubmitted { text = text, nudgeCount = _nudgeCount });
            SceneLoader.Go(SceneNames.Results);
        }
    }
}
