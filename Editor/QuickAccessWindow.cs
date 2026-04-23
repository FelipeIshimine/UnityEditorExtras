// Place this file inside any Editor/ folder in your project.
// QuickAccessAttribute.cs must be accessible from this assembly.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Profiling.Memory.Experimental;
using UnityEditor.Toolbars;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;


public sealed class QuickAccessWindow : EditorWindow
{
    // ── State ─────────────────────────────────────────────────────────────────

    private class QuickAccessItem
    {
        public int Id;
        public Type Type;
        public UnityEngine.Object Target;
        public List<QuickAccessItem> Children = new();
        public bool IsFolder => Target == null;
        public string Name => IsFolder ? GetLabel(Type) : Target.name;
    }

    private readonly List<Type>              _registeredTypes = new();
    private readonly List<QuickAccessItem>    _treeItems       = new();
    private readonly Dictionary<int, QuickAccessItem> _idToItem = new();
    
    private bool    _showAllComponents;
    private bool    _compactMode;

    // ── UI refs ───────────────────────────────────────────────────────────────

    private Label          _headerTitle;
    private Toggle         _allComponentsToggle;
    private Label          _countLabel;
    private TreeView       _treeView;
    private MultiColumnListView _tableView;
    private VisualElement  _inspectorContainer;
    private VisualElement  _emptyState;
    private VisualElement  _contentRow;      // replaces TwoPaneSplitView
    private VisualElement  _leftPanel;
    private VisualElement  _rightPanel;
    private float          _leftPanelWidth = 250f;
    private Button         _compactToggleBtn;

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

    const string ButtonID = "Game/QuickAccess";
    [MainToolbarElement(ButtonID, defaultDockPosition = MainToolbarDockPosition.Left)]
    public static MainToolbarElement CreateButton()
    {
	    var content = new MainToolbarContent(
		    "⚡︎",
		    "QuickAccessAttribute window");

	    return new MainToolbarButton(content, ShowWindow);
    }

    
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
        RefreshTree();
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

        ApplyCompactMode();
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

        var titleLabel = new Label("QUICK ACCESS");
        titleLabel.style.fontSize = 12;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.color = C_TEXT;
        titleLabel.style.flexGrow = 1;

        var refreshBtn = new Button(OnRefreshClicked) { text = "↺  Refresh" };
        StyleButton(refreshBtn, C_ACCENT);
        refreshBtn.style.width = 90;

        bar.Add(badge);
        bar.Add(titleLabel);
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
        _contentRow = new VisualElement();
        _contentRow.style.flexGrow      = 1;
        _contentRow.style.flexDirection = FlexDirection.Row;

        _leftPanel  = BuildLeftPanel();
        _rightPanel = BuildRightPanel();

