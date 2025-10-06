using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.HierarchyExtension.Service {
    public static class ReflectionWrapper {
        private static readonly PropertyInfo LastInteractedHierarchyWindow =
            Type.GetType("UnityEditor.SceneHierarchyWindow,UnityEditor")?.GetProperty("lastInteractedHierarchyWindow",
                BindingFlags.Public | BindingFlags.Static);

        private static object _treeView;
        private static MethodInfo _findItemMethod;

        public static bool IsHierarchyScrollbarVisible() {
            const BindingFlags instanceFlags = BindingFlags.NonPublic | BindingFlags.Instance;

            var lastWindow = LastInteractedHierarchyWindow
                ?.GetValue(null);

            var sceneHierarchy = lastWindow?.GetType()
                .GetField("m_SceneHierarchy", instanceFlags)
                ?.GetValue(lastWindow);

            var finalTreeView = sceneHierarchy?.GetType()
                .GetField("m_TreeView", instanceFlags)
                ?.GetValue(sceneHierarchy);

            if (finalTreeView?.GetType()
                    .GetProperty("showingVerticalScrollBar", BindingFlags.Public | instanceFlags)
                    ?.GetValue(finalTreeView) is bool isVisible) return isVisible;

            return false;
        }

        public static void SetItemIcon(int instanceId, Texture2D icon) {
            if (icon == null) {
                var obj = EditorUtility.InstanceIDToObject(instanceId);
                if (obj != null) {
                    if (PrefabUtility.GetPrefabAssetType(obj) != PrefabAssetType.NotAPrefab &&
                        !PrefabUtility.IsAnyPrefabInstanceRoot(obj as GameObject))
                        icon = EditorGUIUtility.IconContent("GameObject Icon").image as Texture2D;
                    else
                        icon = EditorGUIUtility.ObjectContent(obj, typeof(GameObject)).image as Texture2D;
                }
            }

            if (_treeView == null || _findItemMethod == null) AssignTreeView();

            try {
                if (_findItemMethod == null) return;
                var item = _findItemMethod.Invoke(_treeView, new object[] { instanceId });
                if (item == null) return;

                var iconProp = item.GetType().GetProperty("icon");
                if (iconProp != null && iconProp.CanWrite) {
                    iconProp.SetValue(item, icon);
                }
                else {
                    var iconFieldInfo = item.GetType().GetField("icon", BindingFlags.Public | BindingFlags.Instance);
                    if (iconFieldInfo != null) iconFieldInfo.SetValue(item, icon);
                }
            }
            catch (Exception ex) {
                Debug.LogWarning($"{ex.Message}");
            }
        }

        private static void AssignTreeView() {
            var lastWindow = LastInteractedHierarchyWindow?.GetValue(null);

            var sceneHierarchy = lastWindow?.GetType()
                .GetField("m_SceneHierarchy", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(lastWindow);
            if (sceneHierarchy == null) return;

            _treeView = sceneHierarchy.GetType().GetField("m_TreeView", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(sceneHierarchy);
            if (_treeView == null) return;

            _findItemMethod = _treeView.GetType().GetMethod("FindItem", BindingFlags.Public | BindingFlags.Instance);
        }
    }
}