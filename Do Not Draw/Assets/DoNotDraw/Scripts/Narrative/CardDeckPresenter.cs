using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DoNotDraw.Narrative
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class CardDeckPresenter : MonoBehaviour
    {
        [Header("Deck Visuals")]
        [SerializeField] private Transform deckBody;
        [SerializeField] private Transform deckTop;
        [SerializeField] private GameObject cardTemplate;
        [SerializeField] private Transform drawnCardParent;
        [SerializeField] private Transform displayAnchor;

        [Header("Card Layout")]
        [SerializeField, Min(0f)] private float cardSpread = 0.055f;
        [SerializeField, Min(0f)] private float rowSpread = 0.07f;
        [SerializeField, Min(0f)] private float cardLayerSpacing = 0.006f;
        [SerializeField, Min(1)] private int cardsPerRow = 8;
        [SerializeField, Min(1)] private int sortingOrderStep = 10;

        [Header("Animation")]
        [SerializeField, Min(0.1f)] private float animationDuration = 0.82f;
        [SerializeField, Min(0f)] private float arcHeight = 0.28f;

        [Header("Fallback Card Style")]
        [SerializeField] private Material[] fallbackFaceAccentMaterials = Array.Empty<Material>();

        [Header("Fallback Sound")]
        [SerializeField] private AudioClip drawSound;
        [SerializeField] private AudioClip landingSound;
        [SerializeField, Range(0f, 1f)] private float drawVolume = 0.44f;
        [SerializeField, Range(0f, 1f)] private float landingVolume = 0.28f;

        [Header("Voice Narration")]
        [SerializeField] private bool voiceNarrationEnabled = true;

        private readonly List<GameObject> runtimeCards = new List<GameObject>();
        private AudioSource audioSource;
        private Vector3 initialDeckBodyScale;
        private float deckBottomLocalY;
        private int initialDeckCardCount = 1;
        private int remainingCards = 1;
        private float deckThicknessMultiplier = 1f;
        private bool visualStateCached;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public bool IsPresenting { get; private set; }
        public int RemainingCards => remainingCards;
        public GameObject LatestCard => runtimeCards.Count > 0 ? runtimeCards[runtimeCards.Count - 1] : null;
        public Transform DisplayAnchor => displayAnchor;
        public bool VoiceNarrationEnabled => voiceNarrationEnabled;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            CacheInitialVisualState();

            if (cardTemplate != null)
            {
                cardTemplate.SetActive(false);
            }
        }

        public void ResetPresentation(int deckSize)
        {
            StopAllCoroutines();
            IsPresenting = false;
            ClearRuntimeCards();
            CacheInitialVisualState();

            initialDeckCardCount = Mathf.Max(1, deckSize);
            remainingCards = initialDeckCardCount;
            deckThicknessMultiplier = 1f;

            if (cardTemplate != null)
            {
                cardTemplate.SetActive(false);
            }

            UpdateDeckVisual();
        }

        public bool PresentCard(
            CardDefinition definition,
            int drawIndex,
            Action<GameObject> revealed,
            Action presentationFinished)
        {
            if (IsPresenting || cardTemplate == null || drawnCardParent == null || displayAnchor == null || deckTop == null)
            {
                return false;
            }

            StartCoroutine(PresentCardRoutine(
                definition,
                Mathf.Max(0, drawIndex),
                revealed,
                presentationFinished));
            return true;
        }

        private IEnumerator PresentCardRoutine(
            CardDefinition definition,
            int drawIndex,
            Action<GameObject> revealed,
            Action presentationFinished)
        {
            IsPresenting = true;

            GameObject card = Instantiate(cardTemplate, drawnCardParent);
            card.name = $"Drawn Card {drawIndex + 1:00}";
            card.SetActive(true);
            runtimeCards.Add(card);
            ApplyDrawPriority(card, drawIndex);
            Text faceLabel = ApplyDefinition(card, definition, drawIndex);

            Vector3 startPosition = deckTop.position + deckTop.up * 0.012f;
            Quaternion startRotation = deckTop.rotation * Quaternion.Euler(0f, 0f, 180f);

            int column = drawIndex % cardsPerRow;
            int row = drawIndex / cardsPerRow;
            Vector3 endPosition = displayAnchor.position
                - displayAnchor.right * (column * cardSpread)
                - displayAnchor.forward * (row * rowSpread)
                + displayAnchor.up * (drawIndex * cardLayerSpacing);
            float endYaw = Mathf.Lerp(-7f, 8f, column / (float)Mathf.Max(1, cardsPerRow - 1));
            Quaternion endRotation = displayAnchor.rotation * Quaternion.Euler(0f, endYaw, 0f);

            Transform cardTransform = card.transform;
            cardTransform.SetPositionAndRotation(startPosition, startRotation);

            remainingCards = Mathf.Max(0, remainingCards - 1);
            UpdateDeckVisual();
            PlaySound(definition != null && definition.DrawSoundOverride != null
                ? definition.DrawSoundOverride
                : drawSound, drawVolume);

            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / animationDuration);
                float smoothTime = normalizedTime * normalizedTime * (3f - 2f * normalizedTime);
                Vector3 position = Vector3.LerpUnclamped(startPosition, endPosition, smoothTime);
                position.y += Mathf.Sin(normalizedTime * Mathf.PI) * arcHeight;

                cardTransform.SetPositionAndRotation(
                    position,
                    Quaternion.Slerp(startRotation, endRotation, smoothTime));

                yield return null;
            }

            cardTransform.SetPositionAndRotation(endPosition, endRotation);
            PlaySound(definition != null && definition.LandingSoundOverride != null
                ? definition.LandingSoundOverride
                : landingSound, landingVolume);
            revealed?.Invoke(card);

            yield return AnimateRevealEffects(card, faceLabel, definition);

            AudioClip voiceClip = voiceNarrationEnabled ? definition?.VoiceClip : null;
            if (voiceClip != null)
            {
                if (definition.VoiceDelay > 0f)
                {
                    yield return new WaitForSeconds(definition.VoiceDelay);
                }

                PlaySound(voiceClip, definition.VoiceVolume);
                yield return new WaitForSeconds(voiceClip.length);
            }

            IsPresenting = false;
            presentationFinished?.Invoke();
        }

        public void SetVoiceNarrationEnabled(bool enabled)
        {
            voiceNarrationEnabled = enabled;
        }

        private Text ApplyDefinition(GameObject card, CardDefinition definition, int drawIndex)
        {
            Texture2D faceTexture = definition?.FaceTexture;
            Transform frontSurface = card.transform.Find("Front Surface");
            Transform frontDesign = card.transform.Find("Front Design");
            Text faceLabel = card.GetComponentInChildren<Text>(true);

            if (faceTexture != null)
            {
                ApplyFaceTexture(frontSurface, faceTexture);
                if (frontDesign != null)
                {
                    frontDesign.gameObject.SetActive(false);
                }

                if (faceLabel != null)
                {
                    faceLabel.gameObject.SetActive(false);
                }

                return null;
            }

            Material accentMaterial = definition?.FaceAccentMaterial;
            if (accentMaterial == null && fallbackFaceAccentMaterials is { Length: > 0 })
            {
                accentMaterial = fallbackFaceAccentMaterials[drawIndex % fallbackFaceAccentMaterials.Length];
            }

            if (frontDesign != null && accentMaterial != null)
            {
                Renderer[] renderers = frontDesign.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer faceRenderer in renderers)
                {
                    faceRenderer.sharedMaterial = accentMaterial;
                }
            }

            if (faceLabel != null)
            {
                string faceText = definition?.FaceText ?? string.Empty;
                faceLabel.text = faceText;
                Color textColor = definition != null
                    ? definition.FaceTextColor
                    : new Color(0.055f, 0.035f, 0.025f, 1f);
                textColor.a = 0f;
                faceLabel.color = textColor;
                faceLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(faceText));
                ApplyTypography(faceLabel, definition?.TypographyStage ?? CardTypographyStage.Clean);
            }

            return faceLabel;
        }

        private static void ApplyFaceTexture(Transform frontSurface, Texture2D faceTexture)
        {
            Renderer faceRenderer = frontSurface != null
                ? frontSurface.GetComponent<Renderer>()
                : null;
            if (faceRenderer == null || faceTexture == null)
            {
                return;
            }

            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            faceRenderer.GetPropertyBlock(properties);
            properties.SetTexture(BaseMapId, faceTexture);
            properties.SetTexture(MainTextureId, faceTexture);
            properties.SetColor(BaseColorId, Color.white);
            properties.SetColor(ColorId, Color.white);
            faceRenderer.SetPropertyBlock(properties);
        }

        private void ApplyDrawPriority(GameObject card, int drawIndex)
        {
            int baseOrder = Mathf.Max(0, drawIndex) * sortingOrderStep;
            Renderer[] renderers = card.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer cardRenderer in renderers)
            {
                cardRenderer.sortingOrder = baseOrder;
            }

            Canvas[] canvases = card.GetComponentsInChildren<Canvas>(true);
            foreach (Canvas canvas in canvases)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = baseOrder + 1;
            }
        }

        private static void ApplyTypography(Text label, CardTypographyStage stage)
        {
            if (label == null)
            {
                return;
            }

            RectTransform rect = label.rectTransform;
            switch (stage)
            {
                case CardTypographyStage.Uneven:
                    label.fontStyle = FontStyle.Bold;
                    rect.localRotation = Quaternion.Euler(0f, 0f, 0.35f);
                    break;
                case CardTypographyStage.Damaged:
                    label.fontStyle = FontStyle.BoldAndItalic;
                    rect.localRotation = Quaternion.Euler(0f, 0f, -1.1f);
                    break;
                case CardTypographyStage.DoubleExposure:
                    label.fontStyle = FontStyle.Bold;
                    rect.localRotation = Quaternion.Euler(0f, 0f, 0.5f);
                    break;
                default:
                    label.fontStyle = FontStyle.Normal;
                    rect.localRotation = Quaternion.identity;
                    break;
            }
        }

        private IEnumerator AnimateRevealEffects(
            GameObject card,
            Text faceLabel,
            CardDefinition definition)
        {
            float fadeDuration = definition != null ? definition.TextFadeDuration : 0.28f;
            float liftDuration = definition != null && definition.LiftOnReveal
                ? definition.RevealLiftDuration
                : 0f;
            float echoDuration = definition?.TypographyStage == CardTypographyStage.DoubleExposure
                ? Mathf.Max(0.01f, definition.DoubleExposureDuration)
                : 0f;
            float duration = Mathf.Max(fadeDuration, liftDuration, echoDuration);
            if (duration <= 0f)
            {
                SetTextAlpha(faceLabel, 1f);
                yield break;
            }

            Text echo = CreateTextEcho(faceLabel, definition, out Vector2 echoStartPosition);
            Vector3 basePosition = card.transform.position;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float fadeT = fadeDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeDuration);
                SetTextAlpha(faceLabel, fadeT * fadeT * (3f - 2f * fadeT));

                if (liftDuration > 0f)
                {
                    float liftT = Mathf.Clamp01(elapsed / liftDuration);
                    card.transform.position = basePosition
                        + card.transform.up * (Mathf.Sin(liftT * Mathf.PI) * definition.RevealLiftHeight);
                }

                if (echo != null)
                {
                    float echoT = Mathf.Clamp01(elapsed / echoDuration);
                    echo.rectTransform.anchoredPosition = Vector2.Lerp(echoStartPosition, Vector2.zero, echoT);
                    SetTextAlpha(echo, Mathf.Sin(echoT * Mathf.PI) * 0.72f);
                }

                yield return null;
            }

            card.transform.position = basePosition;
            SetTextAlpha(faceLabel, 1f);
            if (echo != null)
            {
                Destroy(echo.gameObject);
            }
        }

        private static Text CreateTextEcho(
            Text source,
            CardDefinition definition,
            out Vector2 startPosition)
        {
            startPosition = new Vector2(3.5f, -1.5f);
            if (source == null
                || definition == null
                || definition.TypographyStage != CardTypographyStage.DoubleExposure
                || definition.DoubleExposureDuration <= 0f)
            {
                return null;
            }

            GameObject echoObject = Instantiate(source.gameObject, source.transform.parent);
            echoObject.name = "Card Face Echo";
            echoObject.transform.SetSiblingIndex(source.transform.GetSiblingIndex());
            Text echo = echoObject.GetComponent<Text>();
            Color echoColor = definition.FaceTextColor;
            echoColor.r = Mathf.Clamp01(echoColor.r + 0.22f);
            echoColor.a = 0f;
            echo.color = echoColor;
            echo.rectTransform.anchoredPosition = startPosition;
            return echo;
        }

        private static void SetTextAlpha(Text label, float alpha)
        {
            if (label == null)
            {
                return;
            }

            Color color = label.color;
            color.a = Mathf.Clamp01(alpha);
            label.color = color;
        }

        private void CacheInitialVisualState()
        {
            if (visualStateCached || deckBody == null)
            {
                return;
            }

            initialDeckBodyScale = deckBody.localScale;
            deckBottomLocalY = deckBody.localPosition.y - initialDeckBodyScale.y * 0.5f;
            visualStateCached = true;
        }

        private void UpdateDeckVisual()
        {
            if (deckBody == null || !visualStateCached)
            {
                return;
            }

            float remainingRatio = Mathf.Clamp01(remainingCards / (float)Mathf.Max(1, initialDeckCardCount));
            Vector3 newScale = initialDeckBodyScale;
            newScale.y = initialDeckBodyScale.y * remainingRatio * deckThicknessMultiplier;
            deckBody.localScale = newScale;

            Vector3 bodyPosition = deckBody.localPosition;
            bodyPosition.y = deckBottomLocalY + newScale.y * 0.5f;
            deckBody.localPosition = bodyPosition;

            if (deckTop != null)
            {
                Vector3 topPosition = deckTop.localPosition;
                topPosition.y = deckBottomLocalY + newScale.y + 0.003f;
                deckTop.localPosition = topPosition;
                deckTop.gameObject.SetActive(remainingCards > 0);
            }

            deckBody.gameObject.SetActive(remainingCards > 0);
        }

        public void SetDeckThicknessMultiplier(float multiplier)
        {
            deckThicknessMultiplier = Mathf.Max(0.05f, multiplier);
            UpdateDeckVisual();
        }

        private void ClearRuntimeCards()
        {
            foreach (GameObject card in runtimeCards)
            {
                if (card == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(card);
                }
                else
                {
                    DestroyImmediate(card);
                }
            }

            runtimeCards.Clear();
        }

        private void PlaySound(AudioClip clip, float volume)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
        }

        private void OnValidate()
        {
            cardSpread = Mathf.Max(0f, cardSpread);
            rowSpread = Mathf.Max(0f, rowSpread);
            cardLayerSpacing = Mathf.Max(0f, cardLayerSpacing);
            cardsPerRow = Mathf.Max(1, cardsPerRow);
            sortingOrderStep = Mathf.Max(1, sortingOrderStep);
            animationDuration = Mathf.Max(0.1f, animationDuration);
            arcHeight = Mathf.Max(0f, arcHeight);
            drawVolume = Mathf.Clamp01(drawVolume);
            landingVolume = Mathf.Clamp01(landingVolume);
        }
    }
}
