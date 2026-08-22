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
        // Scene wiring. CORRECTION (the old note here said "Boot.unity → SplashCanvas →
        // Bootstrapper", and that is not the hierarchy): `Bootstrapper` is a ROOT GameObject,
        // a SIBLING of `SplashCanvas`, carrying nothing but its Transform. Only `taglineText`
        // is connected in the scene; the other two have been {fileID: 0} for the whole of
        // F15..F58 and both fields are only null-guarded, so the splash bar this project
        // believed it shipped has never once appeared and the "Loading..." label under it has
        // never been drawn. It is the first screen a learner sees, which is exactly why nobody
        // caught it: the splash still runs for its GameRules.SplashSeconds either way, so
        // nothing looks broken.
        //
        // Rather than ask for two more objects to be dragged in (which is what the previous
        // note here asked for, and which stayed undone), EnsureSplashChrome() builds them at
        // runtime when they are unwired — the same pattern ReaderController.EnsureSecondaryControls
        // and Summary/NameEntry's EnsureDoneTypingChip already use for optional chrome. A scene
        // that DOES wire them keeps its own objects untouched. That build ALSO never ran, for
        // the hierarchy reason above — see ResolveSplashCanvas().
        // All three stay optional: with the build skipped, the splash simply runs without them.
        [SerializeField] private TMP_Text taglineText;
        [SerializeField] private TMP_Text loadingText;
        [SerializeField] private UnityEngine.UI.Image splashFill;

        private static bool _initialized;

        /// <summary>The [Core] object this run built, kept so a failed build can be torn down
        /// again rather than left half-assembled with some singletons live and others not.</summary>
        private GameObject _core;

        /// <summary>Set when the [Core] build threw. Start() must not route on from here — see
        /// <see cref="ShowBootFailure"/> for why leaving is worse than staying.</summary>
        private bool _bootFailed;

        /// <summary>Resolved once in Awake and reused by the failure card, so the recovery UI
        /// cannot fail for the same reason the chrome did.</summary>
        private Transform _splashRoot;

        private void Awake()
        {
            // Before the two reads below, and before the _initialized guard: a Boot re-entry
            // skips singleton creation but still plays the splash, so it still needs chrome.
            EnsureSplashChrome();

            if (taglineText != null) taglineText.text = GameText.BootTagline;
            if (loadingText != null) loadingText.text = GameText.LoadingLabel;

            if (_initialized) return;
            _initialized = true;

            // THE ONE THING HERE THAT MUST NOT FAIL SILENTLY. Every singleton the whole game
            // reads through — GameManager, SaveManager, SceneLoader, AudioManager,
            // SessionLogService, BackButtonGuard — is built in the single unprotected block
            // below, and the _initialized latch above means a throw part-way through was
            // permanent for the life of the process: the next scene still loaded, but with no
            // [Core] at all. On MainMenu that is a fully drawn screen where TAP TO START does
            // nothing (SceneLoader.Go no-ops without an Instance), the teacher corner does
            // nothing, there is no music, and Android BACK is NOT swallowed only because
            // BackButtonGuard never got installed — a bricked tablet with no message and no
            // way out. Real triggers exist: SaveManager.LoadSettings touches the file system
            // (a corrupt or unreadable settings.json), and InitProfiles reads and writes
            // profiles. So: catch it, unwind the half-built core, and put a visible retry on
            // the splash instead of walking on into a dead menu.
            try
            {
                BuildCore();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);

                // Unlatch so the retry is allowed to rebuild. Safe to do here and nowhere else:
                // the core was torn down on the very next line, so no second [Core] can appear.
                _initialized = false;
                _bootFailed = true;

                if (_core != null) { Destroy(_core); _core = null; }
                ShowBootFailure();
            }
        }

        /// <summary>
        /// Builds the persistent [Core] singletons. Extracted from Awake purely so the whole
        /// block sits inside one try — a half-built core (say AudioManager alive but
        /// GameManager not) is worse than none, because callers null-check each singleton
        /// separately and would take half the happy path.
        /// </summary>
        private void BuildCore()
        {
            Application.targetFrameRate = GameRules.TargetFrameRate;

            _core = new GameObject("[Core]");
            DontDestroyOnLoad(_core);
            // Single app-wide listener; scene cameras deliberately have none (audio is all 2D).
            _core.AddComponent<AudioListener>();
            _core.AddComponent<GameManager>();
            _core.AddComponent<AudioManager>();
            _core.AddComponent<SaveManager>();
            _core.AddComponent<SceneLoader>();
            _core.AddComponent<SessionLogService>();
            // Stops the Android BACK gesture closing the app mid-story — see BackButtonGuard.
            _core.AddComponent<BackButtonGuard>();

            var settings = _core.GetComponent<SaveManager>().LoadSettings();
            _core.GetComponent<AudioManager>().SetVolumes(settings);

            // Activate a learner before any scene can read progress or write a log.
            _core.GetComponent<GameManager>().InitProfiles();

            EventBus.Raise(new AppReady());
        }

        /// <summary>
        /// The startup recovery card: a scrim, one line of plain language and a TRY AGAIN
        /// button that reloads Boot from scratch. Deliberately STAYS on Boot rather than
        /// routing anywhere — Boot is the only scene that can rebuild [Core], so leaving it
        /// is the one move that makes the failure permanent.
        ///
        /// Reloads through UnityEngine.SceneManagement directly, never SceneLoader.Go: the
        /// thing that just failed may well BE SceneLoader, and Go() silently no-ops without
        /// an Instance, which would make the retry button itself a dead end.
        ///
        /// Strings are literals here, against the project's GameText rule, because Constants/
        /// is out of scope for this pass — see the report for the exact GameText anchor. A
        /// hardcoded sentence that appears is still better than a correct constant nobody can
        /// reach because the app is bricked.
        /// </summary>
        private void ShowBootFailure()
        {
            var root = _splashRoot;
            if (root == null)
            {
                // No canvas at all — nothing to draw on. The log line above is then the only
                // record, so say plainly in it what the teacher should do.
                Debug.LogError("SummaRace: startup failed and there is no canvas to show it on. " +
                               "Close the app fully and open it again.");
                return;
            }

            // BOOT CANNOT RECEIVE A TAP AS IT SHIPS. Verified in Boot.unity: SplashCanvas
            // carries only Canvas + CanvasScaler (no GraphicRaycaster) and the scene has no
            // EventSystem at all — it never needed either, because nothing on the splash is
            // interactive. A TRY AGAIN button built without both is a button that cannot be
            // pressed, which is a worse dead end than no button. Both are added here rather
            // than asked for in the scene, so the recovery path carries its own plumbing.
            var rootCanvas = root.GetComponent<Canvas>();
            if (rootCanvas != null && rootCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                rootCanvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var esGo = new GameObject("BootFailureEventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem));
                // New Input System only for our own code (see CLAUDE.md ▸ Input); the legacy
                // StandaloneInputModule throws under "New only" and would silently kill the tap.
                esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            // Scrim first, and it DOES take raycasts (unlike every other decorative overlay in
            // this project): the splash chrome behind it is now lying about progress, so the
            // card must own every tap on the screen.
            var scrimGo = new GameObject("BootFailureScrim", typeof(RectTransform));
            scrimGo.transform.SetParent(root, false);
            var scrim = scrimGo.AddComponent<UnityEngine.UI.Image>();
            scrim.color = new Color(0f, 0f, 0f, 0.72f);
            scrim.raycastTarget = true;
            var sr = scrim.rectTransform;
            sr.anchorMin = Vector2.zero;
            sr.anchorMax = Vector2.one;
            sr.offsetMin = Vector2.zero;
            sr.offsetMax = Vector2.zero;
            sr.SetAsLastSibling();

            var cardGo = new GameObject("BootFailureCard", typeof(RectTransform));
            cardGo.transform.SetParent(scrimGo.transform, false);
            var card = cardGo.AddComponent<UnityEngine.UI.Image>();
            var panel = Resources.Load<Sprite>("UI/bar_bg");
            if (panel != null) { card.sprite = panel; card.type = UnityEngine.UI.Image.Type.Sliced; card.color = Color.white; }
            else card.color = Theme.Alpha(Theme.Navy, 0.98f);
            var cr = card.rectTransform;
            cr.anchorMin = new Vector2(0.08f, 0.36f);
            cr.anchorMax = new Vector2(0.92f, 0.64f);
            cr.offsetMin = Vector2.zero;
            cr.offsetMax = Vector2.zero;

            var msgGo = new GameObject("Message", typeof(RectTransform));
            msgGo.transform.SetParent(cardGo.transform, false);
            var msg = msgGo.AddComponent<TextMeshProUGUI>();
            // Plain language, no error code, no blame on the child: a Grade-4 learner may be
            // the one holding the tablet when this appears.
            msg.text = GameText.BootFailedBody;
            msg.fontSize = 34f;
            msg.enableAutoSizing = true;
            msg.fontSizeMin = 24f;
            msg.fontSizeMax = 34f;
            msg.alignment = TextAlignmentOptions.Center;
            msg.color = Theme.Paper;
            msg.raycastTarget = false;
            var mr = msg.rectTransform;
            mr.anchorMin = new Vector2(0.06f, 0.42f);
            mr.anchorMax = new Vector2(0.94f, 0.94f);
            mr.offsetMin = Vector2.zero;
            mr.offsetMax = Vector2.zero;

            var btnGo = new GameObject("TryAgain", typeof(RectTransform));
            btnGo.transform.SetParent(cardGo.transform, false);
            var btnImage = btnGo.AddComponent<UnityEngine.UI.Image>();
            btnImage.color = Theme.GoldDeep;
            var brt = btnImage.rectTransform;
            brt.anchorMin = new Vector2(0.24f, 0.10f);
            brt.anchorMax = new Vector2(0.76f, 0.34f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;

            var btnLabelGo = new GameObject("Label", typeof(RectTransform));
            btnLabelGo.transform.SetParent(btnGo.transform, false);
            var btnLabel = btnLabelGo.AddComponent<TextMeshProUGUI>();
            btnLabel.text = GameText.BootFailedRetry;
            btnLabel.fontSize = 34f;
            btnLabel.enableAutoSizing = true;
            btnLabel.fontSizeMin = 24f;
            btnLabel.fontSizeMax = 34f;
            btnLabel.alignment = TextAlignmentOptions.Center;
            btnLabel.color = Theme.TextBrownDeep;   // dark on gold; gold-on-anything fails as text
            btnLabel.raycastTarget = false;
            var blr = btnLabel.rectTransform;
            blr.anchorMin = Vector2.zero;
            blr.anchorMax = Vector2.one;
            blr.offsetMin = new Vector2(8f, 4f);
            blr.offsetMax = new Vector2(-8f, -4f);

            var button = btnGo.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = btnImage;
            button.onClick.AddListener(() =>
                UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.Boot));

            // No sound on this button on purpose: AudioManager is exactly one of the things
            // that may have failed to build, and a null-check that reads as "silence" here
            // would be indistinguishable from the button not working.
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
            var root = ResolveSplashCanvas();
            _splashRoot = root;
            if (root == null) return; // no canvas => nothing to hang chrome on; splash still runs

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

        /// <summary>
        /// The canvas this scene's splash lives on.
        ///
        /// THIS USED TO BE GetComponentInParent&lt;Canvas&gt;(), AND THAT COULD NEVER MATCH.
        /// Verified in Boot.unity: `Bootstrapper` is a ROOT GameObject whose only other
        /// component is its Transform — it is a sibling of `SplashCanvas`, not a child of it.
        /// So the lookup returned null, EnsureSplashChrome() returned on its first line, and
        /// neither the "Loading..." label nor the progress bar was ever built; Start()'s
        /// `splashFill.fillAmount` write then null-skipped on every frame of every launch.
        /// Nothing looked broken because the splash still holds for its full SplashSeconds,
        /// which is why it survived F15..F58. The scene is out of scope for this pass, so the
        /// canvas is resolved from the SCENE instead of from the parent chain.
        ///
        /// Scoped to `gameObject.scene` for the same reason NameEntryController.ResolveSceneCanvas
        /// is: a blind FindAnyObjectByType would happily return SceneLoader's persistent
        /// FadeCanvas ([Core], DontDestroyOnLoad, alpha 0) on a Boot re-entry, and the chrome
        /// would be built invisible on the loading overlay.
        /// </summary>
        private Transform ResolveSplashCanvas()
        {
            // Anything the scene DOES wire names its own canvas, which beats searching.
            var canvas = taglineText != null ? taglineText.GetComponentInParent<Canvas>() : null;
            if (canvas == null && loadingText != null) canvas = loadingText.GetComponentInParent<Canvas>();
            if (canvas == null && splashFill != null) canvas = splashFill.GetComponentInParent<Canvas>();
            // Kept as a fallback rather than deleted: a scene that ever DOES parent the
            // Bootstrapper under its canvas still resolves.
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (canvas != null) return canvas.rootCanvas.transform;

            foreach (var candidate in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (candidate.gameObject.scene == gameObject.scene)
                    return candidate.rootCanvas.transform;

            Debug.LogWarning("Boot: no scene canvas found — splash chrome not built.");
            return null;
        }

        private IEnumerator Start()
        {
            // The [Core] build failed and the recovery card is up. Walking on from here is the
            // one move that makes the failure permanent — MainMenu cannot rebuild the
            // singletons and Boot is the only scene that can, so hold here.
            if (_bootFailed) yield break;

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
