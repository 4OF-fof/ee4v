using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.UI;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ee4v.ProjectTabs
{
    internal sealed class ProjectTabsHost : VisualElement
    {
        private const long TrackingIntervalMilliseconds = 250L;
        private readonly EditorWindow _window;
        private readonly ProjectTabsSession _session;
        private readonly UnityProjectBrowserNavigator _navigator;
        private readonly IProjectTabFolderDropResolver
            _folderDropResolver;
        private readonly ProjectTabsView _view;
        private string _selectedTabId;
        private double _ignoreTrackingUntil;

        public ProjectTabsHost(
            EditorWindow window,
            ProjectTabsSession session,
            IProjectTabFolderDropResolver folderDropResolver)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _session = session ??
                throw new ArgumentNullException(nameof(session));
            _folderDropResolver = folderDropResolver ??
                throw new ArgumentNullException(
                    nameof(folderDropResolver));
            _navigator = new UnityProjectBrowserNavigator(window);
            _view = new ProjectTabsView();
            _selectedTabId = _session.State.Tabs.First().Id;
            style.flexGrow = 1f;
            style.minWidth = 0f;
            style.height = UiSizeTokens.ControlHeightCompact;

            _view.BackRequested += GoBack;
            _view.ForwardRequested += GoForward;
            _view.BackHistoryRequested += GoBack;
            _view.ForwardHistoryRequested += GoForward;
            _view.AddRequested += AddTab;
            _view.TabSelected += SelectTab;
            _view.TabCloseRequested += CloseTab;
            _view.TabMoveRequested += MoveTab;
            _view.TabPinToggleRequested += TogglePinned;
            _view.FolderDropAcceptanceRequested +=
                CanAcceptFolderDrop;
            _view.FolderDropRequested += AddDroppedFolders;
            _session.Changed += OnSessionChanged;

            Add(_view);
            Refresh();

            schedule.Execute(TrackCurrentLocation)
                .Every(TrackingIntervalMilliseconds);
            RegisterCallback<DetachFromPanelEvent>(_ => Dispose());
        }

        private void SelectTab(string tabId)
        {
            var tab = _session.State.Find(tabId);
            if (tab == null)
            {
                return;
            }

            _selectedTabId = tab.Id;
            Open(tab.CurrentLocation);
            Refresh();
        }

        private void AddTab()
        {
            var location = GetCurrentOrSelectedLocation();
            _selectedTabId = _session.Add(location);
            Open(location);
            Refresh();
        }

        private void CloseTab(string tabId)
        {
            var state = _session.State;
            var index = -1;
            for (var i = 0; i < state.Tabs.Count; i++)
            {
                if (string.Equals(
                        state.Tabs[i].Id,
                        tabId,
                        StringComparison.Ordinal))
                {
                    index = i;
                    break;
                }
            }

            if (index <= 0)
            {
                return;
            }

            var wasSelected = string.Equals(
                _selectedTabId,
                tabId,
                StringComparison.Ordinal);
            ProjectTabLocation replacementLocation = null;
            if (wasSelected)
            {
                var replacement = state.Tabs[index - 1];
                _selectedTabId = replacement.Id;
                replacementLocation = replacement.CurrentLocation;
            }

            if (_session.Remove(tabId) && wasSelected)
            {
                Open(replacementLocation);
            }
        }

        private void MoveTab(string tabId, int targetIndex)
        {
            _session.Move(tabId, targetIndex);
        }

        private void TogglePinned(string tabId)
        {
            var tab = _session.State.Find(tabId);
            if (tab != null && !tab.IsHome)
            {
                _session.SetPinned(tabId, !tab.IsPinned);
            }
        }

        private bool CanAcceptFolderDrop(
            IReadOnlyList<string> paths)
        {
            return _folderDropResolver.Resolve(paths).Count > 0;
        }

        private void AddDroppedFolders(
            IReadOnlyList<string> paths)
        {
            var locations = _folderDropResolver.Resolve(paths);
            if (locations.Count > 0)
            {
                _session.AddRange(locations);
            }
        }

        private void GoBack()
        {
            Open(_session.GoBack(_selectedTabId));
        }

        private void GoForward()
        {
            Open(_session.GoForward(_selectedTabId));
        }

        private void GoBack(int steps)
        {
            Open(_session.GoBack(_selectedTabId, steps));
        }

        private void GoForward(int steps)
        {
            Open(_session.GoForward(_selectedTabId, steps));
        }

        private void TrackCurrentLocation()
        {
            if (EditorApplication.timeSinceStartup < _ignoreTrackingUntil ||
                (EditorWindow.focusedWindow != _window &&
                 EditorWindow.mouseOverWindow != _window))
            {
                return;
            }

            if (_navigator.TryGetCurrentLocation(out var location))
            {
                if (_session.ShouldOpenInNewTab(
                        _selectedTabId,
                        location))
                {
                    _selectedTabId = _session.Add(location);
                    Refresh();
                }
                else
                {
                    _session.RecordNavigation(
                        _selectedTabId,
                        location);
                }
            }
        }

        private ProjectTabLocation GetCurrentOrSelectedLocation()
        {
            if (_navigator.TryGetCurrentLocation(out var location))
            {
                return location;
            }

            return _session.State.Find(_selectedTabId)?.CurrentLocation ??
                UnityProjectBrowserNavigator.CreateDefaultLocation();
        }

        private void Open(ProjectTabLocation location)
        {
            if (location == null)
            {
                return;
            }

            _ignoreTrackingUntil =
                EditorApplication.timeSinceStartup + 0.3d;
            _navigator.TryOpen(location);
            _window.Repaint();
        }

        private void OnSessionChanged()
        {
            var state = _session.State;
            if (state.Find(_selectedTabId) == null)
            {
                var home = state.Tabs[0];
                _selectedTabId = home.Id;
                Open(home.CurrentLocation);
            }

            Refresh();
        }

        private void Refresh()
        {
            var state = _session.State;
            var selected = state.Find(_selectedTabId);
            var tabs = state.Tabs
                .Select(tab =>
                {
                    var location = tab.CurrentLocation;
                    var title = tab.IsHome
                        ? string.Empty
                        : location?.DisplayName;
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        title = tab.IsHome
                            ? string.Empty
                            : "Assets";
                    }

                    var tooltip = location?.FolderPath ?? "Assets";
                    if (!string.IsNullOrEmpty(location?.SearchText))
                    {
                        tooltip += "\n" + location.SearchText;
                    }

                    return new ProjectTabViewState(
                        tab.Id,
                        title,
                        tooltip,
                        !tab.IsHome,
                        tab.IsPinned,
                        tab.IsHome);
                })
                .ToArray();

            _view.SetState(new ProjectTabsViewState(
                tabs,
                _selectedTabId,
                selected?.CanGoBack ?? false,
                selected?.CanGoForward ?? false,
                CreateHistoryEntries(selected, true),
                CreateHistoryEntries(selected, false)));
        }

        private static ProjectHistoryEntryViewState[] CreateHistoryEntries(
            ProjectTabState tab,
            bool back)
        {
            if (tab == null)
            {
                return Array.Empty<ProjectHistoryEntryViewState>();
            }

            var start = back
                ? tab.HistoryIndex - 1
                : tab.HistoryIndex + 1;
            var end = back ? -1 : tab.History.Count;
            var direction = back ? -1 : 1;
            var entries = new List<ProjectHistoryEntryViewState>();

            for (var index = start; index != end; index += direction)
            {
                var location = tab.History[index];
                var steps = Math.Abs(index - tab.HistoryIndex);
                entries.Add(new ProjectHistoryEntryViewState(
                    FormatHistoryLabel(location),
                    steps));
            }

            return entries.ToArray();
        }

        internal static string FormatHistoryLabel(
            ProjectTabLocation location)
        {
            if (location == null)
            {
                return string.Empty;
            }

            var displayName = string.IsNullOrWhiteSpace(
                location.DisplayName)
                ? "Assets"
                : location.DisplayName;
            return string.IsNullOrEmpty(location.SearchText)
                ? displayName
                : displayName + " · " + location.SearchText;
        }

        private void Dispose()
        {
            _session.Changed -= OnSessionChanged;
            _view.BackRequested -= GoBack;
            _view.ForwardRequested -= GoForward;
            _view.BackHistoryRequested -= GoBack;
            _view.ForwardHistoryRequested -= GoForward;
            _view.AddRequested -= AddTab;
            _view.TabSelected -= SelectTab;
            _view.TabCloseRequested -= CloseTab;
            _view.TabMoveRequested -= MoveTab;
            _view.TabPinToggleRequested -= TogglePinned;
            _view.FolderDropAcceptanceRequested -=
                CanAcceptFolderDrop;
            _view.FolderDropRequested -= AddDroppedFolders;
        }
    }
}
