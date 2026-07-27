using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.Hierarchy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ee4v.HiddenObjects
{
    internal sealed class UnityHiddenObjectVisibilityService
        : IHierarchyObjectVisibilityService
    {
        private const string HiddenTag = "EditorOnly";

        private readonly IHiddenObjectRestoreStateStore _restoreStates;

        public UnityHiddenObjectVisibilityService(
            IHiddenObjectRestoreStateStore restoreStates)
        {
            _restoreStates = restoreStates ??
                throw new ArgumentNullException(
                    nameof(restoreStates));
        }

        public int HideFromHierarchy(
            IReadOnlyCollection<int> instanceIds,
            string undoOperationName)
        {
            var objects = ResolveGameObjects(instanceIds)
                .Where(gameObject =>
                    gameObject.scene.IsValid() &&
                    (gameObject.hideFlags &
                     HideFlags.HideInHierarchy) == 0)
                .ToArray();
            if (objects.Length == 0)
            {
                return 0;
            }

            for (var i = 0; i < objects.Length; i++)
            {
                var gameObject = objects[i];
                _restoreStates.Put(
                    new HiddenObjectRestoreState(
                        GetObjectId(gameObject),
                        gameObject.activeSelf,
                        gameObject.tag));
            }

            _restoreStates.Save();
            Undo.RecordObjects(
                objects.Cast<UnityEngine.Object>().ToArray(),
                undoOperationName ?? string.Empty);

            var dirtyScenes = new HashSet<int>();
            for (var i = 0; i < objects.Length; i++)
            {
                var gameObject = objects[i];
                gameObject.SetActive(false);
                gameObject.tag = HiddenTag;
                gameObject.hideFlags |=
                    HideFlags.HideInHierarchy;
                MarkDirty(gameObject, dirtyScenes);
            }

            EditorApplication.RepaintHierarchyWindow();
            return objects.Length;
        }

        public int RevealInHierarchy(
            IReadOnlyCollection<int> instanceIds,
            string undoOperationName)
        {
            var objects = ResolveGameObjects(instanceIds)
                .Where(gameObject =>
                    (gameObject.hideFlags &
                     HideFlags.HideInHierarchy) != 0)
                .ToArray();
            if (objects.Length == 0)
            {
                return 0;
            }

            Undo.RecordObjects(
                objects.Cast<UnityEngine.Object>().ToArray(),
                undoOperationName ?? string.Empty);

            var dirtyScenes = new HashSet<int>();
            for (var i = 0; i < objects.Length; i++)
            {
                var gameObject = objects[i];
                var state = _restoreStates.Get(
                    GetObjectId(gameObject));
                gameObject.hideFlags &=
                    ~HideFlags.HideInHierarchy;
                if (state != null)
                {
                    RestoreTag(gameObject, state.Tag);
                    gameObject.SetActive(state.ActiveSelf);
                }

                MarkDirty(gameObject, dirtyScenes);
            }

            EditorApplication.RepaintHierarchyWindow();
            return objects.Length;
        }

        private static IEnumerable<GameObject>
            ResolveGameObjects(
                IReadOnlyCollection<int> instanceIds)
        {
            if (instanceIds == null ||
                instanceIds.Count == 0)
            {
                return Enumerable.Empty<GameObject>();
            }

            return instanceIds
                .Select(EditorUtility.InstanceIDToObject)
                .OfType<GameObject>()
                .Distinct();
        }

        private static string GetObjectId(
            GameObject gameObject)
        {
            var objectId = GlobalObjectId
                .GetGlobalObjectIdSlow(gameObject);
            return objectId.identifierType != 0 &&
                objectId.targetObjectId != 0
                    ? objectId.ToString()
                    : "instance:" +
                      gameObject.GetInstanceID();
        }

        private static void RestoreTag(
            GameObject gameObject,
            string tag)
        {
            try
            {
                gameObject.tag = string.IsNullOrEmpty(tag)
                    ? "Untagged"
                    : tag;
            }
            catch (UnityException)
            {
                gameObject.tag = "Untagged";
            }
        }

        private static void MarkDirty(
            GameObject gameObject,
            ISet<int> dirtyScenes)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(
                gameObject);
            EditorUtility.SetDirty(gameObject);
            if (gameObject.scene.IsValid() &&
                !EditorSceneManager.IsPreviewScene(
                    gameObject.scene) &&
                dirtyScenes.Add(gameObject.scene.handle))
            {
                EditorSceneManager.MarkSceneDirty(
                    gameObject.scene);
            }
        }
    }
}
