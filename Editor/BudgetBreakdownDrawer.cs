using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorExtras.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(BudgetBreakdown<>))]
public sealed class BudgetBreakdownDrawer : PropertyDrawer
{
    private const float RequiredTotalPercent = 100f;
    private const float TotalTolerance = 0.001f;

    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        SerializedProperty entriesProperty = RequiredRelativeProperty(property, nameof(BudgetBreakdown<object>.entries));

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
        VisualElement rows = new VisualElement();

        root.Add(entriesField);
        root.Add(summaryLabel);
        root.Add(sampleButton);
        root.Add(sampleLabel);
        root.Add(rows);

        void Refresh()
        {
            rows.Clear();

            int count = entriesProperty.arraySize;
            if (count == 0)
            {
                summaryLabel.text = "Invalid: budget requires at least one entry.";
                return;
            }

            float totalPercent = TotalPercent(entriesProperty);
            float remainingPercent = RequiredTotalPercent - totalPercent;
            summaryLabel.text = $"Total: {totalPercent:0.###}% | Remaining: {remainingPercent:0.###}%";

            for (int i = 0; i < count; i++)
            {
                SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(i);
                SerializedProperty percentProperty = RequiredRelativeProperty(entryProperty, nameof(BudgetBreakdownEntry<object>.percent));
                rows.Add(new Label($"{i}: {percentProperty.floatValue:0.###}% | fraction {(percentProperty.floatValue / 100f):P2}"));
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

    private static float TotalPercent(SerializedProperty entriesProperty)
    {
        float totalPercent = 0f;
        for (int i = 0; i < entriesProperty.arraySize; i++)
        {
            SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(i);
            SerializedProperty percentProperty = RequiredRelativeProperty(entryProperty, nameof(BudgetBreakdownEntry<object>.percent));
            float percent = percentProperty.floatValue;

            if (float.IsNaN(percent))
                throw new InvalidOperationException($"BudgetBreakdown entry {i} percent cannot be NaN.");

            if (percent <= 0f)
                throw new InvalidOperationException($"BudgetBreakdown entry {i} percent must be greater than zero.");

            totalPercent += percent;
        }

        return totalPercent;
    }

    private static int PickIndex(SerializedProperty entriesProperty, float normalizedRoll)
    {
        if (entriesProperty.arraySize == 0)
            throw new InvalidOperationException("BudgetBreakdown requires at least one entry.");

        float totalPercent = TotalPercent(entriesProperty);
        if (Mathf.Abs(totalPercent - RequiredTotalPercent) > TotalTolerance)
        {
            throw new InvalidOperationException(
                $"BudgetBreakdown total must be {RequiredTotalPercent:0.###}%, got {totalPercent:0.###}%.");
        }

        if (normalizedRoll == 1f)
            return entriesProperty.arraySize - 1;

        float targetPercent = normalizedRoll * RequiredTotalPercent;
        float cumulativePercent = 0f;
        for (int i = 0; i < entriesProperty.arraySize; i++)
        {
            SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(i);
            SerializedProperty percentProperty = RequiredRelativeProperty(entryProperty, nameof(BudgetBreakdownEntry<object>.percent));
            cumulativePercent += percentProperty.floatValue;

            if (targetPercent < cumulativePercent)
                return i;
        }

        throw new InvalidOperationException(
            $"BudgetBreakdown editor roll {normalizedRoll} failed to resolve inside total percent {totalPercent}.");
    }

    private static SerializedProperty RequiredRelativeProperty(SerializedProperty property, string propertyName)
    {
        SerializedProperty relativeProperty = property.FindPropertyRelative(propertyName);
        if (relativeProperty == null)
            throw new InvalidOperationException($"BudgetBreakdown drawer requires serialized field '{propertyName}'.");

        return relativeProperty;
    }
}
