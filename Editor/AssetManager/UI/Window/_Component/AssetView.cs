using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class AssetView : VisualElement {
        private readonly Button _backButton;
        private readonly ScrollView _breadcrumbContainer;
        private readonly Button _forwardButton;
        private readonly ListView _listView;
        private readonly Dictionary<string, Texture2D> _thumbnailCache = new();

        private AssetViewController _controller;
        private CancellationTokenSource _cts;
        private List<object> _items = new();
        private int _itemsPerRow = 5;
        private float _lastWidth;

        public AssetView() {
            style.flexGrow = 1;

            var toolbar = new Toolbar();

            _backButton = new Button(() => _controller?.GoBack()) {
                tooltip = "Back",
                style = { width = 24, paddingLeft = 0, paddingRight = 0 }
            };
            _backButton.Add(new Image {
                image = EditorGUIUtility.IconContent("d_Animation.PrevKey").image,
                scaleMode = ScaleMode.ScaleToFit
            });

            _forwardButton = new Button(() => _controller?.GoForward()) {
                tooltip = "Forward",
                style = { width = 24, paddingLeft = 0, paddingRight = 0 }
            };
            _forwardButton.Add(new Image {
                image = EditorGUIUtility.IconContent("d_Animation.NextKey").image,
                scaleMode = ScaleMode.ScaleToFit
            });

            toolbar.Add(_backButton);
            toolbar.Add(_forwardButton);

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

        private void OnDetach(DetachFromPanelEvent evt) {
            CancelCurrentTasks();
            ClearThumbnailCache();
        }

        private void CancelCurrentTasks() {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }

        private void ClearThumbnailCache() {
            foreach (var tex in _thumbnailCache.Values.Where(tex => tex != null)) Object.DestroyImmediate(tex);
            _thumbnailCache.Clear();
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
                            LoadFolderThumbnailAsync(card, folder.ID);
                            break;
                        case AssetMetadata asset:
                            card.SetData(asset.Name);
                            card.userData = asset;
                            card.SetThumbnail(null);
                            LoadThumbnailAsync(card, asset.ID);
                            break;
                    }
                }
                else {
                    card.style.display = DisplayStyle.None;
                }
            }
        }

        private async void LoadThumbnailAsync(AssetCard card, Ulid assetId) {
            _cts ??= new CancellationTokenSource();
            var token = _cts.Token;

            var idStr = assetId.ToString();
            if (_thumbnailCache.TryGetValue(idStr, out var cachedTex)) {
                if (cachedTex != null) card.SetThumbnail(cachedTex);
                return;
            }

            var thumbnailPath = AssetManagerContainer.Repository.GetThumbnailPath(assetId);
            if (!File.Exists(thumbnailPath)) return;

            try {
                var fileData = await Task.Run(() => File.ReadAllBytes(thumbnailPath), token);
                if (token.IsCancellationRequested) return;

                if (card.userData is not AssetMetadata currentMeta || currentMeta.ID != assetId) return;
                var tex = new Texture2D(2, 2);
                if (tex.LoadImage(fileData)) {
                    _thumbnailCache[idStr] = tex;
                    card.SetThumbnail(tex);
                }
                else {
                    Object.DestroyImmediate(tex);
                }
            }
            catch (Exception e) {
                if (e is not OperationCanceledException) Debug.LogWarning($"Failed to load thumbnail: {e.Message}");
            }
        }

        private async void LoadFolderThumbnailAsync(AssetCard card, Ulid folderId) {
            _cts ??= new CancellationTokenSource();
            var token = _cts.Token;

            var idStr = "folder_" + folderId;
            if (_thumbnailCache.TryGetValue(idStr, out var cachedTex)) {
                if (cachedTex != null) card.SetThumbnail(cachedTex);
                return;
            }

            var thumbnailPath = AssetManagerContainer.Repository.GetFolderThumbnailPath(folderId);
            if (!File.Exists(thumbnailPath)) return;

            try {
                var fileData = await Task.Run(() => File.ReadAllBytes(thumbnailPath), token);
                if (token.IsCancellationRequested) return;

                if (card.userData is not BaseFolder currentFolder || currentFolder.ID != folderId) return;
                var tex = new Texture2D(2, 2);
                if (tex.LoadImage(fileData)) {
                    _thumbnailCache[idStr] = tex;
                    card.SetThumbnail(tex);
                }
                else {
                    Object.DestroyImmediate(tex);
                }
            }
            catch (Exception e) {
                if (e is not OperationCanceledException)
                    Debug.LogWarning($"Failed to load folder thumbnail: {e.Message}");
            }
        }

        private void OnCardPointerDown(PointerDownEvent evt) {
            if (evt.button != 0) return;
            if (evt.currentTarget is not AssetCard card) return;

            switch (card.userData) {
                case BaseFolder folder:
                    if (evt.clickCount == 2)
                        _controller?.SelectFolder(folder.ID);
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
                _backButton.SetEnabled(false);
                _forwardButton.SetEnabled(false);
                return;
            }

            _backButton.SetEnabled(_controller.CanGoBack);
            _forwardButton.SetEnabled(_controller.CanGoForward);
        }

        private void UpdateBreadcrumbs(List<(string Name, Ulid Id)> path) {
            _breadcrumbContainer.Clear();
            if (path == null) return;

            for (var i = 0; i < path.Count; i++) {
                var (itemName, id) = path[i];
                var isLast = i == path.Count - 1;

                var btn = new Button(() => { _controller?.SelectFolder(id); }) {
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
            _items = items ?? new List<object>();
            _listView.itemsSource = GetRows();
            _listView.Rebuild();
        }

        public void ShowBoothItemFolders(List<BoothItemFolder> folders) {
            _items = new List<object>(folders ?? new List<BoothItemFolder>());
            _listView.itemsSource = GetRows();
            _listView.Rebuild();
        }

        private void OnControllerAssetSelected(AssetMetadata asset) {
        }
    }
}