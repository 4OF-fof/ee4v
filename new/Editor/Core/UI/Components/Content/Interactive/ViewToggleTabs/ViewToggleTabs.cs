using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class ViewToggleTabState
    {
        public ViewToggleTabState(string id, string label, bool enabled = true)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Enabled = enabled;
        }

        public string Id { get; }

        public string Label { get; }

        public bool Enabled { get; }
    }

    internal sealed class ViewToggleTabsState
    {
        public ViewToggleTabsState(IReadOnlyList<ViewToggleTabState> tabs, string selectedTabId)
        {
            Tabs = tabs ?? Array.Empty<ViewToggleTabState>();
            SelectedTabId = selectedTabId ?? string.Empty;
        }

        public IReadOnlyList<ViewToggleTabState> Tabs { get; }

        public string SelectedTabId { get; }
    }

    internal sealed class ViewToggleTabs : VisualElement
    {
        private const string RootClassName = "ee4v-ui-view-toggle-tabs";
        private const string TabClassName = "ee4v-ui-view-toggle-tabs__tab";
        private const string TabSelectedClassName = "ee4v-ui-view-toggle-tabs__tab--selected";
        private const string TabContentClassName = "ee4v-ui-view-toggle-tabs__tab-content";
        private const string TabLabelClassName = "ee4v-ui-view-toggle-tabs__label";
        private readonly List<TabView> _tabViews = new List<TabView>();
        private string _selectedTabId = string.Empty;

        public ViewToggleTabs(ViewToggleTabsState state = null)
        {
            AddToClassList(RootClassName);
            SetState(state ?? new ViewToggleTabsState(null, string.Empty));
        }

        public event Action<string> SelectionChanged;

        public string SelectedTabId
        {
            get { return _selectedTabId; }
        }

        public void SetState(ViewToggleTabsState state)
        {
            state = state ?? new ViewToggleTabsState(null, string.Empty);
            _selectedTabId = NormalizeSelectedTabId(state);
            _tabViews.Clear();
            Clear();

            for (var i = 0; i < state.Tabs.Count; i++)
            {
                var tab = state.Tabs[i];
                if (tab == null)
                {
                    continue;
                }

                var tabId = tab.Id;
                var button = CreateTabButton(tab.Label, () => SetSelectedTab(tabId));
                button.SetEnabled(tab.Enabled);
                Add(button);
                _tabViews.Add(new TabView(tabId, button));
            }

            RefreshSelectionVisuals();
        }

        public void SetSelectedTab(string tabId, bool notify = true)
        {
            tabId = tabId ?? string.Empty;
            var tabView = FindTabView(tabId);
            if (tabView == null || !tabView.Button.enabledSelf)
            {
                return;
            }

            if (string.Equals(_selectedTabId, tabId, StringComparison.Ordinal))
            {
                return;
            }

            _selectedTabId = tabId;
            RefreshSelectionVisuals();

            if (notify)
            {
                SelectionChanged?.Invoke(_selectedTabId);
            }
        }

        private static Button CreateTabButton(string labelText, Action onClick)
        {
            var button = new Button(onClick);
            button.AddToClassList(TabClassName);

            var content = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            content.AddToClassList(TabContentClassName);

            var label = UiTextFactory.Create(labelText);
            label.AddToClassList(TabLabelClassName);
            label.SetWhiteSpace(WhiteSpace.NoWrap);
            label.pickingMode = PickingMode.Ignore;

            content.Add(label);
            button.Add(content);
            return button;
        }

        private string NormalizeSelectedTabId(ViewToggleTabsState state)
        {
            var fallback = string.Empty;

            for (var i = 0; i < state.Tabs.Count; i++)
            {
                var tab = state.Tabs[i];
                if (tab == null || !tab.Enabled)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(fallback))
                {
                    fallback = tab.Id;
                }

                if (string.Equals(tab.Id, state.SelectedTabId, StringComparison.Ordinal))
                {
                    return tab.Id;
                }
            }

            return string.IsNullOrEmpty(fallback) ? state.SelectedTabId : fallback;
        }

        private TabView FindTabView(string tabId)
        {
            for (var i = 0; i < _tabViews.Count; i++)
            {
                var view = _tabViews[i];
                if (string.Equals(view.Id, tabId, StringComparison.Ordinal))
                {
                    return view;
                }
            }

            return null;
        }

        private void RefreshSelectionVisuals()
        {
            for (var i = 0; i < _tabViews.Count; i++)
            {
                var view = _tabViews[i];
                view.Button.EnableInClassList(
                    TabSelectedClassName,
                    string.Equals(view.Id, _selectedTabId, StringComparison.Ordinal));
            }
        }

        private sealed class TabView
        {
            public TabView(string id, Button button)
            {
                Id = id ?? string.Empty;
                Button = button;
            }

            public string Id { get; }

            public Button Button { get; }
        }
    }
}
