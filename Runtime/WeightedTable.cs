namespace UnityEditorExtras.Runtime
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    [Serializable]
    public sealed class WeightedTable<T>
    {
        public List<WeightedTableEntry<T>> entries = new List<WeightedTableEntry<T>>();

        public int Count => entries.Count;

        public float TotalWeight
        {
            get
            {
                ValidateEntries();

                float total = 0f;
                for (int i = 0; i < entries.Count; i++)
                    total += entries[i].weight;

                return total;
            }
        }

        public T Roll()
        {
            return Pick(UnityEngine.Random.value);
        }

        public T Pick(float normalizedRoll)
        {
            return entries[PickIndex(normalizedRoll)].value;
        }

        public int PickIndex(float normalizedRoll)
        {
            ValidateNormalized(normalizedRoll);
            float totalWeight = TotalWeight;
            float targetWeight = normalizedRoll * totalWeight;

            if (normalizedRoll == 1f)
                return entries.Count - 1;

            float cumulativeWeight = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                cumulativeWeight += entries[i].weight;
                if (targetWeight < cumulativeWeight)
                    return i;
            }

            throw new InvalidOperationException(
                $"WeightedTable roll {normalizedRoll} failed to resolve inside total weight {totalWeight}.");
        }

        public float GetProbability(int index)
        {
            ValidateIndex(index);
            return entries[index].weight / TotalWeight;
        }

        public void Validate()
        {
            _ = TotalWeight;
        }

        private void ValidateEntries()
        {
            if (entries == null)
                throw new InvalidOperationException("WeightedTable entries must be assigned.");

            if (entries.Count == 0)
                throw new InvalidOperationException("WeightedTable requires at least one entry.");

            for (int i = 0; i < entries.Count; i++)
            {
                WeightedTableEntry<T> entry = entries[i];
                if (entry == null)
                    throw new InvalidOperationException($"WeightedTable entry {i} is null.");

                if (float.IsNaN(entry.weight))
                    throw new InvalidOperationException($"WeightedTable entry {i} weight cannot be NaN.");

                if (entry.weight <= 0f)
                    throw new InvalidOperationException($"WeightedTable entry {i} weight must be greater than zero.");
            }
        }

        private void ValidateIndex(int index)
        {
            ValidateEntries();

            if (index < 0 || index >= entries.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    index,
                    $"WeightedTable index must be between 0 and {entries.Count - 1}.");
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
    public sealed class WeightedTableEntry<T>
    {
        public T value;
        [Min(0.0001f)] public float weight = 1f;
    }
}
