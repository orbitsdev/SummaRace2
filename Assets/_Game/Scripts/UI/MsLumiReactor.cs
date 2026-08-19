using System.Collections;
using PrimeTween;
using SummaRace.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SummaRace.UI
{
    /// <summary>
    /// Makes Ms. Lumi a living reading buddy: she rests in one pose, and pops to a cheer
    /// pose (with a happy punch) when the learner gets something right, then settles back.
    /// Pose swaps only — lightweight, offline, on-style.
    ///
    /// She reacts to the three "you got it" moments the EventBus already carries — a correct
    /// Reader answer, a verified Arrange order, a submitted Summary — so one component works
    /// in every scene she appears in and no feature has to know she exists (TDD §8.2).
    ///
    /// SHE NEVER REACTS TO A WRONG ANSWER. That is not an omission: a disappointed teacher at
    /// the moment a struggling reader is most fragile is exactly what the never-punish rule
    /// forbids, and it is the one place a character like this can do real harm to the study.
    /// Poses that read as disapproval or dismay exist in the folder but are deliberately not
    /// in any pool this component draws from.
    ///
    /// SIZING. Set <see cref="headPixels"/> to how wide her head should be on a 1080-wide
    /// reference canvas, and every pose obeys — see LumiPoseNormalizer for why that is the
    /// only measurement that makes a set of independently cropped poses look like one person.
    /// Leave it at 0 to size her the old way, from the RectTransform, which is what a scene
    /// wired before the pose pool existed does.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class MsLumiReactor : MonoBehaviour
    {
        [Header("Fallback poses (used only when Resources/UI/Lumi is empty)")]
        [SerializeField] private Sprite idleSprite;
        [SerializeField] private Sprite cheerSprite;

        [Header("Pose pools")]
        [Tooltip("Pool she rests in. The part of a filename before the first underscore.")]
        [SerializeField] private string restPool = LumiExpressions.PoolIdle;

        [Tooltip("Pool she celebrates with. Only ever shown for something the learner got right.")]
        [SerializeField] private string cheerPool = LumiExpressions.PoolCheer;

        [Tooltip("Optional exact pose to rest in, e.g. idle_present. Overrides the rest pool, " +
                 "for a screen where she is doing something specific rather than just being there.")]
        [SerializeField] private string restPose = "";

        [Tooltip("Re-draw a new resting pose every time she settles, instead of holding one.")]
        [SerializeField] private bool shuffleRestPose = true;

        [Header("Reaction")]
        [SerializeField] private float cheerSeconds = 1.6f;
        [SerializeField] private bool reactToReaderAnswer = true;
        [SerializeField] private bool reactToArrangeVerified = true;
        [SerializeField] private bool reactToSummarySubmitted = true;

        [Header("Layout")]
        [Tooltip("Her head width in reference-canvas pixels. 0 = leave the RectTransform alone.")]
        [SerializeField] private float headPixels = 0f;

        private Image _image;
        private CanvasGroup _group;
        private RectTransform _rect;
        private Coroutine _routine;
        private Sprite _restSprite;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _group = GetComponent<CanvasGroup>();
            _rect = (RectTransform)transform;

            // Head-anchored layout needs a point anchor, because a stretched rect derives its
            // size from the parent and would fight the per-pose size. Collapsing it here rather
            // than demanding it in every scene means an existing screen keeps working untouched.
            if (headPixels > 0f) CollapseToPointAnchor();

            ShowRest();
        }

        private void OnEnable()
        {
            if (reactToReaderAnswer) EventBus.Subscribe<PageAnswered>(OnPageAnswered);
            if (reactToArrangeVerified) EventBus.Subscribe<ArrangeVerified>(OnArrangeVerified);
            if (reactToSummarySubmitted) EventBus.Subscribe<SummarySubmitted>(OnSummarySubmitted);
        }

        private void OnDisable()
        {
            if (reactToReaderAnswer) EventBus.Unsubscribe<PageAnswered>(OnPageAnswered);
            if (reactToArrangeVerified) EventBus.Unsubscribe<ArrangeVerified>(OnArrangeVerified);
            if (reactToSummarySubmitted) EventBus.Unsubscribe<SummarySubmitted>(OnSummarySubmitted);
        }

        // Never react to a wrong answer — see the class comment.
        private void OnPageAnswered(PageAnswered evt) { if (evt.correct) Celebrate(); }
        private void OnArrangeVerified(ArrangeVerified evt) { if (evt.correct) Celebrate(); }
        private void OnSummarySubmitted(SummarySubmitted evt) { Celebrate(); }

        /// <summary>The badge portrait both Arrange and Summary already carry.</summary>
        public const string BadgeObjectName = "TeacherAvatar";

        /// <summary>
        /// Point this reactor at a different pair of pools and re-draw. Needed because
        /// <see cref="Awake"/> runs the moment AddComponent returns, so a runtime-attached
        /// reactor has already drawn from the default pools before anything can configure it.
        /// </summary>
        public void Configure(string rest, string cheer)
        {
            if (!string.IsNullOrEmpty(rest)) restPool = rest;
            if (!string.IsNullOrEmpty(cheer)) cheerPool = cheer;
            _restSprite = null;
            ShowRest();
        }

        /// <summary>
        /// Give a screen's badge portrait a reaction, using the disc-cropped pools.
        ///
        /// Arrange and Summary have shown Ms. Lumi since F7, but she never moved: the
        /// reactToArrangeVerified / reactToSummarySubmitted switches on this component had no
        /// component to live on, because only Reader.unity ever carried one. Attaching from the
        /// controller rather than editing the two scenes keeps working if either screen is
        /// rebuilt, and does nothing at all when the object or the badge art is absent — so this
        /// can only ever add a reaction, never take a screen away.
        ///
        /// <paramref name="headPixels"/> stays at its 0 default, which is what makes this safe:
        /// ApplyPose then swaps the sprite and returns without touching the RectTransform, so the
        /// scene's own framing of the badge is preserved exactly.
        /// </summary>
        public static MsLumiReactor AttachBadge(string objectName = BadgeObjectName)
        {
            var go = GameObject.Find(objectName);
            if (go == null) return null;
            if (go.GetComponent<Image>() == null) return null;

            var existing = go.GetComponent<MsLumiReactor>();
            if (existing != null) return existing;   // a scene that wires it wins over this

            // No badge art means the pools are empty and every draw would return null, leaving
            // the scene's own sprite in place. Bail rather than add a component that does nothing.
            if (LumiExpressions.CountIn(LumiExpressions.PoolBadgeIdle) == 0) return null;

            var r = go.AddComponent<MsLumiReactor>();
            r.Configure(LumiExpressions.PoolBadgeIdle, LumiExpressions.PoolBadgeCheer);
            return r;
        }

        /// <summary>Pop to a cheer pose and settle back. Safe to call from anywhere.</summary>
        public void Celebrate()
        {
            if (!isActiveAndEnabled) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(CheerRoutine());
        }

        private IEnumerator CheerRoutine()
        {
            var cheer = LumiExpressions.Next(cheerPool) ?? cheerSprite;
            ApplyPose(cheer);
            if (_group != null) _group.alpha = 1f; // pop back in (she may have been hidden)
            Tween.PunchScale(transform, Vector3.one * 0.18f, 0.5f);
            yield return new WaitForSeconds(cheerSeconds);
            ShowRest();
            _routine = null;
        }

        /// <summary>
        /// Her resting pose. Prefers a named pose, then the rest pool, then the sprite the
        /// scene was wired with before the pool existed — so she is never worse off than before.
        /// </summary>
        public void ShowRest()
        {
            if (_restSprite == null || shuffleRestPose)
            {
                _restSprite =
                    LumiExpressions.Get(restPose) ??
                    LumiExpressions.Next(restPool) ??
                    idleSprite;
            }
            ApplyPose(_restSprite);
        }

        /// <summary>Show one named pose and hold it, e.g. while a prompt is on screen.</summary>
        public void ShowPose(string poseName)
        {
            var s = LumiExpressions.Get(poseName);
            if (s == null) return;
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            _restSprite = s;
            ApplyPose(s);
        }

        private void ApplyPose(Sprite sprite)
        {
            if (sprite == null || _image == null) return;
            _image.sprite = sprite;
            if (headPixels <= 0f) return;

            // LumiPoseNormalizer stores her head width in pixelsPerUnit and her head centre in
            // the pivot, so this is the whole of the per-pose layout: size the rect in head
            // widths, then park the rect on the point that IS her head. Her face then holds
            // still while arms and body change around it. Nothing else animates the pivot, and
            // UIFloat's bob is stored as anchoredPosition, which this does not touch — so the
            // two compose rather than fight.
            float ppu = sprite.pixelsPerUnit;
            if (ppu <= 0f) return;

            var rect = sprite.rect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            _rect.pivot = new Vector2(sprite.pivot.x / rect.width, sprite.pivot.y / rect.height);
            _rect.sizeDelta = new Vector2(rect.width / ppu, rect.height / ppu) * headPixels;
        }

        /// <summary>
        /// Turn a stretched rect into a point anchor at the same place and size, so per-pose
        /// sizing has somewhere to write. Size is captured first because changing the anchors
        /// alone would re-derive it from the untouched offsets.
        /// </summary>
        private void CollapseToPointAnchor()
        {
            if (_rect.anchorMin == _rect.anchorMax) return;

            var size = _rect.rect.size;
            var localPos = _rect.localPosition;
            var centre = (_rect.anchorMin + _rect.anchorMax) * 0.5f;

            _rect.anchorMin = centre;
            _rect.anchorMax = centre;
            _rect.sizeDelta = size;
            _rect.localPosition = localPos;
        }
    }
}
