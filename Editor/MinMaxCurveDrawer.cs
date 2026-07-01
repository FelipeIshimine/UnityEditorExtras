using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorExtras.Runtime;
using UnityEngine;
using UnityEngine.UIElements;
using MinMaxCurve = UnityEditorExtras.Runtime.MinMaxCurve;

[CustomPropertyDrawer(typeof(MinMaxCurve))]
public sealed class MinMaxCurveDrawer : PropertyDrawer
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
        SerializedProperty minValueProperty = RequiredRelativeProperty(property, nameof(MinMaxCurve.minValue));
        SerializedProperty maxValueProperty = RequiredRelativeProperty(property, nameof(MinMaxCurve.maxValue));
        SerializedProperty curveProperty = RequiredRelativeProperty(property, nameof(MinMaxCurve.curve));
        SerializedProperty roundingModeProperty = RequiredRelativeProperty(property, nameof(MinMaxCurve.roundingMode));

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

        CurveField curveField = new CurveField("Curve");
        curveField.BindProperty(curveProperty);
        StyleCurveField(curveField);

        EnumField roundingModeField = BoundEnumField(roundingModeProperty, "Round");

        Slider previewSlider = new Slider("Preview", 0f, 1f)
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

        content.Add(rangeRow);
        content.Add(curveField);
        content.Add(roundingModeField);
        content.Add(previewSlider);
        content.Add(previewPanel);
        root.Add(content);

        void Refresh()
        {
            float minValue = minValueProperty.floatValue;
            float maxValue = maxValueProperty.floatValue;
            if (maxValue < minValue)
            {
                previewValueLabel.text = "Invalid";
                previewMetaLabel.text = $"Max {maxValue:0.###} is below min {minValue:0.###}.";
                return;
            }

            float normalized = previewSlider.value;
            float rawValue = EvaluateRaw(minValue, maxValue, curveProperty.animationCurveValue, normalized);
            float roundedValue = ApplyRounding(rawValue, (BalanceCurveRoundingMode)roundingModeProperty.enumValueIndex);
            previewValueLabel.text = $"Value {roundedValue:0.###}";
            previewMetaLabel.text = $"t {normalized:0.###}   raw {rawValue:0.###}";
        }

        previewSlider.RegisterValueChangedCallback(_ => Refresh());
        root.TrackPropertyValue(minValueProperty, _ => Refresh());
        root.TrackPropertyValue(maxValueProperty, _ => Refresh());
        root.TrackPropertyValue(curveProperty, _ => Refresh());
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

    private static void StyleCurveField(CurveField field)
    {
        field.style.marginTop = 2f;
        field.style.marginBottom = 2f;
        StyleLabel(field.labelElement);
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
            throw new InvalidOperationException($"MinMaxCurve drawer requires serialized field '{propertyName}'.");

        return relativeProperty;
    }

    private static float EvaluateRaw(float minValue, float maxValue, AnimationCurve curve, float normalized)
    {
        if (curve == null)
            throw new InvalidOperationException("MinMaxCurve curve must be assigned.");

        return Mathf.LerpUnclamped(minValue, maxValue, curve.Evaluate(normalized));
    }

    private static float ApplyRounding(float value, BalanceCurveRoundingMode roundingMode)
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
}
