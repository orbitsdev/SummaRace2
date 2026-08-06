using UnityEngine;
using UnityEngine.InputSystem;

namespace SummaRace.Features.Race.Endless
{
    /// <summary>
    /// TAP A THIRD OF THE SCREEN TO GO TO THAT LANE.
    ///
    /// Trash Dash's own touch path (CharacterInputController.Update) implements SWIPE ONLY, and
    /// swiping is the wrong ask for this game's player:
    ///   * one swipe = one lane, so reaching the far lane needs two separate swipes, and the
    ///     legibility work upstream buys reading time, not dexterity time;
    ///   * their axis test sends any gesture with |dy| &gt; |dx| to Jump, so a hurried diagonal
    ///     flick — the most likely gesture a nine-year-old under time pressure produces — makes
    ///     the runner jump instead of changing lane;
    ///   * a swipe is simply a higher-dexterity input than a tap, and the learners this study is
    ///     about are the ones least well served by it.
    /// The legacy race (PlayerRunner) already accepted taps, so this is precedent, not novelty.
    ///
    /// It cannot fight their swipe handler. A gesture counts as a tap here only if the finger
    /// stayed inside GameRules.RaceTapMaxDrag (2% of the screen width) and lifted within
    /// RaceTapMaxSeconds; their swipe fires at 1% of the screen width, so any gesture large
    /// enough for them is already too large for us and the two can never both claim it. That
    /// matters: CLAUDE.md records double-binding an input once making lane changes skip two
    /// lanes at a time.
    ///
    /// It is inert whenever the world is not running — the briefing, the 3-2-1 countdown, the
    /// pause screen and the finish all leave TrackManager.isMoving false — and it ignores taps
    /// that land on the race HUD's own controls.
    /// </summary>
    public class EndlessTouchInput : MonoBehaviour
    {
        /// <summary>Their private lane counter. Read rather than inferred from the character's
        /// x: a tap during a lane change would otherwise read a half-way position and issue the
        /// wrong number of steps. Reflection because their script stays untouched on this
        /// branch; the geometric estimate below is the fallback if the field is ever renamed.</summary>
        private static readonly System.Reflection.FieldInfo CurrentLaneField =
            typeof(CharacterInputController).GetField("m_CurrentLane",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        private CharacterInputController _runner;
        private bool _tracking;
        private Vector2 _startPos;
        private float _startTime;

        /// <summary>Screen rects (in pixels) that own their own taps — the pause chip. A tap
        /// there must not also move the runner.</summary>
        private RectTransform[] _blockers;

        public void SetBlockers(params RectTransform[] blockers) => _blockers = blockers;

        private void Update()
        {
            var track = TrackManager.instance;
            // isMoving is false through the briefing, the countdown, our pause and the finish,
            // so this one test covers every state in which input must do nothing.
            if (track == null || !track.isMoving) { _tracking = false; return; }

            Vector2 pos;
            bool down, up;
            if (!ReadPointer(out pos, out down, out up)) { _tracking = false; return; }

            if (down)
            {
                _tracking = true;
                _startPos = pos;
                _startTime = Time.unscaledTime;
                return;
            }

            if (!up || !_tracking) return;
            _tracking = false;

            float dragFraction = Screen.width > 0
                ? (pos - _startPos).magnitude / Screen.width
                : 1f;
            if (dragFraction > SummaRace.Constants.GameRules.RaceTapMaxDrag) return;   // theirs
            if (Time.unscaledTime - _startTime > SummaRace.Constants.GameRules.RaceTapMaxSeconds) return;
            if (IsOverBlocker(pos)) return;

            if (_runner == null) _runner = track.characterController;
            if (_runner == null) return;

            int targetLane = Screen.width > 0
                ? Mathf.Clamp(Mathf.FloorToInt(pos.x / (Screen.width / 3f)), 0, 2)
                : 1;
            MoveToLane(targetLane, track);
        }

        /// <summary>Touch first, mouse second — the mouse path is what makes this testable in
        /// the Editor, where Trash Dash's own touch branch is compiled out entirely.</summary>
        private static bool ReadPointer(out Vector2 pos, out bool down, out bool up)
        {
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed
                || touch != null && touch.primaryTouch.press.wasReleasedThisFrame)
            {
                pos = touch.primaryTouch.position.ReadValue();
                down = touch.primaryTouch.press.wasPressedThisFrame;
                up = touch.primaryTouch.press.wasReleasedThisFrame;
                return true;
            }

            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.leftButton.wasReleasedThisFrame))
            {
                pos = mouse.position.ReadValue();
                down = mouse.leftButton.wasPressedThisFrame;
                up = mouse.leftButton.wasReleasedThisFrame;
                return true;
            }

            pos = default(Vector2); down = false; up = false;
            return false;
        }

        private bool IsOverBlocker(Vector2 screenPos)
        {
            if (_blockers == null) return false;
            for (int i = 0; i < _blockers.Length; i++)
            {
                var rt = _blockers[i];
                if (rt == null || !rt.gameObject.activeInHierarchy) continue;
                // Overlay canvas => null camera is correct here.
                if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null)) return true;
            }
            return false;
        }

        /// <summary>
        /// THE ONE ENTRY POINT FOR "PUT THE RUNNER IN LANE n", for callers that already know the
        /// lane — today the three columns of the reading panel, which are real tap targets so the
        /// learner can choose the answer they are reading instead of translating it into a lane
        /// and then finding that lane on the road (EndlessRaceDirector.OnPreviewColumnTapped).
        ///
        /// It deliberately shares MoveToLane with the road tap rather than calling ChangeLane
        /// itself: one code path decides what a lane request means, so the reflection read of
        /// their private lane counter, its geometric fallback and the two-step clamp cannot drift
        /// between the two surfaces.
        ///
        /// It does NOT record a pick and must never learn how to. A pick is still made only by
        /// driving into an answer card's trigger, so racePicks / raceFirstOutcome reach the log by
        /// exactly the route they always have — this changes how the learner steers, not what is
        /// measured.
        ///
        /// The isMoving test is the same one Update uses, and is false through the briefing, the
        /// 3-2-1, our pause and the finish.
        /// </summary>
        public void SelectLane(int lane)
        {
            var track = TrackManager.instance;
            if (track == null || !track.isMoving) return;
            if (_runner == null) _runner = track.characterController;
            if (_runner == null) return;
            MoveToLane(Mathf.Clamp(lane, 0, 2), track);
        }

        /// <summary>
        /// Steps their relative ChangeLane until the runner is in the tapped lane. Their own
        /// method clamps at the outer lanes, so an over-count can only ever be a no-op — which
        /// is what makes the reflection fallback safe.
        /// </summary>
        private void MoveToLane(int targetLane, TrackManager track)
        {
            int current = 1;
            if (CurrentLaneField != null)
            {
                object value = CurrentLaneField.GetValue(_runner);
                if (value is int) current = (int)value;
            }
            else if (_runner.characterCollider != null && track.laneOffset > 0.01f)
            {
                current = Mathf.Clamp(
                    Mathf.RoundToInt(_runner.characterCollider.transform.localPosition.x / track.laneOffset) + 1,
                    0, 2);
            }

            int step = targetLane > current ? 1 : -1;
            for (int i = 0; i < Mathf.Abs(targetLane - current) && i < 2; i++)
                _runner.ChangeLane(step);
        }
    }
}
