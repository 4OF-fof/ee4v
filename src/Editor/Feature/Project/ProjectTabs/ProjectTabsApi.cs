using System;
using System.Collections.Generic;

namespace Ee4v.ProjectTabs
{
    public static class ProjectTabsApi
    {
        public static event Action Changed
        {
            add
            {
                ProjectTabsBootstrap.EnsureInitialized();
                ProjectTabsBootstrap.Changed += value;
            }
            remove { ProjectTabsBootstrap.Changed -= value; }
        }

        public static ProjectTabsState State
        {
            get { return ProjectTabsBootstrap.Session.State; }
        }

        public static string Add(ProjectTabLocation location)
        {
            return ProjectTabsBootstrap.Session.Add(location);
        }

        public static IReadOnlyList<string> AddRange(
            IEnumerable<ProjectTabLocation> locations)
        {
            return ProjectTabsBootstrap.Session.AddRange(locations);
        }

        public static bool Move(string tabId, int targetIndex)
        {
            return ProjectTabsBootstrap.Session.Move(
                tabId,
                targetIndex);
        }

        public static bool Remove(string tabId)
        {
            return ProjectTabsBootstrap.Session.Remove(tabId);
        }

        public static bool SetPinned(string tabId, bool isPinned)
        {
            return ProjectTabsBootstrap.Session.SetPinned(
                tabId,
                isPinned);
        }

        public static ProjectTabLocation GoBack(
            string tabId,
            int steps = 1)
        {
            return ProjectTabsBootstrap.Session.GoBack(tabId, steps);
        }

        public static ProjectTabLocation GoForward(
            string tabId,
            int steps = 1)
        {
            return ProjectTabsBootstrap.Session.GoForward(tabId, steps);
        }
    }
}
