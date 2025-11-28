using _4OF.ee4v.Core.Utility;
using _4OF.ee4v.ProjectExtension.ItemStyle;
using UnityEditor;

namespace _4OF.ee4v.ProjectExtension.Toolbar {
    public class FolderNamePostProcessor : AssetPostprocessor {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths) {
            if (deletedAssets is { Length: > 0 })
                foreach (var deletedPath in deletedAssets) {
                    if (string.IsNullOrEmpty(deletedPath)) continue;
                    var p = AssetUtility.NormalizePath(deletedPath);
                    var index = FolderStyleService.IndexOfPath(p);
                    if (index >= 0) FolderStyleList.instance.RemoveFolderStyle(index);
                }

            if (movedAssets is not { Length: > 0 }) return;
            for (var i = 0; i < movedAssets.Length; i++) {
                var newPath = movedAssets[i];
                var oldPath = movedFromAssetPaths != null && movedFromAssetPaths.Length > i
                    ? movedFromAssetPaths[i]
                    : null;
                if (string.IsNullOrEmpty(newPath) || string.IsNullOrEmpty(oldPath)) continue;
                var oldP = AssetUtility.NormalizePath(oldPath);
                var newP = AssetUtility.NormalizePath(newPath);
                var idx = FolderStyleService.IndexOfPath(oldP);
                if (idx >= 0) FolderStyleList.instance.UpdateFolderStyle(idx, newP);
            }
        }
    }
}