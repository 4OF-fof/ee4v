using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _4OF.ee4v.HierarchyExtension.Service {
    [InitializeOnLoad]
    public static class HiddenObjectRestorer {
        static HiddenObjectRestorer() {
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.delayCall += RestoreHiddenObjectsInCurrentScenes;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode) {
            RestoreHiddenObjectsInScene(scene);
        }

        private static void RestoreHiddenObjectsInCurrentScenes() {
            for (var i = 0; i < SceneManager.sceneCount; i++) {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded) RestoreHiddenObjectsInScene(scene);
            }
        }

        private static void RestoreHiddenObjectsInScene(Scene scene) {
            if (!scene.isLoaded) return;

            var rootObjects = scene.GetRootGameObjects();
            foreach (var rootObj in rootObjects) RestoreHiddenFlags(rootObj);
        }

        private static void RestoreHiddenFlags(GameObject obj) {
            if (obj == null) return;

            if (obj.CompareTag("EditorOnly") && !obj.activeSelf) obj.hideFlags |= HideFlags.HideInHierarchy;

            var transform = obj.transform;
            for (var i = 0; i < transform.childCount; i++) RestoreHiddenFlags(transform.GetChild(i).gameObject);
        }
    }
}