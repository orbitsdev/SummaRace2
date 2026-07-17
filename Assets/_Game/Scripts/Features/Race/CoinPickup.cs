using PrimeTween;
using SummaRace.Constants;
using SummaRace.Core;
using UnityEngine;

namespace SummaRace.Features.Race
{
    /// <summary>
    /// Decorative coin on the trail (Trash Dash-style micro-reward). Spins, and on
    /// touch: tick sound + fly-up pop. Pure juice — never affects scoring or danger.
    /// </summary>
    public class CoinPickup : MonoBehaviour
    {
        private const float SpinDegreesPerSecond = 220f;

        private bool _collected;
        private float _baseY;
        private float _phase;

        private void Start()
        {
            _baseY = transform.localPosition.y;
            _phase = transform.localPosition.z * 0.9f; // the line ripples instead of bobbing in sync
        }

        private void Update()
        {
            transform.Rotate(0f, SpinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
            if (_collected) return;
            var p = transform.localPosition;
            p.y = _baseY + Mathf.Sin(Time.time * 2.4f + _phase) * 0.12f;
            transform.localPosition = p;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_collected || !other.CompareTag("Player")) return;
            _collected = true;

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxCoin);

            var t = transform;
            t.SetParent(null, true); // stop scrolling with the world while it pops away
            Tween.PositionY(t, t.position.y + 1.6f, 0.3f, Ease.OutQuad);
            Tween.Scale(t, Vector3.zero, 0.3f, Ease.InBack)
                .OnComplete(() => { if (t != null) Destroy(t.gameObject); });
        }
    }
}
