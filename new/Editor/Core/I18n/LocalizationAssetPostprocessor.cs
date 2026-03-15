using Ee4v.Internal;
using UnityEditor;

namespace Ee4v.I18n
{
    internal sealed class LocalizationAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var root = PackagePathUtility.GetPackageRootAssetPath();
            if (string.IsNullOrEmpty(root))
            {
                return;
            }

            if (TouchesLocalization(importedAssets, root) ||
                TouchesLocalization(deletedAssets, root) ||
                TouchesLocalization(movedAssets, root) ||
                TouchesLocalization(movedFromAssetPaths, root))
            {
                I18N.Reload();
            }
        }

        private static bool TouchesLocalization(string[] paths, string root)
        {
            if (paths == null)
            {
                return false;
            }

            for (var i = 0; i < paths.Length; i++)
            {
                var path = paths[i];
                if (path != null &&
                    path.StartsWith(root + "/Editor/") &&
                    path.Contains("/Localization/"))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
