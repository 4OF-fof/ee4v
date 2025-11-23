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
using Object = UnityEngine.Object;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class AssetGrid : VisualElement {
        private readonly VisualElement _emptyStateContainer;
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

            _emptyStateContainer = new VisualElement {
                style = {
                    position = Position.Absolute,
                    top = 0,
                    left = 0,
                    right = 0,
                    bottom = 0,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                    display = DisplayStyle.None
                }
            };

            var emptyContent = new VisualElement {
                style = {
                    alignItems = Align.Center,
                    paddingTop = 40,
                    paddingBottom = 40
                }
            };

            var iconImage = new Image {
                image = (Texture2D)EditorGUIUtility.IconContent("ModelImporter Icon").image,
                style = {
                    width = 64,
                    height = 64,
                    marginBottom = 16,
                    opacity = 0.3f
                }
            };

            var messageLabel = new Label("アセットが見つかりません") {
                style = {
                    fontSize = 16,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    color = new Color(1f, 1f, 1f, 0.5f),
                    marginBottom = 8
                }
            };

            var hintLabel = new Label("検索条件を変更するか、新しいアセットを追加してください") {
                style = {
                    fontSize = 12,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    color = new Color(1f, 1f, 1f, 0.3f)
                }
            };

            emptyContent.Add(iconImage);
            emptyContent.Add(messageLabel);
            emptyContent.Add(hintLabel);
            _emptyStateContainer.Add(emptyContent);
            Add(_emptyStateContainer);

            RegisterCallback<DetachFromPanelEvent>(OnDetach);
            RegisterCallback<PointerUpEvent>(OnPointerUpAnywhere, TrickleDown.TrickleDown);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeaveAnywhere, TrickleDown.TrickleDown);
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
            UpdateEmptyState();
            Refresh();
        }

        private void UpdateEmptyState() {
            if (_flatItems.Count == 0) {
                _listView.style.display = DisplayStyle.None;
                _emptyStateContainer.style.display = DisplayStyle.Flex;
            }
            else {
                _listView.style.display = DisplayStyle.Flex;
                _emptyStateContainer.style.display = DisplayStyle.None;
            }
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
                            card.SetThumbnail(null, true);
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

                if (tex != null) card.SetThumbnail(tex, isFolder);
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

            var currentParent = parent;
            while (currentParent != null) {
                if (currentParent is AssetView assetView) {
                    assetView.Focus();
                    break;
                }

                currentParent = currentParent.parent;
            }

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

            if (!_selectedItems.Contains(targetItem)) {
                _selectedItems.Clear();
                _selectedItems.Add(targetItem);
                _lastSelectedReference = targetItem;
                _listView.RefreshItems();
                OnSelectionChange?.Invoke(_selectedItems.ToList());
            }

            _isDragging = true;

            var selectedAssets = _selectedItems.OfType<AssetMetadata>().ToList();
            if (selectedAssets.Count == 0) return;

            var assetIds = selectedAssets.Select(a => a.ID.ToString()).ToArray();
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData("AssetManagerAssets", assetIds);
            DragAndDrop.objectReferences = Array.Empty<Object>();
            DragAndDrop.StartDrag("Moving Assets");
        }

        private void OnCardPointerUp(PointerUpEvent evt) {
            _isDragging = false;
        }

        private void OnPointerUpAnywhere(PointerUpEvent evt) {
            _isDragging = false;
        }

        private void OnPointerLeaveAnywhere(PointerLeaveEvent evt) {
            _isDragging = false;
        }
    }
}