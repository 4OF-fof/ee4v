using System.Collections.Generic;
using Ee4v.Core.Internal.EditorAPI.Backends;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Core.EditorIntegration
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

    public sealed class ProjectBrowserState
    {
        internal ProjectBrowserState(
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

    public static class ProjectBrowserApi
    {
        public static bool TryGetState(out ProjectBrowserState state)
        {
            return ProjectBrowserBackend.TryGetState(
                null,
                null,
                out state);
        }

        public static bool TryGetState(
            EditorWindow window,
            out ProjectBrowserState state)
        {
            return ProjectBrowserBackend.TryGetState(
                window,
                null,
                out state);
        }

        public static bool TryGetState(
            Rect selectionRect,
            out ProjectBrowserState state)
        {
            return ProjectBrowserBackend.TryGetState(
                null,
                selectionRect,
                out state);
        }

        public static bool TryShowFolder(
            EditorWindow window,
            string folderGuid,
            bool reveal = false)
        {
            return ProjectBrowserBackend.TryShowFolder(
                window,
                folderGuid,
                reveal);
        }

        public static bool TrySetSearch(
            EditorWindow window,
            string searchText)
        {
            return ProjectBrowserBackend.TrySetSearch(window, searchText);
        }

        public static bool TryClearSearch(EditorWindow window)
        {
            return ProjectBrowserBackend.TryClearSearch(window);
        }

        public static bool TryGetOpenWindows(
            out IReadOnlyList<EditorWindow> windows)
        {
            return ProjectBrowserBackend.TryGetOpenWindows(out windows);
        }

        public static bool TryCreateEmbeddedBrowser(out EditorWindow browser, System.Action repaint = null)
        {
            return ProjectBrowserBackend.TryCreateEmbeddedBrowser(out browser, repaint);
        }

        public static bool TryDrawEmbeddedBrowser(EditorWindow browser, EditorWindow host, Rect rect)
        {
            return ProjectBrowserBackend.TryDrawEmbeddedBrowser(browser, host, rect);
        }

        public static void DestroyEmbeddedBrowser(EditorWindow browser)
        {
            ProjectBrowserBackend.DestroyEmbeddedBrowser(browser);
        }

        public static bool TryUpdateEmbeddedBrowser(EditorWindow browser)
        {
            return ProjectBrowserBackend.TryUpdateEmbeddedBrowser(browser);
        }
    }
}
