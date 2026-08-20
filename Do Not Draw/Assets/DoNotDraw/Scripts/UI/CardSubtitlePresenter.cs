using System.Collections;
using DoNotDraw.Narrative;
using UnityEngine;
using UnityEngine.UI;

namespace DoNotDraw.UI
{
    [DisallowMultipleComponent]
    public sealed class CardSubtitlePresenter : MonoBehaviour
    {
        [SerializeField] private CardSequenceRunner[] runners;
        [SerializeField] private GameObject subtitlePanel;
        [SerializeField] private Text subtitleText;
        [SerializeField, Min(0.1f)] private float visibleDuration = 4.5f;

        private Coroutine hideRoutine;

        private void Awake()
        {
            SetVisible(false);
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            CancelHideRoutine();
            SetVisible(false);
        }

        private void Subscribe()
        {
            if (runners == null)
            {
                return;
            }

            foreach (CardSequenceRunner runner in runners)
            {
                if (runner == null)
                {
                    continue;
                }

                runner.CardDrawStarted -= HandleCardDrawStarted;
                runner.CardDrawStarted += HandleCardDrawStarted;
                runner.CardRevealed -= HandleCardRevealed;
                runner.CardRevealed += HandleCardRevealed;
                runner.SequenceStarted -= HandleSequenceStarted;
                runner.SequenceStarted += HandleSequenceStarted;
            }
        }

        private void Unsubscribe()
        {
            if (runners == null)
            {
                return;
            }

            foreach (CardSequenceRunner runner in runners)
            {
                if (runner == null)
                {
                    continue;
                }

                runner.CardDrawStarted -= HandleCardDrawStarted;
                runner.CardRevealed -= HandleCardRevealed;
                runner.SequenceStarted -= HandleSequenceStarted;
            }
        }

        private void HandleSequenceStarted(CardSequenceDefinition sequence)
        {
            HideImmediately();
        }

        private void HandleCardDrawStarted(CardDefinition card, int drawIndex)
        {
            HideImmediately();
        }

        private void HandleCardRevealed(CardDefinition card, GameObject cardObject, int drawIndex)
        {
            if (card == null || subtitleText == null || string.IsNullOrEmpty(card.FaceText))
            {
                HideImmediately();
                return;
            }

            CancelHideRoutine();
            subtitleText.text = card.FaceText;
            SetVisible(true);
            hideRoutine = StartCoroutine(HideAfterDelay());
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(visibleDuration);
            hideRoutine = null;
            SetVisible(false);
        }

        private void HideImmediately()
        {
            CancelHideRoutine();
            SetVisible(false);
        }

        private void CancelHideRoutine()
        {
            if (hideRoutine == null)
            {
                return;
            }

            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        private void SetVisible(bool visible)
        {
            if (subtitlePanel != null && subtitlePanel.activeSelf != visible)
            {
                subtitlePanel.SetActive(visible);
            }
        }

        private void OnValidate()
        {
            visibleDuration = Mathf.Max(0.1f, visibleDuration);
        }
    }
}
