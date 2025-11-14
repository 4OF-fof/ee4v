using _4OF.ee4v.Core.Data;
using _4OF.ee4v.HierarchyExtension.UI.HierarchyItem;
using _4OF.ee4v.HierarchyExtension.UI.HierarchyScene;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.HierarchyExtension.UI {
    public static class HierarchyOverlay {
        [InitializeOnLoadMethod]
        private static void Initialize() {
            if (!EditorPrefsManager.EnableHierarchyExtension) return;
            EditorApplication.hierarchyWindowItemOnGUI += HierarchyOverlayContent;
        }

        private static void HierarchyOverlayContent(int instanceId, Rect selectionRect) {
            if (EditorUtility.InstanceIDToObject(instanceId) is GameObject obj) {
                if (obj.CompareTag("EditorOnly") && !obj.activeSelf) obj.hideFlags |= HideFlags.HideInHierarchy;
                HierarchyItemOverlay.Draw(instanceId, obj, selectionRect);
            }
            else {
                HierarchySceneOverlay.Draw();
            }
        }
    }
}