using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SummaRace.Constants;

namespace SummaRace.Core
{
    /// <summary>
    /// Android hardware/gesture BACK: never quits the app, and never does nothing either.
    ///
    /// WHY IT CANNOT QUIT. Unity's default for BACK is to finish the activity. Nothing handled
    /// it, which meant a learner could be dropped to the launcher at any moment and the run filed
    /// as abandoned. Two things make that a weekly event across 40 tablets rather than an edge
    /// case: on gesture navigation BACK is an EDGE SWIPE, the same motion the race asks for, so
    /// the race actively trains the gesture that quits it; and on three-button navigation it is a
    /// permanent target under a portrait game, next to where a child's thumb already sits.
    ///
    /// <see cref="Application.wantsToQuit"/> is the documented hook: it fires before the app
    /// closes and returning false cancels it. It covers BACK regardless of navigation mode and
    /// regardless of input backend — which matters, because this branch runs
    /// `activeInputHandler: 2` (Both) and the race deliberately relies on the legacy touch path.
    /// An earlier version instead polled <c>escapeKey</c> and did nothing with it, believing that
    /// reading a control marks it handled. It does not: reading has no consuming semantics and
    /// the quit is performed by the platform. That version compiled, ran, and stopped nothing.
    ///
    /// WHY IT NO LONGER DOES NOTHING (owner, 2026-08-22: <i>"when android use back button native
    /// please add confirmation"</i>). Refusing the quit silently is safe but it is not honest
    /// feedback: a child presses a real button on their device and the game does not react, which
    /// reads as broken and invites pressing it repeatedly. BACK now always produces a visible
    /// response — and what that response is comes from the screen, never from here:
    ///
    ///   * a screen with a legal exit registers one, and BACK asks "Leave this screen?" with a
    ///     STAY / LEAVE choice — the confirmation the owner asked for, and STAY is the big one;
    ///   * a screen where leaving is not allowed (mid-story, mid-race-summary) registers a
    ///     REASON, and BACK says it warmly with a single OK. Nothing is punished and nothing
    ///     is lost;
    ///   * the race registers an ACTION instead: BACK opens the pause screen, because that
    ///     screen is already the race's own confirmation and a second dialog over it would be
    ///     two questions for one press;
    ///   * a screen that registers nothing falls back to the blocked form, so a screen someone
    ///     forgets to wire is quiet-but-honest rather than silent.
    ///
    /// This deliberately does NOT become a general "go back" that ignores a screen's own rules.
    /// The Reader's exit, for one, is offered only before the learner's first answer, because
    /// after that the run is study data — so the Reader registers a blocked reason from that
    /// point on, and BACK respects it exactly as the on-screen chip does.
    ///
    /// The overlay lives here, on <c>[Core]</c> (DontDestroyOnLoad), so one canvas serves every
    /// scene and no scene needs editing to gain the behaviour.
    ///
    /// NOTE: `Application.wantsToQuit` is not raised in the editor on Play-mode exit, so the
    /// quit-refusal half can only be confirmed on a device. Verify in the tablet smoke test.
    /// </summary>
    public class BackButtonGuard : MonoBehaviour
    {
        /// <summary>What the current screen wants BACK to do. Replaced on every scene load.</summary>
        private static string _blockedReason;
        private static string _leavePrompt;
        private static Action _onLeave;
        private static Action _customAction;

        private static BackButtonGuard _instance;

        private GameObject _root;
        private TextMeshProUGUI _promptLabel;
        private TextMeshProUGUI _stayLabel;
        private GameObject _leaveButton;
        private bool _open;

        // ---------------------------------------------------------------- registration API

        /// <summary>
        /// The screen has a legal exit: BACK asks before taking it.
        /// </summary>
        /// <param name="prompt">The question, in the learner's own vocabulary.</param>
        /// <param name="onLeave">Run only if they choose to leave.</param>
        public static void RegisterExit(string prompt, Action onLeave)
        {
            Clear();
            _leavePrompt = prompt;
            _onLeave = onLeave;
        }

        /// <summary>
        /// The screen has no legal exit right now: BACK says why, warmly, and stays put.
        /// Never phrased as a refusal — see GameText.BackBlocked*.
        /// </summary>
        public static void RegisterBlocked(string reason)
        {
            Clear();
            _blockedReason = reason;
        }

        /// <summary>
        /// One call for a screen whose exit is CONDITIONAL, so the condition is evaluated in
        /// exactly one place. The Reader is the case this exists for: its exit is legal only
        /// before the learner's first answer, and that decision already lives on one line.
        /// </summary>
        public static void RegisterExitOrBlock(bool canLeave, string prompt, string reason,
                                               Action onLeave)
        {
            if (canLeave) RegisterExit(prompt, onLeave);
            else RegisterBlocked(reason);
        }

        /// <summary>
        /// The screen owns its own answer to BACK — the race, whose pause screen already IS the
        /// confirmation. No overlay is shown; this runs instead.
        /// </summary>
        public static void RegisterAction(Action action)
        {
            Clear();
            _customAction = action;
        }

