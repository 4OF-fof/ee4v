using Ee4v.Core.Internal;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal static class UiStyleUtility
    {
        public static void AddPackageStyleSheet(VisualElement root, string packageRelativePath)
        {
            if (root == null || string.IsNullOrWhiteSpace(packageRelativePath))
            {
                return;
            }

            var packageRoot = PackagePathUtility.GetPackageRootAssetPath();
            if (string.IsNullOrWhiteSpace(packageRoot))
            {
                return;
            }

            var normalizedPath = packageRelativePath.Replace('\\', '/').TrimStart('/');
            var assetPath = (packageRoot + "/" + normalizedPath).Replace("//", "/");
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(assetPath);
            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
            {
                root.styleSheets.Add(styleSheet);
            }
        }
    }
}
