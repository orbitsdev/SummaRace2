using System.Collections;
using PrimeTween;
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
        /// How many naming detours this process may attempt before giving up and letting the
        /// learner stay on a working Main Menu. SceneLoader answers a scene that is missing
        /// from Build Settings by loading the Main Menu instead — and this project has had its
        /// build list wiped twice by asset imports — so an unguarded redirect bounces
        /// MainMenu → NameEntry → MainMenu with no way out.
        /// </summary>
        private const int MaxNameEntryOffers = 2;

        /// <summary>How long to wait before deciding a dispatched redirect was DROPPED. A load
        /// that takes destroys this object and its coroutine long before this elapses.</summary>
        private const float NameEntryRedirectGraceSeconds = 2f;

        /// <summary>
        /// Naming detours attempted this process.
        ///
        /// THIS WAS A PLAIN static bool AND IT WAS WRONG IN BOTH DIRECTIONS. Written as
        /// `_nameEntryOffered = needsNaming`, it (a) latched true the instant a redirect was
        /// DISPATCHED, never checking that it landed, so one dropped hand-off disarmed the
        /// unnamed-learner safety net for the rest of the process and the next run was filed
        /// against a nameless profile — unjoinable to the child's paper booklet, and invisible
        /// until export; and (b) it did not even stop the ping-pong it existed for, because the
        /// bounced-back arrival computed `needsNaming == false` and therefore wrote the flag
        /// back to FALSE, re-arming the redirect on the arrival after that: a two-beat loop
        /// rather than a one-beat one.
        ///
        /// A count fixes both. It only ever RISES while the learner is unnamed (so the loop is
        /// hard-capped), it is reset to zero only on an arrival with a NAMED learner (which
        /// proves the detour worked and the next learner switch may use it too), and an attempt
        /// that is still sitting here <see cref="NameEntryRedirectGraceSeconds"/> later is
        /// refunded, because a redirect that never happened must not spend the net.
        /// </summary>
        private static int _nameEntryOffers;

        /// <summary>Set when this arrival's redirect was dispatched and did not take, so the
        /// screen can say so instead of quietly recording the run against "Runner".</summary>
        private bool _nameEntryDropped;

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
            bool unnamed = learner != null && !learner.named;
            bool needsNaming = unnamed && _nameEntryOffers < MaxNameEntryOffers;
            if (needsNaming) _nameEntryOffers++;
            // Only a NAMED arrival re-arms the net. Never clear it on an unnamed arrival — that
            // is the two-beat ping-pong the old bool created (see the field's note).
            else if (learner != null && learner.named) _nameEntryOffers = 0;

            if (unnamed && !needsNaming)
            {
                // Out of attempts: NameEntry is unreachable (almost always missing from Build
                // Settings). Say so once, loudly, because from here every run on this tablet is
                // logged against the default profile and cannot be joined to a paper booklet.
                Debug.LogError("MainMenu: the learner has no name and Name Entry could not be " +
                               "reached after " + MaxNameEntryOffers + " attempts. Runs will be " +
                               "logged against the default profile — check NameEntry is in Build Settings.");
                _nameEntryDropped = true;
            }

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
            else if (learner != null)
            {
                // The line could not be built AND the scene does not wire one — so the single
                // safeguard against a wrong-learner run does not exist on this screen. It used
                // to fail exactly this quietly (BuildActiveLearnerLine returns null on three
                // separate paths, all of them silent), which is how a shared tablet could record
                // a whole session against the wrong child with nothing on screen to catch it.
                Debug.LogError("MainMenu: no active-learner line — this run will be recorded " +
                               "against '" + learner.displayName + "' with nothing on screen saying so.");
            }

            // Set above when the attempts ran out; applied here because the pill only exists now.
            if (_nameEntryDropped) MarkLearnerUnnamed();

            // Survive being opened directly in the editor for testing (TDD §13).
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayMusic(AudioKeys.MusicMenu);

            if (startButton != null)
                startButton.onClick.AddListener(OnStartTapped);

            if (teacherButton != null)
            {
                // The teacher corner is the only button on this screen with NO press feedback —
                // it never got a ButtonSquash, and it is deliberately low-contrast, so on a
                // muted classroom tablet a tap on it is answered by nothing at all and reads as
                // a broken control. Squash on the ROOT (the standard component, and this root
                // carries no other scale tween) plus a punch on the LABEL, which is a different
                // transform — two tweens driving ONE localScale leave it wherever the last one
                // wrote, which is why the punch must never go on the root as well.
                if (teacherButton.GetComponent<SummaRace.UI.ButtonSquash>() == null)
                    teacherButton.gameObject.AddComponent<SummaRace.UI.ButtonSquash>();

                var teacherLabel = teacherButton.GetComponentInChildren<TMP_Text>(true);

                teacherButton.onClick.AddListener(() =>
                {
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);
                    // Motion, not just sound: the scene change that follows is the real
                    // confirmation, but SceneLoader's fade takes a moment to appear.
                    if (teacherLabel != null)
                        Tween.PunchScale(teacherLabel.transform, Vector3.one * 0.22f, 0.35f);
                    SceneLoader.Go(SceneNames.TeacherMenu);
                });
            }

            SilenceDecorRaycasts();

            // Now that the screen works on its own, take the naming detour — and watch it, so a
            // dispatch that does not land is noticed rather than assumed.
            if (needsNaming) StartCoroutine(OfferNameEntry());
        }

        /// <summary>
        /// Dispatches the Name Entry redirect and refunds the attempt if it never lands.
        ///
        /// A load that takes destroys this GameObject, which kills this coroutine — so simply
        /// still being here after the grace period IS the failure signal, with no need to ask
        /// SceneLoader anything (and nothing to ask: Go() returns void and no-ops silently when
        /// there is no Instance).
        /// </summary>
        private IEnumerator OfferNameEntry()
        {
            SceneLoader.Go(SceneNames.NameEntry);

            // Realtime: a paused or slowed timeScale must not stretch a safety check.
            yield return new WaitForSecondsRealtime(NameEntryRedirectGraceSeconds);

            // Refund, so the net stays armed for the next arrival instead of disarming itself
            // for the whole process — the exact failure the old static bool had.
            _nameEntryOffers = Mathf.Max(0, _nameEntryOffers - 1);
            _nameEntryDropped = true;
            Debug.LogError("MainMenu: the Name Entry redirect was dispatched and did not land. " +
                           "The learner is unnamed and this run would be logged against the " +
                           "default profile — check NameEntry is in Build Settings.");
            MarkLearnerUnnamed();
        }

        /// <summary>
        /// Recolours the learner chip when the profile never got named. A log line reaches
        /// nobody on a tablet in a classroom, and this is the one condition under which the
        /// exported rows cannot be joined to the child's paper booklet — so it has to be
        /// visible to the adult standing there. Amber, not red: nothing about it is the
        /// learner's fault, and the game itself keeps working normally.
        /// </summary>
        private void MarkLearnerUnnamed()
        {
            if (_activeLearnerPill != null)
            {
                var pill = _activeLearnerPill.GetComponent<Image>();
                // Tint, not replace: with a sprite the Image colour multiplies it, without one
                // it IS the fill, and amber reads as amber either way.
                if (pill != null) pill.color = Theme.AmberWarn;
                _activeLearnerPill.SetActive(true);
            }
            // Amber is a FILL colour in this project's theme and fails as a text background for
            // light type (see Theme's measured pairs), so the label flips to dark brown.
            if (activeLearnerText != null) activeLearnerText.color = Theme.TextBrownDeep;
        }

        /// <summary>
        /// Turns off raycasts on the purely decorative art.
        ///
        /// `DecorCoins`, `DecorGems` and the three logo `Sparkle_*` images are Images with
        /// `raycastTarget` left at its default ON, they sit above the rest of the canvas, and
        /// the two decor pieces are in the BOTTOM band — so the shiniest, most tappable-looking
        /// objects on the screen swallow a tap and answer with silence, next to a TAP TO START
        /// that a child has not found yet. Done by name at runtime because the scene is out of
        /// scope for this pass; every lookup is null-safe and a renamed object simply skips.
        /// </summary>
        private void SilenceDecorRaycasts()
        {
            var canvas = ResolveSceneCanvas();
            if (canvas == null) return;

            // Whole subtree, including inactive: an object switched on later must not
            // re-introduce the same dead tap.
            foreach (var graphic in canvas.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == null) continue;
                string n = graphic.gameObject.name;
                // Prefix match on Sparkle so all three (and any future one) are covered.
                if (n == "DecorCoins" || n == "DecorGems" || n.StartsWith("Sparkle"))
                    graphic.raycastTarget = false;
            }
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
            // Canvas first, and from ANY wired reference. The old code could only reach it
            // through `subtitleText`, so a scene that did not wire the SUBTITLE silently lost
            // the LEARNER LINE as well — two unrelated things failing together.
            var canvas = ResolveSceneCanvas();
            if (canvas == null)
            {
                Debug.LogError("MainMenu: no scene canvas — the active-learner line cannot be built.");
                return null;
            }

            // Grab the donor font BEFORE anything new is parented under the canvas, or the
            // search below can find the object it is about to fill in.
            var donor = subtitleText != null ? subtitleText
                      : startLabel != null ? startLabel
                      : canvas.GetComponentInChildren<TMP_Text>(true);

            TMP_Text text = null;
            if (subtitleText != null)
            {
                // Parent to the canvas, not to the subtitle's own parent: the lockup it may sit
                // in is a band of its own, and fractional anchors inside it would land nowhere
                // useful. Cloned rather than built fresh so it carries the screen's own TMP font
                // asset and material (same reason the teacher menu clones its buttons).
                var clone = Instantiate(subtitleText.gameObject, canvas, false);
                clone.name = "ActiveLearner";
                clone.SetActive(true);

                text = clone.GetComponent<TMP_Text>();
                if (text == null) Destroy(clone);   // fall through to the fresh build below
            }

            if (text == null)
            {
                // THE FALLBACK, AND WHY IT HAS TO EXIST: `activeLearnerText` is NOT serialized
                // in MainMenu.unity, so this method is the ONLY thing that ever produces the
                // line — and it used to return null on three separate silent paths, the first
                // of them "the subtitle is null". On a tablet shared by two children the line
                // is the only warning that a run is being recorded against the wrong learner,
                // and a wrong-learner run cannot be undone once exported. A fresh TMP object
                // needs no scene wiring at all; it only lacks the subtitle's font, borrowed above.
                var go = new GameObject("ActiveLearner", typeof(RectTransform));
                go.transform.SetParent(canvas, false);
                var fresh = go.AddComponent<TextMeshProUGUI>();
                if (donor != null && donor.font != null) fresh.font = donor.font;
                text = fresh;
            }

            // This line was deep navy with no backing, on the assumption that y 0.15-0.215 is the
            // "bright sky backdrop". It is not: bg_playground puts its DARK DIRT band exactly
            // there, so on a real portrait render the text came out navy-on-brown and half cut by
            // the ground edge — verified in a full playthrough. Rather than chase the backdrop
            // (which changes per scene tint, and is art someone may re-render), give the line its
            // own pill and stop depending on what is behind it — the same reasoning that fixed
            // StorySelect's locked cards, and the navy chip is already this project's language
            // for a small persistent label (Resources/UI/bar_bg, used by the race HUD).
            var pillGo = new GameObject("ActiveLearnerPill", typeof(RectTransform));
            pillGo.transform.SetParent(canvas, false);
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

        /// <summary>
        /// This scene's own canvas. Scoped to <c>gameObject.scene</c> in the search fallback for
        /// the same reason NameEntryController.ResolveSceneCanvas is: a blind
        /// FindAnyObjectByType would happily return SceneLoader's persistent FadeCanvas
        /// ([Core], DontDestroyOnLoad, alpha 0) and the learner line would be built invisible
        /// on the loading overlay.
        /// </summary>
        private Transform ResolveSceneCanvas()
        {
            var canvas = subtitleText != null ? subtitleText.GetComponentInParent<Canvas>() : null;
            if (canvas == null && startButton != null) canvas = startButton.GetComponentInParent<Canvas>();
            if (canvas == null && teacherButton != null) canvas = teacherButton.GetComponentInParent<Canvas>();
            if (canvas == null && startLabel != null) canvas = startLabel.GetComponentInParent<Canvas>();
            if (canvas != null) return canvas.rootCanvas.transform;

            foreach (var candidate in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (candidate.gameObject.scene == gameObject.scene)
                    return candidate.rootCanvas.transform;

            return null;
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
