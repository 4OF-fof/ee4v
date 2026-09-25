using System;
using System.Collections.Generic;
using System.Linq;

namespace Ee4v.ProjectTabs
{
    internal sealed class ProjectTabsSession
    {
        private const int MaximumHistoryEntries = 50;
        private readonly IProjectTabsStateStore _store;
        private readonly IProjectFavoriteFolderStore _favorites;
        private readonly Func<string> _idFactory;
        private readonly ProjectTabLocation _defaultLocation;
        private readonly List<MutableTab> _tabs = new List<MutableTab>();
        private bool _changingFavorites;

        public ProjectTabsSession(
            IProjectTabsStateStore store,
            ProjectTabLocation defaultLocation,
            IProjectFavoriteFolderStore favorites,
            Func<string> idFactory = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _defaultLocation = defaultLocation ??
                throw new ArgumentNullException(nameof(defaultLocation));
            _favorites = favorites ??
                throw new ArgumentNullException(nameof(favorites));
            _idFactory = idFactory ?? (() => Guid.NewGuid().ToString("N"));
            Restore();
        }

        public event Action Changed;

        public ProjectTabsState State
        {
            get { return CreateSnapshot(); }
        }

        public string Add(ProjectTabLocation location)
        {
            var tab = CreateTab(location);
            _tabs.Add(tab);
            PersistAndNotify();
            return tab.Id;
        }

        public IReadOnlyList<string> AddRange(
            IEnumerable<ProjectTabLocation> locations)
        {
            if (locations == null)
            {
                return Array.Empty<string>();
            }

            var addedIds = new List<string>();
            foreach (var location in locations)
            {
                var tab = CreateTab(location);
                _tabs.Add(tab);
                addedIds.Add(tab.Id);
            }

            if (addedIds.Count == 0)
            {
                return Array.Empty<string>();
            }

            PersistAndNotify();
            return addedIds.ToArray();
        }

        public bool Move(string tabId, int targetIndex)
        {
            var currentIndex = FindIndex(tabId);
            if (currentIndex < 0 ||
                targetIndex < 0 ||
                targetIndex >= _tabs.Count ||
                currentIndex == targetIndex)
            {
                return false;
            }

            var tab = _tabs[currentIndex];
            _tabs.RemoveAt(currentIndex);
            _tabs.Insert(targetIndex, tab);
            PersistAndNotify();
            return true;
        }

        public bool Remove(string tabId)
        {
            var index = FindIndex(tabId);
            if (index < 0 ||
                !TryGetFavoriteFolders(out var favoriteFolders))
            {
                return false;
            }

            var location = _tabs[index].CurrentLocation;
            if (favoriteFolders.Contains(location.FolderPath))
            {
                _changingFavorites = true;
                try
                {
                    if (!_favorites.TryRemove(location) ||
                        !TryGetFavoriteFolders(out var updatedFolders) ||
                        updatedFolders.Contains(location.FolderPath))
                    {
                        return false;
                    }
                }
                finally
                {
                    _changingFavorites = false;
                }
            }

            _tabs.RemoveAt(index);
            if (_tabs.Count == 0)
            {
                _tabs.Add(CreateTab(_defaultLocation));
            }

            PersistAndNotify();
            return true;
        }

        public bool SetPinned(string tabId, bool isPinned)
        {
            var index = FindIndex(tabId);
            if (index < 0 ||
                !TryGetFavoriteFolders(out var favoriteFolders))
            {
                return false;
            }

            var tab = _tabs[index];
            var location = tab.CurrentLocation;
            if (favoriteFolders.Contains(location.FolderPath) == isPinned)
            {
                return false;
            }

            _changingFavorites = true;
            try
            {
                if (!(isPinned
                        ? _favorites.TryAdd(location)
                        : _favorites.TryRemove(location)) ||
                    !TryGetFavoriteFolders(out var updatedFolders) ||
                    updatedFolders.Contains(location.FolderPath) !=
                        isPinned)
                {
                    return false;
                }
            }
            finally
            {
                _changingFavorites = false;
            }

            RefreshFromFavorites();
            return true;
        }

