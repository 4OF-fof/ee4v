using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;

namespace _4OF.ee4v.AssetManager.UI.Window {
    public class AssetViewController {
        private Func<AssetMetadata, bool> _filter = asset => !asset.IsDeleted;

        public AssetViewController() {
            Refresh();
        }

        public event Action<List<AssetMetadata>> AssetsChanged;
        public event Action<AssetMetadata> AssetSelected;

        public void SetFilter(Func<AssetMetadata, bool> filter) {
            _filter = filter ?? (asset => !asset.IsDeleted);
            Refresh();
        }

        public void SelectAsset(AssetMetadata asset) {
            AssetSelected?.Invoke(asset);
        }

        public void Refresh() {
            var assets = AssetLibrary.Instance?.Assets ?? new List<AssetMetadata>();
            var filtered = assets.Where(a => _filter(a)).ToList();

            AssetsChanged?.Invoke(filtered);
        }
    }
}