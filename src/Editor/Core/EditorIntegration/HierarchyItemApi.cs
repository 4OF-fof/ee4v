using Ee4v.Core.Internal.EditorAPI.Backends;
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
    }
}
