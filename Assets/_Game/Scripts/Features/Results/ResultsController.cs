using System.Collections;
using SummaRace.Constants;
using SummaRace.Core;
using SummaRace.Data;
using UnityEngine;
using UnityEngine.UI;

namespace SummaRace.Features.Results
{
    /// <summary>
    /// Stars + praise + main-idea reveal (TDD §10.4). Stars come from
    /// first-pick race accuracy; finishing always earns at least one.
    /// </summary>
    public class ResultsController : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text[] starTexts = new Text[3];
        [SerializeField] private Text praiseText;
        [SerializeField] private GameObject mainIdeaPanel;
        [SerializeField] private Text mainIdeaText;
        [SerializeField] private Button nextButton;

        private static readonly Color StarOff = new Color(0.75f, 0.75f, 0.7f);
        private static readonly Color StarOn = new Color(1f, 0.8f, 0.1f);

        private StoryData _story;

        private void Start()
        {
            _story = GameManager.Instance != null ? GameManager.Instance.CurrentStory : null;
            if (_story == null) _story = StoryLoader.Load("s01_easy"); // editor-direct fallback
            if (_story == null) { Debug.LogError("Results: no story."); return; }

            int stars = GameManager.Instance != null ? GameManager.Instance.CalculateStars() : 1;

            if (titleText != null) titleText.text = _story.title;
            if (praiseText != null) praiseText.text = "";
            if (mainIdeaPanel != null) mainIdeaPanel.SetActive(false);
            if (mainIdeaText != null) mainIdeaText.text = _story.mainIdea;

            foreach (var star in starTexts)
                if (star != null) star.color = StarOff;

            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(false);
                nextButton.onClick.AddListener(OnNextMission);
            }

            if (GameManager.Instance != null) GameManager.Instance.CompleteStory(stars);
            StartCoroutine(RevealRoutine(stars));
        }

        private IEnumerator RevealRoutine(int stars)
        {
            yield return new WaitForSeconds(0.6f);

            for (int i = 0; i < stars; i++)
            {
                if (starTexts[i] != null) starTexts[i].color = StarOn;
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxStar);
                yield return new WaitForSeconds(0.45f);
            }

            if (praiseText != null) praiseText.text = GameText.PraiseByStars[stars];
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMusic(AudioKeys.MusicVictory, false);

            yield return new WaitForSeconds(0.8f);
            if (mainIdeaPanel != null) mainIdeaPanel.SetActive(true);
            if (nextButton != null) nextButton.gameObject.SetActive(true);
        }

        private void OnNextMission()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);
            if (SceneLoader.Instance != null) SceneLoader.Instance.Load(SceneNames.StorySelect);
        }
    }
}
