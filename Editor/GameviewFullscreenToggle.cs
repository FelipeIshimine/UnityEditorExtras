using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;

public static class GameViewFullscreenToggle
{
    private const string MENU_PATH = "Tools/Toggle Game View Fullscreen _F12";

    private static EditorWindow _fullscreenGameView;

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
        var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
        _fullscreenGameView = ScriptableObject.CreateInstance(gameViewType) as EditorWindow;

        var showWithMode = typeof(EditorWindow).GetMethod(
            "ShowWithMode",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        showWithMode.Invoke(_fullscreenGameView, new object[] { 1 });

        // Offset Y negative to push toolbar off the top of the screen
        int toolbarHeight = 40;
        var res = Screen.currentResolution;
        _fullscreenGameView.position = new Rect(0, -toolbarHeight, res.width, res.height + toolbarHeight);

        _fullscreenGameView.Focus();
    }

    private static void ExitFullscreen()
    {
        _fullscreenGameView.Close();
        _fullscreenGameView = null;
    }
}