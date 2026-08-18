using System;
using UnityEngine;

namespace DoNotDraw.World
{
    public enum NarrativeZoneId
    {
        SecondRoom,
        ReturnedToFirstRoom,
        EndingCorridor
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class NarrativeZoneTrigger : MonoBehaviour
    {
        [SerializeField] private NarrativeZoneId zoneId;
        [SerializeField] private Transform playerRoot;
        [SerializeField] private bool triggerEnabled = true;
        [SerializeField] private bool triggerOnce;

        private bool consumed;

        public event Action<NarrativeZoneId> PlayerEntered;
        public NarrativeZoneId ZoneId => zoneId;

        public void SetTriggerEnabled(bool enabled)
        {
            triggerEnabled = enabled;
        }

        public void ResetTrigger()
        {
            consumed = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!triggerEnabled || consumed || !BelongsToPlayer(other))
            {
                return;
            }

            if (triggerOnce)
            {
                consumed = true;
            }

            PlayerEntered?.Invoke(zoneId);
        }

        private bool BelongsToPlayer(Collider other)
        {
            if (other == null)
            {
                return false;
            }

            if (playerRoot != null)
            {
                return other.transform.root == playerRoot.root;
            }

            return other.CompareTag("Player") || other.GetComponentInParent<CharacterController>() != null;
        }

        private void OnValidate()
        {
            Collider trigger = GetComponent<Collider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }
        }
    }
}
