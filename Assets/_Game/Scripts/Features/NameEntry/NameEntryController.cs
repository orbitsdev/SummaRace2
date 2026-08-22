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

        [Header("Keyboard")]
        // Closes the on-screen keyboard. Optional: with nothing wired the controller builds a
        // small kit pill for it at runtime (see EnsureDoneTypingChip) — same pattern, same
        // wording and same rules as Summary's, which is the only other typing screen.
        [SerializeField] private Button doneTypingButton;

        private static readonly Color AvatarOff = new Color(0.72f, 0.76f, 0.80f);
        private static readonly Color AvatarOn = Color.white;
        private static readonly Color ChipText = Theme.Paper;
        private static readonly Color ChipFallback = Theme.Alpha(Theme.Navy, 0.95f);

        private int _avatarIndex;
        private bool _typing;      // input field focused = keyboard up on Android
        private bool _confirmed;   // the scene is on its way out; stop showing the chip

        private void Start()
        {
            // On a fresh device this is the FIRST screen the learner ever sees (Boot routes here
            // before the Main Menu), and it was the only menu screen in the game with no music at
            // all — so the app opened silent, which on a classroom tablet is indistinguishable
            // from a device whose sound is broken. PlayMusic no-ops when the loop is already
            // running, so arriving here from the teacher's "+ New learner" costs nothing.
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMusic(AudioKeys.MusicMenu);

            if (titleText != null)
            {
                titleText.text = GameText.NameEntryTitle;
                SummaRace.UI.TitleBannerSkin.Apply(titleText);
            }
            // Android BACK now answers instead of being swallowed (owner, 2026-08-22).
            // Registered rather than handled here, so one overlay serves every scene and
            // each screen only supplies its own rule - see Core/BackButtonGuard.
            // Nowhere legal to go: Bootstrapper routes here exactly once on a fresh device
            // and there is no screen behind it.
            Core.BackButtonGuard.RegisterBlocked(GameText.BackBlockedNameEntry);

            if (avatarPromptText != null)
            {
                avatarPromptText.text = GameText.NameEntryPickAvatar;
                // WHITE ON LIME MEASURED 1.40:1 - the worst contrast anywhere in the app, on the
                // only instruction telling a child the four badges are choosable, on the first
                // screen the game ever shows them. The backdrop here is bg_storyselect, whose
                // band behind this label samples (190, 235, 21); the font material carries no
                // outline, so nothing was holding the edge. Dark brown on that lime clears AA
                // comfortably and needs no plaque, which is what keeps this a one-line fix.
                avatarPromptText.color = SummaRace.Constants.Theme.TextBrownDeep;
            }
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

            EnsureDoneTypingChip();
            if (doneTypingButton != null)
            {
                // Through OnDoneTyping, not StopTyping directly: the tap needs its own click, and
                // StopTyping is also called from Confirm(), which already plays its own sound.
                doneTypingButton.onClick.AddListener(OnDoneTyping);
                doneTypingButton.gameObject.SetActive(false);
            }
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
            if (_confirmed) return;
            _confirmed = true;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);

            // The Android keyboard belongs to the field, not to the scene: left open it rides
            // over the Main Menu, where there is nothing to type and no way to dismiss it
            // except the system back gesture — which BackButtonGuard now swallows.
            StopTyping();

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

        // ---------- getting out from under the on-screen keyboard ----------

        /// <summary>
        /// On a portrait Android tablet the soft keyboard covers roughly the bottom 45% of the
        /// screen — and on THIS screen that is everything the learner still has to do: the four
        /// runners (y 0.373-0.467) and LET'S GO! (y 0.181-0.259) are both underneath it, with
        /// the name box (0.626-0.694) the only thing left visible. So a child types their name
        /// and the screen appears to have no way forward, on the very first screen the app ever
        /// shows them. Summary had exactly this problem and was given a DONE TYPING chip for it
        /// (SummaryController.EnsureDoneTypingChip); this is that same pattern, not a second one.
        ///
        /// Shown by focus rather than by TouchScreenKeyboard.visible so the behaviour also
        /// appears in an editor playtest, where no soft keyboard exists at all.
        /// </summary>
        private void Update()
        {
            bool typing = nameInput != null && nameInput.isFocused && !_confirmed;
            if (typing == _typing) return;
            _typing = typing;

            if (doneTypingButton != null) doneTypingButton.gameObject.SetActive(typing);
        }

        /// <summary>The learner tapping DONE TYPING — every tap on this screen answers with a
        /// sound, and a classroom tablet is usually muted, so it must also visibly do something:
        /// the keyboard drops and the avatars and LET'S GO! come back.</summary>
        private void OnDoneTyping()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);
            StopTyping();
        }

        /// <summary>
        /// Closes the keyboard. Note the chip may well never receive its click: pressing it
        /// takes selection off the input field, which deactivates it — and that IS the close.
        /// Whether the tap lands as a click or only as a focus change, the learner gets the
        /// same result, so nothing here needs to defend against the chip vanishing mid-tap.
        /// </summary>
        private void StopTyping()
        {
            if (nameInput != null) nameInput.DeactivateInputField();
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }

        /// <summary>
        /// Builds the chip when the scene has no object for it. Serialized wiring wins if it
        /// ever gains one; built here so the escape hatch ships without a scene edit (same
        /// idiom as Summary's chip, SceneLoader's overlay and the race briefing).
        /// </summary>
        private void EnsureDoneTypingChip()
        {
            if (doneTypingButton != null) return;

            var root = ResolveSceneCanvas();
            if (root == null) return;

            var chipGo = new GameObject("DoneTypingChip", typeof(RectTransform));
            chipGo.transform.SetParent(root, false);
            var rect = (RectTransform)chipGo.transform;
            // Directly above the name box (which ends at y 0.694) and well below the title
            // banner (which starts at y 0.854), right-hand end so it never sits over the
            // placeholder text the learner is reading. The keyboard reaches y 0.45 at worst,
            // so this is clear of it by a wide margin on any portrait device.
            rect.anchorMin = new Vector2(0.66f, 0.712f);
            rect.anchorMax = new Vector2(0.97f, 0.766f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();

            var image = chipGo.AddComponent<Image>();
            var pill = Resources.Load<Sprite>("UI/bar_bg"); // navy 9-sliced pill, as Summary's
            if (pill != null) { image.sprite = pill; image.type = Image.Type.Sliced; }
            else image.color = ChipFallback;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(chipGo.transform, false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            // Borrow the confirm button's face (Fredoka) rather than loading one — same scene,
            // already loaded.
            if (confirmLabel != null && confirmLabel.font != null) label.font = confirmLabel.font;
            label.text = GameText.NameEntryDoneTyping;
            label.fontSize = 26f;
            label.enableAutoSizing = true;
            // Floor raised 16 -> 22 (2026-08-19). 22pt is the measured acuity floor for the
            // 10.1" target device in SummaRace_Readability_And_Accessibility_Audit.md 2.1;
            // at 16pt this chip was ~11.9 arcmin, below the 16' minimum the audit sets.
            // The audit's blanket advice is 26, which here would equal fontSizeMax and so
            // disable shrinking entirely — and these chips are NoWrap, so a longer string
            // would then spill outside the pill rather than shrink. 22 clears the floor and
            // keeps a little headroom, which is the safer trade while no one can run a
            // portrait render to catch an overflow.
            label.fontSizeMin = 22f;
            label.fontSizeMax = 26f;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.color = ChipText;
            label.raycastTarget = false;   // the chip's own Image is the tap target
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 6f);
            labelRect.offsetMax = new Vector2(-10f, -6f);

            // ButtonSquash on the root and nothing else: two tweens driving one localScale leave
            // it wherever the last one wrote, so any punch here would have to go on the LABEL.
            chipGo.AddComponent<SummaRace.UI.ButtonSquash>();
            doneTypingButton = chipGo.AddComponent<Button>();
            doneTypingButton.targetGraphic = image;
        }

        /// <summary>
        /// The canvas this scene's own UI lives on. A blind FindAnyObjectByType would happily
        /// return SceneLoader's persistent FadeCanvas ([Core], DontDestroyOnLoad, alpha 0) and
        /// the chip would be built invisible on the loading overlay.
        /// </summary>
        private Transform ResolveSceneCanvas()
        {
            var canvas = confirmButton != null ? confirmButton.GetComponentInParent<Canvas>() : null;
            if (canvas == null && nameInput != null) canvas = nameInput.GetComponentInParent<Canvas>();
            if (canvas != null) return canvas.rootCanvas.transform;

            foreach (var candidate in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (candidate.gameObject.scene == gameObject.scene)
                    return candidate.rootCanvas.transform;

            Debug.LogWarning("NameEntry: no scene canvas found — DONE TYPING chip not built.");
            return null;
        }
    }
}
