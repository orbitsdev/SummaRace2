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
        public bool InputEnabled { get; set; }

        private int _currentLane = 1; // 0 = left, 1 = middle, 2 = right
        private float _targetX;
        private Vector2 _touchStart;

        private void Update()
        {
            if (InputEnabled) ReadInput();

            // Tween toward the lane X (no library needed for grey-box).
            float speed = GameRules.LaneWidth / GameRules.LaneSwitchSeconds;
            var pos = transform.position;
            pos.x = Mathf.MoveTowards(pos.x, _targetX, speed * Time.deltaTime);
            transform.position = pos;
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
        }
    }
}