        _contentRow.Add(_leftPanel);
        _contentRow.Add(_rightPanel);
        return _contentRow;
    }

    // Left panel header with compact toggle

    private VisualElement BuildObjectsPanelHeader()
    {
        var hdr = new VisualElement();
        hdr.style.flexDirection   = FlexDirection.Row;
        hdr.style.alignItems      = Align.Center;
        hdr.style.backgroundColor = C_HEADER;
        hdr.style.paddingLeft     = 10;
        hdr.style.paddingTop      = 5;
        hdr.style.paddingBottom   = 5;
        hdr.style.borderBottomWidth = 1;
        hdr.style.borderBottomColor = C_BORDER;

        var collapseBtn = new Button(() => _treeView.CollapseAll()) { text = "▲" };
        var expandBtn = new Button(() => _treeView.ExpandAll()) { text = "▼" };
        StyleIconButton(collapseBtn, 18);
        StyleIconButton(expandBtn, 18);
        collapseBtn.tooltip = "Collapse All";
        expandBtn.tooltip = "Expand All";

        _headerTitle = new Label("EXPLORER");
        _headerTitle.style.fontSize     = 10;
        _headerTitle.style.letterSpacing = 1.5f;
        _headerTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        _headerTitle.style.color        = C_TEXT_DIM;
        _headerTitle.style.flexGrow     = 1;
        _headerTitle.style.marginLeft   = 4;

        _compactToggleBtn = new Button(OnCompactToggleClicked);
        _compactToggleBtn.text    = "▶";   // will be updated by ApplyCompactMode
        _compactToggleBtn.tooltip = "Compact mode: hide inspector panel";
        _compactToggleBtn.style.width        = 18;
        _compactToggleBtn.style.height       = 18;
        _compactToggleBtn.style.marginRight  = 2;
        _compactToggleBtn.style.paddingLeft  = 0;
        _compactToggleBtn.style.paddingRight = 0;
        _compactToggleBtn.style.paddingTop   = 0;
        _compactToggleBtn.style.paddingBottom = 0;
        _compactToggleBtn.style.fontSize     = 9;
        _compactToggleBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
        _compactToggleBtn.style.backgroundColor = new Color(0, 0, 0, 0);
        _compactToggleBtn.style.color        = C_TEXT_DIM;
        _compactToggleBtn.style.borderTopWidth = _compactToggleBtn.style.borderRightWidth =
            _compactToggleBtn.style.borderBottomWidth = _compactToggleBtn.style.borderLeftWidth = 0;
        SetBorderRadius(_compactToggleBtn.style, 3);

        _compactToggleBtn.RegisterCallback<PointerEnterEvent>(_ =>
        {
            _compactToggleBtn.style.color           = C_TEXT;
            _compactToggleBtn.style.backgroundColor = new Color(C_ACCENT.r, C_ACCENT.g, C_ACCENT.b, 0.15f);
        });
        _compactToggleBtn.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            _compactToggleBtn.style.color           = _compactMode ? C_ACCENT : C_TEXT_DIM;
            _compactToggleBtn.style.backgroundColor = new Color(0, 0, 0, 0);
        });

        hdr.Add(collapseBtn);
        hdr.Add(expandBtn);
        hdr.Add(_headerTitle);
        hdr.Add(_compactToggleBtn);
        return hdr;
    }

    // Left – object list

    private VisualElement BuildLeftPanel()
    {
        var panel = new VisualElement();
        panel.style.width           = _leftPanelWidth;
        panel.style.flexShrink      = 0;
        panel.style.backgroundColor = C_PANEL_L;

        panel.Add(BuildObjectsPanelHeader());

        _treeView = new TreeView
        {
            fixedItemHeight = 24,
            selectionType   = SelectionType.Single,
            makeItem        = MakeTreeItem,
            bindItem        = BindTreeItem
        };
        _treeView.style.flexGrow = 1;
        _treeView.selectionChanged += _ => RefreshInspector();

        panel.Add(_treeView);
        return panel;
    }

    private VisualElement MakeTreeItem()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems    = Align.Center;
        row.style.paddingLeft   = 4;

        var icon = new Label { name = "icon" };
        icon.style.marginRight = 4;
        icon.style.fontSize = 12;

        var label = new Label { name = "label" };
        label.style.fontSize = 12;
        label.style.color = C_TEXT;

        row.Add(icon);
        row.Add(label);
        return row;
    }

    private void BindTreeItem(VisualElement element, int index)
    {
        var item = _treeView.GetItemDataForIndex<QuickAccessItem>(index);
        if (item == null) return;

        var icon = element.Q<Label>("icon");
        var label = element.Q<Label>("label");

        label.text = item.Name;
        if (item.IsFolder)
        {
            //icon.text = "📁";
            //icon.text = "📁";
            //icon.style.color = C_TEXT_DIM;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
        }
        else
        {
            //icon.text = "◈";
            //icon.style.color = C_ACCENT;
            label.style.unityFontStyleAndWeight = FontStyle.Normal;
        }

        // Support for renaming via context menu or similar if needed later, 
        // but for now we removed the old Rename helpers that relied on _foundObjects.
    }

    // Right – inspector

    private VisualElement BuildRightPanel()
    {
        var panel = new VisualElement();
        panel.style.flexGrow        = 1;
        panel.style.backgroundColor = C_PANEL_R;

        panel.Add(PanelHeader("INSPECTOR"));

        // ── Table View (multi-column list) ────────────────────────────────────
        _tableView = new MultiColumnListView
        {
            fixedItemHeight = 24,
            headerTitle     = "OBJECTS",
            showAlternatingRowBackgrounds = AlternatingRowBackground.All,
            selectionType   = SelectionType.Single
        };
        _tableView.style.flexGrow = 1;
        _tableView.style.display  = DisplayStyle.None;
        _tableView.onSelectionChange += items =>
        {
            var item = items.FirstOrDefault() as QuickAccessItem;
            if (item != null)
            {
                _treeView.SetSelectionById(item.Id);
                _treeView.ScrollToItemById(item.Id);
            }
        };

        // ── Inspector View ────────────────────────────────────────────────────
        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow = 1;

        _inspectorContainer = new VisualElement();
        _inspectorContainer.style.flexGrow  = 1;
        _inspectorContainer.style.paddingLeft   = 6;
        _inspectorContainer.style.paddingRight  = 6;
        _inspectorContainer.style.paddingBottom = 10;

        _emptyState = BuildEmptyState("Select an item\nto explore.");
        _inspectorContainer.Add(_emptyState);

        scroll.Add(_inspectorContainer);
        
        panel.Add(_tableView);
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

    // ── Navigation Logic ─────────────────────────────────────────────────────

    private void RefreshTree()
    {
        _treeItems.Clear();
        _idToItem.Clear();
        int nextId = 1;

        foreach (var type in _registeredTypes)
        {
            List<UnityEngine.Object> objects = new();
            if (IsBehaviour(type))
            {
                var found = UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.InstanceID);
                Array.Sort(found, (x, y) => string.Compare(x.name, y.name, StringComparison.Ordinal));
                objects.AddRange(found);
            }
            else if (IsScriptable(type))
            {
                var guids = AssetDatabase.FindAssets($"t:{type.Name}");
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath(path, type);
                    if (asset != null) objects.Add(asset);
                }
                objects.Sort((x, y) => string.Compare(x.name, y.name, StringComparison.Ordinal));
            }

            if (objects.Count == 1)
            {
                var leaf = new QuickAccessItem { Id = nextId++, Type = type, Target = objects[0] };
                _idToItem[leaf.Id] = leaf;
                _treeItems.Add(leaf);
            }
            else
            {
                var folder = new QuickAccessItem { Id = nextId++, Type = type };
                _idToItem[folder.Id] = folder;
                _treeItems.Add(folder);

                foreach (var obj in objects)
                {
                    if (obj == null) continue;
                    var leaf = new QuickAccessItem { Id = nextId++, Type = type, Target = obj };
                    _idToItem[leaf.Id] = leaf;
                    folder.Children.Add(leaf);
                }
            }
        }

        _treeView.SetRootItems(_treeItems.Select(ti => new TreeViewItemData<QuickAccessItem>(ti.Id, ti, ti.Children.Select(c => new TreeViewItemData<QuickAccessItem>(c.Id, c)).ToList())).ToList());
        _treeView.Rebuild();
    }

    private void OnRefreshClicked()
    {
        DiscoverTypes();
        RefreshTree();
    }

    private void OnCompactToggleClicked()
    {
        _compactMode = !_compactMode;
        ApplyCompactMode();
        RefreshInspector();
    }

    // ── Compact mode ──────────────────────────────────────────────────────────

    private void ApplyCompactMode()
    {
        _compactToggleBtn.text    = _compactMode ? "◀" : "▶";
        _compactToggleBtn.tooltip = _compactMode
            ? "Show inspector panel"
            : "Hide inspector panel — selection shown in Unity's Inspector";
        _compactToggleBtn.style.color = _compactMode ? C_ACCENT : C_TEXT_DIM;

        if (_compactMode)
        {
            if (_leftPanel.resolvedStyle.width > 10)
                _leftPanelWidth = _leftPanel.resolvedStyle.width;

            _leftPanel.style.width    = StyleKeyword.Auto;
            _leftPanel.style.flexGrow = 1;
            _rightPanel.style.display = DisplayStyle.None;
        }
        else
        {
            _leftPanel.style.flexGrow = 0;
            _leftPanel.style.width    = _leftPanelWidth;
            _rightPanel.style.display = DisplayStyle.Flex;
        }

        if (_allComponentsToggle != null)
        {
            var selected = _treeView.selectedItem as QuickAccessItem;
            bool isBehaviour = selected != null && !selected.IsFolder && IsBehaviour(selected.Type);
            _allComponentsToggle.style.display = (_compactMode || !isBehaviour) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        minSize = _compactMode ? new Vector2(280, 460) : new Vector2(680, 460);
    }

    // ── Table View ────────────────────────────────────────────────────────────

    private void SetupTableView(Type type)
    {
        _tableView.columns.Clear();
        
        // Name Column
        var nameCol = new Column { title = "Name", width = 150, stretchable = true };
        nameCol.makeCell = () => new Label();
        nameCol.bindCell = (e, i) => ((Label)e).text = ((QuickAccessItem)_tableView.itemsSource[i]).Target.name;
        _tableView.columns.Add(nameCol);

        // Discovery of properties for this type
        if (IsBehaviour(type) || IsScriptable(type))
        {
            var dummy = CreateInstance(type);
            var so = new SerializedObject(dummy);
            var prop = so.GetIterator();
            prop.NextVisible(true); // skip m_Script

            int count = 0;
            while (prop.NextVisible(false) && count < 3)
            {
                string propPath = prop.propertyPath;
                string propName = prop.displayName;
                
                var col = new Column { title = propName, width = 80, stretchable = true };
                col.makeCell = () => new Label();
                col.bindCell = (e, i) =>
                {
                    var item = (QuickAccessItem)_tableView.itemsSource[i];
                    if (item.Target == null) return;
                    var itemSO = new SerializedObject(item.Target);
                    var itemProp = itemSO.FindProperty(propPath);
                    ((Label)e).text = itemProp != null ? GetPropertyValueAsString(itemProp) : "—";
                };
                _tableView.columns.Add(col);
                count++;
            }
            DestroyImmediate(dummy);
        }
    }

    private string GetPropertyValueAsString(SerializedProperty prop)
    {
        return prop.propertyType switch
        {
            SerializedPropertyType.Integer => prop.intValue.ToString(),
            SerializedPropertyType.Boolean => prop.boolValue ? "✔" : "✘",
            SerializedPropertyType.Float => prop.floatValue.ToString("F2"),
            SerializedPropertyType.String => prop.stringValue,
            SerializedPropertyType.Color => "#" + ColorUtility.ToHtmlStringRGB(prop.colorValue),
            SerializedPropertyType.ObjectReference => prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "None",
            SerializedPropertyType.Enum => prop.enumDisplayNames[prop.enumValueIndex],
            _ => "..."
        };
    }

    // ── Inspector ─────────────────────────────────────────────────────────────

    private void RefreshInspector()
    {
        var item = _treeView.selectedItem as QuickAccessItem;
        if (item == null)
        {
            _tableView.style.display = DisplayStyle.None;
            _inspectorContainer.parent.style.display = DisplayStyle.Flex;
            if (!_compactMode) ShowEmptyState(true);
            _countLabel.text = "—";
            return;
        }

        // ── Compact mode: delegate to Unity's own Inspector ───────────────────
        if (_compactMode && !item.IsFolder)
        {
            UnityEngine.Object target = item.Target is Component c ? c.gameObject : item.Target;
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
            return;
        }

        if (item.IsFolder)
        {
            // Show Table View
            _inspectorContainer.parent.style.display = DisplayStyle.None;
            _tableView.style.display = DisplayStyle.Flex;
            _tableView.itemsSource = item.Children;
            SetupTableView(item.Type);
            _tableView.Rebuild();
            _countLabel.text = $"{item.Children.Count} items";
            _headerTitle.text = item.Name.ToUpper();
        }
        else
        {
            // Show Object Inspector
            _tableView.style.display = DisplayStyle.None;
            _inspectorContainer.parent.style.display = DisplayStyle.Flex;
            ClearInspector();
            ShowEmptyState(false);
            _countLabel.text = "1 item";
            _headerTitle.text = item.Target.name.ToUpper();

            if (IsBehaviour(item.Type) && item.Target is Component comp)
            {
                if (_showAllComponents)
                {
                    AddGameObjectHeader(comp.gameObject);
                    foreach (var c2 in comp.gameObject.GetComponents<Component>())
                    {
                        if (c2 == null) continue;
                        bool isTarget = c2.GetType() == item.Type || item.Type.IsInstanceOfType(c2);
                        AddInspectorCard(new SerializedObject(c2), ObjectNames.NicifyVariableName(c2.GetType().Name), isTarget);
                    }
                }
                else
                {
                    AddInspectorCard(new SerializedObject(comp), ObjectNames.NicifyVariableName(comp.GetType().Name), true, true);
                }
            }
            else if (IsScriptable(item.Type) && item.Target is ScriptableObject so)
            {
                AddInspectorCard(new SerializedObject(so), ObjectNames.NicifyVariableName(so.GetType().Name), true, true);
            }
        }
        
        ApplyCompactMode(); 
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

        if (isTarget && so.targetObject != null)
        {
            var pingBtn = MakePingButton(large: false);
            var capturedTarget = so.targetObject;
            pingBtn.clicked += () => PingObject(capturedTarget);
            hdr.Add(pingBtn);
        }

        card.Add(hdr);

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
        _inspectorContainer.Add(_emptyState);
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

        EditorGUIUtility.PingObject(target);
        Selection.activeObject = target;

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

