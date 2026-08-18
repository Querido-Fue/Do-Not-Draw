using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoNotDraw.Narrative
{
    public enum CardSequenceStepMode
    {
        PlayerDraw,
        AutomaticDraw,
        EventOnly
    }

    [Serializable]
    public sealed class CardSequenceTransition
    {
        [SerializeField] private bool finishSequence;
        [SerializeField] private string targetStepId;
        [SerializeField] private StoryConditionGroup conditions = new StoryConditionGroup();

        public bool FinishSequence => finishSequence;
        public string TargetStepId => targetStepId ?? string.Empty;
        public StoryConditionGroup Conditions => conditions;
        public bool IsDefault => conditions == null || conditions.IsEmpty;

        internal void Normalize()
        {
            targetStepId = targetStepId?.Trim() ?? string.Empty;
            conditions ??= new StoryConditionGroup();
        }
    }

    [Serializable]
    public sealed class CardSequenceStep
    {
        [SerializeField] private string stepId = "step.unassigned";
        [SerializeField] private string editorLabel;
        [SerializeField] private CardSequenceStepMode mode = CardSequenceStepMode.PlayerDraw;
        [SerializeField] private CardDefinition card;

        [Header("Availability")]
        [SerializeField, Min(0f)] private float readyDelay;
        [SerializeField] private StoryConditionGroup drawAvailability = new StoryConditionGroup();

        [Header("Completion")]
        [SerializeField, Min(0f)] private float completionDelay;
        [SerializeField] private StoryConditionGroup completionConditions = new StoryConditionGroup();

        [Header("Signals")]
        [SerializeField] private List<StorySignal> enterSignals = new List<StorySignal>();
        [SerializeField] private List<StorySignal> activationSignals = new List<StorySignal>();
        [SerializeField] private List<StorySignal> revealSignals = new List<StorySignal>();
        [SerializeField] private List<StorySignal> completeSignals = new List<StorySignal>();

        [Header("Branching")]
        [SerializeField] private List<CardSequenceTransition> transitions = new List<CardSequenceTransition>();

        public string StepId => stepId;
        public string EditorLabel => string.IsNullOrWhiteSpace(editorLabel) ? stepId : editorLabel;
        public CardSequenceStepMode Mode => mode;
        public CardDefinition Card => card;
        public float ReadyDelay => readyDelay;
        public StoryConditionGroup DrawAvailability => drawAvailability;
        public float CompletionDelay => completionDelay;
        public StoryConditionGroup CompletionConditions => completionConditions;
        public IReadOnlyList<StorySignal> EnterSignals => enterSignals != null
            ? (IReadOnlyList<StorySignal>)enterSignals
            : Array.Empty<StorySignal>();
        public IReadOnlyList<StorySignal> ActivationSignals => activationSignals != null
            ? (IReadOnlyList<StorySignal>)activationSignals
            : Array.Empty<StorySignal>();
        public IReadOnlyList<StorySignal> RevealSignals => revealSignals != null
            ? (IReadOnlyList<StorySignal>)revealSignals
            : Array.Empty<StorySignal>();
        public IReadOnlyList<StorySignal> CompleteSignals => completeSignals != null
            ? (IReadOnlyList<StorySignal>)completeSignals
            : Array.Empty<StorySignal>();
        public IReadOnlyList<CardSequenceTransition> Transitions => transitions != null
            ? (IReadOnlyList<CardSequenceTransition>)transitions
            : Array.Empty<CardSequenceTransition>();
        public bool DrawsCard => mode != CardSequenceStepMode.EventOnly;

        internal void Normalize()
        {
            stepId = string.IsNullOrWhiteSpace(stepId) ? string.Empty : stepId.Trim();
            editorLabel = editorLabel?.Trim() ?? string.Empty;
            readyDelay = Mathf.Max(0f, readyDelay);
            completionDelay = Mathf.Max(0f, completionDelay);
            drawAvailability ??= new StoryConditionGroup();
            completionConditions ??= new StoryConditionGroup();
            enterSignals ??= new List<StorySignal>();
            activationSignals ??= new List<StorySignal>();
            revealSignals ??= new List<StorySignal>();
            completeSignals ??= new List<StorySignal>();
            transitions ??= new List<CardSequenceTransition>();

            foreach (CardSequenceTransition transition in transitions)
            {
                transition?.Normalize();
            }
        }
    }

    [CreateAssetMenu(fileName = "CardSequence", menuName = "Do Not Draw/Narrative/Card Sequence")]
    public sealed class CardSequenceDefinition : ScriptableObject
    {
        [SerializeField] private string stableId = "sequence.unassigned";
        [SerializeField, TextArea] private string description;
        [SerializeField, Min(0)] private int initialDeckSize;
        [SerializeField] private List<CardSequenceStep> steps = new List<CardSequenceStep>();

        public string StableId => stableId;
        public string Description => description;
        public IReadOnlyList<CardSequenceStep> Steps => steps != null
            ? (IReadOnlyList<CardSequenceStep>)steps
            : Array.Empty<CardSequenceStep>();
        public int StepCount => steps?.Count ?? 0;

        public int VisualDeckSize
        {
            get
            {
                if (initialDeckSize > 0)
                {
                    return initialDeckSize;
                }

                int cardCount = 0;
                foreach (CardSequenceStep step in Steps)
                {
                    if (step != null && step.DrawsCard)
                    {
                        cardCount++;
                    }
                }

                return Mathf.Max(1, cardCount);
            }
        }

        public CardSequenceStep GetStep(int index)
        {
            return index >= 0 && index < StepCount ? steps[index] : null;
        }

        public bool TryGetStepIndex(string stepId, out int index)
        {
            if (!string.IsNullOrWhiteSpace(stepId))
            {
                for (int candidateIndex = 0; candidateIndex < StepCount; candidateIndex++)
                {
                    CardSequenceStep step = steps[candidateIndex];
                    if (step != null && string.Equals(step.StepId, stepId, StringComparison.Ordinal))
                    {
                        index = candidateIndex;
                        return true;
                    }
                }
            }

            index = -1;
            return false;
        }

        private void OnValidate()
        {
            stableId = string.IsNullOrWhiteSpace(stableId) ? string.Empty : stableId.Trim();
            initialDeckSize = Mathf.Max(0, initialDeckSize);
            steps ??= new List<CardSequenceStep>();

            foreach (CardSequenceStep step in steps)
            {
                step?.Normalize();
            }
        }
    }
}
