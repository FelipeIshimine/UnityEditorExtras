using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(CompactList<>))]
public class CompactListDrawer : PropertyDrawer
{
    // ── Colours ──────────────────────────────────────────────────────────────
    private static readonly Color BgHeader  = new(0.15f, 0.15f, 0.18f);
    private static readonly Color BgEven    = new(0.22f, 0.22f, 0.24f);
    private static readonly Color BgOdd     = new(0.19f, 0.19f, 0.21f);
    private static readonly Color BgAdd     = new(0.17f, 0.17f, 0.19f);
    private static readonly Color BgSearch  = new(0.14f, 0.14f, 0.16f);
    private static readonly Color BgCount   = new(0.13f, 0.13f, 0.16f);
    private static readonly Color ColAccent = new(0.35f, 0.70f, 1.00f);
    private static readonly Color ColDefault = new(0.9f, 0.9f, 0.9f);
    private static readonly Color ColRemove = new(0.85f, 0.30f, 0.30f);
    private static readonly Color ColMuted  = new(0.40f, 0.40f, 0.50f);

    // ── State shared across makeItem / bindItem ───────────────────────────────
    private struct Entry
    {
        public UnityEngine.Object Asset;
        public int Count;
        public int FirstIndex;
    }

    private SerializedObject _so;
    private string _arrayPath;
    private string _ownerPath;
    private Type _elementType;
    private List<Entry> _entries = new();

    // Header label ref so we can update it without full rebuild
    private Label _headerCountLabel;

    // ── Entry point ───────────────────────────────────────────────────────────

    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        _so          = property.serializedObject;
        _ownerPath   = property.propertyPath;
        _arrayPath   = FindArrayPath(_so, _ownerPath);
        _elementType = ResolveElementType(fieldInfo);

        var root = new VisualElement();
        root.style.marginBottom = 4;

        if (_arrayPath == null)
        {
            root.Add(new HelpBox($"Could not find array inside '{_ownerPath}'.", HelpBoxMessageType.Error));
            return root;
        }

        RefreshEntries();

        // ── Header ────────────────────────────────────────────────────────────
        root.Add(BuildHeader(property.displayName));

        // ── ListView ─────────────────────────────────────────────────────────
        var listView = new ListView
        {
            itemsSource            = _entries,
            fixedItemHeight        = 26,
            makeItem               = MakeRow,
            bindItem               = BindRow,
            selectionType          = SelectionType.None,
            reorderable            = false,
            showAddRemoveFooter    = false,
            showBoundCollectionSize = false,
            showBorder             = false,
        };
        listView.style.flexGrow    = 0;
        listView.style.flexShrink  = 0;
        // Height driven by content
        listView.style.height = _entries.Count * 26;
        root.Add(listView);

        // ── Add slot ──────────────────────────────────────────────────────────
        root.Add(BuildAddSlot(listView));

