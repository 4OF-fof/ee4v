using System;
using System.Collections.Generic;
using System.Linq;

namespace Ee4v.ProjectTabs
{
    internal sealed class ProjectTabsSession
    {
        internal const string HomeTabId =
            "__ee4v_project_tabs_home__";
        private const int MaximumHistoryEntries = 50;
        private readonly IProjectTabsStateStore _store;
        private readonly Func<string> _idFactory;
        private readonly ProjectTabLocation _defaultLocation;
        private readonly List<MutableTab> _tabs = new List<MutableTab>();

        public ProjectTabsSession(
            IProjectTabsStateStore store,
            ProjectTabLocation defaultLocation,
            Func<string> idFactory = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _defaultLocation = defaultLocation ??
                throw new ArgumentNullException(nameof(defaultLocation));
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
                targetIndex < 1 ||
                targetIndex >= _tabs.Count ||
                currentIndex == targetIndex)
            {
                return false;
            }

            var tab = _tabs[currentIndex];
            if (tab.IsHome)
            {
                return false;
            }

            _tabs.RemoveAt(currentIndex);
            _tabs.Insert(targetIndex, tab);
            PersistAndNotify();
            return true;
        }

        public bool Remove(string tabId)
        {
            var index = FindIndex(tabId);
            if (index < 0 || _tabs[index].IsHome)
            {
                return false;
            }

            _tabs.RemoveAt(index);
            PersistAndNotify();
            return true;
        }

        public bool SetPinned(string tabId, bool isPinned)
        {
            var index = FindIndex(tabId);
            if (index < 0)
            {
                return false;
            }

            var tab = _tabs[index];
            if (tab.IsHome || tab.IsPinned == isPinned)
            {
                return false;
            }

            if (isPinned)
            {
                var currentLocation = tab.CurrentLocation;
                tab.History.Clear();
                tab.History.Add(currentLocation);
                tab.HistoryIndex = 0;
            }

            tab.IsPinned = isPinned;

            PersistAndNotify();
            return true;
        }

        public bool ShouldOpenInNewTab(
            string tabId,
            ProjectTabLocation location)
        {
            var tab = Find(tabId);
            if (tab == null || !tab.IsPinned)
            {
                return false;
            }

            var normalized = NormalizeLocation(location);
            if (tab.IsHome)
            {
                return !IsHomeRoot(normalized);
            }

            return !HasSameFolder(
                tab.CurrentLocation,
                normalized);
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

            if (tab.IsHome)
            {
                if (!IsHomeRoot(normalized))
                {
                    return false;
                }

                tab.History[tab.HistoryIndex] = normalized;
                PersistAndNotify();
                return true;
            }

            if (tab.IsPinned)
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
                tab.IsPinned ||
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
                tab.IsPinned ||
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
                        tab.IsHome ||
                        string.Equals(
                            tab.Id,
                            HomeTabId,
                            StringComparison.Ordinal) ||
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
                    if (tab.IsPinned)
                    {
                        var pinnedLocation = history[historyIndex];
                        history.Clear();
                        history.Add(pinnedLocation);
                        historyIndex = 0;
                    }

                    _tabs.Add(new MutableTab(
                        tab.Id,
                        history,
                        historyIndex,
                        tab.IsPinned,
                        false));
                }
            }

            _tabs.Insert(0, new MutableTab(
                HomeTabId,
                new[] { _defaultLocation },
                0,
                true,
                true));
        }

        private ProjectTabsState CreateSnapshot()
        {
            return new ProjectTabsState(
                _tabs.Select(tab => new ProjectTabState(
                        tab.Id,
                        tab.History.ToArray(),
                        tab.HistoryIndex,
                        tab.IsPinned,
                        tab.IsHome))
                    .ToArray());
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

        private static bool IsHomeRoot(ProjectTabLocation location)
        {
            return location != null &&
                (string.Equals(
                     location.FolderPath,
                     "Assets",
                     StringComparison.Ordinal) ||
                 string.Equals(
                     location.FolderPath,
                     "Packages",
                     StringComparison.Ordinal));
        }

        private MutableTab CreateTab(ProjectTabLocation location)
        {
            return new MutableTab(
                CreateUniqueId(),
                new[] { NormalizeLocation(location) },
                0,
                false,
                false);
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
                int historyIndex,
                bool isPinned,
                bool isHome)
            {
                Id = id;
                History = new List<ProjectTabLocation>(history);
                HistoryIndex = historyIndex;
                IsPinned = isPinned || isHome;
                IsHome = isHome;
            }

            public string Id { get; }

            public List<ProjectTabLocation> History { get; }

            public int HistoryIndex { get; set; }

            public bool IsPinned { get; set; }

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
        }
    }
}
