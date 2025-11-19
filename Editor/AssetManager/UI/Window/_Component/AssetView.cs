using System.Collections.Generic;
using System.IO;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.Data;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class AssetView : VisualElement {
        private readonly ScrollView _container;
        private AssetViewController _controller;
        private int _itemsPerRow = 5;
        private List<AssetMetadata> _lastAssets = new();

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
                UpdateGrid();
            });
            toolbar.Add(slider);
            Add(toolbar);

            _container = new ScrollView();
            _container.contentContainer.style.flexDirection = FlexDirection.Row;
            _container.contentContainer.style.flexWrap = Wrap.Wrap;
            _container.RegisterCallback<GeometryChangedEvent>(evt => UpdateGrid());
            Add(_container);
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
            _lastAssets = assets ?? new List<AssetMetadata>();
            RefreshView();
        }

        private void OnBoothItemFoldersChanged(List<BoothItemFolder> folders) {
            ShowBoothItemFolders(folders);
        }

        public void ShowBoothItemFolders(List<BoothItemFolder> folders) {
            _container.Clear();
            foreach (var folder in folders) {
                var card = new AssetCard();
                card.SetData(folder.Name);
                var tex = new Texture2D(2, 2);
                tex.SetPixels(new[] { Color.red, Color.red, Color.red, Color.red });
                tex.Apply();
                card.SetThumbnail(tex);
                card.RegisterCallback<ClickEvent>(_ => _controller?.SelectFolder(folder.ID));
                _container.Add(card);
            }
            UpdateGrid();
        }

        private void OnControllerAssetSelected(AssetMetadata asset) {
            Debug.Log($"Selected asset: {asset.Name}");
        }

        private void RefreshView() {
            _container.Clear();
            foreach (var asset in _lastAssets) {
                var card = new AssetCard();
                card.SetData(asset.Name);

                var thumbnailPath = Path.Combine(EditorPrefsManager.ContentFolderPath, "AssetManager", "Assets",
                    asset.ID.ToString(), "thumbnail.png");
                if (File.Exists(thumbnailPath)) {
                    var fileData = File.ReadAllBytes(thumbnailPath);
                    var tex = new Texture2D(2, 2);
                    if (tex.LoadImage(fileData)) card.SetThumbnail(tex);
                }

                card.RegisterCallback<ClickEvent>(_ => _controller?.SelectAsset(asset));
                _container.Add(card);
            }

            UpdateGrid();
        }

        private void UpdateGrid() {
            if (float.IsNaN(_container.resolvedStyle.width) || _container.resolvedStyle.width == 0) {
                schedule.Execute(UpdateGrid);
                return;
            }

            var containerWidth = _container.resolvedStyle.width;
            containerWidth -= 20;

            var itemWidth = containerWidth / _itemsPerRow;
            var itemHeight = itemWidth * 1.33f; // 3:4 (width:height)

            foreach (var child in _container.Children()) {
                child.style.width = itemWidth;
                child.style.height = itemHeight;
            }
        }
    }
}