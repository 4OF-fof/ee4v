using System.Collections.Generic;
using Ee4v.Core.Internal.EditorAPI.Backends;
using UnityEditor;
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
            return ProjectBrowserBackend.TryGetSnapshot(null, null, out snapshot);
        }

        internal static bool TryGetSnapshot(
            EditorWindow window,
            out ProjectBrowserSnapshot snapshot)
        {
            return ProjectBrowserBackend.TryGetSnapshot(
                window,
                null,
                out snapshot);
        }

        internal static bool TryShowFolder(
            EditorWindow window,
            string folderGuid,
            bool reveal = false)
        {
            return ProjectBrowserBackend.TryShowFolder(
                window,
                folderGuid,
                reveal);
        }

        internal static bool TrySetSearch(
            EditorWindow window,
            string searchText)
        {
            return ProjectBrowserBackend.TrySetSearch(window, searchText);
        }

        internal static bool TryClearSearch(EditorWindow window)
        {
            return ProjectBrowserBackend.TryClearSearch(window);
        }

        internal static bool TryGetSnapshot(Rect selectionRect, out ProjectBrowserSnapshot snapshot)
        {
            return ProjectBrowserBackend.TryGetSnapshot(
                null,
                selectionRect,
                out snapshot);
        }

        internal static bool TryGetOpenWindows(
            out IReadOnlyList<EditorWindow> windows)
        {
            return ProjectBrowserBackend.TryGetOpenWindows(out windows);
        }
    }
}
