using System;
using System.Collections.Generic;
using System.IO;
using _4OF.ee4v.AssetManager.Data;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class AssetView : VisualElement {
        private readonly ListView _listView;
        private AssetViewController _controller;

        private List<object> _items = new();
        private int _itemsPerRow = 5;
        private float _lastWidth;

        public AssetView() {
            style.flexGrow = 1;

            var toolbar = new Toolbar();
            var slider = new SliderInt("Items Per Row", 1) {
                value = _itemsPerRow,
                style = { minWidth = 200 }
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
                style = {
                    flexGrow = 1
                },
                makeItem = MakeRow,
                bindItem = BindRow,
                itemsSource = GetRows(),
                fixedItemHeight = 220,
                selectionType = SelectionType.None
            };
            _listView.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            Add(_listView);
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

        private static VisualElement MakeRow() {
            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.NoWrap
                }
            };
            return row;
        }

        private void BindRow(VisualElement element, int index) {
            element.Clear();

            if (_listView.itemsSource is not List<List<object>> rows || index < 0 || index >= rows.Count) return;

            var row = rows[index];
            var containerWidth = _listView.resolvedStyle.width;
            if (float.IsNaN(containerWidth) || !(containerWidth > 0)) return;
            containerWidth -= 20;
            var itemWidth = containerWidth / _itemsPerRow;

            foreach (var item in row) {
                var card = new AssetCard {
                    style = {
                        width = itemWidth,
                        flexShrink = 0
                    }
                };

                switch (item) {
                    case BoothItemFolder folder: {
                        card.SetData(folder.Name);
                        var tex = new Texture2D(2, 2);
                        tex.SetPixels(new[] { Color.red, Color.red, Color.red, Color.red });
                        tex.Apply();
                        card.SetThumbnail(tex);

                        card.userData = folder;
                        card.RegisterCallback<ClickEvent>(OnCardClick);
                        break;
                    }
                    case AssetMetadata asset: {
                        card.SetData(asset.Name);

                        var thumbnailPath = AssetManagerContainer.Repository.GetThumbnailPath(asset.ID);

                        if (File.Exists(thumbnailPath)) {
                            var fileData = File.ReadAllBytes(thumbnailPath);
                            var tex = new Texture2D(2, 2);
                            if (tex.LoadImage(fileData)) card.SetThumbnail(tex);
                        }

                        card.userData = asset;
                        card.RegisterCallback<ClickEvent>(OnCardClick);
                        break;
                    }
                }

                element.Add(card);
            }
        }

        private void OnCardClick(ClickEvent evt) {
            var card = evt.currentTarget as AssetCard;
            switch (card?.userData) {
                case null:
                    return;
                case BoothItemFolder folder:
                    _controller?.SelectFolder(folder.ID);
                    break;
                case AssetMetadata asset:
                    _controller?.SelectAsset(asset);
                    break;
            }
        }

        public void SetController(AssetViewController controller) {
            if (_controller != null) {
                _controller.AssetsChanged -= OnAssetsChanged;
                _controller.AssetSelected -= OnControllerAssetSelected;
                _controller.BoothItemFoldersChanged -= OnBoothItemFoldersChanged;
            }

            _controller = controller;

            if (_controller == null) return;
            _controller.AssetsChanged += OnAssetsChanged;
            _controller.AssetSelected += OnControllerAssetSelected;
            _controller.BoothItemFoldersChanged += OnBoothItemFoldersChanged;
            _controller.Refresh();
        }

        private void OnAssetsChanged(List<AssetMetadata> assets) {
            _items = new List<object>(assets ?? new List<AssetMetadata>());
            _listView.itemsSource = GetRows();
            _listView.Rebuild();
        }

        private void OnBoothItemFoldersChanged(List<BoothItemFolder> folders) {
            ShowBoothItemFolders(folders);
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