using UnityEngine;

namespace SummaRace.Core
{
    /// <summary>
    /// Stops the Android hardware/gesture BACK closing the app mid-story.
    ///
    /// Unity's default for BACK is to finish the activity — quit. Nothing handled it, which meant
    /// a learner could be dropped to the launcher at any moment and the run filed as abandoned.
    /// Two things make that a weekly event across 40 tablets rather than an edge case: on gesture
    /// navigation BACK is an EDGE SWIPE, the same motion the race asks for, so the race actively
    /// trains the gesture that quits it; and on three-button navigation it is a permanent target
    /// under a portrait game, next to where a child's thumb already sits.
    ///
    /// HOW, and why the obvious version does not work. The first version of this class polled
    /// <c>Keyboard.current.escapeKey</c> (which is where the Input System surfaces Android BACK)
    /// and did nothing with it, on the belief that reading a control marks it handled. **It does
    /// not.** Reading an input control has no consuming semantics, and the quit is performed by
    /// the platform, not by the input stack — so that version compiled, ran, logged, and stopped
    /// nothing. It was caught in review, not by testing, which is exactly how a no-op that looks
    /// like a fix survives.
    ///
    /// <see cref="Application.wantsToQuit"/> is the documented hook: it fires before the app
    /// closes and returning false cancels it. It covers BACK regardless of navigation mode and
    /// regardless of which input backend is active — which matters here, because this branch runs
    /// `activeInputHandler: 2` (Both) and the race deliberately relies on the legacy touch path.
    ///
    /// It deliberately does NOT act as an in-game "go back". Every screen that should be leavable
    /// has its own on-screen control with the right rules — the Reader's exit, for one, is offered
    /// only before the learner's first answer, because after that the run is study data. Wiring a
    /// hardware button to those would reopen the dead-end and data-loss paths those rules close.
    /// A supervising adult can still leave via the system app switcher; nothing here traps a
    /// device, only a stray thumb.
    ///
    /// NOTE: `Application.wantsToQuit` is not raised in the editor on Play-mode exit, so this can
    /// only be confirmed on a device. Verify it in the first on-tablet smoke test.
    /// </summary>
    public class BackButtonGuard : MonoBehaviour
    {
        private void OnEnable() => Application.wantsToQuit += RefuseQuit;
        private void OnDisable() => Application.wantsToQuit -= RefuseQuit;

        /// <summary>Always refuses. There is no state in which a learner tapping BACK should end
        /// the session — a teacher who genuinely wants the app closed uses the app switcher.</summary>
        private static bool RefuseQuit()
        {
#if UNITY_EDITOR
            Debug.Log("BackButtonGuard: quit refused — see the class comment for why.");
#endif
            return false;
        }
    }
}
