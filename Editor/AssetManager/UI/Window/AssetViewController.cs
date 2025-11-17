using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.Utility;

namespace _4OF.ee4v.AssetManager.UI.Window {
    public class AssetViewController {
        private NavigationMode _mode = NavigationMode.All;

        public event Action<List<AssetMetadata>> AssetsChanged;
        public event Action<AssetMetadata> AssetSelected;

        public AssetViewController() {
            Refresh();
        }

        public void SetMode(NavigationMode mode) {
            _mode = mode;
            Refresh();
        }

        public void Refresh() {
            var assets = AssetLibrary.Instance?.Assets ?? new List<AssetMetadata>();
            var filtered = _mode switch {
                NavigationMode.All => assets.Where(a => !a.IsDeleted).ToList(),
                NavigationMode.Uncategorized => assets.Where(a => !a.IsDeleted && (a.Folder == Ulid.Empty && (a.Tags == null || a.Tags.Count == 0))).ToList(),
                NavigationMode.Trash => assets.Where(a => a.IsDeleted).ToList(),
                _ => assets.Where(a => !a.IsDeleted).ToList()
            };

            AssetsChanged?.Invoke(filtered);
        }

        public void SelectAsset(AssetMetadata asset) {
            AssetSelected?.Invoke(asset);
        }
    }
    
    public enum NavigationMode {
        All,
        Uncategorized,
        Trash
    }
}
