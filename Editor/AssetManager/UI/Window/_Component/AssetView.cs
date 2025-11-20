using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class AssetView : VisualElement {
        private readonly Label _backLabel;
        private readonly ScrollView _breadcrumbContainer;
        private readonly Label _forwardLabel;
        private readonly ListView _listView;
        private readonly ToolbarSearchField _searchField;

        private List<object> _allItems = new();

        private AssetViewController _controller;
        private CancellationTokenSource _cts;
        private List<object> _items = new();

        private int _itemsPerRow = 5;
        private float _lastWidth;
        private TextureService _textureService;

        public AssetView() {
            style.flexGrow = 1;

            var toolbar = new Toolbar();

            _backLabel = new Label("<") {
                tooltip = "Back",
                style = {
                    width = 24,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    paddingLeft = 0, paddingRight = 0,
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            _backLabel.RegisterCallback<PointerDownEvent>(_ =>
            {
                if (_controller is { CanGoBack: true }) _controller.GoBack();
            });
            RegisterHoverEvents(_backLabel);

            _forwardLabel = new Label(">") {
                tooltip = "Forward",
                style = {
                    width = 24,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    paddingLeft = 0, paddingRight = 0,
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            _forwardLabel.RegisterCallback<PointerDownEvent>(_ =>
            {
                if (_controller is { CanGoForward: true }) _controller.GoForward();
            });
            RegisterHoverEvents(_forwardLabel);

            toolbar.Add(_backLabel);
            toolbar.Add(_forwardLabel);

            _breadcrumbContainer = new ScrollView(ScrollViewMode.Horizontal) {
                style = {
                    flexGrow = 1,
                    marginLeft = 4,
                    marginRight = 4,
                    alignContent = Align.Center
                }
            };
            _breadcrumbContainer.contentContainer.style.flexDirection = FlexDirection.Row;
            _breadcrumbContainer.contentContainer.style.alignItems = Align.Center;
            toolbar.Add(_breadcrumbContainer);

            var slider = new SliderInt(2, 10) {
                value = _itemsPerRow,
                style = { minWidth = 100, maxWidth = 200 }
            };
            slider.RegisterValueChangedCallback(evt =>
            {
                _itemsPerRow = evt.newValue;
                if (_listView == null) return;
                UpdateItemHeight();
                _listView.itemsSource = GetRows();
                _listView.Rebuild();
            });
            toolbar.Add(slider);

            _searchField = new ToolbarSearchField {
                style = {
                    width = 200,
                    marginLeft = 4,
                    marginRight = 4,
                    alignSelf = Align.Center
                }
            };
            _searchField.RegisterValueChangedCallback(_ => ApplyFilter());
            toolbar.Add(_searchField);

            Add(toolbar);

            _listView = new ListView {
                style = { flexGrow = 1 },
                makeItem = MakeRow,
                bindItem = BindRow,
                itemsSource = GetRows(),
                fixedItemHeight = 220,
                selectionType = SelectionType.None
            };
            _listView.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            Add(_listView);

            RegisterCallback<DetachFromPanelEvent>(OnDetach);

            UpdateNavigationState();
        }

        private void RegisterHoverEvents(VisualElement element) {
            element.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (element.enabledSelf) element.style.backgroundColor = ColorPreset.MouseOverBackground;
            });
            element.RegisterCallback<MouseLeaveEvent>(_ => { element.style.backgroundColor = Color.clear; });
        }

        public void Initialize(TextureService textureService) {
            _textureService = textureService;
        }

        private void OnDetach(DetachFromPanelEvent evt) {
            CancelCurrentTasks();
        }

        private void CancelCurrentTasks() {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt) {
            var newWidth = evt.newRect.width;
            if (float.IsNaN(newWidth) || newWidth == 0 || Math.Abs(newWidth - _lastWidth) < 1) return;

            _lastWidth = newWidth;
            UpdateItemHeight();
            _listView.Rebuild();
        }

        private void UpdateItemHeight() {
            var containerWidth = _listView.resolvedStyle.width;
            if (float.IsNaN(containerWidth) || containerWidth == 0) return;

            containerWidth -= 20;
            var itemWidth = containerWidth / _itemsPerRow;
            var itemHeight = itemWidth + 50;

            _listView.fixedItemHeight = itemHeight;
        }

        private void ApplyFilter() {
            var searchText = _searchField.value;
            if (string.IsNullOrWhiteSpace(searchText))
                _items = new List<object>(_allItems);
            else
                _items = _allItems.Where(item =>
                {
                    var targetName = "";
                    var desc = "";

                    switch (item) {
                        case AssetMetadata asset:
                            targetName = asset.Name;
                            desc = asset.Description;
                            break;
                        case BaseFolder folder:
                            targetName = folder.Name;
                            desc = folder.Description;
                            break;
                    }

                    return (targetName != null &&
                            targetName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (desc != null && desc.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0);
                }).ToList();

            _listView.itemsSource = GetRows();
            _listView.Rebuild();
        }

        private List<List<object>> GetRows() {
            var rows = new List<List<object>>();
            var currentRow = new List<object>();

            foreach (var item in _items) {
                currentRow.Add(item);
                if (currentRow.Count < _itemsPerRow) continue;
                rows.Add(currentRow);
                currentRow = new List<object>();
            }

            if (currentRow.Count > 0) rows.Add(currentRow);

            return rows;
        }

        private VisualElement MakeRow() {
            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.NoWrap
                }
            };

            for (var i = 0; i < _itemsPerRow; i++) {
                var card = new AssetCard {
                    style = { flexShrink = 0 }
                };
                card.RegisterCallback<PointerDownEvent>(OnCardPointerDown);
                row.Add(card);
            }

            return row;
        }

        private void BindRow(VisualElement element, int index) {
            var rows = _listView.itemsSource as List<List<object>>;
            if (rows == null || index < 0 || index >= rows.Count) return;

            var rowData = rows[index];
            var containerWidth = _listView.resolvedStyle.width;
            if (float.IsNaN(containerWidth) || !(containerWidth > 0)) return;
            containerWidth -= 20;
            var itemWidth = containerWidth / _itemsPerRow;

            var elementChildCount = element.childCount;

            if (elementChildCount < _itemsPerRow)
                for (var i = elementChildCount; i < _itemsPerRow; i++) {
                    var card = new AssetCard {
                        style = { flexShrink = 0 }
                    };
                    card.RegisterCallback<PointerDownEvent>(OnCardPointerDown);
                    element.Add(card);
                }

            for (var i = 0; i < element.childCount; i++) {
                var card = element[i] as AssetCard;
                if (card == null) continue;

                if (i < rowData.Count) {
                    card.style.display = DisplayStyle.Flex;
                    card.style.width = itemWidth;

                    var item = rowData[i];
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

                if (tex != null)
                    card.SetThumbnail(tex);
            }
            catch {
                // ignore
            }
        }

        private void OnCardPointerDown(PointerDownEvent evt) {
            if (evt.button != 0) return;
            if (evt.currentTarget is not AssetCard card) return;

            switch (card.userData) {
                case BaseFolder folder:
                    if (evt.clickCount == 2)
                        _controller?.SetFolder(folder.ID);
                    else
                        _controller?.PreviewFolder(folder);
                    break;
                case AssetMetadata asset:
                    _controller?.SelectAsset(asset);
                    break;
            }

            evt.StopPropagation();
        }

        public void SetController(AssetViewController controller) {
            if (_controller != null) {
                _controller.ItemsChanged -= OnItemsChanged;
                _controller.AssetSelected -= OnControllerAssetSelected;
                _controller.OnHistoryChanged -= UpdateNavigationState;
                _controller.BreadcrumbsChanged -= UpdateBreadcrumbs;
            }

            _controller = controller;

            if (_controller == null) return;
            _controller.ItemsChanged += OnItemsChanged;
            _controller.AssetSelected += OnControllerAssetSelected;
            _controller.OnHistoryChanged += UpdateNavigationState;
            _controller.BreadcrumbsChanged += UpdateBreadcrumbs;

            _controller.Refresh();
        }

        private void UpdateNavigationState() {
            if (_controller == null) {
                _backLabel.SetEnabled(false);
                _forwardLabel.SetEnabled(false);
                return;
            }

            _backLabel.SetEnabled(_controller.CanGoBack);
            _forwardLabel.SetEnabled(_controller.CanGoForward);

            _backLabel.style.color = _controller.CanGoBack ? ColorPreset.TextColor : Color.gray;
            _forwardLabel.style.color = _controller.CanGoForward ? ColorPreset.TextColor : Color.gray;
        }

        private void UpdateBreadcrumbs(List<(string Name, Ulid Id)> path) {
            _breadcrumbContainer.Clear();
            if (path == null) return;

            for (var i = 0; i < path.Count; i++) {
                var (itemName, id) = path[i];
                var isLast = i == path.Count - 1;

                var btn = new Button(() => { _controller?.SetFolder(id); }) {
                    text = itemName,
                    style = {
                        backgroundColor = Color.clear,
                        borderTopWidth = 0, borderBottomWidth = 0, borderLeftWidth = 0, borderRightWidth = 0,
                        marginLeft = 0, marginRight = 0, paddingLeft = 2, paddingRight = 2,
                        color = ColorPreset.TextColor,
                        unityTextAlign = TextAnchor.MiddleLeft,
                        fontSize = 12
                    }
                };

                if (isLast) btn.style.unityFontStyleAndWeight = FontStyle.Bold;

                _breadcrumbContainer.Add(btn);

                if (isLast) continue;
                var separator = new Label(">") {
                    style = {
                        marginLeft = 2, marginRight = 2,
                        color = Color.gray,
                        unityTextAlign = TextAnchor.MiddleCenter
                    }
                };
                _breadcrumbContainer.Add(separator);
            }
        }

        private void OnItemsChanged(List<object> items) {
            _allItems = items ?? new List<object>();
            ApplyFilter();
        }

        public void ShowBoothItemFolders(List<BoothItemFolder> folders) {
            _allItems = new List<object>(folders ?? new List<BoothItemFolder>());
            ApplyFilter();
        }

        private void OnControllerAssetSelected(AssetMetadata asset) {
        }
    }
}