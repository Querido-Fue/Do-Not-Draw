using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoNotDraw.Narrative
{
    public enum CardSequenceState
    {
        Stopped,
        WaitingForAvailability,
        ReadyForActivation,
        Activating,
        WaitingForCompletion,
        Completed
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(StoryBlackboard))]
    [RequireComponent(typeof(CardDeckPresenter))]
    public sealed class CardSequenceRunner : MonoBehaviour
    {
        [SerializeField] private CardSequenceDefinition sequence;
        [SerializeField] private StoryBlackboard blackboard;
        [SerializeField] private CardDeckPresenter presenter;
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool resetBlackboardOnStart = true;

        private int currentStepIndex = -1;
        private int drawCount;
        private float availabilityNotBefore;
        private float completionNotBefore;
        private CardDefinition activeCard;
        private int currentStepDrawIndex = -1;
        private bool externalAdvanceQueued;
        private int externalAdvanceStepIndex = -1;
        private bool presentationLocked;

        public event Action<CardSequenceDefinition> SequenceStarted;
        public event Action<CardSequenceState> StateChanged;
        public event Action<CardSequenceStep, int> StepEntered;
        public event Action<CardDefinition, int> CardDrawStarted;
        public event Action<CardDefinition, GameObject, int> CardRevealed;
        public event Action<CardSequenceStep, int> StepCompleted;
        public event Action<CardSequenceDefinition> SequenceCompleted;

        public CardSequenceDefinition Sequence => sequence;
        public StoryBlackboard Blackboard => blackboard;
        public CardSequenceState State { get; private set; } = CardSequenceState.Stopped;
        public int CurrentStepIndex => currentStepIndex;
        public int DrawCount => drawCount;
        public CardSequenceStep CurrentStep => sequence != null ? sequence.GetStep(currentStepIndex) : null;
        public CardDefinition CurrentCard => activeCard != null
            ? activeCard
            : CurrentStep?.ResolveCard(blackboard);
        public bool CanPlayerDraw => State == CardSequenceState.ReadyForActivation
            && CurrentStep?.Mode == CardSequenceStepMode.PlayerDraw
            && !presentationLocked
            && presenter != null
            && !presenter.IsPresenting;
        public bool CanExternallyAdvance => CurrentStep?.AllowExternalAdvance == true
            && State is CardSequenceState.WaitingForAvailability
                or CardSequenceState.ReadyForActivation
                or CardSequenceState.WaitingForCompletion;
        public bool IsComplete => State == CardSequenceState.Completed;

        public void Configure(
            CardSequenceDefinition sequenceDefinition,
            StoryBlackboard storyBlackboard,
            CardDeckPresenter deckPresenter,
            bool shouldPlayOnStart,
            bool shouldResetBlackboardOnStart)
        {
            sequence = sequenceDefinition;
            blackboard = storyBlackboard != null ? storyBlackboard : GetComponent<StoryBlackboard>();
            presenter = deckPresenter != null ? deckPresenter : GetComponent<CardDeckPresenter>();
            playOnStart = shouldPlayOnStart;
            resetBlackboardOnStart = shouldResetBlackboardOnStart;
        }

        private void Awake()
        {
            blackboard ??= GetComponent<StoryBlackboard>();
            presenter ??= GetComponent<CardDeckPresenter>();
        }

        private void Start()
        {
            if (playOnStart)
            {
                BeginSequence();
            }
        }

        private void Update()
        {
            if (externalAdvanceQueued && State != CardSequenceState.Activating)
            {
                TryProcessExternalAdvance();
            }

            switch (State)
            {
                case CardSequenceState.WaitingForAvailability:
                    TryBecomeReady();
                    break;

                case CardSequenceState.ReadyForActivation:
                    if (CurrentStep != null && CurrentStep.Mode != CardSequenceStepMode.PlayerDraw)
                    {
                        if (CurrentStep.Mode == CardSequenceStepMode.EventOnly
                            || (!presentationLocked && presenter != null && !presenter.IsPresenting))
                        {
                            ActivateCurrentStep();
                        }
                    }
                    break;

                case CardSequenceState.WaitingForCompletion:
                    TryCompleteCurrentStep();
                    break;
            }
        }

        public void BeginSequence()
        {
            if (sequence == null)
            {
                Debug.LogError("[CardSequenceRunner] No CardSequenceDefinition is assigned.", this);
                SetState(CardSequenceState.Stopped);
                return;
            }

            if (sequence.StepCount == 0)
            {
                Debug.LogError($"[CardSequenceRunner] Sequence '{sequence.StableId}' has no steps.", sequence);
                CompleteSequence();
                return;
            }

            blackboard ??= GetComponent<StoryBlackboard>();
            presenter ??= GetComponent<CardDeckPresenter>();

            if (resetBlackboardOnStart && blackboard != null)
            {
                blackboard.ResetToDefaults();
            }

            currentStepIndex = -1;
            drawCount = 0;
            activeCard = null;
            currentStepDrawIndex = -1;
            externalAdvanceQueued = false;
            externalAdvanceStepIndex = -1;
            presentationLocked = false;
            presenter?.ResetPresentation(sequence.VisualDeckSize);
            SequenceStarted?.Invoke(sequence);
            EnterStep(0);
        }

        public void RestartSequence()
        {
            BeginSequence();
        }

        public void StopSequence()
        {
            currentStepIndex = -1;
            presentationLocked = false;
            SetState(CardSequenceState.Stopped);
        }

        public bool RequestPlayerDraw()
        {
            return CanPlayerDraw && ActivateCurrentStep();
        }

        public bool RequestExternalAdvance()
        {
            if (!CanExternallyAdvance)
            {
                return false;
            }

            externalAdvanceQueued = true;
            externalAdvanceStepIndex = currentStepIndex;
            return true;
        }

        public bool SetPresenter(CardDeckPresenter nextPresenter, bool resetPresentation)
        {
            if (nextPresenter == null || State == CardSequenceState.Activating)
            {
                return false;
            }

            presenter = nextPresenter;
            if (resetPresentation && sequence != null)
            {
                presenter.ResetPresentation(Mathf.Max(1, sequence.VisualDeckSize - drawCount));
            }

            return true;
        }

        public void DebugForceCompleteCurrentStep()
        {
#if UNITY_EDITOR
            if (CurrentStep != null
                && State != CardSequenceState.Stopped
                && State != CardSequenceState.Completed
                && State != CardSequenceState.Activating)
            {
                CompleteCurrentStep();
            }
#endif
        }

        private void EnterStep(int stepIndex)
        {
            CardSequenceStep step = sequence.GetStep(stepIndex);
            if (step == null)
            {
                CompleteSequence();
                return;
            }

            currentStepIndex = stepIndex;
            activeCard = null;
            currentStepDrawIndex = -1;
            externalAdvanceQueued = false;
            externalAdvanceStepIndex = -1;
            availabilityNotBefore = Time.time + step.ReadyDelay;
            SetState(CardSequenceState.WaitingForAvailability);
            StepEntered?.Invoke(step, stepIndex);
            EmitSignals(step.EnterSignals, StorySignalPhase.StepEntered, -1);
            TryBecomeReady();
        }

        private void TryBecomeReady()
        {
            CardSequenceStep step = CurrentStep;
            if (step == null || Time.time < availabilityNotBefore)
            {
                return;
            }

            if (step.DrawAvailability != null && !step.DrawAvailability.Evaluate(blackboard))
            {
                return;
            }

            SetState(CardSequenceState.ReadyForActivation);
        }

        private bool ActivateCurrentStep()
        {
            CardSequenceStep step = CurrentStep;
            if (State != CardSequenceState.ReadyForActivation || step == null)
            {
                return false;
            }

            if (step.DrawsCard && (presentationLocked || presenter == null || presenter.IsPresenting))
            {
                return false;
            }

            SetState(CardSequenceState.Activating);
            int activationDrawIndex = step.DrawsCard ? drawCount : -1;
            activeCard = step.DrawsCard ? step.ResolveCard(blackboard) : null;
            currentStepDrawIndex = activationDrawIndex;
            EmitSignals(step.ActivationSignals, StorySignalPhase.StepActivated, activationDrawIndex, activeCard);

            if (!step.DrawsCard)
            {
                HandleStepRevealed(null, -1);
                return true;
            }

            if (presenter == null)
            {
                Debug.LogError("[CardSequenceRunner] A card step cannot run without CardDeckPresenter.", this);
                SetState(CardSequenceState.Stopped);
                return false;
            }

            if (activeCard == null)
            {
                Debug.LogError($"[CardSequenceRunner] Step '{step.StepId}' resolved to no card.", sequence);
                SetState(CardSequenceState.Stopped);
                return false;
            }

            int drawIndex = drawCount;
            CardDefinition cardDefinition = activeCard;
            presentationLocked = true;
            if (!presenter.PresentCard(
                    cardDefinition,
                    drawIndex,
                    card => HandleStepRevealed(card, drawIndex),
                    HandlePresentationFinished))
            {
                presentationLocked = false;
                Debug.LogError("[CardSequenceRunner] CardDeckPresenter rejected the draw request.", presenter);
                SetState(CardSequenceState.Stopped);
                return false;
            }

            drawCount++;
            CardDrawStarted?.Invoke(cardDefinition, drawIndex);
            return true;
        }

        private void HandlePresentationFinished()
        {
            presentationLocked = false;
        }

        private void HandleStepRevealed(GameObject cardObject, int drawIndex)
        {
            if (State != CardSequenceState.Activating || CurrentStep == null)
            {
                return;
            }

            CardSequenceStep step = CurrentStep;
            EmitSignals(step.RevealSignals, StorySignalPhase.CardRevealed, drawIndex, activeCard);
            CardRevealed?.Invoke(activeCard, cardObject, drawIndex);
            completionNotBefore = Time.time + step.CompletionDelay;
            SetState(CardSequenceState.WaitingForCompletion);
        }

        private void TryCompleteCurrentStep()
        {
            CardSequenceStep step = CurrentStep;
            if (step == null || Time.time < completionNotBefore)
            {
                return;
            }

            if (step.CompletionConditions != null && !step.CompletionConditions.Evaluate(blackboard))
            {
                return;
            }

            CompleteCurrentStep();
        }

        private void CompleteCurrentStep()
        {
            CardSequenceStep step = CurrentStep;
            if (step == null)
            {
                CompleteSequence();
                return;
            }

            int completedIndex = currentStepIndex;
            EmitSignals(step.CompleteSignals, StorySignalPhase.StepCompleted, currentStepDrawIndex, activeCard);
            StepCompleted?.Invoke(step, completedIndex);

            int nextStepIndex = ResolveNextStepIndex(step, completedIndex);
            if (nextStepIndex < 0)
            {
                CompleteSequence();
            }
            else
            {
                EnterStep(nextStepIndex);
            }
        }

        private int ResolveNextStepIndex(CardSequenceStep step, int completedIndex)
        {
            foreach (CardSequenceTransition transition in step.Transitions)
            {
                if (transition == null || (transition.Conditions != null && !transition.Conditions.Evaluate(blackboard)))
                {
                    continue;
                }

                if (transition.FinishSequence)
                {
                    return -1;
                }

                if (sequence.TryGetStepIndex(transition.TargetStepId, out int targetIndex))
                {
                    return targetIndex;
                }

                Debug.LogError(
                    $"[CardSequenceRunner] Step '{step.StepId}' targets missing step '{transition.TargetStepId}'.",
                    sequence);
                return -1;
            }

            int sequentialIndex = completedIndex + 1;
            return sequentialIndex < sequence.StepCount ? sequentialIndex : -1;
        }

        private void CompleteSequence()
        {
            externalAdvanceQueued = false;
            externalAdvanceStepIndex = -1;
            presentationLocked = false;
            SetState(CardSequenceState.Completed);
            SequenceCompleted?.Invoke(sequence);
        }

        private void TryProcessExternalAdvance()
        {
            if (externalAdvanceStepIndex != currentStepIndex || !CanExternallyAdvance)
            {
                externalAdvanceQueued = false;
                externalAdvanceStepIndex = -1;
                return;
            }

            CardSequenceStep step = CurrentStep;
            if (step?.CompletionConditions != null && !step.CompletionConditions.Evaluate(blackboard))
            {
                return;
            }

            externalAdvanceQueued = false;
            externalAdvanceStepIndex = -1;
            CompleteCurrentStep();
        }

        private void EmitSignals(
            IReadOnlyList<StorySignal> signals,
            StorySignalPhase phase,
            int drawIndex,
            CardDefinition signalCard = null)
        {
            if (signals == null || signals.Count == 0)
            {
                return;
            }

            StorySignalContext context = new StorySignalContext(
                this,
                sequence,
                CurrentStep,
                signalCard,
                phase,
                drawIndex);
            foreach (StorySignal signal in signals)
            {
                signal?.Raise(context);
            }
        }

        private void SetState(CardSequenceState newState)
        {
            if (State == newState)
            {
                return;
            }

            State = newState;
            StateChanged?.Invoke(newState);
        }

        private void OnValidate()
        {
            blackboard ??= GetComponent<StoryBlackboard>();
            presenter ??= GetComponent<CardDeckPresenter>();
        }
    }
}
