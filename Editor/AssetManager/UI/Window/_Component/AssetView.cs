using System.Collections.Generic;
using System.Linq;
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
            Add(_container);
        }

        public void SetController(AssetViewController controller) {
            if (_controller != null) {
                _controller.AssetsChanged -= OnAssetsChanged;
                _controller.AssetSelected -= OnControllerAssetSelected;
            }

            _controller = controller;

            if (_controller == null) return;
            _controller.AssetsChanged += OnAssetsChanged;
            _controller.AssetSelected += OnControllerAssetSelected;
            _controller.Refresh();
        }

        private void OnAssetsChanged(List<AssetMetadata> assets) {
            _lastAssets = assets ?? new List<AssetMetadata>();
            RefreshView();
        }

        public void ShowBoothItemFolders(List<BoothItemFolder> folders) {
            _container.Clear();
            foreach (var button in folders.Select(folder => new Button(() => _controller?.SelectFolder(folder.ID)) {
                         text = folder.Name
                     }))
                _container.Add(button);
        }

        private void OnControllerAssetSelected(AssetMetadata asset) {
            Debug.Log($"Selected asset: {asset.Name}");
        }

        private void RefreshView() {
            _container.Clear();
            foreach (var button in _lastAssets.Select(asset => new Button(() => _controller?.SelectAsset(asset)) {
                         text = asset.Name
                     }))
                _container.Add(button);
        }
    }
}