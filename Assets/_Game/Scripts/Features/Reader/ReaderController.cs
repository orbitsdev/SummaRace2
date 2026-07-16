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
        [SerializeField] private TMP_Text pageText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text nextButtonLabel;

        [Header("Question")]
        [SerializeField] private GameObject questionPanel;
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private Button[] optionButtons = new Button[3];
        [SerializeField] private TMP_Text[] optionLabels = new TMP_Text[3];
        [SerializeField] private TMP_Text feedbackText;

        private static readonly Color OptionNormal = new Color(0.96f, 0.87f, 0.70f); // warm tan
        private static readonly Color OptionCorrect = new Color(0.55f, 0.85f, 0.45f); // friendly green

        private StoryData _story;
        private int _pageIndex;
        private bool _questionAnswered;
        private bool _questionShown;

        private void Start()
        {
            // Survive being opened directly in the editor (TDD §13).
            _story = GameManager.Instance != null ? GameManager.Instance.CurrentStory : null;
            if (_story == null) _story = StoryLoader.Load("s01_easy");
            if (_story == null)
            {
                Debug.LogError("Reader: no story available.");
                return;
            }

            if (nextButton != null) nextButton.onClick.AddListener(OnNext);
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
            if (pageText != null) pageText.text = page.text;
            if (progressText != null)
                progressText.text = $"Page {index + 1} / {_story.pages.Length}";

            if (questionPanel != null) questionPanel.SetActive(false);
            if (nextButton != null) nextButton.gameObject.SetActive(true);
            if (nextButtonLabel != null) nextButtonLabel.text = "NEXT";

            if (index > 0 && AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(AudioKeys.SfxPageTurn);
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
            if (questionPanel != null) questionPanel.SetActive(true);
            if (nextButton != null) nextButton.gameObject.SetActive(false);
            if (feedbackText != null) feedbackText.text = "";

            if (questionText != null) questionText.text = question.text;
            for (int i = 0; i < optionButtons.Length; i++)
            {
                if (optionButtons[i] == null) continue;
                optionButtons[i].interactable = true;
                optionButtons[i].image.color = OptionNormal;
                if (optionLabels[i] != null) optionLabels[i].text = question.options[i];
            }
        }

        private void OnAnswer(int chosenIndex)
        {
            if (_questionAnswered) return;
            _questionAnswered = true;

            var question = _story.pages[_pageIndex].question;
            bool correct = chosenIndex == question.correctIndex;

            // Always reveal the correct answer; never block (GDD §4.3).
            for (int i = 0; i < optionButtons.Length; i++)
            {
                if (optionButtons[i] == null) continue;
                optionButtons[i].interactable = false;
                if (i == question.correctIndex)
                    optionButtons[i].image.color = OptionCorrect;
            }

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(correct ? AudioKeys.SfxCorrect : AudioKeys.SfxNotQuite);

            if (feedbackText != null)
                feedbackText.text = correct ? "Great job!" : "Not quite — the green one is the answer!";

            EventBus.Raise(new PageAnswered
            {
                pageIndex = _pageIndex,
                chosenIndex = chosenIndex,
                correct = correct
            });

            if (nextButton != null) nextButton.gameObject.SetActive(true);
            if (nextButtonLabel != null)
                nextButtonLabel.text = _pageIndex == _story.pages.Length - 1 ? "START RACE!" : "NEXT PAGE";
        }

        private void Advance()
        {
            if (_pageIndex < _story.pages.Length - 1)
            {
                ShowPage(_pageIndex + 1);
                return;
            }

            EventBus.Raise(new ReadingCompleted());
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.Load(SceneNames.Race);
        }
    }
}
