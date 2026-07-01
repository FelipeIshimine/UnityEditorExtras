using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorExtras.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(BalanceCurve))]
public sealed class BalanceCurveDrawer : PropertyDrawer
{
    private const int MaximumStepPreviewRows = 128;
    private static readonly Color PanelBackground = EditorGUIUtility.isProSkin
        ? new Color(1f, 1f, 1f, 0.035f)
        : new Color(0f, 0f, 0f, 0.035f);

    private static readonly Color AccentColor = EditorGUIUtility.isProSkin
        ? new Color(0.45f, 0.67f, 1f, 0.58f)
        : new Color(0.14f, 0.34f, 0.66f, 0.7f);

    private static readonly Color MutedTextColor = EditorGUIUtility.isProSkin
        ? new Color(1f, 1f, 1f, 0.62f)
        : new Color(0f, 0f, 0f, 0.62f);

    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        SerializedProperty minValueProperty = RequiredRelativeProperty(property, nameof(BalanceCurve.minValue));
        SerializedProperty maxValueProperty = RequiredRelativeProperty(property, nameof(BalanceCurve.maxValue));
        SerializedProperty stepsProperty = RequiredRelativeProperty(property, nameof(BalanceCurve.steps));
        SerializedProperty curveProperty = RequiredRelativeProperty(property, nameof(BalanceCurve.curve));
        SerializedProperty roundingModeProperty = RequiredRelativeProperty(property, nameof(BalanceCurve.roundingMode));
        SerializedProperty roundingIncrementProperty = RequiredRelativeProperty(property, nameof(BalanceCurve.roundingIncrement));

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

        VisualElement content = new VisualElement();
        content.style.marginLeft = 2f;
        content.style.paddingLeft = 6f;
        content.style.borderLeftWidth = 2f;
        content.style.borderLeftColor = AccentColor;

        VisualElement rangeRow = Row();
        rangeRow.Add(BoundFloatField(minValueProperty, "Min"));
        rangeRow.Add(BoundFloatField(maxValueProperty, "Max"));

        VisualElement stepRow = Row();
        stepRow.Add(BoundIntegerField(stepsProperty, "Steps"));
        stepRow.Add(BoundEnumField(roundingModeProperty, "Round"));

        VisualElement roundingRow = Row();
        roundingRow.Add(BoundFloatField(roundingIncrementProperty, "To"));

        CurveField curveField = new CurveField("Curve");
        curveField.BindProperty(curveProperty);
        StyleField(curveField);

        Slider previewSlider = new Slider("Step", 0f, 1f)
        {
            value = 0f,
            showInputField = true
        };
        StyleSlider(previewSlider);

        VisualElement previewPanel = Panel();
        Label previewValueLabel = new Label();
        previewValueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        previewValueLabel.style.fontSize = 12;
        Label previewMetaLabel = new Label();
        previewMetaLabel.style.color = MutedTextColor;
        previewMetaLabel.style.fontSize = 10;
        previewPanel.Add(previewValueLabel);
        previewPanel.Add(previewMetaLabel);

        Foldout stepTable = new Foldout
        {
            text = "Step Table",
            value = false
        };
        stepTable.style.marginLeft = 8f;
        stepTable.style.marginTop = 2f;
        VisualElement stepRows = new VisualElement();
        stepRows.style.marginLeft = 0f;
        stepTable.Add(stepRows);

        content.Add(rangeRow);
        content.Add(stepRow);
        content.Add(roundingRow);
        content.Add(curveField);
        content.Add(previewSlider);
        content.Add(previewPanel);
        content.Add(stepTable);
        root.Add(content);

        void SnapPreviewSliderToStep()
        {
            int steps = stepsProperty.intValue;
            if (steps < 2)
                return;

            int stepIndex = Mathf.RoundToInt(previewSlider.value * (steps - 1));
            float snappedValue = (float)stepIndex / (steps - 1);
            if (!Mathf.Approximately(previewSlider.value, snappedValue))
                previewSlider.SetValueWithoutNotify(snappedValue);
        }

