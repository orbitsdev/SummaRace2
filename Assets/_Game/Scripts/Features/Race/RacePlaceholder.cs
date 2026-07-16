using SummaRace.Constants;
using SummaRace.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SummaRace.Features.Race
{
    /// <summary>
    /// Temporary stand-in until the real runner is built (Phase E4).
    /// Keeps the loop alive: never a crash, never a dead end (GDD §11.6).
    /// </summary>
    public class RacePlaceholder : MonoBehaviour
    {
        [SerializeField] private Button backButton;

        private void Start()
        {
            if (backButton != null)
                backButton.onClick.AddListener(() =>
                {
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);
                    if (SceneLoader.Instance != null)
                        SceneLoader.Instance.Load(SceneNames.MainMenu);
                });
        }
    }
}
