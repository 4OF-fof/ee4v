using System.IO;
using System.Linq;
using UnityEditor;

namespace Ee4v.Core.EditorIntegration
{
    public static class PackageAssetApi
    {
        private static string _packageRootAssetPath;

        public static string GetPackageRootAssetPath()
        {
            if (!string.IsNullOrEmpty(_packageRootAssetPath))
            {
                return _packageRootAssetPath;
            }

            var assetPath = AssetDatabase.FindAssets("Ee4vPackageAnchor")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path =>
                    path.EndsWith("Editor/Core/Internal/Ee4vPackageAnchor.cs"));

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
    }
}
