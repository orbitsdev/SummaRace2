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

            var settings = core.GetComponent<SaveManager>().LoadSettings();
            core.GetComponent<AudioManager>().SetVolumes(settings);

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
                if (splashFill != null) splashFill.fillAmount = Mathf.Clamp01(t / GameRules.SplashSeconds);
                yield return null;
            }
            SceneLoader.Instance.Load(SceneNames.MainMenu, false);
        }
    }
}
