using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DoNotDraw.Interaction
{
    public interface IPlayerInteractable
    {
        bool CanInteract { get; }
        string InteractionPrompt { get; }
        Vector3 InteractionPoint { get; }
        float InteractionPriority { get; }
        bool IsInteractionHighlighted { get; }
        void SetInteractionHighlighted(bool highlighted);
        void Interact(PlayerInteractionRouter router);
    }

    public abstract class PlayerInteractableBehaviour : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField] private InteractableOuterGlow outerGlow;

        private bool interactionHighlighted;

        public virtual bool CanInteract => isActiveAndEnabled;
        public abstract string InteractionPrompt { get; }
        public virtual Vector3 InteractionPoint => transform.position;
        public virtual float InteractionPriority => 0f;
        public bool IsInteractionHighlighted => interactionHighlighted;
        protected bool CanExecuteInteraction => CanInteract && interactionHighlighted;
        public abstract void Interact(PlayerInteractionRouter router);

        protected virtual void OnEnable()
        {
            interactionHighlighted = false;
            outerGlow ??= GetComponent<InteractableOuterGlow>();
            outerGlow?.SetVisible(false);
        }

        protected virtual void OnDisable()
        {
            SetInteractionHighlighted(false);
        }

        public void SetInteractionHighlighted(bool highlighted)
        {
            bool shouldHighlight = highlighted && CanInteract;
            outerGlow ??= GetComponent<InteractableOuterGlow>();
            if (outerGlow == null || !outerGlow.isActiveAndEnabled)
            {
                interactionHighlighted = false;
                return;
            }

            outerGlow.SetVisible(shouldHighlight);
            interactionHighlighted = shouldHighlight && outerGlow.IsVisible;
        }
    }

    [DisallowMultipleComponent]
    public sealed class PlayerInteractionRouter : MonoBehaviour
    {
        [SerializeField] private Transform viewTransform;
        [SerializeField, Min(0.5f)] private float maxDistance = 2.5f;
        [SerializeField, Min(0f)] private float inputCooldown = 0.12f;
        [SerializeField] private LayerMask interactionMask = ~0;

        [Header("Shared Prompt")]
        [SerializeField] private GameObject promptPanel;
        [SerializeField] private Text promptText;

        private IPlayerInteractable current;
        private float nextInputTime;

        public IPlayerInteractable Current => current;

        private void Awake()
        {
            if (viewTransform == null && Camera.main != null)
            {
                viewTransform = Camera.main.transform;
            }

            SetPromptVisible(false);
        }

        private void Update()
        {
            SetCurrent(SelectAimedInteractable());
            if (current == null)
            {
                SetPromptVisible(false);
                return;
            }

            if (promptText != null)
            {
                promptText.text = current.InteractionPrompt;
            }

            SetPromptVisible(true);
            if (current.CanInteract
                && current.IsInteractionHighlighted
                && Time.unscaledTime >= nextInputTime
                && WasInteractKeyPressed())
            {
                nextInputTime = Time.unscaledTime + inputCooldown;
                current.Interact(this);
                if (!current.CanInteract || !current.IsInteractionHighlighted)
                {
                    SetCurrent(null);
                }
            }
        }

        private IPlayerInteractable SelectAimedInteractable()
        {
            if (viewTransform == null)
            {
                return null;
            }

            if (!Physics.Raycast(
                    viewTransform.position,
                    viewTransform.forward,
                    out RaycastHit hit,
                    maxDistance,
                    interactionMask,
                    QueryTriggerInteraction.Ignore))
            {
                return null;
            }

            IPlayerInteractable best = null;
            float bestPriority = float.NegativeInfinity;

            foreach (PlayerInteractableBehaviour candidate in
                     hit.collider.GetComponentsInParent<PlayerInteractableBehaviour>(true))
            {
                if (candidate == null || !candidate.CanInteract)
                {
                    continue;
                }

                if (candidate.InteractionPriority > bestPriority)
                {
                    bestPriority = candidate.InteractionPriority;
                    best = candidate;
                }
            }

            return best;
        }

        private void SetCurrent(IPlayerInteractable next)
        {
            if (ReferenceEquals(current, next))
            {
                if (current != null && !current.IsInteractionHighlighted)
                {
                    current.SetInteractionHighlighted(true);
                    if (!current.IsInteractionHighlighted)
                    {
                        current = null;
                    }
                }

                return;
            }

            current?.SetInteractionHighlighted(false);
            current = next;
            if (current == null)
            {
                return;
            }

            current.SetInteractionHighlighted(true);
            if (!current.IsInteractionHighlighted)
            {
                current = null;
            }
        }

        private static bool WasInteractKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F);
#endif
        }

        private void SetPromptVisible(bool visible)
        {
            if (promptPanel != null && promptPanel.activeSelf != visible)
            {
                promptPanel.SetActive(visible);
            }
        }

        private void OnDisable()
        {
            SetCurrent(null);
            SetPromptVisible(false);
        }

        private void OnValidate()
        {
            maxDistance = Mathf.Max(0.5f, maxDistance);
            inputCooldown = Mathf.Max(0f, inputCooldown);
        }
    }
}
