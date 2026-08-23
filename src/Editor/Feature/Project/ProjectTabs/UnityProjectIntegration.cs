using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.EditorIntegration;
using UnityEditor;

namespace Ee4v.ProjectTabs
{
    internal sealed class UnityProjectBrowserNavigator
    {
        private readonly EditorWindow _window;

        public UnityProjectBrowserNavigator(EditorWindow window)
        {
            _window = window;
        }

        public bool IsAvailable()
        {
            return ProjectBrowserApi.TryGetState(_window, out var state) &&
                state.ViewMode == ProjectBrowserViewMode.TwoColumns;
        }

        public bool TryGetCurrentLocation(out ProjectTabLocation location)
        {
            location = null;
            if (!ProjectBrowserApi.TryGetState(_window, out var state) ||
                state.ViewMode != ProjectBrowserViewMode.TwoColumns ||
                string.IsNullOrWhiteSpace(state.FolderGuid) ||
                string.IsNullOrWhiteSpace(state.FolderPath))
            {
                return false;
            }

            location = new ProjectTabLocation(
                state.FolderGuid,
                state.FolderPath,
                state.SearchText);
            return true;
        }

        public bool TryOpen(ProjectTabLocation location)
        {
            if (location == null ||
                !IsAvailable() ||
                !ProjectBrowserApi.TryShowFolder(
                    _window,
                    location.FolderGuid))
            {
                return false;
            }

            return string.IsNullOrEmpty(location.SearchText)
                ? ProjectBrowserApi.TryClearSearch(_window)
                : ProjectBrowserApi.TrySetSearch(
                    _window,
                    location.SearchText);
        }

        public static ProjectTabLocation CreateDefaultLocation()
        {
            return new ProjectTabLocation(
                AssetDatabase.AssetPathToGUID("Assets"),
                "Assets");
        }
    }

    internal sealed class UnityProjectFavoriteFolderStore
        : IProjectFavoriteFolderStore,
          IDisposable
    {
        private bool _isListening;

        public UnityProjectFavoriteFolderStore()
        {
            _isListening =
                ProjectFavoritesApi.TryAddChangedListener(
                    OnFavoritesChanged);
        }

        public event Action Changed;

        public bool TryGetAll(
            out IReadOnlyList<ProjectTabLocation> locations)
        {
            locations = Array.Empty<ProjectTabLocation>();
            if (!ProjectFavoritesApi.TryGetFolders(out var folders))
            {
                return false;
            }

            locations = folders
                .Where(folder =>
                    !string.IsNullOrWhiteSpace(folder.FolderGuid) &&
                    !string.IsNullOrWhiteSpace(folder.FolderPath))
                .Select(folder => new ProjectTabLocation(
                    folder.FolderGuid,
                    folder.FolderPath))
                .GroupBy(
                    location => location.FolderPath,
                    StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
            return true;
        }

        public bool TryAdd(ProjectTabLocation location)
        {
            return location != null &&
                ProjectFavoritesApi.TryAddFolder(location.FolderPath);
        }

        public bool TryRemove(ProjectTabLocation location)
        {
            return location != null &&
                ProjectFavoritesApi.TryRemoveFolder(location.FolderPath);
        }

        public void Dispose()
        {
            if (!_isListening)
            {
                return;
            }

            ProjectFavoritesApi.RemoveChangedListener(
                OnFavoritesChanged);
            _isListening = false;
        }

        private void OnFavoritesChanged()
        {
            Changed?.Invoke();
        }
    }

    internal static class UnityProjectTabFolderDropResolver
    {
        public static IReadOnlyList<ProjectTabLocation> Resolve(
            IReadOnlyList<string> paths)
        {
            if (paths == null || paths.Count == 0)
            {
                return Array.Empty<ProjectTabLocation>();
            }

            var locations = new List<ProjectTabLocation>();
            var resolvedGuids = new HashSet<string>(
                StringComparer.Ordinal);
            for (var i = 0; i < paths.Count; i++)
            {
                var path = paths[i];
                if (string.IsNullOrWhiteSpace(path) ||
                    !AssetDatabase.IsValidFolder(path))
                {
                    continue;
                }

                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrWhiteSpace(guid) ||
                    !resolvedGuids.Add(guid))
                {
                    continue;
                }

                locations.Add(new ProjectTabLocation(guid, path));
            }

            return locations;
        }
    }
}
