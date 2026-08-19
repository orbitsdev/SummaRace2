using SummaRace.Constants;
using SummaRace.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Trash Dash ships its own GameManager in the global namespace, which outranks a
// using-directive, and an alias named GameManager is illegal for the same reason (CS0576).
using Core = SummaRace.Core;

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

        [Tooltip("Optional, discreet: names the learner this run will be recorded against. " +
                 "On a tablet shared by two children a wrong-learner run cannot be undone " +
                 "once exported, so it is worth a corner of the screen.")]
        [SerializeField] private TMP_Text activeLearnerText;

        /// <summary>The navy chip built behind <see cref="activeLearnerText"/> when it is not
        /// wired in the scene. Held so it can be hidden along with an empty name.</summary>
        private GameObject _activeLearnerPill;

        /// <summary>
        /// One naming detour per arrival, so this screen can never trade places with Name Entry
        /// forever. SceneLoader answers a scene that is missing from Build Settings by loading
        /// the Main Menu instead — and this project has had its build list wiped twice by asset
        /// imports — so an unguarded redirect would have bounced MainMenu → NameEntry → MainMenu
        /// with no way out. Cleared again the moment we arrive with a named learner, which means
        /// the detour worked and the next switch may use it too.
        /// </summary>
        private static bool _nameEntryOffered;

        private void Start()
        {
            var learner = Core.GameManager.Instance != null
                ? Core.GameManager.Instance.CurrentLearner
                : null;

            // A learner with no name must never reach a story. Every exported log row keys on
            // their profile id and only the companion roster turns that id back into a child, so
            // a run played before Name Entry is filed under the default "Runner" and cannot be
            // attributed afterwards. Boot covers the fresh device; this covers every other way to
            // arrive here holding a nameless profile — the teacher's picker choosing one, a
            // "+ New learner" abandoned by killing the app at Name Entry, and the blank profile
            // that a tablet reset or a post-study wipe leaves behind. It cannot loop: Name Entry
            // always sets `named`, including when the box is left empty (that is not an error).
            //
            // NOTE THE ORDER. This used to `return` here, before a single button was wired, on
            // the assumption that the redirect always takes. It does not have to: any dropped or
            // deferred scene change leaves a fully drawn Main Menu on which TAP TO START and the
            // teacher corner are both inert and there is no music — and since BackButtonGuard
            // swallows Android BACK, nothing the learner or the teacher can do. That is reachable
            // from the post-study wipe and from the documented PIN-recovery reset, both of which
            // mint a blank unnamed profile and then return here. So: dress and wire the screen
            // first, and let the redirect be an optimisation rather than a load-bearing step.
            bool needsNaming = learner != null && !learner.named && !_nameEntryOffered;
            _nameEntryOffered = needsNaming;

            if (startLabel != null) startLabel.text = GameText.TapToStart;
            if (subtitleText != null) subtitleText.text = GameText.BootTagline;

            if (activeLearnerText == null) activeLearnerText = BuildActiveLearnerLine();
            if (activeLearnerText != null)
            {
                // Blank rather than a placeholder when played straight from the editor: there is
                // no learner then, and inventing a name here would read as a real one.
                activeLearnerText.text = learner != null ? GameText.PlayingAs(learner.displayName) : string.Empty;
                // An empty pill reads as a UI glitch, so the chip goes with the name.
                if (_activeLearnerPill != null) _activeLearnerPill.SetActive(learner != null);
            }

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

            // Now that the screen works on its own, take the naming detour.
            if (needsNaming) SceneLoader.Go(SceneNames.NameEntry);
        }

        /// <summary>
        /// Builds the discreet "Playing as …" line when the scene carries none. The field was
        /// added after MainMenu was last authored, so on the scene as it stands today it is null
        /// and the safeguard would simply not exist — which is the whole failure it guards
        /// against: on a shared tablet a wrong-learner run is invisible until the logs are
        /// exported, and unrecoverable by then. Cloned from the subtitle rather than built fresh
        /// so it carries the screen's own TMP font asset and material (same reason the teacher
        /// menu clones its buttons). Sits in the free band between TAP TO START (bottom 0.248)
        /// and the corner decor (top 0.12), clear of the bottom-right teacher button.
        /// </summary>
        private TMP_Text BuildActiveLearnerLine()
        {
            if (subtitleText == null) return null;

            var canvas = subtitleText.GetComponentInParent<Canvas>();
            if (canvas == null) return null;

            // Parent to the canvas, not to the subtitle's own parent: the lockup it may sit in
            // is a band of its own, and fractional anchors inside it would land nowhere useful.
            var clone = Instantiate(subtitleText.gameObject, canvas.transform, false);
            clone.name = "ActiveLearner";
            clone.SetActive(true);

            var text = clone.GetComponent<TMP_Text>();
            if (text == null) { Destroy(clone); return null; }

            // This line was deep navy with no backing, on the assumption that y 0.15-0.215 is the
            // "bright sky backdrop". It is not: bg_playground puts its DARK DIRT band exactly
            // there, so on a real portrait render the text came out navy-on-brown and half cut by
            // the ground edge — verified in a full playthrough. Rather than chase the backdrop
            // (which changes per scene tint, and is art someone may re-render), give the line its
            // own pill and stop depending on what is behind it — the same reasoning that fixed
            // StorySelect's locked cards, and the navy chip is already this project's language
            // for a small persistent label (Resources/UI/bar_bg, used by the race HUD).
            var pillGo = new GameObject("ActiveLearnerPill", typeof(RectTransform));
            pillGo.transform.SetParent(canvas.transform, false);
            var pill = pillGo.AddComponent<UnityEngine.UI.Image>();
            pill.sprite = Resources.Load<Sprite>("UI/bar_bg");
            if (pill.sprite != null) pill.type = UnityEngine.UI.Image.Type.Sliced;
            pill.color = pill.sprite != null ? Color.white : Theme.Alpha(Theme.Navy, 0.92f);
            pill.raycastTarget = false;
            var prect = pill.rectTransform;
            prect.anchorMin = new Vector2(0.26f, 0.152f);
            prect.anchorMax = new Vector2(0.74f, 0.203f);
            prect.offsetMin = Vector2.zero;
            prect.offsetMax = Vector2.zero;

            text.transform.SetParent(pillGo.transform, false);
            text.fontSize = 30f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 20f;
            text.fontSizeMax = 30f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Theme.Paper;   // light on the navy pill
            text.raycastTarget = false;                 // never steal a tap from TAP TO START

            var rect = text.rectTransform;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(16f, 6f);
            rect.offsetMax = new Vector2(-16f, -6f);

            // The pill only makes sense with a name on it; MainMenu blanks the text when there is
            // no learner (editor-direct play), so hide the whole chip in that case.
            pillGo.SetActive(true);
            _activeLearnerPill = pillGo;
            return text;
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
