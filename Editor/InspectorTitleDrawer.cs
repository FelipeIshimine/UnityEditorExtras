using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(InspectorTitleAttribute))]
public class InspectorTitleDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        var attr = (InspectorTitleAttribute)attribute;

        var field = new TextField(property.displayName);
        field.bindingPath = property.propertyPath;

        field.style.marginTop = 6;
        field.style.marginBottom = 6;

        var label = field.Q<Label>();
        label.style.fontSize = attr.FontSize;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;

        switch (attr.Alignment)
        {
            case TitleAlign.Left:
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                break;

            case TitleAlign.Center:
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                break;

            case TitleAlign.Right:
                label.style.unityTextAlign = TextAnchor.MiddleRight;
                break;
        }

        return field;
    }
}