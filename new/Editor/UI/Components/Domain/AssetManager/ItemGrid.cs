using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class ItemGridState
    {
        public ItemGridState(IReadOnlyList<ItemCardState> items, int itemsPerRow = 6)
        {
            Items = items ?? Array.Empty<ItemCardState>();
            ItemsPerRow = Mathf.Max(1, itemsPerRow);
        }

        public IReadOnlyList<ItemCardState> Items { get; }

        public int ItemsPerRow { get; }
    }

    internal sealed class ItemGrid : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-item-grid";
        private const string ListClassName = "ee4v-asset-manager-item-grid__list";
        private const string RowClassName = "ee4v-asset-manager-item-grid__row";
        private const string RowSlotClassName = "ee4v-asset-manager-item-grid__row-slot";
        private const float ColumnGap = 16f;
        private const float RowVerticalPadding = 4f;
        private const float NameHeight = 25f;
        private const int DefaultRowHeight = 161;
        private readonly ListView _listView;
        private readonly List<ItemGridRowState> _rows = new List<ItemGridRowState>();
        private IReadOnlyList<ItemCardState> _items = Array.Empty<ItemCardState>();
        private int _itemsPerRow = 6;
        private float _cardWidth = 132f;

        public ItemGrid(ItemGridState state = null)
        {
            AddToClassList(RootClassName);

            _listView = new ListView();
            _listView.AddToClassList(ListClassName);
            _listView.selectionType = SelectionType.None;
            _listView.fixedItemHeight = DefaultRowHeight;
            _listView.makeItem = MakeRow;
            _listView.bindItem = BindRow;
            _listView.itemsSource = _rows;
            Add(_listView);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            SetState(state ?? new ItemGridState(null));
        }

        public void SetState(ItemGridState state)
        {
            var nextState = state ?? new ItemGridState(null);
            _items = nextState.Items ?? Array.Empty<ItemCardState>();
            _itemsPerRow = Mathf.Max(1, nextState.ItemsPerRow);
            RecalculateCardWidth(resolvedStyle.width);
            RebuildRows();
        }

        private VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList(RowClassName);

            for (var i = 0; i < _itemsPerRow; i++)
            {
                var slot = CreateSlot(i);
                row.Add(slot);
            }

            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            var rowState = _rows[index];
            EnsureRowSlotCount(element);
            for (var i = 0; i < _itemsPerRow; i++)
            {
                var slot = element.ElementAt(i);
                var itemCard = slot.ElementAt(0) as ItemCard;
                var hasItem = i < rowState.Items.Count && rowState.Items[i] != null;
                ApplySlotWidth(slot, i);
                slot.style.visibility = hasItem ? Visibility.Visible : Visibility.Hidden;
                if (hasItem && itemCard != null)
                {
                    itemCard.SetWidth(_cardWidth);
                    itemCard.SetState(rowState.Items[i]);
                }
            }
        }

        private void EnsureRowSlotCount(VisualElement row)
        {
            while (row.childCount < _itemsPerRow)
            {
                row.Add(CreateSlot(row.childCount));
            }

            while (row.childCount > _itemsPerRow)
            {
                row.RemoveAt(row.childCount - 1);
            }
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            HideScrollbars();
            if (!RecalculateCardWidth(evt.newRect.width))
            {
                return;
            }

            RebuildRows();
        }

        private void RebuildRows()
        {
            _rows.Clear();
            var rowHeight = Mathf.CeilToInt(_cardWidth + NameHeight + RowVerticalPadding);
            _listView.fixedItemHeight = Mathf.Max(1, rowHeight);
            for (var i = 0; i < _items.Count; i += _itemsPerRow)
            {
                var rowItems = new List<ItemCardState>(_itemsPerRow);
                for (var column = 0; column < _itemsPerRow; column++)
                {
                    var itemIndex = i + column;
                    rowItems.Add(itemIndex < _items.Count ? _items[itemIndex] : null);
                }

                _rows.Add(new ItemGridRowState(rowItems));
            }

            _listView.Rebuild();
            HideScrollbars();
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            HideScrollbars();
            schedule.Execute(HideScrollbars);
        }

        private VisualElement CreateSlot(int index)
        {
            var slot = new VisualElement();
            slot.AddToClassList(RowSlotClassName);
            ApplySlotWidth(slot, index);
            var itemCard = new ItemCard();
            itemCard.SetWidth(_cardWidth);
            slot.Add(itemCard);
            return slot;
        }

        private void ApplySlotWidth(VisualElement slot, int index)
        {
            slot.style.width = _cardWidth;
            slot.style.minWidth = _cardWidth;
            slot.style.maxWidth = _cardWidth;
            slot.style.marginRight = index + 1 < _itemsPerRow ? ColumnGap : 0f;
        }

        private bool RecalculateCardWidth(float width)
        {
            if (float.IsNaN(width) || width <= 0f)
            {
                return false;
            }

            var availableWidth = Mathf.Max(1f, width - (ColumnGap * (_itemsPerRow - 1)));
            var nextCardWidth = Mathf.Floor(availableWidth / _itemsPerRow);
            if (Mathf.Approximately(nextCardWidth, _cardWidth))
            {
                return false;
            }

            _cardWidth = Mathf.Max(48f, nextCardWidth);
            return true;
        }

        private void HideScrollbars()
        {
            var scrollers = _listView.Query<Scroller>().ToList();
            for (var i = 0; i < scrollers.Count; i++)
            {
                scrollers[i].style.display = DisplayStyle.None;
                scrollers[i].style.visibility = Visibility.Hidden;
            }

            var scrollViews = _listView.Query<ScrollView>().ToList();
            for (var i = 0; i < scrollViews.Count; i++)
            {
                scrollViews[i].verticalScrollerVisibility = ScrollerVisibility.Hidden;
                scrollViews[i].horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            }
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
