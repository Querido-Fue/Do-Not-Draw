using System.Collections.Generic;
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
        void Interact(PlayerInteractionRouter router);
    }

    public abstract class PlayerInteractableBehaviour : MonoBehaviour, IPlayerInteractable
    {
        private static readonly HashSet<PlayerInteractableBehaviour> Registry =
            new HashSet<PlayerInteractableBehaviour>();

        public static IEnumerable<PlayerInteractableBehaviour> ActiveInteractables => Registry;

        public virtual bool CanInteract => isActiveAndEnabled;
        public abstract string InteractionPrompt { get; }
        public virtual Vector3 InteractionPoint => transform.position;
        public virtual float InteractionPriority => 0f;
        public abstract void Interact(PlayerInteractionRouter router);

        protected virtual void OnEnable()
        {
            Registry.Add(this);
        }

        protected virtual void OnDisable()
        {
            Registry.Remove(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            Registry.Clear();
        }
    }

    [DisallowMultipleComponent]
    public sealed class PlayerInteractionRouter : MonoBehaviour
    {
        [SerializeField] private Transform viewTransform;
        [SerializeField, Min(0.5f)] private float maxDistance = 2.5f;
        [SerializeField, Range(-1f, 1f)] private float minimumFacingDot = 0.45f;
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
            current = SelectBestInteractable();
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
            if (Time.unscaledTime >= nextInputTime && WasInteractKeyPressed())
            {
                nextInputTime = Time.unscaledTime + inputCooldown;
                current.Interact(this);
            }
        }

        private IPlayerInteractable SelectBestInteractable()
        {
            if (viewTransform == null)
            {
                return null;
            }

            IPlayerInteractable best = null;
            float bestScore = float.NegativeInfinity;
            Vector3 origin = viewTransform.position;
            Vector3 forward = viewTransform.forward;

            foreach (PlayerInteractableBehaviour candidate in PlayerInteractableBehaviour.ActiveInteractables)
            {
                if (candidate == null || !candidate.CanInteract)
                {
                    continue;
                }

                Vector3 offset = candidate.InteractionPoint - origin;
                float distance = offset.magnitude;
                if (distance <= 0.001f || distance > maxDistance)
                {
                    continue;
                }

                Vector3 direction = offset / distance;
                float facing = Vector3.Dot(forward, direction);
                if (facing < minimumFacingDot || IsOccluded(candidate, origin, direction, distance))
                {
                    continue;
                }

                float score = candidate.InteractionPriority * 10f
                    + facing * 2f
                    - distance / Mathf.Max(0.01f, maxDistance);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private bool IsOccluded(
            IPlayerInteractable candidate,
            Vector3 origin,
            Vector3 direction,
            float distance)
        {
            if (!Physics.Raycast(
                    origin,
                    direction,
                    out RaycastHit hit,
                    distance + 0.08f,
                    interactionMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            foreach (MonoBehaviour behaviour in hit.collider.GetComponentsInParent<MonoBehaviour>(true))
            {
                if (ReferenceEquals(behaviour, candidate))
                {
                    return false;
                }
            }

            return true;
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
            current = null;
            SetPromptVisible(false);
        }

        private void OnValidate()
        {
            maxDistance = Mathf.Max(0.5f, maxDistance);
            minimumFacingDot = Mathf.Clamp(minimumFacingDot, -1f, 1f);
            inputCooldown = Mathf.Max(0f, inputCooldown);
        }
    }
}
