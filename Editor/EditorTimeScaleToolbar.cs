using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

/// <summary>
/// Adds a Time Scale slider + reset button to Unity 6.3's customisable main toolbar.
///
/// Behaviour:
///   • Slider 0 – 4× sits in the Right zone of the toolbar.
///   • A "1×" reset button sits immediately after it.
///   • When Play mode starts  → applies the slider value as Time.timeScale.
///   • When Play mode ends    → restores Time.timeScale to 1× and refreshes the slider.
///
/// The slider value is stored in EditorPrefs so it survives domain reloads
/// and code recompilation.
/// </summary>
public static class TimeScaleToolbar
{
    const string SliderID = "Game/TimeScale/Slider";
    const string ResetID  = "Game/TimeScale/Reset";
    const string PrefKey  = "TimeScaleToolbar.scale";
    const float  MinScale = 0f;
    const float  MaxScale = 4f;
    const float  Default  = 1f;

    // ── Persisted pending scale ───────────────────────────────────────────────

    static float PendingScale
    {
        get => EditorPrefs.GetFloat(PrefKey, Default);
        set => EditorPrefs.SetFloat(PrefKey, Mathf.Clamp(value, MinScale, MaxScale));
    }

    // ── Play-mode lifecycle ───────────────────────────────────────────────────

    [InitializeOnLoadMethod]
    static void Init()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            // Just entered play mode — apply whatever scale the user queued.
            case PlayModeStateChange.EnteredPlayMode:
                Time.timeScale = PendingScale;
                break;

            // Play ended — snap back to 1× and refresh the toolbar display.
            case PlayModeStateChange.EnteredEditMode:
                Time.timeScale = Default;
                MainToolbar.Refresh(SliderID);
                MainToolbar.Refresh(ResetID);
                break;
        }
    }

    // ── Toolbar elements ──────────────────────────────────────────────────────

    [MainToolbarElement(SliderID, defaultDockPosition = MainToolbarDockPosition.Right)]
    public static MainToolbarElement TimeScaleSlider()
    {
        float current = EditorApplication.isPlaying ? Time.timeScale : PendingScale;

        var content = new MainToolbarContent(
            $"⏱ {current:0.##}×",
            "Time Scale — drag to change playback speed.\n" +
            "Value is applied when you enter Play mode, and restored on exit.");

        return new MainToolbarSlider(content, current, MinScale, MaxScale,
            newValue =>
            {
                PendingScale = newValue;
                if (EditorApplication.isPlaying)
                    Time.timeScale = newValue;
                // Refresh so the ×N label stays in sync.
                MainToolbar.Refresh(SliderID);
            });
    }

    [MainToolbarElement(ResetID, defaultDockPosition = MainToolbarDockPosition.Right)]
    public static MainToolbarElement ResetButton()
    {
        var content = new MainToolbarContent("1×", "Reset time scale to 1×");

        return new MainToolbarButton(content, () =>
        {
            PendingScale = Default;
            if (EditorApplication.isPlaying)
                Time.timeScale = Default;
            MainToolbar.Refresh(SliderID);
        });
    }
}