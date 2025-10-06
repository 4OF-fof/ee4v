using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _4OF.ee4v.HierarchyExtension.Service {
    public static class ReflectionWrapper {
        private static readonly Type SceneHierarchyWindowType =
            typeof(Editor).Assembly.GetType("UnityEditor.SceneHierarchyWindow");

        private static readonly PropertyInfo LastInteractedHierarchyWindow =
            SceneHierarchyWindowType?.GetProperty("lastInteractedHierarchyWindow",
                BindingFlags.Public | BindingFlags.Static);

        private static object _treeView;
        private static MethodInfo _findItemMethod;

        public static bool IsHierarchyScrollbarVisible() {
            const BindingFlags instanceFlags = BindingFlags.NonPublic | BindingFlags.Instance;
            const BindingFlags staticFlags = BindingFlags.Public      | BindingFlags.Static;

            var lastWindow = typeof(Editor).Assembly
                .GetType("UnityEditor.SceneHierarchyWindow")
                ?.GetProperty("lastInteractedHierarchyWindow", staticFlags)
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

        public static void DrawMaterialInspector(MaterialEditor materialEditor, Material material) {
            var customShaderGUIField = typeof(MaterialEditor).GetField("m_CustomShaderGUI",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (customShaderGUIField != null) {
                var customShaderGUI = customShaderGUIField.GetValue(materialEditor);
                var props = MaterialEditor.GetMaterialProperties(new Object[] { material });

                if (customShaderGUI != null) {
                    var onGUIMethod = customShaderGUI.GetType().GetMethod("OnGUI",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (onGUIMethod != null)
                        using (new GUILayout.HorizontalScope()) {
                            GUILayout.Space(8);
                            using (new GUILayout.VerticalScope()) {
                                onGUIMethod.Invoke(customShaderGUI, new object[] { materialEditor, props });
                            }

                            GUILayout.Space(4);
                        }
                    else
                        Debug.LogError("Could not find OnGUI method in custom ShaderGUI.");
                }
                else {
                    Debug.LogError("No custom ShaderGUI.");
                }
            }
            else {
                Debug.LogError("Could not find m_CustomShaderGUI field via reflection.");
            }
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