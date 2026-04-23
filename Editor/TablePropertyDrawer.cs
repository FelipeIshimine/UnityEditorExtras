/*
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditorExtras.Runtime;

namespace UnityEditorExtras.Editor
{
    [CustomPropertyDrawer(typeof(TableAttribute))]
    public class TablePropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            if (!property.isArray || property.propertyType == SerializedPropertyType.String)
            {
                return new HelpBox("Table attribute can only be used on arrays or lists.", HelpBoxMessageType.Error);
            }

            var tableAttr = (TableAttribute)attribute;
            return new TableView(property, tableAttr);
        }
    }
}
*/