        return root;
    }

    // ── Header ────────────────────────────────────────────────────────────────

    private VisualElement BuildHeader(string title)
    {
        var row = Row(BgHeader);
        row.style.paddingLeft          = 8;
        row.style.paddingRight         = 8;
        row.style.paddingTop           = 3;
        row.style.paddingBottom        = 3;
        row.style.borderTopLeftRadius  = 3;
        row.style.borderTopRightRadius = 3;
        row.style.marginBottom         = 1;

        var lbl = new Label(title);
        lbl.style.flexGrow                = 1;
        lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
        lbl.style.color                   = Color.white;
        lbl.style.fontSize                = 11;

        _headerCountLabel             = new Label(CountText());
        _headerCountLabel.style.color    = ColMuted;
        _headerCountLabel.style.fontSize = 10;

        row.Add(lbl);
        row.Add(_headerCountLabel);
        return row;
    }

    // ── ListView item factory / binder ────────────────────────────────────────

    private VisualElement MakeRow()
    {
        var row = Row(Color.clear); // background set in BindRow
        row.style.paddingLeft   = 4;
        row.style.paddingRight  = 4;
        row.style.paddingTop    = 2;
        row.style.paddingBottom = 2;
        row.style.height        = 26;

        // Object field
        var objField = new ObjectField
        {
            objectType        = _elementType,
            allowSceneObjects = false,
            label             = ""
        };
        objField.name              = "obj-field";
        objField.style.flexGrow    = 1;
        objField.style.marginRight = 4;
        row.Add(objField);

        // − button
        row.Add(SmallBtn("−", Color.white));
        row.Q<Button>("−").name = "btn-minus";

        // Count field
        var countField = new IntegerField { label = "" };
        countField.name                          = "count-field";
        countField.style.width                   = 34;
        countField.style.marginLeft              = 2;
        countField.style.marginRight             = 2;
        countField.style.backgroundColor         = BgCount;
        countField.style.color                   = ColAccent;
        countField.style.unityFontStyleAndWeight = FontStyle.Bold;
        countField.style.unityTextAlign          = TextAnchor.MiddleCenter;
        row.Add(countField);

        // + button
        row.Add(SmallBtn("+", Color.white));
        row.Q<Button>("+").name = "btn-plus";

        // ✕ button
        var rm = SmallBtn("✕", Color.gray1);
        rm.name                = "btn-remove";
        rm.style.marginLeft    = 4;
        row.Add(rm);

        return row;
    }

    private void BindRow(VisualElement row, int index)
    {
        if (index >= _entries.Count) return;
        var entry = _entries[index];

        row.style.backgroundColor = index % 2 == 0 ? BgEven : BgOdd;

        // Object field
        var objField = row.Q<ObjectField>("obj-field");
        objField.SetValueWithoutNotify(entry.Asset);
        // Re-register callback cleanly
        objField.UnregisterValueChangedCallback(OnObjChanged);
        objField.userData = index;
        objField.RegisterValueChangedCallback(OnObjChanged);

        // Count
        var countField = row.Q<IntegerField>("count-field");
        countField.SetValueWithoutNotify(entry.Count);
        countField.UnregisterValueChangedCallback(OnCountChanged);
        countField.userData = index;
        countField.RegisterValueChangedCallback(OnCountChanged);

        // − button
        var minus = row.Q<Button>("btn-minus");
        minus.userData  = index;
        minus.clicked  -= OnMinusClicked; // clear previous
        minus.clicked  += OnMinusClicked;

        // + button
        var plus = row.Q<Button>("btn-plus");
        plus.userData  = index;
        plus.clicked  -= OnPlusClicked;
        plus.clicked  += OnPlusClicked;

        // ✕ button
        var remove = row.Q<Button>("btn-remove");
        remove.userData  = index;
        remove.clicked  -= OnRemoveAllClicked;
        remove.clicked  += OnRemoveAllClicked;

        // Closure helpers that read index from userData
        void OnObjChanged(ChangeEvent<UnityEngine.Object> e)
        {
            var i = (int)((VisualElement)e.target).userData;
            if (i >= _entries.Count) return;
            var en = _entries[i];
            if (e.newValue == en.Asset) return;
            ReplaceAsset(en.Asset, e.newValue, en.Count);
            Refresh(row.parent.parent.Q<ListView>());
        }

        void OnCountChanged(ChangeEvent<int> e)
        {
            var i = (int)((VisualElement)e.target).userData;
            if (i >= _entries.Count) return;
            var t = Mathf.Max(0, e.newValue);
            if (t == _entries[i].Count) return;
            SetCount(_entries[i].Asset, t);
            Refresh(row.parent.parent.Q<ListView>());
        }

        void OnMinusClicked()
        {
            var i = (int)minus.userData;
            if (i >= _entries.Count) return;
            RemoveOne(_entries[i].Asset);
            Refresh(row.parent.parent.Q<ListView>());
        }

        void OnPlusClicked()
        {
            var i = (int)plus.userData;
            if (i >= _entries.Count) return;
            AddOne(_entries[i].Asset);
            Refresh(row.parent.parent.Q<ListView>());
        }

        void OnRemoveAllClicked()
        {
            var i = (int)remove.userData;
            if (i >= _entries.Count) return;
            RemoveAll(_entries[i].Asset);
            Refresh(row.parent.parent.Q<ListView>());
        }
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    private void RefreshEntries()
    {
        _entries.Clear();

        var prop = _so.FindProperty(_arrayPath);
        if (prop == null || !prop.isArray) return;

        var counts = new Dictionary<UnityEngine.Object, int>();
        var firsts = new Dictionary<UnityEngine.Object, int>();
        var order  = new List<UnityEngine.Object>();

        for (int i = 0; i < prop.arraySize; i++)
        {
            var obj = prop.GetArrayElementAtIndex(i).objectReferenceValue;
            if (obj == null) continue;
            if (!counts.ContainsKey(obj)) { counts[obj] = 0; firsts[obj] = i; order.Add(obj); }
            counts[obj]++;
        }

        _entries = order.Select(o => new Entry
        {
            Asset      = o,
            Count      = counts[o],
            FirstIndex = firsts[o]
        }).ToList();
    }

    private void Refresh(ListView listView)
    {
        RefreshEntries();

        if (listView == null) return;
        listView.itemsSource = _entries;
        listView.style.height = _entries.Count * 26;
        listView.Rebuild();

        if (_headerCountLabel != null)
            _headerCountLabel.text = CountText();
    }

    private string CountText()
    {
        var prop = _so.FindProperty(_arrayPath);
        return $"{(prop?.arraySize ?? 0)} total";
    }

    // ── Add slot ──────────────────────────────────────────────────────────────

    private VisualElement BuildAddSlot(ListView listView)
    {
        var container = new VisualElement();
        container.style.borderBottomLeftRadius  = 3;
        container.style.borderBottomRightRadius = 3;

        // ── Row 1: ObjectField + ▾ ────────────────────────────────────────────
        var fieldRow = Row(BgAdd);
        fieldRow.style.paddingLeft   = 4;
        fieldRow.style.paddingRight  = 4;
        fieldRow.style.paddingTop    = 2;
        fieldRow.style.paddingBottom = 2;
        fieldRow.style.height        = 26;

        var addField = new ObjectField
        {
            objectType        = _elementType,
            value             = null,
            allowSceneObjects = false,
            label             = ""
        };
        addField.style.flexGrow    = 1;
        addField.style.marginRight = 2;
        addField.RegisterValueChangedCallback(e =>
        {
            if (e.newValue == null) return;
            AddOne(e.newValue);
            addField.SetValueWithoutNotify(null);
            Refresh(listView);
        });
        fieldRow.Add(addField);

        var dropBtn = new Button { text = "▾", tooltip = "Select asset…" };
        dropBtn.style.width       = 24;
        dropBtn.style.height      = 18;
        dropBtn.style.marginLeft  = 2;
        dropBtn.style.paddingLeft = dropBtn.style.paddingRight = 0;
        var elType = _elementType; // capture for closure
        dropBtn.clicked += () =>
        {
            var menu   = new GenericDropdownMenu();
            var assets = AssetDatabase.FindAssets($"t:{elType.Name}")
                .Select(g => AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(g), elType))
                .Where(a => a != null).OrderBy(a => a.name).ToList();

            if (assets.Count == 0)
                menu.AddDisabledItem("No assets found", false);
            else
                foreach (var asset in assets)
                {
                    var captured = asset;
                    menu.AddItem(asset.name, false, () => { AddOne(captured); Refresh(listView); });
                }

            var r = new Rect(fieldRow.worldBound.x, fieldRow.worldBound.yMax,
                             fieldRow.worldBound.width, 0);
            menu.DropDown(r, dropBtn, DropdownMenuSizeMode.Fixed);
        };
        fieldRow.Add(dropBtn);

        fieldRow.RegisterCallback<DragUpdatedEvent>(_ =>
            DragAndDrop.visualMode = DragAndDrop.objectReferences
                .Any(o => o != null && _elementType.IsInstanceOfType(o))
                ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected);

        fieldRow.RegisterCallback<DragPerformEvent>(_ =>
        {
            DragAndDrop.AcceptDrag();
            foreach (var obj in DragAndDrop.objectReferences)
                if (obj != null && _elementType.IsInstanceOfType(obj)) AddOne(obj);
            Refresh(listView);
        });

        container.Add(fieldRow);

        // ── Row 2: Search ─────────────────────────────────────────────────────
        var searchRow = Row(BgSearch);
        searchRow.style.paddingLeft    = 8;
        searchRow.style.paddingRight   = 8;
        searchRow.style.paddingTop     = 3;
        searchRow.style.paddingBottom  = 3;
        searchRow.style.height         = 24;
        searchRow.style.borderTopWidth = 1;
        searchRow.style.borderTopColor = new Color(0.10f, 0.10f, 0.12f);

        var icon = new Label("⌕");
        icon.style.color       = ColMuted;
        icon.style.fontSize    = 13;
        icon.style.marginRight = 4;
        searchRow.Add(icon);

        var searchField = new TextField { value = "" };
        searchField.style.flexGrow = 1;
        searchField.style.color    = Color.white;
        searchField.style.fontSize = 11;
        var lbl = searchField.Q<Label>();
        if (lbl != null) lbl.style.display = DisplayStyle.None;
        searchRow.Add(searchField);
        container.Add(searchRow);

        // ── Dropdown ─────────────────────────────────────────────────────────
        var dropdown = new ScrollView(ScrollViewMode.Vertical);
        dropdown.style.display         = DisplayStyle.None;
        dropdown.style.maxHeight       = 160;
        dropdown.style.backgroundColor = new Color(0.13f, 0.13f, 0.15f);
        dropdown.style.borderTopWidth  = 1;
        dropdown.style.borderTopColor  = new Color(0.08f, 0.08f, 0.10f);
        dropdown.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        container.Add(dropdown);

        var allAssets = AssetDatabase.FindAssets($"t:{_elementType.Name}")
            .Select(g => AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(g), _elementType))
            .Where(a => a != null).OrderBy(a => a.name).ToList();

        // ── Search state ─────────────────────────────────────────────────────
        var currentMatches   = new List<UnityEngine.Object>();
        var selectedIndex    = -1;
        var pendingCount     = 1;
        var suppressFocusOut = false;

        void SetHighlight(int newIndex)
        {
            // Clear old
            if (selectedIndex >= 0 && selectedIndex < dropdown.childCount)
                dropdown[selectedIndex].style.backgroundColor = new Color(0, 0, 0, 0);

            selectedIndex = Mathf.Clamp(newIndex, 0, currentMatches.Count - 1);

            // Highlight new
            if (selectedIndex >= 0 && selectedIndex < dropdown.childCount)
            {
                dropdown[selectedIndex].style.backgroundColor = new Color(0.25f, 0.35f, 0.50f, 0.5f);
                (dropdown[selectedIndex] as VisualElement)?.MarkDirtyRepaint();
            }
        }

        void UpdateCountLabel()
        {
            for (int i = 0; i < dropdown.childCount; i++)
            {
                var countLbl = dropdown[i].Q<Label>("count-badge");
                if (countLbl == null) continue;
                if (i != selectedIndex)
                {
                    // restore original existing-count label
                    var asset    = i < currentMatches.Count ? currentMatches[i] : null;
                    var existing = asset != null ? _entries.FirstOrDefault(en => en.Asset == asset) : default;
                    countLbl.text  = existing.Asset != null ? $"{existing.Count}" : "+";
                    countLbl.style.color = existing.Asset != null ? ColDefault : ColMuted;
                }
                else
                {
                    var asset    = i < currentMatches.Count ? currentMatches[i] : null;
                    var existing = asset != null ? _entries.FirstOrDefault(en => en.Asset == asset) : default;
                    int current  = existing.Asset != null ? existing.Count : 0;
              
                    if (current + pendingCount > 0)
                    {
                        countLbl.text = $"{current + pendingCount}";
                    }
                    else
                    {
                        if (pendingCount > 0)
                        {
                            countLbl.text = $"+{pendingCount}";
                        }
                        else
                        {
                            countLbl.text = $"0";
                        }
                    }

                    if (pendingCount > 0)
                    {
                        countLbl.style.color = ColAccent;
                    }
                    else if (pendingCount < 0)
                    {
                        countLbl.style.color = ColRemove;
                    }
                    else
                    {
                        countLbl.style.color = ColDefault;
                    }
                }
            }
        }

        void CommitSelected()
        {
            if (selectedIndex < 0 || selectedIndex >= currentMatches.Count) return;

            var asset = currentMatches[selectedIndex];

            if (pendingCount > 0)
            {
                for (int i = 0; i < pendingCount; i++)
                    AddOne(asset);
            }
            else if (pendingCount < 0)
            {
                for (int i = 0; i < -pendingCount; i++)
                    RemoveOne(asset);
            }

            pendingCount = 0;
            suppressFocusOut = true;

            Refresh(listView);

            // keep the search value so results stay visible
            searchField.schedule.Execute(() =>
            {
                searchField.Focus();

                var q = searchField.value.Trim();
                var matches = q.Length == 0
                    ? allAssets.Take(12).ToList()
                    : allAssets.Where(a => a.name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                        .Take(12).ToList();

                RebuildDropdown(matches);

                suppressFocusOut = false;
            }).StartingIn(75);
        }

        void RebuildDropdown(List<UnityEngine.Object> matches)
        {
            dropdown.Clear();
            currentMatches = matches;
            selectedIndex  = matches.Count > 0 ? 0 : -1;
            pendingCount   = 0;

            if (matches.Count == 0)
            {
                var none = new Label("  No results");
                none.style.color     = ColMuted;
                none.style.fontSize  = 10;
                none.style.paddingTop = none.style.paddingBottom = 4;
                dropdown.Add(none);
                dropdown.style.display = DisplayStyle.Flex;
                return;
            }

            for (int i = 0; i < matches.Count; i++)
            {
                var asset = matches[i];
                var item  = new VisualElement();
                item.style.flexDirection     = FlexDirection.Row;
                item.style.alignItems        = Align.Center;
                item.style.paddingLeft       = 10;
                item.style.paddingRight      = 8;
                item.style.paddingTop        = 5;
                item.style.paddingBottom     = 5;
                item.style.borderBottomWidth = 1;
                item.style.borderBottomColor = new Color(0.10f, 0.10f, 0.12f);
                if (i == 0)
                    item.style.backgroundColor = new Color(0.25f, 0.35f, 0.50f, 0.5f);

                var nameLbl = new Label(asset.name);
                nameLbl.style.flexGrow = 1;
                nameLbl.style.color    = new Color(0.85f, 0.85f, 0.90f);
                nameLbl.style.fontSize = 11;

                var existing    = _entries.FirstOrDefault(en => en.Asset == asset);
                var existingText = existing.Asset != null ? $"{existing.Count}" : "+";
                var countLbl     = new Label(existingText);
                countLbl.name                          = "count-badge";
                countLbl.style.color                   = existing.Asset != null ? ColDefault : ColMuted;
                countLbl.style.fontSize                = 12;
                countLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                countLbl.style.minWidth                = 22;
                countLbl.style.unityTextAlign          = TextAnchor.MiddleRight;

                item.Add(nameLbl);
                item.Add(countLbl);

                item.RegisterCallback<MouseEnterEvent>(_ => SetHighlight(dropdown.IndexOf(item)));
                item.RegisterCallback<PointerDownEvent>(__ => CommitSelected(), TrickleDown.TrickleDown);
                dropdown.Add(item);
            }

            dropdown.style.display = DisplayStyle.Flex;
        }

        // ── Value changed ─────────────────────────────────────────────────────
        searchField.RegisterValueChangedCallback(e =>
        {
            var q = e.newValue.Trim();
            if (q.Length == 0)
            {
                dropdown.Clear();
                dropdown.style.display = DisplayStyle.None;
                currentMatches.Clear();
                selectedIndex = -1;
                pendingCount  = 1;
                return;
            }

            var matches = allAssets
                .Where(a => a.name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(12).ToList();

            RebuildDropdown(matches);
        });

        // ── Keyboard navigation ───────────────────────────────────────────────
        searchField.RegisterCallback<KeyDownEvent>(e =>
        {
            if (currentMatches.Count == 0) return;

            switch (e.keyCode)
            {
                case KeyCode.DownArrow:
                    pendingCount = 0;
                    SetHighlight(selectedIndex + 1);
                    UpdateCountLabel();
                    e.StopPropagation();
                    break;

                case KeyCode.UpArrow:
                    pendingCount = 0;
                    SetHighlight(selectedIndex - 1);
                    UpdateCountLabel();
                    e.StopPropagation();
                    break;

                case KeyCode.RightArrow:
                    pendingCount++;
                    UpdateCountLabel();
                    e.StopPropagation();
                    break;

                case KeyCode.LeftArrow:
                    
                    var asset = currentMatches[selectedIndex];
                    int existing = 0;
                    if (asset != null)
                    {
                        var en = _entries.FirstOrDefault(x => x.Asset == asset);
                        if (en.Asset != null) existing = en.Count;
                    }

                    // clamp to -existing so we never propose removing more than exist
                    pendingCount = Mathf.Max(-existing, pendingCount - 1);
                    UpdateCountLabel();
                    e.StopPropagation();
                    break;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    CommitSelected();
                    e.StopPropagation();
                    break;

                case KeyCode.Escape:
                    searchField.SetValueWithoutNotify("");
                    dropdown.Clear();
                    dropdown.style.display = DisplayStyle.None;
                    currentMatches.Clear();
                    selectedIndex = -1;
                    pendingCount  = 1;
                    e.StopPropagation();
                    break;
            }
        }, TrickleDown.TrickleDown);

        searchField.RegisterCallback<FocusInEvent>(_ =>
        {
            var q = searchField.value.Trim();
            var matches = q.Length == 0
                ? allAssets.Take(12).ToList()
                : allAssets.Where(a => a.name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                           .Take(12).ToList();
            RebuildDropdown(matches);
        });

        searchField.RegisterCallback<FocusOutEvent>(_ =>
            container.schedule.Execute(() =>
            {
                if (suppressFocusOut) return;
                searchField.SetValueWithoutNotify("");
                dropdown.Clear();
                dropdown.style.display = DisplayStyle.None;
                currentMatches.Clear();
                selectedIndex = -1;
                pendingCount  = 1;
            }).StartingIn(300));

        return container;
    }


    // ── Mutations ─────────────────────────────────────────────────────────────

    private SerializedProperty FreshArray() => _so.FindProperty(_arrayPath);

    private void AddOne(UnityEngine.Object asset)
    {
        var p = FreshArray();
        p.arraySize++;
        p.GetArrayElementAtIndex(p.arraySize - 1).objectReferenceValue = asset;
        _so.ApplyModifiedProperties();
    }

    private void RemoveOne(UnityEngine.Object asset)
    {
        var p = FreshArray();
        for (int i = p.arraySize - 1; i >= 0; i--)
        {
            if (p.GetArrayElementAtIndex(i).objectReferenceValue != asset) continue;
            p.GetArrayElementAtIndex(i).objectReferenceValue = null;
            p.DeleteArrayElementAtIndex(i);
            _so.ApplyModifiedProperties();
            return;
        }
    }

    private void RemoveAll(UnityEngine.Object asset)
    {
        var p = FreshArray();
        for (int i = p.arraySize - 1; i >= 0; i--)
        {
            if (p.GetArrayElementAtIndex(i).objectReferenceValue != asset) continue;
            p.GetArrayElementAtIndex(i).objectReferenceValue = null;
            p.DeleteArrayElementAtIndex(i);
        }
        _so.ApplyModifiedProperties();
    }

    private void SetCount(UnityEngine.Object asset, int target)
    {
        var p = FreshArray();
        int current = 0;
        for (int i = 0; i < p.arraySize; i++)
            if (p.GetArrayElementAtIndex(i).objectReferenceValue == asset) current++;
        int delta = target - current;
        if (delta > 0) for (int i = 0; i < delta; i++) AddOne(asset);
        else           for (int i = 0; i < -delta; i++) RemoveOne(asset);
    }

    private void ReplaceAsset(UnityEngine.Object old, UnityEngine.Object next, int count)
    {
        RemoveAll(old);
        if (next != null) for (int i = 0; i < count; i++) AddOne(next);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FindArrayPath(SerializedObject so, string ownerPath)
    {
        var p = so.FindProperty($"{ownerPath}.items");
        return p != null && p.isArray ? p.propertyPath : null;
    }

    private static string FindOwnerPath(string arrayPath)
    {
        var dot = arrayPath.LastIndexOf('.');
        return dot >= 0 ? arrayPath[..dot] : arrayPath;
    }

    private static Type ResolveElementType(FieldInfo fi)
    {
        if (fi == null) return typeof(UnityEngine.Object);
        var t = fi.FieldType;
        while (t != null)
        {
            if (t.IsGenericType) return t.GetGenericArguments()[0];
            t = t.BaseType;
        }
        return typeof(UnityEngine.Object);
    }

    private static VisualElement Row(Color bg)
    {
        var e = new VisualElement();
        e.style.flexDirection   = FlexDirection.Row;
        e.style.alignItems      = Align.Center;
        e.style.backgroundColor = bg;
        return e;
    }

    private static Button SmallBtn(string text, Color color)
    {
        var btn = new Button { text = text, name = text };
        btn.style.width                   = 18;
        btn.style.height                  = 18;
        btn.style.paddingLeft             = btn.style.paddingRight  = 0;
        btn.style.paddingTop              = btn.style.paddingBottom = 0;
        btn.style.marginLeft              = btn.style.marginRight   = 1;
        btn.style.color                   = color;
        btn.style.unityFontStyleAndWeight = FontStyle.Bold;
        btn.style.fontSize                = 12;
        btn.style.unityTextAlign          = TextAnchor.MiddleCenter;
        btn.style.borderTopLeftRadius     = btn.style.borderTopRightRadius    = 3;
        btn.style.borderBottomLeftRadius  = btn.style.borderBottomRightRadius = 3;
        return btn;
    }
}