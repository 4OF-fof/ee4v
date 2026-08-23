using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.EditorIntegration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ee4v.HiddenObjects
{
    internal sealed class UnityHiddenObjectVisibilityService
    {
        private const string HiddenTag = "EditorOnly";

        private readonly IHiddenObjectRestoreStateStore _restoreStates;
        private readonly Dictionary<string, HiddenObjectRestoreState>
            _sessionStates;

        public UnityHiddenObjectVisibilityService(
            IHiddenObjectRestoreStateStore restoreStates)
        {
            _restoreStates = restoreStates ??
                throw new ArgumentNullException(
                    nameof(restoreStates));
            _sessionStates = _restoreStates.GetAll()
                .ToDictionary(
                    state => state.ObjectId,
                    StringComparer.Ordinal);
        }

        public int Hide(
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
                var state = new HiddenObjectRestoreState(
                    GetObjectId(gameObject),
                    gameObject.activeSelf,
                    gameObject.tag);
                _sessionStates[state.ObjectId] = state;
                _restoreStates.Put(state);
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

        public int RestorePersistedVisibility()
        {
            var states = _restoreStates.GetAll();
            var dirtyScenes = new HashSet<int>();
            var initiallyCleanScenes = new Dictionary<int, Scene>();
            var restoredCount = 0;
            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (!TryResolveGameObject(
                        state.ObjectId,
                        out var gameObject))
                {
                    continue;
                }

                var isAlreadyHidden =
                    !gameObject.activeSelf &&
                    string.Equals(
                        gameObject.tag,
                        HiddenTag,
                        StringComparison.Ordinal) &&
                    (gameObject.hideFlags &
                     HideFlags.HideInHierarchy) != 0;
                if (isAlreadyHidden)
                {
                    continue;
                }

                var activeStateChanged = gameObject.activeSelf;
                var tagChanged = !string.Equals(
                    gameObject.tag,
                    HiddenTag,
                    StringComparison.Ordinal);
                if (activeStateChanged || tagChanged)
                {
                    var scene = gameObject.scene;
                    if (!scene.isDirty)
                    {
                        initiallyCleanScenes[scene.handle] = scene;
                    }

                    gameObject.SetActive(false);
                    gameObject.tag = HiddenTag;
                    MarkDirty(gameObject, dirtyScenes);
                }

                gameObject.hideFlags |=
                    HideFlags.HideInHierarchy;
                restoredCount++;
            }

            foreach (var scene in initiallyCleanScenes.Values)
            {
                EditorSceneApi.TryClearDirtiness(scene);
            }

            if (restoredCount > 0)
            {
                EditorApplication.RepaintHierarchyWindow();
            }

            return restoredCount;
        }

        public void SynchronizePersistedVisibility()
        {
            var states = _sessionStates.Values.ToArray();
            var changed = false;
            for (var i = 0; i < states.Length; i++)
            {
                var state = states[i];
                if (!TryResolveGameObject(
                        state.ObjectId,
                        out var gameObject))
                {
                    continue;
                }

                var isHidden =
                    (gameObject.hideFlags &
                     HideFlags.HideInHierarchy) != 0;
                var isPersisted =
                    _restoreStates.Get(state.ObjectId) != null;
                if (isPersisted == isHidden)
                {
                    continue;
                }

                if (isHidden)
                {
                    _restoreStates.Put(state);
                }
                else
                {
                    _restoreStates.Remove(state.ObjectId);
                }

                changed = true;
            }

            if (changed)
            {
                _restoreStates.Save();
            }
        }

        public int Reveal(
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
                var objectId = GetObjectId(gameObject);
                var state = _restoreStates.Get(objectId);
                if (state == null)
                {
                    _sessionStates.TryGetValue(
                        objectId,
                        out state);
                }

                gameObject.hideFlags &=
                    ~HideFlags.HideInHierarchy;
                if (state != null)
                {
                    RestoreTag(gameObject, state.Tag);
                    gameObject.SetActive(state.ActiveSelf);
                }

                MarkDirty(gameObject, dirtyScenes);
                _restoreStates.Remove(objectId);
            }

            _restoreStates.Save();
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

        private static bool TryResolveGameObject(
            string serializedObjectId,
            out GameObject gameObject)
        {
            gameObject = null;
            if (!GlobalObjectId.TryParse(
                    serializedObjectId,
                    out var objectId))
            {
                return false;
            }

            gameObject = GlobalObjectId
                .GlobalObjectIdentifierToObjectSlow(
                    objectId) as GameObject;
            return gameObject != null &&
                gameObject.scene.IsValid();
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
