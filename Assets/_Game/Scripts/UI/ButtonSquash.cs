using PrimeTween;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SummaRace.UI
{
    /// <summary>Tactile squash-and-bounce on press for any tappable UI (Phase F juice).</summary>
    public class ButtonSquash : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private Vector3 _baseScale;

        private void Awake() => _baseScale = transform.localScale;

        public void OnPointerDown(PointerEventData eventData) =>
            Tween.Scale(transform, _baseScale * 0.92f, 0.08f, Ease.OutQuad);

        public void OnPointerUp(PointerEventData eventData) =>
            Tween.Scale(transform, _baseScale, 0.18f, Ease.OutBack);
    }
}
