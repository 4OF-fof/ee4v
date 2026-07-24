using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace Ee4v.FolderContentOverlay
{
    internal sealed class FolderContentOverlayAssetPostprocessor
        : AssetPostprocessor
    {
        internal static event Action<IReadOnlyCollection<string>>
            FoldersChanged;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var affectedFolders = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            CollectParentFolders(importedAssets, affectedFolders);
            CollectParentFolders(deletedAssets, affectedFolders);
            CollectParentFolders(movedAssets, affectedFolders);
            CollectParentFolders(movedFromAssetPaths, affectedFolders);

            if (affectedFolders.Count > 0)
            {
                FoldersChanged?.Invoke(affectedFolders
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray());
            }
        }

        private static void CollectParentFolders(
            IEnumerable<string> assetPaths,
            ISet<string> output)
        {
            if (assetPaths == null)
            {
                return;
            }

            foreach (var assetPath in assetPaths)
            {
                if (string.IsNullOrWhiteSpace(assetPath) ||
                    assetPath.EndsWith(
                        ".meta",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var parentPath = Path.GetDirectoryName(assetPath)
                    ?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(parentPath))
                {
                    CollectFolderAndAncestors(parentPath, output);
                }
            }
        }

        internal static void CollectFolderAndAncestors(
            string folderPath,
            ISet<string> output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            var current = folderPath?.Replace('\\', '/').TrimEnd('/');
            while (!string.IsNullOrEmpty(current))
            {
                output.Add(current);

                var parent = Path.GetDirectoryName(current)
                    ?.Replace('\\', '/');
                if (string.IsNullOrEmpty(parent) ||
                    string.Equals(
                        parent,
                        current,
                        StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                current = parent;
            }
        }
    }
}
