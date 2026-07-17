using System.Collections;
using PrimeTween;
using SummaRace.Constants;
using SummaRace.Core;
using SummaRace.Data;
using TMPro;
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
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Image[] starImages = new Image[3];
        [SerializeField] private TMP_Text praiseText;
        [SerializeField] private GameObject mainIdeaPanel;
        [SerializeField] private TMP_Text mainIdeaText;
        [SerializeField] private Button nextButton;

        [Header("Story treasure (chest + 5 SWBST gems)")]
        [SerializeField] private RectTransform treasureRow;
        [SerializeField] private Sprite chipSprite;

        // Sprite is already golden — off = dark silhouette, on = full color.
        private static readonly Color StarOff = new Color(0.35f, 0.35f, 0.38f);
        private static readonly Color StarOn = Color.white;

        private StoryData _story;

        private void Start()
        {
            _story = SummaRace.Core.GameManager.Instance != null ? SummaRace.Core.GameManager.Instance.CurrentStory : null;
            if (_story == null) _story = StoryLoader.Load("s01_easy"); // editor-direct fallback
            if (_story == null) { Debug.LogError("Results: no story."); return; }

            int stars = SummaRace.Core.GameManager.Instance != null ? SummaRace.Core.GameManager.Instance.CalculateStars() : 1;

            if (titleText != null) titleText.text = _story.title;
            if (praiseText != null) praiseText.text = "";
            if (mainIdeaPanel != null) mainIdeaPanel.SetActive(false);
            if (mainIdeaText != null) mainIdeaText.text = _story.mainIdea;

            foreach (var star in starImages)
                if (star != null) star.color = StarOff;

            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(false);
                nextButton.onClick.AddListener(OnNextMission);
            }

            if (SummaRace.Core.GameManager.Instance != null) SummaRace.Core.GameManager.Instance.CompleteStory(stars);
            StartCoroutine(RevealRoutine(stars));
        }

        private IEnumerator RevealRoutine(int stars)
        {
            yield return new WaitForSeconds(0.6f);

            for (int i = 0; i < stars; i++)
            {
                if (starImages[i] != null)
                {
                    starImages[i].color = StarOn;
                    Tween.PunchScale(starImages[i].transform, Vector3.one * 0.45f, 0.4f);
                }
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxStar);
                yield return new WaitForSeconds(0.45f);
            }

            yield return RevealTreasure();

            if (praiseText != null) praiseText.text = GameText.PraiseByStars[stars];
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMusic(AudioKeys.MusicVictory, false);

            yield return new WaitForSeconds(0.8f);
            if (mainIdeaPanel != null) mainIdeaPanel.SetActive(true);
            if (nextButton != null) nextButton.gameObject.SetActive(true);
        }

        /// <summary>
        /// The story's 5 SWBST "gems" pop in above the chest — full color when the
        /// first race pick was right, dimmed otherwise. Closes the Boot treasure metaphor.
        /// </summary>
        private IEnumerator RevealTreasure()
        {
            if (treasureRow == null) yield break;
            var result = SummaRace.Core.GameManager.Instance != null ? SummaRace.Core.GameManager.Instance.LastRaceResult : null;

            for (int i = 0; i < 5; i++)
            {
                bool earned = result == null || result.firstPickCorrect[i];

                var chip = new GameObject("Gem_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                chip.transform.SetParent(treasureRow, false);
                var rt = (RectTransform)chip.transform;
                rt.anchorMin = new Vector2(i * 0.2f + 0.015f, 0f);
                rt.anchorMax = new Vector2((i + 1) * 0.2f - 0.015f, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                var img = chip.GetComponent<Image>();
                if (chipSprite != null) { img.sprite = chipSprite; img.type = Image.Type.Sliced; }
                img.color = earned
                    ? SwbstPalette.ForIndex(i)
                    : Color.Lerp(SwbstPalette.ForIndex(i), new Color(0.6f, 0.6f, 0.6f), 0.65f);

                var letterGo = new GameObject("Letter", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                letterGo.transform.SetParent(chip.transform, false);
                var lrt = (RectTransform)letterGo.transform;
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
                var letter = letterGo.GetComponent<TextMeshProUGUI>();
                letter.text = _story.elements[i].type.Substring(0, 1);
                letter.alignment = TextAlignmentOptions.Center;
                letter.enableAutoSizing = true;
                letter.fontSizeMax = 46; letter.fontSizeMin = 10;
                letter.fontStyle = FontStyles.Bold;
                letter.color = earned ? Color.white : new Color(1f, 1f, 1f, 0.6f);

                chip.transform.localScale = Vector3.zero;
                Tween.Scale(chip.transform, Vector3.one, 0.3f, Ease.OutBack);
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxCoin);
                yield return new WaitForSeconds(0.16f);
            }
        }

        private void OnNextMission()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);
            if (SceneLoader.Instance != null) SceneLoader.Instance.Load(SceneNames.StorySelect);
        }
    }
}
