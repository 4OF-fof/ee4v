using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class TabCardTabState
    {
        public TabCardTabState(string id, string label, bool enabled = true)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Enabled = enabled;
        }

        public string Id { get; }

        public string Label { get; }

        public bool Enabled { get; }
    }

    internal sealed class TabCardState
    {
        public TabCardState(IReadOnlyList<TabCardTabState> tabs, string selectedTabId)
        {
            Tabs = tabs ?? new TabCardTabState[0];
            SelectedTabId = selectedTabId ?? string.Empty;
        }

        public IReadOnlyList<TabCardTabState> Tabs { get; }

        public string SelectedTabId { get; }
    }

    internal sealed class TabCard : VisualElement
    {
        private const string RootClassName = "ee4v-ui-tab-card";
        private const string TabsClassName = "ee4v-ui-tab-card__tabs";
        private const string TabClassName = "ee4v-ui-tab-card__tab";
        private const string TabSelectedClassName = "ee4v-ui-tab-card__tab--selected";
        private const string PanelClassName = "ee4v-ui-tab-card__panel";
        private const string ContentClassName = "ee4v-ui-tab-card__content";
        private readonly VisualElement _tabsRow;
        private readonly VisualElement _panel;
        private readonly VisualElement _content;
        private Action<string> _onSelect;

        public TabCard(TabCardState state = null, Action<string> onSelect = null)
        {
            AddToClassList(RootClassName);

            _tabsRow = new VisualElement();
            _tabsRow.AddToClassList(TabsClassName);

            _panel = new VisualElement();
            _panel.AddToClassList(PanelClassName);

            _content = new VisualElement();
            _content.AddToClassList(ContentClassName);

            _panel.Add(_content);
            Add(_tabsRow);
            Add(_panel);

            SetState(state ?? new TabCardState(null, string.Empty), onSelect);
        }

        public VisualElement Content
        {
            get { return _content; }
        }

        public void SetState(TabCardState state, Action<string> onSelect = null)
        {
            state = state ?? new TabCardState(null, string.Empty);
            if (onSelect != null)
            {
                _onSelect = onSelect;
            }

            _tabsRow.Clear();

            for (var i = 0; i < state.Tabs.Count; i++)
            {
                var tab = state.Tabs[i];
                var button = new Button(() =>
                {
                    if (_onSelect != null)
                    {
                        _onSelect(tab.Id);
                    }
                })
                {
                    text = tab.Label
                };
                button.AddToClassList(TabClassName);
                button.EnableInClassList(TabSelectedClassName, string.Equals(tab.Id, state.SelectedTabId, StringComparison.Ordinal));
                button.SetEnabled(tab.Enabled);
                _tabsRow.Add(button);
            }
        }
    }
}
