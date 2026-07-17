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
