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

        [Header("Prompt")]
        [SerializeField] private string drawPrompt = "[F]  DRAW CARD";

        public override bool CanInteract => base.CanInteract
            && interactionEnabled
            && runner != null
            && runner.CanPlayerDraw;
        public override string InteractionPrompt => drawPrompt;
        public override Vector3 InteractionPoint => interactionPoint != null
            ? interactionPoint.position
            : transform.position;
        public override float InteractionPriority => 1f;

        public CardSequenceRunner Runner => runner;

        public override void Interact(PlayerInteractionRouter router)
        {
            if (!CanExecuteInteraction)
            {
                return;
            }

            runner?.RequestPlayerDraw();
        }

        public void SetInteractionEnabled(bool enabled)
        {
            interactionEnabled = enabled;
        }

        private void OnValidate()
        {
            runner ??= GetComponent<CardSequenceRunner>();
            drawPrompt ??= string.Empty;
        }
    }
}
