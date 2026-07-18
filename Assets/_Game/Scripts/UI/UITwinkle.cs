using UnityEngine;
using UnityEngine.UI;

namespace SummaRace.UI
{
    /// <summary>Soft twinkle for decorative sparkles: alpha + scale pulse on a random phase
    /// so multiple sparkles never blink in unison.</summary>
    public class UITwinkle : MonoBehaviour
    {
        [SerializeField] private float speed = 2.2f;
        [SerializeField] private float alphaMin = 0.35f;
        [SerializeField] private float scaleAmount = 0.12f;

        private Graphic _graphic;
        private Vector3 _baseScale;
        private float _baseAlpha;
        private float _phase;

        private void Awake()
        {
            _graphic = GetComponent<Graphic>();
            _baseScale = transform.localScale;
            _baseAlpha = _graphic != null ? _graphic.color.a : 1f;
            _phase = Random.value * Mathf.PI * 2f;
        }

        private void Update()
        {
            float w = (Mathf.Sin(Time.time * speed + _phase) + 1f) * 0.5f;
            if (_graphic != null)
            {
                var c = _graphic.color;
                c.a = Mathf.Lerp(alphaMin, _baseAlpha, w);
                _graphic.color = c;
            }
            transform.localScale = _baseScale * (1f + (w - 0.5f) * 2f * scaleAmount);
        }
    }
}
