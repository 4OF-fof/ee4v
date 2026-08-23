using System;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Ee4v.Core.Internal.EditorAPI.Backends
{
    internal static class EditorSceneBackend
    {
        private static readonly MethodInfo ClearSceneDirtinessMethod =
            typeof(EditorSceneManager).GetMethod(
                "ClearSceneDirtiness",
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static,
                null,
                new[] { typeof(Scene) },
                null);

        public static bool TryClearDirtiness(Scene scene)
        {
            if (!scene.IsValid() ||
                ClearSceneDirtinessMethod == null)
            {
                return false;
            }

            try
            {
                ClearSceneDirtinessMethod.Invoke(
                    null,
                    new object[] { scene });
                return !scene.isDirty;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
