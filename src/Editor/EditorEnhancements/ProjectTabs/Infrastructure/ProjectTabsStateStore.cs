using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ee4v.ProjectTabs
{
    [FilePath(
        "UserSettings/ee4v.project-tabs.asset",
        FilePathAttribute.Location.ProjectFolder)]
    internal sealed class ProjectTabsStateStore
        : ScriptableSingleton<ProjectTabsStateStore>,
          IProjectTabsStateStore
    {
        [SerializeField]
        private List<SerializedTab> _tabs = new List<SerializedTab>();

        public ProjectTabsState Load()
        {
            return new ProjectTabsState(
                _tabs
                    .Where(tab => tab != null)
                    .Select(tab => new ProjectTabState(
                        tab.id,
                        (tab.history ?? new List<SerializedLocation>())
                            .Where(location => location != null)
                            .Select(location => new ProjectTabLocation(
                                location.folderGuid,
                                location.folderPath,
                                location.searchText))
                            .ToArray(),
                        tab.historyIndex,
                        tab.isPinned))
                    .ToArray());
        }

        public void Save(ProjectTabsState state)
        {
            _tabs = (state?.Tabs ?? Array.Empty<ProjectTabState>())
                .Where(tab => tab != null && !tab.IsHome)
                .Select(tab => new SerializedTab
                {
                    id = tab.Id,
                    isPinned = tab.IsPinned,
                    historyIndex = tab.HistoryIndex,
                    history = tab.History
                        .Where(location => location != null)
                        .Select(location => new SerializedLocation
                        {
                            folderGuid = location.FolderGuid,
                            folderPath = location.FolderPath,
                            searchText = location.SearchText
                        })
                        .ToList()
                })
                .ToList();
            Save(true);
        }

        [Serializable]
        private sealed class SerializedTab
        {
            public string id;
            public bool isPinned;
            public List<SerializedLocation> history =
                new List<SerializedLocation>();
            public int historyIndex;
        }

        [Serializable]
        private sealed class SerializedLocation
        {
            public string folderGuid;
            public string folderPath;
            public string searchText;
        }
    }
}
