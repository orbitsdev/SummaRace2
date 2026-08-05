using UnityEngine;
using UnityEngine.InputSystem;

namespace SummaRace.Core
{
    /// <summary>
    /// Swallows the Android hardware/gesture BACK so it can never close the app.
    ///
    /// Unity's default behaviour for BACK is to finish the activity — quit. Nothing in this
    /// project handled it, which meant a learner could be dropped out of the game to the
    /// launcher at any moment, mid-story, and the run would be filed as abandoned. Two things
    /// make that a near-certainty rather than an edge case across 40 tablets and 10 sessions:
    ///
    ///   * on gesture navigation BACK is an EDGE SWIPE — the same motion the race asks the
    ///     learner to make to change lane, so the race actively trains the gesture that quits;
    ///   * on three-button navigation it is a permanent on-screen target sitting directly under
    ///     a portrait game, next to where a child's thumb already is.
    ///
    /// So BACK is consumed and does nothing. It deliberately does NOT act as an in-game "go
    /// back": every screen that should be leavable already has its own on-screen control with
    /// the right rules (the Reader's exit, for instance, is only offered before the learner's
    /// first answer, because after that the run is study data). Wiring a hardware button to
    /// those would re-open the exact dead-end and data-loss paths those rules exist to close.
    ///
    /// A supervising adult can still leave via the system app switcher; nothing here traps a
    /// device, only a stray thumb.
    /// </summary>
    public class BackButtonGuard : MonoBehaviour
    {
        private void Update()
        {
            // Android maps BACK to Escape under the Input System. Reading the control marks it
            // handled for this frame; there is no other consumer, so simply not acting on it is
            // what keeps the app open.
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (!keyboard.escapeKey.wasPressedThisFrame) return;

            // Nothing to do. Logged once per press only in the editor, so a developer wondering
            // why BACK "does nothing" finds the reason instead of assuming a broken build.
#if UNITY_EDITOR
            Debug.Log("BackButtonGuard: BACK swallowed — see the class comment for why.");
#endif
        }
    }
}
