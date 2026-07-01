using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorExtras.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(SoftCapCurve))]
public sealed class SoftCapCurveDrawer : PropertyDrawer
{
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
        SerializedProperty startValueProperty = RequiredRelativeProperty(property, nameof(SoftCapCurve.startValue));
        SerializedProperty linearIncreasePerStepProperty = RequiredRelativeProperty(property, nameof(SoftCapCurve.linearIncreasePerStep));
        SerializedProperty softCapStartStepProperty = RequiredRelativeProperty(property, nameof(SoftCapCurve.softCapStartStep));
        SerializedProperty softCapValueProperty = RequiredRelativeProperty(property, nameof(SoftCapCurve.softCapValue));
        SerializedProperty softnessInStepsProperty = RequiredRelativeProperty(property, nameof(SoftCapCurve.softnessInSteps));
        SerializedProperty roundingModeProperty = RequiredRelativeProperty(property, nameof(SoftCapCurve.roundingMode));

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

        VisualElement startRow = Row();
        startRow.Add(BoundFloatField(startValueProperty, "Start"));
        startRow.Add(BoundFloatField(linearIncreasePerStepProperty, "+/Step"));

        VisualElement capRow = Row();
        capRow.Add(BoundIntegerField(softCapStartStepProperty, "At Step"));
        capRow.Add(BoundFloatField(softCapValueProperty, "Cap"));

        VisualElement taperRow = Row();
        taperRow.Add(BoundFloatField(softnessInStepsProperty, "Soft"));
        taperRow.Add(BoundEnumField(roundingModeProperty, "Round"));

        Slider previewStepSlider = new Slider("Preview Step", 0f, 100f)
        {
            value = 0f,
            showInputField = true
        };
        StyleSlider(previewStepSlider);

        VisualElement previewPanel = Panel();
        Label previewValueLabel = new Label();
        previewValueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        previewValueLabel.style.fontSize = 12;
        Label previewMetaLabel = new Label();
        previewMetaLabel.style.color = MutedTextColor;
        previewMetaLabel.style.fontSize = 10;
        previewPanel.Add(previewValueLabel);
        previewPanel.Add(previewMetaLabel);

        content.Add(startRow);
        content.Add(capRow);
        content.Add(taperRow);
        content.Add(previewStepSlider);
        content.Add(previewPanel);
        root.Add(content);

        void Refresh()
        {
            float startValue = startValueProperty.floatValue;
            float linearIncreasePerStep = linearIncreasePerStepProperty.floatValue;
            int softCapStartStep = softCapStartStepProperty.intValue;
            float softCapValue = softCapValueProperty.floatValue;
            float softnessInSteps = softnessInStepsProperty.floatValue;

            int previewMax = Mathf.Max(10, Mathf.CeilToInt(softCapStartStep + softnessInSteps * 5f));
            previewStepSlider.highValue = previewMax;

            string invalidReason = ValidateForPreview(startValue, linearIncreasePerStep, softCapStartStep, softCapValue, softnessInSteps);
            if (!string.IsNullOrEmpty(invalidReason))
            {
                previewValueLabel.text = "Invalid";
                previewMetaLabel.text = invalidReason;
                return;
            }

            float step = previewStepSlider.value;
            float rawValue = EvaluateRaw(startValue, linearIncreasePerStep, softCapStartStep, softCapValue, softnessInSteps, step);
            float roundedValue = ApplyRounding(rawValue, (BalanceCurveRoundingMode)roundingModeProperty.enumValueIndex);
            previewValueLabel.text = $"Value {roundedValue:0.###}";
            previewMetaLabel.text = $"step {step:0.##}/{previewMax}   raw {rawValue:0.###}";
        }

        previewStepSlider.RegisterValueChangedCallback(_ => Refresh());
        root.TrackPropertyValue(startValueProperty, _ => Refresh());
        root.TrackPropertyValue(linearIncreasePerStepProperty, _ => Refresh());
        root.TrackPropertyValue(softCapStartStepProperty, _ => Refresh());
        root.TrackPropertyValue(softCapValueProperty, _ => Refresh());
        root.TrackPropertyValue(softnessInStepsProperty, _ => Refresh());
        root.TrackPropertyValue(roundingModeProperty, _ => Refresh());
        root.schedule.Execute(Refresh);

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

    private static void StyleField(VisualElement field)
    {
        field.style.flexGrow = 1f;
        field.style.flexBasis = 0f;
        field.style.minWidth = 92f;
        field.style.marginRight = 4f;
    }

    private static void StyleLabel(Label label)
    {
        label.style.minWidth = 44f;
        label.style.width = 44f;
        label.style.fontSize = 10;
        label.style.color = MutedTextColor;
    }

    private static void StyleSlider(Slider slider)
    {
        slider.style.marginTop = 2f;
        slider.style.marginBottom = 2f;
        slider.labelElement.style.minWidth = 74f;
        slider.labelElement.style.width = 74f;
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
            throw new InvalidOperationException($"SoftCapCurve drawer requires serialized field '{propertyName}'.");

        return relativeProperty;
    }

    private static string ValidateForPreview(
        float startValue,
        float linearIncreasePerStep,
        int softCapStartStep,
        float softCapValue,
        float softnessInSteps)
    {
        if (float.IsNaN(startValue))
            return "Invalid: start value cannot be NaN.";

        if (float.IsNaN(linearIncreasePerStep) || linearIncreasePerStep <= 0f)
            return "Invalid: linear increase per step must be greater than zero.";

        if (softCapStartStep < 0)
            return "Invalid: soft cap start step must be zero or greater.";

        if (float.IsNaN(softCapValue))
            return "Invalid: soft cap value cannot be NaN.";

        if (float.IsNaN(softnessInSteps) || softnessInSteps <= 0f)
            return "Invalid: softness in steps must be greater than zero.";

        float valueAtSoftCapStart = startValue + linearIncreasePerStep * softCapStartStep;
        if (softCapValue <= valueAtSoftCapStart)
            return $"Invalid: soft cap value must be above value at soft cap start ({valueAtSoftCapStart:0.###}).";

        return string.Empty;
    }

    private static float EvaluateRaw(
        float startValue,
        float linearIncreasePerStep,
        int softCapStartStep,
        float softCapValue,
        float softnessInSteps,
        float step)
    {
        float valueAtSoftCapStart = startValue + linearIncreasePerStep * softCapStartStep;
        if (step <= softCapStartStep)
            return startValue + linearIncreasePerStep * step;

        float overSoftCapSteps = step - softCapStartStep;
        float easedProgress = 1f - Mathf.Exp(-overSoftCapSteps / softnessInSteps);
        return Mathf.LerpUnclamped(valueAtSoftCapStart, softCapValue, easedProgress);
    }

    private static float ApplyRounding(float value, BalanceCurveRoundingMode roundingMode)
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
