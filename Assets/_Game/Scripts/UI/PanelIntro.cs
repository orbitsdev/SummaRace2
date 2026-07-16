using PrimeTween;
using SummaRace.Constants;
using SummaRace.Core;
using UnityEngine;

namespace SummaRace.UI
{
    /// <summary>Soft pop-in whenever a panel appears — cards, popups, question panels (Phase F juice).</summary>
    public class PanelIntro : MonoBehaviour
    {
        [SerializeField] private float delay;
        [SerializeField] private bool playSound = true;

        private Vector3 _baseScale;
        private bool _cached;

        private void OnEnable()
        {
            if (!_cached) { _baseScale = transform.localScale; _cached = true; }
            transform.localScale = _baseScale * 0.85f;
            Tween.Scale(transform, _baseScale, 0.3f, Ease.OutBack, startDelay: delay);
            if (playSound && AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(AudioKeys.SfxPop);
        }
    }
}
