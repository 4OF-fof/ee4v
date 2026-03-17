using Ee4v.Core.Internal.EditorAPI.Backends;
using UnityEngine;

namespace Ee4v.Core.Internal.EditorAPI
{
    public enum ProjectBrowserViewMode
    {
        Unknown,
        OneColumn,
        TwoColumns
    }

    public enum ProjectBrowserOrientation
    {
        Unknown,
        Horizontal,
        Vertical
    }

    public sealed class ProjectBrowserSnapshot
    {
        internal ProjectBrowserSnapshot(
            string folderGuid,
            string folderPath,
            string searchText,
            bool hasSearch,
            ProjectBrowserViewMode viewMode,
            ProjectBrowserOrientation orientation)
        {
            FolderGuid = folderGuid;
            FolderPath = folderPath;
            SearchText = searchText;
            HasSearch = hasSearch;
            ViewMode = viewMode;
            Orientation = orientation;
        }

        public string FolderGuid { get; }

        public string FolderPath { get; }

        public string SearchText { get; }

        public bool HasSearch { get; }

        public ProjectBrowserViewMode ViewMode { get; }

        public ProjectBrowserOrientation Orientation { get; }
    }

    internal static class ProjectBrowser
    {
        public static bool TryGetSnapshot(out ProjectBrowserSnapshot snapshot)
        {
            return ProjectBrowserBackend.TryGetSnapshot(null, out snapshot);
        }

        public static bool TryShowFolder(string folderGuid, bool reveal = false)
        {
            return ProjectBrowserBackend.TryShowFolder(folderGuid, reveal);
        }

        public static bool TrySetSearch(string searchText)
        {
            return ProjectBrowserBackend.TrySetSearch(searchText);
        }

        public static bool TryClearSearch()
        {
            return ProjectBrowserBackend.TryClearSearch();
        }

        internal static bool TryGetSnapshot(Rect selectionRect, out ProjectBrowserSnapshot snapshot)
        {
            return ProjectBrowserBackend.TryGetSnapshot(selectionRect, out snapshot);
        }
    }
}
