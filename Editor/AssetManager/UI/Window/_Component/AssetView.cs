using System.Collections.Generic;
using _4OF.ee4v.AssetManager.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class AssetView : VisualElement {
        private readonly ScrollView _container;
        private AssetViewController _controller;
        private List<AssetMetadata> _lastAssets = new();

        public AssetView() {
            style.flexGrow = 1;
            _container = new ScrollView();
            _container.contentContainer.style.flexDirection = FlexDirection.Row;
            _container.contentContainer.style.flexWrap = Wrap.Wrap;
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
                card.RegisterCallback<ClickEvent>(_ => _controller?.SelectFolder(folder.ID));
                _container.Add(card);
            }
        }

        private void OnControllerAssetSelected(AssetMetadata asset) {
            Debug.Log($"Selected asset: {asset.Name}");
        }

        private void RefreshView() {
            _container.Clear();
            foreach (var asset in _lastAssets) {
                var card = new AssetCard();
                card.SetData(asset.Name);
                card.RegisterCallback<ClickEvent>(_ => _controller?.SelectAsset(asset));
                _container.Add(card);
            }
        }
    }
}