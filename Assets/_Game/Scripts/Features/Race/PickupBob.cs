using UnityEngine;

namespace SummaRace.Features.Race
{
    /// <summary>Collectible feel: gentle hover bob for answer pickups (label bobs along).</summary>
    public class PickupBob : MonoBehaviour
    {
        [SerializeField] private float amplitude = 0.18f;
        [SerializeField] private float speed = 2.6f;

        private float _baseY;
        private float _phase;

        private void Start()
        {
            _baseY = transform.localPosition.y;
            _phase = transform.position.x * 1.7f; // desync lanes
        }

        private void Update()
        {
            var p = transform.localPosition;
            p.y = _baseY + Mathf.Sin(Time.time * speed + _phase) * amplitude;
            transform.localPosition = p;
        }
    }
}
