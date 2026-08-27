using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class StringListFieldState
    {
        public StringListFieldState(
            IReadOnlyList<string> values,
            string tooltip = null,
            string itemPlaceholder = null,
            string addItemLabel = null,
            string removeItemTooltip = null)
        {
            Values = values ?? Array.Empty<string>();
            Tooltip = tooltip ?? string.Empty;
            ItemPlaceholder = itemPlaceholder ?? string.Empty;
            AddItemLabel = addItemLabel ?? string.Empty;
            RemoveItemTooltip = removeItemTooltip ?? string.Empty;
        }

        public IReadOnlyList<string> Values { get; }

        public string Tooltip { get; }

        public string ItemPlaceholder { get; }

        public string AddItemLabel { get; }

        public string RemoveItemTooltip { get; }

    }

    public sealed class StringListField : VisualElement
    {
        private const string RootClassName =
            "ee4v-ui-string-list-field";
        private const string EditorClassName =
            "ee4v-ui-string-list-field__editor";
        private const string ListClassName =
            "ee4v-ui-string-list-field__list";
        private const string RowClassName =
            "ee4v-ui-string-list-field__row";
        private const string AddButtonClassName =
            "ee4v-ui-string-list-field__add-button";
        private const string ActionSpacerClassName =
            "ee4v-ui-string-list-field__action-spacer";
        private const string RemoveButtonClassName =
            "ee4v-ui-string-list-field__remove-button";
        private readonly ScrollView _list;
        private readonly VisualElement _addRow;
        private readonly UiButton _addButton;
        private readonly List<ItemEditorRow> _rows =
            new List<ItemEditorRow>();
        private bool _isUpdating;
        private string _itemTooltip = string.Empty;
        private string _itemPlaceholder = string.Empty;
        private string _removeItemTooltip = string.Empty;

        public StringListField(
            StringListFieldState state = null)
        {
            AddToClassList(RootClassName);

            _list = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility =
                    ScrollerVisibility.Hidden,
                verticalScrollerVisibility =
                    ScrollerVisibility.Auto
            };
            _list.AddToClassList(ListClassName);
            Add(_list);

            _addRow = new VisualElement();
            _addRow.AddToClassList(RowClassName);
            _addButton = new UiButton(
                string.Empty,
                AddItem,
                variant: UiButtonVariant.Ghost);
            _addButton.AddToClassList(AddButtonClassName);
            _addButton.SetLabelTextAlign(
                UnityEngine.TextAnchor.MiddleLeft);
            _addButton.SetContentAlignment(Justify.FlexStart);
            var actionSpacer = new VisualElement();
            actionSpacer.AddToClassList(ActionSpacerClassName);
            _addRow.Add(_addButton);
            _addRow.Add(actionSpacer);

            SetState(
                state ??
                new StringListFieldState(
                    Array.Empty<string>()));
        }

        public event Action<IReadOnlyList<string>> ValuesChanged;

        public IReadOnlyList<string> Values
        {
            get
            {
                return _rows
                    .Select(row => row.Editor.Value)
                    .Where(value => !string.IsNullOrEmpty(value))
                    .ToArray();
            }
            set { SetItems(value); }
        }

        public void SetState(StringListFieldState state)
        {
            state = state ??
                new StringListFieldState(
                    Array.Empty<string>());
            tooltip = state.Tooltip;
            _itemTooltip = state.Tooltip;
            _itemPlaceholder = state.ItemPlaceholder;
            _removeItemTooltip = state.RemoveItemTooltip;
            _addButton.SetLabel(state.AddItemLabel);
            _addButton.tooltip = state.AddItemLabel;
            _addButton.SetIcon(
                FluentUiIcons.CreateState(
                    "add.png",
                    UiSizeTokens.Size12,
                    state.AddItemLabel));
            SetItems(state.Values);
        }

        private void SetItems(IEnumerable<string> values)
        {
            _isUpdating = true;
            _rows.Clear();
            _list.contentContainer.Clear();

            var items = (values ?? Array.Empty<string>())
                .Select(value => value ?? string.Empty)
                .Where(value => value.Length > 0)
                .ToArray();
            for (var index = 0; index < items.Length; index++)
            {
                InsertRow(index, items[index]);
            }

            if (_rows.Count == 0)
            {
                InsertRow(0, string.Empty);
            }

            _list.contentContainer.Add(_addRow);
            RefreshRemoveButtons();
            _isUpdating = false;
        }

        private ItemEditorRow InsertRow(int index, string value)
        {
            var root = new VisualElement();
            root.AddToClassList(RowClassName);

            var editor = new InputField(
                new InputFieldState(
                    value,
                    placeholder: _itemPlaceholder));
            editor.AddToClassList(EditorClassName);
            editor.tooltip = _itemTooltip;

            ItemEditorRow row = null;
            var removeButton = new UiButton(
                string.Empty,
                () => Remove(row),
                _removeItemTooltip,
                FluentUiIcons.CreateState(
                    "dismiss.png",
                    UiSizeTokens.Size12,
                    _removeItemTooltip),
                UiButtonVariant.Ghost);
            removeButton.AddToClassList(RemoveButtonClassName);

            root.Add(editor);
            root.Add(removeButton);

            row = new ItemEditorRow(
                root,
                editor,
                removeButton);
            editor.ValueChanged += _ =>
            {
                if (_isUpdating)
                {
                    return;
                }

                RefreshRemoveButtons();
                NotifyValuesChanged();
            };

            var safeIndex = Math.Max(
                0,
                Math.Min(index, _rows.Count));
            _rows.Insert(safeIndex, row);
            _list.contentContainer.Insert(safeIndex, root);
            return row;
        }

        private void AddItem()
        {
            var added = InsertRow(_rows.Count, string.Empty);
            RefreshRemoveButtons();
            added.Editor.FocusInput();
        }

        private void Remove(ItemEditorRow row)
        {
            var index = _rows.IndexOf(row);
            if (index < 0)
            {
                return;
            }

            _rows.RemoveAt(index);
            row.Root.RemoveFromHierarchy();
            if (_rows.Count == 0)
            {
                InsertRow(0, string.Empty);
            }

            RefreshRemoveButtons();
            NotifyValuesChanged();
            _rows[Math.Min(index, _rows.Count - 1)]
                .Editor
                .FocusInput();
        }

        private void RefreshRemoveButtons()
        {
            var onlyEmptyRow =
                _rows.Count == 1 &&
                string.IsNullOrWhiteSpace(_rows[0].Editor.Value);
            for (var index = 0; index < _rows.Count; index++)
            {
                _rows[index]
                    .RemoveButton
                    .SetEnabled(!onlyEmptyRow);
            }
        }

        private void NotifyValuesChanged()
        {
            ValuesChanged?.Invoke(Values);
        }

        private sealed class ItemEditorRow
        {
            public ItemEditorRow(
                VisualElement root,
                InputField editor,
                UiButton removeButton)
            {
                Root = root;
                Editor = editor;
                RemoveButton = removeButton;
            }

            public VisualElement Root { get; }

            public InputField Editor { get; }

            public UiButton RemoveButton { get; }
        }
    }
}
