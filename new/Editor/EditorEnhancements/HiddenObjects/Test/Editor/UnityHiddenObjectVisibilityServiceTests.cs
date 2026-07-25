using System.Collections.Generic;
using Ee4v.Testing.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ee4v.HiddenObjects.Tests
{
    public sealed class UnityHiddenObjectVisibilityServiceTests
    {
        [Test]
        [FeatureTestCase(
            "非表示前のactive状態とtagを復元する",
            "非表示時に非アクティブ化とEditorOnly tagを適用し、再表示時に元の値へ戻すことを確認します。",
            order: 40)]
        public void HideAndReveal_RestoresActiveSelfAndTag()
        {
            var scene =
                EditorSceneManager.NewPreviewScene();
            try
            {
                var activeObject =
                    new GameObject("Active Object");
                SceneManager.MoveGameObjectToScene(
                    activeObject,
                    scene);
                activeObject.tag = "MainCamera";
                var inactiveObject =
                    new GameObject("Inactive Object");
                SceneManager.MoveGameObjectToScene(
                    inactiveObject,
                    scene);
                inactiveObject.tag = "Player";
                inactiveObject.SetActive(false);
                var store = new MemoryRestoreStateStore();
                var service =
                    new UnityHiddenObjectVisibilityService(
                        store);
                var instanceIds = new[]
                {
                    activeObject.GetInstanceID(),
                    inactiveObject.GetInstanceID()
                };

                Assert.That(
                    service.HideFromHierarchy(
                        instanceIds,
                        "Hide"),
                    Is.EqualTo(2));
                Assert.That(activeObject.activeSelf, Is.False);
                Assert.That(inactiveObject.activeSelf, Is.False);
                Assert.That(
                    activeObject.tag,
                    Is.EqualTo("EditorOnly"));
                Assert.That(
                    inactiveObject.tag,
                    Is.EqualTo("EditorOnly"));
                Assert.That(
                    activeObject.hideFlags &
                    HideFlags.HideInHierarchy,
                    Is.Not.EqualTo(HideFlags.None));
                Assert.That(store.SaveCount, Is.EqualTo(1));

                Assert.That(
                    service.RevealInHierarchy(
                        instanceIds,
                        "Reveal"),
                    Is.EqualTo(2));
                Assert.That(activeObject.activeSelf, Is.True);
                Assert.That(inactiveObject.activeSelf, Is.False);
                Assert.That(
                    activeObject.tag,
                    Is.EqualTo("MainCamera"));
                Assert.That(
                    inactiveObject.tag,
                    Is.EqualTo("Player"));
                Assert.That(
                    activeObject.hideFlags &
                    HideFlags.HideInHierarchy,
                    Is.EqualTo(HideFlags.None));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private sealed class MemoryRestoreStateStore
            : IHiddenObjectRestoreStateStore
        {
            private readonly Dictionary<
                string,
                HiddenObjectRestoreState> _states =
                    new Dictionary<
                        string,
                        HiddenObjectRestoreState>();

            public int SaveCount { get; private set; }

            public HiddenObjectRestoreState Get(
                string objectId)
            {
                return objectId != null &&
                    _states.TryGetValue(
                        objectId,
                        out var state)
                    ? state
                    : null;
            }

            public void Put(
                HiddenObjectRestoreState state)
            {
                _states[state.ObjectId] = state;
            }

            public void Save()
            {
                SaveCount++;
            }
        }
    }
}
