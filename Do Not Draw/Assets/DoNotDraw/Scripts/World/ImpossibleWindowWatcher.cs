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

        [Header("Faint Glimpse")]
        [SerializeField, Range(0f, 1f)] private float faintAlpha = 0.14f;
        [SerializeField, Min(0.01f)] private float faintFadeInDuration = 0.04f;
        [SerializeField, Min(0f)] private float faintHoldDuration = 0.05f;
        [SerializeField, Min(0.01f)] private float faintFadeOutDuration = 0.13f;

        [Header("Scripted Scare")]
        [SerializeField, Min(0f)] private float scriptedLungeDistance = 0.24f;
        [SerializeField, Min(0.01f)] private float scriptedLungeDuration = 0.12f;
        [SerializeField, Min(0f)] private float scriptedHoldDuration = 0.24f;
        [SerializeField, Min(0.01f)] private float scriptedFadeDuration = 0.46f;
        [SerializeField, Range(1f, 1.4f)] private float scriptedScaleMultiplier = 1.1f;

        private MaterialPropertyBlock propertyBlock;
        private AppearanceState state;
        private Vector3 baseLocalPosition;
        private Vector3 baseLocalScale;
        private float stateElapsed;
        private Vector3 scriptedLocalOffset;

        private enum AppearanceState
        {
            Hidden,
            FaintGlimpse,
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
                case AppearanceState.FaintGlimpse:
                    UpdateFaintGlimpse(Time.unscaledDeltaTime);
                    break;

                case AppearanceState.ScriptedScare:
                    UpdateScriptedScare(Time.unscaledDeltaTime);
                    break;
            }
        }

        public void TriggerFaintGlimpse()
        {
            EnsurePropertyBlock();
            if (apparitionRenderer == null || state == AppearanceState.ScriptedScare)
            {
                return;
            }

            state = AppearanceState.FaintGlimpse;
            stateElapsed = 0f;
            scriptedLocalOffset = Vector3.zero;
            apparitionRenderer.enabled = true;
            apparitionRenderer.transform.localPosition = baseLocalPosition;
            apparitionRenderer.transform.localScale = baseLocalScale;
            SetAlpha(0f);
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

        private void UpdateFaintGlimpse(float deltaTime)
        {
            if (apparitionRenderer == null)
            {
                HideImmediately();
                return;
            }

            stateElapsed += deltaTime;
            float fadeInEndsAt = faintFadeInDuration;
            float holdEndsAt = fadeInEndsAt + faintHoldDuration;
            float glimpseEndsAt = holdEndsAt + faintFadeOutDuration;
            float alpha;
            if (stateElapsed < fadeInEndsAt)
            {
                alpha = faintAlpha * SmoothStep01(stateElapsed / faintFadeInDuration);
            }
            else if (stateElapsed < holdEndsAt)
            {
                alpha = faintAlpha;
            }
            else
            {
                float fadeProgress = SmoothStep01(
                    (stateElapsed - holdEndsAt) / faintFadeOutDuration);
                alpha = faintAlpha * (1f - fadeProgress);
            }

            SetAlpha(alpha);
            if (stateElapsed >= glimpseEndsAt)
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
            propertyBlock.SetFloat(ApparitionAlphaId, Mathf.Clamp01(alpha));
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
            faintFadeInDuration = Mathf.Max(0.01f, faintFadeInDuration);
            faintHoldDuration = Mathf.Max(0f, faintHoldDuration);
            faintFadeOutDuration = Mathf.Max(0.01f, faintFadeOutDuration);
            scriptedLungeDistance = Mathf.Max(0f, scriptedLungeDistance);
            scriptedLungeDuration = Mathf.Max(0.01f, scriptedLungeDuration);
            scriptedHoldDuration = Mathf.Max(0f, scriptedHoldDuration);
            scriptedFadeDuration = Mathf.Max(0.01f, scriptedFadeDuration);
            scriptedScaleMultiplier = Mathf.Clamp(scriptedScaleMultiplier, 1f, 1.4f);
        }
    }
}
