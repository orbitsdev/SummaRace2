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
        private Image _barFill;
        private GameObject _tipCard;
        private GameObject _barRoot;
        private GameObject _loadingLabel;
        private bool _loading;

        /// <summary>The scene the running load is fetching, so a repeat request for that same
        /// scene can be told apart from a genuine rescue to a different one.</summary>
        private string _loadingScene;

        /// <summary>A scene requested while a load was already running, honoured when it ends.
        /// See <see cref="Load"/> for why dropping it was not survivable.</summary>
        private string _pendingScene;
        private bool _pendingShowTips;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildFadeCanvas();
        }

        /// <summary>
        /// The way features change scenes. Uses the loading overlay when the app booted
        /// normally, and falls back to a plain load when a scene is played directly in the
        /// editor (TDD §13) — Boot never ran there, so <see cref="Instance"/> is null.
        /// Call sites used to write `if (Instance != null) Instance.Load(...)` with no else,
        /// which turned every scene's exit button into a silent dead end in editor-direct
        /// play. Never raw-call SceneManager.LoadScene from a feature.
        /// </summary>
        public static void Go(string sceneName, bool showTips = true)
        {
            if (Instance != null) Instance.Load(sceneName, showTips);
            else SceneManager.LoadScene(sceneName);
        }

        /// <summary>Change scene with the tip-card loading overlay. Pass showTips=false for a
        /// plain quiet fade (used Boot→MainMenu so the splash never doubles as two loading pages).</summary>
        /// <remarks>
        /// A request that arrives while a load is running is REMEMBERED, not dropped.
        ///
        /// It used to be dropped, and the comment that justified it — "the new scene's Start()
        /// has already run by the time we clear the flag" — was wrong about Unity's player
        /// loop. Async scene integration and the new scene's `Start()` both run in EarlyUpdate
        /// (`UpdatePreloading`, then `ScriptRunDelayedStartupFrame`); a coroutine resumed by
        /// `yield return null` continues later the same frame, in the Update group. So clearing
        /// the flag anywhere inside this coroutine cannot beat a `Start()` — the ordering is
        /// structural, not a matter of how early in the routine you clear it.
        ///
        /// What that dropped: the four "never a dead end" rescues, which are the whole reason
        /// this matters. `ReaderController`, `ArrangeController`, `SummaryController` and
        /// `ResultsController` each call `SceneLoader.Go(StorySelect)` from `Start()` when their
        /// story is missing, and each `return`s immediately after — past the code that wires
        /// their buttons. Dropping that call leaves a fully drawn screen on which nothing works
        /// and, since `BackButtonGuard` swallows Android BACK, nothing the learner can do.
        /// `MainMenuController`'s Name Entry redirect is the same shape.
        ///
        /// Remembering it (rather than allowing a second concurrent load) keeps exactly one
        /// routine touching the overlay at a time, so the fade and the progress bar cannot be
        /// driven from two places at once.
        /// </remarks>
        public void Load(string sceneName, bool showTips = true)
        {
            if (_loading)
            {
                // A repeat request for the scene already being fetched is a double tap, not a
                // rescue. _loading is set before this routine's first yield, so the second
                // dispatch of one button press lands here — and queueing it made StartPending()
                // run the SAME scene a second time: another loading screen, another Start(), and
                // on a story card another StoryStarted for a run the learner began once.
                // Every genuine rescue targets a DIFFERENT scene (StorySelect from the four
                // story screens, NameEntry from MainMenu), so this cannot swallow one.
                if (sceneName == _loadingScene) return;

                // Last request wins: a rescue raised by the scene we just loaded is more
                // current than anything queued before it.
                _pendingScene = sceneName;
                _pendingShowTips = showTips;
                return;
            }
            StartCoroutine(LoadRoutine(sceneName, showTips));
        }

        private IEnumerator LoadRoutine(string sceneName, bool showTips)
        {
            _loading = true;
            _loadingScene = sceneName;
            if (_tipCard != null) _tipCard.SetActive(showTips);
            if (_barRoot != null) _barRoot.SetActive(showTips);
            if (_loadingLabel != null) _loadingLabel.SetActive(showTips);
            int tip = Random.Range(0, GameText.LoadingTips.Length);
            _tipText.text = GameText.LoadingTips[tip];
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopNarration(); // voice never bleeds into the next scene
                if (showTips)
                {
                    AudioManager.Instance.PlaySfx(AudioKeys.SfxTransition);
                    // The tip is where the framework is TAUGHT, and it is pure interface text —
                    // a learner who cannot read it loses the definition entirely. Bounds-checked
                    // against the voice array's own length so adding a written tip without a
                    // clip degrades to silence rather than throwing mid-scene-change.
                    if (tip < AudioKeys.VoLoadingTips.Length)
                        AudioManager.Instance.PlayVoice(AudioKeys.VoLoadingTips[tip], true);
                }
            }

            _barFill.fillAmount = 0f;
            yield return Fade(0f, 1f);

            // Fill tracks real load progress but sweeps at a capped speed so the
            // bar reads as motion even on near-instant loads. Tip-less fades skip
            // the sweep — nothing visible is animating, so don't hold the load.
            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                // LoadSceneAsync returns null when the scene is not in Build Settings. Without
                // this guard the coroutine died on the next line, leaving _loading stuck true
                // behind an opaque, raycast-blocking overlay — the learner sees a frozen
                // loading card forever and every later Go() early-returns on _loading. That is
                // not hypothetical here: the build list has been wiped twice by imports.
                Debug.LogError("SceneLoader: scene '" + sceneName +
                    "' is not in Build Settings. Falling back to the main menu.");
                yield return Fade(1f, 0f);
                _loading = false;   // after the fade, so nothing starts a second routine over it
                if (sceneName != SceneNames.MainMenu) Go(SceneNames.MainMenu);
                yield break;
            }

            if (showTips)
            {
                op.allowSceneActivation = false;
                while (_barFill.fillAmount < 0.999f)
                {
                    float target = op.progress < 0.9f ? op.progress / 0.9f : 1f;
                    _barFill.fillAmount = Mathf.MoveTowards(_barFill.fillAmount, target, Time.unscaledDeltaTime * 2.5f);
                    yield return null;
                }
                op.allowSceneActivation = true;
            }
            while (!op.isDone) yield return null;

            // The scene we just loaded may have asked to go somewhere else from its own Start()
            // — that is the "never a dead end" rescue path. Go straight there WITHOUT fading in
            // first: the learner has no business seeing a broken screen for half a second, and
            // the overlay is already opaque, so this reads as one continuous load.
            //
            // _loading deliberately stays true across the fade below, so a request arriving
            // during it is queued rather than starting a second routine that would drive the
            // same fade group in the opposite direction.
            if (StartPending()) yield break;

            yield return Fade(1f, 0f);

            _loading = false;
            StartPending();
        }

        /// <summary>Hands the routine over to a queued request, if there is one. Returns true if
        /// it did, in which case the caller must stop — the new routine owns the overlay.</summary>
        private bool StartPending()
        {
            if (string.IsNullOrEmpty(_pendingScene)) return false;
            var next = _pendingScene;
            bool nextTips = _pendingShowTips;
            _pendingScene = null;
            _loading = false;               // so Load() actually starts rather than re-queueing
            Load(next, nextTips);
            return true;
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

            // Bright playground-trail backdrop — same identity as the Boot splash.
            var imageGo = new GameObject("FadeImage");
            imageGo.transform.SetParent(canvasGo.transform, false);
            var image = imageGo.AddComponent<Image>();
            var splash = Resources.Load<Sprite>("UI/bg_splash");
            if (splash != null) image.sprite = splash;
            else image.color = new Color(0.55f, 0.83f, 0.98f); // flat sky fallback matches the art's tone
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Gold-bordered card holding the SWBST tip.
            var cardGo = new GameObject("TipCard");
            _tipCard = cardGo;
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

            // Gold progress bar under the tip card.
            var barGo = new GameObject("ProgressBar");
            _barRoot = barGo;
            barGo.transform.SetParent(canvasGo.transform, false);
            var barBg = barGo.AddComponent<Image>();
            var barBgSprite = Resources.Load<Sprite>("UI/bar_bg");
            if (barBgSprite != null) { barBg.sprite = barBgSprite; barBg.type = Image.Type.Sliced; }
            else barBg.color = new Color(0.08f, 0.14f, 0.28f, 0.9f);
            var barRect = barBg.rectTransform;
            barRect.anchorMin = new Vector2(0.18f, 0.365f);
            barRect.anchorMax = new Vector2(0.82f, 0.395f);
            barRect.offsetMin = Vector2.zero;
            barRect.offsetMax = Vector2.zero;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(barGo.transform, false);
            _barFill = fillGo.AddComponent<Image>();
            var barFillSprite = Resources.Load<Sprite>("UI/bar_fill");
            if (barFillSprite != null) _barFill.sprite = barFillSprite;
            else _barFill.color = new Color(1f, 0.78f, 0.20f);
            _barFill.type = Image.Type.Filled;
            _barFill.fillMethod = Image.FillMethod.Horizontal;
            _barFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _barFill.fillAmount = 0f;
            var fillRect = _barFill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(5f, 5f);
            fillRect.offsetMax = new Vector2(-5f, -5f);

            // Small "Loading..." above the card.
            var loadGo = new GameObject("LoadingText");
            _loadingLabel = loadGo;
            loadGo.transform.SetParent(canvasGo.transform, false);
            var loading = loadGo.AddComponent<TextMeshProUGUI>();
            loading.text = GameText.LoadingLabel;
            loading.fontSize = 34;
            loading.alignment = TextAlignmentOptions.Center;
            loading.color = new Color(0.11f, 0.17f, 0.33f, 0.95f); // deep navy — readable on the bright sky
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
