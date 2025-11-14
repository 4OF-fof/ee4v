using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.Service {
    public static class FolderContentService {
        private static readonly Dictionary<string, Texture> IconCache = new();

        public static Texture GetMostIconInFolder(string path) {
            if (IconCache.TryGetValue(path, out var cachedIcon)) return cachedIcon;

            var icon = CalculateMostIconInFolder(path);
            IconCache[path] = icon;
            return icon;
        }

        public static void InvalidateCache(string folderPath) {
            if (string.IsNullOrEmpty(folderPath)) return;
            IconCache.Remove(folderPath);
        }

        private static Texture CalculateMostIconInFolder(string path) {
            var childAssetPaths = Directory.GetFileSystemEntries(path);
            var iconCounts = new Dictionary<Texture, int>();

            foreach (var assetPath in childAssetPaths) {
                if (AssetDatabase.IsValidFolder(assetPath) || Path.GetExtension(assetPath) == ".meta") continue;

                var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                var icon = asset is Texture
                    ? EditorGUIUtility.IconContent("Texture Icon").image
                    : AssetPreview.GetMiniThumbnail(asset);
                if (icon == null) continue;
                if (!iconCounts.TryAdd(icon, 1)) iconCounts[icon]++;
            }

            return iconCounts.Count == 0 ? null : iconCounts.OrderByDescending(kv => kv.Value).FirstOrDefault().Key;
        }
    }
}