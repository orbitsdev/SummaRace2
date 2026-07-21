using UnityEngine;
using UnityEngine.InputSystem;

namespace SummaRace.Features.Race.Endless
{
    /// <summary>
    /// WASD on PC for the endless race.
    ///
    /// Trash Dash's CharacterInputController reads the legacy Input class and binds only
    /// the arrow keys (plus the touch-swipe path, which is what Android already uses).
    /// SummaRace is a New Input System project, so this reads Keyboard.current and drives
    /// their public ChangeLane/Jump/Slide — their script stays untouched, which is the
    /// rule for this branch.
    ///
    /// Arrows are deliberately NOT handled here: their script still owns those, and
    /// binding both would fire twice for one press and skip two lanes.
    /// </summary>
    public class EndlessKeyboardInput : MonoBehaviour
    {
        private CharacterInputController _runner;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return; // no keyboard on device — swipe handles it

            var track = TrackManager.instance;
            if (track == null || !track.isMoving) return; // world is held during the briefing

            if (_runner == null) _runner = track.characterController;
            if (_runner == null) return;

            if (keyboard.aKey.wasPressedThisFrame) _runner.ChangeLane(-1);
            else if (keyboard.dKey.wasPressedThisFrame) _runner.ChangeLane(1);
            else if (keyboard.wKey.wasPressedThisFrame) _runner.Jump();
            else if (keyboard.sKey.wasPressedThisFrame) _runner.Slide();
        }
    }
}
