using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityEditorExtras.Editor
{
    [CreateAssetMenu(menuName = "Unity Extras/Style Class Settings", fileName = "StyleClassSettings")]
    public sealed class StyleClassSettings : ScriptableObject
    {
        [SerializeField] internal List<DefaultAsset> folders = new();
        [SerializeField] internal string className = "Style";
        [SerializeField] internal string classNamespace = string.Empty;

        public IReadOnlyList<DefaultAsset> Folders => folders;
        public string ClassName => className;
        public string ClassNamespace => classNamespace;

        [ContextMenu("Generate")]
        private void Generate() => StyleClassGenerator.Generate(this);
    }
}
