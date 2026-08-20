using System;
using DoNotDraw.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace DoNotDraw.UI
{
    [DisallowMultipleComponent]
    public sealed class HorrorPerceptionOverlay : MonoBehaviour
    {
        private const float FaceAspect = 736f / 1063f;
        private static readonly int FaceTextureId = Shader.PropertyToID("_FaceTex");
        private static readonly int FaceAlphaId = Shader.PropertyToID("_FaceAlpha");
        private static readonly int FaceCenterId = Shader.PropertyToID("_FaceCenter");
        private static readonly int FaceSizeId = Shader.PropertyToID("_FaceSize");
        private static readonly int ChromaticId = Shader.PropertyToID("_Chromatic");
        private static readonly int GlitchStrengthId = Shader.PropertyToID("_GlitchStrength");
        private static readonly int VignetteIntensityId = Shader.PropertyToID("_VignetteIntensity");
        private static readonly int RedPulseId = Shader.PropertyToID("_RedPulse");

        [Header("Visuals")]
        [SerializeField] private RawImage overlayImage;
        [SerializeField] private Material overlayTemplate;
        [SerializeField] private Texture2D faceTexture;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource presenceSource;
        [SerializeField] private AudioSource growlSource;
        [SerializeField] private AudioSource heartbeatSource;

        [Header("Audio Clips")]
        [SerializeField] private AudioClip multiPresenceClip;
        [SerializeField] private AudioClip growlClip;
        [SerializeField] private AudioClip climaxHeartbeatClip;

        [Header("Peripheral Haunting")]
        [SerializeField] private Vector2 firstAppearanceDelay = new Vector2(2f, 4f);
        [SerializeField] private Vector2 initialRepeatDelay = new Vector2(3f, 7f);
        [SerializeField] private Vector2 escalatedRepeatDelay = new Vector2(1.8f, 3.2f);
        [SerializeField] private Vector2 peripheralVisibleDuration = new Vector2(0.15f, 0.4f);
        [SerializeField, Min(1f)] private float peripheralEscalationDuration = 35f;

        [Header("Climax")]
        [SerializeField, Min(1f)] private float climaxCycleDuration = 7f;
        [SerializeField, Min(0f)] private float climaxSilentGap = 0.42f;
        [SerializeField] private Vector2 climaxRepeatDelay = new Vector2(2f, 3f);
        [SerializeField] private Vector2 climaxVisibleDuration = new Vector2(0.28f, 0.54f);

        private Material runtimeMaterial;
        private OverlayMode mode;
        private Vector2 faceCenter = new Vector2(0.5f, 0.5f);
        private Vector2 faceSize = new Vector2(0.22f, 0.32f);
        private float faceAlpha;
        private float facePeakAlpha;
        private float faceShownAt;
        private float faceVisibleFor;
        private float modeStartedAt;
        private float nextFaceAt;
        private float chromatic;
        private float glitchStrength;
        private float vignetteIntensity;
        private float redPulse;
        private float presenceLogicalVolume;
        private float growlLogicalVolume;
        private float heartbeatLogicalVolume;
        private float fadeStartedAt;
        private float fadeDuration;
        private float fadeStartFaceAlpha;
        private float fadeStartGlitch;
        private float fadeStartVignette;
        private float fadeStartPresenceVolume;
        private float fadeStartGrowlVolume;
        private float fadeStartHeartbeatVolume;
        private bool faceVisible;
        private bool climaxHardCut;
        private int climaxFaceIndex;

        private enum OverlayMode
        {
            Hidden,
            Peripheral,
            Climax,
            Fading
        }

        public event Action ClimaxHardCut;

        public bool IsPeripheralActive => mode == OverlayMode.Peripheral;
        public bool IsClimaxActive => mode == OverlayMode.Climax;
        public float ClimaxIntensity { get; private set; }

        private void Awake()
        {
            EnsureRuntimeMaterial();
            ConfigureAudioSources();
            StopImmediate();
        }

        private void OnEnable()
        {
            EnsureRuntimeMaterial();
            ConfigureAudioSources();
            StopImmediate();
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            float deltaTime = Time.unscaledDeltaTime;
            switch (mode)
            {
                case OverlayMode.Peripheral:
                    UpdatePeripheral(now, deltaTime);
                    break;
                case OverlayMode.Climax:
                    UpdateClimax(now);
                    break;
                case OverlayMode.Fading:
                    UpdateFade(now);
                    break;
            }

            ApplyMaterialState();
            ApplyLoopVolumes();
        }

        public void BeginPeripheralHaunting()
        {
            StopImmediate();
            mode = OverlayMode.Peripheral;
            modeStartedAt = Time.unscaledTime;
            nextFaceAt = modeStartedAt + RandomRange(firstAppearanceDelay);
            StartLoop(presenceSource, multiPresenceClip);
        }

        public void BeginClimaxPressure()
        {
            StopImmediate();
            mode = OverlayMode.Climax;
            StartClimaxCycle(Time.unscaledTime);
        }

        public void StopAllEffects(float duration = 0.2f)
        {
            if (mode == OverlayMode.Hidden)
            {
                return;
            }

            if (duration <= 0f)
            {
                StopImmediate();
                return;
            }

            mode = OverlayMode.Fading;
            fadeStartedAt = Time.unscaledTime;
            fadeDuration = Mathf.Max(0.01f, duration);
            fadeStartFaceAlpha = faceAlpha;
            fadeStartGlitch = glitchStrength;
            fadeStartVignette = vignetteIntensity;
            fadeStartPresenceVolume = presenceLogicalVolume;
            fadeStartGrowlVolume = growlLogicalVolume;
            fadeStartHeartbeatVolume = heartbeatLogicalVolume;
        }

        private void UpdatePeripheral(float now, float deltaTime)
        {
            float escalation = Mathf.Clamp01((now - modeStartedAt) / peripheralEscalationDuration);
            presenceLogicalVolume = Mathf.MoveTowards(
                presenceLogicalVolume,
                Mathf.Lerp(0.1f, 0.24f, escalation),
                deltaTime * 0.18f);
            vignetteIntensity = Mathf.Lerp(0.015f, 0.08f, escalation);
            redPulse = 0.18f + Mathf.Sin(now * 1.7f) * 0.08f;

            if (faceVisible)
            {
                UpdateFaceEnvelope(now);
                return;
            }

            faceAlpha = 0f;
            glitchStrength = Mathf.Lerp(glitchStrength, 0.035f + escalation * 0.08f, deltaTime * 8f);
            if (now < nextFaceAt)
            {
                return;
            }

            ShowPeripheralFace(now, escalation);
            Vector2 repeatDelay = Vector2.Lerp(initialRepeatDelay, escalatedRepeatDelay, escalation);
            nextFaceAt = now + RandomRange(repeatDelay);
        }

        private void ShowPeripheralFace(float now, float escalation)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float horizontalRadius = Mathf.Lerp(0.42f, 0.17f, escalation);
            float verticalRadius = Mathf.Lerp(0.34f, 0.14f, escalation);
            faceCenter = new Vector2(
                0.5f + Mathf.Cos(angle) * horizontalRadius,
                0.5f + Mathf.Sin(angle) * verticalRadius);
            faceCenter.x = Mathf.Clamp(faceCenter.x, 0.09f, 0.91f);
            faceCenter.y = Mathf.Clamp(faceCenter.y, 0.12f, 0.88f);

            float height = UnityEngine.Random.Range(0.24f, 0.35f)
                * Mathf.Lerp(0.9f, 1.22f, escalation);
            faceSize = FaceSizeForHeight(height);
            chromatic = UnityEngine.Random.Range(0.3f, 1.15f);
            glitchStrength = UnityEngine.Random.Range(0.42f, 0.9f);
            facePeakAlpha = Mathf.Lerp(0.7f, 0.95f, escalation);
            faceVisibleFor = RandomRange(peripheralVisibleDuration);
            faceShownAt = now;
            faceVisible = true;
        }

        private void UpdateClimax(float now)
        {
            float cycleTime = now - modeStartedAt;
            if (!climaxHardCut && cycleTime >= climaxCycleDuration)
            {
                EnterClimaxHardCut();
            }

            if (climaxHardCut)
            {
                ClimaxIntensity = 0f;
                faceAlpha = 0f;
                glitchStrength = 0f;
                vignetteIntensity = 0f;
                redPulse = 0f;
                if (cycleTime >= climaxCycleDuration + climaxSilentGap)
                {
                    StartClimaxCycle(now);
                }
                return;
            }

            float progress = Mathf.Clamp01(cycleTime / climaxCycleDuration);
            float shaped = progress * progress * (3f - 2f * progress);
            ClimaxIntensity = shaped;
            vignetteIntensity = Mathf.Lerp(0.12f, 0.94f, Mathf.Pow(shaped, 1.25f));
            glitchStrength = Mathf.Lerp(0.14f, 0.92f, shaped);
            redPulse = 0.5f + 0.5f * Mathf.Sin(now * Mathf.Lerp(5f, 11f, shaped));
            growlLogicalVolume = Mathf.Lerp(0.13f, 0.48f, shaped);
            heartbeatLogicalVolume = Mathf.Lerp(0.1f, 0.92f, Mathf.Pow(shaped, 0.8f));

            if (faceVisible)
            {
                UpdateFaceEnvelope(now);
            }
            else if (now >= nextFaceAt)
            {
                ShowClimaxFace(now, shaped);
                nextFaceAt = now + RandomRange(climaxRepeatDelay);
            }
        }

        private void StartClimaxCycle(float now)
        {
            modeStartedAt = now;
            climaxHardCut = false;
            climaxFaceIndex = 0;
            faceVisible = false;
            faceAlpha = 0f;
            nextFaceAt = now + 0.35f;
            ClimaxIntensity = 0f;
            StartLoop(growlSource, growlClip);
            StartLoop(heartbeatSource, climaxHeartbeatClip);
            growlLogicalVolume = 0.13f;
            heartbeatLogicalVolume = 0.1f;
        }

        private void ShowClimaxFace(float now, float progress)
        {
            float growth = Mathf.Pow(UnityEngine.Random.Range(1.75f, 2f), climaxFaceIndex);
            float height = Mathf.Min(1.6f, 0.36f * growth);
            float centerJitter = Mathf.Lerp(0.1f, 0.018f, progress);
            faceCenter = new Vector2(
                0.5f + UnityEngine.Random.Range(-centerJitter, centerJitter),
                0.5f + UnityEngine.Random.Range(-centerJitter, centerJitter));
            faceSize = FaceSizeForHeight(height);
            chromatic = Mathf.Lerp(0.65f, 1.45f, progress);
            glitchStrength = Mathf.Max(glitchStrength, Mathf.Lerp(0.58f, 1f, progress));
            facePeakAlpha = Mathf.Lerp(0.78f, 1f, progress);
            faceVisibleFor = RandomRange(climaxVisibleDuration);
            faceShownAt = now;
            faceVisible = true;
            climaxFaceIndex++;
        }

        private void UpdateFaceEnvelope(float now)
        {
            float progress = Mathf.Clamp01((now - faceShownAt) / Mathf.Max(0.01f, faceVisibleFor));
            float envelope = Mathf.Pow(Mathf.Sin(progress * Mathf.PI), 0.38f);
            faceAlpha = facePeakAlpha * envelope;
            if (progress < 1f)
            {
                return;
            }

            faceVisible = false;
            faceAlpha = 0f;
        }

        private void EnterClimaxHardCut()
        {
            climaxHardCut = true;
            faceVisible = false;
            faceAlpha = 0f;
            presenceLogicalVolume = 0f;
            growlLogicalVolume = 0f;
            heartbeatLogicalVolume = 0f;
            presenceSource?.Stop();
            growlSource?.Stop();
            heartbeatSource?.Stop();
            ClimaxHardCut?.Invoke();
        }

        private void UpdateFade(float now)
        {
            float progress = Mathf.Clamp01((now - fadeStartedAt) / fadeDuration);
            float eased = progress * progress * (3f - 2f * progress);
            faceAlpha = Mathf.Lerp(fadeStartFaceAlpha, 0f, eased);
            glitchStrength = Mathf.Lerp(fadeStartGlitch, 0f, eased);
            vignetteIntensity = Mathf.Lerp(fadeStartVignette, 0f, eased);
            redPulse = Mathf.Lerp(redPulse, 0f, eased);
            presenceLogicalVolume = Mathf.Lerp(fadeStartPresenceVolume, 0f, eased);
            growlLogicalVolume = Mathf.Lerp(fadeStartGrowlVolume, 0f, eased);
            heartbeatLogicalVolume = Mathf.Lerp(fadeStartHeartbeatVolume, 0f, eased);
            ClimaxIntensity = Mathf.Lerp(ClimaxIntensity, 0f, eased);
            if (progress >= 1f)
            {
                StopImmediate();
            }
        }

        private void ApplyMaterialState()
        {
            if (runtimeMaterial == null)
            {
                return;
            }

            runtimeMaterial.SetFloat(FaceAlphaId, faceAlpha);
            runtimeMaterial.SetVector(FaceCenterId, faceCenter);
            runtimeMaterial.SetVector(FaceSizeId, faceSize);
            runtimeMaterial.SetFloat(ChromaticId, chromatic);
            runtimeMaterial.SetFloat(GlitchStrengthId, glitchStrength);
            runtimeMaterial.SetFloat(VignetteIntensityId, vignetteIntensity);
            runtimeMaterial.SetFloat(RedPulseId, redPulse);
        }

        private void ApplyLoopVolumes()
        {
            if (presenceSource != null)
            {
                presenceSource.volume = presenceLogicalVolume * BgmVolume.Scale;
            }
            if (growlSource != null)
            {
                growlSource.volume = growlLogicalVolume * BgmVolume.Scale;
            }
            if (heartbeatSource != null)
            {
                heartbeatSource.volume = heartbeatLogicalVolume * BgmVolume.Scale;
            }
        }

        private void EnsureRuntimeMaterial()
        {
            if (runtimeMaterial != null || overlayImage == null)
            {
                return;
            }

            Material source = overlayTemplate != null ? overlayTemplate : overlayImage.material;
            if (source == null)
            {
                return;
            }

            runtimeMaterial = new Material(source)
            {
                name = source.name + " (Runtime)",
                hideFlags = HideFlags.DontSave
            };
            if (faceTexture != null)
            {
                runtimeMaterial.SetTexture(FaceTextureId, faceTexture);
            }
            overlayImage.material = runtimeMaterial;
            overlayImage.raycastTarget = false;
        }

        private void ConfigureAudioSources()
        {
            ConfigureAudioSource(presenceSource, true);
            ConfigureAudioSource(growlSource, true);
            ConfigureAudioSource(heartbeatSource, true);
        }

        private static void ConfigureAudioSource(AudioSource source, bool loop)
        {
            if (source == null)
            {
                return;
            }

            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
        }

        private static void StartLoop(AudioSource source, AudioClip clip)
        {
            if (source == null || clip == null)
            {
                return;
            }

            if (source.clip != clip)
            {
                source.Stop();
                source.clip = clip;
            }
            source.loop = true;
            if (!source.isPlaying)
            {
                source.Play();
            }
        }

        private void StopImmediate()
        {
            mode = OverlayMode.Hidden;
            faceVisible = false;
            climaxHardCut = false;
            faceAlpha = 0f;
            chromatic = 0f;
            glitchStrength = 0f;
            vignetteIntensity = 0f;
            redPulse = 0f;
            presenceLogicalVolume = 0f;
            growlLogicalVolume = 0f;
            heartbeatLogicalVolume = 0f;
            ClimaxIntensity = 0f;
            presenceSource?.Stop();
            growlSource?.Stop();
            heartbeatSource?.Stop();
            ApplyMaterialState();
            ApplyLoopVolumes();
        }

        private static float RandomRange(Vector2 range)
        {
            float minimum = Mathf.Min(range.x, range.y);
            float maximum = Mathf.Max(range.x, range.y);
            return UnityEngine.Random.Range(minimum, maximum);
        }

        private static Vector2 FaceSizeForHeight(float height)
        {
            float screenAspect = Screen.height > 0
                ? Mathf.Max(0.1f, (float)Screen.width / Screen.height)
                : 16f / 9f;
            return new Vector2(height * FaceAspect / screenAspect, height);
        }

        private void OnDisable()
        {
            StopImmediate();
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
                runtimeMaterial = null;
            }
        }

        private void OnValidate()
        {
            peripheralEscalationDuration = Mathf.Max(1f, peripheralEscalationDuration);
            climaxCycleDuration = Mathf.Max(1f, climaxCycleDuration);
            climaxSilentGap = Mathf.Max(0f, climaxSilentGap);
            NormalizeRange(ref firstAppearanceDelay, 0f);
            NormalizeRange(ref initialRepeatDelay, 0.1f);
            NormalizeRange(ref escalatedRepeatDelay, 0.1f);
            NormalizeRange(ref peripheralVisibleDuration, 0.05f);
            NormalizeRange(ref climaxRepeatDelay, 0.1f);
            NormalizeRange(ref climaxVisibleDuration, 0.05f);
        }

        private static void NormalizeRange(ref Vector2 range, float minimum)
        {
            float lower = Mathf.Max(minimum, Mathf.Min(range.x, range.y));
            float upper = Mathf.Max(lower, Mathf.Max(range.x, range.y));
            range = new Vector2(lower, upper);
        }
    }
}