        void RefreshPreview()
        {
            float normalized = previewSlider.value;
            int steps = stepsProperty.intValue;
            bool roundingEnabled = (BalanceCurveRoundingMode)roundingModeProperty.enumValueIndex != BalanceCurveRoundingMode.None;
            roundingRow.SetEnabled(roundingEnabled);

            stepRows.Clear();

            if (steps < 2)
            {
                previewValueLabel.text = "Invalid";
                previewMetaLabel.text = "Steps must be at least 2.";
                stepRows.Add(PreviewRow("Invalid", "Steps must be at least 2."));
                return;
            }

            int stepIndex = Mathf.RoundToInt(normalized * (steps - 1));
            float rawValue = EvaluateRaw(
                minValueProperty.floatValue,
                maxValueProperty.floatValue,
                curveProperty.animationCurveValue,
                normalized);

            float roundedValue = ApplyRounding(
                rawValue,
                (BalanceCurveRoundingMode)roundingModeProperty.enumValueIndex,
                roundingIncrementProperty.floatValue);
            previewValueLabel.text = $"Value {roundedValue:0.###}";
            previewMetaLabel.text = BuildPreviewMeta(stepIndex, steps, normalized, rawValue, roundingModeProperty, roundingIncrementProperty);

            BuildStepTable(
                stepRows,
                steps,
                minValueProperty.floatValue,
                maxValueProperty.floatValue,
                curveProperty.animationCurveValue,
                (BalanceCurveRoundingMode)roundingModeProperty.enumValueIndex,
                roundingIncrementProperty.floatValue);
        }

        previewSlider.RegisterValueChangedCallback(_ =>
        {
            SnapPreviewSliderToStep();
            RefreshPreview();
        });
        root.TrackPropertyValue(minValueProperty, _ => RefreshPreview());
        root.TrackPropertyValue(maxValueProperty, _ => RefreshPreview());
        root.TrackPropertyValue(stepsProperty, _ =>
        {
            SnapPreviewSliderToStep();
            RefreshPreview();
        });
        root.TrackPropertyValue(curveProperty, _ => RefreshPreview());
        root.TrackPropertyValue(roundingModeProperty, _ => RefreshPreview());
        root.TrackPropertyValue(roundingIncrementProperty, _ => RefreshPreview());

        root.schedule.Execute(() =>
        {
            SnapPreviewSliderToStep();
            RefreshPreview();
        });

