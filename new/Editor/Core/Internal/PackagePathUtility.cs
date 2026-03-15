using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Ee4v.Core.Internal
{
    internal static class PackagePathUtility
    {
        private const string Ee4vNamespacePrefix = "Ee4v.";
        private static readonly Regex NamespaceRegex =
            new Regex(@"^\s*namespace\s+([A-Za-z_][A-Za-z0-9_\.]*)\s*(?:\{|;)", RegexOptions.Multiline | RegexOptions.Compiled);

        private static string _packageRootAssetPath;
        private static string _packageRootFullPath;
        private static readonly Dictionary<string, string> SourceFileNamespaceCache =
            new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

        public static string GetPackageRootAssetPath()
        {
            if (!string.IsNullOrEmpty(_packageRootAssetPath))
            {
                return _packageRootAssetPath;
            }

            var assetPath = AssetDatabase.FindAssets("Ee4vPackageAnchor")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path => path.EndsWith("Editor/Core/Internal/Ee4vPackageAnchor.cs"));

            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            _packageRootAssetPath = Path.GetDirectoryName(
                    Path.GetDirectoryName(
                        Path.GetDirectoryName(
                            Path.GetDirectoryName(assetPath))))
                ?.Replace('\\', '/');
            return _packageRootAssetPath;
        }

        public static string GetPackageRootFullPath()
        {
            if (!string.IsNullOrEmpty(_packageRootFullPath))
            {
                return _packageRootFullPath;
            }

            var assetPath = GetPackageRootAssetPath();
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            _packageRootFullPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath));
            return _packageRootFullPath;
        }

        public static IEnumerable<string> GetLocalizationRootFullPaths()
        {
            var packageRoot = GetPackageRootFullPath();
            if (string.IsNullOrEmpty(packageRoot))
            {
                return Enumerable.Empty<string>();
            }

            var editorRoot = Path.Combine(packageRoot, "Editor");
            if (!Directory.Exists(editorRoot))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.GetDirectories(editorRoot, "Localization", SearchOption.AllDirectories)
                .OrderBy(path => path, System.StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static string GetEditorRootFullPath()
        {
            var packageRoot = GetPackageRootFullPath();
            return string.IsNullOrEmpty(packageRoot) ? null : Path.Combine(packageRoot, "Editor");
        }

        public static string GetScopeNameForLocalizationRoot(string localizationRootPath)
        {
            if (string.IsNullOrWhiteSpace(localizationRootPath))
            {
                return null;
            }

            var parentDirectory = Path.GetDirectoryName(localizationRootPath);
            return string.IsNullOrWhiteSpace(parentDirectory)
                ? null
                : Path.GetFileName(parentDirectory);
        }

        public static string GetDeclaredNamespace(string sourceFilePath)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                return null;
            }

            string namespaceName;
            if (SourceFileNamespaceCache.TryGetValue(sourceFilePath, out namespaceName))
            {
                return namespaceName;
            }

            var content = File.ReadAllText(sourceFilePath);
            var match = NamespaceRegex.Match(content);
            namespaceName = match.Success ? match.Groups[1].Value : null;
            SourceFileNamespaceCache[sourceFilePath] = namespaceName;
            return namespaceName;
        }

        public static string GetScopeNameForNamespace(string namespaceName)
        {
            if (string.IsNullOrWhiteSpace(namespaceName) ||
                !namespaceName.StartsWith(Ee4vNamespacePrefix, System.StringComparison.Ordinal))
            {
                return null;
            }

            var segments = namespaceName.Split('.');
            return segments.Length < 2 || string.IsNullOrWhiteSpace(segments[1]) ? null : segments[1];
        }
    }
}
