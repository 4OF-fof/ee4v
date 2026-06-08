using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class UrlInputFieldState
    {
        public UrlInputFieldState(string value = null)
        {
            Value = value ?? string.Empty;
        }

        public string Value { get; }
    }

    internal sealed class UrlInputField : VisualElement
    {
        private const string PlaceholderText = "https://";
        private const string RootClassName = "ee4v-ui-url-input-field";
        private const string FieldContainerClassName = "ee4v-ui-url-input-field__field-container";
        private const string FieldClassName = "ee4v-ui-url-input-field__field";
        private const string PlaceholderClassName = "ee4v-ui-url-input-field__placeholder";
        private const string OpenButtonClassName = "ee4v-ui-url-input-field__open-button";

        private readonly Button _openButton;
        private readonly Label _placeholderLabel;
        private readonly TextField _textField;
        private bool _isFocused;

        public UrlInputField(UrlInputFieldState state = null)
        {
            AddToClassList(RootClassName);

            var fieldContainer = new VisualElement();
            fieldContainer.AddToClassList(FieldContainerClassName);

            _textField = new TextField();
            _textField.AddToClassList(FieldClassName);
            _textField.RegisterValueChangedCallback(evt =>
            {
                RefreshVisualState();
                ValueChanged?.Invoke(Value);
            });
            _textField.RegisterCallback<FocusInEvent>(_ =>
            {
                _isFocused = true;
                RefreshVisualState();
            });
            _textField.RegisterCallback<FocusOutEvent>(_ =>
            {
                _isFocused = false;
                NormalizeInputValue();
                RefreshVisualState();
                ValueChanged?.Invoke(Value);
            });

            _placeholderLabel = new Label();
            _placeholderLabel.AddToClassList(PlaceholderClassName);
            _placeholderLabel.pickingMode = PickingMode.Ignore;

            _openButton = new Button(OpenCurrentUrl)
            {
                text = "↗",
                tooltip = "Open URL"
            };
            _openButton.AddToClassList(OpenButtonClassName);

            fieldContainer.Add(_textField);
            fieldContainer.Add(_placeholderLabel);
            fieldContainer.Add(_openButton);
            Add(fieldContainer);

            SetState(state ?? new UrlInputFieldState(string.Empty));
        }

        public event Action<string> ValueChanged;

        public string Value
        {
            get { return _textField.value ?? string.Empty; }
            set { _textField.value = NormalizeUrl(value); }
        }

        public void SetState(UrlInputFieldState state)
        {
            state = state ?? new UrlInputFieldState(string.Empty);
            _placeholderLabel.text = PlaceholderText;
            _textField.SetValueWithoutNotify(NormalizeUrl(state.Value));
            RefreshVisualState();
        }

        public void SetValueWithoutNotify(string value)
        {
            _textField.SetValueWithoutNotify(NormalizeUrl(value));
            RefreshVisualState();
        }

        private void OpenCurrentUrl()
        {
            var url = NormalizeUrl(Value);
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            Application.OpenURL(url);
        }

        private void RefreshVisualState()
        {
            var hasValue = !string.IsNullOrWhiteSpace(Value);
            _placeholderLabel.style.display = !hasValue && !_isFocused && !string.IsNullOrWhiteSpace(_placeholderLabel.text)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _openButton.style.display = IsOpenableUrl(Value) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void NormalizeInputValue()
        {
            var normalized = NormalizeUrl(_textField.value);
            if (string.Equals(_textField.value ?? string.Empty, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _textField.SetValueWithoutNotify(normalized);
        }

        private static string NormalizeUrl(string value)
        {
            value = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }

            if (!LooksLikeDomain(value))
            {
                return value;
            }

            return "https://" + value;
        }

        private static bool LooksLikeDomain(string value)
        {
            return value.IndexOf('.') > 0 && value.IndexOf(' ') < 0;
        }

        private static bool IsOpenableUrl(string value)
        {
            value = (value ?? string.Empty).Trim();
            return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }
    }
}
