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
        /// <summary>Which question the single input field is asking right now.</summary>
        private enum GateStep { EnterPin, CreatePin, ConfirmPin, Recovery, ParticipantCode }

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
        /// <summary>Built in code by cloning Unlock — see EnsureExtraActions.</summary>
        private Button musicButton;
        [SerializeField] private TMP_Text deleteLabel;
        [SerializeField] private Button backButton;

        [Header("Learners")]
        [Tooltip("Optional. Left unwired the screen clones it from the Unlock button, so the " +
                 "action exists on the scene as it stands today without a re-author.")]
        [SerializeField] private Button switchLearnerButton;

        [Tooltip("Optional, same cloning rule. Sets the active learner's participant code — " +
                 "the id on their paper test booklet.")]
        [SerializeField] private Button participantButton;

        [Header("Action labels")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text unlockLabel;
        [SerializeField] private TMP_Text exportLabel;

        // Gate policy, kept local: these are this screen's interaction rules, not gameplay
        // tuning, so they do not belong in GameRules with the race numbers.
        private const float RecoveryHoldSeconds = 6f;
        private const int AttemptsBeforeCooldown = 5;
        private const float CooldownSeconds = 30f;

        // Layout of the action column, also local for the same reason. The scene authored three
        // buttons at y = 150 / 0 / -150 inside ActionsPanel and gave it no layout group; a
        // fourth no longer centres on that spacing, so the column is positioned from one rule
        // here instead — the scene and this file cannot drift apart that way.
        private const float ActionSpacing = 150f;
        private const float LearnerRowHeight = 120f;

        /// <summary>The learner picker, built on first use out of this screen's own controls.
        /// Null until then (and if there is nothing to clone).</summary>
        private GameObject _learnerPanel;
        private RectTransform _learnerList;
        private TMP_Text _learnerTitle;

        private GateStep _step;

        /// <summary>The first of the two setup entries. Memory only, cleared the moment the
        /// step changes — the disk never sees anything but the salted hash.</summary>
        private string _pendingPin;

        private bool _deleteArmed;
        private bool _recoveryArmed;

        /// <summary>The participant code is being asked as part of CREATING a learner, so
        /// saving it continues to Name Entry rather than back to the action list. Creation is
        /// the one moment the code is guaranteed to be asked for, which is what stops a tablet
        /// reaching a child with an unidentifiable profile on it.</summary>
        private bool _codeThenNameEntry;
        /// <summary>
        /// STATIC ON PURPOSE. These were instance fields on a scene-scoped controller, and
        /// ShowGate() reset both on Start() - so the five-try lockout was defeated by two taps:
        /// Back to the menu, teacher corner again, fresh scene, cooldown gone. It is the only
        /// thing standing between a nine-year-old and Delete-all-data, and "1111 / 1234 / 0000"
        /// is exactly the attack it exists for. Static survives the scene; a full app restart
        /// still clears it, which is the honest limit of doing this without persisting to disk.
        /// </summary>
        private static int _wrongAttempts;
        private static float _retryAt;

        /// <summary>Unscaled time the recovery hold began, or -1 when nothing is being held.</summary>
        private float _holdStart = -1f;

        /// <summary>The release that completes the hold also fires the button's click; that
        /// release is the end of a gesture, never a confirmation of the screen it just opened.</summary>
        private bool _swallowNextSubmit;

        private void Start()
        {
            if (titleText != null)
            {
                titleText.text = GameText.TeacherTitle;
                SummaRace.UI.TitleBannerSkin.Apply(titleText);
            }

            // The status line prints the EXPORT FILE PATH - the longest string this app ever
            // shows, and the one a researcher has to read off the screen and then find over USB.
            // Authored with autosize off and overflow mode Overflow, so a long persistentDataPath
            // drew straight out of its box. On the single action that retrieves the entire study
            // dataset, an unreadable path is a data-loss bug wearing a layout bug's clothes.
            SummaRace.UI.LabelFit.Harden(statusText, 18f);

            // Android BACK now answers instead of being swallowed (owner, 2026-08-22).
            // Registered rather than handled here, so one overlay serves every scene and
            // each screen only supplies its own rule - see Core/BackButtonGuard.
            Core.BackButtonGuard.RegisterExit(GameText.BackLeaveToMenu,
                () => SceneLoader.Go(SceneNames.MainMenu));
            if (unlockLabel != null) unlockLabel.text = GameText.TeacherUnlockNext;
            if (exportLabel != null) exportLabel.text = GameText.TeacherExport;
            if (deleteLabel != null)
            {
                deleteLabel.text = GameText.TeacherDelete;
                // White on the kit orange measured 2.40:1 - failing even the 3.0 large-text bar,
                // on the one action that cannot be undone, and its armed state ("Tap again to
                // confirm") was in the same pairing. Dark brown on that orange is about 7.9:1.
                deleteLabel.color = Theme.TextBrownDeep;
            }

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

            EnsureExtraActions();
            LayOutActions(participantButton, switchLearnerButton, musicButton, unlockButton, exportButton, deleteButton);

            ShowGate();

            if (submitButton != null) submitButton.onClick.AddListener(Submit);
            if (participantButton != null) participantButton.onClick.AddListener(EditParticipantCode);
            if (switchLearnerButton != null) switchLearnerButton.onClick.AddListener(OpenLearnerPicker);
            if (unlockButton != null) unlockButton.onClick.AddListener(UnlockNext);
            if (exportButton != null) exportButton.onClick.AddListener(Export);
            if (deleteButton != null) deleteButton.onClick.AddListener(DeleteData);
            if (musicButton != null) musicButton.onClick.AddListener(ToggleMusic);
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
            // Deliberately NOT cleared here any more - see the fields. Re-entering the screen
            // used to be the reset.
            if (gatePanel != null) gatePanel.SetActive(true);
            if (actionsPanel != null) actionsPanel.SetActive(false);
            // The picker lives behind the PIN too — it must not survive a return to the gate.
            if (_learnerPanel != null) _learnerPanel.SetActive(false);
        }

        /// <summary>Points the one input field at one question and clears what it held.</summary>
        private void SetStep(GateStep step)
        {
            _step = step;
            // The raw PIN survives only across the two setup taps.
            if (step != GateStep.ConfirmPin) _pendingPin = null;
            if (step != GateStep.Recovery) _recoveryArmed = false;
            if (step != GateStep.ParticipantCode) _codeThenNameEntry = false;

            if (pinInput != null)
            {
                // The field is shared, so its rules have to follow the question. A PIN is
                // digits behind dots; a participant code is letters AND digits and must be
                // READABLE while it is typed — it is being copied off a paper booklet, and a
                // masked code cannot be checked against the page before it is saved.
                if (step == GateStep.ParticipantCode)
                {
                    pinInput.contentType = TMP_InputField.ContentType.Alphanumeric;
                    pinInput.characterLimit = Data.ParticipantCodes.MaxLength;
                }
                else
                {
                    pinInput.contentType = TMP_InputField.ContentType.Pin;
                    pinInput.characterLimit = 0;
                }
                pinInput.text = string.Empty;
            }

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
                case GateStep.ParticipantCode:
                    Prompt(GameText.TeacherParticipantPrompt);
                    SubmitText(GameText.TeacherParticipantSubmit);
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
            string typed = pinInput != null ? pinInput.text : string.Empty;

            switch (_step)
            {
                case GateStep.CreatePin: BeginSetPin(typed); break;
                case GateStep.ConfirmPin: FinishSetPin(typed); break;
                case GateStep.Recovery: ConfirmReset(); break;
                case GateStep.ParticipantCode: SaveParticipantCode(typed); break;
                default: TryEnter(typed); break;
            }
        }

        /// <summary>First half of setup — nothing is written yet.</summary>
        private void BeginSetPin(string pin)
        {
            if (!TeacherGate.IsPinAcceptable(pin))
            {
                Reject(GameText.TeacherPinTooShort);
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
                Reject(GameText.TeacherPinMismatch);
                return;
            }

            if (!TeacherGate.SetPin(_pendingPin))
            {
                // Both entries were fine, so this is the device refusing the write (no [Core]
                // when the scene is played directly, or no room on disk) — say so rather than
                // repeating "too short" at a researcher who did nothing wrong.
                SetStep(GateStep.CreatePin);
                Reject(GameText.TeacherSaveFailed);
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
                Reject(GameText.TeacherCooldown(Mathf.CeilToInt(wait)));
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
                    Reject(GameText.TeacherCooldown(Mathf.CeilToInt(CooldownSeconds)));
                    return;
                }

                Reject(GameText.TeacherWrongPin);
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

        /// <summary>
        /// A press that is interrupted rather than released — a call, a notification, the home
        /// button — is not guaranteed to deliver PointerUp, and unscaled time keeps running while
        /// the app is away. Without this the hold could "complete" in the background and the
        /// researcher would come back to "Reset this tablet?" with no idea what they did: an
        /// alarming screen that is two taps from erasing the study. Interrupted is not held.
        /// </summary>
        private void OnApplicationPause(bool paused) { if (paused) EndHold(); }

        private void OnApplicationFocus(bool focused) { if (!focused) EndHold(); }

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
            // Sounds like a warning, not like progress: the hold has just opened the one screen
            // in this app that can erase the study.
            Reject(GameText.TeacherRecoveryWarning);
        }

        /// <summary>Two taps on top of the six-second hold, because this erases the tablet.</summary>
        private void ConfirmReset()
        {
            if (!_recoveryArmed)
            {
                _recoveryArmed = true;
                SubmitText(GameText.TeacherRecoveryConfirm);
                Reject(GameText.TeacherRecoveryLastChance);
                return;
            }

            if (!TeacherGate.ResetDevice())
            {
                // The gate could not be cleared, so nothing was erased. Stay armed: the next tap
                // retries, rather than making the researcher hold for six seconds again.
                Reject(GameText.TeacherRecoveryFailed);
                return;
            }

            SetStep(GateStep.CreatePin);
            Status(GameText.TeacherRecoveryDone);
        }

        // ---------- Behind the gate ----------

        private void OpenActions()
        {
            // The PIN was accepted — the one moment on this screen that deserves a "yes". The
            // panels swap, which is the visual half, but a correct PIN and a wrong one sounded
            // identical, and this screen is used one-handed in a noisy classroom.
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxCorrect);

            // Leave the gate on its default question with an empty field: the panel is only
            // hidden, and it must not come back mid-setup or mid-reset if it is shown again.
            SetStep(GateStep.EnterPin);
            _wrongAttempts = 0;
            _deleteArmed = false;
            if (deleteLabel != null) deleteLabel.text = GameText.TeacherDelete;
            RefreshParticipantLabel();

            // A learner with no participant code has no route back to their paper pretest, and
            // that is only discoverable during analysis — long after the tablets are collected.
            // So the code is asked for HERE, the first time an adult gets through the PIN,
            // rather than left as a button someone might not press. Back still leaves (TDD §13),
            // and it simply asks again next time.
            var manager = Core.GameManager.Instance;
            if (manager != null && manager.CurrentLearner != null &&
                !Data.ParticipantCodes.IsSet(manager.CurrentLearner))
            {
                ShowParticipantStep(false);
                Status(GameText.TeacherParticipantMissing);
                return;
            }

            if (gatePanel != null) gatePanel.SetActive(false);
            if (_learnerPanel != null) _learnerPanel.SetActive(false);
            if (actionsPanel != null) actionsPanel.SetActive(true);
            Status(ParticipantWarning() ?? string.Empty);
        }

        // ---------- The participant code: the join to the paper pretest/posttest ----------

        /// <summary>
        /// Opens the code entry for the active learner. Behind the PIN with everything else,
        /// deliberately: the learner types their own NAME at Name Entry and that stays theirs,
        /// but the identifier the study joins on is copied off a booklet by an adult. A
        /// nine-year-old's spelling of their own name is exactly the join the results chapter
        /// cannot be allowed to rest on.
        /// </summary>
        private void EditParticipantCode()
        {
            Click();
            Disarm();

            var manager = Core.GameManager.Instance;
            if (manager == null || manager.CurrentLearner == null)
            {
                Reject(GameText.TeacherSaveFailed);
                return;
            }

            ShowParticipantStep(false);
            Status(string.Empty);
        }

        /// <summary>Swaps the gate in on the code question, pre-filled with whatever this
        /// learner already has so an edit is a correction rather than a retype.</summary>
        private void ShowParticipantStep(bool thenNameEntry)
        {
            SetStep(GateStep.ParticipantCode);
            _codeThenNameEntry = thenNameEntry;   // set AFTER SetStep, which clears it

            var manager = Core.GameManager.Instance;
            if (pinInput != null && manager != null)
                pinInput.text = Data.ParticipantCodes.Of(manager.CurrentLearner);

            if (_learnerPanel != null) _learnerPanel.SetActive(false);
            if (actionsPanel != null) actionsPanel.SetActive(false);
            if (gatePanel != null) gatePanel.SetActive(true);
        }

        private void SaveParticipantCode(string typed)
        {
            var manager = Core.GameManager.Instance;
            if (manager == null)
            {
                Reject(GameText.TeacherSaveFailed);
                return;
            }

            string code;
            string clash;
            var result = manager.SetParticipantCode(manager.CurrentLearner, typed, out code, out clash);

            switch (result)
            {
                case ParticipantCodeResult.Invalid:
                    Reject(GameText.TeacherParticipantInvalid);
                    return;
                case ParticipantCodeResult.Duplicate:
                    // The one failure this whole field exists to prevent. Refused at the moment
                    // it is typed, while the teacher still has both booklets in front of them —
                    // after export, two children sharing a code cannot be separated at all.
                    Reject(GameText.TeacherParticipantDuplicate(code, clash));
                    return;
                case ParticipantCodeResult.NoLearner:
                    Reject(GameText.TeacherSaveFailed);
                    return;
                case ParticipantCodeResult.SaveFailed:
                    // The code was fine; the disk was not. Previously indistinguishable from
                    // success, on the field that joins this child's logs to their paper test.
                    Reject(GameText.TeacherSaveFailed);
                    return;
            }

            if (_codeThenNameEntry)
            {
                // Created from the picker: the learner is identified, now let them name
                // themselves and pick a runner exactly as a first-boot learner does.
                _codeThenNameEntry = false;
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxStar);
                SceneLoader.Go(SceneNames.NameEntry);
                return;
            }

            // OpenActions plays the accepted sound, so nothing extra here — two confirmations
            // on one tap reads as two things having happened.
            OpenActions();
            Status(GameText.TeacherParticipantSaved(code));
        }

        /// <summary>The code problems worth interrupting an adult about, or null when there are
        /// none. Duplicates lead: a missing code loses one learner's rows, a shared one merges
        /// two learners' rows into an unusable single participant.</summary>
        private static string ParticipantWarning()
        {
            var manager = Core.GameManager.Instance;
            if (manager == null) return null;

            string duplicate = manager.FirstDuplicateParticipantCode();
            if (!string.IsNullOrEmpty(duplicate))
                return GameText.TeacherParticipantDuplicateWarning(duplicate);

            int missing = manager.CountLearnersWithoutParticipantCode();
            return missing > 0 ? GameText.TeacherParticipantMissingCount(missing) : null;
        }

        /// <summary>Keeps the action button reading the ACTIVE learner's code, so the tablet
        /// answers "who is this, on paper?" without a tap.</summary>
        private void RefreshParticipantLabel()
        {
            if (participantButton == null) return;
            var manager = Core.GameManager.Instance;
            string code = manager != null ? Data.ParticipantCodes.Of(manager.CurrentLearner) : string.Empty;
            SetButtonLabel(participantButton, GameText.TeacherParticipantActionLabel(code));
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
            int session = TeacherGate.UnlockNextSession(out bool saveFailed);
            if (session == 0)
            {
                // "Nothing left to open" and "it did not save" are opposite facts and used to
                // produce the same sentence. A teacher told the former hands over a tablet that
                // is still on the previous session.
                Reject(saveFailed ? GameText.TeacherSaveFailed : GameText.TeacherAllUnlocked);
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
            var status = SaveManager.ExportStatus.Failed;
            string path = SaveManager.Instance != null
                ? SaveManager.Instance.ExportLogs(out status)
                : null;
            // Show the full path: the researcher has to find this file over USB. The bare path
            // was not enough — the export is deliberately pseudonymised, so the companion roster
            // written beside it has to be pulled too or the rows cannot be tied to a child.
            if (string.IsNullOrEmpty(path))
            {
                // Nothing was written. That is the one outcome here a researcher must not read
                // past — "exported" and "there was nothing to export" are one line of text apart
                // and only one of them means the study data is on the tablet's file system.
                //
                // And they must be told WHICH. An empty tablet and a failed write used to give
                // the same message, so a disk-full or permission failure read as "this device
                // has no data" — on the single action that retrieves the dataset, with no second
                // chance to collect it once the study ends.
                Reject(status == SaveManager.ExportStatus.NoLogs
                    ? GameText.TeacherNothingToExport
                    : GameText.TeacherExportFailed);
                return;
            }
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxStar);

            // The roster beside the export carries each learner's participant code, and this is
            // the last moment anyone looks at it while the tablet is still in hand. A missing or
            // shared code is not a formatting problem — it is rows that cannot be joined to a
            // pretest score — so say it here rather than let it be found during analysis.
            string warning = ParticipantWarning();
            Status(warning == null
                ? GameText.TeacherExported(path)
                : GameText.TeacherExported(path) + "\n" + warning);
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
                // Arming a wipe must not sound like every other button on the screen. The label
                // change is easy to miss on a tablet held at arm's length; the nudge is not.
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxSlotWiggle);
                return;
            }

            // Checked, not assumed. A wipe that half-failed used to answer with the success
            // line; the researcher would hand the tablet back believing it clean.
            bool wiped = SaveManager.Instance != null && SaveManager.Instance.DeleteAllData();
            if (Core.GameManager.Instance != null) Core.GameManager.Instance.InitProfiles();

            _deleteArmed = false;
            if (deleteLabel != null) deleteLabel.text = GameText.TeacherDelete;
            if (wiped) Status(GameText.TeacherDeleted);
            else Reject(GameText.TeacherDeleteFailed);
        }

        // ---------- Who is holding the tablet ----------

        /// <summary>
        /// Opens the learner picker. This is the only way to change learner in the whole app,
        /// and it is behind the PIN with session unlocking for the same reason: a learner who
        /// could switch could also play as a classmate, and the stars, unlocks and log rows
        /// would land on the wrong child (GDD §8.3). Nothing about this is undoable after the
        /// study exports, so it is an adult action by design.
        /// </summary>
        private void OpenLearnerPicker()
        {
            Click();
            Disarm();
            EnsureLearnerPanel();

            if (_learnerPanel == null)
            {
                // Everything here is cloned from this screen's own controls; with none wired
                // there is nothing to clone. Say so rather than open an empty board (TDD §13).
                Reject(GameText.TeacherLearnerPickerUnavailable);
                return;
            }

            RefreshLearnerRows();
            if (actionsPanel != null) actionsPanel.SetActive(false);
            _learnerPanel.SetActive(true);
            Status(string.Empty);
        }

        private void CloseLearnerPicker()
        {
            if (_learnerPanel != null) _learnerPanel.SetActive(false);
            if (actionsPanel != null) actionsPanel.SetActive(true);
        }

        private void ChooseLearner(Data.LearnerProfile learner)
        {
            Click();
            var manager = Core.GameManager.Instance;
            if (manager == null || learner == null) return;

            manager.SetActiveLearner(learner);
            RefreshParticipantLabel();

            // A learner brought in from an older tablet (or an install that was interrupted)
            // may have no code. Ask now, while the adult is still on this screen with the
            // booklets — not at export, when it is only a hole in the data.
            if (!Data.ParticipantCodes.IsSet(learner))
            {
                ShowParticipantStep(false);
                Status(GameText.TeacherParticipantMissing);
                return;
            }

            CloseLearnerPicker();
            // Name them back: on a shared tablet the confirmation IS the safeguard.
            Status(GameText.TeacherActiveLearner(learner.displayName));
        }

        /// <summary>
        /// Starts a second (third, fourth…) learner on this tablet. Two steps, in this order:
        /// the adult gives the participant code here, then Name Entry lets the child give their
        /// own name and avatar. Asking for the code AT CREATION is what guarantees no profile
        /// can ever collect data without an identifier — the alternative, a button someone is
        /// meant to remember to press, is the same "discovered during analysis" failure one
        /// step removed. Name Entry exits to the Main Menu, so this is never a dead end.
        /// </summary>
        private void StartNewLearner()
        {
            Click();
            var manager = Core.GameManager.Instance;
            if (manager == null)
            {
                // No [Core] means no profiles and nowhere to save — the same failure the PIN
                // step reports, so it reads the same way.
                Reject(GameText.TeacherSaveFailed);
                return;
            }

            manager.CreateLearner();
            RefreshParticipantLabel();
            ShowParticipantStep(true);
            Status(GameText.TeacherParticipantMissing);
        }

        /// <summary>Rebuilds the list. Cheap enough to redo per open, and it always agrees with
        /// the profiles as they are right now (a name set at Name Entry, a session unlocked).</summary>
        private void RefreshLearnerRows()
        {
            if (_learnerList == null) return;

            // Unparent before destroying: Destroy only takes effect at the end of the frame, so
            // the layout group would otherwise stack the old rows under the new ones for a frame
            // — and the stale buttons would still be tappable while it did.
            for (int i = _learnerList.childCount - 1; i >= 0; i--)
            {
                var stale = _learnerList.GetChild(i);
                stale.SetParent(null, false);
                Destroy(stale.gameObject);
            }

            if (_learnerTitle != null) _learnerTitle.text = GameText.TeacherLearnerPickerTitle;

            var newRow = CloneButton(unlockButton, _learnerList, "NewLearner", GameText.TeacherNewLearner);
            if (newRow != null)
            {
                ShapeRow(newRow);
                newRow.onClick.AddListener(StartNewLearner);
            }

            var manager = Core.GameManager.Instance;
            if (manager == null) return;

            var learners = manager.Learners;
            for (int i = 0; i < learners.Count; i++)
            {
                var learner = learners[i];
                if (learner == null) continue;

                bool playing = learner == manager.CurrentLearner;
                // The row leads with the participant code, so "which of these is P07?" is
                // answerable at a glance and a profile that never got one is visible as
                // "(no code)" instead of looking like every other row.
                string code = Data.ParticipantCodes.Of(learner);
                var row = CloneButton(unlockButton, _learnerList, "Learner" + i,
                    playing
                        ? GameText.TeacherLearnerRowActive(code, learner.displayName, learner.unlockedSession)
                        : GameText.TeacherLearnerRow(code, learner.displayName, learner.unlockedSession));
                if (row == null) continue;

                ShapeRow(row);
                // Re-picking whoever is already playing would only flush their log for nothing.
                row.interactable = !playing;
                var chosen = learner;              // capture per row, not per loop
                row.onClick.AddListener(() => ChooseLearner(chosen));
            }
        }

        // ---------- Building the picker out of the screen's own parts ----------

        /// <summary>
        /// Adds the actions the scene does not author by copying the Unlock button. Cloning
        /// rather than building keeps the kit styling (9-sliced pill, dark ring, label font,
        /// ButtonSquash) exactly as the scene already carries it — a hand-built button would
        /// drift the moment the skin changes. Skipped for anything the scene wires itself.
        /// </summary>
        private void EnsureExtraActions()
        {
            if (unlockButton == null) return;

            if (switchLearnerButton == null)
                switchLearnerButton = CloneButton(
                    unlockButton, unlockButton.transform.parent, "Switch learner",
                    GameText.TeacherSwitchLearner);

            if (participantButton == null)
                participantButton = CloneButton(
                    unlockButton, unlockButton.transform.parent, "Participant code",
                    GameText.TeacherParticipantActionLabel(string.Empty));

            if (musicButton == null)
                musicButton = CloneButton(
                    unlockButton, unlockButton.transform.parent, "Music",
                    GameText.TeacherMusicAction(MusicIsOn()));
        }

        /// <summary>
        /// Is music on for this tablet? Read from disk rather than cached, because the teacher
        /// screen is entered rarely and a stale answer here would mislabel the button.
        /// </summary>
        private static bool MusicIsOn()
        {
            var save = Core.SaveManager.Instance;
            if (save == null) return true;                 // editor-direct: assume the default
            var settings = save.LoadSettings();
            return settings != null && settings.musicVolume > 0.001f;
        }

        /// <summary>
        /// Flips the tablet's music on or off.
        ///
        /// THREE THINGS HAVE TO HAPPEN TOGETHER, and only the first was ever wired:
        ///   1. persist it  - AppSettings.musicVolume, written through SaveManager;
        ///   2. APPLY IT NOW - AudioManager.SetVolumes was called only by Bootstrapper at
        ///      launch, so writing the setting alone would have done nothing audible until the
        ///      app was restarted. That is exactly the "declared, persisted and ignored" shape
        ///      that narrationVolume had until F50, and it is why this calls SetVolumes directly;
        ///   3. stop the loop that is already playing - SetVolumes changes the AudioSource's
        ///      volume, which silences it, but a source left running at volume 0 is still a
        ///      source; stopping it is what makes "off" mean off.
        ///
        /// Narration is deliberately untouched. It is the accessibility support the study
        /// depends on, it has its own control (the Reader's VOICE toggle), and a teacher
        /// silencing background music must never silence the reading voice by accident.
        /// </summary>
        private void ToggleMusic()
        {
            var save = Core.SaveManager.Instance;
            if (save == null) { Reject(GameText.TeacherMusicStatus(true)); return; }

            var settings = save.LoadSettings();
            if (settings == null) { Reject(GameText.TeacherMusicStatus(true)); return; }

            bool turningOn = settings.musicVolume <= 0.001f;
            settings.musicVolume = turningOn ? GameRules.MusicOnVolume : 0f;
            save.SaveSettings(settings);

            var audio = AudioManager.Instance;
            if (audio != null)
            {
                audio.SetVolumes(settings);
                if (turningOn) audio.PlayMusic(AudioKeys.MusicMenu);
                else audio.StopMusic();
            }

            SetButtonLabel(musicButton, GameText.TeacherMusicAction(turningOn));
            Status(GameText.TeacherMusicStatus(turningOn));
            // The click is played AFTER the switch, and only on success - F50's rule: a sound
            // that fires before the outcome is known tells the teacher something worked when it
            // may not have.
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);
        }

        /// <summary>Retitles a cloned action. Leaves the label object's own activation alone for
        /// the same reason CloneButton does — several kit buttons carry baked text.</summary>
        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null) return;
            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = label;
        }

        /// <summary>Centres the action column however many buttons it ends up with.</summary>
        private static void LayOutActions(params Button[] actions)
        {
            int count = 0;
            for (int i = 0; i < actions.Length; i++) if (actions[i] != null) count++;
            if (count == 0) return;

            float top = (count - 1) * 0.5f * ActionSpacing;
            int slot = 0;
            for (int i = 0; i < actions.Length; i++)
            {
                if (actions[i] == null) continue;
                var rect = actions[i].transform as RectTransform;
                if (rect != null)
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, top - slot * ActionSpacing);
                slot++;
            }
        }

        private void EnsureLearnerPanel()
        {
            if (_learnerPanel != null) return;
            if (actionsPanel == null || unlockButton == null) return;

            var panel = new GameObject("LearnerPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(actionsPanel.transform.parent, false);
            // Its own box rather than the actions panel's: the list needs the height, and it has
            // to clear the title banner above (bottom ≈0.865) and the status line below (0.26).
            rect.anchorMin = new Vector2(0.06f, 0.28f);
            rect.anchorMax = new Vector2(0.94f, 0.83f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var backdrop = panel.GetComponent<Image>();
            var board = actionsPanel.GetComponent<Image>();
            if (board != null)
            {
                backdrop.sprite = board.sprite;
                backdrop.type = board.type;
                backdrop.color = board.color;
                backdrop.pixelsPerUnitMultiplier = board.pixelsPerUnitMultiplier;
            }
            else
            {
                backdrop.color = new Color(0f, 0f, 0f, 0.35f);
            }
            backdrop.raycastTarget = false;

            _learnerTitle = CloneText(statusText, rect, "PickerTitle");
            if (_learnerTitle != null)
            {
                var titleRect = (RectTransform)_learnerTitle.transform;
                titleRect.anchorMin = new Vector2(0.05f, 1f);
                titleRect.anchorMax = new Vector2(0.95f, 1f);
                titleRect.pivot = new Vector2(0.5f, 1f);
                titleRect.sizeDelta = new Vector2(0f, 110f);
                titleRect.anchoredPosition = new Vector2(0f, -24f);
                _learnerTitle.alignment = TextAlignmentOptions.Center;
                _learnerTitle.text = GameText.TeacherLearnerPickerTitle;
            }

            BuildLearnerScroll(rect);

            var done = CloneButton(unlockButton, rect, "PickerDone", GameText.TeacherLearnerPickerClose);
            if (done != null)
            {
                var doneRect = (RectTransform)done.transform;
                doneRect.anchorMin = new Vector2(0.5f, 0f);
                doneRect.anchorMax = new Vector2(0.5f, 0f);
                doneRect.pivot = new Vector2(0.5f, 0f);
                doneRect.sizeDelta = new Vector2(600f, 120f);
                doneRect.anchoredPosition = new Vector2(0f, 26f);
                done.onClick.AddListener(() => { Click(); CloseLearnerPicker(); });
            }

            panel.SetActive(false);
            _learnerPanel = panel;
        }

        /// <summary>A real scroll view, because a tablet shared by a whole reading group can
        /// hold more learners than the board is tall.</summary>
        private void BuildLearnerScroll(RectTransform parent)
        {
            var scrollGo = new GameObject("LearnerScroll", typeof(RectTransform), typeof(ScrollRect));
            var scrollRect = (RectTransform)scrollGo.transform;
            scrollRect.SetParent(parent, false);
            scrollRect.anchorMin = new Vector2(0.05f, 0f);
            scrollRect.anchorMax = new Vector2(0.95f, 1f);
            scrollRect.offsetMin = new Vector2(0f, 170f);    // clear of the Done button
            scrollRect.offsetMax = new Vector2(0f, -140f);   // clear of the title

            var viewportGo = new GameObject(
                "Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            var viewport = (RectTransform)viewportGo.transform;
            viewport.SetParent(scrollRect, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            // Fully transparent, but still a raycast target: a drag started on the gap between
            // two rows has to land on something or the list will not scroll.
            viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            var contentGo = new GameObject(
                "Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            _learnerList = (RectTransform)contentGo.transform;
            _learnerList.SetParent(viewport, false);
            _learnerList.anchorMin = new Vector2(0f, 1f);
            _learnerList.anchorMax = new Vector2(1f, 1f);
            _learnerList.pivot = new Vector2(0.5f, 1f);
            _learnerList.sizeDelta = Vector2.zero;
            _learnerList.anchoredPosition = Vector2.zero;

            var column = contentGo.GetComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(10, 10, 10, 10);
            column.spacing = 16f;
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlWidth = true;
            column.childForceExpandWidth = true;
            column.childControlHeight = true;
            column.childForceExpandHeight = false;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = _learnerList;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 30f;
        }

        /// <summary>Rows are laid out by the column, so their height has to be stated as a
        /// layout preference rather than a rect size.</summary>
        private static void ShapeRow(Button row)
        {
            var element = row.gameObject.GetComponent<LayoutElement>();
            if (element == null) element = row.gameObject.AddComponent<LayoutElement>();
            element.minHeight = LearnerRowHeight;
            element.preferredHeight = LearnerRowHeight;
        }

        /// <summary>
        /// Copies one of this screen's own buttons instead of building a new one, so a
        /// code-added control cannot drift from the kit styling the scene already carries.
        /// The clone's onClick is cleared first: runtime listeners are not copied by
        /// Instantiate, but a scene-authored one would be.
        /// </summary>
        private Button CloneButton(Button template, Transform parent, string name, string label)
        {
            if (template == null || parent == null) return null;

            var clone = Instantiate(template.gameObject, parent, false);
            clone.name = name;
            clone.SetActive(true);

            var button = clone.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = true;
            }

            // Leave the label's own activation alone — several kit buttons carry baked text and
            // deliberately keep their TMP child switched off (F17/F22).
            var text = clone.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = label;

            return button;
        }

        private TMP_Text CloneText(TMP_Text template, Transform parent, string name)
        {
            if (template == null || parent == null) return null;

            var clone = Instantiate(template.gameObject, parent, false);
            clone.name = name;
            clone.SetActive(true);
            return clone.GetComponent<TMP_Text>();
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

        /// <summary>
        /// A tap that was refused — wrong PIN, mismatched setup, cooldown, nothing to export.
        /// Every one of these used to answer with the same cheerful click as a tap that WORKED,
        /// because Submit() clicks before it knows the answer, and then changed one line of small
        /// status text. A researcher standing over a child, glancing at the tablet, could not
        /// tell the two apart. The nudge sound is the warm one used everywhere else for "not
        /// that" (never a harsh buzzer — the same restraint the learner's screens get).
        /// </summary>
        private void Reject(string message)
        {
            Status(message);
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxSlotWiggle);
        }

        private static void Click()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);
        }
    }
}
