using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorExtras.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(InlineAttribute))]
public sealed class InlineDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        InlineAttribute inline = (InlineAttribute)attribute;

        if (property.isArray && property.propertyType != SerializedPropertyType.String)
        {
            throw new InvalidOperationException(
                $"{nameof(InlineAttribute)} is for embedded serialized classes and structs, not arrays or lists. Field: {property.propertyPath}.");
        }

        VisualElement root = new VisualElement();
        
        VisualElement content = new VisualElement();
        content.style.marginLeft = 8f;
        content.style.marginTop = 2f;
        content.style.marginBottom = 2f;
        content.style.paddingLeft = inline.IndentPixels;
        content.style.borderLeftWidth = 2f;

        if (inline.ShowLabel)
        {
            Label label = new Label(property.displayName);
            label.style.marginLeft = 1f;
            label.style.marginBottom = 1f;
            label.style.fontSize = 11;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.opacity = 0.72f;
            root.Add(label);
        }

        int childCount = AddDirectChildren(property, content);
        if (childCount == 0)
        {
            throw new InvalidOperationException(
                $"{nameof(InlineAttribute)} requires a field with visible serialized children. Field: {property.propertyPath}.");
        }

        root.Add(content);
        return root;
    }

    private static int AddDirectChildren(SerializedProperty property, VisualElement root)
    {
        SerializedProperty child = property.Copy();
        SerializedProperty endProperty = property.GetEndProperty();

        if (!child.NextVisible(true))
            return 0;

        int childCount = 0;
        do
        {
            if (SerializedProperty.EqualContents(child, endProperty))
                break;

            if (child.depth != property.depth + 1)
                continue;

            SerializedProperty childCopy = child.Copy();
            PropertyField field = new PropertyField(childCopy);
            field.style.marginLeft = 0f;
            field.style.marginRight = 0f;
            root.Add(field);
            childCount++;
        }
        while (child.NextVisible(false));

        return childCount;
    }
}
