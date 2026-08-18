using UnityEngine;

namespace DoNotDraw.Narrative
{
    public sealed class StoryFactSetter : MonoBehaviour
    {
        [SerializeField] private StoryBlackboard blackboard;
        [SerializeField] private StoryFact fact;
        [SerializeField] private bool boolValue;
        [SerializeField] private int intValue;
        [SerializeField] private float floatValue;
        [SerializeField] private string stringValue;

        public void Apply()
        {
            if (blackboard == null || fact == null)
            {
                Debug.LogError("[StoryFactSetter] Blackboard and StoryFact must both be assigned.", this);
                return;
            }

            switch (fact.FactType)
            {
                case StoryFactType.Boolean:
                    blackboard.SetBool(fact, boolValue);
                    break;
                case StoryFactType.Integer:
                    blackboard.SetInt(fact, intValue);
                    break;
                case StoryFactType.Float:
                    blackboard.SetFloat(fact, floatValue);
                    break;
                case StoryFactType.String:
                    blackboard.SetString(fact, stringValue);
                    break;
            }
        }

        private void OnValidate()
        {
            blackboard ??= GetComponentInParent<StoryBlackboard>();
            stringValue ??= string.Empty;
        }
    }
}
