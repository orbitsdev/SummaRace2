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
        // connected today; the two below are still {fileID: 0}, which is why the F15 splash
        // bar never appears. To finish them, add under SplashCanvas:
        //   • "LoadingText"  — TextMeshProUGUI under the lockup → drag onto `loadingText`.
        //   • "SplashBar"    — Image using Resources UI/bar_bg, with a child "Fill" Image
        //     using UI/bar_fill, Image Type = Filled, Fill Method = Horizontal, Origin = Left
        //     → drag the CHILD onto `splashFill`. (SceneLoader builds the same pair in code
        //     for its transition overlay — copy those proportions.)
        // All three stay optional: unwired, the splash simply runs without them.
        [SerializeField] private TMP_Text taglineText;
        [SerializeField] private TMP_Text loadingText;
        [SerializeField] private UnityEngine.UI.Image splashFill;

        private static bool _initialized;

        private void Awake()
        {
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
