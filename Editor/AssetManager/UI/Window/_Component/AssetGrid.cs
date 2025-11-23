using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class AssetGrid : VisualElement {
        private readonly ListView _listView;
        private readonly List<List<object>> _rows = new();
        private readonly HashSet<object> _selectedItems = new();
        private CancellationTokenSource _cts;

        private List<object> _flatItems = new();
        private bool _isDragging;
        private int _itemsPerRow;
        private object _lastSelectedReference;
        private float _lastWidth;

        private TextureService _textureService;

        public AssetGrid(int initialItemsPerRow) {
            _itemsPerRow = initialItemsPerRow;
            style.flexGrow = 1;
            style.backgroundColor = ColorPreset.DefaultBackground;

            _listView = new ListView {
                style = { flexGrow = 1 },
                makeItem = MakeRow,
                bindItem = BindRow,
                itemsSource = _rows,
                selectionType = SelectionType.None,
                fixedItemHeight = 220
            };

            _listView.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            Add(_listView);

            RegisterCallback<DetachFromPanelEvent>(OnDetach);
            RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.target != this && evt.target != _listView) return;
                ClearSelection();
            }, TrickleDown.TrickleDown);
        }

        public event Action<List<object>> OnSelectionChange;
        public event Action<BaseFolder> OnFolderDoubleClicked;

        public void Initialize(TextureService textureService) {
            _textureService = textureService;
        }

        public void SetItems(List<object> items) {
            _flatItems = items ?? new List<object>();

            _selectedItems.RemoveWhere(i => !_flatItems.Contains(i));
            if (_lastSelectedReference != null && !_flatItems.Contains(_lastSelectedReference))
                _lastSelectedReference = null;

            RebuildRows();
            Refresh();
        }

        public void SetItemsPerRow(int count) {
            if (_itemsPerRow == count) return;
            _itemsPerRow = count;
            UpdateItemHeight();
            RebuildRows();
            Refresh();
        }

        private void Refresh() {
            _listView.RefreshItems();
        }

        public void ClearSelection() {
            _selectedItems.Clear();
            _lastSelectedReference = null;
            _listView.RefreshItems();
            OnSelectionChange?.Invoke(new List<object>());
        }

        private void RebuildRows() {
            _rows.Clear();
            var currentRow = new List<object>();

            foreach (var item in _flatItems) {
                currentRow.Add(item);
                if (currentRow.Count < _itemsPerRow) continue;
                _rows.Add(currentRow);
                currentRow = new List<object>();
            }

            if (currentRow.Count > 0) _rows.Add(currentRow);
            _listView.itemsSource = _rows;
            _listView.Rebuild();
        }

        private void OnDetach(DetachFromPanelEvent evt) {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt) {
            var newWidth = evt.newRect.width;
            if (float.IsNaN(newWidth) || newWidth == 0 || Math.Abs(newWidth - _lastWidth) < 1) return;
            _lastWidth = newWidth;
            UpdateItemHeight();
            _listView.RefreshItems();
        }

        private void UpdateItemHeight() {
            var containerWidth = _listView.resolvedStyle.width;
            if (float.IsNaN(containerWidth) || containerWidth == 0) return;
            containerWidth -= 20;
            var itemWidth = containerWidth / _itemsPerRow;
            var itemHeight = itemWidth + 50;
            _listView.fixedItemHeight = itemHeight;
        }

        private VisualElement MakeRow() {
            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.NoWrap,
                    flexGrow = 1,
                    backgroundColor = ColorPreset.DefaultBackground
                }
            };

            for (var i = 0; i < _itemsPerRow; i++) {
                var card = new AssetCard { style = { flexShrink = 0 } };
                card.RegisterCallback<PointerDownEvent>(OnCardPointerDown);
                card.RegisterCallback<PointerMoveEvent>(OnCardPointerMove);
                card.RegisterCallback<PointerUpEvent>(OnCardPointerUp);
                row.Add(card);
            }

            return row;
        }

        private void BindRow(VisualElement element, int index) {
            if (index < 0 || index >= _rows.Count) return;
            var rowData = _rows[index];

            var containerWidth = _listView.resolvedStyle.width;
            if (float.IsNaN(containerWidth) || !(containerWidth > 0)) return;
            containerWidth -= 20;
            var itemWidth = containerWidth / _itemsPerRow;

            var elementChildCount = element.childCount;
            if (elementChildCount < _itemsPerRow)
                for (var i = elementChildCount; i < _itemsPerRow; i++) {
                    var card = new AssetCard { style = { flexShrink = 0 } };
                    card.RegisterCallback<PointerDownEvent>(OnCardPointerDown);
                    element.Add(card);
                }

            for (var i = 0; i < element.childCount; i++) {
                var card = element[i] as AssetCard;
                if (card == null) continue;

                card.UnregisterCallback<PointerMoveEvent>(OnCardPointerMove);
                card.UnregisterCallback<PointerUpEvent>(OnCardPointerUp);
                card.RegisterCallback<PointerMoveEvent>(OnCardPointerMove);
                card.RegisterCallback<PointerUpEvent>(OnCardPointerUp);

                if (i < rowData.Count) {
                    card.style.display = DisplayStyle.Flex;
                    card.style.width = itemWidth;

                    var item = rowData[i];
                    card.SetSelected(_selectedItems.Contains(item));

                    switch (item) {
                        case BaseFolder folder:
                            card.SetData(folder.Name);
                            card.userData = folder;
                            card.SetThumbnail(null);
                            LoadImageAsync(card, folder.ID, true);
                            break;
                        case AssetMetadata asset:
                            card.SetData(asset.Name);
                            card.userData = asset;
                            card.SetThumbnail(null);
                            LoadImageAsync(card, asset.ID, false);
                            break;
                    }
                }
                else {
                    card.style.display = DisplayStyle.None;
                }
            }
        }

        private async void LoadImageAsync(AssetCard card, Ulid id, bool isFolder) {
            if (_textureService == null) return;
            _cts ??= new CancellationTokenSource();
            var token = _cts.Token;

            try {
                Texture2D tex;
                if (isFolder)
                    tex = await _textureService.GetFolderThumbnailAsync(id);
                else
                    tex = await _textureService.GetAssetThumbnailAsync(id);

                if (token.IsCancellationRequested) return;

                switch (card.userData) {
                    case AssetMetadata meta when meta.ID != id:
                    case BaseFolder folder when folder.ID != id:
                        return;
                }

                if (tex != null) card.SetThumbnail(tex);
            }
            catch {
                /* ignore */
            }
        }

        private void OnCardPointerDown(PointerDownEvent evt) {
            if (evt.button != 0) return;
            if (evt.currentTarget is not AssetCard card) return;
            var targetItem = card.userData;
            if (targetItem == null) return;

            if (evt.clickCount == 2) {
                if (targetItem is BaseFolder folder)
                    OnFolderDoubleClicked?.Invoke(folder);
                evt.StopPropagation();
                return;
            }

            if (evt.ctrlKey || evt.commandKey) {
                if (!_selectedItems.Add(targetItem))
                    _selectedItems.Remove(targetItem);
                else
                    _lastSelectedReference = targetItem;
            }
            else if (evt.shiftKey && _lastSelectedReference != null && _flatItems.Contains(_lastSelectedReference)) {
                var startIndex = _flatItems.IndexOf(_lastSelectedReference);
                var endIndex = _flatItems.IndexOf(targetItem);
                if (startIndex != -1 && endIndex != -1) {
                    var min = Mathf.Min(startIndex, endIndex);
                    var max = Mathf.Max(startIndex, endIndex);
                    _selectedItems.Clear();
                    for (var i = min; i <= max; i++) _selectedItems.Add(_flatItems[i]);
                }
                else {
                    _selectedItems.Clear();
                    _selectedItems.Add(targetItem);
                    _lastSelectedReference = targetItem;
                }
            }
            else {
                _selectedItems.Clear();
                _selectedItems.Add(targetItem);
                _lastSelectedReference = targetItem;
            }

            _listView.RefreshItems();
            OnSelectionChange?.Invoke(_selectedItems.ToList());
            evt.StopPropagation();
        }

        private void OnCardPointerMove(PointerMoveEvent evt) {
            if (evt.pressedButtons != 1 || _isDragging) return;
            if (evt.currentTarget is not AssetCard card) return;
            var targetItem = card.userData;
            if (targetItem is not AssetMetadata) return;

            _isDragging = true;

            var selectedAssets = _selectedItems.OfType<AssetMetadata>().ToList();
            if (selectedAssets.Count == 0) return;

            var assetIds = selectedAssets.Select(a => a.ID.ToString()).ToArray();
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData("AssetManagerAssets", assetIds);
            DragAndDrop.StartDrag("Moving Assets");
            evt.StopPropagation();
        }

        private void OnCardPointerUp(PointerUpEvent evt) {
            _isDragging = false;
        }
    }
}