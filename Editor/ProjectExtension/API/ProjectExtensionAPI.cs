using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace _4OF.ee4v.ProjectExtension.API {
    public static class ProjectExtensionAPI {
        private static readonly HashSet<string> HighlightedGuids = new();

        public static bool IsHighlighted(string guid) {
            return HighlightedGuids.Contains(guid);
        }

        public static void SetHighlights(IEnumerable<string> guids) {
            HighlightedGuids.Clear();
            if (guids != null) {
                foreach (var guid in guids) {
                    if (string.IsNullOrEmpty(guid)) continue;

                    HighlightedGuids.Add(guid);

                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    while (!string.IsNullOrEmpty(path)) {
                        path = Path.GetDirectoryName(path)?.Replace('\\', '/');
                        if (string.IsNullOrEmpty(path) || path == "Assets") break;

                        var parentGuid = AssetDatabase.AssetPathToGUID(path);
                        if (!string.IsNullOrEmpty(parentGuid)) {
                            HighlightedGuids.Add(parentGuid);
                        }
                    }
                }
            }
            EditorApplication.RepaintProjectWindow();
        }

        [MenuItem("ee4v/Clear highlight")]
        public static void ClearHighlights() {
            HighlightedGuids.Clear();
            EditorApplication.RepaintProjectWindow();
        }
    }
}