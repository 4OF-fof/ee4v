using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace _4OF.ee4v.ProjectExtension {
    internal class FolderIconCacheProcessor : AssetPostprocessor {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths) {
            var foldersToInvalidate = new HashSet<string>();

            CollectFoldersToInvalidate(importedAssets, foldersToInvalidate);
            CollectFoldersToInvalidate(deletedAssets, foldersToInvalidate);
            CollectFoldersToInvalidate(movedFromAssetPaths, foldersToInvalidate);
            CollectFoldersToInvalidate(movedAssets, foldersToInvalidate);

            foreach (var folderPath in foldersToInvalidate) FolderContentService.InvalidateCache(folderPath);
        }

        private static void CollectFoldersToInvalidate(string[] assetPaths, HashSet<string> foldersToInvalidate) {
            if (assetPaths == null || assetPaths.Length == 0) return;

            foreach (var assetPath in assetPaths) {
                if (string.IsNullOrEmpty(assetPath)) continue;

                if (assetPath.EndsWith(".meta")) continue;

                if (AssetDatabase.IsValidFolder(assetPath)) {
                    foldersToInvalidate.Add(assetPath);
                    continue;
                }

                var parentFolder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(parentFolder)) foldersToInvalidate.Add(parentFolder);
            }
        }
    }
}