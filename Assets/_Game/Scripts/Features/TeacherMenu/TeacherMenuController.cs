using SummaRace.Constants;
using SummaRace.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Trash Dash ships its own GameManager in the global namespace, which outranks a
// using-directive, and an alias named GameManager is illegal for the same reason (CS0576).
using Core = SummaRace.Core;

namespace SummaRace.Features.TeacherMenu
{
    /// <summary>
    /// PIN-gated adult screen: open the next session, export logs, wipe data (GDD §8.3, §9.4).
    /// Deliberately plain — the measurement instrument is the paper rubric, so this only has to
    /// be reliable, not pretty. Nothing here is reachable without the PIN, which is what stops
    /// learners self-advancing through the ten scheduled sessions.
    /// </summary>
    public class TeacherMenuController : MonoBehaviour
    {
        [Header("Gate")]
        [SerializeField] private GameObject gatePanel;
        [SerializeField] private TMP_InputField pinInput;
        [SerializeField] private Button submitButton;
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private TMP_Text submitLabel;
        [SerializeField] private TMP_Text statusText;

        [Header("Actions")]
        [SerializeField] private GameObject actionsPanel;
        [SerializeField] private Button unlockButton;
        [SerializeField] private Button exportButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private TMP_Text deleteLabel;
        [SerializeField] private Button backButton;

        [Header("Action labels")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text unlockLabel;
        [SerializeField] private TMP_Text exportLabel;

        private bool _deleteArmed;

        private void Start()
        {
            if (titleText != null) titleText.text = GameText.TeacherTitle;
            if (unlockLabel != null) unlockLabel.text = GameText.TeacherUnlockNext;
            if (exportLabel != null) exportLabel.text = GameText.TeacherExport;
            if (deleteLabel != null) deleteLabel.text = GameText.TeacherDelete;
            if (submitLabel != null) submitLabel.text = GameText.TeacherSubmit;

            if (pinInput != null)
            {
                pinInput.contentType = TMP_InputField.ContentType.Pin;
                pinInput.text = string.Empty;
            }

            ShowGate();

            if (submitButton != null) submitButton.onClick.AddListener(Submit);
            if (unlockButton != null) unlockButton.onClick.AddListener(UnlockNext);
            if (exportButton != null) exportButton.onClick.AddListener(Export);
            if (deleteButton != null) deleteButton.onClick.AddListener(DeleteData);
            if (backButton != null)
                backButton.onClick.AddListener(() =>
                {
                    Click();
                    SceneLoader.Go(SceneNames.MainMenu);
                });
        }

        private void ShowGate()
        {
            // First launch has no PIN, so the same field is used to set one (§9 install checklist).
            bool hasPin = TeacherGate.HasPin();
            if (promptText != null)
                promptText.text = hasPin ? GameText.TeacherEnterPin : GameText.TeacherSetPin;
            if (statusText != null) statusText.text = string.Empty;
            if (gatePanel != null) gatePanel.SetActive(true);
            if (actionsPanel != null) actionsPanel.SetActive(false);
        }

        private void Submit()
        {
            Click();
            string pin = pinInput != null ? pinInput.text : string.Empty;

            if (!TeacherGate.HasPin())
            {
                if (!TeacherGate.SetPin(pin))
                {
                    Status(GameText.TeacherPinTooShort);
                    return;
                }
                OpenActions();
                return;
            }

            if (!TeacherGate.VerifyPin(pin))
            {
                Status(GameText.TeacherWrongPin);
                if (pinInput != null) pinInput.text = string.Empty;
                return;
            }
            OpenActions();
        }

        private void OpenActions()
        {
            if (pinInput != null) pinInput.text = string.Empty;
            if (gatePanel != null) gatePanel.SetActive(false);
            if (actionsPanel != null) actionsPanel.SetActive(true);
            _deleteArmed = false;
            if (deleteLabel != null) deleteLabel.text = GameText.TeacherDelete;
            Status(string.Empty);
        }

        /// <summary>Cancel a primed delete. Any other action counts as "not that, then":
        /// the confirm used to stay armed across Export and Unlock, so a researcher who
        /// tapped Delete, changed their mind, checked the export path, and later tapped
        /// Delete again meaning to re-read the warning would wipe every profile and every
        /// log on that device instead — irreversibly, mid-study.</summary>
        private void Disarm()
        {
            if (!_deleteArmed) return;
            _deleteArmed = false;
            if (deleteLabel != null) deleteLabel.text = GameText.TeacherDelete;
        }

        private void UnlockNext()
        {
            Click();
            Disarm();
            int session = TeacherGate.UnlockNextSession();
            if (session == 0)
            {
                Status(GameText.TeacherAllUnlocked);
                return;
            }
            // sfx_unlock is specified in the asset list but not yet produced; the rising star
            // pick reads correctly for "opened" until it exists.
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxStar);
            Status("Session " + session + " is now open.");
        }

        private void Export()
        {
            Click();
            Disarm();
            string path = SaveManager.Instance != null ? SaveManager.Instance.ExportLogs() : null;
            // Show the full path: the researcher has to find this file over USB.
            Status(string.IsNullOrEmpty(path) ? GameText.TeacherNothingToExport : path);
        }

        /// <summary>Two taps, because this is unrecoverable.</summary>
        private void DeleteData()
        {
            Click();
            if (!_deleteArmed)
            {
                _deleteArmed = true;
                if (deleteLabel != null) deleteLabel.text = GameText.TeacherDeleteConfirm;
                return;
            }

            if (SaveManager.Instance != null) SaveManager.Instance.DeleteAllData();
            if (Core.GameManager.Instance != null) Core.GameManager.Instance.InitProfiles();

            _deleteArmed = false;
            if (deleteLabel != null) deleteLabel.text = GameText.TeacherDelete;
            Status(GameText.TeacherDeleted);
        }

        private void Status(string message)
        {
            if (statusText != null) statusText.text = message;
        }

        private static void Click()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);
        }
    }
}
