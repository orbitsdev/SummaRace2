using UnityEngine;

namespace SummaRace.Features.Race
{
    /// <summary>
    /// Keeps uncurved visuals (sprite cards, TMP labels, particle pads) glued to the
    /// curved world: offsets this group downward by the same visual dip the
    /// CurvedVertexColor shader applies at its distance from the camera.
    /// </summary>
    public class CurveDip : MonoBehaviour
    {
        /// <summary>Set by RaceController: curve strength in road mode, 0 otherwise.</summary>
        public static float Strength;

        // tan(FOV/2) for the race camera (59°) — converts the shader's clip-space
        // shift into world units at a given view depth.
        private const float FrustumScale = 0.566f;

        private float _baseY;
        private Transform _cam;
        private SpriteRenderer[] _sprites;
        private TMPro.TMP_Text[] _labels;

        private void Start()
        {
            _baseY = transform.localPosition.y;
            _cam = Camera.main != null ? Camera.main.transform : null;
            _sprites = GetComponentsInChildren<SpriteRenderer>(true);
            _labels = GetComponentsInChildren<TMPro.TMP_Text>(true);
        }

        private void LateUpdate()
        {
            if (_cam == null) return;
            float d = Mathf.Max(0f, transform.position.z - _cam.position.z);
            float dip = Strength * d * d * FrustumScale;
            var p = transform.localPosition;
            p.y = _baseY - dip;
            transform.localPosition = p;

            // Fade cards as they reach the camera so they never wall off the view.
            float alpha = Mathf.Clamp01(Mathf.InverseLerp(2.5f, 7f, d));
            alpha = Mathf.Max(alpha, 0.15f);
            foreach (var s in _sprites)
            {
                if (s == null) continue;
                var c = s.color; c.a = alpha; s.color = c;
            }
            foreach (var t in _labels)
                if (t != null) t.alpha = alpha;
        }
    }
}
