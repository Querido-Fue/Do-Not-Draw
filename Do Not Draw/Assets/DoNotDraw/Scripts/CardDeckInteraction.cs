using System.Collections;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DoNotDraw.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class CardDeckInteraction : MonoBehaviour
    {
        [Header("Interaction")]
        [SerializeField] private Transform player;
        [SerializeField, Min(0.25f)] private float interactionDistance = 1.8f;

        [Header("Deck")]
        [SerializeField] private Transform deckBody;
        [SerializeField] private Transform deckTop;
        [SerializeField] private GameObject cardTemplate;
        [SerializeField] private Transform drawnCardParent;
        [SerializeField] private Transform displayAnchor;
        [SerializeField, Min(1)] private int totalCards = 8;

        [Header("Animation")]
        [SerializeField, Min(0.1f)] private float animationDuration = 0.82f;
        [SerializeField, Min(0f)] private float arcHeight = 0.28f;
        [SerializeField, Min(0f)] private float cardSpread = 0.055f;

        [Header("Prompt")]
        [SerializeField] private GameObject promptPanel;
        [SerializeField] private Text promptText;

        [Header("Card Faces")]
        [SerializeField] private Material[] faceAccentMaterials = System.Array.Empty<Material>();

        [Header("Sound")]
        [SerializeField] private AudioClip drawSound;
        [SerializeField] private AudioClip landingSound;
        [SerializeField, Range(0f, 1f)] private float drawVolume = 0.44f;
        [SerializeField, Range(0f, 1f)] private float landingVolume = 0.28f;

        private AudioSource audioSource;
        private Vector3 initialDeckBodyScale;
        private float deckBottomLocalY;
        private int remainingCards;
        private int drawnCardCount;
        private bool isDrawing;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            remainingCards = Mathf.Max(1, totalCards);

            if (deckBody != null)
            {
                initialDeckBodyScale = deckBody.localScale;
                deckBottomLocalY = deckBody.localPosition.y - initialDeckBodyScale.y * 0.5f;
            }

            if (cardTemplate != null)
            {
                cardTemplate.SetActive(false);
            }

            SetPromptVisible(false);
            UpdateDeckVisual();
        }

        private void Update()
        {
            bool isNearby = IsPlayerNearby();
            SetPromptVisible(isNearby);

            if (!isNearby)
            {
                return;
            }

            UpdatePromptText();
            if (!isDrawing && remainingCards > 0 && WasDrawKeyPressed())
            {
                StartCoroutine(DrawCard());
            }
        }

        private bool IsPlayerNearby()
        {
            if (player == null)
            {
                return false;
            }

            Vector3 offset = player.position - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= interactionDistance * interactionDistance;
        }

        private static bool WasDrawKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F);
#endif
        }

        private IEnumerator DrawCard()
        {
            if (cardTemplate == null || drawnCardParent == null || displayAnchor == null || deckTop == null)
            {
                yield break;
            }

            isDrawing = true;
            UpdatePromptText();

            GameObject card = Instantiate(cardTemplate, drawnCardParent);
            card.name = $"Drawn Card {drawnCardCount + 1:00}";
            card.SetActive(true);
            ApplyFaceAccent(card, drawnCardCount);

            Vector3 startPosition = deckTop.position + deckTop.up * 0.012f;
            Quaternion startRotation = deckTop.rotation * Quaternion.Euler(0f, 0f, 180f);
            Vector3 endPosition = displayAnchor.position
                + displayAnchor.right * (drawnCardCount * cardSpread)
                + displayAnchor.up * (drawnCardCount * 0.0025f);
            float endYaw = Mathf.Lerp(-7f, 8f, (drawnCardCount % 5) / 4f);
            Quaternion endRotation = displayAnchor.rotation * Quaternion.Euler(0f, endYaw, 0f);

            Transform cardTransform = card.transform;
            cardTransform.SetPositionAndRotation(startPosition, startRotation);

            remainingCards--;
            UpdateDeckVisual();
            PlaySound(drawSound, drawVolume);

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
            PlaySound(landingSound, landingVolume);

            drawnCardCount++;
            isDrawing = false;
            UpdatePromptText();
        }

        private void ApplyFaceAccent(GameObject card, int cardIndex)
        {
            if (faceAccentMaterials == null || faceAccentMaterials.Length == 0)
            {
                return;
            }

            Transform frontDesign = card.transform.Find("Front Design");
            if (frontDesign == null)
            {
                return;
            }

            Material material = faceAccentMaterials[cardIndex % faceAccentMaterials.Length];
            if (material == null)
            {
                return;
            }

            Renderer[] renderers = frontDesign.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer faceRenderer in renderers)
            {
                faceRenderer.sharedMaterial = material;
            }
        }

        private void UpdateDeckVisual()
        {
            if (deckBody == null || totalCards <= 0)
            {
                return;
            }

            float remainingRatio = Mathf.Clamp01(remainingCards / (float)totalCards);
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

        private void PlaySound(AudioClip clip, float volume)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
        }

        private void UpdatePromptText()
        {
            if (promptText == null)
            {
                return;
            }

            if (isDrawing)
            {
                promptText.text = "DRAWING...";
            }
            else if (remainingCards <= 0)
            {
                promptText.text = "DECK EMPTY";
            }
            else
            {
                promptText.text = "[F]  DRAW CARD";
            }
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
            SetPromptVisible(false);
        }

        private void OnValidate()
        {
            interactionDistance = Mathf.Max(0.25f, interactionDistance);
            totalCards = Mathf.Max(1, totalCards);
            animationDuration = Mathf.Max(0.1f, animationDuration);
            arcHeight = Mathf.Max(0f, arcHeight);
            cardSpread = Mathf.Max(0f, cardSpread);
            drawVolume = Mathf.Clamp01(drawVolume);
            landingVolume = Mathf.Clamp01(landingVolume);
        }
    }
}
