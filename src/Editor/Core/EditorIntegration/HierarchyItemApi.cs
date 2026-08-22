using System.Collections.Generic;
using Ee4v.Core.Internal.EditorAPI.Backends;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Core.EditorIntegration
{
    public static class HierarchyItemApi
    {
        public static bool IsIconSupported =>
            SceneHierarchyBackend.IsItemIconSupported;

        public static bool TrySetIcon(
            int instanceId,
            Texture2D icon)
        {
            return SceneHierarchyBackend.TrySetItemIcon(
                instanceId,
                icon);
        }

        public static bool TryGetOpenWindows(
            out IReadOnlyList<EditorWindow> windows)
        {
            return SceneHierarchyBackend.TryGetOpenWindows(
                out windows);
        }

        public static bool TryGetTreeViewRect(
            EditorWindow window,
            out Rect rect)
        {
            return SceneHierarchyBackend.TryGetTreeViewRect(
                window,
                out rect);
        }
    }
}
