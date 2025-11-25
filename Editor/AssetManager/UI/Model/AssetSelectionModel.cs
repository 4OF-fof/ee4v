using System.Collections.Generic;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.Utility;

namespace _4OF.ee4v.AssetManager.UI.Model {
    public class AssetSelectionModel {
        private BindableProperty<List<object>> SelectedItems { get; } = new(new List<object>());
        public BindableProperty<AssetMetadata> SelectedAsset { get; } = new();
        public BindableProperty<Ulid> PreviewFolderId { get; } = new(Ulid.Empty);

        public void SetSelection(List<object> items) {
            SelectedItems.Value = items;
        }

        public void SetSelectedAsset(AssetMetadata asset) {
            SelectedAsset.Value = asset;
        }

        public void SetPreviewFolder(Ulid folderId) {
            PreviewFolderId.Value = folderId;
        }

        public void Clear() {
            SelectedItems.Value = new List<object>();
            SelectedAsset.Value = null;
            PreviewFolderId.Value = Ulid.Empty;
        }
    }
}