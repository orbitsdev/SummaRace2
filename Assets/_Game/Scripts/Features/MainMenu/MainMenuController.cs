using SummaRace.Constants;
using SummaRace.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SummaRace.Features.MainMenu
{
    /// <summary>Entry screen: cheerful music + TAP TO START (TDD §9.2).</summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button startButton;

        private void Start()
        {
            // Survive being opened directly in the editor for testing (TDD §13).
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayMusic(AudioKeys.MusicMenu);

            if (startButton != null)
                startButton.onClick.AddListener(OnStartTapped);
        }

        private void OnStartTapped()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);

            if (SceneLoader.Instance != null)
                SceneLoader.Instance.Load(SceneNames.StorySelect);
        }
    }
}
