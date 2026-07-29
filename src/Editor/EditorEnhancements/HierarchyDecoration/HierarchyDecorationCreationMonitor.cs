using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ee4v.HierarchyDecoration
{
    internal static class HierarchyDecorationCreationMonitor
    {
        public static void HandleChanges(
            ref ObjectChangeEventStream stream)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            for (var i = 0; i < stream.length; i++)
            {
                if (stream.GetEventType(i) !=
                    ObjectChangeKind.CreateGameObjectHierarchy)
                {
                    continue;
                }

                stream.GetCreateGameObjectHierarchyEvent(
                    i,
                    out var change);
                var gameObject =
                    EditorUtility.InstanceIDToObject(
                        change.instanceId) as GameObject;
                TryNormalizeCreatedObject(gameObject);
            }
        }

        internal static bool TryNormalizeCreatedObject(
            GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            var componentCount =
                gameObject.GetComponents<Component>().Length;
            if (!HierarchyDecorationRules
                    .TryNormalizeSeparatorName(
                        gameObject.name,
                        componentCount,
                        gameObject.transform.childCount,
                        out var normalizedName))
            {
                return false;
            }

            gameObject.name = normalizedName;
            if (gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(
                    gameObject.scene);
            }

            return true;
        }
    }
}
