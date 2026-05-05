using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Toolbars;
using UnityEngine;

/// <summary>
/// Shows the current game version (PlayerSettings.bundleVersion) in the main toolbar.
/// Clicking it opens a menu with options to:
///   • Toggle auto-increment of the patch number on each build
///   • Manually increment major / minor / patch
///   • Copy the version string to the clipboard
///
/// Auto-increment is implemented via IPreprocessBuildWithReport, which fires
/// before every player build.  The setting is persisted in EditorPrefs.
///
/// Expected version format: MAJOR.MINOR.PATCH  (e.g. "1.4.7")
/// If the format doesn't match, manual and auto increment are skipped with a warning.
/// </summary>
public static class VersionToolbar
{
    public const string DropdownID     = "Game/Version";
    public const string BuildButtonID  = "Game/Build";
    const string AutoIncrPref   = "VersionToolbar.autoIncrement";

    static bool AutoIncrement
    {
        get => EditorPrefs.GetBool(AutoIncrPref, false);
        set => EditorPrefs.SetBool(AutoIncrPref, value);
    }

    // ── Toolbar element ───────────────────────────────────────────────────────

    [MainToolbarElement(DropdownID, defaultDockPosition = MainToolbarDockPosition.Right)]
    public static MainToolbarElement CreateVersionDropdown()
    {
        string version = PlayerSettings.bundleVersion;
        string label   = $"v{version}{(AutoIncrement ? " ⬆" : "")}";

        var content = new MainToolbarContent(
            label,
            $"Game version: {version}\n" +
            $"Auto-increment patch on build: {(AutoIncrement ? "ON" : "OFF")}");

        return new MainToolbarDropdown(content, ShowMenu);
    }

    [MainToolbarElement(BuildButtonID, defaultDockPosition = MainToolbarDockPosition.Right)]
    public static MainToolbarElement CreateBuildButton()
    {
        var content = new MainToolbarContent(
            "🔨 BUILD",
            $"Build the project into 'Builds/{PlayerSettings.bundleVersion}' folder.");

        return new MainToolbarButton(content, BuildProject);
    }

