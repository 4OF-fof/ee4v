using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;

namespace Ee4v.Internal
{
    internal static class PackagePathUtility
    {
        private static string _packageRootAssetPath;
        private static string _packageRootFullPath;

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
    }
}
