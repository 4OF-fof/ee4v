using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ee4v.HiddenObjects
{
    internal sealed class UnityHiddenObjectRepository
        : IHiddenObjectRepository
    {
        public IReadOnlyList<HiddenObjectSnapshotItem> Load()
        {
            var items = new List<HiddenObjectSnapshotItem>();
            var order = 0;

            for (var sceneIndex = 0;
                 sceneIndex < SceneManager.sceneCount;
                 sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                var sceneName = string.IsNullOrWhiteSpace(scene.name)
                    ? scene.path
                    : scene.name;
                var roots = scene.GetRootGameObjects();
                for (var rootIndex = 0;
                     rootIndex < roots.Length;
                     rootIndex++)
                {
                    Collect(
                        roots[rootIndex],
                        0,
                        scene.handle,
                        sceneName,
                        items,
                        ref order);
                }
            }

            return items;
        }

        public int Reveal(
            IReadOnlyCollection<int> instanceIds,
            string undoOperationName)
        {
            if (instanceIds == null || instanceIds.Count == 0)
            {
                return 0;
            }

            var objects = instanceIds
                .Select(EditorUtility.InstanceIDToObject)
                .OfType<GameObject>()
                .Where(gameObject =>
                    (gameObject.hideFlags &
                     HideFlags.HideInHierarchy) != 0)
                .Distinct()
                .ToArray();
            if (objects.Length == 0)
            {
                return 0;
            }

            Undo.RecordObjects(
                objects.Cast<Object>().ToArray(),
                undoOperationName ?? string.Empty);

            var dirtyScenes = new HashSet<int>();
            for (var i = 0; i < objects.Length; i++)
            {
                var gameObject = objects[i];
                gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
                EditorUtility.SetDirty(gameObject);
                if (gameObject.scene.IsValid() &&
                    dirtyScenes.Add(gameObject.scene.handle))
                {
                    EditorSceneManager.MarkSceneDirty(gameObject.scene);
                }
            }

            EditorApplication.RepaintHierarchyWindow();
            return objects.Length;
        }

        private static void Collect(
            GameObject gameObject,
            int parentInstanceId,
            int sceneHandle,
            string sceneName,
            ICollection<HiddenObjectSnapshotItem> items,
            ref int order)
        {
            if (gameObject == null)
            {
                return;
            }

            var instanceId = gameObject.GetInstanceID();
            items.Add(new HiddenObjectSnapshotItem(
                instanceId,
                parentInstanceId,
                sceneHandle,
                sceneName,
                gameObject.name,
                (gameObject.hideFlags &
                 HideFlags.HideInHierarchy) != 0,
                order++));

            var transform = gameObject.transform;
            for (var childIndex = 0;
                 childIndex < transform.childCount;
                 childIndex++)
            {
                Collect(
                    transform.GetChild(childIndex).gameObject,
                    instanceId,
                    sceneHandle,
                    sceneName,
                    items,
                    ref order);
            }
        }
    }
}
