namespace UnityEditorExtras.Runtime
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    [Serializable]
    public sealed class BudgetBreakdown<T>
    {
        public const float RequiredTotalPercent = 100f;
        public const float TotalTolerance = 0.001f;

        public List<BudgetBreakdownEntry<T>> entries = new List<BudgetBreakdownEntry<T>>();

        public int Count => entries.Count;

        public float TotalPercent
        {
            get
            {
                ValidateEntriesOnly();

                float total = 0f;
                for (int i = 0; i < entries.Count; i++)
                    total += entries[i].percent;

                return total;
            }
        }

        public float RemainingPercent => RequiredTotalPercent - TotalPercent;

        public T Pick(float normalizedRoll)
        {
            return entries[PickIndex(normalizedRoll)].value;
        }

        public int PickIndex(float normalizedRoll)
        {
            Validate();
            ValidateNormalized(normalizedRoll);

            if (normalizedRoll == 1f)
                return entries.Count - 1;

            float targetPercent = normalizedRoll * RequiredTotalPercent;
            float cumulativePercent = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                cumulativePercent += entries[i].percent;
                if (targetPercent < cumulativePercent)
                    return i;
            }

            throw new InvalidOperationException(
                $"BudgetBreakdown roll {normalizedRoll} failed to resolve inside total percent {TotalPercent}.");
        }

        public float GetPercent(int index)
        {
            ValidateIndex(index);
            return entries[index].percent;
        }

        public float GetFraction(int index)
        {
            return GetPercent(index) / RequiredTotalPercent;
        }

        public void Validate()
        {
            float total = TotalPercent;
            if (Mathf.Abs(total - RequiredTotalPercent) > TotalTolerance)
            {
                throw new InvalidOperationException(
                    $"BudgetBreakdown total must be {RequiredTotalPercent:0.###}%, got {total:0.###}%.");
            }
        }

        private void ValidateEntriesOnly()
        {
            if (entries == null)
                throw new InvalidOperationException("BudgetBreakdown entries must be assigned.");

            if (entries.Count == 0)
                throw new InvalidOperationException("BudgetBreakdown requires at least one entry.");

            for (int i = 0; i < entries.Count; i++)
            {
                BudgetBreakdownEntry<T> entry = entries[i];
                if (entry == null)
                    throw new InvalidOperationException($"BudgetBreakdown entry {i} is null.");

                if (float.IsNaN(entry.percent))
                    throw new InvalidOperationException($"BudgetBreakdown entry {i} percent cannot be NaN.");

                if (entry.percent <= 0f)
                    throw new InvalidOperationException($"BudgetBreakdown entry {i} percent must be greater than zero.");
            }
        }

        private void ValidateIndex(int index)
        {
            ValidateEntriesOnly();

            if (index < 0 || index >= entries.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    index,
                    $"BudgetBreakdown index must be between 0 and {entries.Count - 1}.");
            }
        }

        private static void ValidateNormalized(float normalizedRoll)
        {
            if (float.IsNaN(normalizedRoll))
                throw new ArgumentException("Normalized roll cannot be NaN.", nameof(normalizedRoll));

            if (normalizedRoll < 0f || normalizedRoll > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(normalizedRoll),
                    normalizedRoll,
                    "Normalized roll must be between 0 and 1.");
            }
        }
    }

    [Serializable]
    public sealed class BudgetBreakdownEntry<T>
    {
        public T value;
        [Range(0f, 100f)] public float percent = 1f;
    }
}
