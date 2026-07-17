using SummaRace.Constants;
using SummaRace.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SummaRace.Features.Race
{
    /// <summary>
    /// Lane switching for the runner (TDD §11.2). The player stays at Z = 0;
    /// the world scrolls past (classic runner, TDD §11.3 option b).
    /// Keyboard A/D + arrows in editor, swipe on device.
    /// </summary>
    public class PlayerRunner : MonoBehaviour
    {
        // Visual polish only: the model gently yaws into a lane change and eases back.
        private const float MaxLeanYaw = 12f;
        private const float LeanEaseSpeed = 9f;

        public bool InputEnabled { get; set; }

        private int _currentLane = 1; // 0 = left, 1 = middle, 2 = right
        private float _targetX;
        private Vector2 _touchStart;
        private Transform _model; // spawned character child (null in grey-box fallback)
        private float _modelYaw;

        private void Start()
        {
            var animator = GetComponentInChildren<Animator>();
            if (animator != null) _model = animator.transform;
        }

        private void Update()
        {
            if (InputEnabled) ReadInput();

            // Tween toward the lane X (no library needed for grey-box).
            float speed = GameRules.LaneWidth / GameRules.LaneSwitchSeconds;
            var pos = transform.position;
            pos.x = Mathf.MoveTowards(pos.x, _targetX, speed * Time.deltaTime);
            transform.position = pos;

            LeanIntoLaneChange(pos.x);
        }

        /// <summary>Turns the model toward where it is heading, easing back to forward on arrival.</summary>
        private void LeanIntoLaneChange(float currentX)
        {
            if (_model == null) return;

            float remaining = _targetX - currentX;
            float targetYaw = Mathf.Clamp(remaining / GameRules.LaneWidth, -1f, 1f) * MaxLeanYaw;
            _modelYaw = Mathf.LerpAngle(_modelYaw, targetYaw, LeanEaseSpeed * Time.deltaTime);
            _model.localRotation = Quaternion.Euler(0f, _modelYaw, 0f);
        }

        private void ReadInput()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) Switch(-1);
                if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) Switch(+1);
            }

            var touch = Touchscreen.current;
            if (touch != null)
            {
                var press = touch.primaryTouch;
                if (press.press.wasPressedThisFrame)
                {
                    _touchStart = press.position.ReadValue();
                }
                else if (press.press.wasReleasedThisFrame)
                {
                    float dx = press.position.ReadValue().x - _touchStart.x;
                    if (Mathf.Abs(dx) > 60f) Switch(dx > 0 ? +1 : -1);
                }
            }
        }

        private void Switch(int direction)
        {
            int next = Mathf.Clamp(_currentLane + direction, 0, GameRules.MaxLanes - 1);
            if (next == _currentLane) return;

            _currentLane = next;
            _targetX = (_currentLane - 1) * GameRules.LaneWidth;

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(AudioKeys.SfxWhoosh);

            if (RaceController.Instance != null)
                RaceController.Instance.OnLaneSwitched(direction);
        }
    }
}
