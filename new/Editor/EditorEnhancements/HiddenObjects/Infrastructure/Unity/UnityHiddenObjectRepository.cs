using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ee4v.HiddenObjects
{
    internal sealed class UnityHiddenObjectRepository
        : IHiddenObjectRepository
    {
        private readonly UnityHiddenObjectVisibilityService _visibility;

        public UnityHiddenObjectRepository(
            UnityHiddenObjectVisibilityService visibility)
        {
            _visibility = visibility;
        }

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
            return _visibility.RevealInHierarchy(
                instanceIds,
                undoOperationName);
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
