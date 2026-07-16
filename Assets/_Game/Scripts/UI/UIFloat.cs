using PrimeTween;
using UnityEngine;

namespace SummaRace.UI
{
    /// <summary>Gentle endless bob for decorative UI — titles, logos (Phase F juice).</summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIFloat : MonoBehaviour
    {
        [SerializeField] private float amplitude = 12f;
        [SerializeField] private float period = 2.2f;

        private RectTransform _rt;
        private float _baseY;
        private bool _cached;

        private void OnEnable()
        {
            _rt = (RectTransform)transform;
            if (!_cached) { _baseY = _rt.anchoredPosition.y; _cached = true; }
            Tween.UIAnchoredPositionY(_rt, _baseY + amplitude, period, Ease.InOutSine, cycles: -1, CycleMode.Yoyo);
        }

        private void OnDisable()
        {
            Tween.StopAll(_rt);
            _rt.anchoredPosition = new Vector2(_rt.anchoredPosition.x, _baseY);
        }
    }
}
