using UnityEngine;

namespace DoNotDraw.World
{
    [DisallowMultipleComponent]
    public sealed class ImpossibleWindowWatcher : MonoBehaviour
    {
        private static readonly int ApparitionAlphaId = Shader.PropertyToID("_ApparitionAlpha");

        [Header("References")]
        [SerializeField] private Camera viewer;
        [SerializeField] private Renderer apparitionRenderer;

        [Header("Faint Presence")]
        [SerializeField, Range(0f, 1f)] private float faintAlpha = 0.14f;
        [SerializeField, Min(0.01f)] private float faintEntryFadeDuration = 1.4f;

        [Header("Scripted Scare")]
        [SerializeField, Min(0f)] private float scriptedLungeDistance = 0.38f;
        [SerializeField, Min(0.01f)] private float scriptedLungeDuration = 0.12f;
        [SerializeField, Min(0f)] private float scriptedHoldDuration = 0.24f;
        [SerializeField, Min(0.01f)] private float scriptedFadeDuration = 0.46f;
        [SerializeField, Range(1f, 1.4f)] private float scriptedScaleMultiplier = 1.1f;

        private MaterialPropertyBlock propertyBlock;
        private AppearanceState state;
        private Vector3 baseLocalPosition;
        private Vector3 baseLocalScale;
        private float currentAlpha;
        private float fadeStartAlpha;
        private float stateElapsed;
        private Vector3 scriptedLocalOffset;

        private enum AppearanceState
        {
            Hidden,
            FaintPresence,
            FadingFaintPresence,
            ScriptedScare
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
            HideImmediately();
        }

        private void Update()
        {
            switch (state)
            {
                case AppearanceState.FadingFaintPresence:
                    UpdateFaintPresenceFade(Time.unscaledDeltaTime);
                    break;

                case AppearanceState.ScriptedScare:
                    UpdateScriptedScare(Time.unscaledDeltaTime);
                    break;
            }
        }

        public void ShowFaintUntilDismissed()
        {
            EnsurePropertyBlock();
            if (apparitionRenderer == null || state == AppearanceState.ScriptedScare)
            {
                return;
            }

            state = AppearanceState.FaintPresence;
            stateElapsed = 0f;
            scriptedLocalOffset = Vector3.zero;
            apparitionRenderer.enabled = true;
            apparitionRenderer.transform.localPosition = baseLocalPosition;
            apparitionRenderer.transform.localScale = baseLocalScale;
            SetAlpha(faintAlpha);
        }

        public void FadeOutFaintPresence()
        {
            if (state != AppearanceState.FaintPresence
                && state != AppearanceState.FadingFaintPresence)
            {
                return;
            }

            state = AppearanceState.FadingFaintPresence;
            stateElapsed = 0f;
            fadeStartAlpha = currentAlpha;
        }

        public void TriggerScriptedScare()
        {
            EnsurePropertyBlock();
            CacheReferences();
            if (apparitionRenderer == null)
            {
                return;
            }

            Transform apparitionTransform = apparitionRenderer.transform;
            Vector3 towardViewer = viewer != null
                ? viewer.transform.position - apparitionTransform.position
                : -apparitionTransform.forward;
            if (towardViewer.sqrMagnitude < 0.001f)
            {
                towardViewer = -apparitionTransform.forward;
            }

            Vector3 worldOffset = towardViewer.normalized * scriptedLungeDistance;
            scriptedLocalOffset = apparitionTransform.parent != null
                ? apparitionTransform.parent.InverseTransformVector(worldOffset)
                : worldOffset;
            state = AppearanceState.ScriptedScare;
            stateElapsed = 0f;
            apparitionRenderer.enabled = true;
            apparitionTransform.localPosition = baseLocalPosition;
            apparitionTransform.localScale = baseLocalScale;
            SetAlpha(1f);
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

        private void UpdateFaintPresenceFade(float deltaTime)
        {
            if (apparitionRenderer == null)
            {
                HideImmediately();
                return;
            }

            stateElapsed += deltaTime;
            float fadeProgress = SmoothStep01(stateElapsed / faintEntryFadeDuration);
            SetAlpha(Mathf.Lerp(fadeStartAlpha, 0f, fadeProgress));
            if (stateElapsed >= faintEntryFadeDuration)
            {
                HideImmediately();
            }
        }

        private void UpdateScriptedScare(float deltaTime)
        {
            if (apparitionRenderer == null)
            {
                HideImmediately();
                return;
            }

            stateElapsed += deltaTime;
            float lungeProgress = SmoothStep01(stateElapsed / scriptedLungeDuration);
            Transform apparitionTransform = apparitionRenderer.transform;
            apparitionTransform.localPosition = baseLocalPosition + scriptedLocalOffset * lungeProgress;
            apparitionTransform.localScale = baseLocalScale
                * Mathf.Lerp(1f, scriptedScaleMultiplier, lungeProgress);

            float fadeStart = scriptedLungeDuration + scriptedHoldDuration;
            float fadeProgress = stateElapsed <= fadeStart
                ? 0f
                : SmoothStep01((stateElapsed - fadeStart) / scriptedFadeDuration);
            SetAlpha(1f - fadeProgress);
            if (stateElapsed >= fadeStart + scriptedFadeDuration)
            {
                HideImmediately();
            }
        }

        private void SetAlpha(float alpha)
        {
            EnsurePropertyBlock();
            if (apparitionRenderer == null)
            {
                return;
            }

            apparitionRenderer.GetPropertyBlock(propertyBlock);
            currentAlpha = Mathf.Clamp01(alpha);
            propertyBlock.SetFloat(ApparitionAlphaId, currentAlpha);
            apparitionRenderer.SetPropertyBlock(propertyBlock);
        }

        private void EnsurePropertyBlock()
        {
            propertyBlock ??= new MaterialPropertyBlock();
        }

        private void HideImmediately()
        {
            state = AppearanceState.Hidden;
            stateElapsed = 0f;
            fadeStartAlpha = 0f;
            scriptedLocalOffset = Vector3.zero;
            if (apparitionRenderer == null)
            {
                return;
            }

            SetAlpha(0f);
            apparitionRenderer.enabled = false;
            apparitionRenderer.transform.localPosition = baseLocalPosition;
            apparitionRenderer.transform.localScale = baseLocalScale;
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
            faintAlpha = Mathf.Clamp01(faintAlpha);
            faintEntryFadeDuration = Mathf.Max(0.01f, faintEntryFadeDuration);
            scriptedLungeDistance = Mathf.Max(0f, scriptedLungeDistance);
            scriptedLungeDuration = Mathf.Max(0.01f, scriptedLungeDuration);
            scriptedHoldDuration = Mathf.Max(0f, scriptedHoldDuration);
            scriptedFadeDuration = Mathf.Max(0.01f, scriptedFadeDuration);
            scriptedScaleMultiplier = Mathf.Clamp(scriptedScaleMultiplier, 1f, 1.4f);
        }
    }
}
