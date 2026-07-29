using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.Internal.EditorAPI;

namespace Ee4v.ProjectTabs
{
    internal sealed class UnityProjectFavoriteFolderStore
        : IProjectFavoriteFolderStore,
          IDisposable
    {
        private bool _isListening;

        public UnityProjectFavoriteFolderStore()
        {
            _isListening =
                ProjectFavorites.TryAddChangedListener(
                    OnFavoritesChanged);
        }

        public event Action Changed;

        public bool TryGetAll(
            out IReadOnlyList<ProjectTabLocation> locations)
        {
            locations = Array.Empty<ProjectTabLocation>();
            if (!ProjectFavorites.TryGetFolders(out var folders))
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
                ProjectFavorites.TryAddFolder(location.FolderPath);
        }

        public bool TryRemove(ProjectTabLocation location)
        {
            return location != null &&
                ProjectFavorites.TryRemoveFolder(location.FolderPath);
        }

        public void Dispose()
        {
            if (!_isListening)
            {
                return;
            }

            ProjectFavorites.RemoveChangedListener(
                OnFavoritesChanged);
            _isListening = false;
        }

        private void OnFavoritesChanged()
        {
            Changed?.Invoke();
        }
    }
}
