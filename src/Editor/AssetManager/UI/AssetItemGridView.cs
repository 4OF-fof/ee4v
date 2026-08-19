using System;
using System.Collections.Generic;
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
        public byte[] ThumbnailData { get; private set; }

        public void SetThumbnail(byte[] data)
        {
            ThumbnailData = data ?? Array.Empty<byte>();
        }
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
        private readonly Dictionary<string, ThumbnailTexture> _thumbnails =
            new Dictionary<string, ThumbnailTexture>(
                StringComparer.Ordinal);
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
        private string _selectedItemId = string.Empty;

        public AssetItemGridView()
        {
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
        }

        public event Action<string> ItemSelected;
        public event Action<int> RecommendedMinimumItemsPerRowChanged;

        public int ItemsPerRow => _itemsPerRow;
        public int RecommendedMinimumItemsPerRow =>
            _recommendedMinimumItemsPerRow;

        public void SetItems(IReadOnlyList<AssetItemGridEntry> items)
        {
            _items = items ?? Array.Empty<AssetItemGridEntry>();
            UpdateVisibleRows();
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
            UpdateVisibleRows();
        }

        public void SetSelectedItemId(string itemId)
        {
            var nextValue = itemId ?? string.Empty;
            if (string.Equals(
                    _selectedItemId,
                    nextValue,
                    StringComparison.Ordinal))
            {
                return;
            }

            _selectedItemId = nextValue;
            UpdateVisibleRows();
        }

        public void SetThumbnails(
            IReadOnlyDictionary<string, byte[]> thumbnails)
        {
            if (thumbnails == null || thumbnails.Count == 0)
            {
                return;
            }

            var changed = false;
            for (var i = 0; i < _items.Count; i++)
            {
                if (thumbnails.TryGetValue(
                        _items[i].Id,
                        out var data))
                {
                    _items[i].SetThumbnail(data);
                    changed = true;
                }
            }

            if (changed)
            {
                UpdateVisibleRows();
            }
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

            ClearThumbnailCache();
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
            int rowIndex,
            ISet<string> visibleItemIds)
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

                visibleItemIds.Add(item.Id);
                card.SetWidth(_cardWidth);
                card.SetState(
                    item,
                    string.Equals(
                        item.Id,
                        _selectedItemId,
                        StringComparison.Ordinal),
                    GetThumbnail(item));
            }
        }

        private void EnsureSlotCount(VisualElement row)
        {
            while (row.childCount < _itemsPerRow)
            {
                var card = new AssetItemGridCard();
                card.Clicked += SelectItem;
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

        private void SelectItem(string itemId)
        {
            SetSelectedItemId(itemId);
            ItemSelected?.Invoke(itemId);
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
            UpdateVisibleRows();
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
            _scroll.contentContainer.style.height = contentHeight;
            _scroll.contentContainer.style.minHeight = contentHeight;

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
            EnsureRowPoolCount(visibleRowCount);
            var visibleItemIds = new HashSet<string>(
                StringComparer.Ordinal);
            for (var poolIndex = 0;
                 poolIndex < _rowPool.Count;
                 poolIndex++)
            {
                var row = _rowPool[poolIndex];
                var visible = poolIndex < visibleRowCount;
                row.style.display = visible
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                if (visible)
                {
                    BindRow(
                        row,
                        firstRow + poolIndex,
                        visibleItemIds);
                }
                else
                {
                    ClearRowThumbnails(row);
                }
            }

            RemoveHiddenThumbnails(visibleItemIds);
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

        private Texture2D GetThumbnail(AssetItemGridEntry item)
        {
            if (_thumbnails.TryGetValue(item.Id, out var cached) &&
                ReferenceEquals(cached.Data, item.ThumbnailData))
            {
                return cached.Texture;
            }

            RemoveThumbnail(item.Id);
            Texture2D texture = null;
            if (item.ThumbnailData.Length > 0)
            {
                var candidate = new Texture2D(2, 2)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (candidate.LoadImage(item.ThumbnailData))
                {
                    texture = candidate;
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(candidate);
                }
            }

            _thumbnails[item.Id] = new ThumbnailTexture(
                item.ThumbnailData,
                texture);
            return texture;
        }

        private void RemoveHiddenThumbnails(ISet<string> visibleItemIds)
        {
            var hiddenItemIds = new List<string>();
            foreach (var itemId in _thumbnails.Keys)
            {
                if (!visibleItemIds.Contains(itemId))
                {
                    hiddenItemIds.Add(itemId);
                }
            }

            for (var index = 0;
                 index < hiddenItemIds.Count;
                 index++)
            {
                RemoveThumbnail(hiddenItemIds[index]);
            }
        }

        private static void ClearRowThumbnails(VisualElement row)
        {
            for (var column = 0; column < row.childCount; column++)
            {
                (row.ElementAt(column).ElementAt(0) as
                    AssetItemGridCard)?.Dispose();
            }
        }

        private void ClearThumbnailCache()
        {
            foreach (var thumbnail in _thumbnails.Values)
            {
                if (thumbnail.Texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        thumbnail.Texture);
                }
            }

            _thumbnails.Clear();
        }

        private void RemoveThumbnail(string itemId)
        {
            if (!_thumbnails.TryGetValue(itemId, out var thumbnail))
            {
                return;
            }

            if (thumbnail.Texture != null)
            {
                UnityEngine.Object.DestroyImmediate(thumbnail.Texture);
            }

            _thumbnails.Remove(itemId);
        }

        private sealed class ThumbnailTexture
        {
            public ThumbnailTexture(byte[] data, Texture2D texture)
            {
                Data = data;
                Texture = texture;
            }

            public byte[] Data { get; }
            public Texture2D Texture { get; }
        }
    }

    internal sealed class AssetItemGridCard : VisualElement, IDisposable
    {
        private readonly VisualElement _imageFrame;
        private readonly Image _image;
        private readonly VisualElement _placeholder;
        private readonly UiTextElement _name;
        private string _itemId = string.Empty;

        public AssetItemGridCard()
        {
            AddToClassList("ee4v-asset-grid-card");
            focusable = true;

            _imageFrame = new VisualElement();
            _imageFrame.AddToClassList("ee4v-asset-grid-card__image-frame");
            _image = new Image
            {
                scaleMode = ScaleMode.ScaleAndCrop,
                pickingMode = PickingMode.Ignore
            };
            _image.AddToClassList("ee4v-asset-grid-card__image");
            _placeholder = new VisualElement();
            _placeholder.AddToClassList(
                "ee4v-asset-grid-card__placeholder");
            _placeholder.pickingMode = PickingMode.Ignore;
            _imageFrame.Add(_placeholder);
            _imageFrame.Add(_image);

            _name = UiTextFactory.Create(
                string.Empty,
                "ee4v-asset-grid-card__name");
            _name.SetWhiteSpace(WhiteSpace.NoWrap);

            Add(_imageFrame);
            Add(_name);

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        public event Action<string> Clicked;

        public void SetState(
            AssetItemGridEntry state,
            bool selected,
            Texture2D thumbnail)
        {
            _itemId = state.Id;
            _name.SetText(state.Name);
            EnableInClassList(
                "ee4v-asset-grid-card--selected",
                selected);
            SetThumbnail(thumbnail);
        }

        public void SetWidth(float width)
        {
            style.width = width;
            style.minWidth = width;
            style.maxWidth = width;
            _imageFrame.style.width = width;
            _imageFrame.style.height = width;
            _imageFrame.style.minWidth = width;
            _imageFrame.style.minHeight = width;
            _imageFrame.style.maxWidth = width;
            _imageFrame.style.maxHeight = width;
        }

        public void Dispose()
        {
            SetThumbnail(null);
        }

        private void SetThumbnail(Texture2D texture)
        {
            if (ReferenceEquals(_image.image, texture))
            {
                return;
            }

            _image.image = texture;
            _image.style.display = texture == null
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            _placeholder.style.display = texture == null
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != (int)MouseButton.LeftMouse)
            {
                return;
            }

            Focus();
            Clicked?.Invoke(_itemId);
            evt.StopPropagation();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Return &&
                evt.keyCode != KeyCode.Space)
            {
                return;
            }

            Clicked?.Invoke(_itemId);
            evt.StopPropagation();
        }
    }
}
