using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class ExclusiveButtonOption<TValue>
    {
        public ExclusiveButtonOption(TValue value, string label)
        {
            Value = value;
            Label = label ?? string.Empty;
        }

        public TValue Value { get; }
        public string Label { get; }
    }

    public sealed class ExclusiveButtonGroup<TValue> : VisualElement
    {
        private readonly List<(TValue Value, VisualElement Button, Action<bool> SetSelected)> _buttons =
            new List<(TValue, VisualElement, Action<bool>)>();
        private readonly IEqualityComparer<TValue> _comparer;

        public ExclusiveButtonGroup(IReadOnlyList<ExclusiveButtonOption<TValue>> options,
            TValue selected, Action<TValue> select = null, Func<TValue, bool> available = null,
            IEqualityComparer<TValue> comparer = null, SelectionTabVariant? tabVariant = null)
        {
            if (options == null) { throw new ArgumentNullException(nameof(options)); }
            if (options.Count == 0) { throw new ArgumentException("At least one option is required.", nameof(options)); }
            _comparer = comparer ?? EqualityComparer<TValue>.Default;
            AddToClassList(tabVariant.HasValue ? "ee4v-ui-selection-tabs" : "ee4v-ui-exclusive-button-group");
            foreach (var option in options)
            {
                if (option == null || _buttons.Exists(pair => _comparer.Equals(pair.Value, option.Value)))
                { throw new ArgumentException("Options must have unique values.", nameof(options)); }
                var value = option.Value;
                Action click = () =>
                {
                    var previous = SelectedValue;
                    SetSelected(value);
                    try { select?.Invoke(value); }
                    catch { SetSelected(previous); throw; }
                };
                VisualElement button;
                Action<bool> setSelected;
                if (tabVariant.HasValue)
                {
                    var tab = new SelectionTab(option.Label, tabVariant.Value, click);
                    button = tab;
                    setSelected = tab.SetSelected;
                }
                else
                {
                    button = new UiButton(option.Label, click, variant: UiButtonVariant.Ghost);
                    button.AddToClassList("ee4v-ui-exclusive-button-group__button");
                    setSelected = active => button.EnableInClassList(
                        "ee4v-ui-exclusive-button-group__button--active", active);
                }
                button.userData = value;
                button.SetEnabled(available == null || available(value));
                _buttons.Add((value, button, setSelected));
                Add(button);
            }
            SetSelected(selected);
        }

        public TValue SelectedValue { get; private set; }

        public void SetSelected(TValue selected)
        {
            if (!_buttons.Exists(pair => _comparer.Equals(pair.Value, selected)))
            { throw new ArgumentException("The selected value must belong to the options.", nameof(selected)); }
            SelectedValue = selected;
            foreach (var pair in _buttons)
            {
                pair.SetSelected(_comparer.Equals(pair.Value, selected));
            }
        }

        public void Refresh(TValue selected, Func<TValue, bool> available = null)
        {
            SetSelected(selected);
            foreach (var pair in _buttons)
            { pair.Button.SetEnabled(available == null || available(pair.Value)); }
        }
    }
}
