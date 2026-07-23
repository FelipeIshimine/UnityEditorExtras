namespace UnityEditorExtras.Runtime
{
    using System;
    using UnityEngine;

    [Serializable]
    public sealed class BalanceCurve
    {
        [Tooltip("Value returned by step 0 when the curve starts at 0.")]
        public float minValue = 1f;
        [Tooltip("Value returned by the final step when the curve ends at 1.")]
        public float maxValue = 100f;
        [Min(2)]
        [Tooltip("Number of discrete steps. Step 0 is the first value, and the last step maps to normalized 1.")]
        public int steps = 5;
        [Tooltip("Maps normalized progress to the value range between Min Value and Max Value.")]
        public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Tooltip("Optional rounding applied by EvaluateStep and EvaluateNormalized. Raw evaluation APIs ignore this.")]
        public BalanceCurveRoundingMode roundingMode = BalanceCurveRoundingMode.None;
        [Min(0.0001f)]
        [Tooltip("Rounding increment used by Floor, Round, and Ceil. Use 5 to round to the nearest 5, 10 to round to the nearest 10, or 0.25 for quarter steps.")]
        public float roundingIncrement = 1f;

        public int LastStepIndex
        {
            get
            {
                ValidateSteps();
                return steps - 1;
            }
        }

        public float EvaluateStep(int stepIndex)
        {
            return ApplyRounding(EvaluateStepRaw(stepIndex));
        }

        public float EvaluateStepRaw(int stepIndex)
        {
            return EvaluateNormalizedRaw(StepToNormalized(stepIndex));
        }

        public int EvaluateStepAsInt(int stepIndex)
        {
            return Mathf.RoundToInt(EvaluateStep(stepIndex));
        }

        public float EvaluateNormalized(float normalized)
        {
            return ApplyRounding(EvaluateNormalizedRaw(normalized));
        }

        public float EvaluateNormalizedRaw(float normalized)
        {
            ValidateCurve();
            ValidateNormalized(normalized);
            return Mathf.LerpUnclamped(minValue, maxValue, curve.Evaluate(normalized));
        }

        public int EvaluateNormalizedAsInt(float normalized)
        {
            return Mathf.RoundToInt(EvaluateNormalized(normalized));
        }

        public float StepToNormalized(int stepIndex)
        {
            ValidateSteps();

            if (stepIndex < 0 || stepIndex >= steps)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stepIndex),
                    stepIndex,
                    $"Step index must be between 0 and {steps - 1}.");
            }

            return (float)stepIndex / (steps - 1);
        }

        public int NormalizedToNearestStep(float normalized)
        {
            ValidateSteps();
            ValidateNormalized(normalized);
            return Mathf.RoundToInt(normalized * (steps - 1));
        }

        /// <summary>
        /// Precomputes every step value into a <see cref="Baked"/> snapshot, so a hot path can read the
        /// curve with an array lookup instead of <see cref="AnimationCurve.Evaluate"/> — which is slow
        /// under IL2CPP/WebGL and cost ~7 ms/tick when evaluated per entity. Bake once whenever the
        /// authored values change (typically at load / match start), then reuse the snapshot.
        /// </summary>
        public Baked Bake() => new Baked(this);

        /// <summary>
        /// An immutable, evaluate-free snapshot of a <see cref="BalanceCurve"/>'s discrete step values.
        /// Reads are array lookups (no <c>AnimationCurve.Evaluate</c>). Rounding and step spacing from the
        /// source curve are already applied. A <c>default</c> value is un-baked — check <see cref="IsBaked"/>.
        /// </summary>
        public readonly struct Baked
        {
            private readonly float[] steps;

            internal Baked(BalanceCurve source)
            {
                if (source == null)
                    throw new ArgumentNullException(nameof(source));
                source.ValidateSteps();
                steps = new float[source.steps];
                for (int i = 0; i < source.steps; i++)
                    steps[i] = source.EvaluateStep(i);
            }

            public bool IsBaked => steps != null;
            public int StepCount => steps.Length;

            /// <summary>
            /// Value at <paramref name="stepIndex"/>, <b>clamped</b> to the authored step range. This is the
            /// array-lookup equivalent of <c>EvaluateStep(Mathf.Clamp(stepIndex, 0, LastStepIndex))</c> — the
            /// clamp is built in because callers key on unbounded counts (e.g. army size).
            /// </summary>
            public float EvaluateStep(int stepIndex)
            {
                int i = stepIndex < 0 ? 0 : (stepIndex >= steps.Length ? steps.Length - 1 : stepIndex);
                return steps[i];
            }

            /// <summary>
            /// Continuous read across the baked step samples (linear interpolation between adjacent steps),
            /// approximating <see cref="BalanceCurve.EvaluateNormalized"/> at the curve's authored step
            /// resolution. <paramref name="normalized"/> is clamped to [0, 1].
            /// </summary>
            public float EvaluateNormalized(float normalized)
            {
                float clamped = normalized < 0f ? 0f : (normalized > 1f ? 1f : normalized);
                float scaled = clamped * (steps.Length - 1);
                int lower = (int)scaled;
                if (lower >= steps.Length - 1)
                    return steps[steps.Length - 1];
                return Mathf.LerpUnclamped(steps[lower], steps[lower + 1], scaled - lower);
            }
        }

        private float ApplyRounding(float value)
        {
            if (roundingMode != BalanceCurveRoundingMode.None && roundingIncrement <= 0f)
            {
                throw new InvalidOperationException(
                    $"BalanceCurve roundingIncrement must be greater than zero when rounding is enabled, got {roundingIncrement}.");
            }

            return roundingMode switch
            {
                BalanceCurveRoundingMode.None => value,
                BalanceCurveRoundingMode.Floor => Mathf.Floor(value / roundingIncrement) * roundingIncrement,
                BalanceCurveRoundingMode.Round => Mathf.Round(value / roundingIncrement) * roundingIncrement,
                BalanceCurveRoundingMode.Ceil => Mathf.Ceil(value / roundingIncrement) * roundingIncrement,
                _ => throw new InvalidOperationException($"Unsupported balance curve rounding mode '{roundingMode}'.")
            };
        }

        private void ValidateCurve()
        {
            if (curve == null)
                throw new InvalidOperationException("BalanceCurve curve must be assigned.");
        }

        private void ValidateSteps()
        {
            if (steps < 2)
                throw new InvalidOperationException("BalanceCurve steps must be at least 2.");
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

    public enum BalanceCurveRoundingMode
    {
        None,
        Floor,
        Round,
        Ceil
    }
}
