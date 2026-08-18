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
    }
}
