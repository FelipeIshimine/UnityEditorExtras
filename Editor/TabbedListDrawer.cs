using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorExtras.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditorExtras.Editor
{
    // Draws a [TabbedList] list/array as a horizontal tab strip where only the selected element's
    // body is shown. The attribute sets applyToCollection, so this drawer receives the whole array
    // property rather than one element at a time.
    [CustomPropertyDrawer(typeof(TabbedListAttribute))]
    public sealed class TabbedListDrawer : PropertyDrawer
    {
        private static readonly Color Accent = new Color(0.36f, 0.66f, 1f);
        private static readonly Color TabIdle = new Color(1f, 1f, 1f, 0.06f);
        private static readonly Color TabIdleHover = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color PanelBg = new Color(0f, 0f, 0f, 0.16f);
        private static readonly Color BorderCol = new Color(0f, 0f, 0f, 0.35f);

        // Element child-property names probed (in order) to build a friendly tab label.
        private static readonly string[] LabelCandidates = { "displayName", "name", "title", "label", "id" };

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            // With applyToCollection, 'property' is the whole array. Being called per-element means
            // the attribute is on something that isn't a list/array — that's a usage error.
            if (!property.isArray || property.propertyType == SerializedPropertyType.String)
                throw new InvalidOperationException(
                    $"[{nameof(TabbedListAttribute)}] is for lists/arrays. Field: {property.propertyPath}.");

            // 'property' is the whole collection (applyToCollection). Operate on a stable copy.
            SerializedProperty listProp = property.Copy();
            string sessionKey = "TabbedListDrawer.sel:" + property.propertyPath;

            VisualElement panel = new VisualElement();
            panel.style.marginTop = 2f;
            panel.style.marginBottom = 4f;
            panel.style.paddingTop = 6f;
            panel.style.paddingBottom = 6f;
            panel.style.paddingLeft = 16f;
            panel.style.paddingRight = 6f;
            panel.style.backgroundColor = PanelBg;
            SetBorder(panel, BorderCol, 1f, 5f);

            // Toolbar: list label + count + element controls.
            VisualElement toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.marginBottom = 6f;

            Label headerLabel = new Label(property.displayName.ToUpperInvariant());
            headerLabel.style.fontSize = 10;
            headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerLabel.style.opacity = 0.55f;
            toolbar.Add(headerLabel);

            Label countLabel = new Label();
            countLabel.style.fontSize = 10;
            countLabel.style.opacity = 0.45f;
            countLabel.style.marginLeft = 6f;
            countLabel.style.flexGrow = 1f;
            toolbar.Add(countLabel);

            Button leftBtn = MakeIconButton("◀", "Move selected element earlier");
            Button rightBtn = MakeIconButton("▶", "Move selected element later");
            Button dupBtn = MakeIconButton("⧉", "Duplicate the selected element");
            Button addBtn = MakeIconButton("+", "Add a new element after the selected one");
            Button delBtn = MakeIconButton("−", "Delete the selected element");
            toolbar.Add(leftBtn);
            toolbar.Add(rightBtn);
            toolbar.Add(dupBtn);
            toolbar.Add(addBtn);
            toolbar.Add(delBtn);
            panel.Add(toolbar);

            VisualElement tabStrip = new VisualElement();
            tabStrip.style.flexDirection = FlexDirection.Row;
            tabStrip.style.flexWrap = Wrap.Wrap;
            tabStrip.style.marginBottom = 6f;
            panel.Add(tabStrip);

            VisualElement body = new VisualElement();
            panel.Add(body);

            int GetSelected()
            {
                int count = listProp.arraySize;
                if (count == 0)
                    return -1;
                return Mathf.Clamp(SessionState.GetInt(sessionKey, 0), 0, count - 1);
            }

            void SetSelected(int index) => SessionState.SetInt(sessionKey, index);

            // Tracks the element count last rendered, so external value edits don't trigger a full
            // rebuild (which would destroy the field being typed into and steal keyboard focus).
            int renderedCount = -1;

            void Rebuild()
            {
                int count = listProp.arraySize;
                int selected = GetSelected();
                renderedCount = count;

                countLabel.text = count == 0 ? string.Empty : $"{count} item{(count == 1 ? "" : "s")}";

                tabStrip.Clear();
                for (int i = 0; i < count; i++)
                    tabStrip.Add(MakeTab(i, selected));

                bool has = count > 0;
                delBtn.SetEnabled(has);
                dupBtn.SetEnabled(has);
                leftBtn.SetEnabled(selected > 0);
                rightBtn.SetEnabled(has && selected < count - 1);

                body.Clear();
                if (!has)
                {
                    Label empty = new Label("Empty — press + to add the first element.");
                    empty.style.opacity = 0.6f;
                    empty.style.unityFontStyleAndWeight = FontStyle.Italic;
                    empty.style.paddingTop = 4f;
                    empty.style.paddingBottom = 4f;
                    body.Add(empty);
                    return;
                }

                SerializedProperty element = listProp.GetArrayElementAtIndex(selected);

                Label bodyHeader = new Label($"{TabLabel(element, selected)}  ({selected + 1}/{count})");
                bodyHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
                bodyHeader.style.fontSize = 12;
                bodyHeader.style.marginBottom = 4f;
                bodyHeader.style.color = Accent;
                body.Add(bodyHeader);

                AddElementBody(element, body);
            }

            // Renders the element's fields directly, skipping the "Element X" foldout wrapper.
            void AddElementBody(SerializedProperty element, VisualElement container)
            {
                if (!element.hasVisibleChildren)
                {
                    PropertyField leaf = new PropertyField(element, string.Empty);
                    leaf.BindProperty(element);
                    container.Add(leaf);
                    return;
                }

                SerializedProperty child = element.Copy();
                SerializedProperty end = element.GetEndProperty();
                if (!child.NextVisible(true))
                    return;

                do
                {
                    if (SerializedProperty.EqualContents(child, end))
                        break;
                    if (child.depth != element.depth + 1)
                        continue;

                    SerializedProperty childCopy = child.Copy();
                    PropertyField field = new PropertyField(childCopy);
                    field.BindProperty(childCopy);
                    container.Add(field);
                }
                while (child.NextVisible(false));
            }

            VisualElement MakeTab(int index, int selected)
            {
                bool isActive = index == selected;
                SerializedProperty element = listProp.GetArrayElementAtIndex(index);
                Button tab = new Button(() =>
                {
                    SetSelected(index);
                    Rebuild();
                });
                tab.text = TabLabel(element, index);
                tab.tooltip = tab.text;
                tab.style.height = 24f;
                tab.style.maxWidth = 160f;
                tab.style.marginRight = 4f;
                tab.style.marginBottom = 4f;
                tab.style.marginLeft = 0f;
                tab.style.marginTop = 0f;
                tab.style.paddingLeft = 8f;
                tab.style.paddingRight = 8f;
                tab.style.overflow = Overflow.Hidden;
                tab.style.textOverflow = TextOverflow.Ellipsis;
                tab.style.unityFontStyleAndWeight = isActive ? FontStyle.Bold : FontStyle.Normal;
                tab.style.fontSize = 12;
                SetBorder(tab, isActive ? Accent : new Color(0f, 0f, 0f, 0.25f), isActive ? 1.5f : 1f, 4f);
                tab.style.backgroundColor = isActive ? new Color(Accent.r, Accent.g, Accent.b, 0.22f) : TabIdle;
                tab.style.color = isActive ? Accent : new Color(1f, 1f, 1f, 0.85f);

                if (!isActive)
                {
                    tab.RegisterCallback<MouseEnterEvent>(_ => tab.style.backgroundColor = TabIdleHover);
                    tab.RegisterCallback<MouseLeaveEvent>(_ => tab.style.backgroundColor = TabIdle);
                }
                return tab;
            }

            addBtn.clicked += () =>
            {
                int insertAt = listProp.arraySize == 0 ? 0 : GetSelected() + 1;
                listProp.InsertArrayElementAtIndex(insertAt);
                property.serializedObject.ApplyModifiedProperties();
                SetSelected(insertAt);
                Rebuild();
            };

            dupBtn.clicked += () =>
            {
                if (listProp.arraySize == 0)
                    return;
                int selected = GetSelected();
                listProp.InsertArrayElementAtIndex(selected); // copies element at 'selected'
                // Unity copies [SerializeReference] managed references by reference, not by value,
                // so the new element shares effect/interface instances with the original. Deep-clone
                // them so edits on the duplicate no longer bleed into the source.
                DeepCloneManagedReferences(listProp.GetArrayElementAtIndex(selected + 1));
                property.serializedObject.ApplyModifiedProperties();
                SetSelected(selected + 1);
                Rebuild();
            };

            delBtn.clicked += () =>
            {
                if (listProp.arraySize == 0)
                    return;
                int selected = GetSelected();
                listProp.DeleteArrayElementAtIndex(selected);
                property.serializedObject.ApplyModifiedProperties();
                SetSelected(Mathf.Max(0, selected - 1));
                Rebuild();
            };

            leftBtn.clicked += () =>
            {
                int selected = GetSelected();
                if (selected <= 0)
                    return;
                listProp.MoveArrayElement(selected, selected - 1);
                property.serializedObject.ApplyModifiedProperties();
                SetSelected(selected - 1);
                Rebuild();
            };

            rightBtn.clicked += () =>
            {
                int selected = GetSelected();
                if (selected < 0 || selected >= listProp.arraySize - 1)
                    return;
                listProp.MoveArrayElement(selected, selected + 1);
                property.serializedObject.ApplyModifiedProperties();
                SetSelected(selected + 1);
                Rebuild();
            };

            // Only rebuild when the list grows/shrinks (add, delete, reorder from outside, undo).
            // Value edits within an element must NOT rebuild, or the focused field is recreated.
            tabStrip.TrackPropertyValue(listProp, tracked =>
            {
                if (tracked.arraySize != renderedCount)
                    Rebuild();
            });

            Rebuild();
            return panel;
        }

        // Replaces every [SerializeReference] managed reference under 'element' with a fresh
        // deep copy, so a duplicated element stops sharing instances with the one it was copied
        // from. Walks descendants so nested references (e.g. an effect inside a tier) are covered.
        private static void DeepCloneManagedReferences(SerializedProperty element)
        {
            SerializedProperty child = element.Copy();
            SerializedProperty end = element.GetEndProperty();
            bool enterChildren = true;
            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                enterChildren = true;
                if (child.propertyType != SerializedPropertyType.ManagedReference)
                    continue;

                object original = child.managedReferenceValue;
                if (original == null)
                    continue;

                // JsonUtility round-trip produces a new instance of the concrete type with all
                // serialized fields copied by value.
                Type concrete = original.GetType();
                child.managedReferenceValue = JsonUtility.FromJson(JsonUtility.ToJson(original), concrete);
            }
        }

        // Builds a friendly tab caption from the element's data, falling back to a 1-based index.
        private static string TabLabel(SerializedProperty element, int index)
        {
            string ordinal = (index + 1).ToString();

            if (element.propertyType == SerializedPropertyType.ObjectReference)
                return element.objectReferenceValue != null ? element.objectReferenceValue.name : ordinal;

            if (element.hasVisibleChildren)
            {
                foreach (string candidate in LabelCandidates)
                {
                    SerializedProperty field = element.FindPropertyRelative(candidate);
                    if (field == null)
                        continue;
                    string value = FieldToLabel(field);
                    if (!string.IsNullOrEmpty(value))
                        return $"{ordinal}· {value}";
                }
            }
            else
            {
                string value = FieldToLabel(element);
                if (!string.IsNullOrEmpty(value))
                    return value;
            }

            return ordinal;
        }

        private static string FieldToLabel(SerializedProperty field)
        {
            switch (field.propertyType)
            {
                case SerializedPropertyType.String:
                    return field.stringValue;
                case SerializedPropertyType.Integer:
                    return field.intValue.ToString();
                case SerializedPropertyType.Enum:
                    return field.enumValueIndex >= 0 && field.enumValueIndex < field.enumDisplayNames.Length
                        ? field.enumDisplayNames[field.enumValueIndex]
                        : string.Empty;
                case SerializedPropertyType.ObjectReference:
                    return field.objectReferenceValue != null ? field.objectReferenceValue.name : string.Empty;
                default:
                    return string.Empty;
            }
        }

        private static Button MakeIconButton(string glyph, string tooltip)
        {
            Button b = new Button { text = glyph, tooltip = tooltip };
            b.style.width = 24f;
            b.style.height = 22f;
            b.style.marginLeft = 2f;
            b.style.marginRight = 0f;
            b.style.marginTop = 0f;
            b.style.marginBottom = 0f;
            b.style.paddingLeft = 0f;
            b.style.paddingRight = 0f;
            b.style.fontSize = 13;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            return b;
        }

        private static void SetBorder(VisualElement e, Color color, float width, float radius)
        {
            e.style.borderTopWidth = width;
            e.style.borderBottomWidth = width;
            e.style.borderLeftWidth = width;
            e.style.borderRightWidth = width;
            e.style.borderTopColor = color;
            e.style.borderBottomColor = color;
            e.style.borderLeftColor = color;
            e.style.borderRightColor = color;
            e.style.borderTopLeftRadius = radius;
            e.style.borderTopRightRadius = radius;
            e.style.borderBottomLeftRadius = radius;
            e.style.borderBottomRightRadius = radius;
        }
    }
}
