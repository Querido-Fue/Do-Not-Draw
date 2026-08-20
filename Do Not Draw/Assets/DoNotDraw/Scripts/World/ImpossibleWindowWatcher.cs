using UnityEngine;

namespace DoNotDraw.World
{
    [DisallowMultipleComponent]
    public sealed class ImpossibleWindowWatcher : MonoBehaviour
    {
        private static readonly int ApparitionAlphaId = Shader.PropertyToID("_ApparitionAlpha");

        [Header("References")]
        [SerializeField] private Camera viewer;
        [SerializeField] private Transform gazeTarget;
        [SerializeField] private Renderer apparitionRenderer;

        [Header("Gaze")]
        [SerializeField, Min(0.5f)] private float maximumDistance = 8f;
        [SerializeField, Range(0.5f, 0.999f)] private float gazeThreshold = 0.955f;
        [SerializeField, Min(0f)] private float lookAwayGracePeriod = 0.45f;

        [Header("Appearance")]
        [SerializeField, Range(0f, 1f)] private float maximumAlpha = 0.55f;
        [SerializeField, Min(0.1f)] private float fadeInDuration = 2.6f;
        [SerializeField, Min(0f)] private float holdDuration = 1.15f;
        [SerializeField, Min(0.1f)] private float fadeOutDuration = 3.1f;
        [SerializeField, Min(0f)] private float minimumCooldown = 4.5f;
        [SerializeField, Min(0f)] private float maximumCooldown = 8.5f;
        [SerializeField] private Vector2 horizontalOffsetRange = new Vector2(-0.1f, 0.1f);
        [SerializeField] private Vector2 verticalOffsetRange = new Vector2(-0.08f, 0.08f);

        private MaterialPropertyBlock propertyBlock;
        private AppearanceState state;
        private Vector3 baseLocalPosition;
        private Vector3 baseLocalScale;
        private Vector3 cycleOffset;
        private float currentAlpha;
        private float fadeStartAlpha;
        private float phase;
        private float stateElapsed;
        private float lookAwayElapsed;
        private float nextAppearanceTime;

        private enum AppearanceState
        {
            Hidden,
            FadingIn,
            Holding,
            FadingOut
        }

        private void Awake()
        {
            EnsurePropertyBlock();
            CacheReferences();
            CachePresentationDefaults();
        }

        private void OnEnable()
        {
            EnsurePropertyBlock();
            CacheReferences();
            CachePresentationDefaults();
            phase = Random.Range(0f, Mathf.PI * 2f);
            HideImmediately();
            ScheduleNextAppearance(1.5f, 3.5f);
        }

        private void Update()
        {
            if (viewer == null || apparitionRenderer == null)
            {
                CacheReferences();
                if (viewer == null || apparitionRenderer == null)
                {
                    return;
                }
            }

            float deltaTime = Time.deltaTime;
            bool viewerIsLooking = IsViewerLooking();

            switch (state)
            {
                case AppearanceState.Hidden:
                    if (viewerIsLooking && Time.time >= nextAppearanceTime)
                    {
                        BeginFadeIn();
                    }

                    break;

                case AppearanceState.FadingIn:
                    TrackLookAway(viewerIsLooking, deltaTime);
                    stateElapsed += deltaTime;
                    SetAlpha(SmoothStep01(stateElapsed / fadeInDuration));
                    if (lookAwayElapsed >= lookAwayGracePeriod)
                    {
                        BeginFadeOut();
                    }
                    else if (stateElapsed >= fadeInDuration)
                    {
                        state = AppearanceState.Holding;
                        stateElapsed = 0f;
                        SetAlpha(1f);
                    }

                    break;

                case AppearanceState.Holding:
                    TrackLookAway(viewerIsLooking, deltaTime);
                    stateElapsed += deltaTime;
                    if (lookAwayElapsed >= lookAwayGracePeriod || stateElapsed >= holdDuration)
                    {
                        BeginFadeOut();
                    }

                    break;

                case AppearanceState.FadingOut:
                    stateElapsed += deltaTime;
                    float fadeProgress = SmoothStep01(stateElapsed / fadeOutDuration);
                    SetAlpha(Mathf.Lerp(fadeStartAlpha, 0f, fadeProgress));
                    if (stateElapsed >= fadeOutDuration)
                    {
                        HideImmediately();
                        ScheduleNextAppearance(minimumCooldown, maximumCooldown);
                    }

                    break;
            }

            UpdateSubtleMotion();
        }

        private void CacheReferences()
        {
            if (viewer == null)
            {
                viewer = Camera.main;
            }
        }

        private void CachePresentationDefaults()
        {
            if (apparitionRenderer == null)
            {
                return;
            }

            Transform apparitionTransform = apparitionRenderer.transform;
            baseLocalPosition = apparitionTransform.localPosition;
            baseLocalScale = apparitionTransform.localScale;
        }