        internal void RefreshFromFavorites()
        {
            if (_changingFavorites ||
                !_favorites.TryGetAll(out var favoriteLocations))
            {
                return;
            }

            favoriteLocations = favoriteLocations ??
                Array.Empty<ProjectTabLocation>();
            var changed = false;
            foreach (var location in favoriteLocations)
            {
                if (location == null ||
                    string.IsNullOrWhiteSpace(location.FolderPath) ||
                    _tabs.Any(tab => string.Equals(
                        tab.CurrentLocation?.FolderPath,
                        location.FolderPath,
                        StringComparison.Ordinal)))
                {
                    continue;
                }

                _tabs.Add(CreateTab(location));
                changed = true;
            }

            var favoriteFolders = new HashSet<string>(
                favoriteLocations
                    .Where(location => location != null)
                    .Select(location => location.FolderPath),
                StringComparer.Ordinal);
            foreach (var tab in _tabs)
            {
                if (!favoriteFolders.Contains(
                        tab.CurrentLocation.FolderPath) ||
                    (tab.History.Count == 1 &&
                     tab.HistoryIndex == 0))
                {
                    continue;
                }

                var currentLocation = tab.CurrentLocation;
                tab.History.Clear();
                tab.History.Add(currentLocation);
                tab.HistoryIndex = 0;
                changed = true;
            }

            if (changed)
            {
                PersistAndNotify();
            }
            else
            {
                Changed?.Invoke();
            }
        }

        public bool ShouldOpenInNewTab(
            string tabId,
            ProjectTabLocation location)
        {
            var tab = Find(tabId);
            if (tab == null || !IsPinned(tab.CurrentLocation))
            {
                return false;
            }

            return !HasSameFolder(
                tab.CurrentLocation,
                NormalizeLocation(location));
        }

        public bool RecordNavigation(
            string tabId,
            ProjectTabLocation location)
        {
            var tab = Find(tabId);
            if (tab == null)
            {
                return false;
            }

            var normalized = NormalizeLocation(location);
            var current = tab.CurrentLocation;
            if (normalized.Equals(current))
            {
                return false;
            }

            if (IsPinned(current))
            {
                if (HasSameFolder(current, normalized))
                {
                    tab.History[tab.HistoryIndex] = normalized;
                    PersistAndNotify();
                    return true;
                }

                return false;
            }

            if (HasSameFolder(current, normalized))
            {
                tab.History[tab.HistoryIndex] = normalized;
                PersistAndNotify();
                return true;
            }

            if (tab.HistoryIndex < tab.History.Count - 1)
            {
                tab.History.RemoveRange(
                    tab.HistoryIndex + 1,
                    tab.History.Count - tab.HistoryIndex - 1);
            }

            tab.History.Add(normalized);
            if (tab.History.Count > MaximumHistoryEntries)
            {
                tab.History.RemoveAt(0);
            }

            tab.HistoryIndex = tab.History.Count - 1;
            PersistAndNotify();
            return true;
        }

        public ProjectTabLocation GoBack(string tabId)
        {
            return GoBack(tabId, 1);
        }

        public ProjectTabLocation GoBack(string tabId, int steps)
        {
            var tab = Find(tabId);
            if (tab == null ||
                IsPinned(tab.CurrentLocation) ||
                tab.HistoryIndex <= 0 ||
                steps <= 0)
            {
                return null;
            }

            tab.HistoryIndex = Math.Max(0, tab.HistoryIndex - steps);
            PersistAndNotify();
            return tab.CurrentLocation;
        }

        public ProjectTabLocation GoForward(string tabId)
        {
            return GoForward(tabId, 1);
        }

        public ProjectTabLocation GoForward(string tabId, int steps)
        {
            var tab = Find(tabId);
            if (tab == null ||
                IsPinned(tab.CurrentLocation) ||
                tab.HistoryIndex >= tab.History.Count - 1 ||
                steps <= 0)
            {
                return null;
            }

            tab.HistoryIndex = Math.Min(
                tab.History.Count - 1,
                tab.HistoryIndex + steps);
            PersistAndNotify();
            return tab.CurrentLocation;
        }

