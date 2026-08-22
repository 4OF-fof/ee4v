using System.Collections.Generic;
using Ee4v.HiddenObjects;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ee4v.HierarchyStyle.Tests
{
    public sealed class HierarchyStyleTests
    {
        private const string PersistenceScenePath =
            "Assets/__Ee4vHierarchyStylePersistenceTest.unity";

        [Test]
        public void VisibilityApi_RestoresActiveStateAndTag()
        {
            var gameObject = new GameObject("VisibilityApiTarget");
            try
            {
                var instanceIds = new[]
                {
                    gameObject.GetInstanceID()
                };

                Assert.That(
                    HierarchyStyleApi.Hide(
                        instanceIds,
                        "Hide test object"),
                    Is.EqualTo(1));
                Assert.That(gameObject.activeSelf, Is.False);
                Assert.That(gameObject.tag, Is.EqualTo("EditorOnly"));
                Assert.That(
                    gameObject.hideFlags & HideFlags.HideInHierarchy,
                    Is.EqualTo(HideFlags.HideInHierarchy));

                Assert.That(
                    HierarchyStyleApi.Reveal(
                        instanceIds,
                        "Reveal test object"),
                    Is.EqualTo(1));
                Assert.That(gameObject.activeSelf, Is.True);
                Assert.That(gameObject.tag, Is.EqualTo("Untagged"));
                Assert.That(
                    gameObject.hideFlags & HideFlags.HideInHierarchy,
                    Is.EqualTo(HideFlags.None));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void VisibilityService_RestoresPersistedHiddenState()
        {
            AssetDatabase.DeleteAsset(PersistenceScenePath);
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            try
            {
                Assert.That(
                    EditorSceneManager.SaveScene(
                        scene,
                        PersistenceScenePath),
                    Is.True);
                var gameObject = new GameObject(
                    "PersistedVisibilityTarget");
                SceneManager.MoveGameObjectToScene(
                    gameObject,
                    scene);
                Assert.That(
                    EditorSceneManager.SaveScene(scene),
                    Is.True);

                var objectId = GlobalObjectId
                    .GetGlobalObjectIdSlow(gameObject)
                    .ToString();
                var store = new FakeRestoreStateStore(
                    new HiddenObjectRestoreState(
                        objectId,
                        true,
                        "Untagged"));
                var service =
                    new UnityHiddenObjectVisibilityService(store);

                Assert.That(
                    service.RestorePersistedVisibility(),
                    Is.EqualTo(1));
                Assert.That(gameObject.activeSelf, Is.False);
                Assert.That(gameObject.tag, Is.EqualTo("EditorOnly"));
                Assert.That(
                    gameObject.hideFlags & HideFlags.HideInHierarchy,
                    Is.EqualTo(HideFlags.HideInHierarchy));
            }
            finally
            {
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
                AssetDatabase.DeleteAsset(PersistenceScenePath);
            }
        }

        private sealed class FakeRestoreStateStore
            : IHiddenObjectRestoreStateStore
        {
            private readonly List<HiddenObjectRestoreState> _states;

            public FakeRestoreStateStore(
                HiddenObjectRestoreState state)
            {
                _states = new List<HiddenObjectRestoreState>
                {
                    state
                };
            }

            public HiddenObjectRestoreState Get(string objectId)
            {
                return _states.Find(state =>
                    state.ObjectId == objectId);
            }

            public IReadOnlyList<HiddenObjectRestoreState> GetAll()
            {
                return _states;
            }

            public void Put(HiddenObjectRestoreState state)
            {
                for (var i = 0; i < _states.Count; i++)
                {
                    if (_states[i].ObjectId != state.ObjectId)
                    {
                        continue;
                    }

                    _states[i] = state;
                    return;
                }

                _states.Add(state);
            }

            public void Remove(string objectId)
            {
                _states.RemoveAll(state =>
                    state.ObjectId == objectId);
            }

            public void Save()
            {
            }
        }

    }
}
