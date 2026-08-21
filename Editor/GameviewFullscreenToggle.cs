using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;

public static class GameViewFullscreenToggle
{
    private const string MENU_PATH = "Tools/Toggle Game View Fullscreen _F12";
    private const string LAST_SIZE_INDEX_PREF = "GameViewFullscreenToggle.LastSizeIndex";

    private static EditorWindow _fullscreenGameView;
    private static readonly Type GameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
    private static readonly PropertyInfo SelectedSizeIndexProperty = GameViewType.GetProperty(
        "selectedSizeIndex",
        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance
    );

    static GameViewFullscreenToggle()
    {
        if (SelectedSizeIndexProperty == null)
        {
            throw new InvalidOperationException(
                "GameView.selectedSizeIndex property not found via reflection - Unity API may have changed."
            );
        }
    }

    [MenuItem(MENU_PATH)]
    private static void Toggle()
    {
        if (_fullscreenGameView != null)
        {
            ExitFullscreen();
            return;
        }

        EnterFullscreen();
    }

    private static void EnterFullscreen()
    {
        int sizeIndex = GetExistingGameViewSizeIndex()
            ?? EditorPrefs.GetInt(LAST_SIZE_INDEX_PREF, 0);

        _fullscreenGameView = ScriptableObject.CreateInstance(GameViewType) as EditorWindow;

        var showWithMode = typeof(EditorWindow).GetMethod(
            "ShowWithMode",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        showWithMode.Invoke(_fullscreenGameView, new object[] { 1 });

        SelectedSizeIndexProperty.SetValue(_fullscreenGameView, sizeIndex);

        // Offset Y negative to push toolbar off the top of the screen
        int toolbarHeight = 40;
        var res = Screen.currentResolution;
        _fullscreenGameView.position = new Rect(0, -toolbarHeight, res.width, res.height + toolbarHeight);

        _fullscreenGameView.Focus();
    }

    private static void ExitFullscreen()
    {
        int sizeIndex = (int)SelectedSizeIndexProperty.GetValue(_fullscreenGameView);
        EditorPrefs.SetInt(LAST_SIZE_INDEX_PREF, sizeIndex);

        _fullscreenGameView.Close();
        _fullscreenGameView = null;
    }

    private static int? GetExistingGameViewSizeIndex()
    {
        var openGameViews = Resources.FindObjectsOfTypeAll(GameViewType);
        foreach (var window in openGameViews)
        {
            if (window is EditorWindow editorWindow && editorWindow != _fullscreenGameView)
            {
                return (int)SelectedSizeIndexProperty.GetValue(editorWindow);
            }
        }

        return null;
    }
}