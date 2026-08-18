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
        [SerializeField, Min(1)] private int cardsPerRow = 8;

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

        private readonly List<GameObject> runtimeCards = new List<GameObject>();
        private AudioSource audioSource;
        private Vector3 initialDeckBodyScale;
        private float deckBottomLocalY;
        private int initialDeckCardCount = 1;
        private int remainingCards = 1;
        private bool visualStateCached;

        public bool IsPresenting { get; private set; }
        public int RemainingCards => remainingCards;

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

            if (cardTemplate != null)
            {
                cardTemplate.SetActive(false);
            }

            UpdateDeckVisual();
        }

        public bool PresentCard(CardDefinition definition, int drawIndex, Action<GameObject> completed)
        {
            if (IsPresenting || cardTemplate == null || drawnCardParent == null || displayAnchor == null || deckTop == null)
            {
                return false;
            }

            StartCoroutine(PresentCardRoutine(definition, Mathf.Max(0, drawIndex), completed));
            return true;
        }

        private IEnumerator PresentCardRoutine(CardDefinition definition, int drawIndex, Action<GameObject> completed)
        {
            IsPresenting = true;

            GameObject card = Instantiate(cardTemplate, drawnCardParent);
            card.name = $"Drawn Card {drawIndex + 1:00}";
            card.SetActive(true);
            runtimeCards.Add(card);
            ApplyDefinition(card, definition, drawIndex);

            Vector3 startPosition = deckTop.position + deckTop.up * 0.012f;
            Quaternion startRotation = deckTop.rotation * Quaternion.Euler(0f, 0f, 180f);

            int column = drawIndex % cardsPerRow;
            int row = drawIndex / cardsPerRow;
            Vector3 endPosition = displayAnchor.position
                + displayAnchor.right * (column * cardSpread)
                - displayAnchor.forward * (row * rowSpread)
                + displayAnchor.up * (drawIndex * 0.0025f);
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

            IsPresenting = false;
            completed?.Invoke(card);
        }

        private void ApplyDefinition(GameObject card, CardDefinition definition, int drawIndex)
        {
            Material accentMaterial = definition?.FaceAccentMaterial;
            if (accentMaterial == null && fallbackFaceAccentMaterials is { Length: > 0 })
            {
                accentMaterial = fallbackFaceAccentMaterials[drawIndex % fallbackFaceAccentMaterials.Length];
            }

            Transform frontDesign = card.transform.Find("Front Design");
            if (frontDesign != null && accentMaterial != null)
            {
                Renderer[] renderers = frontDesign.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer faceRenderer in renderers)
                {
                    faceRenderer.sharedMaterial = accentMaterial;
                }
            }

            Text faceLabel = card.GetComponentInChildren<Text>(true);
            if (faceLabel != null)
            {
                string faceText = definition?.FaceText ?? string.Empty;
                faceLabel.text = faceText;
                faceLabel.color = definition != null
                    ? definition.FaceTextColor
                    : new Color(0.055f, 0.035f, 0.025f, 1f);
                faceLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(faceText));
            }
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
            newScale.y = initialDeckBodyScale.y * remainingRatio;
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
            cardsPerRow = Mathf.Max(1, cardsPerRow);
            animationDuration = Mathf.Max(0.1f, animationDuration);
            arcHeight = Mathf.Max(0f, arcHeight);
            drawVolume = Mathf.Clamp01(drawVolume);
            landingVolume = Mathf.Clamp01(landingVolume);
        }
    }
}
