using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.Core.Utility;

namespace _4OF.ee4v.AssetManager.UI.Presenter {
    public class AssetGridPresenter {
        private readonly AssetService _assetService;
        private readonly FolderService _folderService;
        private readonly Action<bool> _refreshUI;
        private readonly IAssetRepository _repository;

        public AssetGridPresenter(
            IAssetRepository repository,
            AssetService assetService,
            FolderService folderService,
            Action<bool> refreshUI
        ) {
            _repository = repository;
            _assetService = assetService;
            _folderService = folderService;
            _refreshUI = refreshUI;
        }

        public List<AssetMetadata> FindAssetsFromBoothItemFolder(List<Ulid> assetIds) {
            var libMetadata = _repository.GetLibraryMetadata();
            var assetsFromBoothItemFolder = (from assetId in assetIds
                select _repository.GetAsset(assetId)
                into asset
                where asset != null
                where asset.Folder != Ulid.Empty && libMetadata != null
                let currentFolder = libMetadata.GetFolder(asset.Folder)
                where currentFolder is BoothItemFolder
                select asset).ToList();

            return assetsFromBoothItemFolder;
        }

        public void PerformSetFolderForAssets(List<Ulid> assetIds, Ulid targetFolderId) {
            if (assetIds.Count > 0) _assetService.SetFolder(assetIds, targetFolderId);

            _refreshUI(true);
        }

        public void PerformItemsDroppedToFolder(List<Ulid> assetIds, List<Ulid> folderIds, Ulid targetFolderId) {
            if (assetIds.Count > 0) _assetService.SetFolder(assetIds, targetFolderId);

            if (folderIds.Count > 0)
                foreach (var folderId in folderIds)
                    _folderService.MoveFolder(folderId, targetFolderId);

            _refreshUI(true);
        }
    }
}