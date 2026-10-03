using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Ee4v.Core.Internal.EditorAPI.Backends
{
    internal static class EditorSceneBackend
    {
        private static readonly MethodInfo CreateSceneAssetMethod =
            typeof(EditorSceneManager).GetMethod(
                "CreateSceneAsset",
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(bool) },
                null);

        private static readonly MethodInfo ClearSceneDirtinessMethod =
            typeof(EditorSceneManager).GetMethod(
                "ClearSceneDirtiness",
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static,
                null,
                new[] { typeof(Scene) },
                null);

        public static bool TryCreateEmptySceneAsset(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath) ||
                !scenePath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                scenePath.Contains("\\") ||
                Array.Exists(scenePath.Split('/'), part => part == "." || part == "..") ||
                File.Exists(scenePath) ||
                CreateSceneAssetMethod == null)
            {
                return false;
            }

            try
            {
                if (!(bool)CreateSceneAssetMethod.Invoke(
                        null, new object[] { scenePath, false }))
                {
                    return false;
                }
                AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceSynchronousImport);
                return AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

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
