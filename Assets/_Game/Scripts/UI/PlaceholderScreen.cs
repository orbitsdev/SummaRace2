using SummaRace.Constants;
using SummaRace.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SummaRace.UI
{
    /// <summary>
    /// Generic "under construction" screen so unfinished scenes are never
    /// a dead end (GDD §11.6). Removed as real features replace them.
    /// </summary>
    public class PlaceholderScreen : MonoBehaviour
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
