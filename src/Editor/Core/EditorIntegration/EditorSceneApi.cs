using Ee4v.Core.Internal.EditorAPI.Backends;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ee4v.Core.EditorIntegration
{
    public static class EditorSceneApi
    {
        public static bool TryCreateEmptySceneAsset(string scenePath)
        {
            return EditorSceneBackend.TryCreateEmptySceneAsset(scenePath);
        }

        public static bool TryClearDirtiness(Scene scene)
        {
            return EditorSceneBackend.TryClearDirtiness(scene);
        }

        public static void HidePreviewHierarchy(Transform root)
        {
            root.gameObject.hideFlags = HideFlags.HideAndDontSave;
            for (var index = 0; index < root.childCount; index++)
            {
                HidePreviewHierarchy(root.GetChild(index));
            }
        }
    }
}
