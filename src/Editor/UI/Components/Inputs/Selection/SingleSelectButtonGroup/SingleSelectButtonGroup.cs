using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class SingleSelectButtonGroupItemState
    {
        public SingleSelectButtonGroupItemState(string id, string label, string meta = null, bool enabled = true, IconState iconState = null)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Meta = meta ?? string.Empty;
            Enabled = enabled;
            IconState = iconState;
        }

        public string Id { get; }

        public string Label { get; }

        public string Meta { get; }

        public bool Enabled { get; }

        public IconState IconState { get; }
    }

    internal sealed class SingleSelectButtonGroupState
    {
        public SingleSelectButtonGroupState(IReadOnlyList<SingleSelectButtonGroupItemState> items, string selectedItemId)
        {
            Items = items ?? Array.Empty<SingleSelectButtonGroupItemState>();
            SelectedItemId = selectedItemId ?? string.Empty;
        }

        public IReadOnlyList<SingleSelectButtonGroupItemState> Items { get; }

        public string SelectedItemId { get; }
    }

    internal sealed class SingleSelectButtonGroup : VisualElement
    {
        private const string RootClassName = "ee4v-ui-single-select-button-group";
        private const string ButtonClassName = "ee4v-ui-single-select-button-group__button";
        private readonly List<ButtonView> _buttonViews = new List<ButtonView>();
        private Action<string> _onSelect;
        private string _selectedItemId = string.Empty;

        public SingleSelectButtonGroup(SingleSelectButtonGroupState state = null, Action<string> onSelect = null)
        {
            AddToClassList(RootClassName);
            SetState(state ?? new SingleSelectButtonGroupState(null, string.Empty), onSelect);
        }

        public event Action<string> SelectionChanged;

        public string SelectedItemId
        {
            get { return _selectedItemId; }
        }

        public void SetState(SingleSelectButtonGroupState state, Action<string> onSelect = null)
        {
            state = state ?? new SingleSelectButtonGroupState(null, string.Empty);
            if (onSelect != null)
            {
                _onSelect = onSelect;
            }

            _selectedItemId = state.SelectedItemId;
            _buttonViews.Clear();
            Clear();

            for (var i = 0; i < state.Items.Count; i++)
            {
                var item = state.Items[i];
                if (item == null)
                {
                    continue;
                }

                var itemId = item.Id;
                var button = new UiButton(
                    new UiButtonState(
                        item.Label,
                        item.Meta,
                        iconState: item.IconState,
                        enabled: item.Enabled,
                        selected: string.Equals(
                            item.Id,
                            _selectedItemId,
                            StringComparison.Ordinal),
                        variant: UiButtonVariant.Ghost),
                    () => SelectItem(itemId));
                button.AddToClassList(ButtonClassName);

                Add(button);
                _buttonViews.Add(new ButtonView(itemId, button));
            }

            RefreshSelectionVisuals();
        }

        public void SetSelectedItem(string itemId, bool notify = true)
        {
            itemId = itemId ?? string.Empty;
            if (string.Equals(_selectedItemId, itemId, StringComparison.Ordinal))
            {
                return;
            }

            _selectedItemId = itemId;
            RefreshSelectionVisuals();

            if (notify)
            {
                SelectionChanged?.Invoke(_selectedItemId);
                _onSelect?.Invoke(_selectedItemId);
            }
        }

        private void SelectItem(string itemId)
        {
            SetSelectedItem(itemId, notify: true);
        }

        private void RefreshSelectionVisuals()
        {
            for (var i = 0; i < _buttonViews.Count; i++)
            {
                var view = _buttonViews[i];
                view.Button.SetSelected(
                    string.Equals(view.Id, _selectedItemId, StringComparison.Ordinal));
            }
        }

        private sealed class ButtonView
        {
            public ButtonView(string id, UiButton button)
            {
                Id = id ?? string.Empty;
                Button = button;
            }

            public string Id { get; }

            public UiButton Button { get; }
        }
    }
}