        /// <summary>Forget the current screen's registration. Called on every new registration,
        /// and automatically on every scene change (<see cref="OnActiveSceneChanged"/>), so a
        /// stale destination from the previous screen can never be what BACK does on this one.
        /// Scenes never need to remember to call this on the way out — no scene ever did, which
        /// is how MainMenu (registering nothing) replayed NameEntry's blocked card, or
        /// TeacherMenu's "leave?" dialog whose LEAVE reloaded MainMenu onto itself.</summary>
        public static void Clear()
        {
            _blockedReason = null;
            _leavePrompt = null;
            _onLeave = null;
            _customAction = null;
        }

        // ---------------------------------------------------------------- lifecycle

        private void Awake() => _instance = this;

        private void OnEnable()
        {
            Application.wantsToQuit += RefuseQuit;
            // This component lives on [Core] (DontDestroyOnLoad), so it outlives every scene —
            // which is exactly why it must be the one to forget them: the registrations are
            // static, no scene ever called Clear() on its way out, and Unity gives a destroyed
            // controller no reliable place to do so anyway. Hooked here, the leak is
            // structurally impossible rather than a convention each new screen must remember.
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDisable()
        {
            Application.wantsToQuit -= RefuseQuit;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// Scene teardown for the registrations AND the overlay. Without this, BACK on a screen
        /// that registered nothing replayed whatever the PREVIOUS screen registered — after
        /// NameEntry, MainMenu showed "Let's get you set up first!"; after TeacherMenu, a
        /// "leave?" dialog whose LEAVE reloaded MainMenu onto itself. Each scene then registers
        /// its own rule in Start(), which always runs after this fires, so a legitimate
        /// registration is never wiped. The dialog is dismissed SILENTLY: nobody tapped
        /// anything, so playing Close()'s click here would answer a scene change with a sound.
        /// </summary>
        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            Clear();
            Dismiss();
        }

        /// <summary>Always refuses. There is no state in which a learner tapping BACK should end
        /// the session — a teacher who genuinely wants the app closed uses the app switcher.</summary>
        private static bool RefuseQuit()
        {
#if UNITY_EDITOR
            Debug.Log("BackButtonGuard: quit refused - see the class comment for why.");
#endif
            return false;
        }

        private void Update()
        {
            // Android BACK surfaces as Escape. Guarded on Keyboard.current: on a device with no
            // keyboard device present this is null, and on the Editor it is the real Escape key,
            // which makes the whole flow testable from a desktop once the project compiles again.
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;
            OnBackPressed();
        }

        private void OnBackPressed()
        {
            // Already asking: a second press is "no", which is the answer a child pressing BACK
            // repeatedly almost certainly means. Never treat repeat presses as confirmation.
            if (_open) { Close(); return; }

            if (_customAction != null) { _customAction(); return; }

            Open(_onLeave != null);
        }

        // ---------------------------------------------------------------- the overlay

        private void Open(bool canLeave)
        {
            EnsureBuilt();
            if (_root == null) return;

            _promptLabel.text = canLeave
                ? (string.IsNullOrEmpty(_leavePrompt) ? GameText.BackLeavePrompt : _leavePrompt)
                : (string.IsNullOrEmpty(_blockedReason) ? GameText.BackBlockedDefault : _blockedReason);

            // Blocked form is one button, and it says OK rather than STAY: there is no choice
            // being offered, and a two-button dialog where one button does nothing is a dialog
            // that teaches a child their answer did not matter.
            _stayLabel.text = canLeave ? GameText.BackStayLabel : GameText.BackOkLabel;
            _leaveButton.SetActive(canLeave);
            // With no second choice there is no fork, so the single button returns to centre -
            // an off-centre lone button reads as one half of a pair whose other half failed to
            // draw.
            var stayRect = (RectTransform)_stayLabel.transform.parent;
            stayRect.anchoredPosition = new Vector2(canLeave ? -167f : 0f, 0f);

            _root.SetActive(true);
            _open = true;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxPop);
        }

        /// <summary>Hides the dialog without a sound — the scene-change path, where no tap
        /// happened and a click would be feedback for nothing.</summary>
        private void Dismiss()
        {
            _open = false;
            if (_root != null) _root.SetActive(false);
        }

        /// <summary>The learner's own "no": dismiss, and answer the tap.</summary>
        private void Close()
        {
            Dismiss();
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);
        }

        private void ConfirmLeave()
        {
            var go = _onLeave;
            Close();
            Clear();      // before running it: the destination screen registers its own
            if (go != null) go();
        }