        private bool IsViewerLooking()
        {
            Vector3 targetPosition = gazeTarget != null
                ? gazeTarget.position
                : apparitionRenderer.bounds.center;
            Vector3 toTarget = targetPosition - viewer.transform.position;
            float distance = toTarget.magnitude;
            if (distance <= 0.001f || distance > maximumDistance)
            {
                return false;
            }

            float gazeDot = Vector3.Dot(viewer.transform.forward, toTarget / distance);
            if (gazeDot < gazeThreshold)
            {
                return false;
            }

            Vector3 viewportPoint = viewer.WorldToViewportPoint(targetPosition);
            return viewportPoint.z > 0f
                && viewportPoint.x >= 0.06f
                && viewportPoint.x <= 0.94f
                && viewportPoint.y >= 0.06f
                && viewportPoint.y <= 0.94f;
        }

        private void BeginFadeIn()
        {
            state = AppearanceState.FadingIn;
            stateElapsed = 0f;
            lookAwayElapsed = 0f;
            cycleOffset = new Vector3(
                Random.Range(horizontalOffsetRange.x, horizontalOffsetRange.y),
                Random.Range(verticalOffsetRange.x, verticalOffsetRange.y),
                0f);
            apparitionRenderer.enabled = true;
            SetAlpha(0f);
        }

        private void BeginFadeOut()
        {
            if (state == AppearanceState.FadingOut || state == AppearanceState.Hidden)
            {
                return;
            }

            state = AppearanceState.FadingOut;
            stateElapsed = 0f;
            fadeStartAlpha = currentAlpha;
        }

        private void TrackLookAway(bool viewerIsLooking, float deltaTime)
        {
            lookAwayElapsed = viewerIsLooking
                ? 0f
                : lookAwayElapsed + deltaTime;
        }

        private void UpdateSubtleMotion()
        {
            if (apparitionRenderer == null)
            {
                return;
            }

            Transform apparitionTransform = apparitionRenderer.transform;
            float breath = Mathf.Sin(Time.time * 0.38f + phase) * 0.006f;
            float revealScale = Mathf.Lerp(0.965f, 1f, currentAlpha) + breath;
            apparitionTransform.localPosition = baseLocalPosition + cycleOffset * currentAlpha;
            apparitionTransform.localScale = baseLocalScale * revealScale;
        }

        private void SetAlpha(float alpha)
        {
            EnsurePropertyBlock();
            currentAlpha = Mathf.Clamp01(alpha);
            apparitionRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(ApparitionAlphaId, currentAlpha * maximumAlpha);
            apparitionRenderer.SetPropertyBlock(propertyBlock);
        }

        private void EnsurePropertyBlock()
        {
            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }
        }

        private void HideImmediately()
        {
            state = AppearanceState.Hidden;
            stateElapsed = 0f;
            lookAwayElapsed = 0f;
            cycleOffset = Vector3.zero;
            if (apparitionRenderer == null)
            {
                return;
            }

            SetAlpha(0f);
            apparitionRenderer.enabled = false;
            apparitionRenderer.transform.localPosition = baseLocalPosition;
            apparitionRenderer.transform.localScale = baseLocalScale;
        }

        private void ScheduleNextAppearance(float minimumDelay, float maximumDelay)
        {
            float lowerBound = Mathf.Max(0f, Mathf.Min(minimumDelay, maximumDelay));
            float upperBound = Mathf.Max(lowerBound, Mathf.Max(minimumDelay, maximumDelay));
            nextAppearanceTime = Time.time + Random.Range(lowerBound, upperBound);
        }

        private static float SmoothStep01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private void OnDisable()
        {
            HideImmediately();
        }

        private void OnValidate()
        {
            maximumDistance = Mathf.Max(0.5f, maximumDistance);
            maximumAlpha = Mathf.Clamp01(maximumAlpha);
            fadeInDuration = Mathf.Max(0.1f, fadeInDuration);
            holdDuration = Mathf.Max(0f, holdDuration);
            fadeOutDuration = Mathf.Max(0.1f, fadeOutDuration);
            lookAwayGracePeriod = Mathf.Max(0f, lookAwayGracePeriod);
            minimumCooldown = Mathf.Max(0f, minimumCooldown);
            maximumCooldown = Mathf.Max(minimumCooldown, maximumCooldown);

            if (horizontalOffsetRange.x > horizontalOffsetRange.y)
            {
                horizontalOffsetRange = new Vector2(
                    horizontalOffsetRange.y,
                    horizontalOffsetRange.x);
            }

            if (verticalOffsetRange.x > verticalOffsetRange.y)
            {
                verticalOffsetRange = new Vector2(
                    verticalOffsetRange.y,
                    verticalOffsetRange.x);
            }
        }
    }
}
