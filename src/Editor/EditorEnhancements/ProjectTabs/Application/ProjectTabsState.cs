using System;
using System.Collections.Generic;
using System.Linq;

namespace Ee4v.ProjectTabs
{
    internal sealed class ProjectTabLocation : IEquatable<ProjectTabLocation>
    {
        public ProjectTabLocation(
            string folderGuid,
            string folderPath,
            string searchText = "")
        {
            FolderGuid = folderGuid ?? string.Empty;
            FolderPath = NormalizePath(folderPath);
            SearchText = searchText ?? string.Empty;
        }

        public string FolderGuid { get; }

        public string FolderPath { get; }

        public string SearchText { get; }

        public string DisplayName
        {
            get
            {
                if (string.IsNullOrEmpty(FolderPath))
                {
                    return string.Empty;
                }

                var separatorIndex = FolderPath.LastIndexOf('/');
                return separatorIndex < 0
                    ? FolderPath
                    : FolderPath.Substring(separatorIndex + 1);
            }
        }

        public bool Equals(ProjectTabLocation other)
        {
            return other != null &&
                string.Equals(
                    FolderGuid,
                    other.FolderGuid,
                    StringComparison.Ordinal) &&
                string.Equals(
                    FolderPath,
                    other.FolderPath,
                    StringComparison.Ordinal) &&
                string.Equals(
                    SearchText,
                    other.SearchText,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ProjectTabLocation);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = FolderGuid.GetHashCode();
                hashCode = (hashCode * 397) ^ FolderPath.GetHashCode();
                hashCode = (hashCode * 397) ^ SearchText.GetHashCode();
                return hashCode;
            }
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Replace('\\', '/').TrimEnd('/');
        }
    }

    internal sealed class ProjectTabState
    {
        public ProjectTabState(
            string id,
            IReadOnlyList<ProjectTabLocation> history,
            int historyIndex,
            bool isPinned = false,
            bool isHome = false)
        {
            Id = id ?? string.Empty;
            History = history ?? Array.Empty<ProjectTabLocation>();
            HistoryIndex = historyIndex;
            IsPinned = isPinned || isHome;
            IsHome = isHome;
        }

        public string Id { get; }

        public IReadOnlyList<ProjectTabLocation> History { get; }

        public int HistoryIndex { get; }

        public bool IsPinned { get; }

        public bool IsHome { get; }

        public ProjectTabLocation CurrentLocation
        {
            get
            {
                return HistoryIndex >= 0 && HistoryIndex < History.Count
                    ? History[HistoryIndex]
                    : null;
            }
        }

        public bool CanGoBack
        {
            get { return HistoryIndex > 0; }
        }

        public bool CanGoForward
        {
            get { return HistoryIndex >= 0 && HistoryIndex < History.Count - 1; }
        }
    }

    internal sealed class ProjectTabsState
    {
        public ProjectTabsState(IReadOnlyList<ProjectTabState> tabs)
        {
            Tabs = tabs ?? Array.Empty<ProjectTabState>();
        }

        public IReadOnlyList<ProjectTabState> Tabs { get; }

        public ProjectTabState Find(string tabId)
        {
            return Tabs.FirstOrDefault(tab =>
                string.Equals(tab.Id, tabId, StringComparison.Ordinal));
        }

        public ProjectTabState FindByCurrentLocation(
            ProjectTabLocation location)
        {
            if (location == null)
            {
                return null;
            }

            return Tabs.FirstOrDefault(tab =>
                location.Equals(tab?.CurrentLocation));
        }
    }

    internal interface IProjectTabsStateStore
    {
        ProjectTabsState Load();

        void Save(ProjectTabsState state);
    }
}
