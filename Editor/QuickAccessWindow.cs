// Place this file inside any Editor/ folder in your project.
// QuickAccessAttribute.cs must be accessible from this assembly.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class QuickAccessWindow : EditorWindow
{
    // ── State ─────────────────────────────────────────────────────────────────

    private readonly List<Type>              _registeredTypes = new();
    private readonly List<UnityEngine.Object> _foundObjects   = new();

    private Type    _selectedType;
    private bool    _showAllComponents;

    // ── UI refs ───────────────────────────────────────────────────────────────

    private DropdownField  _typeDropdown;
    private Toggle         _allComponentsToggle;
    private Label          _countLabel;
    private ListView       _objectList;
    private VisualElement  _inspectorContainer;
    private VisualElement  _emptyState;

    // ── Colors ────────────────────────────────────────────────────────────────

    static readonly Color C_BG        = new(0.160f, 0.160f, 0.160f);
    static readonly Color C_TOOLBAR   = new(0.120f, 0.120f, 0.120f);
    static readonly Color C_OPTBAR    = new(0.145f, 0.145f, 0.145f);
    static readonly Color C_PANEL_L   = new(0.175f, 0.175f, 0.175f);
    static readonly Color C_PANEL_R   = new(0.200f, 0.200f, 0.200f);
    static readonly Color C_HEADER    = new(0.130f, 0.130f, 0.130f);
    static readonly Color C_CARD      = new(0.220f, 0.220f, 0.220f);
    static readonly Color C_CARD_HDR  = new(0.175f, 0.175f, 0.175f);
    static readonly Color C_BORDER    = new(0.090f, 0.090f, 0.090f);
    static readonly Color C_ACCENT    = new(0.255f, 0.490f, 0.965f);
    static readonly Color C_TEXT_DIM  = new(0.500f, 0.500f, 0.500f);
    static readonly Color C_TEXT      = new(0.850f, 0.850f, 0.850f);
    static readonly Color C_SUBTEXT   = new(0.580f, 0.580f, 0.580f);

    // ── Entry point ───────────────────────────────────────────────────────────

    [MenuItem("Window/Quick Access ⚡")]
    public static void ShowWindow()
    {
        var w = GetWindow<QuickAccessWindow>();
        w.titleContent = new GUIContent("Quick Access", EditorGUIUtility.IconContent("d_FilterByType").image);
        w.minSize = new Vector2(680, 460);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void CreateGUI()
    {
        DiscoverTypes();
        BuildUI();
    }

    // ── Type discovery ────────────────────────────────────────────────────────

    private void DiscoverTypes()
    {
        _registeredTypes.Clear();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var hits = assembly.GetTypes().Where(t =>
                    !t.IsAbstract &&
                    !t.IsGenericTypeDefinition &&
                    t.GetCustomAttribute<QuickAccessAttribute>(false) != null &&
                    (typeof(Component).IsAssignableFrom(t) || typeof(ScriptableObject).IsAssignableFrom(t)));

                _registeredTypes.AddRange(hits);
            }
            catch { /* skip inaccessible assemblies */ }
        }

        // Stable alphabetical order
        _registeredTypes.Sort((a, b) =>
            string.Compare(GetLabel(a), GetLabel(b), StringComparison.OrdinalIgnoreCase));
    }

    private static string GetLabel(Type t) =>
        t.GetCustomAttribute<QuickAccessAttribute>()?.Label ?? t.Name;

    private static bool IsBehaviour(Type t)    => typeof(Component).IsAssignableFrom(t);
    private static bool IsScriptable(Type t)   => typeof(ScriptableObject).IsAssignableFrom(t);

    // ── UI construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        var root = rootVisualElement;
        root.Clear();
        root.style.flexDirection = FlexDirection.Column;
        root.style.flexGrow      = 1;
        root.style.backgroundColor = C_BG;

        root.Add(BuildToolbar());
        root.Add(BuildOptionsBar());
        root.Add(BuildSplitView());

        PopulateDropdown();
    }

    // ── Toolbar ───────────────────────────────────────────────────────────────

    private VisualElement BuildToolbar()
    {
        var bar = Row(C_TOOLBAR, 10, 7);
        Border(bar.style, C_BORDER, bottom: 1);

        var badge = new Label("⚡");
        badge.style.fontSize  = 15;
        badge.style.marginRight = 6;
        badge.style.color     = C_ACCENT;
        badge.style.unityTextAlign = TextAnchor.MiddleCenter;

        _typeDropdown = new DropdownField { label = string.Empty };
        _typeDropdown.style.flexGrow = 1;
        _typeDropdown.style.marginRight = 8;

        var refreshBtn = new Button(OnRefreshClicked) { text = "↺  Refresh" };
        StyleButton(refreshBtn, C_ACCENT);
        refreshBtn.style.width = 90;

        bar.Add(badge);
        bar.Add(_typeDropdown);
        bar.Add(refreshBtn);
        return bar;
    }

    // ── Options bar ───────────────────────────────────────────────────────────

    private VisualElement BuildOptionsBar()
    {
        var bar = Row(C_OPTBAR, 10, 5);
        Border(bar.style, C_BORDER, bottom: 1);

        _allComponentsToggle = new Toggle("All components");
        _allComponentsToggle.tooltip = "When enabled, shows every component on the selected GameObject, not just the matched type.";
        _allComponentsToggle.style.flexGrow = 0;
        _allComponentsToggle.style.marginRight = 14;
        _allComponentsToggle.RegisterValueChangedCallback(e =>
        {
            _showAllComponents = e.newValue;
            RefreshInspector();
        });

        _countLabel = new Label("—");
        _countLabel.style.color    = C_TEXT_DIM;
        _countLabel.style.fontSize = 11;
        _countLabel.style.flexGrow = 1;
        _countLabel.style.unityTextAlign = TextAnchor.MiddleRight;

        bar.Add(_allComponentsToggle);
        bar.Add(_countLabel);
        return bar;
    }

    // ── Split view ────────────────────────────────────────────────────────────

    private VisualElement BuildSplitView()
    {
        var split = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
        split.style.flexGrow = 1;

        split.Add(BuildLeftPanel());
        split.Add(BuildRightPanel());
        return split;
    }

    // Left – object list

    private VisualElement BuildLeftPanel()
    {
        var panel = new VisualElement();
        panel.style.flexGrow        = 1;
        panel.style.minWidth        = 130;
        panel.style.backgroundColor = C_PANEL_L;

        panel.Add(PanelHeader("OBJECTS"));

        _objectList = new ListView
        {
            fixedItemHeight = 28,
            selectionType   = SelectionType.Single,
            makeItem        = MakeListItem,
            bindItem        = BindListItem,
        };
        _objectList.style.flexGrow = 1;
        _objectList.selectionChanged += _ => RefreshInspector();

        panel.Add(_objectList);
        return panel;
    }

    private VisualElement MakeListItem()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems    = Align.Center;
        row.style.paddingLeft   = 10;
        row.style.paddingRight  = 6;

        // ── dot / ping button ─────────────────────────────────────────────────
        // The dot doubles as the ping button — click it to center the scene view
        var pingDot = new Button { name = "ping", text = "◆", tooltip = "Ping & focus in Scene / Project" };
        pingDot.style.fontSize        = 7;
        pingDot.style.width           = 16;
        pingDot.style.height          = 16;
        pingDot.style.marginRight     = 6;
        pingDot.style.paddingLeft     = 0;
        pingDot.style.paddingRight    = 0;
        pingDot.style.paddingTop      = 0;
        pingDot.style.paddingBottom   = 0;
        pingDot.style.backgroundColor = new Color(0, 0, 0, 0);
        pingDot.style.color           = C_ACCENT;
        pingDot.style.borderTopWidth  = pingDot.style.borderRightWidth =
            pingDot.style.borderBottomWidth = pingDot.style.borderLeftWidth = 0;
        SetBorderRadius(pingDot.style, 2);
        pingDot.style.unityTextAlign  = TextAnchor.MiddleCenter;

        pingDot.RegisterCallback<PointerEnterEvent>(_ =>
        {
            pingDot.style.color           = Color.white;
            pingDot.style.backgroundColor = new Color(C_ACCENT.r, C_ACCENT.g, C_ACCENT.b, 0.25f);
        });
        pingDot.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            pingDot.style.color           = C_ACCENT;
            pingDot.style.backgroundColor = new Color(0, 0, 0, 0);
        });

        // ── name label (normal state) ─────────────────────────────────────────
        var nameLabel = new Label();
        nameLabel.name           = "name";
        nameLabel.style.flexGrow = 1;
        nameLabel.style.fontSize = 12;
        nameLabel.style.color    = C_TEXT;

        // Double-click to enter rename mode
        nameLabel.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.clickCount == 2) EnterRenameMode(row);
        });

        // ── rename text field (hidden until activated) ────────────────────────
        var renameField = new TextField { name = "rename-field" };
        renameField.style.display      = DisplayStyle.None;
        renameField.style.flexGrow     = 1;
        renameField.style.fontSize     = 12;
        renameField.style.height       = 20;
        renameField.style.marginTop    = 0;
        renameField.style.marginBottom = 0;

        renameField.RegisterCallback<AttachToPanelEvent>(_ =>
        {
            var input = renameField.Q(className: "unity-base-field__input");
            if (input == null) return;
            input.style.backgroundColor         = new Color(0.10f, 0.10f, 0.10f, 0.85f);
            input.style.borderTopColor          = C_ACCENT;
            input.style.borderRightColor        = C_ACCENT;
            input.style.borderBottomColor       = C_ACCENT;
            input.style.borderLeftColor         = C_ACCENT;
            input.style.borderTopWidth          = 1;
            input.style.borderRightWidth        = 1;
            input.style.borderBottomWidth       = 1;
            input.style.borderLeftWidth         = 1;
            input.style.borderTopLeftRadius     = 3;
            input.style.borderTopRightRadius    = 3;
            input.style.borderBottomLeftRadius  = 3;
            input.style.borderBottomRightRadius = 3;
        });

        renameField.RegisterCallback<KeyDownEvent>(e =>
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                CommitRename(row, renameField.value.Trim());
                e.StopPropagation();
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                CancelRename(row);
                e.StopPropagation();
            }
        });

        renameField.RegisterCallback<FocusOutEvent>(_ => CommitRename(row, renameField.value.Trim()));

        // ── scene/asset tag ───────────────────────────────────────────────────
        var tag = new Label();
        tag.name              = "tag";
        tag.style.fontSize    = 10;
        tag.style.color       = C_SUBTEXT;
        tag.style.marginRight = 4;

        row.Add(pingDot);
        row.Add(nameLabel);
        row.Add(renameField);
        row.Add(tag);
        return row;
    }

    // ── Rename helpers ────────────────────────────────────────────────────────

    private void EnterRenameMode(VisualElement row)
    {
        var nameL = row.Q<Label>("name");
        var field = row.Q<TextField>("rename-field");
        var tag   = row.Q<Label>("tag");
        if (nameL == null || field == null) return;

        field.SetValueWithoutNotify(nameL.text);
        nameL.style.display = DisplayStyle.None;
        tag.style.display   = DisplayStyle.None;
        field.style.display = DisplayStyle.Flex;
        field.schedule.Execute(() => { field.Focus(); field.SelectAll(); }).StartingIn(10);
    }

    private void CommitRename(VisualElement row, string newName)
    {
        var nameL  = row.Q<Label>("name");
        var field  = row.Q<TextField>("rename-field");
        if (nameL == null || field == null || field.style.display == DisplayStyle.None) return;

        // Find bound object via name match (robust across recycled rows)
        int idx = _foundObjects.FindIndex(o => o != null && o.name == nameL.text);
        if (idx >= 0 && !string.IsNullOrEmpty(newName) && newName != nameL.text)
        {
            var obj = _foundObjects[idx];
            Undo.RecordObject(obj, $"Rename {obj.name}");
            if (obj is ScriptableObject)
            {
                string assetPath = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(assetPath))
                    AssetDatabase.RenameAsset(assetPath, newName);
            }
            else
            {
                // GameObject or Component – rename the object itself
                var target = obj is Component c ? (UnityEngine.Object)c.gameObject : obj;
                Undo.RecordObject(target, $"Rename {target.name}");
                target.name = newName;
                if (obj is Component comp2) EditorUtility.SetDirty(comp2.gameObject);
            }
            nameL.text = newName;
            _objectList.RefreshItem(idx);
        }

        ExitRenameMode(row);
    }

    private void CancelRename(VisualElement row) => ExitRenameMode(row);

    private static void ExitRenameMode(VisualElement row)
    {
        var nameL = row.Q<Label>("name");
        var field = row.Q<TextField>("rename-field");
        var tag   = row.Q<Label>("tag");
        if (field == null) return;

        field.style.display = DisplayStyle.None;
        nameL.style.display = DisplayStyle.Flex;
        tag.style.display   = DisplayStyle.Flex;
    }

    private void BindListItem(VisualElement element, int index)
    {
        // Always exit rename mode when a row gets reused for a different index
        ExitRenameMode(element);

        if (index < 0 || index >= _foundObjects.Count || _foundObjects[index] == null)
            return;

        var obj   = _foundObjects[index];
        var nameL = element.Q<Label>("name");
        var tagL  = element.Q<Label>("tag");
        var pingB = element.Q<Button>("ping");

        if (nameL != null) nameL.text = obj.name;

        if (tagL != null)
        {
            tagL.text = (obj is Component comp && _selectedType != null)
                ? comp.gameObject.scene.name
                : string.Empty;
        }

        if (pingB != null)
        {
            pingB.clicked -= pingB.userData as Action;
            Action pingAction = () => PingObject(obj);
            pingB.userData = pingAction;
            pingB.clicked += pingAction;
        }
    }

    // Right – inspector

    private VisualElement BuildRightPanel()
    {
        var panel = new VisualElement();
        panel.style.flexGrow        = 1;
        panel.style.backgroundColor = C_PANEL_R;

        panel.Add(PanelHeader("INSPECTOR"));

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow = 1;

        // Inspector cards land here
        _inspectorContainer = new VisualElement();
        _inspectorContainer.style.flexGrow  = 1;
        _inspectorContainer.style.paddingLeft   = 6;
        _inspectorContainer.style.paddingRight  = 6;
        _inspectorContainer.style.paddingBottom = 10;

        // Empty state
        _emptyState = BuildEmptyState("Select an object\nto inspect it.");
        _inspectorContainer.Add(_emptyState);

        scroll.Add(_inspectorContainer);
        panel.Add(scroll);
        return panel;
    }

    private VisualElement BuildEmptyState(string message)
    {
        var wrap = new VisualElement();
        wrap.style.flexGrow      = 1;
        wrap.style.alignItems    = Align.Center;
        wrap.style.justifyContent = Justify.Center;
        wrap.style.paddingTop    = 60;

        var icon = new Label("◈");
        icon.style.fontSize  = 28;
        icon.style.color     = C_TEXT_DIM;
        icon.style.unityTextAlign = TextAnchor.MiddleCenter;

        var lbl = new Label(message);
        lbl.style.color     = C_TEXT_DIM;
        lbl.style.fontSize  = 12;
        lbl.style.marginTop = 10;
        lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
        lbl.style.whiteSpace = WhiteSpace.Normal;

        wrap.Add(icon);
        wrap.Add(lbl);
        return wrap;
    }

    // ── Dropdown population ───────────────────────────────────────────────────

    private void PopulateDropdown()
    {
        var choices = _registeredTypes.Select(GetLabel).ToList();
        _typeDropdown.choices = choices;
        _typeDropdown.RegisterValueChangedCallback(_ => OnTypeChanged());

        if (choices.Count > 0)
        {
            _typeDropdown.index = 0;
            _selectedType = _registeredTypes[0];
            ScanForObjects();
        }
        else
        {
            _countLabel.text = "No [QuickAccess] types found";
            _allComponentsToggle.SetEnabled(false);
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnTypeChanged()
    {
        int i = _typeDropdown.index;
        if (i < 0 || i >= _registeredTypes.Count) return;
        _selectedType = _registeredTypes[i];
        ScanForObjects();
    }

    private void OnRefreshClicked()
    {
        DiscoverTypes();

        // Re-build dropdown preserving selection label if possible
        var prevLabel = _typeDropdown.value;
        var choices   = _registeredTypes.Select(GetLabel).ToList();
        _typeDropdown.choices = choices;

        int restore = choices.IndexOf(prevLabel);
        _typeDropdown.index = restore >= 0 ? restore : (choices.Count > 0 ? 0 : -1);

        _selectedType = _typeDropdown.index >= 0 ? _registeredTypes[_typeDropdown.index] : null;
        ScanForObjects();
    }

    // ── Scanning ──────────────────────────────────────────────────────────────

    private void ScanForObjects()
    {
        _foundObjects.Clear();
        ClearInspector();

        if (_selectedType == null) return;

        if (IsBehaviour(_selectedType))
        {
            var found = UnityEngine.Object.FindObjectsByType(_selectedType, FindObjectsSortMode.InstanceID);
            
            Array.Sort(found, (x,y)=> String.Compare(x.name, y.name, StringComparison.Ordinal));
            
            foreach (var o in found)
            {
                if (o != null)
                {
                    _foundObjects.Add(o);
                }
            }

            _countLabel.text = $"{_foundObjects.Count} object{(_foundObjects.Count == 1 ? "" : "s")} in scene";
            _allComponentsToggle.style.display = DisplayStyle.Flex;
        }
        else if (IsScriptable(_selectedType))
        {
            var guids = AssetDatabase.FindAssets($"t:{_selectedType.Name}");
            foreach (var guid in guids)
            {
                var path  = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath(path, _selectedType);
                if (asset != null) _foundObjects.Add(asset);
            }

            _countLabel.text = $"{_foundObjects.Count} asset{(_foundObjects.Count == 1 ? "" : "s")} in project";
            _allComponentsToggle.style.display = DisplayStyle.None;
        }

        _objectList.itemsSource = _foundObjects;
        _objectList.Rebuild();
        ShowEmptyState(_foundObjects.Count == 0);
    }

    // ── Inspector ─────────────────────────────────────────────────────────────

    private void RefreshInspector()
    {
        ClearInspector();

        int idx = _objectList.selectedIndex;
        if (idx < 0 || idx >= _foundObjects.Count || _foundObjects[idx] == null)
        {
            ShowEmptyState(true);
            return;
        }

        ShowEmptyState(false);
        var obj = _foundObjects[idx];

        if (IsBehaviour(_selectedType) && obj is Component comp)
        {
            if (_showAllComponents)
            {
                // Game Object foldout header
                AddGameObjectHeader(comp.gameObject);

                foreach (var c in comp.gameObject.GetComponents<Component>())
                {
                    if (c == null) continue;
                    bool isTarget = c.GetType() == _selectedType || _selectedType.IsInstanceOfType(c);
                    AddInspectorCard(new SerializedObject(c), ObjectNames.NicifyVariableName(c.GetType().Name), isTarget);
                }
            }
            else
            {
                AddInspectorCard(new SerializedObject(comp),
                    ObjectNames.NicifyVariableName(comp.GetType().Name),
                    isTarget: true,
                    showHeader: true);
            }
        }
        else if (IsScriptable(_selectedType) && obj is ScriptableObject so)
        {
            AddInspectorCard(new SerializedObject(so),
                ObjectNames.NicifyVariableName(so.GetType().Name),
                isTarget: true,
                showHeader: true);
        }
    }

    private void AddGameObjectHeader(GameObject go)
    {
        var header = new VisualElement();
        header.style.flexDirection  = FlexDirection.Row;
        header.style.alignItems     = Align.Center;
        header.style.paddingLeft    = 8;
        header.style.paddingTop     = 8;
        header.style.paddingBottom  = 6;
        header.style.marginTop      = 6;

        var goIcon = new Label("◉");
        goIcon.style.color      = new Color(0.85f, 0.75f, 0.25f);
        goIcon.style.fontSize   = 10;
        goIcon.style.marginRight = 6;

        var goName = new Label(go.name);
        goName.style.fontSize  = 13;
        goName.style.unityFontStyleAndWeight = FontStyle.Bold;
        goName.style.color     = C_TEXT;

        var sceneTag = new Label($"  [{go.scene.name}]");
        sceneTag.style.fontSize = 10;
        sceneTag.style.color    = C_TEXT_DIM;
        sceneTag.style.unityTextAlign = TextAnchor.MiddleLeft;
        sceneTag.style.flexGrow = 1;

        var pingBtn = MakePingButton(large: true);
        pingBtn.clicked += () => PingObject(go);

        header.Add(goIcon);
        header.Add(goName);
        header.Add(sceneTag);
        header.Add(pingBtn);
        _inspectorContainer.Add(header);
    }

    private void AddInspectorCard(SerializedObject so, string title, bool isTarget, bool showHeader = false)
    {
        var card = new VisualElement();
        card.style.backgroundColor = C_CARD;
        card.style.marginTop       = 5;
        card.style.marginBottom    = 2;
        card.style.overflow        = Overflow.Hidden;
        SetBorderRadius(card.style, 5);
        if (isTarget)
        {
            card.style.borderLeftWidth = 3;
            card.style.borderLeftColor = C_ACCENT;
        }

        // Card header
        var hdr = Row(C_CARD_HDR, 10, 6);
        hdr.style.borderBottomWidth = 1;
        hdr.style.borderBottomColor = C_BORDER;

        var dot = new Label(isTarget ? "◆" : "·");
        dot.style.fontSize   = isTarget ? 8 : 12;
        dot.style.color      = isTarget ? C_ACCENT : C_TEXT_DIM;
        dot.style.marginRight = 6;
        dot.style.unityTextAlign = TextAnchor.MiddleCenter;

        var titleLbl = new Label(title);
        titleLbl.style.fontSize   = 11;
        titleLbl.style.flexGrow   = 1;
        titleLbl.style.color      = isTarget ? C_TEXT : C_SUBTEXT;
        titleLbl.style.unityFontStyleAndWeight = isTarget ? FontStyle.Bold : FontStyle.Normal;

        hdr.Add(dot);
        hdr.Add(titleLbl);

        // Ping button on the targeted card only
        if (isTarget && so.targetObject != null)
        {
            var pingBtn = MakePingButton(large: false);
            var capturedTarget = so.targetObject;
            pingBtn.clicked += () => PingObject(capturedTarget);
            hdr.Add(pingBtn);
        }

        card.Add(hdr);

        // Inspector element
        var inspector = new InspectorElement(so);
        inspector.style.paddingLeft   = 2;
        inspector.style.paddingRight  = 2;
        inspector.style.paddingBottom = 4;
        card.Add(inspector);

        _inspectorContainer.Add(card);
    }

    private void ClearInspector()
    {
        _inspectorContainer.Clear();
        _inspectorContainer.Add(_emptyState); // always keep in DOM, toggle visibility
        _emptyState.style.display = DisplayStyle.None;
    }

    private void ShowEmptyState(bool show)
    {
        _emptyState.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // ── Ping ──────────────────────────────────────────────────────────────────

    private static void PingObject(UnityEngine.Object obj)
    {
        if (obj == null) return;

        UnityEngine.Object target = obj is Component c ? c.gameObject : obj;

        // Highlight in Hierarchy / Project window
        EditorGUIUtility.PingObject(target);

        // Select so the default Inspector updates
        Selection.activeObject = target;

        // Center the Scene View on the object (behaviours only)
        if (target is GameObject go)
        {
            var scene = SceneView.lastActiveSceneView;
            if (scene != null)
            {
                scene.Frame(go.GetComponent<Renderer>() != null
                    ? go.GetComponent<Renderer>().bounds
                    : new Bounds(go.transform.position, Vector3.one * 2f),
                    instant: false);
                scene.Repaint();
            }
        }
    }

    private static void StyleIconButton(Button btn, int size)
    {
        btn.style.fontSize        = size == 22 ? 11 : 12;
        btn.style.width           = size;
        btn.style.height          = size;
        btn.style.paddingLeft     = 0;
        btn.style.paddingRight    = 0;
        btn.style.paddingTop      = 0;
        btn.style.paddingBottom   = 0;
        btn.style.marginLeft      = 2;
        btn.style.backgroundColor = new Color(0, 0, 0, 0);
        btn.style.color           = C_TEXT_DIM;
        btn.style.borderTopWidth  = btn.style.borderRightWidth =
            btn.style.borderBottomWidth = btn.style.borderLeftWidth = 0;
        SetBorderRadius(btn.style, 3);
    }

    private Button MakePingButton(bool large)
    {
        int size = large ? 26 : 20;
        var btn  = new Button { text = "⦿", tooltip = "Ping in Hierarchy / Project" };
        btn.style.fontSize       = large ? 13 : 11;
        btn.style.width          = size;
        btn.style.height         = size;
        btn.style.paddingLeft    = 0;
        btn.style.paddingRight   = 0;
        btn.style.paddingTop     = 0;
        btn.style.paddingBottom  = 0;
        btn.style.marginLeft     = 4;
        btn.style.backgroundColor = new Color(0, 0, 0, 0);
        btn.style.color          = C_TEXT_DIM;
        btn.style.borderTopWidth = btn.style.borderRightWidth =
            btn.style.borderBottomWidth = btn.style.borderLeftWidth = 0;
        SetBorderRadius(btn.style, 3);

        btn.RegisterCallback<PointerEnterEvent>(_ =>
        {
            btn.style.color           = C_ACCENT;
            btn.style.backgroundColor = new Color(C_ACCENT.r, C_ACCENT.g, C_ACCENT.b, 0.15f);
        });
        btn.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            btn.style.color           = C_TEXT_DIM;
            btn.style.backgroundColor = new Color(0, 0, 0, 0);
        });
        return btn;
    }

    // ── Style helpers ─────────────────────────────────────────────────────────

    private static VisualElement Row(Color bg, int hPad, int vPad)
    {
        var e = new VisualElement();
        e.style.flexDirection   = FlexDirection.Row;
        e.style.alignItems      = Align.Center;
        e.style.backgroundColor = bg;
        e.style.paddingLeft     = hPad;
        e.style.paddingRight    = hPad;
        e.style.paddingTop      = vPad;
        e.style.paddingBottom   = vPad;
        return e;
    }

    private static Label PanelHeader(string title)
    {
        var h = new Label(title);
        h.style.paddingLeft  = 10;
        h.style.paddingTop   = 5;
        h.style.paddingBottom = 5;
        h.style.fontSize     = 10;
        h.style.letterSpacing = 1.5f;
        h.style.unityFontStyleAndWeight = FontStyle.Bold;
        h.style.color            = C_TEXT_DIM;
        h.style.backgroundColor  = C_HEADER;
        h.style.borderBottomWidth = 1;
        h.style.borderBottomColor = C_BORDER;
        return h;
    }

    private static void StyleButton(Button b, Color bg)
    {
        b.style.backgroundColor = bg;
        b.style.color           = Color.white;
        b.style.paddingLeft     = 10;
        b.style.paddingRight    = 10;
        b.style.paddingTop      = 4;
        b.style.paddingBottom   = 4;
        SetBorderRadius(b.style, 4);
        SetBorderWidth(b.style, 0);
    }

    private static void SetBorderRadius(IStyle s, float r)
    {
        s.borderTopLeftRadius     = r;
        s.borderTopRightRadius    = r;
        s.borderBottomLeftRadius  = r;
        s.borderBottomRightRadius = r;
    }

    private static void SetBorderWidth(IStyle s, float w)
    {
        s.borderTopWidth    = w;
        s.borderRightWidth  = w;
        s.borderBottomWidth = w;
        s.borderLeftWidth   = w;
    }

    private static void Border(IStyle s, Color color,
        float top = 0, float right = 0, float bottom = 0, float left = 0)
    {
        if (top    > 0) { s.borderTopWidth    = top;    s.borderTopColor    = color; }
        if (right  > 0) { s.borderRightWidth  = right;  s.borderRightColor  = color; }
        if (bottom > 0) { s.borderBottomWidth = bottom; s.borderBottomColor = color; }
        if (left   > 0) { s.borderLeftWidth   = left;   s.borderLeftColor   = color; }
    }
}