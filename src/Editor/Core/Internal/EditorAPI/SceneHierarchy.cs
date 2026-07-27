using Ee4v.Core.Internal.EditorAPI.Backends;
using UnityEngine;

namespace Ee4v.Core.Internal.EditorAPI
{
    internal static class SceneHierarchyItemIcon
    {
        internal static bool IsItemIconSupported
        {
            get
            {
                return SceneHierarchyBackend
                    .IsItemIconSupported;
            }
        }

        public static bool TrySetItemIcon(
            int instanceId,
            Texture2D icon)
        {
            return SceneHierarchyBackend.TrySetItemIcon(
                instanceId,
                icon);
        }
    }
}
