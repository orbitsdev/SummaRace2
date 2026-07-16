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

        /// <summary>Builds the persistent full-screen fade overlay in code (no prefab needed).</summary>
        private void BuildFadeCanvas()
        {
            var canvasGo = new GameObject("FadeCanvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // always on top

            var imageGo = new GameObject("FadeImage");
            imageGo.transform.SetParent(canvasGo.transform, false);
            var image = imageGo.AddComponent<Image>();
            image.color = Color.black;
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var tipGo = new GameObject("TipText");
            tipGo.transform.SetParent(canvasGo.transform, false);
            _tipText = tipGo.AddComponent<TextMeshProUGUI>();
            // Font comes from TMP Settings default (Nunito) — no explicit assignment needed.
            _tipText.fontSize = 36;
            _tipText.alignment = TextAlignmentOptions.Center;
            _tipText.color = Color.white;
            var tipRect = _tipText.rectTransform;
            tipRect.anchorMin = new Vector2(0.1f, 0.4f);
            tipRect.anchorMax = new Vector2(0.9f, 0.6f);
            tipRect.offsetMin = Vector2.zero;
            tipRect.offsetMax = Vector2.zero;

            _fadeGroup = canvasGo.AddComponent<CanvasGroup>();
            _fadeGroup.alpha = 0f;
            _fadeGroup.blocksRaycasts = false;
        }
    }
}
