using System;
using DoNotDraw.Narrative;
using UnityEngine;

namespace DoNotDraw.Interaction
{
    [DisallowMultipleComponent]
    public sealed class CardDeckInteraction : PlayerInteractableBehaviour
    {
        [Header("Interaction")]
        [SerializeField] private CardSequenceRunner runner;
        [SerializeField] private Transform interactionPoint;
        [SerializeField] private bool interactionEnabled = true;
        [SerializeField] private bool blockedDrawInteractionEnabled;

        [Header("Prompt")]
        [SerializeField] private string drawPrompt = "[F]  DRAW CARD";

        public override bool CanInteract => base.CanInteract
            && interactionEnabled
            && runner != null
            && (runner.CanPlayerDraw || blockedDrawInteractionEnabled);
        public override string InteractionPrompt => drawPrompt;
        public override Vector3 InteractionPoint => interactionPoint != null
            ? interactionPoint.position
            : transform.position;
        public override float InteractionPriority => 1f;

        public CardSequenceRunner Runner => runner;
        public bool IsBlockedDrawInteractionEnabled => blockedDrawInteractionEnabled;
        public event Action<CardDeckInteraction> BlockedDrawRequested;

        public override void Interact(PlayerInteractionRouter router)
        {
            if (!CanExecuteInteraction)
            {
                return;
            }

            if (runner != null && runner.CanPlayerDraw)
            {
                runner.RequestPlayerDraw();
                return;
            }

            if (blockedDrawInteractionEnabled)
            {
                BlockedDrawRequested?.Invoke(this);
            }
        }

        public void SetInteractionEnabled(bool enabled)
        {
            interactionEnabled = enabled;
        }

        public void SetBlockedDrawInteractionEnabled(bool enabled)
        {
            blockedDrawInteractionEnabled = enabled;
        }

        private void OnValidate()
        {
            runner ??= GetComponent<CardSequenceRunner>();
            drawPrompt ??= string.Empty;
        }
    }
}
