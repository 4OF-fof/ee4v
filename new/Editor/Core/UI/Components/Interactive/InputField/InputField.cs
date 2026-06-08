using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class InputFieldState
    {
        public InputFieldState(string value = null, string label = null, bool multiline = false)
        {
            Value = value ?? string.Empty;
            Label = label ?? string.Empty;
            Multiline = multiline;
        }

        public string Value { get; }

        public string Label { get; }

        public bool Multiline { get; }
    }

    internal sealed class InputField : VisualElement
    {
        private const string RootClassName = "ee4v-ui-input-field";
        private const string MultilineClassName = "ee4v-ui-input-field--multiline";
        private const string LabelClassName = "ee4v-ui-input-field__label";
        private const string FieldContainerClassName = "ee4v-ui-input-field__field-container";
        private const string FieldContainerFocusedClassName = "ee4v-ui-input-field__field-container--focused";
        private const string FieldClassName = "ee4v-ui-input-field__field";
        private readonly UiTextElement _label;
        private readonly VisualElement _fieldContainer;
        private readonly TextField _textField;
        private bool _isFocused;

        public InputField(InputFieldState state = null)
        {
            AddToClassList(RootClassName);

            _label = UiTextFactory.Create(string.Empty, LabelClassName);
            _label.SetWhiteSpace(WhiteSpace.NoWrap);

            _fieldContainer = new VisualElement();
            _fieldContainer.AddToClassList(FieldContainerClassName);

            _textField = new TextField();
            _textField.AddToClassList(FieldClassName);
            _textField.RegisterValueChangedCallback(evt =>
            {
                ValueChanged?.Invoke(evt.newValue ?? string.Empty);
            });
            _textField.RegisterCallback<FocusInEvent>(_ =>
            {
                _isFocused = true;
                RefreshVisualState();
            });
            _textField.RegisterCallback<FocusOutEvent>(_ =>
            {
                _isFocused = false;
                RefreshVisualState();
            });

            _fieldContainer.Add(_textField);
            Add(_label);
            Add(_fieldContainer);

            SetState(state ?? new InputFieldState());
        }

        public event Action<string> ValueChanged;

        public string Value
        {
            get { return _textField.value ?? string.Empty; }
            set { _textField.value = value ?? string.Empty; }
        }

        public void SetState(InputFieldState state)
        {
            state = state ?? new InputFieldState();
            SetLabel(state.Label);
            SetMultiline(state.Multiline);
            SetValueWithoutNotify(state.Value);
        }

        public void SetLabel(string label)
        {
            _label.SetText(label);
            _label.style.display = string.IsNullOrWhiteSpace(label) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void SetValueWithoutNotify(string value)
        {
            _textField.SetValueWithoutNotify(value ?? string.Empty);
            RefreshVisualState();
        }

        public void SetMinHeight(float minHeight)
        {
            var safeHeight = Mathf.Max(48f, minHeight);
            _fieldContainer.style.minHeight = safeHeight;
            _textField.style.minHeight = safeHeight - 2f;
        }

        private void SetMultiline(bool multiline)
        {
            _textField.multiline = multiline;
            EnableInClassList(MultilineClassName, multiline);
        }

        private void RefreshVisualState()
        {
            _fieldContainer.EnableInClassList(FieldContainerFocusedClassName, _isFocused);
        }
    }
}
