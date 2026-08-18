using System;
using UnityEngine;

namespace DoNotDraw.Narrative
{
    public enum StoryFactType
    {
        Boolean,
        Integer,
        Float,
        String
    }

    [Serializable]
    public struct StoryValue : IEquatable<StoryValue>
    {
        [SerializeField] private StoryFactType type;
        [SerializeField] private bool boolValue;
        [SerializeField] private int intValue;
        [SerializeField] private float floatValue;
        [SerializeField] private string stringValue;

        public StoryFactType Type => type;
        public bool BoolValue => boolValue;
        public int IntValue => intValue;
        public float FloatValue => floatValue;
        public string StringValue => stringValue ?? string.Empty;

        public static StoryValue FromBool(bool value)
        {
            return new StoryValue { type = StoryFactType.Boolean, boolValue = value };
        }

        public static StoryValue FromInt(int value)
        {
            return new StoryValue { type = StoryFactType.Integer, intValue = value };
        }

        public static StoryValue FromFloat(float value)
        {
            return new StoryValue { type = StoryFactType.Float, floatValue = value };
        }

        public static StoryValue FromString(string value)
        {
            return new StoryValue { type = StoryFactType.String, stringValue = value ?? string.Empty };
        }

        public bool Equals(StoryValue other)
        {
            if (type != other.type)
            {
                return false;
            }

            return type switch
            {
                StoryFactType.Boolean => boolValue == other.boolValue,
                StoryFactType.Integer => intValue == other.intValue,
                StoryFactType.Float => floatValue.Equals(other.floatValue),
                StoryFactType.String => string.Equals(StringValue, other.StringValue, StringComparison.Ordinal),
                _ => false
            };
        }

        public override bool Equals(object obj)
        {
            return obj is StoryValue other && Equals(other);
        }

        public override int GetHashCode()
        {
            return type switch
            {
                StoryFactType.Boolean => HashCode.Combine(type, boolValue),
                StoryFactType.Integer => HashCode.Combine(type, intValue),
                StoryFactType.Float => HashCode.Combine(type, floatValue),
                StoryFactType.String => HashCode.Combine(type, StringValue),
                _ => (int)type
            };
        }

        public override string ToString()
        {
            return type switch
            {
                StoryFactType.Boolean => boolValue.ToString(),
                StoryFactType.Integer => intValue.ToString(),
                StoryFactType.Float => floatValue.ToString("0.###"),
                StoryFactType.String => StringValue,
                _ => string.Empty
            };
        }
    }
}
