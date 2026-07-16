using UnityEngine;

namespace SummaRace.Features.Race
{
    /// <summary>
    /// Smooth chase camera for the race: eases sideways after the runner's
    /// lane changes and kicks the FOV out during speed boosts (GDD F juice).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class RaceChaseCamera : MonoBehaviour
    {
        [SerializeField] private float followShare = 0.55f; // how much of the lane offset the camera follows
        [SerializeField] private float positionDamping = 6f;
        [SerializeField] private float baseFov = 60f;
        [SerializeField] private float boostFov = 68f;
        [SerializeField] private float fovDamping = 4f;

        private Camera _cam;
        private Transform _target;
        private Vector3 _basePos;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _basePos = transform.position;
        }

        public void SetTarget(Transform target) => _target = target;

        private void LateUpdate()
        {
            if (_target == null) return;

            float wantedX = _target.position.x * followShare;
            float x = Mathf.Lerp(transform.position.x, wantedX, positionDamping * Time.deltaTime);
            transform.position = new Vector3(x, _basePos.y, _basePos.z);

            float mult = RaceController.Instance != null ? RaceController.Instance.SpeedMultiplier : 1f;
            float wantedFov = mult > 1.05f ? boostFov : baseFov;
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, wantedFov, fovDamping * Time.deltaTime);
        }
    }
}