        return root;
    }

    private static VisualElement Row()
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.marginBottom = 2f;
        return row;
    }

    private static FloatField BoundFloatField(SerializedProperty property, string label)
    {
        FloatField field = new FloatField(label);
        field.BindProperty(property);
        StyleField(field);
        return field;
    }

    private static IntegerField BoundIntegerField(SerializedProperty property, string label)
    {
        IntegerField field = new IntegerField(label);
        field.BindProperty(property);
        StyleField(field);
        return field;
    }

    private static EnumField BoundEnumField(SerializedProperty property, string label)
    {
        BalanceCurveRoundingMode currentValue = (BalanceCurveRoundingMode)property.enumValueIndex;
        EnumField field = new EnumField(label, currentValue);
        field.BindProperty(property);
        StyleField(field);
        return field;
    }

    private static void StyleField(BaseField<float> field)
    {
        StyleField((VisualElement)field);
        StyleLabel(field.labelElement);
    }

    private static void StyleField(BaseField<int> field)
    {
        StyleField((VisualElement)field);
        StyleLabel(field.labelElement);
    }

    private static void StyleField(BaseField<Enum> field)
    {
        StyleField((VisualElement)field);
        StyleLabel(field.labelElement);
    }

    private static void StyleField(CurveField field)
    {
        field.style.marginTop = 2f;
        field.style.marginBottom = 2f;
        StyleLabel(field.labelElement);
    }

    private static void StyleField(VisualElement field)
    {
        field.style.flexGrow = 1f;
        field.style.flexBasis = 0f;
        field.style.minWidth = 92f;
        field.style.marginRight = 4f;
    }

    private static void StyleLabel(Label label)
    {
        label.style.minWidth = 38f;
        label.style.width = 38f;
        label.style.fontSize = 10;
        label.style.color = MutedTextColor;
    }

    private static void StyleSlider(Slider slider)
    {
        slider.style.marginTop = 2f;
        slider.style.marginBottom = 2f;
        slider.labelElement.style.minWidth = 48f;
        slider.labelElement.style.width = 48f;
        slider.labelElement.style.fontSize = 10;
        slider.labelElement.style.color = MutedTextColor;
    }

    private static VisualElement Panel()
    {
        VisualElement panel = new VisualElement();
        panel.style.backgroundColor = PanelBackground;
        panel.style.borderLeftWidth = 2f;
        panel.style.borderLeftColor = AccentColor;
        panel.style.paddingLeft = 6f;
        panel.style.paddingRight = 4f;
        panel.style.paddingTop = 3f;
        panel.style.paddingBottom = 3f;
        panel.style.marginTop = 2f;
        panel.style.marginBottom = 2f;
        return panel;
    }

    private static SerializedProperty RequiredRelativeProperty(SerializedProperty property, string propertyName)
    {
        SerializedProperty relativeProperty = property.FindPropertyRelative(propertyName);
        if (relativeProperty == null)
            throw new InvalidOperationException($"BalanceCurve drawer requires serialized field '{propertyName}'.");

        return relativeProperty;
    }

    private static float EvaluateRaw(float minValue, float maxValue, AnimationCurve curve, float normalized)
    {
        if (curve == null)
            throw new InvalidOperationException("BalanceCurve curve must be assigned.");

        return Mathf.LerpUnclamped(minValue, maxValue, curve.Evaluate(normalized));
    }

    private static float ApplyRounding(float value, BalanceCurveRoundingMode roundingMode, float roundingIncrement = 1f)
    {
        if (roundingMode != BalanceCurveRoundingMode.None && roundingIncrement <= 0f)
        {
            throw new InvalidOperationException(
                $"BalanceCurve rounding increment must be greater than zero when rounding is enabled, got {roundingIncrement}.");
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

    private static string BuildPreviewMeta(
        int stepIndex,
        int steps,
        float normalized,
        float rawValue,
        SerializedProperty roundingModeProperty,
        SerializedProperty roundingIncrementProperty)
    {
        BalanceCurveRoundingMode roundingMode = (BalanceCurveRoundingMode)roundingModeProperty.enumValueIndex;
        string roundingText = roundingMode == BalanceCurveRoundingMode.None
            ? string.Empty
            : $"   {roundingMode} to {roundingIncrementProperty.floatValue:0.###}";

        return $"Step {stepIndex + 1}/{steps}   t {normalized:0.###}   raw {rawValue:0.###}{roundingText}";
    }

    private static void BuildStepTable(
        VisualElement container,
        int steps,
        float minValue,
        float maxValue,
        AnimationCurve curve,
        BalanceCurveRoundingMode roundingMode,
        float roundingIncrement)
    {
        int visibleRows = Mathf.Min(steps, MaximumStepPreviewRows);
        for (int i = 0; i < visibleRows; i++)
        {
            float normalized = (float)i / (steps - 1);
            float rawValue = EvaluateRaw(minValue, maxValue, curve, normalized);
            float roundedValue = ApplyRounding(rawValue, roundingMode, roundingIncrement);
            container.Add(PreviewRow($"{i}", $"value {roundedValue:0.###}   t {normalized:0.###}   raw {rawValue:0.###}"));
        }

        if (steps > MaximumStepPreviewRows)
            container.Add(PreviewRow("...", $"Showing first {MaximumStepPreviewRows} of {steps} steps."));
    }

    private static VisualElement PreviewRow(string key, string value)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.marginBottom = 1f;

        Label keyLabel = new Label(key);
        keyLabel.style.width = 30f;
        keyLabel.style.minWidth = 30f;
        keyLabel.style.color = MutedTextColor;
        keyLabel.style.fontSize = 10;

        Label valueLabel = new Label(value);
        valueLabel.style.flexGrow = 1f;
        valueLabel.style.fontSize = 10;

        row.Add(keyLabel);
        row.Add(valueLabel);
        return row;
    }
}
