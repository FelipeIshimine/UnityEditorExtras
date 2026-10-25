using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;

namespace UnityEditorExtras.Editor
{
    public static class StyleClassGenerator
    {
        private static readonly Regex Comment = new(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex ClassSelector = new(@"\.(-?[_a-zA-Z][_a-zA-Z0-9-]*)", RegexOptions.Compiled);
        private static readonly Regex Identifier = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        [MenuItem("Tools/Unity Extras/Generate Style Classes")]
        public static void GenerateAll()
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(StyleClassSettings)}"))
            {
                var settings = AssetDatabase.LoadAssetAtPath<StyleClassSettings>(AssetDatabase.GUIDToAssetPath(guid));
                if (settings.Folders.Count > 0)
                    Generate(settings);
            }
        }

        public static void Generate(StyleClassSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            string settingsPath = AssetDatabase.GetAssetPath(settings);
            if (!Identifier.IsMatch(settings.ClassName))
                throw new InvalidOperationException($"{settingsPath}: '{settings.ClassName}' is not a valid C# class name.");
            if (settings.Folders.Count == 0)
                throw new InvalidOperationException($"{settingsPath}: no folders to scan for style sheets.");

            string[] folders = settings.Folders.Select(folder => FolderPath(settingsPath, folder)).ToArray();
            var sheets = AssetDatabase.FindAssets("t:StyleSheet", folders)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".uss", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToDictionary(path => path, File.ReadAllText);

            string source = Emit(settings.ClassName, settings.ClassNamespace, Members(settings.ClassName, sheets));
            string outputPath = $"{Path.GetDirectoryName(settingsPath).Replace('\\', '/')}/{settings.ClassName}.cs";

            if (File.Exists(outputPath) && File.ReadAllText(outputPath) == source)
                return;

            File.WriteAllText(outputPath, source, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(outputPath);
        }

        internal static IEnumerable<string> ClassesIn(string uss)
        {
            string text = Comment.Replace(uss, string.Empty);
            var selectors = new StringBuilder();
            int depth = 0;

            foreach (char c in text)
            {
                if (c == '{')
                    depth++;
                else if (c == '}')
                    depth--;
                else if (depth == 0)
                    selectors.Append(c);
            }

            return ClassSelector.Matches(selectors.ToString()).Select(match => match.Groups[1].Value).Distinct();
        }

        internal static string IdentifierOf(string ussClass) =>
            string.Join("_", ussClass.Split(new[] { "--" }, StringSplitOptions.None).Select(Pascal));

        internal static SortedDictionary<string, string> Members(string className, IReadOnlyDictionary<string, string> sheets)
        {
            var members = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var sources = new Dictionary<string, string>();

            foreach ((string path, string uss) in sheets)
            {
                foreach (string ussClass in ClassesIn(uss))
                {
                    string identifier = IdentifierOf(ussClass);
                    if (identifier == className)
                        throw new InvalidOperationException($"{path}: the class '.{ussClass}' would be named {identifier}, the same as the generated class.");

                    if (members.TryGetValue(identifier, out string existing))
                    {
                        if (existing != ussClass)
                            throw new InvalidOperationException(
                                $"'.{ussClass}' ({path}) and '.{existing}' ({sources[identifier]}) would both be named {identifier}; rename one of them.");
                        continue;
                    }

                    members.Add(identifier, ussClass);
                    sources.Add(identifier, path);
                }
            }

            return members;
        }

        internal static string Emit(string className, string classNamespace, SortedDictionary<string, string> members)
        {
            bool hasNamespace = !string.IsNullOrEmpty(classNamespace);
            string indent = hasNamespace ? "    " : string.Empty;
            var builder = new StringBuilder();

            if (hasNamespace)
            {
                builder.AppendLine($"namespace {classNamespace}");
                builder.AppendLine("{");
            }

            builder.AppendLine($"{indent}public static class {className}");
            builder.AppendLine($"{indent}{{");
            foreach ((string identifier, string ussClass) in members)
                builder.AppendLine($"{indent}    public const string {identifier} = \"{ussClass}\";");
            builder.AppendLine($"{indent}}}");

            if (hasNamespace)
                builder.AppendLine("}");

            return builder.ToString();
        }

        private static string Pascal(string part) =>
            string.Concat(part.Split('-', '_')
                .Where(word => word.Length > 0)
                .Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1)));

        private static string FolderPath(string settingsPath, DefaultAsset folder)
        {
            if (folder == null)
                throw new InvalidOperationException($"{settingsPath}: a folder entry is empty.");

            string path = AssetDatabase.GetAssetPath(folder);
            if (!AssetDatabase.IsValidFolder(path))
                throw new InvalidOperationException($"{settingsPath}: '{path}' is not a folder.");
            return path;
        }
    }

    internal sealed class StyleClassPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool touchesStyles = imported.Concat(deleted).Concat(moved).Concat(movedFrom)
                .Any(path => path.EndsWith(".uss", StringComparison.OrdinalIgnoreCase)
                    || AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(StyleClassSettings));

            if (touchesStyles)
                StyleClassGenerator.GenerateAll();
        }
    }
}
