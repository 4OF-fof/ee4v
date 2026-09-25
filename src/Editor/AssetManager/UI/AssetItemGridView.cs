using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.Images;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetItemGridEntry
    {
        public AssetItemGridEntry(
            string id,
            string name,
            byte[] thumbnailData = null)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            ThumbnailData = thumbnailData ?? Array.Empty<byte>();
        }

        public string Id { get; }
        public string Name { get; }
        public byte[] ThumbnailData { get; }
    }

    internal sealed class AssetItemGridView : VisualElement, IDisposable
    {
        internal const int MinimumItemsPerRow = 1;
        internal const int MaximumItemsPerRow = 12;
        private const float PreferredGap = UiSpacingTokens.Xxl;
        private const float PreferredMinimumCardWidth = 48f;
        private const float MinimumCardWidth = UiSizeTokens.Size1;
        private const float CardTextHeight = 25f;
        private const float RowVerticalPadding = UiSpacingTokens.Xs;
        private const float ViewportSafetyMargin = UiSizeTokens.Size1;
        private const int DefaultRowHeight = 161;
        private const int OverscanRowCount = 1;

        private readonly ScrollView _scroll;
        private readonly List<VisualElement> _rowPool =
            new List<VisualElement>();
        private readonly CachedImageCache _imageCache;
        private readonly bool _ownsImageCache;
        private readonly Dictionary<string, int> _itemIndices =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> _selectedItemIds =
            new HashSet<string>(StringComparer.Ordinal);
        private IReadOnlyList<AssetItemGridEntry> _items =
            Array.Empty<AssetItemGridEntry>();
        private int _itemsPerRow = 6;
        private float _cardWidth = 132f;
        private float _columnGap = PreferredGap;
        private int _rowHeight = DefaultRowHeight;
        private int _recommendedMinimumItemsPerRow =
            MinimumItemsPerRow;
        private float _viewportWidth;
        private float _viewportHeight;
        private string _primarySelectedItemId = string.Empty;
        private string _selectionAnchorItemId = string.Empty;
        private int _boundFirstRow = -1;
        private int _boundVisibleRowCount = -1;
        private float _boundContentHeight = -1f;
        private IReadOnlyList<string> _notifiedVisibleItemIds =
            Array.Empty<string>();

        public AssetItemGridView()
            : this(new CachedImageCache(), true)
        {
        }

        public AssetItemGridView(CachedImageCache imageCache)
            : this(
                imageCache ?? throw new ArgumentNullException(
                    nameof(imageCache)),
                false)
        {
        }

        private AssetItemGridView(
            CachedImageCache imageCache,
            bool ownsImageCache)
        {
            _ownsImageCache = ownsImageCache;
            _imageCache = imageCache;
            AddToClassList("ee4v-asset-grid");
            focusable = true;

            _scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                verticalScrollerVisibility = ScrollerVisibility.Hidden,
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };
            _scroll.AddToClassList("ee4v-asset-grid__list");
            _scroll.contentContainer.style.position = Position.Relative;
            _scroll.RegisterCallback<GeometryChangedEvent>(
                OnGeometryChanged);
            _scroll.verticalScroller.valueChanged += _ =>
                UpdateVisibleRows();
            Add(_scroll);
            RegisterCallback<PointerDownEvent>(OnGridPointerDown);
            RegisterCallback<KeyDownEvent>(OnGridKeyDown);
        }

        public event Action<IReadOnlyList<string>, string> SelectionChanged;
        public event Action<string> ItemDoubleClicked;
        public event Action<IReadOnlyList<string>, VisualElement>
            ContextMenuRequested;
        public event Action<int> RecommendedMinimumItemsPerRowChanged;
        public event Action<IReadOnlyList<string>> VisibleItemsChanged;

        public int ItemsPerRow => _itemsPerRow;
        public int RecommendedMinimumItemsPerRow =>
            _recommendedMinimumItemsPerRow;
        internal IReadOnlyList<string> SelectedItemIds =>
            CreateSelectionSnapshot();
        internal string PrimarySelectedItemId => _primarySelectedItemId;

        public void SetItems(IReadOnlyList<AssetItemGridEntry> items)
        {
            _items = items ?? Array.Empty<AssetItemGridEntry>();
            _itemIndices.Clear();
            for (var i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                _itemIndices[item.Id] = i;
                if (item.ThumbnailData.Length > 0)
                {
                    _imageCache.SetSource(
                        item.Id,
                        item.ThumbnailData);
                }
            }

            InvalidateVisibleRows();
        }

        public void SetItemsPerRow(int value)
        {
            var nextValue = Mathf.Clamp(
                value,
                _recommendedMinimumItemsPerRow,
                MaximumItemsPerRow);
            if (_itemsPerRow == nextValue)
            {
                return;
            }

            _itemsPerRow = nextValue;
            RecalculateLayout(_viewportWidth, _viewportHeight);
            InvalidateVisibleRows();
        }

        public void SetSelectedItemId(string itemId)
        {
            SetSelectedItemIds(
                string.IsNullOrEmpty(itemId)
                    ? Array.Empty<string>()
                    : new[] { itemId },
                itemId);
        }

        public void SetSelectedItemIds(
            IEnumerable<string> itemIds,
            string primaryItemId = null)
        {
            var nextSelection = new HashSet<string>(
                StringComparer.Ordinal);
            if (itemIds != null)
            {
                foreach (var itemId in itemIds)
                {
                    if (!string.IsNullOrEmpty(itemId))
                    {
                        nextSelection.Add(itemId);
                    }
                }
            }

            var nextPrimary = !string.IsNullOrEmpty(primaryItemId) &&
                              nextSelection.Contains(primaryItemId)
                ? primaryItemId
                : GetPreferredSelectedItemId(nextSelection);
            if (_selectedItemIds.SetEquals(nextSelection) &&
                string.Equals(
                    _primarySelectedItemId,
                    nextPrimary,
                    StringComparison.Ordinal))
            {
                return;
            }

            _selectedItemIds.Clear();
            _selectedItemIds.UnionWith(nextSelection);
            _primarySelectedItemId = nextPrimary;
            _selectionAnchorItemId = nextPrimary;
            InvalidateVisibleRows();
        }

        public void SetThumbnail(string itemId, byte[] data)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return;
            }

            var thumbnail = data ?? Array.Empty<byte>();
            _imageCache.SetSource(itemId, thumbnail);
            if (IsItemVisible(itemId))
            {
                RefreshVisibleItem(itemId);
            }
        }

        public bool HasThumbnailResult(string itemId)
        {
            return !string.IsNullOrEmpty(itemId) &&
                _imageCache.HasSource(itemId);
        }

        public IReadOnlyList<string> GetVisibleItemIds()
        {
            if (_boundFirstRow < 0 || _boundVisibleRowCount <= 0)
            {
                return Array.Empty<string>();
            }

            var firstIndex = _boundFirstRow * _itemsPerRow;
            var lastIndex = Mathf.Min(
                _items.Count,
                (_boundFirstRow + _boundVisibleRowCount) * _itemsPerRow);
            var result = new string[lastIndex - firstIndex];
            for (var index = firstIndex; index < lastIndex; index++)
            {
                result[index - firstIndex] = _items[index].Id;
            }

            return result;
        }

        public void ClearThumbnails()
        {
            _imageCache.Clear();
            InvalidateVisibleRows();
        }

        public void Dispose()
        {
            for (var rowIndex = 0;
                 rowIndex < _rowPool.Count;
                 rowIndex++)
            {
                var row = _rowPool[rowIndex];
                for (var column = 0;
                     column < row.childCount;
                     column++)
                {
                    (row.ElementAt(column).ElementAt(0) as
                        AssetItemGridCard)?.Dispose();
                }
            }

            if (_ownsImageCache)
            {
                _imageCache.Dispose();
            }
        }

        private VisualElement CreateRow()
        {
            var row = new VisualElement();
            row.AddToClassList("ee4v-asset-grid__row");
            row.style.position = Position.Absolute;
            row.style.left = 0f;
            row.style.right = 0f;
            EnsureSlotCount(row);
            return row;
        }

        private void BindRow(
            VisualElement row,
            int rowIndex)
        {
            EnsureSlotCount(row);
            row.style.top = rowIndex * _rowHeight;
            row.style.height = _rowHeight;
            row.style.minHeight = _rowHeight;
            row.style.maxHeight = _rowHeight;
            for (var column = 0; column < _itemsPerRow; column++)
            {
                var slot = row.ElementAt(column);
                var card = slot.ElementAt(0) as AssetItemGridCard;
                var itemIndex = (rowIndex * _itemsPerRow) + column;
                var item = itemIndex < _items.Count
                    ? _items[itemIndex]
                    : null;
                slot.style.width = _cardWidth;
                slot.style.minWidth = _cardWidth;
                slot.style.maxWidth = _cardWidth;
                slot.style.marginRight = column + 1 < _itemsPerRow
                    ? _columnGap
                    : 0f;
                slot.style.visibility = item == null
                    ? Visibility.Hidden
                    : Visibility.Visible;
                if (item == null)
                {
                    card?.Dispose();
                    continue;
                }

                if (card == null)
                {
                    continue;
                }

                card.SetWidth(_cardWidth);
                card.SetState(
                    item,
                    _selectedItemIds.Contains(item.Id));
            }
        }

        private void EnsureSlotCount(VisualElement row)
        {
            while (row.childCount < _itemsPerRow)
            {
                var card = new AssetItemGridCard(_imageCache);
                card.Clicked += SelectItem;
                card.DoubleClicked += OpenItem;
                card.ContextClicked += OpenContextMenu;
                var slot = new VisualElement();
                slot.AddToClassList("ee4v-asset-grid__slot");
                slot.Add(card);
                row.Add(slot);
            }

            while (row.childCount > _itemsPerRow)
            {
                var slot = row.ElementAt(row.childCount - 1);
                (slot.ElementAt(0) as AssetItemGridCard)?.Dispose();
                row.RemoveAt(row.childCount - 1);
            }
        }

        internal void SelectItem(
            string itemId,
            bool toggle,
            bool range)
        {
            if (string.IsNullOrEmpty(itemId) ||
                !_itemIndices.ContainsKey(itemId))
            {
                return;
            }

            if (range)
            {
                SelectRange(itemId, toggle);
            }
            else if (toggle)
            {
                if (!_selectedItemIds.Add(itemId))
                {
                    _selectedItemIds.Remove(itemId);
                }
                _selectionAnchorItemId = itemId;
            }
            else
            {
                _selectedItemIds.Clear();
                _selectedItemIds.Add(itemId);
                _selectionAnchorItemId = itemId;
            }

            _primarySelectedItemId = _selectedItemIds.Contains(itemId)
                ? itemId
                : GetPreferredSelectedItemId(_selectedItemIds);
            InvalidateVisibleRows();
            SelectionChanged?.Invoke(
                CreateSelectionSnapshot(),
                _primarySelectedItemId);
        }

        internal void ClearSelection()
        {
            var hadSelection = _selectedItemIds.Count > 0 ||
                !string.IsNullOrEmpty(_primarySelectedItemId);
            _selectedItemIds.Clear();
            _primarySelectedItemId = string.Empty;
            _selectionAnchorItemId = string.Empty;
            if (!hadSelection)
            {
                return;
            }

            InvalidateVisibleRows();
            SelectionChanged?.Invoke(
                Array.Empty<string>(),
                string.Empty);
        }

        private void SelectRange(string itemId, bool additive)
        {
            if (!_itemIndices.TryGetValue(
                    _selectionAnchorItemId,
                    out var anchorIndex))
            {
                anchorIndex = _itemIndices[itemId];
                _selectionAnchorItemId = itemId;
            }

            if (!additive)
            {
                _selectedItemIds.Clear();
            }

            var itemIndex = _itemIndices[itemId];
            var start = Math.Min(anchorIndex, itemIndex);
            var end = Math.Max(anchorIndex, itemIndex);
            for (var index = start; index <= end; index++)
            {
                _selectedItemIds.Add(_items[index].Id);
            }
        }

        private string GetPreferredSelectedItemId(
            HashSet<string> selectedItemIds)
        {
            for (var index = _items.Count - 1; index >= 0; index--)
            {
                if (selectedItemIds.Contains(_items[index].Id))
                {
                    return _items[index].Id;
                }
            }

            var fallback = string.Empty;
            foreach (var itemId in selectedItemIds)
            {
                if (string.IsNullOrEmpty(fallback) ||
                    StringComparer.Ordinal.Compare(
                        itemId,
                        fallback) < 0)
                {
                    fallback = itemId;
                }
            }
            return fallback;
        }

        private IReadOnlyList<string> CreateSelectionSnapshot()
        {
            var selected = new List<string>(_selectedItemIds.Count);
            for (var index = 0; index < _items.Count; index++)
            {
                var itemId = _items[index].Id;
                if (_selectedItemIds.Contains(itemId))
                {
                    selected.Add(itemId);
                }
            }

            var hidden = new List<string>();
            foreach (var itemId in _selectedItemIds)
            {
                if (!_itemIndices.ContainsKey(itemId))
                {
                    hidden.Add(itemId);
                }
            }
            hidden.Sort(StringComparer.Ordinal);
            selected.AddRange(hidden);
            return selected;
        }

        private void OpenItem(string itemId)
        {
            ItemDoubleClicked?.Invoke(itemId);
        }

        private void OpenContextMenu(string itemId, VisualElement anchor)
        {
            if (!_selectedItemIds.Contains(itemId))
            {
                SelectItem(itemId, toggle: false, range: false);
            }

            ContextMenuRequested?.Invoke(
                CreateSelectionSnapshot(),
                anchor);
        }

        private void OnGridPointerDown(PointerDownEvent evt)
        {
            if (evt.button != (int)MouseButton.LeftMouse)
            {
                return;
            }

            Focus();
            ClearSelection();
            evt.StopPropagation();
        }

        internal void OnGridKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape)
            {
                return;
            }

            ClearSelection();
            evt.StopPropagation();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (Mathf.Approximately(_viewportWidth, evt.newRect.width) &&
                Mathf.Approximately(_viewportHeight, evt.newRect.height))
            {
                return;
            }

            _viewportWidth = evt.newRect.width;
            _viewportHeight = evt.newRect.height;
            UpdateRecommendedMinimumItemsPerRow(
                _viewportWidth,
                _viewportHeight);
            RecalculateLayout(_viewportWidth, _viewportHeight);
            InvalidateVisibleRows();
        }

        private bool RecalculateLayout(float width, float height)
        {
            if (!IsValidDimension(width))
            {
                return false;
            }

            var nextGap = CalculateColumnGap(width, _itemsPerRow);
            var nextWidth = CalculateCardWidth(
                width,
                height,
                _itemsPerRow,
                nextGap);
            var nextRowHeight = CalculateRowHeight(nextWidth, height);
            if (Mathf.Approximately(_cardWidth, nextWidth) &&
                Mathf.Approximately(_columnGap, nextGap) &&
                _rowHeight == nextRowHeight)
            {
                return false;
            }

            _cardWidth = nextWidth;
            _columnGap = nextGap;
            _rowHeight = nextRowHeight;
            return true;
        }

        private static float CalculateColumnGap(
            float width,
            int itemsPerRow)
        {
            var safeItemsPerRow = Mathf.Max(1, itemsPerRow);
            if (!IsValidDimension(width) || safeItemsPerRow == 1)
            {
                return 0f;
            }

            var fittingGap =
                (width - ViewportSafetyMargin -
                 (PreferredMinimumCardWidth * safeItemsPerRow)) /
                (safeItemsPerRow - 1);
            return Mathf.Clamp(fittingGap, 0f, PreferredGap);
        }

        private static float CalculateCardWidth(
            float width,
            float height,
            int itemsPerRow)
        {
            return CalculateCardWidth(
                width,
                height,
                itemsPerRow,
                CalculateColumnGap(width, itemsPerRow));
        }

        private static int CalculateRecommendedMinimumItemsPerRow(
            float width,
            float height)
        {
            if (!IsValidDimension(width) ||
                !IsValidDimension(height))
            {
                return MinimumItemsPerRow;
            }

            var maximumCardWidth = Mathf.Max(
                MinimumCardWidth,
                Mathf.Floor(
                    height -
                    CardTextHeight -
                    RowVerticalPadding -
                    ViewportSafetyMargin));
            return Mathf.Clamp(
                Mathf.CeilToInt(
                    (width - ViewportSafetyMargin + PreferredGap) /
                    (maximumCardWidth + PreferredGap)),
                MinimumItemsPerRow,
                MaximumItemsPerRow);
        }

        private static int CalculateRowHeight(
            float cardWidth,
            float viewportHeight)
        {
            var naturalHeight = Mathf.Max(
                1,
                Mathf.CeilToInt(
                    cardWidth +
                    CardTextHeight +
                    RowVerticalPadding));
            return IsValidDimension(viewportHeight)
                ? Mathf.Max(
                    1,
                    Mathf.Min(
                        naturalHeight,
                        Mathf.FloorToInt(
                            viewportHeight - ViewportSafetyMargin)))
                : naturalHeight;
        }

        private static float CalculateCardWidth(
            float width,
            float height,
            int itemsPerRow,
            float columnGap)
        {
            var safeItemsPerRow = Mathf.Max(1, itemsPerRow);
            var availableWidth = Mathf.Max(
                MinimumCardWidth,
                width - ViewportSafetyMargin -
                (columnGap * (safeItemsPerRow - 1)));
            var naturalWidth = Mathf.Max(
                MinimumCardWidth,
                Mathf.Floor(availableWidth / safeItemsPerRow));
            if (!IsValidDimension(height))
            {
                return naturalWidth;
            }

            var maximumWidth = Mathf.Max(
                MinimumCardWidth,
                Mathf.Floor(
                    height -
                    CardTextHeight -
                    RowVerticalPadding -
                    ViewportSafetyMargin));
            return Mathf.Min(naturalWidth, maximumWidth);
        }

        private static bool IsValidDimension(float value)
        {
            return !float.IsNaN(value) &&
                !float.IsInfinity(value) &&
                value > 0f;
        }

        private bool UpdateRecommendedMinimumItemsPerRow(
            float width,
            float height)
        {
            var nextMinimum = CalculateRecommendedMinimumItemsPerRow(
                width,
                height);
            if (_recommendedMinimumItemsPerRow == nextMinimum)
            {
                return false;
            }

            _recommendedMinimumItemsPerRow = nextMinimum;
            var previousItemsPerRow = _itemsPerRow;
            _itemsPerRow = Mathf.Clamp(
                _itemsPerRow,
                _recommendedMinimumItemsPerRow,
                MaximumItemsPerRow);
            RecommendedMinimumItemsPerRowChanged?.Invoke(nextMinimum);
            return previousItemsPerRow != _itemsPerRow;
        }

        private void UpdateVisibleRows()
        {
            var rowCount = Mathf.CeilToInt(
                (float)_items.Count / _itemsPerRow);
            var contentHeight = rowCount * _rowHeight;
            if (!Mathf.Approximately(
                    _boundContentHeight,
                    contentHeight))
            {
                _scroll.contentContainer.style.height = contentHeight;
                _scroll.contentContainer.style.minHeight = contentHeight;
                _boundContentHeight = contentHeight;
            }

            var viewportHeight = IsValidDimension(_viewportHeight)
                ? _viewportHeight
                : _rowHeight;
            var maximumOffset = Mathf.Max(
                0f,
                contentHeight - viewportHeight);
            var offset = Mathf.Clamp(
                _scroll.scrollOffset.y,
                0f,
                maximumOffset);
            if (!Mathf.Approximately(offset, _scroll.scrollOffset.y))
            {
                _scroll.scrollOffset = new Vector2(0f, offset);
            }

            var firstRow = rowCount == 0
                ? 0
                : Mathf.Clamp(
                    Mathf.FloorToInt(offset / _rowHeight),
                    0,
                    rowCount - 1);
            var visibleRowCount = rowCount == 0
                ? 0
                : Mathf.Min(
                    rowCount - firstRow,
                    Mathf.CeilToInt(viewportHeight / _rowHeight) +
                    OverscanRowCount);
            if (_boundFirstRow == firstRow &&
                _boundVisibleRowCount == visibleRowCount)
            {
                return;
            }

            EnsureRowPoolCount(visibleRowCount);
            var lastRow = firstRow + visibleRowCount;
            var retainedRows = new HashSet<int>();
            var reusableRows = new Queue<VisualElement>();
            for (var poolIndex = 0;
                 poolIndex < _rowPool.Count;
                 poolIndex++)
            {
                var row = _rowPool[poolIndex];
                if (row.userData is int boundRow &&
                    boundRow >= firstRow &&
                    boundRow < lastRow &&
                    retainedRows.Add(boundRow))
                {
                    row.style.display = DisplayStyle.Flex;
                }
                else
                {
                    row.userData = null;
                    reusableRows.Enqueue(row);
                }
            }

            for (var rowIndex = firstRow;
                 rowIndex < lastRow;
                 rowIndex++)
            {
                if (retainedRows.Contains(rowIndex))
                {
                    continue;
                }

                var row = reusableRows.Dequeue();
                row.userData = rowIndex;
                row.style.display = DisplayStyle.Flex;
                BindRow(row, rowIndex);
            }

            while (reusableRows.Count > 0)
            {
                var row = reusableRows.Dequeue();
                row.style.display = DisplayStyle.None;
                ClearRowImages(row);
            }

            _boundFirstRow = firstRow;
            _boundVisibleRowCount = visibleRowCount;
            var visibleItemIds = GetVisibleItemIds();
            if (_notifiedVisibleItemIds.Count != visibleItemIds.Count ||
                !_notifiedVisibleItemIds.SequenceEqual(visibleItemIds))
            {
                _notifiedVisibleItemIds = visibleItemIds;
                VisibleItemsChanged?.Invoke(visibleItemIds);
            }
        }

        private void InvalidateVisibleRows()
        {
            _boundFirstRow = -1;
            _boundVisibleRowCount = -1;
            for (var i = 0; i < _rowPool.Count; i++)
            {
                _rowPool[i].userData = null;
            }

            UpdateVisibleRows();
        }

        private bool IsItemVisible(string itemId)
        {
            if (!_itemIndices.TryGetValue(itemId, out var itemIndex) ||
                _boundFirstRow < 0 ||
                _boundVisibleRowCount <= 0)
            {
                return false;
            }

            var rowIndex = itemIndex / _itemsPerRow;
            return rowIndex >= _boundFirstRow &&
                rowIndex < _boundFirstRow + _boundVisibleRowCount;
        }

        private void RefreshVisibleItem(string itemId)
        {
            var itemIndex = _itemIndices[itemId];
            var rowIndex = itemIndex / _itemsPerRow;
            for (var poolIndex = 0; poolIndex < _rowPool.Count; poolIndex++)
            {
                var row = _rowPool[poolIndex];
                if (!(row.userData is int boundRow) ||
                    boundRow != rowIndex)
                {
                    continue;
                }

                var column = itemIndex % _itemsPerRow;
                var card = row.ElementAt(column).ElementAt(0) as
                    AssetItemGridCard;
                card?.SetState(
                    _items[itemIndex],
                    _selectedItemIds.Contains(itemId));
                return;
            }
        }

        private void EnsureRowPoolCount(int count)
        {
            while (_rowPool.Count < count)
            {
                var row = CreateRow();
                _rowPool.Add(row);
                _scroll.Add(row);
            }
        }

        private static void ClearRowImages(VisualElement row)
        {
            for (var column = 0; column < row.childCount; column++)
            {
                (row.ElementAt(column).ElementAt(0) as
                    AssetItemGridCard)?.Dispose();
            }
        }
    }

}
