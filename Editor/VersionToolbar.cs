using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEditor.Toolbars;
using UnityEngine;

/// <summary>
/// Main-toolbar helper for game versioning and builds.
///
/// The version dropdown shows the current <see cref="PlayerSettings.bundleVersion"/> and lets you:
///   • Toggle auto-increment of the patch number before each build (default ON)
///   • Toggle splitting build output by platform (default ON)
///   • Manually increment major / minor / patch
///   • Copy the version string / open Player Settings
///
/// Two build buttons run the project through the <b>active Build Profile</b> (falling back to the
/// active build target) and place the output under:
///   • <c>Builds/{version}/{platform}</c>  when "split by platform" is ON
///   • <c>Builds/{version}</c>             when it is OFF
///
/// Auto-increment happens <b>before</b> the build (bump-then-build) and is persisted via
/// <see cref="AssetDatabase.SaveAssets"/>, so the toolbar version always matches the newest build folder.
///
/// Expected version format: MAJOR.MINOR.PATCH (e.g. "1.4.7"). A malformed version throws loudly.
/// </summary>
public static class VersionToolbar
{
	public const string DropdownID = "Game/Version";
	public const string BuildButtonID = "Game/Build";
	public const string BuildRunButtonID = "Game/BuildAndRun";

	const string AutoIncrPref = "VersionToolbar.autoIncrement";
	const string SplitPlatformPref = "VersionToolbar.splitByPlatform";

	static bool AutoIncrement
	{
		get => EditorPrefs.GetBool(AutoIncrPref, true);
		set => EditorPrefs.SetBool(AutoIncrPref, value);
	}

	static bool SplitByPlatform
	{
		get => EditorPrefs.GetBool(SplitPlatformPref, true);
		set => EditorPrefs.SetBool(SplitPlatformPref, value);
	}

	// ── Toolbar elements ──────────────────────────────────────────────────────

	[MainToolbarElement(DropdownID, defaultDockPosition = MainToolbarDockPosition.Right)]
	public static MainToolbarElement CreateVersionDropdown()
	{
		string version = PlayerSettings.bundleVersion;
		string label = $"v{version}{(AutoIncrement ? " ⬆" : "")}";

		var content = new MainToolbarContent(
			label,
			$"Game version: {version}\n"                                         +
			$"Auto-increment patch on build: {(AutoIncrement ? "ON" : "OFF")}\n" +
			$"Split builds by platform: {(SplitByPlatform ? "ON" : "OFF")}");

		return new MainToolbarDropdown(content, ShowMenu);
	}

	[MainToolbarElement(BuildButtonID, defaultDockPosition = MainToolbarDockPosition.Right)]
	public static MainToolbarElement CreateBuildButton()
	{
		var content = new MainToolbarContent(
			"🔨 Build",
			$"Build into '{DescribeDestination()}'.");

		return new MainToolbarButton(content, () => RunBuild(andRun: false));
	}

	[MainToolbarElement(BuildRunButtonID, defaultDockPosition = MainToolbarDockPosition.Right)]
	public static MainToolbarElement CreateBuildAndRunButton()
	{
		var content = new MainToolbarContent(
			"▶ Build & Run",
			$"Build and run into '{DescribeDestination()}'.");

		return new MainToolbarButton(content, () => RunBuild(andRun: true));
	}

	// ── Build flow ────────────────────────────────────────────────────────────

