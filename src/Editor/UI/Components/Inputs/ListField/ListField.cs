using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class ListFieldState<T>
    {
        public ListFieldState(
            IReadOnlyList<T> values,
            Func<T, Action<T>, VisualElement> createItem,
            Func<T> createNewItem = null,
            Func<T, bool> isEmpty = null,
            Action<VisualElement> focusItem = null,
            string tooltip = null,
            string addItemLabel = null,
            string removeItemTooltip = null)
        {
            Values = values ?? Array.Empty<T>();
            CreateItem = createItem ??
                throw new ArgumentNullException(nameof(createItem));
            CreateNewItem = createNewItem;
            IsEmpty = isEmpty ?? (_ => false);
            FocusItem = focusItem;
            Tooltip = tooltip ?? string.Empty;
            AddItemLabel = addItemLabel ?? string.Empty;
            RemoveItemTooltip = removeItemTooltip ?? string.Empty;
        }

        public IReadOnlyList<T> Values { get; }

        public Func<T, Action<T>, VisualElement> CreateItem { get; }

        public Func<T> CreateNewItem { get; }

        public Func<T, bool> IsEmpty { get; }

        public Action<VisualElement> FocusItem { get; }

        public string Tooltip { get; }

        public string AddItemLabel { get; }

        public string RemoveItemTooltip { get; }
    }

    public sealed class ListField<T> : VisualElement
    {
        private const string RootClassName =
            "ee4v-ui-list-field";
        private const string ItemClassName =
            "ee4v-ui-list-field__item";
        private const string ListClassName =
            "ee4v-ui-list-field__list";
        private const string RowClassName =
            "ee4v-ui-list-field__row";
        private const string AddButtonClassName =
            "ee4v-ui-list-field__add-button";
        private const string ActionSpacerClassName =
            "ee4v-ui-list-field__action-spacer";
        private const string RemoveButtonClassName =
            "ee4v-ui-list-field__remove-button";
        private readonly ScrollView _list;
        private readonly VisualElement _addRow;
        private readonly UiButton _addButton;
        private readonly List<ItemRow> _rows =
            new List<ItemRow>();
        private Func<T, Action<T>, VisualElement> _createItem;
        private Func<T> _createNewItem;
        private Func<T, bool> _isEmpty;
        private Action<VisualElement> _focusItem;
        private bool _isUpdating;
        private string _itemTooltip = string.Empty;
        private string _removeItemTooltip = string.Empty;

        public ListField(ListFieldState<T> state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

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

            SetState(state);
        }

        public event Action<IReadOnlyList<T>> ValuesChanged;

        public IReadOnlyList<T> Values
        {
            get
            {
                return _rows
                    .Select(row => row.Value)
                    .Where(value => !_isEmpty(value))
                    .ToArray();
            }
            set { SetItems(value); }
        }

        public void SetState(ListFieldState<T> state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            _createItem = state.CreateItem;
            _createNewItem = state.CreateNewItem;
            _isEmpty = state.IsEmpty;
            _focusItem = state.FocusItem;
            tooltip = state.Tooltip;
            _itemTooltip = state.Tooltip;
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

        private void SetItems(IEnumerable<T> values)
        {
            _isUpdating = true;
            _rows.Clear();
            _list.contentContainer.Clear();

            var items = (values ?? Array.Empty<T>())
                .Where(value => !_isEmpty(value))
                .ToArray();
            for (var index = 0; index < items.Length; index++)
            {
                InsertRow(index, items[index]);
            }

            if (_createNewItem != null)
            {
                if (_rows.Count == 0)
                {
                    InsertRow(0, _createNewItem());
                }

                _list.contentContainer.Add(_addRow);
            }

            RefreshRemoveButtons();
            _isUpdating = false;
        }

        private ItemRow InsertRow(int index, T value)
        {
            var root = new VisualElement();
            root.AddToClassList(RowClassName);

            var row = new ItemRow(root, value);
            var item = _createItem(
                value,
                nextValue => OnItemValueChanged(row, nextValue));
            if (item == null)
            {
                throw new InvalidOperationException(
                    "ListField item factory returned null.");
            }

            item.AddToClassList(ItemClassName);
            item.tooltip = _itemTooltip;
            row.Item = item;
            root.Add(item);

            if (_createNewItem != null)
            {
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
                row.RemoveButton = removeButton;
                root.Add(removeButton);
            }

            var safeIndex = Math.Max(
                0,
                Math.Min(index, _rows.Count));
            _rows.Insert(safeIndex, row);
            _list.contentContainer.Insert(safeIndex, root);
            return row;
        }

        private void AddItem()
        {
            if (_createNewItem == null)
            {
                return;
            }

            var added = InsertRow(_rows.Count, _createNewItem());
            RefreshRemoveButtons();
            Focus(added);
        }

        private void Remove(ItemRow row)
        {
            if (_createNewItem == null)
            {
                return;
            }

            var index = _rows.IndexOf(row);
            if (index < 0)
            {
                return;
            }

            _rows.RemoveAt(index);
            row.Root.RemoveFromHierarchy();
            if (_rows.Count == 0)
            {
                InsertRow(0, _createNewItem());
            }

            RefreshRemoveButtons();
            NotifyValuesChanged();
            Focus(_rows[Math.Min(index, _rows.Count - 1)]);
        }

        private void OnItemValueChanged(ItemRow row, T value)
        {
            row.Value = value;
            if (_isUpdating)
            {
                return;
            }

            RefreshRemoveButtons();
            NotifyValuesChanged();
        }

        private void RefreshRemoveButtons()
        {
            var onlyEmptyRow =
                _rows.Count == 1 &&
                _isEmpty(_rows[0].Value);
            for (var index = 0; index < _rows.Count; index++)
            {
                _rows[index].RemoveButton?.SetEnabled(!onlyEmptyRow);
            }
        }

        private void Focus(ItemRow row)
        {
            if (_focusItem != null)
            {
                _focusItem(row.Item);
                return;
            }

            row.Item.Focus();
        }

        private void NotifyValuesChanged()
        {
            ValuesChanged?.Invoke(Values);
        }

        private sealed class ItemRow
        {
            public ItemRow(VisualElement root, T value)
            {
                Root = root;
                Value = value;
            }

            public VisualElement Root { get; }

            public VisualElement Item { get; set; }

            public UiButton RemoveButton { get; set; }

            public T Value { get; set; }
        }
    }
}
