using UnityEngine;

namespace SummaRace.Features.Race.Endless
{
    /// <summary>
    /// Glues our uncurved gate visuals (kit sprites + TMP) to the Trash Dash curved
    /// world: their CurvedCode.cginc drops clip-space Y by _CurveStrength·d², so at
    /// 80 m a straight-line card floats ~18 m above the visually-bent road. Reproduce
    /// the same dip in world units each frame (dip→0 at pickup range, so triggers
    /// stay honest — F31 lesson).
    /// </summary>
    public class EndlessCurveDip : MonoBehaviour
    {
        private static readonly int CurveStrengthId = Shader.PropertyToID("_CurveStrength");

        private float _baseLocalY;
        private Transform _cam;
        private float _frustumScale; // tan(vertical FOV / 2)

        private void Start()
        {
            _baseLocalY = transform.localPosition.y;
            var cam = Camera.main;
            _cam = cam != null ? cam.transform : null;
            _frustumScale = cam != null
                ? Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad)
                : 0.6f;
        }

        private void LateUpdate()
        {
            if (_cam == null) return;
            float d = Mathf.Max(0f, transform.position.z - _cam.position.z);
            float dip = Shader.GetGlobalFloat(CurveStrengthId) * d * d * _frustumScale;
            var p = transform.localPosition;
            p.y = _baseLocalY - dip;
            transform.localPosition = p;
        }
    }
}
