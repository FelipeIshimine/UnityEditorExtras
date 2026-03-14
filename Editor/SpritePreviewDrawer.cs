using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(SpritePreviewAttribute))]
public class SpritePreviewDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        var attr = (SpritePreviewAttribute)attribute;

        var root = new VisualElement();
        root.style.flexDirection = FlexDirection.Row;
        root.style.alignItems = Align.Center;

        var image = new Image();
        image.scaleMode = ScaleMode.ScaleToFit;
        image.style.width = attr.Size;
        image.style.height = attr.Size;
        image.style.marginRight = 4;

        void UpdateImage()
        {
            var sprite = property.objectReferenceValue as Sprite;
            image.sprite = sprite ? sprite : null;
        }

        UpdateImage();

        var field = new ObjectField(property.displayName)
        {
            objectType = typeof(Sprite),
            value = property.objectReferenceValue,
        };

        field.RegisterValueChangedCallback(evt =>
        {
            property.objectReferenceValue = evt.newValue as Sprite;
            property.serializedObject.ApplyModifiedProperties();
            UpdateImage();
        });

        field.style.flexGrow = 1;
        field.style.alignSelf = Align.Stretch;

        switch (attr.Alignment)
        {
            case SpritePreviewAlignment.Left:
                root.Add(image);
                if (attr.ShowField) root.Add(field);
                break;

            case SpritePreviewAlignment.Right:
                if (attr.ShowField) root.Add(field);
                root.Add(image);
                break;

            case SpritePreviewAlignment.Center:
                root.style.flexDirection = FlexDirection.Column;
                root.style.alignItems = Align.Center;
                root.Add(image);
                if (attr.ShowField) root.Add(field);
                break;
        }

        if (!attr.ShowField)
            image.style.marginRight = 0;

        return root;
    }
}