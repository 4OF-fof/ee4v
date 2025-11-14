using _4OF.ee4v.Core.Data;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.UI {
    public static class ProjectOverlay {
        [InitializeOnLoadMethod]
        private static void Initialize() {
            if (!EditorPrefsManager.EnableProjectExtension) return;
            EditorApplication.projectWindowItemOnGUI += ProjectOverlayContent;
        }

        private static void ProjectOverlayContent(string guid, Rect selectionRect) {
            var path = AssetDatabase.GUIDToAssetPath(guid);

            if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
                ProjectFolderOverlay.Draw(path, selectionRect);
        }
    }
}