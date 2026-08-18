using UnityEngine;
using UnityEngine.Events;

namespace DoNotDraw.Narrative
{
    public sealed class StorySignalListener : MonoBehaviour
    {
        [SerializeField] private StorySignal signal;
        [SerializeField] private UnityEvent response = new UnityEvent();

        public StorySignal Signal => signal;
        public StorySignalContext LastContext { get; private set; }

        private void OnEnable()
        {
            if (signal != null)
            {
                signal.Raised += HandleSignal;
            }
        }

        private void OnDisable()
        {
            if (signal != null)
            {
                signal.Raised -= HandleSignal;
            }
        }

        private void HandleSignal(StorySignalContext context)
        {
            LastContext = context;
            response?.Invoke();
        }
    }
}
