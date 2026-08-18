using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoNotDraw.Narrative
{
    public enum StoryComparison
    {
        Equals,
        NotEquals,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual
    }

    public enum StoryConditionMatchMode
    {
        All,
        Any
    }

    [Serializable]
    public sealed class StoryCondition
    {
        [SerializeField] private StoryFact fact;
        [SerializeField] private StoryComparison comparison;
        [SerializeField] private bool boolValue;
        [SerializeField] private int intValue;
        [SerializeField] private float floatValue;
        [SerializeField] private string stringValue;

        public StoryFact Fact => fact;
        public StoryComparison Comparison => comparison;

        public bool IsComparisonSupported
        {
            get
            {
                if (fact == null)
                {
                    return false;
                }

                return fact.FactType switch
                {
                    StoryFactType.Boolean => comparison is StoryComparison.Equals or StoryComparison.NotEquals,
                    StoryFactType.String => comparison is StoryComparison.Equals or StoryComparison.NotEquals,
                    StoryFactType.Integer => true,
                    StoryFactType.Float => true,
                    _ => false
                };
            }
        }

        public bool Evaluate(StoryBlackboard blackboard)
        {
            if (blackboard == null || fact == null || !IsComparisonSupported)
            {
                return false;
            }

            StoryValue currentValue = blackboard.GetValue(fact);
            return fact.FactType switch
            {
                StoryFactType.Boolean => CompareBoolean(currentValue.BoolValue, boolValue),
                StoryFactType.Integer => CompareInteger(currentValue.IntValue, intValue),
                StoryFactType.Float => CompareNumber(currentValue.FloatValue, floatValue),
                StoryFactType.String => CompareString(currentValue.StringValue, stringValue ?? string.Empty),
                _ => false
            };
        }

        private bool CompareBoolean(bool current, bool expected)
        {
            return comparison == StoryComparison.Equals ? current == expected : current != expected;
        }

        private bool CompareString(string current, string expected)
        {
            bool equal = string.Equals(current, expected, StringComparison.Ordinal);
            return comparison == StoryComparison.Equals ? equal : !equal;
        }

        private bool CompareInteger(int current, int expected)
        {
            return comparison switch
            {
                StoryComparison.Equals => current == expected,
                StoryComparison.NotEquals => current != expected,
                StoryComparison.Greater => current > expected,
                StoryComparison.GreaterOrEqual => current >= expected,
                StoryComparison.Less => current < expected,
                StoryComparison.LessOrEqual => current <= expected,
                _ => false
            };
        }

        private bool CompareNumber(float current, float expected)
        {
            return comparison switch
            {
                StoryComparison.Equals => Mathf.Approximately(current, expected),
                StoryComparison.NotEquals => !Mathf.Approximately(current, expected),
                StoryComparison.Greater => current > expected,
                StoryComparison.GreaterOrEqual => current > expected || Mathf.Approximately(current, expected),
                StoryComparison.Less => current < expected,
                StoryComparison.LessOrEqual => current < expected || Mathf.Approximately(current, expected),
                _ => false
            };
        }
    }

    [Serializable]
    public sealed class StoryConditionGroup
    {
        [SerializeField] private StoryConditionMatchMode matchMode = StoryConditionMatchMode.All;
        [SerializeField] private List<StoryCondition> conditions = new List<StoryCondition>();

        public StoryConditionMatchMode MatchMode => matchMode;
        public IReadOnlyList<StoryCondition> Conditions => conditions != null
            ? (IReadOnlyList<StoryCondition>)conditions
            : Array.Empty<StoryCondition>();
        public bool IsEmpty => conditions == null || conditions.Count == 0;

        public bool Evaluate(StoryBlackboard blackboard)
        {
            if (IsEmpty)
            {
                return true;
            }

            if (matchMode == StoryConditionMatchMode.Any)
            {
                foreach (StoryCondition condition in Conditions)
                {
                    if (condition != null && condition.Evaluate(blackboard))
                    {
                        return true;
                    }
                }

                return false;
            }

            foreach (StoryCondition condition in Conditions)
            {
                if (condition == null || !condition.Evaluate(blackboard))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
