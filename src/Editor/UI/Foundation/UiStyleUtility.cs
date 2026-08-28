using System;
using Ee4v.Core.EditorIntegration;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal static class UiStyleUtility
    {
        private const string PaletteStyleSheetPath =
            "Editor/UI/Foundation/ui-color-tokens.uss";

        public static void AddPackageStyleSheet(
            VisualElement root,
            string packageRelativePath)
        {
            if (root == null || string.IsNullOrWhiteSpace(packageRelativePath))
            {
                return;
            }

            AddStyleSheet(root, PaletteStyleSheetPath);
            if (string.Equals(
                    packageRelativePath,
                    PaletteStyleSheetPath,
                    StringComparison.Ordinal))
            {
                return;
            }

            AddStyleSheet(root, packageRelativePath);
        }

        private static void AddStyleSheet(
            VisualElement root,
            string packageRelativePath)
        {
            var styleSheet = LoadPackageStyleSheet(packageRelativePath);
            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
            {
                root.styleSheets.Add(styleSheet);
            }
        }

        private static StyleSheet LoadPackageStyleSheet(
            string packageRelativePath)
        {
            if (string.IsNullOrWhiteSpace(packageRelativePath))
            {
                return null;
            }

            var packageRoot = PackageAssetApi.GetPackageRootAssetPath();
            if (string.IsNullOrWhiteSpace(packageRoot))
            {
                return null;
            }

            var normalizedPath = packageRelativePath
                .Replace('\\', '/')
                .TrimStart('/');
            var assetPath = (packageRoot + "/" + normalizedPath)
                .Replace("//", "/");
            return AssetDatabase.LoadAssetAtPath<StyleSheet>(assetPath);
        }
    }
}
