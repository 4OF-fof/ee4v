using UnityEditor;

using System.Linq;

using _4OF.ee4v.HierarchyExtension.Data;

namespace _4OF.ee4v.HierarchyExtension.Service {
    public class SceneListPostProcessor: AssetPostprocessor {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
            var shouldUpdate = importedAssets.Any(p => p.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase));

            if (!shouldUpdate) {
                if (movedAssets.Any(p => p.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase))
                    || deletedAssets.Any(p => p.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase))
                    || movedFromAssetPaths.Any(p => p.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase))) {
                    shouldUpdate = true;
                }
            }
            if (shouldUpdate) {
                SceneListController.SceneListRegister();
            }
        }
    }
}