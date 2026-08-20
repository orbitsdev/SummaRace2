using System.Collections;
using SummaRace.Constants;
using TMPro;
using UnityEngine;

namespace SummaRace.Core
{
    /// <summary>
    /// Runs first (Boot scene). Creates the persistent singletons, loads settings,
    /// then routes to MainMenu (TDD §7.1).
    /// </summary>
    public class Bootstrapper : MonoBehaviour
    {
        // Scene wiring (Boot.unity → SplashCanvas → Bootstrapper). Only `taglineText` is
        // connected in the scene. The other two were {fileID: 0} for the whole of F15..F58 —
        // the objects were never added under SplashCanvas, and both fields are only
        // null-guarded, so the splash bar this project believed it shipped has never once
        // appeared and the "Loading..." label under it has never been drawn. It is the first
        // screen a learner sees, which is exactly why nobody caught it: the splash still runs
        // for its GameRules.SplashSeconds either way, so nothing looks broken.
        //
        // Rather than ask for two more objects to be dragged in (which is what the previous
        // note here asked for, and which stayed undone), EnsureSplashChrome() builds them at
        // runtime when they are unwired — the same pattern ReaderController.EnsureSecondaryControls
        // and Summary/NameEntry's EnsureDoneTypingChip already use for optional chrome. A scene
        // that DOES wire them keeps its own objects untouched.
        // All three stay optional: with the build skipped, the splash simply runs without them.
        [SerializeField] private TMP_Text taglineText;
        [SerializeField] private TMP_Text loadingText;
        [SerializeField] private UnityEngine.UI.Image splashFill;

        private static bool _initialized;

        private void Awake()
        {
            // Before the two reads below, and before the _initialized guard: a Boot re-entry
            // skips singleton creation but still plays the splash, so it still needs chrome.
            EnsureSplashChrome();

            if (taglineText != null) taglineText.text = GameText.BootTagline;
            if (loadingText != null) loadingText.text = GameText.LoadingLabel;

            if (_initialized) return;
            _initialized = true;

            Application.targetFrameRate = GameRules.TargetFrameRate;

            var core = new GameObject("[Core]");
            DontDestroyOnLoad(core);
            // Single app-wide listener; scene cameras deliberately have none (audio is all 2D).
            core.AddComponent<AudioListener>();
            core.AddComponent<GameManager>();
            core.AddComponent<AudioManager>();
            core.AddComponent<SaveManager>();
            core.AddComponent<SceneLoader>();
            core.AddComponent<SessionLogService>();
            // Stops the Android BACK gesture closing the app mid-story — see BackButtonGuard.
            core.AddComponent<BackButtonGuard>();

            var settings = core.GetComponent<SaveManager>().LoadSettings();
            core.GetComponent<AudioManager>().SetVolumes(settings);

            // Activate a learner before any scene can read progress or write a log.
            core.GetComponent<GameManager>().InitProfiles();

            EventBus.Raise(new AppReady());
        }


        /// <summary>
        /// Builds the splash's "Loading..." label and progress bar when the scene carries no
        /// objects for them, so the beat the learner watches has something in it. Anything the
        /// scene DOES wire is left alone — this only fills gaps.
        ///
        /// Geometry is deliberately copied from SceneLoader's transition overlay (bar at
        /// y 0.365-0.395, same bar_bg/bar_fill sprites, same 5px fill inset) so the bar does
        /// not jump position when the splash hands over to the first real scene load. The
        /// label sits at y 0.425-0.465 rather than the overlay's 0.585, because in Boot that
        /// band is inside LogoLockup (0.545-0.795) and Tagline (0.51-0.57); everything below
        /// 0.51 is free. Reference resolution here is 1080x1920.
        ///
        /// Both sprites are optional: Resources.Load returning null falls back to flat colour,
        /// exactly as SceneLoader does, so a stripped build degrades instead of throwing.
        /// </summary>
        private void EnsureSplashChrome()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return; // no canvas => nothing to hang chrome on; splash still runs
            var root = canvas.transform;

            if (loadingText == null)
            {
                var loadGo = new GameObject("LoadingText", typeof(RectTransform));
                loadGo.transform.SetParent(root, false);
                var label = loadGo.AddComponent<TextMeshProUGUI>();
                label.text = GameText.LoadingLabel;
                label.fontSize = 34;
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
                label.color = Theme.Alpha(Theme.Navy, 0.95f);
                var lr = label.rectTransform;
                lr.anchorMin = new Vector2(0.2f, 0.425f);
                lr.anchorMax = new Vector2(0.8f, 0.465f);
                lr.offsetMin = Vector2.zero;
                lr.offsetMax = Vector2.zero;
                loadingText = label;
            }

            if (splashFill == null)
            {
                var barGo = new GameObject("SplashBar", typeof(RectTransform));
                barGo.transform.SetParent(root, false);
                var barBg = barGo.AddComponent<UnityEngine.UI.Image>();
                barBg.raycastTarget = false;
                var barBgSprite = Resources.Load<Sprite>("UI/bar_bg");
                if (barBgSprite != null) { barBg.sprite = barBgSprite; barBg.type = UnityEngine.UI.Image.Type.Sliced; }
                else barBg.color = new Color(0.08f, 0.14f, 0.28f, 0.9f);
                var br = barBg.rectTransform;
                br.anchorMin = new Vector2(0.18f, 0.365f);
                br.anchorMax = new Vector2(0.82f, 0.395f);
                br.offsetMin = Vector2.zero;
                br.offsetMax = Vector2.zero;

                var fillGo = new GameObject("Fill", typeof(RectTransform));
                fillGo.transform.SetParent(barGo.transform, false);
                var fill = fillGo.AddComponent<UnityEngine.UI.Image>();
                fill.raycastTarget = false;
                var barFillSprite = Resources.Load<Sprite>("UI/bar_fill");
                if (barFillSprite != null) fill.sprite = barFillSprite;
                else fill.color = Theme.GoldDeep;
                fill.type = UnityEngine.UI.Image.Type.Filled;
                fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
                fill.fillAmount = 0f;
                var fr = fill.rectTransform;
                fr.anchorMin = Vector2.zero;
                fr.anchorMax = Vector2.one;
                fr.offsetMin = new Vector2(5f, 5f);
                fr.offsetMax = new Vector2(-5f, -5f);
                splashFill = fill;
            }
        }

        private IEnumerator Start()
        {
            // Sound comes from the lockup's own PanelIntro pop — nothing extra here.
            // Brief splash beat, then a quiet tip-less fade into the menu — the
            // splash IS the startup loading screen (no second loading page).
            float t = 0f;
            while (t < GameRules.SplashSeconds)
            {
                t += Time.deltaTime;
                // Real elapsed time against the splash beat — the singletons above are already
                // built, so there is nothing else left to measure. Unwired, the beat is
                // unchanged: the splash just holds silently for the same duration.
                if (splashFill != null) splashFill.fillAmount = Mathf.Clamp01(t / GameRules.SplashSeconds);
                yield return null;
            }
            // First run on a device goes to Name Entry once, so every exported log line is
            // attributable; afterwards the learner is already named and it is skipped.
            var learner = GameManager.Instance != null ? GameManager.Instance.CurrentLearner : null;
            bool needsName = learner != null && !learner.named;
            // Via Go, so a Boot re-entry that skipped singleton creation still leaves the splash.
            SceneLoader.Go(needsName ? SceneNames.NameEntry : SceneNames.MainMenu, false);
        }
    }
}
