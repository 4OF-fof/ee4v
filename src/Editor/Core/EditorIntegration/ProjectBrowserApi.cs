using System.Collections.Generic;
using Ee4v.Core.Internal.EditorAPI;
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
            Ee4v.Core.Internal.EditorAPI.ProjectBrowserSnapshot snapshot)
        {
            FolderGuid = snapshot.FolderGuid;
            FolderPath = snapshot.FolderPath;
            SearchText = snapshot.SearchText;
            HasSearch = snapshot.HasSearch;
            ViewMode = (ProjectBrowserViewMode)snapshot.ViewMode;
            Orientation = (ProjectBrowserOrientation)snapshot.Orientation;
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
            return Convert(
                ProjectBrowser.TryGetSnapshot(out var snapshot),
                snapshot,
                out state);
        }

        public static bool TryGetState(
            EditorWindow window,
            out ProjectBrowserState state)
        {
            return Convert(
                ProjectBrowser.TryGetSnapshot(window, out var snapshot),
                snapshot,
                out state);
        }

        public static bool TryGetState(
            Rect selectionRect,
            out ProjectBrowserState state)
        {
            return Convert(
                ProjectBrowser.TryGetSnapshot(
                    selectionRect,
                    out var snapshot),
                snapshot,
                out state);
        }

        public static bool TryShowFolder(
            EditorWindow window,
            string folderGuid,
            bool reveal = false)
        {
            return ProjectBrowser.TryShowFolder(
                window,
                folderGuid,
                reveal);
        }

        public static bool TrySetSearch(
            EditorWindow window,
            string searchText)
        {
            return ProjectBrowser.TrySetSearch(window, searchText);
        }

        public static bool TryClearSearch(EditorWindow window)
        {
            return ProjectBrowser.TryClearSearch(window);
        }

        public static bool TryGetOpenWindows(
            out IReadOnlyList<EditorWindow> windows)
        {
            return ProjectBrowser.TryGetOpenWindows(out windows);
        }

        private static bool Convert(
            bool succeeded,
            Ee4v.Core.Internal.EditorAPI.ProjectBrowserSnapshot snapshot,
            out ProjectBrowserState state)
        {
            state = succeeded && snapshot != null
                ? new ProjectBrowserState(snapshot)
                : null;
            return state != null;
        }
    }
}
