using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorExtras.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(Optional<>))]
public class OptionalDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        var root = new VisualElement();
        root.style.flexDirection = FlexDirection.Row;

        var enabledProp = property.FindPropertyRelative("_enabled");
        var valueProp = property.FindPropertyRelative("_value");

        var propertyField = new PropertyField(valueProp, property.displayName);
        propertyField.style.flexGrow = 1;
        
        // Bind the enabled state of the property field to the toggle
        propertyField.SetEnabled(enabledProp.boolValue);

        var toggle = new Toggle();
        toggle.value = enabledProp.boolValue;
        toggle.RegisterValueChangedCallback(evt =>
        {
            enabledProp.boolValue = evt.newValue;
            enabledProp.serializedObject.ApplyModifiedProperties();
            propertyField.SetEnabled(evt.newValue);
        });
        
        toggle.style.marginLeft = 5;
        toggle.style.marginRight = 0;

        root.Add(propertyField);
        root.Add(toggle);

        return root;
    }
}
