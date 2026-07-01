namespace UnityEditorExtras.Runtime
{
    using System;
    using UnityEngine;

    [Serializable]
    public sealed class MinMaxCurve
    {
        public float minValue;
        public float maxValue = 1f;
        public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public BalanceCurveRoundingMode roundingMode = BalanceCurveRoundingMode.None;

        public float RandomValue()
        {
            return Evaluate(UnityEngine.Random.value);
        }

        public float RandomValueRaw()
        {
            return EvaluateRaw(UnityEngine.Random.value);
        }

        public float Evaluate(float normalized)
        {
            return ApplyRounding(EvaluateRaw(normalized));
        }

        public float EvaluateRaw(float normalized)
        {
            Validate();
            ValidateNormalized(normalized);
            return Mathf.LerpUnclamped(minValue, maxValue, curve.Evaluate(normalized));
        }

        public int EvaluateAsInt(float normalized)
        {
            return Mathf.RoundToInt(Evaluate(normalized));
        }

        public void Validate()
        {
            if (maxValue < minValue)
                throw new InvalidOperationException($"MinMaxCurve maxValue {maxValue} must be greater than or equal to minValue {minValue}.");

            if (curve == null)
                throw new InvalidOperationException("MinMaxCurve curve must be assigned.");
        }

        private float ApplyRounding(float value)
        {
            return roundingMode switch
            {
                BalanceCurveRoundingMode.None => value,
                BalanceCurveRoundingMode.Floor => Mathf.Floor(value),
                BalanceCurveRoundingMode.Round => Mathf.Round(value),
                BalanceCurveRoundingMode.Ceil => Mathf.Ceil(value),
                _ => throw new InvalidOperationException($"Unsupported min-max curve rounding mode '{roundingMode}'.")
            };
        }

        private static void ValidateNormalized(float normalized)
        {
            if (float.IsNaN(normalized))
                throw new ArgumentException("Normalized value cannot be NaN.", nameof(normalized));

            if (normalized < 0f || normalized > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(normalized),
                    normalized,
                    "Normalized value must be between 0 and 1.");
            }
        }
    }
}
