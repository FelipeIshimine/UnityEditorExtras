// File: Editor/LabelBrowserWindow.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class LabelBrowserWindow : EditorWindow
{
    const string PREF_SELECTED_LABEL = "LabelBrowser_SelectedLabel";
    const float COMPACT_WIDTH = 250f;

    class AssetInfo
    {
        public UnityEngine.Object asset;
        public string path;
        public string folder;
        public string[] labels;
    }

    List<AssetInfo> _allAssets = new();
    List<string> _labels = new();

    string _selectedLabel = "All";
    string _search = "";

    VisualElement _rootContainer;
    VisualElement _sidebarContainer;
    VisualElement _contentContainer;

    ListView _labelListView;
    ListView _assetListView;
    ToolbarSearchField _searchField;

    bool _isCompact;

    // Drag state
    bool _mouseDown;
    bool _dragStarted;
    Vector2 _mouseDownPos;
    const float DRAG_THRESHOLD = 6f;

    [MenuItem("Extras/Label Browser")]
    public static void Open()
    {
        var wnd = GetWindow<LabelBrowserWindow>();
        wnd.titleContent = new GUIContent("Labels");
        wnd.minSize = new Vector2(400, 300);
    }

    void OnEnable()
    {
        LoadAssets();
        LoadPrefs();
        BuildUI();
        UpdateLayout();
    }

    void OnDisable()
    {
        SavePrefs();
    }

    void OnGUI()
    {
        // Detect width change for responsive layout
        bool shouldCompact = position.width < COMPACT_WIDTH;
        if (shouldCompact != _isCompact)
        {
            _isCompact = shouldCompact;
            UpdateLayout();
        }
    }

    #region Data

    void LoadAssets()
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
                folder = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/"),
                labels = labels
            });
        }

        _allAssets = _allAssets
            .OrderBy(a => a.asset.name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        RebuildLabels();
    }

    void RebuildLabels()
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

    void BuildUI()
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

    void UpdateLayout()
    {
        _rootContainer.Clear();

        if (_isCompact)
            BuildCompactLayout();
        else
            BuildWideLayout();
    }

    #endregion

    #region Wide Mode (Split View)

    void BuildWideLayout()
    {
        var main = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };

        _sidebarContainer = new VisualElement
        {
            style =
            {
                width = 120,
                borderRightWidth = 1,
                borderRightColor = new Color(0,0,0,0.1f),
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

        _sidebarContainer.Add(_labelListView);
        main.Add(_sidebarContainer);

        _contentContainer = new VisualElement { style = { flexGrow = 1 } };
        main.Add(_contentContainer);

        _rootContainer.Add(main);

        SelectLabelInUI();
        BuildAssetListWide();
    }

    void BuildAssetListWide()
    {
        _contentContainer.Clear();

        var assets = GetAssetsForLabel(_selectedLabel);

        _assetListView = CreateAssetListView(assets);
        _contentContainer.Add(_assetListView);
    }

    #endregion

    #region Compact Mode (Foldouts)

    void BuildCompactLayout()
    {
        var scroll = new ScrollView { style = { flexGrow = 1 } };

        foreach (var label in _labels.Where(l => l != "All"))
        {
            var foldout = new Foldout { text = label };

            var assets = GetAssetsForLabel(label);
            var list = CreateAssetListView(assets);

            foldout.Add(list);
            scroll.Add(foldout);
        }

        _rootContainer.Add(scroll);
    }

    #endregion

    #region Asset ListView

    ListView CreateAssetListView(List<AssetInfo> assets)
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
            e.Q<Image>().image = AssetPreview.GetMiniThumbnail(info.asset) as Texture2D;
            e.Q<Label>().text = info.asset.name;
        };

        list.selectionChanged += objs =>
        {
            Selection.objects = objs.Cast<AssetInfo>().Select(a => a.asset).ToArray();
        };

        RegisterDragHandlers(list);

        return list;
    }

    List<AssetInfo> GetAssetsForLabel(string label)
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

    void RegisterDragHandlers(ListView list)
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

    void LoadPrefs()
    {
        _selectedLabel = EditorPrefs.GetString(PREF_SELECTED_LABEL, "All");
    }

    void SavePrefs()
    {
        EditorPrefs.SetString(PREF_SELECTED_LABEL, _selectedLabel ?? "All");
    }

    void SelectLabelInUI()
    {
        var idx = _labels.IndexOf(_selectedLabel);
        if (idx < 0) idx = 0;
        _labelListView?.SetSelection(idx);
    }

    #endregion
}