using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.Utility;

namespace _4OF.ee4v.AssetManager.UI.Window {
    public class AssetViewController {
        private Func<AssetMetadata, bool> _filter = asset => !asset.IsDeleted;
        private Ulid _selectedFolderId = Ulid.Empty;

        public AssetViewController() {
            Refresh();
        }

        public event Action<List<AssetMetadata>> AssetsChanged;
        public event Action<List<BoothItemFolder>> BoothItemFoldersChanged;
        public event Action<AssetMetadata> AssetSelected;
        public event Action<List<BaseFolder>> FoldersChanged;

        public void SetFilter(Func<AssetMetadata, bool> filter) {
            _filter = filter ?? (asset => !asset.IsDeleted);
            _selectedFolderId = Ulid.Empty;
            Refresh();
        }

        public void SelectAsset(AssetMetadata asset) {
            AssetSelected?.Invoke(asset);
        }

        public void SelectFolder(Ulid folderId) {
            _selectedFolderId = folderId;
            Refresh();
        }

        public void ShowBoothItemFolders() {
            var boothItemFolders = new List<BoothItemFolder>();
            var rootFolders = AssetLibrary.Instance?.Libraries?.FolderList ?? new List<BaseFolder>();
            CollectBoothItemFolders(rootFolders, boothItemFolders);
            BoothItemFoldersChanged?.Invoke(boothItemFolders);
        }

        private void CollectBoothItemFolders(IEnumerable<BaseFolder> folders, List<BoothItemFolder> result) {
            foreach (var folder in folders) {
                switch (folder) {
                    case BoothItemFolder boothItemFolder:
                        result.Add(boothItemFolder);
                        break;
                    case Folder parentFolder:
                        CollectBoothItemFolders(parentFolder.Children, result);
                        break;
                }
            }
        }

        public void Refresh() {
            var assets = _selectedFolderId == Ulid.Empty
                ? AssetLibrary.Instance?.Assets ?? new List<AssetMetadata>()
                : AssetLibrary.Instance?.GetAssetsByFolder(_selectedFolderId) ?? new List<AssetMetadata>();

            var filtered = assets.Where(a => _filter(a)).ToList();
            AssetsChanged?.Invoke(filtered);

            var folders = AssetLibrary.Instance?.Libraries?.FolderList.Where(f => !(f is BoothItemFolder)).ToList() ??
                new List<BaseFolder>();
            FoldersChanged?.Invoke(folders);
        }
    }
}