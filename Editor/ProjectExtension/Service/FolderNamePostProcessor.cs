using System.Linq;
using _4OF.ee4v.ProjectExtension.Data;
using UnityEditor;

namespace _4OF.ee4v.ProjectExtension.Service {
    public class FolderNamePostProcessor : AssetPostprocessor {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
            if (deletedAssets is { Length: > 0 }) {
                foreach (var deletedPath in deletedAssets) {
                    if (string.IsNullOrEmpty(deletedPath)) continue;
                    FolderStyleController.Remove(deletedPath);
                }
            }

            if (movedAssets is { Length: > 0 }) {
                for (var i = 0; i < movedAssets.Length; i++) {
                    var newPath = movedAssets[i];
                    var oldPath = movedFromAssetPaths != null && movedFromAssetPaths.Length > i ? movedFromAssetPaths[i] : null;
                    if (string.IsNullOrEmpty(newPath) || string.IsNullOrEmpty(oldPath)) continue;
                    FolderStyleController.UpdatePath(oldPath, newPath);
                }
            }
        }
    }
}