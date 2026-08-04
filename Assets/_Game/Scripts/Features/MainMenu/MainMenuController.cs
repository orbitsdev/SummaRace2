using SummaRace.Constants;
using SummaRace.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SummaRace.Features.MainMenu
{
    /// <summary>Entry screen: cheerful music + TAP TO START (TDD §9.2).</summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text startLabel;
        [SerializeField] private TMP_Text subtitleText;

        [Tooltip("Small, out-of-the-way adult entry point — everything behind it is PIN-gated.")]
        [SerializeField] private Button teacherButton;

        private void Start()
        {
            if (startLabel != null) startLabel.text = GameText.TapToStart;
            if (subtitleText != null) subtitleText.text = GameText.BootTagline;

            // Survive being opened directly in the editor for testing (TDD §13).
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayMusic(AudioKeys.MusicMenu);

            if (startButton != null)
                startButton.onClick.AddListener(OnStartTapped);

            if (teacherButton != null)
                teacherButton.onClick.AddListener(() =>
                {
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);
                    SceneLoader.Go(SceneNames.TeacherMenu);
                });
        }

        private void OnStartTapped()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);

            // Into the session map, so the learner picks a mission before a story (GDD §3.1).
            SceneLoader.Go(SceneNames.SessionMap);
        }
    }
}
