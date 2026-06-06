using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class ItemGridState
    {
        public ItemGridState(IReadOnlyList<ItemCardState> items)
        {
            Items = items ?? Array.Empty<ItemCardState>();
        }

        public IReadOnlyList<ItemCardState> Items { get; }
    }

    internal sealed class ItemGrid : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-item-grid";
        private const string ListClassName = "ee4v-asset-manager-item-grid__list";
        private const string RowClassName = "ee4v-asset-manager-item-grid__row";
        private const string RowSlotClassName = "ee4v-asset-manager-item-grid__row-slot";
        private const float CardWidth = 132f;
        private const float ColumnGap = 16f;
        private const int RowHeight = 188;
        private readonly ListView _listView;
        private readonly List<ItemGridRowState> _rows = new List<ItemGridRowState>();
        private IReadOnlyList<ItemCardState> _items = Array.Empty<ItemCardState>();
        private int _columnCount = 1;

        public ItemGrid(ItemGridState state = null)
        {
            AddToClassList(RootClassName);

            _listView = new ListView();
            _listView.AddToClassList(ListClassName);
            _listView.selectionType = SelectionType.None;
            _listView.fixedItemHeight = RowHeight;
            _listView.makeItem = MakeRow;
            _listView.bindItem = BindRow;
            _listView.itemsSource = _rows;
            Add(_listView);

            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            SetState(state ?? new ItemGridState(null));
        }

        public void SetState(ItemGridState state)
        {
            var nextState = state ?? new ItemGridState(null);
            _items = nextState.Items ?? Array.Empty<ItemCardState>();
            RebuildRows();
        }

        private VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList(RowClassName);

            for (var i = 0; i < _columnCount; i++)
            {
                var slot = new VisualElement();
                slot.AddToClassList(RowSlotClassName);
                slot.Add(new ItemCard());
                row.Add(slot);
            }

            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            var rowState = _rows[index];
            EnsureRowSlotCount(element);
            for (var i = 0; i < _columnCount; i++)
            {
                var slot = element.ElementAt(i);
                var itemCard = slot.ElementAt(0) as ItemCard;
                var hasItem = i < rowState.Items.Count && rowState.Items[i] != null;
                slot.style.visibility = hasItem ? Visibility.Visible : Visibility.Hidden;
                if (hasItem && itemCard != null)
                {
                    itemCard.SetState(rowState.Items[i]);
                }
            }
        }

        private void EnsureRowSlotCount(VisualElement row)
        {
            while (row.childCount < _columnCount)
            {
                var slot = new VisualElement();
                slot.AddToClassList(RowSlotClassName);
                slot.Add(new ItemCard());
                row.Add(slot);
            }

            while (row.childCount > _columnCount)
            {
                row.RemoveAt(row.childCount - 1);
            }
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            var width = evt.newRect.width;
            if (float.IsNaN(width) || width <= 0f)
            {
                return;
            }

            var nextColumnCount = Mathf.Max(1, Mathf.FloorToInt((width + ColumnGap) / (CardWidth + ColumnGap)));
            if (nextColumnCount == _columnCount)
            {
                return;
            }

            _columnCount = nextColumnCount;
            RebuildRows();
        }

        private void RebuildRows()
        {
            _rows.Clear();
            for (var i = 0; i < _items.Count; i += _columnCount)
            {
                var rowItems = new List<ItemCardState>(_columnCount);
                for (var column = 0; column < _columnCount; column++)
                {
                    var itemIndex = i + column;
                    rowItems.Add(itemIndex < _items.Count ? _items[itemIndex] : null);
                }

                _rows.Add(new ItemGridRowState(rowItems));
            }

            _listView.Rebuild();
        }

        private sealed class ItemGridRowState
        {
            public ItemGridRowState(IReadOnlyList<ItemCardState> items)
            {
                Items = items ?? Array.Empty<ItemCardState>();
            }

            public IReadOnlyList<ItemCardState> Items { get; }
        }
    }
}
