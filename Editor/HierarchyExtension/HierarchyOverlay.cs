using UnityEditor;
using UnityEngine;

using _4OF.ee4v.Core.Data;
using _4OF.ee4v.HierarchyExtension.UI.HierarchyItem;
using _4OF.ee4v.HierarchyExtension.UI.HierarchyScene;

namespace _4OF.ee4v.HierarchyExtension {
    public static class HierarchyOverlay {
        [InitializeOnLoadMethod]
        private static void Initialize() {
            if (!EditorPrefsManager.EnableHierarchyExtension) return;
            EditorApplication.hierarchyWindowItemOnGUI += HierarchyOverlayContent;
        }
        
        private static void HierarchyOverlayContent(int instanceId, Rect selectionRect) {
            if (EditorUtility.InstanceIDToObject(instanceId) is GameObject obj) {
                HierarchyItemOverlay.Draw(instanceId, obj, selectionRect);
            }
            else {
                HierarchySceneOverlay.Draw();
            }
        }
    }
}