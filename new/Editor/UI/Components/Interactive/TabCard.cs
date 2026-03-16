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
        private readonly VisualElement _tabsRow;
        private readonly VisualElement _panel;
        private readonly VisualElement _content;
        private Action<string> _onSelect;

        public TabCard(TabCardState state = null, Action<string> onSelect = null)
        {
            AddToClassList(UiClassNames.TabCard);

            _tabsRow = new VisualElement();
            _tabsRow.AddToClassList(UiClassNames.TabCardTabs);

            _panel = new VisualElement();
            _panel.AddToClassList(UiClassNames.TabCardPanel);

            _content = new VisualElement();
            _content.AddToClassList(UiClassNames.TabCardContent);

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
                button.AddToClassList(UiClassNames.TabCardTab);
                button.EnableInClassList(UiClassNames.TabCardTabSelected, string.Equals(tab.Id, state.SelectedTabId, StringComparison.Ordinal));
                button.SetEnabled(tab.Enabled);
                _tabsRow.Add(button);
            }
        }
    }
}
