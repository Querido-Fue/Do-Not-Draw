using DoNotDraw.Narrative;
using UnityEngine;

namespace DoNotDraw.World
{
    [DisallowMultipleComponent]
    public sealed class OpeningDiscoveryReveal : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] private Transform playerRoot;
        [SerializeField] private Transform viewTransform;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform roomCenter;
        [SerializeField] private Transform switchTarget;

        [Header("Reveal Targets")]
        [SerializeField] private GameObject deckRoot;
        [SerializeField] private GameObject graffitiRoot;
        [SerializeField] private StorySignal graffitiRevealSignal;

        [Header("Switch-Side Trigger")]
        [Tooltip("Room-center local direction that defines the switch-side half of the room.")]
        [SerializeField] private Vector3 switchSideDirection = Vector3.back;
        [SerializeField, Range(1f, 360f)] private float viewArcDegrees = 120f;

        [Header("Hidden Spawn Guard")]
        [SerializeField] private Vector3 deckSpawnBoundsCenter = new Vector3(0f, 0.12f, 0f);
        [SerializeField] private Vector3 deckSpawnBoundsSize = new Vector3(1.05f, 0.6f, 1.3f);
        [SerializeField] private bool hideTargetsOnAwake = true;

        public bool HasRevealed => HasDeckRevealed;
        public bool HasDeckRevealed { get; private set; }
        public bool HasGraffitiRevealed { get; private set; }
        public bool IsPlayerOnSwitchSide => EvaluatePlayerSide();
        public float ViewAngleToSwitch => EvaluateViewAngle();
        public bool IsDeckSpawnAreaInView => EvaluateDeckSpawnAreaInView();

        private void Awake()
        {
            if (!HasRequiredReferences())
            {
                Debug.LogError(
                    "[OpeningDiscoveryReveal] Player, camera, room center, switch, deck, graffiti, and reveal signal references are required.",
                    this);
                enabled = false;
                return;
            }

            if (hideTargetsOnAwake)
            {
                SetDeckVisible(false);
                SetGraffitiVisible(false);
            }
        }

        private void OnEnable()
        {
            if (graffitiRevealSignal != null)
            {
                graffitiRevealSignal.Raised += HandleGraffitiRevealSignal;
            }
        }

        private void OnDisable()
        {
            if (graffitiRevealSignal != null)
            {
                graffitiRevealSignal.Raised -= HandleGraffitiRevealSignal;
            }
        }

        private void Update()
        {
            TryRevealDeck();
        }

        public bool TryReveal()
        {
            return TryRevealDeck();
        }

        public bool TryRevealDeck()
        {
            if (HasDeckRevealed
                || !HasRequiredReferences()
                || !playerRoot.gameObject.activeInHierarchy
                || !viewTransform.gameObject.activeInHierarchy
                || !EvaluatePlayerSide()
                || EvaluateViewAngle() > viewArcDegrees * 0.5f
                || EvaluateDeckSpawnAreaInView())
            {
                return false;
            }

            HasDeckRevealed = true;
            SetDeckVisible(true);
            DisableWhenComplete();
            return true;
        }

        private void HandleGraffitiRevealSignal(StorySignalContext context)
        {
            if (HasGraffitiRevealed)
            {
                return;
            }

            HasGraffitiRevealed = true;
            SetGraffitiVisible(true);
            DisableWhenComplete();
        }

        private bool EvaluatePlayerSide()
        {
            if (playerRoot == null || roomCenter == null)
            {
                return false;
            }

            Vector3 offsetFromCenter = Vector3.ProjectOnPlane(
                playerRoot.position - roomCenter.position,
                Vector3.up);
            return Vector3.Dot(offsetFromCenter, GetHorizontalSwitchSideDirection()) >= 0f;
        }

        private float EvaluateViewAngle()
        {
            if (viewTransform == null || switchTarget == null)
            {
                return 180f;
            }

            Vector3 horizontalForward = Vector3.ProjectOnPlane(viewTransform.forward, Vector3.up);
            Vector3 directionToSwitch = Vector3.ProjectOnPlane(
                switchTarget.position - viewTransform.position,
                Vector3.up);
            if (horizontalForward.sqrMagnitude <= 0.0001f
                || directionToSwitch.sqrMagnitude <= 0.0001f)
            {
                return 180f;
            }

            return Vector3.Angle(horizontalForward.normalized, directionToSwitch.normalized);
        }

        private bool EvaluateDeckSpawnAreaInView()
        {
            if (playerCamera == null || deckRoot == null)
            {
                return true;
            }

            Transform deckTransform = deckRoot.transform;
            Vector3 scale = deckTransform.lossyScale;
            Vector3 worldSize = new Vector3(
                Mathf.Abs(deckSpawnBoundsSize.x * scale.x),
                Mathf.Abs(deckSpawnBoundsSize.y * scale.y),
                Mathf.Abs(deckSpawnBoundsSize.z * scale.z));
            Bounds spawnBounds = new Bounds(
                deckTransform.TransformPoint(deckSpawnBoundsCenter),
                worldSize);
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(playerCamera);
            return GeometryUtility.TestPlanesAABB(frustumPlanes, spawnBounds);
        }

        private Vector3 GetHorizontalSwitchSideDirection()
        {
            Vector3 worldDirection = roomCenter != null
                ? roomCenter.TransformDirection(switchSideDirection)
                : switchSideDirection;
            Vector3 horizontalDirection = Vector3.ProjectOnPlane(worldDirection, Vector3.up);
            return horizontalDirection.sqrMagnitude > 0.0001f
                ? horizontalDirection.normalized
                : Vector3.back;
        }

        private bool HasRequiredReferences()
        {
            return playerRoot != null
                && viewTransform != null
                && playerCamera != null
                && roomCenter != null
                && switchTarget != null
                && deckRoot != null
                && graffitiRoot != null
                && graffitiRevealSignal != null;
        }

        private void SetDeckVisible(bool visible)
        {
            if (deckRoot != null)
            {
                deckRoot.SetActive(visible);
            }
        }

        private void SetGraffitiVisible(bool visible)
        {
            if (graffitiRoot != null)
            {
                graffitiRoot.SetActive(visible);
            }
        }

        private void DisableWhenComplete()
        {
            if (HasDeckRevealed && HasGraffitiRevealed)
            {
                enabled = false;
            }
        }

        private void OnValidate()
        {
            viewArcDegrees = Mathf.Clamp(viewArcDegrees, 1f, 360f);
            deckSpawnBoundsSize = new Vector3(
                Mathf.Max(0.01f, deckSpawnBoundsSize.x),
                Mathf.Max(0.01f, deckSpawnBoundsSize.y),
                Mathf.Max(0.01f, deckSpawnBoundsSize.z));
            if (Vector3.ProjectOnPlane(switchSideDirection, Vector3.up).sqrMagnitude <= 0.0001f)
            {
                switchSideDirection = Vector3.back;
            }
        }
    }
}
