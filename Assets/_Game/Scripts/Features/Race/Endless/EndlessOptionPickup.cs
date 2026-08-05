using UnityEngine;

namespace SummaRace.Features.Race.Endless
{
    /// <summary>
    /// Trigger on one answer card of a SWBST gate (or the FINISH card).
    /// Detects the Trash Dash character by its CharacterCollider component —
    /// no tag/layer assumptions about their prefabs.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class EndlessOptionPickup : MonoBehaviour
    {
        public int elementIndex;
        /// <summary>Monotonic id of the gate this card belongs to. The element index alone
        /// cannot identify a gate: a wrong pick re-presents the SAME element immediately, so
        /// a stale card and the fresh re-present share an index. The id never repeats.</summary>
        public int gateId;
        public bool isCorrect;
        public bool isFinishGate;

        /// <summary>Which option of the story JSON this card carries: 0 = <c>correct</c>,
        /// 1 = <c>distractors[0]</c>, 2 = <c>distractors[1]</c>. Captured at build time,
        /// before the lane shuffle scrambles the order, so the log can say WHICH wrong idea a
        /// learner held and not merely that they were wrong.</summary>
        public int optionIndex = -1;

        /// <summary>0 left, 1 centre, 2 right; -1 unknown.</summary>
        public int lane = -1;

        /// <summary>This card is the single gold re-presented answer, not one of three.</summary>
        public bool isRepresent;

        /// <summary>The exact text drawn on the card, kept so the log row reads on its own
        /// after the story pipeline regenerates the content.</summary>
        public string optionText;

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<CharacterCollider>() == null) return;
            if (EndlessRaceDirector.Instance != null)
                EndlessRaceDirector.Instance.OnPickupHit(this);
        }
    }
}
