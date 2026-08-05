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

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<CharacterCollider>() == null) return;
            if (EndlessRaceDirector.Instance != null)
                EndlessRaceDirector.Instance.OnPickupHit(this);
        }
    }
}
