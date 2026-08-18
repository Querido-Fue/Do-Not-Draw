using DoNotDraw.Narrative;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DoNotDraw.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CardSequenceRunner))]
    public sealed class CardDeckInteraction : MonoBehaviour
    {
        [Header("Interaction")]
        [SerializeField] private Transform player;
        [SerializeField] private CardSequenceRunner runner;
        [SerializeField, Min(0.25f)] private float interactionDistance = 1.8f;

        [Header("Prompt")]
        [SerializeField] private GameObject promptPanel;
        [SerializeField] private Text promptText;
        [SerializeField] private string drawPrompt = "[F]  DRAW CARD";
        [SerializeField] private string drawingPrompt = "DRAWING...";
        [SerializeField] private string emptyPrompt = "DECK EMPTY";

        private void Awake()
        {
            runner ??= GetComponent<CardSequenceRunner>();
            SetPromptVisible(false);
        }

        private void Update()
        {
            if (runner == null || !IsPlayerNearby())
            {
                SetPromptVisible(false);
                return;
            }

            if (runner.CanPlayerDraw)
            {
                ShowPrompt(drawPrompt);
                if (WasDrawKeyPressed())
                {
                    runner.RequestPlayerDraw();
                }

                return;
            }

            if (runner.State == CardSequenceState.Activating
                && runner.CurrentStep?.Mode == CardSequenceStepMode.PlayerDraw)
            {
                ShowPrompt(drawingPrompt);
                return;
            }

            if (runner.IsComplete)
            {
                ShowPrompt(emptyPrompt);
                return;
            }

            SetPromptVisible(false);
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

        private void ShowPrompt(string message)
        {
            if (promptText != null)
            {
                promptText.text = message;
            }

            SetPromptVisible(true);
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
            runner ??= GetComponent<CardSequenceRunner>();
            interactionDistance = Mathf.Max(0.25f, interactionDistance);
            drawPrompt ??= string.Empty;
            drawingPrompt ??= string.Empty;
            emptyPrompt ??= string.Empty;
        }
    }
}
