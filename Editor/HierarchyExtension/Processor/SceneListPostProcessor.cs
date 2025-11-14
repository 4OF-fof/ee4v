using System;
using System.Linq;
using UnityEditor;

namespace _4OF.ee4v.HierarchyExtension.Service {
    public class SceneListPostProcessor : AssetPostprocessor {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths) {
            var shouldUpdate = importedAssets.Any(p => p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase));

            if (!shouldUpdate)
                if (movedAssets.Any(p => p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    || deletedAssets.Any(p => p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    || movedFromAssetPaths.Any(p => p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)))
                    shouldUpdate = true;

            if (shouldUpdate) SceneListService.SceneListRegister();
        }
    }
}