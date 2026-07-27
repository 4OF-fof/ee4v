using System;
using System.Linq;
using UnityEditor;

namespace Ee4v.SceneSwitcher
{
    internal sealed class SceneSwitcherAssetPostprocessor
        : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (ContainsScene(importedAssets) ||
                ContainsScene(deletedAssets) ||
                ContainsScene(movedAssets) ||
                ContainsScene(movedFromAssetPaths))
            {
                SceneSwitcherBootstrap.RefreshCatalog();
            }
        }

        private static bool ContainsScene(string[] paths)
        {
            return paths != null &&
                   paths.Any(path =>
                       path.EndsWith(
                           ".unity",
                           StringComparison.OrdinalIgnoreCase));
        }
    }
}
