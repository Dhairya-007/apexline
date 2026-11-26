using UnityEngine;
using Apexline.Vehicle;

namespace Apexline.Race
{
    /// <summary>
    /// A trigger plane across the track. Index 0 is the start/finish line;
    /// the rest exist so a lap only counts when the full track was driven.
    /// Put these on a layer that only collides with cars.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class RaceSector : MonoBehaviour
    {
        public RaceManager race;
        [Tooltip("0 = start/finish line. Others increase in lap order.")]
        public int sectorIndex;

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (race == null) race = FindFirstObjectByType<RaceManager>();
        }

        void OnTriggerEnter(Collider other)
        {
            var car = other.GetComponentInParent<CarController>();
            if (car != null) race?.OnSectorEntered(sectorIndex, car);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = sectorIndex == 0
                ? new Color(1f, 0.9f, 0.2f, 0.35f)
                : new Color(0.2f, 1f, 0.5f, 0.2f);

            var box = GetComponent<BoxCollider>();
            if (box == null) return;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
#endif
    }
}
