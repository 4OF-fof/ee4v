using Ee4v.Core.Internal.EditorAPI.Backends;
using UnityEngine.SceneManagement;

namespace Ee4v.Core.EditorIntegration
{
    public static class EditorSceneApi
    {
        public static bool TryClearDirtiness(Scene scene)
        {
            return EditorSceneBackend.TryClearDirtiness(scene);
        }
    }
}
