using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class SearchField : VisualElement
    {
        private readonly TextField _input;
        private readonly Button _clearButton;
        private Action<string> _onValueChanged;

        public SearchField(string value = "", Action<string> onValueChanged = null)
        {
            AddToClassList(UiClassNames.SearchField);
            style.minHeight = 18f;
            style.height = 18f;

            _input = new TextField();
            _input.AddToClassList(UiClassNames.SearchFieldInput);
            _input.style.minHeight = 0f;
            _input.style.height = 14f;
            _input.style.marginTop = 0f;
            _input.style.marginBottom = 0f;
            _input.RegisterValueChangedCallback(evt =>
            {
                RefreshVisualState(evt.newValue);
                if (_onValueChanged != null)
                {
                    _onValueChanged(evt.newValue ?? string.Empty);
                }
            });

            _clearButton = new Button(() =>
            {
                if (string.IsNullOrEmpty(_input.value))
                {
                    return;
                }

                _input.value = string.Empty;
            })
            {
                text = "X"
            };
            _clearButton.AddToClassList(UiClassNames.SearchFieldClear);
            _clearButton.style.width = 12f;
            _clearButton.style.minWidth = 12f;
            _clearButton.style.maxWidth = 12f;
            _clearButton.style.height = 12f;
            _clearButton.style.minHeight = 12f;
            _clearButton.style.maxHeight = 12f;

            Add(_input);
            Add(_clearButton);

            SetState(value, onValueChanged);
        }

        public string Value
        {
            get { return _input.value ?? string.Empty; }
        }

        public void SetState(string value, Action<string> onValueChanged = null)
        {
            _onValueChanged = onValueChanged;
            _input.SetValueWithoutNotify(value ?? string.Empty);
            _input.tooltip = "Search";
            RefreshVisualState(_input.value);
        }

        private void RefreshVisualState(string value)
        {
            var hasValue = !string.IsNullOrWhiteSpace(value);
            EnableInClassList(UiClassNames.SearchFieldHasValue, hasValue);
            _clearButton.style.display = hasValue ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
