using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(MeasurementAttribute))]
public class MeasurementDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        var root = new VisualElement();
        root.style.flexDirection = FlexDirection.Row;

        var field = new PropertyField(property);
        field.style.flexGrow = 1;

        var unit = ((MeasurementAttribute)attribute).Measurement;

        var label = new Label(GetUnitString(unit));
        label.style.marginLeft = 4;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.fontSize = 10;
        label.style.opacity = 0.7f;

        root.Add(field);
        root.Add(label);

        return root;
    }

    static string GetUnitString(MeasurementUnit m)
    {
        return m switch
        {
            MeasurementUnit.Meter => "m",
            MeasurementUnit.Kilogram => "kg",
            MeasurementUnit.Second => "s",
            MeasurementUnit.Ampere => "A",
            MeasurementUnit.Kelvin => "K",
            MeasurementUnit.Mole => "mol",
            MeasurementUnit.Candela => "cd",

            MeasurementUnit.MeterPerSecond => "m/s",
            MeasurementUnit.MeterPerSecondSquared => "m/s²",

            MeasurementUnit.PerSecond => "perSecond",
            MeasurementUnit.PerMinute => "perMinute",
            MeasurementUnit.PerHour => "perHour",

            MeasurementUnit.Newton => "N",
            MeasurementUnit.Pascal => "Pa",
            MeasurementUnit.Joule => "J",
            MeasurementUnit.Watt => "W",
            MeasurementUnit.Coulomb => "C",
            MeasurementUnit.Volt => "V",
            MeasurementUnit.Ohm => "Ω",
            MeasurementUnit.Siemens => "S",
            MeasurementUnit.Farad => "F",
            MeasurementUnit.Henry => "H",
            MeasurementUnit.Tesla => "T",

            MeasurementUnit.Hertz => "Hz",

            MeasurementUnit.SquareMeter => "m²",
            MeasurementUnit.CubicMeter => "m³",

            MeasurementUnit.Liter => "L",
            MeasurementUnit.Milliliter => "mL",

            MeasurementUnit.Celsius => "°C",

            MeasurementUnit.Percent => "%",

            _ => ""
        };
    }
}