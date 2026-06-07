// A slide-in popup variant of QuickAccessWindow.
//
// Press the shortcut (Edit ▸ Shortcuts ▸ "Window/Quick Access Popup", default Ctrl/Cmd+Shift+Q)
// and a borderless panel folds open from the left edge, hovering above the docked editor.
// Tabs along the top list every [QuickAccess] type that has at least one instance. Selecting a
// tab shows that type's instances; selecting an instance swaps the body to its inspector while
// the tabs stay visible. When a type has a single instance the list is skipped and the instance
// is shown directly. The last selection is remembered between opens.
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
    const float ExpandedWidth  = 620f;
    const string LastTypeKey   = "QuickAccessPopup.LastType";     // stored in SessionState
    const string LastSelKey    = "QuickAccessPopup.LastSelection"; // stored in SessionState

    // ── State ──────────────────────────────────────────────────────────────────

    private class Group
    {
        public Type Type;
        public readonly List<UnityEngine.Object> Objects = new();
        public Button Tab;
        public string Label;
    }

    private readonly List<Group> _groups = new();
    private Group _activeGroup;

    // ── UI refs ────────────────────────────────────────────────────────────────

    private VisualElement _tabBar;
    private VisualElement _listView;
    private VisualElement _listBody;
    private VisualElement _inspectorView;
    private VisualElement _inspectorBody;
    private Label         _inspectorTitle;
    private Button        _backButton;

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
    static readonly Color C_TAB      = new(0.130f, 0.130f, 0.130f);
    static readonly Color C_TAB_HOV  = new(0.200f, 0.200f, 0.200f);

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void OnEnable()  => _instance = this;
    private void OnDisable() { if (_instance == this) _instance = null; EditorApplication.update -= Animate; }

    // Esc closes; clicking elsewhere does NOT (dismiss is Esc / hotkey only).
    private void OnLostFocus() { /* intentionally stays open */ }

    public void CreateGUI()
    {
        Discover();
        Build();
        BuildTabs();
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
        _groups.Clear();

        var types = new List<Type>();
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                types.AddRange(asm.GetTypes().Where(t =>
                    !t.IsAbstract && !t.IsGenericTypeDefinition &&
                    t.GetCustomAttribute<QuickAccessAttribute>(false) != null &&
                    (typeof(Component).IsAssignableFrom(t) || typeof(ScriptableObject).IsAssignableFrom(t))));
            }
            catch { /* skip inaccessible assemblies */ }
        }
        types.Sort((a, b) => string.Compare(GetLabel(a), GetLabel(b), StringComparison.OrdinalIgnoreCase));

        foreach (var type in types)
        {
            var objects = CollectObjects(type);
            if (objects.Count == 0) continue; // tabs with no instances are not shown
            var group = new Group { Type = type, Label = GetLabel(type) };
            group.Objects.AddRange(objects);
            _groups.Add(group);
        }
    }

    private static List<UnityEngine.Object> CollectObjects(Type type)
    {
        var objects = new List<UnityEngine.Object>();
        if (IsBehaviour(type))
        {
            var found = UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.InstanceID);
            Array.Sort(found, (x, y) => string.Compare(x.name, y.name, StringComparison.Ordinal));
            objects.AddRange(found.Where(o => o != null));
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
        return objects;
    }

    private static string GetLabel(Type t) => t.GetCustomAttribute<QuickAccessAttribute>()?.Label ?? t.Name;
    private static bool IsBehaviour(Type t)  => typeof(Component).IsAssignableFrom(t);
    private static bool IsScriptable(Type t) => typeof(ScriptableObject).IsAssignableFrom(t);

    private static string KeyOf(UnityEngine.Object o) => o != null ? o.name : "<missing>";

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

        // Top header (title + close).
        var hdr = Header();
        var badge = new Label("⚡") { style = { color = C_ACCENT, fontSize = 14, marginRight = 6 } };
        var title = new Label("QUICK ACCESS")
        { style = { fontSize = 11, color = C_TEXT, unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1, letterSpacing = 1f } };
        var close = IconButton("✕", () => Close());
        hdr.Add(badge); hdr.Add(title); hdr.Add(close);
        root.Add(hdr);

        // Tab bar (always visible).
        _tabBar = new VisualElement
        { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, backgroundColor = C_HEADER,
                    paddingLeft = 4, paddingRight = 4, paddingTop = 4, paddingBottom = 4,
                    borderBottomWidth = 1, borderBottomColor = C_BORDER } };
        root.Add(_tabBar);

        root.Add(BuildListView());
        root.Add(BuildInspectorView());

        // Grab keyboard focus so Esc works immediately.
        root.schedule.Execute(() => root.Focus()).StartingIn(50);
    }

    private VisualElement BuildListView()
    {
        _listView = new VisualElement { style = { flexGrow = 1, display = DisplayStyle.None } };
        var scroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
        _listBody = new VisualElement { style = { flexGrow = 1, paddingTop = 4 } };
        scroll.Add(_listBody);
        _listView.Add(scroll);
        return _listView;
    }

    private VisualElement BuildInspectorView()
    {
        _inspectorView = new VisualElement { style = { flexGrow = 1, display = DisplayStyle.None } };

        var hdr = Header();
        _backButton = IconButton("◀", ShowList);
        _backButton.tooltip = "Back to list";
        _inspectorTitle = new Label("INSPECTOR")
        { style = { fontSize = 11, color = C_TEXT, unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1, marginLeft = 4, letterSpacing = 1f } };
        hdr.Add(_backButton); hdr.Add(_inspectorTitle);
        _inspectorView.Add(hdr);

        var scroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
        _inspectorBody = new VisualElement
        { style = { flexGrow = 1, paddingLeft = 6, paddingRight = 6, paddingBottom = 10 } };
        scroll.Add(_inspectorBody);
        _inspectorView.Add(scroll);
        return _inspectorView;
    }

    // ── Tabs ───────────────────────────────────────────────────────────────────

    private void BuildTabs()
    {
        _tabBar.Clear();
        foreach (var group in _groups)
        {
            var g = group;
            var tab = new Button(() => SelectTab(g)) { text = g.Label };
            tab.style.marginLeft = 2; tab.style.marginRight = 2; tab.style.marginTop = 1; tab.style.marginBottom = 1;
            tab.style.paddingLeft = 10; tab.style.paddingRight = 10; tab.style.paddingTop = 4; tab.style.paddingBottom = 4;
            tab.style.fontSize = 11;
            tab.style.borderTopWidth = tab.style.borderRightWidth = tab.style.borderLeftWidth = 0;
            tab.style.borderBottomWidth = 2;
            tab.style.borderBottomColor = C_TAB;
            tab.style.backgroundColor = C_TAB;
            tab.style.color = C_TEXT_DIM;
            SetRadius(tab.style, 3);
            tab.RegisterCallback<PointerEnterEvent>(_ => { if (g != _activeGroup) tab.style.backgroundColor = C_TAB_HOV; });
            tab.RegisterCallback<PointerLeaveEvent>(_ => { if (g != _activeGroup) tab.style.backgroundColor = C_TAB; });
            g.Tab = tab;
            _tabBar.Add(tab);
        }
    }

    private void RefreshTabStyles()
    {
        foreach (var g in _groups)
        {
            bool active = g == _activeGroup;
            g.Tab.style.backgroundColor = active ? C_CARD : C_TAB;
            g.Tab.style.borderBottomColor = active ? C_ACCENT : C_TAB;
            g.Tab.style.color = active ? C_TEXT : C_TEXT_DIM;
            g.Tab.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
        }
    }

    private void SelectTab(Group group)
    {
        _activeGroup = group;
        SessionState.SetString(LastTypeKey, group.Type.FullName);
        RefreshTabStyles();

        if (group.Objects.Count == 1)
        {
            // Single instance: skip the list and show it directly.
            SessionState.SetString(LastSelKey, KeyOf(group.Objects[0]));
            BuildInspector(group.Objects[0]);
            ShowInspector();
        }
        else
        {
            BuildList(group);
            ShowList();
        }
    }

    // ── List of instances ──────────────────────────────────────────────────────

    private void BuildList(Group group)
    {
        _listBody.Clear();
        foreach (var obj in group.Objects)
        {
            if (obj == null) continue;
            var o = obj;
            var row = new Button(() => SelectInstance(o)) { text = o.name };
            row.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.style.height = 26;
            row.style.marginLeft = 4; row.style.marginRight = 4; row.style.marginTop = 1; row.style.marginBottom = 1;
            row.style.paddingLeft = 10;
            row.style.fontSize = 12;
            row.style.color = C_TEXT;
            row.style.backgroundColor = C_TAB;
            row.style.borderTopWidth = row.style.borderRightWidth = row.style.borderBottomWidth = row.style.borderLeftWidth = 0;
            row.style.borderLeftWidth = 3;
            row.style.borderLeftColor = C_TAB;
            SetRadius(row.style, 3);
            row.RegisterCallback<PointerEnterEvent>(_ => { row.style.backgroundColor = C_TAB_HOV; row.style.borderLeftColor = C_ACCENT; });
            row.RegisterCallback<PointerLeaveEvent>(_ => { row.style.backgroundColor = C_TAB; row.style.borderLeftColor = C_TAB; });
            _listBody.Add(row);
        }
    }

    private void SelectInstance(UnityEngine.Object obj)
    {
        SessionState.SetString(LastSelKey, KeyOf(obj));
        BuildInspector(obj);
        ShowInspector();
    }

    private void ShowList()
    {
        _listView.style.display = DisplayStyle.Flex;
        _inspectorView.style.display = DisplayStyle.None;
        rootVisualElement.Focus();
    }

    private void ShowInspector()
    {
        // Back button only makes sense when there's a list to go back to.
        _backButton.style.display = (_activeGroup != null && _activeGroup.Objects.Count > 1)
            ? DisplayStyle.Flex : DisplayStyle.None;
        _listView.style.display = DisplayStyle.None;
        _inspectorView.style.display = DisplayStyle.Flex;
    }

    // ── Selection restore ──────────────────────────────────────────────────────

    private void RestoreLastSelection()
    {
        if (_groups.Count == 0) return;

        var typeName = SessionState.GetString(LastTypeKey, null);
        var group = _groups.FirstOrDefault(g => g.Type.FullName == typeName) ?? _groups[0];
        _activeGroup = group;
        RefreshTabStyles();

        if (group.Objects.Count == 1)
        {
            BuildInspector(group.Objects[0]);
            ShowInspector();
            return;
        }

        BuildList(group);

        var selKey = SessionState.GetString(LastSelKey, null);
        var match = !string.IsNullOrEmpty(selKey)
            ? group.Objects.FirstOrDefault(o => KeyOf(o) == selKey)
            : null;
        if (match != null)
        {
            BuildInspector(match);
            ShowInspector();
        }
        else
        {
            ShowList();
        }
    }

    // ── Inspector ──────────────────────────────────────────────────────────────

    private void BuildInspector(UnityEngine.Object target)
    {
        _inspectorBody.Clear();
        _inspectorTitle.text = (target != null ? target.name : "<missing>").ToUpper();
        if (target == null) return;

        if (target is Component comp)
            AddCard(new SerializedObject(comp), ObjectNames.NicifyVariableName(comp.GetType().Name), comp);
        else if (target is ScriptableObject so)
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
        else if (e.keyCode == KeyCode.LeftArrow
                 && _inspectorView.style.display == DisplayStyle.Flex
                 && _activeGroup != null && _activeGroup.Objects.Count > 1)
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
