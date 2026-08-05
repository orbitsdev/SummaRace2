using SummaRace.Constants;
using SummaRace.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    ///
    /// The gate is a small state machine over one input field, because the scene has exactly one
    /// (EnterPin | CreatePin → ConfirmPin | Recovery). Setting the first PIN takes two matching
    /// entries and losing it takes a hidden gesture — both for the same reason: MainMenu puts a
    /// live button to this screen in a nine-year-old's reach, and a PIN nobody knows locks the
    /// researcher out of their own session unlocks and their own log export for the rest of the
    /// study, on a device that cannot be recovered over a network.
    /// </summary>
    public class TeacherMenuController : MonoBehaviour
    {
        /// <summary>Which question the single PIN field is asking right now.</summary>
        private enum GateStep { EnterPin, CreatePin, ConfirmPin, Recovery }

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

        // Gate policy, kept local: these are this screen's interaction rules, not gameplay
        // tuning, so they do not belong in GameRules with the race numbers.
        private const float RecoveryHoldSeconds = 6f;
        private const int AttemptsBeforeCooldown = 5;
        private const float CooldownSeconds = 30f;

        private GateStep _step;

        /// <summary>The first of the two setup entries. Memory only, cleared the moment the
        /// step changes — the disk never sees anything but the salted hash.</summary>
        private string _pendingPin;

        private bool _deleteArmed;
        private bool _recoveryArmed;
        private int _wrongAttempts;
        private float _retryAt;

        /// <summary>Unscaled time the recovery hold began, or -1 when nothing is being held.</summary>
        private float _holdStart = -1f;

        /// <summary>The release that completes the hold also fires the button's click; that
        /// release is the end of a gesture, never a confirmation of the screen it just opened.</summary>
        private bool _swallowNextSubmit;

        private void Start()
        {
            if (titleText != null) titleText.text = GameText.TeacherTitle;
            if (unlockLabel != null) unlockLabel.text = GameText.TeacherUnlockNext;
            if (exportLabel != null) exportLabel.text = GameText.TeacherExport;
            if (deleteLabel != null) deleteLabel.text = GameText.TeacherDelete;

            if (promptText != null)
            {
                // The prompt now carries copy of varying length (one word to two lines), and its
                // box is fixed, so let TMP shrink rather than spill over the PIN field below.
                // Read the authored size before enabling auto-size — after that, fontSize is
                // whatever the fitting pass last computed.
                promptText.fontSizeMax = promptText.fontSize;
                promptText.fontSizeMin = 26f;
                promptText.enableAutoSizing = true;
            }

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
            // Back sits on the canvas, not inside either panel, so every state of this screen —
            // gate, setup, reset warning, actions — keeps one working way out (TDD §13).
            if (backButton != null)
                backButton.onClick.AddListener(() =>
                {
                    Click();
                    SceneLoader.Go(SceneNames.MainMenu);
                });

            WireRecoveryGesture();
        }

        private void ShowGate()
        {
            bool hasPin = TeacherGate.HasPin();
            SetStep(hasPin ? GateStep.EnterPin : GateStep.CreatePin);
            Status(string.Empty);
            _wrongAttempts = 0;
            _retryAt = 0f;
            if (gatePanel != null) gatePanel.SetActive(true);
            if (actionsPanel != null) actionsPanel.SetActive(false);
        }

        /// <summary>Points the one input field at one question and clears what it held.</summary>
        private void SetStep(GateStep step)
        {
            _step = step;
            // The raw PIN survives only across the two setup taps.
            if (step != GateStep.ConfirmPin) _pendingPin = null;
            if (step != GateStep.Recovery) _recoveryArmed = false;
            if (pinInput != null) pinInput.text = string.Empty;

            switch (step)
            {
                case GateStep.CreatePin:
                    Prompt(GameText.TeacherSetPin);
                    SubmitText(GameText.TeacherSubmitNext);
                    break;
                case GateStep.ConfirmPin:
                    Prompt(GameText.TeacherConfirmPin);
                    SubmitText(GameText.TeacherSubmit);
                    break;
                case GateStep.Recovery:
                    Prompt(GameText.TeacherRecoveryTitle);
                    SubmitText(GameText.TeacherRecoveryErase);
                    break;
                default:
                    Prompt(GameText.TeacherEnterPin);
                    SubmitText(GameText.TeacherSubmit);
                    break;
            }
        }

        private void Submit()
        {
            if (_swallowNextSubmit)
            {
                _swallowNextSubmit = false;
                return;
            }

            Click();
            string pin = pinInput != null ? pinInput.text : string.Empty;

            switch (_step)
            {
                case GateStep.CreatePin: BeginSetPin(pin); break;
                case GateStep.ConfirmPin: FinishSetPin(pin); break;
                case GateStep.Recovery: ConfirmReset(); break;
                default: TryEnter(pin); break;
            }
        }

        /// <summary>First half of setup — nothing is written yet.</summary>
        private void BeginSetPin(string pin)
        {
            if (!TeacherGate.IsPinAcceptable(pin))
            {
                Status(GameText.TeacherPinTooShort);
                return;
            }

            _pendingPin = pin;
            SetStep(GateStep.ConfirmPin);
        }

        private void FinishSetPin(string pin)
        {
            if (!TeacherGate.PinsMatch(_pendingPin, pin))
            {
                SetStep(GateStep.CreatePin);
                Status(GameText.TeacherPinMismatch);
                return;
            }

            if (!TeacherGate.SetPin(_pendingPin))
            {
                // Both entries were fine, so this is the device refusing the write (no [Core]
                // when the scene is played directly, or no room on disk) — say so rather than
                // repeating "too short" at a researcher who did nothing wrong.
                SetStep(GateStep.CreatePin);
                Status(GameText.TeacherSaveFailed);
                return;
            }

            OpenActions();
            Status(GameText.TeacherPinSaved);
        }

        private void TryEnter(string pin)
        {
            float wait = _retryAt - Time.unscaledTime;
            if (wait > 0f)
            {
                Status(GameText.TeacherCooldown(Mathf.CeilToInt(wait)));
                return;
            }

            if (!TeacherGate.VerifyPin(pin))
            {
                if (pinInput != null) pinInput.text = string.Empty;
                _wrongAttempts++;

                // Slow down guessing — "1111" and "1234" are a nine-year-old's first two tries.
                // Friction, not security: it clears itself, so the researcher is never stuck.
                if (_wrongAttempts >= AttemptsBeforeCooldown)
                {
                    _wrongAttempts = 0;
                    _retryAt = Time.unscaledTime + CooldownSeconds;
                    Status(GameText.TeacherCooldown(Mathf.CeilToInt(CooldownSeconds)));
                    return;
                }

                Status(GameText.TeacherWrongPin);
                return;
            }

            _wrongAttempts = 0;
            OpenActions();
        }

        // ---------- Recovery: the way back in when the PIN is lost ----------

        /// <summary>
        /// Press and hold OK for six seconds with the PIN box empty. It is a hidden gesture and
        /// not a "forgot the PIN?" button because a visible one is exactly what a curious
        /// learner taps, and the only recovery a salted hash allows erases the tablet. The
        /// researcher gets it from the install checklist; the screen never advertises it.
        /// </summary>
        private void WireRecoveryGesture()
        {
            if (submitButton == null) return;

            var trigger = submitButton.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = submitButton.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerDown, BeginHold);
            AddTrigger(trigger, EventTriggerType.PointerUp, EndHold);
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        private void BeginHold()
        {
            _swallowNextSubmit = false;  // a fresh press is always a real tap

            // Two deliberate conditions: only on the "enter the PIN you already set" step (there
            // is nothing to recover before one exists) and only with the box empty.
            bool boxEmpty = pinInput == null || string.IsNullOrEmpty(pinInput.text);
            _holdStart = _step == GateStep.EnterPin && boxEmpty ? Time.unscaledTime : -1f;
        }

        private void EndHold() => _holdStart = -1f;

        private void Update()
        {
            if (_holdStart < 0f) return;
            if (Time.unscaledTime - _holdStart < RecoveryHoldSeconds) return;

            _holdStart = -1f;
            ArmReset();
        }

        /// <summary>The hold landed: show the cost. Nothing is erased yet.</summary>
        private void ArmReset()
        {
            Click();
            _swallowNextSubmit = true;   // the finger that armed this is still down
            SetStep(GateStep.Recovery);
            Status(GameText.TeacherRecoveryWarning);
        }

        /// <summary>Two taps on top of the six-second hold, because this erases the tablet.</summary>
        private void ConfirmReset()
        {
            if (!_recoveryArmed)
            {
                _recoveryArmed = true;
                SubmitText(GameText.TeacherRecoveryConfirm);
                Status(GameText.TeacherRecoveryLastChance);
                return;
            }

            TeacherGate.ResetDevice();
            SetStep(GateStep.CreatePin);
            Status(GameText.TeacherRecoveryDone);
        }

        // ---------- Behind the gate ----------

        private void OpenActions()
        {
            // Leave the gate on its default question with an empty field: the panel is only
            // hidden, and it must not come back mid-setup or mid-reset if it is shown again.
            SetStep(GateStep.EnterPin);
            _wrongAttempts = 0;
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
            Status(GameText.TeacherSessionOpened(session));
        }

        private void Export()
        {
            Click();
            Disarm();
            string path = SaveManager.Instance != null ? SaveManager.Instance.ExportLogs() : null;
            // Show the full path: the researcher has to find this file over USB.
            Status(string.IsNullOrEmpty(path) ? GameText.TeacherNothingToExport : path);
        }

        /// <summary>Two taps, because this is unrecoverable. Keeps the PIN — a post-study wipe
        /// hands the tablet on clean, it is not a way out of the gate.</summary>
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

        private void Prompt(string message)
        {
            if (promptText != null) promptText.text = message;
        }

        private void SubmitText(string message)
        {
            if (submitLabel != null) submitLabel.text = message;
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
