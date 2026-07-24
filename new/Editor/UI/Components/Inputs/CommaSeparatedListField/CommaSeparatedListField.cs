using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class CommaSeparatedListFieldState
    {
        public CommaSeparatedListFieldState(
            IReadOnlyList<string> values,
            string tooltip = null,
            string itemPlaceholder = null)
        {
            Values = values ?? Array.Empty<string>();
            Tooltip = tooltip ?? string.Empty;
            ItemPlaceholder = itemPlaceholder ?? string.Empty;
        }

        public IReadOnlyList<string> Values { get; }

        public string Tooltip { get; }

        public string ItemPlaceholder { get; }

    }

    internal sealed class CommaSeparatedListField : VisualElement
    {
        private const string RootClassName =
            "ee4v-ui-comma-separated-list-field";
        private const string EditorClassName =
            "ee4v-ui-comma-separated-list-field__editor";
        private static readonly char[] InputSeparators =
        {
            ',',
            ';',
            '\r',
            '\n'
        };
        private const int MaximumVisibleLineCount = 6;

        private readonly InputField _editor;
        private bool _isUpdating;

        public CommaSeparatedListField(
            CommaSeparatedListFieldState state = null)
        {
            AddToClassList(RootClassName);

            _editor = new InputField();
            _editor.AddToClassList(EditorClassName);
            _editor.ValueChanged += OnEditorValueChanged;
            Add(_editor);

            SetState(
                state ??
                new CommaSeparatedListFieldState(
                    Array.Empty<string>()));
        }

        public event Action<IReadOnlyList<string>> ValuesChanged;

        public IReadOnlyList<string> Values
        {
            get { return ParseItems(_editor.Value); }
            set { SetItems(value); }
        }

        internal int ItemCount
        {
            get { return Values.Count; }
        }

        internal IReadOnlyList<string> ItemValues
        {
            get { return Values; }
        }

        public void SetState(CommaSeparatedListFieldState state)
        {
            state = state ??
                new CommaSeparatedListFieldState(
                    Array.Empty<string>());
            tooltip = state.Tooltip;
            _editor.tooltip = state.Tooltip;
            _editor.SetState(
                new InputFieldState(
                    ToMultilineValue(state.Values),
                    multiline: true,
                    maxHeight:
                        UiSizeTokens.ControlHeightDefault *
                        MaximumVisibleLineCount,
                    placeholder: state.ItemPlaceholder));
        }

        private void SetItems(IEnumerable<string> values)
        {
            _isUpdating = true;
            _editor.SetValueWithoutNotify(
                ToMultilineValue(values));
            _isUpdating = false;
        }

        private void OnEditorValueChanged(string value)
        {
            if (_isUpdating)
            {
                return;
            }

            var values = ParseItems(value);
            if (!string.IsNullOrEmpty(value) &&
                (value.IndexOf(',') >= 0 ||
                 value.IndexOf(';') >= 0))
            {
                _isUpdating = true;
                _editor.SetValueWithoutNotify(
                    ToMultilineValue(values));
                _isUpdating = false;
            }

            ValuesChanged?.Invoke(values);
        }

        private static string ToMultilineValue(
            IEnumerable<string> values)
        {
            return string.Join(
                "\n",
                (values ?? Array.Empty<string>())
                    .SelectMany(ParseItems));
        }

        private static IReadOnlyList<string> ParseItems(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }

            return value
                .Split(
                    InputSeparators,
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .ToArray();
        }
    }
}