	static void RunBuild(bool andRun)
	{
		// Validate loudly — a malformed version is a bug we want surfaced, not silently skipped.
		if (!TryParse(PlayerSettings.bundleVersion, out int maj, out int min, out int pat))
			throw new InvalidOperationException(
				$"[VersionToolbar] Cannot build: version '{PlayerSettings.bundleVersion}' " +
				"is not in MAJOR.MINOR.PATCH format. Fix it in Player Settings.");

		// Bump-before-build so the destination folder is named with the new version.
		if (AutoIncrement)
			Apply(maj, min, pat + 1);

		string version = PlayerSettings.bundleVersion;

		BuildProfile profile = BuildProfile.GetActiveBuildProfile();
		BuildTarget target = EditorUserBuildSettings.activeBuildTarget;

		string platform = target.ToString();
		string destFolder = SplitByPlatform
			? Path.Combine("Builds", version, platform)
			: Path.Combine("Builds", version);

		Directory.CreateDirectory(destFolder);

		// File-per-build targets need the executable name appended inside the folder;
		// folder targets (e.g. WebGL) build directly into the folder.
		string location = destFolder;
		string extension = ExecutableExtension(target);
		if (extension != null)
			location = Path.Combine(destFolder, PlayerSettings.productName + extension);

		BuildOptions buildOptions = andRun ? BuildOptions.AutoRunPlayer : BuildOptions.None;

		Debug.Log($"[VersionToolbar] Building v{version} for {target} → {location}" +
			(profile != null ? $" (profile: {profile.name})" : " (no active build profile)"));

		BuildReport report;
		if (profile != null)
		{
			report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
			{
				buildProfile = profile,
				locationPathName = location,
				options = buildOptions,
			});
		}
		else
		{
			report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
			{
				scenes = GetEnabledScenes(),
				locationPathName = location,
				target = target,
				options = buildOptions,
			});
		}

		BuildSummary summary = report.summary;
		if (summary.result == BuildResult.Succeeded)
		{
			Debug.Log($"[VersionToolbar] Build succeeded: {summary.totalSize} bytes → {location}");
			EditorUtility.RevealInFinder(destFolder);
		}
		else
			Debug.LogError($"[VersionToolbar] Build {summary.result} ({summary.totalErrors} errors) → {location}");

		MainToolbar.Refresh(DropdownID);
	}

	static string DescribeDestination()
	{
		string version = PlayerSettings.bundleVersion;
		string platform = EditorUserBuildSettings.activeBuildTarget.ToString();
		return SplitByPlatform
			? Path.Combine("Builds", version, platform)
			: Path.Combine("Builds", version);
	}

	/// <summary>Executable extension for file-per-build targets, or null for folder-based targets (WebGL).</summary>
	static string ExecutableExtension(BuildTarget target)
	{
		switch (target)
		{
			case BuildTarget.StandaloneWindows:
			case BuildTarget.StandaloneWindows64: return ".exe";
			case BuildTarget.StandaloneOSX: return ".app";
			case BuildTarget.Android: return ".apk";
			default: return null; // WebGL, Linux server dir, etc.
		}
	}

	static string[] GetEnabledScenes()
	{
		var scenes = EditorBuildSettings.scenes;
		var enabled = new System.Collections.Generic.List<string>();
		foreach (var scene in scenes)
			if (scene.enabled)
				enabled.Add(scene.path);
		return enabled.ToArray();
	}

	// ── Dropdown menu ─────────────────────────────────────────────────────────

	static void ShowMenu(Rect dropdownRect)
	{
		var menu = new GenericMenu();

		menu.AddItem(new GUIContent("Auto-increment patch before build"), AutoIncrement, () =>
		{
			AutoIncrement = !AutoIncrement;
			MainToolbar.Refresh(DropdownID);
		});
		menu.AddItem(new GUIContent("Split builds by platform"), SplitByPlatform, () =>
		{
			SplitByPlatform = !SplitByPlatform;
			MainToolbar.Refresh(DropdownID);
		});

		menu.AddSeparator("");

		menu.AddItem(new GUIContent("Increment Patch (x.x.+1)"), false, () => Increment(2));
		menu.AddItem(new GUIContent("Increment Minor (x.+1.0)"), false, () => Increment(1));
		menu.AddItem(new GUIContent("Increment Major (+1.0.0)"), false, () => Increment(0));

		menu.AddSeparator("");

		menu.AddItem(new GUIContent("Copy version to clipboard"), false, () =>
			GUIUtility.systemCopyBuffer = PlayerSettings.bundleVersion);

		menu.AddSeparator("");

		menu.AddItem(new GUIContent("Open build folder"), false, () =>
		{
			string dest = DescribeDestination();
			if (!Directory.Exists(dest))
				Directory.CreateDirectory(dest);
			EditorUtility.RevealInFinder(dest);
		});
		menu.AddItem(new GUIContent("Open Builds root"), false, () =>
		{
			Directory.CreateDirectory("Builds");
			EditorUtility.RevealInFinder("Builds");
		});

		menu.AddSeparator("");

		menu.AddItem(new GUIContent("Open Player Settings…"), false, () =>
			SettingsService.OpenProjectSettings("Project/Player"));

		menu.DropDown(dropdownRect);
	}

	// ── Version manipulation ──────────────────────────────────────────────────

	/// <param name="part">0=major, 1=minor, 2=patch</param>
	static void Increment(int part)
	{
		if (!TryParse(PlayerSettings.bundleVersion, out int maj, out int min, out int pat))
			throw new InvalidOperationException(
				$"[VersionToolbar] Cannot increment: version '{PlayerSettings.bundleVersion}' " +
				"is not in MAJOR.MINOR.PATCH format.");

		switch (part)
		{
			case 0:
				maj++;
				min = 0;
				pat = 0;
				break;
			case 1:
				min++;
				pat = 0;
				break;
			case 2: pat++; break;
		}

		Apply(maj, min, pat);
	}

	static void Apply(int maj, int min, int pat)
	{
		PlayerSettings.bundleVersion = $"{maj}.{min}.{pat}";
		// Keep the Android bundle version code monotonic with the semantic version.
		PlayerSettings.Android.bundleVersionCode = maj * 10000 + min * 100 + pat;
		AssetDatabase.SaveAssets();
		MainToolbar.Refresh(DropdownID);
		Debug.Log($"[VersionToolbar] Version set to {PlayerSettings.bundleVersion}");
	}

	static bool TryParse(string version, out int maj, out int min, out int pat)
	{
		maj = min = pat = 0;
		var parts = version?.Split('.');
		return parts != null && parts.Length >= 3
			&& int.TryParse(parts[0], out maj)
			&& int.TryParse(parts[1], out min)
			&& int.TryParse(parts[2], out pat);
	}
}