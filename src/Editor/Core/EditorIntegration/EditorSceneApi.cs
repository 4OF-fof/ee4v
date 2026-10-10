using System;
using System.Collections.Generic;
using Ee4v.Core.Internal.EditorAPI.Backends;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ee4v.Core.EditorIntegration
{
    public static class EditorSceneApi
    {
        private static readonly HashSet<int> PendingSaves = new HashSet<int>();

        /// <summary>Saves only the named Scene edited by the caller after the current UI operation.</summary>
        public static void SaveAfterEdit(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || !scene.isLoaded ||
                EditorSceneManager.IsPreviewScene(scene) || !scene.path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !PendingSaves.Add(scene.handle)) return;
            var path = scene.path;
            EditorApplication.CallbackFunction save = null;
            save = () =>
            {
                EditorApplication.update -= save;
                PendingSaves.Remove(scene.handle);
                if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || !scene.isLoaded ||
                    scene.path != path || !scene.isDirty) return;
                try
                {
                    Undo.FlushUndoRecordObjects();
                    if (!EditorSceneManager.SaveScene(scene))
                        throw new InvalidOperationException("The edited Scene could not be saved: " + path);
                }
                catch (Exception exception) { Debug.LogException(exception); }
            };
            EditorApplication.update += save;
            EditorApplication.QueuePlayerLoopUpdate();
        }

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
