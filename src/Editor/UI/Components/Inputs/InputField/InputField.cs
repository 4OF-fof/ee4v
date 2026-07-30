using System;
using Ee4v.Core.Internal.EditorAPI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class InputFieldState
    {
        public InputFieldState(string value = null, bool multiline = false, float maxHeight = 0f, string placeholder = null)
        {
            Value = value ?? string.Empty;
            Multiline = multiline;
            MaxHeight = Mathf.Max(0f, maxHeight);
            Placeholder = placeholder ?? string.Empty;
        }

        public string Value { get; }

        public bool Multiline { get; }

        public float MaxHeight { get; }

        public string Placeholder { get; }
    }

    internal class InputField : VisualElement
    {
        private const string RootClassName = "ee4v-ui-input-field";
        private const string MultilineClassName = "ee4v-ui-input-field--multiline";
        private const string FieldContainerClassName = "ee4v-ui-input-field__field-container";
        private const string FieldContainerFocusedClassName = "ee4v-ui-input-field__field-container--focused";
        private const string FieldClassName = "ee4v-ui-input-field__field";
        private const string PlaceholderClassName = "ee4v-ui-input-field__placeholder";
        private readonly VisualElement _fieldContainer;
        private readonly UiTextElement _placeholderLabel;
        private readonly TextField _textField;
        private bool _isFocused;
        private bool _multiline;
        private float _maxHeight;

        public InputField(InputFieldState state = null)
        {
            AddToClassList(RootClassName);

            _fieldContainer = new VisualElement();
            _fieldContainer.AddToClassList(FieldContainerClassName);

            _placeholderLabel = UiTextFactory.Create(
                string.Empty,
                UiClassNames.InputPlaceholder);
            _placeholderLabel.AddToClassList(PlaceholderClassName);
            _placeholderLabel.pickingMode = PickingMode.Ignore;

            _textField = new TextField();
            _textField.AddToClassList(FieldClassName);
            _textField.RegisterValueChangedCallback(evt =>
            {
                RefreshVisualState();
                OnValueChanged(evt.newValue ?? string.Empty);
                ValueChanged?.Invoke(evt.newValue ?? string.Empty);
            });
            _textField.RegisterCallback<FocusInEvent>(_ =>
            {
                _isFocused = true;
                RefreshVisualState();
                OnFocusChanged(true);
            });
            _textField.RegisterCallback<FocusOutEvent>(_ =>
            {
                _isFocused = false;
                RefreshVisualState();
                OnFocusChanged(false);
            });
            _textField.RegisterCallback<GeometryChangedEvent>(_ => RefreshScrollView());

            _fieldContainer.Add(_textField);
            _fieldContainer.Add(_placeholderLabel);
            Add(_fieldContainer);
            RegisterCallback<AttachToPanelEvent>(_ => ScheduleScrollViewRefresh());

            SetState(state ?? new InputFieldState());
        }

        public event Action<string> ValueChanged;

        public virtual string Value
        {
            get { return _textField.value ?? string.Empty; }
            set
            {
                _textField.value = value ?? string.Empty;
                RefreshVisualState();
            }
        }

        protected VisualElement FieldContainer
        {
            get { return _fieldContainer; }
        }

        public virtual void SetState(InputFieldState state)
        {
            state = state ?? new InputFieldState();
            SetMultiline(state.Multiline);
            SetMaxHeight(state.MaxHeight);
            SetPlaceholder(state.Placeholder);
            SetValueWithoutNotify(state.Value);
        }

        public virtual void SetValueWithoutNotify(string value)
        {
            _textField.SetValueWithoutNotify(value ?? string.Empty);
            RefreshVisualState();
        }

        public void FocusInput()
        {
            _textField.Focus();
        }

        public void SetPlaceholder(string placeholder)
        {
            _placeholderLabel.SetText(placeholder ?? string.Empty);
            RefreshVisualState();
        }

        public void SetMaxHeight(float maxHeight)
        {
            _maxHeight = Mathf.Max(0f, maxHeight);
            ApplyHeightConstraints();
        }

        private void SetMultiline(bool multiline)
        {
            if (_multiline == multiline)
            {
                return;
            }

            _multiline = multiline;
            _textField.multiline = multiline;
            EnableInClassList(MultilineClassName, multiline);
            ApplyHeightConstraints();
        }

        private void ApplyHeightConstraints()
        {
            if (!_multiline || _maxHeight <= 0f)
            {
                _fieldContainer.style.maxHeight = new StyleLength(StyleKeyword.Null);
                _textField.style.maxHeight = new StyleLength(StyleKeyword.Null);
                if (_multiline)
                {
                    RefreshScrollView();
                    ScheduleScrollViewRefresh();
                }

                return;
            }

            var safeHeight = Mathf.Max(48f, _maxHeight);
            var contentMaxHeight = Mathf.Max(36f, safeHeight - 12f);
            _fieldContainer.style.maxHeight = safeHeight;
            _textField.style.maxHeight = contentMaxHeight;
            RefreshScrollView();
            ScheduleScrollViewRefresh();
        }

        private void ScheduleScrollViewRefresh()
        {
            schedule.Execute(RefreshScrollView);
        }

        private void RefreshScrollView()
        {
            if (!_multiline)
            {
                return;
            }

            var useVerticalScroll = _maxHeight > 0f;
            var contentMaxHeight = Mathf.Max(36f, Mathf.Max(48f, _maxHeight) - 12f);
            TextFieldMultilineScroll.Configure(_textField, useVerticalScroll, contentMaxHeight);
        }

        protected virtual void OnValueChanged(string value)
        {
        }

        protected virtual void OnFocusChanged(bool isFocused)
        {
        }

        protected void NotifyValueChanged(string value)
        {
            ValueChanged?.Invoke(value ?? string.Empty);
        }

        protected void RefreshVisualState()
        {
            var hasValue = !string.IsNullOrEmpty(Value);
            _placeholderLabel.style.display = !hasValue && !_isFocused && !string.IsNullOrWhiteSpace(_placeholderLabel.Text)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _fieldContainer.EnableInClassList(FieldContainerFocusedClassName, _isFocused);
        }
    }
}
