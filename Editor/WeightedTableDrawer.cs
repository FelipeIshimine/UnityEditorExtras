using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorExtras.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(WeightedTable<>))]
public sealed class WeightedTableDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        SerializedProperty entriesProperty = RequiredRelativeProperty(property, nameof(WeightedTable<object>.entries));

        Foldout root = new Foldout
        {
            text = property.displayName,
            value = property.isExpanded
        };

        root.RegisterValueChangedCallback(evt =>
        {
            property.isExpanded = evt.newValue;
            property.serializedObject.ApplyModifiedProperties();
        });

        PropertyField entriesField = new PropertyField(entriesProperty, "Entries");
        Label summaryLabel = new Label();
        summaryLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        Button sampleButton = new Button();
        sampleButton.text = "Sample Roll";

        Label sampleLabel = new Label();
        VisualElement percentageRows = new VisualElement();

        root.Add(entriesField);
        root.Add(summaryLabel);
        root.Add(sampleButton);
        root.Add(sampleLabel);
        root.Add(percentageRows);

        void Refresh()
        {
            percentageRows.Clear();

            int count = entriesProperty.arraySize;
            if (count == 0)
            {
                summaryLabel.text = "Invalid: table requires at least one entry.";
                return;
            }

            float totalWeight = TotalWeight(entriesProperty);
            summaryLabel.text = $"Total weight: {totalWeight:0.###}";

            for (int i = 0; i < count; i++)
            {
                SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(i);
                SerializedProperty weightProperty = RequiredRelativeProperty(entryProperty, nameof(WeightedTableEntry<object>.weight));
                float probability = weightProperty.floatValue / totalWeight;
                percentageRows.Add(new Label($"{i}: weight {weightProperty.floatValue:0.###} | chance {probability:P2}"));
            }
        }

        sampleButton.clicked += () =>
        {
            float roll = UnityEngine.Random.value;
            int index = PickIndex(entriesProperty, roll);
            sampleLabel.text = $"Roll {roll:0.###} selected entry {index}.";
            Refresh();
        };

        root.TrackPropertyValue(entriesProperty, _ => Refresh());
        root.schedule.Execute(Refresh);

        return root;
    }

    private static float TotalWeight(SerializedProperty entriesProperty)
    {
        float totalWeight = 0f;
        for (int i = 0; i < entriesProperty.arraySize; i++)
        {
            SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(i);
            SerializedProperty weightProperty = RequiredRelativeProperty(entryProperty, nameof(WeightedTableEntry<object>.weight));
            float weight = weightProperty.floatValue;

            if (float.IsNaN(weight))
                throw new InvalidOperationException($"WeightedTable entry {i} weight cannot be NaN.");

            if (weight <= 0f)
                throw new InvalidOperationException($"WeightedTable entry {i} weight must be greater than zero.");

            totalWeight += weight;
        }

        if (totalWeight <= 0f)
            throw new InvalidOperationException("WeightedTable total weight must be greater than zero.");

        return totalWeight;
    }

    private static int PickIndex(SerializedProperty entriesProperty, float normalizedRoll)
    {
        if (entriesProperty.arraySize == 0)
            throw new InvalidOperationException("WeightedTable requires at least one entry.");

        float totalWeight = TotalWeight(entriesProperty);
        float targetWeight = normalizedRoll * totalWeight;

        if (normalizedRoll == 1f)
            return entriesProperty.arraySize - 1;

        float cumulativeWeight = 0f;
        for (int i = 0; i < entriesProperty.arraySize; i++)
        {
            SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(i);
            SerializedProperty weightProperty = RequiredRelativeProperty(entryProperty, nameof(WeightedTableEntry<object>.weight));
            cumulativeWeight += weightProperty.floatValue;

            if (targetWeight < cumulativeWeight)
                return i;
        }

        throw new InvalidOperationException(
            $"WeightedTable editor roll {normalizedRoll} failed to resolve inside total weight {totalWeight}.");
    }

    private static SerializedProperty RequiredRelativeProperty(SerializedProperty property, string propertyName)
    {
        SerializedProperty relativeProperty = property.FindPropertyRelative(propertyName);
        if (relativeProperty == null)
            throw new InvalidOperationException($"WeightedTable drawer requires serialized field '{propertyName}'.");

        return relativeProperty;
    }
}
