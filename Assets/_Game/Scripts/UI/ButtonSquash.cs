using PrimeTween;
using SummaRace.Constants;
using SummaRace.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SummaRace.UI
{
    /// <summary>Tactile squash-and-bounce on press for any tappable UI (Phase F juice).</summary>
    public class ButtonSquash : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
                /// <summary>
        /// Off by default, because this component is on EVERY button in the game while nearly
        /// every button's own handler already plays SfxClick — so one physical tap fired
        /// sfx_press on finger-DOWN and sfx_click on finger-UP, about 100ms apart. Two
        /// different sounds that close together do not read as "responsive", they read as a
        /// stutter or a double-tap. On a navigation button it was three: press, click, then
        /// SceneLoader's transition chime.
        ///
        /// The squash animation stays on unconditionally — the VISUAL already answers the
        /// touch, which is the part that matters on a muted classroom tablet.
        ///
        /// Turn it back on per-button only where the button has no click sound of its own.
        /// Same switch, same default, and the same reason as PanelIntro.playSound.
        /// </summary>
        [SerializeField] private bool playSound = false;

        private Vector3 _baseScale;

        private void Awake() => _baseScale = transform.localScale;

        public void OnPointerDown(PointerEventData eventData)
        {
            Tween.Scale(transform, _baseScale * 0.92f, 0.08f, Ease.OutQuad);
            if (playSound && AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(AudioKeys.SfxPress);
        }

        public void OnPointerUp(PointerEventData eventData) =>
            Tween.Scale(transform, _baseScale, 0.18f, Ease.OutBack);
    }
}
