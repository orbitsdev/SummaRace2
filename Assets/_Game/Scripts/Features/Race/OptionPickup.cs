using UnityEngine;

namespace SummaRace.Features.Race
{
    /// <summary>
    /// A collectible answer cube on the track (TDD §11.4).
    /// Reports trigger hits with the player back to the RaceController.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class OptionPickup : MonoBehaviour
    {
        public int elementIndex;
        public bool isCorrect;
        public bool isFinishGate;
        public GameObject padFx; // glow ring under this card — dies with it on a wrong pick

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (RaceController.Instance != null)
                RaceController.Instance.OnPickupHit(this);
        }
    }
}
