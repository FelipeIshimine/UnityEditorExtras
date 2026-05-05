using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;

public static class FastPlayButton
{
    const string ButtonID = "Game/FastPlay";
    [MainToolbarElement(ButtonID, defaultDockPosition = MainToolbarDockPosition.Left)]
    public static MainToolbarElement CreateButton()
    {
        bool isPlaying = EditorApplication.isPlayingOrWillChangePlaymode;

        var content = new MainToolbarContent(
            isPlaying ? "■ STOP" : "▶ PLAY (F5)",
            isPlaying ? "Stop play mode" : "Load first build scene and enter play mode (F5)");

        return new MainToolbarButton(content, Execute);
    }

    [MenuItem("Tools/Fast Play _F5")]
    public static void Execute()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.ExitPlaymode();
        }
        else
        {
            if (EditorBuildSettings.scenes.Length == 0)
            {
                Debug.LogWarning("[FastPlayButton] No scenes in Build Settings.");
                return;
            }
            EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);
            EditorApplication.EnterPlaymode();
        }

        MainToolbar.Refresh(ButtonID);
    }

    [InitializeOnLoadMethod]
    static void Init()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange _) => MainToolbar.Refresh(ButtonID);
}