        /// <summary>
        /// Builds the overlay once, in code, on its own canvas above everything.
        ///
        /// Sorting order 90 is above the race's pause screen (70) and its briefing (60) on
        /// purpose — the race registers a custom action so this never appears there, but if a
        /// future screen both shows a full-screen panel and registers an exit, the question must
        /// not open underneath it.
        /// </summary>
        private void EnsureBuilt()
        {
            if (_root != null) return;

            var canvasGo = new GameObject("BackConfirm");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;   // the authored scenes' value; this floats over them
            canvasGo.AddComponent<GraphicRaycaster>();
            _root = canvasGo;

            var dim = new GameObject("Dim");
            dim.transform.SetParent(canvasGo.transform, false);
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0.03f, 0.05f, 0.09f, 0.78f);
            Stretch(dimImg.rectTransform);

            var card = new GameObject("Card");
            card.transform.SetParent(canvasGo.transform, false);
            // THE SAME CARD THE RACE DRAWS, not a flat rectangle (owner, 2026-08-22: "why do you
            // use simple flat card in confirms when leave? consider as well the consistency of
            // game interface"). This was a single solid Theme.Wood Image while every other card
            // in the app — the mission briefing, the pause card, the race's own leave prompt — is
            // a wood FRAME around a cream FACE. On the one screen that interrupts a learner, the
            // app stopped looking like itself.
            //
            // WoodPanel lives in SummaRace.UI precisely so this can reach it: BackButtonGuard is
            // on [Core] and must not depend on a feature assembly's internals. Build() adds the
            // frame and the face and returns the FRAME, so the anchors below are unchanged.
            var cardImg = SummaRace.UI.WoodPanel.Build(card);
            var crt = cardImg.rectTransform;
            crt.anchorMin = new Vector2(0.08f, 0.36f);
            crt.anchorMax = new Vector2(0.92f, 0.64f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;

            _promptLabel = MakeLabel(card.transform, new Vector2(0.5f, 0.70f), 56f);
            // DARK INK, because the face under it is now CREAM, not wood. Cream type was correct
            // against the solid wood card above and would have been invisible the moment the
            // face went in — swapping the panel without swapping this is exactly how a 1:1
            // contrast ships. Theme.TextBrownDeep on Theme.Cream measures 11.9:1.
            _promptLabel.color = Theme.TextBrownDeep;
            _promptLabel.rectTransform.sizeDelta = new Vector2(760f, 220f);

            // ---- SIDE BY SIDE, NOT STACKED (owner, 2026-08-22) ----------------------------
            //
            // "please use the yes or no design side by side instead of vertical panel question
            // — most commonly games ask yes/no then left and right decision, to avoid confusion."
            //
            // He is right, and it is not only convention. A stacked pair reads as a LIST, and a
            // list implies ranking and invites reading top-to-bottom before deciding; a pair
            // side by side reads as a FORK, which is what a yes/no actually is. It is also the
            // layout a child has already met in every other game and every OS dialog they have
            // touched, so it costs no learning.
            //
            // NO is on the LEFT and YES on the RIGHT. That ordering is deliberate and matches
            // Android's own convention (dismiss left, confirm right), so muscle memory built
            // anywhere else on the tablet keeps working here. The safe answer stays the visually
            // louder of the two - green, and it is the one that does nothing irreversible.
            const float rowY = 0.30f;
            const float halfW = 310f, gap = 24f;

            var stay = MakeButton(canvasGo.transform, new Vector2(0.5f, rowY),
                                  new Vector2(halfW, 140f), new Color(0.16f, 0.46f, 0.22f));
            ((RectTransform)stay.transform).anchoredPosition = new Vector2(-(halfW + gap) * 0.5f, 0f);
            stay.onClick.AddListener(Close);
            _stayLabel = MakeLabel(stay.transform, new Vector2(0.5f, 0.5f), 44f);
            _stayLabel.color = Color.white;
            _stayLabel.rectTransform.sizeDelta = new Vector2(halfW - 30f, 110f);

            var leave = MakeButton(canvasGo.transform, new Vector2(0.5f, rowY),
                                   new Vector2(halfW, 140f), new Color(0.52f, 0.34f, 0.20f));
            ((RectTransform)leave.transform).anchoredPosition = new Vector2((halfW + gap) * 0.5f, 0f);
            leave.onClick.AddListener(ConfirmLeave);
            var ll = MakeLabel(leave.transform, new Vector2(0.5f, 0.5f), 44f);
            ll.text = GameText.BackLeaveLabel;
            ll.color = new Color(1f, 0.96f, 0.88f);
            ll.rectTransform.sizeDelta = new Vector2(halfW - 30f, 110f);
            _leaveButton = leave.gameObject;

            canvasGo.SetActive(false);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI MakeLabel(Transform parent, Vector2 anchor, float size)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.alignment = TextAlignmentOptions.Center;
            t.fontStyle = FontStyles.Bold;
            t.fontSize = size;
            t.raycastTarget = false;
            t.enableAutoSizing = true;
            t.fontSizeMin = 24f;
            t.fontSizeMax = size;
            t.overflowMode = TextOverflowModes.Ellipsis;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = Vector2.zero;
            return t;
        }

        private static Button MakeButton(Transform parent, Vector2 anchor, Vector2 size, Color fill)
        {
            var go = new GameObject("Button");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = fill;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            return btn;
        }
    }
}
