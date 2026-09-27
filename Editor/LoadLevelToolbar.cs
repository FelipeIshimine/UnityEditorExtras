using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;

/// <summary>
/// Adds a scene-switcher dropdown to Unity 6.3's main toolbar.
/// Scans Assets/Scenes for all SceneAssets and lists them in an AdvancedDropdown.
/// The button label shows the currently active scene name.
/// </summary>
public static class LoadLevelToolbar
{
    const string DropdownID  = "Game/SceneSwitcher";
    const string ScenesRoot  = "Assets/Scenes";

    // ── Toolbar element ───────────────────────────────────────────────────────

    [MainToolbarElement(DropdownID, defaultDockPosition = MainToolbarDockPosition.Left)]
    public static MainToolbarElement CreateSceneDropdown()
    {
        string activeScene = EditorSceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(activeScene)) activeScene = "Untitled";

        var icon    = EditorGUIUtility.FindTexture("SceneAsset Icon");
        var content = new MainToolbarContent(activeScene, icon, "Switch Scene");

        return new MainToolbarDropdown(content, ShowMenu);
    }

    // ── Dropdown menu ─────────────────────────────────────────────────────────

    [MenuItem("Window/General/Switch Scene %#e")]
    static void ShowMenuShortcut()
    {
        if (!GetCursorPos(out var cursor))
            throw new System.ComponentModel.Win32Exception();

        var mouse = new Vector2(cursor.X, cursor.Y) / EditorGUIUtility.pixelsPerPoint;
        var window = EditorWindow.focusedWindow;
        if (window != null)
            mouse -= window.position.position;

        ShowMenu(new Rect(mouse.x, mouse.y, 260, 0));
    }

    [StructLayout(LayoutKind.Sequential)]
    struct CursorPoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool GetCursorPos(out CursorPoint point);

    static void ShowMenu(Rect dropdownRect)
    {
        var scenes = AssetDatabase
            .FindAssets("t:SceneAsset", new[] { ScenesRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => p)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogWarning($"[LoadLevelToolbar] No scenes found in {ScenesRoot}");
            return;
        }

        string activeScene = EditorSceneManager.GetActiveScene().name;

        var builder = new AdvancedDropdownBuilder()
            .WithTitle("Switch Scene")
            .SetCallback(index =>
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(scenes[index]);
                    MainToolbar.Refresh(DropdownID);
                }
            });

        foreach (string path in scenes)
        {
            // Build a submenu from folder structure: Scenes/World/Level1 → World/Level1
            string relative = path
                .Replace(ScenesRoot + "/", "")
                .Replace(".unity", "");

            builder.AddElement(relative, out _);
        }

        builder.Build().Show(dropdownRect);
    }

    // ── Keep label in sync when the active scene changes ─────────────────────

    [InitializeOnLoadMethod]
    static void Init()
    {
        EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
        EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
    }

    static void OnSceneChanged(UnityEngine.SceneManagement.Scene _, UnityEngine.SceneManagement.Scene __)
        => MainToolbar.Refresh(DropdownID);
}