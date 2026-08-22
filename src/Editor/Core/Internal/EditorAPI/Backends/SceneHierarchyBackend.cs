using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Core.Internal.EditorAPI.Backends
{
    internal static class SceneHierarchyBackend
    {
        private const BindingFlags InstanceFlags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private static readonly Type SceneHierarchyWindowType =
            typeof(Editor).Assembly.GetType(
                "UnityEditor.SceneHierarchyWindow");
        private static readonly Type SceneHierarchyType =
            typeof(Editor).Assembly.GetType(
                "UnityEditor.SceneHierarchy");
        private static readonly Type TreeViewControllerType =
            typeof(Editor).Assembly.GetType(
                "UnityEditor.IMGUI.Controls.TreeViewController");
        private static readonly Type TreeViewItemType =
            typeof(Editor).Assembly.GetType(
                "UnityEditor.IMGUI.Controls.TreeViewItem");

        private static readonly FieldInfo SceneHierarchyField =
            SceneHierarchyWindowType?.GetField(
                "m_SceneHierarchy",
                InstanceFlags);
        private static readonly PropertyInfo TreeViewRectProperty =
            SceneHierarchyType?.GetProperty(
                "treeViewRect",
                InstanceFlags);
        private static readonly FieldInfo TreeViewField =
            SceneHierarchyType?.GetField(
                "m_TreeView",
                InstanceFlags);
        private static readonly MethodInfo FindItemMethod =
            TreeViewControllerType?.GetMethod(
                "FindItem",
                InstanceFlags,
                null,
                new[] { typeof(int) },
                null);
        private static readonly PropertyInfo IconProperty =
            TreeViewItemType?.GetProperty(
                "icon",
                InstanceFlags);
        private static readonly FieldInfo IconField =
            TreeViewItemType?.GetField(
                "m_Icon",
                InstanceFlags);

        public static bool IsItemIconSupported =>
            SceneHierarchyWindowType != null &&
            SceneHierarchyField != null &&
            TreeViewField != null &&
            FindItemMethod != null &&
            ((IconProperty != null &&
              IconProperty.CanWrite) ||
             IconField != null);

        public static bool TrySetItemIcon(
            int instanceId,
            Texture2D icon)
        {
            if (instanceId == 0 ||
                !IsItemIconSupported)
            {
                return false;
            }

            if (!TryGetOpenWindows(out var windows))
            {
                return false;
            }

            var updated = false;
            for (var i = 0; i < windows.Count; i++)
            {
                updated |= TrySetItemIcon(
                    windows[i],
                    instanceId,
                    icon);
            }

            return updated;
        }

        public static bool TryGetOpenWindows(
            out IReadOnlyList<EditorWindow> windows)
        {
            if (SceneHierarchyWindowType == null)
            {
                windows = Array.Empty<EditorWindow>();
                return false;
            }

            windows = Resources
                .FindObjectsOfTypeAll(
                    SceneHierarchyWindowType)
                .OfType<EditorWindow>()
                .ToArray();
            return true;
        }

        public static bool TryGetTreeViewRect(
            EditorWindow window,
            out Rect rect)
        {
            rect = default;
            if (window == null ||
                SceneHierarchyWindowType == null ||
                !SceneHierarchyWindowType.IsInstanceOfType(window) ||
                SceneHierarchyField == null ||
                TreeViewRectProperty == null)
            {
                return false;
            }

            try
            {
                var sceneHierarchy =
                    SceneHierarchyField.GetValue(window);
                if (sceneHierarchy == null ||
                    !(TreeViewRectProperty.GetValue(
                        sceneHierarchy) is Rect resolved))
                {
                    return false;
                }

                rect = resolved;
                return rect.width > 0f && rect.height > 0f;
            }
            catch (Exception)
            {
                rect = default;
                return false;
            }
        }

        private static bool TrySetItemIcon(
            EditorWindow window,
            int instanceId,
            Texture2D icon)
        {
            if (window == null)
            {
                return false;
            }

            try
            {
                var sceneHierarchy =
                    SceneHierarchyField.GetValue(window);
                if (sceneHierarchy == null)
                {
                    return false;
                }

                var treeView =
                    TreeViewField.GetValue(sceneHierarchy);
                if (treeView == null)
                {
                    return false;
                }

                var item = FindItemMethod.Invoke(
                    treeView,
                    new object[] { instanceId });
                if (item == null)
                {
                    return false;
                }

                if (IconProperty != null &&
                    IconProperty.CanWrite)
                {
                    IconProperty.SetValue(
                        item,
                        icon,
                        null);
                }
                else if (IconField != null)
                {
                    IconField.SetValue(item, icon);
                }
                else
                {
                    return false;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
