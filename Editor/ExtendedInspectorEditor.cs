using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

[CustomEditor(typeof(MonoBehaviour), true, isFallback = true)]
[CanEditMultipleObjects]
public class ExtendedInspectorEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();

        // Draw default inspector (UI Toolkit)
        InspectorElement.FillDefaultInspector(root, serializedObject, this);

        InjectReadShowInInspector(targets, target, root);
        InjectButtons(targets, target, root);

        return root;
    }

    public static void InjectButtons(Object[] targets, Object target, VisualElement root)
    {
        var type = target.GetType();

        var methods = type
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m =>
                m.GetCustomAttribute<ButtonAttribute>() != null &&
                m.GetParameters().Length == 0)
            .Select(m => new
            {
                Method = m,
                Attribute = m.GetCustomAttribute<ButtonAttribute>()
            })
            .ToList();

        var grouped = methods.GroupBy(x =>
        {
            var attr = x.Attribute;

            if (attr.Layout == ButtonGroupLayout.None)
                return null;

            return string.IsNullOrEmpty(attr.GroupName)
                ? "__DEFAULT__"
                : attr.GroupName;
        });
        
        var separator = new VisualElement();
        separator.style.height = 1;
        separator.style.marginTop = 8;
        separator.style.marginBottom = 6;
        separator.style.backgroundColor = 
            EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.05f)
                : new Color(0f, 0f, 0f, 0.08f);

        root.Add(separator);
        
        foreach (var group in grouped)
        {
            if (group.Key == null)
            {
                // No group → render normally
                foreach (var entry in group)
                {
                    root.Add(CreateButton(targets, target, entry.Method, entry.Attribute));
                }
            }
            else
            {
                var container = new VisualElement();
                container.style.marginBottom = 4;
                container.style.marginTop = 6;

                var first = group.First().Attribute;

                if (first.Layout == ButtonGroupLayout.Horizontal)
                {
                    container.style.flexDirection = FlexDirection.Row;
                    container.style.alignItems = Align.Center;
                    container.style.alignContent = Align.Stretch;
                }
                else
                {
                    container.style.flexDirection = FlexDirection.Column;
                }

                if (first.Layout != ButtonGroupLayout.None &&
                    group.Key != "__DEFAULT__")
                {
                    var header = new Label(group.Key);
                    header.style.marginTop = 8;
                    header.style.marginBottom = 3;
                    header.style.unityFontStyleAndWeight = FontStyle.Bold;
                    header.style.opacity = 0.85f;
                    header.style.fontSize = 11;

                    root.Add(header);
                }

                foreach (var entry in group)
                {
                    container.Add(CreateButton(targets, target, entry.Method, entry.Attribute));
                }

                root.Add(container);
            }
        }
    }

    private static Button CreateButton(Object[] targets, Object target, MethodInfo method, ButtonAttribute attr)
    {
        string label = string.IsNullOrEmpty(attr.Label)
            ? ObjectNames.NicifyVariableName(method.Name)
            : attr.Label;

        var button = new Button(() =>
        {
            foreach (var t in targets)
            {
                Undo.RecordObject((UnityEngine.Object)t, method.Name);
                method.Invoke(t, null);
                EditorUtility.SetDirty((UnityEngine.Object)t);
            }
        })
        {
            text = label
        };
        
        button.style.flexGrow = 1;
        button.style.height = 22;
        button.style.marginRight = 4;
        button.style.marginTop = 2;

        SetupValidation(targets, target, button, method, attr);

        return button;
    }

    private static void SetupValidation(Object[] targets, Object target, Button button, MethodInfo actionMethod,
        ButtonAttribute attr)
    {
        if (string.IsNullOrEmpty(attr.Validation))
            return;

        var type = target.GetType();
        var memberName = attr.Validation;

        MethodInfo validatorMethod = type.GetMethod(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        PropertyInfo validatorProperty = type.GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Func<UnityEngine.Object, bool> validator = null;

        if (validatorMethod != null &&
            validatorMethod.ReturnType == typeof(bool) &&
            validatorMethod.GetParameters().Length == 0)
        {
            validator = (obj) => (bool)validatorMethod.Invoke(obj, null);
        }
        else if (validatorProperty != null &&
                 validatorProperty.PropertyType == typeof(bool))
        {
            validator = (obj) => (bool)validatorProperty.GetValue(obj);
        }
        else
        {
            Debug.LogError(
                $"Invalid validation member '{memberName}' on {type.Name}. " +
                "Must be bool method (no params) or bool property.");
            return;
        }

        void RefreshState()
        {
            bool enabled = targets.All(t => validator((UnityEngine.Object)t));
            button.SetEnabled(enabled);
        }

        RefreshState();

        button.schedule.Execute(RefreshState).Every(200);
    }
    
    public static void InjectReadShowInInspector(
        Object[] targets,
        Object target,
        VisualElement root)
    {
        var type = target.GetType();

        var properties = type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(p =>
                p.GetCustomAttribute<ShowInInspectorAttribute>() != null &&
                p.GetIndexParameters().Length == 0);

        foreach (var prop in properties)
        {
            var attr = prop.GetCustomAttribute<ShowInInspectorAttribute>();

            string labelText = string.IsNullOrEmpty(attr.Label)
                ? ObjectNames.NicifyVariableName(prop.Name)
                : attr.Label;

            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginTop = 2;
            container.style.marginBottom = 2;

            var label = new Label(labelText);
            label.style.minWidth = 150;
            label.style.unityFontStyleAndWeight = FontStyle.Normal;
            label.style.opacity = 0.75f;

            var valueLabel = new Label();
            valueLabel.style.flexGrow = 1;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            valueLabel.style.unityFont = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).font;
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

            container.Add(label);
            container.Add(valueLabel);

            void Refresh()
            {
                if (targets.Length == 1)
                {
                    var value = prop.GetValue(target);
                    valueLabel.text = FormatValue(value);
                }
                else
                {
                    bool allEqual = true;
                    object first = prop.GetValue(targets[0]);

                    for (int i = 1; i < targets.Length; i++)
                    {
                        var other = prop.GetValue(targets[i]);
                        if (!Equals(first, other))
                        {
                            allEqual = false;
                            break;
                        }
                    }

                    valueLabel.text = allEqual
                        ? (first != null ? first.ToString() : "null")
                        : "—";
                }
            }

            Refresh();
            container.schedule.Execute(Refresh).Every(200);

            root.Add(container);
        }
        
        string FormatValue(object value)
        {
            if (value == null)
                return "null";

            switch (value)
            {
                case float f: return f.ToString("0.###");
                case double d: return d.ToString("0.###");
                case Vector2 v2: return $"({v2.x:0.##}, {v2.y:0.##})";
                case Vector3 v3: return $"({v3.x:0.##}, {v3.y:0.##}, {v3.z:0.##})";
                case Color c: return $"RGBA({c.r:0.##}, {c.g:0.##}, {c.b:0.##}, {c.a:0.##})";
                default: return value.ToString();
            }
        }
    }
}