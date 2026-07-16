using PrimeTween;
using UnityEngine;

namespace SummaRace.UI
{
    /// <summary>Soft pop-in whenever a panel appears — cards, popups, question panels (Phase F juice).</summary>
    public class PanelIntro : MonoBehaviour
    {
        [SerializeField] private float delay;

        private Vector3 _baseScale;
        private bool _cached;

        private void OnEnable()
        {
            if (!_cached) { _baseScale = transform.localScale; _cached = true; }
            transform.localScale = _baseScale * 0.85f;
            Tween.Scale(transform, _baseScale, 0.3f, Ease.OutBack, startDelay: delay);
        }
    }
}
