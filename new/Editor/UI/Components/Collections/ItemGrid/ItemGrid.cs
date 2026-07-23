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

    internal class ItemGrid : VisualElement
    {
        private const string RootClassName = "ee4v-ui-item-grid";
        private const string ListClassName = "ee4v-ui-item-grid__list";
        private const string RowClassName = "ee4v-ui-item-grid__row";
        protected const string RowSlotClassName = "ee4v-ui-item-grid__row-slot";
        private const float PreferredColumnGap = UiSpacingTokens.Xxl;
        private const float MinimumCardWidth = UiSizeTokens.Size1;
        private const float RowVerticalPadding = UiSpacingTokens.Xs;
        private const float NameHeight = 25f;
        private const float ViewportSafetyMargin = UiSizeTokens.Size1;
        private const int DefaultRowHeight = 161;
        protected readonly ListView ListView;
        private readonly List<ItemGridRowState> _rows = new List<ItemGridRowState>();
        private IReadOnlyList<ItemCardState> _items = Array.Empty<ItemCardState>();
        private int _itemsPerRow = 6;
        private float _cardWidth = 132f;
        private float _columnGap = PreferredColumnGap;
        private int _rowHeight = DefaultRowHeight;
        private float _viewportWidth;
        private float _viewportHeight;

        public ItemGrid(ItemGridState state = null)
        {
            AddToClassList(RootClassName);

            ListView = new ListView();
            ListView.AddToClassList(ListClassName);
            ListView.selectionType = SelectionType.None;
            ListView.fixedItemHeight = DefaultRowHeight;
            ListView.makeItem = MakeRow;
            ListView.bindItem = BindRow;
            ListView.itemsSource = _rows;
            ListView.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            Add(ListView);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            ApplyState(state ?? new ItemGridState(null));
        }

        public IReadOnlyList<ItemCardState> Items
        {
            get { return _items; }
        }

        public int ItemsPerRow
        {
            get { return _itemsPerRow; }
        }

        public virtual void SetState(ItemGridState state)
        {
            ApplyState(state);
        }

        public void SetItemsPerRow(int value)
        {
            var nextValue = Mathf.Max(1, value);
            if (_itemsPerRow == nextValue)
            {
                return;
            }

            _itemsPerRow = nextValue;
            RecalculateLayout(_viewportWidth, _viewportHeight);
            RebuildRows();
        }

        private void ApplyState(ItemGridState state)
        {
            var nextState = state ?? new ItemGridState(null);
            _items = nextState.Items ?? Array.Empty<ItemCardState>();

            RecalculateLayout(_viewportWidth, _viewportHeight);
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
                var itemIndex = index * _itemsPerRow + i;
                ApplySlotWidth(slot, i);
                slot.userData = hasItem ? itemIndex : -1;
                slot.style.visibility = hasItem ? Visibility.Visible : Visibility.Hidden;
                if (hasItem && itemCard != null)
                {
                    itemCard.SetWidth(_cardWidth);
                    itemCard.SetState(rowState.Items[i]);
                }

                OnBindSlot(slot, hasItem ? rowState.Items[i] : null, itemIndex, hasItem);
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
            _viewportWidth = evt.newRect.width;
            _viewportHeight = evt.newRect.height;
            if (RecalculateLayout(_viewportWidth, _viewportHeight))
            {
                RebuildRows();
            }
        }

        private void RebuildRows()
        {
            _rows.Clear();
            ListView.fixedItemHeight = _rowHeight;
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

            ListView.Rebuild();
            HideScrollbars();
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            HideScrollbars();
            schedule.Execute(HideScrollbars);
        }

        protected virtual ItemCard CreateItemCard()
        {
            return new ItemCard();
        }

        protected virtual void OnCreateSlot(VisualElement slot)
        {
        }

        protected virtual void OnBindSlot(VisualElement slot, ItemCardState item, int itemIndex, bool hasItem)
        {
        }

        private VisualElement CreateSlot(int index)
        {
            var slot = new VisualElement();
            slot.AddToClassList(RowSlotClassName);
            ApplySlotWidth(slot, index);
            OnCreateSlot(slot);
            var itemCard = CreateItemCard();
            itemCard.SetWidth(_cardWidth);
            slot.Add(itemCard);
            return slot;
        }

        private void ApplySlotWidth(VisualElement slot, int index)
        {
            slot.style.width = _cardWidth;
            slot.style.minWidth = _cardWidth;
            slot.style.maxWidth = _cardWidth;
            slot.style.marginRight = index + 1 < _itemsPerRow ? _columnGap : 0f;
        }

        private bool RecalculateLayout(float width, float height)
        {
            if (float.IsNaN(width) || width <= 0f)
            {
                return false;
            }

            var nextColumnGap = CalculateColumnGap(width, _itemsPerRow);
            var nextCardWidth = CalculateCardWidth(width, height, _itemsPerRow, nextColumnGap);
            var nextRowHeight = CalculateRowHeight(nextCardWidth, height);
            if (Mathf.Approximately(nextCardWidth, _cardWidth) &&
                Mathf.Approximately(nextColumnGap, _columnGap) &&
                nextRowHeight == _rowHeight)
            {
                return false;
            }

            _cardWidth = nextCardWidth;
            _columnGap = nextColumnGap;
            _rowHeight = nextRowHeight;
            return true;
        }

        internal static float CalculateColumnGap(float width, int itemsPerRow)
        {
            var safeItemsPerRow = Mathf.Max(1, itemsPerRow);
            if (!IsValidDimension(width) || safeItemsPerRow == 1)
            {
                return 0f;
            }

            var fittingGap =
                (width - ViewportSafetyMargin - (ItemCard.PreferredMinimumWidth * safeItemsPerRow)) /
                (safeItemsPerRow - 1);
            return Mathf.Clamp(fittingGap, 0f, PreferredColumnGap);
        }

        internal static float CalculateCardWidth(float width, float height, int itemsPerRow)
        {
            return CalculateCardWidth(width, height, itemsPerRow, CalculateColumnGap(width, itemsPerRow));
        }

        private static float CalculateCardWidth(float width, float height, int itemsPerRow, float columnGap)
        {
            var naturalWidth = CalculateNaturalCardWidth(width, itemsPerRow, columnGap);
            if (!IsValidDimension(height))
            {
                return naturalWidth;
            }

            var fallbackWidth = Mathf.Max(MinimumCardWidth, Mathf.Floor(height - NameHeight - RowVerticalPadding - ViewportSafetyMargin));
            return Mathf.Min(naturalWidth, fallbackWidth);
        }

        internal static int CalculateRowHeight(float cardWidth, float viewportHeight)
        {
            var naturalHeight = Mathf.Max(1, Mathf.CeilToInt(cardWidth + NameHeight + RowVerticalPadding));
            return IsValidDimension(viewportHeight)
                ? Mathf.Max(1, Mathf.Min(naturalHeight, Mathf.FloorToInt(viewportHeight - ViewportSafetyMargin)))
                : naturalHeight;
        }

        private static float CalculateNaturalCardWidth(float width, int itemsPerRow, float columnGap)
        {
            var safeItemsPerRow = Mathf.Max(1, itemsPerRow);
            var availableWidth = Mathf.Max(
                MinimumCardWidth,
                width - ViewportSafetyMargin - (columnGap * (safeItemsPerRow - 1)));
            return Mathf.Max(MinimumCardWidth, Mathf.Floor(availableWidth / safeItemsPerRow));
        }

        private static bool IsValidDimension(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private void HideScrollbars()
        {
            var scrollers = ListView.Query<Scroller>().ToList();
            for (var i = 0; i < scrollers.Count; i++)
            {
                scrollers[i].style.display = DisplayStyle.None;
                scrollers[i].style.visibility = Visibility.Hidden;
            }

            var scrollViews = ListView.Query<ScrollView>().ToList();
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
