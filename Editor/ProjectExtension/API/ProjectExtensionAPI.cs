using System.Collections.Generic;
using UnityEditor;

namespace _4OF.ee4v.ProjectExtension.API {
    public static class ProjectExtensionAPI {
        private static readonly HashSet<string> HighlightedGuids = new();

        public static bool IsHighlighted(string guid) {
            return HighlightedGuids.Contains(guid);
        }

        public static void SetHighlights(IEnumerable<string> guids) {
            HighlightedGuids.Clear();
            if (guids != null)
                foreach (var guid in guids)
                    HighlightedGuids.Add(guid);
            EditorApplication.RepaintProjectWindow();
        }

        [MenuItem("ee4v/Clear highlight")]
        public static void ClearHighlights() {
            HighlightedGuids.Clear();
            EditorApplication.RepaintProjectWindow();
        }
    }
}