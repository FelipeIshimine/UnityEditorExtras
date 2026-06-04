// A slide-in popup variant of QuickAccessWindow.
//
// Press the shortcut (Edit ▸ Shortcuts ▸ "Window/Quick Access Popup", default Ctrl/Cmd+Shift+Q)
// and a borderless panel folds open from the left edge, hovering above the docked editor.
// It shows the same [QuickAccess] list; selecting an item swaps the panel to an inspector,
// and the ◀ arrow returns to the list. The last selection is remembered between opens.
// Press the shortcut again, or Esc, to dismiss.
//
// Reuses QuickAccessAttribute from the Runtime assembly.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class QuickAccessPopup : EditorWindow
{
    // ── Singleton / toggle ─────────────────────────────────────────────────────

    private static QuickAccessPopup _instance;

    [Shortcut("Window/Quick Access Popup", KeyCode.Q, ShortcutModifiers.Action | ShortcutModifiers.Shift)]
    public static void Toggle()
    {
        if (_instance != null)
        {
            _instance.Close();
            return;
        }

        var w = CreateInstance<QuickAccessPopup>();
        _instance = w;

        var main = EditorGUIUtility.GetMainWindowPosition();
        w.position = new Rect(main.x, main.y, CollapsedWidth, main.height);
        w.ShowPopup();
        w.Focus();
        w.StartOpenAnimation(main);
    }

    // ── Layout constants ───────────────────────────────────────────────────────

    const float CollapsedWidth = 8f;
    const float ExpandedWidth  = 520f;
    const string LastSelKey    = "QuickAccessPopup.LastSelection"; // stored in SessionState

    // ── State ──────────────────────────────────────────────────────────────────

    private class Item
    {
        public int Id;
        public Type Type;
        public UnityEngine.Object Target;
        public readonly List<Item> Children = new();
        public bool IsFolder => Target == null;
        public string Name => IsFolder ? GetLabel(Type) : (Target != null ? Target.name : "<missing>");
        // Stable identity across rebuilds (Id is regenerated each refresh, so don't persist it).
        public string Key => $"{Type.FullName}|{(IsFolder ? "" : Name)}";
    }

    private readonly List<Type> _types = new();
    private readonly List<Item> _roots = new();
    private readonly Dictionary<int, Item> _idToItem = new();

    // ── UI refs ────────────────────────────────────────────────────────────────

    private VisualElement _listView;
    private VisualElement _inspectorView;
    private TreeView      _treeView;
    private VisualElement _inspectorBody;
    private Label         _inspectorTitle;

    private Rect  _targetRect;
    private double _animEnd;

    // ── Colors (matched to QuickAccessWindow) ──────────────────────────────────

    static readonly Color C_BG       = new(0.160f, 0.160f, 0.160f);
    static readonly Color C_HEADER   = new(0.130f, 0.130f, 0.130f);
    static readonly Color C_BORDER   = new(0.090f, 0.090f, 0.090f);
    static readonly Color C_ACCENT   = new(0.255f, 0.490f, 0.965f);
    static readonly Color C_TEXT     = new(0.850f, 0.850f, 0.850f);
    static readonly Color C_TEXT_DIM = new(0.500f, 0.500f, 0.500f);
    static readonly Color C_CARD     = new(0.220f, 0.220f, 0.220f);
    static readonly Color C_CARD_HDR = new(0.175f, 0.175f, 0.175f);

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void OnEnable()  => _instance = this;
    private void OnDisable() { if (_instance == this) _instance = null; EditorApplication.update -= Animate; }

    // Esc closes; clicking elsewhere does NOT (dismiss is Esc / hotkey only).
    private void OnLostFocus() { /* intentionally stays open */ }

    public void CreateGUI()
    {
        Discover();
        Build();
        BuildTree();
        RestoreLastSelection();
    }

    // ── Open animation ─────────────────────────────────────────────────────────

    private void StartOpenAnimation(Rect main)
    {
        _targetRect = new Rect(main.x, main.y, ExpandedWidth, main.height);
        _animEnd = EditorApplication.timeSinceStartup + 0.14;
        EditorApplication.update -= Animate;
        EditorApplication.update += Animate;
    }

    private void Animate()
    {
        double remaining = _animEnd - EditorApplication.timeSinceStartup;
        if (remaining <= 0)
        {
            position = _targetRect;
            EditorApplication.update -= Animate;
            Focus();
            return;
        }

        float t = 1f - (float)(remaining / 0.14);
        t = 1f - (1f - t) * (1f - t); // ease-out
        float w = Mathf.Lerp(CollapsedWidth, ExpandedWidth, t);
        position = new Rect(_targetRect.x, _targetRect.y, w, _targetRect.height);
    }

    // ── Discovery (same rules as QuickAccessWindow) ────────────────────────────

    private void Discover()
    {
        _types.Clear();
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                _types.AddRange(asm.GetTypes().Where(t =>
                    !t.IsAbstract && !t.IsGenericTypeDefinition &&
                    t.GetCustomAttribute<QuickAccessAttribute>(false) != null &&
                    (typeof(Component).IsAssignableFrom(t) || typeof(ScriptableObject).IsAssignableFrom(t))));
            }
            catch { /* skip inaccessible assemblies */ }
        }
        _types.Sort((a, b) => string.Compare(GetLabel(a), GetLabel(b), StringComparison.OrdinalIgnoreCase));
    }

    private static string GetLabel(Type t) => t.GetCustomAttribute<QuickAccessAttribute>()?.Label ?? t.Name;
    private static bool IsBehaviour(Type t)  => typeof(Component).IsAssignableFrom(t);
    private static bool IsScriptable(Type t) => typeof(ScriptableObject).IsAssignableFrom(t);

    // ── UI ─────────────────────────────────────────────────────────────────────

    private void Build()
    {
        var root = rootVisualElement;
        root.Clear();
        root.style.flexGrow = 1;
        root.style.backgroundColor = C_BG;
        root.style.borderRightWidth = 1;
        root.style.borderRightColor = C_ACCENT;
        root.focusable = true;
        root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

        root.Add(BuildListView());
        root.Add(BuildInspectorView());

        ShowList();
        // Grab keyboard focus so Esc works immediately.
        root.schedule.Execute(() => root.Focus()).StartingIn(50);
    }

    private VisualElement BuildListView()
    {
        _listView = new VisualElement { style = { flexGrow = 1 } };

        var hdr = Header();
        var badge = new Label("⚡") { style = { color = C_ACCENT, fontSize = 14, marginRight = 6 } };
        var title = new Label("QUICK ACCESS")
        { style = { fontSize = 11, color = C_TEXT, unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1, letterSpacing = 1f } };
        var close = IconButton("✕", () => Close());
        hdr.Add(badge); hdr.Add(title); hdr.Add(close);
        _listView.Add(hdr);

        _treeView = new TreeView
        {
            fixedItemHeight = 24,
            selectionType   = SelectionType.Single,
            makeItem        = MakeRow,
            bindItem        = BindRow
        };
        _treeView.style.flexGrow = 1;
        _treeView.selectionChanged += _ => OnSelectionChanged();
        _listView.Add(_treeView);
        return _listView;
    }

    private VisualElement BuildInspectorView()
    {
        _inspectorView = new VisualElement { style = { flexGrow = 1, display = DisplayStyle.None } };

        var hdr = Header();
        var back = IconButton("◀", ShowList);
        back.tooltip = "Back to list";
        _inspectorTitle = new Label("INSPECTOR")
        { style = { fontSize = 11, color = C_TEXT, unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1, marginLeft = 4, letterSpacing = 1f } };
        var close = IconButton("✕", () => Close());
        hdr.Add(back); hdr.Add(_inspectorTitle); hdr.Add(close);
        _inspectorView.Add(hdr);

        var scroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
        _inspectorBody = new VisualElement
        { style = { flexGrow = 1, paddingLeft = 6, paddingRight = 6, paddingBottom = 10 } };
        scroll.Add(_inspectorBody);
        _inspectorView.Add(scroll);
        return _inspectorView;
    }

    private void ShowList()
    {
        _listView.style.display = DisplayStyle.Flex;
        _inspectorView.style.display = DisplayStyle.None;
        rootVisualElement.Focus();
    }

    private void ShowInspector()
    {
        _listView.style.display = DisplayStyle.None;
        _inspectorView.style.display = DisplayStyle.Flex;
    }

    // ── Tree build ─────────────────────────────────────────────────────────────

    private void BuildTree()
    {
        _roots.Clear();
        _idToItem.Clear();
        int id = 1;

        foreach (var type in _types)
        {
            var objects = new List<UnityEngine.Object>();
            if (IsBehaviour(type))
            {
                var found = UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.InstanceID);
                Array.Sort(found, (x, y) => string.Compare(x.name, y.name, StringComparison.Ordinal));
                objects.AddRange(found);
            }
            else if (IsScriptable(type))
            {
                foreach (var guid in AssetDatabase.FindAssets($"t:{type.Name}"))
                {
                    var asset = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), type);
                    if (asset != null) objects.Add(asset);
                }
                objects.Sort((x, y) => string.Compare(x.name, y.name, StringComparison.Ordinal));
            }

            if (objects.Count == 1)
            {
                var leaf = new Item { Id = id++, Type = type, Target = objects[0] };
                _idToItem[leaf.Id] = leaf;
                _roots.Add(leaf);
            }
            else
            {
                var folder = new Item { Id = id++, Type = type };
                _idToItem[folder.Id] = folder;
                _roots.Add(folder);
                foreach (var obj in objects)
                {
                    if (obj == null) continue;
                    var leaf = new Item { Id = id++, Type = type, Target = obj };
                    _idToItem[leaf.Id] = leaf;
                    folder.Children.Add(leaf);
                }
            }
        }

        _treeView.SetRootItems(_roots.Select(r =>
            new TreeViewItemData<Item>(r.Id, r,
                r.Children.Select(c => new TreeViewItemData<Item>(c.Id, c)).ToList())).ToList());
        _treeView.Rebuild();
    }

    private VisualElement MakeRow()
    {
        var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, paddingLeft = 4 } };
        row.Add(new Label { name = "label", style = { fontSize = 12, color = C_TEXT } });
        return row;
    }

    private void BindRow(VisualElement e, int index)
    {
        var item = _treeView.GetItemDataForIndex<Item>(index);
        if (item == null) return;
        var label = e.Q<Label>("label");
        label.text = item.Name;
        label.style.unityFontStyleAndWeight = item.IsFolder ? FontStyle.Bold : FontStyle.Normal;
    }

    // ── Selection ──────────────────────────────────────────────────────────────

    private void OnSelectionChanged()
    {
        if (_treeView.selectedItem is not Item item) return;
        if (item.IsFolder) { _treeView.ExpandItem(item.Id); return; }

        SessionState.SetString(LastSelKey, item.Key);
        BuildInspector(item);
        ShowInspector();
    }

    private void RestoreLastSelection()
    {
        var key = SessionState.GetString(LastSelKey, null);
        if (string.IsNullOrEmpty(key)) return;
        var match = _idToItem.Values.FirstOrDefault(i => !i.IsFolder && i.Key == key);
        if (match == null) return;
        _treeView.SetSelectionById(match.Id);
        _treeView.ScrollToItemById(match.Id);
        BuildInspector(match);
        ShowInspector();
    }

    private void BuildInspector(Item item)
    {
        _inspectorBody.Clear();
        _inspectorTitle.text = item.Name.ToUpper();
        if (item.Target == null) return;

        if (IsBehaviour(item.Type) && item.Target is Component comp)
            AddCard(new SerializedObject(comp), ObjectNames.NicifyVariableName(comp.GetType().Name), comp);
        else if (IsScriptable(item.Type) && item.Target is ScriptableObject so)
            AddCard(new SerializedObject(so), ObjectNames.NicifyVariableName(so.GetType().Name), so);
    }

    private void AddCard(SerializedObject so, string title, UnityEngine.Object pingTarget)
    {
        var card = new VisualElement { style = { backgroundColor = C_CARD, marginTop = 5, overflow = Overflow.Hidden } };
        SetRadius(card.style, 5);
        card.style.borderLeftWidth = 3;
        card.style.borderLeftColor = C_ACCENT;

        var hdr = new VisualElement
        { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, backgroundColor = C_CARD_HDR,
                    paddingLeft = 10, paddingRight = 6, paddingTop = 6, paddingBottom = 6 } };
        hdr.Add(new Label("◆") { style = { fontSize = 8, color = C_ACCENT, marginRight = 6 } });
        hdr.Add(new Label(title) { style = { fontSize = 11, color = C_TEXT, flexGrow = 1, unityFontStyleAndWeight = FontStyle.Bold } });
        hdr.Add(IconButton("⦿", () => Ping(pingTarget), "Ping / select in Hierarchy / Project"));
        card.Add(hdr);

        var inspector = new InspectorElement(so)
        { style = { paddingLeft = 14, paddingRight = 2, paddingBottom = 4 } };
        card.Add(inspector);
        _inspectorBody.Add(card);
    }

    private static void Ping(UnityEngine.Object obj)
    {
        if (obj == null) return;
        UnityEngine.Object target = obj is Component c ? c.gameObject : obj;
        EditorGUIUtility.PingObject(target);
        Selection.activeObject = target;
    }

    // ── Input ──────────────────────────────────────────────────────────────────

    private void OnKeyDown(KeyDownEvent e)
    {
        if (e.keyCode == KeyCode.Escape)
        {
            e.StopPropagation();
            Close();
        }
        else if (e.keyCode == KeyCode.LeftArrow && _inspectorView.style.display == DisplayStyle.Flex)
        {
            e.StopPropagation();
            ShowList();
        }
    }

    // ── Style helpers ──────────────────────────────────────────────────────────

    private static VisualElement Header()
    {
        var h = new VisualElement
        { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, backgroundColor = C_HEADER,
                    paddingLeft = 8, paddingRight = 6, paddingTop = 6, paddingBottom = 6,
                    borderBottomWidth = 1, borderBottomColor = C_BORDER } };
        return h;
    }

    private static Button IconButton(string text, Action onClick, string tooltip = null)
    {
        var b = new Button(onClick) { text = text, tooltip = tooltip };
        b.style.width = 20; b.style.height = 20;
        b.style.paddingLeft = b.style.paddingRight = b.style.paddingTop = b.style.paddingBottom = 0;
        b.style.marginLeft = 2;
        b.style.fontSize = 11;
        b.style.color = C_TEXT_DIM;
        b.style.backgroundColor = new Color(0, 0, 0, 0);
        b.style.borderTopWidth = b.style.borderRightWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = 0;
        SetRadius(b.style, 3);
        b.RegisterCallback<PointerEnterEvent>(_ => { b.style.color = C_TEXT; b.style.backgroundColor = new Color(C_ACCENT.r, C_ACCENT.g, C_ACCENT.b, 0.15f); });
        b.RegisterCallback<PointerLeaveEvent>(_ => { b.style.color = C_TEXT_DIM; b.style.backgroundColor = new Color(0, 0, 0, 0); });
        return b;
    }

    private static void SetRadius(IStyle s, float r)
    {
        s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = r;
    }
}
