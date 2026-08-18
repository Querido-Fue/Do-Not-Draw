using UnityEngine;

namespace DoNotDraw.Narrative
{
    [CreateAssetMenu(fileName = "StoryFact", menuName = "Do Not Draw/Narrative/Story Fact")]
    public sealed class StoryFact : ScriptableObject
    {
        [SerializeField] private string stableId = "fact.unassigned";
        [SerializeField, TextArea] private string description;
        [SerializeField] private StoryFactType factType;
        [SerializeField] private bool defaultBool;
        [SerializeField] private int defaultInt;
        [SerializeField] private float defaultFloat;
        [SerializeField] private string defaultString;

        public string StableId => stableId;
        public string Description => description;
        public StoryFactType FactType => factType;

        public StoryValue DefaultValue => factType switch
        {
            StoryFactType.Boolean => StoryValue.FromBool(defaultBool),
            StoryFactType.Integer => StoryValue.FromInt(defaultInt),
            StoryFactType.Float => StoryValue.FromFloat(defaultFloat),
            StoryFactType.String => StoryValue.FromString(defaultString),
            _ => StoryValue.FromBool(false)
        };

        private void OnValidate()
        {
            stableId = string.IsNullOrWhiteSpace(stableId) ? string.Empty : stableId.Trim();
            defaultString ??= string.Empty;
        }
    }
}
