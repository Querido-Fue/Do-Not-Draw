using System;
using UnityEngine;

namespace DoNotDraw.Narrative
{
    public enum StorySignalPhase
    {
        StepEntered,
        StepActivated,
        CardRevealed,
        StepCompleted
    }

    public sealed class StorySignalContext
    {
        public StorySignalContext(
            CardSequenceRunner runner,
            CardSequenceDefinition sequence,
            CardSequenceStep step,
            CardDefinition card,
            StorySignalPhase phase,
            int drawIndex)
        {
            Runner = runner;
            Sequence = sequence;
            Step = step;
            Card = card;
            Phase = phase;
            DrawIndex = drawIndex;
        }

        public CardSequenceRunner Runner { get; }
        public CardSequenceDefinition Sequence { get; }
        public CardSequenceStep Step { get; }
        public CardDefinition Card { get; }
        public StorySignalPhase Phase { get; }
        public int DrawIndex { get; }
        public GameObject Source => Runner != null ? Runner.gameObject : null;
    }

    [CreateAssetMenu(fileName = "StorySignal", menuName = "Do Not Draw/Narrative/Story Signal")]
    public sealed class StorySignal : ScriptableObject
    {
        [SerializeField] private string stableId = "signal.unassigned";
        [SerializeField, TextArea] private string description;

        public string StableId => stableId;
        public string Description => description;

        public event Action<StorySignalContext> Raised;

        public void Raise(StorySignalContext context)
        {
            Action<StorySignalContext> handlers = Raised;
            if (handlers == null)
            {
                return;
            }

            foreach (Delegate subscriber in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<StorySignalContext>)subscriber).Invoke(context);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private void OnDisable()
        {
            Raised = null;
        }

        private void OnValidate()
        {
            stableId = string.IsNullOrWhiteSpace(stableId) ? string.Empty : stableId.Trim();
        }
    }
}
