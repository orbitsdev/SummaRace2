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

        [Header("Select-level dressing (F22)")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Image[] starImages = new Image[3];
        [SerializeField] private TMP_Text easyChipText;
        [SerializeField] private TMP_Text averageChipText;
        [SerializeField] private TMP_Text hardChipText;
        [SerializeField] private TMP_Text[] lockedLabels = new TMP_Text[2];
        [SerializeField] private TMP_Text[] lockedHints = new TMP_Text[2];

        // Same silhouette trick as ResultsController: sprite is golden, off = dark.
        private static readonly Color StarOff = new Color(0.20f, 0.28f, 0.32f);
        private static readonly Color StarOn = Color.white;

        private void Start()
        {
            if (easyButton != null)
                easyButton.onClick.AddListener(() => SelectStory("s01_easy"));

            SetupTexts();
            SetupStars();
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

        /// <summary>All learner-facing strings come from GameText (GDD §7.4).</summary>
        private void SetupTexts()
        {
            if (titleText != null) titleText.text = GameText.StorySelectTitle;
            if (easyChipText != null) easyChipText.text = GameText.DifficultyEasy;
            if (averageChipText != null) averageChipText.text = GameText.DifficultyAverage;
            if (hardChipText != null) hardChipText.text = GameText.DifficultyHard;
            foreach (var label in lockedLabels)
                if (label != null) label.text = GameText.LockedLabel;
            foreach (var hint in lockedHints)
                if (hint != null) hint.text = GameText.LockedHint;
        }

        /// <summary>Best-stars row on the Easy card; editor-direct fallback shows silhouettes.</summary>
        private void SetupStars()
        {
            int best = GameManager.Instance != null ? GameManager.Instance.GetBestStars("s01_easy") : 0;
            for (int i = 0; i < starImages.Length; i++)
                if (starImages[i] != null) starImages[i].color = i < best ? StarOn : StarOff;
        }

        /// <summary>Hero image + title come from story data; missing art falls back to the title-only card (TDD §9.4).</summary>
        private void SetupEasyCard()
        {
            var story = StoryLoader.Load("s01_easy");
            if (story == null) return;

            if (easyLabel != null) easyLabel.text = story.title;

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
