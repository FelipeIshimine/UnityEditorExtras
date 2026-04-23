/*
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditorExtras.Runtime;

namespace UnityEditorExtras.Editor
{
    public class TableView : VisualElement
    {
        private readonly SerializedProperty _listProperty;
        private readonly TableAttribute _attribute;
        private readonly string _sessionKey;

        private bool _showSharedOnly;
        private HashSet<string> _hiddenProperties = new HashSet<string>();

        private VisualElement _tableContainer;
        private ScrollView _scrollView;

        private static readonly Color C_BORDER = new Color(0.12f, 0.12f, 0.12f);
        private static readonly Color C_HEADER = new Color(0.18f, 0.18f, 0.18f);
        private static readonly Color C_ROW_EVEN = new Color(0.22f, 0.22f, 0.22f);
        private static readonly Color C_ROW_ODD = new Color(0.25f, 0.25f, 0.25f);
        private static readonly Color C_CELL_DIM = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        private static readonly Color C_ACCENT = new Color(0.255f, 0.490f, 0.965f);
        private static readonly Color C_REMOVE = new Color(0.85f, 0.30f, 0.30f);

        public TableView(SerializedProperty listProperty, TableAttribute attribute)
        {
            _listProperty = listProperty;
            _attribute = attribute;
            _sessionKey = "UnityExtras_Table_" + attribute.Key;
            if (string.IsNullOrEmpty(attribute.Key))
            {
                 _sessionKey = "UnityExtras_Table_" + listProperty.serializedObject.targetObject.GetInstanceID() + "_" + listProperty.propertyPath;
            }

            _showSharedOnly = attribute.ShowSharedOnly;
            LoadSettings();

            BuildUI();
            this.RegisterCallback<SerializedPropertyChangeEvent>(evt => RebuildTable());
            this.schedule.Execute(() => {
                if (_listProperty == null || _listProperty.serializedObject == null || _listProperty.serializedObject.targetObject == null) return;
                if (_listProperty.serializedObject.UpdateIfRequiredOrScript())
                {
                    RebuildTable();
                }
            }).Every(500);
        }

        private void BuildUI()
        {
            style.flexGrow = 1;
            style.marginTop = 4; style.marginBottom = 4;
            style.borderTopWidth = 1; style.borderBottomWidth = 1;
            style.borderLeftWidth = 1; style.borderRightWidth = 1;
            style.borderTopColor = C_BORDER; style.borderBottomColor = C_BORDER;
            style.borderLeftColor = C_BORDER; style.borderRightColor = C_BORDER;

            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.backgroundColor = C_HEADER;
            toolbar.style.paddingLeft = 5; toolbar.style.paddingRight = 5;
            toolbar.style.height = 24; toolbar.style.alignItems = Align.Center;

            var title = new Label(_listProperty.displayName);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1;
            toolbar.Add(title);

            var addBtn = new Button(OnAddClicked); addBtn.text = "+"; StyleToolbarButton(addBtn);
            addBtn.style.marginRight = 4; toolbar.Add(addBtn);

            var settingsBtn = new Button(ShowSettingsPopup); settingsBtn.text = "?"; StyleToolbarButton(settingsBtn);
            toolbar.Add(settingsBtn);

            Add(toolbar);

            _scrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            _scrollView.style.flexGrow = 1;
            _tableContainer = new VisualElement();
            _scrollView.Add(_tableContainer);
            Add(_scrollView);
            RebuildTable();
        }

        private void RebuildTable()
        {
            _tableContainer.Clear();
            int arraySize = _listProperty.arraySize;
            if (arraySize == 0) {
                var empty = new Label("List is empty");
                empty.style.paddingLeft = 10; empty.style.paddingTop = 10; empty.style.color = Color.gray;
                _tableContainer.Add(empty); return;
            }

            var existenceCount = new Dictionary<string, int>();
            var allProps = DiscoverAllPropertyInfos(existenceCount);

            var filteredProps = allProps.Where(p => {
                if (_hiddenProperties.Contains(p.Path)) return false;
                if (_showSharedOnly && existenceCount[p.Path] < arraySize) return false;
                return true;
            }).ToList();

            var headerRow = CreateRow(true);
            headerRow.Add(CreateHeaderCell("Item", 120));
            foreach (var p in filteredProps) headerRow.Add(CreateHeaderCell(p.DisplayName, 120));
            headerRow.Add(CreateHeaderCell("", 30));
            _tableContainer.Add(headerRow);

            for (int i = 0; i < arraySize; i++) {
                int index = i; var element = _listProperty.GetArrayElementAtIndex(i); var row = CreateRow(false, i % 2 == 0);
                string name = "Element " + i;
                if (element.propertyType == SerializedPropertyType.ObjectReference && element.objectReferenceValue != null) name = element.objectReferenceValue.name;
                row.Add(CreateCell(120, name));

                foreach (var p in filteredProps) {
                    var cell = CreateCell(120); var prop = element.FindPropertyRelative(p.Path);
                    if (prop != null) {
                        if (p.AtMaxDepth && prop.hasVisibleChildren) {
                            var btn = new Button(() => InspectProperty(prop)); btn.text = "Inspect"; btn.style.flexGrow = 1; cell.Add(btn);
                        } else {
                            var field = new PropertyField(prop, ""); field.Bind(prop.serializedObject); cell.Add(field);
                        }
                    } else {
                        var missing = new Label("?"); missing.style.unityTextAlign = TextAnchor.MiddleCenter;
                        cell.style.backgroundColor = C_CELL_DIM; cell.Add(missing);
                    }
                    row.Add(cell);
                }

                var removeCell = CreateCell(30);
                var removeBtn = new Button(() => OnRemoveClicked(index)); removeBtn.text = "?";
                removeBtn.style.backgroundColor = new Color(0,0,0,0); removeBtn.style.color = C_REMOVE;
                removeBtn.style.borderTopWidth = 0; removeBtn.style.borderRightWidth = 0;
                removeBtn.style.borderBottomWidth = 0; removeBtn.style.borderLeftWidth = 0;
                removeBtn.style.fontSize = 14; removeBtn.style.paddingLeft = 0; removeBtn.style.paddingRight = 0;
                removeBtn.style.paddingTop = 0; removeBtn.style.paddingBottom = 0;
                removeCell.Add(removeBtn); row.Add(removeCell);
                _tableContainer.Add(row);
            }
        }

        private void InspectProperty(SerializedProperty property) { PropertyInspectorWindow.ShowWindow(property); }

        private List<PropertyInfo> DiscoverAllPropertyInfos(Dictionary<string, int> existenceCount) {
            var infoMap = new Dictionary<string, PropertyInfo>();
            for (int i = 0; i < _listProperty.arraySize; i++) {
                var element = _listProperty.GetArrayElementAtIndex(i); DiscoverRecursive(element, element.propertyPath, 0, infoMap, existenceCount);
            }
            return infoMap.Values.OrderBy(p => p.Path).ToList();
        }

        private void DiscoverRecursive(SerializedProperty parent, string rootPath, int depth, Dictionary<string, PropertyInfo> infoMap, Dictionary<string, int> existenceCount) {
            var iterator = parent.Copy(); var end = parent.GetEndProperty(); bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end)) {
                enterChildren = false; if (iterator.propertyPath.EndsWith("m_Script")) continue;
                string rel = iterator.propertyPath.Substring(rootPath.Length).TrimStart('.'); if (string.IsNullOrEmpty(rel)) continue;
                bool atMaxDepth = depth >= _attribute.MaxDepth;
                if (!infoMap.ContainsKey(rel)) {
                    PropertyInfo info = new PropertyInfo(); info.Path = rel; info.DisplayName = iterator.displayName; info.AtMaxDepth = atMaxDepth;
                    infoMap[rel] = info; if (existenceCount != null) existenceCount[rel] = 0;
                }
                if (existenceCount != null) existenceCount[rel]++;
                if (!atMaxDepth && iterator.hasVisibleChildren && iterator.propertyType == SerializedPropertyType.Generic) {
                    DiscoverRecursive(iterator, rootPath, depth + 1, infoMap, existenceCount);
                }
            }
        }

        private VisualElement CreateRow(bool isHeader, bool even = false) {
            var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row;
            row.style.borderBottomWidth = 1; row.style.borderBottomColor = C_BORDER;
            row.style.backgroundColor = isHeader ? C_HEADER : (even ? C_ROW_EVEN : C_ROW_ODD);
            if (isHeader) { row.style.borderTopWidth = 1; row.style.borderTopColor = C_BORDER; } return row;
        }

        private VisualElement CreateHeaderCell(string text, float width) {
            var cell = CreateCell(width); var label = new Label(text); label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleCenter; cell.Add(label); return cell;
        }

        private VisualElement CreateCell(float width, string text = null) {
            var cell = new VisualElement(); cell.style.width = width; cell.style.paddingLeft = 5; cell.style.paddingRight = 5;
            cell.style.paddingTop = 2; cell.style.paddingBottom = 2; cell.style.borderRightWidth = 1; cell.style.borderRightColor = C_BORDER;
            cell.style.justifyContent = Justify.Center;
            if (text != null) { var label = new Label(text); label.style.unityTextAlign = TextAnchor.MiddleLeft; cell.Add(label); } return cell;
        }

        private void OnAddClicked() { _listProperty.arraySize++; _listProperty.serializedObject.ApplyModifiedProperties(); RebuildTable(); }

        private void OnRemoveClicked(int index) {
            if (index < 0 || index >= _listProperty.arraySize) return; var prop = _listProperty.GetArrayElementAtIndex(index);
            if (prop.propertyType == SerializedPropertyType.ObjectReference && prop.objectReferenceValue != null) _listProperty.DeleteArrayElementAtIndex(index);
            _listProperty.DeleteArrayElementAtIndex(index); _listProperty.serializedObject.ApplyModifiedProperties(); RebuildTable();
        }

        private void StyleToolbarButton(Button b) {
            b.style.width = 20; b.style.height = 20; b.style.paddingLeft = 0; b.style.paddingRight = 0; b.style.paddingTop = 0; b.style.paddingBottom = 0;
            b.style.backgroundColor = new Color(0,0,0,0); b.style.color = Color.white; b.style.borderTopWidth = 0; b.style.borderRightWidth = 0;
            b.style.borderBottomWidth = 0; b.style.borderLeftWidth = 0;
            b.RegisterCallback<PointerEnterEvent>((evt) => { b.style.backgroundColor = new Color(1,1,1,0.1f); });
            b.RegisterCallback<PointerLeaveEvent>((evt) => { b.style.backgroundColor = new Color(0,0,0,0); });
        }

        private void ShowSettingsPopup() {
            var menu = new GenericMenu(); menu.AddItem(new GUIContent("Show Shared Only"), _showSharedOnly, () => { _showSharedOnly = !_showSharedOnly; SaveSettings(); RebuildTable(); });
            menu.AddSeparator(""); var allInfos = DiscoverAllPropertyInfos(null);
            foreach (var info in allInfos) {
                bool isVisible = !_hiddenProperties.Contains(info.Path); string p = info.Path;
                menu.AddItem(new GUIContent("Properties/" + info.DisplayName + " (" + p + ")"), isVisible, () => {
                    if (isVisible) _hiddenProperties.Add(p); else _hiddenProperties.Remove(p); SaveSettings(); RebuildTable();
                });
            } menu.ShowAsContext();
        }

        private void SaveSettings() 
        {
            TableSettingsData data = new TableSettingsData(); data.ShowSharedOnly = _showSharedOnly; data.HiddenProperties = _hiddenProperties.ToList();
            SessionState.SetString(_sessionKey, JsonUtility.ToJson(data));
        }

        private void LoadSettings() {
            string json = SessionState.GetString(_sessionKey, ""); if (!string.IsNullOrEmpty(json)) {
                TableSettingsData data = JsonUtility.FromJson<TableSettingsData>(json);
                _showSharedOnly = data.ShowSharedOnly; _hiddenProperties = new HashSet<string>(data.HiddenProperties);
            }
        }

        [Serializable] private class TableSettingsData { public bool ShowSharedOnly; public List<string> HiddenProperties; }

        private struct PropertyInfo { public string Path; public string DisplayName; public bool AtMaxDepth; }

        private class PropertyInspectorWindow : EditorWindow {
            private SerializedObject _serializedObject; private string _propertyPath; private string _displayName;
            public static void ShowWindow(SerializedProperty property) {
                PropertyInspectorWindow window = CreateInstance<PropertyInspectorWindow>();
                window._serializedObject = property.serializedObject; window._propertyPath = property.propertyPath;
                window._displayName = property.displayName; window.titleContent = new GUIContent("Inspect: " + window._displayName); window.ShowUtility();
            }
            private void OnGUI() {
                if (_serializedObject == null || _serializedObject.targetObject == null) { Close(); return; }
                _serializedObject.Update(); SerializedProperty property = _serializedObject.FindProperty(_propertyPath);
                if (property == null) { EditorGUILayout.LabelField("Property not found."); return; }
                EditorGUILayout.Space(10); EditorGUILayout.PropertyField(property, true); _serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
*/
