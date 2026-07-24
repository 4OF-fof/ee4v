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
        private readonly ProjectTabsView _view;
        private string _selectedTabId;
        private double _ignoreTrackingUntil;

        public ProjectTabsHost(
            EditorWindow window,
            ProjectTabsSession session)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _session = session ??
                throw new ArgumentNullException(nameof(session));
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

            if (index < 0)
            {
                return;
            }

            if (string.Equals(
                    _selectedTabId,
                    tabId,
                    StringComparison.Ordinal) &&
                state.Tabs.Count > 1)
            {
                var replacementIndex = index > 0 ? index - 1 : 1;
                _selectedTabId = state.Tabs[replacementIndex].Id;
            }

            if (_session.Remove(tabId))
            {
                var selected = _session.State.Find(_selectedTabId);
                Open(selected?.CurrentLocation);
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
                _session.RecordNavigation(_selectedTabId, location);
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
                _selectedTabId = state.Tabs.First().Id;
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
                    var title = location?.DisplayName;
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        title = "Assets";
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
                        true);
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
        }
    }
}
