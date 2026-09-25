using System;
using System.Collections.Generic;

namespace Ee4v.ProjectTabs
{
    internal interface IProjectFavoriteFolderStore
    {
        event Action Changed;

        bool TryGetAll(
            out IReadOnlyList<ProjectTabLocation> locations);

        bool TryAdd(ProjectTabLocation location);

        bool TryRemove(ProjectTabLocation location);
    }

    internal sealed class ProjectTabsFavoriteSynchronizer : IDisposable
    {
        private readonly ProjectTabsSession _session;
        private readonly IProjectFavoriteFolderStore _favorites;

        public ProjectTabsFavoriteSynchronizer(
            ProjectTabsSession session,
            IProjectFavoriteFolderStore favorites)
        {
            _session = session ??
                throw new ArgumentNullException(nameof(session));
            _favorites = favorites ??
                throw new ArgumentNullException(nameof(favorites));

            _favorites.Changed += OnFavoritesChanged;
            _session.RefreshFromFavorites();
        }

        public void Dispose()
        {
            _favorites.Changed -= OnFavoritesChanged;
        }

        private void OnFavoritesChanged()
        {
            _session.RefreshFromFavorites();
        }
    }
}
