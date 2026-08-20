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
        public void ReplacingRegistration_IgnoresDisposalOfOldRegistration()
        {
            var registry = new WindowGroupRegistry<object>();
            var window = new object();
            var oldRegistration = registry.Register(window, "old");
            registry.Register(window, "new");

            oldRegistration.Dispose();

            Assert.That(
                registry.TryGetGroupId(window, out var groupId),
                Is.True);
            Assert.That(groupId, Is.EqualTo("new"));
        }

        [Test]
        public void AssigningWindowType_MovesItBetweenGroupsAndPersists()
        {
            var store = new MemoryWindowGroupStore();
            var configuration = new WindowGroupConfiguration(store);
            var firstGroupId = configuration.CreateGroup("First");
            var secondGroupId = configuration.CreateGroup("Second");
            const string windowTypeId =
                "UnityEditor.ConsoleWindow, UnityEditor.CoreModule";
            configuration.AssignWindowType(
                windowTypeId,
                firstGroupId);

            configuration.AssignWindowType(
                windowTypeId,
                secondGroupId);

            Assert.That(
                configuration.GetGroup(firstGroupId).WindowTypeIds,
                Does.Not.Contain(windowTypeId));
            Assert.That(
                configuration.GetGroup(secondGroupId).WindowTypeIds,
                Is.EqualTo(new[] { windowTypeId }));
            var reloaded = new WindowGroupConfiguration(store);
            Assert.That(
                reloaded.GetGroupId(windowTypeId),
                Is.EqualTo(secondGroupId));
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
