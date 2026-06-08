using System;
using Ee4v.Core.Internal.EditorAPI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class InputFieldState
    {
        public InputFieldState(string value = null, string label = null, bool multiline = false, float maxHeight = 0f)
        {
            Value = value ?? string.Empty;
            Label = label ?? string.Empty;
            Multiline = multiline;
            MaxHeight = Mathf.Max(0f, maxHeight);
        }

        public string Value { get; }

        public string Label { get; }

        public bool Multiline { get; }

        public float MaxHeight { get; }
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
        private bool _multiline;
        private float _maxHeight;

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
            _textField.RegisterCallback<GeometryChangedEvent>(_ => RefreshScrollView());

            _fieldContainer.Add(_textField);
            Add(_label);
            Add(_fieldContainer);
            RegisterCallback<AttachToPanelEvent>(_ => ScheduleScrollViewRefresh());

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
            SetMaxHeight(state.MaxHeight);
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
            ApplyHeightConstraints();
        }

        public void SetMaxHeight(float maxHeight)
        {
            _maxHeight = Mathf.Max(0f, maxHeight);
            ApplyHeightConstraints();
        }

        private void SetMultiline(bool multiline)
        {
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

        private void RefreshVisualState()
        {
            _fieldContainer.EnableInClassList(FieldContainerFocusedClassName, _isFocused);
        }
    }
}