    static void BuildProject()
    {
        string version = PlayerSettings.bundleVersion;
        string buildPath = System.IO.Path.Combine("Builds", version);

        if (!System.IO.Directory.Exists(buildPath))
        {
            System.IO.Directory.CreateDirectory(buildPath);
        }

        // Determine the executable name based on the target platform
        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        string extension = "";
        switch (target)
        {
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                extension = ".exe";
                break;
            case BuildTarget.StandaloneOSX:
                extension = ".app";
                break;
            case BuildTarget.Android:
                extension = ".apk";
                break;
            // Add more as needed, or use a generic name
        }

        string productName = PlayerSettings.productName;
        string fullPath = System.IO.Path.Combine(buildPath, productName + extension);

        Debug.Log($"[VersionToolbar] Starting build for {target} to: {fullPath}");

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = GetEnabledScenes(),
            locationPathName = fullPath,
            target = target,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[VersionToolbar] Build Succeeded: {summary.totalSize} bytes");
            // Note: AutoIncrement is handled by VersionAutoIncrementPreprocessor (IPostprocessBuildWithReport)
        }
        else if (summary.result == BuildResult.Failed)
        {
            Debug.LogError($"[VersionToolbar] Build Failed");
        }
    }

    static string[] GetEnabledScenes()
    {
        var scenes = EditorBuildSettings.scenes;
        var enabledScenes = new System.Collections.Generic.List<string>();
        foreach (var scene in scenes)
        {
            if (scene.enabled)
                enabledScenes.Add(scene.path);
        }
        return enabledScenes.ToArray();
    }

    // ── Dropdown menu ─────────────────────────────────────────────────────────

    static void ShowMenu(Rect dropdownRect)
    {
        var menu = new GenericMenu();

        // Auto-increment toggle
        menu.AddItem(
            new GUIContent("Auto-increment patch on build"),
            AutoIncrement,
            () =>
            {
                AutoIncrement = !AutoIncrement;
                MainToolbar.Refresh(DropdownID);
            });

        menu.AddSeparator("");

        // Manual increments
        menu.AddItem(new GUIContent("Increment Patch (x.x.+1)"), false, () => Increment(2));
        menu.AddItem(new GUIContent("Increment Minor (x.+1.0)"), false, () => Increment(1));
        menu.AddItem(new GUIContent("Increment Major (+1.0.0)"), false, () => Increment(0));

        menu.AddSeparator("");

        // Clipboard
        menu.AddItem(new GUIContent("Copy version to clipboard"), false, () =>
            GUIUtility.systemCopyBuffer = PlayerSettings.bundleVersion);

        // Open Player Settings
        menu.AddItem(new GUIContent("Open Player Settings…"), false, () =>
            SettingsService.OpenProjectSettings("Project/Player"));

        menu.DropDown(dropdownRect);
    }

    // ── Version manipulation ──────────────────────────────────────────────────

    /// <param name="part">0=major, 1=minor, 2=patch</param>
    static void Increment(int part)
    {
        if (!TryParse(PlayerSettings.bundleVersion, out int maj, out int min, out int pat))
            return;

        switch (part)
        {
            case 0: maj++; min = 0; pat = 0; break;
            case 1: min++; pat = 0;           break;
            case 2: pat++;                    break;
        }

        Apply(maj, min, pat);
    }

    static void Apply(int maj, int min, int pat)
    {
        PlayerSettings.bundleVersion = $"{maj}.{min}.{pat}";
        // Also bump the Android bundle version code to keep stores happy
        PlayerSettings.Android.bundleVersionCode = maj * 10000 + min * 100 + pat;
        AssetDatabase.SaveAssets();
        MainToolbar.Refresh(DropdownID);
        Debug.Log($"[VersionToolbar] Version set to {PlayerSettings.bundleVersion}");
    }

    static bool TryParse(string version, out int maj, out int min, out int pat)
    {
        maj = min = pat = 0;
        var parts = version?.Split('.');
        if (parts == null || parts.Length < 3 ||
            !int.TryParse(parts[0], out maj) ||
            !int.TryParse(parts[1], out min) ||
            !int.TryParse(parts[2], out pat))
        {
            Debug.LogWarning(
                $"[VersionToolbar] Version '{version}' is not in MAJOR.MINOR.PATCH format. " +
                "Please fix it in Player Settings before using version increment.");
            return false;
        }
        return true;
    }
}

/// <summary>
/// Build preprocessor — increments the patch version before every player build
/// when the "Auto-increment patch on build" option is enabled.
/// </summary>
class VersionAutoIncrementPreprocessor : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
	    if(report.summary.result != BuildResult.Succeeded)
	    {
		    Debug.Log("A");
		    return;
	    }
	    
        if (!EditorPrefs.GetBool("VersionToolbar.autoIncrement", false))
        {
		    Debug.Log("B");
	        return;
        }

        string current = PlayerSettings.bundleVersion;
        var parts = current?.Split('.');
        if (parts == null || parts.Length < 3 ||
            !int.TryParse(parts[0], out int maj) ||
            !int.TryParse(parts[1], out int min) ||
            !int.TryParse(parts[2], out int pat))
        {
            Debug.LogWarning(
                $"[VersionToolbar] Cannot auto-increment: version '{current}' " +
                "is not in MAJOR.MINOR.PATCH format.");
            return;
        }

        pat++;
        PlayerSettings.bundleVersion = $"{maj}.{min}.{pat}";
        PlayerSettings.Android.bundleVersionCode = maj * 10000 + min * 100 + pat;

        Debug.Log($"[VersionToolbar] Auto-incremented patch → {PlayerSettings.bundleVersion}");
        MainToolbar.Refresh(VersionToolbar.DropdownID);
    }
}