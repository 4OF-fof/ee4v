using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Ee4v.WindowGroup.Tests
{
    public sealed class WindowGroupTests
    {
        [Test]
        public void EnteringGroup_FocusesPeersAndRestoresOriginalFocus()
        {
            var registry = new WindowGroupRegistry<object>();
            var focusedWindow = new object();
            var peerWindow = new object();
            var outsideWindow = new object();
            registry.Register(focusedWindow, "tools");
            registry.Register(peerWindow, "tools");
            var focused = new List<object>();
            var coordinator = new WindowGroupCoordinator<object>(
                registry,
                focused.Add);

            coordinator.ProcessFocus(outsideWindow);
            coordinator.ProcessFocus(focusedWindow);

            Assert.That(
                focused,
                Is.EqualTo(new[] { peerWindow, focusedWindow }));

            coordinator.ProcessFocus(peerWindow);
            Assert.That(
                focused,
                Has.Count.EqualTo(2),
                "Focus changes caused by a group must not cascade.");
        }

        [Test]
        public void FollowerRole_IsEvaluatedPerGroup()
        {
            var registry = new WindowGroupRegistry<object>();
            var sharedWindow = new object();
            var firstPeer = new object();
            var secondLeader = new object();
            var outsideWindow = new object();
            registry.Register(firstPeer, "first");
            registry.Register(secondLeader, "second");
            registry.SetManaged(
                sharedWindow,
                new[]
                {
                    new WindowGroupMembership("first", false),
                    new WindowGroupMembership("second", true)
                });
            var focused = new List<object>();
            var coordinator = new WindowGroupCoordinator<object>(
                registry,
                focused.Add);

            coordinator.ProcessFocus(outsideWindow);
            coordinator.ProcessFocus(sharedWindow);

            Assert.That(
                focused,
                Is.EqualTo(new[] { firstPeer, sharedWindow }));

            coordinator.ProcessFocus(outsideWindow);
            coordinator.ProcessFocus(secondLeader);

            Assert.That(
                focused,
                Is.EqualTo(new[]
                {
                    firstPeer,
                    sharedWindow,
                    sharedWindow,
                    secondLeader
                }));
        }

        [Test]
        public void RegisteringWindowAsRegularInMultipleGroups_IsRejected()
        {
            var registry = new WindowGroupRegistry<object>();
            var window = new object();
            registry.Register(window, "first");

            Assert.Throws<System.InvalidOperationException>(() =>
                registry.Register(window, "second"));
        }

        [Test]
        public void ReplacingSameGroupRegistration_IgnoresOldDisposal()
        {
            var registry = new WindowGroupRegistry<object>();
            var window = new object();
            var peer = new object();
            var outsideWindow = new object();
            var oldRegistration = registry.Register(window, "tools");
            registry.Register(window, "tools");
            registry.Register(peer, "tools");

            oldRegistration.Dispose();

            var focused = new List<object>();
            var coordinator = new WindowGroupCoordinator<object>(
                registry,
                focused.Add);
            coordinator.ProcessFocus(outsideWindow);
            coordinator.ProcessFocus(window);

            Assert.That(
                focused,
                Is.EqualTo(new[] { peer, window }));
        }

        [Test]
        public void AdditionalMembership_BecomesFollowerAndPersists()
        {
            var store = new MemoryWindowGroupStore();
            var configuration = new WindowGroupConfiguration(store);
            var firstGroupId = configuration.CreateGroup("First");
            var secondGroupId = configuration.CreateGroup("Second");
            const string windowTypeId =
                "UnityEditor.ConsoleWindow, UnityEditor.CoreModule";
            configuration.SetWindowTypeAssigned(
                windowTypeId,
                firstGroupId,
                true);
            configuration.SetWindowTypeAssigned(
                windowTypeId,
                secondGroupId,
                true);

            Assert.That(
                configuration.SetFollower(
                    windowTypeId,
                    secondGroupId,
                    false),
                Is.False);

            var reloaded = new WindowGroupConfiguration(store);
            Assert.That(
                reloaded.IsAssigned(windowTypeId, firstGroupId),
                Is.True);
            Assert.That(
                reloaded.IsAssigned(windowTypeId, secondGroupId),
                Is.True);
            Assert.That(
                reloaded.IsFollower(windowTypeId, firstGroupId),
                Is.False);
            Assert.That(
                reloaded.IsFollower(windowTypeId, secondGroupId),
                Is.True);
        }

        private sealed class MemoryWindowGroupStore
            : IWindowGroupStore
        {
            private string _json;

            public WindowGroupDocument Load()
            {
                return string.IsNullOrEmpty(_json)
                    ? new WindowGroupDocument()
                    : JsonUtility.FromJson<WindowGroupDocument>(
                        _json);
            }

            public void Save(WindowGroupDocument document)
            {
                _json = JsonUtility.ToJson(document);
            }
        }
    }
}
