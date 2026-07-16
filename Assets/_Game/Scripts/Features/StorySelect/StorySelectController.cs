using SummaRace.Constants;
using SummaRace.Core;
using SummaRace.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SummaRace.Features.StorySelect
{
    /// <summary>
    /// Pick Easy/Average/Hard within the current session (TDD §9.4).
    /// MVP: only Easy ("The Playground") is playable; the others show locked.
    /// </summary>
    public class StorySelectController : MonoBehaviour
    {
        [SerializeField] private Button easyButton;
        [SerializeField] private Button averageButton;
        [SerializeField] private Button hardButton;
        [SerializeField] private Button backButton;

        [Header("Easy card content")]
        [SerializeField] private Image easyHeroImage;
        [SerializeField] private TMP_Text easyLabel;

        private void Start()
        {
            if (easyButton != null)
                easyButton.onClick.AddListener(() => SelectStory("s01_easy"));

            SetupEasyCard();

            // Locked for the MVP slice — gentle feedback only, never a dead end.
            SetupLocked(averageButton);
            SetupLocked(hardButton);

            if (backButton != null)
                backButton.onClick.AddListener(() =>
                {
                    PlayClick();
                    if (SceneLoader.Instance != null)
                        SceneLoader.Instance.Load(SceneNames.MainMenu);
                });
        }

        /// <summary>Hero image + title come from story data; missing art falls back to the title-only card (TDD §9.4).</summary>
        private void SetupEasyCard()
        {
            var story = StoryLoader.Load("s01_easy");
            if (story == null) return;

            if (easyLabel != null) easyLabel.text = "EASY\n" + story.title;

            if (easyHeroImage == null) return;
            var sprite = string.IsNullOrEmpty(story.heroImage) ? null : Resources.Load<Sprite>(story.heroImage);
            if (sprite != null) easyHeroImage.sprite = sprite;
            easyHeroImage.gameObject.SetActive(sprite != null);
        }

        private void SelectStory(string storyId)
        {
            PlayClick();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartStory(storyId);
            }
            else if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.Load(SceneNames.Reader);
            }
        }

        private static void SetupLocked(Button button)
        {
            if (button == null) return;
            button.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySfx(AudioKeys.SfxSlotWiggle);
            });
        }

        private static void PlayClick()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);
        }
    }
}
