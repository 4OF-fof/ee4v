using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ee4v.HierarchyStyle
{
    internal sealed class UnityHierarchyObjectVisibility
    {
        public int Hide(
            IReadOnlyList<GameObject> targets,
            string undoOperationName)
        {
            if (targets == null || targets.Count == 0)
            {
                return 0;
            }

            var objects = targets
                .Where(gameObject =>
                    gameObject != null &&
                    gameObject.scene.IsValid() &&
                    (gameObject.hideFlags &
                     HideFlags.HideInHierarchy) == 0)
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
                gameObject.hideFlags |=
                    HideFlags.HideInHierarchy;
                EditorUtility.SetDirty(gameObject);
                if (dirtyScenes.Add(
                        gameObject.scene.handle))
                {
                    EditorSceneManager.MarkSceneDirty(
                        gameObject.scene);
                }
            }

            EditorApplication.RepaintHierarchyWindow();
            return objects.Length;
        }
    }
}
