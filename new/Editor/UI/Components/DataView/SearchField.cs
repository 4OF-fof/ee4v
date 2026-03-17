using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class SearchFieldState
    {
        public SearchFieldState(string value = null, string placeholder = null)
        {
            Value = value ?? string.Empty;
            Placeholder = placeholder ?? string.Empty;
        }

        public string Value { get; }

        public string Placeholder { get; }
    }

    internal sealed class SearchField : VisualElement
    {
        private readonly Icon _searchIcon;
        private readonly VisualElement _inputHost;
        private readonly TextField _input;
        private readonly UiTextElement _placeholderLabel;
        private readonly Button _clearButton;
        private bool _isFocused;

        public SearchField(SearchFieldState state = null)
        {
            AddToClassList(UiClassNames.SearchField);

            _searchIcon = new Icon(IconState.FromBuiltinIcon(UiBuiltinIcon.Search, size: 14f, tooltip: "Search"));
            _searchIcon.AddToClassList(UiClassNames.SearchFieldIcon);

            _inputHost = new VisualElement();
            _inputHost.AddToClassList(UiClassNames.SearchFieldInputHost);

            _input = new TextField();
            _input.AddToClassList(UiClassNames.SearchFieldInput);
            _input.RegisterValueChangedCallback(evt =>
            {
                RefreshVisualState();
                ValueChanged?.Invoke(evt.newValue ?? string.Empty);
            });
            _input.RegisterCallback<FocusInEvent>(_ =>
            {
                _isFocused = true;
                RefreshVisualState();
            });
            _input.RegisterCallback<FocusOutEvent>(_ =>
            {
                _isFocused = false;
                RefreshVisualState();
            });

            _placeholderLabel = UiTextFactory.Create(string.Empty, UiClassNames.SearchFieldPlaceholder);
            _placeholderLabel.pickingMode = PickingMode.Ignore;

            _clearButton = new Button(ClearValue)
            {
                text = "X"
            };
            _clearButton.AddToClassList(UiClassNames.SearchFieldClear);

            _inputHost.Add(_input);
            _inputHost.Add(_placeholderLabel);

            Add(_searchIcon);
            Add(_inputHost);
            Add(_clearButton);

            SetState(state ?? new SearchFieldState());
        }

        public event Action<string> ValueChanged;

        public string Value
        {
            get { return _input.value ?? string.Empty; }
            set { _input.value = value ?? string.Empty; }
        }

        public void SetState(SearchFieldState state)
        {
            state = state ?? new SearchFieldState();
            SetValueWithoutNotify(state.Value);
            SetPlaceholder(state.Placeholder);
        }

        public void SetValueWithoutNotify(string value)
        {
            _input.SetValueWithoutNotify(value ?? string.Empty);
            RefreshVisualState();
        }

        public void SetPlaceholder(string placeholder)
        {
            _placeholderLabel.SetText(placeholder ?? string.Empty);
            RefreshVisualState();
        }

        public void ClearValue()
        {
            if (string.IsNullOrEmpty(Value))
            {
                return;
            }

            Value = string.Empty;
        }

        private void RefreshVisualState()
        {
            var hasValue = !string.IsNullOrWhiteSpace(Value);
            var showPlaceholder = !hasValue && !_isFocused && !string.IsNullOrWhiteSpace(_placeholderLabel.Text);

            EnableInClassList(UiClassNames.SearchFieldHasValue, hasValue);
            EnableInClassList(UiClassNames.SearchFieldFocused, _isFocused);
            _clearButton.style.display = hasValue ? DisplayStyle.Flex : DisplayStyle.None;
            _placeholderLabel.style.display = showPlaceholder ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
