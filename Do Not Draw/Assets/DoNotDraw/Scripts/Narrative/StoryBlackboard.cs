using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoNotDraw.Narrative
{
    [Serializable]
    public sealed class StoryFactInitialValue
    {
        [SerializeField] private StoryFact fact;
        [SerializeField] private bool boolValue;
        [SerializeField] private int intValue;
        [SerializeField] private float floatValue;
        [SerializeField] private string stringValue;

        public StoryFact Fact => fact;

        public StoryValue GetValue()
        {
            if (fact == null)
            {
                return StoryValue.FromBool(false);
            }

            return fact.FactType switch
            {
                StoryFactType.Boolean => StoryValue.FromBool(boolValue),
                StoryFactType.Integer => StoryValue.FromInt(intValue),
                StoryFactType.Float => StoryValue.FromFloat(floatValue),
                StoryFactType.String => StoryValue.FromString(stringValue),
                _ => fact.DefaultValue
            };
        }
    }

    [DisallowMultipleComponent]
    public sealed class StoryBlackboard : MonoBehaviour
    {
        [SerializeField] private List<StoryFactInitialValue> initialValues = new List<StoryFactInitialValue>();

        private readonly Dictionary<StoryFact, StoryValue> runtimeValues = new Dictionary<StoryFact, StoryValue>();
        private bool initialized;

        public event Action<StoryFact, StoryValue> ValueChanged;

        public IReadOnlyList<StoryFactInitialValue> InitialValues => initialValues != null
            ? (IReadOnlyList<StoryFactInitialValue>)initialValues
            : Array.Empty<StoryFactInitialValue>();

        private void Awake()
        {
            ResetToDefaults();
        }

        public void ResetToDefaults()
        {
            runtimeValues.Clear();

            foreach (StoryFactInitialValue initialValue in InitialValues)
            {
                if (initialValue?.Fact != null)
                {
                    runtimeValues[initialValue.Fact] = initialValue.GetValue();
                }
            }

            initialized = true;
        }

        public StoryValue GetValue(StoryFact fact)
        {
            if (fact == null)
            {
                throw new ArgumentNullException(nameof(fact));
            }

            EnsureInitialized();
            return runtimeValues.TryGetValue(fact, out StoryValue value) ? value : fact.DefaultValue;
        }

        public bool SetValue(StoryFact fact, StoryValue value)
        {
            if (fact == null)
            {
                Debug.LogError("[StoryBlackboard] Cannot set a null fact.", this);
                return false;
            }

            if (fact.FactType != value.Type)
            {
                Debug.LogError(
                    $"[StoryBlackboard] Type mismatch for '{fact.StableId}': expected {fact.FactType}, received {value.Type}.",
                    this);
                return false;
            }

            EnsureInitialized();
            StoryValue previousValue = GetValue(fact);
            if (previousValue.Equals(value))
            {
                return true;
            }

            runtimeValues[fact] = value;
            ValueChanged?.Invoke(fact, value);
            return true;
        }

        public bool SetBool(StoryFact fact, bool value) => SetValue(fact, StoryValue.FromBool(value));
        public bool SetInt(StoryFact fact, int value) => SetValue(fact, StoryValue.FromInt(value));
        public bool SetFloat(StoryFact fact, float value) => SetValue(fact, StoryValue.FromFloat(value));
        public bool SetString(StoryFact fact, string value) => SetValue(fact, StoryValue.FromString(value));

        private void EnsureInitialized()
        {
            if (!initialized)
            {
                ResetToDefaults();
            }
        }

        private void OnValidate()
        {
            initialValues ??= new List<StoryFactInitialValue>();
        }
    }
}
