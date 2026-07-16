using System.Collections;
using SummaRace.Constants;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SummaRace.Core
{
    /// <summary>
    /// The single way the app changes screens: fade out → load → fade in,
    /// with a random SWBST tip during the load (TDD §7.3).
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        private const float FadeSeconds = 0.25f;

        private CanvasGroup _fadeGroup;
        private TextMeshProUGUI _tipText;
        private bool _loading;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildFadeCanvas();
        }

        public void Load(string sceneName)
        {
            if (_loading) return;
            StartCoroutine(LoadRoutine(sceneName));
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            _loading = true;
            _tipText.text = GameText.LoadingTips[Random.Range(0, GameText.LoadingTips.Length)];

            yield return Fade(0f, 1f);

            var op = SceneManager.LoadSceneAsync(sceneName);
            while (!op.isDone) yield return null;

            yield return Fade(1f, 0f);
            _loading = false;
        }

        private IEnumerator Fade(float from, float to)
        {
            _fadeGroup.blocksRaycasts = true;
            float t = 0f;
            while (t < FadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                _fadeGroup.alpha = Mathf.Lerp(from, to, t / FadeSeconds);
                yield return null;
            }
            _fadeGroup.alpha = to;
            _fadeGroup.blocksRaycasts = to > 0.5f;
        }

        /// <summary>Builds the persistent loading overlay in code: gradient sky, gold tip card (no prefab needed).</summary>
        private void BuildFadeCanvas()
        {
            var canvasGo = new GameObject("FadeCanvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // always on top

            // Deep-blue gradient backdrop (falls back to flat navy if sprite missing).
            var imageGo = new GameObject("FadeImage");
            imageGo.transform.SetParent(canvasGo.transform, false);
            var image = imageGo.AddComponent<Image>();
            var gradient = Resources.Load<Sprite>("UI/loading_gradient");
            if (gradient != null)
            {
                image.sprite = gradient;
                image.transform.localScale = new Vector3(1f, -1f, 1f); // deep at top
            }
            image.color = new Color(0.16f, 0.32f, 0.55f);
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Gold-bordered card holding the SWBST tip.
            var cardGo = new GameObject("TipCard");
            cardGo.transform.SetParent(canvasGo.transform, false);
            var card = cardGo.AddComponent<Image>();
            var goldPanel = Resources.Load<Sprite>("UI/panel_gold");
            if (goldPanel != null)
            {
                card.sprite = goldPanel;
                card.type = Image.Type.Sliced;
                card.pixelsPerUnitMultiplier = 0.6f;
            }
            else
            {
                card.color = new Color(0.98f, 0.93f, 0.80f);
            }
            var cardRect = card.rectTransform;
            cardRect.anchorMin = new Vector2(0.08f, 0.42f);
            cardRect.anchorMax = new Vector2(0.92f, 0.58f);
            cardRect.offsetMin = Vector2.zero;
            cardRect.offsetMax = Vector2.zero;

            var tipGo = new GameObject("TipText");
            tipGo.transform.SetParent(cardGo.transform, false);
            _tipText = tipGo.AddComponent<TextMeshProUGUI>();
            // Font comes from TMP Settings default (Nunito).
            _tipText.fontSize = 38;
            _tipText.alignment = TextAlignmentOptions.Center;
            _tipText.color = new Color(0.35f, 0.25f, 0.10f); // warm brown on cream
            var tipRect = _tipText.rectTransform;
            tipRect.anchorMin = new Vector2(0.06f, 0.10f);
            tipRect.anchorMax = new Vector2(0.94f, 0.90f);
            tipRect.offsetMin = Vector2.zero;
            tipRect.offsetMax = Vector2.zero;

            // Small "Loading..." above the card.
            var loadGo = new GameObject("LoadingText");
            loadGo.transform.SetParent(canvasGo.transform, false);
            var loading = loadGo.AddComponent<TextMeshProUGUI>();
            loading.text = GameText.LoadingLabel;
            loading.fontSize = 34;
            loading.alignment = TextAlignmentOptions.Center;
            loading.color = new Color(1f, 1f, 1f, 0.85f);
            var loadRect = loading.rectTransform;
            loadRect.anchorMin = new Vector2(0.2f, 0.585f);
            loadRect.anchorMax = new Vector2(0.8f, 0.635f);
            loadRect.offsetMin = Vector2.zero;
            loadRect.offsetMax = Vector2.zero;

            _fadeGroup = canvasGo.AddComponent<CanvasGroup>();
            _fadeGroup.alpha = 0f;
            _fadeGroup.blocksRaycasts = false;
        }
    }
}
