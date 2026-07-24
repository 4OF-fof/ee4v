using System;
using System.Collections.Generic;
using System.Linq;

namespace Ee4v.ProjectTabs
{
    internal sealed class ProjectTabsSession
    {
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
            var tab = new MutableTab(
                CreateUniqueId(),
                new[] { NormalizeLocation(location) },
                0);
            _tabs.Add(tab);
            PersistAndNotify();
            return tab.Id;
        }

        public bool Remove(string tabId)
        {
            if (_tabs.Count <= 1)
            {
                return false;
            }

            var index = FindIndex(tabId);
            if (index < 0)
            {
                return false;
            }

            _tabs.RemoveAt(index);
            PersistAndNotify();
            return true;
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

            if (current != null &&
                string.Equals(
                    current.FolderGuid,
                    normalized.FolderGuid,
                    StringComparison.Ordinal) &&
                string.Equals(
                    current.FolderPath,
                    normalized.FolderPath,
                    StringComparison.Ordinal))
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
            var tab = Find(tabId);
            if (tab == null || tab.HistoryIndex <= 0)
            {
                return null;
            }

            tab.HistoryIndex--;
            PersistAndNotify();
            return tab.CurrentLocation;
        }

        public ProjectTabLocation GoForward(string tabId)
        {
            var tab = Find(tabId);
            if (tab == null || tab.HistoryIndex >= tab.History.Count - 1)
            {
                return null;
            }

            tab.HistoryIndex++;
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
                    _tabs.Add(new MutableTab(tab.Id, history, historyIndex));
                }
            }

            if (_tabs.Count == 0)
            {
                _tabs.Add(new MutableTab(
                    CreateUniqueId(),
                    new[] { _defaultLocation },
                    0));
                _store.Save(CreateSnapshot());
            }
        }

        private ProjectTabsState CreateSnapshot()
        {
            return new ProjectTabsState(
                _tabs.Select(tab => new ProjectTabState(
                        tab.Id,
                        tab.History.ToArray(),
                        tab.HistoryIndex))
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
