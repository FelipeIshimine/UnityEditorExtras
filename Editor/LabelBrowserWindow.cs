// File: Editor/LabelBrowserWindow.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public class LabelBrowserWindow : EditorWindow
{
    private const string PREF_SELECTED_LABEL = "LabelBrowser_SelectedLabel";
    private const string SESSION_ASSETS = "LabelBrowser_Assets";
    
    private const float COMPACT_WIDTH = 250f;
    private const float DRAG_THRESHOLD = 6f;

    private List<AssetInfo> _allAssets = new();
    private ListView _assetListView;
    private VisualElement _contentContainer;
    private bool _dragStarted;
    Dictionary<string, bool> _foldoutStates = new();
    
    private bool _isCompact;

    private ListView _labelListView;
    private List<string> _labels = new();

    // Drag state
    private bool _mouseDown;
    private Vector2 _mouseDownPos;

    private VisualElement _rootContainer;
    private string _search = "";
    private ToolbarSearchField _searchField;

    private string _selectedLabel = "All";
    private VisualElement _sidebarContainer;
    
    
    void OnEnable()
    {
        if (!TryLoadAssetsFromCache())
            LoadAssets();
    
        LoadPrefs();
        BuildUI();
        RegisterWindowContextMenu();
        UpdateLayout();
    }
    
    private bool TryLoadAssetsFromCache()
    {
        var json = SessionState.GetString(SESSION_ASSETS, "");
        if (string.IsNullOrEmpty(json)) return false;

        var cache = JsonUtility.FromJson<AssetInfoCache>(json);
        if (cache?.paths == null || cache.paths.Count == 0) return false;

        _allAssets.Clear();
        for (int i = 0; i < cache.paths.Count; i++)
        {
            var path = cache.paths[i];
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (!asset) continue;

            _allAssets.Add(new AssetInfo
            {
                asset = asset,
                path = path,
                folder = Path.GetDirectoryName(path)?.Replace("\\", "/"),
                labels = cache.labelSets[i].Split(';')
            });
        }

        RebuildLabels();
        return true;
    }

    private void SaveAssetsToCache()
    {
        var cache = new AssetInfoCache();
        foreach (var a in _allAssets)
        {
            cache.paths.Add(a.path);
            cache.labelSets.Add(string.Join(";", a.labels));
        }
        SessionState.SetString(SESSION_ASSETS, JsonUtility.ToJson(cache));
    }
    
    private void OnDisable()
    {
        SavePrefs();
    }

    private void OnGUI()
    {
        // Detect width change for responsive layout
        var shouldCompact = position.width < COMPACT_WIDTH;
        if (shouldCompact != _isCompact)
        {
            _isCompact = shouldCompact;
            UpdateLayout();
        }
    }

    [MenuItem("Extras/Label Browser")]
    public static void Open()
    {
        var wnd = GetWindow<LabelBrowserWindow>();
        wnd.titleContent = new GUIContent("Labels");
        wnd.minSize = new Vector2(400, 300);
    }

    private void DeleteLabel(string label)
    {
        if (string.IsNullOrEmpty(label) || label == "All")
            return;

        if (!EditorUtility.DisplayDialog(
                "Delete Label",
                $"Remove label '{label}' from all assets?",
                "Delete",
                "Cancel"))
            return;

        foreach (var assetInfo in _allAssets)
        {
            if (!assetInfo.labels.Contains(label))
                continue;

            var current = AssetDatabase.GetLabels(assetInfo.asset).ToList();
            current.Remove(label);
            AssetDatabase.SetLabels(assetInfo.asset, current.ToArray());
        }

        AssetDatabase.SaveAssets();

        LoadAssets();
        UpdateLayout();
    }

    private class AssetInfo
    {
        public Object asset;
        public string path;
        public string folder;
        public string[] labels;
    }

    #region Data

    private void LoadAssets()
    {
        _allAssets.Clear();

        foreach (var path in AssetDatabase.GetAllAssetPaths())
        {
            if (!path.StartsWith("Assets")) continue;

            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (!asset) continue;

            var labels = AssetDatabase.GetLabels(asset);
            if (labels == null || labels.Length == 0) continue;

            _allAssets.Add(new AssetInfo
            {
                asset = asset,
                path = path,
                folder = Path.GetDirectoryName(path)?.Replace("\\", "/"),
                labels = labels
            });
        }
        _allAssets = _allAssets
            .OrderBy(a => a.asset.name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        RebuildLabels();
        SaveAssetsToCache();
    }

    private void RebuildLabels()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var a in _allAssets)
        foreach (var l in a.labels)
            set.Add(l);

        _labels = set.OrderBy(l => l).ToList();
        _labels.Insert(0, "All");

        if (!_labels.Contains(_selectedLabel))
            _selectedLabel = "All";
    }

    #endregion

    #region UI

    private void BuildUI()
    {
        rootVisualElement.Clear();

        var toolbar = new Toolbar();

        _searchField = new ToolbarSearchField();
        _searchField.RegisterValueChangedCallback(evt =>
        {
            _search = evt.newValue?.Trim() ?? "";
            UpdateLayout();
        });

        var refresh = new ToolbarButton(() =>
        {
            LoadAssets();
            UpdateLayout();
        }) { text = "Refresh" };

        toolbar.Add(_searchField);
        toolbar.Add(new ToolbarSpacer());
        toolbar.Add(refresh);

        rootVisualElement.Add(toolbar);

        _rootContainer = new VisualElement { style = { flexGrow = 1 } };
        rootVisualElement.Add(_rootContainer);
    }

    private void UpdateLayout()
    {
        _rootContainer.Clear();

        if (_isCompact)
            BuildCompactLayout();
        else
            BuildWideLayout();
    }

    #endregion

    #region Wide Mode (Split View)

    private void BuildWideLayout()
    {
        var main = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };

        _sidebarContainer = new VisualElement
        {
            style =
            {
                width = 120,
                borderRightWidth = 1,
                borderRightColor = new Color(0, 0, 0, 0.1f),
                paddingLeft = 6,
                paddingTop = 6
            }
        };

        _labelListView = new ListView
        {
            itemsSource = _labels,
            fixedItemHeight = 22,
            selectionType = SelectionType.Single
        };

        _labelListView.makeItem = () => new Label();
        _labelListView.bindItem = (e, i) => ((Label)e).text = _labels[i];

        _labelListView.selectionChanged += objs =>
        {
            var sel = objs.FirstOrDefault() as string;
            if (sel == null) return;

            _selectedLabel = sel;
            EditorPrefs.SetString(PREF_SELECTED_LABEL, _selectedLabel);
            BuildAssetListWide();
        };

        _labelListView.RegisterCallback<ContextClickEvent>(evt =>
        {
            var index = _labelListView.selectedIndex;
            if (index < 0) return;

            var label = _labels[index];
            if (label == "All") return;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Delete Label"), false, () => DeleteLabel(label));
            menu.ShowAsContext();
        });


        _sidebarContainer.Add(_labelListView);
        main.Add(_sidebarContainer);

        _contentContainer = new VisualElement { style = { flexGrow = 1 } };
        main.Add(_contentContainer);

        _rootContainer.Add(main);

        SelectLabelInUI();
        BuildAssetListWide();
    }

    private void BuildAssetListWide()
    {
        _contentContainer.Clear();

        var assets = GetAssetsForLabel(_selectedLabel);

        _assetListView = CreateAssetListView(assets);
        _contentContainer.Add(_assetListView);
    }

    #endregion
    private void RegisterWindowContextMenu()
    {
        rootVisualElement.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            evt.menu.AppendAction("Refresh", _ =>
            {
                LoadAssets();
                UpdateLayout();
            });

            evt.menu.AppendSeparator();
        }));
    }

    
    #region Compact Mode (Foldouts)


    void BuildCompactLayout()
    {
        var scroll = new ScrollView { style = { flexGrow = 1 } };

        foreach (var label in _labels.Where(l => l != "All"))
        {
            bool savedState = EditorPrefs.GetBool(GetFoldoutPrefKey(label), false);
            _foldoutStates[label] = savedState;

            var foldout = new Foldout
            {
                text = label,
                value = savedState
            };

            foldout.RegisterValueChangedCallback(evt =>
            {
                _foldoutStates[label] = evt.newValue;
                EditorPrefs.SetBool(GetFoldoutPrefKey(label), evt.newValue);
            });

            foldout.RegisterCallback<ContextClickEvent>(evt =>
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Delete Label"), false, () => DeleteLabel(label));
                menu.ShowAsContext();
                evt.StopPropagation();
            });

            var assets = GetAssetsForLabel(label);
            var list = CreateAssetListView(assets);

            foldout.Add(list);
            scroll.Add(foldout);
        }

        _rootContainer.Add(scroll);
    }
    
    string GetFoldoutPrefKey(string label)
    {
        return $"LabelBrowser_Foldout_{label}";
    }

    #endregion

    #region Asset ListView

    private ListView CreateAssetListView(List<AssetInfo> assets)
    {
        var list = new ListView
        {
            itemsSource = assets,
            fixedItemHeight = 30,
            selectionType = SelectionType.Multiple
        };

        list.makeItem = () =>
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            var icon = new Image { style = { width = 25, height = 25, marginRight = 6 } };
            var label = new Label { style = { unityFontStyleAndWeight = FontStyle.Bold } };

            row.Add(icon);
            row.Add(label);

            return row;
        };

        list.bindItem = (e, i) =>
        {
            var info = assets[i];
            e.Q<Image>().image = AssetPreview.GetMiniThumbnail(info.asset);
            e.Q<Label>().text = info.asset.name;
        };

        list.selectionChanged += objs => { Selection.objects = objs.Cast<AssetInfo>().Select(a => a.asset).ToArray(); };

        RegisterDragHandlers(list);

        return list;
    }

    private List<AssetInfo> GetAssetsForLabel(string label)
    {
        if (!string.IsNullOrEmpty(_search))
        {
            var lower = _search.ToLowerInvariant();
            return _allAssets
                .Where(a => a.asset.name.ToLowerInvariant().Contains(lower))
                .ToList();
        }

        if (label == "All")
            return new List<AssetInfo>(_allAssets);

        return _allAssets
            .Where(a => a.labels.Contains(label))
            .ToList();
    }

    private void RegisterDragHandlers(ListView list)
    {
        list.RegisterCallback<MouseDownEvent>(evt =>
        {
            if (evt.button != 0) return;
            _mouseDown = true;
            _dragStarted = false;
            _mouseDownPos = evt.localMousePosition;
        });

        list.RegisterCallback<MouseMoveEvent>(evt =>
        {
            if (!_mouseDown || _dragStarted) return;

            if (Vector2.Distance(evt.localMousePosition, _mouseDownPos) < DRAG_THRESHOLD)
                return;

            var selected = list.selectedItems.Cast<AssetInfo>().ToArray();
            if (selected.Length == 0) return;

            _dragStarted = true;

            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = selected.Select(a => a.asset).ToArray();
            DragAndDrop.StartDrag($"{selected.Length} asset(s)");
        });

        list.RegisterCallback<MouseUpEvent>(_ =>
        {
            _mouseDown = false;
            _dragStarted = false;
        });
    }

    #endregion

    #region Prefs

    private void LoadPrefs()
    {
        _selectedLabel = EditorPrefs.GetString(PREF_SELECTED_LABEL, "All");
    }

    private void SavePrefs()
    {
        EditorPrefs.SetString(PREF_SELECTED_LABEL, _selectedLabel ?? "All");
    }

    private void SelectLabelInUI()
    {
        var idx = _labels.IndexOf(_selectedLabel);
        if (idx < 0) idx = 0;
        _labelListView?.SetSelection(idx);
    }

    #endregion
[Serializable]
private class AssetInfoCache
{
    public List<string> paths = new();
    public List<string> labelSets = new(); // semicolon-joined labels per asset
}}

