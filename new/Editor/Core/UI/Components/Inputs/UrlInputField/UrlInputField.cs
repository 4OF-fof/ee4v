using System;
using Ee4v.Core.I18n;
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

    internal sealed class UrlInputField : InputField
    {
        private const string PlaceholderText = "https://";
        private const string RootClassName = "ee4v-ui-url-input-field";
        private const string HasTrailingClassName = "ee4v-ui-input-field--has-trailing";
        private const string OpenButtonClassName = "ee4v-ui-url-input-field__open-button";

        private readonly Button _openButton;

        public UrlInputField(UrlInputFieldState state = null)
            : base(new InputFieldState(null, false, 0f, PlaceholderText))
        {
            AddToClassList(RootClassName);
            AddToClassList(HasTrailingClassName);

            _openButton = new Button(OpenCurrentUrl)
            {
                text = "↗",
                tooltip = I18N.Get("ui.url.openTooltip")
            };
            _openButton.AddToClassList(OpenButtonClassName);
            FieldContainer.Add(_openButton);

            SetState(state ?? new UrlInputFieldState(string.Empty));
        }

        public override string Value
        {
            get { return base.Value; }
            set { base.Value = NormalizeUrl(value); }
        }

        public void SetState(UrlInputFieldState state)
        {
            state = state ?? new UrlInputFieldState(string.Empty);
            base.SetState(new InputFieldState(NormalizeUrl(state.Value), false, 0f, PlaceholderText));
            RefreshOpenButton();
        }

        public override void SetValueWithoutNotify(string value)
        {
            base.SetValueWithoutNotify(NormalizeUrl(value));
            RefreshOpenButton();
        }

        protected override void OnValueChanged(string value)
        {
            RefreshOpenButton();
        }

        protected override void OnFocusChanged(bool isFocused)
        {
            if (isFocused)
            {
                return;
            }

            SetValueWithoutNotify(Value);
            NotifyValueChanged(Value);
        }

        private void RefreshOpenButton()
        {
            if (_openButton == null)
            {
                return;
            }

            _openButton.style.display = IsOpenableUrl(Value) ? DisplayStyle.Flex : DisplayStyle.None;
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