        private void Restore()
        {
            var restored = _store.Load();
            if (restored != null)
            {
                for (var i = 0; i < restored.Tabs.Count; i++)
                {
                    var tab = restored.Tabs[i];
                    if (tab == null ||
                        string.IsNullOrWhiteSpace(tab.Id) ||
                        _tabs.Any(existing =>
                            string.Equals(
                                existing.Id,
                                tab.Id,
                                StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    var history = tab.History
                        .Where(location => location != null)
                        .Select(NormalizeLocation)
                        .Take(MaximumHistoryEntries)
                        .ToList();
                    if (history.Count == 0)
                    {
                        history.Add(_defaultLocation);
                    }

                    var historyIndex = Math.Max(
                        0,
                        Math.Min(tab.HistoryIndex, history.Count - 1));
                    _tabs.Add(new MutableTab(
                        tab.Id,
                        history,
                        historyIndex));
                }
            }

            if (_tabs.Count == 0)
            {
                _tabs.Add(CreateTab(_defaultLocation));
            }
        }

        private ProjectTabsState CreateSnapshot()
        {
            TryGetFavoriteFolders(out var favoriteFolders);
            return new ProjectTabsState(
                _tabs.Select(tab => new ProjectTabState(
                        tab.Id,
                        tab.History.ToArray(),
                        tab.HistoryIndex,
                        favoriteFolders.Contains(
                            tab.CurrentLocation.FolderPath)))
                    .ToArray());
        }

        private bool IsPinned(ProjectTabLocation location)
        {
            return location != null &&
                TryGetFavoriteFolders(out var favoriteFolders) &&
                favoriteFolders.Contains(location.FolderPath);
        }

        private bool TryGetFavoriteFolders(
            out HashSet<string> favoriteFolders)
        {
            favoriteFolders = new HashSet<string>(StringComparer.Ordinal);
            if (!_favorites.TryGetAll(out var locations))
            {
                return false;
            }

            foreach (var location in locations ??
                Array.Empty<ProjectTabLocation>())
            {
                if (location != null)
                {
                    favoriteFolders.Add(location.FolderPath);
                }
            }

            return true;
        }

        private ProjectTabLocation NormalizeLocation(
            ProjectTabLocation location)
        {
            if (location == null ||
                string.IsNullOrWhiteSpace(location.FolderGuid) ||
                string.IsNullOrWhiteSpace(location.FolderPath))
            {
                return _defaultLocation;
            }

            return location;
        }

        private static bool HasSameFolder(
            ProjectTabLocation first,
            ProjectTabLocation second)
        {
            return first != null &&
                second != null &&
                string.Equals(
                    first.FolderGuid,
                    second.FolderGuid,
                    StringComparison.Ordinal) &&
                string.Equals(
                    first.FolderPath,
                    second.FolderPath,
                    StringComparison.Ordinal);
        }

        private MutableTab CreateTab(ProjectTabLocation location)
        {
            return new MutableTab(
                CreateUniqueId(),
                new[] { NormalizeLocation(location) },
                0);
        }

        private MutableTab Find(string tabId)
        {
            var index = FindIndex(tabId);
            return index < 0 ? null : _tabs[index];
        }

        private int FindIndex(string tabId)
        {
            return _tabs.FindIndex(tab =>
                string.Equals(tab.Id, tabId, StringComparison.Ordinal));
        }

        private string CreateUniqueId()
        {
            string id;
            do
            {
                id = _idFactory() ?? string.Empty;
            }
            while (string.IsNullOrWhiteSpace(id) ||
                _tabs.Any(tab =>
                    string.Equals(tab.Id, id, StringComparison.Ordinal)));

            return id;
        }

        private void PersistAndNotify()
        {
            _store.Save(CreateSnapshot());
            Changed?.Invoke();
        }

        private sealed class MutableTab
        {
            public MutableTab(
                string id,
                IEnumerable<ProjectTabLocation> history,
                int historyIndex)
            {
                Id = id;
                History = new List<ProjectTabLocation>(history);
                HistoryIndex = historyIndex;
            }

            public string Id { get; }

            public List<ProjectTabLocation> History { get; }

            public int HistoryIndex { get; set; }

            public ProjectTabLocation CurrentLocation
            {
                get
                {
                    return HistoryIndex >= 0 && HistoryIndex < History.Count
                        ? History[HistoryIndex]
                        : null;
                }
            }
        }
    }
}
