using _4OF.ee4v.Core.Setting;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.ProjectExtension.API;
using _4OF.ee4v.ProjectExtension.ItemStyle;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.Core {
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

            if (ProjectExtensionAPI.IsHighlighted(guid))
                EditorGUI.DrawRect(selectionRect, ColorPreset.HighlightColor);
        }
    }
}