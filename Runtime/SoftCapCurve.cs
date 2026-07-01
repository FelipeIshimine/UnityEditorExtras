namespace UnityEditorExtras.Runtime
{
    using System;
    using UnityEngine;

    [Serializable]
    public sealed class SoftCapCurve
    {
        public float startValue;
        public float linearIncreasePerStep = 1f;
        [Min(0)] public int softCapStartStep = 10;
        public float softCapValue = 100f;
        [Min(0.0001f)] public float softnessInSteps = 10f;
        public BalanceCurveRoundingMode roundingMode = BalanceCurveRoundingMode.None;

        public float EvaluateStep(int stepIndex)
        {
            if (stepIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(stepIndex), stepIndex, "Step index must be zero or greater.");

            return Evaluate(stepIndex);
        }

        public float Evaluate(float step)
        {
            return ApplyRounding(EvaluateRaw(step));
        }

        public float EvaluateRaw(float step)
        {
            Validate();

            if (float.IsNaN(step))
                throw new ArgumentException("Step cannot be NaN.", nameof(step));

            if (step < 0f)
                throw new ArgumentOutOfRangeException(nameof(step), step, "Step must be zero or greater.");

            float valueAtSoftCapStart = LinearValue(softCapStartStep);
            if (step <= softCapStartStep)
                return LinearValue(step);

            float overSoftCapSteps = step - softCapStartStep;
            float easedProgress = 1f - Mathf.Exp(-overSoftCapSteps / softnessInSteps);
            return Mathf.LerpUnclamped(valueAtSoftCapStart, softCapValue, easedProgress);
        }

        public int EvaluateStepAsInt(int stepIndex)
        {
            return Mathf.RoundToInt(EvaluateStep(stepIndex));
        }

        public float LinearValue(float step)
        {
            return startValue + linearIncreasePerStep * step;
        }

        public void Validate()
        {
            if (float.IsNaN(startValue))
                throw new InvalidOperationException("SoftCapCurve startValue cannot be NaN.");

            if (float.IsNaN(linearIncreasePerStep))
                throw new InvalidOperationException("SoftCapCurve linearIncreasePerStep cannot be NaN.");

            if (linearIncreasePerStep <= 0f)
                throw new InvalidOperationException("SoftCapCurve linearIncreasePerStep must be greater than zero.");

            if (softCapStartStep < 0)
                throw new InvalidOperationException("SoftCapCurve softCapStartStep must be zero or greater.");

            if (float.IsNaN(softCapValue))
                throw new InvalidOperationException("SoftCapCurve softCapValue cannot be NaN.");

            if (float.IsNaN(softnessInSteps))
                throw new InvalidOperationException("SoftCapCurve softnessInSteps cannot be NaN.");

            if (softnessInSteps <= 0f)
                throw new InvalidOperationException("SoftCapCurve softnessInSteps must be greater than zero.");

            float valueAtSoftCapStart = LinearValue(softCapStartStep);
            if (softCapValue <= valueAtSoftCapStart)
            {
                throw new InvalidOperationException(
                    $"SoftCapCurve softCapValue {softCapValue} must be greater than value at soft cap start {valueAtSoftCapStart}.");
            }
        }

        private float ApplyRounding(float value)
        {
            return roundingMode switch
            {
                BalanceCurveRoundingMode.None => value,
                BalanceCurveRoundingMode.Floor => Mathf.Floor(value),
                BalanceCurveRoundingMode.Round => Mathf.Round(value),
                BalanceCurveRoundingMode.Ceil => Mathf.Ceil(value),
                _ => throw new InvalidOperationException($"Unsupported soft-cap curve rounding mode '{roundingMode}'.")
            };
        }
    }
}
