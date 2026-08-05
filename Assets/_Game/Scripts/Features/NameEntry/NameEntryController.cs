using SummaRace.Constants;
using SummaRace.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Trash Dash ships its own GameManager in the global namespace, which outranks a
// using-directive, and an alias named GameManager is illegal for the same reason (CS0576).
using Core = SummaRace.Core;

namespace SummaRace.Features.NameEntry
{
    /// <summary>
    /// Names the learner on this device and picks an avatar (TDD §9.2). Every log line is
    /// attributed to this profile, so it is what makes the exported data identifiable.
    /// Shown once — Boot skips it after the learner has a name.
    /// </summary>
    public class NameEntryController : MonoBehaviour
    {
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button[] avatarButtons = new Button[4];

        [Header("Labels")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text avatarPromptText;
        [SerializeField] private TMP_Text confirmLabel;

        private static readonly Color AvatarOff = new Color(0.72f, 0.76f, 0.80f);
        private static readonly Color AvatarOn = Color.white;

        private int _avatarIndex;

        private void Start()
        {
            // On a fresh device this is the FIRST screen the learner ever sees (Boot routes here
            // before the Main Menu), and it was the only menu screen in the game with no music at
            // all — so the app opened silent, which on a classroom tablet is indistinguishable
            // from a device whose sound is broken. PlayMusic no-ops when the loop is already
            // running, so arriving here from the teacher's "+ New learner" costs nothing.
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMusic(AudioKeys.MusicMenu);

            if (titleText != null) titleText.text = GameText.NameEntryTitle;
            if (avatarPromptText != null) avatarPromptText.text = GameText.NameEntryPickAvatar;
            if (confirmLabel != null) confirmLabel.text = GameText.NameEntryConfirm;

            var learner = Core.GameManager.Instance != null
                ? Core.GameManager.Instance.CurrentLearner
                : null;

            if (nameInput != null)
            {
                // Re-entering keeps the existing name; a never-named profile starts blank so the
                // placeholder "Runner" is not something the learner has to delete.
                nameInput.text = learner != null && learner.named ? learner.displayName : string.Empty;
                if (nameInput.placeholder is TMP_Text placeholder)
                    placeholder.text = GameText.NameEntryHint;
            }

            _avatarIndex = learner != null ? Mathf.Clamp(learner.avatarIndex, 0, avatarButtons.Length - 1) : 0;

            for (int i = 0; i < avatarButtons.Length; i++)
            {
                if (avatarButtons[i] == null) continue;
                int index = i;                       // capture per button, not per loop
                avatarButtons[i].onClick.AddListener(() => SelectAvatar(index));
            }
            RefreshAvatars();

            if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
        }

        private void SelectAvatar(int index)
        {
            _avatarIndex = index;
            RefreshAvatars();
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);
        }

        private void RefreshAvatars()
        {
            for (int i = 0; i < avatarButtons.Length; i++)
            {
                if (avatarButtons[i] == null) continue;
                var image = avatarButtons[i].GetComponent<Image>();
                if (image != null) image.color = i == _avatarIndex ? AvatarOn : AvatarOff;
            }
        }

        private void Confirm()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);

            var learner = Core.GameManager.Instance != null
                ? Core.GameManager.Instance.CurrentLearner
                : null;

            if (learner != null)
            {
                // An empty box is never an error — the learner just keeps the default name.
                string typed = nameInput != null ? nameInput.text.Trim() : string.Empty;
                learner.displayName = string.IsNullOrEmpty(typed) ? GameText.DefaultLearnerName : typed;
                learner.avatarIndex = _avatarIndex;
                learner.named = true;
                Core.GameManager.Instance.PersistProfiles();
            }

            SceneLoader.Go(SceneNames.MainMenu);
        }
    }
}
