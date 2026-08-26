using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Toolbars;
using UnityEngine;

namespace UnityExtras.Editor
{
    public static class BuildProfileToolbar
    {
        const string DropdownID = "Game/BuildProfileSwitcher";
        const string ClassicLabel = "Classic";

        [MainToolbarElement(DropdownID, defaultDockPosition = MainToolbarDockPosition.Left)]
        public static MainToolbarElement CreateBuildProfileDropdown()
        {
            var active = BuildProfile.GetActiveBuildProfile();
            string label = active != null ? active.name : ClassicLabel;

            var icon = EditorGUIUtility.FindTexture("BuildSettings.WebGL.Small");
            var content = new MainToolbarContent(label, icon, "Switch Build Profile");

            return new MainToolbarDropdown(content, ShowMenu);
        }

        static void ShowMenu(Rect dropdownRect)
        {
            var profiles = AssetDatabase
                .FindAssets("t:BuildProfile")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p)
                .ToArray();

            if (profiles.Length == 0)
            {
                Debug.LogWarning("[BuildProfileToolbar] No BuildProfile assets found.");
                return;
            }

            var builder = new AdvancedDropdownBuilder()
                .WithTitle("Switch Build Profile")
                .SetCallback(index =>
                {
                    var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(profiles[index]);
                    if (profile == null) return;
                    BuildProfile.SetActiveBuildProfile(profile);
                    MainToolbar.Refresh(DropdownID);
                });

            foreach (string path in profiles)
                builder.AddElement(Path.GetFileNameWithoutExtension(path), out _);

            builder.Build().Show(dropdownRect);
        }
    }
}
