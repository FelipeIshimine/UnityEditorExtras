using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityExtras.Editor;

[CustomPropertyDrawer(typeof(SceneSelectionAttribute))]
public class SceneSelectionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, label, new GUIContent("SceneSelection only works with string fields"));
            return;
        }

        var rect = EditorGUI.PrefixLabel(position, label);

        if (GUI.Button(rect, property.stringValue, EditorStyles.popup))
        {
            ShowSceneDropdown(rect, property);
        }
    }

    private void ShowSceneDropdown(Rect dropdownRect, SerializedProperty property)
    {
        var buildScenes = EditorBuildSettings.scenes.ToList();
        var buildSceneNames = buildScenes
            .Select(s => Path.GetFileNameWithoutExtension(s.path))
            .ToList();

        var allScenes = GetAllSceneFilesInAssets();
        var nonProfileScenes = allScenes.Where(s => !buildSceneNames.Contains(s)).ToList();

        var sceneOptions = new List<(string, string)>();
        
        if (buildSceneNames.Count > 0)
        {
            foreach (var sceneName in buildSceneNames)
            {
                sceneOptions.Add(($"In Profile/{sceneName}", sceneName));
            }
        }

        if (nonProfileScenes.Count > 0)
        {
            foreach (var sceneName in nonProfileScenes)
            {
                sceneOptions.Add(($"Not In Profile/{sceneName}", sceneName));
            }
        }

        var builder = new AdvancedDropdownBuilder()
            .WithTitle("Select Scene")
            .SetCallback(index =>
            {
                if (index >= 0 && index < sceneOptions.Count)
                {
                    var (_, sceneName) = sceneOptions[index];
                    
                    // Check if scene is in profile
                    if (!buildSceneNames.Contains(sceneName))
                    {
                        // Add to build settings
                        var sceneAssetPath = GetScenePathByName(sceneName);
                        if (!string.IsNullOrEmpty(sceneAssetPath))
                        {
                            var newScene = new EditorBuildSettingsScene(sceneAssetPath, true);
                            var scenes = buildScenes;
                            scenes.Add(newScene);
                            EditorBuildSettings.scenes = scenes.ToArray();
                        }
                    }

                    property.stringValue = sceneName;
                    property.serializedObject.ApplyModifiedProperties();
                }
            });

        builder.AddElements(sceneOptions, out _);
        builder.Build().Show(dropdownRect);
    }

    private List<string> GetAllSceneFilesInAssets()
    {
        var scenes = new List<string>();
        var sceneGuids = AssetDatabase.FindAssets("t:SceneAsset");
        
        foreach (var guid in sceneGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var sceneName = Path.GetFileNameWithoutExtension(path);
            scenes.Add(sceneName);
        }

        return scenes;
    }

    private string GetScenePathByName(string sceneName)
    {
        var sceneGuids = AssetDatabase.FindAssets("t:SceneAsset");
        
        foreach (var guid in sceneGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == sceneName)
            {
                return path;
            }
        }

        return null;
    }
}
