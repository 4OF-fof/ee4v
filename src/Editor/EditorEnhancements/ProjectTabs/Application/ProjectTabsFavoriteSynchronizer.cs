using System;
using System.Collections.Generic;
using System.Linq;

namespace Ee4v.ProjectTabs
{
    internal sealed class ProjectTabsFavoriteSynchronizer : IDisposable
    {
        private readonly ProjectTabsSession _session;
        private readonly IProjectFavoriteFolderStore _favorites;
        private bool _isSynchronizing;
        private bool _isDisposed;

        public ProjectTabsFavoriteSynchronizer(
            ProjectTabsSession session,
            IProjectFavoriteFolderStore favorites)
        {
            _session = session ??
                throw new ArgumentNullException(nameof(session));
            _favorites = favorites ??
                throw new ArgumentNullException(nameof(favorites));

            _session.Changed += OnSessionChanged;
            _favorites.Changed += OnFavoritesChanged;
            MergeInitialState();
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _session.Changed -= OnSessionChanged;
            _favorites.Changed -= OnFavoritesChanged;
        }

        private void MergeInitialState()
        {
            if (!_favorites.TryGetAll(out var favoriteLocations))
            {
                return;
            }

            Synchronize(() =>
            {
                var pinnedLocations = GetPinnedLocations();
                AddMissingFavorites(
                    pinnedLocations,
                    favoriteLocations);
                PinMissingTabs(
                    favoriteLocations,
                    pinnedLocations);
            });
        }

        private void OnSessionChanged()
        {
            if (_isSynchronizing ||
                !_favorites.TryGetAll(out var favoriteLocations))
            {
                return;
            }

            Synchronize(() =>
            {
                var pinnedLocations = GetPinnedLocations();
                AddMissingFavorites(
                    pinnedLocations,
                    favoriteLocations);
                RemoveMissingFavorites(
                    favoriteLocations,
                    pinnedLocations);
            });
        }

        private void OnFavoritesChanged()
        {
            if (_isSynchronizing ||
                !_favorites.TryGetAll(out var favoriteLocations))
            {
                return;
            }

            Synchronize(() =>
            {
                var pinnedLocations = GetPinnedLocations();
                UnpinMissingTabs(
                    pinnedLocations,
                    favoriteLocations);
                PinMissingTabs(
                    favoriteLocations,
                    GetPinnedLocations());
            });
        }

        private IReadOnlyList<ProjectTabLocation> GetPinnedLocations()
        {
            return _session.State.Tabs
                .Where(tab =>
                    !tab.IsHome &&
                    tab.IsPinned &&
                    tab.CurrentLocation != null)
                .Select(tab => tab.CurrentLocation)
                .GroupBy(
                    location => location.FolderPath,
                    StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
        }

        private void AddMissingFavorites(
            IReadOnlyList<ProjectTabLocation> pinnedLocations,
            IReadOnlyList<ProjectTabLocation> favoriteLocations)
        {
            foreach (var pinnedLocation in pinnedLocations)
            {
                if (!ContainsFolder(
                        favoriteLocations,
                        pinnedLocation.FolderPath))
                {
                    _favorites.TryAdd(pinnedLocation);
                }
            }
        }

        private void RemoveMissingFavorites(
            IReadOnlyList<ProjectTabLocation> favoriteLocations,
            IReadOnlyList<ProjectTabLocation> pinnedLocations)
        {
            foreach (var favoriteLocation in favoriteLocations)
            {
                if (!ContainsFolder(
                        pinnedLocations,
                        favoriteLocation.FolderPath))
                {
                    _favorites.TryRemove(favoriteLocation);
                }
            }
        }

        private void PinMissingTabs(
            IReadOnlyList<ProjectTabLocation> favoriteLocations,
            IReadOnlyList<ProjectTabLocation> pinnedLocations)
        {
            foreach (var favoriteLocation in favoriteLocations)
            {
                if (ContainsFolder(
                        pinnedLocations,
                        favoriteLocation.FolderPath))
                {
                    continue;
                }

                var existingTab = _session.State.Tabs.FirstOrDefault(tab =>
                    !tab.IsHome &&
                    !tab.IsPinned &&
                    HasSameFolder(
                        tab.CurrentLocation,
                        favoriteLocation));
                var tabId = existingTab?.Id ??
                    _session.Add(favoriteLocation);
                _session.SetPinned(tabId, true);
            }
        }

        private void UnpinMissingTabs(
            IReadOnlyList<ProjectTabLocation> pinnedLocations,
            IReadOnlyList<ProjectTabLocation> favoriteLocations)
        {
            var missingPaths = new HashSet<string>(
                pinnedLocations
                    .Where(location =>
                        !ContainsFolder(
                            favoriteLocations,
                            location.FolderPath))
                    .Select(location => location.FolderPath),
                StringComparer.Ordinal);
            if (missingPaths.Count == 0)
            {
                return;
            }

            var tabIds = _session.State.Tabs
                .Where(tab =>
                    !tab.IsHome &&
                    tab.IsPinned &&
                    tab.CurrentLocation != null &&
                    missingPaths.Contains(
                        tab.CurrentLocation.FolderPath))
                .Select(tab => tab.Id)
                .ToArray();
            foreach (var tabId in tabIds)
            {
                _session.SetPinned(tabId, false);
            }
        }

        private void Synchronize(Action operation)
        {
            if (_isDisposed || _isSynchronizing)
            {
                return;
            }

            _isSynchronizing = true;
            try
            {
                operation();
            }
            finally
            {
                _isSynchronizing = false;
            }
        }

        private static bool ContainsFolder(
            IEnumerable<ProjectTabLocation> locations,
            string folderPath)
        {
            return locations.Any(location =>
                HasSameFolderPath(location, folderPath));
        }

        private static bool HasSameFolder(
            ProjectTabLocation first,
            ProjectTabLocation second)
        {
            return first != null &&
                second != null &&
                HasSameFolderPath(first, second.FolderPath);
        }

        private static bool HasSameFolderPath(
            ProjectTabLocation location,
            string folderPath)
        {
            return location != null &&
                string.Equals(
                    location.FolderPath,
                    folderPath,
                    StringComparison.Ordinal);
        }
    }
}
