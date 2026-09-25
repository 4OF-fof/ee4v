using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Ee4v.ProjectTabs.Tests
{
    public sealed class ProjectTabsTests
    {
        [Test]
        public void PinnedTab_RejectsNavigationToAnotherFolder()
        {
            var defaultLocation = new ProjectTabLocation(
                "assets-guid",
                "Assets");
            var session = new ProjectTabsSession(
                new MemoryProjectTabsStore(),
                defaultLocation,
                new MemoryProjectFavoriteFolderStore(),
                () => "tab");
            var tabId = session.State.Tabs[0].Id;
            session.SetPinned(tabId, true);
            var other = new ProjectTabLocation(
                "other-guid",
                "Assets/Other");

            Assert.That(
                session.ShouldOpenInNewTab(tabId, other),
                Is.True);
            Assert.That(session.RecordNavigation(tabId, other), Is.False);
            Assert.That(
                session.State.Find(tabId).CurrentLocation,
                Is.EqualTo(defaultLocation));
        }

        private sealed class MemoryProjectTabsStore
            : IProjectTabsStateStore
        {
            private ProjectTabsState _state;

            public ProjectTabsState Load()
            {
                return _state;
            }

            public void Save(ProjectTabsState state)
            {
                _state = state;
            }
        }

        private sealed class MemoryProjectFavoriteFolderStore
            : IProjectFavoriteFolderStore
        {
            private readonly List<ProjectTabLocation> _locations =
                new List<ProjectTabLocation>();

            public event Action Changed;

            public bool TryGetAll(
                out IReadOnlyList<ProjectTabLocation> locations)
            {
                locations = _locations.ToArray();
                return true;
            }

            public bool TryAdd(ProjectTabLocation location)
            {
                _locations.Add(location);
                Changed?.Invoke();
                return true;
            }

            public bool TryRemove(ProjectTabLocation location)
            {
                _locations.RemoveAll(existing =>
                    string.Equals(
                        existing.FolderPath,
                        location.FolderPath,
                        StringComparison.Ordinal));
                Changed?.Invoke();
                return true;
            }
        }
    }
}
